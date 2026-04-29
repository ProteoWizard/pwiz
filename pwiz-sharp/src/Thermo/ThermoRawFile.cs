using ThermoFisher.CommonCore.Data;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.FilterEnums;
using ThermoFisher.CommonCore.Data.Interfaces;
using ThermoFisher.CommonCore.RawFileReader;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Thermo;

/// <summary>
/// Thin managed wrapper around a thread-owned Thermo <c>IRawDataPlus</c>. C# equivalent of
/// pwiz::vendor_api::Thermo::RawFile (<c>RawFile.h/.cpp</c>).
/// </summary>
public sealed class ThermoRawFile : IDisposable
{
    private readonly IRawFileThreadManager _manager;
    private bool _disposed;

    public string Filename { get; }
    public IRawDataPlus Raw { get; }
    public int FirstScan { get; }
    public int LastScan { get; }
    public int ScanCount => LastScan - FirstScan + 1;
    public string RunId { get; }
    public string CreationDate { get; }

    public ThermoRawFile(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        if (!File.Exists(filename))
            throw new FileNotFoundException("Thermo RAW file not found", filename);
        Filename = filename;

        _manager = RawFileReaderAdapter.ThreadedFileFactory(filename);
        Raw = _manager.CreateThreadAccessor();
        Raw.IncludeReferenceAndExceptionData = true;

        if (Raw.IsError)
            throw new InvalidDataException($"Thermo RAW file reports IsError: {filename}");
        if (Raw.InAcquisition)
            throw new InvalidDataException($"Thermo RAW file is still being acquired: {filename}");
        if (Raw.GetInstrumentCountOfType(Device.MS) == 0)
            throw new InvalidDataException($"Thermo RAW file has no MS controllers: {filename}");

        Raw.SelectInstrument(Device.MS, 1);

        var hdr = Raw.RunHeaderEx;
        FirstScan = hdr.FirstSpectrum;
        LastScan = hdr.LastSpectrum;
        RunId = Path.GetFileNameWithoutExtension(filename);
        try
        {
            // pwiz C++ emits the instrument's local clock value verbatim with a "Z" suffix
            // (strictly incorrect per ISO-8601 but long-standing pwiz behavior — matches
            // the reference mzML fixtures byte-for-byte).
            CreationDate = Raw.FileHeader.CreationDate.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch { CreationDate = string.Empty; }
    }

    public double RetentionTimeMinutes(int scanNumber) =>
        Raw.RetentionTimeFromScanNumber(scanNumber);

    public int MsLevel(int scanNumber)
    {
        var order = Raw.GetFilterForScanNumber(scanNumber).MSOrder;
        return order switch
        {
            MSOrderType.Ms => 1,
            MSOrderType.Ms2 => 2,
            MSOrderType.Ms3 => 3,
            MSOrderType.Ms4 => 4,
            MSOrderType.Ms5 => 5,
            MSOrderType.Ms6 => 6,
            MSOrderType.Ms7 => 7,
            MSOrderType.Ms8 => 8,
            MSOrderType.Ms9 => 9,
            MSOrderType.Ms10 => 10,
            MSOrderType.Nl => 2,
            MSOrderType.Ng => 2,
            MSOrderType.Par => 2,
            _ => 1,
        };
    }

    public string FilterString(int scanNumber) =>
        Raw.GetFilterForScanNumber(scanNumber)?.ToString() ?? string.Empty;

    /// <summary>Diagnostic: dump trailer key/value pairs matching one of the given substrings.</summary>
    public string DebugDumpTrailers(int scanNumber, params string[] labelSubstrings)
    {
        var sb = new System.Text.StringBuilder();
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            var hdr = Raw.GetTrailerExtraHeaderInformation();
            for (int i = 0; i < hdr.Length; i++)
            {
                string lbl = hdr[i].Label;
                bool keep = labelSubstrings.Length == 0;
                foreach (var s in labelSubstrings)
                    if (lbl.Contains(s, StringComparison.OrdinalIgnoreCase)) { keep = true; break; }
                if (!keep) continue;
                object? v = null;
                try { v = Raw.GetTrailerExtraValue(scanNumber, i); } catch { }
                sb.AppendLine(ci, $"  trailer[{i}] '{lbl}' = '{v}'");
            }
        }
        catch (Exception ex) { sb.AppendLine(ci, $"  trailer err: {ex.Message}"); }
        return sb.ToString();
    }

