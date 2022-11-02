/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009-2011 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public sealed class LibraryRankedSpectrumInfo : Immutable
    {
        private ImmutableList<RankedMI> _spectrum;
        public static LibraryRankedSpectrumInfo NewLibraryRankedSpectrumInfo(SpectrumPeaksInfo info,
            IsotopeLabelType labelType, TransitionGroupDocNode group,
            SrmSettings settings, Target lookupSequence, ExplicitMods lookupMods,
            IEnumerable<Adduct> charges, IEnumerable<IonType> types,
            IEnumerable<Adduct> rankCharges, IEnumerable<IonType> rankTypes, double? score)
        {
            return MakeLibraryRankedSpectrumInfo(info, labelType, group, settings, lookupSequence, lookupMods,
                charges, types, rankCharges, rankTypes, score, false, true, -1);
        }

        public static LibraryRankedSpectrumInfo NewLibraryRankedSpectrumInfo(SpectrumPeaksInfo info, IsotopeLabelType labelType,
            TransitionGroupDocNode group, SrmSettings settings, ExplicitMods lookupMods,
            bool useFilter, int minPeaks)
        {
            return MakeLibraryRankedSpectrumInfo(info, labelType, group, settings, group.Peptide.Target, lookupMods,
                null, // charges
                null, // types
                // ReadOnlyCollection enumerators are too slow, and show under a profiler
                group.IsCustomIon
                    ? settings.TransitionSettings.Filter.SmallMoleculeFragmentAdducts.ToArray()
                    : settings.TransitionSettings.Filter.PeptideProductCharges.ToArray(),
                group.IsCustomIon
                    ? settings.TransitionSettings.Filter.SmallMoleculeIonTypes.ToArray()
                    : settings.TransitionSettings.Filter.PeptideIonTypes.ToArray(),
                null, useFilter, false, minPeaks);
        }

        private static LibraryRankedSpectrumInfo MakeLibraryRankedSpectrumInfo(SpectrumPeaksInfo info,
            IsotopeLabelType labelType,
            TransitionGroupDocNode groupDocNode, SrmSettings settings,
            Target lookupSequence, ExplicitMods lookupMods,
            IEnumerable<Adduct> charges, IEnumerable<IonType> types,
            IEnumerable<Adduct> rankCharges, IEnumerable<IonType> rankTypes,
            double? score, bool useFilter, bool matchAll, int minPeaks)
        {
            return SpectrumRanker.RankSpectrum(info, labelType, groupDocNode, settings, lookupSequence, lookupMods,
                charges, types, rankCharges, rankTypes, score, useFilter, matchAll, minPeaks);
        }

        public LibraryRankedSpectrumInfo(IsotopeLabelType labelType, double tolerance, IEnumerable<RankedMI> spectrum, double? score)
        {
            LabelType = labelType;
            _spectrum = ImmutableList.ValueOf(spectrum);
            Tolerance = tolerance;
            Score = score;
        }

        public double? Score { get; private set; }

        public LibraryRankedSpectrumInfo ChangeScore(double? score)
        {
            return ChangeProp(ImClone(this), im=>im.Score = score);
        }
        public IsotopeLabelType LabelType { get; private set; }
        public double Tolerance { get; private set; }

        public IList<RankedMI> Peaks { get { return _spectrum; } }

        public IEnumerable<RankedMI> PeaksRanked
        {
            get
            {
                foreach (RankedMI rmi in _spectrum)
                {
                    if (rmi.Rank > 0)
                        yield return rmi;
                }
            }
        }

        public IEnumerable<RankedMI> PeaksMatched
        {
            get
            {
                foreach (RankedMI rmi in _spectrum)
                {
                    if (rmi.MatchedIons != null)
                        yield return rmi;
                }
            }
        }

        public IEnumerable<double> MZs
        {
            get
            {
                foreach (RankedMI rmi in _spectrum)
                    yield return rmi.ObservedMz;
            }
        }

        public IEnumerable<double> Intensities
        {
            get
            {
                foreach (RankedMI rmi in _spectrum)
                    yield return rmi.Intensity;
            }
        }

        public sealed class RankedMI : Immutable
        {
            public override string ToString()
            {
                var annotation = !HasAnnotations ?
                    string.Empty:
                    string.Format(@" ({0})", string.Join(@"/", _mi.Annotations.Where(a => !SpectrumPeakAnnotation.IsNullOrEmpty(a))).Select(a => a.ToString()));
                return string.Format(@"i={0}, mz={1}{2}", _mi.Intensity, _mi.Mz, annotation);
            }

            private SpectrumPeaksInfo.MI _mi;

            public static readonly RankedMI EMPTY = new RankedMI(new SpectrumPeaksInfo.MI(), 0);

            public RankedMI(SpectrumPeaksInfo.MI mi, int indexMz)
            {
                _mi = mi;

                IndexMz = indexMz;
            }

            public RankedMI ChangeAnnotations(List<SpectrumPeakAnnotation> newAnnotations)
            {
                var newMI = _mi.ChangeAnnotations(newAnnotations);
                if (!Equals(_mi, newMI))
                {
                    return ChangeProp(ImClone(this), im => im._mi = newMI);
                }
                return this;
            }

            public RankedMI ChangeRank(int rank)
            {
                return ChangeProp(ImClone(this), im => im.Rank = rank);
            }


            public int Rank { get; private set; }

            public int IndexMz { get; private set; }

            public SpectrumPeaksInfo.MI MI { get { return _mi; } }
            public float Intensity { get { return _mi.Intensity; } }

            public bool Quantitative { get { return _mi.Quantitative; } }

            public bool HasAnnotations { get { return !(_mi.Annotations == null || _mi.Annotations.All(SpectrumPeakAnnotation.IsNullOrEmpty)); } }
            public IList<SpectrumPeakAnnotation> Annotations { get { return _mi.Annotations; } }
            public CustomIon AnnotationsAggregateDescriptionIon { get { return _mi.AnnotationsAggregateDescriptionIon; } } 

            public double ObservedMz { get { return _mi.Mz; } }

            public ImmutableList<MatchedFragmentIon> MatchedIons { get; private set; }

            public RankedMI ChangeMatchedIons(IEnumerable<MatchedFragmentIon> matchedIons)
            {
                return ChangeProp(ImClone(this), im => im.MatchedIons = ImmutableList.ValueOf(matchedIons));
            }

            private bool Equals(RankedMI other)
            {
                return _mi.Equals(other._mi) && Rank == other.Rank && IndexMz == other.IndexMz && Equals(MatchedIons, other.MatchedIons);
            }

            public override bool Equals(object obj)
            {
                return ReferenceEquals(this, obj) || obj is RankedMI other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = _mi.GetHashCode();
                    hashCode = (hashCode * 397) ^ Rank;
                    hashCode = (hashCode * 397) ^ IndexMz;
                    hashCode = (hashCode * 397) ^ (MatchedIons != null ? MatchedIons.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }
    }

    public class MatchedFragmentIon : Immutable
    {
        public MatchedFragmentIon(IonType ionType, int ordinal, Adduct charge, string fragmentName, TransitionLosses losses, double predictedMz)
        {
            IonType = ionType;
            Ordinal = ordinal;
            Charge = charge;
            Losses = losses;
            PredictedMz = predictedMz;
            FragmentName = fragmentName;
        }

        public IonType IonType { get; private set; }

        public int Ordinal { get; private set; }

        public Adduct Charge { get; private set; }

        public TransitionLosses Losses { get; private set; }

        public double PredictedMz { get; private set; }

        public string FragmentName { get; private set; } // For small molecules

        public IonChain ComplexFragmentIonName { get; private set; }

        public MatchedFragmentIon ChangeComplexFragmentIonName(IonChain complexFragmentIonName)
        {
            return ChangeProp(ImClone(this), im => im.ComplexFragmentIonName = complexFragmentIonName);
        }

        public MatchedFragmentIon ChangePredictedMz(double predictedMz)
        {
            return ChangeProp(ImClone(this), im => im.PredictedMz = predictedMz);
        }
        public bool HasVisibleLoss(ICollection<string> showLosses)
        {
            return showLosses == null || Losses == null ||
                   Losses.Losses.Any(loss => showLosses.Contains(loss.Loss.Formula));
        }

    }
}
