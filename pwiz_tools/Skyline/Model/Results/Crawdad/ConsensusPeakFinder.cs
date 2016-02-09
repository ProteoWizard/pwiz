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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;

namespace pwiz.Skyline.Model.Results.Crawdad
{
    /// <summary>
    /// Peak finder which makes use of a number of different peak finder implementations
    /// and makes sure that they all agree on the values, within tolerance specified by
    /// <see cref="ValuesCloseEnough"/>.
    /// </summary>
    public class ConsensusPeakFinder : IPeakFinder
    {
        // ReSharper disable NonLocalizedString
        // This class is debug only code: does not need to be localized.

        public static double allowedAbsoluteError = 4E-4;
        public static double allowedRelativeError = 1E-4;
        private ImmutableList<IPeakFinder> _peakFinders;
        private IList<float> _intensities;
        public ConsensusPeakFinder(IEnumerable<IPeakFinder> peakFinders)
        {
            _peakFinders = ImmutableList<IPeakFinder>.ValueOf(peakFinders);
        }

        public bool ThrowOnMismatch { get;set; }

        public void Dispose()
        {
            foreach (var peakFinder in _peakFinders)
            {
                peakFinder.Dispose();
            }
        }

        public void SetChromatogram(IList<float> times, IList<float> intensities)
        {
            _intensities = intensities;
            foreach (var peakFinder in _peakFinders)
            {
                peakFinder.SetChromatogram(times, intensities);
            }
        }

        public IFoundPeak GetPeak(int startIndex, int endIndex)
        {
            var results = _peakFinders.Select(finder => finder.GetPeak(startIndex, endIndex)).ToArray();
            var firstPeak = results[0];
            for (int i = 1; i < results.Length; i++)
            {
                if (!AreEquivalent(firstPeak, results[i]))
                {
                    ReportMismatch(string.Format("Mismatch on peak from {0} to {1}", startIndex, endIndex), new[]{firstPeak}, new[]{results[i]}, null);
                    break;
                }
            }
            return firstPeak;
        }

        public IList<IFoundPeak> CalcPeaks(int max, int[] idIndices)
        {
            var results = new List<IList<IFoundPeak>>();
            foreach (var peakFinder in _peakFinders)
            {
                results.Add(peakFinder.CalcPeaks(max, idIndices));
            }
            for (int i = 1; i < results.Count; i++)
            {
                if (!EnsurePeaklistsEqual(results[0], results[1], idIndices))
                {
                    break;
                }
            }
            
            return results.First();
        }

        public IList<float> Intensities1D
        {
            get
            {
                var results = new List<IList<float>>();
                foreach (var peakFinder in _peakFinders)
                {
                    results.Add(peakFinder.Intensities1D);
                }
                for (int iFinder = 1; iFinder < results.Count; iFinder++)
                {
                    if (!EnsureIntensitiesEqual(results[0], results[iFinder]))
                    {
                        break;
                    }
                }
                return results.First();
            }
        }

        public IList<float> Intensities2d
        {
            get
            {
                var results = new List<IList<float>>();
                foreach (var peakFinder in _peakFinders)
                {
                    results.Add(peakFinder.Intensities2d);
                }
                for (int iFinder = 1; iFinder < results.Count; iFinder++)
                {
                    if (!EnsureIntensitiesEqual(results[0], results[iFinder]))
                    {
                        break;
                    }
                }
                return results.First();
            }
        }

        private bool EnsureIntensitiesEqual(IList<float> intensities1, IList<float> intensities2)
        {
            if (intensities1.Count != intensities2.Count)
            {
                ReportMismatch(string.Format("Different number of intensities: {0} vs {1}", intensities1.Count, intensities2.Count), 
                    new IFoundPeak[0], new IFoundPeak[0], new int[0]);
                return false;
            }
            for (int i = 0; i < intensities1.Count; i++)
            {
                var diff = Math.Abs(intensities1[i] - intensities2[i]);
                if (diff < 1)
                {
                    continue;
                }
                if (ValuesCloseEnough(intensities1[i], intensities2[i]))
                {
                    continue;
                }
                ReportMismatch(string.Format("Values differ at position {0}: {1} vs {2}", i, intensities1[i], intensities2[i]),
                    new IFoundPeak[0], new IFoundPeak[0], new int[0]);
            }
            return true;
        }