    /// <summary>Diagnostic: filter isolation widths per index for a scan.</summary>
    public double[] FilterIsolationWidths(int scanNumber)
    {
        var f = Raw.GetFilterForScanNumber(scanNumber);
        var result = new double[f.MassCount];
        for (int i = 0; i < f.MassCount; i++)
        {
            try { result[i] = f.GetIsolationWidth(i); }
            catch { result[i] = double.NaN; }
        }
        return result;
    }

    /// <summary>
    /// Reads (masses, intensities) for <paramref name="scanNumber"/>. When
    /// <paramref name="preferCentroid"/> is true, returns centroided peaks using whichever
    /// Thermo API matches the scan's analyzer: <c>GetCentroidStream</c> for FTMS profile
    /// scans, <c>Scan.ToCentroid(Scan.FromFile(...))</c> for non-FTMS profile scans. Scans
    /// already acquired as centroid or with no vendor centroider available fall through to
    /// the segmented stream.
    /// </summary>
    public (double[] Masses, double[] Intensities) GetPeaks(int scanNumber, bool preferCentroid)
    {
        if (preferCentroid)
        {
            var filter = Raw.GetFilterForScanNumber(scanNumber);
            if (filter.MassAnalyzer == MassAnalyzerType.MassAnalyzerFTMS)
            {
                // FTMS: the centroid stream is populated during acquisition.
                var stream = Raw.GetCentroidStream(scanNumber, true);
                if (stream?.Masses is { } m && stream.Intensities is { } i)
                    return (m, i);
            }
            else if (filter.ScanData == ScanDataType.Profile)
            {
                // Non-FTMS profile (e.g. ITMS): use Thermo's CommonCore centroider, which
                // wraps the same XRawfile label-data peak detector pwiz C++ uses via
                // GetLabelData. ToCentroid returns a Scan whose SegmentedScan holds the
                // centroided peaks (CentroidScan stays null — it's reserved for FTMS).
                var profile = Scan.FromFile(Raw, scanNumber, System.Globalization.CultureInfo.InvariantCulture);
                if (profile is not null)
                {
                    var centroid = Scan.ToCentroid(profile);
                    if (centroid?.SegmentedScan is { Positions: { Length: > 0 } p2, Intensities: var intensities })
                        return (p2, intensities!);
                }
            }
        }

        var seg = Raw.GetSegmentedScanFromScanNumber(scanNumber, null);
        return (seg.Positions ?? Array.Empty<double>(), seg.Intensities ?? Array.Empty<double>());
    }

    /// <summary>Native id string in pwiz's Thermo format (MS controller).</summary>
    public static string NativeId(int scanNumber) =>
        $"controllerType=0 controllerNumber=1 scan={scanNumber}";

    /// <summary>
    /// Native id string for any controller. The controllerType integer matches the cpp
    /// <c>ControllerType</c> enum (Win64): 0=MS, 1=Analog, 2=ADCard, 3=UV, 4=PDA, 5=Other.
    /// </summary>
    public static string NativeId(int scanNumber, Device controller, int controllerNumber) =>
        $"controllerType={(int)controller} controllerNumber={controllerNumber} scan={scanNumber}";

    /// <summary>Number of PDA controllers on the file (0 if none).</summary>
    public int PdaControllerCount
    {
        get { try { return Raw.GetInstrumentCountOfType(Device.Pda); } catch { return 0; } }
    }

