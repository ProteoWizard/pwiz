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
    /// Unit tests for <see cref="Pass2FdrSidecar"/>, the merge-node 2nd-pass FDR
    /// sidecar step extracted from MergeNodeTask.Run. Covers the pure
    /// <see cref="Pass2FdrSidecar.MapFeaturesByIdentity"/> seam (the
    /// reconciled-feature overlay) that previously rode only the nightly
    /// regression, plus the increment (A) scan-omitted 2nd-pass projection sort
    /// equivalence (<see cref="TestScanOmittedProjectionSortMatchesLegacyOrder"/>).
    /// The reload/percolator/sidecar-IO orchestration itself stays parity-locked
    /// and is characterized by regression.ps1, not here.
    /// </summary>
    [TestClass]
    public class Pass2FdrSidecarTest
    {
        /// <summary>
        /// MapFeaturesByIdentity must: assign each entry's Features from the
        /// feature row whose stable identity (entry_id, charge, scan_number)
        /// matches -- NOT its ParquetIndex, which is stale relative to the
        /// re-indexed reconciled parquet (issue #4355) -- skip any entry whose
        /// identity is absent from the map (leaving its Features untouched so the
        /// caller's nMapped &lt; count check fires), and return the count mapped.
        /// </summary>
        [TestMethod]
        public void TestMapFeaturesByIdentity()
        {
            var rowA = new[] { 0.0, 0.1 };
            var rowB = new[] { 1.0, 1.1 };
            var featByIdentity = new Dictionary<(uint, byte, uint), double[]>
            {
                { (10u, 2, 100u), rowA },
                { (20u, 3, 200u), rowB },
            };

            // matchA carries a deliberately WRONG ParquetIndex (999): identity, not
            // the index, must select its features -- the exact reconciled-parquet
            // reindex case that regressed 2nd-pass FDR. matchB matches too; noMatch
            // has an identity absent from the map and must keep its stale features.
            var stale = new[] { 9.0 };
            var matchA = new FdrEntry { EntryId = 10, Charge = 2, ScanNumber = 100, ParquetIndex = 999, Features = null };
            var matchB = new FdrEntry { EntryId = 20, Charge = 3, ScanNumber = 200, ParquetIndex = 0, Features = null };
            var noMatch = new FdrEntry { EntryId = 30, Charge = 2, ScanNumber = 300, ParquetIndex = 1, Features = stale };
            var entries = new List<FdrEntry> { matchA, matchB, noMatch };

            int nMapped = Pass2FdrSidecar.MapFeaturesByIdentity(entries, featByIdentity);

            // Only the two identity-matched entries are mapped; the caller detects
            // the mismatch via nMapped (2) < entries.Count (3).
            Assert.AreEqual(2, nMapped);

            // Features are assigned by identity, by reference (same array instance),
            // ignoring the stale ParquetIndex.
            Assert.AreSame(rowA, matchA.Features);
            Assert.AreSame(rowB, matchB.Features);

            // The unmatched entry keeps its original (stale) features untouched.
            Assert.AreSame(stale, noMatch.Features);

            // Empty map maps nothing and never throws.
            var loneEntry = new FdrEntry { EntryId = 40, Charge = 1, ScanNumber = 400, Features = stale };
            int nMappedEmpty = Pass2FdrSidecar.MapFeaturesByIdentity(
                new List<FdrEntry> { loneEntry }, new Dictionary<(uint, byte, uint), double[]>());
            Assert.AreEqual(0, nMappedEmpty);
            Assert.AreSame(stale, loneEntry.Features);
        }

        /// <summary>
        /// Guards the byte-identity invariant behind increment (A): the scan-omitted
        /// 2nd-pass projection sort key <c>(EntryId, Charge, ParquetIndex)</c> -- where
        /// <c>ParquetIndex</c> is the RECONCILED-parquet row baked by
        /// <see cref="Pass2FdrSidecar.BuildReconciledIdentityToRow"/> -- must produce
        /// the SAME row order as the legacy/oracle resident sort key
        /// <c>(EntryId, Charge, ScanNumber, original-ParquetIndex)</c> (the FdrEntry
        /// overload of <c>PercolatorEngine.RunPercolatorFdr</c>). The two provably
        /// coincide for distinct scans because the reconciled parquet is written
        /// <c>(entry_id, charge, scan)</c>-sorted, so its row is scan-monotonic within
        /// a <c>(entry_id, charge)</c> group. The never-asserted corner -- exercised
        /// here -- is the scan-tie / gap-fill case: the reconciled re-sort is a STABLE
        /// <c>OrderBy(EntryId).ThenBy(Charge).ThenBy(ScanNumber)</c> with NO ParquetIndex
        /// tiebreak (<c>ParquetScoreCache.WriteScoresParquet</c>) and gap-fill rows are
        /// appended (<c>ReconciledParquetWriter.ApplyRescoredRows</c>). Two clean 8-file
        /// Carafe runs were byte-identical end-to-end but never asserted this in
        /// isolation.
        ///
        /// The fixture packs all three risk factors into one <c>(EntryId, Charge)</c>
        /// group: multiple distinct scans, a scan-tie (two rows sharing
        /// <c>(EntryId, Charge, ScanNumber)</c> with different original ParquetIndex),
        /// and an appended gap-fill row (the <see cref="uint.MaxValue"/> sentinel). The
        /// reconciled parquet is produced by the REAL Stage-6 paths -- the gap-fill is
        /// appended through <c>ReconciledParquetWriter.ApplyRescoredRows</c>, written and
        /// stably re-sorted by <c>ParquetScoreCache.WriteScoresParquet</c>, and read back
        /// through <c>BuildReconciledIdentityToRow</c> -- so the tie/gap-fill placement
        /// is production's, not a mock. The projection itself is baked by the real
        /// <see cref="FdrProjectionSet.BuildFromEntries"/> resolver path.
        /// </summary>
        [TestMethod]
        public void TestScanOmittedProjectionSortMatchesLegacyOrder()
        {
            const string fileName = @"file1";

            // Survivor buffer (the rows both sorts operate on), in a deliberately
            // scrambled construction order so neither sort is a no-op. Each row carries
            // a distinct CoelutionSum marker (10..70) used only as a stable per-row token
            // to compare the two resulting orders -- BuildFromEntries copies CoelutionSum
            // straight onto the FdrProjection. Group A = (EntryId 100, Charge 2):
            // P(scan10), G(gap-fill scan15), Q(scan20), R(scan20 == scan-tie with Q),
            // S(scan30). Group B = (EntryId 200, Charge 3, decoys): T(scan5), U(scan25).
            // Original ParquetIndex is (entry,charge,scan)-monotonic across the real rows
            // (0..5); the gap-fill carries the uint.MaxValue sentinel.
            var rowP = MakeSurvivor(100, 2, 10, 0, 10.0, false);
            var rowQ = MakeSurvivor(100, 2, 20, 1, 20.0, false);
            var rowR = MakeSurvivor(100, 2, 20, 2, 30.0, false); // scan-tie with Q
            var rowS = MakeSurvivor(100, 2, 30, 3, 40.0, false);
            var rowT = MakeSurvivor(200, 3, 5, 4, 50.0, true);
            var rowU = MakeSurvivor(200, 3, 25, 5, 60.0, true);
            var rowG = MakeSurvivor(100, 2, 15, uint.MaxValue, 70.0, false); // gap-fill sentinel
            var survivors = new List<FdrEntry> { rowS, rowR, rowT, rowG, rowP, rowU, rowQ };

            // Build the reconciled parquet through the REAL Stage-6 paths on fresh clones
            // (the writer reassigns ParquetIndex, so cloning keeps the survivor buffer's
            // original ParquetIndex -- the legacy sort tiebreak -- intact).
            var reconEntries = new List<FdrEntry>
            {
                MakeSurvivor(100, 2, 10, 0, 10.0, false),
                MakeSurvivor(100, 2, 20, 1, 20.0, false),
                MakeSurvivor(100, 2, 20, 2, 30.0, false),
                MakeSurvivor(100, 2, 30, 3, 40.0, false),
                MakeSurvivor(200, 3, 5, 4, 50.0, true),
                MakeSurvivor(200, 3, 25, 5, 60.0, true),
            };
            var reconGapFill = MakeSurvivor(100, 2, 15, uint.MaxValue, 70.0, false);
            ReconciledParquetWriter.ApplyRescoredRows(
                reconEntries, new List<FdrEntry> { reconGapFill }, fileName, s => { },
                out int nAppended);
            Assert.AreEqual(1, nAppended, @"gap-fill row must append through the real Stage-6 path");

            string reconciledPath = Path.Combine(Path.GetTempPath(),
                @"osprey_pass2sort_" + Guid.NewGuid().ToString(@"N") + @".parquet");
            try
            {
                ParquetScoreCache.WriteScoresParquet(reconciledPath, reconEntries, null, null, fileName);

                // REAL identity -> reconciled-row map (last-write-wins collapses a scan-tie).
                var reconMap = Pass2FdrSidecar.BuildReconciledIdentityToRow(reconciledPath);

                // Legacy/oracle order: sort a fresh copy by the resident FdrEntry key.
                var legacyList = new List<FdrEntry>(survivors);
                legacyList.Sort(LegacyResidentComparison);
                var legacyOrder = legacyList.Select(e => e.CoelutionSum).ToList();

                // Projection order, mirroring Pass2FdrSidecar.ComputePass2Projection:
                // (1) canonicalize the survivor buffer with the SAME legacy key (the
                // production pre-sort), (2) build the projection with each row's
                // ParquetIndex baked to its reconciled row, (3) sort by the scan-omitted
                // projection key.
                var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>
                {
                    new KeyValuePair<string, List<FdrEntry>>(fileName, new List<FdrEntry>(survivors)),
                };
                perFileEntries[0].Value.Sort(LegacyResidentComparison); // ComputePass2Projection pre-sort

                var projections = FdrProjectionSet.BuildFromEntries(perFileEntries, _ => reconMap);
                Assert.AreEqual(1, projections.PerFile.Count);
                var projRows = projections.PerFile[0].Value;
                Assert.AreEqual(survivors.Count, projRows.Count);

                // Sanity: confirm the corner is actually exercised. The gap-fill (marker
                // 70) must have interleaved BY SCAN into the reconciled parquet -- its row
                // falls between P's scan-10 row and Q's scan-20 row, not appended at the
                // end -- and the scan-tied pair (markers 20 and 30) must collapse to the
                // SAME reconciled row so the projection comparer genuinely ties on them.
                var reconRowByMarker = new Dictionary<double, uint>();
                foreach (var p in projRows)
                    reconRowByMarker[p.CoelutionSum] = p.ParquetIndex;
                Assert.IsTrue(
                    reconRowByMarker[10.0] < reconRowByMarker[70.0] &&
                    reconRowByMarker[70.0] < reconRowByMarker[20.0],
                    @"gap-fill row must interleave by scan in the reconciled parquet");
                Assert.AreEqual(reconRowByMarker[20.0], reconRowByMarker[30.0],
                    @"scan-tied rows must bake the same reconciled row (last-write-wins collapse)");

                projRows.Sort(ProjectionComparison);
                var projectionOrder = projRows.Select(p => p.CoelutionSum).ToList();

                // The guarded invariant: scan-omitted projection order == legacy order.
                CollectionAssert.AreEqual(legacyOrder, projectionOrder,
                    @"scan-omitted projection sort diverged from the legacy resident sort; legacy=[" +
                    string.Join(@",", legacyOrder) + @"] projection=[" +
                    string.Join(@",", projectionOrder) + @"]");
            }
            finally
            {
                if (File.Exists(reconciledPath))
                    File.Delete(reconciledPath);
            }
        }

        private static FdrEntry MakeSurvivor(
            uint entryId, byte charge, uint scanNumber, uint parquetIndex, double marker, bool isDecoy)
        {
            return new FdrEntry
            {
                EntryId = entryId,
                Charge = charge,
                ScanNumber = scanNumber,
                ParquetIndex = parquetIndex,
                CoelutionSum = marker,
                IsDecoy = isDecoy,
                ModifiedSequence = @"PEPTIDE" + entryId,
            };
        }

        // Verbatim copy of the FdrEntry-overload comparer in
        // PercolatorEngine.RunPercolatorFdr (the legacy/oracle resident sort). Inlined
        // because the production comparer is a private lambda inside the SVM run and
        // cannot be invoked in isolation.
        private static int LegacyResidentComparison(FdrEntry a, FdrEntry b)
        {
            int c = a.EntryId.CompareTo(b.EntryId);
            if (c != 0) return c;
            c = a.Charge.CompareTo(b.Charge);
            if (c != 0) return c;
            c = a.ScanNumber.CompareTo(b.ScanNumber);
            if (c != 0) return c;
            return a.ParquetIndex.CompareTo(b.ParquetIndex);
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

        // Verbatim copy of the FdrProjectionSet-overload comparer in
        // PercolatorEngine.RunPercolatorFdr (the scan-omitted projection sort). Same
        // isolation caveat as LegacyResidentComparison.
        private static int ProjectionComparison(FdrProjection a, FdrProjection b)
        {
            int c = a.EntryId.CompareTo(b.EntryId);
            if (c != 0) return c;
            c = a.Charge.CompareTo(b.Charge);
            if (c != 0) return c;
            return a.ParquetIndex.CompareTo(b.ParquetIndex);
        }
    }
}
