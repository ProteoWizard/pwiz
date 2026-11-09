using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Pwiz.Vendor.UNIFI.Protobuf;

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// Live <see cref="IUnifiDataSource"/> backed by a UNIFI / waters_connect HTTP endpoint.
/// C# port of cpp <c>UnifiData::Impl</c> (UnifiData.cpp).
/// </summary>
/// <remarks>
/// <para>Construction kicks off the discovery walk that cpp's <c>connect()</c>
/// (UnifiData.cpp:218-258) does up-front: pull <c>/spectrumInfos</c>, walk every MS
/// retention-data function, fetch each function's <c>/data</c> for its scan count, sort by
/// MSe energy slot, then pull <c>/chromatogramInfos</c> for the chromatogram catalog and
/// (when IMS) POST <c>/binToDriftTime</c> for the bin → drift-time table.</para>
///
/// <para><b>Current scope:</b> UNIFI flavor only. Discovery + chromatogram-data fetch are
/// implemented; spectrum chunk fetching (the protobuf <c>/spectra/mass.mse</c> path that
/// drives <see cref="ParallelDownloadQueue"/>) lands in the next slice — <see cref="GetSpectrum"/>
/// throws <see cref="NotImplementedException"/>. waters_connect's parallel data layer
/// (different URL shapes, different protobuf schema for per-channel arrays) lands separately
/// after that.</para>
/// </remarks>
public sealed class HttpUnifiDataSource : IUnifiDataSource, IDisposable
{
    private readonly AbstractWatersHttpClient _client;
    private readonly bool _ownsClient;
    private readonly bool _combineIonMobilitySpectra;
    private readonly List<UnifiFunctionInfo> _functions = new();
    private readonly List<UnifiChromatogramInfo> _chromatogramInfo = new();
    private readonly List<string> _chromatogramIds = new();
    private readonly List<double> _binToDriftTime = new();
    private bool _disposed;

    // cpp UnifiData.cpp:229 picks 20 as the default chunk size. Each chunk fetch returns
    // protobuf-length-prefixed MSeMassSpectrum entries. We cache by (network) chunk start
    // index. Without ParallelDownloadQueue wiring this is synchronous-fetch + bounded LRU,
    // matching cpp's correctness path; the queue's value is throughput, which we add later.
    private const int DefaultChunkSize = 20;
    private const int MaxCachedChunks = 16; // bounded so we don't accumulate the whole run
    private readonly ConcurrentDictionary<int, IReadOnlyList<MSeMassSpectrum>> _chunkCache = new();
    private readonly LinkedList<int> _chunkLru = new();
    private readonly object _chunkLruLock = new();

    /// <summary>Constructs the data source and runs the metadata-discovery walk.
    /// <paramref name="client"/> must already be authenticated (constructed via
    /// <see cref="UnifiHttpClient"/> or <see cref="WatersConnectHttpClient"/> — both run
    /// the OAuth handshake in their own ctor).</summary>
    /// <param name="client">Authenticated HTTP client.</param>
    /// <param name="combineIonMobilitySpectra">Mirrors cpp <c>config.combineIonMobilitySpectra</c>
    /// — when false (default) and the source has IMS data, each IMS function contributes
    /// <c>numSpectra × 200</c> logical spectra (one per drift-time bin).</param>
    /// <param name="ownsClient">When true, disposing this source disposes <paramref name="client"/>.</param>
    public HttpUnifiDataSource(AbstractWatersHttpClient client, bool combineIonMobilitySpectra, bool ownsClient = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _ownsClient = ownsClient;
        _combineIonMobilitySpectra = combineIonMobilitySpectra;

        FetchFunctionInfos();
        FetchChromatogramInfos();
        if (HasIonMobilityData)
            FetchBinToDriftTime();
    }

    /// <inheritdoc/>
    public RemoteApiType RemoteApi => _client.Config.Api;

    /// <inheritdoc/>
    public bool HasIonMobilityData { get; private set; }

