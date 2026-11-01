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

using System.Diagnostics;
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
        private readonly StaggeredDemuxPipeline _pipeline;
        private readonly bool _centroided; // the demultiplexed spectra are centroids, whatever the input was
        private long _sourceTicks, _peaksTicks; // with the solve profile: the acquired spectra read, and the peaks got
        // The acquired spectrum the last demultiplexed output was built on, as read (metadata only), and its index.
        private Spectrum _acquired;
        private int _acquiredIndex = -1;

        public StaggeredDemuxSpectrumList(ISpectrumList inner, ScanningDemuxOptions options, TextWriter log)
            : base(inner)
        {
            _pipeline = new StaggeredDemuxPipeline(new PwizDemuxSource(inner), options, log);
            // The joint solve writes centroids from the profile: its fitted peaks, or Centrix's of each bin's share.
            // Its shares written as profile (--joint-profile) stay profile, and the other solves' output follows
            // their input.
            _centroided = options.Joint && (!options.JointApportion || options.JointCentroid);
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

        public double IonsUnexplained
        {
            get { return _pipeline.IonsUnexplained; }
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

        /// <summary>With the solve profile: seconds spent reading each output spectrum's acquired spectrum from the source.</summary>
        public double SourceSeconds
        {
            get { return _sourceTicks / (double)Stopwatch.Frequency; }
        }

        /// <summary>
        /// With the solve profile: seconds spent getting each output spectrum's peaks, the batches' solves waited for and
        /// laid out included.
        /// </summary>
        public double PeaksSeconds
        {
            get { return _peaksTicks / (double)Stopwatch.Frequency; }
        }

        /// <summary>
        /// An output spectrum. The demultiplexed outputs of one acquired spectrum come one after another, and when they are
        /// centroids its metadata is read once for them all (reading it costs more than the rest of writing a spectrum):
        /// each is a shallow copy of it, sharing the parts it rewrites (the isolation window and the peak arrays,
        /// overwritten by the next), so a caller must be done with one before it asks for the next, as the mzML writer is.
        /// Profile outputs are read each on its own: a peak picker after this list marks the one it centroids, which
        /// in a shared copy would mark its siblings centroided before they were picked.
        /// </summary>
        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            bool passThrough = _pipeline.IsPassThrough(index);
            int baseIndex = _pipeline.BaseIndex(index);
            long t0 = JointDemuxProfile.Enabled ? Stopwatch.GetTimestamp() : 0;
            Spectrum spectrum;
            if (passThrough || !_centroided)
            {
                // A demultiplexed spectrum's peaks are replaced, so only a pass-through reads the acquired peaks.
                spectrum = Inner.GetSpectrum(baseIndex, getBinaryData && passThrough);
            }
            else
            {
                if (baseIndex != _acquiredIndex)
                {
                    _acquired = Inner.GetSpectrum(baseIndex);
                    _acquiredIndex = baseIndex;
                }
                spectrum = _acquired.ShallowCopy();
                // Not the arrays a previous output left in the shared list.
                spectrum.BinaryDataArrays.Clear();
            }
            if (JointDemuxProfile.Enabled)
                _sourceTicks += Stopwatch.GetTimestamp() - t0;
            spectrum.Index = index;
            if (passThrough)
                return spectrum;
            spectrum.Id = OutputId(spectrum.Id, index);
            var demuxBin = _pipeline.BinOf(index);
            var window = spectrum.Precursors[0].IsolationWindow;
            window.Set(CVID.MS_isolation_window_target_m_z, demuxBin.Center, CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_lower_offset, demuxBin.Width / 2, CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_upper_offset, demuxBin.Width / 2, CVID.MS_m_z);
            if (_centroided)
                ScanningDemuxSpectrumList.MarkCentroid(spectrum);
            if (!getBinaryData)
                return spectrum;
            long t1 = JointDemuxProfile.Enabled ? Stopwatch.GetTimestamp() : 0;
            var (mz, ions) = _pipeline.Peaks(index);
            if (JointDemuxProfile.Enabled)
                _peaksTicks += Stopwatch.GetTimestamp() - t1;
            var counts = new double[ions.Length];
            double perIon = _pipeline.IntensityPerIon(index);
            for (int i = 0; i < ions.Length; i++)
                counts[i] = ions[i] * perIon;
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
