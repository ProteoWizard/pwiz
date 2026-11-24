/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Globalization;
using System.IO;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// How long each tutorial screenshot waits for the UI to be ready. Instead of fixed sleeps, which
    /// added up to over an hour of a full screenshot run, a screenshot waits for the UI thread to finish
    /// its pending work and painting, and for the result to reach the screen (<see cref="PaintFlush"/>).
    /// The fixed waits remain, at 0 by default; turning the flush off restores the old waits. Each
    /// setting can be overridden for a run with an environment variable, to measure its effect. Each screenshot's timing can be logged, and each
    /// saved screenshot copied per test, to compare runs.
    /// </summary>
    public static class ScreenshotWaits
    {
        public const string SETTLE_MS_VARIABLE = @"SKYLINE_SCREENSHOT_SETTLE_MS";
        public const string ACTIVATE_MS_VARIABLE = @"SKYLINE_SCREENSHOT_ACTIVATE_MS";
        public const string REPAINT_MS_VARIABLE = @"SKYLINE_SCREENSHOT_REPAINT_MS";
        public const string PAINT_FLUSH_VARIABLE = @"SKYLINE_SCREENSHOT_PAINT_FLUSH";
        public const string TIMING_LOG_VARIABLE = @"SKYLINE_SCREENSHOT_TIMING_LOG";
        public const string COPY_DIR_VARIABLE = @"SKYLINE_SCREENSHOT_COPY_DIR";

        /// <summary>
        /// Fixed wait before activating the screenshot form (default 0, or 1500 ms without <see cref="PaintFlush"/>).
        /// </summary>
        public static int SettleMillis => GetMillis(SETTLE_MS_VARIABLE, PaintFlush ? 0 : 1500);

        /// <summary>
        /// Fixed wait after activating the form (default 0, or 500 ms without <see cref="PaintFlush"/>).
        /// </summary>
        public static int ActivateMillis => GetMillis(ACTIVATE_MS_VARIABLE, PaintFlush ? 0 : 500);

        /// <summary>
        /// Fixed wait after adjusting controls for consistent screenshots (default 0, or 500 ms without
        /// <see cref="PaintFlush"/>).
        /// </summary>
        public static int RepaintMillis => GetMillis(REPAINT_MS_VARIABLE, PaintFlush ? 0 : 500);

        /// <summary>
        /// True to wait for pending UI work and painting to reach the screen before activating the form,
        /// after activating it, and before capturing it (default true; 0 turns it off).
        /// </summary>
        public static bool PaintFlush => GetMillis(PAINT_FLUSH_VARIABLE, 1) != 0;

        /// <summary>
        /// Appends one tab-separated line of timings for a screenshot to the file named by
        /// <see cref="TIMING_LOG_VARIABLE"/>, if it is set, writing a header line to a new file.
        /// </summary>
        public static void LogTiming(string testName, string language, int screenshotNumber, long graphsMillis,
            long settleMillis, long activateMillis, long repaintMillis, long captureMillis, int captureRetries)
        {
            string logPath = Environment.GetEnvironmentVariable(TIMING_LOG_VARIABLE);
            if (string.IsNullOrEmpty(logPath))
                return;
            if (!File.Exists(logPath))
            {
                File.AppendAllText(logPath, string.Join("\t", @"Test", @"Language", @"Screenshot", @"GraphsMs",
                    @"SettleMs", @"ActivateMs", @"RepaintMs", @"CaptureMs", @"SettleSetting", @"ActivateSetting",
                    @"RepaintSetting", @"PaintFlush", @"CaptureRetries") + Environment.NewLine);
            }
            File.AppendAllText(logPath, string.Join("\t", testName, language,
                screenshotNumber.ToString(CultureInfo.InvariantCulture), graphsMillis, settleMillis, activateMillis,
                repaintMillis, captureMillis, SettleMillis, ActivateMillis, RepaintMillis, PaintFlush,
                captureRetries) + Environment.NewLine);
        }

        /// <summary>
        /// Copies a saved screenshot to &lt;dir&gt;\&lt;test&gt;\&lt;language&gt;\&lt;file&gt; under the directory named by
        /// <see cref="COPY_DIR_VARIABLE"/>, if it is set. Several tests save to the same tutorial folder, so
        /// without a copy only the last test's screenshots survive a run for comparison.
        /// </summary>
        public static void CopyShot(string testName, string language, string filePath)
        {
            string copyDir = Environment.GetEnvironmentVariable(COPY_DIR_VARIABLE);
            if (string.IsNullOrEmpty(copyDir) || !File.Exists(filePath))
                return;
            string destDir = Path.Combine(copyDir, testName, language);
            Directory.CreateDirectory(destDir);
            File.Copy(filePath, Path.Combine(destDir, Path.GetFileName(filePath)), true);
        }

        private static int GetMillis(string variableName, int defaultMillis)
        {
            string value = Environment.GetEnvironmentVariable(variableName);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int millis) && millis >= 0
                ? millis
                : defaultMillis;
        }
    }
}
