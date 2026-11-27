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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Core.Elution;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// Tests for the EMG elution model ported from datum: the fit, the consensus integrator,
    /// the height crossings and the linear baseline.
    /// </summary>
    [TestClass]
    public class EmgTest
    {
        /// <summary>
        /// Relative tolerance against datum's outputs. The port keeps datum's arithmetic, so the
        /// values agree to rounding; the margin covers libm differences between platforms.
        /// </summary>
        private const double DATUM_TOLERANCE = 1e-9;

        private static readonly EmgShape TAILED = new EmgShape(12.0, 0.25, 0.35);
        private static readonly double[] RELATIVE_INTENSITIES = { 1.0, 0.8, 0.6, 0.5, 0.45, 0.34 };

        /// <summary>
        /// The port reproduces datum's values on fixed inputs. The expected values come from
        /// datum (maccoss/datum, Datum.Core) run on inputs built with the same expressions as the
        /// helpers below.
        /// </summary>
        [TestMethod]
        public void TestEmgMatchesDatum()
        {
            // One tailed EMG with a fixed integer noise pattern.
            var rt = Grid(60);
            var y = NoisyPeak(rt);
            var fit = EmgFit.Fit(rt, y);
            Assert.IsNotNull(fit);
            AssertRelative(1498.6264068701057, fit.Area);
            AssertRelative(12.00165183388489, fit.Shape.Mu);
            AssertRelative(0.24995198068009924, fit.Shape.Sigma);
            AssertRelative(0.3479751723876055, fit.Shape.Tau);

            // Six traces sharing one EMG, with interference on the first two.
            var rtb = Grid(120);
            var result = ConsensusEmgIntegrator.Integrate(rtb, InterferedTraces(rtb), 0, rtb.Length - 1);
            var expectedAreas = new[] { 1030.2205167143034, 830.2205167143032, 600, 500, 450, 340 };
            Assert.AreEqual(expectedAreas.Length, result.Areas.Length);
            for (int i = 0; i < expectedAreas.Length; i++)
                AssertRelative(expectedAreas[i], result.Areas[i]);

            // Height crossings of the analytic shape.
            var crossings5 = TAILED.HeightCrossings(0.05);
            AssertRelative(11.50930557905629, crossings5.Left);
            AssertRelative(13.467737949816645, crossings5.Right);
            var crossings50 = TAILED.HeightCrossings(0.5);
            AssertRelative(11.859734928065494, crossings50.Left);
            AssertRelative(12.65143328308863, crossings50.Right);

            // Linear baseline between boundaries that fall between scans.
            var sloped = y.Select((v, i) => v + 50.0 + 3.0 * i).ToArray();
            var subtracted = LinearBaseline.Subtract(rt, sloped, rt[10] + 0.013, rt[50] - 0.021);
            AssertRelative(12781.137875234968, subtracted.Sum());
            AssertRelative(2.279977725051509, subtracted[10]);
            AssertRelative(1062.5222136016919, subtracted[30]);
            Assert.AreEqual(0.0, subtracted[55]);
        }

        /// <summary>
        /// The behavior Quasar relies on: the fit recovers a known area, the consensus areas of
        /// clean traces are exact while independent fits of interfered traces are not, and too
        /// few points give no result rather than a bad one.
        /// </summary>
        [TestMethod]
        public void TestConsensusEmgRejectsMinorityInterference()
        {
            var rt = Grid(120);
            var traces = InterferedTraces(rt);
            var result = ConsensusEmgIntegrator.Integrate(rt, traces, 0, rt.Length - 1);
            Assert.IsNotNull(result.Shape);
            AssertRelative(TAILED.Mu, result.Shape.Mu, 1e-6);

            double trueTotal = RELATIVE_INTENSITIES.Sum() * 1000.0;
            double consensusTotal = result.Areas.Sum();
            double perTraceTotal = traces.Sum(trace => EmgFit.Fit(rt, trace).Area);
            double consensusError = Math.Abs(consensusTotal - trueTotal) / trueTotal;
            double perTraceError = Math.Abs(perTraceTotal - trueTotal) / trueTotal;
            Assert.IsTrue(consensusError < 0.05, @"Consensus error " + consensusError);
            Assert.IsTrue(consensusError < perTraceError / 3.0,
                @"Consensus error " + consensusError + @" vs per-trace " + perTraceError);

            // Clean traces under a clean consensus come back at their true areas.
            for (int i = 2; i < RELATIVE_INTENSITIES.Length; i++)
                AssertRelative(RELATIVE_INTENSITIES[i] * 1000.0, result.Areas[i], 1e-6);

            // A clean single peak is recovered by the plain fit.
            var clean = rt.Select(t => 1500.0 * TAILED.Density(t)).ToArray();
            AssertRelative(1500.0, EmgFit.Fit(rt, clean).Area, 1e-6);

            // Too few points: no fit, and NaN areas with no shape.
            Assert.IsNull(EmgFit.Fit(rt.Take(4).ToArray(), clean.Take(4).ToArray()));
            var tooShort = ConsensusEmgIntegrator.Integrate(rt, traces, 10, 13);
            Assert.IsNull(tooShort.Shape);
            Assert.IsTrue(tooShort.Areas.All(double.IsNaN));
        }

        private static double[] Grid(int n)
        {
            var rt = new double[n];
            for (int i = 0; i < n; i++)
                rt[i] = 9.0 + 7.0 * i / (n - 1);
            return rt;
        }

        private static double[] NoisyPeak(double[] rt)
        {
            var y = new double[rt.Length];
            for (int i = 0; i < rt.Length; i++)
                y[i] = 1500.0 * TAILED.Density(rt[i]) + ((i * 37) % 11 - 5) * 3.0;
            return y;
        }

        /// <summary>
        /// Six traces with the <see cref="TAILED"/> profile at 1000 x the relative intensities, the
        /// first two carrying a Gaussian interference of height 600 at 12.5 minutes.
        /// </summary>
        private static double[][] InterferedTraces(double[] rt)
        {
            var traces = new double[RELATIVE_INTENSITIES.Length][];
            for (int f = 0; f < traces.Length; f++)
            {
                traces[f] = new double[rt.Length];
                for (int i = 0; i < rt.Length; i++)
                {
                    traces[f][i] = RELATIVE_INTENSITIES[f] * 1000.0 * TAILED.Density(rt[i]);
                    if (f < 2)
                    {
                        double d = rt[i] - 12.5;
                        traces[f][i] += 600.0 * Math.Exp(-(d * d) / (2.0 * 0.18 * 0.18));
                    }
                }
            }

            return traces;
        }

        private static void AssertRelative(double expected, double actual, double tolerance = DATUM_TOLERANCE)
        {
            Assert.AreEqual(expected, actual, Math.Abs(expected) * tolerance + 1e-12);
        }
    }
}
