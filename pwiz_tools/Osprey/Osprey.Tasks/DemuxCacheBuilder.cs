/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Osprey.Core;
using pwiz.Osprey.Demux;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Turns a run's <c>.spectra.bin</c> into the spectra cache it is searched from: itself
    /// when its isolation windows do not overlap, or a demultiplexed
    /// <c>.demux.spectra.bin</c> derived from it when they do and <c>--demux</c> is on.
    /// </summary>
    /// <remarks>
    /// The <c>.spectra.bin</c> is never altered, so it stays a pure function of the raw file
    /// and shareable across analyses. The demultiplexed cache is derived from it cache to
    /// cache, with no second vendor read, and carries the demux descriptor, so changing a
    /// demux setting rebuilds seconds of demultiplexing instead of minutes of parsing. With
    /// <c>--demux off</c>, an overlapping scheme is refused rather than searched as acquired.
    /// </remarks>
    internal static class DemuxCacheBuilder
    {
        /// <summary>
        /// The demux settings a run uses. Library defaults throughout; <c>--demux</c> is the
        /// only switch, so the demultiplexed cache stays independent of search settings. The
        /// developer overrides in <see cref="OspreyEnvironment.DemuxEngine"/> and
        /// <see cref="OspreyEnvironment.DemuxBlockMode"/> exist to attribute differences from
        /// msconvert.
        /// </summary>
        internal static DemuxParams CreateParams(OspreyConfig config)
        {
            var parameters = new DemuxParams { Threads = Math.Max(1, config.NThreads) };
            parameters.Engine = ParseOverride(OspreyEnvironment.DemuxEngine,
                @"OSPREY_DEMUX_ENGINE", parameters.Engine);
            parameters.BlockMode = ParseOverride(OspreyEnvironment.DemuxBlockMode,
                @"OSPREY_DEMUX_BLOCK", parameters.BlockMode);
            parameters.Interpolation = ParseOverride(OspreyEnvironment.DemuxInterpolation,
                @"OSPREY_DEMUX_INTERPOLATION", parameters.Interpolation);
            parameters.OutputMode = ParseOverride(OspreyEnvironment.DemuxOutputMode,
                @"OSPREY_DEMUX_OUTPUT", parameters.OutputMode);
            parameters.MinChannelIons = ParseOverride(OspreyEnvironment.DemuxMinChannelIons,
                @"OSPREY_DEMUX_MIN_CHANNEL_IONS", parameters.MinChannelIons);
            double countsPerIon = ParseOverride(OspreyEnvironment.DemuxCountsPerIon,
                @"OSPREY_DEMUX_COUNTS_PER_ION", double.NaN);
            if (!double.IsNaN(countsPerIon))
            {
                parameters.CountsPerIon = countsPerIon;
                parameters.IonsFromFillTime = false;
            }
            return parameters;
        }

        /// <summary>
        /// The task validity-key suffix for demultiplexing: empty when it is off, so every
        /// existing key is unchanged, and otherwise the settings descriptor. The search hash
        /// records only the mode, because <c>Osprey.Core</c> cannot see the descriptor. But a
        /// new algorithm version or a developer override rebuilds the demultiplexed cache with
        /// different spectra, and every score computed from the old spectra must be rebuilt
        /// with it.
        /// </summary>
        internal static string ValidityKeySuffix(OspreyConfig config)
        {
            return config.DemuxMode == DemuxMode.off
                ? ValidityKeySuffix(config.DemuxMode, null)
                : ValidityKeySuffix(config.DemuxMode, CreateParams(config).Descriptor);
        }

        /// <summary>
        /// <see cref="ValidityKeySuffix(OspreyConfig)"/> for an explicitly supplied descriptor.
        /// </summary>
        internal static string ValidityKeySuffix(DemuxMode mode, string descriptor)
        {
            return mode == DemuxMode.off ? string.Empty : @";demux=" + descriptor;
        }

        /// <summary>
        /// A valid demultiplexed cache for the input, or null when there is none to use. Tried
        /// before the <c>.spectra.bin</c>, so a run staged with demux can be searched from its
        /// demultiplexed cache alone.
        /// </summary>
        internal static SpectraWindowIndex TryOpenDemuxCache(string inputFile, PipelineContext ctx)
        {
            if (ctx.Config.DemuxMode == DemuxMode.off)
                return null;
            string demuxPath = SpectraCache.GetDemuxCachePath(inputFile);
            if (!File.Exists(demuxPath))
                return null;
            try
            {
                var hit = SpectraWindowIndex.BuildFromCache(demuxPath, inputFile, out var reason,
                    CreateParams(ctx.Config).Descriptor);
                if (hit != null)
                {
                    ctx.LogInfo(string.Format(OspreyTasksResources.DemuxCacheBuilder_TryOpenDemuxCache_Streaming_demultiplexed_spectra_from_cache___0_, demuxPath));
                    return hit;
                }
                // No promise of a rebuild here: Stage 6 cannot rebuild, and stops instead.
                ctx.LogInfo(string.Format(OspreyTasksResources.DemuxCacheBuilder_TryOpenDemuxCache_Demultiplexed_spectra_cache__0__is_not_usable___1__,
                    demuxPath, SpectraCacheException.Describe(reason)));
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.DemuxCacheBuilder_TryOpenDemuxCache_Failed_to_index_demultiplexed_spectra_cache__0____1_, demuxPath, ex.Message));
            }
            return null;
        }

        /// <summary>
        /// Why the run's demultiplexed cache would be refused under the current settings, or
        /// <see cref="SpectraCacheRejection.None"/> when it would be searched. Reads the header
        /// only, for the start-up check of an input whose source is gone.
        /// </summary>
        internal static SpectraCacheRejection CheckDemuxCache(string inputFile, OspreyConfig config)
        {
            return SpectraCache.CheckHeader(SpectraCache.GetDemuxCachePath(inputFile), inputFile,
                CreateParams(config).Descriptor);
        }

        /// <summary>
        /// Given the run's valid <c>.spectra.bin</c> index, returns the index to search.
        /// </summary>
        /// <param name="inputFile">The source data file.</param>
        /// <param name="rawIndex">Index over the <c>.spectra.bin</c>.</param>
        /// <param name="parsed">
        /// The spectra just parsed from the source, when the <c>.spectra.bin</c> was written
        /// in this call; null on a cache hit, in which case they are read back from the cache.
        /// </param>
        /// <param name="parseSeconds">Wall time of that parse, or NaN on a cache hit.</param>
        /// <param name="ctx">Pipeline context for the settings and the log.</param>
        internal static SpectraWindowIndex Resolve(string inputFile, SpectraWindowIndex rawIndex,
            SpectrumFileResult parsed, double parseSeconds, PipelineContext ctx)
        {
            var scheme = DetectScheme(rawIndex);
            if (ctx.Config.DemuxMode == DemuxMode.off)
            {
                ThrowIfOverlapping(inputFile, scheme);
                return rawIndex;
            }
            if (scheme.Kind == DemuxSchemeKind.non_overlapping)
            {
                ctx.LogInfo(string.Format(
                    OspreyTasksResources.DemuxCacheBuilder_Resolve_Isolation_windows_of__0__do_not_overlap__searching_as_acquired_,
                    Path.GetFileName(inputFile)));
                return rawIndex;
            }

            var parameters = CreateParams(ctx.Config);
            string descriptor = parameters.Descriptor;
            string demuxPath = SpectraCache.GetDemuxCachePath(inputFile);
            List<Spectrum> ms2;
            List<MS1Spectrum> ms1;
            if (parsed != null)
            {
                ms2 = parsed.Ms2Spectra;
                ms1 = parsed.Ms1Spectra;
            }
            else
            {
                var loaded = SpectraCache.LoadSpectraCache(rawIndex.CachePath, inputFile);
                if (loaded == null)
                {
                    throw new IOException(string.Format(
                        OspreyTasksResources.DemuxCacheBuilder_Resolve_Could_not_read___0___to_demultiplex_it_, rawIndex.CachePath));
                }
                ms2 = loaded.Ms2Spectra;
                ms1 = loaded.Ms1Spectra;
            }

            IReadOnlyList<SpectrumAcquisition> acquisitions = null;
            if (parameters.Engine == DemuxEngine.weighted && parameters.IonsFromFillTime)
                acquisitions = AcquisitionsOf(inputFile, rawIndex, parsed, ms2, ctx);

            var stopwatch = Stopwatch.StartNew();
            var result = Demultiplexer.Demultiplex(ms2, parameters, acquisitions);
            double demuxSeconds = stopwatch.Elapsed.TotalSeconds;
            LogSummary(inputFile, result, parameters, demuxSeconds, parseSeconds, ctx);

            SpectraCache.SaveSpectraCache(demuxPath, result.Spectra, ms1, inputFile, descriptor);
            var index = SpectraWindowIndex.BuildFromCache(demuxPath, inputFile, out var reason, descriptor);
            if (index == null)
            {
                throw new IOException(string.Format(
                    OspreyTasksResources.DemuxCacheBuilder_Resolve_Could_not_index_the_demultiplexed_spectra_cache___0___just_written___1__,
                    demuxPath, SpectraCacheException.Describe(reason)));
            }
            return index;
        }

        /// <summary>
        /// For a reader that found only the <c>.spectra.bin</c> while <c>--demux</c> is on:
        /// fine when the run does not overlap, an error when it does, because the demultiplexed
        /// cache the earlier stages searched is missing.
        /// </summary>
        internal static void ThrowIfDemuxCacheMissing(string inputFile, SpectraWindowIndex rawIndex,
            PipelineContext ctx)
        {
            if (ctx.Config.DemuxMode == DemuxMode.off)
            {
                ThrowIfOverlapping(inputFile, DetectScheme(rawIndex));
                return;
            }
            if (DetectScheme(rawIndex).Kind == DemuxSchemeKind.overlapping)
            {
                throw new SpectraCacheException(string.Format(
                    OspreyTasksResources.DemuxCacheBuilder_ThrowIfDemuxCacheMissing___0___is_demultiplexed__but_its_demultiplexed_spectra_cache___1___is_missing_or_stale_,
                    Path.GetFileName(inputFile), SpectraCache.GetDemuxCachePath(inputFile),
                    OspreyArgNames.TaskText(PerFileScoringTask.TASK_NAME), OspreyArgNames.Text(OspreyArgNames.DEMUX)),
                    SpectraCacheRejection.Absent, SpectraCache.GetDemuxCachePath(inputFile));
            }
        }

        /// <summary>
        /// Each MS2 spectrum's fill time and ion response, for counting its ions: from the parse just done, else from
        /// the acquisition sidecar beside the <c>.spectra.bin</c>, else read from the source's metadata (a cache
        /// written before the sidecar existed) and saved beside it. Null when the source is gone too: the spectra are
        /// then counted at <see cref="DemuxParams.CountsPerIon"/>.
        /// </summary>
        private static IReadOnlyList<SpectrumAcquisition> AcquisitionsOf(string inputFile,
            SpectraWindowIndex rawIndex, SpectrumFileResult parsed, List<Spectrum> ms2, PipelineContext ctx)
        {
            var info = parsed?.Acquisition;
            if (info == null)
            {
                string sidecarPath = AcquisitionSidecar.PathFor(rawIndex.CachePath);
                info = AcquisitionSidecar.Load(sidecarPath, inputFile);
                if (info == null)
                {
                    if (!File.Exists(inputFile) && !Directory.Exists(inputFile))
                    {
                        ctx.LogWarning(string.Format(
                            OspreyTasksResources.DemuxCacheBuilder_AcquisitionsOf_No_fill_times_for__0____1__is_missing_and_so_is_its_source__Counting_ions_at__2__per_ion_,
                            Path.GetFileName(inputFile), sidecarPath, DemuxParams.DEFAULT_COUNTS_PER_ION));
                        return null;
                    }
                    ctx.LogInfo(string.Format(OspreyTasksResources.DemuxCacheBuilder_AcquisitionsOf_Reading_the_fill_times_of__0__, Path.GetFileName(inputFile)));
                    info = SpectrumFileReader.ReadAcquisition(inputFile);
                    SaveAcquisition(sidecarPath, info, inputFile, ctx);
                }
            }

            var responses = new Dictionary<string, double>();
            var acquisitions = new SpectrumAcquisition[ms2.Count];
            for (int i = 0; i < acquisitions.Length; i++)
            {
                if (!info.TryGet(ms2[i].ScanNumber, out double fillTimeMs, out string analyzer))
                {
                    acquisitions[i] = new SpectrumAcquisition(double.NaN, double.NaN);
                    continue;
                }
                if (!responses.TryGetValue(analyzer, out double alpha))
                {
                    alpha = IonResponse.Of(IonResponse.KindOf(analyzer));
                    responses.Add(analyzer, alpha);
                }
                acquisitions[i] = new SpectrumAcquisition(fillTimeMs, alpha);
            }
            return acquisitions;
        }

        /// <summary>
        /// Writes the acquisition sidecar beside a <c>.spectra.bin</c>. A failure costs only a metadata re-read on a
        /// later demultiplexing run, so it is a warning.
        /// </summary>
        internal static void SaveAcquisition(string sidecarPath, AcquisitionInfo info, string inputFile,
            PipelineContext ctx)
        {
            try
            {
                AcquisitionSidecar.Save(sidecarPath, info, inputFile);
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(OspreyTasksResources.DemuxCacheBuilder_SaveAcquisition_Failed_to_save__0____1_, sidecarPath, ex.Message));
            }
        }

        private static DemuxScheme DetectScheme(SpectraWindowIndex index)
        {
            // Every distinct window the run recorded, not just the first cycle: a staggered
            // run's offset set only appears after the first set has been acquired.
            return DemuxSchemeDetector.Detect(index.IsolationWindows);
        }

        private static void ThrowIfOverlapping(string inputFile, DemuxScheme scheme)
        {
            if (scheme.Kind != DemuxSchemeKind.overlapping)
                return;
            throw new InvalidOperationException(string.Format(
                OspreyTasksResources.DemuxCacheBuilder_ThrowIfOverlapping___0___was_acquired_with_overlapping_isolation_windows___1__fold___2__windows_over__3__bins___Searche,
                Path.GetFileName(inputFile), scheme.OverlapFactor, scheme.Windows.Count, scheme.Bins.Count,
                OspreyArgNames.Text(OspreyArgNames.DEMUX)));
        }

        private static void LogSummary(string inputFile, DemuxResult result, DemuxParams parameters,
            double demuxSeconds, double parseSeconds, PipelineContext ctx)
        {
            var scheme = result.Scheme;
            var stats = result.Statistics;
            double minWidth = scheme.Bins.Min(b => b.Width);
            double maxWidth = scheme.Bins.Max(b => b.Width);
            ctx.LogInfo(string.Format(
                OspreyTasksResources.DemuxCacheBuilder_LogSummary_Demultiplexing__0____1__fold_overlap___2__windows_into__3__bins_of__4_F3___5_F3__Th,
                Path.GetFileName(inputFile), scheme.OverlapFactor, scheme.Windows.Count,
                scheme.Bins.Count, minWidth, maxWidth));
            if (parameters.Engine == DemuxEngine.weighted)
            {
                ctx.LogInfo(string.Format(
                    OspreyTasksResources.DemuxCacheBuilder_LogSummary____0_N0__MS2_spectra_____1_N0____2_N0__fragment_channels___3_N0__solved___4_P2__of__5_E3__ions_passe,
                    stats.SpectraIn, stats.SpectraOut, stats.Channels, stats.ChannelsSolved,
                    stats.IonsPassedThrough / Math.Max(stats.IonsIn, 1e-30), stats.IonsIn));
            }
            else
            {
                double channels = Math.Max(1, stats.Channels);
                ctx.LogInfo(string.Format(
                    OspreyTasksResources.DemuxCacheBuilder_LogSummary____0_N0__MS2_spectra_____1_N0____2_N0__channel_solves___3_P1__zero___4_P1__unconstrained___5_P1__act,
                    stats.SpectraIn, stats.SpectraOut, stats.Channels, stats.ZeroSolves / channels,
                    stats.UnconstrainedSolves / channels, stats.ActiveSetSolves / channels,
                    stats.IterationCapSolves, stats.Geometries));
            }
            // The timing gate: demux against the parse it follows decides whether the solver
            // needs more than scalar code.
            string parse = double.IsNaN(parseSeconds)
                ? @"no parse (cache hit)"
                : string.Format(@"parse {0:F1}s, ratio {1:F2}", parseSeconds, demuxSeconds / Math.Max(parseSeconds, 1e-9));
            ctx.LogInfo(string.Format(OspreyTasksResources.DemuxCacheBuilder_LogSummary___Demultiplexed_in__0_F1_s_with__1__threads___2____3_,
                demuxSeconds, parameters.Threads, parse, parameters.Descriptor));
        }

        private static double ParseOverride(string value, string variable, double defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) &&
                parsed > 0 && !double.IsInfinity(parsed))
                return parsed;
            throw new InvalidOperationException(string.Format(@"{0}={1} is not a positive number", variable, value));
        }

        private static TEnum ParseOverride<TEnum>(string value, string variable, TEnum defaultValue)
            where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            if (Enum.TryParse(value.Trim(), true, out TEnum parsed) && Enum.IsDefined(typeof(TEnum), parsed))
                return parsed;
            throw new InvalidOperationException(string.Format(@"{0}={1} is not one of: {2}",
                variable, value, string.Join(@", ", Enum.GetNames(typeof(TEnum)))));
        }
    }
}
