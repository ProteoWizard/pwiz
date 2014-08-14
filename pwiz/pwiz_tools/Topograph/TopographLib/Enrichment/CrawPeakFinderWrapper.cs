/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Crawdad;

namespace pwiz.Topograph.Enrichment
{
    public class CrawPeakFinderWrapper
    {
        private readonly CrawdadPeakFinder _crawdadPeakFinder;
        private const int MinToleranceLen = 4;
        private const int MinToleranceSmoothFwhm = 3;
        private const float FractionFwhmLen = 0.5F;
        private const float DescentTol = 0.005f;
        private const float AscentTol = 0.50f;
// ReSharper disable NotAccessedField.Local
        // TODO(nicksh): Use these times.
        private IList<double> _times;
// ReSharper restore NotAccessedField.Local
        private IList<double> _intensities;

        public CrawPeakFinderWrapper()
        {
            _crawdadPeakFinder = new CrawdadPeakFinder();
        }

        public void SetChromatogram(IList<double> times, IList<double> intensities)
        {
            _times = times;
            _intensities = intensities;
            _crawdadPeakFinder.SetChromatogram(times, intensities);
        }

        public List<CrawdadPeak> CalcPeaks(int maxPeaks)
        {
            var result = new List<CrawdadPeak>();
            foreach (var crawdadPeak in _crawdadPeakFinder.CalcPeaks(maxPeaks))
            {
                Extend(crawdadPeak);
                result.Add(crawdadPeak);
            }
            return result;
        }

        private void Extend(CrawdadPeak crawdadPeak)
        {
            // Look a number of steps dependent on the width of the peak, since interval width
            // may vary.
            int toleranceLen = Math.Max(MinToleranceLen, (int)Math.Round(crawdadPeak.Fwhm * FractionFwhmLen));

            crawdadPeak.ResetBoundaries(ExtendBoundary(crawdadPeak, crawdadPeak.StartIndex, -1, toleranceLen),
                ExtendBoundary(crawdadPeak, crawdadPeak.EndIndex, 1, toleranceLen));
        }

        private int ExtendBoundary(CrawdadPeak peakPrimary, int indexBoundary, int increment, int toleranceLen)
        {
            if (peakPrimary.Fwhm >= MinToleranceSmoothFwhm)
            {
                indexBoundary = ExtendBoundary(peakPrimary, false, indexBoundary, increment, toleranceLen);
            }
            // TODO:
            // Because smoothed data can have a tendency to reach baseline one
            // interval sooner than the raw data, do a final check to choose the
            // boundary correctly for the raw data.
            //indexBoundary = RetractBoundary(peakPrimary, true, indexBoundary, -increment);
            //indexBoundary = ExtendBoundary(peakPrimary, true, indexBoundary, increment, toleranceLen);
            return indexBoundary;
        }

        private int ExtendBoundary(CrawdadPeak peakPrimary, bool useRaw, int indexBoundary, int increment, int toleranceLen)
        {
            var intensities = _intensities;
            int lenIntensities = intensities.Count;
            var boundaryIntensity = intensities[indexBoundary];
            var maxIntensity = boundaryIntensity;
            // Look for a descent proportional to the height of the peak.  Because, SRM data is
            // so low noise, just looking for any descent can lead to boundaries very far away from
            // the peak.
            float height = peakPrimary.Height;
            double minDescent = height * DescentTol;
            // Put a limit on how high intensity can go before the search is terminated
            double maxHeight = ((height - boundaryIntensity) * AscentTol) + boundaryIntensity;

            // Extend the index in the direction of the increment
            for (int i = indexBoundary + increment;
                 i > 0 && i < lenIntensities - 1 && Math.Abs(indexBoundary - i) < toleranceLen;
                 i += increment)
            {
                double maxIntensityCurrent = intensities[i];

                // If intensity goes above the maximum, stop looking
                if (maxIntensityCurrent > maxHeight)
                    break;

                // If descent greater than tolerance, step until it no longer is
                while (maxIntensity - maxIntensityCurrent > minDescent)
                {
                    indexBoundary += increment;
                    if (indexBoundary == i)
                        maxIntensity = maxIntensityCurrent;
                    else
                        maxIntensityCurrent = intensities[indexBoundary];
                }
            }

            return indexBoundary;
        }

    }
}
