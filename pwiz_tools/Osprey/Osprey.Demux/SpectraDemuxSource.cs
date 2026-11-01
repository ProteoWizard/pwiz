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
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// A run's spectra in memory as a demultiplexing source: its MS2 spectra in acquisition order, each with its fill
    /// time and ion response when known (<see cref="SpectrumAcquisition"/>), and its MS1 spectra when the scheme needs
    /// them (a ZT Scan cycle begins with one), merged with the MS2 by scan number. The staggered engine's source in
    /// Osprey's search, and the tests' source for a run read from a file.
    /// </summary>
    internal sealed class SpectraDemuxSource : IDemuxSource
    {
        private readonly IReadOnlyList<Spectrum> _ms2;
        private readonly IReadOnlyList<SpectrumAcquisition> _acquisitions;
        private readonly IReadOnlyList<MS1Spectrum> _ms1;
        private readonly int[] _order;   // by source index: an MS2 index, or ~(an MS1 index); null for the MS2 alone

        public SpectraDemuxSource(IReadOnlyList<Spectrum> ms2, IReadOnlyList<SpectrumAcquisition> acquisitions = null,
            IReadOnlyList<MS1Spectrum> ms1 = null)
        {
            if (acquisitions != null && acquisitions.Count != ms2.Count)
                throw new ArgumentException(@"One acquisition per spectrum is required.", nameof(acquisitions));
            _ms2 = ms2;
            _acquisitions = acquisitions;
            _ms1 = ms1;
            if (ms1 == null || ms1.Count == 0)
                return;
            var scans = ms2.Select((s, i) => (Scan: s.ScanNumber, Code: i))
                .Concat(ms1.Select((s, i) => (Scan: s.ScanNumber, Code: ~i)))
                .OrderBy(e => e.Scan)
                .ToArray();
            _order = scans.Select(e => e.Code).ToArray();
        }

        public int Count
        {
            get { return _order?.Length ?? _ms2.Count; }
        }

        public string NativeId(int index)
        {
            int code = CodeOf(index);
            uint scan = code >= 0 ? _ms2[code].ScanNumber : _ms1[~code].ScanNumber;
            return scan.ToString(CultureInfo.InvariantCulture);
        }

        public DemuxSpectrumInfo Describe(int index)
        {
            int code = CodeOf(index);
            if (code < 0)
                return new DemuxSpectrumInfo(1, false, default, _ms1[~code].RetentionTime);
            var spectrum = _ms2[code];
            if (_acquisitions == null)
                return new DemuxSpectrumInfo(2, true, spectrum.IsolationWindow, spectrum.RetentionTime);
            var acquisition = _acquisitions[code];
            return new DemuxSpectrumInfo(2, true, spectrum.IsolationWindow, spectrum.RetentionTime,
                acquisition.FillTimeMs, acquisition.IonResponse);
        }

        public void Read(int index, out IReadOnlyList<double> mz, out IReadOnlyList<double> intensity)
        {
            int code = CodeOf(index);
            float[] values;
            if (code >= 0)
            {
                mz = _ms2[code].Mzs;
                values = _ms2[code].Intensities;
            }
            else
            {
                mz = _ms1[~code].Mzs;
                values = _ms1[~code].Intensities;
            }
            var widened = new double[values.Length];
            for (int i = 0; i < widened.Length; i++)
                widened[i] = values[i];
            intensity = widened;
        }

        public void Release(int index)
        {
        }

        public void Forget(int index)
        {
        }

        private int CodeOf(int index)
        {
            return _order?[index] ?? index;
        }
    }
}
