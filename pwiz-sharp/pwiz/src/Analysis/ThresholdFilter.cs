using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>How <see cref="ThresholdFilter"/> interprets the threshold value.</summary>
public enum ThresholdingBy
{
    /// <summary>Keep the top/bottom N peaks (N = round(<see cref="ThresholdFilter.Threshold"/>)).
    /// If the cut falls between two peaks of equal intensity, the whole run at that intensity is
    /// dropped; a run lying wholly inside the kept set is kept.</summary>
    Count,
    /// <summary>Keep the top/bottom N peaks, extended forward through any run tied at the cutoff
    /// (so this may return more than N).</summary>
    CountAfterTies,
    /// <summary>Keep peaks whose intensity is strictly past <see cref="ThresholdFilter.Threshold"/>
    /// - above it for <see cref="ThresholdingOrientation.MostIntense"/>, below it for
    /// <see cref="ThresholdingOrientation.LeastIntense"/>. A peak exactly at the threshold is dropped.</summary>
    AbsoluteIntensity,
    /// <summary>As <see cref="AbsoluteIntensity"/>, against
    /// <see cref="ThresholdFilter.Threshold"/> times the base-peak intensity.</summary>
    FractionOfBasePeakIntensity,
    /// <summary>As <see cref="AbsoluteIntensity"/>, against
    /// <see cref="ThresholdFilter.Threshold"/> times the total ion current.</summary>
    FractionOfTotalIntensity,
    /// <summary>Sort by intensity in the orientation's direction and keep peaks from that end until
    /// their cumulative intensity reaches <see cref="ThresholdFilter.Threshold"/> times the TIC,
    /// then extend through any run tied at the cut point.</summary>
    FractionOfTotalIntensityCutoff,
}

/// <summary>Which end of the intensity distribution to keep.</summary>
public enum ThresholdingOrientation
{
    /// <summary>Keep the most intense peaks (drop low-intensity).</summary>
    MostIntense,
    /// <summary>Keep the least intense peaks (drop high-intensity).</summary>
    LeastIntense,
}

/// <summary>
/// Intensity-based peak filter applied to the m/z + intensity arrays of a <see cref="Spectrum"/>.
/// Port of pwiz::analysis::ThresholdFilter.
/// </summary>
public sealed class ThresholdFilter : ISpectrumDataFilter
{
    /// <summary>How the <see cref="Threshold"/> value is interpreted.</summary>
    public ThresholdingBy By { get; }

    /// <summary>The threshold value (see <see cref="ThresholdingBy"/> for the meaning).</summary>
    public double Threshold { get; }

    /// <summary>Whether we keep the most- or least-intense peaks.</summary>
    public ThresholdingOrientation Orientation { get; }

    /// <summary>Only apply to spectra whose MS level is in this set; others pass through unmodified. Defaults to all.</summary>
    public IntegerSet MsLevels { get; }

    /// <summary>Creates a threshold filter.</summary>
    public ThresholdFilter(
        ThresholdingBy by = ThresholdingBy.Count,
        double threshold = 1.0,
        ThresholdingOrientation orientation = ThresholdingOrientation.MostIntense,
        IntegerSet? msLevels = null)
    {
        By = by;
        Threshold = threshold;
        Orientation = orientation;
        MsLevels = msLevels ?? IntegerSet.Positive;
    }

