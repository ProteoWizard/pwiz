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
using Pwiz.Analysis.PeakPicking;
using pwiz.Osprey.Demux;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>
    /// <see cref="EventCentroider"/> as a pwiz-sharp peak detector, so <c>SpectrumList_PeakPicker</c>
    /// can centroid profile spectra with it and mark them centroided.
    /// </summary>
    internal sealed class EventPeakDetector : IPeakDetector
    {
        public string Name => @"event centroiding";

        public void Detect(IReadOnlyList<double> x, IReadOnlyList<double> y, List<double> xPeakValues,
            List<double> yPeakValues, List<Peak> peaks = null)
        {
            EventCentroider.Centroid(x, y, xPeakValues, yPeakValues);
        }
    }
}
