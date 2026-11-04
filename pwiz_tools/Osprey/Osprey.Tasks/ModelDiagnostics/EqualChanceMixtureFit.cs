/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

namespace pwiz.Osprey.Tasks.ModelDiagnostics
{
    /// <summary>
    /// A two-component Gaussian mixture fitted by EM to a BINNED score distribution (bin
    /// centers weighted by counts): <c>f = piFalse * N(falseMean, falseStd) + (1 - piFalse) *
    /// N(trueMean, trueStd)</c>, the lower-mean component taken as the false-target null.
    /// Fitted to the target scores only, so the null it yields is independent of the decoys
    /// it is then compared against (issue #4464 idea A: a decoy-anchored null would make the
    /// comparison vacuous).
    ///
    /// <para>Several deterministic starts from the target quantiles; the converged fit with
    /// the highest log-likelihood wins. Variances are floored at the bin-width variance
    /// (<c>w^2 / 12</c>) so a component cannot collapse onto one bin.</para>
    /// </summary>
    public sealed class EqualChanceMixtureFit
    {
        // Starting (lower, upper) component means, as quantiles of the fitted distribution.
        private static readonly double[][] START_QUANTILES =
        {
            new[] { 0.25, 0.75 },
            new[] { 0.10, 0.90 },
            new[] { 0.50, 0.95 },
            new[] { 0.05, 0.50 },
        };

        private const int MAX_ITERATIONS = 1000;
        private const double RELATIVE_TOLERANCE = 1e-10;

        public double PiFalse { get; set; }
        public double FalseMean { get; set; }
        public double FalseStd { get; set; }
        public double TrueMean { get; set; }
        public double TrueStd { get; set; }
        /// <summary>Ashman's D, <c>sqrt(2) |trueMean - falseMean| / sqrt(falseStd^2 + trueStd^2)</c>:
        /// above 2 the two components are cleanly separated.</summary>
        public double Separation { get; set; }
        public double LogLikelihood { get; set; }
        public int Iterations { get; set; }
        public bool Converged { get; set; }
        /// <summary>Total count the fit was made to.</summary>
        public long N { get; set; }

        /// <summary>
        /// Fit the mixture to <paramref name="counts"/> over the bins whose centers are
        /// <paramref name="centers"/> (equal width <paramref name="binWidth"/>). Returns null
        /// when there is nothing to fit (fewer than two populated bins).
        /// </summary>
        public static EqualChanceMixtureFit Fit(double[] centers, long[] counts, double binWidth)
        {
            if (centers == null || counts == null || centers.Length != counts.Length)
                return null;
            long n = 0;
            int populated = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                {
                    n += counts[i];
                    populated++;
                }
            }
            if (populated < 2)
                return null;

            double mean = 0;
            for (int i = 0; i < counts.Length; i++)
                mean += centers[i] * counts[i];
            mean /= n;
            double var = 0;
            for (int i = 0; i < counts.Length; i++)
                var += counts[i] * (centers[i] - mean) * (centers[i] - mean);
            double sd = Math.Sqrt(var / n);
            double varFloor = binWidth * binWidth / 12;

            EqualChanceMixtureFit best = null;
            foreach (var start in START_QUANTILES)
            {
                var fit = RunEm(centers, counts, n,
                    Quantile(centers, counts, n, start[0]), Quantile(centers, counts, n, start[1]),
                    Math.Max(sd / 2, Math.Sqrt(varFloor)), varFloor);
                if (best == null || (fit.Converged && !best.Converged) ||
                    (fit.Converged == best.Converged && fit.LogLikelihood > best.LogLikelihood))
                {
                    best = fit;
                }
            }
            return best;
        }

        /// <summary>
        /// Probability mass of <c>N(mean, std)</c> inside each bin of <paramref name="edges"/>,
        /// renormalized to the binned range so it compares with an area-normalized histogram
        /// over the same edges.
        /// </summary>
        public static double[] BinMass(double[] edges, double mean, double std)
        {
            int nb = edges.Length - 1;
            var mass = new double[nb];
            double total = 0;
            for (int i = 0; i < nb; i++)
            {
                mass[i] = NormalCdf((edges[i + 1] - mean) / std) - NormalCdf((edges[i] - mean) / std);
                total += mass[i];
            }
            if (total > 0)
            {
                for (int i = 0; i < nb; i++)
                    mass[i] /= total;
            }
            return mass;
        }

