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
using System.Globalization;
using System.Text.RegularExpressions;

namespace SkylineNightly
{
    /// <summary>
    /// A TestRunner process that died on its own, and the test it is charged to.
    /// </summary>
    public class CrashedTest
    {
        public string Timestamp { get; set; }
        public string Pass { get; set; }
        public string TestNumber { get; set; }
        /// <summary>The test that was running, or <see cref="CrashedTestLog.CRASH_BETWEEN_TESTS"/>
        /// when the last test had already finished.</summary>
        public string Name { get; set; }
        public string Language { get; set; }
        public string ExitCode { get; set; }
        /// <summary>One sentence for the failure report saying what happened.</summary>
        public string Summary { get; set; }
        /// <summary>What the log holds between the last finished test (and its failure block, if
        /// any) and the exit code line, which is where a fatal error's own report - e.g.
        /// "Fatal error. 0xC0000005" and its stack - lands.</summary>
        public string Details { get; set; }
    }

    /// <summary>
    /// Finds TestRunner crashes in a SkylineTester log. A test that ends the process - an access
    /// violation in native code, a stack overflow, a FailFast - never writes its "!!! name FAILED"
    /// block, so the parsers that only look for those blocks reported such a run as having no
    /// failures. What it does leave is SkylineTester's "# Process TestRunner had nonzero exit code"
    /// line, written only when TestRunner exits on its own (not when it is stopped or killed), and,
    /// when a test was running, that test's line with no result.
    /// </summary>
    public static class CrashedTestLog
    {
        /// <summary>The failure name for a crash after the last test finished.</summary>
        public const string CRASH_BETWEEN_TESTS = @"TestRunnerCrash";

        private static readonly Regex EXIT_CODE = new Regex(@"\r?\n# Process TestRunner had nonzero exit code ([^\r\n]+)\r?\n");
        private static readonly Regex START_TEST = new Regex(@"\r?\n\[(\d\d:\d\d)\] +(\d+)\.(\d+) +(\S+) +\((\w\w)\) ", RegexOptions.RightToLeft);
        // No leading space: the failure count is written right-aligned in 3 columns, so a count of
        // 100 or more directly follows the space START_TEST consumes.
        private static readonly Regex END_TEST = new Regex(@"\b\d+ failures, [\d./]+ MB[^\n]*\n");
        private static readonly Regex FAILURE_BLOCK_END = new Regex(@"\r?\n!!!\r?\n");

        public static IEnumerable<CrashedTest> Find(string log)
        {
            int floor = 0; // Each exit code line is charged only to what was logged after the previous one
            for (var exit = EXIT_CODE.Match(log); exit.Success; exit = exit.NextMatch())
            {
                int exitEnd = exit.Index + exit.Length;
                int searchFloor = floor;
                floor = exitEnd;

                var start = START_TEST.Match(log, exit.Index);
                if (!start.Success || start.Index < searchFloor)
                    continue; // No test ran in this process

                string exitCode = exit.Groups[1].Value;
                int afterStart = start.Index + start.Length;
                var end = END_TEST.Match(log, afterStart, exit.Index - afterStart);
                if (end.Success)
                {
                    // The last test finished. Exit code 1 is how TestRunner reports ordinary
                    // failures; a crash, FailFast or stack overflow exits with a negative NTSTATUS
                    // or HRESULT.
                    if (!IsCrash(exitCode))
                        continue;
                    int resultLineEnd = end.Index + end.Length;
                    yield return NewCrash(start, CRASH_BETWEEN_TESTS, exitCode,
                        string.Format(@"TestRunner exited with code {0} after {1} finished.", exitCode, start.Groups[4].Value),
                        log, SkipFailureBlocks(log, resultLineEnd, exit.Index), exitEnd);
                    continue;
                }

                // The fatal error's report can precede the unfinished test's line, so begin after
                // the previous test line and any failure block it ended with.
                int detailsStart = Math.Max(searchFloor, start.Index);
                var previous = START_TEST.Match(log, start.Index);
                if (previous.Success && previous.Index >= searchFloor)
                {
                    int previousLineEnd = log.IndexOf('\n', previous.Index + previous.Length);
                    if (previousLineEnd >= 0 && previousLineEnd < start.Index)
                        detailsStart = SkipFailureBlocks(log, previousLineEnd + 1, start.Index);
                }
                yield return NewCrash(start, start.Groups[4].Value, exitCode,
                    string.Format(@"TestRunner exited with code {0} while this test was running.", exitCode),
                    log, detailsStart, exitEnd);
            }
        }

        private static CrashedTest NewCrash(Match start, string name, string exitCode, string summary,
            string log, int detailsStart, int detailsEnd)
        {
            return new CrashedTest
            {
                Timestamp = start.Groups[1].Value,
                Pass = start.Groups[2].Value,
                TestNumber = start.Groups[3].Value,
                Name = name,
                Language = start.Groups[5].Value,
                ExitCode = exitCode,
                Summary = summary,
                Details = log.Substring(detailsStart, detailsEnd - detailsStart).Trim()
            };
        }

        /// <summary>Position after the last "!!!" failure-block terminator in [from, to), or from.</summary>
        private static int SkipFailureBlocks(string log, int from, int to)
        {
            int result = from;
            for (var match = FAILURE_BLOCK_END.Match(log, from, to - from); match.Success; match = match.NextMatch())
                result = match.Index + match.Length;
            return result;
        }

        /// <summary>
        /// True for an exit code that means the process died rather than returned: a negative
        /// value, i.e. an NTSTATUS error (0xC0000005 access violation, 0xC00000FD stack overflow,
        /// 0xC0000409 FailFast) or a CLR HRESULT (0x80131506, 0xE0434352). The code is logged as
        /// the decimal value followed by its hex and name.
        /// </summary>
        private static bool IsCrash(string exitCode)
        {
            var firstToken = exitCode.Split(' ')[0];
            return int.TryParse(firstToken, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int code) && code < 0;
        }
    }
}
