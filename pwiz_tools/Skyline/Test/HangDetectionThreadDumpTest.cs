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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Verifies that the thread dump attached to wait timeouts actually produces call stacks.
    /// <para>This earns a test because the failure mode is silent. Taking the dump means attaching
    /// to this process with ClrMD, which can fail for reasons that have nothing to do with the code
    /// - a runtime version it cannot read, a security policy that refuses the attach - and the
    /// helper deliberately swallows that so it can never replace the failure it is explaining. An
    /// empty dump is therefore indistinguishable from a healthy one at the call site, and a
    /// diagnostic that quietly reports nothing is worse than none, because it is trusted.</para>
    /// </summary>
    [TestClass]
    public class HangDetectionThreadDumpTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestThreadDumpNamesRunningFrames()
        {
            var dump = HangDetection.TryGetThreadDump();

            // The helper reports its own unavailability rather than throwing, so an unusable dump
            // reaches the log looking like a dump. Fail here instead, where it is actionable.
            AssertEx.IsFalse(dump.Contains(@"Thread dump unavailable"), dump);
            AssertEx.Contains(dump, @"*** Thread dump:", @"*** End of thread dump");

            // Frames, not just thread headers. A dump listing threads and nothing else is the
            // failure this test exists to catch, and it looks healthy at the call site.
            var frameLines = dump.Split('\n')
                .Count(line => line.StartsWith(@"  ") && !line.Contains(@"[Unknown]"));
            AssertEx.IsTrue(frameLines > 0,
                TextUtil.LineSeparate(@"Thread dump named no frames on any thread.", dump));

            // NOT asserted: that this test's own frames appear. A passive self-attach cannot walk a
            // stack that is running, so the calling thread comes back empty every time - see the
            // blind spot documented on TryGetThreadDump. Asserting it would encode the wrong
            // expectation and fail forever.
        }
    }
}
