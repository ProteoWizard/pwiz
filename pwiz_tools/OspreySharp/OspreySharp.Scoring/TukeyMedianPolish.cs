// Tukey median polish decomposition for fragment XIC matrices.
//
// Decomposes a (fragment x scan) intensity matrix into overall + row effects
// + column effects + residuals using iterative median subtraction in ln space.
// Provides robust elution profiles and interference-free fragment intensities
// for FDR scoring features.
//
// Port of osprey-scoring/src/lib.rs tukey_median_polish() and related feature
// computation functions (median_polish_libcosine, median_polish_rsquared,
// median_polish_residual_ratio, median_polish_min_fragment_r2,
// median_polish_residual_correlation).

using System;
using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Result of Tukey median polish decomposition of a fragment XIC matrix.
    /// All effects are in ln space; convert to linear via exp(overall + effect).
    /// </summary>
    public class TukeyMedianPolishResult
    {
        /// <summary>Grand median in ln space.</summary>
        public double Overall { get; set; }

        /// <summary>Row effects per fragment in ln space (data-derived intensities).</summary>
        public double[] RowEffects { get; set; }

        /// <summary>Column effects per scan in ln space (elution profile shape).</summary>
        public double[] ColEffects { get; set; }

        /// <summary>Elution profile in linear space: array of (rt, intensity) pairs.</summary>
        public double[] ElutionProfileRts { get; set; }
        public double[] ElutionProfileIntensities { get; set; }

        /// <summary>
        /// Residuals[fragment][scan] in ln space. NaN for zero-intensity cells
        /// (treated as missing data).
        /// </summary>
        public double[][] Residuals { get; set; }

        /// <summary>Number of iterations used by the algorithm.</summary>
        public int NIterations { get; set; }

        /// <summary>Whether the algorithm converged within tolerance.</summary>
        public bool Converged { get; set; }

        /// <summary>Number of fragments that had at least one non-zero intensity.</summary>
        public int NFragmentsUsed { get; set; }

        /// <summary>Original fragment index for each row (maps row -> library fragment).</summary>
        public int[] FragmentIndices { get; set; }
    }

    /// <summary>
    /// Tukey median polish algorithm and derived scoring features.
    /// Port of the median_polish_* functions from osprey-scoring/src/lib.rs.
    /// </summary>
    public static class TukeyMedianPolish
    {
        // .NET Framework 4.7.2 doesn't have double.IsFinite, so we provide our own.
        // A double is finite when it's neither NaN nor +/- infinity.
        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        /// <summary>
        /// Decompose a (fragment x scan) intensity matrix into row + column effects
        /// using iterative median subtraction in ln space. Zeros are treated as
        /// missing data (NaN).
        /// </summary>
        /// <param name="xics">List of fragment XICs as (fragmentIndex, intensities[]) tuples.</param>
        /// <param name="rts">Retention times for each scan column.</param>
        /// <param name="maxIter">Maximum iterations (default 20).</param>
        /// <param name="tol">Convergence tolerance on max residual change (default 1e-4).</param>
        /// <returns>Decomposition result, or null if input is too small.</returns>
        public static TukeyMedianPolishResult Compute(
            IList<KeyValuePair<int, double[]>> xics,
            double[] rts,
            int maxIter,
            double tol)
        {
            if (xics == null || xics.Count < 2)
                return null;

            int nScans = xics[0].Value.Length;
            if (nScans < 3)
                return null;

            int nFrags = xics.Count;
            var fragmentIndices = new int[nFrags];
            for (int f = 0; f < nFrags; f++)
                fragmentIndices[f] = xics[f].Key;

            // Build ln-space matrix. Zeros -> NaN (missing).
            var residuals = new double[nFrags][];
            for (int f = 0; f < nFrags; f++)
            {
                var src = xics[f].Value;
                var row = new double[nScans];
                for (int s = 0; s < nScans; s++)
                {
                    double v = src[s];
                    row[s] = v > 0.0 ? Math.Log(v) : double.NaN;
                }
                residuals[f] = row;
            }

            double overall = 0.0;
            var rowEffects = new double[nFrags];
            var colEffects = new double[nScans];
            bool converged = false;
            int nIter = 0;

            var oldRow = new double[nScans];
            var colBuf = new double[nFrags];
            var rowMedians = new double[nFrags];
            var colMedians = new double[nScans];

            for (int iteration = 0; iteration < maxIter; iteration++)
            {
                nIter = iteration + 1;
                double maxChange = 0.0;

                // Row sweep: subtract nanmedian of each row
                for (int f = 0; f < nFrags; f++)
                {
                    rowMedians[f] = NanMedian(residuals[f]);
                }

                for (int f = 0; f < nFrags; f++)
                {
                    double rm = rowMedians[f];
                    if (IsFinite(rm))
                    {
                        var row = residuals[f];
                        for (int s = 0; s < nScans; s++)
                        {
                            double val = row[s];
                            if (IsFinite(val))
                            {
                                double newVal = val - rm;
                                double diff = Math.Abs(newVal - val);
                                if (diff > maxChange) maxChange = diff;
                                row[s] = newVal;
                            }
                        }
                    }
                }

                double medianOfRowMedians = NanMedian(rowMedians);
                if (IsFinite(medianOfRowMedians))
                {
                    for (int f = 0; f < nFrags; f++)
                    {
                        if (IsFinite(rowMedians[f]))
                            rowEffects[f] += rowMedians[f] - medianOfRowMedians;
                    }
                    overall += medianOfRowMedians;
                }

                // Column sweep: subtract nanmedian of each column
                for (int s = 0; s < nScans; s++)
                {
                    for (int f = 0; f < nFrags; f++)
                        colBuf[f] = residuals[f][s];
                    colMedians[s] = NanMedian(colBuf);
                }

                for (int f = 0; f < nFrags; f++)
                {
                    var row = residuals[f];
                    for (int s = 0; s < nScans; s++)
                    {
                        double val = row[s];
                        double cm = colMedians[s];
                        if (IsFinite(val) && IsFinite(cm))
                        {
                            double newVal = val - cm;
                            double diff = Math.Abs(newVal - val);
                            if (diff > maxChange) maxChange = diff;
                            row[s] = newVal;
                        }
                    }
                }

                double medianOfColMedians = NanMedian(colMedians);
                if (IsFinite(medianOfColMedians))
                {
                    for (int s = 0; s < nScans; s++)
                    {
                        if (IsFinite(colMedians[s]))
                            colEffects[s] += colMedians[s] - medianOfColMedians;
                    }
                    overall += medianOfColMedians;
                }

                if (maxChange < tol)
                {
                    converged = true;
                    break;
                }
            }

            // Count fragments with at least one finite value
            int nFragmentsUsed = 0;
            for (int f = 0; f < nFrags; f++)
            {
                bool any = false;
                var row = residuals[f];
                for (int s = 0; s < nScans; s++)
                {
                    if (IsFinite(row[s]))
                    {
                        any = true;
                        break;
                    }
                }
                if (any) nFragmentsUsed++;
            }

            if (nFragmentsUsed < 2)
                return null;

            // Build linear-space elution profile: exp(overall + col_effects[s])
            var profileRts = new double[nScans];
            var profileInts = new double[nScans];
            for (int s = 0; s < nScans; s++)
            {
                profileRts[s] = rts[s];
                profileInts[s] = Math.Exp(overall + colEffects[s]);
            }

            return new TukeyMedianPolishResult
            {
                Overall = overall,
                RowEffects = rowEffects,
                ColEffects = colEffects,
                ElutionProfileRts = profileRts,
                ElutionProfileIntensities = profileInts,
                Residuals = residuals,
                NIterations = nIter,
                Converged = converged,
                NFragmentsUsed = nFragmentsUsed,
                FragmentIndices = fragmentIndices,
            };
        }

        /// <summary>
        /// Cosine similarity between Tukey median polish row effects and library
        /// fragment intensities, using sqrt preprocessing (Poisson noise model).
        /// Library intensity always contributes; undetected fragments push 0 into
        /// the dot product but increase the denominator, penalizing decoys that
        /// fail to detect high-intensity fragments.
        /// </summary>
        public static double LibCosine(
            TukeyMedianPolishResult polish,
            IList<LibraryFragment> libraryFragments)
        {
            if (polish == null) return 0.0;
            int n = polish.FragmentIndices.Length;
            if (n < 2) return 0.0;

            var rowVec = new List<double>(n);
            var libVec = new List<double>(n);

            for (int i = 0; i < n; i++)
            {
                int fragIdx = polish.FragmentIndices[i];
                if (fragIdx >= libraryFragments.Count)
                    continue;

                double libInt = libraryFragments[fragIdx].RelativeIntensity;
                libVec.Add(Math.Sqrt(Math.Max(0.0, libInt)));

                bool hasSignal = false;
                var resRow = polish.Residuals[i];
                for (int s = 0; s < resRow.Length; s++)
                {
                    if (IsFinite(resRow[s]))
                    {
                        hasSignal = true;
                        break;
                    }
                }

                if (!hasSignal || !IsFinite(polish.RowEffects[i]))
                {
                    rowVec.Add(0.0);
                    continue;
                }

                double linear = Math.Exp(polish.Overall + polish.RowEffects[i]);
                rowVec.Add(Math.Sqrt(Math.Max(0.0, linear)));
            }

            if (rowVec.Count < 2)
                return 0.0;

            return CosineAngle(rowVec, libVec);
        }

        /// <summary>
        /// Residual ratio in linear space: sum|observed - predicted| / sum(observed).
        /// Lower is better. Higher values indicate interference or noise on individual fragments.
        /// </summary>
        public static double ResidualRatio(TukeyMedianPolishResult polish)
        {
            if (polish == null) return 1.0;
            int nFrags = polish.RowEffects.Length;
            int nScans = nFrags > 0 ? polish.ColEffects.Length : 0;
            if (nFrags < 2 || nScans < 3) return 1.0;

            double sumAbsResidual = 0.0;
            double sumObserved = 0.0;

            for (int f = 0; f < nFrags; f++)
            {
                for (int s = 0; s < nScans; s++)
                {
                    double predicted = Math.Exp(polish.Overall + polish.RowEffects[f] + polish.ColEffects[s]);
                    double observed;
                    if (IsFinite(polish.Residuals[f][s]))
                    {
                        observed = Math.Exp(polish.Overall + polish.RowEffects[f] + polish.ColEffects[s] + polish.Residuals[f][s]);
                    }
                    else
                    {
                        observed = 0.0001; // pseudocount for true-zero observations
                    }

                    sumAbsResidual += Math.Abs(observed - predicted);
                    sumObserved += observed;
                }
            }

            if (sumObserved < 1e-30) return 1.0;
            return sumAbsResidual / sumObserved;
        }

        /// <summary>
        /// Minimum per-fragment R² (sqrt-preprocessed) between predicted and observed
        /// intensities across scans. Identifies the weakest-correlating fragment.
        /// </summary>
        public static double MinFragmentR2(TukeyMedianPolishResult polish)
        {
            if (polish == null) return 0.0;
            int nFrags = polish.RowEffects.Length;
            int nScans = polish.ColEffects.Length;
            if (nFrags < 1 || nScans < 3) return 0.0;

            double minR2 = double.MaxValue;
            var pred = new List<double>(nScans);
            var obs = new List<double>(nScans);

            for (int f = 0; f < nFrags; f++)
            {
                pred.Clear();
                obs.Clear();

                for (int s = 0; s < nScans; s++)
                {
                    if (!IsFinite(polish.Residuals[f][s])) continue;
                    double predicted = Math.Exp(polish.Overall + polish.RowEffects[f] + polish.ColEffects[s]);
                    double observed = Math.Exp(polish.Overall + polish.RowEffects[f] + polish.ColEffects[s] + polish.Residuals[f][s]);
                    pred.Add(Math.Sqrt(predicted));
                    obs.Add(Math.Sqrt(observed));
                }

                double r2 = pred.Count < 3 ? 0.0 : ComputeR2(pred, obs);
                if (r2 < minR2) minR2 = r2;
            }

            if (minR2 == double.MaxValue) return 0.0;
            return Math.Max(0.0, minR2);
        }

        /// <summary>
        /// Mean pairwise Pearson correlation of median polish residuals across fragments.
        /// For a clean target, residuals should be uncorrelated noise. Correlated residuals
        /// suggest a co-eluting interferer affecting multiple fragments.
        /// </summary>
        public static double ResidualCorrelation(TukeyMedianPolishResult polish)
        {
            if (polish == null) return 0.0;
            int nFrags = polish.RowEffects.Length;
            int nScans = polish.ColEffects.Length;
            if (nFrags < 2 || nScans < 3) return 0.0;

            double corrSum = 0.0;
            int nPairs = 0;
            var ri = new List<double>(nScans);
            var rj = new List<double>(nScans);

            for (int i = 0; i < nFrags; i++)
            {
                for (int j = i + 1; j < nFrags; j++)
                {
                    ri.Clear();
                    rj.Clear();
                    for (int s = 0; s < nScans; s++)
                    {
                        if (IsFinite(polish.Residuals[i][s]) && IsFinite(polish.Residuals[j][s]))
                        {
                            ri.Add(polish.Residuals[i][s]);
                            rj.Add(polish.Residuals[j][s]);
                        }
                    }
                    if (ri.Count >= 3)
                    {
                        double r = PearsonCorrelationRaw(ri, rj);
                        if (IsFinite(r))
                        {
                            corrSum += r;
                            nPairs++;
                        }
                    }
                }
            }

            return nPairs == 0 ? 0.0 : corrSum / nPairs;
        }

        // ============================================================
        // Private helpers
        // ============================================================

        /// <summary>
        /// Median of a slice, skipping NaN values. Returns NaN if no finite values.
        /// </summary>
        private static double NanMedian(double[] values)
        {
            var finite = new List<double>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                if (IsFinite(values[i]))
                    finite.Add(values[i]);
            }
            if (finite.Count == 0) return double.NaN;
            finite.Sort();
            int mid = finite.Count / 2;
            if (finite.Count % 2 == 0)
                return 0.5 * (finite[mid - 1] + finite[mid]);
            return finite[mid];
        }

        private static double CosineAngle(List<double> a, List<double> b)
        {
            int n = Math.Min(a.Count, b.Count);
            double dot = 0.0, normA = 0.0, normB = 0.0;
            for (int i = 0; i < n; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            if (normA < 1e-30 || normB < 1e-30) return 0.0;
            double v = dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
            if (v < 0.0) return 0.0;
            if (v > 1.0) return 1.0;
            return v;
        }

        private static double ComputeR2(List<double> predicted, List<double> observed)
        {
            int n = predicted.Count;
            if (n < 2) return 0.0;
            double obsMean = 0.0;
            for (int i = 0; i < n; i++) obsMean += observed[i];
            obsMean /= n;

            double ssTot = 0.0, ssRes = 0.0;
            for (int i = 0; i < n; i++)
            {
                double dt = observed[i] - obsMean;
                double dr = observed[i] - predicted[i];
                ssTot += dt * dt;
                ssRes += dr * dr;
            }
            if (ssTot < 1e-30) return 0.0;
            return Math.Max(0.0, 1.0 - ssRes / ssTot);
        }

        private static double PearsonCorrelationRaw(List<double> x, List<double> y)
        {
            int n = Math.Min(x.Count, y.Count);
            if (n < 2) return 0.0;
            double mx = 0.0, my = 0.0;
            for (int i = 0; i < n; i++) { mx += x[i]; my += y[i]; }
            mx /= n; my /= n;

            double sxy = 0.0, sxx = 0.0, syy = 0.0;
            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - mx;
                double dy = y[i] - my;
                sxy += dx * dy;
                sxx += dx * dx;
                syy += dy * dy;
            }
            if (sxx < 1e-30 || syy < 1e-30) return 0.0;
            return sxy / Math.Sqrt(sxx * syy);
        }
    }
}
