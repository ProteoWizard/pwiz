/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

// Tests for OspreySharp.FDR.Reconciliation module.
// Ports the Rust reconciliation tests in
// osprey/crates/osprey/src/reconciliation.rs.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR.Reconciliation;

namespace pwiz.OspreySharp.Test
{
    [TestClass]
    public class ReconciliationTest
    {
        private const double TOLERANCE = 1e-9;

        #region WeightedMedian helper

        [TestMethod]
        public void TestWeightedMedianEmpty()
        {
            Assert.AreEqual(0.0, ConsensusRts.WeightedMedian(Array.Empty<(double, double)>()));
        }

        [TestMethod]
        public void TestWeightedMedianSingle()
        {
            Assert.AreEqual(3.5, ConsensusRts.WeightedMedian(new[] { (3.5, 1.0) }));
        }

        [TestMethod]
        public void TestWeightedMedianEqualWeights()
        {
            // Values 1, 2, 3, 4, 5 each with weight 1.0. Half = 2.5. Cumulative at
            // index 2 (value 3) reaches 3.0 >= 2.5 first, so median = 3.
            var pairs = new[] { (1.0, 1.0), (2.0, 1.0), (3.0, 1.0), (4.0, 1.0), (5.0, 1.0) };
            Assert.AreEqual(3.0, ConsensusRts.WeightedMedian(pairs));
        }

        [TestMethod]
        public void TestWeightedMedianUnequalWeights()
        {
            // One dominant weight pulls the median toward that value.
            // Values 1, 2, 10 with weights 1, 1, 10. Total = 12, half = 6.
            // Cumulative: 1 (<6), 2 (<6), 12 (>=6) → median = 10.
            var pairs = new[] { (1.0, 1.0), (2.0, 1.0), (10.0, 10.0) };
            Assert.AreEqual(10.0, ConsensusRts.WeightedMedian(pairs));
        }

        [TestMethod]
        public void TestWeightedMedianInsensitiveToInputOrder()
        {
            var sorted = new[] { (1.0, 0.5), (2.0, 0.5), (3.0, 5.0), (4.0, 0.5), (5.0, 0.5) };
            var shuffled = new[] { (3.0, 5.0), (5.0, 0.5), (1.0, 0.5), (4.0, 0.5), (2.0, 0.5) };
            Assert.AreEqual(ConsensusRts.WeightedMedian(sorted),
                              ConsensusRts.WeightedMedian(shuffled));
        }

        #endregion

        #region ComputeConsensusRts end-to-end

        [TestMethod]
        public void TestComputeEmptyReturnsEmpty()
        {
            var result = ConsensusRts.Compute(
                new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>(),
                new Dictionary<string, RTCalibration>(),
                consensusFdr: 0.01,
                proteinFdrThreshold: 0.0);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void TestComputeThreeFileTargetWithPairedDecoy()
        {
            // Three files, identity RT calibration on each. One target peptide
            // detected at apex 10.0, 10.1, 9.9 across the three runs — consensus
            // library RT should land at one of those values (sigmoid-weighted,
            // non-interpolated cumulative median).
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration>
            {
                { @"f1", cal }, { @"f2", cal }, { @"f3", cal }
            };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"PEPTIDE1", apexRt: 10.0, score: 3.0)),
                Pair(@"f2", PassingTarget(@"PEPTIDE1", apexRt: 10.1, score: 3.0)),
                Pair(@"f3", PassingTarget(@"PEPTIDE1", apexRt: 9.9,  score: 3.0)),
            };

            var consensus = ConsensusRts.Compute(perFile, cals, 0.01, 0.0);

