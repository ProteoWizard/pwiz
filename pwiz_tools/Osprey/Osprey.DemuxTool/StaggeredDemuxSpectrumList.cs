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

using System.Globalization;
using System.IO;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using pwiz.Osprey.Demux;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>
    /// A spectrum list that demultiplexes a stepped staggered acquisition (Orbitrap, Astral) on
    /// <see cref="StaggeredDemuxPipeline"/>: each MS2 spectrum becomes one spectrum per narrow bin
    /// its window covers, under that bin's window, as msconvert's demultiplex filter writes them;
    /// MS1 spectra pass through.
    /// </summary>
    internal sealed class StaggeredDemuxSpectrumList : SpectrumListWrapper
    {
        private readonly ScanningDemuxOptions _options;
        private readonly StaggeredDemuxPipeline _pipeline;

        public StaggeredDemuxSpectrumList(ISpectrumList inner, ScanningDemuxOptions options, TextWriter log)
            : base(inner)
        {
            _options = options;
            _pipeline = new StaggeredDemuxPipeline(new PwizDemuxSource(inner), options, log);
        }

        public long Channels
        {
            get { return _pipeline.Channels; }
        }

        public long ChannelsSolved
        {
            get { return _pipeline.ChannelsSolved; }
        }

        public double IonsIn
        {
            get { return _pipeline.IonsIn; }
        }

        public double IonsPassedThrough
        {
            get { return _pipeline.IonsPassedThrough; }
        }

        public override int Count
        {
            get { return _pipeline.OutputCount; }
        }

        public override SpectrumIdentity SpectrumIdentity(int index)
        {
            var identity = Inner.SpectrumIdentity(_pipeline.BaseIndex(index));
            return new SpectrumIdentity
            {
                Index = index,
                Id = OutputId(identity.Id, index),
                SpotId = identity.SpotId,
                SourceFilePosition = identity.SourceFilePosition,
            };
        }

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            bool passThrough = _pipeline.IsPassThrough(index);
            // A demultiplexed spectrum's peaks are replaced, so only a pass-through reads the acquired peaks.
            var spectrum = Inner.GetSpectrum(_pipeline.BaseIndex(index), getBinaryData && passThrough);
            spectrum.Index = index;
            if (passThrough)
                return spectrum;
            spectrum.Id = OutputId(spectrum.Id, index);
            var demuxBin = _pipeline.BinOf(index);
            var window = spectrum.Precursors[0].IsolationWindow;
            window.Set(CVID.MS_isolation_window_target_m_z, demuxBin.Center, CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_lower_offset, demuxBin.Width / 2, CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_upper_offset, demuxBin.Width / 2, CVID.MS_m_z);
            if (!getBinaryData)
                return spectrum;
            var (mz, ions) = _pipeline.Peaks(index);
            var counts = new double[ions.Length];
            for (int i = 0; i < ions.Length; i++)
                counts[i] = ions[i] * _options.CountsPerIon;
            spectrum.SetMZIntensityArrays(mz, counts, CVID.MS_number_of_detector_counts);
            return spectrum;
        }

        private string OutputId(string id, int index)
        {
            if (_pipeline.IsPassThrough(index))
                return id;
            return id + @" demux=" + _pipeline.PartOf(index).ToString(CultureInfo.InvariantCulture);
        }
    }
}
