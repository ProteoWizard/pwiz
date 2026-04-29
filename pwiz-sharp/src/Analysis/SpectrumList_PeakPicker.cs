using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

#pragma warning disable CA1707

namespace Pwiz.Analysis;

/// <summary>
/// SpectrumList decorator that replaces profile spectra with picked peaks. Port of
/// <c>pwiz::analysis::SpectrumList_PeakPicker</c>.
/// </summary>
/// <remarks>
/// Vendor-prefer mode defers to a vendor-native centroid feed when the inner list is a
/// recognized vendor reader; otherwise the supplied <see cref="IPeakDetector"/> is run over
/// the profile m/z / intensity arrays. Spectra outside <see cref="MsLevels"/>, or those already
/// marked <see cref="CVID.MS_centroid_spectrum"/>, pass through unchanged.
/// </remarks>
public sealed class SpectrumList_PeakPicker : SpectrumListWrapper
{
    private readonly IPeakDetector? _algorithm;
    private readonly IntegerSet _msLevels;
    private readonly bool _preferVendor;
    private readonly Func<int, bool, Spectrum>? _vendorCentroidPath;
    private readonly string _mode;
    private readonly DataProcessing _dp;

    /// <summary>MS levels this detector targets (anything outside passes through).</summary>
    public IntegerSet MsLevels => _msLevels;

    /// <summary>The wrapped spectrum list. Public so wrappers (e.g.
    /// <c>SpectrumList_LockmassRefiner</c>) can detect a vendor source through a peak picker.</summary>
    public new ISpectrumList Inner => base.Inner;

    /// <inheritdoc/>
    public override DataProcessing DataProcessing => _dp;

    /// <summary>
    /// Creates a peak picker with <paramref name="algorithm"/> as the fallback when the
    /// vendor reader doesn't expose a centroid feed.
    /// </summary>
    /// <param name="inner">The SpectrumList to wrap.</param>
    /// <param name="algorithm">Fallback detector; may be null to require vendor centroiding.</param>
    /// <param name="preferVendorPeakPicking">When true, defer to vendor centroid if available.</param>
    /// <param name="msLevelsToPeakPick">Only spectra with MS level in this set are picked.</param>
    public SpectrumList_PeakPicker(
        ISpectrumList inner,
        IPeakDetector? algorithm,
        bool preferVendorPeakPicking,
        IntegerSet msLevelsToPeakPick)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(msLevelsToPeakPick);
        _algorithm = algorithm;
        _msLevels = msLevelsToPeakPick;
        _preferVendor = preferVendorPeakPicking;

        // Vendor-prefer mode: detect inner list type via a delegate the vendor list exposes.
        if (_preferVendor && inner is IVendorCentroidingSpectrumList vendor)
        {
            _vendorCentroidPath = vendor.GetCentroidSpectrum;
            _mode = vendor.VendorCentroidName;
        }
        else
        {
            _mode = algorithm?.Name ?? "vendor-only peak picker";
        }

