/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;
using SkylineNightly;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// A TestRunner crash must be reported as a failure by the nightly and report parsers, not as
    /// a clean run. The first log is the shape a nightly actually wrote when Bruker timsdata
    /// crashed TestRunner in TestTicChromatogram: the fatal error's stack lands before the
    /// unfinished test's line, and SkylineTester appends the exit code.
    /// </summary>
    [TestClass]
    public class CrashedTestLogTest : AbstractUnitTest
    {
        private static string Log(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

        private const string FINISHED_TEST =
            @"[04:23]   2.1065 TestThreadDumpNamesRunningFrames               (zh)   0 failures, 113.33/87.15/291.1 MB, 182/1721 handles, 0 sec.";
        private const string FINISHED_TEST_100_FAILURES =
            @"[04:23]   2.1065 TestThreadDumpNamesRunningFrames               (zh) 100 failures, 113.33/87.15/291.1 MB, 182/1721 handles, 0 sec.";
        private const string UNFINISHED_TEST =
            @"[04:23]   2.1066 TestTicChromatogram                            (en) ";
        private const string ACCESS_VIOLATION =
            @"# Process TestRunner had nonzero exit code -1073741819 (0xC0000005 STATUS_ACCESS_VIOLATION)";
        private const string TESTS_FAILED =
            @"# Process TestRunner had nonzero exit code 1 (0x00000001)";

        private static readonly string[] FATAL_ERROR =
        {
            @"Fatal error.",
            @"0xC0000005",
            @"   at Pwiz.Vendor.Bruker.NativeMethods.tims_open(System.String, UInt32)",
            @"   at pwiz.SkylineTestData.PwizFileInfoTest.TestTicChromatogram()"
        };

        private static readonly string[] PREVIOUS_FAILURE =
        {
            string.Empty,
            @"!!! TestThreadDumpNamesRunningFrames FAILED",
            @"Previous test's own failure message",
            @"!!!",
            string.Empty
        };

        [TestMethod]
        public void TestCrashedTestIsFound()
        {
            // A crash during a test, as a nightly logged it.
            var crashed = CrashedTestLog.Find(Log(new[] { string.Empty, FINISHED_TEST }
                .Concat(FATAL_ERROR)
                .Concat(new[] { UNFINISHED_TEST, string.Empty, ACCESS_VIOLATION, string.Empty, @"# Stopped" })
                .ToArray())).Single();
            Assert.AreEqual(@"TestTicChromatogram", crashed.Name);
            Assert.AreEqual(@"2", crashed.Pass);
            Assert.AreEqual(@"1066", crashed.TestNumber);
            Assert.AreEqual(@"en", crashed.Language);
            Assert.AreEqual(@"04:23", crashed.Timestamp);
            StringAssert.StartsWith(crashed.ExitCode, @"-1073741819");
            StringAssert.Contains(crashed.Summary, @"while this test was running");
            StringAssert.Contains(crashed.Details, @"NativeMethods.tims_open");
            Assert.IsFalse(crashed.Details.Contains(@"TestThreadDumpNamesRunningFrames"),
                @"The details reached back into the previous, finished test.");

            // The previous test failed: its failure block is its own, not part of the crash.
            crashed = CrashedTestLog.Find(Log(new[] { string.Empty, FINISHED_TEST }
                .Concat(PREVIOUS_FAILURE).Concat(FATAL_ERROR)
                .Concat(new[] { UNFINISHED_TEST, string.Empty, ACCESS_VIOLATION })
                .ToArray())).Single();
            StringAssert.Contains(crashed.Details, @"Fatal error.");
            Assert.IsFalse(crashed.Details.Contains(@"Previous test's own failure message"),
                @"The details included the previous test's failure block.");

            // A crash after the last test finished is charged to no test, but still reported.
            crashed = CrashedTestLog.Find(Log(string.Empty, FINISHED_TEST, @"Fatal error.", string.Empty, ACCESS_VIOLATION)).Single();
            Assert.AreEqual(CrashedTestLog.CRASH_BETWEEN_TESTS, crashed.Name);
            Assert.AreEqual(@"1065", crashed.TestNumber);
            StringAssert.Contains(crashed.Summary, @"after TestThreadDumpNamesRunningFrames finished");
            StringAssert.Contains(crashed.Details, @"Fatal error.");

            // A crash, then a restarted TestRunner that fails before running a test: one crash.
            Assert.AreEqual(1, CrashedTestLog.Find(Log(string.Empty, FINISHED_TEST, UNFINISHED_TEST, string.Empty,
                ACCESS_VIOLATION, @"Restarting TestRunner", string.Empty, ACCESS_VIOLATION)).Count());

            // Not crashes: tests failed normally (exit code 1, also with a 3-digit failure count
            // that leaves no space before it), and an unfinished test with no exit code line (the
            // run was stopped, or it hung and was killed).
            var notCrashes = new[]
            {
                Log(string.Empty, FINISHED_TEST, string.Empty, TESTS_FAILED),
                Log(string.Empty, FINISHED_TEST_100_FAILURES, string.Empty, TESTS_FAILED),
                Log(string.Empty, FINISHED_TEST, UNFINISHED_TEST, string.Empty, @"# Stopped"),
            };
            foreach (var log in notCrashes)
                Assert.AreEqual(0, CrashedTestLog.Find(log).Count(), log);
        }
    }
}
