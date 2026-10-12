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
using System.IO;
using System.Threading;
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
        public const string TASK_NAME = OspreyTaskNames.SPECTRA_CACHE;

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
                return RequiresError(OspreyArgNames.Text(OspreyArgNames.INPUT, @"<file...>"));
            return null;
        }

        public override string DescribeOutput(OspreyConfig config)
        {
            // With --output-dir and no --cache-dir, ArtifactPaths.ResolveCacheDir writes beside each
            // input only where that folder is writable, and into the output directory otherwise.
            string perInput = config.InputFiles != null && config.InputFiles.Count > 1 &&
                              string.IsNullOrEmpty(config.CacheDir) && !string.IsNullOrEmpty(config.OutputDir)
                ? string.Format(OspreyTasksResources.SpectraCacheTask_DescribeOutput_a__spectra_bin_file_next_to_each_input__or_in__0__where_the_input_folder_is_read_only,
                    config.OutputDir, SpectraCache.EXT)
                : DescribePerInputOutput(config, SpectraCache.GetCachePath, SpectraCache.EXT, config.CacheDir);
            return string.Format(OspreyTasksResources.SpectraCacheTask_DescribeOutput__0_____output_and___library_are_not_used_, perInput,
                OspreyArgNames.Text(OspreyArgNames.OUTPUT), OspreyArgNames.Text(OspreyArgNames.LIBRARY));
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
        /// the driver skip or run the task wholesale; the per-file
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

            // Each file's cache is independent - its own parse, its own .spectra.bin - so files
            // can run on lanes in any order. Unlike the scoring path, a vendor parse is NOT
            // funneled through ScoringTaskShared.s_mzmlReadGate: it is bound by its own decode
            // thread far more than by the disk (measured: ~20 MB/s per Thermo file against an
            // SSD array sitting 43% idle), so gating it would put the lanes back in single file.
            // An mzML parse still is (see CacheFile).
            // The lane count is this stage's own (--parallel-files-caching, else --parallel-files),
            // resolved like every other per-file stage's; one lane is the plain loop. --threads is
            // not divided by it: a decode runs on its own thread.
            int lanes = PerFileScoringTask.ResolveFileParallelism(config, FileStage.Caching, nFiles, ctx.LogInfo);
            if (lanes <= 1)
            {
                for (int fileIdx = 0; fileIdx < nFiles; fileIdx++)
                {
                    if (CacheFile(ctx, fileIdx, nFiles, false))
                        built++;
                }
            }
            else
            {
                // Same presentation as PerFileScoring's lanes: a numbered legend once, then one
                // "[i] p%" line, with each file's narrative flushed as a block when it finishes.
                ctx.LogInfo(string.Format(OspreyTasksResources.SpectraCacheTask_Run_Caching__0__files___1__at_a_time_,
                    nFiles, lanes));
                for (int legendIdx = 0; legendIdx < nFiles; legendIdx++)
                    ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(@"{0}. {1}", legendIdx + 1, config.InputFiles[legendIdx]));
                var multi = new MultiProgressReporter();
                OrderedFileLanes.For(nFiles, lanes, fileIdx =>
                {
                    using (multi.BeginFile(fileIdx, 1))
                    {
                        MultiProgressReporter.Current.BeginSegment();
                        if (CacheFile(ctx, fileIdx, nFiles, true))
                            Interlocked.Increment(ref built);
                    }
                });
            }

            swAll.Stop();
            ctx.LogInfo(nFiles == 1 && built == 1
                ? string.Format(OspreyTasksResources.SpectraCacheTask_Run_Cached_1_file_in__0_s, swAll.Elapsed.TotalSeconds)
                : string.Format(OspreyTasksResources.SpectraCacheTask_Run_Cached__0__of__1__files_in__2_s, built, nFiles, swAll.Elapsed.TotalSeconds));
            return ctx.ExitCode == 0;
        }

        /// <summary>
        /// Builds (or confirms) one input's cache and logs its time. Returns false when the
        /// file failed, having set the run's exit code: one unreadable input must not abandon
        /// the rest of a long staging sweep, but it must still fail the run - a partially
        /// staged dataset that reports success would be discovered much later, in a scoring
        /// run that silently re-parses. Safe on a lane: everything it touches is the file's
        /// own, and the exit code is only ever set to failure.
        /// </summary>
        private static bool CacheFile(PipelineContext ctx, int fileIdx, int nFiles, bool onLanes)
        {
            string inputFile = ctx.Config.InputFiles[fileIdx];
            ctx.LogInfo(string.Format(OspreyTasksResources.SpectraCacheTask_Run_Caching_spectra__0___1____2_,
                fileIdx + 1, nFiles, inputFile));

            var swFile = Stopwatch.StartNew();
            SpectraWindowIndex index;
            try
            {
                // A vendor decode runs on one thread, so lanes run it ungated. An mzML parse
                // decodes on OSPREY_MZML_DECODE_THREADS of its own and streams the file at disk
                // speed - sized for one file at a time - so on lanes it still takes the read gate.
                bool gateRead = onLanes && !SpectrumFileReader.IsVendorFormat(inputFile);
                index = ScoringTaskShared.EnsureSpectraCache(
                    inputFile, gateRead, out int unsortedCount, ctx);
                if (unsortedCount > 0)
                {
                    ctx.LogWarning(string.Format(
                        OspreyTasksResources.SpectraCacheTask_Run__0____1__spectra_had_unsorted_peaks_and_were_sorted_before_caching_,
                        Path.GetFileName(inputFile), unsortedCount));
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // Not an out-of-memory: on lanes that is the lane count's failure, not this
                // input's, and reporting it as one file's read failure would let every lane
                // keep starting whole-file parses at the memory ceiling.
                ctx.LogError(string.Format(OspreyTasksResources.SpectraCacheTask_Run_Failed_to_cache__0____1_, inputFile, ex.Message));
                ctx.ExitCode = 1;
                return false;
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
                    OspreyTasksResources.SpectraCacheTask_Run_No_MS_MS_spectra_were_read_from__0___so_no_cache_is_written_, inputFile));
                ctx.ExitCode = 1;
                return false;
            }

            // The cache writer already said what it saved, in words; this is the per-file
            // time for a measurement, not a second report of the same numbers.
            string cachePath = SpectraCache.GetCachePath(inputFile);
            var cacheInfo = new FileInfo(cachePath);
            ctx.LogInfo(LogTag.TIMING, @"Spectra cache {0}: ms2={1} ms1={2} {3:F2} GB in {4:F1}s",
                Path.GetFileName(cachePath), index.Ms2Count, index.Ms1Spectra.Count,
                cacheInfo.Length / (1024.0 * 1024.0 * 1024.0), swFile.Elapsed.TotalSeconds);
            return true;
        }

        /// <summary>
        /// Nothing to rehydrate: this task publishes no byproducts, so no consumer
        /// can demand it. Its outputs are read back off disk by
        /// <see cref="ScoringTaskShared.EnsureSpectraCache"/> on a later scoring run.
        /// </summary>
        public override bool Rehydrate(PipelineContext ctx) => true;
    }
}
