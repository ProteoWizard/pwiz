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
using System.IO;
using System.Threading;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// The process-wide output sink for a command-line tool's logging that originates below
    /// its exe layer (library code that cannot see the exe's writer). The exe points
    /// <see cref="Out"/> at its single <see cref="CommandStatusWriter"/>, so every line picks
    /// up the <c>--timestamp</c> / <c>--memstamp</c> prefixes and <c>--log-file</c>
    /// redirection. Defaults to stderr for standalone and test use. Typed as the BCL
    /// <see cref="TextWriter"/> so a library needs nothing more to write a line.
    ///
    /// <para>One twist, for files processed in parallel: while a file runs inside a
    /// <see cref="MultiProgressReporter"/> per-file scope (entered on this async flow via
    /// <see cref="PushScopedOut"/>), the getter returns that file's private buffer instead,
    /// so the file's narrative (log lines, <see cref="ProgressReporter"/> headings, warnings)
    /// accumulates in its own block rather than interleaving with other files'. The setter
    /// always targets the real process writer. The scoped redirect flows into an inner
    /// <c>Parallel.For</c> (ExecutionContext capture), so a file's nested-thread output is
    /// attributed to that file.</para>
    ///
    /// <para>Moved here from Osprey, which keeps only its own flags on <c>OspreyOutput</c>.
    /// A stepping stone still: long operations should ultimately report through an
    /// <see cref="IProgressMonitor"/> rather than writing lines directly.</para>
    /// </summary>
    public static class CommandOutput
    {
        private static TextWriter _out = Console.Error;

        // Per-async-flow output override for a MultiProgressReporter file scope. Null on the
        // common (sequential / single-file) paths, where Out is just the process writer and
        // ProgressReporter prints its percent lines inline.
        private static readonly AsyncLocal<TextWriter> _scopedOut = new AsyncLocal<TextWriter>();

        /// <summary>
        /// The process-wide output writer, or the current file's buffer inside a
        /// <see cref="MultiProgressReporter"/> scope. The setter always sets the real writer.
        /// </summary>
        public static TextWriter Out
        {
            get { return _scopedOut.Value ?? _out; }
            set { _out = value; }
        }

        /// <summary>
        /// The real process writer, ignoring any per-file scoped redirect. The
        /// <see cref="MultiProgressReporter"/> renders its cross-file <c>[i] p%</c> aggregate
        /// line and flushes each completed file's buffered block here, so those bypass the
        /// per-file buffer (which would otherwise swallow the aggregate line into whichever
        /// file's block is active on the rendering thread).
        /// </summary>
        internal static TextWriter RealOut
        {
            get { return _out; }
        }

        /// <summary>
        /// Redirect <see cref="Out"/> to <paramref name="writer"/> for the current async flow
        /// (and any work it spawns) until the returned cookie is disposed. Used by
        /// <see cref="MultiProgressReporter.BeginFile"/> to buffer one file's narrative, and by
        /// tests to capture output without touching the process writer; nesting restores the
        /// prior writer on dispose.
        /// </summary>
        public static IDisposable PushScopedOut(TextWriter writer)
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
    }
}
