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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
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
                runPrecursorQvalue: 0.001, runPeptideQvalue: 0.002);
            // The EXPERIMENT-scope half is one analysis-wide record per entry_id (format v5,
            // issue #4486), so it arrives beside the run-scope record rather than inside it.
            var exp = new FdrExperimentRecord(
                entryId: 1, experimentPrecursorQvalue: 0.0005,
                experimentPeptideQvalue: 0.0006, experimentProteinQvalue: 0.004,
                experimentAggregateScore: 12.5, pep: 0.03);

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
                experimentAggregateScore: 7.25, pep: 1.0);
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
    }
}
