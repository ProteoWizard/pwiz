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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Chooses how many files first-pass FDR (and the Stage 6 planning inside it) works on at
    /// once. Its per-file phases run on <see cref="OrderedFileLanes"/>, which applies every
    /// cross-file effect in file order, so the count changes the wall clock and the memory
    /// peak and nothing else.
    ///
    /// <para>Deliberately NOT <c>--parallel-files</c>. That argument bounds the PerFile* tasks,
    /// whose working set is ~10-15 GB per file; a lane here holds a few GB, and this is the
    /// stage where the experiment-wide state peaks, so the two need different limits. The
    /// count is the smallest of:</para>
    /// <list type="bullet">
    /// <item>threads: <c>--threads</c> / <see cref="THREADS_PER_LANE"/>. A lane is mostly one
    /// thread decoding and scoring one file; the rest of the budget goes to the in-order
    /// consumer and the parallel sorts inside a file.</item>
    /// <item>memory: free physical memory x <see cref="RAM_BUDGET_FRACTION"/> over the bytes
    /// one lane holds for the cohort's largest file.</item>
    /// <item><see cref="MAX_LANES"/>, and the file count.</item>
    /// </list>
    /// <para><c>OSPREY_FDR_FILE_LANES</c> overrides all of it, for measuring.</para>
    /// </summary>
    public static class FdrLaneResolver
    {
        /// <summary>
        /// Upper bound on lanes. The largest count measured: on NVMe 6 lanes was still 15%
        /// faster than 3 (CHS, 128 files), while on one HDD the knee was 3 (4 lanes 1.6%
        /// faster at 446 files, behind the process-wide disk gate). Beyond 8 is unmeasured.
        /// </summary>
        public const int MAX_LANES = 8;

        /// <summary>
        /// Bytes one lane adds to the peak, per row of the largest file. Measured on CHS
        /// (~3.08 M rows per file) as the private-bytes rise per extra lane: 2.9 GB with the
        /// disk gate on HDD and 4.6 GB without it on NVMe, i.e. ~950-1,500 B per row. Private
        /// bytes include uncollected garbage, which is the safe direction for a cap; rounded
        /// up again here so the error is toward fewer lanes.
        /// </summary>
        public const long BYTES_PER_ROW_PER_LANE = 1600;

        /// <summary>
        /// Threads of <c>--threads</c> per lane.
        /// </summary>
        public const int THREADS_PER_LANE = 2;

        /// <summary>
        /// Share of the free memory measured on entry that lanes may take. Half, not the 0.8
        /// <see cref="FileParallelismResolver"/> commits: free memory is measured before the
        /// first pass grows its experiment-wide state, which at 446 CHS files is still
        /// ~20 GB to come and is where the stage peaks regardless of the lane count.
        /// </summary>
        public const double RAM_BUDGET_FRACTION = 0.5;

        /// <summary>
        /// Resolve the lane count. <paramref name="availableBytes"/> of 0 means unknown, and
        /// then memory does not limit the count; <paramref name="maxRowsPerFile"/> of 0 (no
        /// rows) likewise. <paramref name="overrideLanes"/> &gt; 0 wins, clamped to the file
        /// count. <paramref name="log"/> (optional) receives one line naming the count and
        /// what limited it.
        /// </summary>
        public static int Resolve(int nFiles, int nThreads, long maxRowsPerFile, long availableBytes,
            int overrideLanes, Action<string> log = null)
        {
            if (nFiles <= 1)
                return 1;

            if (overrideLanes > 0)
            {
                int forced = Math.Min(overrideLanes, nFiles);
                log?.Invoke(string.Format(@"First-pass FDR file lanes: {0} (OSPREY_FDR_FILE_LANES={1}, {2} files)",
                    forced, overrideLanes, nFiles));
                return forced;
            }

            int threadCap = Math.Max(1, nThreads / THREADS_PER_LANE);
            long bytesPerLane = maxRowsPerFile * BYTES_PER_ROW_PER_LANE;
            int memoryCap = int.MaxValue;
            if (availableBytes > 0 && bytesPerLane > 0)
            {
                long budget = (long)(availableBytes * RAM_BUDGET_FRACTION);
                memoryCap = (int)Math.Max(1, Math.Min(int.MaxValue, budget / bytesPerLane));
            }

            int lanes = Math.Min(Math.Min(threadCap, memoryCap), Math.Min(MAX_LANES, nFiles));
            if (log != null)
            {
                string limit;
                if (lanes == memoryCap && memoryCap < threadCap)
                    limit = OspreyCoreResources.FdrLaneResolver_Resolve_limit_memory;
                else if (lanes == threadCap)
                    limit = OspreyCoreResources.FdrLaneResolver_Resolve_limit_threads;
                else if (lanes == nFiles)
                    limit = OspreyCoreResources.FdrLaneResolver_Resolve_limit_files;
                else
                    limit = OspreyCoreResources.FdrLaneResolver_Resolve_limit_maximum;
                log(string.Format(
                    OspreyCoreResources.FdrLaneResolver_Resolve_First_pass_FDR_file_lanes___0___limited_by__1____2__threads___3__GB_free___4__GB_per_lane___5__files_,
                    lanes, limit, nThreads, availableBytes / BYTES_PER_GB, bytesPerLane / BYTES_PER_GB, nFiles));
            }
            return lanes;
        }

        private const double BYTES_PER_GB = 1024.0 * 1024.0 * 1024.0;
    }
}
