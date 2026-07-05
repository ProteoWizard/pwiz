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
    /// <see cref="Pass2FdrSidecar.MapFeaturesByIdentity"/> seam (the
    /// reconciled-feature overlay) that previously rode only the nightly
    /// regression. The reload/percolator/sidecar-IO orchestration itself stays
    /// parity-locked and is characterized by regression.ps1, not here.
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
    }
}
