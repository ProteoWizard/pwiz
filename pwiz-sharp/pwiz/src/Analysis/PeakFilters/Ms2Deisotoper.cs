using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>
/// MS2 deisotoping. Port of pwiz cpp's <c>MS2Deisotoper</c>
/// (<c>pwiz/analysis/spectrum_processing/MS2Deisotoper.cpp</c>).
/// </summary>
/// <remarks>
/// Two algorithms:
/// <list type="bullet">
///   <item><b>Low-/hi-res scan</b> (default): for each peak in descending intensity order, drop
///   peaks that fall within the isotope spacing tolerance ahead of it. Hi-res mode walks forward
///   from the seed peak; low-res mode iterates the intensity-sorted list.</item>
///   <item><b>Poisson</b>: builds candidate isotope chains (peaks spaced by m_neutron / charge),
///   scores each via Kullback-Leibler divergence + summed-square-error against a Poisson model
///   (Breen et al. 2000, Bellew et al. 2006), and drops the M+1..M+N peaks of high-confidence
///   chains while keeping the monoisotope.</item>
/// </list>
/// </remarks>
public sealed class Ms2Deisotoper : ISpectrumDataFilter
{
    private const double MassNeutron = 1.00335;          // m(13C) - m(12C)
    private const double PoissonMzTolPpm = 100.0;        // matches cpp
    private const double PoissonLambda = 1.0 / 1800.0;   // msInspect's poisson lambda parameter

    /// <summary>m/z matching tolerance for isotope spacing (cpp default 0.5 Da).</summary>
    public MZTolerance MatchingTolerance { get; }

    /// <summary>Hi-resolution mode for the scan algorithm (different inner loop). Ignored when
    /// <see cref="Poisson"/> is true.</summary>
    public bool HiRes { get; }

    /// <summary>Use Poisson algorithm instead of the intensity-sorted scan.</summary>
    public bool Poisson { get; }

    /// <summary>Maximum charge considered during Poisson chain detection.</summary>
    public int MaxCharge { get; }

    /// <summary>Minimum charge considered during Poisson chain detection.</summary>
    public int MinCharge { get; }

    /// <summary>Creates the filter.</summary>
    public Ms2Deisotoper(MZTolerance? matchingTolerance = null, bool hiRes = false, bool poisson = false,
        int minCharge = 1, int maxCharge = 3)
    {
        MatchingTolerance = matchingTolerance ?? new MZTolerance(0.5);
        HiRes = hiRes;
        Poisson = poisson;
        MinCharge = minCharge;
        MaxCharge = maxCharge;
        if (minCharge > maxCharge)
            throw new ArgumentException("minCharge must be <= maxCharge");
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

        var keep = new bool[n];
        for (int i = 0; i < n; i++) keep[i] = true;

        if (Poisson)
            DeisotopePoisson(mz, inten, n, keep);
        else
            DeisotopeScan(mz, inten, n, keep);

        Compact(spectrum, keep, n, mz, inten, intArr);
    }

    /// <summary>Sort peaks by descending intensity and walk the order — for each kept peak, drop
    /// peaks within (2 + tol) Da downstream that have lower intensity (hi-res) OR drop peaks in
    /// the intensity-sorted list whose m/z is between (mz - tol, mz + 2 + tol) (low-res).</summary>
    private void DeisotopeScan(IList<double> mz, IList<double> inten, int n, bool[] keep)
    {
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        // Descending by intensity.
        Array.Sort(order, (a, b) => inten[b].CompareTo(inten[a]));

        double tol = MatchingTolerance.Value;

        for (int rank = 0; rank < n; rank++)
        {
            int seed = order[rank];
            if (!keep[seed]) continue;

            if (HiRes)
            {
                // Walk m/z-forward; drop peaks within (2 + tol) Da of seed that have lower intensity.
                for (int i = seed + 1; i < n; i++)
                {
                    if (mz[i] - mz[seed] >= 2.0 + tol) break;
                    if (keep[i] && inten[i] < inten[seed]) keep[i] = false;
                }
            }
            else
            {
                // Iterate the intensity-sorted list past the current rank; drop peaks whose m/z
                // satisfies -massDiff < tol AND massDiff < 2 + tol (cpp lines 174-181).
                for (int rj = rank + 1; rj < n; rj++)
                {
                    int j = order[rj];
                    double diff = mz[j] - mz[seed];
                    if (-diff < tol && diff < 2.0 + tol) keep[j] = false;
                }
            }
        }
    }

