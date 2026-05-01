using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>How <see cref="ThresholdFilter"/> interprets the threshold value.</summary>
public enum ThresholdingBy
{
    /// <summary>Keep the top/bottom N peaks (N = round(<see cref="ThresholdFilter.Threshold"/>)). Peaks tied at the cutoff are dropped.</summary>
    Count,
    /// <summary>Keep the top/bottom N peaks, preserving ties at the cutoff (may return more than N).</summary>
    CountAfterTies,
    /// <summary>Keep peaks with intensity ≥/≤ <see cref="ThresholdFilter.Threshold"/>.</summary>
    AbsoluteIntensity,
    /// <summary>Keep peaks whose intensity is ≥/≤ <see cref="ThresholdFilter.Threshold"/> × base-peak intensity.</summary>
    FractionOfBasePeakIntensity,
    /// <summary>Keep peaks whose intensity is ≥/≤ <see cref="ThresholdFilter.Threshold"/> × total ion current.</summary>
    FractionOfTotalIntensity,
    /// <summary>Sort by intensity; keep top peaks until their cumulative intensity ≥ <see cref="ThresholdFilter.Threshold"/> × TIC.</summary>
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
public sealed class ThresholdFilter
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

                // pwiz semantics: a tie exists whenever the cutoff intensity appears more than once
                // in the full list (ambiguous which tied peak to drop). Checking neighbors on either
                // side of the count boundary catches both within-top-N ties and spill-over ties.
                bool hasTieAtCutoff =
                    intArr.Data[ordered[count]] == cutoffIntensity
                    || (count >= 2 && intArr.Data[ordered[count - 2]] == cutoffIntensity);

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
                double target = Threshold * sum;
                var ordered = new int[n];
                for (int i = 0; i < n; i++) ordered[i] = i;
                Array.Sort(ordered, (a, b) => intArr.Data[b].CompareTo(intArr.Data[a])); // always descending
                double cumulative = 0;
                foreach (var idx in ordered)
                {
                    keep[idx] = true;
                    cumulative += intArr.Data[idx];
                    if (cumulative >= target) break;
                }
                // If LeastIntense, invert the result (keep the complement).
                if (Orientation == ThresholdingOrientation.LeastIntense)
                    for (int i = 0; i < n; i++) keep[i] = !keep[i];
                break;
            }
        }

        EmitFiltered(spectrum, keep);
    }

    private bool PassesIntensity(double actual, double threshold) =>
        Orientation == ThresholdingOrientation.MostIntense ? actual >= threshold : actual <= threshold;

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
/// Wraps an <see cref="ISpectrumList"/> and applies a <see cref="ThresholdFilter"/> on every loaded spectrum.
/// </summary>
public sealed class SpectrumListPeakFilter : SpectrumListWrapper
{
    private readonly ThresholdFilter _filter;

    /// <summary>Creates a peak-filter wrapper around <paramref name="inner"/>.</summary>
    public SpectrumListPeakFilter(ISpectrumList inner, ThresholdFilter filter) : base(inner)
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
