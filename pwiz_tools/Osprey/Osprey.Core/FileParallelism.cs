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
using System.IO;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// How the user requested outer (across-files) parallelism, from the
    /// <c>--parallel-files</c> CLI argument. Distinct from <c>--threads</c>,
    /// which is the INNER per-file main-search thread budget.
    /// </summary>
    public enum FileParallelismMode
    {
        /// <summary>Argument absent: process input files one at a time (the default).</summary>
        Sequential = 0,

        /// <summary><c>--parallel-files</c> with no value: pick N from free RAM and cores.</summary>
        Auto,

        /// <summary><c>--parallel-files N</c>: exactly N concurrent files (clamped to file count).</summary>
        Explicit
    }

    /// <summary>
    /// The parsed <c>--parallel-files</c> request. A value type whose default
    /// (<c>default(FileParallelism)</c>) is <see cref="FileParallelismMode.Sequential"/>,
    /// so an un-set <see cref="OspreyConfig.FileParallelism"/> means "one file at
    /// a time" with no extra wiring. The effective concurrent-file count is
    /// resolved at run time by <see cref="FileParallelismResolver"/>, which folds
    /// in file count, cores, free RAM, and the legacy
    /// <c>OSPREY_MAX_PARALLEL_FILES</c> cap.
    /// </summary>
    public readonly struct FileParallelism
    {
        private FileParallelism(FileParallelismMode mode, int count)
        {
            Mode = mode;
            Count = count;
        }

        public FileParallelismMode Mode { get; }

        /// <summary>Explicit file count; only meaningful when <see cref="Mode"/> is
        /// <see cref="FileParallelismMode.Explicit"/> (0 otherwise).</summary>
        public int Count { get; }

        /// <summary>Argument absent: one file at a time.</summary>
        public static readonly FileParallelism Sequential = new FileParallelism(FileParallelismMode.Sequential, 0);

        /// <summary><c>--parallel-files</c> with no value: RAM/CPU-aware auto.</summary>
        public static readonly FileParallelism Auto = new FileParallelism(FileParallelismMode.Auto, 0);

        /// <summary><c>--parallel-files N</c>: an explicit positive count.</summary>
        public static FileParallelism Explicit(int count)
        {
            return new FileParallelism(FileParallelismMode.Explicit, count);
        }
    }

    /// <summary>
    /// The per-file stages whose concurrent-file count can be set on its own, with
    /// <c>--parallel-files-caching</c>, <c>--parallel-files-scoring</c> and
    /// <c>--parallel-files-rescoring</c>; each falls back to the shared <c>--parallel-files</c>.
    /// They do not scale alike: caching is bound by a single-threaded vendor decode, scoring
    /// by CPU and its large per-file working set, re-scoring by a much smaller one.
    /// </summary>
    public enum FileStage
    {
        Caching,
        Scoring,
        Rescoring
    }

    /// <summary>
    /// Resolves the parsed <see cref="FileParallelism"/> request into the actual
    /// number of input files one per-file stage runs concurrently -- the single
    /// place that owns the precedence between the CLI argument, the
    /// <c>OSPREY_MAX_PARALLEL_FILES</c> back-compat cap, free RAM, and the core
    /// count. Each <see cref="FileStage"/> calls it once, with the request
    /// <see cref="OspreyConfig.GetFileParallelism"/> chose for it: the stage's own
    /// flag when given, otherwise the shared <c>--parallel-files</c>.
    ///
    /// Precedence (highest first):
    ///   1. explicit <c>--parallel-files N</c>  -> N, clamped to file count only
    ///                                             (a 500 GB box can force more).
    ///   2. <c>--parallel-files</c> (auto)      -> RAM/CPU-aware estimate.
    ///   3. <c>OSPREY_MAX_PARALLEL_FILES</c>    -> legacy cap (only when the
    ///                                             argument is absent).
    ///   4. otherwise                           -> 1 (sequential default).
    /// The argument wins over the env var when both are set.
    /// </summary>
    public static class FileParallelismResolver
    {
        // Per-file peak working-set estimate = largest input x this factor.
        // Grounded in the 2026-06-11 Astral (hram) observation: a ~6 GB on-disk
        // mzML drove a ~14.6 GB per-file working set (~2.4x), rounded up to bias
        // AUTO toward FEWER concurrent files (over-estimating footprint is the
        // safe error - it avoids the OOM this argument exists to prevent).
        // It also scales a .spectra.bin size when a cache-only input is sized
        // from its cache (EstimatePerFileBytes); a cache-only run never pays the
        // source parse this was calibrated against, so recalibrating from mzML
        // parses keeps that case conservative too.
        // Coarse by design; explicit --parallel-files N bypasses it entirely.
        private const double FOOTPRINT_MULTIPLIER = 3.0;

        // Only commit this fraction of free RAM to concurrent files, leaving
        // headroom for the shared library, GC slack, and OS cache.
        private const double RAM_BUDGET_FRACTION = 0.8;

        /// <summary>
        /// Resolve the effective concurrent-file count. <paramref name="availableBytesProbe"/>
        /// and <paramref name="perFileBytesEstimate"/> are only invoked in auto
        /// mode, so the common (sequential / explicit) paths do no I/O or system
        /// probing. <paramref name="log"/> (optional) receives a one-line summary
        /// of the chosen N and the reason; pass null on the bookkeeping-only paths
        /// that never actually parallelize. <paramref name="argName"/> is the argument the
        /// request came from - a stage's own flag or the shared <c>--parallel-files</c> - so
        /// the summary says which one decided.
        /// </summary>
        public static int Resolve(
            FileParallelism request, int nFiles, int envCap, int processorCount,
            Func<long> availableBytesProbe, Func<long> perFileBytesEstimate,
            Action<string> log = null, string argName = OspreyArgNames.PARALLEL_FILES)
        {
            string argText = OspreyArgNames.Text(argName);
            if (nFiles <= 1)
                return 1;

            int cores = Math.Max(1, processorCount);
            int cpuCap = Math.Min(nFiles, cores);

            switch (request.Mode)
            {
                case FileParallelismMode.Explicit:
                    // The argument wins: honor N regardless of RAM/cores, clamped
                    // only to the file count (more would idle). A box with more
                    // RAM than this machine reports can force the value it knows
                    // is safe.
                    int explicitN = Math.Max(1, Math.Min(request.Count, nFiles));
                    log?.Invoke(string.Format(
                        OspreyCoreResources.FileParallelismResolver_Resolve_File_parallelism___0___explicit___parallel_files___1__files_,
                        explicitN, nFiles, argText));
                    return explicitN;

                case FileParallelismMode.Auto:
                    return ResolveAuto(nFiles, cores, cpuCap,
                        availableBytesProbe, perFileBytesEstimate, log, argText);

                default:
                    // Sequential default -- unless the legacy env cap is set, in
                    // which case honor it as a back-compat cap (arg absent here).
                    if (envCap == 1)
                    {
                        log?.Invoke(@"File parallelism: 1 (OSPREY_MAX_PARALLEL_FILES=1)");
                        return 1;
                    }
                    if (envCap > 1)
                    {
                        int capped = Math.Min(envCap, nFiles);
                        log?.Invoke(string.Format(
                            @"File parallelism: {0} (OSPREY_MAX_PARALLEL_FILES={1} back-compat cap, {2} files)",
                            capped, envCap, nFiles));
                        return capped;
                    }
                    log?.Invoke(string.Format(
                        OspreyCoreResources.FileParallelismResolver_Resolve_File_parallelism__1__sequential_default__pass__1__to_run__0__files_at_once_,
                        nFiles, argText));
                    return 1;
            }
        }

        /// <summary>
        /// Largest input footprint estimate in bytes (max input size x
        /// <see cref="FOOTPRINT_MULTIPLIER"/>), or 0 when no size can be read. Uses
        /// the max rather than the mean because the concurrent peak is bounded by
        /// the biggest files running together.
        ///
        /// An input that measures 0 - its source deleted once cached (pwiz #4616),
        /// or truncated - is sized from the <c>.spectra.bin</c> that
        /// <paramref name="cachePathResolver"/> locates for it (null skips this, for
        /// callers with no caches). Without that, a staged cohort sizes 0 throughout
        /// and auto mode loses its memory budget on exactly the largest runs.
        /// </summary>
        public static long EstimatePerFileBytes(IEnumerable<string> inputFiles,
            Func<string, string> cachePathResolver)
        {
            long maxBytes = 0;
            if (inputFiles != null)
            {
                foreach (var file in inputFiles)
                {
                    long len = SafeFileLength(file);
                    if (len == 0 && cachePathResolver != null)
                        len = SafeFileLength(SafeResolve(cachePathResolver, file));
                    if (len > maxBytes)
                        maxBytes = len;
                }
            }
            if (maxBytes <= 0)
                return 0;
            return (long)(maxBytes * FOOTPRINT_MULTIPLIER);
        }

        private static int ResolveAuto(
            int nFiles, int cores, int cpuCap,
            Func<long> availableBytesProbe, Func<long> perFileBytesEstimate,
            Action<string> log, string argText)
        {
            long availableBytes = availableBytesProbe?.Invoke() ?? 0;
            long perFileBytes = perFileBytesEstimate?.Invoke() ?? 0;

            if (perFileBytes <= 0)
            {
                // No per-file estimate - fall back to a CPU-bound cap rather than
                // guessing. Still safer than the old unbounded default.
                log?.Invoke(string.Format(
                    OspreyCoreResources.FileParallelismResolver_ResolveAuto_File_parallelism___0___auto__CPU_bound___1__cores___2__files__memory_estimate_unavailable_,
                    cpuCap, cores, nFiles, argText));
                return cpuCap;
            }

            // Zero free memory is exhausted (a cgroup or heap limit at its ceiling),
            // or unreadable; either way the budget below comes to one file at a time,
            // as FdrLaneResolver reads it, never the CPU cap.
            long budget = (long)(Math.Max(0, availableBytes) * RAM_BUDGET_FRACTION);
            int memFit = (int)Math.Max(1, Math.Min(int.MaxValue, budget / perFileBytes));
            int chosen = Math.Max(1, Math.Min(cpuCap, memFit));
            log?.Invoke(string.Format(
                OspreyCoreResources.FileParallelismResolver_ResolveAuto_File_parallelism___0___auto___1__GB_free_x__2______3__GB_est_per_file_____4__by_RAM__,
                chosen, availableBytes / (double)BYTES_PER_GB, RAM_BUDGET_FRACTION,
                perFileBytes / (double)BYTES_PER_GB, memFit, cores, nFiles, argText));
            return chosen;
        }

        private static long SafeFileLength(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return 0;
                if (File.Exists(path))
                    return TargetLength(path);
                // A vendor bundle is a DIRECTORY (Agilent .d, Bruker .d, Waters .raw).
                // Sizing it at 0 does not merely lose precision: EstimatePerFileBytes
                // returns 0 for the whole set, ResolveAuto takes its no-signal branch
                // and runs at min(file count, cores) with NO memory budget at all.
                // That is the opposite of conservative on exactly the largest inputs.
                var dir = new DirectoryInfo(path);
                if (!dir.Exists)
                    return 0;
                long total = 0;
                foreach (var f in dir.EnumerateFiles(@"*", SearchOption.AllDirectories))
                    total += f.Length;
                return total;
            }
            catch (Exception)
            {
                // Unreadable path - treat as unknown size (0), never throw from a
                // sizing hint.
                return 0;
            }
        }

        // Through a handle, so a symbolic link reports the size of its target:
        // FileInfo.Length measures the link itself (0 on Windows, the length of the
        // stored target path on Unix). A dangling link throws, which reads as 0.
        private static long TargetLength(string path)
        {
            using (var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            {
                return RandomAccess.GetLength(handle);
            }
        }

        private static string SafeResolve(Func<string, string> cachePathResolver, string inputPath)
        {
            try
            {
                return cachePathResolver(inputPath);
            }
            catch (Exception)
            {
                // A sizing hint never throws; an unresolvable cache is an unknown size.
                return null;
            }
        }

        private const long BYTES_PER_GB = 1024L * 1024L * 1024L;
    }
}
