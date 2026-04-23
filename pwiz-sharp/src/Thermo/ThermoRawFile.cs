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

    /// <summary>Native id string in pwiz's Thermo format.</summary>
    public static string NativeId(int scanNumber) =>
        $"controllerType=0 controllerNumber=1 scan={scanNumber}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _manager.Dispose();
    }
}