    // ---- Instrument method isolation-width lookup ----
    // Older LTQ-class instruments often return 1.0 (or 0) from filter.GetIsolationWidth even
    // when the real method width was configured as e.g. 2.0. pwiz C++ parses the embedded
    // instrument-method text for "Isolation Width:" / "MS{n} Isolation Width:" lines and uses
    // them to fill in the gap — port of RawFile.cpp parseInstrumentMethod (1921+).
    private Dictionary<int, Dictionary<int, double>>? _widthBySegmentAndEvent;
    private Dictionary<int, Dictionary<int, double>>? _defaultWidthBySegmentAndMsLevel;

    /// <summary>Per-scan-event isolation width parsed from the instrument method; 0 if unknown.</summary>
    public double GetMethodIsolationWidth(int scanSegment, int scanEvent)
    {
        EnsureMethodParsed();
        return _widthBySegmentAndEvent!.TryGetValue(scanSegment, out var byEvent)
            && byEvent.TryGetValue(scanEvent, out var w)
                ? w : 0.0;
    }

    /// <summary>Per-msLevel default isolation width parsed from the instrument method; 0 if unknown.</summary>
    public double GetMethodDefaultIsolationWidth(int scanSegment, int msLevel)
    {
        EnsureMethodParsed();
        return _defaultWidthBySegmentAndMsLevel!.TryGetValue(scanSegment, out var byLvl)
            && byLvl.TryGetValue(msLevel, out var w)
                ? w : 0.0;
    }

