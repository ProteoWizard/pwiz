using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>
/// Wraps an <see cref="ISpectrumList"/> and recomputes spectrum-level metadata
/// (base peak m/z &amp; intensity, lowest/highest observed m/z, total ion current)
/// from the m/z and intensity binary arrays.
/// </summary>
/// <remarks>
/// Port of pwiz::analysis::SpectrumList_MetadataFixer.
/// Useful after filters that add/remove peaks (e.g. <see cref="SpectrumListPeakFilter"/>) so
/// the downstream mzML metadata matches the actual peak list. Unlike <see cref="ThresholdFilter"/>,
/// this always loads binary data when asked for a spectrum.
/// </remarks>
public sealed class SpectrumListMetadataFixer : SpectrumListWrapper
{
    /// <summary>Creates a metadata-fixing wrapper around <paramref name="inner"/>.</summary>
    public SpectrumListMetadataFixer(ISpectrumList inner) : base(inner) { }

    /// <summary>Derived per-spectrum statistics produced by <see cref="Calculate"/>.</summary>
    public readonly record struct PeakMetadata(
        double BasePeakMz, double BasePeakIntensity,
        double LowestMz, double HighestMz, double TotalIntensity);

    /// <summary>Computes peak metadata for a (m/z, intensity) pair of arrays.</summary>
    public static PeakMetadata Calculate(IReadOnlyList<double> mz, IReadOnlyList<double> intensity)
    {
        ArgumentNullException.ThrowIfNull(mz);
        ArgumentNullException.ThrowIfNull(intensity);

        if (mz.Count == 0) return new PeakMetadata(0, 0, 0, 0, 0);

        double basePeakMz = -1, basePeakIntensity = -1;
        double lowMz = double.MaxValue, highMz = double.MinValue;
        double totalIntensity = 0;
        int n = System.Math.Min(mz.Count, intensity.Count);

        for (int i = 0; i < n; i++)
        {
            double y = intensity[i];
            if (y == 0) continue; // match pwiz's almost_equal(0) skip for zero-intensity samples
            double x = mz[i];
            if (x > highMz) highMz = x;
            if (x < lowMz) lowMz = x;
            totalIntensity += y;
            if (y > basePeakIntensity) { basePeakIntensity = y; basePeakMz = x; }
        }

        // If the entire spectrum had zero-intensity samples, return zeros so we don't write −1 into CV params.
        if (basePeakIntensity < 0)
            return new PeakMetadata(0, 0, 0, 0, 0);

        return new PeakMetadata(basePeakMz, basePeakIntensity, lowMz, highMz, totalIntensity);
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        // Always load binary data — we need the peaks to recompute metadata.
        var s = Inner.GetSpectrum(index, getBinaryData: true);
        var mz = s.GetMZArray();
        var intensity = s.GetIntensityArray();
        if (mz is null || intensity is null) return s;

        var m = Calculate(mz.Data, intensity.Data);
        Replace(s.Params, CVID.MS_base_peak_intensity, m.BasePeakIntensity, CVID.MS_number_of_detector_counts);
        Replace(s.Params, CVID.MS_base_peak_m_z, m.BasePeakMz, CVID.MS_m_z);
        Replace(s.Params, CVID.MS_lowest_observed_m_z, m.LowestMz, CVID.MS_m_z);
        Replace(s.Params, CVID.MS_highest_observed_m_z, m.HighestMz, CVID.MS_m_z);
        Replace(s.Params, CVID.MS_total_ion_current, m.TotalIntensity, CVID.MS_number_of_detector_counts);
        return s;
    }

    private static void Replace(Pwiz.Data.Common.Params.ParamContainer pc, CVID cvid, double value, CVID unit)
    {
        // Matches pwiz: if the param already exists, only update its value (leave units alone);
        // otherwise add a fresh one with the canonical unit.
        foreach (var p in pc.CVParams)
        {
            if (p.Cvid == cvid)
            {
                p.Value = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                return;
            }
        }
        pc.Set(cvid, value, unit);
    }
}
