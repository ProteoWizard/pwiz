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
            // The Thermo SDK tags CreationDate with Kind=Utc but the clock value is actually the
            // instrument's local time at acquisition — force Local, then convert to UTC to match
            // pwiz C++'s ISO-8601 "Z" output.
            var created = DateTime.SpecifyKind(Raw.FileHeader.CreationDate, DateTimeKind.Local);
            CreationDate = created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
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
    /// Reads (masses, intensities) for <paramref name="scanNumber"/>. Uses the centroid stream when
    /// the underlying analyzer is FTMS (already centroided by acquisition) and the segmented stream
    /// otherwise.
    /// </summary>
    public (double[] Masses, double[] Intensities) GetPeaks(int scanNumber, bool preferCentroid)
    {
        var filter = Raw.GetFilterForScanNumber(scanNumber);
        bool useCentroid = preferCentroid && filter.MassAnalyzer == MassAnalyzerType.MassAnalyzerFTMS;

        if (useCentroid)
        {
            var stream = Raw.GetCentroidStream(scanNumber, true);
            if (stream?.Masses is { } m && stream.Intensities is { } i)
                return (m, i);
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
