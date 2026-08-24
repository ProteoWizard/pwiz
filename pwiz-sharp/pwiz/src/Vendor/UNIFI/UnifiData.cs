namespace Pwiz.Vendor.UNIFI;

/// <summary>Identifies which Waters HTTP-API the input URL targets. Mirrors cpp
/// <c>UnifiData::RemoteApi</c> in <c>UnifiData.hpp:215-219</c>.</summary>
public enum RemoteApiType
{
    /// <summary>Classic UNIFI sample-result endpoint
    /// (<c>https://host:port/unifi/v1/sampleresults(GUID)?...</c>).</summary>
    Unifi,
    /// <summary>Newer waters_connect sample-result endpoint
    /// (<c>https://host:port/?sampleSetId=GUID&amp;injectionId=GUID&amp;...</c>).</summary>
    WatersConnect,
}

/// <summary>Scan polarity. Numeric values mirror the cpp enum (UnifiData.hpp:133-138)
/// so we can convert directly from the protobuf payload's int field once the data
/// layer is wired.</summary>
public enum UnifiPolarity
{
    /// <summary>Polarity not reported.</summary>
    Unknown = 0,
    /// <summary>Negative ion mode.</summary>
    Negative = 1,
    /// <summary>Positive ion mode.</summary>
    Positive = 2,
}

/// <summary>MSe energy level — Waters' alternating low/high collision energy mode.
/// Mirrors cpp <c>EnergyLevel</c> (UnifiData.hpp:140-145).</summary>
public enum UnifiEnergyLevel
{
    /// <summary>Energy level not reported.</summary>
    Unknown = 0,
    /// <summary>Low-energy MSe channel (precursor scan).</summary>
    Low = 1,
    /// <summary>High-energy MSe channel (fragment scan).</summary>
    High = 2,
}

/// <summary>One spectrum's worth of metadata + arrays returned by the data layer
/// (populated lazily once we move past Identify-only). Port of cpp
/// <c>UnifiSpectrum</c> (UnifiData.hpp:147-161).</summary>
public sealed class UnifiSpectrum
{
    /// <summary>1 for MS1, 2 for MSn (or 0 for non-MS / unknown).</summary>
    public int MsLevel { get; set; }

    /// <summary>Retention time in seconds.</summary>
    public double RetentionTime { get; set; }

    /// <summary>Scan polarity.</summary>
    public UnifiPolarity ScanPolarity { get; set; }

    /// <summary>MSe energy-level slot (Low / High / Unknown).</summary>
    public UnifiEnergyLevel EnergyLevel { get; set; }

    /// <summary>Drift time in milliseconds for ion-mobility data; 0 otherwise.</summary>
    public double DriftTime { get; set; }

    /// <summary>True when the m/z array is profile-mode.</summary>
    public bool DataIsContinuous { get; set; }

    /// <summary>(low, high) m/z scan window — function-level acquisition range.</summary>
    public (double Low, double High) ScanRange { get; set; }

    /// <summary>Length of the data arrays.</summary>
    public int ArrayLength { get; set; }

    /// <summary>m/z values.</summary>
    public double[] MzArray { get; set; } = Array.Empty<double>();

    /// <summary>Intensity values; same length as <see cref="MzArray"/>.</summary>
    public double[] IntensityArray { get; set; } = Array.Empty<double>();

    /// <summary>Per-bin drift times for combined-IMS spectra; empty for non-IMS data.</summary>
    public double[] DriftTimeArray { get; set; } = Array.Empty<double>();
}

/// <summary>Top-level identity of a chromatogram exposed by the API. Port of cpp
/// <c>UnifiChromatogramInfo</c> (UnifiData.hpp:163-176).</summary>
public class UnifiChromatogramInfo
{
    /// <summary>Per-channel chromatogram type.</summary>
    public ChromatogramType Type { get; set; }
    /// <summary>Q1 m/z for SRM/SIM; 0 otherwise.</summary>
    public double Q1 { get; set; }
    /// <summary>Q3 m/z for SRM; 0 otherwise.</summary>
    public double Q3 { get; set; }
    /// <summary>(start, end) acquisition time range, in seconds.</summary>
    public (double Start, double End) AcquiredTimeRange { get; set; }
    /// <summary>Polarity of the underlying scan function.</summary>
    public UnifiPolarity Polarity { get; set; }
    /// <summary>Index in the spectrum-info / channel listing.</summary>
    public int Index { get; set; }
    /// <summary>Display name (e.g. <c>"1: TOF MSe (50-1200) 4eV ESI+ - Low CE"</c>).</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Backend GUID for the channel (informational; round-trips into mzML id when useful).</summary>
    public string AltId { get; set; } = string.Empty;
}

/// <summary>Top-level chromatogram type returned by the API. Mirrors cpp
/// <c>UnifiChromatogramInfo::Type</c> (UnifiData.hpp:165).</summary>
public enum ChromatogramType
{
    /// <summary>Chromatogram type not reported.</summary>
    Unknown,
    /// <summary>Total ion current.</summary>
    TIC,
    /// <summary>Base peak intensity.</summary>
    BPI,
    /// <summary>UV detector trace.</summary>
    UV,
    /// <summary>Fluorescence detector trace.</summary>
    FLR,
    /// <summary>Infrared detector trace.</summary>
    IR,
    /// <summary>NMR detector trace.</summary>
    NMR,
    /// <summary>Multiple-reaction monitoring transition.</summary>
    MRM,
    /// <summary>Selected-ion monitoring trace.</summary>
    SIM,
}

/// <summary>Adds binary arrays to <see cref="UnifiChromatogramInfo"/>. Mirrors cpp
/// <c>UnifiChromatogram</c> (UnifiData.hpp:178-183).</summary>
public sealed class UnifiChromatogram : UnifiChromatogramInfo
{
    /// <summary>Length of <see cref="TimeArray"/> / <see cref="IntensityArray"/>.</summary>
    public int ArrayLength { get; set; }
    /// <summary>Retention times in seconds.</summary>
    public double[] TimeArray { get; set; } = Array.Empty<double>();
    /// <summary>Intensity values aligned to <see cref="TimeArray"/>.</summary>
    public double[] IntensityArray { get; set; } = Array.Empty<double>();
}