        private bool EnsurePeaklistsEqual(IList<IFoundPeak> peakList1, IList<IFoundPeak> peakList2, IList<int> idTimes)
        {
            if (peakList1.Count != peakList2.Count)
            {
                ReportMismatch("Number of peaks is different", peakList1, peakList2, idTimes);
                return false;
            }
            float minArea1 = GetMinArea(peakList1);
            float minArea2 = GetMinArea(peakList2);
            float minArea = Math.Min(minArea1, minArea2);
            if (!ValuesCloseEnough(minArea1, minArea2))
            {
                ReportMismatch(string.Format("Minimum areas {0} and {1} are not close enough", minArea1, minArea2), peakList1, peakList2, idTimes);
                return false;
            }

            foreach (var peakToCompare in peakList1.Concat(peakList2))
            {
                var peak1 = peakList1.FirstOrDefault(p => p.StartIndex == peakToCompare.StartIndex);
                var peak2 = peakList2.FirstOrDefault(p => p.StartIndex == peakToCompare.StartIndex);
                if (peak1 == null || peak2 == null)
                {
                    if (!ValuesCloseEnough(peakToCompare.Area, minArea))
                    {
                        ReportMismatch("Missing peak " + peakToCompare, peakList1, peakList2, idTimes);
                        return false;
                    }
                } 
                else if (!AreEquivalent(peak1, peak2))
                {
                    ReportMismatch(string.Format("Peak1 ({0}) is different than \nPeak2 ({1})", peak1, peak2), peakList1, peakList2, idTimes);
                }
            }
            return true;
        }

        private float GetMinArea(IEnumerable<IFoundPeak> peaks)
        {
            float? minIdentifiedArea = null;
            float? minUnidentifiedArea = null;
            foreach (var peak in peaks)
            {
                if (peak.Identified)
                {
                    if (peak.Area < minIdentifiedArea.GetValueOrDefault(float.MaxValue))
                    {
                        minIdentifiedArea = peak.Area;
                    }
                }
                else
                {
                    if (peak.Area < minUnidentifiedArea.GetValueOrDefault(float.MaxValue))
                    {
                        minUnidentifiedArea = peak.Area;
                    }
                }
            }
            return minUnidentifiedArea ?? minIdentifiedArea ?? float.MaxValue;
        }

        public bool IsHeightAsArea
        {
            get
            {
                var values = _peakFinders.Select(finder => finder.IsHeightAsArea).Distinct().ToArray();
                if (values.Length != 1)
                {
                    throw new InvalidOperationException("No consensus on IsHeightAsArea");
                }
                return values[0];
            }
        }

        protected void ReportMismatch(string message, IList<IFoundPeak> firstPeaks, IList<IFoundPeak> secondPeaks, IList<int> idIndices)
        {
            StringWriter errorWriter = new StringWriter();
            errorWriter.WriteLine(message);
            errorWriter.WriteLine("First Peaks:");
            foreach (var peak in firstPeaks)
            {
                errorWriter.WriteLine(peak);
            }
            errorWriter.WriteLine("Second Peaks:");
            foreach (var peak in secondPeaks)
            {
                errorWriter.WriteLine(peak);
            }
            if (null != _intensities)
            {
                errorWriter.Write("intensities = {");
                DumpIntensities(errorWriter, _intensities);
                errorWriter.WriteLine("};");
            }
            if (null != idIndices)
            {
                errorWriter.Write("idIndices = {");
                errorWriter.Write(string.Join(",", idIndices));
                errorWriter.WriteLine("};");
            }
            Console.Out.WriteLine(errorWriter);
            Debug.WriteLine(errorWriter);
            if (ThrowOnMismatch)
            {
                throw new ApplicationException("Peak mismatch");
            }
        }

        private static void DumpIntensities(TextWriter writer, IEnumerable<float> intensities)
        {
            int count = 0;
            foreach (var value in intensities)
            {
                writer.Write(value.ToString("R", CultureInfo.InvariantCulture) + "f,");
                count++;
                if (count % 8 == 0)
                {
                    writer.WriteLine();
                }
            }
        }

        public static bool AreEquivalent(IFoundPeak peak1, IFoundPeak peak2)
        {
            return peak1.StartIndex == peak2.StartIndex
                   && peak1.EndIndex == peak2.EndIndex
                   && peak1.TimeIndex == peak2.TimeIndex
                   && ValuesCloseEnough(peak1.Area, peak2.Area)
                   && ValuesCloseEnough(peak1.BackgroundArea, peak2.BackgroundArea)
                   && ValuesCloseEnough(peak1.Fwhm, peak2.Fwhm)
                   && ValuesCloseEnough(peak1.Height, peak2.Height)
                   && peak1.FwhmDegenerate == peak2.FwhmDegenerate;
        }

        public static bool ValuesCloseEnough(float f1, float f2)
        {
            if (f1.Equals(f2))
            {
                return true;
            }
            var difference = Math.Abs(f1 - f2);
            if (difference < allowedAbsoluteError)
            {
                return true;
            }
            var smallerAbsValue = Math.Min(Math.Abs(f1), Math.Abs(f2));
            if (smallerAbsValue == 0)
            {
                return false;
            }
            double maxAllowedDifference = smallerAbsValue * allowedRelativeError;

            return difference < maxAllowedDifference;
        }
    }
}
