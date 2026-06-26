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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Where a <see cref="ProgressReporter"/> sends its 0..100 percent when it runs
    /// inside a <see cref="MultiProgressReporter"/> per-file scope, instead of
    /// printing a "&lt;pct&gt;%" line. The single segment-scoped implementation
    /// (<c>MultiProgressReporter.FileScope</c>'s segment sink) maps that percent
    /// into the file's composite percent; the aggregate <c>[i] p%</c> line shows the
    /// motion. A null sink (the common sequential / single-file path) means the
    /// reporter prints inline as before -- so this seam is inert outside
    /// <c>--parallel-files</c>.
    /// </summary>
    public interface IProgressSink
    {
        /// <summary>Report <paramref name="percent"/> (0..100) of the current
        /// segment's work done.</summary>
        void Report(int percent);
    }

    /// <summary>
    /// Collapses concurrent per-file progress (<c>--parallel-files</c>) into
    /// Skyline's MultiProgressStatus look: a single throttled
    /// <c>[1] 42%  [2] 38%  [3] 51%</c> line while files run at once, plus each
    /// file's narrative buffered and flushed as one contiguous block when the file
    /// finishes (Boost-Build per-action buffering) so blocks never interleave. A
    /// thin wrapper over <see cref="ProgressReporter"/>'s stopwatch throttle and the
    /// <see cref="OspreyOutput"/> seam -- deliberately NOT a port of Skyline's
    /// IProgressMonitor / ProgressStatus / MultiProgressStatus machinery.
    ///
    /// Per-file percent uses the ProgressStatus *segments* model (mimicked, not the
    /// class): one file's work is N equal-weight segments (read, calibrate, score,
    /// write), each counting its own 0..100 via a segment sink, combined into one
    /// 0..100 for the file. Two combine levels then exist: segments -&gt; one file's
    /// percent (the segment sink), and N files' percents -&gt; the <c>[i] p%</c> line
    /// (this class). Equal weight is the simple ProgressStatus default; weighted
    /// segment ends can come later if one phase dominates wall-clock.
    ///
    /// A file is entered with <see cref="BeginFile"/>, which redirects that async
    /// flow's narrative into the file's own buffer and makes the file the ambient
    /// <see cref="Current"/> scope (flowing into the inner scoring
    /// <c>Parallel.For</c> via ExecutionContext). The phase code advances segments
    /// through <see cref="FileScope.BeginSegment"/>; <see cref="ProgressReporter"/> finds the
    /// active segment sink through <see cref="CurrentSink"/> with no signature
    /// changes through the IO / scoring stack. Disposing the file handle flushes its
    /// block and drops it from the aggregate line.
    /// </summary>
    public sealed class MultiProgressReporter
    {
        // The active per-file scope for THIS async flow. Set by BeginFile and flowed
        // into the inner scoring Parallel.For (ExecutionContext copy-on-write per
        // iteration isolates one file's scope from its siblings), so ProcessFile can
        // advance segments and ProgressReporter can read the active segment sink
        // without any per-file argument threaded through the scoring stack.
        private static readonly AsyncLocal<FileScope> _current = new AsyncLocal<FileScope>();

        /// <summary>The per-file scope active on the current async flow, or null when
        /// not inside one (sequential / single-file paths). Phase code advances the
        /// active file's segment via <c>Current?.BeginSegment()</c>, a no-op off the
        /// parallel path.</summary>
        public static FileScope Current
        {
            get { return _current.Value; }
        }

        /// <summary>The active file segment's percent sink for this async flow, or
        /// null when not inside a per-file scope -- the seam <see cref="ProgressReporter"/>
        /// reads to decide whether to route its percent or print it.</summary>
        internal static IProgressSink CurrentSink
        {
            get { return _current.Value?.CurrentSegmentSink; }
        }

        // The real, unscoped process writer the aggregate line + completion blocks
        // render to (bypassing the per-file buffer redirect). Captured once at
        // construction, before any file scope is pushed.
        private readonly TextWriter _out;
        private readonly double _intervalSeconds;
        private readonly Stopwatch _stopwatch;

        // Guards the slot set, the last-rendered state, and every write to _out
        // (aggregate line + block flush), so concurrent files serialize their output.
        private readonly object _renderLock = new object();
        private readonly SortedDictionary<int, FileSlot> _slots = new SortedDictionary<int, FileSlot>();
        private double _lastRenderSeconds = double.NegativeInfinity;
        private string _lastRendered;

        /// <summary>
        /// Start an aggregator that renders the <c>[i] p%</c> line no more than once
        /// per <paramref name="intervalSeconds"/> (the same I/O cadence the per-file
        /// reporters use by default). Captures the current process writer as the
        /// render target.
        /// </summary>
        public MultiProgressReporter(double intervalSeconds = ProgressReporter.IO_INTERVAL_SECONDS)
        {
            _out = OspreyOutput.RealOut;
            _intervalSeconds = intervalSeconds;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Enter a per-file scope for file <paramref name="index"/> (0-based; shown
        /// as <c>[index+1]</c>): register its slot, redirect this async flow's
        /// narrative into the file's own buffer, and make the scope the ambient
        /// <see cref="Current"/>. <paramref name="segmentCount"/> is the number of
        /// equal-weight phases the file's percent is divided into. Returns a handle
        /// whose disposal flushes the file's buffered block and drops its slot.
        /// </summary>
        public FileScope BeginFile(int index, string displayName, int segmentCount)
        {
            var buffer = new StringWriter();
            var slot = new FileSlot(index, displayName);
            var scope = new FileScope(this, slot, buffer, segmentCount);
            lock (_renderLock)
            {
                _slots[index] = slot;
                RenderLocked(force: true);
            }
            // Redirect narrative (ctx.LogInfo, ProgressReporter headings, WARN) for
            // this async flow -- and the inner scoring Parallel.For it spawns -- into
            // the file buffer until the scope is disposed, then publish the scope.
            // Wrap the buffer in a StatFilteringTextWriter so the machine-parseable
            // [COUNT]/[TIMING]/[BENCH]/[STAGE-WALL] lines are dropped as they are
            // buffered (unless --perf-stats), exactly as the unbuffered Out does --
            // otherwise a file's buffered block would leak the stat lines the default
            // log suppresses, making a normal run look like perf mode.
            scope.OutCookie = OspreyOutput.PushScopedOut(
                new StatFilteringTextWriter(scope.ScopedWriter));
            _current.Value = scope;
            return scope;
        }

        // Raise file <paramref name="slot"/> to <paramref name="filePercent"/> and
        // re-render on the throttle. Monotonic: a lower value (e.g. a later segment's
        // reporter restarting at 0) never pulls the line backward.
        internal void ReportFilePercent(FileSlot slot, int filePercent)
        {
            lock (_renderLock)
            {
                if (filePercent <= slot.Percent)
                    return;
                slot.Percent = filePercent;
                RenderLocked(force: false);
            }
        }

        // Flush the file's buffered narrative as one contiguous block, then drop its
        // slot and re-render so the finished file leaves the aggregate line.
        internal void CompleteFile(FileScope scope)
        {
            lock (_renderLock)
            {
                string block = scope.BufferContents;
                if (!string.IsNullOrEmpty(block))
                    _out.Write(block);
                _slots.Remove(scope.Slot.Index);
                RenderLocked(force: true);
            }
        }

        // Render the "[i] p%" line for the currently-active slots, throttled unless
        // forced (a file entering or completing forces it so the set is current).
        // Caller holds _renderLock.
        private void RenderLocked(bool force)
        {
            if (_slots.Count == 0)
                return;
            double now = _stopwatch.Elapsed.TotalSeconds;
            if (!force && now - _lastRenderSeconds < _intervalSeconds)
                return;

            var sb = new StringBuilder();
            bool first = true;
            foreach (var kv in _slots)
            {
                if (!first)
                    sb.Append(@"  ");
                first = false;
                sb.AppendFormat(@"[{0}] {1}%", kv.Key + 1, kv.Value.Percent);
            }
            string line = sb.ToString();
            // Suppress an identical repeat (no advance since the last render).
            if (string.Equals(line, _lastRendered, StringComparison.Ordinal))
                return;
            _out.WriteLine(line);
            _lastRendered = line;
            _lastRenderSeconds = now;
        }

        /// <summary>One concurrently-processing file's aggregate-line state: its
        /// display index and current composite percent.</summary>
        internal sealed class FileSlot
        {
            internal FileSlot(int index, string displayName)
            {
                Index = index;
                DisplayName = displayName;
            }

            internal int Index { get; }
            internal string DisplayName { get; }
            internal int Percent { get; set; }
        }

        /// <summary>
        /// A live per-file scope: owns the file's narrative buffer and its segment
        /// cursor. Phase code advances segments with <see cref="BeginSegment"/>; the
        /// active segment's <see cref="IProgressSink"/> maps an inner reporter's
        /// 0..100 into that segment's equal-weight slice of the file's 0..100.
        /// Disposing flushes the buffered block and removes the file from the
        /// aggregate line.
        /// </summary>
        public sealed class FileScope : IDisposable
        {
            private readonly MultiProgressReporter _owner;
            private readonly StringWriter _buffer;
            private readonly TextWriter _syncBuffer;
            private readonly int _segmentCount;
            private readonly object _segmentLock = new object();
            // Segments fully behind the cursor (each contributes its full slice).
            private int _completedSegments;
            private SegmentSink _currentSegmentSink;
            private bool _disposed;

            internal FileScope(MultiProgressReporter owner, FileSlot slot,
                StringWriter buffer, int segmentCount)
            {
                _owner = owner;
                Slot = slot;
                _buffer = buffer;
                // The inner scoring Parallel.For can write narrative from several
                // threads at once into this one buffer; serialize those writes.
                _syncBuffer = TextWriter.Synchronized(buffer);
                _segmentCount = Math.Max(1, segmentCount);
            }

            internal FileSlot Slot { get; }

            /// <summary>The writer the per-file scope redirects narrative into (a
            /// thread-safe wrapper over the file's buffer).</summary>
            internal TextWriter ScopedWriter
            {
                get { return _syncBuffer; }
            }

            internal IProgressSink CurrentSegmentSink
            {
                get { return _currentSegmentSink; }
            }

            // The cookie that restores the prior OspreyOutput.Out on dispose.
            internal IDisposable OutCookie { get; set; }

            // Snapshot of the buffered narrative, read once on completion after the
            // file's work (and its inner threads) have finished writing.
            internal string BufferContents
            {
                get
                {
                    lock (_segmentLock)
                        return _buffer.ToString();
                }
            }

            /// <summary>
            /// Advance to the next equal-weight segment: bank the previous segment at
            /// its full slice end, then open a fresh segment whose 0..100 maps into
            /// the next slice. Bumps the file percent to the new segment's start
            /// boundary so a phase that emits no reporter (e.g. calibration) still
            /// carries the file forward.
            /// </summary>
            public void BeginSegment()
            {
                lock (_segmentLock)
                {
                    if (_currentSegmentSink != null && _completedSegments < _segmentCount)
                        _completedSegments++;
                    _currentSegmentSink = new SegmentSink(this, _completedSegments);
                }
                UpdateFilePercent(_completedSegments, 0);
            }

            // Combine (completedSegments + segPercent/100) / segmentCount into the
            // file's 0..100 and report it to the aggregator.
            internal void UpdateFilePercent(int completedSegments, int segPercent)
            {
                if (segPercent < 0)
                    segPercent = 0;
                else if (segPercent > 100)
                    segPercent = 100;
                long composite = (completedSegments * 100L + segPercent) / _segmentCount;
                if (composite > 100)
                    composite = 100;
                _owner.ReportFilePercent(Slot, (int)composite);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                // Restore the prior Out and clear the ambient scope BEFORE flushing,
                // so the block flush (and aggregate render) write to the real writer.
                OutCookie?.Dispose();
                _current.Value = null;
                _owner.CompleteFile(this);
            }

            /// <summary>
            /// One segment's percent sink: maps an inner reporter's 0..100 into this
            /// segment's slice of the file's percent. Monotonic within the segment so
            /// a phase with more than one reporter (e.g. parquet "Preparing" then
            /// "Writing") never regresses the file percent; the slice fills and holds.
            /// </summary>
            private sealed class SegmentSink : IProgressSink
            {
                private readonly FileScope _scope;
                private readonly int _baseSegment;
                private int _maxPercent = -1;

                internal SegmentSink(FileScope scope, int baseSegment)
                {
                    _scope = scope;
                    _baseSegment = baseSegment;
                }

                public void Report(int percent)
                {
                    if (percent < 0)
                        percent = 0;
                    else if (percent > 100)
                        percent = 100;
                    if (percent <= _maxPercent)
                        return;
                    _maxPercent = percent;
                    _scope.UpdateFilePercent(_baseSegment, percent);
                }
            }
        }
    }
}
