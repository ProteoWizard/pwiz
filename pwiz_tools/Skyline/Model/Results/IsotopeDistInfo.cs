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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public sealed class IsotopeDistInfo : Immutable
    {
        private readonly TypedMass _monoisotopicMass;
        private readonly Adduct _adduct;
        private ImmutableList<MzRankProportion> _expectedDistribution;

        public IsotopeDistInfo(MassDistribution massDistribution,
                               TypedMass monoisotopicMass,
                               Adduct adduct,
                               Func<double, double> calcFilterWindow,
                               double massResolution,
                               double minimumAbundance)
        {
            _monoisotopicMass = monoisotopicMass;
            _adduct = adduct.Unlabeled; // Don't reapply explicit isotope labels

            // Get peak center of mass values for the given resolution
            var q1FilterValues = MassDistribution.NewInstance(massDistribution, massResolution, 0).Keys.ToList();
            // Find the monoisotopic m/z and make sure it is exactly the expected number
            double monoMz = _adduct.MzFromNeutralMass(_monoisotopicMass);
            double monoMzDist = monoMz;
            int monoMassIndex = 0;
            for (int i = 0; i < q1FilterValues.Count; i++)
            {
                double peakCenterMz = q1FilterValues[i];
                double filterWindow = calcFilterWindow(peakCenterMz);
                double startMz = peakCenterMz - filterWindow/2;
                double endMz = startMz + filterWindow;
                if (startMz < monoMz && monoMz < endMz)
                {
                    monoMzDist = q1FilterValues[i];
                    q1FilterValues[i] = monoMz;
                    monoMassIndex = i;
                    break;
                }                
            }
            // Insert a M-1 peak, even if it is not expected in the isotope mass distribution
            if (monoMassIndex == 0 && q1FilterValues.Count > 1)
            {
                // Use the delta from the original distribution monoMz to the next peak
                q1FilterValues.Insert(0, monoMz + monoMzDist - q1FilterValues[1]);
                monoMassIndex++;
            }

            if (!q1FilterValues.Any())
            {
                // This should never happen, but just in case it does happen, we safely exit the constructor
                ExpectedPeaks = ImmutableList.Singleton(new MzRankProportion(monoMz, 0, 1.0f));
                MonoMassIndex = BaseMassIndex = 0;
                return;
            }

            var signedQ1FilterValues = q1FilterValues.Select(q => new SignedMz(q, adduct.AdductCharge < 0)).ToList();

            // Use the filtering algorithm that will be used on real data to determine the
            // expected proportions of the mass distribution that will end up filtered into
            // peaks
            // CONSIDER: Mass accuracy information is not calculated here
            var key = new PrecursorTextId(signedQ1FilterValues[monoMassIndex], null, null, ChromExtractor.summed);
            var filter = new SpectrumFilterPair(key, PeptideDocNode.UNKNOWN_COLOR, 0, null, null, false, false);
            filter.AddQ1FilterValues(signedQ1FilterValues, calcFilterWindow);

            var expectedSpectrum = filter.FilterQ1SpectrumList(new[] { new MsDataSpectrum
            { Mzs = massDistribution.Keys.ToArray(), Intensities = massDistribution.Values.ToArray(), NegativeCharge = (adduct.AdductCharge < 0) } });

            int startIndex = expectedSpectrum.Intensities.IndexOf(inten => inten >= minimumAbundance);
            if (startIndex == -1)
            {
                // This can happen if the amino acid modifications are messed up, 
                // and the peptide mass is negative or something.
                ExpectedPeaks = ImmutableList.Singleton(new MzRankProportion(monoMz, 0, 1.0f));
                MonoMassIndex = BaseMassIndex = 0;
                return;
            }
            // Always include the M-1 peak, even if it is expected to have zero intensity
            if (startIndex > monoMassIndex - 1)
                startIndex = monoMassIndex - 1;
            if (startIndex < 0)
                startIndex = 0;
            int endIndex = expectedSpectrum.Intensities.LastIndexOf(inten => inten >= minimumAbundance) + 1;
            int countPeaks = endIndex - startIndex;
            var listProportionIndices = new List<KeyValuePair<float, int>>(countPeaks);
            for (int i = 0; i < countPeaks; i++)
            {
                listProportionIndices.Add(new KeyValuePair<float, int>(
                    expectedSpectrum.Intensities[i + startIndex], i));
            }
            // Sort proportions descending.
            listProportionIndices.Sort((p1, p2) => Comparer.Default.Compare(p2.Key, p1.Key));

            // Set proportions and ranks back in the original locations
            var expectedProportionRanks = new KeyValuePair<float, int>[countPeaks];
            for (int i = 0; i < countPeaks; i++)
            {
                expectedProportionRanks[listProportionIndices[i].Value] =
                    new KeyValuePair<float, int>(listProportionIndices[i].Key, i + 1);
            }

            MonoMassIndex = monoMassIndex - startIndex;

            // Find the base peak and fill in the masses and proportions
            var expectedPeaks = new List<MzRankProportion>();
            for (int i = 0; i < countPeaks; i++)
            {
                float expectedProportion = expectedProportionRanks[i].Key;
                int rank = expectedProportionRanks[i].Value;
                expectedPeaks.Add(new MzRankProportion(q1FilterValues[i + startIndex], rank, expectedProportion));
                if (expectedProportion > expectedProportionRanks[BaseMassIndex].Key)
                    BaseMassIndex = i;
            }
            ExpectedPeaks = expectedPeaks;
        }

        public int CountPeaks { get { return _expectedDistribution.Count; } }

        private int MonoMassIndex { get; set; }

        private int BaseMassIndex { get; set; }

        private IList<MzRankProportion> ExpectedPeaks
        {
            get { return _expectedDistribution; }
            set { _expectedDistribution = MakeReadOnly(value); }
        }

        public int MassIndexToPeakIndex(int massIndex)
        {
            return massIndex + MonoMassIndex;
        }

        public int PeakIndexToMassIndex(int index)
        {
            return index - MonoMassIndex;
        }

        public IEnumerable<float> ExpectedProportions
        {
            get { return _expectedDistribution.Select(mzP => mzP.Proportion); }
        }

        public float BaseMassPercent
        {
            get { return ExpectedPeaks[BaseMassIndex].Proportion; }
        }

        public float GetProportionI(int massIndex)
        {
            return ExpectedPeaks[MassIndexToPeakIndex(massIndex)].Proportion;
        }

        public int GetRankI(int massIndex)
        {
            return ExpectedPeaks[MassIndexToPeakIndex(massIndex)].Rank;
        }

        public double GetMZI(int massIndex)
        {
            return ExpectedPeaks[MassIndexToPeakIndex(massIndex)].Mz;
        }

        public TypedMass GetMassI(int massIndex, int? decoyMassShift = null)
        {
            // Return the original monoisotopic mass + H, if requested to maintain an exact match.
            if (massIndex == 0 && !decoyMassShift.HasValue)
                return _monoisotopicMass;
            // Otherwize use the charge to convert from the peak center m/z values
            double shift = SequenceMassCalc.GetPeptideInterval(decoyMassShift);    // Correct for shift applied to the distribution
            // ReSharper disable ImpureMethodCallOnReadonlyValueField
            return _monoisotopicMass.IsMassH() ? 
                new TypedMass(SequenceMassCalc.GetMH(ExpectedPeaks[MassIndexToPeakIndex(massIndex)].Mz - shift, _adduct.AdductCharge), _monoisotopicMass.MassType) :
                _adduct.MassFromMz(ExpectedPeaks[MassIndexToPeakIndex(massIndex)].Mz - shift, _monoisotopicMass.MassType);
            // ReSharper restore ImpureMethodCallOnReadonlyValueField
        }

        #region object overrides

        public bool Equals(IsotopeDistInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other._monoisotopicMass.Equals(_monoisotopicMass) &&
                   other._adduct == _adduct &&
                   ArrayUtil.EqualsDeep(other._expectedDistribution, _expectedDistribution) &&
                   other.MonoMassIndex == MonoMassIndex &&
                   other.BaseMassIndex == BaseMassIndex;
        }

        // For test purposes
        public bool IsSimilar(IsotopeDistInfo other) 
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            // ReSharper disable ImpureMethodCallOnReadonlyValueField
            return _monoisotopicMass.Equivalent(other._monoisotopicMass) && // Allows comparison of massH and straight mass
                   other._adduct.AdductFormula == _adduct.AdductFormula &&
                   ArrayUtil.EqualsDeep(other._expectedDistribution, _expectedDistribution) &&
                   other.MonoMassIndex == MonoMassIndex &&
                   other.BaseMassIndex == BaseMassIndex;
            // ReSharper restore ImpureMethodCallOnReadonlyValueField
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (IsotopeDistInfo)) return false;
            return Equals((IsotopeDistInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _monoisotopicMass.GetHashCode();
                result = (result*397) ^ _adduct.GetHashCode();
                result = (result*397) ^ (_expectedDistribution != null ? _expectedDistribution.GetHashCodeDeep() : 0);
                result = (result*397) ^ MonoMassIndex;
                result = (result*397) ^ BaseMassIndex;
                return result;
            }
        }

        #endregion

        private struct MzRankProportion
        {
            public MzRankProportion(double mz, int rank, float proportion) : this()
            {
                Mz = mz;
                Rank = rank;
                Proportion = proportion;
            }

            public double Mz { get; private set; }
            public int Rank { get; private set; }
            public float Proportion { get; private set; }

            public override string ToString() // For ease in debugging
            {
                return String.Format(@"mz {0} rank {1} proportion {2}", Mz, Rank, Proportion);
            }
        }
    }

    public sealed class TransitionIsotopeDistInfo
    {
        public TransitionIsotopeDistInfo(int rank, float proportion)
        {
            Rank = rank;
            Proportion = proportion;
        }

        public int Rank { get; private set; }
        public float Proportion { get; private set; }

        #region object overrides

        public bool Equals(TransitionIsotopeDistInfo obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.Proportion == Proportion && obj.Rank == Rank;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(TransitionIsotopeDistInfo)) return false;
            return Equals((TransitionIsotopeDistInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Proportion.GetHashCode() * 397) ^ Rank;
            }
        }

        public override string ToString() // For debugging convenience
        {
            return string.Format(@"r={0} p={1}", Rank, Proportion);
        }

        #endregion
    }
}
