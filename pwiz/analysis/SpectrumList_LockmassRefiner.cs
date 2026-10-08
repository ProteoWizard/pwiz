using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Vendor.Waters;

#pragma warning disable CA1707

namespace Pwiz.Analysis;

/// <summary>
/// SpectrumList wrapper that routes <see cref="GetSpectrum"/> calls through Waters'
/// post-acquisition lockmass correction. Mirrors pwiz C++
/// <c>pwiz::analysis::SpectrumList_LockmassRefiner</c>.
/// </summary>
/// <remarks>
/// Construction-time behavior:
/// <list type="bullet">
///   <item>If the inner list (or the inner of a wrapping <see cref="SpectrumList_PeakPicker"/>)
///         is a <see cref="SpectrumList_Waters"/>, append a "m/z calibration" processing
///         method tagged "Waters lockmass correction" to the data-processing chain.</item>
///   <item>Otherwise the wrapper is effectively a passthrough — useful when an msconvert
///         pipeline blindly applies the filter regardless of vendor.</item>
/// </list>
/// At read time the wrapper detects the same chain again and calls the lockmass-aware
/// overload on <see cref="SpectrumList_Waters"/> with the configured (positive, negative)
/// lockmass m/z values and tolerance. It also strips the redundant <see cref="CVID.MS_profile_spectrum"/>
/// term when the inner Waters list claims a centroid result (mirrors pwiz cpp).
/// <para>A <see cref="SpectrumListWrapper"/>, as in cpp, so what the list underneath knows
/// stays visible through it: <see cref="ISpectrumList.CalibrationSpectraAreOmitted"/> is
/// forwarded, and <see cref="SpectrumListWrapper.Innermost"/> reaches the Waters list, which
/// is how <see cref="SpectrumList_IonMobility"/> finds its ion mobility and SONAR support.
/// Skyline installs this over every Waters list it lockmass corrects.</para>
/// </remarks>
public sealed class SpectrumList_LockmassRefiner : SpectrumListWrapper
{
    private readonly double _mzPositiveScans;
    private readonly double _mzNegativeScans;
    private readonly double _tolerance;
    private readonly DataProcessing? _dp;

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => _dp;

    /// <summary>Constructs the wrapper with separate positive/negative lockmass m/z values.</summary>
    public SpectrumList_LockmassRefiner(ISpectrumList inner,
        double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance)
        : base(inner)
    {
        _mzPositiveScans = lockmassMzPosScans;
        _mzNegativeScans = lockmassMzNegScans;
        _tolerance = lockmassTolerance;

        // Build the data-processing chain: copy inner's methods + append the m/z calibration
        // method when there's a Waters list somewhere underneath. If there's no Waters list
        // we leave _dp null and behave as a passthrough.
        if (FindInnerWaters(out _) is not null)
        {
            var innerDp = inner.DataProcessing;
            var dp = new DataProcessing(innerDp?.Id ?? "pwiz_Reader_conversion");
            if (innerDp is not null)
                foreach (var pm in innerDp.ProcessingMethods)
                    dp.ProcessingMethods.Add(pm);
            var method = new ProcessingMethod
            {
                Order = dp.ProcessingMethods.Count,
                Software = dp.ProcessingMethods.FirstOrDefault()?.Software,
            };
            method.Set(CVID.MS_m_z_calibration);
            method.UserParams.Add(new UserParam("Waters lockmass correction"));
            dp.ProcessingMethods.Add(method);
            _dp = dp;
        }
        else
        {
            _dp = inner.DataProcessing;
        }
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var waters = FindInnerWaters(out var picker);
        Spectrum spec;
        if (waters is null)
        {
            // Passthrough — non-Waters source, lockmass refinement isn't meaningful but the
            // wrapper is still semantically valid (matches pwiz C++ which logs a warning).
            spec = Inner.GetSpectrum(index, getBinaryData);
        }
        else if (picker is not null)
        {
            // Outer peak picker: the Waters list emits centroided data via its lockmass-aware
            // GetCentroidSpectrum overload, then the picker no-ops because the spectrum is
            // already marked centroid. Strip any leftover MS_profile_spectrum term.
            spec = waters.GetCentroidSpectrumWithLockmass(index, getBinaryData, picker.MsLevels,
                _mzPositiveScans, _mzNegativeScans, _tolerance);
            if (spec.Params.HasCVParam(CVID.MS_centroid_spectrum))
            {
                for (int i = spec.Params.CVParams.Count - 1; i >= 0; i--)
                    if (spec.Params.CVParams[i].Cvid == CVID.MS_profile_spectrum)
                        spec.Params.CVParams.RemoveAt(i);
            }
        }
        else
        {
            spec = waters.GetSpectrumWithLockmass(index, getBinaryData,
                _mzPositiveScans, _mzNegativeScans, _tolerance);
        }
        spec.DataProcessing = _dp;
        return spec;
    }

    /// <summary>
    /// Walks <see cref="SpectrumListWrapper.Inner"/> down through one optional <see cref="SpectrumList_PeakPicker"/>
    /// and returns the underlying <see cref="SpectrumList_Waters"/>, or null if the chain
    /// doesn't terminate in a Waters list.
    /// </summary>
    private SpectrumList_Waters? FindInnerWaters(out SpectrumList_PeakPicker? peakPicker)
    {
        peakPicker = Inner as SpectrumList_PeakPicker;
        if (peakPicker is not null)
            return peakPicker.Inner as SpectrumList_Waters;
        return Inner as SpectrumList_Waters;
    }
}
