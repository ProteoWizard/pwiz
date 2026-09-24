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
        /// developer overrides in <see cref="OspreyEnvironment.DemuxBlockMode"/> exist to
        /// attribute differences from msconvert.
        /// </summary>
        internal static DemuxParams CreateParams(OspreyConfig config)
        {
            var parameters = new DemuxParams { Threads = Math.Max(1, config.NThreads) };
            parameters.BlockMode = ParseOverride(OspreyEnvironment.DemuxBlockMode,
                @"OSPREY_DEMUX_BLOCK", parameters.BlockMode);
            parameters.Interpolation = ParseOverride(OspreyEnvironment.DemuxInterpolation,
                @"OSPREY_DEMUX_INTERPOLATION", parameters.Interpolation);
            parameters.OutputMode = ParseOverride(OspreyEnvironment.DemuxOutputMode,
                @"OSPREY_DEMUX_OUTPUT", parameters.OutputMode);
            return parameters;
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
                    ctx.LogInfo(string.Format("Streaming demultiplexed spectra from cache: {0}", demuxPath));
                    return hit;
                }
                ctx.LogInfo(string.Format("Demultiplexed spectra cache not usable ({0}); rebuilding it.",
                    SpectraCacheException.Describe(reason)));
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    "Failed to index demultiplexed spectra cache: {0}. Rebuilding it.", ex.Message));
            }
            return null;
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
                    "Isolation windows of {0} do not overlap; searching as acquired.",
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
                        "Could not read '{0}' to demultiplex it.", rawIndex.CachePath));
                }
                ms2 = loaded.Ms2Spectra;
                ms1 = loaded.Ms1Spectra;
            }

            var stopwatch = Stopwatch.StartNew();
            var result = Demultiplexer.Demultiplex(ms2, parameters);
            double demuxSeconds = stopwatch.Elapsed.TotalSeconds;
            LogSummary(inputFile, result, parameters, demuxSeconds, parseSeconds, ctx);

            SpectraCache.SaveSpectraCache(demuxPath, result.Spectra, ms1, inputFile, descriptor);
            var index = SpectraWindowIndex.BuildFromCache(demuxPath, inputFile, out var reason, descriptor);
            if (index == null)
            {
                throw new IOException(string.Format(
                    "Could not index the demultiplexed spectra cache '{0}' just written: {1}.",
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
                    "'{0}' is demultiplexed, but its demultiplexed spectra cache '{1}' is missing or " +
                    "stale. Re-run PerFileScoring with --demux auto to rebuild it.",
                    Path.GetFileName(inputFile), SpectraCache.GetDemuxCachePath(inputFile)),
                    SpectraCacheRejection.Absent, SpectraCache.GetDemuxCachePath(inputFile));
            }
        }

        private static DemuxScheme DetectScheme(SpectraWindowIndex index)
        {
            // Every distinct window the run recorded, not just the first cycle: a staggered
            // run's offset set only appears after the first set has been acquired.
            var windows = new List<IsolationWindow>(index.WindowKeysInFileOrder.Count);
            foreach (int key in index.WindowKeysInFileOrder)
            {
                if (index.TryGetWindowIsolation(key, out var window))
                    windows.Add(window);
            }
            return DemuxSchemeDetector.Detect(windows);
        }

        private static void ThrowIfOverlapping(string inputFile, DemuxScheme scheme)
        {
            if (scheme.Kind != DemuxSchemeKind.overlapping)
                return;
            throw new InvalidOperationException(string.Format(
                "'{0}' was acquired with overlapping isolation windows ({1}-fold, {2} windows over " +
                "{3} bins). Searched as acquired, every precursor would be scored in {1} windows. " +
                "Re-run with --demux auto to demultiplex it first.",
                Path.GetFileName(inputFile), scheme.OverlapFactor, scheme.Windows.Count, scheme.Bins.Count));
        }

        private static void LogSummary(string inputFile, DemuxResult result, DemuxParams parameters,
            double demuxSeconds, double parseSeconds, PipelineContext ctx)
        {
            var scheme = result.Scheme;
            var stats = result.Statistics;
            double minWidth = scheme.Bins.Min(b => b.Width);
            double maxWidth = scheme.Bins.Max(b => b.Width);
            ctx.LogInfo(string.Format(
                "Demultiplexing {0}: {1}-fold overlap, {2} windows into {3} bins of {4:F3}-{5:F3} Th",
                Path.GetFileName(inputFile), scheme.OverlapFactor, scheme.Windows.Count,
                scheme.Bins.Count, minWidth, maxWidth));
            double channels = Math.Max(1, stats.Channels);
            ctx.LogInfo(string.Format(
                "  {0:N0} MS2 spectra -> {1:N0}; {2:N0} channel solves ({3:P1} zero, {4:P1} unconstrained, " +
                "{5:P1} active set, {6:N0} at the iteration cap) over {7} block geometries",
                stats.SpectraIn, stats.SpectraOut, stats.Channels, stats.ZeroSolves / channels,
                stats.UnconstrainedSolves / channels, stats.ActiveSetSolves / channels,
                stats.IterationCapSolves, stats.Geometries));
            // The timing gate: demux against the parse it follows decides whether the solver
            // needs more than scalar code.
            string parse = double.IsNaN(parseSeconds)
                ? @"no parse (cache hit)"
                : string.Format(@"parse {0:F1}s, ratio {1:F2}", parseSeconds, demuxSeconds / Math.Max(parseSeconds, 1e-9));
            ctx.LogInfo(string.Format("  Demultiplexed in {0:F1}s on {1} thread(s); {2}; {3}",
                demuxSeconds, parameters.Threads, parse, parameters.Descriptor));
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
