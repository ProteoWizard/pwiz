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

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

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

            // The decision line names the argument that set the request - a stage's own flag
            // when it was given - and the shared --parallel-files by default.
            AssertDecisionNames(OspreyArgNames.PARALLEL_FILES_CACHING);
            AssertDecisionNames(null);

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

            // Auto with no per-file estimate falls back to the CPU/file cap
            // (still bounded, unlike the old unbounded default).
            Assert.AreEqual(4, Resolve(FileParallelism.Auto, 4, cores: 8, availableBytes: 64 * GB, perFileBytes: 0));
            Assert.AreEqual(8, Resolve(FileParallelism.Auto, 10, cores: 8, availableBytes: 0, perFileBytes: 0));
            // Zero free memory with a known estimate is exhausted memory (a cgroup or
            // heap limit at its ceiling), not a missing signal: one file at a time.
            Assert.AreEqual(1, Resolve(FileParallelism.Auto, 3, cores: 8, availableBytes: 0, perFileBytes: 6 * GB));
            // A tiny estimate against a large budget must not overflow the RAM fit.
            Assert.AreEqual(8, Resolve(FileParallelism.Auto, 10, cores: 8, availableBytes: 512 * GB, perFileBytes: 1));

            // Auto ignores the env cap entirely (the argument wins when both set).
            Assert.AreEqual(3, Resolve(FileParallelism.Auto, 3, envCap: 1, cores: 8, availableBytes: 512 * GB, perFileBytes: GB));

            // Footprint estimate: null / empty / unreadable paths -> 0 (unknown),
            // which routes auto mode to the CPU/file cap rather than throwing.
            Assert.AreEqual(0, FileParallelismResolver.EstimatePerFileBytes(null, null));
            Assert.AreEqual(0, FileParallelismResolver.EstimatePerFileBytes(new string[0], null));
            Assert.AreEqual(0, FileParallelismResolver.EstimatePerFileBytes(
                new[] { @"C:\does\not\exist\a.mzML", @"C:\does\not\exist\b.mzML" }, null));

            AssertCacheOnlySizing();
        }

        /// <summary>
        /// Every decision line - explicit, sequential default, and both auto forms - names
        /// <paramref name="argName"/>, or <c>--parallel-files</c> when it is null (the default).
        /// </summary>
        private static void AssertDecisionNames(string argName)
        {
            string argText = OspreyArgNames.Text(argName ?? OspreyArgNames.PARALLEL_FILES);
            var requests = new[]
            {
                (FileParallelism.Explicit(2), 0L),
                (FileParallelism.Sequential, 0L),
                (FileParallelism.Auto, 0L),
                (FileParallelism.Auto, GB)
            };
            foreach (var (request, perFileBytes) in requests)
            {
                string logged = null;
                Action<string> log = line => logged = line;
                if (argName == null)
                    FileParallelismResolver.Resolve(request, 5, 0, 8, () => 64 * GB, () => perFileBytes, log);
                else
                    FileParallelismResolver.Resolve(request, 5, 0, 8, () => 64 * GB, () => perFileBytes, log, argName);
                Assert.IsNotNull(logged, request.Mode.ToString());
                StringAssert.Contains(logged, argText);
                // The shared flag's text is a prefix of every stage flag's, so the default case
                // must also name none of them.
                if (argName == null)
                {
                    foreach (var stageArg in new[] { OspreyArgNames.PARALLEL_FILES_CACHING,
                                 OspreyArgNames.PARALLEL_FILES_SCORING, OspreyArgNames.PARALLEL_FILES_RESCORING })
                    {
                        Assert.IsFalse(logged.Contains(OspreyArgNames.Text(stageArg)), logged);
                    }
                }
            }
        }

        /// <summary>
        /// Inputs whose source is gone or empty but whose <c>.spectra.bin</c> exists are
        /// sized from the cache through the production wiring
        /// (<see cref="PerFileScoringTask.EstimateInputBytes"/>), both beside the data and
        /// in a separate cache directory, and auto mode then budgets RAM instead of taking
        /// the CPU/file cap.
        /// </summary>
        private static void AssertCacheOnlySizing()
        {
            const int cacheBytes = 4096;
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_fp_" + Path.GetRandomFileName());
            string cacheDir = Path.Combine(dir, @"cache");
            Directory.CreateDirectory(cacheDir);
            try
            {
                // run1's source was deleted after caching; run2's was truncated to 0 bytes.
                string deleted = Path.Combine(dir, @"run1.mzML");
                string truncated = Path.Combine(dir, @"run2.mzML");
                File.WriteAllBytes(truncated, new byte[0]);
                var inputs = new[] { deleted, truncated };
                string cache1 = WriteCache(dir, deleted, cacheBytes);
                WriteCache(dir, truncated, cacheBytes);

                // Without a resolver both size 0, and so does the set.
                Assert.AreEqual(0L, FileParallelismResolver.EstimatePerFileBytes(inputs, null));

                // Production wiring, caches beside the data: a cache-only input sizes
                // exactly like its cache, multiplier applied.
                long cacheEstimate = FileParallelismResolver.EstimatePerFileBytes(new[] { cache1 }, null);
                Assert.IsTrue(cacheEstimate > cacheBytes);
                Assert.AreEqual(cacheEstimate, PerFileScoringTask.EstimateInputBytes(inputs));

                // Auto mode budgets RAM from it: free memory below one file's estimate
                // gives 1, where an unknown estimate would take the CPU/file cap (2).
                Assert.AreEqual(1, FileParallelismResolver.Resolve(FileParallelism.Auto, inputs.Length, 0, 8,
                    () => cacheEstimate / 2, () => PerFileScoringTask.EstimateInputBytes(inputs)));

                // A separate --cache-dir is honored, and an empty one sizes to the unknown 0.
                string emptyCacheDir = Path.Combine(dir, @"empty");
                Directory.CreateDirectory(emptyCacheDir);
                WriteCache(cacheDir, deleted, cacheBytes);
                ArtifactPathsTest.WithArtifactDirs(null, cacheDir, () =>
                    Assert.AreEqual(cacheEstimate, PerFileScoringTask.EstimateInputBytes(new[] { deleted })));
                ArtifactPathsTest.WithArtifactDirs(null, emptyCacheDir, () =>
                    Assert.AreEqual(0L, PerFileScoringTask.EstimateInputBytes(new[] { deleted })));

                // A resolver that finds nothing, or throws, reports the unknown 0 rather
                // than inventing a size or failing the run.
                Assert.AreEqual(0L, FileParallelismResolver.EstimatePerFileBytes(
                    inputs, p => Path.Combine(dir, @"absent.spectra.bin")));
                Assert.AreEqual(0L, FileParallelismResolver.EstimatePerFileBytes(
                    inputs, p => throw new IOException()));
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                    // A scanner holding a just-written file must not mask the assertion.
                }
            }
        }

        private static string WriteCache(string dir, string inputPath, int bytes)
        {
            string cachePath = Path.Combine(dir, Path.GetFileNameWithoutExtension(inputPath) + SpectraCache.EXT);
            File.WriteAllBytes(cachePath, new byte[bytes]);
            return cachePath;
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
