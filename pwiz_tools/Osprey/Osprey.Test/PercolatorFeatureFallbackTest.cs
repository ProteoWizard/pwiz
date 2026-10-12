/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.FDR;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The basic-feature fallback the streaming score passes apply to an entry whose
    /// parquet row cannot be resolved (<see cref="PercolatorScorer.ResolveFeatureRow"/>).
    /// It used to be covered through the removed <c>PercolatorEntryBuilder</c>; the
    /// fallback vector itself moved into <see cref="PercolatorScorer"/> with it.
    /// </summary>
    [TestClass]
    public class PercolatorFeatureFallbackTest
    {
        /// <summary>
        /// A resolvable index returns the live parquet row by reference; an index past the
        /// end, a <c>uint.MaxValue</c> appended entry, or a null row list falls back to a
        /// fresh full-length vector carrying the basic coelution-derived features.
        /// </summary>
        [TestMethod]
        public void TestResolveFeatureRowFallback()
        {
            int nFeat = ScoringTaskShared.NUM_PIN_FEATURES;
            var liveRow = new double[nFeat];
            liveRow[0] = 42.0;
            var rows = new[] { new double[nFeat], liveRow };

            // Resolvable: the live row, not a copy.
            Assert.AreSame(liveRow, PercolatorScorer.ResolveFeatureRow(rows, 1, 7.0, nFeat));

            // Out of range, appended (uint.MaxValue) and no rows at all: basic fallback.
            AssertBasicFallback(PercolatorScorer.ResolveFeatureRow(rows, 2, 4.0, nFeat), 4.0, nFeat);
            AssertBasicFallback(PercolatorScorer.ResolveFeatureRow(rows, uint.MaxValue, 2.0, nFeat), 2.0, nFeat);
            AssertBasicFallback(PercolatorScorer.ResolveFeatureRow(null, 0, 6.0, nFeat), 6.0, nFeat);
        }

        private static void AssertBasicFallback(double[] features, double coelutionSum, int nFeat)
        {
            Assert.AreEqual(nFeat, features.Length);
            Assert.AreEqual(coelutionSum, features[0]); // coelution_sum
            Assert.AreEqual(coelutionSum * 0.5, features[1]); // coelution_max approximation
            Assert.AreEqual(3.0, features[2]); // n_coeluting_fragments
            for (int i = 3; i < nFeat; i++)
                Assert.AreEqual(0.0, features[i]);
        }
    }
}
