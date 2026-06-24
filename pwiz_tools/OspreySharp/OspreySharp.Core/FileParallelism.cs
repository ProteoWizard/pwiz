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

namespace pwiz.OspreySharp.Core
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
    /// Resolves the parsed <see cref="FileParallelism"/> request into the actual
    /// number of input files to score concurrently for one run -- the single
    /// place that owns the precedence between the CLI argument, the
    /// <c>OSPREY_MAX_PARALLEL_FILES</c> back-compat cap, free RAM, and the core
    /// count. <c>PerFileScoringTask</c> calls it once per invocation and stores
    /// the result on <c>RunPlan.EffectiveFileParallelism</c>.
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
        // Per-file peak working-set estimate = largest input mzML x this factor.
        // Grounded in the 2026-06-11 Astral (hram) observation: a ~6 GB on-disk
        // mzML drove a ~14.6 GB per-file working set (~2.4x), rounded up to bias
        // AUTO toward FEWER concurrent files (over-estimating footprint is the
        // safe error -- it avoids the OOM this argument exists to prevent).
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
        /// that never actually parallelize.
        /// </summary>
        public static int Resolve(
            FileParallelism request, int nFiles, int envCap, int processorCount,
            Func<long> availableBytesProbe, Func<long> perFileBytesEstimate,
            Action<string> log = null)
        {
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
                        @"File parallelism: {0} (explicit --parallel-files, {1} files)",
                        explicitN, nFiles));
                    return explicitN;

                case FileParallelismMode.Auto:
                    return ResolveAuto(nFiles, cores, cpuCap,
                        availableBytesProbe, perFileBytesEstimate, log);

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
                        @"File parallelism: 1 (sequential default; pass --parallel-files to score {0} files concurrently)",
                        nFiles));
                    return 1;
            }
        }

        /// <summary>
        /// Largest input footprint estimate in bytes (max input file size x
        /// <see cref="FOOTPRINT_MULTIPLIER"/>), or 0 when no file size can be
        /// read. Uses the max rather than the mean because the concurrent peak is
        /// bounded by the biggest files running together.
        /// </summary>
        public static long EstimatePerFileBytes(IEnumerable<string> inputFiles)
        {
            long maxBytes = 0;
            if (inputFiles != null)
            {
                foreach (var file in inputFiles)
                {
                    long len = SafeFileLength(file);
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
            Action<string> log)
        {
            long availableBytes = availableBytesProbe?.Invoke() ?? 0;
            long perFileBytes = perFileBytesEstimate?.Invoke() ?? 0;

            if (availableBytes <= 0 || perFileBytes <= 0)
            {
                // No usable memory signal -- fall back to a CPU-bound cap rather
                // than guessing. Still safer than the old unbounded default.
                log?.Invoke(string.Format(
                    @"File parallelism: {0} (auto, CPU-bound: {1} cores, {2} files; memory estimate unavailable)",
                    cpuCap, cores, nFiles));
                return cpuCap;
            }

            long budget = (long)(availableBytes * RAM_BUDGET_FRACTION);
            int memFit = (int)Math.Max(1, budget / perFileBytes);
            int chosen = Math.Max(1, Math.Min(cpuCap, memFit));
            log?.Invoke(string.Format(
                @"File parallelism: {0} (auto: {1:F1} GB free x {2:P0} / ~{3:F1} GB est per file -> {4} by RAM, capped to {5} cores / {6} files)",
                chosen, availableBytes / (double)BYTES_PER_GB, RAM_BUDGET_FRACTION,
                perFileBytes / (double)BYTES_PER_GB, memFit, cores, nFiles));
            return chosen;
        }

        private static long SafeFileLength(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return new FileInfo(path).Length;
            }
            catch (Exception)
            {
                // Unreadable path -- treat as unknown size (0), never throw from a
                // sizing hint.
            }
            return 0;
        }

        private const long BYTES_PER_GB = 1024L * 1024L * 1024L;
    }
}
