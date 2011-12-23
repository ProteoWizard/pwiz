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
using System.Linq;
using System.Text;
using pwiz.Crawdad;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Enrichment
{
    public class PeakFinder
    {
        public bool IsotopesEluteEarlier
        {
            get; set;
        }
        public bool IsotopesEluteLater
        {
            get; set;
        }
        public TracerChromatograms Chromatograms
        {
            get; set;
        }
        public int FirstDetectedScan
        {
            get; set;
        }
        public int LastDetectedScan
        {
            get; set;
        }
        public void FindPeak(out int peakStart, out int peakEnd)
        {
            peakStart = FirstDetectedScan;
            if (LastDetectedScan > FirstDetectedScan + 100)
            {
                peakEnd = FirstDetectedScan;
            }
            else
            {
                peakEnd = LastDetectedScan;
            }
            int nextPeakStart = peakStart;
            int nextPeakEnd = peakEnd;
            foreach (var entry in Chromatograms.Points)
            {
                if (IsotopesEluteLater)
                {
                    peakEnd = nextPeakEnd;
                }
                if (IsotopesEluteEarlier)
                {
                    peakStart = nextPeakStart;
                }
                int chromPeakStart, chromPeakEnd;
                FindPeak(Chromatograms.Times, entry.Value, peakStart, peakEnd, out chromPeakStart, out chromPeakEnd);
                nextPeakStart = Math.Min(nextPeakStart, chromPeakStart);
                nextPeakEnd = Math.Max(nextPeakEnd, chromPeakEnd);
            }
            peakStart = nextPeakStart;
            peakEnd = nextPeakEnd;
        }
        static void FindPeak(IList<double> times, IList<double> intensities, int firstDetectedScan, int lastDetectedScan, 
            out int peakStart, out int peakEnd)
        {
            CrawdadPeakFinder peakFinder = new CrawdadPeakFinder();
            peakFinder.SetChromatogram(times, intensities);
            double minDist = Double.MaxValue;
            var overlappingPeaks = new List<CrawdadPeak>();
            CrawdadPeak closestPeak = null;
            foreach (var peak in peakFinder.CalcPeaks())
            {
                double distance;
                if (peak.EndIndex < firstDetectedScan)
                {
                    distance = firstDetectedScan - peak.EndIndex;
                }
                else if (peak.StartIndex > lastDetectedScan)
                {
                    distance = peak.StartIndex - lastDetectedScan;
                }
                else
                {
                    distance = 0;
                    overlappingPeaks.Add(peak);
                }
                if (distance < minDist)
                {
                    minDist = distance;
                    closestPeak = peak;
                }
            }
            peakStart = firstDetectedScan;
            peakEnd = lastDetectedScan;
            if (closestPeak == null)
            {
                return;
            }
            if (overlappingPeaks.Count == 0)
            {
                overlappingPeaks.Add(closestPeak);
            }
            foreach (var peak in overlappingPeaks)
            {
                peakStart = Math.Min(peakStart, peak.StartIndex);
                peakEnd = Math.Max(peakEnd, peak.EndIndex);
            }
        }

        static int MaxInRange(IList<double> intensities, int first, int last)
        {
            double max = intensities[first];
            int maxIndex = first;
            for (int i = first + 1; i <= last; i++)
            {
                if (intensities[i] > max)
                {
                    maxIndex = i;
                    max = intensities[i];
                }
            }
            return maxIndex;
        }

//        public static void ComputePeak(
//            ChromatogramData chromatogram, 
//            DbPeak peak,
//            double unitBackground)
//        {
//            int startIndex = Array.BinarySearch(chromatogram.ScanIndexes, peak.PeakStart);
//            if (startIndex < 0)
//            {
//                startIndex = ~startIndex;
//            }
//            int endIndex = Array.BinarySearch(chromatogram.ScanIndexes, peak.PeakEnd);
//            {
//                if (endIndex < 0)
//                {
//                    endIndex = ~endIndex;
//                }
//            }
//            double totalArea = 0;
//            for (int iScan = startIndex; iScan <= endIndex; iScan++)
//            {
//                double[] times = chromatogram.Times;
//                var intensities = chromatogram.GetIntensities();
//                int prevScan = Math.Max(0, iScan - 1);
//                int nextScan = Math.Min(times.Length - 1, iScan + 1);
//                double width = (times[nextScan] - times[prevScan]) / (nextScan - prevScan);
//                totalArea += intensities[iScan] * width;
//            }
//            peak.Background = 0;
//            peak.TotalArea = totalArea;
//        }
    }
}
