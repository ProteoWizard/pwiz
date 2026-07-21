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
