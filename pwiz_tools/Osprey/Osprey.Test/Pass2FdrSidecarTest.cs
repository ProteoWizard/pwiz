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
    /// Unit tests for <see cref="Pass2FdrSidecar"/>, the merge-node 2nd-pass FDR
    /// sidecar step extracted from MergeNodeTask.Run. Covers the pure
    /// <see cref="Pass2FdrSidecar.MapFeaturesByParquetIndex"/> seam (the
    /// reconciled-feature overlay) that previously rode only the nightly
    /// regression. The reload/percolator/sidecar-IO orchestration itself stays
    /// parity-locked and is characterized by regression.ps1, not here.
    /// </summary>
    [TestClass]
    public class Pass2FdrSidecarTest
    {
        /// <summary>
        /// MapFeaturesByParquetIndex must: assign each entry's Features from the
        /// feature row at its ParquetIndex (by reference), skip any entry whose
        /// index is out of range (leaving its Features untouched so the caller's
        /// nMapped &lt; count check fires), and return the count actually mapped.
        /// </summary>
        [TestMethod]
        public void TestMapFeaturesByParquetIndex()
        {
            var row0 = new[] { 0.0, 0.1 };
            var row1 = new[] { 1.0, 1.1 };
            var row2 = new[] { 2.0, 2.1 };
            var featRows = new List<double[]> { row0, row1, row2 };

            // Two in-range entries (indices 2 and 0), one past the end (a
            // stub/parquet mismatch) that must be skipped with its stale Features
            // left intact.
            var stale = new[] { 9.0 };
            var inRangeHigh = new FdrEntry { EntryId = 1, ParquetIndex = 2, Features = null };
            var inRangeLow = new FdrEntry { EntryId = 2, ParquetIndex = 0, Features = null };
            var outOfRange = new FdrEntry { EntryId = 3, ParquetIndex = 3, Features = stale };
            var entries = new List<FdrEntry> { inRangeHigh, inRangeLow, outOfRange };

            int nMapped = Pass2FdrSidecar.MapFeaturesByParquetIndex(entries, featRows);

            // Only the two in-range entries are mapped; the caller detects the
            // mismatch via nMapped (2) < entries.Count (3).
            Assert.AreEqual(2, nMapped);

            // Features are assigned by index, by reference (same array instance).
            Assert.AreSame(row2, inRangeHigh.Features);
            Assert.AreSame(row0, inRangeLow.Features);

            // The out-of-range entry keeps its original (stale) features untouched.
            Assert.AreSame(stale, outOfRange.Features);

            // Empty feature rows map nothing and never throw.
            var loneEntry = new FdrEntry { EntryId = 4, ParquetIndex = 0, Features = stale };
            int nMappedEmpty = Pass2FdrSidecar.MapFeaturesByParquetIndex(
                new List<FdrEntry> { loneEntry }, new List<double[]>());
            Assert.AreEqual(0, nMappedEmpty);
            Assert.AreSame(stale, loneEntry.Features);
        }
    }
}
