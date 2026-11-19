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

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SkylineTester
{
    /// <summary>
    /// Follows TestRunner's output so that, when the process dies on its own, the test it was
    /// running is logged as an ordinary failure. A test that ends the process - an access
    /// violation in native code, a stack overflow, a FailFast - never gets to write its own
    /// "!!! name FAILED" block, so without this the log parsers (SkylineNightly, TestRunner's
    /// report) found no failure in a run that crashed. The block written here has the same shape
    /// as TestRunner's, so they read it like any other failure.
    /// </summary>
    public class TestRunnerCrashReport
    {
        /// <summary>The failure name for a crash after the last test had finished.</summary>
        public const string CRASH_BETWEEN_TESTS = @"TestRunnerCrash";

        private const int MAX_DETAIL_LINES = 200;

        // The start of a test line, e.g. "[04:23]   2.1066 TestTicChromatogram   (en) ". Its result,
        // e.g. "  0 failures, 113.33/87.15/291.1 MB, ...", finishes it, on the same line or on a later
        // one when the test wrote output of its own. The count is right-aligned in 3 columns, so a
        // count of 100 or more has no space before it.
        private static readonly Regex START_TEST = new Regex(@"^\[\d\d:\d\d\] +\d+\.\d+ +(\S+) +\(\w+\)");
        private static readonly Regex END_TEST = new Regex(@"\b\d+ failures, [\d./]+ MB");
        private const string FAILURE_BLOCK_END = @"!!!";

        private readonly object _lock = new object();
        private readonly List<string> _linesSinceResult = new List<string>();
        private string _runningTest;
        private string _lastTest;

        /// <summary>One line of TestRunner's standard output or standard error, in arrival order.</summary>
        public void Observe(string line)
        {
            if (line == null)
                return;
            lock (_lock)
            {
                var start = START_TEST.Match(line);
                if (start.Success)
                    _runningTest = _lastTest = start.Groups[1].Value;

                // A result, or the end of the failure block that follows a failed one: what came
                // before belongs to that test, not to whatever crashes next.
                if (END_TEST.IsMatch(line))
                {
                    _runningTest = null;
                    _linesSinceResult.Clear();
                    return;
                }
                if (line.Trim() == FAILURE_BLOCK_END)
                {
                    _linesSinceResult.Clear();
                    return;
                }

                // A crash's own report (e.g. "Fatal error. 0xC0000005" and its stack) arrives on
                // standard error, often before the unfinished test's line, which is only flushed
                // when the output closes.
                if (!start.Success && _linesSinceResult.Count < MAX_DETAIL_LINES)
                    _linesSinceResult.Add(line);
            }
        }

        /// <summary>
        /// The lines to log for a TestRunner that exited with <paramref name="exitCode"/> without
        /// being stopped, or null when nothing failed that its own output did not already report:
        /// no test was running, and the exit code was not a crash. Call once all of its output
        /// has been observed.
        /// </summary>
        /// <param name="exitCode">The process exit code.</param>
        /// <param name="exitCodeText">The same, formatted for people (with its hex and name).</param>
        public IList<string> GetFailureLines(int exitCode, string exitCodeText)
        {
            lock (_lock)
            {
                string name, summary;
                if (_runningTest != null)
                {
                    name = _runningTest;
                    summary = string.Format(@"TestRunner exited with code {0} while this test was running.", exitCodeText);
                }
                // Exit code 1 is how TestRunner reports ordinary failures, already logged. A crash,
                // FailFast or stack overflow exits with a negative NTSTATUS or HRESULT.
                else if (_lastTest != null && exitCode < 0)
                {
                    name = CRASH_BETWEEN_TESTS;
                    summary = string.Format(@"TestRunner exited with code {0} after {1} finished.", exitCodeText, _lastTest);
                }
                else
                {
                    return null;
                }

                var lines = new List<string> { string.Empty, string.Format(@"!!! {0} FAILED", name), summary };
                lines.AddRange(_linesSinceResult);
                lines.Add(FAILURE_BLOCK_END);
                lines.Add(string.Empty);
                return lines;
            }
        }
    }
}
