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

    /// <summary>Returns a centroided spectrum at <paramref name="index"/>.</summary>
    Spectrum GetCentroidSpectrum(int index, bool getBinaryData);
}
