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
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class MedianPeakShape
    {
        public MedianPeakShape(IEnumerable<float> times, IEnumerable<float> intensities)
        {
            Times = ImmutableList.ValueOf(times);
            Intensities = ImmutableList.ValueOf(intensities);
        }
        public ImmutableList<float> Times { get; }
        public ImmutableList<float> Intensities { get; }

        public static MedianPeakShape GetMedianPeakShape(float startTime, float endTime,
            IEnumerable<TimeIntensities> chromatograms)
        {
            const int numPoints = 100;
            var validChromatograms = new List<TimeIntensities>();
            var normalizationFactors = new List<float>();
            foreach (var chromatogram in chromatograms)
            {
                float normalizationFactor = chromatogram.MaxIntensityInRange(startTime, endTime);
                if (normalizationFactor > 0)
                {
                    validChromatograms.Add(chromatogram);
                    normalizationFactors.Add(normalizationFactor);
                }
            }
            var times = Enumerable.Range(0, numPoints)
                .Select(i => startTime + (endTime - startTime) * i / (numPoints - 1)).ToList();
            if (validChromatograms.Count == 0)
            {
                return new MedianPeakShape(times, Enumerable.Repeat(0f, times.Count));
            }

            List<double[]> normalizedIntensityArrays = Enumerable.Range(0, numPoints)
                .Select(i => new double[validChromatograms.Count]).ToList();
            for (int iChromatogram = 0; iChromatogram < validChromatograms.Count; iChromatogram++)
            {
                var chromatogram = validChromatograms[iChromatogram];
                var normalizationFactor = normalizationFactors[iChromatogram];
                int iTime = 0;
                foreach (var intensity in chromatogram.GetInterpolatedIntensities(times))
                {
                    normalizedIntensityArrays[iTime][iChromatogram] = intensity / normalizationFactor;
                    iTime++;
                }
            }

            var intensities = normalizedIntensityArrays.Select(array => (float)MedianOfArray(array));
            return new MedianPeakShape(times, intensities);
        }

        private static double MedianOfArray(double[] values)
        {
            double upperMedian = Statistics.QNthItem(values, values.Length / 2);
            if (1 == (values.Length & 1))
            {
                return upperMedian;
            }
            double lowerMedian = Statistics.QNthItem(values, values.Length / 2 - 1);
            return (upperMedian + lowerMedian) / 2;
        }

        public double GetCorrelation(TimeIntensities chromatogram)
        {
            var intensities = chromatogram.GetInterpolatedIntensities(Times)
                .Select(intensity => (double)intensity).ToList();
            return MathNet.Numerics.Statistics.Correlation.Pearson(
                Intensities.Select(i => (double) i),
                intensities);
        }
    }
}