    /// <summary>
    /// Applies the filter to <paramref name="spectrum"/>, mutating its <see cref="Spectrum.BinaryDataArrays"/> in place.
    /// No-op when the spectrum's MS level is outside <see cref="MsLevels"/> or binary data is missing.
    /// </summary>
    public void Apply(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        int msLevel = spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        if (!MsLevels.Contains(msLevel)) return;

        var mzArr = spectrum.GetMZArray();
        var intArr = spectrum.GetIntensityArray();
        if (mzArr is null || intArr is null) return;

        int n = System.Math.Min(mzArr.Data.Count, intArr.Data.Count);
        if (n == 0) return;

        // Pair up (index, intensity) so we can sort by intensity while preserving original-index
        // for producing the output arrays in mass-ascending order.
        var keep = new bool[n];
        double sum = 0, baseIntensity = 0;
        for (int i = 0; i < n; i++)
        {
            double v = intArr.Data[i];
            sum += v;
            if (v > baseIntensity) baseIntensity = v;
        }

        switch (By)
        {
            case ThresholdingBy.Count:
            case ThresholdingBy.CountAfterTies:
            {
                int count = (int)System.Math.Round(Threshold, MidpointRounding.AwayFromZero);
                if (count <= 0) { EmitFiltered(spectrum, keep); return; }

                // Sort indices by intensity, picking descending for MostIntense / ascending for LeastIntense.
                var ordered = new int[n];
                for (int i = 0; i < n; i++) ordered[i] = i;
                Array.Sort(ordered, (a, b) =>
                {
                    int cmp = intArr.Data[a].CompareTo(intArr.Data[b]);
                    return Orientation == ThresholdingOrientation.MostIntense ? -cmp : cmp;
                });

                if (count >= n) { for (int i = 0; i < n; i++) keep[i] = true; break; }

                double cutoffIntensity = intArr.Data[ordered[count - 1]];

                // Only a tie ACROSS the cut matters: cpp starts at the first point to erase and
                // walks backward while consecutive intensities are equal, so a run of equal
                // intensities that lies wholly inside the kept set is kept. Testing
                // ordered[count - 2] as well would treat such an inside run as ambiguous and
                // discard it: intensities 30 20 20 10 10 at count 3 keep all of 30 20 20.
                bool hasTieAtCutoff = intArr.Data[ordered[count]] == cutoffIntensity;

                if (hasTieAtCutoff)
                {
                    if (By == ThresholdingBy.CountAfterTies)
                    {
                        // Keep the top `count` plus every further peak tied at the cutoff.
                        for (int i = 0; i < count; i++) keep[ordered[i]] = true;
                        for (int i = count; i < n; i++)
                        {
                            if (intArr.Data[ordered[i]] == cutoffIntensity) keep[ordered[i]] = true;
                            else break;
                        }
                    }
                    else
                    {
                        // Drop ALL peaks at the cutoff intensity (can't fairly pick among ties).
                        for (int i = 0; i < count; i++)
                            if (intArr.Data[ordered[i]] != cutoffIntensity) keep[ordered[i]] = true;
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++) keep[ordered[i]] = true;
                }
                break;
            }

            case ThresholdingBy.AbsoluteIntensity:
                for (int i = 0; i < n; i++)
                    keep[i] = PassesIntensity(intArr.Data[i], Threshold);
                break;

            case ThresholdingBy.FractionOfBasePeakIntensity:
                for (int i = 0; i < n; i++)
                    keep[i] = PassesIntensity(intArr.Data[i], Threshold * baseIntensity);
                break;

            case ThresholdingBy.FractionOfTotalIntensity:
                for (int i = 0; i < n; i++)
                    keep[i] = PassesIntensity(intArr.Data[i], Threshold * sum);
                break;

            case ThresholdingBy.FractionOfTotalIntensityCutoff:
            {
                // cpp sorts in the orientation's direction and accumulates from that end, so
                // LeastIntense keeps the least-intense prefix - NOT the complement of the
                // most-intense one. The two differ: at threshold 1.0 the prefix is everything,
                // where the complement is nothing.
                var ordered = new int[n];
                for (int i = 0; i < n; i++) ordered[i] = i;
                Array.Sort(ordered, (a, b) =>
                {
                    int cmp = intArr.Data[a].CompareTo(intArr.Data[b]);
                    return Orientation == ThresholdingOrientation.MostIntense ? -cmp : cmp;
                });

                // Accumulate until the running fraction reaches the threshold. cpp compares
                // against threshold - 1e-6 so a sum landing exactly on the threshold stops here.
                int last = 0;
                double cumulative = intArr.Data[ordered[0]] / sum;
                while (cumulative < Threshold - 1e-6 && last + 1 < n)
                {
                    last++;
                    cumulative += intArr.Data[ordered[last]] / sum;
                }

                // Ties at the cut point are included, so the kept set never splits equal
                // intensities: intensities 12 2 2 1 1 1 1 0 0 at threshold .90 cut after the
                // fifth point, but keep seven, because points 5-7 are all intensity 1.
                while (last + 1 < n && intArr.Data[ordered[last + 1]] == intArr.Data[ordered[last]])
                    last++;

                for (int i = 0; i <= last; i++) keep[ordered[i]] = true;
                break;
            }
        }

        EmitFiltered(spectrum, keep);
    }

    // The comparison is strict, as it is in cpp. There, lower_bound over the intensity-sorted
    // pairs returns the first point to ERASE, so a point exactly at the threshold is dropped:
    // intensities 10 20 30 20 10 thresholded at 10 keep only 20 30 20, and at 30 keep nothing.
    private bool PassesIntensity(double actual, double threshold) =>
        Orientation == ThresholdingOrientation.MostIntense ? actual > threshold : actual < threshold;

    private static void EmitFiltered(Spectrum spectrum, bool[] keep)
    {
        var mzArr = spectrum.GetMZArray()!;
        var intArr = spectrum.GetIntensityArray()!;
        int n = System.Math.Min(mzArr.Data.Count, intArr.Data.Count);

        int kept = 0;
        for (int i = 0; i < n; i++) if (keep[i]) kept++;

        var newMz = new double[kept];
        var newInt = new double[kept];
        int j = 0;
        for (int i = 0; i < n; i++)
        {
            if (!keep[i]) continue;
            newMz[j] = mzArr.Data[i];
            newInt[j] = intArr.Data[i];
            j++;
        }

        // Figure out what units the original intensity array was in so we preserve them.
        CVID intensityUnits = CVID.MS_number_of_detector_counts;
        foreach (var p in intArr.CVParams)
            if (p.Units != CVID.CVID_Unknown) { intensityUnits = p.Units; break; }

        spectrum.SetMZIntensityArrays(newMz, newInt, intensityUnits);
    }
}

/// <summary>
/// Wraps an <see cref="ISpectrumList"/> and applies an <see cref="ISpectrumDataFilter"/> on
/// every spectrum loaded with binary data. Mirror of cpp's <c>SpectrumList_PeakFilter</c>.
/// </summary>
public sealed class SpectrumListPeakFilter : SpectrumListWrapper
{
    private readonly ISpectrumDataFilter _filter;

    /// <summary>Creates a peak-filter wrapper around <paramref name="inner"/>.</summary>
    public SpectrumListPeakFilter(ISpectrumList inner, ISpectrumDataFilter filter) : base(inner)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _filter = filter;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var spec = Inner.GetSpectrum(index, getBinaryData);
        if (getBinaryData) _filter.Apply(spec);
        return spec;
    }
}