    /// <inheritdoc/>
    public int NumberOfSpectra { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<UnifiChromatogramInfo> ChromatogramInfo => _chromatogramInfo;

    /// <summary>Read-only view of the discovered MS scan functions, sorted by
    /// <see cref="UnifiEnergyLevel"/> (Low → High → Unknown). cpp UnifiData.cpp:1139-1153.</summary>
    public IReadOnlyList<UnifiFunctionInfo> Functions => _functions;

    /// <summary>200-element bin → drift-time-millisecond table for IMS data; empty when
    /// the source has no IMS functions. cpp UnifiData.cpp:1268-1289.</summary>
    public IReadOnlyList<double> BinToDriftTime => _binToDriftTime;

    /// <inheritdoc/>
    /// <remarks>cpp <c>UnifiData::Impl::getMsLevel</c> (UnifiData.cpp:264-268) alternates
    /// MSn level by even/odd index — Low-energy MS1 / High-energy MS2 in MSe acquisitions.</remarks>
    public int GetMsLevel(int index) => index % 2 == 0 ? 1 : 2;

    /// <inheritdoc/>
    public (int ChannelIndex, int ScanIndexInChannel) GetChannelAndScanIndex(int index)
        => throw new NotSupportedException("UNIFI spectra are not organized by channel; this is a waters_connect-only API");

    /// <inheritdoc/>
    public void GetSpectrum(int index, UnifiSpectrum spectrum, bool getBinaryData, bool doCentroid)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        if (index < 0 || index >= NumberOfSpectra)
            throw new ArgumentOutOfRangeException(nameof(index));

        int networkIndex = NetworkIndexFromLogicalIndex(index);
        int taskIndex = (networkIndex / DefaultChunkSize) * DefaultChunkSize;
        int localIndex = networkIndex - taskIndex;

        var chunk = EnsureChunkLoaded(taskIndex);
        if (localIndex >= chunk.Count)
            throw new InvalidOperationException(
                $"chunk at task {taskIndex} returned {chunk.Count} spectra; expected at least {localIndex + 1} (logical index {index})");

        ConvertWatersToPwizSpectrum(chunk[localIndex], spectrum, index, getBinaryData);
    }

    /// <summary>Maps a logical spectrum index to the network chunk's spectrum index. cpp
    /// <c>networkIndexFromLogicalIndex</c> (UnifiData.cpp:950-960) — for combined-IMS or
    /// non-IMS data the mapping is identity; otherwise floor(logicalIndex / 200) collapses
    /// the per-bin breakdown back into one network entry per IMS scan.</summary>
    private int NetworkIndexFromLogicalIndex(int logicalIndex)
    {
        if (!HasIonMobilityData || _combineIonMobilitySpectra) return logicalIndex;
        return logicalIndex / 200;
    }

    private IReadOnlyList<MSeMassSpectrum> EnsureChunkLoaded(int taskIndex)
    {
        if (_chunkCache.TryGetValue(taskIndex, out var cached))
        {
            // touch LRU
            lock (_chunkLruLock)
            {
                _chunkLru.Remove(taskIndex);
                _chunkLru.AddFirst(taskIndex);
            }
            return cached;
        }

        var fetched = FetchSpectrumChunk(taskIndex);
        _chunkCache[taskIndex] = fetched;
        lock (_chunkLruLock)
        {
            _chunkLru.AddFirst(taskIndex);
            // Bounded LRU eviction. Without this, fetching every chunk in a 100k-spectrum
            // run holds the entire decoded run in memory.
            while (_chunkLru.Count > MaxCachedChunks)
            {
                int oldest = _chunkLru.Last!.Value;
                _chunkLru.RemoveLast();
                _chunkCache.TryRemove(oldest, out _);
            }
        }
        return fetched;
    }

    private List<MSeMassSpectrum> FetchSpectrumChunk(int taskIndex)
    {
        // cpp UnifiData.cpp:804-808: /spectra/mass.mse?$skip=N&$top=K returns a
        // length-prefixed protobuf stream of K MSeMassSpectrum messages. Google.Protobuf's
        // ParseDelimitedFrom reads exactly that wire format (varint-length + message).
        string url = $"{_client.Config.SampleResultUrl.AbsoluteUri}/spectra/mass.mse?$skip={taskIndex}&$top={DefaultChunkSize}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));
        using var response = _client.SendAuthenticatedPostSync(request); // POST helper sends arbitrary requests; the verb is GET here
        // (Note: SendAuthenticatedPostSync just delegates to HttpClient.Send — name is misleading
        // but it works for any pre-built HttpRequestMessage. Keeping the existing signature for now.)
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"failed to fetch spectrum chunk at $skip={taskIndex}: {(int)response.StatusCode} {response.ReasonPhrase}");

