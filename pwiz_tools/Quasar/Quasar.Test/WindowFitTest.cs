/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Core.Elution;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// Tests for the template-first clustering math (spec Stages 2d, 3a and 3b): the
    /// non-negative least squares solver, the fixed-template window fit with its membership
    /// statistic, and the pooled template fit. The synthetic data mimics the Astral HeLa runs:
    /// each window sampled every 1.1 s, peaks about 4 s wide, Poisson counting noise.
    /// </summary>
    [TestClass]
    public class WindowFitTest
    {
        /// <summary>One Astral DIA cycle (167 windows of 3 Th), in minutes.</summary>
        private const double CYCLE_MINUTES = 1.1 / 60.0;

        /// <summary>The true elution shape: FWHM about 4 s, tau/sigma 0.5.</summary>
        private static readonly EmgShape TRUE_SHAPE = new EmgShape(20.0, 0.028, 0.014);

        /// <summary>Ions are the intensity unit, plus a small baseline variance.</summary>
        private static readonly NoiseModel COUNTS = new NoiseModel(1.0, 1.0);

        /// <summary>Noise draws per statistical check.</summary>
        private const int SEED_COUNT = 20;

        [TestMethod]
        public void TestNonNegativeLeastSquares()
        {
            // Every random problem is solved exactly: the result matches the best feasible
            // solution found by trying every subset of free variables.
            var random = new Random(17);
            for (int trial = 0; trial < 200; trial++)
            {
                int p = 1 + trial % 5;
                int n = p + 3 + trial % 7;
                var columns = Enumerable.Range(0, p)
                    .Select(_ => Enumerable.Range(0, n).Select(__ => random.NextDouble() * 2.0 - 0.5).ToArray())
                    .ToArray();
                var y = Enumerable.Range(0, n).Select(_ => random.NextDouble() * 4.0 - 1.0).ToArray();
                var weights = Enumerable.Range(0, n).Select(_ => 0.5 + random.NextDouble()).ToArray();

                double[] x = NonNegativeLeastSquares.Solve(columns, y, weights);
                Assert.IsTrue(x.All(v => v >= 0.0));
                double sse = NonNegativeLeastSquares.WeightedSumOfSquares(columns, y, weights, x);
                double best = BruteForceBest(columns, y, weights);
                Assert.AreEqual(best, sse, 1e-9 * (1.0 + best), @"trial " + trial);
            }

            // A duplicated column cannot make the solver fail: the pair shares the amplitude.
            var shared = new[] { 1.0, 2.0, 3.0, 2.0, 1.0 };
            var duplicate = new[] { shared, (double[])shared.Clone() };
            var target = shared.Select(v => 3.0 * v).ToArray();
            double[] split = NonNegativeLeastSquares.Solve(duplicate, target, null);
            Assert.AreEqual(3.0, split.Sum(), 1e-9);
        }

        [TestMethod]
        public void TestTemplateFitSplitsAndTestsMembership()
        {
            var rt = Grid(TRUE_SHAPE.Mu, 40);
            var first = TRUE_SHAPE;
            var second = new EmgShape(TRUE_SHAPE.Mu + 0.05, TRUE_SHAPE.Sigma, TRUE_SHAPE.Tau);
            var templates = new[] { first, second };

            // Noise-free: a fragment shared by two overlapping elution profiles is split exactly,
            // and the baseline is recovered.
            var shared = rt.Select((t, i) => 40.0 * first.Density(t) + 25.0 * second.Density(t) + 3.0 + 0.05 * i)
                .ToArray();
            var exact = TemplateFit.Fit(rt, shared, 0, rt.Length - 1, templates, COUNTS);
            Assert.AreEqual(40.0, exact.Amplitudes[0], 1e-6);
            Assert.AreEqual(25.0, exact.Amplitudes[1], 1e-6);
            Assert.AreEqual(3.0, exact.BaselineStart, 1e-6);
            Assert.AreEqual(3.0 + 0.05 * (rt.Length - 1), exact.BaselineEnd, 1e-6);
            Assert.IsTrue(exact.IsMember(0) && exact.IsMember(1));

            // With counting noise, a trace from the first profile only is a member of the first
            // template and not of the second, even though the two overlap.
            var random = new Random(5);
            var onlyFirst = rt.Select(t => (double)Poisson(random, 30.0 * first.Density(t) + 2.0)).ToArray();
            var single = TemplateFit.Fit(rt, onlyFirst, 0, rt.Length - 1, templates, COUNTS);
            Assert.IsTrue(single.IsMember(0));
            Assert.IsFalse(single.IsMember(1));
            Assert.AreEqual(30.0, single.Amplitudes[0], 30.0 * 0.15);

            // A weak member (apex about 25 ions) still passes; pure baseline joins nothing.
            var weak = rt.Select(t => (double)Poisson(random, 2.0 * first.Density(t) + 2.0)).ToArray();
            Assert.IsTrue(TemplateFit.Fit(rt, weak, 0, rt.Length - 1, templates, COUNTS).IsMember(0));
            var baseline = rt.Select(_ => (double)Poisson(random, 2.0)).ToArray();
            var none = TemplateFit.Fit(rt, baseline, 0, rt.Length - 1, templates, COUNTS);
            Assert.IsFalse(none.IsMember(0) || none.IsMember(1));

            // An ion common to many peptides (a y1 ion, say) gets its share in each of three
            // co-eluting templates instead of joining them into one cluster.
            var third = new EmgShape(TRUE_SHAPE.Mu + 0.12, TRUE_SHAPE.Sigma, TRUE_SHAPE.Tau);
            var three = new[] { first, second, third };
            var common = rt.Select(t => 10.0 * first.Density(t) + 6.0 * second.Density(t) + 8.0 * third.Density(t))
                .ToArray();
            var commonFit = TemplateFit.Fit(rt, common, 0, rt.Length - 1, three, COUNTS);
            Assert.AreEqual(10.0, commonFit.Amplitudes[0], 1e-6);
            Assert.AreEqual(6.0, commonFit.Amplitudes[1], 1e-6);
            Assert.AreEqual(8.0, commonFit.Amplitudes[2], 1e-6);

            // Support: a template far outside the segment has no significant points.
            var far = new EmgShape(TRUE_SHAPE.Mu + 5.0, TRUE_SHAPE.Sigma, TRUE_SHAPE.Tau);
            Assert.AreEqual(0, TemplateFit.SupportPointCount(far, rt));
            Assert.IsTrue(TemplateFit.SupportPointCount(first, rt) >= TemplateFit.MIN_SUPPORT_POINTS);
        }

        [TestMethod]
        public void TestPooledFitDeterminesSparselySampledShape()
        {
            // Six fragments of one peptide (apexes about 80 to 520 ions), sampled every 1.1 s with
            // counting noise, over 20 noise draws. Fitted one at a time the shape is poorly
            // determined; fitted together it is close to the truth. Medians over the draws keep the
            // test independent of any one draw (a 200-draw sweep gave a median pooled error of 0.09
            // against 0.49 for single traces).
            var rt = Grid(TRUE_SHAPE.Mu, 22);
            var areas = new[] { 40.0, 28.0, 20.0, 14.0, 10.0, 6.0 };
            var pooledErrors = new List<double>();
            var singleErrors = new List<double>();
            var robustShifts = new List<double>();
            var areaErrors = new List<double>();
            for (int seed = 1; seed <= SEED_COUNT; seed++)
            {
                var random = new Random(seed);
                var traces = areas.Select(a => Sample(random, rt, TRUE_SHAPE, a, 2.0)).ToList();
                var pooled = PooledEmgFit.Fit(rt, traces, 0, rt.Length - 1, COUNTS);
                Assert.IsNotNull(pooled);
                Assert.AreEqual(traces.Count * rt.Length, pooled.PointCount);
                pooledErrors.Add(ShapeError(pooled.Shape));
                singleErrors.Add(Median(traces
                    .Select(trace => EmgFit.Fit(rt, trace))
                    .Select(fit => fit == null ? double.PositiveInfinity : ShapeError(fit.Shape))));
                areaErrors.AddRange(areas.Select((a, k) => Math.Abs(pooled.Amplitudes[k] - a) / a));

                // An interference bump on one member does not move the shared shape much.
                var interfered = traces.Select(t => (double[])t.Clone()).ToList();
                var bump = new EmgShape(TRUE_SHAPE.Mu + 0.06, 0.02, 0.01);
                for (int i = 0; i < rt.Length; i++)
                    interfered[0][i] += Poisson(random, 30.0 * bump.Density(rt[i]));
                var robust = PooledEmgFit.Fit(rt, interfered, 0, rt.Length - 1, COUNTS);
                Assert.IsNotNull(robust);
                robustShifts.Add(Math.Abs(robust.Shape.Mu - TRUE_SHAPE.Mu) / TRUE_SHAPE.Sigma);
            }

            double pooledError = Median(pooledErrors);
            double singleError = Median(singleErrors);
            Assert.IsTrue(pooledError < 0.2, @"median pooled shape error " + pooledError);
            Assert.IsTrue(pooledError < singleError / 3.0, @"pooled " + pooledError + @" vs single " + singleError);
            Assert.IsTrue(Median(areaErrors) < 0.1, @"median area error " + Median(areaErrors));
            Assert.IsTrue(robustShifts.Max() < 0.5, @"largest mu shift under interference " + robustShifts.Max());

            // At a quarter of the signal (apexes about 20 to 130 ions) the tail alone is not
            // determined and tau can collapse to zero. The run's shape prior prevents that, even
            // when it is 20% off the truth as a run-level estimate would be.
            var prior = new EmgShapePrior(TRUE_SHAPE.Sigma * 1.2, TRUE_SHAPE.Tau * 0.8);
            var tauRatios = new List<double>();
            for (int seed = 1; seed <= SEED_COUNT; seed++)
            {
                var random = new Random(seed);
                var weak = areas.Select(a => Sample(random, rt, TRUE_SHAPE, a / 4.0, 2.0)).ToList();
                var withPrior = PooledEmgFit.Fit(rt, weak, 0, rt.Length - 1, COUNTS, prior);
                Assert.IsNotNull(withPrior);
                tauRatios.Add(withPrior.Shape.Tau / TRUE_SHAPE.Tau);
            }

            Assert.IsTrue(tauRatios.Min() > 0.4, @"smallest tau ratio with the prior " + tauRatios.Min());
            Assert.AreEqual(1.0, Median(tauRatios), 0.3);

            // Too few points, or no traces: no fit.
            var any = areas.Select(a => Sample(new Random(1), rt, TRUE_SHAPE, a, 2.0)).ToList();
            Assert.IsNull(PooledEmgFit.Fit(rt, any, 0, 3, COUNTS));
            Assert.IsNull(PooledEmgFit.Fit(rt, new List<double[]>(), 0, rt.Length - 1, COUNTS));
        }

        /// <summary>
        /// Relative error of a fitted shape against <see cref="TRUE_SHAPE"/>: the larger of the sigma
        /// and tau errors, plus the mu error in units of sigma.
        /// </summary>
        private static double ShapeError(EmgShape shape)
        {
            double sigmaError = Math.Abs(shape.Sigma - TRUE_SHAPE.Sigma) / TRUE_SHAPE.Sigma;
            double tauError = Math.Abs(shape.Tau - TRUE_SHAPE.Tau) / TRUE_SHAPE.Tau;
            double muError = Math.Abs(shape.Mu - TRUE_SHAPE.Mu) / TRUE_SHAPE.Sigma;
            return Math.Max(sigmaError, tauError) + muError;
        }

        /// <summary><paramref name="n"/> scan times one cycle apart, the apex a third of the way in.</summary>
        private static double[] Grid(double apex, int n)
        {
            int pointsBeforeApex = n / 3;
            double first = apex - pointsBeforeApex * CYCLE_MINUTES;
            return Enumerable.Range(0, n).Select(i => first + i * CYCLE_MINUTES).ToArray();
        }

        private static double[] Sample(Random random, double[] rt, EmgShape shape, double area, double baseline)
        {
            return rt.Select(t => (double)Poisson(random, area * shape.Density(t) + baseline)).ToArray();
        }

        private static int Poisson(Random random, double mean)
        {
            return SyntheticCounts.Poisson(random, mean);
        }

        /// <summary>
        /// The smallest weighted sum of squares over all non-negative solutions, found by solving
        /// the unconstrained problem on every subset of variables and keeping the feasible ones.
        /// </summary>
        private static double BruteForceBest(double[][] columns, double[] y, double[] weights)
        {
            int p = columns.Length;
            double best = y.Select((v, i) => weights[i] * v * v).Sum();
            for (int mask = 1; mask < 1 << p; mask++)
            {
                var subset = Enumerable.Range(0, p).Where(j => (mask & (1 << j)) != 0).ToArray();
                var gram = new double[subset.Length, subset.Length];
                var rhs = new double[subset.Length];
                for (int a = 0; a < subset.Length; a++)
                {
                    for (int i = 0; i < y.Length; i++)
                        rhs[a] += weights[i] * columns[subset[a]][i] * y[i];
                    for (int b = 0; b < subset.Length; b++)
                    {
                        for (int i = 0; i < y.Length; i++)
                            gram[a, b] += weights[i] * columns[subset[a]][i] * columns[subset[b]][i];
                    }
                }

                var z = SolveSmall(gram, rhs);
                if (z == null || z.Any(v => v < 0.0))
                    continue;
                var x = new double[p];
                for (int a = 0; a < subset.Length; a++)
                    x[subset[a]] = z[a];
                best = Math.Min(best, NonNegativeLeastSquares.WeightedSumOfSquares(columns, y, weights, x));
            }

            return best;
        }

        /// <summary>Gauss-Jordan elimination, independent of the code under test; null when singular.</summary>
        private static double[] SolveSmall(double[,] a, double[] b)
        {
            int n = b.Length;
            var m = new double[n, n + 1];
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                    m[r, c] = a[r, c];
                m[r, n] = b[r];
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = Enumerable.Range(col, n - col).OrderByDescending(r => Math.Abs(m[r, col])).First();
                if (Math.Abs(m[pivot, col]) < 1e-12)
                    return null;
                for (int c = 0; c <= n; c++)
                    (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
                for (int r = 0; r < n; r++)
                {
                    if (r == col)
                        continue;
                    double factor = m[r, col] / m[col, col];
                    for (int c = col; c <= n; c++)
                        m[r, c] -= factor * m[col, c];
                }
            }

            return Enumerable.Range(0, n).Select(r => m[r, n] / m[r, r]).ToArray();
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[mid] : 0.5 * (sorted[mid - 1] + sorted[mid]);
        }
    }
}
