using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>
/// MS2 noise reduction by sliding-window top-N peak retention. Port of pwiz cpp's
/// <c>MS2NoiseFilter</c> (<c>pwiz/analysis/spectrum_processing/MS2NoiseFilter.cpp</c>).
/// </summary>
/// <remarks>
/// Reference: Renard et al., "When less can yield more — Computational preprocessing of MS/MS
/// spectra for peptide identification", Proteomics 9, 4978-4984 (2009).
///
/// Pipeline (only on MSn spectra with a precursor):
///   1. Drop peaks above (precursor m/z * charge) - 57.0214640 Da (the mass of glycine).
///   2. Drop peaks within ±0.5 Da of the unfragmented precursor m/z.
///   3. Slide a window of <see cref="WindowWidthDa"/> Da; in each window keep the top
///      <see cref="PeaksInWindow"/> intensities. With <see cref="RelaxLowMass"/> and a
///      multiply-charged precursor, the budget grows for windows below the precursor.
/// </remarks>
public sealed class Ms2NoiseFilter : ISpectrumDataFilter
{
    private const double RelaxFactor = 0.5;

    /// <summary>Top-N retention budget per sliding window (cpp default 6).</summary>
    public int PeaksInWindow { get; }

    /// <summary>Window width in Da. When ≤ 0 the spectrum's <see cref="CVID.MS_highest_observed_m_z"/>
    /// is used (or 1e6 fallback) — i.e. one window covering the full spectrum.</summary>
    public double WindowWidthDa { get; }

    /// <summary>When true the per-window budget is scaled up below multiply-charged precursors
    /// (more fragments expected at low mass).</summary>
    public bool RelaxLowMass { get; }