        var result = new List<MSeMassSpectrum>();
        // Read the entire response body into a MemoryStream so we can detect EOF by position
        // rather than catching exceptions. Google.Protobuf 3.7's ParseDelimitedFrom throws on
        // empty stream rather than returning null (the docs imply null-on-EOF but the
        // implementation in this version does not honor that).
        using var stream = new MemoryStream();
        using (var src = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
            src.CopyTo(stream);
        stream.Position = 0;

        while (stream.Position < stream.Length)
        {
            // The Spectrum hierarchy is encoded as nested message fields (Spectrum -> 200 ->
            // MassSpectrum -> 100 -> MSeMassSpectrum). Parse the outer Spectrum and reach
            // through to the leaf — wire bytes match cpp's protobuf-net [ProtoInclude] format.
            var spec = Spectrum.Parser.ParseDelimitedFrom(stream);
            if (spec is null) break;
            var mass = spec.MassSpectrum;
            var mse = mass?.MseMassSpectrum;
            if (mse is null)
            {
                // Defensive: cpp throws on null spectrum; we mirror by skipping. Real UNIFI
                // payloads always have the full chain populated.
                continue;
            }
            // Lift the mz / intensity arrays + ScanSize onto the MSe instance so
            // ConvertWatersToPwizSpectrum can find them all in one place. We attach via the
            // generated message's repeated fields rather than going through the abstract
            // parent (proto3 generated code wires them on the MassSpectrum scope).
            // Stash the parent fields on the MSe via reflection-free helper carriers below.
            result.Add(mse);
            // Save the masses + intensities + scan_size on a sidecar dict keyed by the MSe ref.
            // Wire encodes both as `repeated float` (4-byte fixed32) — widen to double for the
            // pwiz BinaryDataArray side, which uses double throughout.
            int massesCount = mass!.Masses.Count;
            var massesD = new double[massesCount];
            for (int i = 0; i < massesCount; i++) massesD[i] = mass.Masses[i];
            int intensCount = spec.Intensities.Count;
            var intensD = new double[intensCount];
            for (int i = 0; i < intensCount; i++) intensD[i] = spec.Intensities[i];
            _spectrumExtras.AddOrUpdate(mse, new SpectrumExtras
            {
                Masses = massesD,
                Intensities = intensD,
                ScanSize = mass.ScanSize.ToArray(),
            });
        }
        return result;
    }

    private sealed class SpectrumExtras
    {
        public double[] Masses = Array.Empty<double>();
        public double[] Intensities = Array.Empty<double>();
        public int[] ScanSize = Array.Empty<int>();
    }
    private readonly ConditionalWeakTable<MSeMassSpectrum, SpectrumExtras> _spectrumExtras = new();

    private void ConvertWatersToPwizSpectrum(MSeMassSpectrum mse, UnifiSpectrum result, int logicalIndex, bool getBinaryData)
    {
        // cpp convertWatersToPwizSpectrum (UnifiData.cpp:1305-1353).
        result.RetentionTime = mse.RetentionTime;
        result.ScanPolarity = (UnifiPolarity)mse.IonizationPolarity;
        result.EnergyLevel = (UnifiEnergyLevel)mse.EnergyLevel;
        int functionIndex = result.EnergyLevel == UnifiEnergyLevel.Low ? 0 : 1;
        result.MsLevel = functionIndex + 1;
        if (functionIndex < _functions.Count)
        {
            result.ScanRange = (_functions[functionIndex].LowMass, _functions[functionIndex].HighMass);
        }
        result.DataIsContinuous = true;

        if (!_spectrumExtras.TryGetValue(mse, out var extras))
            extras = new SpectrumExtras();

        if (_combineIonMobilitySpectra || !HasIonMobilityData)
        {
            // Non-IMS or combined-IMS: arrays come through as-is.
            if (!HasIonMobilityData && extras.ScanSize.Length > 1)
                throw new InvalidOperationException(
                    $"non-ion-mobility spectrum has ScanSize.Length = {extras.ScanSize.Length} (expected ≤ 1)");
            result.DriftTime = 0;
            result.ArrayLength = extras.Masses.Length;
            if (getBinaryData && result.ArrayLength > 0)
            {
                result.MzArray = extras.Masses;
                result.IntensityArray = extras.Intensities;
                // No driftTimeArray when not combining (cpp lifts it through unchanged when
                // combineIonMobilitySpectra=true; we'd need bin → drift expansion to do the
                // same here — TODO when combine-IMS support lands).
            }
        }
        else
        {
            // Per-bin IMS: ScanSize must be 200; localIndex picks the bin.
            if (extras.ScanSize.Length != 200)
                throw new InvalidOperationException(
                    $"ion-mobility spectrum has ScanSize.Length = {extras.ScanSize.Length} (expected 200)");
            int driftBin = logicalIndex % 200;
            result.DriftTime = _binToDriftTime[driftBin];
            result.ArrayLength = extras.ScanSize[driftBin];
            if (getBinaryData && result.ArrayLength > 0)
            {
                // Compute the cumulative offset into Masses / Intensities arrays for this bin.
                int offset = 0;
                for (int i = 0; i < driftBin; i++) offset += extras.ScanSize[i];
                result.MzArray = extras.Masses.AsSpan(offset, result.ArrayLength).ToArray();
                result.IntensityArray = extras.Intensities.AsSpan(offset, result.ArrayLength).ToArray();
            }
        }
    }

