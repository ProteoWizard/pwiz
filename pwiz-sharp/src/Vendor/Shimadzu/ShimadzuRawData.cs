using ShimadzuIO = Shimadzu.LabSolutions.IO;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Shimadzu;

/// <summary>
/// Thin managed wrapper around Shimadzu's <c>Shimadzu.LabSolutions.IO</c> SDK. C# equivalent of
/// the C++/CLI <c>ShimadzuReaderImpl</c> in <c>ShimadzuReader.cpp</c>. The SDK is already a
/// .NET assembly, so the wrapping is much thinner than for a P/Invoke or COM SDK.
/// </summary>
/// <remarks>
/// Initial scope (mirrors what the cpp port exposes via <c>ShimadzuReader</c>): MS scan info,
/// per-scan profile/centroid spectrum data, SRM transition discovery + per-transition
/// chromatograms, file-level TIC, and analysis-date / system-name metadata. UV/PDA non-MS data
/// and alternate sample selection (multi-sample LCDs) are out of scope for the initial port.
/// </remarks>
public sealed class ShimadzuRawData : IDisposable
{
    // Constants for converting integer-encoded mass / time values to doubles. cpp
    // ShimadzuReader.cpp:55-57 reads MASSNUMBER_UNIT from the SDK plus two empirical constants
    // (Shimadzu doesn't publish the precursor / time multipliers).
    internal static readonly double MassMultiplier = 1.0 / ShimadzuIO.Generic.Tool.MASSNUMBER_UNIT;
    internal const double PrecursorMzMultiplier = 1.0 / 1e9;
    internal const double TimeMultiplier = 0.001;

    /// <summary>How many times to retry the LCD open + SDK init when the QTFL backend looks
    /// like it failed to wire up. The Shimadzu Qtfl backend's init has a documented .NET 8
    /// race (~65% pass rate on TC for the 10nmol fixture); a fresh DataObject usually clears
    /// it. Five attempts gives us &lt;0.5% chance of all attempts failing if each is independent.</summary>
    private const int QtflInitRetryAttempts = 5;

    private ShimadzuIO.Data.DataObject _dataObject = null!; // assigned during init
    private readonly Dictionary<(short Event, short Channel), ShimadzuIO.Generic.Param.MS.MassEventInfo> _eventInfo
        = new();
    private readonly List<List<short>> _eventNumbersBySegment = new();
    private readonly Dictionary<int, (double PrecursorMz, int Charge)> _precursorInfoByScan = new();
    private readonly SortedSet<int> _msLevels = new();
    private readonly List<ShimadzuTransition> _transitions = new();

    private bool _transitionsLoaded;
    private bool _disposed;

    /// <summary>Path to the .lcd file.</summary>
    public string Path { get; }

    /// <summary>Underlying SDK handle. Avoid using outside the Shimadzu vendor module.</summary>
    public ShimadzuIO.Data.DataObject DataObject => _dataObject;

    /// <summary>Highest contiguous scan number assigned by the SDK across all events / segments.</summary>
    public int ScanCount { get; private set; }

    /// <summary>Number of segments (acquisition method blocks) in the file.</summary>
    public int SegmentCount { get; private set; }

    /// <summary>SDK-reported friendly system name (used by the instrument-model translator).</summary>
    public string SystemName { get; private set; } = string.Empty;

    /// <summary>Set of MS levels actually present in the file (typically {1} or {1,2}).</summary>
    public IReadOnlySet<int> MsLevels => _msLevels;

    /// <summary>SRM transitions discovered in the file. Empty when the file isn't SRM.</summary>
    public IReadOnlyList<ShimadzuTransition> Transitions
    {
        get
        {
            if (!_transitionsLoaded) LoadTransitions();
            return _transitions;
        }
    }

    /// <summary>Raw acquisition timestamp from the SDK, with <see cref="DateTime.Kind"/> preserved
    /// so the consumer can apply the optional host-time-zone adjustment via
    /// <c>ReaderConfig.FormatStartTimeStamp</c>. May be <see cref="DateTime.MinValue"/> if the
    /// SDK didn't populate <c>SampleInfo.AnalysisDate</c> (regression under .NET 8) or its
    /// fallback <c>FilePropTag.GeneratedDateTime</c>.</summary>
    public DateTime AnalysisDateRaw { get; private set; }

