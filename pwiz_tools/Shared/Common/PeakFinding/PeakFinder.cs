/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.Common.PeakFinding
{
    internal class PeakFinder : IPeakFinder
    {
        // ReSharper disable NotAccessedField.Local
        private IList<float> _times;
        // ReSharper restore NotAccessedField.Local
        private IList<float> _intensities;
        private float _baselineIntensity;
        void IDisposable.Dispose()
        {
        }

        public void SetChromatogram(IList<float> times, IList<float> intensities)
        {
            _times = times;
            _intensities = intensities;
            if (_intensities.Count > 0)
            {
                _baselineIntensity = _intensities.Min();
            }
            else
            {
                _baselineIntensity = 0;
            }
        }

        public IFoundPeak GetPeak(int startIndex, int endIndex)
        {
            return new FoundPeak(0, _intensities, _baselineIntensity, startIndex, endIndex);
        }

        public IList<IFoundPeak> CalcPeaks(int max, int[] idIndices)
        {
            PeakAndValleyFinder peakAndValleyFinder = new PeakAndValleyFinder(_intensities);
            var allPeaks = new List<FoundPeak>();
            foreach (var startEnd in peakAndValleyFinder.FindPeaks())
            {
                if (startEnd.Key < _intensities.Count - 1 && startEnd.Value > 0)
                {
                    var peak = new FoundPeak(peakAndValleyFinder._widthDataWings, _intensities, _baselineIntensity, startEnd.Key, startEnd.Value);
                    double rheight = peak.Height / peak.RawHeight;
                    double rarea = peak.Area / peak.RawArea;
                    if (rheight > 0.02 && rarea > 0.02)
                    {
                        peak.Identified = idIndices.Any(idx => idx >= peak.StartIndex && idx <= peak.EndIndex);
                        allPeaks.Add(peak);
                    }
                }
            }
            if (max == -1)
            {
                return allPeaks.Cast<IFoundPeak>().ToArray();
            }
            allPeaks.Sort(ComparePeakIdentifiedArea);
            return allPeaks.Take(max).Cast<IFoundPeak>().ToArray();
        }

        private int ComparePeakIdentifiedArea(FoundPeak peak1, FoundPeak peak2)
        {
            if (peak1.Identified)
            {
                if (!peak2.Identified)
                {
                    return -1;
                }
            }
            else if (peak2.Identified)
            {
                return 1;
            }
            return -peak1.Area.CompareTo(peak2.Area);
        }

        public bool IsHeightAsArea { get { return false; } }

    }
}
