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
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using pwiz.Osprey.Demux;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>
    /// A pwiz spectrum list as a demultiplexing source that keeps nothing it reads: for detecting the scheme, and
    /// for the descriptions a spectrum list built on the pipeline passes through.
    /// </summary>
    internal sealed class PwizDemuxSource : IDemuxSource
    {
        private readonly ISpectrumList _spectra;

        public PwizDemuxSource(ISpectrumList spectra)
        {
            _spectra = spectra;
        }

        public int Count
        {
            get { return _spectra.Count; }
        }

        public string NativeId(int index)
        {
            return _spectra.SpectrumIdentity(index).Id;
        }

        public int MsLevel(int index)
        {
            return _spectra.GetSpectrum(index).Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        }

        public double IsolationTarget(int index)
        {
            var spectrum = _spectra.GetSpectrum(index);
            return spectrum.Precursors.Count > 0
                ? spectrum.Precursors[0].IsolationWindow.CvParamValueOrDefault(CVID.MS_isolation_window_target_m_z, 0.0)
                : 0.0;
        }

        public void Read(int index, out IReadOnlyList<double> mz, out IReadOnlyList<double> intensity)
        {
            var spectrum = _spectra.GetSpectrum(index, true);
            mz = spectrum.GetMZArray()?.Data;
            intensity = spectrum.GetIntensityArray()?.Data;
        }

        public void Release(int index)
        {
        }

        public void Forget(int index)
        {
        }
    }
}