        private static EqualChanceMixtureFit RunEm(double[] x, long[] w, long n,
            double m1, double m2, double sd0, double varFloor)
        {
            double p = 0.5, s1 = sd0, s2 = sd0;
            double llOld = double.NegativeInfinity, ll = double.NegativeInfinity;
            bool converged = false;
            int it;
            for (it = 1; it <= MAX_ITERATIONS; it++)
            {
                double r1 = 0, r1X = 0, r1Xx = 0, r2 = 0, r2X = 0, r2Xx = 0;
                ll = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    if (w[i] <= 0)
                        continue;
                    double a = p * NormalDensity(x[i], m1, s1);
                    double b = (1 - p) * NormalDensity(x[i], m2, s2);
                    double t = a + b;
                    double resp = t > 0 ? a / t : (Math.Abs(x[i] - m1) <= Math.Abs(x[i] - m2) ? 1 : 0);
                    ll += w[i] * Math.Log(Math.Max(t, double.Epsilon));
                    double wi = w[i];
                    r1 += wi * resp;
                    r1X += wi * resp * x[i];
                    r1Xx += wi * resp * x[i] * x[i];
                    r2 += wi * (1 - resp);
                    r2X += wi * (1 - resp) * x[i];
                    r2Xx += wi * (1 - resp) * x[i] * x[i];
                }
                if (r1 <= 0 || r2 <= 0)
                    break;   // a component emptied: degenerate, never converged
                p = r1 / n;
                m1 = r1X / r1;
                s1 = Math.Sqrt(Math.Max(r1Xx / r1 - m1 * m1, varFloor));
                m2 = r2X / r2;
                s2 = Math.Sqrt(Math.Max(r2Xx / r2 - m2 * m2, varFloor));
                if (Math.Abs(ll - llOld) <= RELATIVE_TOLERANCE * Math.Abs(ll))
                {
                    converged = true;
                    break;
                }
                llOld = ll;
            }

            // Orient so the lower-mean component is the false-target null.
            if (m1 > m2)
            {
                p = 1 - p;
                (m1, m2) = (m2, m1);
                (s1, s2) = (s2, s1);
            }
            return new EqualChanceMixtureFit
            {
                PiFalse = p,
                FalseMean = m1,
                FalseStd = s1,
                TrueMean = m2,
                TrueStd = s2,
                Separation = Math.Sqrt(2) * Math.Abs(m2 - m1) / Math.Sqrt(s1 * s1 + s2 * s2),
                LogLikelihood = ll,
                Iterations = Math.Min(it, MAX_ITERATIONS),
                Converged = converged,
                N = n,
            };
        }

        private static double Quantile(double[] x, long[] w, long n, double q)
        {
            double target = q * n;
            long cum = 0;
            for (int i = 0; i < x.Length; i++)
            {
                cum += w[i];
                if (cum >= target && w[i] > 0)
                    return x[i];
            }
            return x[x.Length - 1];
        }

        private static double NormalDensity(double x, double mean, double std)
        {
            double z = (x - mean) / std;
            return Math.Exp(-0.5 * z * z) / (std * Math.Sqrt(2 * Math.PI));
        }

        // Standard normal CDF via the Abramowitz-Stegun 7.1.26 erf (|error| < 1.5e-7), ample
        // for the bin masses a divergence over an 80-bin histogram reads.
        private static double NormalCdf(double z)
        {
            double x = Math.Abs(z) / Math.Sqrt(2);
            double t = 1 / (1 + 0.3275911 * x);
            double y = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t
                            + 0.254829592) * t * Math.Exp(-x * x);
            return z >= 0 ? 0.5 * (1 + y) : 0.5 * (1 - y);
        }
    }
}
