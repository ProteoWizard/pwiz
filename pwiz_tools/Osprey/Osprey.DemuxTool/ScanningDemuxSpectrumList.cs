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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using pwiz.Osprey.Demux;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>
    /// The scanning-quadrupole demultiplexer (<see cref="ScanningDemuxPipeline"/>) as a spectrum list: each output
    /// spectrum is built on the acquired spectrum the pipeline names, its window and peaks replaced and marked
    /// centroid. It is also the pipeline's source, and keeps the headers of the spectra read for demultiplexing, so
    /// a layout spectrum's header is not decoded, and centroided by the vendor reader, a second time.
    /// </summary>
    internal sealed class ScanningDemuxSpectrumList : SpectrumListWrapper, IDemuxSource
    {
        private readonly ScanningDemuxOptions _options;
        private readonly PwizDemuxSource _describe;
        private readonly ScanningDemuxPipeline _pipeline;
        private readonly object _lock = new object();
        private readonly Dictionary<int, Spectrum> _reading = new Dictionary<int, Spectrum>(); // read, points not yet released
        private readonly Dictionary<int, Spectrum> _headers = new Dictionary<int, Spectrum>(); // by acquired index, peaks dropped

        public ScanningDemuxSpectrumList(ISpectrumList inner, ScanningKernel kernel, ScanningDemuxOptions options,
            TextWriter log)
            : base(inner)
        {
            _options = options;
            _describe = new PwizDemuxSource(inner);
            _pipeline = new ScanningDemuxPipeline(this, kernel, options, log);
        }

        public int CycleCount
        {
            get { return _pipeline.CycleCount; }
        }

        public int FirstCycle
        {
            get { return _pipeline.FirstCycle; }
        }

        public int LastCycle
        {
            get { return _pipeline.LastCycle; }
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
                Id = identity.Id,
                SpotId = identity.SpotId,
                SourceFilePosition = identity.SourceFilePosition,
            };
        }

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            int baseIndex = _pipeline.BaseIndex(index);
            if (_pipeline.IsSurvey(index))
            {
                var survey = Inner.GetSpectrum(baseIndex, getBinaryData);
                survey.Index = index;
                if (getBinaryData && _options.JointMs1)
                {
                    var centroids = _pipeline.CentroidSurvey(survey.GetMZArray()?.Data, survey.GetIntensityArray()?.Data);
                    if (centroids.HasValue)
                    {
                        survey.SetMZIntensityArrays(centroids.Value.Mz, centroids.Value.Counts, CVID.MS_number_of_detector_counts);
                        MarkCentroid(survey);
                    }
                }
                return survey;
            }

            // A layout spectrum's peaks are replaced. Its header is the one kept when its bin was read for
            // demultiplexing: asking the source again decodes the spectrum, and centroids it, a second time.
            Spectrum spectrum = null;
            (double[] Mz, double[] Ions) peaks = default;
            if (getBinaryData)
            {
                peaks = _pipeline.Peaks(index);
                spectrum = TakeHeader(baseIndex);
            }
            spectrum ??= Inner.GetSpectrum(baseIndex);
            spectrum.Index = index;
            MarkCentroid(spectrum);

            _pipeline.WindowOf(index, out double lo, out double hi);
            var window = spectrum.Precursors[0].IsolationWindow;
            window.Set(CVID.MS_isolation_window_target_m_z, 0.5 * (lo + hi), CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_lower_offset, 0.5 * (hi - lo), CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_upper_offset, 0.5 * (hi - lo), CVID.MS_m_z);
            if (!getBinaryData)
                return spectrum;
            var counts = new double[peaks.Ions.Length];
            for (int i = 0; i < peaks.Ions.Length; i++)
                counts[i] = peaks.Ions[i] * _options.CountsPerIon;
            spectrum.SetMZIntensityArrays(peaks.Mz, counts, CVID.MS_number_of_detector_counts);
            return spectrum;
        }

        int IDemuxSource.Count
        {
            get { return _describe.Count; }
        }

        string IDemuxSource.NativeId(int index)
        {
            return _describe.NativeId(index);
        }

        int IDemuxSource.MsLevel(int index)
        {
            return _describe.MsLevel(index);
        }

        double IDemuxSource.IsolationTarget(int index)
        {
            return _describe.IsolationTarget(index);
        }

        void IDemuxSource.Read(int index, out IReadOnlyList<double> mz, out IReadOnlyList<double> intensity)
        {
            var spectrum = Inner.GetSpectrum(index, true);
            mz = spectrum.GetMZArray()?.Data;
            intensity = spectrum.GetIntensityArray()?.Data;
            lock (_lock)
                _reading[index] = spectrum;
        }

        void IDemuxSource.Release(int index)
        {
            lock (_lock)
            {
                if (!_reading.Remove(index, out var spectrum))
                    return;
                spectrum.BinaryDataArrays.Clear();
                spectrum.IntegerDataArrays.Clear();
                if (_pipeline.IsLayoutBase(index))
                    _headers[index] = spectrum;
            }
        }

        void IDemuxSource.Forget(int index)
        {
            lock (_lock)
                _headers.Remove(index);
        }

        /// <summary>
        /// The header of an acquired spectrum read for demultiplexing, its peaks dropped; each is handed out
        /// once, so no caller shares one. Null if it was not read, or was already taken.
        /// </summary>
        private Spectrum TakeHeader(int innerIndex)
        {
            lock (_lock)
                return _headers.Remove(innerIndex, out var header) ? header : null;
        }

        /// <summary>
        /// Labels a layout spectrum centroid: its peaks are the demultiplexed centroids, whether the source
        /// was centroided or, for the joint solve, profile.
        /// </summary>
        private static void MarkCentroid(Spectrum spectrum)
        {
            var terms = spectrum.Params;
            for (int i = terms.CVParams.Count - 1; i >= 0; i--)
            {
                if (terms.CVParams[i].Cvid == CVID.MS_profile_spectrum)
                    terms.CVParams.RemoveAt(i);
            }
            foreach (var group in terms.ParamGroups.ToList())
            {
                if (group.CVParams.All(p => p.Cvid != CVID.MS_profile_spectrum))
                    continue;
                // The term comes from a shared group: copy the group's other terms onto the spectrum.
                foreach (var p in group.CVParams.Where(p => p.Cvid != CVID.MS_profile_spectrum && !terms.HasCVParam(p.Cvid)))
                    terms.CVParams.Add(p);
                terms.UserParams.AddRange(group.UserParams);
                terms.ParamGroups.Remove(group);
            }
            terms.Set(CVID.MS_centroid_spectrum);
        }
    }
}
