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
using System.Diagnostics;
using pwiz.Common.CommonResources;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Cheap, self-contained progress reporter for a command-line tool's determinate loops - a
    /// stopwatch-throttled percent printer modeled on Skyline's CommandProgressMonitor, but
    /// with none of the IProgressMonitor / ProgressStatus / UI machinery. Written for Osprey
    /// and moved here as the one progress reporter the command-line tools share (Osprey,
    /// CarafeSharp): where no GUI tracks the work in memory, this simplification has proved
    /// enough, and the full ProgressStatus machinery too complicated for general use.
    ///
    /// Prints a "&lt;activity&gt;..." heading on construction, then a throttled "&lt;pct&gt;%"
    /// line only when the percent advances AND at least the report interval has elapsed, and
    /// closes with "100%" on <see cref="Complete"/> unless the step finished inside
    /// <see cref="MIN_PERCENT_SECONDS"/> without showing a percent. So a fast step shows just
    /// its heading, while a multi-second op shows a handful of intermediate percents. The timer
    /// throttle is the whole point: progress just needs to say "still working" without an
    /// arbitrary per-N-units cadence cluttering the important output - so it behaves the same
    /// regardless of a verbose switch (implementer detail belongs in the surrounding log lines,
    /// not in finer-grained progress).
    ///
    /// Writes to the process-wide <see cref="CommandOutput.Out"/> seam. Thread-safe: callers in
    /// parallel loops may call <see cref="Report"/> concurrently. Run a step through
    /// <see cref="Run(string, long, Action{ProgressReporter}, string, double, double, double)"/>
    /// (or <see cref="Run{T}"/> when it produces a value): the wrapper prints the
    /// heading, runs the body, and closes with <see cref="Complete"/> only when the body returns,
    /// so a step that throws never logs completion and no success path can forget to declare it.
    /// An early <c>return</c> from the body is a success like any other.
    /// <code>
    /// ProgressReporter.Run("Scoring isolation windows", windows.Count, p =>
    /// {
    ///     Parallel.For(0, windows.Count, i => { /* ... */ p.Report(Interlocked.Increment(ref done)); });
    /// });
    /// </code>
    /// A lambda cannot <c>yield</c>, and cannot capture an outer <c>ref</c> local, <c>ref</c> or
    /// <c>out</c> parameter or <see cref="Span{T}"/> (it may declare its own); copy such a value to
    /// an ordinary local, return it through <see cref="Run{T}"/>, or move the code into a method
    /// the body calls. An iterator that yields inside its step opens it with <see cref="Begin"/>.
    /// </summary>
    public sealed class ProgressReporter : IDisposable
    {
        /// <summary>
        /// Throttle interval for the large I/O steps (mzML read, parquet writes). Wider
        /// than the compute loops' cadence because on HRAM/Astral-class data these steps
        /// run for tens of seconds and a 2s cadence emits a long string of percent lines;
        /// 5s still reassures a watching user the run is alive without cluttering the log.
        /// </summary>
        public const double IO_INTERVAL_SECONDS = 5.0;

        /// <summary>
        /// Idle threshold for the frozen-percent heartbeat. When a determinate phase
        /// progresses slower than 1% per this many seconds, the integer percent does not
        /// advance, so the advance-gated <see cref="Report"/> would print nothing for as
        /// long as it takes to cross the next whole percent - the console looks hung
        /// exactly when progress is slowest and a watching user most needs reassurance
        /// (an 82-file Osprey Stage-6 rescore went ~1 h silent this way). After this long with
        /// no line, Report reprints the current percent with elapsed time. Fast phases advance
        /// the percent within the report interval and never reach the idle window, so no
        /// extra lines appear on them. Wider than <see cref="IO_INTERVAL_SECONDS"/> so it
        /// only trips on genuinely stalled-looking phases.
        ///
        /// <para>Held BELOW the 30 s gap that ai/scripts/perfviz.py flags as a reporting
        /// stall, and deliberately not equal to it. At 30 s the two coincided, so any phase
        /// slow enough to be heartbeat-driven rather than percent-driven parked exactly on
        /// the diagnostic threshold: a 257-file CHS Stage 7 produced 34 gaps of 30-49 s, all
        /// of them healthy work. That matters beyond tidiness - silence is how a hung run and
        /// an OOM-killed one both look, so the heartbeat has to stay clear of the line that
        /// says "this went quiet". Cohort size sets which regime a phase is in: the same
        /// phase at 86 files advanced a whole percent every ~7 s and never reached the idle
        /// window at all.</para>
        /// </summary>
        public const double HEARTBEAT_SECONDS = 15.0;

        /// <summary>
        /// How long a step must run before <see cref="Complete"/> closes it with 100%. The heading
        /// always prints - a CLI log is read afterwards, and a step that ran must say so - but a
        /// step that finished before this and showed no percent reads "Reading..." alone rather
        /// than "Reading..." plus a 100% that only says it was fast.
        ///
        /// <para>Deliberately NOT Skyline's LongWaitDlg rule, which hides the dialog entirely
        /// for a fast operation. Osprey #4582 applied that rule here, hiding the heading too;
        /// headings then came and went between runs of the same data, and a route check keyed
        /// on one could not fire at test scale (Brendan, 2026-09-25).</para>
        /// </summary>
        public const double MIN_PERCENT_SECONDS = 1.0;

        /// <summary>
        /// Run one progress-reported step: print the "&lt;activity&gt;..." heading, run
        /// <paramref name="body"/> with the reporter, and declare success with
        /// <see cref="Complete"/> when the body returns. There is no <c>catch</c>: an exception
        /// skips <see cref="Complete"/>, so a failed step ends without a completion line, and
        /// propagates unchanged.
        /// <para><paramref name="activity"/> is the heading text, printed first (without the
        /// trailing "..."); <paramref name="total"/> the number of units the body reports against
        /// (a <see cref="long"/>, so byte and row counts on Astral-class data cannot overflow);
        /// <paramref name="indent"/> leading whitespace for the heading (null for none), with the
        /// percent lines one level (2 spaces) deeper; <paramref name="intervalSeconds"/> the minimum
        /// seconds between percent lines; <paramref name="heartbeatSeconds"/> the idle threshold
        /// of the frozen-percent heartbeat (<see cref="HEARTBEAT_SECONDS"/>); and
        /// <paramref name="minPercentSeconds"/> the run time before a step that showed no percent
        /// also closes with 100% (<see cref="MIN_PERCENT_SECONDS"/>). The wrappers and
        /// <see cref="Begin"/> take the same parameters.</para>
        /// </summary>
        public static void Run(string activity, long total, Action<ProgressReporter> body, string indent = null,
            double intervalSeconds = 1.0, double heartbeatSeconds = HEARTBEAT_SECONDS,
            double minPercentSeconds = MIN_PERCENT_SECONDS)
        {
            TryRun(activity, total, progress =>
            {
                body(progress);
                return true;
            }, indent, intervalSeconds, heartbeatSeconds, minPercentSeconds);
        }

        /// <summary>
        /// <see cref="Run(string, long, Action{ProgressReporter}, string, double, double, double)"/>
        /// for a step that produces a value: returns what
        /// <paramref name="body"/> returns, after declaring the step complete.
        ///
        /// <para>Refuses <c>bool</c>: a bool-returning body has the same shape as a
        /// <see cref="TryRun"/> body, and passed here by mistake it would declare a failed step
        /// complete. A success flag goes through <see cref="TryRun"/>.</para>
        /// </summary>
        public static T Run<T>(string activity, long total, Func<ProgressReporter, T> body, string indent = null,
            double intervalSeconds = 1.0, double heartbeatSeconds = HEARTBEAT_SECONDS,
            double minPercentSeconds = MIN_PERCENT_SECONDS)
        {
            if (typeof(T) == typeof(bool))
                throw new InvalidOperationException(@"ProgressReporter.Run<bool> would declare a failed step complete; use TryRun for a body that returns success.");
            T result = default;
            TryRun(activity, total, progress =>
            {
                result = body(progress);
                return true;
            }, indent, intervalSeconds, heartbeatSeconds, minPercentSeconds);
            return result;
        }

        /// <summary>
        /// <see cref="Run(string, long, Action{ProgressReporter}, string, double, double, double)"/>
        /// for a step that can fail without throwing - one that reports its error and returns
        /// false. The step is declared complete only when <paramref name="body"/> returns true,
        /// so a failure reported that way ends without a completion line, as a throw does.
        /// Returns what the body returned.
        /// </summary>
        public static bool TryRun(string activity, long total, Func<ProgressReporter, bool> body, string indent = null,
            double intervalSeconds = 1.0, double heartbeatSeconds = HEARTBEAT_SECONDS,
            double minPercentSeconds = MIN_PERCENT_SECONDS)
        {
            using (var progress = new ProgressReporter(activity, total, indent, intervalSeconds, heartbeatSeconds, minPercentSeconds))
            {
                bool succeeded = body(progress);
                if (succeeded)
                    progress.Complete();
                return succeeded;
            }
        }

        /// <summary>
        /// Open a progress-reported step whose body cannot be a lambda - an iterator that
        /// <c>yield</c>s inside the step. Prints the "&lt;activity&gt;..." heading now and returns a
        /// <see cref="Scope"/> for a <c>using</c> block. The body calls
        /// <see cref="Scope.MarkComplete"/> as the last statement of its success path, and the step
        /// is declared complete when the scope is disposed, only if that happened: an exception, or
        /// an iterator its consumer abandoned, disposes the scope unmarked and the step ends without
        /// a completion line. Prefer
        /// <see cref="Run(string, long, Action{ProgressReporter}, string, double, double, double)"/>
        /// wherever the body can be a lambda, since there success cannot be left undeclared.
        /// Parameters as for
        /// <see cref="Run(string, long, Action{ProgressReporter}, string, double, double, double)"/>.
        /// </summary>
        public static Scope Begin(string activity, long total, string indent = null,
            double intervalSeconds = 1.0, double heartbeatSeconds = HEARTBEAT_SECONDS,
            double minPercentSeconds = MIN_PERCENT_SECONDS)
        {
            return new Scope(new ProgressReporter(activity, total, indent, intervalSeconds, heartbeatSeconds, minPercentSeconds));
        }

        private readonly long _total;
        private readonly string _indent;
        private readonly double _intervalSeconds;
        private readonly double _heartbeatSeconds;
        private readonly double _minPercentSeconds;
        private readonly Stopwatch _stopwatch;
        private readonly object _lock = new object();
        // Non-null only inside a MultiProgressReporter per-file scope (files processed in
        // parallel): captured at construction so this reporter feeds the file's active segment
        // instead of printing "<pct>%" lines. The aggregate "[i] p%" line shows the
        // motion; the heading still buffers into the file's narrative block. Null on
        // the common sequential / single-file paths - inline percent printing then.
        private readonly IProgressSink _sink;
        private int _lastPercent = -1;
        private double _lastReportSeconds;
        private bool _completed;

        /// <summary>
        /// Start a reporter for an <paramref name="activity"/> spanning <paramref name="total"/>
        /// units, printing the "&lt;activity&gt;..." heading immediately.
        ///
        /// <para>Private: a reporter is created only by the Run wrappers and <see cref="Begin"/>,
        /// which decide completion from how the step ended, so no caller can construct one and
        /// forget to declare its success.</para>
        /// </summary>
        /// <param name="activity">Heading text printed on construction (without the trailing "...").</param>
        /// <param name="total">Total number of units; percent is reported as current/total. A
        /// <see cref="long"/> so byte counts (mzML reads) and row counts (parquet writes) on
        /// Astral-class data can exceed <see cref="int.MaxValue"/> without overflow; an
        /// <see cref="int"/> argument widens implicitly.</param>
        /// <param name="indent">Leading whitespace for the heading so it lines up with the
        /// matching completion line (null for none); the percent lines are indented one level
        /// (2 spaces) deeper.</param>
        /// <param name="intervalSeconds">Minimum seconds between percent lines (timer throttle).</param>
        /// <param name="heartbeatSeconds">Idle threshold for the frozen-percent heartbeat
        /// (see <see cref="HEARTBEAT_SECONDS"/>). Injectable so tests can trip it quickly.</param>
        /// <param name="minPercentSeconds">Minimum run time before the closing 100% is printed
        /// for a step that showed no percent (see <see cref="MIN_PERCENT_SECONDS"/>).</param>
        private ProgressReporter(string activity, long total, string indent = null, double intervalSeconds = 1.0,
            double heartbeatSeconds = HEARTBEAT_SECONDS, double minPercentSeconds = MIN_PERCENT_SECONDS)
        {
            _total = total;
            _indent = indent ?? string.Empty;
            _intervalSeconds = intervalSeconds;
            _heartbeatSeconds = heartbeatSeconds;
            // Clamped to the report interval, which makes the closing rule hold by construction:
            // the first percent line cannot print before one interval has elapsed
            // (_lastReportSeconds starts at 0), so any step that showed a percent has passed
            // _minPercentSeconds and Complete closes it with 100% - a successful step's display is
            // never left at 83%.
            _minPercentSeconds = Math.Min(minPercentSeconds, intervalSeconds);
            _sink = MultiProgressReporter.CurrentSink;
            _stopwatch = Stopwatch.StartNew();
            // The heading is written now, always: the log says what ran, in order, whatever it
            // cost. Only the PERCENT lines wait for the operation to prove it is worth watching,
            // so a fast step reads "Reading..." with no "100%" under it. (Deferring the heading
            // too made steps appear and vanish between runs of the same data, depending only on
            // how long they happened to take.)
            //
            // Inside a MultiProgressReporter scope the heading buffers into the file's narrative
            // block rather than racing other files to the console.
            // The trailing ellipsis is a resource: Chinese writes it as one full-width character.
            CommandOutput.Out.WriteLine(_indent + string.Format(MessageResources.ProgressReporter_ProgressReporter__0____, activity));
        }

        /// <summary>
        /// Report that <paramref name="current"/> of the total units are done. Safe to call from
        /// parallel workers. Prints a throttled percent only when the percent advances and the
        /// report interval has elapsed.
        /// </summary>
        public void Report(long current)
        {
            lock (_lock)
            {
                int percent = _total > 0 ? (int)(100L * current / _total) : 100;
                if (_sink != null)
                {
                    // Multi-file mode: feed the file's active segment (which maps it
                    // into the "[i] p%" aggregate line) rather than printing a percent
                    // line. The MultiProgressReporter owns the display throttle, so we
                    // forward every advance; the segment sink is monotonic.
                    if (percent > _lastPercent)
                    {
                        _lastPercent = percent;
                        _sink.Report(percent);
                    }
                    return;
                }
                double now = _stopwatch.Elapsed.TotalSeconds;
                // The first percent waits one interval (_lastReportSeconds starts at 0).
                if (percent > _lastPercent && now - _lastReportSeconds >= _intervalSeconds)
                {
                    CommandOutput.Out.WriteLine(@"{0}  {1}%", _indent, percent);
                    _lastPercent = percent;
                    _lastReportSeconds = now;
                }
                else if (percent < 100 && now - _lastReportSeconds >= _heartbeatSeconds)
                {
                    // Slow-phase heartbeat: when progress is under 1% per _heartbeatSeconds
                    // the integer percent freezes, so switch to FINER granularity - a
                    // fractional percent plus the running item count - so a genuinely
                    // moving job shows a moving number (real progress), not just a ticking
                    // clock. Elapsed still surfaces pathological slowness. Fast phases
                    // advance the whole percent within the report interval and never reach
                    // this idle window, so they stay clutter-free. NOTE: this only fires
                    // when the phase calls Report; a phase that blocks inside one bulk
                    // operation (no Report calls) needs to be wrapped in a reporter first.
                    double pctExact = _total > 0 ? 100.0 * current / _total : 100.0;
                    CommandOutput.Out.WriteLine(
                        MessageResources.ProgressReporter_Report__0____1_____2___3____4__elapsed_,
                        _indent, pctExact, current, _total, FormatElapsed(_stopwatch.Elapsed));
                    _lastPercent = percent;
                    _lastReportSeconds = now;
                }
            }
        }

        /// <summary>
        /// Close the scope. Prints nothing: only <see cref="Complete"/> can say the step
        /// succeeded, because Dispose runs the same way whether the scope finished, threw or
        /// returned early, and forcing 100% here made a failed step log completion (#4583).
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Declare that the step succeeded, and close it with 100% unless it already showed it
        /// or finished inside <see cref="MIN_PERCENT_SECONDS"/>. Private: the Run wrappers call it
        /// when their body succeeds, and a <see cref="Scope"/> when it is disposed after
        /// <see cref="Scope.MarkComplete"/>; a step that ends without it has no completion line.
        /// Calling it more than once has no further effect.
        /// </summary>
        private void Complete()
        {
            lock (_lock)
            {
                if (_completed)
                    return;
                _completed = true;
                if (_sink != null)
                {
                    // Bank this segment at 100% so the file percent reaches the
                    // segment boundary even on a sub-second phase; no percent line.
                    _sink.Report(100);
                    return;
                }
                // No explicit "did we already show a percent?" test: the constructor bounds
                // _minPercentSeconds by the report interval, and a sub-100% line cannot print
                // before that interval has elapsed, so any scope that showed progress has
                // necessarily passed this threshold and closes with 100%. The threshold's only
                // real job is the step that showed its heading and no percent - it decides
                // whether that step also gets a 100%.
                if (_lastPercent < 100 && _stopwatch.Elapsed.TotalSeconds >= _minPercentSeconds)
                {
                    CommandOutput.Out.WriteLine(@"{0}  100%", _indent);
                }
            }
        }

        /// <summary>
        /// Compact elapsed-time formatting for the heartbeat line: "45s", "2m03s", "1h05m".
        /// </summary>
        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalHours >= 1)
                return string.Format(MessageResources.ProgressReporter_FormatElapsed__0_h_1_00_m, (int)elapsed.TotalHours, elapsed.Minutes);
            if (elapsed.TotalMinutes >= 1)
                return string.Format(MessageResources.ProgressReporter_FormatElapsed__0_m_1_00_s, (int)elapsed.TotalMinutes, elapsed.Seconds);
            return string.Format(MessageResources.ProgressReporter_FormatElapsed__0_s, (int)elapsed.TotalSeconds);
        }

        /// <summary>
        /// The handle <see cref="Begin"/> returns: the step's reporter, completed on
        /// <see cref="Dispose"/> only when <see cref="MarkComplete"/> was called first.
        /// </summary>
        public sealed class Scope : IDisposable
        {
            private readonly ProgressReporter _progress;
            private bool _succeeded;

            internal Scope(ProgressReporter progress)
            {
                _progress = progress;
            }

            /// <summary>As <see cref="ProgressReporter.Report"/>.</summary>
            public void Report(long current)
            {
                _progress.Report(current);
            }

            /// <summary>
            /// Declare that the step succeeded. Prints nothing itself: the completion line is
            /// written when the scope is disposed, at the same point in the log a Run wrapper
            /// writes it.
            /// </summary>
            public void MarkComplete()
            {
                _succeeded = true;
            }

            /// <summary>
            /// Close the step, declaring it complete only if <see cref="MarkComplete"/> was called.
            /// </summary>
            public void Dispose()
            {
                if (_succeeded)
                    _progress.Complete();
                _progress.Dispose();
            }
        }
    }
}
