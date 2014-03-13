/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Skyline.Model.Results
{
    public sealed class SpectrumFilterPair : IComparable<SpectrumFilterPair>
    {
        public SpectrumFilterPair(PrecursorModSeq precursorModSeq, int id, double? minTime, double? maxTime, bool highAccQ1, bool highAccQ3)
        {
            Id = id;
            ModifiedSequence = precursorModSeq.ModifiedSequence;
            Q1 = precursorModSeq.PrecursorMz;
            Extractor = precursorModSeq.Extractor;
            MinTime = minTime;
            MaxTime = maxTime;
            HighAccQ1 = highAccQ1;
            HighAccQ3 = highAccQ3;

            if (Q1 == 0)
            {
                ArrayQ1 = ArrayQ1Window = new[] {0.0};
            }
        }

        public int Id { get; private set; }
        public ChromExtractor Extractor { get; private set; }
        public bool HighAccQ1 { get; private set; }
        public bool HighAccQ3 { get; private set; }
        public string ModifiedSequence { get; private set; }
        public double Q1 { get; private set; }
        private double? MinTime { get; set; }
        private double? MaxTime { get; set; }
        // Q1 values for when precursor ions are filtered from MS1
        private double[] ArrayQ1 { get; set; }
        private double[] ArrayQ1Window { get; set; }
        // Q3 values for product ions filtered in MS/MS
        public double[] ArrayQ3 { get; private set; }
        public double[] ArrayQ3Window { get; private set; }

        public void AddQ1FilterValues(IEnumerable<double> filterValues, Func<double, double> getFilterWindow)
        {
            AddFilterValues(MergeFilters(ArrayQ1, filterValues).Distinct(), getFilterWindow,
                centers => ArrayQ1 = centers, windows => ArrayQ1Window = windows);
        }

        public void AddQ3FilterValues(IEnumerable<double> filterValues, Func<double, double> getFilterWindow)
        {
            AddFilterValues(MergeFilters(ArrayQ3, filterValues).Distinct(), getFilterWindow,
                centers => ArrayQ3 = centers, windows => ArrayQ3Window = windows);
        }

        private static IEnumerable<double> MergeFilters(IEnumerable<double> existing, IEnumerable<double> added)
        {
            if (existing == null)
                return added;
            return existing.Union(added);
        }

        private static void AddFilterValues(IEnumerable<double> filterValues,
                                            Func<double, double> getFilterWindow,
                                            Action<double[]> setCenters, Action<double[]> setWindows)
        {
            var listQ3 = filterValues.ToList();

            listQ3.Sort();

            setCenters(listQ3.ToArray());
            setWindows(listQ3.ConvertAll(mz => getFilterWindow(mz)).ToArray());
        }

        public ExtractedSpectrum FilterQ1Spectrum(double[] mzArray, double[] intensityArray)
        {
            return FilterSpectrum(mzArray, intensityArray, ArrayQ1, ArrayQ1Window, HighAccQ1);
        }

        public ExtractedSpectrum FilterQ3Spectrum(double[] mzArray, double[] intensityArray)
        {
            // All-ions extraction for MS1 scans only
            if (Q1 == 0)
                return null;

            return FilterSpectrum(mzArray, intensityArray, ArrayQ3, ArrayQ3Window, HighAccQ3);
        }

        private ExtractedSpectrum FilterSpectrum(double[] mzArray, double[] intensityArray,
                                                 double[] centerArray, double[] windowArray, bool highAcc)
        {
            int len = 1;
            if (Q1 == 0)
                highAcc = false;    // No mass error for all-ions extraction
            else
            {
                if (centerArray.Length == 0)
                    return null;
                len = centerArray.Length;
            }

            float[] extractedIntensities = new float[len];
            float[] massErrors = highAcc ? new float[len] : null;

            // Search for matching peaks for each Q3 filter
            // Use binary search to get to the first m/z value to be considered more quickly
            // This should help MS1 where isotope distributions will be very close in m/z
            // It should also help MS/MS when more selective, larger fragment ions are used,
            // since then a lot of less selective, smaller peaks must be skipped
            int iPeak = Q1 != 0
                ? Array.BinarySearch(mzArray, centerArray[0] - windowArray[0]/2)
                : 0;

            if (iPeak < 0)
                iPeak = ~iPeak;

            for (int i = 0; i < len; i++)
            {
                // Look for the first peak that is greater than the start of the filter
                double target = 0, endFilter = double.MaxValue;
                if (Q1 != 0)
                {
                    target = centerArray[i];
                    double filterWindow = windowArray[i];
                    double startFilter = target - filterWindow / 2;
                    endFilter = startFilter + filterWindow;

                    if (iPeak < mzArray.Length)
                    {
                        iPeak = Array.BinarySearch(mzArray, iPeak, mzArray.Length - iPeak, startFilter);
                        if (iPeak < 0)
                            iPeak = ~iPeak;
                    }
                }

                // Add the intensity values of all peaks less than the end of the filter
                double totalIntensity = 0;
                double meanError = 0;
                for (int iNext = iPeak; iNext < mzArray.Length && mzArray[iNext] < endFilter; iNext++)
                {
                    double mz = mzArray[iNext];
                    double intensity = intensityArray[iNext];
                    
                    if (Extractor == ChromExtractor.summed)
                        totalIntensity += intensity;
                    else if (intensity > totalIntensity)
                    {
                        totalIntensity = intensity;
                        meanError = 0;
                    }

                    // Accumulate weighted mean mass error for summed, or take a single
                    // mass error of the most intense peak for base peak.
                    if (highAcc && (Extractor == ChromExtractor.summed || meanError == 0))
                    {
                        if (totalIntensity > 0.0)
                        {
                            double deltaPeak = mz - target;
                            meanError += (deltaPeak - meanError) * intensity / totalIntensity;
                        }
                    }
                }
                extractedIntensities[i] = (float) totalIntensity;
                if (massErrors != null)
                    massErrors[i] = (float) SequenceMassCalc.GetPpm(target, meanError);
            }

            return new ExtractedSpectrum(ModifiedSequence,
                Q1,
                Extractor,
                Id,
                centerArray,
                windowArray,
                extractedIntensities,
                massErrors);
        }

        public int CompareTo(SpectrumFilterPair other)
        {
            return Comparer.Default.Compare(Q1, other.Q1);
        }

        public bool ContainsTime(double time)
        {
            return (!MinTime.HasValue || MinTime.Value <= time) &&
                (!MaxTime.HasValue || MaxTime.Value >= time);
        }
    }
}