    /// <summary>Poisson chain-based deisotoping (Breen et al. 2000, Bellew et al. 2006).</summary>
    private void DeisotopePoisson(IList<double> mz, IList<double> inten, int n, bool[] keep)
    {
        if (n < 2) return;

        const int maxIsotopePeaks = 5;
        double mzTolFraction = PoissonMzTolPpm / 1_000_000.0;

        var chains = new List<IsotopeChain>();
        for (int j = 0; j < n; j++)
        {
            for (int k = j + 1; k < n; k++)
            {
                double mzDiff = mz[k] - mz[j];
                double mzTol = mzTolFraction * mz[k];
                if (mzDiff > MassNeutron + mzTol) break;

                double recip = MassNeutron / mzDiff;
                int possibleCharge = (int)(recip + 0.5);
                if (possibleCharge > MaxCharge || possibleCharge < MinCharge) continue;

                if (System.Math.Abs(MassNeutron / possibleCharge - mzDiff) >= mzTol) continue;

                // Try to extend an existing chain at the same charge.
                int snapshot = chains.Count;
                for (int w = 0; w < snapshot; w++)
                {
                    var chain = chains[w];
                    if (chain.Charge != possibleCharge) continue;
                    if (chain.Indices.Count >= maxIsotopePeaks) continue;
                    if (chain.Indices[^1] != j) continue;

                    chains.Add(new IsotopeChain(chain.Charge, new List<int>(chain.Indices)));
                    chain.Indices.Add(k);
                }

                // Always seed a new length-2 chain.
                chains.Add(new IsotopeChain(possibleCharge, new List<int> { j, k }));
            }
        }

        ReadOnlySpan<double> klCutoffs = stackalloc double[] { 0.025, 0.05, 0.1, 0.2, 0.3 };
        ReadOnlySpan<double> sseCutoffs = stackalloc double[] { 0.00005, 0.0001, 0.0003, 0.0006, 0.001 };

        var remove = new bool[n];
        foreach (var chain in chains)
        {
            int length = chain.Indices.Count;
            double kl = KlScore(chain, mz, inten);

            // Mean monoisotopic mass back-calculated from each peak's m/z.
            double avg = 0;
            for (int k = 0; k < length; k++)
                avg += mz[chain.Indices[k]] - k * MassNeutron / chain.Charge;
            avg /= length;

            double sse = 0;
            for (int k = 0; k < length; k++)
            {
                double err = avg - (mz[chain.Indices[k]] - k * MassNeutron / chain.Charge);
                sse += err * err;
            }

            int reducedLength = length <= 6 ? length - 2 : 4;
            if (reducedLength < 0 || reducedLength >= klCutoffs.Length) continue;

            double monoIntensity = inten[chain.Indices[0]];
            if (kl < klCutoffs[reducedLength] && sse < sseCutoffs[reducedLength] && monoIntensity > 5.0)
            {
                // Drop the M+1..M+N peaks; keep the monoisotope.
                for (int i = 1; i < length; i++) remove[chain.Indices[i]] = true;
            }
        }

        for (int i = 0; i < n; i++) if (remove[i]) keep[i] = false;
    }

    private static double KlScore(IsotopeChain chain, IList<double> mz, IList<double> inten)
    {
        double mStar = PoissonLambda * mz[chain.Indices[0]] * chain.Charge;
        double mExp = System.Math.Exp(-mStar);

        var poissonVals = new double[chain.Indices.Count];
        double poissonSum = 0;
        double observedSum = 0;
        for (int k = 0; k < chain.Indices.Count; k++)
        {
            double poisson = mExp * System.Math.Pow(mStar, k);
            for (int w = k; w > 1; w--) poisson /= w;
            poissonVals[k] = poisson;
            poissonSum += poisson;
            observedSum += inten[chain.Indices[k]];
        }

        double kl = 0;
        for (int k = 0; k < chain.Indices.Count; k++)
        {
            poissonVals[k] /= poissonSum;
            double normObs = inten[chain.Indices[k]] / observedSum;
            kl += normObs * System.Math.Log10(normObs / poissonVals[k]);
        }
        return kl;
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

    private sealed class IsotopeChain
    {
        public int Charge { get; }
        public List<int> Indices { get; }
        public IsotopeChain(int charge, List<int> indices) { Charge = charge; Indices = indices; }
    }
}
