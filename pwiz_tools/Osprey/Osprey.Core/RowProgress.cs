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
using System.Threading;
using pwiz.Common.SystemUtil;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Counts rows toward one <see cref="ProgressReporter"/> step from inside the reads that
    /// produce them, for a step whose unit of work - one file on a lane - runs far longer than
    /// the reporter's heartbeat. A reporter can only print when something calls
    /// <see cref="ProgressReporter.Report"/>, so a step that reports once per finished file is
    /// silent for as long as a file takes: on 82 SEA-AD files on 7 lanes that was 30-136 s
    /// at a time, in all three of first-pass FDR's per-file steps.
    ///
    /// <para>The lanes share one counter. A lane makes it the ambient counter only for the
    /// duration of one read (<see cref="Counting{T}"/>), and a reader reports each block of
    /// rows it finishes through <see cref="Report"/> - for a parquet reader, one call per row
    /// group. The ambient route is the one <see cref="MultiProgressReporter"/> takes, for the
    /// same reason: the readers sit below delegates that many callers and tests pass, and
    /// none of them needs a progress parameter. A read outside any counting scope pays one
    /// null test per row group.</para>
    ///
    /// <para>The count only feeds the log, so nothing a run writes depends on it.</para>
    /// </summary>
    public sealed class RowProgress
    {
        private static readonly AsyncLocal<RowProgress> _current = new AsyncLocal<RowProgress>();

        /// <summary>
        /// Adds <paramref name="rows"/> to the counter of the enclosing <see cref="Counting{T}"/>
        /// scope, if any. Called by readers, once per block of rows.
        /// </summary>
        public static void Report(int rows)
        {
            _current.Value?.Add(rows);
        }

        private readonly ProgressReporter _reporter;
        private readonly int _countsPerRow;
        private readonly object _lock = new object();
        private long _counts;

        /// <summary>
        /// A counter feeding <paramref name="reporter"/>, whose total is the step's row count.
        /// A step that passes over each row more than once - read, loaded, scored - counts it
        /// <paramref name="countsPerRow"/> times, and the counter reports rows, so the
        /// reporter's heartbeat line shows a row count that matches the step's heading.
        /// </summary>
        public RowProgress(ProgressReporter reporter, int countsPerRow = 1)
        {
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
            if (countsPerRow < 1)
                throw new ArgumentOutOfRangeException(nameof(countsPerRow));
            _countsPerRow = countsPerRow;
        }

        /// <summary>
        /// Adds <paramref name="rows"/> directly, for work that is not a read - scoring a file's
        /// rows once they are loaded. Safe from any lane: the add and the report happen under
        /// one lock, so the reporter never sees the count go backwards.
        /// </summary>
        public void Add(long rows)
        {
            lock (_lock)
            {
                _counts += rows;
                _reporter.Report(_counts / _countsPerRow);
            }
        }

        /// <summary>
        /// Runs <paramref name="read"/> with this as the ambient counter, so the rows it reads
        /// are counted as they are read, and restores the previous counter afterwards.
        /// </summary>
        public T Counting<T>(Func<T> read)
        {
            var previous = _current.Value;
            _current.Value = this;
            try
            {
                return read();
            }
            finally
            {
                _current.Value = previous;
            }
        }
    }
}