    /// <summary>Creates the filter.</summary>
    public Ms2NoiseFilter(int peaksInWindow = 6, double windowWidthDa = 30.0, bool relaxLowMass = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peaksInWindow);
        PeaksInWindow = peaksInWindow;
        WindowWidthDa = windowWidthDa;
        RelaxLowMass = relaxLowMass;
    }

    /// <inheritdoc/>
    public void Apply(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        if (!IsMs2WithPrecursor(spectrum)) return;

        var mzArr = spectrum.GetMZArray();
        var intArr = spectrum.GetIntensityArray();
        if (mzArr is null || intArr is null) return;

        var mz = mzArr.Data;
        var inten = intArr.Data;
        int n = System.Math.Min(mz.Count, inten.Count);
        if (n == 0) return;

        var (precursorMz, precursorCharge) = GetPrecursor(spectrum);

        // Resolve the window width: 0 means use the spectrum's highest observed m/z (cpp default).
        double windowWidth = WindowWidthDa > 0
            ? WindowWidthDa
            : spectrum.Params.CvParamValueOrDefault(CVID.MS_highest_observed_m_z, 1_000_000.0);

        // Mark which peaks survive each cull pass; compact at the end so we don't shift arrays repeatedly.
        var keep = new bool[n];
        for (int i = 0; i < n; i++) keep[i] = true;

        // Pass 1: drop peaks above precursor*charge - mass(glycine).
        if (precursorCharge > 0)
        {
            double upperBound = precursorMz * precursorCharge - 57.0214640;
            for (int i = 0; i < n; i++) if (mz[i] >= upperBound) keep[i] = false;
        }

        // Pass 2: drop unfragmented precursor (within ±0.5 Da, inclusive on both sides —
        // cpp uses lower_bound..upper_bound iterators which translate to a [closed, closed]
        // range when exact matches sit on the boundaries).
        if (precursorMz > 0)
            for (int i = 0; i < n; i++)
                if (mz[i] >= precursorMz - 0.5 && mz[i] <= precursorMz + 0.5) keep[i] = false;

        // Pass 3: sliding-window top-N keeps. Walk over surviving peaks; each peak starts a new
        // window of [mz[lb], mz[lb] + windowWidth]. The cpp version uses upper_bound to find the
        // window's right edge, then drops all but the top numMassesInWindow within it. We follow
        // that contract exactly.
        int lb = NextSurviving(keep, 0);
        while (lb < n)
        {
            // Window edge: peaks with mz <= mz[lb] + windowWidth.
            double rightEdge = mz[lb] + windowWidth;
            int ub = lb;
            while (ub < n && mz[ub] <= rightEdge) ub++;

            // Collect surviving (index, intensity) within [lb, ub).
            var inWindow = new List<(int Index, double Intensity)>(ub - lb);
            for (int i = lb; i < ub; i++)
                if (keep[i] && inten[i] > 0)
                    inWindow.Add((i, inten[i]));

            int budget = PeaksInWindow;
            if (RelaxLowMass && precursorCharge > 1 && mz[lb] > 0)
            {
                // cpp:
                //   if (min(precursorCharge, (int)((precursorMz*charge)/mz[lb])) > 1)
                //     numMassesInWindow *= factor * min(...)
                int factor = System.Math.Min(precursorCharge, (int)(precursorMz * precursorCharge / mz[lb]));
                if (factor > 1)
                    budget = (int)(PeaksInWindow * (RelaxFactor * factor));
            }

            if (inWindow.Count > budget)
            {
                inWindow.Sort((a, b) => b.Intensity.CompareTo(a.Intensity));
                for (int i = budget; i < inWindow.Count; i++)
                    keep[inWindow[i].Index] = false;
            }

            // Advance to the next surviving peak (cpp: `do { lb++; } while (intensity <= 0);`).
            lb = NextSurviving(keep, lb + 1);
        }

        Compact(spectrum, keep, n, mz, inten, intArr);
    }

    private static int NextSurviving(bool[] keep, int from)
    {
        for (int i = from; i < keep.Length; i++) if (keep[i]) return i;
        return keep.Length;
    }

    private static void Compact(Spectrum spectrum, bool[] keep, int n,
        IReadOnlyList<double> mz, IReadOnlyList<double> inten, BinaryDataArray intArr)
    {
        int kept = 0;
        for (int i = 0; i < n; i++) if (keep[i]) kept++;

        var newMz = new double[kept];
        var newInt = new double[kept];
        int j = 0;
        for (int i = 0; i < n; i++)
        {
            if (!keep[i]) continue;
            newMz[j] = mz[i];
            newInt[j] = inten[i];
            j++;
        }

        CVID intensityUnits = CVID.MS_number_of_detector_counts;
        foreach (var p in intArr.CVParams)
            if (p.Units != CVID.CVID_Unknown) { intensityUnits = p.Units; break; }

        spectrum.SetMZIntensityArrays(newMz, newInt, intensityUnits);
    }

    private static bool IsMs2WithPrecursor(Spectrum spectrum)
    {
        if (spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) <= 1) return false;
        if (!spectrum.Params.HasCVParam(CVID.MS_MSn_spectrum)) return false;
        if (spectrum.Precursors.Count == 0) return false;
        if (spectrum.Precursors[0].SelectedIons.Count == 0) return false;
        if (spectrum.Precursors[0].SelectedIons[0].IsEmpty) return false;
        return true;
    }

    private static (double Mz, int Charge) GetPrecursor(Spectrum spectrum)
    {
        double mz = 0;
        int charge = 0;
        foreach (var precursor in spectrum.Precursors)
        {
            foreach (var ion in precursor.SelectedIons)
            {
                if (ion.HasCVParam(CVID.MS_m_z))
                    mz = ion.CvParamValueOrDefault(CVID.MS_m_z, 0.0);
                else if (ion.HasCVParam(CVID.MS_selected_ion_m_z))
                    mz = ion.CvParamValueOrDefault(CVID.MS_selected_ion_m_z, 0.0);

                if (ion.HasCVParam(CVID.MS_charge_state))
                    charge = ion.CvParamValueOrDefault(CVID.MS_charge_state, 0);
            }
        }
        return (mz, charge);
    }
}

