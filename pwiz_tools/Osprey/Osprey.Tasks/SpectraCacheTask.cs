/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Globalization;
using System.IO;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Stage 1 alone: build each input's <c>.spectra.bin</c> cache and stop
    /// (<c>--task SpectraCache</c>).
    ///
    /// This is the data-staging step ahead of the pipeline rather than one of its
    /// HPC fan-out nodes, which is why it needs no <c>--library</c> and publishes no
    /// byproducts: caching depends only on the input file. Staging a dataset is
    /// otherwise only reachable by running all of Stage 1-4 through
    /// <see cref="PerFileScoringTask"/>, which additionally demands a library and
    /// spends calibration + scoring + parquet time that a staging pass does not need.
    ///
    /// It builds caches through the same
    /// <see cref="ScoringTaskShared.EnsureSpectraCache"/> the scoring path uses, so a
    /// cache written here is byte-for-byte what a full run would have written.
    /// </summary>
    internal sealed class SpectraCacheTask : OspreyTask
    {
        /// <summary>
        /// This task's name, as a constant so the CLI selector, the validity stamp another
        /// task looks for, and the tests all spell it from here rather than duplicating it.
        /// </summary>
        public const string TASK_NAME = @"SpectraCache";

        public override string Name => TASK_NAME;

        /// <summary>
        /// Sees one input at a time and never computes an experiment-wide score.
        /// </summary>
        public override bool IsPerFileWorker => true;

        /// <summary>
        /// Inputs in, <c>.spectra.bin</c> out. Deliberately does NOT require
        /// <c>--library</c>: caching depends only on the input file, and demanding one
        /// would make staging a dataset wait on a library that is often chosen later.
        /// </summary>
        public override string ValidateSelection(OspreyConfig config)
        {
            if (!config.HasInputFiles)
                return RequiresError(@"--input <file...>");
            return null;
        }

        public override string DescribeOutput(OspreyConfig config)
        {
            return string.Format("{0} (--output and --library are not used)",
                DescribePerInputOutput(config, SpectraCache.GetCachePath, @".spectra.bin", config.CacheDir));
        }

        public override IEnumerable<string> Inputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null)
                yield break;
            foreach (var input in ctx.Config.InputFiles)
                yield return input;
        }

        /// <summary>
        /// Deliberately empty, so the driver always calls <see cref="Run"/>. The
        /// caches ARE this task's durable output, but reporting them here would have
        /// the driver skip the task wholesale on a validity sidecar; the per-file
        /// cache-hit check inside <see cref="ScoringTaskShared.EnsureSpectraCache"/>
        /// already makes a re-run cheap, and it is per input rather than all-or-
        /// nothing, which is what a 164-file staging sweep interrupted partway
        /// through actually needs.
        /// </summary>
        public override IEnumerable<string> Outputs(PipelineContext ctx) => Array.Empty<string>();

        public override bool Run(PipelineContext ctx)
        {
            var config = ctx.Config;
            int nFiles = config.InputFiles.Count;
            var swAll = Stopwatch.StartNew();
            int built = 0;

            // Sequential by design. The work is one disk-bound parse per file, which
            // is exactly what s_mzmlReadGate serializes on the scoring path anyway;
            // running files in parallel here would contend for the same spindle while
            // multiplying the transient parse buffer by the file count.
            for (int fileIdx = 0; fileIdx < nFiles; fileIdx++)
            {
                string inputFile = config.InputFiles[fileIdx];
                ctx.LogInfo(string.Format("Caching spectra {0}/{1}: {2}",
                    fileIdx + 1, nFiles, inputFile));

                var swFile = Stopwatch.StartNew();
                SpectraWindowIndex index;
                try
                {
                    index = ScoringTaskShared.EnsureSpectraCache(
                        inputFile, false, out int unsortedCount, ctx);
                    if (unsortedCount > 0)
                    {
                        ctx.LogWarning(string.Format(
                            "{0}: {1:N0} spectra had unsorted peaks and were sorted before caching.",
                            Path.GetFileName(inputFile), unsortedCount));
                    }
                }
                catch (Exception ex)
                {
                    // One unreadable input must not abandon the rest of a long
                    // staging sweep, but it must still fail the run: a partially
                    // staged dataset that reports success would be discovered much
                    // later, in a scoring run that silently re-parses.
                    ctx.LogError(string.Format("Failed to cache {0}: {1}", inputFile, ex.Message));
                    ctx.ExitCode = 1;
                    continue;
                }
                swFile.Stop();

                // A cache with no MS2 is a staging failure, not a staged file. It is
                // header-valid, so every later scoring run ACCEPTS it, reports "No
                // spectra found" and drops the file - and never re-parses, because the
                // cache is valid rather than stale. The scoring path already refuses a
                // zero-MS2 index; staging has to refuse it too or it launders the
                // problem into a cache. The usual cause is a reader configuration that
                // filtered every spectrum out, which is worth failing loudly on.
                if (index.Ms2Count == 0)
                {
                    ctx.LogError(string.Format(
                        "No MS2 spectra were read from {0}; refusing to stage an empty cache.", inputFile));
                    ctx.ExitCode = 1;
                    continue;
                }
                built++;

                // The cache writer already said what it saved, in words; this is the per-file
                // time for a measurement, not a second report of the same numbers.
                string cachePath = SpectraCache.GetCachePath(inputFile);
                var cacheInfo = new FileInfo(cachePath);
                ctx.LogInfo(LogTag.TIMING, string.Format(CultureInfo.InvariantCulture,
                    @"Spectra cache {0}: ms2={1} ms1={2} {3:F2} GB in {4:F1}s",
                    Path.GetFileName(cachePath), index.Ms2Count, index.Ms1Spectra.Count,
                    cacheInfo.Length / (1024.0 * 1024.0 * 1024.0), swFile.Elapsed.TotalSeconds));
            }

            swAll.Stop();
            ctx.LogInfo(nFiles == 1 && built == 1
                ? string.Format("Cached 1 file in {0:N1}s", swAll.Elapsed.TotalSeconds)
                : string.Format("Cached {0:N0} of {1:N0} files in {2:N1}s", built, nFiles, swAll.Elapsed.TotalSeconds));
            return ctx.ExitCode == 0;
        }

        /// <summary>
        /// Nothing to rehydrate: this task publishes no byproducts, so no consumer
        /// can demand it. Its outputs are read back off disk by
        /// <see cref="ScoringTaskShared.EnsureSpectraCache"/> on a later scoring run.
        /// </summary>
        public override bool Rehydrate(PipelineContext ctx) => true;
    }
}
