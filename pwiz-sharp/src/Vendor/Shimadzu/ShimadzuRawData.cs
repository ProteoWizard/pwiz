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

    private readonly ShimadzuIO.Data.DataObject _dataObject;
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

    /// <summary>Acquisition timestamp (UTC).</summary>
    public DateTime AnalysisDateUtc { get; private set; }

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

        _dataObject = new ShimadzuIO.Data.DataObject();
        var loadResult = _dataObject.IO.LoadData(lcdPath);
        if (ShimadzuIO.Generic.Tool.Failed(loadResult))
            throw new InvalidDataException($"Shimadzu LoadData failed: {loadResult}");

        try
        {
            try { SystemName = _dataObject.IO.SystemName() ?? string.Empty; }
            catch { SystemName = string.Empty; }

            // cpp reads SampleInfo.AnalysisDate.ToUniversalTime() (ShimadzuReader.cpp:370). Under
            // .NET 8 the C# SDK leaves SampleInfo.AnalysisDate at DateTime.MinValue (its loader
            // depends on BinaryFormatter paths whose default behavior changed in .NET Core), so
            // also try FilePropTag.GeneratedDateTime — the file-level "generated" timestamp the
            // SDK does populate. The cpp reference mzMLs were written with the cpp value (often
            // tens of seconds off from GeneratedDateTime), so the Shimadzu test config sets
            // IgnoreStartTimeStamp; keeping the fallback means production msconvert-sharp output
            // still has *a* timestamp instead of nothing.
            try
            {
                var sdkDate = _dataObject.SampleInfo.AnalysisDate;
                if (sdkDate == default)
                    sdkDate = _dataObject.FilePropTag.GeneratedDateTime;
                AnalysisDateUtc = sdkDate == default ? default : sdkDate.ToUniversalTime();
            }
            catch { AnalysisDateUtc = default; }

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
            catch { /* mirrors cpp: tolerate failure */ }

            // Build segment / event topology and discover ScanCount + msLevels by walking
            // each (segment, event) and asking the SDK for the highest scan number reachable
            // before endTime. cpp ShimadzuReader.cpp:244-292.
            var chromatogramMng = _dataObject.MS.Chromatogram;
            try
            {
                var dummySpectrum = new ShimadzuIO.Generic.MassSpectrumObject();
                _dataObject.MS.Spectrum.GetMSSpectrumByScan(out dummySpectrum, 1u, true);
            }
            catch { /* warming the SDK; cpp does the same */ }

            int startTime, endTime;
            _dataObject.MS.Parameters.GetAnalysisTime(out startTime, out endTime, 0);

            SegmentCount = chromatogramMng.SegmentCount;
            uint lastScanNumber = 0;
            for (short seg = 1; seg <= SegmentCount; seg++)
            {
                var eventNumbers = new List<short>();
                short eventCount = chromatogramMng.EventCount(seg);
                for (short j = 1; j <= eventCount; j++)
                {
                    short eventNo = chromatogramMng.GetEventNo(seg, j);
                    eventNumbers.Add(eventNo);

                    var info = TryGetEventInfo(eventNo);
                    if (info is null) continue;
                    if (!srmAsSpectra && info.AnalysisMode == ShimadzuIO.Generic.AcqModes.MRM)
                        continue;

                    uint eventLastScanNumber;
                    var rt2sn = _dataObject.MS.Spectrum.RetTimeToScan(out eventLastScanNumber, endTime, eventNo);
                    if (ShimadzuIO.Generic.Tool.Failed(rt2sn) || eventLastScanNumber == 0)
                        continue;

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
                _eventNumbersBySegment.Add(eventNumbers);
            }
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
