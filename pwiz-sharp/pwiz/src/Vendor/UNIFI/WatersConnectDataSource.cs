using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Google.Protobuf;
using Pwiz.Vendor.UNIFI.Protobuf;

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// Live <see cref="IUnifiDataSource"/> backed by a waters_connect HTTP endpoint. Sibling to
/// <see cref="HttpUnifiDataSource"/> for the v2 API path. C# port of cpp <c>WatersConnectImpl</c>
/// (WatersConnectData.ipp).
/// </summary>
/// <remarks>
/// <para>waters_connect addresses spectra by (channelId, scanIndexInChannel) rather than a
/// flat global index — discovery walks <c>/waters_connect/v2.0/injection-data/{id}/channels</c>
/// and builds a flat spectrum index that maps each logical index back to its channel.
/// <see cref="IUnifiDataSource.GetChannelAndScanIndex"/> returns those pairs so
/// <see cref="SpectrumList_UNIFI"/> can produce the cpp-equivalent
/// <c>channelIndex=C scanIndex=N</c> spectrum ids.</para>
///
/// <para><b>Current scope:</b> channel discovery + spectrum chunk fetch. TIC + MRM
/// chromatograms (cpp <c>getTicChromatograms</c> / <c>getMrmChromatograms</c>) are deferred —
/// <see cref="ChromatogramInfo"/> stays empty until that follow-on slice lands.</para>
/// </remarks>
public sealed class WatersConnectDataSource : IUnifiDataSource, IDisposable
{
    // Channel name like "1: TOF MS..." → leading channel number for index assignment.
    private static readonly Regex ChannelNumberRegex = new(@"^(\d+)\:.*", RegexOptions.Compiled);
    // cpp WatersConnectData.ipp:175-180: ideal chunk readahead is 2 on x64.
    private const int DefaultChunkSize = 20;
    private const int MaxCachedChunks = 16;

    private readonly AbstractWatersHttpClient _client;
    private readonly bool _ownsClient;
    private readonly bool _combineIonMobilitySpectra;
    private readonly List<ChannelInfo> _channels = new();
    private readonly Dictionary<string, ChannelInfo> _channelById = new();
    private readonly List<SpectrumKey> _spectrumIndex = new();
    // Per-channel TIC time/intensity arrays, keyed by parentChannelId. Drives spectrum index
    // RT-sort + powers GetChromatogram for the synthetic per-channel TIC entries.
    private readonly Dictionary<string, (double[] Times, double[] Intensities, string Title)> _channelTics = new();
    private readonly List<UnifiChromatogramInfo> _chromatogramInfoList = new();
    // Parallel arrays for chromatogram data — index matches _chromatogramInfoList.
    private readonly List<(double[] Times, double[] Intensities)> _chromatogramData = new();
    private readonly ConcurrentDictionary<int, IReadOnlyList<MzSpectrumItemDtoV2>> _profileChunkCache = new();
    private readonly ConcurrentDictionary<int, IReadOnlyList<MzSpectrumItemDtoV2>> _centroidChunkCache = new();
    private readonly LinkedList<int> _profileLru = new();
    private readonly LinkedList<int> _centroidLru = new();
    private readonly object _lruLock = new();
    private bool _disposed;

    /// <summary>Constructs and runs channel discovery.</summary>
    public WatersConnectDataSource(AbstractWatersHttpClient client, bool combineIonMobilitySpectra, bool ownsClient = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (client.Config.Api != RemoteApiType.WatersConnect)
            throw new ArgumentException("WatersConnectDataSource requires a waters_connect-configured client", nameof(client));
        _client = client;
        _ownsClient = ownsClient;
        _combineIonMobilitySpectra = combineIonMobilitySpectra;

        FetchChannels();
        // cpp WatersConnectData.ipp:878-879 calls these in order before building the spectrum
        // index — TIC retention times drive the RT-sorted index and SRM transitions populate
        // the chromatogram list.
        FetchTicChromatograms();
        FetchMrmChromatograms();
    }

