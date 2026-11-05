/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using pwiz.Common.CommonResources;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    /// <summary>
    /// Tests for <see cref="ProgressReporter"/>'s display rules: the frozen-percent heartbeat
    /// (a phase advancing slower than 1% per <see cref="ProgressReporter.HEARTBEAT_SECONDS"/>
    /// must still emit a line so it never looks hung, while a normally advancing phase gains no
    /// heartbeat clutter), and the heading and closing rules of a CLI log. The intervals are
    /// injected small or large so the timer-driven behavior is deterministic without a real
    /// heartbeat-length wait.
    /// </summary>
    [TestClass]
    public class ProgressReporterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestProgressReporter()
        {
            TestHeartbeat();
            TestHeadingAlwaysPrints();
            TestOnlySuccessCompletes();
        }

        private static void TestHeartbeat()
        {
            // Frozen integer percent (1 of 1,000,000 == 0%): after the idle threshold the
            // reporter reprints the current percent with an elapsed parenthetical so the
            // console stays alive. intervalSeconds 0 lets the initial advance print at once.
            var frozen = CaptureLines(total: 1000000, intervalSeconds: 0.0, heartbeatSeconds: 0.05,
                act: p =>
                {
                    p.Report(1);            // Prints "0%"
                    Thread.Sleep(150);      // Idle past the 0.05 s heartbeat threshold
                    p.Report(1);            // Still 0% -> heartbeat line
                });
            // Only the heartbeat line carries the "(... elapsed)" parenthetical; the plain
            // "N%" advance lines and the heading do not. The parenthesis is read from the
            // resource, because Japanese and Chinese write it full-width.
            AssertEx.Contains(string.Join("\n", frozen), HeartbeatMarker);

            // A phase that advances on every report but never idles past a (wide) heartbeat
            // threshold emits only advance lines - no heartbeat clutter on healthy phases.
            var fast = CaptureLines(total: 100, intervalSeconds: 0.0, heartbeatSeconds: 999.0,
                act: p =>
                {
                    p.Report(25);
                    p.Report(50);
                    p.Report(75);
                });
            AssertEx.IsFalse(string.Join("\n", fast).Contains(HeartbeatMarker),
                "a fast, always-advancing phase should emit no heartbeat line");
        }

        /// <summary>
        /// The display rules for a CLI log (Brendan, 2026-09-25): the heading ALWAYS prints, so
        /// the log says what ran, in order; a step that finished inside MinPercentTime and showed
        /// no percent reads as its heading alone ("Reading... / Removing duplicates..."), not a
        /// heading plus a 100% that only says it was fast; and a step that showed a percent always
        /// closes with 100%, which the constructor's clamp to the report interval guarantees.
        /// </summary>
        private static void TestHeadingAlwaysPrints()
        {
            // Inside MinPercentTime, no percent shown: the heading and nothing else. Reported at
            // 50% so this proves the thresholds held back the percent, not an absent Report call.
            var fast = CaptureLines(total: 100, intervalSeconds: 60.0, heartbeatSeconds: 60.0,
                act: p => p.Report(50), minPercentSeconds: 60.0);
            AssertEx.AreEqual(1, fast.Count, "a fast step prints only its heading");
            AssertEx.Contains(fast[0], Heading("phase"));

            // Past MinPercentTime but no percent shown (the interval has not elapsed): the
            // heading, then the closing 100%.
            var announced = CaptureLines(total: 100, intervalSeconds: 60.0, heartbeatSeconds: 60.0,
                act: p => p.Report(50), minPercentSeconds: 0.0);
            AssertEx.AreEqual(2, announced.Count, "expected the heading and its completion line");
            AssertEx.Contains(announced[0], Heading("phase"));
            AssertEx.Contains(announced[1], "100%");

            // MinPercentTime is clamped to the interval, so a step that showed 50% always closes.
            var partial = CaptureLines(total: 100, intervalSeconds: 0.0, heartbeatSeconds: 60.0,
                act: p => p.Report(50), minPercentSeconds: 60.0);
            AssertEx.Contains(partial[0], Heading("phase"));
            AssertEx.Contains(string.Join("\n", partial), "50%");
            AssertEx.Contains(partial[partial.Count - 1], "100%");
        }

        /// <summary>
        /// Success is decided by how the body ended (#4583, #4979): the Run wrappers close a step
        /// with 100% only when its body returns - including an early return - and never when it
        /// throws, or when a <see cref="ProgressReporter.TryRun"/> body reports failure by
        /// returning false. A step that fails after showing a percent must not log completion on
        /// its way out.
        /// </summary>
        private static void TestOnlySuccessCompletes()
        {
            // Past MinPercentTime with no percent shown: the same case that closes with 100% when
            // completed (above) ends at its heading when the body reports failure.
            var abandoned = CaptureLines(total: 100, intervalSeconds: 60.0, heartbeatSeconds: 60.0,
                act: p => p.Report(50), minPercentSeconds: 0.0, complete: false);
            AssertEx.AreEqual(1, abandoned.Count, "a step whose body fails prints no completion line");
            AssertEx.Contains(abandoned[0], Heading("phase"));

            // A step that showed 50% and then threw: the exception passes through the wrapper
            // unchanged, and the last line is still the 50%.
            var failed = Capture(() => AssertEx.ThrowsException<InvalidOperationException>(() =>
                ProgressReporter.Run("phase", 100, p =>
                {
                    p.Report(50);
                    throw new InvalidOperationException();
                }, string.Empty, 0.0, 60.0, 0.0)));
            AssertEx.Contains(failed[failed.Count - 1], "50%");
            AssertEx.IsFalse(failed.Any(line => line.Contains("100%")),
                "a step that threw must not log completion");

            // A body that returns early completed its work: it closes with 100%, and Run<T> hands
            // back what the body returned.
            int returned = 0;
            var early = Capture(() => returned = ProgressReporter.Run("phase", 100, p =>
            {
                for (int done = 10; done <= 100; done += 10)
                {
                    p.Report(done);
                    if (done == 50)
                        return done;    // Stops early, at 50%
                }
                return -1;
            }, string.Empty, 60.0, 60.0, 0.0));
            AssertEx.AreEqual(50, returned);
            AssertEx.Contains(early[early.Count - 1], "100%");

            // TryRun returns what its body returned, and closes the step when that was true.
            bool succeeded = false;
            var tried = Capture(() => succeeded = ProgressReporter.TryRun("phase", 100, p => true,
                string.Empty, 60.0, 60.0, 0.0));
            AssertEx.IsTrue(succeeded);
            AssertEx.Contains(tried[tried.Count - 1], "100%");

            // A success flag passed to Run<T> would be ignored, so Run<bool> refuses it.
            AssertEx.ThrowsException<InvalidOperationException>(() =>
                ProgressReporter.Run("phase", 100, p => true));

            // Begin, for an iterator that yields inside its step: disposing the scope closes the
            // step only when MarkComplete was called, so an exception or an abandoned walk ends
            // at the last percent shown.
            var marked = Capture(() =>
            {
                using (var scope = ProgressReporter.Begin("phase", 100, string.Empty, 60.0, 60.0, 0.0))
                {
                    scope.Report(50);
                    scope.MarkComplete();
                    // A second Dispose (here and at the end of the using) does not close the step twice.
                    scope.Dispose();
                }
            });
            AssertEx.Contains(marked[0], Heading("phase"));
            AssertEx.AreEqual(1, marked.Count(line => line.Contains("100%")),
                "a marked scope should close the step exactly once");
            var unmarked = Capture(() =>
            {
                using (var scope = ProgressReporter.Begin("phase", 100, string.Empty, 0.0, 60.0, 0.0))
                    scope.Report(50);
            });
            AssertEx.Contains(unmarked[unmarked.Count - 1], "50%");
            AssertEx.IsFalse(unmarked.Any(line => line.Contains("100%")),
                "a scope disposed without MarkComplete must not log completion");
        }

        /// <summary>
        /// Run <paramref name="act"/> as the body of a <see cref="ProgressReporter.Run(string, long, Action{ProgressReporter}, string, double, double, double)"/>
        /// step whose output is captured, and return the non-empty output lines. The body
        /// succeeds unless <paramref name="complete"/> is false, which stands for a
        /// <see cref="ProgressReporter.TryRun"/> body that reports failure.
        /// </summary>
        private static List<string> CaptureLines(long total, double intervalSeconds,
            double heartbeatSeconds, Action<ProgressReporter> act,
            double minPercentSeconds = 0.0, bool complete = true)
        {
            // minPercent defaults to 0 here so the display tests keep exercising the format at
            // millisecond durations, the same reason they already inject a 0.05 s heartbeat. The
            // suppression behaviour it governs has its own test above.
            return Capture(() => ProgressReporter.TryRun("phase", total, reporter =>
            {
                act(reporter);
                return complete;
            }, string.Empty, intervalSeconds, heartbeatSeconds, minPercentSeconds));
        }

        /// <summary>
        /// Run <paramref name="action"/> with the command output captured through a scoped writer
        /// (AsyncLocal, so no cross-test static leak) and return the non-empty output lines.
        /// </summary>
        private static List<string> Capture(Action action)
        {
            var writer = new StringWriter();
            using (CommandOutput.PushScopedOut(writer))
            {
                action();
            }
            return writer.ToString()
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        /// <summary>
        /// The text between the percent and the counts in the heartbeat line - "% (" in English,
        /// "%" and a full-width parenthesis in Japanese - which no plain advance line or heading
        /// contains.
        /// </summary>
        private static string HeartbeatMarker
        {
            get
            {
                string format = MessageResources.ProgressReporter_Report__0____1_____2___3____4__elapsed_;
                int start = format.IndexOf("{1", StringComparison.Ordinal);
                start = format.IndexOf('}', start) + 1;
                return format.Substring(start, format.IndexOf("{2", start, StringComparison.Ordinal) - start).Trim();
            }
        }

        /// <summary>
        /// A progress heading as the reporter prints it in the current UI language: the ellipsis
        /// is part of the resource because Chinese writes it as one full-width character.
        /// </summary>
        private static string Heading(string activity)
        {
            return string.Format(MessageResources.ProgressReporter_ProgressReporter__0____, activity);
        }
    }
}
