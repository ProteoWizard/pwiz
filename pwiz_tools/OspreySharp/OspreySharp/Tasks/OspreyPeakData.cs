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
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Reusable <see cref="IOspreyDetailedPeakData"/> adapter over the harness's
    /// per-candidate scoring state. One instance is created per window and
    /// <see cref="Set"/> is called for each candidate, so the per-candidate
    /// scoring loop allocates no peak-data objects. Windows are scored on separate
    /// threads, so each window owns its own instance -- this is never shared task
    /// state.
    /// </summary>
    internal sealed class OspreyPeakData : IOspreyDetailedPeakData
    {
        private LibraryEntry _candidate;
        private XICPeakBounds _peakBounds;
        private IReadOnlyList<XicData> _xics;
        private double _apexRetentionTime;
        private double _expectedRt;

        public void Set(LibraryEntry candidate, XICPeakBounds peakBounds, IReadOnlyList<XicData> xics,
            double apexRetentionTime, double expectedRt)
        {
            _candidate = candidate;
            _peakBounds = peakBounds;
            _xics = xics;
            _apexRetentionTime = apexRetentionTime;
            _expectedRt = expectedRt;
        }

        public LibraryEntry Candidate { get { return _candidate; } }
        public XICPeakBounds PeakBounds { get { return _peakBounds; } }
        public double ApexRetentionTime { get { return _apexRetentionTime; } }
        public double ExpectedRt { get { return _expectedRt; } }
        public IReadOnlyList<XicData> Xics { get { return _xics; } }
    }
}
