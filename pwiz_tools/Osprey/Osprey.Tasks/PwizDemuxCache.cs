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
using Pwiz.Analysis.Demux;
using Pwiz.Util.Chemistry;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// ProteoWizard's demultiplexer as the <c>--demux auto</c> engine (<c>OSPREY_DEMUX_ENGINE=pwiz</c>),
    /// for comparing Osprey's own demultiplexing (<see cref="DemuxCacheBuilder"/>) against it.
    /// </summary>
    /// <remarks>
    /// The run is read through ProteoWizard with its demultiplexer applied
    /// (<see cref="SpectrumFileReader.LoadAllSpectra(string,CentroidingOptions,SpectrumListDemux.Params)"/>),
    /// so the demultiplexed cache holds the spectra msconvert's <c>demultiplex optimization=overlap_only</c>
    /// filter would write to an mzML, without the mzML. A run ProteoWizard does not detect as multiplexed is
    /// read as acquired into the ordinary <c>.spectra.bin</c>.
    ///
    /// The cache has the same name as Osprey's demultiplexed cache, with this engine's descriptor in its
    /// header, so switching engines rebuilds it, and the descriptor enters every task's validity key. It is
    /// built from the source file: a demultiplexed read is a different read of the run, not a transformation
    /// of the <c>.spectra.bin</c>, so without the source it cannot be rebuilt.
    /// </remarks>
    internal static class PwizDemuxCache
    {
        /// <summary>
        /// Bumped when ProteoWizard's demultiplexer changes the spectra it produces, so every
        /// demultiplexed cache and every score computed from one is rebuilt. Its settings are in
        /// the descriptor already; this covers a change of the algorithm under the same settings.
        /// </summary>
        private const int DESCRIPTOR_VERSION = 1;

        // msconvert's demultiplex default, and the tolerance the overlap demultiplexer bins the
        // peaks of neighboring spectra with.
        private const double MASS_ERROR_PPM = 10;

        /// <summary>
        /// The demultiplexing settings for a run, or null unless this engine is selected. msconvert's
        /// <c>demultiplex optimization=overlap_only massError=10ppm interpolation=makima
        /// poissonWeights=true</c>. Solved on the search's threads shared among the
        /// <paramref name="concurrentReads"/> files being read at once (one, except on
        /// <c>--task SpectraCache</c> lanes): each solve thread holds blocks.
        /// </summary>
        internal static SpectrumListDemux.Params CreateParams(OspreyConfig config, int concurrentReads = 1)
        {
            if (!DemuxCacheBuilder.UsesPwizEngine(config))
                return null;
            // ProteoWizard demultiplexes the spectra its reader returns, so they must be the vendor's
            // centroids: Centrix centroids after the read.
            if (config.CentroidMode != CentroidMode.vendor)
            {
                throw new InvalidOperationException(string.Format(@"OSPREY_DEMUX_ENGINE={0} cannot be used with {1}",
                    DemuxCacheBuilder.PWIZ_ENGINE, OspreyArgNames.Text(OspreyArgNames.CENTROID)));
            }
            return new SpectrumListDemux.Params
            {
                Optimization = SpectrumListDemux.Optimization.OverlapOnly,
                MassError = new MZTolerance(MASS_ERROR_PPM, MZToleranceUnits.Ppm),
                Interpolation = DemuxInterpolation.Makima,
                PoissonWeights = true,
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
                @"variableFill={6};nnlsMaxIter={7};nnlsEps={8};blockExtra={9};minWindowSize={10};removeNonOverlappingEdges={11};" +
                @"interpolation={12};poissonWeights={13}",
                DESCRIPTOR_VERSION, p.MassError.Value.ToString(@"R", ic), p.MassError.Units,
                p.InterpolateRetentionTime, p.ApplyWeighting, p.RegularizeSums, p.VariableFill,
                p.NnlsMaxIter, p.NnlsEps.ToString(@"R", ic), p.DemuxBlockExtra.ToString(@"R", ic),
                p.MinimumWindowSize.ToString(@"R", ic), p.RemoveNonOverlappingEdges, p.Interpolation, p.PoissonWeights);
        }

        /// <summary>
        /// Whether the run a <c>.spectra.bin</c> holds is multiplexed, so that this engine must search
        /// its demultiplexed cache instead: the reason a valid <c>.spectra.bin</c> cannot be searched in
        /// its place. Decided from the isolation windows the cache recorded, by the same ProteoWizard
        /// detection the read uses over the same spectra, so it needs neither the source file nor a
        /// vendor reader. An MSX run is the exception: the cache keeps one window per spectrum, so it is
        /// not recognized here. False unless this engine is selected.
        /// </summary>
        internal static bool IsDemultiplexedRun(SpectraWindowIndex plainIndex, OspreyConfig config)
        {
            if (!DemuxCacheBuilder.UsesPwizEngine(config))
                return false;
            plainIndex.ReadRecordedIsolationWindows(out var centers, out var lowerOffsets, out var upperOffsets);
            return SpectrumFileReader.DetectDemultiplexScheme(centers, lowerOffsets, upperOffsets) != null;
        }

        /// <summary>
        /// <see cref="IsDemultiplexedRun(SpectraWindowIndex,OspreyConfig)"/> for the input's
        /// <c>.spectra.bin</c>, false when there is no usable one: a damaged cache is then
        /// reported where it is read. For the start-up check.
        /// </summary>
        internal static bool IsDemultiplexedRun(string inputFile, OspreyConfig config)
        {
            if (!DemuxCacheBuilder.UsesPwizEngine(config))
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
    }
}
