/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Reusable <see cref="IOspreyApexSpectraPeakData"/> adapter over the harness's
    /// per-candidate scoring state. It implements the widest peak-data tier; each
    /// calculator receives it narrowed to the tier its family declares. One instance
    /// is created per window and <see cref="Set"/> is called for each candidate, so the
    /// per-candidate scoring loop allocates no peak-data objects. Windows are scored on
    /// separate threads, so each window owns its own instance -- this is never shared
    /// task state.
    ///
    /// Lives in Scoring (alongside the peak-data tier seam it implements and the
    /// feature calculators that consume it); relocated out of the Tasks layer so the
    /// coelution scorer can move down here too.
    /// </summary>
    public sealed class OspreyPeakData : IOspreyApexSpectraPeakData
    {
        private LibraryEntry _candidate;
        private XICPeakBounds _peakBounds;
        private IReadOnlyList<XicData> _xics;
        private double _apexRetentionTime;
        private double _expectedRt;
        private Spectrum _apexSpectrum;
        private int _apexGlobalIndex;
        private int _apexLocalIndex;
        private int _windowStartIndex;
        private int _windowLength;
        private IReadOnlyList<Spectrum> _windowSpectra;
        private XicData _ms1PrecursorXic;
        private XicData _ms1ReferenceXic;
        private double[] _apexIsotopeEnvelope;

        public void Set(LibraryEntry candidate, XICPeakBounds peakBounds, IReadOnlyList<XicData> xics,
            double apexRetentionTime, double expectedRt, Spectrum apexSpectrum,
            int apexGlobalIndex, int apexLocalIndex, int windowStartIndex, int windowLength,
            IReadOnlyList<Spectrum> windowSpectra)
        {
            _candidate = candidate;
            _peakBounds = peakBounds;
            _xics = xics;
            _apexRetentionTime = apexRetentionTime;
            _expectedRt = expectedRt;
            _apexSpectrum = apexSpectrum;
            _apexGlobalIndex = apexGlobalIndex;
            _apexLocalIndex = apexLocalIndex;
            _windowStartIndex = windowStartIndex;
            _windowLength = windowLength;
            _windowSpectra = windowSpectra;
            // Reset the produced MS1 data each candidate; SetMs1 overrides it when
            // the extractor produced it (HRAM + MS1 present). Without this reset the
            // reused instance would leak the previous candidate's MS1 chromatograms.
            _ms1PrecursorXic = null;
            _ms1ReferenceXic = null;
            _apexIsotopeEnvelope = null;
        }

        /// <summary>
        /// Publish the MS1 data the extractor produced for this candidate (the
        /// precursor chromatogram, its co-sampled reference fragment chromatogram,
        /// and the apex isotope envelope). Called only when the run has MS1 features
        /// and an MS1 scan was found; otherwise the <see cref="Set"/> reset leaves
        /// all three null and the MS1 features evaluate to 0.0.
        /// </summary>
        public void SetMs1(XicData ms1PrecursorXic, XicData ms1ReferenceXic, double[] apexIsotopeEnvelope)
        {
            _ms1PrecursorXic = ms1PrecursorXic;
            _ms1ReferenceXic = ms1ReferenceXic;
            _apexIsotopeEnvelope = apexIsotopeEnvelope;
        }

        public LibraryEntry Candidate { get { return _candidate; } }
        public XICPeakBounds PeakBounds { get { return _peakBounds; } }
        public double ApexRetentionTime { get { return _apexRetentionTime; } }
        public double ExpectedRt { get { return _expectedRt; } }
        public IReadOnlyList<XicData> Xics { get { return _xics; } }
        public Spectrum ApexSpectrum { get { return _apexSpectrum; } }
        public int ApexGlobalIndex { get { return _apexGlobalIndex; } }

        public bool TryGetApexOffsetSpectrum(int offset, out Spectrum spectrum, out int cacheIndex)
        {
            // candidate-local index within the scoring range; the window-spectrum
            // list and the start/length come from the per-candidate Set. Out-of-range
            // offsets (window edges) return false -- the asymmetric boundary skip.
            int candIdx = _apexLocalIndex + offset;
            if (candIdx < 0 || candIdx >= _windowLength)
            {
                spectrum = null;
                cacheIndex = -1;
                return false;
            }
            cacheIndex = _windowStartIndex + candIdx;
            spectrum = _windowSpectra[cacheIndex];
            return true;
        }

        public XicData Ms1PrecursorXic { get { return _ms1PrecursorXic; } }
        public XicData Ms1ReferenceXic { get { return _ms1ReferenceXic; } }
        public double[] ApexIsotopeEnvelope { get { return _apexIsotopeEnvelope; } }
    }
}
