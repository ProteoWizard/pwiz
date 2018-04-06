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
    internal class FoundPeak : IFoundPeak
    {
        private readonly IList<float> _allIntensities;
        private readonly float _baselineIntensity;
        // Only used in calculation of FWHM.
        // This is an extra integer that gets added to the start and end of the peak, which slightly affects 
        // the way floating point numbers get rounded.
        private readonly int _widthDataWings;

        /// <param name="widthDataWings">Extra number that gets added to start and end of peak.  Only used so that floating point numbers
        /// get rounded exactly the way they were in the old Crawdad.</param>
        /// <param name="intensities"></param>
        /// <param name="baselineIntensity"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        public FoundPeak(int widthDataWings, IList<float> intensities, float baselineIntensity, int startIndex, int endIndex)
        {
            _allIntensities = intensities;
            _baselineIntensity = baselineIntensity;
            _widthDataWings = widthDataWings;
            SetBoundaries(startIndex, endIndex);
        }
        public int StartIndex { get; private set; }

        void IDisposable.Dispose()
        {
        }

        public int EndIndex { get; private set; }

        public int TimeIndex { get; private set; }

        public float Area { get; private set; }

        public float RawArea { get; private set; }

        public float BackgroundArea { get; private set; }

        public float Height { get; private set; }

        public float RawHeight { get; private set; }

        public float Fwhm { get; private set; }

        public bool FwhmDegenerate { get; private set; }

        public bool Identified { get; internal set; }

        public int Length
        {
            get { return EndIndex - StartIndex + 1; }
        }

        public override string ToString()
        {
            return PeakFinders.PeakToString(this);
        }

        public void ResetBoundaries(int startIndex, int endIndex)
        {
            if (startIndex > endIndex)
            {
                throw new ArgumentException();
            }

            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        private void SetBoundaries(int startIndex, int endIndex)
        {
            float backgroundLevel = Math.Min(SafeGetIntensity(startIndex), SafeGetIntensity(endIndex));
            IList<float> intensitiesToSum = Enumerable.Range(startIndex, endIndex - startIndex + 1)
                .Select(SafeGetIntensity).ToArray();
            int maxIndex = startIndex;
            float rawHeight = SafeGetIntensity(startIndex);
            for (int i = startIndex + 1; i < endIndex; i++)
            {
                if (SafeGetIntensity(i) > rawHeight)
                {
                    maxIndex = i;
                    rawHeight = SafeGetIntensity(i);
                }
            }
            float rawArea = GetAreaUnderCurve(intensitiesToSum);
            float backgroundArea = GetAreaUnderCurve(intensitiesToSum.Select(intensity => Math.Min(backgroundLevel, intensity)));

            float height = rawHeight - backgroundLevel;
            float halfMax = rawHeight - height / 2;
            int? halfHeightStartIndex = null;
            for (int i = startIndex; i < maxIndex; i++)
            {
                if (SafeGetIntensity(i) <= halfMax && SafeGetIntensity(i + 1) > halfMax)
                {
                    halfHeightStartIndex = i;
                    break;
                }
            }
            int? halfHeightEndIndex = null;
            for (int i = maxIndex; i < endIndex; i++)
            {
                if (SafeGetIntensity(i) >= halfMax && SafeGetIntensity(i + 1) < halfMax)
                {
                    halfHeightEndIndex = i;
                    break;
                }
            }

            double halfHeightStart;
            if (!halfHeightStartIndex.HasValue)
            {
                halfHeightStart = startIndex + _widthDataWings;
            }
            else
            {
                double frac_delta = (halfMax - SafeGetIntensity(halfHeightStartIndex.Value)) /
                                   (SafeGetIntensity(halfHeightStartIndex.Value + 1) - SafeGetIntensity(halfHeightStartIndex.Value));
                halfHeightStart = halfHeightStartIndex.Value + frac_delta + _widthDataWings;
            }
            double halfHeightEnd;
            if (!halfHeightEndIndex.HasValue)
            {
                halfHeightEnd = endIndex + _widthDataWings;
            }
            else
            {
                double frac_delta = (SafeGetIntensity(halfHeightEndIndex.Value) - halfMax) /
                    (SafeGetIntensity(halfHeightEndIndex.Value) - SafeGetIntensity(halfHeightEndIndex.Value + 1));
                halfHeightEnd = halfHeightEndIndex.Value + frac_delta + _widthDataWings;
            }
            StartIndex = ConstrainIndex(startIndex);
            EndIndex = ConstrainIndex(endIndex);
            TimeIndex = ConstrainIndex(maxIndex);
            Height = height;
            RawHeight = rawHeight;
            Area = rawArea - backgroundArea;
            RawArea = rawArea;
            BackgroundArea = backgroundArea;
            Fwhm = (float)halfHeightEnd - (float)halfHeightStart;
            FwhmDegenerate = !halfHeightStartIndex.HasValue || !halfHeightEndIndex.HasValue;
        }

        private float GetAreaUnderCurve(IEnumerable<float> intensities)
        {
            bool first = true;
            float firstValue = 0;
            float lastValue = 0;
            double sum = 0;
            foreach (var intensity in intensities)
            {
                if (first)
                {
                    firstValue = intensity;
                    first = false;
                }
                lastValue = intensity;
                sum += intensity;
            }
            return (float) (sum - firstValue/2 - lastValue/2);
        }

        public float SafeGetIntensity(int index)
        {
            if (index < 0 || index >= _allIntensities.Count)
            {
                return _baselineIntensity;
            }
            return _allIntensities[index];
        }

        private int ConstrainIndex(int index)
        {
            return Math.Min(_allIntensities.Count - 1, Math.Max(0, index));

        }
    }
}