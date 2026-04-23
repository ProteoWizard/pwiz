using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>
/// Wraps an <see cref="ISpectrumList"/> and drops zero-intensity peaks from each spectrum.
/// </summary>
/// <remarks>
/// Port of pwiz::analysis::SpectrumList_ZeroSamplesFilter in <c>Mode_RemoveExtraZeros</c> mode.
/// The add-missing-zeros mode (padding profile spectra with zeros) is deferred to a follow-up task.
/// Binary data is always loaded; when the caller asks for metadata-only we forward the request to
/// the inner list unchanged.
/// </remarks>
public sealed class SpectrumListZeroSamplesFilter : SpectrumListWrapper
{
    /// <summary>MS levels the filter applies to; others pass through unchanged.</summary>
    public IntegerSet MsLevels { get; }

    /// <summary>Creates a zero-samples filter affecting <paramref name="msLevels"/> (or all MS levels if null).</summary>
    public SpectrumListZeroSamplesFilter(ISpectrumList inner, IntegerSet? msLevels = null)
        : base(inner)
    {
        MsLevels = msLevels ?? IntegerSet.Positive;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var s = Inner.GetSpectrum(index, getBinaryData);
        if (!getBinaryData) return s;

        int msLevel = s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        if (!MsLevels.Contains(msLevel)) return s;

        var mzArr = s.GetMZArray();
        var intArr = s.GetIntensityArray();
        if (mzArr is null || intArr is null) return s;

        int n = System.Math.Min(mzArr.Data.Count, intArr.Data.Count);
        var newMz = new List<double>(n);
        var newInt = new List<double>(n);
        for (int i = 0; i < n; i++)
        {
            if (intArr.Data[i] != 0)
            {
                newMz.Add(mzArr.Data[i]);
                newInt.Add(intArr.Data[i]);
            }
        }

        CVID intensityUnits = CVID.MS_number_of_detector_counts;
        foreach (var p in intArr.CVParams)
            if (p.Units != CVID.CVID_Unknown) { intensityUnits = p.Units; break; }

        s.SetMZIntensityArrays(newMz, newInt, intensityUnits);
        return s;
    }
}