    /// <inheritdoc/>
    public void GetChromatogram(int index, UnifiChromatogram chromatogram, bool getBinaryData)
    {
        ArgumentNullException.ThrowIfNull(chromatogram);
        if (index < 0 || index >= _chromatogramInfo.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var info = _chromatogramInfo[index];
        // Mirror the static fields onto the result so the caller can use them without an
        // extra ChromatogramInfo lookup.
        chromatogram.Index = info.Index;
        chromatogram.Type = info.Type;
        chromatogram.Name = info.Name;
        chromatogram.AltId = info.AltId;
        chromatogram.Q1 = info.Q1;
        chromatogram.Q3 = info.Q3;
        chromatogram.Polarity = info.Polarity;
        chromatogram.AcquiredTimeRange = info.AcquiredTimeRange;

        // cpp chromatogramEndpoint (UnifiData.cpp:817): /chromatogramInfos(<guid>)/data.
        string guid = _chromatogramIds[index];
        string url = $"{_client.Config.SampleResultUrl.AbsoluteUri}/chromatogramInfos({guid})/data";
        using var response = SendGetSync(url);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"failed to fetch chromatogram {info.Name}: {(int)response.StatusCode} {response.ReasonPhrase}");

        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        ParseChromatogramData(body, chromatogram, getBinaryData);
    }

    private static void ParseChromatogramData(string json, UnifiChromatogram chromatogram, bool getBinaryData)
    {
        // The /chromatogramInfos(<guid>)/data endpoint returns the per-chromatogram entity
        // directly — not wrapped in a `value:[]` collection (the comment block at cpp
        // UnifiData.cpp:1186-1232 shows the shape). Tolerate both:
        //   {"@odata.context": "...", "retentionTimes": [...], "intensities": [...]}
        //   {"@odata.context": "...", "value": [{"retentionTimes": [...], ...}]}
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement source = root;
        if (root.TryGetProperty("value", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            if (arr.GetArrayLength() == 0)
            {
                chromatogram.ArrayLength = 0;
                return;
            }
            source = arr[0];
        }

        var times = ReadDoubleArray(source, "retentionTimes");
        var intensities = ReadDoubleArray(source, "intensities");
        int n = Math.Min(times.Length, intensities.Length);
        chromatogram.ArrayLength = n;
        if (getBinaryData)
        {
            chromatogram.TimeArray = times.Length == n ? times : times.AsSpan(0, n).ToArray();
            chromatogram.IntensityArray = intensities.Length == n ? intensities : intensities.AsSpan(0, n).ToArray();
        }
    }

    private static double[] ReadDoubleArray(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<double>();
        var result = new double[arr.GetArrayLength()];
        int i = 0;
        foreach (var v in arr.EnumerateArray())
        {
            // UNIFI sometimes emits "NaN" as a string for missing data points. Guard the parse.
            if (v.ValueKind == JsonValueKind.Number) result[i++] = v.GetDouble();
            else if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(),
                         NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) result[i++] = parsed;
            else result[i++] = double.NaN;
        }
        return result;
    }

