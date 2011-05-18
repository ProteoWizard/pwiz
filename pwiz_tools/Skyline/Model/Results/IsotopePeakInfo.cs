/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public sealed class IsotopePeakInfo : Immutable
    {
        private readonly double _monoisotopicMassH;
        private readonly int _charge;
        private ReadOnlyCollection<float> _expectedDistribution;

        public IsotopePeakInfo(MassDistribution massDistribution,
                               double monoisotopicMassH,
                               int charge,
                               Func<double, double> calcFilterWindow,
                               double minimumAbundance)
        {
            _monoisotopicMassH = monoisotopicMassH;
            _charge = charge;

            // Insert M-N peaks, always including M-1
            var q1FilterValues = new List<double>();
            double minMz = massDistribution.MinMass, maxMz = massDistribution.MaxMass;
            double startFilter;
            do
            {
                double peakCenterMz = GetMZI(-(q1FilterValues.Count + 1));
                q1FilterValues.Insert(0, peakCenterMz);

                double filterWindow = calcFilterWindow(peakCenterMz);
                startFilter = peakCenterMz - filterWindow / 2;
            }
            while (minMz < startFilter);

            int monoMassIndex = q1FilterValues.Count;

            // Then add the non-negative peaks
            int i = 0;
            double endFilter;
            do
            {
                double peakCenterMz = GetMZI(i++);
                q1FilterValues.Add(peakCenterMz);

                double filterWindow = calcFilterWindow(peakCenterMz);
                startFilter = peakCenterMz - filterWindow / 2;
                endFilter = startFilter + filterWindow;
            }
            while (endFilter <= maxMz);

            // Use the filtering algorithm that will be used on real data to determine the
            // expected proportions of the mass distribution that will end up filtered into
            // peaks
            var filter = new SpectrumFilterPair(q1FilterValues[monoMassIndex]);
            filter.AddQ1FilterValues(q1FilterValues, calcFilterWindow);

            var expectedSpectrum = filter.FilterQ1Spectrum(massDistribution.Keys.ToArray(),
                                                           massDistribution.Values.ToArray());

            int startIndex = expectedSpectrum.Intensities.IndexOf(inten => inten >= minimumAbundance);
            if (startIndex == -1)
                throw new InvalidOperationException(string.Format("Minimum abundance {0} too high", minimumAbundance));
            // Always include the M-1 peak, even if it is expected to have zero intensity
            if (startIndex > monoMassIndex - 1)
                startIndex = monoMassIndex - 1;
            int endIndex = expectedSpectrum.Intensities.LastIndexOf(inten => inten >= minimumAbundance) + 1;
            int countPeaks = endIndex - startIndex;
            var expectedPeaks = new float[countPeaks];
            for (i = 0; i < countPeaks; i++)
            {
                expectedPeaks[i] = (float) expectedSpectrum.Intensities[i + startIndex];
            }

            ExpectedPeaks = expectedPeaks;

            // TODO: Can this be discarded?
            // MassDistribution = massDistribution;

            // Find the base peak
            MonoMassIndex = monoMassIndex - startIndex;
            for (i = 1; i < countPeaks; i++)
            {
                if (expectedPeaks[i] > expectedPeaks[BaseMassIndex])
                    BaseMassIndex = i;
            }
        }

        public int MonoMassIndex { get; private set; }

        public int BaseMassIndex { get; private set; }

        public IList<float> ExpectedPeaks
        {
            get { return _expectedDistribution; }
            private set { _expectedDistribution = MakeReadOnly(value); }
        }

        public float BaseMassPercent
        {
            get { return ExpectedPeaks[BaseMassIndex]; }
        }

        public double GetMZI(int index)
        {
            return SequenceMassCalc.GetMZI(_monoisotopicMassH, index, _charge);
        }

        #region object overrides

        public bool Equals(IsotopePeakInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other._monoisotopicMassH.Equals(_monoisotopicMassH) &&
                other._charge == _charge &&
                ArrayUtil.EqualsDeep(other._expectedDistribution, _expectedDistribution) &&
                other.MonoMassIndex == MonoMassIndex &&
                other.BaseMassIndex == BaseMassIndex;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (IsotopePeakInfo)) return false;
            return Equals((IsotopePeakInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _monoisotopicMassH.GetHashCode();
                result = (result*397) ^ _charge;
                result = (result*397) ^ (_expectedDistribution != null ? _expectedDistribution.GetHashCodeDeep() : 0);
                result = (result*397) ^ MonoMassIndex;
                result = (result*397) ^ BaseMassIndex;
                return result;
            }
        }

        #endregion
    }
}