    /// <summary>Diagnostic: returns a summary of parsed isolation-width lookups.</summary>
    public string DebugMethodIsolationWidths()
    {
        EnsureMethodParsed();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("widthBySegmentAndEvent:");
        foreach (var (seg, byEvt) in _widthBySegmentAndEvent!)
            foreach (var (evt, w) in byEvt)
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  seg={seg} evt={evt} width={w}");
        sb.AppendLine("defaultWidthBySegmentAndMsLevel:");
        foreach (var (seg, byLvl) in _defaultWidthBySegmentAndMsLevel!)
            foreach (var (lvl, w) in byLvl)
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  seg={seg} msLevel={lvl} width={w}");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the 1-based <c>(SegmentNumber, ScanEventNumber)</c> for <paramref name="scanNumber"/>,
    /// matching the numbering used by the instrument-method text (where "Segment 1 Information"
    /// and " 1: scan event ..." are 1-indexed). CommonCore's <c>ScanStatistics</c> reports
    /// both as 0-based, so we add 1 to align with the method parser's keys.
    /// </summary>
    public (int SegmentNumber, int ScanEventNumber) GetScanSegmentAndEvent(int scanNumber)
    {
        var s = Raw.GetScanStatsForScanNumber(scanNumber);
        return (s.SegmentNumber + 1, s.ScanEventNumber + 1);
    }

    private void EnsureMethodParsed()
    {
        if (_widthBySegmentAndEvent is not null) return;
        _widthBySegmentAndEvent = new Dictionary<int, Dictionary<int, double>>();
        _defaultWidthBySegmentAndMsLevel = new Dictionary<int, Dictionary<int, double>>();

        // Concatenate instrument methods + status log (pwiz C++ does both).
        var sb = new System.Text.StringBuilder();
        try
        {
            int count = Raw.InstrumentMethodsCount;
            for (int i = 0; i < count; i++)
            {
                string? m = Raw.GetInstrumentMethod(i);
                if (!string.IsNullOrEmpty(m)) { sb.Append(m); sb.Append('\n'); }
            }
        }
        catch { /* Method fetch can fail on some files. */ }
        try
        {
            var statusLog = Raw.GetStatusLogForRetentionTime(0);
            if (statusLog?.Labels is { } labels && statusLog.Values is { } values)
            {
                int n = Math.Min(labels.Length, values.Length);
                for (int i = 0; i < n; i++)
                {
                    sb.Append(labels[i]); sb.Append(' '); sb.Append(values[i]); sb.Append('\n');
                }
            }
        }
        catch { }

        ParseInstrumentMethodText(sb.ToString(), _widthBySegmentAndEvent, _defaultWidthBySegmentAndMsLevel);
    }

    // Regex patterns ported from RawFile.cpp:1939-1949. Kept internal + static for cheap init.
    private static readonly System.Text.RegularExpressions.Regex s_segmentRegex =
        new(@"^\s*Segment (\d+) Information\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex s_scanEventRegex =
        new(@"^\s*(\d+):.*$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex s_isolationWidthRegex =
        new(@"^\s*Isolation Width:\s*(\S+)\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex s_isoWRegex =
        new(@"^\s*MS.*:.*\s+IsoW\s+(\S+)\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex s_repeatedEventRegex =
        new(@"^\s*Scan Event (\d+) repeated for top (\d+)\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex s_defaultIsoWidthRegex =
        new(@"^\s*MS(\d+) Isolation Width:\s*(\S+)\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex s_defaultIsoWindowRegex =
        new(@"^\s*Isolation Window \(m/z\) =\s*(\S+)\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static void ParseInstrumentMethodText(
        string text,
        Dictionary<int, Dictionary<int, double>> widthBySegmentAndEvent,
        Dictionary<int, Dictionary<int, double>> defaultWidthBySegmentAndMsLevel)
    {
        int segment = 1, scanEvent = 0;
        bool inScanEventDetails = false;
        bool inDataDependentSettings = false;
        var ci = System.Globalization.CultureInfo.InvariantCulture;

        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            System.Text.RegularExpressions.Match m;

            if ((m = s_segmentRegex.Match(line)).Success)
            {
                segment = int.Parse(m.Groups[1].Value, ci);
                continue;
            }

            if (line.Contains("Scan Event Details", StringComparison.OrdinalIgnoreCase))
            {
                inScanEventDetails = true;
                continue;
            }

            if (inScanEventDetails)
            {
                if ((m = s_scanEventRegex.Match(line)).Success)
                {
                    scanEvent = int.Parse(m.Groups[1].Value, ci);
                    continue;
                }

                if ((m = s_isolationWidthRegex.Match(line)).Success
                    || (m = s_isoWRegex.Match(line)).Success)
                {
                    if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, ci, out double w))
                    {
                        if (!widthBySegmentAndEvent.TryGetValue(segment, out var dict))
                            widthBySegmentAndEvent[segment] = dict = new Dictionary<int, double>();
                        dict[scanEvent] = w;
                    }
                    continue;
                }

                if ((m = s_repeatedEventRegex.Match(line)).Success)
                {
                    int baseEvent = int.Parse(m.Groups[1].Value, ci);
                    int repeatCount = int.Parse(m.Groups[2].Value, ci);
                    if (widthBySegmentAndEvent.TryGetValue(segment, out var dict)
                        && dict.TryGetValue(baseEvent, out var baseWidth))
                    {
                        for (int i = baseEvent + 1; i < baseEvent + repeatCount; i++)
                            dict[i] = baseWidth;
                    }
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) inScanEventDetails = false;
            }

            if (line.Contains("Data Dependent Settings", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Scan DIAScan", StringComparison.OrdinalIgnoreCase))
            {
                inDataDependentSettings = true;
                continue;
            }

            if (inDataDependentSettings)
            {
                if ((m = s_defaultIsoWidthRegex.Match(line)).Success)
                {
                    if (int.TryParse(m.Groups[1].Value, out int lvl)
                        && double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float, ci, out double w))
                    {
                        if (!defaultWidthBySegmentAndMsLevel.TryGetValue(segment, out var dict))
                            defaultWidthBySegmentAndMsLevel[segment] = dict = new Dictionary<int, double>();
                        dict[lvl] = w;
                    }
                    continue;
                }

                if ((m = s_defaultIsoWindowRegex.Match(line)).Success)
                {
                    if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, ci, out double w))
                    {
                        if (!defaultWidthBySegmentAndMsLevel.TryGetValue(segment, out var dict))
                            defaultWidthBySegmentAndMsLevel[segment] = dict = new Dictionary<int, double>();
                        dict[2] = w;
                    }
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) inDataDependentSettings = false;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _manager.Dispose();
    }
}
