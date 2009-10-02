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
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Enrichment
{
    public class PeakFinder
    {
        public int MassDelta
        {
            get; set;
        }
        public bool IsotopesEluteEarlier
        {
            get; set;
        }
        public bool IsotopesEluteLater
        {
            get; set;
        }
        public IList<ChromatogramData> Chromatograms
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
            peakEnd = LastDetectedScan;
            int nextPeakStart = peakStart;
            int nextPeakEnd = peakEnd;
            for (int i = 0; i < Chromatograms.Count; i++)
            {
                if (true || i % MassDelta == 0)
                {
                    if (IsotopesEluteLater)
                    {
                        peakEnd = nextPeakEnd;
                    }
                    if (IsotopesEluteEarlier)
                    {
                        peakStart = nextPeakStart;
                    }
                }
                var chromatogram = Chromatograms[i];
                int chromPeakStart, chromPeakEnd;
                FindPeak(chromatogram, peakStart, peakEnd, out chromPeakStart, out chromPeakEnd);
                nextPeakStart = Math.Min(nextPeakStart, chromPeakStart);
                nextPeakEnd = Math.Max(nextPeakEnd, chromPeakEnd);
            }
            peakStart = nextPeakStart;
            peakEnd = nextPeakEnd;
        }
        static void FindPeak(ChromatogramData chromatogramData, int firstDetectedScan, int lastDetectedScan, 
            out int peakStart, out int peakEnd)
        {
            int maxIndex = MaxInRange(chromatogramData.Intensities, firstDetectedScan, lastDetectedScan);
            double baseline = FindBaseline(chromatogramData.Intensities);
            for (peakStart = maxIndex; peakStart > 0; peakStart--)
            {
                if (chromatogramData.Intensities[peakStart] <= baseline)
                {
                    break;
                }
                if (false && !chromatogramData.IsMzAccurate(peakStart))
                {
                    break;
                }
            }
            for (peakEnd = maxIndex; peakEnd < chromatogramData.Intensities.Length - 1; peakEnd++ )
            {
                if (chromatogramData.Intensities[peakEnd] <= baseline)
                {
                    break;
                }
                if (false && !chromatogramData.IsMzAccurate(peakEnd))
                {
                    break;
                }
            }
        }

        static double FindBaseline(IList<double> intensities)
        {
            List<double> maxes = new List<double>();
            int maxCount = 4;
            for (int i = 0; i < intensities.Count; i+= maxCount)
            {
                int maxIndex = MaxInRange(intensities, i, Math.Min(i + maxCount - 1, intensities.Count - 1));
                maxes.Add(intensities[maxIndex]);
            }
            maxes.Sort();
            return maxes[maxes.Count/2];
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

        public static void ComputePeak(
            ChromatogramData chromatogram, 
            DbPeak peak,
            double unitBackground)
        {
            int startIndex = Array.BinarySearch(chromatogram.ScanIndexes, peak.PeakStart);
            if (startIndex < 0)
            {
                startIndex = ~startIndex;
            }
            int endIndex = Array.BinarySearch(chromatogram.ScanIndexes, peak.PeakEnd);
            {
                if (endIndex < 0)
                {
                    endIndex = ~endIndex;
                }
            }
            double totalArea = 0;
            double totalError = 0;
            double background = 0;
            double errorThreshold = Math.Pow(.5, 1.0/(endIndex - startIndex));
            for (int iScan = startIndex; iScan < endIndex; iScan++)
            {
                double[] times = chromatogram.Times;
                int prevScan = Math.Max(0, iScan - 1);
                int nextScan = Math.Min(times.Length - 1, iScan + 1);
                double width = (times[nextScan] - times[prevScan]) / (nextScan - prevScan);
                background += unitBackground*width;
                totalArea += chromatogram.Intensities[iScan] * width;
                double error = Math.Abs(chromatogram.GetMzError(iScan));
                if (error > errorThreshold)
                {
                    totalError += error * chromatogram.Intensities[iScan] * width;
                }
            }
            peak.Background = background;
            peak.TotalArea = totalArea;
            peak.TotalError = totalError;
        }

//        public bool IsAcceptable(int peakStart, int peakEnd)
//        {
//            if (peakStart == peakEnd)
//            {
//                return false;
//            }
//            double maxArea = 0;
//            foreach (var chromatogram in Chromatograms)
//            {
//                maxArea = Math.Max(maxArea, ComputeArea(chromatogram, peakStart, peakEnd, 0, false));
//            }
//            foreach (var chromatogram in Chromatograms)
//            {
//                double areaLenient = ComputeArea(chromatogram, peakStart, peakEnd, 0, false);
//                double areaStrict = ComputeArea(chromatogram, peakStart, peakEnd, 0, true);
//                if (areaLenient - areaStrict <= maxArea / 10)
//                {
//                    continue;
//                }
//                if (areaStrict >= areaLenient / 2)
//                {
//                    continue;
//                }
//                return false;
//            }
//            return true;
//        }
//
        private static int ExtendPeak(double[] intensities, int startIndex, int direction)
        {
            int index = startIndex;
            if (!IsTrending(intensities, index, -direction, true))
            {
                while (IsTrending(intensities, index, direction, true))
                {
                    index += direction;
                }
            }
            while(IsTrending(intensities, index, direction, false))
            {
                index += direction;
            }
            return index;
        }
        private const int trend_count = 3;
        static bool IsTrending(double[] intensities, int startIndex, int direction, bool increase)
        {
            if (startIndex < 0 || startIndex >= intensities.Length)
            {
                return false;
            }
            double compare = intensities[startIndex];
            for (int i = 1; i <= trend_count; i ++)
            {
                int index = startIndex + direction*i;
                if (index < 0 || index >= intensities.Length)
                {
                    return false;
                }
                double intensity = intensities[index];
                if (increase)
                {
                    if (intensity > compare)
                    {
                        return true;
                    }
                }
                else
                {
                    if (intensity < compare)
                    {
                        return true;
                    }
                }
                int oppositeIndex = startIndex - direction*i;
                if (oppositeIndex < 0 || oppositeIndex >= intensities.Length)
                {
                    return false;
                }
                if (increase)
                {
                    compare = Math.Max(compare, intensities[oppositeIndex]);
                }
                else
                {
                    compare = Math.Min(compare, intensities[oppositeIndex]);
                }
            }
            return false;
        }
        public const int background_avg_count = 8;

        public static double ComputeBackground(IEnumerable<ChromatogramData> chromatograms)
        {
            List<double> averages = new List<double>();
            foreach (var chromatogram in chromatograms)
            {
                if (chromatogram == null)
                {
                    continue;
                }
                double[] intensities = chromatogram.Intensities;
                for (int i = 0; i < intensities.Length; i += background_avg_count)
                {
                    double total = 0;
                    int count;
                    for (count = 0; count < background_avg_count && i + count < intensities.Length; count++)
                    {
                        total += intensities[i + count];
                    }
                    averages.Add(total / count);
                }
            }
            if (averages.Count == 0)
            {
                return 0;
            }
            averages.Sort();
            return averages[averages.Count / 2];

        }
    }
}
