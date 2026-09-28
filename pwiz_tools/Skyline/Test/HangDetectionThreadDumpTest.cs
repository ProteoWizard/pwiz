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

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Verifies that the diagnostic attached to wait timeouts actually produces call stacks.
    /// <para>This earns a test because the failure mode is silent. Taking the full dump means
    /// attaching to this process with ClrMD, which can fail for reasons that have nothing to do
    /// with the code - a runtime whose DAC it cannot resolve, a security policy that refuses the
    /// attach - and the helper deliberately swallows that so it can never replace the failure it
    /// is explaining. An unusable dump is therefore indistinguishable from a healthy one at the
    /// call site, and a diagnostic that quietly reports nothing is worse than none, because it is
    /// trusted.</para>
    /// <para>What is PINNED here is the degraded form, because that is what every machine can
    /// produce. The full dump is checked only where the machine can take one - see the TODO
    /// below.</para>
    /// </summary>
    [TestClass]
    public class HangDetectionThreadDumpTest : AbstractUnitTest
    {
        /// <summary>
        /// Generous against the 5-second bound the helper enforces, because a loaded agent is
        /// allowed to be slow - but far under the minutes this is here to keep out.
        /// </summary>
        private static readonly TimeSpan MAX_DUMP_DURATION = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Generous, because it only has to rule out "never started" - a loaded agent is allowed
        /// to take its time getting a thread onto a core.
        /// </summary>
        private static readonly TimeSpan THREAD_START_TIMEOUT = TimeSpan.FromSeconds(30);

        [TestMethod]
        public void TestThreadDumpNamesRunningFrames()
        {
            // PINNED: reading your own stack needs no attach and no debugging support, so this
            // form works everywhere and a timeout never arrives with nothing on it.
            var callingStack = HangDetection.GetCallingThreadStack();
            AssertEx.Contains(callingStack, @"*** Calling thread stack:", @"*** End of calling thread stack");
            AssertEx.Contains(callingStack, nameof(TestThreadDumpNamesRunningFrames));

            // PINNED: how long it takes to find OUT. Nothing measured the cost of an unavailable
            // dump, so the agents' 745-1035 seconds reached CI as a test that appeared merely to
            // fail. A diagnostic that cannot be taken has to say so in seconds, or it costs more
            // than the failure it explains.
            var timer = Stopwatch.StartNew();
            var dump = HangDetection.TryGetThreadDump();
            timer.Stop();
            AssertEx.IsTrue(timer.Elapsed < MAX_DUMP_DURATION,
                string.Format(@"Thread dump took {0}, which is over the {1} bound.",
                    timer.Elapsed, MAX_DUMP_DURATION));

            // Deliberately silent on the passing path. Writing which path this machine took was
            // how the agents were shown to produce full dumps, but a test that prints lands its
            // output BETWEEN the harness's test-name line and its memory line, and SkylineTester
            // parses those as one line - so a Quality run could not read memory for this test at
            // all. The question that output answered is settled, and where a machine genuinely
            // cannot dump, the assertion below fails with the CLR and DAC in its message.

            if (dump.Contains(@"Thread dump unavailable"))
            {
                // Tolerated, not expected. Every machine measured takes the full dump: this
                // developer box, MacCoss TeamCity Agent 1, and an AWS agent all produce one in
                // 0 seconds. They did NOT before the attach was pointed at the local DAC
                // explicitly - the same agents spent 745-1035 seconds and then failed with
                // "Array dimensions exceeded supported range", which is what ClrMD resolving its
                // own DAC was doing.
                //
                // So this branch is for a machine that genuinely cannot attach - no matching DAC,
                // or a policy that refuses - where a failure is the environment's, not the code's.
                // Failing here would redden a PR that broke nothing; the degraded report names the
                // CLR and the DAC, which is what a reader needs to tell those apart.
                AssertEx.Contains(dump, nameof(TestThreadDumpNamesRunningFrames));
                return;
            }

            AssertDumpNamesFrames(dump);
            AssertSecondDumpIsNotTheFirstOne();
        }

        /// <summary>
        /// PINNED: that a second dump reports the process as it is NOW. The attach is made once
        /// and reused, so every dump after the first is served by a runtime that ClrMD will answer
        /// out of its snapshot unless Flush is called - and a snapshot served as current is worse
        /// than no dump, because it reads as a healthy answer to the wrong question.
        /// <para>Nothing else covers it. One call exercises only the attach, so deleting the Flush
        /// leaves every later dump reporting the first dump's threads forever and the rest of this
        /// test still passes - exactly the silent failure this class exists to catch.</para>
        /// </summary>
        private static void AssertSecondDumpIsNotTheFirstOne()
        {
            using var blocking = new ManualResetEventSlim(false);
            using var started = new ManualResetEventSlim(false);

            // A thread that did not exist when the first dump was taken. Blocked, not spinning, so
            // the DAC can read it - a running thread is the documented blind spot.
            var thread = new Thread(() =>
            {
                started.Set();
                blocking.Wait();
            })
            {
                IsBackground = true,
                Name = nameof(AssertSecondDumpIsNotTheFirstOne)
            };
            thread.Start();
            try
            {
                // Wait for the thread to signal it is up, then assert once. Waiting instead for
                // the dump to mention it would pass the moment it happened to and prove nothing.
                AssertEx.IsTrue(started.Wait(THREAD_START_TIMEOUT),
                    string.Format(@"Thread did not start within {0}.", THREAD_START_TIMEOUT));

                var dump = HangDetection.TryGetThreadDump();
                AssertEx.Contains(dump,
                    string.Format(@"(Managed ID: {0})", thread.ManagedThreadId));
            }
            finally
            {
                blocking.Set();
                thread.Join(THREAD_START_TIMEOUT);
            }
        }

        private static void AssertDumpNamesFrames(string dump)
        {
            AssertEx.Contains(dump, @"*** Thread dump:", @"*** End of thread dump");

            // Frames, not just thread headers. A dump listing threads and nothing else is the
            // failure this test exists to catch, and it looks healthy at the call site.
            // Notes are indented like frames because they belong to the thread above them, so they
            // have to be excluded explicitly. Counting them would let a dump that walked NOTHING -
            // every thread noted, no stack read - satisfy the assertion below, which is the exact
            // silent success this test exists to prevent.
            var frameLines = dump.Split('\n')
                .Count(line => line.StartsWith(@"  ")
                               && !line.StartsWith(HangDetection.THREAD_NOTE_PREFIX)
                               && !line.Contains(@"[Unknown]"));
            AssertEx.IsTrue(frameLines > 0,
                TextUtil.LineSeparate(@"Thread dump named no frames on any thread.", dump));

            // NOT asserted: that this test's own frames appear. A passive self-attach cannot walk a
            // stack that is running, so the calling thread comes back empty every time - see the
            // blind spot documented on TryGetThreadDump. Asserting it would encode the wrong
            // expectation and fail forever.
        }
    }
}