        _dp = BuildDataProcessing();
    }

    private DataProcessing BuildDataProcessing()
    {
        // Copy the inner DP and add a "peak picking" processing method that names the mode.
        var innerDp = Inner.DataProcessing;
        var dp = new DataProcessing(innerDp?.Id ?? "pwiz_Reader_conversion");
        if (innerDp is not null)
            foreach (var pm in innerDp.ProcessingMethods)
                dp.ProcessingMethods.Add(pm);

        var method = new ProcessingMethod
        {
            Order = dp.ProcessingMethods.Count,
            Software = dp.ProcessingMethods.FirstOrDefault()?.Software,
        };
        method.Set(CVID.MS_peak_picking);
        method.UserParams.Add(new UserParam(_mode));
        dp.ProcessingMethods.Add(method);
        return dp;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var spec = _vendorCentroidPath is not null
            ? _vendorCentroidPath(index, getBinaryData)
            : Inner.GetSpectrum(index, getBinaryData);

        int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        if (!_msLevels.Contains(msLevel)) return spec;

        bool isCentroid = spec.Params.HasCVParam(CVID.MS_centroid_spectrum);
        if (isCentroid)
        {
            // Sanity: remove any lingering profile term so the emitted spectrum is unambiguous.
            RemoveCv(spec.Params, CVID.MS_profile_spectrum);
            return spec;
        }

        bool isProfile = spec.Params.HasCVParam(CVID.MS_profile_spectrum);

        // Swap the profile term (if present) for the centroid term; if the term lives on a
        // ParamGroup, copy the group's params onto the spectrum first. If the spectrum doesn't
        // declare either, assume profile and pick anyway (matches pwiz C++ fallback).
        bool replaced = RemoveCv(spec.Params, CVID.MS_profile_spectrum);
        if (!replaced && isProfile)
        {
            foreach (var pg in spec.Params.ParamGroups)
            {
                if (RemoveCv(pg, CVID.MS_profile_spectrum))
                {
                    foreach (var p in pg.CVParams) if (!spec.Params.HasCVParam(p.Cvid)) spec.Params.CVParams.Add(p);
                    foreach (var u in pg.UserParams) spec.Params.UserParams.Add(u);
                    spec.Params.ParamGroups.Remove(pg);
                    break;
                }
            }
        }
        spec.Params.Set(CVID.MS_centroid_spectrum);
        spec.DataProcessing = _dp;

        // In vendor-prefer mode, the vendor list may decline to centroid some spectra
        // (e.g. Thermo only has a centroid stream for FTMS analyzers). Those come back with
        // MS_profile_spectrum and we fall through to the algorithmic picker below. Only
        // short-circuit when the algorithm isn't actually needed.
        if (_algorithm is null && _vendorCentroidPath is not null)
            return spec; // vendor-only mode; caller accepts profile passthrough for analyzers the vendor can't centroid

        if (_algorithm is null)
            throw new InvalidOperationException(
                "[SpectrumList_PeakPicker] vendor peak picking is not available for this source and no algorithm was provided.");

        var mzArr = spec.GetMZArray();
        var intArr = spec.GetIntensityArray();
        if (mzArr is null || intArr is null)
        {
            // Need binary data to pick — re-read with getBinaryData.
            var withData = Inner.GetSpectrum(index, true);
            mzArr = withData.GetMZArray();
            intArr = withData.GetIntensityArray();
            spec = withData;
            RemoveCv(spec.Params, CVID.MS_profile_spectrum);
            spec.Params.Set(CVID.MS_centroid_spectrum);
            spec.DataProcessing = _dp;
        }
        if (mzArr is null || intArr is null || mzArr.Data.Count == 0) return spec;

        var xPeaks = new List<double>();
        var yPeaks = new List<double>();
        _algorithm.Detect(mzArr.Data, intArr.Data, xPeaks, yPeaks);

        // Overwrite the arrays in place — any binary arrays whose length matched the original
        // m/z array can't be preserved (peak picking breaks the one-to-one correspondence), so
        // drop them beyond mz + intensity.
        int originalLen = mzArr.Data.Count;
        for (int i = spec.BinaryDataArrays.Count - 1; i >= 0; i--)
        {
            var arr = spec.BinaryDataArrays[i];
            if (arr == mzArr || arr == intArr) continue;
            if (arr.Data.Count == originalLen) spec.BinaryDataArrays.RemoveAt(i);
        }

        mzArr.Data.Clear();
        mzArr.Data.AddRange(xPeaks);
        intArr.Data.Clear();
        intArr.Data.AddRange(yPeaks);
        spec.DefaultArrayLength = xPeaks.Count;

        return spec;
    }

    private static bool RemoveCv(ParamContainer p, CVID cvid)
    {
        for (int i = 0; i < p.CVParams.Count; i++)
        {
            if (p.CVParams[i].Cvid == cvid) { p.CVParams.RemoveAt(i); return true; }
        }
        return false;
    }
}

/// <summary>
/// Implemented by vendor spectrum lists that can emit centroided spectra natively. The
/// <see cref="SpectrumList_PeakPicker"/> uses this to defer to vendor centroiding when the
/// caller sets <c>preferVendorPeakPicking=true</c>.
/// </summary>
public interface IVendorCentroidingSpectrumList
{
    /// <summary>Human-readable label for the vendor's peak picking method.</summary>
    string VendorCentroidName { get; }

    /// <summary>Returns a centroided spectrum at <paramref name="index"/>.</summary>
    Spectrum GetCentroidSpectrum(int index, bool getBinaryData);
}
