/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Information which is shared between PeakIntegrators. When calculating the "Area" of
    /// a peak in a DDA chromatogram, the time point is used which has the greatest total MS2
    /// intensity across all of the other DDA chromatograms.
    /// </summary>
    public class PeakGroupIntegrator
    {
        private List<PeakIntegrator> _peakIntegrators;
        private Dictionary<PeakBounds, MedianPeakShape> _medianChromatograms = new Dictionary<PeakBounds, MedianPeakShape>();
        public PeakGroupIntegrator(FullScanAcquisitionMethod acquisitionMethod, TimeIntervals timeIntervals)
        {
            FullScanAcquisitionMethod = acquisitionMethod;
            TimeIntervals = timeIntervals;
            _peakIntegrators = new List<PeakIntegrator>();
        }

        public FullScanAcquisitionMethod FullScanAcquisitionMethod { get; }

        public TimeIntervals TimeIntervals { get; }

        public IEnumerable<PeakIntegrator> PeakIntegrators
        {
            get { return _peakIntegrators.AsEnumerable(); }
        }

        public void AddPeakIntegrator(PeakIntegrator peakIntegrator)
        {
            _peakIntegrators.Add(peakIntegrator);
        }
        
        public MedianPeakShape GetMedianChromatogram(float startTime, float endTime)
        {
            var key = new PeakBounds(startTime, endTime);
            MedianPeakShape medianChromatogram;
            if (_medianChromatograms.TryGetValue(key, out medianChromatogram))
            {
                return medianChromatogram;
            }
            medianChromatogram = MedianPeakShape.GetMedianPeakShape(startTime, endTime, PeakIntegrators.Select(p => p.RawTimeIntensities ?? p.InterpolatedTimeIntensities)
                .ToList());
            _medianChromatograms[key] = medianChromatogram;
            return medianChromatogram;
        }
    }
}
