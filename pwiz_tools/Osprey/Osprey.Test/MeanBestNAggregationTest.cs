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
using pwiz.Osprey.Core;
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

        private bool _savedFloorMean;
        private double? _savedFloorPercentile;
        private int _savedMeanBestN;

        /// <summary>
        /// Pin the missing-run floor to its default (decoy MEDIAN) for the duration of each test.
        /// Every assertion below is an exact floor-dependent value, and the floor toggles are read
        /// from the process environment at type load - so on the machine actually running a floor
        /// A/B sweep (OSPREY_MEANBEST2_FLOOR_MEAN=1 exported) these tests would compute
        /// mean(-3,-1,0) = -1.3333 where they assert a median of -1, and fail for reasons that
        /// have nothing to do with the code under test.
        /// </summary>
        [TestInitialize]
        public void PinFloorToMedian()
        {
            _savedFloorMean = OspreyEnvironment.MeanBest2FloorMean;
            _savedFloorPercentile = OspreyEnvironment.MeanBest2FloorPercentile;
            _savedMeanBestN = OspreyEnvironment.MeanBestN;
            OspreyEnvironment.MeanBest2FloorMean = false;
            OspreyEnvironment.MeanBest2FloorPercentile = null;
            OspreyEnvironment.MeanBestN = 0;
        }

        [TestCleanup]
        public void RestoreFloor()
        {
            OspreyEnvironment.MeanBest2FloorMean = _savedFloorMean;
            OspreyEnvironment.MeanBest2FloorPercentile = _savedFloorPercentile;
            OspreyEnvironment.MeanBestN = _savedMeanBestN;
        }

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
        /// The top-N EVICTION branch, with hand-computed values. A group larger than N must keep
        /// the N HIGHEST scores and discard the rest; the interesting failure is discarding the
        /// wrong element (dropping the largest, or an off-by-one in the shift loop).
        ///
        /// This is the only oracle for that branch. The streaming-vs-resident parity tests cannot
        /// cover it: both sides call the same <c>MeanBestNAcc</c>, so a wrong-element eviction
        /// produces the same wrong aggregate on both and the comparison still passes. Scores are
        /// fed in a deliberately awkward order (ascending, descending and interleaved across the
        /// three base_ids) so a buffer that only happens to work on sorted input fails here.
        /// </summary>
        [TestMethod]
        public void TestMeanBestNEvictsSmallest()
        {
            // base10 ascending {1,2,3,4,5}, base20 descending {9,7,5,3,1}, base30 interleaved
            // {2,8,4,6}. Decoys {-3,-1,0} -> median floor -1, and every target group is larger
            // than N so no floor is used on the target side.
            var scores = new[]
            {
                1.0, 2.0, 3.0, 4.0, 5.0,
                9.0, 7.0, 5.0, 3.0, 1.0,
                2.0, 8.0, 4.0, 6.0,
                -3.0, -1.0, 0.0
            };
            var labels = new[]
            {
                false, false, false, false, false,
                false, false, false, false, false,
                false, false, false, false,
                true, true, true
            };
            var entryIds = new uint[]
            {
                10, 10, 10, 10, 10,
                20, 20, 20, 20, 20,
                30, 30, 30, 30,
                10 | DECOY_BIT, 20 | DECOY_BIT, 30 | DECOY_BIT
            };

            var n2 = TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, 2);
            Assert.AreEqual(4.5, n2[0], 1e-12, @"base10 top-2 of {1,2,3,4,5} = mean(5,4)");
            Assert.AreEqual(8.0, n2[5], 1e-12, @"base20 top-2 of {9,7,5,3,1} = mean(9,7)");
            Assert.AreEqual(7.0, n2[10], 1e-12, @"base30 top-2 of {2,8,4,6} = mean(8,6)");

            var n3 = TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, 3);
            Assert.AreEqual(4.0, n3[0], 1e-12, @"base10 top-3 of {1,2,3,4,5} = mean(5,4,3)");
            Assert.AreEqual(7.0, n3[5], 1e-12, @"base20 top-3 of {9,7,5,3,1} = mean(9,7,5)");
            Assert.AreEqual(6.0, n3[10], 1e-12, @"base30 top-3 of {2,8,4,6} = mean(8,6,4)");
        }

        /// <summary>
        /// A NaN observation must not poison the accumulator. Unlike the MAX aggregation this
        /// replaced - where a NaN simply lost every comparison - a NaN admitted into the top-N
        /// buffer breaks the ascending invariant, can never be evicted (<c>score &gt; _top[0]</c>
        /// is false against NaN), and makes every row of that base_id aggregate to NaN, which then
        /// loses its target/decoy competition silently. The guard drops it, so the remaining real
        /// observations decide the aggregate.
        /// </summary>
        [TestMethod]
        public void TestMeanBestNIgnoresNaN()
        {
            // base10 takes the NaN SECOND (the Add path) and base20 takes it FIRST (the First
            // path, which seeds the buffer). Position matters: an earlier version of this test
            // only covered the Add path, so a missing guard in First - where a NaN occupies
            // _top[0] permanently and can never be evicted - passed unnoticed.
            var scores = new[] { 5.0, double.NaN, 7.0, double.NaN, 4.0, 6.0, -3.0, -1.0, 0.0 };
            var labels = new[] { false, false, false, false, false, false, true, true, true };
            var entryIds = new uint[]
                { 10, 10, 10, 20, 20, 20, 10 | DECOY_BIT, 20 | DECOY_BIT, 30 | DECOY_BIT };

            var agg = TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, 2);

            Assert.IsFalse(double.IsNaN(agg[0]), @"NaN mid-group must not propagate");
            Assert.AreEqual(6.0, agg[0], 1e-12, @"base10 = mean(7,5), the NaN dropped entirely");
            Assert.IsFalse(double.IsNaN(agg[3]), @"NaN as the FIRST observation must not propagate");
            Assert.AreEqual(5.0, agg[3], 1e-12, @"base20 = mean(6,4), the leading NaN dropped");
        }

        /// <summary>
        /// An infinite score is excluded for a reason NaN alone does not cover: AggregateScore's
        /// missing-run term is <c>(n - _len) * floor</c>, which for a FULLY detected group is
        /// <c>0.0 * floor</c> - and 0*Infinity is NaN. So one infinite value admitted anywhere
        /// would poison every base_id in the experiment through the shared floor, not just its own.
        /// </summary>
        [TestMethod]
        public void TestMeanBestNIgnoresInfinity()
        {
            var scores = new[] { 5.0, double.PositiveInfinity, 7.0, -3.0, -1.0, 0.0 };
            var labels = new[] { false, false, false, true, true, true };
            var entryIds = new uint[] { 10, 10, 10, 10 | DECOY_BIT, 20 | DECOY_BIT, 30 | DECOY_BIT };

            var agg = TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, 2);

            foreach (double a in agg)
                Assert.IsFalse(double.IsNaN(a), @"an infinite observation must not produce NaN");
            Assert.AreEqual(6.0, agg[0], 1e-12, @"base10 = mean(7,5), the infinity dropped");
        }

        /// <summary>
        /// An infinite DECOY score must not reach the floor SAMPLE either. This is a distinct hole
        /// from the accumulator guard above: the floor is one global scalar, so with
        /// OSPREY_MEANBEST2_FLOOR_MEAN the mean of the decoy sample goes infinite and then
        /// AggregateScore's <c>(n - _len) * floor</c> is 0*Infinity == NaN for EVERY base_id,
        /// including fully-detected target groups that never saw a bad value themselves. Exercises
        /// the mean branch specifically, since the median/percentile branches route an infinity to
        /// the out-of-range bucket instead.
        /// </summary>
        [TestMethod]
        public void TestMeanBestNFloorSampleIgnoresInfinity()
        {
            OspreyEnvironment.MeanBest2FloorMean = true; // Restored by RestoreFloor.

            // base10 is fully detected (2 of 2) and base20 is single-run, so base20 uses the floor.
            // One decoy score is infinite; without the guard it makes the mean floor infinite and
            // NaNs BOTH groups.
            var scores = new[] { 5.0, 7.0, 4.0, -3.0, double.PositiveInfinity, -1.0 };
            var labels = new[] { false, false, false, true, true, true };
            var entryIds = new uint[]
                { 10, 10, 20, 10 | DECOY_BIT, 20 | DECOY_BIT, 30 | DECOY_BIT };

            var agg = TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, 2);

            foreach (double a in agg)
                Assert.IsFalse(double.IsNaN(a), @"an infinite decoy score must not NaN the floor");
            Assert.AreEqual(6.0, agg[0], 1e-12, @"fully-detected group is unaffected by the floor");
            // Floor = mean of the finite decoys (-3, -1) = -2; base20 = (4 + -2)/2 = 1.
            Assert.AreEqual(1.0, agg[2], 1e-12, @"single-run group uses the finite-only decoy mean");
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

        /// <summary>
        /// The experiment-q wrappers must be pure expansions of the bounded maps they document
        /// themselves as expanding, for EITHER value of <c>applyExperimentAgg</c>.
        ///
        /// This is the invariant that broke silently. The bounded maps gained the pass gate while
        /// the full-length wrappers did not even take the parameter, so under mean(best-N) the
        /// RESIDENT 2nd pass kept re-aggregating while the PROJECTION 2nd pass did not - and those
        /// two paths are each other's byte-identity oracle. A per-row disagreement between a
        /// wrapper and its own map is exactly the shape of that defect, and nothing asserted it.
        /// </summary>
        [TestMethod]
        public void TestExperimentQWrappersExpandTheirMaps()
        {
            // The fixture must make the aggregation change the target/decoy INTERLEAVING, not just
            // the scores: conservative q is a function of the target/decoy SEQUENCE down the
            // ranked list, so an aggregation that reorders units without crossing a target past a
            // decoy leaves every q identical and would make this test vacuous (the first attempt
            // here did exactly that).
            //
            // Decoy sample {-5,-5,-5, 1,1,1, -3} -> median floor -3. With N = 3:
            //   t10  2 runs at 4.0 -> (4+4-3)/3   =  1.667   raw max 4.0
            //   t20  1 run  at 5.0 -> (5-3-3)/3   = -0.333   raw max 5.0
            //   d20  3 runs at 1.0 -> (1+1+1)/3   =  1.0     raw max 1.0
            //   d30  1 run  at -3  -> (-3-3-3)/3  = -3.0     raw max -3.0
            //   d10  3 runs at -5  -> -5.0                   raw max -5.0
            // raw-max order  T(5) T(4) D(1) D(-3) D(-5)  ->  T,T,D,D,D
            // aggregated     T(1.667) D(1.0) T(-0.333) ...  ->  T,D,T,D,D
            // The single-run target crosses below a decoy - the reproducibility demotion - so the
            // two arms genuinely produce different q.
            var scores = new[] { 4.0, 4.0, 5.0, -5.0, -5.0, -5.0, 1.0, 1.0, 1.0, -3.0 };
            var labels = new[] { false, false, false, true, true, true, true, true, true, true };
            var entryIds = new uint[]
            {
                10, 10, 20,
                10 | DECOY_BIT, 10 | DECOY_BIT, 10 | DECOY_BIT,
                20 | DECOY_BIT, 20 | DECOY_BIT, 20 | DECOY_BIT,
                30 | DECOY_BIT
            };
            var peptides = new[]
            {
                "PEPA", "PEPA", "PEPB",
                "DECA", "DECA", "DECA",
                "DECB", "DECB", "DECB",
                "DECC"
            };

            // Aggregation ON (the 1st-pass arm) and OFF (the 2nd pass), each checked against its
            // own map. With MeanBestN pinned to 3 the ON case genuinely aggregates, so the two
            // cases are different statistics and neither assertion is vacuous.
            OspreyEnvironment.MeanBestN = 3;
            Assert.IsTrue(OspreyEnvironment.ExperimentAggMeanBest, @"fixture must have the arm engaged");
            AssertWrappersMatchMaps(scores, labels, entryIds, peptides, true);
            AssertWrappersMatchMaps(scores, labels, entryIds, peptides, false);

            // ON and OFF must actually DIFFER on this fixture, or the two calls above would agree
            // no matter how the gate were wired and the test would prove nothing.
            var on = PercolatorQValues.ComputeExperimentPrecursorQvalues(
                scores, labels, entryIds, applyExperimentAgg: true);
            var off = PercolatorQValues.ComputeExperimentPrecursorQvalues(
                scores, labels, entryIds, applyExperimentAgg: false);
            bool differs = false;
            for (int i = 0; i < on.Length && !differs; i++)
                differs = on[i] != off[i];
            Assert.IsTrue(differs,
                @"fixture must separate the aggregated and raw-max arms, else the gate is untested");
        }

        /// <summary>
        /// The OSPREY_EXPERIMENT_AGG family validates itself before any I/O, and every check stays
        /// silent on the DEFAULT path - an operator who merely left a floor sweep exported must
        /// still be able to run an ordinary analysis.
        /// </summary>
        [TestMethod]
        public void TestExperimentAggSettingsValidation()
        {
            // Default (aggregation off): every setting is ignored, including combinations that
            // would be refused when engaged. This is the default-path guarantee.
            OspreyEnvironment.MeanBestN = 0;
            OspreyEnvironment.MeanBest2FloorMean = true;
            OspreyEnvironment.MeanBest2FloorPercentile = 500.0;
            Assert.IsNull(OspreyEnvironment.ValidateExperimentAggSettings(4),
                @"default path must not be refused over variables it never reads");

            // Both floor overrides set: not composable, refused.
            OspreyEnvironment.MeanBestN = 2;
            Assert.IsNotNull(OspreyEnvironment.ValidateExperimentAggSettings(4),
                @"both floor overrides set must be refused");

            // Percentile outside [0, 100].
            OspreyEnvironment.MeanBest2FloorMean = false;
            OspreyEnvironment.MeanBest2FloorPercentile = 500.0;
            Assert.IsNotNull(OspreyEnvironment.ValidateExperimentAggSettings(4),
                @"percentile above 100 must be refused");
            OspreyEnvironment.MeanBest2FloorPercentile = -1.0;
            Assert.IsNotNull(OspreyEnvironment.ValidateExperimentAggSettings(4),
                @"negative percentile must be refused");
            OspreyEnvironment.MeanBest2FloorPercentile = double.NaN;
            Assert.IsNotNull(OspreyEnvironment.ValidateExperimentAggSettings(4),
                @"NaN percentile must be refused");
            OspreyEnvironment.MeanBest2FloorPercentile = 0.0;
            Assert.IsNull(OspreyEnvironment.ValidateExperimentAggSettings(4), @"0 is a valid percentile");
            OspreyEnvironment.MeanBest2FloorPercentile = 100.0;
            Assert.IsNull(OspreyEnvironment.ValidateExperimentAggSettings(4), @"100 is a valid percentile");
            OspreyEnvironment.MeanBest2FloorPercentile = null;

            // N versus the run count. N > files leaves every unit floor-filled, which saturates
            // the statistic: the ranking is identical for every N at or above the largest
            // observation count, so the arm would be recorded as distinct while measuring the same
            // thing. N == files is the legitimate "detected in every run" frontier.
            OspreyEnvironment.MeanBestN = 4;
            Assert.IsNull(OspreyEnvironment.ValidateExperimentAggSettings(4), @"N == file count is allowed");
            Assert.IsNull(OspreyEnvironment.ValidateExperimentAggSettings(9), @"N < file count is allowed");
            Assert.IsNotNull(OspreyEnvironment.ValidateExperimentAggSettings(3),
                @"N greater than the file count must be refused");
            Assert.IsNull(OspreyEnvironment.ValidateExperimentAggSettings(0),
                @"an unknown file count (a per-file HPC worker) must not be refused");
        }

        /// <summary>
        /// Every run states which aggregation it used, and flipping the arm changes the cache
        /// validity suffix - while the DEFAULT arm's suffix stays EMPTY.
        ///
        /// The empty-when-default half is not cosmetic: emitting the normalized "max" spelling
        /// unconditionally changed every default run's key and invalidated every output directory
        /// produced before the suffix existed, costing hours of recompute to reproduce
        /// byte-identical output. The byte-identity gate could not see it, because that gate always
        /// starts from fresh directories.
        /// </summary>
        [TestMethod]
        public void TestExperimentAggIsReportedAndKeyed()
        {
            OspreyEnvironment.MeanBestN = 0;
            StringAssert.Contains(OspreyEnvironment.DescribeExperimentAgg(),
                OspreyEnvironment.EXPERIMENT_AGG_MAX);
            Assert.AreEqual(string.Empty, OspreyEnvironment.ExperimentAggValidityKeySuffix(),
                @"the default arm must add nothing to the validity key");
            Assert.IsFalse(OspreyEnvironment.IsMeanBestArm(OspreyEnvironment.ExperimentAgg));

            OspreyEnvironment.MeanBestN = 3;
            Assert.AreEqual(@"mean-best-3", OspreyEnvironment.ExperimentAgg,
                @"the normalized arm must track the N actually in use");
            Assert.IsTrue(OspreyEnvironment.IsMeanBestArm(OspreyEnvironment.ExperimentAgg));
            StringAssert.Contains(OspreyEnvironment.DescribeExperimentAgg(), OspreyEnvironment.ExperimentAgg);
            string keyMedian = OspreyEnvironment.ExperimentAggValidityKeySuffix();
            StringAssert.Contains(keyMedian, OspreyEnvironment.ExperimentAgg);

            // The floor arm is part of the identity of a run's output, so it must move the key too
            // - a floor sweep in one output directory would otherwise reuse the previous arm's q
            // as the new arm's measurement.
            OspreyEnvironment.MeanBest2FloorMean = true;
            Assert.AreNotEqual(keyMedian, OspreyEnvironment.ExperimentAggValidityKeySuffix(),
                @"the decoy-mean floor must change the validity key");
            OspreyEnvironment.MeanBest2FloorMean = false;
            OspreyEnvironment.MeanBest2FloorPercentile = 5.0;
            Assert.AreNotEqual(keyMedian, OspreyEnvironment.ExperimentAggValidityKeySuffix(),
                @"a percentile floor must change the validity key");

            // A different N is a different statistic, so it must key differently too.
            OspreyEnvironment.MeanBest2FloorPercentile = null;
            OspreyEnvironment.MeanBestN = 4;
            Assert.AreNotEqual(keyMedian, OspreyEnvironment.ExperimentAggValidityKeySuffix(),
                @"a different N must change the validity key");
        }

        private static void AssertWrappersMatchMaps(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides, bool applyExperimentAgg)
        {
            string which = applyExperimentAgg ? @"agg-on" : @"agg-off";

            var precMap = PercolatorQValues.ComputeExperimentPrecursorQMap(
                scores, labels, entryIds, applyExperimentAgg);
            var precRows = PercolatorQValues.ComputeExperimentPrecursorQvalues(
                scores, labels, entryIds, applyExperimentAgg);
            for (int i = 0; i < scores.Length; i++)
            {
                // Full entry_id, NOT base_id: the map is keyed by the WINNER of each target/decoy
                // competition, so the losing side keeps 1.0 rather than inheriting the winner's q.
                double expected = precMap.TryGetValue(entryIds[i], out double q) ? q : 1.0;
                Assert.AreEqual(expected, precRows[i], 0.0,
                    string.Format(@"{0}: precursor wrapper row {1} must equal its map entry", which, i));
            }

            var peptMap = PercolatorQValues.ComputeExperimentPeptideQMap(
                scores, labels, entryIds, peptides, applyExperimentAgg);
            var peptRows = PercolatorQValues.ComputeExperimentPeptideQvalues(
                scores, labels, entryIds, peptides, applyExperimentAgg);
            for (int i = 0; i < scores.Length; i++)
            {
                double expected = peptMap.TryGetValue(peptides[i], out double q) ? q : 1.0;
                Assert.AreEqual(expected, peptRows[i], 0.0,
                    string.Format(@"{0}: peptide wrapper row {1} must equal its map entry", which, i));
            }
        }
    }
}
