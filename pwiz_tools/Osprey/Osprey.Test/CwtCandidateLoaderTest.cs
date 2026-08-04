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
    /// Unit tests for <see cref="CwtCandidateLoader"/>, the Stage 6 CWT-candidate
    /// load + bounds-validation seam extracted from FirstJoinTask.PlanStage6.
    /// Covers the pure <see cref="CwtCandidateLoader.MaxParquetIndex"/> helper that
    /// drives the in-range gate; the parquet load itself stays parity-locked and
    /// is characterized by regression.ps1.
    /// </summary>
    [TestClass]
    public class CwtCandidateLoaderTest
    {
        /// <summary>
        /// MaxParquetIndex returns the largest ParquetIndex across the stubs, 0 for
        /// an empty list (the caller's <c>kvp.Value.Count &gt; 0</c> guard keeps an
        /// empty file from being treated as out-of-range), and is order-independent.
        /// </summary>
        [TestMethod]
        public void TestMaxParquetIndex()
        {
            // Max appears in the middle to confirm it is not just first/last.
            var entries = new List<FdrEntry>
            {
                new FdrEntry { EntryId = 1, ParquetIndex = 3 },
                new FdrEntry { EntryId = 2, ParquetIndex = 17 },
                new FdrEntry { EntryId = 3, ParquetIndex = 9 },
            };
            Assert.AreEqual(17u, CwtCandidateLoader.MaxParquetIndex(entries));

            // Single entry returns its own index.
            Assert.AreEqual(5u, CwtCandidateLoader.MaxParquetIndex(
                new List<FdrEntry> { new FdrEntry { EntryId = 4, ParquetIndex = 5 } }));

            // Empty list returns 0 (no out-of-range trigger for an empty file).
            Assert.AreEqual(0u, CwtCandidateLoader.MaxParquetIndex(new List<FdrEntry>()));
        }
    }
}
