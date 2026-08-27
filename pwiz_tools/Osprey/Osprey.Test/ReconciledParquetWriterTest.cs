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
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for <see cref="ReconciledParquetWriter"/>, the Stage 6
    /// reconciled-parquet seam extracted from PerFileRescoreTask. These cover the
    /// two pure helpers (row overlay/append and metadata-hash selection) that
    /// previously rode only the 41-min nightly regression.
    /// </summary>
    [TestClass]
    public class ReconciledParquetWriterTest
    {
        /// <summary>
        /// BuildOverlay must: key each re-scored row (Features != null,
        /// ParquetIndex != uint.MaxValue) into the overlay map by its original
        /// ParquetIndex, skip hydrated stubs (Features == null), and collect
        /// gap-fill rows (ParquetIndex == uint.MaxValue) into the append list.
        /// Out-of-range detection is the streaming write's job, so an out-of-range
        /// index still lands in the overlay map here.
        /// </summary>
        [TestMethod]
        public void TestBuildOverlaySplit()
        {
            var rescored = new FdrEntry { EntryId = 201, ParquetIndex = 1, Features = new[] { 1.0 } };
            var hydratedStub = new FdrEntry { EntryId = 202, ParquetIndex = 0, Features = null };
            var gapFillEntry = new FdrEntry { EntryId = 203, ParquetIndex = uint.MaxValue, Features = new[] { 2.0 } };
            var outOfRange = new FdrEntry { EntryId = 204, ParquetIndex = 99, Features = new[] { 3.0 } };
            var fdrEntries = new List<FdrEntry> { rescored, hydratedStub, gapFillEntry, outOfRange };

            var overlayByIndex = new Dictionary<uint, FdrEntry>();
            var gapFill = new List<FdrEntry>();
            ReconciledParquetWriter.BuildOverlay(fdrEntries, overlayByIndex, gapFill);

            // Re-scored in-range row keyed by its ParquetIndex; hydrated stub skipped;
            // out-of-range row still present (the streaming write drops it, not BuildOverlay).
            Assert.AreEqual(2, overlayByIndex.Count);
            Assert.AreSame(rescored, overlayByIndex[1]);
            Assert.AreSame(outOfRange, overlayByIndex[99]);
            Assert.IsFalse(overlayByIndex.ContainsKey(0), "hydrated stub (Features == null) must be skipped");

            // Only the gap-fill row is collected for append.
            Assert.AreEqual(1, gapFill.Count);
            Assert.AreSame(gapFillEntry, gapFill[0]);
        }

        /// <summary>
        /// BuildReconciliationMetadata must stamp the fixed parquet metadata keys
        /// and choose the join-wide reconciliation hash when join file stems are
        /// supplied, falling back to the config-derived hash when they are absent
        /// or empty.
        /// </summary>
        [TestMethod]
        public void TestBuildReconciliationMetadataHashSelection()
        {
            var config = new OspreyConfig();
            var stems = new List<string> { "fileA", "fileB" };

            var withStems = ReconciledParquetWriter.BuildReconciliationMetadata(config, stems);
            var withNull = ReconciledParquetWriter.BuildReconciliationMetadata(config, null);
            var withEmpty = ReconciledParquetWriter.BuildReconciliationMetadata(config, new List<string>());

            // Fixed contract keys are always present and constant.
            foreach (var metadata in new[] { withStems, withNull, withEmpty })
            {
                Assert.AreEqual(OspreyVersion.Current, metadata["osprey.version"]);
                Assert.AreEqual(config.Identity.SearchParameterHash(), metadata["osprey.search_hash"]);
                Assert.AreEqual(config.Identity.LibraryIdentityHash(), metadata["osprey.library_hash"]);
                Assert.AreEqual(ParquetScoreCache.RECONCILED_SURVIVORS, metadata["osprey.reconciled"]);
            }

            // Stems supplied -> join-wide hash; absent/empty -> config-derived hash.
            Assert.AreEqual(config.Identity.ReconciliationParameterHashForStems(stems),
                withStems["osprey.reconciliation_hash"]);
            Assert.AreEqual(config.Identity.ReconciliationParameterHash(),
                withNull["osprey.reconciliation_hash"]);
            Assert.AreEqual(config.Identity.ReconciliationParameterHash(),
                withEmpty["osprey.reconciliation_hash"]);

            // The two hash regimes must actually differ, or the join-wide branch
            // would be doing nothing.
            Assert.AreNotEqual(withNull["osprey.reconciliation_hash"],
                withStems["osprey.reconciliation_hash"]);
        }

        /// <summary>
        /// --task CompactPerFileRescoring must REFUSE a reconciled parquet whose footer
        /// names a library other than the one it was passed, and refuse one that names no
        /// library at all.
        ///
        /// <para>This is a silent-corruption guard, which is why it is a unit test rather
        /// than something the regression would catch. The compaction re-derives every row's
        /// sequence, precursor m/z and protein_ids from the library BY ENTRY ID, and entry
        /// ids are assigned at library load - so a different build of a same-named library
        /// produces a well-formed parquet in which every row names the wrong peptide, and
        /// the run that consumes it exits 0. Two SEA-AD entrapment libraries with identical
        /// file names differ by 149,311 entries, and the wrong one rewrote 72 CHS files
        /// before a run-log entry count gave it away.</para>
        /// </summary>
        /// <summary>
        /// BuildScoreIndicesByPairing must recover every reconciled row's Stage 4 ordinal from
        /// the source file's (entry_id, charge) group counts alone, and number rows with no
        /// counterpart past the source row count.
        ///
        /// <para>This is how a reconciled parquet written before the <c>score_index</c> column
        /// gets one without re-running Stage 6. Row POSITION cannot supply it: gap-fill rows
        /// merged into canonical position shift every later row. Matching on the full
        /// (entry_id, charge, scan_number) key cannot either - a rescored row's scan moved when
        /// it was re-integrated at the consensus boundary, so it does not equal its Stage 4
        /// counterpart's. The GROUP key is invariant under both, which is what makes this
        /// exact rather than a heuristic.</para>
        /// </summary>
        [TestMethod]
        public void TestScoreIndexPairingRecoversSourceOrdinals()
        {
            // Source: 3 rows for (100,2), 2 for (200,3) - five rows, ordinals 0..4.
            var sourceGroups = new List<(uint, byte, int)>
            {
                (100u, (byte)2, 3),
                (200u, (byte)3, 2),
            };

            // Same groups, same counts: every row pairs, ordinals in order. A rescored row
            // whose scan moved is still in its own group, so it pairs on position within it.
            var same = ParquetScoreCache.BuildScoreIndicesByPairing(
                sourceGroups, new List<(uint, byte, int)> { (100u, (byte)2, 3), (200u, (byte)3, 2) },
                out int srcRows);
            Assert.AreEqual(5, srcRows);
            CollectionAssert.AreEqual(new uint[] { 0, 1, 2, 3, 4 }, same);

            // A gap-fill GROUP the source does not have at all - the only shape gap-fill takes,
            // since it exists for precursors the run did not detect. Its row numbers past the
            // source row count, and the groups after it keep their own ordinals.
            var withGapGroup = ParquetScoreCache.BuildScoreIndicesByPairing(
                sourceGroups,
                new List<(uint, byte, int)> { (100u, (byte)2, 3), (150u, (byte)2, 1), (200u, (byte)3, 2) },
                out _);
            CollectionAssert.AreEqual(new uint[] { 0, 1, 2, 5, 3, 4 }, withGapGroup);

            // Extra rows WITHIN a paired group also number past the source count, so a row can
            // never be handed an ordinal that belongs to a different Stage 4 row.
            var withExtraRow = ParquetScoreCache.BuildScoreIndicesByPairing(
                sourceGroups,
                new List<(uint, byte, int)> { (100u, (byte)2, 4), (200u, (byte)3, 2) },
                out _);
            CollectionAssert.AreEqual(new uint[] { 0, 1, 2, 5, 3, 4 }, withExtraRow);
        }

        [TestMethod]
        public void TestCompactRefusesForeignLibrary()
        {
            var config = new OspreyConfig
            {
                LibrarySource = new LibrarySource(LibraryFormat.DiannTsv,
                    @"C:\lib\carafe_spectral_library.tsv")
            };
            var ctx = new PipelineContext(config, new OspreyTask[0], null, null, null);
            string expected = config.Identity.LibraryIdentityHash();

            // Matching hash: proceed, and nothing is reported.
            var match = new Dictionary<string, string> { { "osprey.library_hash", expected } };
            Assert.IsTrue(CompactPerFileRescoreTask.VerifyLibraryMatches(
                @"f.scores-reconciled.parquet", match, expected, config, ctx));
            Assert.AreEqual(0, ctx.ExitCode);

            // A DIFFERENT library: refuse, and fail the run rather than warn. Warning and
            // proceeding would rewrite the file with another peptide's identity on every row.
            var foreign = new Dictionary<string, string>
                { { "osprey.library_hash", new string('a', 64) } };
            Assert.IsFalse(CompactPerFileRescoreTask.VerifyLibraryMatches(
                @"f.scores-reconciled.parquet", foreign, expected, config, ctx));
            Assert.AreEqual(1, ctx.ExitCode);

            // No hash at all: also refused. "Cannot verify" is not "verified" when the
            // rewrite is destructive and in place.
            ctx.ExitCode = 0;
            Assert.IsFalse(CompactPerFileRescoreTask.VerifyLibraryMatches(
                @"f.scores-reconciled.parquet", new Dictionary<string, string>(),
                expected, config, ctx));
            Assert.AreEqual(1, ctx.ExitCode);
        }
    }
}