    private void FetchFunctionInfos()
    {
        // cpp UnifiData.cpp:1071-1164. Walk /spectrumInfos, filter to MS+retention-data
        // functions, read low/high mass + isIonMobilityData + mseLevel, then per-function GET
        // {id}/data for the totalNumberOfSpectra count.
        string functionInfoUrl = $"{_client.Config.SampleResultUrl.AbsoluteUri}/spectrumInfos";
        string body = GetJson(functionInfoUrl, "function info");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var array) || array.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("function info JSON missing 'value' array");

        bool hasMse = false;
        var draft = new List<UnifiFunctionInfo>();
        foreach (var fn in array.EnumerateArray())
        {
            string detectorType = fn.GetPropertyOrEmpty("detectorType");
            bool isRetentionData = fn.GetPropertyOrFalse("isRetentionData");
            if (detectorType != "MS" || !isRetentionData) continue;

            string id = fn.GetPropertyOrEmpty("id");
            bool isCentroid = fn.GetPropertyOrFalse("isCentroidData");
            bool isIms = fn.GetPropertyOrFalse("isIonMobilityData");
            bool hasCcs = fn.GetPropertyOrFalse("hasCCSCalibration");
            HasIonMobilityData |= isIms;

            double lowMass = fn.GetPropertyAsDouble("analyticalTechnique.lowMass");
            double highMass = fn.GetPropertyAsDouble("analyticalTechnique.highMass");

            // Skip non-MSe functions — cpp UnifiData.cpp:1109-1115 notes the UNIFI API
            // doesn't expose chunk download for them.
            string? mseLevel = fn.GetPropertyAtPath("analyticalTechnique.tofGroup.mseLevel")?.GetString();
            if (string.IsNullOrEmpty(mseLevel)) continue;
            UnifiEnergyLevel energy = mseLevel switch
            {
                "Low" => UnifiEnergyLevel.Low,
                "High" => UnifiEnergyLevel.High,
                _ => UnifiEnergyLevel.Unknown,
            };
            if (energy == UnifiEnergyLevel.Unknown) continue;
            hasMse = true;

            // Per-function spectrum count.
            int numSpectra = FetchFunctionSpectrumCount(id);

            draft.Add(new UnifiFunctionInfo
            {
                Index = draft.Count,
                Id = id,
                IsCentroidData = isCentroid,
                IsRetentionData = isRetentionData,
                IsIonMobilityData = isIms,
                HasCCSCalibration = hasCcs,
                LowMass = lowMass,
                HighMass = highMass,
                EnergyLevel = energy,
                NumberOfSpectra = numSpectra,
            });
        }

        if (!hasMse)
            throw new InvalidOperationException("only MSe and HD-MSe data is supported at this time");

        // cpp UnifiData.cpp:1139-1153: sort by energy slot Low → High → Unknown. Reassign
        // Index after the sort so consumers see a stable order.
        draft.Sort((a, b) => EnergyOrder(a.EnergyLevel).CompareTo(EnergyOrder(b.EnergyLevel)));
        for (int i = 0; i < draft.Count; i++)
        {
            _functions.Add(new UnifiFunctionInfo
            {
                Index = i,
                Id = draft[i].Id,
                IsCentroidData = draft[i].IsCentroidData,
                IsRetentionData = draft[i].IsRetentionData,
                IsIonMobilityData = draft[i].IsIonMobilityData,
                HasCCSCalibration = draft[i].HasCCSCalibration,
                LowMass = draft[i].LowMass,
                HighMass = draft[i].HighMass,
                EnergyLevel = draft[i].EnergyLevel,
                NumberOfSpectra = draft[i].NumberOfSpectra,
            });
        }

