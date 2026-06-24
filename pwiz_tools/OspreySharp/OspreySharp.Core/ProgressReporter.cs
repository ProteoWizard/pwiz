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

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Cheap, self-contained progress reporter for OspreySharp's determinate loops -- a
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
        private readonly int _total;
        private readonly string _indent;
        private readonly double _intervalSeconds;
        private readonly Stopwatch _stopwatch;
        private readonly object _lock = new object();
        private int _lastPercent = -1;
        private double _lastReportSeconds;

        /// <summary>
        /// Start a reporter for an <paramref name="activity"/> spanning <paramref name="total"/>
        /// units, printing the "&lt;activity&gt;..." heading immediately.
        /// </summary>
        /// <param name="activity">Heading text printed on construction (without the trailing "...").</param>
        /// <param name="total">Total number of units; percent is reported as current/total.</param>
        /// <param name="indent">Leading whitespace for the heading so it lines up with the
        /// matching completion line; the percent lines are indented one level (2 spaces) deeper.</param>
        /// <param name="intervalSeconds">Minimum seconds between percent lines (timer throttle).</param>
        public ProgressReporter(string activity, int total, string indent = "", double intervalSeconds = 1.0)
        {
            _total = total;
            _indent = indent;
            _intervalSeconds = intervalSeconds;
            _stopwatch = Stopwatch.StartNew();
            OspreyOutput.Out.WriteLine("{0}{1}...", indent, activity);
        }

        /// <summary>
        /// Report that <paramref name="current"/> of the total units are done. Safe to call from
        /// parallel workers. Prints a throttled percent only when the percent advances and the
        /// report interval has elapsed.
        /// </summary>
        public void Report(int current)
        {
            lock (_lock)
            {
                int percent = _total > 0 ? (int)(100L * current / _total) : 100;
                double now = _stopwatch.Elapsed.TotalSeconds;
                if (percent > _lastPercent && now - _lastReportSeconds >= _intervalSeconds)
                {
                    OspreyOutput.Out.WriteLine("{0}  {1}%", _indent, percent);
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
                if (_lastPercent < 100)
                    OspreyOutput.Out.WriteLine("{0}  100%", _indent);
            }
        }
    }
}
