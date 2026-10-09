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

using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Process-wide totals for the block reads of <see cref="OspreyEnvironment.BlockReadMb"/>:
    /// disk reads, bytes, time reading and time waiting for the gate, summed over threads.
    /// Here rather than beside the stream so every project can report them per phase.
    /// </summary>
    public static class BlockReadStats
    {
        private static long _reads;
        private static long _bytes;
        private static long _readTicks;
        private static long _gateWaitTicks;
        private static long _parquetBytes;

        /// <summary>
        /// Records one disk read of <paramref name="bytes"/> that waited
        /// <paramref name="gateWaitTicks"/> for the gate and then took <paramref name="readTicks"/>.
        /// </summary>
        public static void Add(long bytes, long readTicks, long gateWaitTicks, bool parquet)
        {
            if (parquet)
                Interlocked.Add(ref _parquetBytes, bytes);
            Interlocked.Increment(ref _reads);
            Interlocked.Add(ref _bytes, bytes);
            Interlocked.Add(ref _readTicks, readTicks);
            Interlocked.Add(ref _gateWaitTicks, gateWaitTicks);
        }

        /// <summary>
        /// The totals as one log fragment, or null when block reads are off.
        /// </summary>
        public static string Text()
        {
            if (OspreyEnvironment.BlockReadMb <= 0)
                return null;
            return string.Format(CultureInfo.InvariantCulture,
                @"block reads (block {0} MB, gate {1}) so far: {2:N0} reads, {3:F1} GB ({6:F1} GB parquet), read {4:F1}s, gate wait {5:F1}s",
                OspreyEnvironment.BlockReadMb, OspreyEnvironment.BlockReadGate ? @"on" : @"off",
                Interlocked.Read(ref _reads), Interlocked.Read(ref _bytes) / 1e9,
                Interlocked.Read(ref _readTicks) / (double)Stopwatch.Frequency,
                Interlocked.Read(ref _gateWaitTicks) / (double)Stopwatch.Frequency,
                Interlocked.Read(ref _parquetBytes) / 1e9);
        }
    }
}
