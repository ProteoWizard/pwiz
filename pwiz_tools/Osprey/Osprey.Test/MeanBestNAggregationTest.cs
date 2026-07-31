/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for the reproducibility mean(best-N) experiment aggregation
    /// (<see cref="TargetDecoyCompetition.ComputeBaseIdMeanBestN"/>,
    /// OSPREY_EXPERIMENT_AGG=mean-best-N): each row gets its base_id's mean of best-N per-run
    /// scores, an under-detected precursor is filled with the decoy-median floor, and both target
    /// and decoy sides are treated symmetrically.
    /// </summary>
    [TestClass]
    public class MeanBestNAggregationTest
    {
        private const uint DECOY_BIT = 0x80000000;
        private const uint BASE_ID_MASK = 0x7FFFFFFF;

        /// <summary>
        /// A >=2-run precursor scores mean(top-2); a 1-run precursor scores mean(score, floor)
        /// where floor = the decoy MEDIAN; decoys are aggregated the same way. Every row of a
        /// base_id gets the same aggregated value. Decoy scores {-3, -1, 0} -> median -1. base10
        /// target has runs {4, 2} -> mean 3; base20 target one run {5} -> (5 + (-1))/2 = 2;
        /// base10 decoy one run {-3} -> -2; base20 decoy {-1} -> -1; base30 decoy {0} -> -0.5.
        /// </summary>
        [TestMethod]
        public void TestMeanBest2AggregationAndFloor()
        {
            // idx:        0(t10)  1(t10)  2(t20)  3(d10)  4(d20)  5(d30)
            var scores = new[] { 4.0, 2.0, 5.0, -3.0, -1.0, 0.0 };
            var labels = new[] { false, false, false, true, true, true };
            var entryIds = new uint[] { 10, 10, 20, 10 | DECOY_BIT, 20 | DECOY_BIT, 30 | DECOY_BIT };

            var agg = TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, 2);

            Assert.AreEqual(3.0, agg[0], 1e-12, @"base10 target run1 = mean(4,2)");
            Assert.AreEqual(3.0, agg[1], 1e-12, @"base10 target run2 = mean(4,2)");
            Assert.AreEqual(2.0, agg[2], 1e-12, @"base20 target 1-run = mean(5, floor -1)");
            Assert.AreEqual(-2.0, agg[3], 1e-12, @"base10 decoy 1-run = mean(-3, floor -1)");
            Assert.AreEqual(-1.0, agg[4], 1e-12, @"base20 decoy 1-run = mean(-1, floor -1)");
            Assert.AreEqual(-0.5, agg[5], 1e-12, @"base30 decoy 1-run = mean(0, floor -1)");
        }

        /// <summary>
        /// N=3 (OSPREY_EXPERIMENT_AGG=mean-best-3): a &gt;=3-run precursor scores mean(top-3); a k-run
        /// precursor with k&lt;3 fills its (3-k) missing runs with the decoy-median floor. base10 target
        /// {6,4,2} -&gt; 4; base20 target {5,3} -&gt; (5+3-1)/3 = 7/3; base30 target {8} -&gt; (8-2)/3 = 2.
        /// Decoys {-3,-1,0} -&gt; median floor -1, aggregated the same way. Exercises the top-N buffer
        /// (fill, full-replace) and the multi-floor fill beyond the best-2 case.
        /// </summary>
        [TestMethod]
        public void TestMeanBest3Aggregation()
        {
            var scores = new[] { 6.0, 4.0, 2.0, 5.0, 3.0, 8.0, -3.0, -1.0, 0.0 };
            var labels = new[] { false, false, false, false, false, false, true, true, true };
            var entryIds = new uint[]
                { 10, 10, 10, 20, 20, 30, 10 | DECOY_BIT, 20 | DECOY_BIT, 30 | DECOY_BIT };

            var agg = TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, 3);

            Assert.AreEqual(4.0, agg[0], 1e-12, @"base10 target 3-run = mean(6,4,2)");
            Assert.AreEqual(7.0 / 3.0, agg[3], 1e-12, @"base20 target 2-run = mean(5,3, floor -1)");
            Assert.AreEqual(2.0, agg[5], 1e-12, @"base30 target 1-run = mean(8, floor -1, floor -1)");
            Assert.AreEqual(-5.0 / 3.0, agg[6], 1e-12, @"base10 decoy 1-run = mean(-3, -1, -1)");
            Assert.AreEqual(-1.0, agg[7], 1e-12, @"base20 decoy 1-run = mean(-1, -1, -1)");
            Assert.AreEqual(-2.0 / 3.0, agg[8], 1e-12, @"base30 decoy 1-run = mean(0, -1, -1)");
        }

        /// <summary>
        /// A single-run experiment (every base_id at one member) makes the aggregation a uniform
        /// monotonic transform x -> (x + floor)/2, so competing on the aggregated scores produces
        /// the SAME ranked winner sequence (base_id + target/decoy) as competing on the raw scores.
        /// The winner scores differ (by the affine transform); the q-values, which depend only on
        /// the ranking, are unchanged.
        /// </summary>
        [TestMethod]
        public void TestMeanBest2SingleRunMatchesMaxRanking()
        {
            var scores = new[] { 4.0, 5.0, -3.0, -1.0 };
            var labels = new[] { false, false, true, true };
            var entryIds = new uint[] { 10, 20, 10 | DECOY_BIT, 30 | DECOY_BIT };
            var indices = new[] { 0, 1, 2, 3 };

            var agg = TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, 2);

            TargetDecoyCompetition.CompeteFromIndices(scores, labels, entryIds, indices,
                out int[] wiMax, out _, out bool[] wdMax);
            TargetDecoyCompetition.CompeteFromIndices(agg, labels, entryIds, indices,
                out int[] wiMb2, out _, out bool[] wdMb2);

            Assert.AreEqual(wiMax.Length, wiMb2.Length, @"same number of winners");
            for (int i = 0; i < wiMax.Length; i++)
            {
                Assert.AreEqual(wdMax[i], wdMb2[i], @"winner target/decoy at rank " + i);
                Assert.AreEqual(entryIds[wiMax[i]] & BASE_ID_MASK, entryIds[wiMb2[i]] & BASE_ID_MASK,
                    @"winner base_id at rank " + i);
            }
        }
    }
}