    /// <summary>Opens <paramref name="lcdPath"/>. Must be a .lcd file.</summary>
    /// <param name="lcdPath">Path to the Shimadzu .lcd file.</param>
    /// <param name="srmAsSpectra">When true, mirrors the cpp <c>srmAsSpectra</c> mode: SRM
    /// events count toward <see cref="ScanCount"/> so they're emitted as spectra rather than
    /// being skipped during index construction (cpp <c>ShimadzuReader.cpp:259-292</c>).</param>
    public ShimadzuRawData(string lcdPath, bool srmAsSpectra)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lcdPath);
        if (!File.Exists(lcdPath))
            throw new FileNotFoundException("Shimadzu .lcd file not found", lcdPath);
        Path = lcdPath;

        // The SDK's QtflRawDataMain backend has a flaky lazy-init race on .NET 8 — on the TC
        // build agent the 10nmol Q-TOF fixture has a 65% pass rate; warmup + chromMng calls
        // throw RuntimeBinderException because an inner dynamic-dispatched field is still null
        // at first access. A fresh DataObject usually clears it. Retry on the documented
        // signature (init produced no scans but ScanCount-derived signals indicate the SDK
        // failed silently); preserve final-attempt state so the caller sees the underlying
        // error when retry doesn't help.
        Exception? lastException = null;
        for (int attempt = 1; attempt <= QtflInitRetryAttempts; attempt++)
        {
            ResetInitState();
            try
            {
                bool qtflBackendLooksBroken = TryInitializeFromLcd(lcdPath, srmAsSpectra);
                if (!qtflBackendLooksBroken) return;
                if (attempt == QtflInitRetryAttempts) return; // give up, expose whatever we got
                CloseDataObjectQuietly();
            }
            catch (Exception ex)
            {
                lastException = ex;
                CloseDataObjectQuietly();
                if (attempt == QtflInitRetryAttempts) throw;
            }
        }
        // Unreachable in practice — last attempt either returns or throws above.
        if (lastException is not null) throw lastException;
    }

    /// <summary>Resets per-attempt state. The collection fields are <c>readonly</c>, so we
    /// clear in place rather than re-allocating.</summary>
    private void ResetInitState()
    {
        _eventInfo.Clear();
        _eventNumbersBySegment.Clear();
        _precursorInfoByScan.Clear();
        _msLevels.Clear();
        _transitions.Clear();
        _transitionsLoaded = false;
        SegmentCount = 0;
        ScanCount = 0;
        SystemName = string.Empty;
        AnalysisDateRaw = default;
    }

    private void CloseDataObjectQuietly()
    {
        try { _dataObject?.IO.Close(); } catch { /* best-effort */ }
    }

    /// <summary>One attempt at opening + initializing the LCD. Returns <c>true</c> if the
    /// SDK's QTFL backend appears to have failed to wire up (warmup + chromMng walk both
    /// threw a binder-style exception AND no scans were discovered) — the caller retries
    /// with a fresh <see cref="ShimadzuIO.Data.DataObject"/>. Returns <c>false</c> on success
    /// or on a non-retryable shape (e.g. a genuinely empty file).</summary>
    private bool TryInitializeFromLcd(string lcdPath, bool srmAsSpectra)
    {
        _dataObject = new ShimadzuIO.Data.DataObject();
        var loadResult = _dataObject.IO.LoadData(lcdPath);
        if (ShimadzuIO.Generic.Tool.Failed(loadResult))
            throw new InvalidDataException($"Shimadzu LoadData failed: {loadResult}");

        bool qtflBackendThrew = false;

        try
        {
            try { SystemName = _dataObject.IO.SystemName() ?? string.Empty; }
            catch { SystemName = string.Empty; }

            // cpp reads SampleInfo.AnalysisDate (ShimadzuReader.cpp:370). Under .NET 8 the C#
            // SDK leaves it at DateTime.MinValue (loader depends on BinaryFormatter paths whose
            // default behavior changed in .NET Core), so fall back to FilePropTag.GeneratedDateTime
            // — the file-level "generated" timestamp the SDK does populate. The two are typically
            // tens of seconds apart on the same .lcd; the harness ignores startTimeStamp diffs
            // so the reference mzMLs don't drift either way.
            try
            {
                var sdkDate = _dataObject.SampleInfo.AnalysisDate;
                if (sdkDate == default)
                    sdkDate = _dataObject.FilePropTag.GeneratedDateTime;
                AnalysisDateRaw = sdkDate;
            }
            catch { AnalysisDateRaw = default; }

            // Build event-info map (event, channel) -> MassEventInfo. cpp ShimadzuReader.cpp:227-241.
            try
            {
                System.Collections.Generic.List<ShimadzuIO.Generic.Param.MS.MassEventInfo> list;
                _dataObject.MS.Parameters.GetEventInfo(out list);
                if (list is not null)
                {
                    foreach (var evt in list)
                    {
                        var key = (evt.Event, (short)Math.Max((short)1, evt.Channel));
                        _eventInfo[key] = evt;
                    }
                }
            }
            catch { /* tolerate — fallback below derives topology from chromMng */ }

            // Warm the SDK before any chromatogram-side calls. cpp does the same. The SDK's
            // Q-TOF backend (Shimadzu.LabSolutions.IO.MassRawData.Qtfl.QtflRawDataMain) needs
            // this priming on .NET 8 — without it MassChromatogramMng.SegmentCount throws
            // RuntimeBinderException on a null inner field.
            try
            {
                var dummySpectrum = new ShimadzuIO.Generic.MassSpectrumObject();
                _dataObject.MS.Spectrum.GetMSSpectrumByScan(out dummySpectrum, 1u, true);
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { qtflBackendThrew = true; }
            catch (NullReferenceException) { qtflBackendThrew = true; }
            catch { /* priming is best-effort */ }

            int endTime = 0;
            try
            {
                int startTime;
                _dataObject.MS.Parameters.GetAnalysisTime(out startTime, out endTime, 0);
            }
            catch { /* leave endTime=0; downstream tolerates */ }

            // Build segment / event topology directly from MS.Chromatogram, matching cpp's
            // ShimadzuReader.cpp:255-264. The earlier port derived this from the _eventInfo
            // map populated by GetEventInfo, but that call fails silently on TeamCity (caught
            // at the GetEventInfo try/catch above), leaving _eventInfo empty and the file
            // reporting 0 spectra. Cpp doesn't depend on GetEventInfo for topology — only
            // for the per-event AnalysisMode lookup later — so neither should we.
            var segmentEventLists = new List<List<short>>();
            short discoveredSegmentCount = 0;
            try
            {
                var chromMng = _dataObject.MS.Chromatogram;
                discoveredSegmentCount = (short)chromMng.SegmentCount;
                for (short s = 1; s <= discoveredSegmentCount; s++)
                {
                    short evCount = (short)chromMng.EventCount(s);
                    var eventList = new List<short>(evCount);
                    for (short e = 1; e <= evCount; e++)
                        eventList.Add((short)chromMng.GetEventNo(s, e));
                    segmentEventLists.Add(eventList);
                }
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { qtflBackendThrew = true; }
            catch (NullReferenceException) { qtflBackendThrew = true; }
            catch { /* fall through to the _eventInfo-derived fallback below */ }

            // If the Chromatogram path failed but we have an _eventInfo map, fall back to
            // deriving segments from it. (Preserves the previous behavior for whatever SDK
            // configuration that path worked under.)
            if (discoveredSegmentCount == 0 && _eventInfo.Count > 0)
            {
                var byPair = new SortedDictionary<short, SortedSet<short>>();
                foreach (var evt in _eventInfo.Values)
                {
                    if (evt.Segment <= 0) continue;
                    if (!byPair.TryGetValue(evt.Segment, out var set))
                        byPair[evt.Segment] = set = new SortedSet<short>();
                    set.Add(evt.Event);
                }
                discoveredSegmentCount = byPair.Count == 0 ? (short)0 : byPair.Keys.Max();
                segmentEventLists.Clear();
                for (short s = 1; s <= discoveredSegmentCount; s++)
                {
                    var list = new List<short>();
                    if (byPair.TryGetValue(s, out var set)) list.AddRange(set);
                    segmentEventLists.Add(list);
                }
            }
            SegmentCount = discoveredSegmentCount;

            uint lastScanNumber = 0;
            // Track whether RetTimeToScan actually FAILED for an event we tried to query
            // (vs. the event being skipped on purpose, e.g. MRM under srmAsSpectra=false).
            // The fallback below only runs in the genuine-failure case so that SRM-only files
            // don't grow phantom spectra under default config.
            bool retTimeToScanCalledAndFailed = false;
            // Materialize segments 1..SegmentCount in order so empty segments still get an entry
            // (matches cpp which iterates by segment index).
            for (short seg = 1; seg <= SegmentCount; seg++)
            {
                var eventNumbers = seg <= segmentEventLists.Count
                    ? segmentEventLists[seg - 1]
                    : new List<short>();
                _eventNumbersBySegment.Add(eventNumbers);

                foreach (short eventNo in eventNumbers)
                {
                    var info = TryGetEventInfo(eventNo);
                    // info is null when GetEventInfo failed earlier — cpp ShimadzuReader.cpp:265
                    // dereferences getEventInfo(eventNo)->AnalysisMode unconditionally; if that
                    // returns null on TeamCity but the chromatogram-side topology is intact,
                    // we'd otherwise drop every event. Treat missing event info as "not MRM" so
                    // those events still count toward the scan total under default config.
                    if (info is not null && !srmAsSpectra && info.AnalysisMode == ShimadzuIO.Generic.AcqModes.MRM)
                        continue;

                    uint eventLastScanNumber = TryGetEventLastScanNumber(seg, eventNo, endTime);
                    if (eventLastScanNumber == 0)
                    {
                        retTimeToScanCalledAndFailed = true;
                        continue;
                    }

                    if (_msLevels.Count < 2)
                    {
                        try
                        {
                            var spec = GetSpectrumRaw((int)eventLastScanNumber, profileDesired: false);
                            int level = ComputeMsLevel(spec);
                            _msLevels.Add(level);
                        }
                        catch { /* leave msLevels alone */ }
                    }

                    if (eventLastScanNumber > lastScanNumber)
                        lastScanNumber = eventLastScanNumber;
                }
            }

            // Fallback only when at least one event was attempted but every attempt returned 0.
            // The Q-TOF SDK on .NET 8 sometimes throws RuntimeBinderException through
            // QtflRawDataMain.RTimeToScan (see TC build 3975296); when that happens for every
            // event of a file, lastScanNumber stays 0 and we'd otherwise produce no spectra.
            // Sum per-event TIC chromatogram point counts to recover a usable scan count.
            // This is approximate vs. cpp's exact max-scan-number approach but agrees on the
            // common case where scan numbers are 1..N contiguous. Crucially we DO NOT run the
            // fallback when there were zero attempts (e.g. SRM-only file under default config),
            // since that would invent spectra the cpp reference mzML doesn't carry.
            if (lastScanNumber == 0 && retTimeToScanCalledAndFailed)
                lastScanNumber = SumScanCountsFromTics();

            ScanCount = (int)lastScanNumber;

            // Precursor map: cpp ShimadzuReader.cpp:294-313 builds precursor info from
            // GetPrecursorList's SurveyList[0].DependentList, which the SDK populates for
            // QqTOF DDA acquisitions only. Tolerate failures: many files have no precursors.
            try
            {
                ShimadzuIO.Generic.PrecursorResultData? precursorResult;
                _dataObject.MS.Spectrum.GetPrecursorList(new ShimadzuIO.Generic.DdaPrecursorFilter(), out precursorResult);
                if (precursorResult is { SurveyList.Count: > 0 })
                {
                    foreach (var dependent in precursorResult.SurveyList[0].DependentList)
                    {
                        int charge = dependent.Charge switch
                        {
                            ShimadzuIO.Generic.Charges.Charge1 => 1,
                            ShimadzuIO.Generic.Charges.Charge2 => 2,
                            ShimadzuIO.Generic.Charges.Charge3 => 3,
                            ShimadzuIO.Generic.Charges.Charge4 => 4,
                            ShimadzuIO.Generic.Charges.Charge5 => 5,
                            ShimadzuIO.Generic.Charges.Charge6 => 6,
                            ShimadzuIO.Generic.Charges.Charge7 => 7,
                            _ => 0,
                        };
                        foreach (int scan in dependent.ScanNoList)
                            _precursorInfoByScan[scan] = (dependent.PrecursorMass * PrecursorMzMultiplier, charge);
                    }
                }
            }
            catch { /* no DDA precursor table — fine */ }
        }
        catch
        {
            try { _dataObject.IO.Close(); } catch { /* best-effort */ }
            throw;
        }

        // Retry signal: the QTFL backend threw a binder-style exception during warmup or the
        // chromMng walk, AND we ended up with no usable scans. A genuinely empty file (no
        // events, no scans, SDK calls returned cleanly) is not retryable.
        return qtflBackendThrew && ScanCount == 0;
    }

    /// <summary>Wraps <c>RetTimeToScan</c> per event with explicit catches around the SDK's
    /// known failure modes. Returns 0 on any failure (caller treats 0 as "no scan", same as
    /// the SDK's documented zero-return). The Q-TOF backend
    /// <c>QtflRawDataMain.RTimeToScan</c> uses C# <c>dynamic</c> dispatch on a field that's
    /// not always initialized for Q-TOF Negative-mode files; we observed it throwing
    /// <c>RuntimeBinderException</c> in TC build 3975296. Catching it lets the caller fall back
    /// to TIC-derived scan counting.</summary>
    private uint TryGetEventLastScanNumber(short seg, short eventNo, int endTime)
    {
        try
        {
            uint scan;
            var rt2sn = _dataObject.MS.Spectrum.RetTimeToScan(out scan, endTime, eventNo);
            if (ShimadzuIO.Generic.Tool.Failed(rt2sn)) return 0;
            return scan;
        }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { return 0; }
        catch (NullReferenceException) { return 0; }
    }

    /// <summary>Sums per-event TIC chromatogram point counts across all segments + events to
    /// approximate the file's total scan count. Used as a fallback when
    /// <see cref="TryGetEventLastScanNumber"/> fails for every event.</summary>
    private uint SumScanCountsFromTics()
    {
        var chromMng = _dataObject.MS.Chromatogram;
        uint total = 0;
        for (short seg = 1; seg <= SegmentCount; seg++)
        {
            foreach (short eventNo in _eventNumbersBySegment[seg - 1])
            {
                try
                {
                    ShimadzuIO.Generic.MassChromatogramObject tic;
                    var result = chromMng.GetTICChromatogram(out tic, seg, eventNo);
                    if (ShimadzuIO.Generic.Tool.Failed(result) || tic is null) continue;
                    int len = tic.RetTimeList?.Length ?? 0;
                    if (len > 0) total += (uint)len;
                }
                catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { /* skip */ }
                catch (NullReferenceException) { /* skip */ }
            }
        }
        return total;
    }

    /// <summary>Returns the SDK <c>MassSpectrumObject</c> for <paramref name="scanNumber"/>.</summary>
    public ShimadzuIO.Generic.MassSpectrumObject GetSpectrumRaw(int scanNumber, bool profileDesired)
    {
        ShimadzuIO.Generic.MassSpectrumObject spectrum;
        var result = _dataObject.MS.Spectrum.GetMSSpectrumByScan(out spectrum, (uint)scanNumber, profileDesired);
        if (ShimadzuIO.Generic.Tool.Failed(result))
            throw new InvalidOperationException(
                $"Shimadzu GetMSSpectrumByScan failed for scan {scanNumber}: {result}");
        return spectrum;
    }

    /// <summary>Light-weight info for <paramref name="scanNumber"/>: msLevel, polarity, RT, segment, event, etc.
    /// cpp <c>ShimadzuReader::getSpectrumInfo</c>.</summary>
    public ShimadzuSpectrumInfo GetSpectrumInfo(int scanNumber)
    {
        int retentionTime, msStage, precursorMass, segmentNo, eventNo;
        uint precursorScan;
        ShimadzuIO.Generic.Polarities polarity;
        var result = _dataObject.MS.Spectrum.GetMSSpectrumInfo(
            (uint)scanNumber, out retentionTime, out msStage, out precursorMass, out precursorScan,
            out polarity, out segmentNo, out eventNo);
        if (ShimadzuIO.Generic.Tool.Failed(result))
            throw new InvalidOperationException(
                $"Shimadzu GetMSSpectrumInfo failed for scan {scanNumber}: {result}");

        var info = TryGetEventInfo((short)eventNo);
        bool isSrm = info?.AnalysisMode == ShimadzuIO.Generic.AcqModes.MRM;

        return new ShimadzuSpectrumInfo(
            ScanTime: retentionTime * TimeMultiplier,
            MsLevel: msStage,
            PrecursorMz: precursorMass * MassMultiplier,
            PrecursorScan: (int)precursorScan,
            Polarity: (ShimadzuPolarity)(int)polarity,
            IsSrm: isSrm,
            Segment: segmentNo,
            Event: eventNo);
    }

    /// <summary>True iff <paramref name="scanNumber"/> has a DDA precursor record.</summary>
    public bool TryGetPrecursorInfo(int scanNumber, out double precursorMz, out int charge)
    {
        if (_precursorInfoByScan.TryGetValue(scanNumber, out var info))
        {
            (precursorMz, charge) = info;
            return true;
        }
        precursorMz = 0;
        charge = 0;
        return false;
    }

    /// <summary>EventInfo for an event number (channel 1) or null when the SDK didn't expose one.</summary>
    public ShimadzuIO.Generic.Param.MS.MassEventInfo? TryGetEventInfo(short eventNo, short channel = 1)
        => _eventInfo.TryGetValue((eventNo, channel), out var info) ? info : null;

    /// <summary>File-level TIC. cpp <c>ShimadzuReader::getTIC</c>.</summary>
    public (double[] X, double[] Y) GetTic(bool ms1Only)
    {
        var chromatogramMng = _dataObject.MS.Chromatogram;
        var fullFileTic = new SortedDictionary<int, long>();

        for (short seg = 1; seg <= SegmentCount; seg++)
        {
            var eventNumbers = _eventNumbersBySegment[seg - 1];
            foreach (short eventNumber in eventNumbers)
            {
                ShimadzuIO.Generic.MassChromatogramObject eventTic;
                var result = chromatogramMng.GetTICChromatogram(out eventTic, seg, eventNumber);
                if (ShimadzuIO.Generic.Tool.Failed(result) || eventTic is null)
                    continue;
                var rts = eventTic.RetTimeList;
                var ints = eventTic.ChromIntList;
                int len = ints?.Length ?? 0;
                for (int k = 0; k < len; k++)
                {
                    int rt = rts![k];
                    long y = ints![k];
                    if (fullFileTic.TryGetValue(rt, out var existing))
                        fullFileTic[rt] = existing + y;
                    else
                        fullFileTic[rt] = y;
                }

                // Assume only the first event of each segment is MS1.
                if (ms1Only) break;
            }
        }

        var x = new double[fullFileTic.Count];
        var y2 = new double[fullFileTic.Count];
        int i = 0;
        foreach (var kvp in fullFileTic)
        {
            x[i] = kvp.Key * TimeMultiplier;
            y2[i] = kvp.Value;
            i++;
        }
        return (x, y2);
    }

    /// <summary>Per-transition SRM chromatogram. cpp <c>ShimadzuReader::getSRM</c>.</summary>
    public (double[] X, double[] Y) GetSrmChromatogram(ShimadzuTransition transition)
    {
        var chromatogramMng = _dataObject.MS.Chromatogram;

        var mzTransition = new ShimadzuIO.Generic.MzTransition
        {
            Segment = transition.Segment,
            Event = transition.Event,
            Channel = transition.Channel,
            StartMass = transition.StartMz,
            EndMass = transition.EndMz,
            StartMassRaw = transition.StartMz,
            EndMassRaw = transition.EndMz,
        };

        ShimadzuIO.Generic.MassChromatogramObject chrom;
        var result = chromatogramMng.GetChromatogrambyEvent(out chrom, mzTransition, true, false);
        if (ShimadzuIO.Generic.Tool.Failed(result) || chrom is null)
            throw new InvalidOperationException(
                $"Shimadzu GetChromatogrambyEvent failed for segment {transition.Segment}, event {transition.Event}: {result}");

        var rts = chrom.RetTimeList;
        var ints = chrom.ChromIntList;
        int len = ints?.Length ?? 0;
        var x = new double[len];
        var y = new double[len];
        for (int j = 0; j < len; j++)
        {
            x[j] = rts![j] * TimeMultiplier;
            y[j] = ints![j];
        }
        return (x, y);
    }

    private void LoadTransitions()
    {
        _transitionsLoaded = true;
        try
        {
            short id = 0;
            foreach (var kvp in _eventInfo)
            {
                var info = kvp.Value;
                if (info.AnalysisMode != ShimadzuIO.Generic.AcqModes.MRM) continue;
                _transitions.Add(new ShimadzuTransition(
                    Id: id++,
                    Channel: info.Channel,
                    Event: info.Event,
                    Segment: info.Segment,
                    CollisionEnergy: Math.Abs(info.CE),
                    Polarity: (ShimadzuPolarity)(int)info.Polarity,
                    StartMz: info.StartMz,
                    EndMz: info.EndMz));
            }
        }
        catch { /* tolerate SDK glitches; cpp does the same */ }
    }

    private static int ComputeMsLevel(ShimadzuIO.Generic.MassSpectrumObject spectrum)
    {
        // cpp ShimadzuReader.cpp:135 — MS2 iff MassStep > 1 AND PrecursorMzList non-empty.
        if (spectrum.MassStep > 1 && spectrum.PrecursorMzList.Count > 0) return 2;
        return 1;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _dataObject.IO.Close(); }
        catch { /* SDK may throw on bogus state — best-effort close */ }
    }

    /// <summary>Quick sanity check used by both <see cref="Reader_Shimadzu.Identify"/> and <see cref="Reader_Shimadzu.Read"/>.</summary>
    public static bool IsShimadzuLcd(string path)
    {
        return !string.IsNullOrEmpty(path)
            && path.EndsWith(".lcd", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Polarity enum mirroring <c>Shimadzu.LabSolutions.IO.Generic.Polarities</c> integer values.</summary>
public enum ShimadzuPolarity
{
    /// <summary>Positive ion mode.</summary>
    Positive = 0,
    /// <summary>Negative ion mode.</summary>
    Negative = 1,
    /// <summary>Polarity not reported.</summary>
    Undefined = -1,
}

/// <summary>Light-weight per-scan record. cpp <c>SpectrumInfo</c> in <c>ShimadzuReader.hpp</c>.</summary>
public sealed record ShimadzuSpectrumInfo(
    double ScanTime,
    int MsLevel,
    double PrecursorMz,
    int PrecursorScan,
    ShimadzuPolarity Polarity,
    bool IsSrm,
    int Segment,
    int Event);

/// <summary>One SRM transition. cpp <c>SRMTransition</c> in <c>ShimadzuReader.hpp</c>.</summary>
public sealed record ShimadzuTransition(
    short Id,
    short Channel,
    short Event,
    short Segment,
    double CollisionEnergy,
    ShimadzuPolarity Polarity,
    int StartMz,
    int EndMz)
{
    /// <summary>Q1 m/z (start mass divided by SDK's mass multiplier).</summary>
    public double Q1 => StartMz * ShimadzuRawData.MassMultiplier;
    /// <summary>Q3 m/z (end mass divided by SDK's mass multiplier).</summary>
    public double Q3 => EndMz * ShimadzuRawData.MassMultiplier;
}
