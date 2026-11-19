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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;
using SkylineTester;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// A TestRunner crash must reach the log as an ordinary failure of the test that was running.
    /// The first sequence is TestRunner's output as SkylineTester received it when Bruker timsdata
    /// crashed it in TestTicChromatogram: the fatal error's stack on standard error first, then the
    /// unfinished test line, flushed only when standard output closed.
    /// </summary>
    [TestClass]
    public class TestRunnerCrashReportTest : AbstractUnitTest
    {
        private const int ACCESS_VIOLATION = -1073741819;
        private const string ACCESS_VIOLATION_TEXT = @"-1073741819 (0xC0000005 STATUS_ACCESS_VIOLATION)";
        private const int TESTS_FAILED = 1;

        private const string FINISHED_TEST =
            @"[04:23]   2.1065 TestThreadDumpNamesRunningFrames               (zh)   0 failures, 113.33/87.15/291.1 MB, 182/1721 handles, 0 sec.";
        private const string FINISHED_TEST_100_FAILURES =
            @"[04:23]   2.1065 TestThreadDumpNamesRunningFrames               (zh) 100 failures, 113.33/87.15/291.1 MB, 182/1721 handles, 0 sec.";
        private const string UNFINISHED_TEST =
            @"[04:23]   2.1066 TestTicChromatogram                            (en) ";

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

        // The log formats SkylineNightly's ParseFailures reads a failure with, and how it finds the
        // pass and test number for it: the closest test line before the failure block.
        private static readonly Regex START_FAILURE = new Regex(@"\r\n!!! (\S+) FAILED\r\n");
        private static readonly Regex END_FAILURE = new Regex(@"\r\n!!!\r\n");
        private static readonly Regex FAILURE_TEST = new Regex(@"\r\n\[(\d\d:\d\d)\] +(\d+).(\d+) +(\S+)\s+\(+(\S+)\)", RegexOptions.RightToLeft);

        [TestMethod]
        public void TestCrashReportedAsFailure()
        {
            // A crash during a test, in the order SkylineTester received the output.
            var output = new[] { FINISHED_TEST }.Concat(FATAL_ERROR).Concat(new[] { UNFINISHED_TEST }).ToArray();
            var failure = Failure(output, ACCESS_VIOLATION);
            Assert.IsNotNull(failure);
            Assert.AreEqual(@"!!! TestTicChromatogram FAILED", failure[1]);
            StringAssert.Contains(failure[2], ACCESS_VIOLATION_TEXT);
            CollectionAssert.IsSubsetOf(FATAL_ERROR, failure.ToList());
            Assert.IsFalse(failure.Any(line => line.Contains(@"TestThreadDumpNamesRunningFrames")),
                @"The failure reached back into the previous, finished test.");

            // As the log parsers read it: one failure, charged to pass 2 test 1066.
            var log = string.Join(Environment.NewLine, output.Concat(failure)) + Environment.NewLine;
            var start = START_FAILURE.Match(log);
            Assert.IsTrue(start.Success && END_FAILURE.Match(log, start.Index).Success, log);
            Assert.AreEqual(@"TestTicChromatogram", start.Groups[1].Value);
            var test = FAILURE_TEST.Match(log, start.Index);
            Assert.AreEqual(@"2", test.Groups[2].Value);
            Assert.AreEqual(@"1066", test.Groups[3].Value);

            // The previous test failed: its failure block is its own.
            failure = Failure(new[] { FINISHED_TEST }.Concat(PREVIOUS_FAILURE).Concat(FATAL_ERROR)
                .Concat(new[] { UNFINISHED_TEST }), ACCESS_VIOLATION);
            CollectionAssert.IsSubsetOf(FATAL_ERROR, failure.ToList());
            Assert.IsFalse(failure.Any(line => line.Contains(@"Previous test's own failure message")),
                @"The failure included the previous test's failure block.");

            // A crash after the last test finished is charged to no test, but still reported.
            failure = Failure(new[] { FINISHED_TEST, @"Fatal error." }, ACCESS_VIOLATION);
            Assert.AreEqual(@"!!! " + TestRunnerCrashReport.CRASH_BETWEEN_TESTS + @" FAILED", failure[1]);
            StringAssert.Contains(failure[2], @"after TestThreadDumpNamesRunningFrames finished");
            CollectionAssert.Contains(failure.ToList(), @"Fatal error.");

            // Not crashes: tests failed normally (exit code 1, also with a 3-digit failure count
            // that leaves no space before it, and with a test that wrote its own output before its
            // result), and an exit before any test ran.
            var notCrashes = new[]
            {
                new[] { FINISHED_TEST },
                new[] { FINISHED_TEST_100_FAILURES },
                new[] { UNFINISHED_TEST + @"Warning: test output", @"  0 failures, 10.85/21.67/55.2 MB, 11/493 handles, 1 sec." },
                new string[0],
            };
            foreach (var lines in notCrashes)
                Assert.IsNull(Failure(lines, TESTS_FAILED), string.Join(@" | ", lines));
            Assert.IsNull(Failure(new string[0], ACCESS_VIOLATION), @"Reported a test when none had run.");
        }

        private static IList<string> Failure(IEnumerable<string> output, int exitCode)
        {
            var report = new TestRunnerCrashReport();
            foreach (var line in output)
                report.Observe(line);
            return report.GetFailureLines(exitCode,
                exitCode == ACCESS_VIOLATION ? ACCESS_VIOLATION_TEXT : exitCode.ToString());
        }
    }
}
