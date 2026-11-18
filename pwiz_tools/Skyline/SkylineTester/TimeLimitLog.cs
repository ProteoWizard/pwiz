/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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

using System;
using System.Text.RegularExpressions;

namespace SkylineTester
{
    /// <summary>
    /// Keeps a test that the nightly time limit cut off from being reported as a hang.
    /// <para>The results server decides a run hung by finding the last test line in the posted log
    /// and checking whether that test started more than 30 minutes before the run's scheduled end.
    /// It never asks whether the test finished. A long test - a perf test can run well over an
    /// hour - that starts in the last part of the run is therefore reported as a hang when the
    /// time limit kills it, though nothing was wrong.</para>
    /// <para>Rewriting the unfinished test's line as a comment makes the run look as if that test
    /// never started: the server and SkylineNightly's parser both read the last completed test as
    /// the last test. The comment keeps the test's name and start time for anyone reading the log.
    /// It must not contain a bracketed HH:MM time, which is what both of them match on.</para>
    /// <para>Only for a test the hang threshold does not yet call hung: one that wrote a line of
    /// output, or started, less than <see cref="GetHangThresholdMinutes"/> ago. The start counts
    /// because TestRunner writes the start of a test's line as soon as the test begins, and a
    /// test that prints nothing has no other output. A test past the threshold is left for the
    /// server to report, with a note saying why. This is the same threshold SkylineNightly uses to
    /// send its hang alert, so a run is called hung by both or by neither.</para>
    /// </summary>
    public static class TimeLimitLog
    {
        public const string TIME_LIMIT_COMMENT = @"# Time limit reached during ";
        public const string TIME_LIMIT_HANG_NOTE = @"# Time limit reached after ";

        /// <summary>
        /// Minutes without output after which the test in progress is taken to be hung. Shared with
        /// SkylineNightly's LogFileMonitor, which sends its hang alert at the same point.
        /// </summary>
        public static int GetHangThresholdMinutes(bool isPerf)
        {
            return isPerf ? 90 : 60;
        }

        // As in "[17:12]   2.1066 TestTicChromatogram                            (en) "
        private static readonly Regex START_TEST = new Regex(@"\[(\d\d:\d\d)\] +(\d+)\.(\d+) +(\S+) +\((\w+)\) ",
            RegexOptions.RightToLeft);
        // As in " 0 failures, 94.99/66.86/252.6 MB, 202/1525 handles, 0 sec."
        private static readonly Regex END_TEST = new Regex(@" \d+ failures, [^\r\n]* sec\.");

        /// <summary>
        /// Returns the log of a run the time limit stopped, as it should be posted: with an
        /// unfinished last test rewritten as a comment if it was not yet past the hang threshold,
        /// or with a note on why it was left if it was. Returns <paramref name="log"/> unchanged
        /// when its last test finished or it has none.
        /// </summary>
        /// <param name="log">The complete log of the run</param>
        /// <param name="silence">How long the run had gone without a line of output when the time
        /// limit stopped it</param>
        /// <param name="isPerf">True for a perf run, which allows longer silences</param>
        public static string RewriteForTimeLimit(string log, TimeSpan silence, bool isPerf)
        {
            var startMatch = FindUnfinishedTest(log);
            if (startMatch == null)
                return log;

            if (silence.TotalMinutes >= GetHangThresholdMinutes(isPerf))
            {
                return log + string.Format(@"{0}{1:F0} minutes without output. Leaving {2} to be reported as a hang.",
                    TIME_LIMIT_HANG_NOTE, silence.TotalMinutes, startMatch.Groups[4].Value) + Environment.NewLine;
            }

            var comment = string.Format(@"{0}{1} ({2}), pass {3} test {4}, started at {5}. Not counted as a hang.",
                TIME_LIMIT_COMMENT,
                startMatch.Groups[4].Value,
                startMatch.Groups[5].Value,
                startMatch.Groups[2].Value,
                startMatch.Groups[3].Value,
                startMatch.Groups[1].Value);
            return log.Substring(0, startMatch.Index) + comment + Environment.NewLine +
                   log.Substring(startMatch.Index + startMatch.Length);
        }

        /// <summary>
        /// The start of the log's last test line, if that test never reported its result.
        /// </summary>
        private static Match FindUnfinishedTest(string log)
        {
            var startMatch = START_TEST.Match(log);
            if (!startMatch.Success)
                return null;

            // Test output can come between the start of the line and its result, so look anywhere
            // after the start rather than only on the same line.
            if (END_TEST.IsMatch(log, startMatch.Index + startMatch.Length))
                return null;

            return startMatch;
        }
    }
}
