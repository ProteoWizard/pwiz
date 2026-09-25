/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    [TestClass]
    public class PerfUtilTest : AbstractUnitTest
    {
        /// <summary>
        /// <see cref="PerfUtilActual"/> infers each timer's nesting depth from the previous event in
        /// its list, which breaks in two ways the measured code must survive: a timer created after
        /// <see cref="PerfUtilActual.GetLog"/> has closed the root event, and nesting deeper than the
        /// fixed call stack it was written with. Both happened in chromatogram extraction, where a
        /// two-pass import reads the same data file again after the first pass has logged.
        /// </summary>
        [TestMethod]
        public void TestPerfUtilTimers()
        {
            TestReuseAfterGetLog();
            TestDeepNesting();
        }

        private static void TestReuseAfterGetLog()
        {
            var perf = new PerfUtilActual(@"reuse");
            using (perf.CreateTimer(@"first"))
            {
            }
            AssertEx.Contains(perf.GetLog(), @"first");

            // The root event is now closed. The next timer must be accepted as a new top-level
            // timer rather than failing on a negative depth.
            using (perf.CreateTimer(@"second"))
            {
            }
            var log = perf.GetLog();
            AssertEx.Contains(log, @"first");
            AssertEx.Contains(log, @"second");
        }

        private static void TestDeepNesting()
        {
            const int depth = 20; // Well past the 9 slots the call stack started with
            var perf = new PerfUtilActual(@"deep");
            var timers = new List<IPerfUtilTimer>();
            for (int i = 0; i < depth; i++)
                timers.Add(perf.CreateTimer(@"level" + i));
            for (int i = depth - 1; i >= 0; i--)
                timers[i].Dispose();
            AssertEx.Contains(perf.GetLog(), @"level" + (depth - 1));
        }
    }
}