            Assert.AreEqual(1, consensus.Count);
            var entry = consensus[0];
            Assert.AreEqual(@"PEPTIDE1", entry.ModifiedSequence);
            Assert.IsFalse(entry.IsDecoy);
            Assert.AreEqual(3, entry.NRunsDetected);
            Assert.IsTrue(entry.ApexLibraryRtMad.HasValue,
                @"MAD should be populated with 3 runs");
            Assert.IsTrue(Math.Abs(entry.ConsensusLibraryRt - 10.0) <= 0.1,
                string.Format(@"consensus should be near 10.0, got {0}",
                              entry.ConsensusLibraryRt));
        }

        [TestMethod]
        public void TestComputePairedDecoyIncludedWhenTargetQualifies()
        {
            // Target qualifies in f1 + f2. A paired decoy (DECOY_PEPTIDE1)
            // appears in f3. The decoy consensus should be emitted even though
            // the decoy itself does not pass qualification.
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration>
            {
                { @"f1", cal }, { @"f2", cal }, { @"f3", cal }
            };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"PEPTIDE1", apexRt: 10.0, score: 3.0)),
                Pair(@"f2", PassingTarget(@"PEPTIDE1", apexRt: 10.1, score: 3.0)),
                Pair(@"f3", Decoy(@"DECOY_PEPTIDE1", apexRt: 15.0, score: -1.0)),
            };

            var consensus = ConsensusRts.Compute(perFile, cals, 0.01, 0.0);

            Assert.AreEqual(2, consensus.Count);
            Assert.IsFalse(consensus[0].IsDecoy);
            Assert.AreEqual(@"PEPTIDE1", consensus[0].ModifiedSequence);
            Assert.IsTrue(consensus[1].IsDecoy);
            Assert.AreEqual(@"DECOY_PEPTIDE1", consensus[1].ModifiedSequence);
        }

        [TestMethod]
        public void TestComputeRejectsLowPrecursorQvalueDespiteProteinRescue()
        {
            // Mirrors Rust test_consensus_rejects_low_precursor_q_despite_protein_rescue.
            // Hard precursor-q gate must reject a target even when its protein
            // q-value would otherwise rescue it. Consensus is driven by the
            // detection's own apex_rt, so poor precursor-level evidence cannot
            // be rescued by protein-level aggregate evidence.
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration>
            {
                { @"f1", cal }, { @"f2", cal }
            };

            var weakPrecursor = new FdrEntry
            {
                EntryId = 1,
                IsDecoy = false,
                Charge = 2,
                ApexRt = 10.0,
                StartRt = 9.5,
                EndRt = 10.5,
                CoelutionSum = 1.0,
                Score = 3.0,
                RunPrecursorQvalue = 0.20,  // fails hard gate
                RunPeptideQvalue = 0.02,    // fails peptide too
                RunProteinQvalue = 0.001,   // would rescue if allowed
                ModifiedSequence = @"PEPTIDE1",
            };
            var goodTarget = PassingTarget(@"PEPTIDE2", apexRt: 20.0, score: 3.0);

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", new[] { weakPrecursor }),
                Pair(@"f2", goodTarget),
            };

            var consensus = ConsensusRts.Compute(
                perFile, cals,
                consensusFdr: 0.01,
                proteinFdrThreshold: 0.01);

            Assert.AreEqual(1, consensus.Count);
            Assert.AreEqual(@"PEPTIDE2", consensus[0].ModifiedSequence);
        }

        [TestMethod]
        public void TestComputeProteinRescueUpgradesBorderlinePeptideQvalue()
        {
            // Precursor-q passes the hard gate, peptide-q is borderline but
            // fails consensus_fdr, protein-q passes protein threshold →
            // detection should be included.
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var borderline = new FdrEntry
            {
                EntryId = 1,
                IsDecoy = false,
                Charge = 2,
                ApexRt = 10.0,
                StartRt = 9.5,
                EndRt = 10.5,
                CoelutionSum = 1.0,
                Score = 3.0,
                RunPrecursorQvalue = 0.005, // passes hard gate
                RunPeptideQvalue = 0.05,    // fails borderline
                RunProteinQvalue = 0.005,   // rescues
                ModifiedSequence = @"PEPTIDE1",
            };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", new[] { borderline }),
            };

            var consensus = ConsensusRts.Compute(
                perFile, cals,
                consensusFdr: 0.01,
                proteinFdrThreshold: 0.01);

            Assert.AreEqual(1, consensus.Count);
            Assert.AreEqual(@"PEPTIDE1", consensus[0].ModifiedSequence);

            // Repeat with protein rescue disabled — same detection must be rejected.
            var consensusNoRescue = ConsensusRts.Compute(
                perFile, cals,
                consensusFdr: 0.01,
                proteinFdrThreshold: 0.0);
            Assert.AreEqual(0, consensusNoRescue.Count);
        }

        [TestMethod]
        public void TestComputeSigmoidWeightingDownweightsNegativeScores()
        {
            // Mirrors Rust test_consensus_weighting_downweights_negative_score_detections.
            // Three detections: a strong positive-score detection at 10.0 and
            // two wrong-peak weak negative-score detections at 20.0. Sigmoid
            // weighting (~0.95 vs ~0.02) means the positive detection
            // dominates the weighted median.
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration>
            {
                { @"f1", cal }, { @"f2", cal }, { @"f3", cal }
            };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"PEPTIDE1", apexRt: 10.0, score: 3.0)),
                Pair(@"f2", PassingTarget(@"PEPTIDE1", apexRt: 20.0, score: -4.0)),
                Pair(@"f3", PassingTarget(@"PEPTIDE1", apexRt: 20.0, score: -4.0)),
            };

            var consensus = ConsensusRts.Compute(perFile, cals, 0.01, 0.0);

            Assert.AreEqual(1, consensus.Count);
            Assert.AreEqual(10.0, consensus[0].ConsensusLibraryRt, TOLERANCE);
        }

        [TestMethod]
        public void TestComputeFiltersNonPositiveCoelutionSum()
        {
            // Two detections: one with coelution_sum = 0 (anti-correlated /
            // forced integration at empty region), one positive. The
            // non-positive coelution entry must be excluded from the weighted
            // median; the remaining detection provides the consensus.
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration>
            {
                { @"f1", cal }, { @"f2", cal }
            };

            var zeroCoelution = new FdrEntry
            {
                EntryId = 1, IsDecoy = false, Charge = 2,
                ApexRt = 30.0, StartRt = 29.0, EndRt = 31.0,
                CoelutionSum = 0.0,
                Score = 3.0,
                RunPrecursorQvalue = 0.0, RunPeptideQvalue = 0.0,
                ModifiedSequence = @"PEPTIDE1",
            };
            var good = PassingTarget(@"PEPTIDE1", apexRt: 10.0, score: 3.0);

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", new[] { zeroCoelution }),
                Pair(@"f2", good),
            };

            var consensus = ConsensusRts.Compute(perFile, cals, 0.01, 0.0);

            Assert.AreEqual(1, consensus.Count);
            Assert.AreEqual(1, consensus[0].NRunsDetected);
            Assert.AreEqual(10.0, consensus[0].ConsensusLibraryRt, TOLERANCE);
        }

        [TestMethod]
        public void TestComputeMadNullBelowThreeDetections()
        {
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration>
            {
                { @"f1", cal }, { @"f2", cal }
            };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"PEPTIDE1", apexRt: 10.0, score: 3.0)),
                Pair(@"f2", PassingTarget(@"PEPTIDE1", apexRt: 11.0, score: 3.0)),
            };

            var consensus = ConsensusRts.Compute(perFile, cals, 0.01, 0.0);
            Assert.AreEqual(1, consensus.Count);
            Assert.AreEqual(2, consensus[0].NRunsDetected);
            Assert.IsFalse(consensus[0].ApexLibraryRtMad.HasValue);
        }

        [TestMethod]
        public void TestComputeDeterministicSortOrder()
        {
            // Two target peptides + one matching decoy. Output must be:
            // targets first (by modified_sequence ordinal), decoys last.
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", new[]
                {
                    MakeEntry(@"ZETA",  apexRt: 10.0, score: 3.0, isDecoy: false, precursorQ: 0.0, peptideQ: 0.0),
                    MakeEntry(@"ALPHA", apexRt: 10.0, score: 3.0, isDecoy: false, precursorQ: 0.0, peptideQ: 0.0),
                    MakeEntry(@"DECOY_ZETA", apexRt: 15.0, score: -1.0, isDecoy: true,
                              precursorQ: 1.0, peptideQ: 1.0),
                }),
            };

            var consensus = ConsensusRts.Compute(perFile, cals, 0.01, 0.0);

            Assert.AreEqual(3, consensus.Count);
            Assert.AreEqual(@"ALPHA", consensus[0].ModifiedSequence);
            Assert.IsFalse(consensus[0].IsDecoy);
            Assert.AreEqual(@"ZETA", consensus[1].ModifiedSequence);
            Assert.IsFalse(consensus[1].IsDecoy);
            Assert.AreEqual(@"DECOY_ZETA", consensus[2].ModifiedSequence);
            Assert.IsTrue(consensus[2].IsDecoy);
        }

        [TestMethod]
        public void TestComputeTargetWithoutCalibrationSkipped()
        {
            // File with no calibration entry → detection silently skipped
            // (matches Rust behavior: HashMap lookup returns None, detection
            // contributes nothing).
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"PEPTIDE1", apexRt: 10.0, score: 3.0)),
                Pair(@"f_no_cal", PassingTarget(@"PEPTIDE1", apexRt: 99.0, score: 3.0)),
            };

            var consensus = ConsensusRts.Compute(perFile, cals, 0.01, 0.0);
            Assert.AreEqual(1, consensus.Count);
            Assert.AreEqual(1, consensus[0].NRunsDetected);
            Assert.AreEqual(10.0, consensus[0].ConsensusLibraryRt, TOLERANCE);
        }

        #endregion

        #region Fixture helpers

        /// <summary>
        /// Identity LOESS calibration fit on 30 equally-spaced (x, x) points so
        /// InversePredict(y) = y for y in [0, 29].
        /// </summary>
        private static RTCalibration IdentityCalibration()
        {
            int n = 30;
            var libRts = new double[n];
            var measRts = new double[n];
            for (int i = 0; i < n; i++)
            {
                libRts[i] = i;
                measRts[i] = i;
            }
            var config = new RTCalibratorConfig
            {
                Bandwidth = 0.3,
                Degree = 1,
                MinPoints = 5,
                RobustnessIterations = 0,
                OutlierRetention = 1.0,
            };
            return new RTCalibrator(config).Fit(libRts, measRts);
        }

        private static KeyValuePair<string, IReadOnlyList<FdrEntry>> Pair(
            string fileName, IReadOnlyList<FdrEntry> entries)
        {
            return new KeyValuePair<string, IReadOnlyList<FdrEntry>>(fileName, entries);
        }

        private static IReadOnlyList<FdrEntry> PassingTarget(
            string modifiedSequence, double apexRt, double score)
        {
            return new[]
            {
                MakeEntry(modifiedSequence, apexRt, score,
                          isDecoy: false, precursorQ: 0.0, peptideQ: 0.0),
            };
        }

        private static IReadOnlyList<FdrEntry> Decoy(
            string modifiedSequence, double apexRt, double score)
        {
            return new[]
            {
                MakeEntry(modifiedSequence, apexRt, score,
                          isDecoy: true, precursorQ: 1.0, peptideQ: 1.0),
            };
        }

        private static FdrEntry MakeEntry(
            string modifiedSequence, double apexRt, double score,
            bool isDecoy, double precursorQ, double peptideQ)
        {
            return new FdrEntry
            {
                EntryId = 1,
                IsDecoy = isDecoy,
                Charge = 2,
                ApexRt = apexRt,
                StartRt = apexRt - 0.5,
                EndRt = apexRt + 0.5,
                CoelutionSum = 1.0,
                Score = score,
                RunPrecursorQvalue = precursorQ,
                RunPeptideQvalue = peptideQ,
                RunProteinQvalue = 1.0,
                ModifiedSequence = modifiedSequence,
            };
        }

        #endregion
    }
}
