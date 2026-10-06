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
using System.Globalization;
using System.IO;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// The <see cref="DemuxEngine.weighted"/> engine for a run's MS2 spectra in memory: the stepped staggered scheme
    /// on <see cref="StaggeredDemuxPipeline"/>, as <c>Osprey.DemuxTool --scheme staggered</c> runs it on a file.
    /// </summary>
    internal static class WeightedDemultiplexer
    {
        /// <summary>
        /// Demultiplexes MS2 spectra given in acquisition order, each into one spectrum per bin its window covers,
        /// keeping its scan number and retention time.
        /// </summary>
        public static DemuxResult Run(IReadOnlyList<Spectrum> ms2Spectra, DemuxParams parameters)
        {
            if (!parameters.ChannelToleranceIsPpm)
                throw new NotSupportedException(@"The weighted demultiplexer matches fragment channels in ppm only.");
            var options = CreateOptions(parameters);
            var pipeline = new StaggeredDemuxPipeline(new SpectraSource(ms2Spectra), options, TextWriter.Null,
                parameters.MinimumBinWidth);
            var spectra = new List<Spectrum>(pipeline.OutputCount);
            for (int index = 0; index < pipeline.OutputCount; index++)
            {
                var target = ms2Spectra[pipeline.BaseIndex(index)];
                var bin = pipeline.BinOf(index);
                var (mz, ions) = pipeline.Peaks(index);
                var intensities = new float[ions.Length];
                for (int i = 0; i < ions.Length; i++)
                    intensities[i] = (float)(ions[i] * options.CountsPerIon);
                spectra.Add(new Spectrum
                {
                    ScanNumber = target.ScanNumber,
                    RetentionTime = target.RetentionTime,
                    PrecursorMz = bin.Center,
                    IsolationWindow = bin.ToIsolationWindow(),
                    Mzs = mz,
                    Intensities = intensities,
                });
            }
            var statistics = new DemuxStatistics
            {
                SpectraIn = ms2Spectra.Count,
                SpectraOut = spectra.Count,
                Channels = pipeline.Channels,
                ChannelsSolved = pipeline.ChannelsSolved,
                IonsIn = pipeline.IonsIn,
                IonsPassedThrough = pipeline.IonsPassedThrough,
            };
            return new DemuxResult(pipeline.Scheme, spectra, statistics);
        }

        /// <summary>The pipeline settings for a run's demux settings: the tool's staggered defaults otherwise.</summary>
        public static ScanningDemuxOptions CreateOptions(DemuxParams parameters)
        {
            var options = new ScanningDemuxOptions
            {
                Threads = Math.Max(1, parameters.Threads),
                CountsPerIon = parameters.CountsPerIon,
            };
            options.Parameters.ChannelTolerancePpm = parameters.ChannelTolerance;
            return options;
        }

        /// <summary>The settings of the staggered solve that change its output, for the demux cache's descriptor.</summary>
        public static string Descriptor(ScanningDemuxOptions options)
        {
            var ic = CultureInfo.InvariantCulture;
            var p = options.Parameters;
            return string.Format(ic,
                @"block_cycles={0};cycle_pad={1};group_bins={2};min_channel_ions={3};min_channel_cells={4};" +
                @"poisson_weights={5};weight_floor_ions={6};min_output_ions={7}",
                options.BlockCycles, options.CyclePad, options.GroupBins, p.MinChannelIons.ToString(@"R", ic),
                p.MinChannelCells, p.PoissonWeights, p.WeightFloorIons.ToString(@"R", ic),
                p.MinOutputIons.ToString(@"R", ic));
        }

        /// <summary>A run's MS2 spectra in memory as a demultiplexing source.</summary>
        private sealed class SpectraSource : IDemuxSource
        {
            private readonly IReadOnlyList<Spectrum> _spectra;

            public SpectraSource(IReadOnlyList<Spectrum> spectra)
            {
                _spectra = spectra;
            }

            public int Count
            {
                get { return _spectra.Count; }
            }

            public string NativeId(int index)
            {
                return _spectra[index].ScanNumber.ToString(CultureInfo.InvariantCulture);
            }

            public DemuxSpectrumInfo Describe(int index)
            {
                var spectrum = _spectra[index];
                return new DemuxSpectrumInfo(2, true, spectrum.IsolationWindow, spectrum.RetentionTime);
            }

            public void Read(int index, out IReadOnlyList<double> mz, out IReadOnlyList<double> intensity)
            {
                var spectrum = _spectra[index];
                var values = new double[spectrum.Intensities.Length];
                for (int i = 0; i < values.Length; i++)
                    values[i] = spectrum.Intensities[i];
                mz = spectrum.Mzs;
                intensity = values;
            }

            public void Release(int index)
            {
            }

            public void Forget(int index)
            {
            }
        }
    }
}