    /// <inheritdoc/>
    public RemoteApiType RemoteApi => RemoteApiType.WatersConnect;

    /// <inheritdoc/>
    public bool HasIonMobilityData { get; private set; }

    /// <inheritdoc/>
    public int NumberOfSpectra { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<UnifiChromatogramInfo> ChromatogramInfo => _chromatogramInfoList;

    /// <summary>Read-only view of the discovered channels (each MS channel is the
    /// waters_connect equivalent of a UNIFI scan function).</summary>
    public IReadOnlyList<ChannelInfo> Channels => _channels;

    /// <inheritdoc/>
    public int GetMsLevel(int index)
    {
        // cpp WatersConnectImpl::getMsLevel (WatersConnectData.ipp:222-245) maps the channel's
        // scanning method to an MS level. We mirror that exactly.
        var key = _spectrumIndex[index];
        return key.Channel.ScanningMethod switch
        {
            ScanningMethod.MS => 1,
            ScanningMethod.SIR => 1,
            ScanningMethod.PRECURSOR => -1, // cpp returns -1; consumer interprets as ms1-or-unknown
            ScanningMethod.PRODUCT or ScanningMethod.MSMS or ScanningMethod.DDA
              or ScanningMethod.MRM or ScanningMethod.NL or ScanningMethod.NG
              or ScanningMethod.PICS => 2,
            _ => 0,
        };
    }

    /// <inheritdoc/>
    public (int ChannelIndex, int ScanIndexInChannel) GetChannelAndScanIndex(int index)
    {
        var key = _spectrumIndex[index];
        return (key.Channel.Index, key.ScanIndexInChannel);
    }

    /// <inheritdoc/>
    public void GetSpectrum(int index, UnifiSpectrum spectrum, bool getBinaryData, bool doCentroid)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        if (index < 0 || index >= _spectrumIndex.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var key = _spectrumIndex[index];
        int taskIndex = (key.ScanIndexInChannel / DefaultChunkSize) * DefaultChunkSize;
        int localIndex = key.ScanIndexInChannel - taskIndex;

        var chunk = EnsureChunkLoaded(key.Channel, taskIndex, doCentroid);
        if (localIndex >= chunk.Count)
            throw new InvalidOperationException(
                $"chunk at task {taskIndex} (channel {key.Channel.Id}) returned {chunk.Count} spectra; expected at least {localIndex + 1}");

        var item = chunk[localIndex];
        FillUnifiSpectrum(item, key.Channel, spectrum, getBinaryData, doCentroid);
    }

    /// <inheritdoc/>
    public void GetChromatogram(int index, UnifiChromatogram chromatogram, bool getBinaryData)
    {
        ArgumentNullException.ThrowIfNull(chromatogram);
        if (index < 0 || index >= _chromatogramInfoList.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var info = _chromatogramInfoList[index];
        chromatogram.Index = info.Index;
        chromatogram.Type = info.Type;
        chromatogram.Name = info.Name;
        chromatogram.AltId = info.AltId;
        chromatogram.Q1 = info.Q1;
        chromatogram.Q3 = info.Q3;
        chromatogram.Polarity = info.Polarity;
        chromatogram.AcquiredTimeRange = info.AcquiredTimeRange;

        var (times, intensities) = _chromatogramData[index];
        chromatogram.ArrayLength = times.Length;
        if (getBinaryData)
        {
            chromatogram.TimeArray = times;
            chromatogram.IntensityArray = intensities;
        }
    }

    private void FetchChannels()
    {
        // cpp WatersConnectImpl getChannels (WatersConnectData.ipp:765-866).
        // /channels endpoint returns a JSON ARRAY (not a {value:[...]} envelope like UNIFI).
        string url = $"{BaseUrl()}/waters_connect/v2.0/injection-data/{_client.Config.InjectionId}/channels";
        using var response = _client.SendAuthenticatedGetSync(url);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"failed to fetch channels at {url}: {(int)response.StatusCode} {response.ReasonPhrase}");

        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("waters_connect channels response was not a JSON array");

        foreach (var ch in doc.RootElement.EnumerateArray())
        {
            string shape = ch.GetPropertyOrEmpty("channelDataShape");
            if (shape != "Spectra" && shape != "Spectrum") continue; // skip non-MS channels

            string id = ch.GetPropertyOrEmpty("id");
            string name = ch.GetPropertyOrEmpty("name");
            string dataType = ch.GetPropertyOrEmpty("channelDataType");

            int channelIndex = -1;
            var match = ChannelNumberRegex.Match(name);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                channelIndex = n - 1; // names start at 1; index is 0-based

            var info = new ChannelInfo
            {
                Index = channelIndex,
                Id = id,
                Name = name,
                ChannelDataType = ParseDataType(dataType),
                ChannelDataShape = ParseDataShape(shape),
            };

            if (info.ChannelDataType == ChannelDataType.IMSScanning
                || info.ChannelDataType == ChannelDataType.MSScanning)
            {
                info.ScanningMethod = ParseScanningMethod(ch.GetPropertyAtPath("technique.basicMsProperties.scanningMethod")?.GetString());
                info.IonizationType = ch.GetPropertyAtPath("technique.basicMsProperties.ionisationType")?.GetString() ?? string.Empty;
                info.LowMass = ch.GetPropertyAsDouble("technique.basicMsProperties.acquiredMOverZRange.start");
                info.HighMass = ch.GetPropertyAsDouble("technique.basicMsProperties.acquiredMOverZRange.end");
                info.NumSpectra = (int)ch.GetPropertyAsDouble("scanCount");
                info.IsIonMobilityData = info.ChannelDataType == ChannelDataType.IMSScanning;

                string polarity = ch.GetPropertyAtPath("technique.basicMsProperties.ionisationMode")?.GetString() ?? string.Empty;
                info.Polarity = polarity switch
                {
                    "Positive" => UnifiPolarity.Positive,
                    "Negative" => UnifiPolarity.Negative,
                    _ => UnifiPolarity.Unknown,
                };

                string energyLevel = ch.GetPropertyAtPath("technique.fragmentationProperties.energyLevelType")?.GetString() ?? string.Empty;
                info.EnergyLevel = energyLevel switch
                {
                    "Low" => UnifiEnergyLevel.Low,
                    "High" => UnifiEnergyLevel.High,
                    _ => UnifiEnergyLevel.Unknown,
                };
            }

            HasIonMobilityData |= info.IsIonMobilityData;
            _channels.Add(info);
            _channelById[id] = info;
        }

        // Build the flat spectrum index. cpp WatersConnectData.ipp:1150-1162 builds the index
        // tagged with each scan's retention time (from the per-channel TIC chromatogram), then
        // sorts the whole list by RT. The reference mzMLs reflect that interleaved order.
        // The TIC fetch happens after this method returns, so the actual sort is done in
        // FetchTicChromatograms once per-channel time arrays are known.
    }

    private IReadOnlyList<MzSpectrumItemDtoV2> EnsureChunkLoaded(ChannelInfo channel, int taskIndex, bool doCentroid)
    {
        // Cache key: (channel.Id, taskIndex). To keep the LRU per-mode (matching cpp's
        // separate _profileCache + _centroidCache), use two parallel caches keyed by an int
        // hash of (channelId, taskIndex). Keep it simple — combine into a hash.
        var cache = doCentroid ? _centroidChunkCache : _profileChunkCache;
        var lru = doCentroid ? _centroidLru : _profileLru;
        int key = HashChunkKey(channel.Id, taskIndex);

        if (cache.TryGetValue(key, out var cached))
        {
            lock (_lruLock)
            {
                lru.Remove(key);
                lru.AddFirst(key);
            }
            return cached;
        }

        var fetched = FetchSpectrumChunk(channel, taskIndex, doCentroid);
        cache[key] = fetched;
        lock (_lruLock)
        {
            lru.AddFirst(key);
            while (lru.Count > MaxCachedChunks)
            {
                int oldest = lru.Last!.Value;
                lru.RemoveLast();
                cache.TryRemove(oldest, out _);
            }
        }
        return fetched;
    }

    private List<MzSpectrumItemDtoV2> FetchSpectrumChunk(ChannelInfo channel, int taskIndex, bool doCentroid)
    {
        // cpp WatersConnectImpl::spectrumEndpoint (WatersConnectData.ipp:272-284):
        //   /waters_connect/v2.0/injection-data/{injectionId}/ms/spectra
        //   ?channelFilter=channelId eq {channelId}
        //   &scanFilter=index ge {start} and index lt {end}
        //   &spectrumType=Continuum|Centered
        int spectrumIndexEnd = Math.Min(taskIndex + DefaultChunkSize, channel.NumSpectra);
        string typeFilter = doCentroid ? "Centered" : "Continuum";
        string url =
            $"{BaseUrl()}/waters_connect/v2.0/injection-data/{_client.Config.InjectionId}/ms/spectra"
            + $"?channelFilter=channelId eq {channel.Id}"
            + $"&scanFilter=index ge {taskIndex} and index lt {spectrumIndexEnd}"
            + $"&spectrumType={typeFilter}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/x-protobuf"));
        using var response = _client.SendAuthenticatedPostSync(request); // sends arbitrary HttpRequestMessage
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"failed to fetch waters_connect spectrum chunk (channel {channel.Id}, $skip={taskIndex}): "
                + $"{(int)response.StatusCode} {response.ReasonPhrase}");

        // cpp uses Serializer.Deserialize<MzSpectraDtoV2Collection^> (single message, no length
        // prefix). Google.Protobuf's MergeFrom on a known Length stream does the same.
        using var stream = new MemoryStream();
        using (var src = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
            src.CopyTo(stream);
        stream.Position = 0;

        if (stream.Length == 0)
            return new List<MzSpectrumItemDtoV2>();

        var collection = MzSpectraDtoV2Collection.Parser.ParseFrom(stream);
        var result = new List<MzSpectrumItemDtoV2>();
        if (collection.Items.Count == 0) return result;

        // cpp peels off Items[0] — only one channel per chunk request.
        var spectraEnvelope = collection.Items[0];
        foreach (var item in spectraEnvelope.Values)
            result.Add(item);
        return result;
    }

    private void FillUnifiSpectrum(MzSpectrumItemDtoV2 item, ChannelInfo channel, UnifiSpectrum result, bool getBinaryData, bool doCentroid)
    {
        // cpp WatersConnectImpl::convertWatersToPwizSpectrum (WatersConnectData.ipp:382-440 ish).
        // The MS level + scan range come from the channel; the protobuf item carries RT +
        // arrays + drift-time metadata.
        result.RetentionTime = item.RetentionTime;
        result.ScanPolarity = channel.Polarity;
        result.EnergyLevel = channel.EnergyLevel;
        result.MsLevel = GetMsLevel(_spectrumIndex.IndexOf(_spectrumIndex.First(k => k.Channel.Id == channel.Id && k.ScanIndexInChannel == item.ScanIndex)));
        // ^ The MS level lookup goes through the spectrum-index map. For most callers we can
        // skip the full search by inferring from channel.ScanningMethod directly:
        result.MsLevel = MsLevelFor(channel.ScanningMethod);
        result.ScanRange = (channel.LowMass, channel.HighMass);
        result.DataIsContinuous = !doCentroid;

        if (doCentroid)
        {
            var data = item.CentroidSpectrumData;
            if (data is null)
            {
                result.ArrayLength = 0;
                return;
            }
            result.ArrayLength = data.MOverZs.Count;
            if (getBinaryData && result.ArrayLength > 0)
            {
                result.MzArray = new double[data.MOverZs.Count];
                data.MOverZs.CopyTo(result.MzArray, 0);
                result.IntensityArray = new double[data.Intensities.Count];
                data.Intensities.CopyTo(result.IntensityArray, 0);
            }
        }
        else
        {
            var data = item.ContinuumSpectrumData;
            if (data is null)
            {
                result.ArrayLength = 0;
                return;
            }
            result.ArrayLength = data.Masses.Count;
            if (getBinaryData && result.ArrayLength > 0)
            {
                result.MzArray = new double[data.Masses.Count];
                data.Masses.CopyTo(result.MzArray, 0);
                result.IntensityArray = new double[data.Intensities.Count];
                data.Intensities.CopyTo(result.IntensityArray, 0);
            }
        }
    }

    private static int MsLevelFor(ScanningMethod m) => m switch
    {
        ScanningMethod.MS or ScanningMethod.SIR => 1,
        ScanningMethod.PRECURSOR => -1,
        ScanningMethod.PRODUCT or ScanningMethod.MSMS or ScanningMethod.DDA
          or ScanningMethod.MRM or ScanningMethod.NL or ScanningMethod.NG
          or ScanningMethod.PICS => 2,
        _ => 0,
    };

    private string BaseUrl() => _client.Config.SampleResultUrl.GetLeftPart(UriPartial.Authority);

    private void FetchTicChromatograms()
    {
        // cpp WatersConnectData.ipp:928-1020 — `/channels/ms/tic` returns one TIC per MS
        // channel. Each entry has parentChannelId + retentionTimes + intensities. We use these
        // both to populate ChromatogramInfo (one TIC per channel) and to drive the spectrum
        // index RT-sort. The endpoint returns a bare JSON array.
        string url = $"{BaseUrl()}/waters_connect/v2.0/injection-data/{_client.Config.InjectionId}/channels/ms/tic";
        using var response = _client.SendAuthenticatedGetSync(url);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"failed to fetch TIC chromatograms at {url}: {(int)response.StatusCode} {response.ReasonPhrase}");

        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("waters_connect TIC response was not a JSON array");

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            string parentChannelId = entry.GetPropertyOrEmpty("parentChannelId");
            string title = entry.GetPropertyOrEmpty("title");
            double[] times = ReadDoubleArray(entry, "retentionTimes");
            double[] intensities = ReadDoubleArray(entry, "intensities");

            _channelTics[parentChannelId] = (times, intensities, title);

            var info = new UnifiChromatogramInfo
            {
                Index = _chromatogramInfoList.Count,
                Type = ChromatogramType.TIC,
                Name = title,
                AltId = parentChannelId,
            };
            _chromatogramInfoList.Add(info);
            _chromatogramData.Add((times, intensities));
        }

        BuildSpectrumIndex();
    }

    private void BuildSpectrumIndex()
    {
        // cpp WatersConnectData.ipp:1148-1162: per-channel scans contribute (RT, channel,
        // scanIndex) tuples; the whole list sorts by RT to interleave channels acquired
        // serially with similar timing.
        var entries = new List<(double Rt, int Order, ChannelInfo Channel, int ScanIndex)>();
        int channelOrder = 0;
        foreach (var ch in _channels)
        {
            if (ch.ScanningMethod == ScanningMethod.Unknown) continue;
            // Use TIC retention times when available; otherwise fall back to channel-major
            // order so the offline tests (which don't stub /channels/ms/tic) still build a
            // deterministic index. The Order field is a tiebreaker that preserves insertion
            // order within identical RTs.
            if (_channelTics.TryGetValue(ch.Id, out var tic) && tic.Times.Length > 0)
            {
                if (tic.Times.Length != ch.NumSpectra)
                    throw new InvalidOperationException(
                        $"TIC retention-time count ({tic.Times.Length}) does not match channel {ch.Id} numSpectra ({ch.NumSpectra})");
                for (int s = 0; s < ch.NumSpectra; s++)
                    entries.Add((tic.Times[s], entries.Count, ch, s));
            }
            else
            {
                // Synthetic RT keeps channel-major when no TIC is available: channel 0 lands
                // before channel 1 etc.
                double basis = channelOrder * 1e9;
                for (int s = 0; s < ch.NumSpectra; s++)
                    entries.Add((basis + s, entries.Count, ch, s));
            }
            channelOrder++;
        }
        entries.Sort((a, b) =>
        {
            int c = a.Rt.CompareTo(b.Rt);
            return c != 0 ? c : a.Order.CompareTo(b.Order);
        });

        _spectrumIndex.Clear();
        if (!_combineIonMobilitySpectra && HasIonMobilityData)
        {
            // IMS spectra split into 200 bins each. Keep RT-sorted parent order and expand
            // each into 200 bins inline.
            foreach (var e in entries)
            {
                if (!e.Channel.IsIonMobilityData) continue;
                for (int b = 0; b < 200; b++)
                    _spectrumIndex.Add(new SpectrumKey(e.Channel, e.ScanIndex));
            }
        }
        else
        {
            foreach (var e in entries)
                _spectrumIndex.Add(new SpectrumKey(e.Channel, e.ScanIndex));
        }
        NumberOfSpectra = _spectrumIndex.Count;
    }

    private void FetchMrmChromatograms()
    {
        // cpp WatersConnectData.ipp:1022-1132 — `/channels/mrm` returns one chromatogram per
        // SRM transition (precursor/product m/z + RT/intensity arrays). Stash into
        // ChromatogramInfo + parallel data list.
        string url = $"{BaseUrl()}/waters_connect/v2.0/injection-data/{_client.Config.InjectionId}/channels/mrm";
        using var response = _client.SendAuthenticatedGetSync(url);
        if (!response.IsSuccessStatusCode)
        {
            // Empty MRM is normal for non-SRM acquisitions; only throw on hard errors.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return;
            throw new InvalidOperationException(
                $"failed to fetch MRM chromatograms at {url}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            string title = entry.GetPropertyOrEmpty("title");
            double q1 = entry.GetPropertyAsDouble("msTechnique.basicMsProperties.precursorMz");
            double q3 = entry.GetPropertyAsDouble("msTechnique.basicMsProperties.productMz");
            string polarity = entry.GetPropertyAtPath("msTechnique.basicMsProperties.ionisationMode")?.GetString() ?? string.Empty;
            UnifiPolarity p = polarity switch
            {
                "Positive" => UnifiPolarity.Positive,
                "Negative" => UnifiPolarity.Negative,
                _ => UnifiPolarity.Unknown,
            };

            double[] times = ReadDoubleArray(entry, "retentionTimes");
            double[] intensities = ReadDoubleArray(entry, "intensities");

            var info = new UnifiChromatogramInfo
            {
                Index = _chromatogramInfoList.Count,
                Type = ChromatogramType.MRM,
                Name = title,
                Q1 = q1,
                Q3 = q3,
                Polarity = p,
            };
            _chromatogramInfoList.Add(info);
            _chromatogramData.Add((times, intensities));
        }
    }

    private static double[] ReadDoubleArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<double>();
        var result = new double[arr.GetArrayLength()];
        int i = 0;
        foreach (var el in arr.EnumerateArray())
            result[i++] = el.ValueKind == JsonValueKind.Number ? el.GetDouble() : 0.0;
        return result;
    }

    private static int HashChunkKey(string channelId, int taskIndex)
        => HashCode.Combine(channelId, taskIndex);

    private static ChannelDataType ParseDataType(string s) => s switch
    {
        "MSScanning" => ChannelDataType.MSScanning,
        "IMSScanning" => ChannelDataType.IMSScanning,
        _ => ChannelDataType.Unknown,
    };

    private static ChannelDataShape ParseDataShape(string s) => s switch
    {
        "Spectra" => ChannelDataShape.Spectra,
        "Spectrum" => ChannelDataShape.Spectrum,
        _ => ChannelDataShape.Unknown,
    };

    private static ScanningMethod ParseScanningMethod(string? s) => s switch
    {
        "MS" => ScanningMethod.MS,
        "PRECURSOR" => ScanningMethod.PRECURSOR,
        "PRODUCT" => ScanningMethod.PRODUCT,
        "SCAN_WAVE_PRODUCT" => ScanningMethod.SCAN_WAVE_PRODUCT,
        "MSMS" => ScanningMethod.MSMS,
        "SIR" => ScanningMethod.SIR,
        "MRM" => ScanningMethod.MRM,
        "NL" => ScanningMethod.NL,
        "NG" => ScanningMethod.NG,
        "PICS" => ScanningMethod.PICS,
        "DDA" => ScanningMethod.DDA,
        _ => ScanningMethod.Unknown,
    };

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsClient) _client.Dispose();
    }

    private readonly record struct SpectrumKey(ChannelInfo Channel, int ScanIndexInChannel);
}

