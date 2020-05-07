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
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using RankParams = pwiz.Skyline.Model.Lib.SpectrumRanker.RankParams;
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

        public sealed class RankedMI
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
                    return new RankedMI(newMI, IndexMz);
                }
                return this;
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

            public IList<MatchedFragmentIon> MatchedIons { get; private set; }

            public void CalculateRank(RankParams rp)
            {
                // Rank based on filtered range, if the settings use it in picking
                bool filter = (rp.pick == TransitionLibraryPick.filter);

                if (rp.knownFragments != null)
                {
                    // Small molecule work - we only know about the fragments we're given, we can't predict others
                    foreach (IonType type in rp.types)
                    {
                        if (Transition.IsPrecursor(type))
                        {
                            if (!MatchNext(rp, type, 0, null, rp.precursorAdduct, null, 0, filter, 0, 0, 0))
                            {
                                // If matched return.  Otherwise look for other ion types.
                                if (rp.matched)
                                {
                                    rp.Clean();
                                    return;
                                }
                            }
                        }
                        else
                        {
                            for (var i = 0; i < rp.knownFragments.Count; i++)
                            {
                                var fragment = rp.knownFragments[i];
                                if (!MatchNext(rp, IonType.custom, i, null, fragment.Adduct, fragment.Name, 0, filter, 0, 0, fragment.Mz))
                                {
                                    // If matched return.  Otherwise look for other ion types.
                                    if (rp.matched)
                                    {
                                        rp.Clean();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    return;
                }

                // Look for a predicted match within the acceptable tolerance
                int len = rp.massesMatch.GetLength(1);
                foreach (IonType type in rp.types)
                {
                    if (Transition.IsPrecursor(type))
                    {
                        foreach (var losses in TransitionGroup.CalcTransitionLosses(type, 0, rp.massType, rp.potentialLosses))
                        {
                            if (!MatchNext(rp, type, len, losses, rp.precursorAdduct, null, len + 1, filter, len, len, 0))
                            {
                                // If matched return.  Otherwise look for other ion types.
                                if (rp.matched)
                                {
                                    rp.Clean();
                                    return;
                                }
                            }
                        }
                        continue;
                    }

                    foreach (var adduct in rp.adducts)
                    {
                        // Precursor charge can never be lower than product ion charge.
                        if (Math.Abs(rp.precursorAdduct.AdductCharge) < Math.Abs(adduct.AdductCharge))
                            continue;

                        int start = 0, end = 0;
                        double startMz = 0;
                        if (filter)
                        {
                            start = rp.startFinder.FindStartFragment(rp.massesMatch, type, adduct,
                                                                     rp.precursorMz, rp.filter.PrecursorMzWindow, out startMz);
                            end = rp.endFinder.FindEndFragment(type, start, len);
                            if (Transition.IsCTerminal(type))
                                Helpers.Swap(ref start, ref end);
                        }

                        // These inner loops are performance bottlenecks, and the following
                        // code duplication proved the fastest implementation under a
                        // profiler.  Apparently .NET failed to inline an attempt to put
                        // the loop contents in a function.
                        if (Transition.IsCTerminal(type))
                        {
                            for (int i = len - 1; i >= 0; i--)
                            {
                                foreach (var losses in TransitionGroup.CalcTransitionLosses(type, i, rp.massType, rp.potentialLosses))
                                {
                                    if (!MatchNext(rp, type, i, losses, adduct, null, len, filter, end, start, startMz))
                                    {
                                        if (rp.matched)
                                        {
                                            rp.Clean();
                                            return;
                                        }
                                        i = -1; // Terminate loop on i
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < len; i++)
                            {
                                foreach (var losses in TransitionGroup.CalcTransitionLosses(type, i, rp.massType, rp.potentialLosses))
                                {
                                    if (!MatchNext(rp, type, i, losses, adduct, null, len, filter, end, start, startMz))
                                    {
                                        if (rp.matched)
                                        {
                                            rp.Clean();
                                            return;
                                        }
                                        i = len; // Terminate loop on i
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            private bool MatchNext(RankParams rp, IonType type, int offset, TransitionLosses losses, Adduct adduct, string fragmentName, int len, bool filter, int end, int start, double startMz)
            {
                bool isFragment = !Transition.IsPrecursor(type);
                var ionMass = isFragment ? rp.massesMatch[type, offset] : rp.massPreMatch;
                if (losses != null)
                    ionMass -= losses.Mass;
                double ionMz = SequenceMassCalc.GetMZ(ionMass, adduct);
                // Unless trying to match everything, stop looking outside the instrument range
                if (!rp.matchAll && !rp.HasLosses && ionMz > rp.maxMz)
                    return false;
                // Check filter properties, if apropriate
                if ((rp.matchAll || ionMz >= rp.minMz) && Math.Abs(ionMz - ObservedMz) < rp.tolerance)
                {
                    // Make sure each m/z value is only used for the most intense peak
                    // that is within the tolerance range.
                    if (rp.IsSeen(ionMz))
                        return true; // Keep looking
                    rp.Seen(ionMz);

                    int ordinal = Transition.OffsetToOrdinal(type, offset, len + 1);
                    // If this m/z aready matched a different ion, just remember the second ion.
                    var predictedMass = isFragment ? rp.massesPredict[type, offset] : rp.massPrePredict;
                    if (losses != null)
                        predictedMass -= losses.Mass;
                    double predictedMz = SequenceMassCalc.GetMZ(predictedMass, adduct);
                    if (MatchedIons != null)
                    {
                        // If first type was excluded from causing a ranking, but second does, then make it the first
                        // Otherwise, this can cause very mysterious failures to rank transitions that appear in the
                        // document.
                        var match = new MatchedFragmentIon(type, ordinal, adduct, fragmentName, losses, predictedMz);
                        if (Rank == 0 && ApplyRanking(rp, type, offset, losses, adduct, filter, start, end, startMz, ionMz))
                        {
                            MatchedIons.Insert(0, match);
                        }
                        else
                        {
                            MatchedIons.Add(match);
                        }
                        if (MatchedIons.Count < RankParams.MAX_MATCH)
                            return true;

                        rp.matched = true;
                        return false;
                    }
                    
                    // Avoid using the same predicted m/z on two different peaks
                    if (predictedMz == ionMz || !rp.IsSeen(predictedMz))
                    {
                        rp.Seen(predictedMz);

                        ApplyRanking(rp, type, offset, losses, adduct, filter, start, end, startMz, ionMz);

                        MatchedIons = new List<MatchedFragmentIon> { new MatchedFragmentIon(type, ordinal, adduct, fragmentName, losses, predictedMz) };
                        rp.matched = !rp.matchAll;
                        return rp.matchAll;
                    }
                }
                // Stop looking once the mass has been passed, unless there are losses to consider
                if (rp.HasLosses)
                    return true;
                return (ionMz <= ObservedMz);
            }

            private bool ApplyRanking(RankParams rp, IonType type, int offset, TransitionLosses losses, Adduct adduct, bool filter,
                int start, int end, double startMz, double ionMz)
            {
                // Avoid ranking precursor ions without losses, if the precursor isotopes will
                // not be taken from product ions
                if (!rp.excludePrecursorIsotopes || type != IonType.precursor || losses != null)
                {
                    if (!filter || rp.tranSettings.Accept(rp.sequence, rp.precursorMz, type, offset, ionMz, start, end, startMz))
                    {
                        if (!rp.matchAll || (rp.minMz <= ionMz && ionMz <= rp.maxMz &&
                                             rp.rankTypes.Contains(type) &&
                                             (!rp.rankLimit.HasValue || rp.Ranked < rp.rankLimit) &&
                                             (rp.rankCharges.Contains(Math.Abs(adduct.AdductCharge)) || type == IonType.precursor))) // CONSIDER(bspratt) we may eventually want adduct-level control for small molecules, not just abs charge
                        {
                            Rank = rp.RankNext();
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }

    public class MatchedFragmentIon
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
    }
}
