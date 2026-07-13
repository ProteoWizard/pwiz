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
using System.Diagnostics;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Cheap, self-contained progress reporter for Osprey's determinate loops -- a
    /// stopwatch-throttled percent printer modeled on Skyline's CommandProgressMonitor, but
    /// with none of the IProgressMonitor / ProgressStatus / UI machinery. Deliberately a
    /// stopgap: it lets us get timer-style percent output now and learn whether the full
    /// Skyline ProgressStatus/ProgressMonitor (CLI + UI) is worth porting into PortableUtil.
    ///
    /// Prints a "&lt;activity&gt;..." heading on construction, then a throttled "&lt;pct&gt;%"
    /// line only when the percent advances AND at least the report interval has elapsed, and
    /// always forces a final "100%" on Dispose. So a sub-second op shows just the heading +
    /// "100%", while a multi-second op shows a handful of intermediate percents. The timer
    /// throttle is the whole point: progress just needs to say "still working" without an
    /// arbitrary per-N-units cadence cluttering the important output -- so it behaves the same
    /// regardless of --verbose (implementer detail belongs in the surrounding log lines, not
    /// in finer-grained progress).
    ///
    /// Writes to the process-wide <see cref="OspreyOutput.Out"/> seam. Thread-safe: callers in
    /// parallel loops may call <see cref="Report"/> concurrently. Use with <c>using</c> so the
    /// final 100% is emitted on scope exit:
    /// <code>
    /// using (var p = new ProgressReporter("Scoring isolation windows", windows.Count))
    ///     Parallel.For(0, windows.Count, i => { /* ... */ p.Report(Interlocked.Increment(ref done)); });
    /// </code>
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
        /// long as it takes to cross the next whole percent -- the console looks hung
        /// exactly when progress is slowest and a watching user most needs reassurance
        /// (an 82-file Stage-6 rescore went ~1 h silent this way). After this long with no
        /// line, Report reprints the current percent with elapsed time. Fast phases advance
        /// the percent within the report interval and never reach the idle window, so no
        /// extra lines appear on them. Wider than <see cref="IO_INTERVAL_SECONDS"/> so it
        /// only trips on genuinely stalled-looking phases.
        /// </summary>
        public const double HEARTBEAT_SECONDS = 30.0;

        private readonly long _total;
        private readonly string _indent;
        private readonly double _intervalSeconds;
        private readonly double _heartbeatSeconds;
        private readonly Stopwatch _stopwatch;
        private readonly object _lock = new object();
        // Non-null only inside a MultiProgressReporter per-file scope (--parallel-files):
        // captured at construction so this reporter feeds the file's active segment
        // instead of printing "<pct>%" lines. The aggregate "[i] p%" line shows the
        // motion; the heading still buffers into the file's narrative block. Null on
        // the common sequential / single-file paths -- inline percent printing then.
        private readonly IProgressSink _sink;
        private int _lastPercent = -1;
        private double _lastReportSeconds;

        /// <summary>
        /// Start a reporter for an <paramref name="activity"/> spanning <paramref name="total"/>
        /// units, printing the "&lt;activity&gt;..." heading immediately.
        /// </summary>
        /// <param name="activity">Heading text printed on construction (without the trailing "...").</param>
        /// <param name="total">Total number of units; percent is reported as current/total. A
        /// <see cref="long"/> so byte counts (mzML reads) and row counts (parquet writes) on
        /// Astral-class data can exceed <see cref="int.MaxValue"/> without overflow; an
        /// <see cref="int"/> argument widens implicitly.</param>
        /// <param name="indent">Leading whitespace for the heading so it lines up with the
        /// matching completion line; the percent lines are indented one level (2 spaces) deeper.</param>
        /// <param name="intervalSeconds">Minimum seconds between percent lines (timer throttle).</param>
        /// <param name="heartbeatSeconds">Idle threshold for the frozen-percent heartbeat
        /// (see <see cref="HEARTBEAT_SECONDS"/>). Injectable so tests can trip it quickly.</param>
        public ProgressReporter(string activity, long total, string indent = "", double intervalSeconds = 1.0,
            double heartbeatSeconds = HEARTBEAT_SECONDS)
        {
            _total = total;
            _indent = indent;
            _intervalSeconds = intervalSeconds;
            _heartbeatSeconds = heartbeatSeconds;
            _sink = MultiProgressReporter.CurrentSink;
            _stopwatch = Stopwatch.StartNew();
            OspreyOutput.Out.WriteLine("{0}{1}...", indent, activity);
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
                if (percent > _lastPercent && now - _lastReportSeconds >= _intervalSeconds)
                {
                    OspreyOutput.Out.WriteLine("{0}  {1}%", _indent, percent);
                    _lastPercent = percent;
                    _lastReportSeconds = now;
                }
                else if (percent < 100 && now - _lastReportSeconds >= _heartbeatSeconds)
                {
                    // Slow-phase heartbeat: when progress is under 1% per _heartbeatSeconds
                    // the integer percent freezes, so switch to FINER granularity -- a
                    // fractional percent plus the running item count -- so a genuinely
                    // moving job shows a moving number (real progress), not just a ticking
                    // clock. Elapsed still surfaces pathological slowness. Fast phases
                    // advance the whole percent within the report interval and never reach
                    // this idle window, so they stay clutter-free. NOTE: this only fires
                    // when the phase calls Report; a phase that blocks inside one bulk
                    // operation (no Report calls) needs to be wrapped in a reporter first.
                    double pctExact = _total > 0 ? 100.0 * current / _total : 100.0;
                    OspreyOutput.Out.WriteLine("{0}  {1:0.00}% ({2:N0}/{3:N0}, {4} elapsed)",
                        _indent, pctExact, current, _total, FormatElapsed(_stopwatch.Elapsed));
                    _lastPercent = percent;
                    _lastReportSeconds = now;
                }
            }
        }

        /// <summary>Force a final 100% so even a sub-second op shows completion.</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_sink != null)
                {
                    // Bank this segment at 100% so the file percent reaches the
                    // segment boundary even on a sub-second phase; no percent line.
                    _sink.Report(100);
                    return;
                }
                if (_lastPercent < 100)
                    OspreyOutput.Out.WriteLine("{0}  100%", _indent);
            }
        }

        /// <summary>
        /// Compact elapsed-time formatting for the heartbeat line: "45s", "2m03s", "1h05m".
        /// </summary>
        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalHours >= 1)
                return string.Format("{0}h{1:00}m", (int)elapsed.TotalHours, elapsed.Minutes);
            if (elapsed.TotalMinutes >= 1)
                return string.Format("{0}m{1:00}s", (int)elapsed.TotalMinutes, elapsed.Seconds);
            return string.Format("{0}s", (int)elapsed.TotalSeconds);
        }
    }
}
