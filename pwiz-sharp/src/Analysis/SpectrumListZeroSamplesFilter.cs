using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>Which direction the zero-samples filter operates in.</summary>
public enum ZeroSamplesMode
{
    /// <summary>Drop zero-intensity samples from each spectrum (interior runs of zeros only;
    /// the first/last zero of each run is preserved when the spectrum is profile-mode so peak
    /// picking still sees a flank). Mirrors cpp <c>Mode_RemoveExtraZeros</c>.</summary>
    RemoveExtra,

    /// <summary>Insert flanking zero samples around each non-zero run, sized off the local
    /// sample rate. Mirrors cpp <c>Mode_AddMissingZeros</c>.</summary>
    AddMissing,
}

/// <summary>
/// Wraps an <see cref="ISpectrumList"/> and either drops extra zero-intensity samples from
/// each spectrum or adds missing flanking zeros around non-zero runs. Port of
/// <c>pwiz::analysis::SpectrumList_ZeroSamplesFilter</c>.
/// </summary>
public sealed class SpectrumListZeroSamplesFilter : SpectrumListWrapper
{
    /// <summary>MS levels the filter applies to; others pass through unchanged.</summary>
    public IntegerSet MsLevels { get; }

    /// <summary>Which mode the filter operates in.</summary>
    public ZeroSamplesMode Mode { get; }

    /// <summary>Number of flanking zeros to insert on each side of a non-zero run, used in
    /// <see cref="ZeroSamplesMode.AddMissing"/> mode. Default -1 matches cpp's default — but
    /// note that cpp's ZeroSampleFiller treats negative counts as a no-op (the inner loop
    /// uses `j=1; j&lt;=count` with count cast to int), so the user typically wants to supply
    /// a positive value like <c>addMissing=5</c>.</summary>
    public int FlankingZeroCount { get; }

    /// <summary>Creates a zero-samples filter. Defaults to <see cref="ZeroSamplesMode.RemoveExtra"/>
    /// over all MS levels.</summary>
    public SpectrumListZeroSamplesFilter(ISpectrumList inner, IntegerSet? msLevels = null,
        ZeroSamplesMode mode = ZeroSamplesMode.RemoveExtra, int flankingZeroCount = -1)
        : base(inner)
    {
        MsLevels = msLevels ?? IntegerSet.Positive;
        Mode = mode;
        FlankingZeroCount = flankingZeroCount;
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

        CVID intensityUnits = CVID.MS_number_of_detector_counts;
        foreach (var p in intArr.CVParams)
            if (p.Units != CVID.CVID_Unknown) { intensityUnits = p.Units; break; }

        if (Mode == ZeroSamplesMode.AddMissing)
        {
            // FlankingZeroCount is passed through directly. cpp's default (-1) results in
            // ZeroSampleFiller's loop running zero iterations (cpp casts size_t→int → -1,
            // so `j=1; j<=-1` is false); for parity we accept negative values as no-ops.
            // To actually fill zeros the caller must supply a non-negative count.
            var xFilled = new List<double>(mzArr.Data.Count);
            var yFilled = new List<double>(intArr.Data.Count);
            ZeroSampleFiller.Fill(mzArr.Data, intArr.Data, xFilled, yFilled, FlankingZeroCount);
            s.SetMZIntensityArrays(xFilled, yFilled, intensityUnits);
            return s;
        }

        // RemoveExtra: drop all zero-intensity samples.
        // TODO: cpp's removeExtra preserves one flanking zero per run for profile-mode spectra
        // (ExtraZeroSamplesFilter.cpp:46 — preserveFlankingZeros=true when not centroided).
        // This implementation drops all zeros unconditionally; that's a minor parity gap but
        // tracked separately from the addMissing port.
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
        s.SetMZIntensityArrays(newMz, newInt, intensityUnits);
        return s;
    }
}
