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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for <see cref="Pass2FdrSidecar"/>, the SecondPassFDR 2nd-pass FDR
    /// sidecar step extracted from SecondPassFdrTask.Run. Covers the pure
    /// <see cref="Pass2FdrSidecar.MapFeaturesByScoreIndex"/> seam (the
    /// reconciled-feature overlay) that previously rode only the nightly
    /// regression, plus the OSPREY_PASS2_QVALUE=transfer score-to-q table and its
    /// per-run q assignment.
    /// The reload/percolator/sidecar-IO orchestration itself stays parity-locked
    /// and is characterized by regression.ps1, not here.
    /// </summary>
    [TestClass]
    public class Pass2FdrSidecarTest
    {
        /// <summary>
        /// MapFeaturesByScoreIndex must assign each entry's Features from the feature row
        /// whose <c>score_index</c> matches the entry's <see cref="FdrEntry.ParquetIndex"/>,
        /// skip any entry whose index is absent from the map (leaving its Features untouched
        /// so the caller's nMapped &lt; count check fires), and return the count mapped.
        ///
        /// <para>This replaces a compound (entry_id, charge, scan_number) key, which existed
        /// only because the reconciled parquet used to be re-indexed relative to the stubs and
        /// the row ordinal therefore addressed the wrong row. Now that the reconciled parquet
        /// PERSISTS the Stage 4 ordinal as <c>score_index</c>, the index is the identity and
        /// the compound key is unnecessary. The compound key was also subtly wrong for
        /// anything spanning a rescore: re-integrating at the consensus boundary moves the
        /// apex, so <c>scan_number</c> differs before and after (#4486).</para>
        /// </summary>
        [TestMethod]
        public void TestMapFeaturesByScoreIndex()
        {
            var rowA = new[] { 0.0, 0.1 };
            var rowB = new[] { 1.0, 1.1 };
            var featByScoreIndex = new Dictionary<uint, double[]>
            {
                { 999u, rowA },
                { 0u, rowB },
            };

            // matchA's scan_number deliberately differs from anything in the map: the score
            // index alone must select its features, which is what makes the mapping survive a
            // rescore that moved the apex. noMatch's index is absent and must keep its stale
            // features.
            var stale = new[] { 9.0 };
            var matchA = new FdrEntry { EntryId = 10, Charge = 2, ScanNumber = 100, ParquetIndex = 999, Features = null };
            var matchB = new FdrEntry { EntryId = 20, Charge = 3, ScanNumber = 200, ParquetIndex = 0, Features = null };
            var noMatch = new FdrEntry { EntryId = 30, Charge = 2, ScanNumber = 300, ParquetIndex = 1, Features = stale };
            var entries = new List<FdrEntry> { matchA, matchB, noMatch };

            int nMapped = Pass2FdrSidecar.MapFeaturesByScoreIndex(entries, featByScoreIndex);

            Assert.AreEqual(2, nMapped);
            Assert.AreSame(rowA, matchA.Features);
            Assert.AreSame(rowB, matchB.Features);
            Assert.AreSame(stale, noMatch.Features);

            // Empty map maps nothing and never throws.
            var loneEntry = new FdrEntry { EntryId = 40, Charge = 1, ScanNumber = 400, ParquetIndex = 7, Features = stale };
            int nMappedEmpty = Pass2FdrSidecar.MapFeaturesByScoreIndex(
                new List<FdrEntry> { loneEntry }, new Dictionary<uint, double[]>());
            Assert.AreEqual(0, nMappedEmpty);
            Assert.AreSame(stale, loneEntry.Features);
        }

        /// <summary>
        /// The OSPREY_PASS2_QVALUE=transfer score-&gt;q table
        /// (<see cref="Pass2FdrSidecar.BuildScoreToQTable"/> +
        /// <see cref="Pass2FdrSidecar.LookupQForScore"/>) must produce a CALIBRATED,
        /// monotone map even when the input (score, q) pairs are individually
        /// non-monotone (the raw averaged-model score is a different scale from the
        /// stored per-fold CV q). Asserts: (a) the emitted table is descending in score
        /// with q non-decreasing as score falls (isotonic); (b) lookups clamp to the
        /// table's min q above the top score and max q below the bottom score; (c) the
        /// lookup is monotone -- a higher query score never yields a larger q.
        /// </summary>
        [TestMethod]
        public void TestTransferScoreQTable()
        {
            // Underlying trend: higher score -> lower q, from ~0 at the top to ~0.05 at
            // the bottom, with a deterministic wobble so adjacent pairs are NOT monotone
            // (exercises the quantile-bin mean + pool-adjacent-violators smoothing).
            const int n = 2000;
            var scores = new List<double>(n);
            var qs = new List<double>(n);
            for (int i = 0; i < n; i++)
            {
                double s = i * 0.01;                       // 0 .. 19.99, ascending
                double trend = 0.05 * (n - 1 - i) / (n - 1); // 0.05 at low score -> 0 at high
                double wobble = 0.01 * Math.Sin(i * 0.7);   // deterministic non-monotone noise
                double q = Math.Max(0.0, Math.Min(0.05, trend + wobble));
                scores.Add(s);
                qs.Add(q);
            }

            Pass2FdrSidecar.BuildScoreToQTable(
                scores, qs, out double[] scoresDesc, out double[] qDesc);

            Assert.AreEqual(scoresDesc.Length, qDesc.Length);
            Assert.IsTrue(scoresDesc.Length > 1);

            // (a) scores descending; q non-decreasing along the descending scores (i.e.
            // calibrated: q only rises as the score falls).
            for (int i = 1; i < scoresDesc.Length; i++)
            {
                Assert.IsTrue(scoresDesc[i] <= scoresDesc[i - 1],
                    "score->q table must be sorted by score descending");
                Assert.IsTrue(qDesc[i] >= qDesc[i - 1] - 1e-12,
                    "q must be monotone non-decreasing as score decreases (isotonic)");
            }

            // (b) clamping: a score above the top gets the minimum q; a score below the
            // bottom gets the maximum q.
            double qMin = qDesc[0];
            double qMax = qDesc[qDesc.Length - 1];
            Assert.AreEqual(qMin, Pass2FdrSidecar.LookupQForScore(1e6, scoresDesc, qDesc), 1e-12);
            Assert.AreEqual(qMax, Pass2FdrSidecar.LookupQForScore(-1e6, scoresDesc, qDesc), 1e-12);
            Assert.IsTrue(qMin <= qMax, "top-score q must not exceed bottom-score q");

            // (c) lookup monotonicity: sweep ascending query scores; the returned q must
            // never increase as the query score increases.
            double prevQ = double.PositiveInfinity;
            for (double s = -1.0; s <= 21.0; s += 0.05)
            {
                double q = Pass2FdrSidecar.LookupQForScore(s, scoresDesc, qDesc);
                Assert.IsTrue(q <= prevQ + 1e-12,
                    "LookupQForScore must be non-increasing in score");
                prevQ = q;
            }

            // An empty table returns the conservative q = 1.
            Assert.AreEqual(1.0, Pass2FdrSidecar.LookupQForScore(0.0, new double[0], new double[0]), 1e-12);
        }

        /// <summary>
        /// The core of the per-run-only redesign
        /// (<see cref="Pass2FdrSidecar.AssignPerRunQ"/>): pass 2 may change ONLY the per-run
        /// q of a reconciliation-moved peak; the experiment q is a pass-1 property carried
        /// through unchanged (the best-peak anchor). Exercises all three classes against a
        /// hand-built per-file (score -&gt; run q) table.
        /// </summary>
        [TestMethod]
        public void TestAssignPerRunQCarriesExperimentQ()
        {
            // Per-file tables: score DESCENDING, q non-decreasing along it (a better score ->
            // a lower q). Distinct precursor/peptide tables prove the two levels stay separate.
            var precScoresDesc = new[] { 10.0, 5.0, 0.0 };
            var precQDesc = new[] { 0.001, 0.01, 0.5 };
            var pepScoresDesc = new[] { 10.0, 5.0, 0.0 };
            var pepQDesc = new[] { 0.002, 0.02, 0.6 };

            // A well-identified 1st-pass record for a precursor: high score, low q at every level.
            // rec.Score is the averaged-model score the pass-2 recomputation reproduces bit-exact.
            // experimentAggregateScore is deliberately DIFFERENT from score: it is the
            // cross-run roll-up, not the per-row discriminant, so a carry that confused the
            // two would show up here.
            var rec = new FdrScoreRecord(
                entryId: 1, score: 10.0,
                runPrecursorQvalue: 0.001, runPeptideQvalue: 0.002, apexRt: 21.75);
            // The EXPERIMENT-scope half is one analysis-wide record per entry_id (format v5,
            // issue #4486), so it arrives beside the run-scope record rather than inside it.
            var exp = new FdrExperimentRecord(
                entryId: 1, experimentPrecursorQvalue: 0.0005,
                experimentPeptideQvalue: 0.0006, experimentProteinQvalue: 0.004,
                experimentAggregateScore: 12.5, pep: 0.03, peptidePep: 0.04);

            // (a) UNCHANGED: recomputed score == the record's score -> carry the whole record.
            var unchanged = new FdrEntry { EntryId = 1 };
            var clsU = Pass2FdrSidecar.AssignPerRunQ(unchanged, 10.0, rec, exp,
                precScoresDesc, precQDesc, pepScoresDesc, pepQDesc);
            Assert.AreEqual(Pass2FdrSidecar.PerRunClass.Unchanged, clsU);
            Assert.AreEqual(12.5, unchanged.ExperimentAggregateScore, 1e-12);
            Assert.AreEqual(10.0, unchanged.Score, 1e-12);
            Assert.AreEqual(0.001, unchanged.RunPrecursorQvalue, 1e-12);
            Assert.AreEqual(0.002, unchanged.RunPeptideQvalue, 1e-12);
            Assert.AreEqual(0.0005, unchanged.ExperimentPrecursorQvalue, 1e-12);
            Assert.AreEqual(0.0006, unchanged.ExperimentPeptideQvalue, 1e-12);
            Assert.AreEqual(0.03, unchanged.Pep, 1e-12);
            Assert.AreEqual(0.04, unchanged.ExperimentPeptidePep, 1e-12);

            // (b) MOVED: reconciliation dropped the score to 5.0 -> run q re-maps UP (worse), but
            // the experiment q is CARRIED from the 1st-pass record unchanged. This is the whole
            // invariant: only per-run q moves, and only toward higher (less confident) values.
            var moved = new FdrEntry { EntryId = 1 };
            var clsM = Pass2FdrSidecar.AssignPerRunQ(moved, 5.0, rec, exp,
                precScoresDesc, precQDesc, pepScoresDesc, pepQDesc);
            Assert.AreEqual(Pass2FdrSidecar.PerRunClass.Moved, clsM);
            Assert.AreEqual(5.0, moved.Score, 1e-12);
            Assert.AreEqual(0.01, moved.RunPrecursorQvalue, 1e-12);   // table lookup at score 5
            Assert.AreEqual(0.02, moved.RunPeptideQvalue, 1e-12);     // peptide table, distinct value
            Assert.AreEqual(0.0005, moved.ExperimentPrecursorQvalue, 1e-12); // CARRIED, not re-mapped
            Assert.AreEqual(0.0006, moved.ExperimentPeptideQvalue, 1e-12);   // CARRIED, not re-mapped
            Assert.AreEqual(0.04, moved.ExperimentPeptidePep, 1e-12);       // CARRIED with it
            Assert.IsTrue(moved.RunPrecursorQvalue > rec.RunPrecursorQvalue,
                "a moved peak's per-run q can only worsen");

            // (c) GAP-FILL: no 1st-pass RUN-scope record -> run q from the table; the
            // experiment values still come from the precursor's analysis-wide record, which is
            // the point of the v5 split - a gap-fill peak is no longer a special case needing a
            // separately reduced cross-file value. A gap-fill that took the q without the
            // aggregate would persist a real q beside ResetScores' 0.0, and a score-space
            // acceptance boundary read back from the 2nd-pass artifacts would collapse onto it.
            var gap = new FdrEntry { EntryId = 2 };
            gap.ResetScores();
            var gapExp = new FdrExperimentRecord(
                entryId: 2, experimentPrecursorQvalue: 0.004,
                experimentPeptideQvalue: 0.006, experimentProteinQvalue: 0.5,
                experimentAggregateScore: 7.25, pep: 1.0, peptidePep: 1.0);
            var clsG = Pass2FdrSidecar.AssignPerRunQ(gap, 5.0, null, gapExp,
                precScoresDesc, precQDesc, pepScoresDesc, pepQDesc);
            Assert.AreEqual(Pass2FdrSidecar.PerRunClass.GapFill, clsG);
            Assert.AreEqual(5.0, gap.Score, 1e-12);
            Assert.AreEqual(0.01, gap.RunPrecursorQvalue, 1e-12);
            Assert.AreEqual(0.02, gap.RunPeptideQvalue, 1e-12);
            Assert.AreEqual(0.004, gap.ExperimentPrecursorQvalue, 1e-12);
            Assert.AreEqual(0.006, gap.ExperimentPeptideQvalue, 1e-12);
            Assert.AreEqual(7.25, gap.ExperimentAggregateScore, 1e-12);

            // (d) NO experiment record at all: an entry that competed in nothing takes the
            // defaults rather than inheriting whatever the last lookup left behind.
            var orphan = new FdrEntry { EntryId = 3 };
            orphan.ResetScores();
            Pass2FdrSidecar.AssignPerRunQ(orphan, 5.0, null, null,
                precScoresDesc, precQDesc, pepScoresDesc, pepQDesc);
            Assert.AreEqual(1.0, orphan.ExperimentPrecursorQvalue, 1e-12);
            Assert.AreEqual(1.0, orphan.ExperimentPeptideQvalue, 1e-12);
            Assert.AreEqual(0.0, orphan.ExperimentAggregateScore, 1e-12);
        }

        /// <summary>
        /// OSPREY_PASS2_QVALUE=transfer must re-score each survivor with whichever classifier the
        /// first pass trained (<see cref="Pass2FdrSidecar.TransferOneFile"/>). It averaged the
        /// fold weights itself, which threw on a gradient-boosted-tree model - there are no
        /// weights - before a single file was transferred; nothing reached it with one while the
        /// default first pass trained the SVM when gbdt was selected.
        ///
        /// <para>The UNCHANGED classification is the key check: it needs the re-score to
        /// reproduce the first pass's score bit for bit, which only the tree model itself can.
        /// One record carries a different first-pass score, standing in for a peak that
        /// reconciliation moved, so the MOVED branch runs too.</para>
        /// </summary>
        [TestMethod]
        public void TestTransferOneFileScoresTreeModel()
        {
            var scorer = FrozenModelScorer.TryCreate(FirstPassModelIoTest.MakeTreeModel());
            Assert.IsNotNull(scorer);
            Assert.IsTrue(scorer.IsGradientBoostedTrees);

            string dir = Path.Combine(Path.GetTempPath(),
                @"osprey_transfer_trees_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            string inputFile = Path.Combine(dir, @"run1.mzML");
            string pass1Path = FdrScoresSidecar.Pass1Path(inputFile);
            try
            {
                const uint movedId = 7;
                var rng = new Random(3);
                var survivors = new List<FdrEntry>();
                var records = new List<FdrScoreRecord>();
                for (uint id = 1; id <= 20; id++)
                {
                    var features = new double[scorer.NumFeatures];
                    for (int j = 0; j < features.Length; j++)
                        features[j] = rng.NextDouble();
                    // The score the first pass wrote: this model, these features.
                    double firstPassScore = scorer.Score(features);
                    if (id == movedId)
                        firstPassScore += 1.0;
                    records.Add(new FdrScoreRecord(id, firstPassScore, 0.001 * id, 0.002 * id, 10.0 + id));
                    survivors.Add(new FdrEntry { EntryId = id, Features = features });
                }
                FdrScoresSidecar.Write(pass1Path, records, FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);

                var tally = new Pass2FdrSidecar.TransferTally();
                Pass2FdrSidecar.TransferOneFile(@"run1", pass1Path, survivors, scorer, null,
                    new Dictionary<uint, FdrExperimentRecord>(), message => Assert.Fail(message), ref tally);

                Assert.AreEqual(1, tally.FilesDone);
                Assert.AreEqual(0, tally.Skipped);
                Assert.AreEqual(1, tally.Moved);
                Assert.AreEqual(survivors.Count - 1, tally.Unchanged);
                foreach (var entry in survivors)
                {
                    if (entry.EntryId == movedId)
                        continue;
                    // Carried verbatim from the first-pass record it reproduced.
                    Assert.AreEqual(0.001 * entry.EntryId, entry.RunPrecursorQvalue, 0.0);
                    Assert.AreEqual(0.002 * entry.EntryId, entry.RunPeptideQvalue, 0.0);
                }
            }
            finally
            {
                if (File.Exists(pass1Path))
                    File.Delete(pass1Path);
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// OSPREY_PROTEIN_BEST in the second pass: a model trained with the column scores a
        /// rescored row with the value the first pass persisted for its run and entry, and a row
        /// the first pass had no value for with the run's floor. The transfer then classifies a
        /// row it reproduces as UNCHANGED bit for bit, a moved row as MOVED on its new raw
        /// features and the same column, and a precursor with no first-pass row as a gap fill
        /// scored at the floor. The column file is refused when absent or written by another
        /// first pass.
        /// </summary>
        [TestMethod]
        public void TestPass2ProteinBestColumn()
        {
            var model = FirstPassModelIoTest.MakeTreeModel();
            model.FeatureNames = new[] { @"f0", @"f1", @"f2", @"f3", ProteinBestFeature.FEATURE_NAME };
            var scorer = FrozenModelScorer.TryCreate(model);
            Assert.IsNotNull(scorer);
            Assert.IsTrue(scorer.ScoresProteinBest);
            Assert.AreEqual(scorer.NumFeatures - 1, scorer.InputFeatures);
            var scorerWithout = FrozenModelScorer.TryCreate(FirstPassModelIoTest.MakeTreeModel());
            Assert.IsNotNull(scorerWithout);
            Assert.IsFalse(scorerWithout.ScoresProteinBest);

            string dir = Path.Combine(Path.GetTempPath(),
                @"osprey_transfer_protein_best_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            string inputFile = Path.Combine(dir, @"run1.mzML");
            string pass1Path = FdrScoresSidecar.Pass1Path(inputFile);
            try
            {
                const uint movedId = 7;
                const uint gapFillId = 20;
                const uint decoyId = 0x80000003;
                const double floor = -0.25;
                // The first pass's column: a value for every entry it scored in this run - a
                // decoy included - and none for the gap fill.
                var rowIds = new List<uint>();
                var rowValues = new List<double>();
                for (uint id = 1; id < gapFillId; id++)
                {
                    rowIds.Add(id);
                    rowValues.Add(0.1 * id);
                }
                rowIds.Add(decoyId);
                rowValues.Add(0.05);
                // A second row of one entry, as a run's first-pass rows can hold several peaks.
                rowIds.Add(4);
                rowValues.Add(0.4);
                var persisted = ProteinBestRunValues.FromRows(rowIds, rowValues, floor);
                Assert.AreEqual(rowIds.Count - 1, persisted.EntryIds.Count);
                ProteinBestSidecar.Write(ProteinBestSidecar.PathFor(inputFile), persisted.EntryIds, persisted.Values,
                    persisted.Floor, TestStamps.Any);
                var column = Pass2FdrSidecar.LoadProteinBestForRun(scorer, pass1Path, @"run1", TestStamps.Any);
                Assert.IsNotNull(column);
                CollectionAssert.AreEqual(persisted.EntryIds.ToArray(), column.EntryIds.ToArray());
                CollectionAssert.AreEqual(persisted.Values.ToArray(), column.Values.ToArray());
                Assert.AreEqual(floor, column.Floor);
                Assert.AreEqual(0.05, column.ValueFor(decoyId));

                var rng = new Random(5);
                var survivors = new List<FdrEntry>();
                var records = new List<FdrScoreRecord>();
                for (uint id = 1; id <= gapFillId; id++)
                {
                    var raw = new double[scorer.InputFeatures];
                    for (int j = 0; j < raw.Length; j++)
                        raw[j] = rng.NextDouble();
                    if (id != gapFillId)
                    {
                        // The score the first pass wrote: this model, the raw features at its own
                        // peak and the column it computed.
                        records.Add(new FdrScoreRecord(id, scorer.Score(raw, 0.1 * id), 0.001 * id, 0.002 * id, 10.0 + id));
                    }
                    // Reconciliation moved one peak: new raw features, the same column.
                    var features = id == movedId ? raw.Select(v => v + 0.5).ToArray() : raw;
                    survivors.Add(new FdrEntry { EntryId = id, Features = features });
                }
                FdrScoresSidecar.Write(pass1Path, records, FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);

                // The column of a moved row is the persisted value; of a row with none, the floor.
                var moved = survivors.Single(e => e.EntryId == movedId);
                Assert.AreEqual(scorer.Score(moved.Features, 0.1 * movedId), scorer.Score(moved.Features, movedId, column));
                var gapFill = survivors.Single(e => e.EntryId == gapFillId);
                Assert.AreEqual(scorer.Score(gapFill.Features, floor), scorer.Score(gapFill.Features, gapFillId, column));

                var tally = new Pass2FdrSidecar.TransferTally();
                Pass2FdrSidecar.TransferOneFile(@"run1", pass1Path, survivors, scorer, column,
                    new Dictionary<uint, FdrExperimentRecord>(), message => Assert.Fail(message), ref tally);
                Assert.AreEqual(1, tally.FilesDone);
                Assert.AreEqual(0, tally.Skipped);
                Assert.AreEqual(1, tally.Moved);
                Assert.AreEqual(1, tally.GapFill);
                Assert.AreEqual(survivors.Count - 2, tally.Unchanged);

                // A model with the column never scores a vector without it.
                Assert.ThrowsException<ArgumentNullException>(() => scorer.Score(moved.Features, movedId, null));

                // The column file of another first pass, and a missing one, stop the run.
                var otherStamp = OspreyVersion.Stamp(TestStamps.TASK, TestStamps.KEY + @";other");
                Assert.ThrowsException<InvalidDataException>(() =>
                    Pass2FdrSidecar.LoadProteinBestForRun(scorer, pass1Path, @"run1", otherStamp));
                File.Delete(ProteinBestSidecar.PathFor(inputFile));
                Assert.ThrowsException<InvalidDataException>(() =>
                    Pass2FdrSidecar.LoadProteinBestForRun(scorer, pass1Path, @"run1", TestStamps.Any));
                // A model without the column reads no column file at all.
                Assert.IsNull(Pass2FdrSidecar.LoadProteinBestForRun(scorerWithout, pass1Path, @"run1", TestStamps.Any));

                // One value per entry_id: rows of one entry that disagree are a defect.
                Assert.ThrowsException<InvalidOperationException>(() =>
                    ProteinBestRunValues.FromRows(new uint[] { 4, 4 }, new[] { 1.0, 2.0 }, floor));
                Assert.IsTrue(column.TryGetValue(decoyId, out double decoyValue));
                Assert.AreEqual(0.05, decoyValue);
                Assert.IsFalse(column.TryGetValue(gapFillId, out _));

                // A model of the other --protein-best arm is refused by a message naming both
                // settings, the model's first, and the task to re-run - not the missing-file one.
                string on = OspreyArgNames.Text(OspreyArgNames.PROTEIN_BEST, OspreyArgNames.VALUE_ON);
                string off = OspreyArgNames.Text(OspreyArgNames.PROTEIN_BEST, OspreyArgNames.VALUE_OFF);
                string modelOn = PerFileRescoreTask.ProteinBestMismatchMessage(true, false);
                string modelOff = PerFileRescoreTask.ProteinBestMismatchMessage(false, true);
                Assert.AreEqual(string.Format(
                    OspreyTasksResources.PerFileRescoreTask_CreatePass2Worker_Second_pass_FDR_cannot_run__the_saved_first_pass_model_was_trained_with__0___but_this_run_has__1___Run__2__again_with__1__,
                    on, off, OspreyArgNames.TaskText(FirstPassFdrTask.TASK_NAME)), modelOn);
                StringAssert.Contains(modelOff, off);
                StringAssert.Contains(modelOff, on);
                Assert.IsTrue(modelOn.IndexOf(on, StringComparison.Ordinal) < modelOn.IndexOf(off, StringComparison.Ordinal));
                Assert.IsTrue(modelOff.IndexOf(off, StringComparison.Ordinal) < modelOff.IndexOf(on, StringComparison.Ordinal));
                Assert.AreNotEqual(OspreyTasksResources.Pass2FdrSidecar_ComputePass2FrozenCompetition_Second_pass_FDR_cannot_run__the_saved_first_pass_model__a_first_pass_intermediate_file__,
                    OspreyTasksResources.PerFileRescoreTask_CreatePass2Worker_Second_pass_FDR_cannot_run__the_saved_first_pass_model_was_trained_with__0___but_this_run_has__1___Run__2__again_with__1__);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// The OSPREY_DUMP_RUN_PEP fit must compete each target with its paired decoy as the
        /// first pass does (TargetDecoyCompetition.CompeteFromDicts): higher score wins, the DECOY
        /// on a tie. Giving ties to the target fit the run PEP to a different winner population
        /// and undercounted decoys wherever scores tie, as tree models make common. Also pins
        /// best-of-several per side, unpaired targets and decoys, and base-id order.
        /// </summary>
        [TestMethod]
        public void TestRunPepCompetitionGivesTiesToDecoy()
        {
            uint decoy = LibraryEntry.DECOY_ID_BIT;
            var records = new List<FdrScoreRecord>
            {
                Record(3, 2.0), Record(3 | decoy, 2.0),            // tie -> decoy
                Record(1, 1.0), Record(1, 3.0), Record(1 | decoy, 2.5), // best target 3.0 beats 2.5
                Record(2 | decoy, 4.0), Record(2, 1.5),            // decoy wins outright
                Record(5, 0.5),                                    // unpaired target
                Record(4 | decoy, 0.7),                            // unpaired decoy
            };
            var winners = Pass2FdrSidecar.CompeteRunPepWinners(records);
            CollectionAssert.AreEqual(new[] { 3.0, 4.0, 2.0, 0.7, 0.5 }, winners.Scores);
            CollectionAssert.AreEqual(new[] { false, true, true, true, false }, winners.IsDecoyWinner);
        }

        private static FdrScoreRecord Record(uint entryId, double score)
        {
            return new FdrScoreRecord(entryId, score, 1.0, 1.0, 0.0);
        }
    }
}
