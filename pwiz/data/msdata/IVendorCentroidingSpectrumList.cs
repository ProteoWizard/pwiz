using Pwiz.Util.Misc;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// Implemented by vendor spectrum lists that can emit centroided spectra natively. The
/// analysis-side <c>SpectrumList_PeakPicker</c> uses this to defer to vendor centroiding when
/// the caller sets <c>preferVendorPeakPicking=true</c>. Lives in <c>Pwiz.Data.MsData</c> so
/// vendor readers (which sit below the Analysis layer) can implement it without taking a
/// reference on Analysis — mirrors cpp's virtual-method-on-base pattern.
/// </summary>
public interface IVendorCentroidingSpectrumList
{
    /// <summary>Human-readable label for the vendor's peak picking method.</summary>
    string VendorCentroidName { get; }

    /// <summary>
    /// Returns the spectrum at <paramref name="index"/>, vendor-centroided only if its MS level
    /// is in <paramref name="msLevelsToCentroid"/>; otherwise the spectrum is returned untouched.
    /// </summary>
    /// <remarks>
    /// The MS-level gate belongs here, not in the caller, because cpp applies it inside each
    /// reader (e.g. <c>SpectrumList_Waters.cpp:219</c>,
    /// <c>doCentroid = msLevelsToCentroid.contains(msLevel) &amp;&amp; isProfile</c>) against the
    /// reader's own FINAL MS level. Two things depend on that placement:
    /// <list type="bullet">
    ///   <item>Waters promotes high-energy MSe survey scans to level 2 before gating, so an
    ///     outside gate would test the wrong level.</item>
    ///   <item>Non-MS spectra (DiodeArray / EMR) report level 0, so no caller-supplied set
    ///     selects them. Centroiding one destroys it - MassLynx cannot centroid an
    ///     absorbance-vs-wavelength trace and returns nothing.</item>
    /// </list>
    /// Gating after the call instead of before it is what produced both symptoms: 834 of 1000
    /// Waters DAD spectra emitted with defaultArrayLength=0, and Shimadzu spectra carrying
    /// <c>centroid spectrum</c> and <c>profile spectrum</c> at once.
    /// </remarks>
    Spectrum GetCentroidSpectrum(int index, bool getBinaryData, IntegerSet msLevelsToCentroid);
}
