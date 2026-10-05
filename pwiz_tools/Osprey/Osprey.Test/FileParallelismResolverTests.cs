/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="FileParallelismResolver"/>, the single owner of the
    /// concurrent-file decision: precedence between the <c>--parallel-files</c>
    /// argument, the <c>OSPREY_MAX_PARALLEL_FILES</c> back-compat cap, free RAM,
    /// and the core count. System inputs (free RAM, footprint estimate) are
    /// injected so the policy is exercised deterministically with no real probing.
    /// </summary>
    [TestClass]
    public class FileParallelismResolverTests
    {
        private const long GB = 1024L * 1024L * 1024L;

        // Inject fixed system inputs so the policy is the only thing under test.
        private static int Resolve(FileParallelism request, int nFiles,
            int envCap = 0, int cores = 8, long availableBytes = 0, long perFileBytes = 0)
        {
            return FileParallelismResolver.Resolve(
                request, nFiles, envCap, cores,
                () => availableBytes, () => perFileBytes);
        }

        [TestMethod]
        public void TestFileParallelismResolution()
        {
            // A single file is always 1, regardless of the request.
            Assert.AreEqual(1, Resolve(FileParallelism.Auto, 1, cores: 32, availableBytes: 256 * GB, perFileBytes: GB));
            Assert.AreEqual(1, Resolve(FileParallelism.Explicit(8), 1));
            Assert.AreEqual(1, Resolve(FileParallelism.Sequential, 0));

            // Sequential default (argument absent, no env cap) = one at a time.
            Assert.AreEqual(1, Resolve(FileParallelism.Sequential, 3));

            // OSPREY_MAX_PARALLEL_FILES back-compat cap applies ONLY when the
            // argument is absent (Sequential request). =1 stays sequential;
            // >1 caps, clamped to the file count.
            Assert.AreEqual(1, Resolve(FileParallelism.Sequential, 5, envCap: 1));
            Assert.AreEqual(4, Resolve(FileParallelism.Sequential, 10, envCap: 4));
            Assert.AreEqual(3, Resolve(FileParallelism.Sequential, 3, envCap: 4)); // clamp to nFiles

            // Explicit N wins regardless of RAM/cores/env -- clamped only to the
            // file count (a bigger box can force more than this machine fits).
            Assert.AreEqual(8, Resolve(FileParallelism.Explicit(8), 10, cores: 4, availableBytes: GB, perFileBytes: 100 * GB));
            Assert.AreEqual(3, Resolve(FileParallelism.Explicit(8), 3)); // clamp to nFiles
            Assert.AreEqual(2, Resolve(FileParallelism.Explicit(2), 5, envCap: 1)); // env cap ignored when arg present
            Assert.AreEqual(1, Resolve(FileParallelism.Explicit(0), 5)); // floored at 1

            // Auto, RAM-bound: budget = 80% of free RAM; N = budget / per-file,
            // then capped by cores and file count.
            //   51.2 GB budget / 18 GB per file -> 2, capped to min(3 files, 32 cores).
            Assert.AreEqual(2, Resolve(FileParallelism.Auto, 3, cores: 32, availableBytes: 64 * GB, perFileBytes: 18 * GB));
            // Plenty of RAM -> CPU/file cap dominates.
            Assert.AreEqual(3, Resolve(FileParallelism.Auto, 3, cores: 32, availableBytes: 512 * GB, perFileBytes: 6 * GB));
            // Core-bound even with plenty of RAM.
            Assert.AreEqual(2, Resolve(FileParallelism.Auto, 8, cores: 2, availableBytes: 512 * GB, perFileBytes: GB));
            // Tight RAM still yields at least 1 (never 0).
            Assert.AreEqual(1, Resolve(FileParallelism.Auto, 4, cores: 16, availableBytes: 4 * GB, perFileBytes: 30 * GB));

            // Auto with no usable memory signal falls back to the CPU/file cap
            // (still bounded, unlike the old unbounded default).
            Assert.AreEqual(3, Resolve(FileParallelism.Auto, 3, cores: 8, availableBytes: 0, perFileBytes: 6 * GB));
            Assert.AreEqual(4, Resolve(FileParallelism.Auto, 4, cores: 8, availableBytes: 64 * GB, perFileBytes: 0));
            Assert.AreEqual(8, Resolve(FileParallelism.Auto, 10, cores: 8, availableBytes: 0, perFileBytes: 0));

            // Auto ignores the env cap entirely (the argument wins when both set).
            Assert.AreEqual(3, Resolve(FileParallelism.Auto, 3, envCap: 1, cores: 8, availableBytes: 512 * GB, perFileBytes: GB));

            // Footprint estimate: null / empty / unreadable paths -> 0 (unknown),
            // which routes auto mode to the CPU/file cap rather than throwing.
            Assert.AreEqual(0, FileParallelismResolver.EstimatePerFileBytes(null));
            Assert.AreEqual(0, FileParallelismResolver.EstimatePerFileBytes(new string[0]));
            Assert.AreEqual(0, FileParallelismResolver.EstimatePerFileBytes(
                new[] { @"C:\does\not\exist\a.mzML", @"C:\does\not\exist\b.mzML" }));
        }

        /// <summary>
        /// <see cref="FdrLaneResolver"/>: first-pass FDR lanes are the smallest of threads / 2,
        /// the memory fit for the largest file, the maximum, and the file count - never
        /// <c>--parallel-files</c> - and the override wins over all of them.
        /// </summary>
        [TestMethod]
        public void TestFdrLaneResolution()
        {
            const long chsRows = 3080000;   // CHS rows per file: ~4.6 GB per lane
            const long seaAdRows = 4200000;

            // A single file is always one lane, even when forced.
            Assert.AreEqual(1, ResolveLanes(1, 32, chsRows, 256 * GB));
            Assert.AreEqual(1, ResolveLanes(1, 32, chsRows, 256 * GB, overrideLanes: 4));

            // The 64 GB i9 at 446 CHS files: 50 GB free x 0.5 / ~4.6 GB -> 5 by memory, under
            // 30 threads / 2 = 15 and the maximum.
            Assert.AreEqual(5, ResolveLanes(446, 30, chsRows, 50 * GB, expectLimit: OspreyCoreResources.FdrLaneResolver_Resolve_limit_memory));
            // The 72-core, 512 GB box: neither threads nor memory bind, the maximum does.
            Assert.AreEqual(FdrLaneResolver.MAX_LANES, ResolveLanes(82, 72, seaAdRows, 400 * GB, expectLimit: OspreyCoreResources.FdrLaneResolver_Resolve_limit_maximum));
            // A small machine with plenty of memory: threads / 2.
            Assert.AreEqual(2, ResolveLanes(10, 4, chsRows, 256 * GB, expectLimit: OspreyCoreResources.FdrLaneResolver_Resolve_limit_threads));
            Assert.AreEqual(1, ResolveLanes(10, 1, chsRows, 256 * GB));
            // Fewer files than any other limit: one lane per file.
            Assert.AreEqual(3, ResolveLanes(3, 32, chsRows, 256 * GB, expectLimit: OspreyCoreResources.FdrLaneResolver_Resolve_limit_files));
            // Memory too tight for even one lane still gives one.
            Assert.AreEqual(1, ResolveLanes(446, 30, chsRows, 4 * GB));

            // Zero free is exhausted memory - a cgroup or heap limit at its ceiling - not an
            // unknown: one lane, however many threads. No rows leaves memory out of it.
            Assert.AreEqual(1, ResolveLanes(446, 12, chsRows, 0, expectLimit: OspreyCoreResources.FdrLaneResolver_Resolve_limit_memory));
            Assert.AreEqual(FdrLaneResolver.MAX_LANES, ResolveLanes(446, 30, 0, 50 * GB));

            // The override wins over threads and memory, clamped only to the file count.
            Assert.AreEqual(4, ResolveLanes(446, 2, chsRows, 4 * GB, overrideLanes: 4));
            Assert.AreEqual(12, ResolveLanes(446, 30, chsRows, 50 * GB, overrideLanes: 12));
            Assert.AreEqual(3, ResolveLanes(3, 30, chsRows, 50 * GB, overrideLanes: 12));
        }

        // Resolve with a captured log; when expectLimit is given, the one line must be the
        // decision line naming it as the limit.
        private static int ResolveLanes(int nFiles, int nThreads, long maxRowsPerFile, long availableBytes,
            int overrideLanes = 0, string expectLimit = null)
        {
            string logged = null;
            int lanes = FdrLaneResolver.Resolve(nFiles, nThreads, maxRowsPerFile, availableBytes, overrideLanes,
                line => logged = line);
            if (expectLimit != null)
            {
                string expected = string.Format(
                    OspreyCoreResources.FdrLaneResolver_Resolve_First_pass_FDR_file_lanes___0___limited_by__1____2__threads___3__GB_free___4__GB_per_lane___5__files_,
                    lanes, expectLimit, nThreads, availableBytes / (double)GB,
                    maxRowsPerFile * FdrLaneResolver.BYTES_PER_ROW_PER_LANE / (double)GB, nFiles);
                Assert.AreEqual(expected, logged);
            }
            return lanes;
        }
    }
}
