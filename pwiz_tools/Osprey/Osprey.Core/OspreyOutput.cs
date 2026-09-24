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
using System.IO;
using System.Threading;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Process-wide output sink for Osprey logging that originates below the exe
    /// layer (e.g. the FDR and IO assemblies, which cannot see <c>Program._out</c>).
    /// <c>Program</c> points <c>Out</c> at its single
    /// <c>CommandStatusWriter</c> so every line picks up the <c>--timestamp</c> /
    /// <c>--memstamp</c> prefixes and <c>--log-file</c> redirection. Defaults to stderr
    /// for standalone / test use.
    ///
    /// Typed as the BCL <see cref="TextWriter"/> so Core needs no PortableUtil reference,
    /// and deliberately NOT the diagnostics module's hook (that stays <c>-d</c>-only).
    /// This is a stepping stone: long operations should ultimately report through an
    /// IProgressMonitor / ProgressStatus rather than writing lines directly.
    /// </summary>
    public static class OspreyOutput
    {
        private static TextWriter _out = Console.Error;

        /// <summary>
        /// The process-wide output writer, with one twist for <c>--parallel-files</c>:
        /// while a file runs inside a <see cref="MultiProgressReporter"/> per-file
        /// scope (entered on this async flow via <see cref="PushScopedOut"/>), the
        /// getter returns that file's private buffer instead, so the file's
        /// narrative (ctx.LogInfo, ProgressReporter headings, WARN) accumulates in
        /// its own block rather than interleaving with other files'. The setter
        /// always targets the real process writer; <c>Program</c> points it at its
        /// single <c>CommandStatusWriter</c>. The scoped redirect flows into the
        /// inner scoring <c>Parallel.For</c> (ExecutionContext capture), so a file's
        /// nested-thread output is attributed to that file.
        /// </summary>
        public static TextWriter Out
        {
            get { return _scopedOut.Value ?? _out; }
            set { _out = value; }
        }

        /// <summary>
        /// The real process writer, ignoring any per-file scoped redirect. The
        /// <see cref="MultiProgressReporter"/> renders its cross-file <c>[i] p%</c>
        /// aggregate line and flushes each completed file's buffered block here, so
        /// those bypass the per-file buffer (which would otherwise swallow the
        /// aggregate line into whichever file's block is active on the rendering thread).
        /// </summary>
        internal static TextWriter RealOut
        {
            get { return _out; }
        }

        // Per-async-flow output override for a MultiProgressReporter file scope.
        // Null on the common (sequential / single-file) paths, where Out is just
        // the process writer and ProgressReporter prints its percent lines inline.
        private static readonly AsyncLocal<TextWriter> _scopedOut = new AsyncLocal<TextWriter>();

        /// <summary>
        /// Redirect <see cref="Out"/> to <paramref name="writer"/> for the current
        /// async flow (and any work it spawns) until the returned cookie is
        /// disposed. Used by <see cref="MultiProgressReporter.BeginFile"/> to buffer
        /// one file's narrative; nesting restores the prior writer on dispose.
        /// </summary>
        internal static IDisposable PushScopedOut(TextWriter writer)
        {
            var previous = _scopedOut.Value;
            _scopedOut.Value = writer;
            return new ScopedOutCookie(previous);
        }

        private sealed class ScopedOutCookie : IDisposable
        {
            private readonly TextWriter _previous;
            private bool _disposed;

            public ScopedOutCookie(TextWriter previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _scopedOut.Value = _previous;
            }
        }

        /// <summary>
        /// When false (default), the gated machine tags (<see cref="LogTag.COUNT"/>,
        /// <see cref="LogTag.TIMING"/>, <see cref="LogTag.BENCH"/>, <see cref="LogTag.STAGE_WALL"/>,
        /// <see cref="LogTag.PATH"/>, <see cref="LogTag.TRAIN"/>) are not written, so the human log
        /// stays clean. The --perf-stats flag sets this true so the perf tools and the regression
        /// gate get the tagged lines.
        /// </summary>
        public static bool PerfStats { get; set; }

        /// <summary>
        /// When true (--verbose), implementer-grade detail that the default log hides is
        /// emitted (e.g. per-fold Percolator iterations). Use <see cref="WriteVerbose"/> to
        /// gate such lines.
        /// </summary>
        public static bool Verbose { get; set; }

        /// <summary>
        /// Write a line to <see cref="Out"/> only when <see cref="Verbose"/> is set. The
        /// inherited WriteLine(format, args) overload routes through the overridden
        /// WriteLine(string), so the stamps still apply.
        /// </summary>
        public static void WriteVerbose(string format, params object[] args)
        {
            if (Verbose)
                Out.WriteLine(format, args);
        }
    }
}
