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

        #region DetermineAction

        [TestMethod]
        public void TestDetermineActionApexWithinToleranceReturnsKeep()
        {
            var cwt = new[]
            {
                new CwtCandidate { ApexRt = 12.0, StartRt = 11.5, EndRt = 12.5 },
            };
            var action = ReconciliationPlanner.DetermineAction(
                apexRt: 10.05, cwtCandidates: cwt,
                expectedMeasuredRt: 10.0, rtTolerance: 0.1, halfWidth: 0.5);
            Assert.AreSame(ReconcileAction.Keep, action);
        }

        [TestMethod]
        public void TestDetermineActionPicksClosestCwtApex()
        {
            // Current apex is at 15.0, expected is 10.0 (outside tolerance).
            // Three CWT candidates: 12.0 (outside), 9.7 (within, |dev|=0.3),
            // 10.15 (within, |dev|=0.15). Must pick index 2.
            var cwt = new[]
            {
                new CwtCandidate { ApexRt = 12.0, StartRt = 11.5, EndRt = 12.5 },
                new CwtCandidate { ApexRt = 9.7,  StartRt = 9.2,  EndRt = 10.2 },
                new CwtCandidate { ApexRt = 10.15, StartRt = 9.65, EndRt = 10.65 },
            };
            var action = ReconciliationPlanner.DetermineAction(
                apexRt: 15.0, cwtCandidates: cwt,
                expectedMeasuredRt: 10.0, rtTolerance: 0.5, halfWidth: 0.5);

            Assert.IsInstanceOfType(action, typeof(ReconcileAction.UseCwtPeak));
            var use = (ReconcileAction.UseCwtPeak)action;
            Assert.AreEqual(2, use.CandidateIndex);
            Assert.AreEqual(10.15, use.ApexRt, TOLERANCE);
            Assert.AreEqual(9.65, use.StartRt, TOLERANCE);
            Assert.AreEqual(10.65, use.EndRt, TOLERANCE);
        }

        [TestMethod]
        public void TestDetermineActionForcedIntegrationWhenNoCwtInTolerance()
        {
            var cwt = new[]
            {
                new CwtCandidate { ApexRt = 15.0, StartRt = 14.5, EndRt = 15.5 },
                new CwtCandidate { ApexRt = 20.0, StartRt = 19.5, EndRt = 20.5 },
            };
            var action = ReconciliationPlanner.DetermineAction(
                apexRt: 15.0, cwtCandidates: cwt,
                expectedMeasuredRt: 10.0, rtTolerance: 0.5, halfWidth: 0.75);

            Assert.IsInstanceOfType(action, typeof(ReconcileAction.ForcedIntegration));
            var forced = (ReconcileAction.ForcedIntegration)action;
            Assert.AreEqual(10.0, forced.ExpectedRt, TOLERANCE);
            Assert.AreEqual(0.75, forced.HalfWidth, TOLERANCE);
        }

        [TestMethod]
        public void TestDetermineActionEmptyCwtForcesIntegration()
        {
            var action = ReconciliationPlanner.DetermineAction(
                apexRt: 15.0, cwtCandidates: Array.Empty<CwtCandidate>(),
                expectedMeasuredRt: 10.0, rtTolerance: 0.5, halfWidth: 0.4);

            Assert.IsInstanceOfType(action, typeof(ReconcileAction.ForcedIntegration));
            var forced = (ReconcileAction.ForcedIntegration)action;
            Assert.AreEqual(10.0, forced.ExpectedRt, TOLERANCE);
            Assert.AreEqual(0.4, forced.HalfWidth, TOLERANCE);
        }

        [TestMethod]
        public void TestDetermineActionNullCwtForcesIntegration()
        {
            var action = ReconciliationPlanner.DetermineAction(
                apexRt: 15.0, cwtCandidates: null,
                expectedMeasuredRt: 10.0, rtTolerance: 0.5, halfWidth: 0.3);

            Assert.IsInstanceOfType(action, typeof(ReconcileAction.ForcedIntegration));
        }

        #endregion

        #region SigmaClippedMad

        [TestMethod]
        public void TestSigmaClippedMadEmpty()
        {
            Assert.AreEqual(0.0, ReconciliationPlanner.SigmaClippedMad(
                Array.Empty<double>(), clipThreshold: 1.0));
        }

        [TestMethod]
        public void TestSigmaClippedMadFallbackOnTooFewSurvivors()
        {
            // 19 small residuals + 5 large outliers. A tight clipThreshold
            // keeps 19 survivors (< min 20) so it falls back to the raw
            // (sorted) median of all 24 values.
            var residuals = new List<double>();
            for (int i = 0; i < 19; i++)
                residuals.Add(0.01 * i);
            residuals.AddRange(new[] { 5.0, 5.1, 5.2, 5.3, 5.4 });

            double mad = ReconciliationPlanner.SigmaClippedMad(residuals, clipThreshold: 1.0);

            // Sorted 24 values; median index 12 of sorted = 12 * 0.01 = 0.12.
            var sorted = residuals.ToArray();
            Array.Sort(sorted);
            Assert.AreEqual(sorted[12], mad, TOLERANCE);
        }

        [TestMethod]
        public void TestSigmaClippedMadUsesClippedMedianWhenEnoughSurvive()
        {
            // 40 residuals of 0.1 + 10 large outliers at 10.0. Clip at 1.0
            // survives 40 values; survivor median = 0.1.
            var residuals = new List<double>();
            for (int i = 0; i < 40; i++)
                residuals.Add(0.1);
            for (int i = 0; i < 10; i++)
                residuals.Add(10.0);

            double mad = ReconciliationPlanner.SigmaClippedMad(residuals, clipThreshold: 1.0);
            Assert.AreEqual(0.1, mad, TOLERANCE);
        }

        #endregion

        #region Plan

        [TestMethod]
        public void TestPlanEmptyConsensusReturnsEmpty()
        {
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };
            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"PEPTIDE1", apexRt: 10.0, score: 3.0)),
            };

            var actions = ReconciliationPlanner.Plan(
                Array.Empty<PeptideConsensusRT>(),
                perFile,
                new Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>>(),
                cals, cals, experimentFdr: 0.01);

            Assert.AreEqual(0, actions.Count);
        }

        [TestMethod]
        public void TestPlanEntryAlreadyAtExpectedRtOmittedFromMap()
        {
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var consensus = new[]
            {
                new PeptideConsensusRT
                {
                    ModifiedSequence = @"PEPTIDE1", IsDecoy = false,
                    ConsensusLibraryRt = 10.0, MedianPeakWidth = 0.8,
                    NRunsDetected = 3, ApexLibraryRtMad = 0.02,
                },
            };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"PEPTIDE1", apexRt: 10.0, score: 3.0)),
            };

            var actions = ReconciliationPlanner.Plan(
                consensus, perFile,
                new Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>>(),
                cals, cals, experimentFdr: 0.01);

            Assert.AreEqual(0, actions.Count);
        }

        [TestMethod]
        public void TestPlanWrongApexForcesIntegrationWithoutCwt()
        {
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var consensus = new[]
            {
                new PeptideConsensusRT
                {
                    ModifiedSequence = @"PEPTIDE1", IsDecoy = false,
                    ConsensusLibraryRt = 10.0, MedianPeakWidth = 0.8,
                    NRunsDetected = 3, ApexLibraryRtMad = 0.02,
                },
            };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"PEPTIDE1", apexRt: 20.0, score: 3.0)),
            };

            var actions = ReconciliationPlanner.Plan(
                consensus, perFile,
                new Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>>(),
                cals, cals, experimentFdr: 0.01);

            Assert.AreEqual(1, actions.Count);
            var action = actions[(@"f1", 0)];
            Assert.IsInstanceOfType(action, typeof(ReconcileAction.ForcedIntegration));
            var forced = (ReconcileAction.ForcedIntegration)action;
            Assert.AreEqual(10.0, forced.ExpectedRt, 1e-6);
            Assert.AreEqual(0.4, forced.HalfWidth, 1e-6);
        }

        [TestMethod]
        public void TestPlanWrongApexPicksCwtCandidate()
        {
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var consensus = new[]
            {
                new PeptideConsensusRT
                {
                    ModifiedSequence = @"PEPTIDE1", IsDecoy = false,
                    ConsensusLibraryRt = 10.0, MedianPeakWidth = 0.8,
                    NRunsDetected = 3, ApexLibraryRtMad = 0.02,
                },
            };

            // Entry ParquetIndex = 0 → look up CWT at index 0.
            var entry = MakeEntry(@"PEPTIDE1", apexRt: 20.0, score: 3.0,
                isDecoy: false, precursorQ: 0.0, peptideQ: 0.0);
            entry.ParquetIndex = 0;

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", new[] { entry }),
            };

            // Two CWT candidates for entry 0 — index 1 is within tolerance.
            var cwtForFile = new IReadOnlyList<CwtCandidate>[]
            {
                new[]
                {
                    new CwtCandidate { ApexRt = 20.0, StartRt = 19.5, EndRt = 20.5 },
                    new CwtCandidate { ApexRt = 10.1, StartRt = 9.6,  EndRt = 10.6 },
                },
            };
            var cwt = new Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>>
            {
                { @"f1", cwtForFile },
            };

            var actions = ReconciliationPlanner.Plan(
                consensus, perFile, cwt, cals, cals, experimentFdr: 0.01);

            Assert.AreEqual(1, actions.Count);
            var action = actions[(@"f1", 0)];
            Assert.IsInstanceOfType(action, typeof(ReconcileAction.UseCwtPeak));
            var use = (ReconcileAction.UseCwtPeak)action;
            Assert.AreEqual(1, use.CandidateIndex);
            Assert.AreEqual(10.1, use.ApexRt, TOLERANCE);
        }

        [TestMethod]
        public void TestPlanNonPassingPrecursorSkipped()
        {
            // Target with high run-level q-values in the only run, and high
            // experiment q-values too. Best q > experimentFdr → not in the
            // passingPrecursors set → skipped even though the peptide is in
            // consensus.
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var consensus = new[]
            {
                new PeptideConsensusRT
                {
                    ModifiedSequence = @"PEPTIDE1", IsDecoy = false,
                    ConsensusLibraryRt = 10.0, MedianPeakWidth = 0.8,
                    NRunsDetected = 3, ApexLibraryRtMad = 0.02,
                },
            };

            var failing = new FdrEntry
            {
                EntryId = 1, IsDecoy = false, Charge = 2,
                ApexRt = 20.0, StartRt = 19.5, EndRt = 20.5,
                CoelutionSum = 1.0, Score = 1.0,
                RunPrecursorQvalue = 0.9, RunPeptideQvalue = 0.9,
                ExperimentPrecursorQvalue = 0.9, ExperimentPeptideQvalue = 0.9,
                ModifiedSequence = @"PEPTIDE1",
            };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", new[] { failing }),
            };

            var actions = ReconciliationPlanner.Plan(
                consensus, perFile,
                new Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>>(),
                cals, cals, experimentFdr: 0.01);

            Assert.AreEqual(0, actions.Count);
        }

        [TestMethod]
        public void TestPlanDecoyReconciledAlongsidePassingTarget()
        {
            // Target passes in f1, decoy appears in f1 at the wrong RT.
            // Decoy should be reconciled (ForcedIntegration here since no CWT).
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var consensus = new[]
            {
                new PeptideConsensusRT
                {
                    ModifiedSequence = @"PEPTIDE1", IsDecoy = false,
                    ConsensusLibraryRt = 10.0, MedianPeakWidth = 0.6,
                    NRunsDetected = 3, ApexLibraryRtMad = 0.02,
                },
                new PeptideConsensusRT
                {
                    ModifiedSequence = @"DECOY_PEPTIDE1", IsDecoy = true,
                    ConsensusLibraryRt = 10.0, MedianPeakWidth = 0.6,
                    NRunsDetected = 3, ApexLibraryRtMad = 0.02,
                },
            };

            var target = MakeEntry(@"PEPTIDE1", apexRt: 10.0, score: 3.0,
                isDecoy: false, precursorQ: 0.0, peptideQ: 0.0);
            var decoy = MakeEntry(@"DECOY_PEPTIDE1", apexRt: 25.0, score: -1.0,
                isDecoy: true, precursorQ: 1.0, peptideQ: 1.0);

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", new[] { target, decoy }),
            };

            var actions = ReconciliationPlanner.Plan(
                consensus, perFile,
                new Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>>(),
                cals, cals, experimentFdr: 0.01);

            // Target at 10.0 is already at consensus → Keep (absent).
            // Decoy at 25.0 is way off consensus 10.0 → needs reconciling.
            Assert.AreEqual(1, actions.Count);
            Assert.IsTrue(actions.ContainsKey((@"f1", 1)));
            Assert.IsInstanceOfType(
                actions[(@"f1", 1)], typeof(ReconcileAction.ForcedIntegration));
        }

        [TestMethod]
        public void TestPlanNonConsensusPeptideSkipped()
        {
            var cal = IdentityCalibration();
            var cals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"NOT_IN_CONSENSUS", apexRt: 20.0, score: 3.0)),
            };

            var actions = ReconciliationPlanner.Plan(
                Array.Empty<PeptideConsensusRT>(),
                perFile,
                new Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>>(),
                cals, cals, experimentFdr: 0.01);

            Assert.AreEqual(0, actions.Count);
        }

        [TestMethod]
        public void TestPlanFallsBackToOriginalCalibrationWhenNoRefined()
        {
            var cal = IdentityCalibration();
            var originalCals = new Dictionary<string, RTCalibration> { { @"f1", cal } };

            var consensus = new[]
            {
                new PeptideConsensusRT
                {
                    ModifiedSequence = @"PEPTIDE1", IsDecoy = false,
                    ConsensusLibraryRt = 10.0, MedianPeakWidth = 0.8,
                    NRunsDetected = 3, ApexLibraryRtMad = 0.02,
                },
            };

            var perFile = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                Pair(@"f1", PassingTarget(@"PEPTIDE1", apexRt: 20.0, score: 3.0)),
            };

            var actions = ReconciliationPlanner.Plan(
                consensus, perFile,
                new Dictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>>(),
                perFileRefinedCal: new Dictionary<string, RTCalibration>(),
                perFileOriginalCal: originalCals,
                experimentFdr: 0.01);

            // Original cal still produces expectedRt = 10.0 for lib_rt 10.0,
            // so wrong-apex detection at 20.0 needs reconciliation.
            Assert.AreEqual(1, actions.Count);
            Assert.IsInstanceOfType(
                actions[(@"f1", 0)], typeof(ReconcileAction.ForcedIntegration));
        }

        #endregion

        #region CalibrationRefit

        [TestMethod]
        public void TestRefitReturnsNullWhenTooFewConsensusPoints()
        {
            // Three consensus peptides, three entries → 3 pairs, below the
            // 20-point minimum.
            var consensus = MakeConsensus(3);
            var entries = MakeRefitEntries(3, measuredOffset: 0.0);

            var cal = CalibrationRefit.Refit(consensus, entries, consensusFdr: 0.01);
            Assert.IsNull(cal);
        }

        [TestMethod]
        public void TestRefitReturnsNullWhenAllDecoys()
        {
            var consensus = MakeConsensus(30);
            var entries = new List<FdrEntry>();
            for (int i = 0; i < 30; i++)
            {
                entries.Add(MakeEntry(@"DECOY_PEP_" + i, apexRt: i, score: 1.0,
                    isDecoy: true, precursorQ: 0.0, peptideQ: 0.0));
            }

            var cal = CalibrationRefit.Refit(consensus, entries, consensusFdr: 0.01);
            Assert.IsNull(cal);
        }

        [TestMethod]
        public void TestRefitReturnsNullWhenAllFailExperimentFdr()
        {
            var consensus = MakeConsensus(30);
            var entries = new List<FdrEntry>();
            for (int i = 0; i < 30; i++)
            {
                var e = MakeEntry(@"PEP_" + i, apexRt: i, score: 1.0,
                    isDecoy: false, precursorQ: 0.0, peptideQ: 0.0);
                e.ExperimentPrecursorQvalue = 0.5;
                e.ExperimentPeptideQvalue = 0.5;
                entries.Add(e);
            }

            var cal = CalibrationRefit.Refit(consensus, entries, consensusFdr: 0.01);
            Assert.IsNull(cal);
        }

        [TestMethod]
        public void TestRefitProducesCalibrationFromValidConsensus()
        {
            // 30 peptides with consensus_library_rt = i. Entries have
            // apex_rt = 2*i + 5 (a linear 2x+5 shift, cleanly LOESS-fit-able).
            // Refit should predict measured ≈ 2x + 5 for library = x.
            var consensus = MakeConsensus(30);
            var entries = MakeRefitEntries(30, measuredOffset: 0.0, scale: 2.0, intercept: 5.0);

            var cal = CalibrationRefit.Refit(consensus, entries, consensusFdr: 0.01);
            Assert.IsNotNull(cal);

            double pred10 = cal.Predict(10.0);
            Assert.IsTrue(Math.Abs(pred10 - 25.0) < 1.0,
                string.Format(@"Predict(10) should be near 25, got {0}", pred10));
        }

        [TestMethod]
        public void TestRefitExcludesDecoysFromFit()
        {
            // 25 target peptides + 5 decoy entries whose ApexRt would wildly
            // distort a fit if not filtered. Refit should ignore decoys and
            // produce a linear calibration that matches the target data.
            var consensus = MakeConsensus(25);
            var entries = MakeRefitEntries(25, measuredOffset: 0.0, scale: 1.0, intercept: 0.0);
            for (int i = 0; i < 5; i++)
            {
                var decoy = MakeEntry(@"DECOY_PEP_" + i, apexRt: 1e6, score: -1.0,
                    isDecoy: true, precursorQ: 1.0, peptideQ: 1.0);
                decoy.ExperimentPrecursorQvalue = 1.0;
                decoy.ExperimentPeptideQvalue = 1.0;
                entries.Add(decoy);
            }

            var cal = CalibrationRefit.Refit(consensus, entries, consensusFdr: 0.01);
            Assert.IsNotNull(cal);
            Assert.IsTrue(Math.Abs(cal.Predict(10.0) - 10.0) < 1.0);
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

        /// <summary>
        /// Build <paramref name="count"/> target-only consensus entries with
        /// modified sequences PEP_0, PEP_1, ... and ConsensusLibraryRt = i.
        /// </summary>
        private static IReadOnlyList<PeptideConsensusRT> MakeConsensus(int count)
        {
            var result = new PeptideConsensusRT[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = new PeptideConsensusRT
                {
                    ModifiedSequence = @"PEP_" + i,
                    IsDecoy = false,
                    ConsensusLibraryRt = i,
                    MedianPeakWidth = 0.6,
                    NRunsDetected = 3,
                    ApexLibraryRtMad = 0.05,
                };
            }
            return result;
        }

        /// <summary>
        /// Build <paramref name="count"/> target entries paired with the
        /// consensus above: ApexRt = scale * i + intercept + measuredOffset.
        /// Experiment q-values default to 0, so entries pass any normal FDR.
        /// </summary>
        private static List<FdrEntry> MakeRefitEntries(
            int count, double measuredOffset,
            double scale = 1.0, double intercept = 0.0)
        {
            var result = new List<FdrEntry>(count);
            for (int i = 0; i < count; i++)
            {
                var e = MakeEntry(@"PEP_" + i,
                    apexRt: scale * i + intercept + measuredOffset,
                    score: 3.0, isDecoy: false,
                    precursorQ: 0.0, peptideQ: 0.0);
                // Experiment q-values default to 1.0 in the FdrEntry constructor,
                // so set them explicitly for the refit gate.
                e.ExperimentPrecursorQvalue = 0.0;
                e.ExperimentPeptideQvalue = 0.0;
                result.Add(e);
            }
            return result;
        }

        #endregion
    }
}
