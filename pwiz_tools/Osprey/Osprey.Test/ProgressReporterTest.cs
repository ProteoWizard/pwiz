/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for <see cref="ProgressReporter"/>'s frozen-percent heartbeat: a phase
    /// advancing slower than 1% per <see cref="ProgressReporter.HEARTBEAT_SECONDS"/> must
    /// still emit a line so it never looks hung, while a normally-advancing phase must not
    /// gain heartbeat clutter. The heartbeat interval is injected small/large so the
    /// timer-driven behavior is deterministic without a real heartbeat-length wait.
    /// </summary>
    [TestClass]
    public class ProgressReporterTest
    {
        [TestMethod]
        public void TestProgressReporterHeartbeat()
        {
            // Frozen integer percent (1 of 1,000,000 == 0%): after the idle threshold the
            // reporter reprints the current percent with an elapsed parenthetical so the
            // console stays alive. intervalSeconds 0 lets the initial advance print at once.
            var frozen = CaptureLines(total: 1000000, intervalSeconds: 0.0, heartbeatSeconds: 0.05,
                act: p =>
                {
                    p.Report(1);            // prints "0%"
                    Thread.Sleep(150);      // idle past the 0.05 s heartbeat threshold
                    p.Report(1);            // still 0% -> heartbeat line
                });
            // Only the heartbeat line carries the "(... elapsed)" parenthetical; the plain
            // "N%" advance lines and the heading do not (punctuation, not localizable text).
            StringAssert.Contains(string.Join("\n", frozen), "(");

            // A phase that advances on every report but never idles past a (wide) heartbeat
            // threshold emits only advance lines -- no heartbeat clutter on healthy phases.
            var fast = CaptureLines(total: 100, intervalSeconds: 0.0, heartbeatSeconds: 999.0,
                act: p =>
                {
                    p.Report(25);
                    p.Report(50);
                    p.Report(75);
                });
            Assert.IsFalse(string.Join("\n", fast).Contains("("),
                "a fast, always-advancing phase should emit no heartbeat line");
        }

        /// <summary>
        /// The display rules for a CLI log (Brendan, 2026-09-25): the heading ALWAYS prints, so
        /// the log says what ran, in order; a step that finished inside MinPercentTime and showed
        /// no percent reads as its heading alone ("Reading... / Removing duplicates..."), not a
        /// heading plus a 100% that only says it was fast; and a step that showed a percent always
        /// closes with 100%, which the constructor's clamp to the report interval guarantees.
        /// </summary>
        [TestMethod]
        public void TestProgressReporterHeadingAlwaysPrints()
        {
            // Inside MinPercentTime, no percent shown: the heading and nothing else. Reported at
            // 50% so this proves the thresholds held back the percent, not an absent Report call.
            var fast = CaptureLines(total: 100, intervalSeconds: 60.0, heartbeatSeconds: 60.0,
                act: p => p.Report(50), minPercentSeconds: 60.0);
            Assert.AreEqual(1, fast.Count, @"a fast step prints only its heading");
            StringAssert.Contains(fast[0], @"phase...");

            // Past MinPercentTime but no percent shown (the interval has not elapsed): the
            // heading, then the closing 100%.
            var announced = CaptureLines(total: 100, intervalSeconds: 60.0, heartbeatSeconds: 60.0,
                act: p => p.Report(50), minPercentSeconds: 0.0);
            Assert.AreEqual(2, announced.Count, @"expected the heading and its completion line");
            StringAssert.Contains(announced[0], @"phase...");
            StringAssert.Contains(announced[1], @"100%");

            // MinPercentTime is clamped to the interval, so a step that showed 50% always closes.
            var partial = CaptureLines(total: 100, intervalSeconds: 0.0, heartbeatSeconds: 60.0,
                act: p => p.Report(50), minPercentSeconds: 60.0);
            StringAssert.Contains(partial[0], @"phase...");
            StringAssert.Contains(string.Join("\n", partial), @"50%");
            StringAssert.Contains(partial[partial.Count - 1], @"100%");
        }

        /// <summary>
        /// Run <paramref name="act"/> against a <see cref="ProgressReporter"/> whose output is
        /// captured through a scoped writer (AsyncLocal, so no cross-test static leak) and
        /// return the non-empty output lines.
        /// </summary>
        private static List<string> CaptureLines(long total, double intervalSeconds,
            double heartbeatSeconds, Action<ProgressReporter> act,
            double minPercentSeconds = 0.0)
        {
            var writer = new StringWriter();
            using (OspreyOutput.PushScopedOut(writer))
            // minPercent defaults to 0 here so the display tests keep exercising the format at
            // millisecond durations, the same reason they already inject a 0.05 s heartbeat. The
            // suppression behaviour it governs has its own test above.
            using (var reporter = new ProgressReporter(@"phase", total, string.Empty,
                intervalSeconds, heartbeatSeconds, minPercentSeconds))
            {
                act(reporter);
            }
            return writer.ToString()
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }
    }
}
