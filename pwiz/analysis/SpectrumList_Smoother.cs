using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

#pragma warning disable CA1707

namespace Pwiz.Analysis;

/// <summary>
/// SpectrumList wrapper that smooths profile-mode spectra via an <see cref="ISmoother"/>
/// (typically <see cref="SavitzkyGolaySmoother"/>). Centroid spectra pass through unchanged.
/// Port of <c>pwiz::analysis::SpectrumList_Smoother</c>.
/// </summary>
public sealed class SpectrumList_Smoother : SpectrumListWrapper
{
    private readonly ISmoother _algorithm;
    private readonly IntegerSet _msLevelsToSmooth;

    /// <summary>Constructs a smoothing wrapper around <paramref name="inner"/>.</summary>
    /// <param name="inner">The spectrum list to wrap.</param>
    /// <param name="algorithm">Smoothing algorithm.</param>
    /// <param name="msLevelsToSmooth">MS levels to apply smoothing to. Spectra at other levels
    /// pass through unchanged.</param>
    public SpectrumList_Smoother(ISpectrumList inner, ISmoother algorithm, IntegerSet msLevelsToSmooth)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        ArgumentNullException.ThrowIfNull(msLevelsToSmooth);
        _algorithm = algorithm;
        _msLevelsToSmooth = msLevelsToSmooth;
    }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing
    {
        get
        {
            var dp = new DataProcessing(Inner.DataProcessing?.Id ?? "pwiz_Reader_conversion");
            if (Inner.DataProcessing is not null)
                foreach (var pm in Inner.DataProcessing.ProcessingMethods)
                    dp.ProcessingMethods.Add(pm);
            var method = new ProcessingMethod
            {
                Order = dp.ProcessingMethods.Count,
                Software = dp.ProcessingMethods.FirstOrDefault()?.Software,
            };
            method.Set(CVID.MS_smoothing);
            method.UserParams.Add(new UserParam("Savitzky-Golay smoothing"));
            dp.ProcessingMethods.Add(method);
            return dp;
        }
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var spec = Inner.GetSpectrum(index, getBinaryData: true);

        // Skip non-profile spectra (centroided data is one peak per m/z; smoothing it would
        // average away the peaks themselves).
        if (!spec.Params.HasCVParam(CVID.MS_profile_spectrum))
            return spec;

        int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        if (!_msLevelsToSmooth.Contains(msLevel))
            return spec;

        var mzArr = spec.GetMZArray();
        var intArr = spec.GetIntensityArray();
        if (mzArr is null || intArr is null) return spec;

        var smoothedMz = new List<double>(mzArr.Data.Count);
        var smoothedInt = new List<double>(intArr.Data.Count);
        try
        {
            _algorithm.Smooth(mzArr.Data, intArr.Data, smoothedMz, smoothedInt);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                "[SpectrumList_Smoother] Error smoothing intensity data: " + e.Message, e);
        }

        // The two arrays should be the same length post-smooth — Smooth() pads xSmoothed via
        // ZeroSampleFiller to match the y output. Mirror cpp's swap-then-update-defaultArrayLength.
        spec.SetMZIntensityArrays(smoothedMz, smoothedInt, intArr.Params.CvParam(CVID.MS_intensity_unit).Cvid == CVID.CVID_Unknown
            ? CVID.MS_number_of_detector_counts
            : intArr.Params.CvParam(CVID.MS_intensity_unit).Cvid);
        spec.DefaultArrayLength = smoothedMz.Count;

        spec.DataProcessing = DataProcessing;
        return spec;
    }
}
