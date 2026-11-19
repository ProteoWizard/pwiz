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

        // Only the most recent lines are kept: a crash's own report comes last.
        private const int MAX_OUTPUT_LINES = 200;
        private const int MAX_ERROR_LINES = 100;

        // "#@ Running TestTicChromatogram (en)..." - a whole line TestRunner writes as each test
        // starts (SkylineTester always runs it with status=on), so the running test is known before
        // anything can go wrong. The test line that follows, "[04:23]   2.1066 TestTicChromatogram
        // (en) ", gets no newline until its result is appended, so after a crash it is read only
        // once the output closes, if at all.
        private static readonly Regex RUNNING_TEST = new Regex(@"^#@ Running (\S+) \(\w+\)\.\.\.$");
        private static readonly Regex TEST_LINE = new Regex(@"^\[\d\d:\d\d\] +\d+\.\d+ +\S+ +\(\w+\)");
        // A test's result, e.g. "  0 failures, 113.33/87.15/291.1 MB, ...", on the test line or on a
        // later one when the test wrote output of its own. The count is right-aligned in 3 columns,
        // so a count of 100 or more has no space before it.
        private static readonly Regex RESULT = new Regex(@"\b\d+ failures, [\d./]+ MB");
        private const string FAILURE_BLOCK_END = @"!!!";

        private readonly object _lock = new object();
        private readonly Queue<string> _output = new Queue<string>();
        private readonly Queue<string> _errors = new Queue<string>();
        private string _runningTest;
        private string _lastTest;

        /// <summary>
        /// One line of TestRunner's output, as it arrives.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="isError">True for standard error, where a fatal error's report (e.g. "Fatal
        /// error. 0xC0000005" and its stack) is written. The two streams are read on separate threads,
        /// so they are kept apart: standard error is cleared only when the next test starts, never by
        /// a result line on standard output, which can be read after a crash's report. A crash within
        /// moments of its own test's start line can still lose its report that way.</param>
        public void Observe(string line, bool isError)
        {
            if (line == null)
                return;
            lock (_lock)
            {
                var running = RUNNING_TEST.Match(line);
                if (running.Success)
                {
                    _runningTest = _lastTest = running.Groups[1].Value;
                    _output.Clear();
                    _errors.Clear();
                    return;
                }
                if (RESULT.IsMatch(line))
                {
                    _runningTest = null;
                    _output.Clear();
                    return;
                }
                if (line.Trim() == FAILURE_BLOCK_END)
                {
                    // The end of a failed test's failure block: its message is that test's.
                    _output.Clear();
                    return;
                }
                // Lines the log parsers read in their own right are not repeated in a failure block.
                if (TEST_LINE.IsMatch(line) || line.StartsWith(@"!!! ") || line.StartsWith(@"#@ "))
                    return;

                var lines = isError ? _errors : _output;
                lines.Enqueue(line);
                if (lines.Count > (isError ? MAX_ERROR_LINES : MAX_OUTPUT_LINES))
                    lines.Dequeue();
            }
        }

        /// <summary>
        /// The lines to log for a TestRunner that exited with <paramref name="exitCode"/> without
        /// being stopped, or null when nothing failed that its own output did not already report:
        /// no test was running, and the exit code was not a crash.
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
                lines.AddRange(_errors);
                lines.AddRange(_output);
                lines.Add(FAILURE_BLOCK_END);
                lines.Add(string.Empty);
                return lines;
            }
        }
    }
}
