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

        /// <summary>
        /// Returns the sum of the intensities across all of the MS2 chromatograms at a particular time point.
        /// </summary>
        /// <param name="time">The time point to look at in the chromatograms</param>
        /// <param name="timeIndexHint">The index of the time in the TimeIntensities.Times list, to avoid having to do a binary search</param>
        /// <returns>Sum of intensities</returns>
        public double GetTotalMs2IntensityAtTime(float time, int timeIndexHint)
        {
            double totalIntensity = 0;
            foreach (var peakIntegrator in PeakIntegrators)
            {
                if (peakIntegrator.ChromSource != ChromSource.fragment)
                {
                    continue;
                }

                var timeIntensities = peakIntegrator.RawTimeIntensities ?? peakIntegrator.InterpolatedTimeIntensities;
                float intensity;
                if (timeIndexHint >= 0 && timeIndexHint < timeIntensities.NumPoints && timeIntensities.Times[timeIndexHint] == time)
                {
                    intensity = timeIntensities.Intensities[timeIndexHint];
                }
                else
                {
                    intensity = timeIntensities.Intensities[timeIntensities.IndexOfNearestTime(time)];
                }

                totalIntensity += intensity;
            }

            return totalIntensity;
        }
    }
}
