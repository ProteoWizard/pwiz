namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// One MS scan function (channel) in a UNIFI sample result. C# port of cpp
/// <c>UnifiData::Impl::FunctionInfo</c> (UnifiData.cpp:838-869).
/// </summary>
/// <remarks>
/// Populated from the <c>/spectrumInfos</c> response: each entry there with
/// <c>detectorType == "MS"</c> and <c>isRetentionData == true</c> becomes one of these.
/// The fetch-per-function "totalNumberOfSpectra" lookup at <c>/spectrumInfos/{id}/data</c>
/// fills <see cref="NumberOfSpectra"/>.
/// </remarks>
public sealed class UnifiFunctionInfo
{
    /// <summary>Zero-based position in the discovery walk — the index used as the function
    /// number for filtering chromatograms by function (cpp ChromatogramList_UNIFI.cpp:107).</summary>
    public required int Index { get; init; }
    /// <summary>UNIFI's GUID for the function (used to build the per-function data URL).</summary>
    public required string Id { get; init; }
    /// <summary>True when the function is centroid-only (otherwise continuum data is available).</summary>
    public bool IsCentroidData { get; init; }
    /// <summary>True for retention-bound MS data (false would be a static / "snapshot" function
    /// — currently filtered out at parse time).</summary>
    public bool IsRetentionData { get; init; }
    /// <summary>True when the function carries ion-mobility bins. cpp drives the
    /// <c>numSpectra * 200</c> bin multiplication off this flag.</summary>
    public bool IsIonMobilityData { get; init; }
    /// <summary>True when the function has CCS calibration data on the server.</summary>
    public bool HasCCSCalibration { get; init; }
    /// <summary>Acquired m/z range low / high.</summary>
    public double LowMass { get; init; }
    /// <summary>Acquired m/z range high.</summary>
    public double HighMass { get; init; }
    /// <summary>MSe energy slot. <see cref="UnifiEnergyLevel.Unknown"/> for non-MSe functions
    /// (currently filtered out — UNIFI's API doesn't expose chunk download for them).</summary>
    public UnifiEnergyLevel EnergyLevel { get; init; }
    /// <summary>Total spectra in the function (from
    /// <c>/spectrumInfos/{id}/data</c>'s <c>totalNumberOfSpectra</c> field).</summary>
    public int NumberOfSpectra { get; init; }
}
