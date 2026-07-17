/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The streaming <see cref="IWindowSpectraProvider"/>: it reads one isolation
    /// window's raw MS2 spectra from a <see cref="SpectraWindowIndex"/> on demand
    /// and applies the MS2 m/z calibration in place, so scoring never materializes
    /// the whole ~6 GB MS2 list -- only the windows being scored concurrently are
    /// resident. The load lives in Osprey.Tasks because it bridges the Osprey.IO
    /// index and the Osprey.Chromatography calibration; the equivalent
    /// <see cref="ResidentWindowSpectraProvider"/> lives in Osprey.Scoring because
    /// it needs neither.
    ///
    /// Output is byte-identical to the resident provider: <see cref="SpectraWindowIndex.LoadWindow"/>
    /// decodes the same bytes the resident cache load decoded, and
    /// <see cref="MzCalibration.ApplyCalibration"/> is the same pure per-m/z
    /// function the resident provider applies. Because each window is a fresh
    /// decode, calibration is done in place (no extra copy) rather than into a new
    /// array as the shared resident list requires.
    /// </summary>
    public class StreamingWindowSpectraProvider : IWindowSpectraProvider
    {
        private readonly SpectraWindowIndex _index;
        private readonly MzCalibrationResult _ms2Calibration;

        public StreamingWindowSpectraProvider(SpectraWindowIndex index, MzCalibrationResult ms2Calibration)
        {
            _index = index;
            _ms2Calibration = ms2Calibration;
        }

        public IReadOnlyList<double> Ms2RetentionTimes { get { return _index.AllMs2Rts; } }

        public List<Spectrum> GetCalibratedWindow(int windowKey)
        {
            // Fresh, uncalibrated decode of this window's spectra from disk.
            var windowSpectra = _index.LoadWindow(windowKey);
            if (_ms2Calibration.Calibrated)
            {
                // These arrays are freshly decoded and owned solely by this list,
                // so calibrate in place -- same values the resident provider writes
                // into a new array, without the copy.
                foreach (var s in windowSpectra)
                {
                    double[] mzs = s.Mzs;
                    for (int i = 0; i < mzs.Length; i++)
                        mzs[i] = MzCalibration.ApplyCalibration(mzs[i], _ms2Calibration);
                }
            }
            return windowSpectra;
        }
    }
}
