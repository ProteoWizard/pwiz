namespace Pwiz.Analysis.PeakPicking;

/// <summary>
/// Sentinel "detector" that throws when invoked — used with
/// <see cref="Pwiz.Analysis.SpectrumList_PeakPicker"/> in vendor-only mode: the peak picker
/// must defer to native vendor centroiding, and if no vendor centroid path exists
/// (non-vendor input, or vendor centroid feed unavailable) the pipeline should fail
/// rather than fall back to a generic detector. Port of pwiz.CLI's
/// <c>VendorOnlyPeakDetector</c>.
/// </summary>
public sealed class VendorOnlyPeakDetector : IPeakDetector
{
    /// <inheritdoc/>
    public string Name => "vendor-only peak picker";

    /// <inheritdoc/>
    /// <remarks>
    /// Throws with a specific message pattern that Skyline's ChromCacheBuilder catches
    /// (searches for <c>"PeakDetector::NoVendorPeakPickingException"</c>) and converts
    /// to a user-friendly <c>NoCentroidedDataException</c>. This matches legacy pwiz.CLI's
    /// throw-with-magic-message behavior.
    /// </remarks>
    public void Detect(
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        List<double> xPeakValues,
        List<double> yPeakValues,
        List<Peak>? peaks = null)
    {
        throw new System.InvalidOperationException(
            "PeakDetector::NoVendorPeakPickingException: Vendor centroid feed unavailable; " +
            "SpectrumList_PeakPicker was constructed with vendor-only mode.");
    }
}
