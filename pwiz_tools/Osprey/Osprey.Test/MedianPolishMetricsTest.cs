/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for the <see cref="TukeyMedianPolish"/> post-fit quality metrics
    /// (LibCosine, ResidualRatio, MinFragmentR2, ResidualCorrelation), which the
    /// unit suite did not previously exercise. Uses a perfectly multiplicative
    /// (fragment scale x elution profile) fixture whose ln-space matrix is exactly
    /// additive, so a converged polish leaves ~zero residuals.
    /// </summary>
    [TestClass]
    public class MedianPolishMetricsTest
    {
        // Elution profile shared by every fragment; fragment f is scale[f] * profile.
        private static readonly double[] PROFILE = { 1.0, 4.0, 8.0, 4.0, 1.0 };
        private static readonly double[] SCALES = { 100.0, 50.0, 25.0 };
        private static readonly double[] RTS = { 1.0, 2.0, 3.0, 4.0, 5.0 };

        private static TukeyMedianPolishResult CleanPolish()
        {
            var xics = new List<KeyValuePair<int, double[]>>();
            for (int f = 0; f < SCALES.Length; f++)
            {
                var row = new double[PROFILE.Length];
                for (int s = 0; s < PROFILE.Length; s++)
                    row[s] = SCALES[f] * PROFILE[s];
                xics.Add(new KeyValuePair<int, double[]>(f, row));
            }
            var polish = TukeyMedianPolish.Compute(xics, RTS, 20, 1e-6);
            Assert.IsNotNull(polish, "Compute should fit the additive fixture");
            return polish;
        }

        #region Null-input guards

        [TestMethod]
        public void TestMetricsNullPolishDefaults()
        {
            // Each metric has a defined default for a missing fit.
            Assert.AreEqual(1.0, TukeyMedianPolish.ResidualRatio(null), 1e-12);
            Assert.AreEqual(0.0, TukeyMedianPolish.MinFragmentR2(null), 1e-12);
            Assert.AreEqual(0.0, TukeyMedianPolish.ResidualCorrelation(null), 1e-12);
            Assert.AreEqual(0.0,
                TukeyMedianPolish.LibCosine(null, new List<LibraryFragment>()), 1e-12);
        }

        #endregion

        #region Clean-fixture behavior

        [TestMethod]
        public void TestResidualRatioNearZeroForCleanFit()
        {
            // A perfectly additive ln-matrix leaves predicted == observed, so the
            // linear-space residual ratio collapses to ~0.
            double ratio = TukeyMedianPolish.ResidualRatio(CleanPolish());
            Assert.IsTrue(ratio >= 0.0 && ratio < 0.01,
                string.Format("Clean fit residual ratio should be ~0, got {0:E3}", ratio));
        }

        [TestMethod]
        public void TestMinFragmentR2HighForCleanFit()
        {
            // Predicted and observed sqrt-intensities coincide per fragment -> R^2 ~ 1.
            double minR2 = TukeyMedianPolish.MinFragmentR2(CleanPolish());
            Assert.IsTrue(minR2 > 0.99,
                string.Format("Clean fit min fragment R^2 should be ~1, got {0:F4}", minR2));
        }

        [TestMethod]
        public void TestResidualCorrelationFiniteForCleanFit()
        {
            // With near-zero residuals there is no co-eluting interferer signal; the
            // mean pairwise residual correlation stays a finite value in [-1, 1].
            double corr = TukeyMedianPolish.ResidualCorrelation(CleanPolish());
            Assert.IsFalse(double.IsNaN(corr) || double.IsInfinity(corr),
                "Residual correlation should be finite");
            Assert.IsTrue(corr >= -1.0 && corr <= 1.0,
                string.Format("Residual correlation out of range: {0}", corr));
        }

        [TestMethod]
        public void TestLibCosineMatchesParallelLibrary()
        {
            // Row effects recover the fragment scales (100:50:25). A library whose
            // relative intensities are parallel to those scales gives cosine ~1
            // (cosine is scale-invariant).
            var polish = CleanPolish();
            var library = new List<LibraryFragment>
            {
                new LibraryFragment { Mz = 300.0, RelativeIntensity = 100.0f },
                new LibraryFragment { Mz = 400.0, RelativeIntensity = 50.0f },
                new LibraryFragment { Mz = 500.0, RelativeIntensity = 25.0f },
            };

            double cosine = TukeyMedianPolish.LibCosine(polish, library);
            Assert.IsTrue(cosine > 0.99,
                string.Format("LibCosine for a parallel library should be ~1, got {0:F4}", cosine));
        }

        #endregion
    }
}
