/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The survivor-count invariants <see cref="FirstPassSurvivorLoader"/> holds every Stage 4
    /// load to. The straight-through subset tests show the check passing on real data; these pin
    /// that each violation is actually reported.
    /// </summary>
    [TestClass]
    public class SurvivorCountCheckTest
    {
        [TestMethod]
        public void TestSurvivorCountCheck()
        {
            // Every run loaded, counts sum to the reported compaction count: no violation, and a
            // reload with the same count is fine at any point.
            var check = new SurvivorCountCheck(2) { Expected = 30 };
            Assert.IsNull(check.Check("a", 10));
            Assert.IsNull(check.Check("a", 10));
            Assert.IsNull(check.Check("b", 20));
            Assert.IsNull(check.Check("b", 20));

            // The sum is only compared once EVERY run has loaded, then disagrees.
            check = new SurvivorCountCheck(2) { Expected = 31 };
            Assert.IsNull(check.Check("a", 10));
            string mismatch = check.Check("b", 20);
            Assert.IsNotNull(mismatch);
            StringAssert.Contains(mismatch, "31");
            StringAssert.Contains(mismatch, "30");

            // A reload that changes a run's count is reported, naming the run and both counts.
            check = new SurvivorCountCheck(2) { Expected = 30 };
            Assert.IsNull(check.Check("a", 10));
            string reload = check.Check("a", 11);
            Assert.IsNotNull(reload);
            StringAssert.Contains(reload, "a");
            StringAssert.Contains(reload, "11");
            StringAssert.Contains(reload, "10");

            // No expected count (the resume loader, or runs sharing a stem): only reloads are
            // checked, and any total is accepted.
            check = new SurvivorCountCheck(1);
            Assert.IsNull(check.Check("a", 5));
            Assert.IsNotNull(check.Check("a", 6));
        }
    }
}
