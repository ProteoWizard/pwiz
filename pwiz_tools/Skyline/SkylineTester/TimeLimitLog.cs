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
    /// It never asks whether the test finished, or whether it was still making progress. A long
    /// test - a perf test can run well over an hour - that starts in the last part of the run is
    /// therefore reported as a hang when the time limit kills it, though nothing was wrong.</para>
    /// <para>Rewriting the unfinished test's line as a comment makes the run look as if that test
    /// never started: the server and SkylineNightly's parser both read the last completed test as
    /// the last test. The comment keeps the test's name and start time for anyone reading the log.
    /// It must not contain a bracketed HH:MM time, which is what both of them match on.</para>
    /// <para>Only for a test that was still producing output when the time limit arrived. One that
    /// had gone silent for the hang threshold is left alone, so a real hang is still reported.</para>
    /// </summary>
    public static class TimeLimitLog
    {
        public const string TIME_LIMIT_COMMENT = @"# Time limit reached during ";

        /// <summary>
        /// Minutes of silence after which the test in progress is taken to be hung. The same values
        /// SkylineNightly's LogFileMonitor uses to send its hang alert.
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
        /// Returns <paramref name="log"/> with its last test line rewritten as a comment, if that
        /// test never reported its result, or the log unchanged if it did.
        /// </summary>
        public static string DropUnfinishedTest(string log)
        {
            var startMatch = START_TEST.Match(log);
            if (!startMatch.Success)
                return log;

            // Test output can come between the start of the line and its result, so look anywhere
            // after the start rather than only on the same line.
            if (END_TEST.IsMatch(log, startMatch.Index + startMatch.Length))
                return log;

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
    }
}