/// <summary>One waters_connect channel — equivalent of UNIFI's <see cref="UnifiFunctionInfo"/>.
/// cpp <c>WatersConnectImpl::ChannelInfo</c> (WatersConnectData.ipp:306-324).</summary>
public sealed class ChannelInfo
{
    /// <summary>Zero-based index parsed from the channel name (e.g. "1: TOF MS..." → 0).
    /// Drives the <c>channelIndex=N</c> portion of waters_connect spectrum ids.</summary>
    public int Index { get; init; }
    /// <summary>waters_connect channel GUID. Used in spectrum-fetch URLs (<c>channelFilter=channelId eq ...</c>).</summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>Display name (e.g. <c>"1: TOF MS (50-1200) ESI+"</c>).</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Container shape — <c>Spectra</c> for retention-bound, <c>Spectrum</c> for single.</summary>
    public ChannelDataShape ChannelDataShape { get; init; }
    /// <summary>Underlying data type (MSScanning, IMSScanning, etc.).</summary>
    public ChannelDataType ChannelDataType { get; init; }
    /// <summary>Scanning method (MS / MRM / DDA / ...). Drives MS-level mapping.</summary>
    public ScanningMethod ScanningMethod { get; set; }
    /// <summary>Ionization source (e.g. <c>"ESI"</c>).</summary>
    public string IonizationType { get; set; } = string.Empty;
    /// <summary>Scan polarity.</summary>
    public UnifiPolarity Polarity { get; set; }
    /// <summary>True when the channel carries ion-mobility bins.</summary>
    public bool IsIonMobilityData { get; set; }
    /// <summary>Number of spectra in the channel.</summary>
    public int NumSpectra { get; set; }
    /// <summary>Acquired m/z range low.</summary>
    public double LowMass { get; set; }
    /// <summary>Acquired m/z range high.</summary>
    public double HighMass { get; set; }
    /// <summary>MSe energy slot, when applicable.</summary>
    public UnifiEnergyLevel EnergyLevel { get; set; }
}

/// <summary>Channel data shape. cpp ChannelDataShape (WatersConnectData.ipp). Names match
/// the wire enum from the channels endpoint.</summary>
#pragma warning disable CS1591
public enum ChannelDataShape { Unknown, Spectra, Spectrum }
public enum ChannelDataType { Unknown, MSScanning, IMSScanning }
/// <summary>Scanning method per channel. cpp ScanningMethod (WatersConnectData.ipp:114-129).
/// Names match the wire enum from the channels endpoint's
/// <c>technique.basicMsProperties.scanningMethod</c> string field.</summary>
#pragma warning disable CA1707
public enum ScanningMethod { Unknown, MS, PRECURSOR, PRODUCT, SCAN_WAVE_PRODUCT, MSMS, SIR, MRM, NL, NG, PICS, DDA }
#pragma warning restore CA1707
#pragma warning restore CS1591
