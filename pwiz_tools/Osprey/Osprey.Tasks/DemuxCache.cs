/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * The demultiplexed cache contract (file name, descriptor, validity key, start-up check)
 * follows Michael MacCoss's DemuxCacheBuilder in ProteoWizard/pwiz PR #4710.
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
using System.Globalization;
using System.IO;
using Pwiz.Analysis;
using Pwiz.Util.Chemistry;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The demultiplexed spectra cache (<c>{stem}.demux.spectra.bin</c>) that <c>--demux auto</c>
    /// searches a multiplexed run from.
    /// </summary>
    /// <remarks>
    /// Osprey does no demultiplexing of its own. With <c>--demux auto</c> the run is read through
    /// ProteoWizard with its demultiplexer applied (<see cref="SpectrumFileReader.LoadAllSpectra"/>),
    /// so the cache holds the spectra msconvert's <c>demultiplex optimization=overlap_only</c>
    /// filter would write to an mzML, without the mzML. A run that is not multiplexed is read as
    /// acquired into the ordinary <c>.spectra.bin</c>, so <c>--demux auto</c> is safe to pass for
    /// any run.
    ///
    /// The demultiplexed cache is a separate file, with its own magic and the settings descriptor
    /// in its header (<see cref="SpectraCache"/>), so it is never mistaken for the
    /// <c>.spectra.bin</c> of the same run, and a change of settings rebuilds it. It is built
    /// from the source file: a demultiplexed read is a different read of the run, not a
    /// transformation of the <c>.spectra.bin</c>.
    /// </remarks>
    internal static class DemuxCache
    {
        /// <summary>
        /// Bumped when ProteoWizard's demultiplexer changes the spectra it produces, so every
        /// demultiplexed cache and every score computed from one is rebuilt. Its settings are in
        /// the descriptor already; this covers a change of the algorithm under the same settings.
        /// </summary>
        private const int DESCRIPTOR_VERSION = 1;

        // msconvert's demultiplex default, and the tolerance the overlap demultiplexer bins the
        // peaks of neighbouring spectra with.
        private const double MASS_ERROR_PPM = 10;

        /// <summary>
        /// The demultiplexing settings for a run, or null when <c>--demux</c> is off. msconvert's
        /// <c>demultiplex optimization=overlap_only massError=10ppm</c>, solved on the search's
        /// threads shared among the <paramref name="concurrentReads"/> files being read at once
        /// (one, except on <c>--task SpectraCache</c> lanes): each solve thread holds blocks.
        /// </summary>
        internal static SpectrumListDemux.Params CreateParams(OspreyConfig config, int concurrentReads = 1)
        {
            if (config.DemuxMode == DemuxMode.off)
                return null;
            return new SpectrumListDemux.Params
            {
                Optimization = SpectrumListDemux.Optimization.OverlapOnly,
                MassError = new MZTolerance(MASS_ERROR_PPM, MZToleranceUnits.Ppm),
                SolveThreads = Math.Max(1, config.NThreads / Math.Max(1, concurrentReads)),
            };
        }

        /// <summary>
        /// Every setting that changes the demultiplexed spectra, in a stable, culture-invariant
        /// form; <see cref="SpectrumListDemux.Params.SolveThreads"/> is left out because the
        /// output does not depend on it.
        /// </summary>
        internal static string Descriptor(SpectrumListDemux.Params p)
        {
            var ic = CultureInfo.InvariantCulture;
            return string.Format(ic,
                @"pwiz-demux/{0};massError={1}{2};interpolateRT={3};weighting={4};sumNormalize={5};" +
                @"variableFill={6};nnlsMaxIter={7};nnlsEps={8};blockExtra={9};minWindowSize={10};removeNonOverlappingEdges={11}",
                DESCRIPTOR_VERSION, p.MassError.Value.ToString(@"R", ic), p.MassError.Units,
                p.InterpolateRetentionTime, p.ApplyWeighting, p.RegularizeSums, p.VariableFill,
                p.NnlsMaxIter, p.NnlsEps.ToString(@"R", ic), p.DemuxBlockExtra.ToString(@"R", ic),
                p.MinimumWindowSize.ToString(@"R", ic), p.RemoveNonOverlappingEdges);
        }

        /// <summary>
        /// The task validity-key suffix for demultiplexing: empty when it is off, so every
        /// existing key is unchanged, and otherwise the descriptor. The search hash records only
        /// the mode, because <c>Osprey.Core</c> cannot see the descriptor, but a new descriptor
        /// means different spectra, and every score computed from the old ones must be rebuilt.
        /// </summary>
        internal static string ValidityKeySuffix(OspreyConfig config)
        {
            var p = CreateParams(config);
            return p == null ? string.Empty : @";demux=" + Descriptor(p);
        }

        /// <summary>
        /// A valid demultiplexed cache for the input, or null when there is none to use. Tried
        /// before anything else, so a run staged with demultiplexing can be searched from its
        /// demultiplexed cache alone. <paramref name="loadMs1"/> as for
        /// <see cref="SpectraWindowIndex.BuildFromCache(string,string,bool,out SpectraCacheRejection,string)"/>.
        /// </summary>
        internal static SpectraWindowIndex TryOpen(string inputFile, PipelineContext ctx, bool loadMs1 = true)
        {
            var p = CreateParams(ctx.Config);
            if (p == null)
                return null;
            string demuxPath = SpectraCache.GetDemuxCachePath(inputFile);
            if (!File.Exists(demuxPath))
                return null;
            try
            {
                var hit = SpectraWindowIndex.BuildFromCache(demuxPath, inputFile, loadMs1, out var reason, Descriptor(p));
                if (hit != null)
                {
                    ctx.LogInfo(string.Format(OspreyTasksResources.DemuxCache_TryOpen_Streaming_demultiplexed_spectra_from_cache___0_, demuxPath));
                    return hit;
                }
                ctx.LogInfo(string.Format(OspreyTasksResources.DemuxCache_TryOpen_Demultiplexed_spectra_cache__0__is_not_usable___1__,
                    demuxPath, SpectraCacheException.Describe(reason)));
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(OspreyTasksResources.DemuxCache_TryOpen_Failed_to_index_demultiplexed_spectra_cache__0____1_,
                    demuxPath, ex.Message));
            }
            return null;
        }

        /// <summary>
        /// Why the run's demultiplexed cache would be refused under the current settings, or
        /// <see cref="SpectraCacheRejection.None"/> when it would be searched. Reads the header
        /// only, for the start-up check of an input whose source is gone.
        /// </summary>
        internal static SpectraCacheRejection Check(string inputFile, OspreyConfig config)
        {
            var p = CreateParams(config);
            if (p == null)
                return SpectraCacheRejection.Absent;
            return SpectraCache.CheckHeader(SpectraCache.GetDemuxCachePath(inputFile), inputFile, Descriptor(p));
        }

        /// <summary>
        /// Whether the run a <c>.spectra.bin</c> holds is multiplexed, so that <c>--demux auto</c>
        /// must search its demultiplexed cache instead: the reason a valid <c>.spectra.bin</c>
        /// cannot be searched in its place. Decided from the isolation windows the cache
        /// recorded, by the same ProteoWizard detection the read uses over the same spectra, so it
        /// needs neither the source file nor a vendor reader. An MSX run is the exception: the
        /// cache keeps one window per spectrum, so it is not recognized here. False when
        /// <c>--demux</c> is off.
        /// </summary>
        internal static bool IsDemultiplexedRun(SpectraWindowIndex plainIndex, OspreyConfig config)
        {
            if (CreateParams(config) == null)
                return false;
            plainIndex.ReadRecordedIsolationWindows(out var centers, out var lowerOffsets, out var upperOffsets);
            return SpectrumFileReader.DetectDemultiplexScheme(centers, lowerOffsets, upperOffsets) != null;
        }

        /// <summary>
        /// <see cref="IsDemultiplexedRun(SpectraWindowIndex,OspreyConfig)"/> for the input's
        /// <c>.spectra.bin</c>, false when there is no usable one: a damaged cache is then
        /// reported where it is read, as before. For the start-up check.
        /// </summary>
        internal static bool IsDemultiplexedRun(string inputFile, OspreyConfig config)
        {
            if (CreateParams(config) == null)
                return false;
            try
            {
                var plainIndex = SpectraWindowIndex.BuildFromCache(SpectraCache.GetCachePath(inputFile), inputFile,
                    false, out _);
                return plainIndex != null && IsDemultiplexedRun(plainIndex, config);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException)
            {
                return false;
            }
        }

        /// <summary>
        /// The run's demultiplexed cache when one exists, otherwise its <c>.spectra.bin</c>: the
        /// larger of the two a run can be searched from, for sizing a run without its source.
        /// </summary>
        internal static string GetLargestCachePath(string inputFile)
        {
            string demuxPath = SpectraCache.GetDemuxCachePath(inputFile);
            return File.Exists(demuxPath) ? demuxPath : SpectraCache.GetCachePath(inputFile);
        }

        /// <summary>
        /// For a reader that found only the <c>.spectra.bin</c> while <c>--demux</c> is on: fine
        /// when the run is not multiplexed, an error when it is, because the demultiplexed cache
        /// the earlier stages searched is missing.
        /// </summary>
        internal static void ThrowIfDemuxCacheMissing(string inputFile, SpectraWindowIndex plainIndex,
            PipelineContext ctx)
        {
            if (!IsDemultiplexedRun(plainIndex, ctx.Config))
                return;
            string demuxPath = SpectraCache.GetDemuxCachePath(inputFile);
            throw new SpectraCacheException(string.Format(
                OspreyTasksResources.DemuxCache_ThrowIfDemuxCacheMissing___0___is_demultiplexed__but_its_demultiplexed_spectra_cache___1___is_missing_or_stale_,
                Path.GetFileName(inputFile), demuxPath,
                OspreyArgNames.TaskText(PerFileScoringTask.TASK_NAME), OspreyArgNames.Text(OspreyArgNames.DEMUX)),
                SpectraCacheRejection.Absent, demuxPath);
        }
    }
}
