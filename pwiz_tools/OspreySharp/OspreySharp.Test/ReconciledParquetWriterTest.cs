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
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp.Test
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
        /// ApplyRescoredRows must: overlay re-scored rows in place by
        /// ParquetIndex, leave hydrated stubs (Features == null) untouched,
        /// append gap-fill rows (ParquetIndex == uint.MaxValue) at the end with a
        /// reassigned ParquetIndex, and warn-and-skip an out-of-range index.
        /// </summary>
        [TestMethod]
        public void TestApplyRescoredRowsOverlayAndAppend()
        {
            // Original parquet rows (loaded read-only). Identified by EntryId.
            var fullEntries = new List<FdrEntry>
            {
                new FdrEntry { EntryId = 100 },
                new FdrEntry { EntryId = 101 },
                new FdrEntry { EntryId = 102 },
            };

            var rescored = new FdrEntry { EntryId = 201, ParquetIndex = 1, Features = new[] { 1.0 } };
            var hydratedStub = new FdrEntry { EntryId = 202, ParquetIndex = 0, Features = null };
            var gapFill = new FdrEntry { EntryId = 203, ParquetIndex = uint.MaxValue, Features = new[] { 2.0 } };
            var outOfRange = new FdrEntry { EntryId = 204, ParquetIndex = 99, Features = new[] { 3.0 } };
            var fdrEntries = new List<FdrEntry> { rescored, hydratedStub, gapFill, outOfRange };

            var warnings = new List<string>();
            int nReplaced = ReconciledParquetWriter.ApplyRescoredRows(
                fullEntries, fdrEntries, "file1", warnings.Add, out int nAppended);

            // Only the in-range rescored row counts as a replacement.
            Assert.AreEqual(1, nReplaced);
            // Only the gap-fill row is appended.
            Assert.AreEqual(1, nAppended);

            // Row 1 was overlaid with the rescored entry; rows 0 and 2 untouched
            // (hydrated stub at index 0 must NOT clobber its original row).
            Assert.AreEqual(4, fullEntries.Count);
            Assert.AreEqual(100u, fullEntries[0].EntryId);
            Assert.AreEqual(201u, fullEntries[1].EntryId);
            Assert.AreEqual(102u, fullEntries[2].EntryId);

            // Gap-fill row appended at the end with ParquetIndex reassigned to the
            // row it now occupies.
            Assert.AreEqual(203u, fullEntries[3].EntryId);
            Assert.AreEqual(3u, gapFill.ParquetIndex);

            // The out-of-range index is dropped (not replaced, not appended) with
            // exactly one warning emitted.
            Assert.AreEqual(1, warnings.Count);
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
                Assert.AreEqual("true", metadata["osprey.reconciled"]);
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
    }
}