        // Total logical spectra. cpp UnifiData.cpp:1155-1164.
        int total = 0;
        foreach (var f in _functions)
        {
            if (!_combineIonMobilitySpectra && HasIonMobilityData)
            {
                if (f.IsIonMobilityData) total += f.NumberOfSpectra * 200;
            }
            else
            {
                total += f.NumberOfSpectra;
            }
        }
        NumberOfSpectra = total;
    }

    private int FetchFunctionSpectrumCount(string id)
    {
        // cpp UnifiData.cpp:1121 uses ?$top=1 — only the count is needed, not the full page.
        string url = $"{_client.Config.SampleResultUrl.AbsoluteUri}/spectrumInfos({id})/data?$top=1";
        string body = GetJson(url, $"function-data {id}");
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var arr) || arr.ValueKind != JsonValueKind.Array
            || arr.GetArrayLength() == 0)
            return 0;
        if (arr[0].TryGetProperty("totalNumberOfSpectra", out var n) && n.TryGetInt32(out int v))
            return v;
        return 0;
    }

    private void FetchChromatogramInfos()
    {
        // cpp UnifiData.cpp:1178-1259. The endpoint returns one chromatogram per (function,
        // metric, detector) combination — a typical TIC + BPI + UV/FLR set. We classify by
        // detectorType + name-contains-"TIC".
        string url = $"{_client.Config.SampleResultUrl.AbsoluteUri}/chromatogramInfos";
        string body = GetJson(url, "chromatogram info");
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var ci in arr.EnumerateArray())
        {
            string id = ci.GetPropertyOrEmpty("id");
            string name = ci.GetPropertyOrEmpty("name");
            string detectorType = ci.GetPropertyOrEmpty("detectorType");

            ChromatogramType type = detectorType switch
            {
                "MS" when name.Contains("TIC", StringComparison.Ordinal) => ChromatogramType.TIC,
                "UV" => ChromatogramType.UV,
                "FLR" => ChromatogramType.FLR,
                "IR" => ChromatogramType.IR,
                "NMR" => ChromatogramType.NMR,
                _ => ChromatogramType.Unknown,
            };

            _chromatogramIds.Add(id);
            _chromatogramInfo.Add(new UnifiChromatogramInfo
            {
                Index = _chromatogramInfo.Count,
                Type = type,
                Name = name,
                AltId = id,
            });
        }
    }

    private void FetchBinToDriftTime()
    {
        // cpp UnifiData.cpp:1268-1289. POST {"bins":[1..200]} to /binToDriftTime; the response
        // is a {"value":[double, ...]} array of 200 ms-domain values. Used at spectrum-fetch
        // time to translate the protobuf's ScanSize-relative bin offsets into milliseconds.
        string url = $"{_client.Config.SampleResultUrl.AbsoluteUri}/binToDriftTime";
        var bins = new StringBuilder("{\"bins\":[");
        for (int i = 1; i <= 200; i++)
        {
            if (i > 1) bins.Append(',');
            bins.Append(i.ToString(CultureInfo.InvariantCulture));
        }
        bins.Append("]}");

        var content = new StringContent(bins.ToString(), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        using var response = _client.SendAuthenticatedPostSync(request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"failed to fetch bin→drift-time table: {(int)response.StatusCode} {response.ReasonPhrase}");

        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var arr) || arr.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("bin→drift-time response missing 'value' array");

        foreach (var v in arr.EnumerateArray())
            _binToDriftTime.Add(v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN);

        if (_binToDriftTime.Count != 200)
            throw new InvalidOperationException(
                $"bin→drift-time table size {_binToDriftTime.Count} ≠ 200 (cpp expects exactly 200)");
    }

    private string GetJson(string url, string label)
    {
        using var response = SendGetSync(url);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"failed to fetch {label} ({url}): {(int)response.StatusCode} {response.ReasonPhrase}");
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private HttpResponseMessage SendGetSync(string url)
        => _client.SendAuthenticatedGetSync(url);

    private static int EnergyOrder(UnifiEnergyLevel level) => level switch
    {
        UnifiEnergyLevel.Low => 0,
        UnifiEnergyLevel.High => 1,
        UnifiEnergyLevel.Unknown => 2,
        _ => 3,
    };

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsClient) _client.Dispose();
    }
}

internal static class JsonExtensions
{
    public static string GetPropertyOrEmpty(this JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty : string.Empty;

    public static bool GetPropertyOrFalse(this JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    public static double GetPropertyAsDouble(this JsonElement e, string dottedPath)
    {
        var node = GetPropertyAtPath(e, dottedPath);
        if (node is not JsonElement v) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        if (v.ValueKind == JsonValueKind.String
            && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return 0;
    }

    public static JsonElement? GetPropertyAtPath(this JsonElement e, string dottedPath)
    {
        // Walks "a.b.c" through nested objects, returning null if any segment is missing or
        // a non-object intermediate.
        JsonElement current = e;
        foreach (var segment in dottedPath.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object) return null;
            if (!current.TryGetProperty(segment, out var next)) return null;
            current = next;
        }
        return current;
    }
}
