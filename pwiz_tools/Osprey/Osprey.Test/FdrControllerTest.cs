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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.FDR;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for <see cref="FdrController"/> target-decoy competition, focused on
    /// the winners-sort stability that must match Rust winners.sort_by
    /// (osprey-fdr/src/lib.rs:148, a stable sort).
    /// </summary>
    [TestClass]
    public class FdrControllerTest
    {
        /// <summary>
        /// #7 parity/determinism: the winners sort is STABLE, so competition winners
        /// tied on score keep their input (dictionary insertion) order, matching Rust's
        /// stable sort. C# previously used the unstable List&lt;T&gt;.Sort, which at an
        /// exact score tie could reorder winners and make the order-sensitive cumulative
        /// FDR walk non-deterministic.
        ///
        /// Uses 24 equal-score targets, above List.Sort's ~16-element insertion-sort
        /// threshold, so the unstable introsort would scramble the tie group: with a
        /// stable sort PassingTargets comes out in input order 1..24. FAILS on revert to
        /// List&lt;T&gt;.Sort.
        /// </summary>
        [TestMethod]
        public void TestCompeteAndFilterStableTieOrder()
        {
            // 24 targets, all identical score, no decoys -> every target wins and the
            // cumulative FDR stays 0, so all 24 pass. base_id == the int value (high bit
            // clear = target); Dictionary insertion order is 1..24.
            var items = Enumerable.Range(1, 24).ToList();

            var controller = new FdrController(0.01);
            var result = controller.CompeteAndFilter(
                items,
                _ => 1.0,        // getScore: all tied
                _ => false,      // isDecoy: all targets
                i => (uint)i);   // getEntryId: base_id == value

            // All 24 pass (no decoys), and a stable sort preserves the input order.
            Assert.AreEqual(24, result.PassingTargets.Count);
            CollectionAssert.AreEqual(items, result.PassingTargets);
        }
    }
}
