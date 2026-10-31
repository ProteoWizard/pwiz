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
    /// The fixed waits taken around each tutorial screenshot, which add up to over an hour of a full
    /// screenshot run. Each can be overridden for a run with an environment variable, to measure how
    /// short it can be before screenshots change, and each screenshot's timing can be logged.
    /// </summary>
    public static class ScreenshotWaits
    {
        public const string SETTLE_MS_VARIABLE = @"SKYLINE_SCREENSHOT_SETTLE_MS";
        public const string ACTIVATE_MS_VARIABLE = @"SKYLINE_SCREENSHOT_ACTIVATE_MS";
        public const string REPAINT_MS_VARIABLE = @"SKYLINE_SCREENSHOT_REPAINT_MS";
        public const string TIMING_LOG_VARIABLE = @"SKYLINE_SCREENSHOT_TIMING_LOG";

        /// <summary>
        /// Wait before activating the screenshot form, for the UI to settle (default 1500 ms).
        /// </summary>
        public static int SettleMillis => GetMillis(SETTLE_MS_VARIABLE, 1500);

        /// <summary>
        /// Wait after activating the form, for activation messages to be processed (default 500 ms).
        /// </summary>
        public static int ActivateMillis => GetMillis(ACTIVATE_MS_VARIABLE, 500);

        /// <summary>
        /// Wait after adjusting controls for consistent screenshots, for them to repaint (default 500 ms).
        /// </summary>
        public static int RepaintMillis => GetMillis(REPAINT_MS_VARIABLE, 500);

        /// <summary>
        /// Appends one tab-separated line of timings for a screenshot to the file named by
        /// <see cref="TIMING_LOG_VARIABLE"/>, if it is set, writing a header line to a new file.
        /// </summary>
        public static void LogTiming(string testName, string language, int screenshotNumber, long graphsMillis,
            long settleMillis, long activateMillis, long repaintMillis, long captureMillis)
        {
            string logPath = Environment.GetEnvironmentVariable(TIMING_LOG_VARIABLE);
            if (string.IsNullOrEmpty(logPath))
                return;
            if (!File.Exists(logPath))
            {
                File.AppendAllText(logPath, string.Join("\t", @"Test", @"Language", @"Screenshot", @"GraphsMs",
                    @"SettleMs", @"ActivateMs", @"RepaintMs", @"CaptureMs", @"SettleSetting", @"ActivateSetting",
                    @"RepaintSetting") + Environment.NewLine);
            }
            File.AppendAllText(logPath, string.Join("\t", testName, language,
                screenshotNumber.ToString(CultureInfo.InvariantCulture), graphsMillis, settleMillis, activateMillis,
                repaintMillis, captureMillis, SettleMillis, ActivateMillis, RepaintMillis) + Environment.NewLine);
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
