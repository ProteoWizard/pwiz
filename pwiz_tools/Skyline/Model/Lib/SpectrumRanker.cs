/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using RankedMI = pwiz.Skyline.Model.Lib.LibraryRankedSpectrumInfo.RankedMI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// Matches fragments seen in a spectrum to m/z values predicted for fragments.
    /// </summary>
    public class SpectrumRanker
    {
        /// <summary>
        /// Returns a LibraryRankedSpectrumInfo
        /// </summary>
        /// <param name="info">The spectrum to be ranked</param>
        /// <param name="labelType">The IsotopeLabelType for the peptide in the library. This might be different from the
        /// LabelType on the GroupDocNode.</param>
        /// <param name="groupDocNode">The Transition Group from the user's document</param>
        /// <param name="settings"></param>
        /// <param name="lookupSequence"></param>
        /// <param name="lookupMods"></param>
        /// <param name="charges">The set of charges that the user is choosing to show in the spectrum viewer.</param>
        /// <param name="types">The set of ion types to be displayed</param>
        /// <param name="rankCharges">The set of charges that are enabled in the document's Transition Settings</param>
        /// <param name="rankTypes">The set of ion types in the user's transition settings</param>
        /// <param name="score">the score to assign to the spectrum. If it is null, then the spectrum gets the score from transition group's LibInfo</param>
        /// <param name="useFilter">true if this list is being generated in order to show the filtered list of potential transitions</param>
        /// <param name="matchAll">true if peaks matched peaks should be given a list of all of the ion types that they match, instead
        /// of only being annotated with the first matching one</param>
        /// <param name="minPeaks">The minimum number of peaks to match, or -1 to match as many as possible</param>
        /// <returns></returns>
        public static LibraryRankedSpectrumInfo RankSpectrum(SpectrumPeaksInfo info,
            IsotopeLabelType labelType,
            TransitionGroupDocNode groupDocNode, SrmSettings settings,
            Target lookupSequence, ExplicitMods lookupMods,
            IEnumerable<Adduct> charges, IEnumerable<IonType> types,
            IEnumerable<Adduct> rankCharges, IEnumerable<IonType> rankTypes,
            double? score, bool useFilter, bool matchAll, int minPeaks)
        {
            var targetInfo = new TargetInfo(labelType, groupDocNode, lookupSequence, lookupMods);
            var fragmentFilter = new FragmentFilter(settings.TransitionSettings, rankCharges, rankTypes).ChangeMatchAll(matchAll);
            if (!useFilter)
            {
                bool isProteomic = groupDocNode.TransitionGroup.IsProteomic;
                fragmentFilter = fragmentFilter.ChangeUseFilter(false);
                fragmentFilter = fragmentFilter
                    .ChangeAdductsToDisplay(charges ?? GetRanked(fragmentFilter.RankedAdducts,
                                                isProteomic
                                                    ? Transition.DEFAULT_PEPTIDE_CHARGES
                                                    : Transition.DEFAULT_MOLECULE_CHARGES));
                fragmentFilter = fragmentFilter.ChangeIonTypesToDisplay(
                    types ?? GetRanked(fragmentFilter.RankedIonTypes,
                        isProteomic ? Transition.PEPTIDE_ION_TYPES : Transition.MOLECULE_ION_TYPES));
                fragmentFilter = fragmentFilter.ChangeMatchAll(true);
            }
            else
            {
                if (null != charges)
                {
                    fragmentFilter = fragmentFilter.ChangeAdductsToDisplay(charges);
                }

                if (null != types)
                {
                    fragmentFilter = fragmentFilter.ChangeIonTypesToDisplay(types);
                }
            }
            bool limitRanks =
                groupDocNode.IsCustomIon && // For small molecules, cap the number of ranked ions displayed if we don't have any peak metadata
                groupDocNode.Transitions.Any(t => string.IsNullOrEmpty(t.FragmentIonName));
            if (limitRanks)
            {
                fragmentFilter = fragmentFilter.ChangeRankLimit(settings.TransitionSettings.Libraries.IonCount);
            }
            // If no library filtering will happen, return all rankings for view in the UI
            if (!useFilter || fragmentFilter.LibraryPick == TransitionLibraryPick.none)
            {
                if (fragmentFilter.LibraryPick == TransitionLibraryPick.none)
                    fragmentFilter = fragmentFilter.ChangeLibraryPick(TransitionLibraryPick.all);
                fragmentFilter = fragmentFilter.ChangeFragmentMatchCount(null);
            }

            var spectrumRanker = new SpectrumRanker(targetInfo, settings, fragmentFilter);
            return spectrumRanker.RankSpectrum(info, minPeaks, score);
        }


        public SpectrumRanker(TargetInfo targetInfo, SrmSettings settings,
            FragmentFilter fragmentFilter)
        {
            TargetInfoObj = targetInfo;
            FragmentFilterObj = fragmentFilter;
            var groupDocNode = TargetInfoObj.TransitionGroupDocNode;
            TransitionGroup group = groupDocNode.TransitionGroup;
            bool isProteomic = group.IsProteomic;

            bool limitRanks =
                groupDocNode.IsCustomIon && // For small molecules, cap the number of ranked ions displayed if we don't have any peak metadata
                groupDocNode.Transitions.Any(t => string.IsNullOrEmpty(t.FragmentIonName));
            RankLimit = limitRanks ? settings.TransitionSettings.Libraries.IonCount : (int?) null;

            // Get necessary mass calculators and masses
            var labelType = targetInfo.SpectrumLabelType;
            var lookupMods = targetInfo.LookupMods;
            var calcMatchPre = settings.GetPrecursorCalc(labelType, lookupMods);
            var calcMatch = isProteomic ? settings.GetFragmentCalc(labelType, lookupMods) : settings.GetDefaultFragmentCalc();
            var calcPredict = isProteomic ? settings.GetFragmentCalc(group.LabelType, lookupMods) : calcMatch;
            MoleculeMasses moleculeMasses;
            if (null != lookupMods && lookupMods.HasCrosslinks)
            {
                moleculeMasses = GetCrosslinkMasses(settings);
            }
            else
            {
                if (isProteomic && Sequence.IsProteomic)
                {
                    moleculeMasses = new MoleculeMasses(
                        SequenceMassCalc.GetMZ(calcMatchPre.GetPrecursorMass(Sequence), PrecursorAdduct),
                        new IonMasses(calcMatch.GetPrecursorFragmentMass(Sequence),
                            calcMatch.GetFragmentIonMasses(Sequence)));
                }
                else if (!isProteomic && !Sequence.IsProteomic)
                {
                    string isotopicFormula;
                    var knownFragments = new List<MatchedFragmentIon>();
                    foreach (var tran in groupDocNode.Transitions)
                    {
                        if (tran.Transition.IsNonPrecursorNonReporterCustomIon())
                        {

                            knownFragments.Add(new MatchedFragmentIon(IonType.custom, knownFragments.Count + 1,
                                tran.Transition.Adduct,
                                tran.GetFragmentIonName(CultureInfo.CurrentCulture,
                                    settings.TransitionSettings.Libraries.IonMatchTolerance),
                                null,
                                tran.Mz));
                        }
                    }

                    var ionMasses =
                        new IonMasses(calcMatch.GetPrecursorFragmentMass(Sequence), IonTable<TypedMass>.EMPTY)
                            .ChangeKnownFragments(knownFragments);
                    moleculeMasses =
                        new MoleculeMasses(
                            SequenceMassCalc.GetMZ(
                                calcMatchPre.GetPrecursorMass(Sequence.Molecule, null, PrecursorAdduct,
                                    out isotopicFormula), PrecursorAdduct), ionMasses);
                }
                else
                {
                    moleculeMasses = new MoleculeMasses(0.0,
                        new IonMasses(TypedMass.ZERO_MONO_MASSH, IonTable<TypedMass>.EMPTY));
                }

                if (!ReferenceEquals(calcPredict, calcMatch))
                {
                    var ionTable = moleculeMasses.MatchIonMasses.FragmentMasses;
                    if (Sequence.IsProteomic
                    ) // CONSIDER - eventually we may be able to predict fragments for small molecules?
                        ionTable = calcPredict.GetFragmentIonMasses(Sequence);
                    moleculeMasses =
                        moleculeMasses.ChangePredictIonMasses(new IonMasses(
                            calcPredict.GetPrecursorFragmentMass(Sequence),
                            ionTable));
                }
            }

            MoleculeMassesObj = moleculeMasses;

            // Get values of interest from the settings.
            TransitionSettings = settings.TransitionSettings;

            // Get potential losses to all fragments in this peptide
            PotentialLosses = TransitionGroup.CalcPotentialLosses(Sequence, settings.PeptideSettings.Modifications,
                lookupMods, MassType);
        }

        public TargetInfo TargetInfoObj { get; private set; }

        public TransitionLibraryPick Pick
        {
            get { return FragmentFilterObj.LibraryPick; }
        }

        public double MinMz
        {
            get { return TransitionSettings.Instrument.MinMz; }
        }

        public double MaxMz
        {
            get { return TransitionSettings.Instrument.MaxMz; }
        }

        public TransitionSettings TransitionSettings { get; private set; }

        public TransitionLibraries Libraries
        {
            get { return TransitionSettings.Libraries; }
        }

        public MassType MassType
        {
            get { return TransitionSettings.Prediction.FragmentMassType; }
        }

        public bool MatchAll
        {
            get { return FragmentFilterObj.MatchAll; }
        }

        private MoleculeMasses MoleculeMassesObj { get; set; }

        public TransitionGroupDocNode GroupDocNode
        {
            get { return TargetInfoObj.TransitionGroupDocNode; }
        }
        public Adduct PrecursorAdduct
        {
            get { return GroupDocNode.PrecursorAdduct; }
        }
        public IsotopeLabelType PredictLabelType
        {
            get { return GroupDocNode.LabelType; }
        }

        public FragmentFilter FragmentFilterObj { get; private set; }

        public bool UseFilter
        {
            get { return FragmentFilterObj.UseFilter; }
        }

        public IList<IList<ExplicitLoss>> PotentialLosses { get; }

        public LibraryRankedSpectrumInfo RankSpectrum(SpectrumPeaksInfo info, int minPeaks, double? score)
        {
            var ionsToReturn = FragmentFilterObj.FragmentMatchCount;
            RankingState rankingState = new RankingState()
            {
                matchAll = MatchAll,

            };
            // Get the library spectrum mass-intensity pairs
            IList<SpectrumPeaksInfo.MI> listMI = info.Peaks;

            // Because sorting and matching observed ions with predicted
            // ions appear as bottlenecks in a profiler, a minimum number
            // of peaks may be supplied to allow the use of a 2-phase linear
            // filter that can significantly reduce the number of peaks
            // needing the O(n*log(n)) sorting and the O(n*m) matching.

            int len = listMI.Count;
            float intensityCutoff = 0;

            if (minPeaks != -1)
            {
                // Start searching for good cut-off at mean intensity.
                double totalIntensity = info.Intensities.Sum();

                FindIntensityCutoff(listMI, 0, (float)(totalIntensity / len) * 2, minPeaks, 1, ref intensityCutoff, ref len);
            }
            // Create filtered peak array storing original index for m/z ordering
            // to avoid needing to sort to return to this order.
            RankedMI[] arrayRMI = new RankedMI[len];
            // Detect when m/z values are out of order, and use the expensive sort
            // by m/z to correct this.
            double lastMz = double.MinValue;
            bool sortMz = false;
            for (int i = 0, j = 0, lenOrig = listMI.Count; i < lenOrig; i++)
            {
                SpectrumPeaksInfo.MI mi = listMI[i];
                if (mi.Intensity >= intensityCutoff || intensityCutoff == 0)
                {
                    arrayRMI[j] = new RankedMI(mi, j);
                    j++;
                }
                if (!ionsToReturn.HasValue)
                {
                    if (mi.Mz < lastMz)
                        sortMz = true;
                    lastMz = mi.Mz;
                }
            }

            // The one expensive sort is used to determine rank order
            // by intensity, or m/z in case of a tie.
            Array.Sort(arrayRMI, OrderIntensityDesc);


            RankedMI[] arrayResult = new RankedMI[ionsToReturn.HasValue ? ionsToReturn.Value : arrayRMI.Length];

            foreach (RankedMI rmi in arrayRMI)
            {
                var rankedRmi = CalculateRank(rankingState, rmi);

                // If not filtering for only the highest ionMatchCount ranks
                if (!ionsToReturn.HasValue)
                {
                    // Put the ranked record back where it started in the
                    // m/z ordering to avoid a second sort.
                    arrayResult[rmi.IndexMz] = rankedRmi;
                }
                // Otherwise, if this ion was ranked, add it to the result array
                else if (rankedRmi.Rank > 0)
                {
                    int countRanks = rankedRmi.Rank;
                    arrayResult[countRanks - 1] = rankedRmi;
                    // And stop when the array is full
                    if (countRanks == ionsToReturn.Value)
                        break;
                }
            }

            // Is this a theoretical library with no intensity variation? If so it can't be ranked.
            // If it has any interesting peak annotations, pass those through
            if (rankingState.Ranked == 0 && arrayRMI.All(rmi => rmi.Intensity == arrayRMI[0].Intensity))
            {
                // Only do this if we have been asked to limit the ions matched, and there are any annotations
                if (ionsToReturn.HasValue && arrayRMI.Any(rmi => rmi.HasAnnotations))
                {
                    // Pass through anything with an annotation as being of probable interest
                    arrayResult = arrayRMI.Where(rmi => rmi.HasAnnotations).ToArray();
                    ionsToReturn = null;
                }
            }

            // If not enough ranked ions were found, fill the rest of the results array
            if (ionsToReturn.HasValue)
            {
                for (int i = rankingState.Ranked; i < ionsToReturn.Value; i++)
                    arrayResult[i] = RankedMI.EMPTY;
            }
            // If all ions are to be included, and some were found out of order, then
            // the expensive full sort by m/z is necessary.
            else if (sortMz)
            {
                Array.Sort(arrayResult, OrderMz);
            }

            double? spectrumScore;
            if (score == null && GroupDocNode.HasLibInfo && GroupDocNode.LibInfo is BiblioSpecSpectrumHeaderInfo libInfo)
            {
                spectrumScore = libInfo.Score;
            }
            else
            {
                spectrumScore = score;
            }
            return new LibraryRankedSpectrumInfo(PredictLabelType, Libraries.IonMatchTolerance, arrayResult, spectrumScore);
        }

        /// <summary>
        /// Make sure array ordering starts with ranked items to avoid changing ranked items between
        /// filtered and unfiltered queries
        /// </summary>
        private static IEnumerable<TItem> GetRanked<TItem>(IEnumerable<TItem> rankItems, IEnumerable<TItem> allItems)
        {
            var setSeen = new HashSet<TItem>();
            foreach (var item in rankItems)
            {
                setSeen.Add(item);
                yield return item;
            }
            foreach (var item in allItems)
            {
                if (!setSeen.Contains(item))
                    yield return item;
            }
        }

        // ReSharper disable ParameterTypeCanBeEnumerable.Local
        private static void FindIntensityCutoff(IList<SpectrumPeaksInfo.MI> listMI, float left, float right, int minPeaks, int calls, ref float cutoff, ref int len)
            // ReSharper restore ParameterTypeCanBeEnumerable.Local
        {
            if (calls < 3)
            {
                float mid = (left + right) / 2;
                int count = FilterPeaks(listMI, mid);
                if (count < minPeaks)
                    FindIntensityCutoff(listMI, left, mid, minPeaks, calls + 1, ref cutoff, ref len);
                else
                {
                    cutoff = mid;
                    len = count;
                    if (count > minPeaks * 1.5)
                        FindIntensityCutoff(listMI, mid, right, minPeaks, calls + 1, ref cutoff, ref len);
                }
            }
        }

        private static int FilterPeaks(IEnumerable<SpectrumPeaksInfo.MI> listMI, float intensityCutoff)
        {
            int nonNoise = 0;
            foreach (SpectrumPeaksInfo.MI mi in listMI)
            {
                if (mi.Intensity >= intensityCutoff)
                    nonNoise++;
            }
            return nonNoise;
        }

        private static int OrderIntensityDesc(RankedMI mi1, RankedMI mi2)
        {
            float i1 = mi1.Intensity, i2 = mi2.Intensity;
            if (i1 > i2)
                return -1;
            if (i1 < i2)
                return 1;
            return -OrderMz(mi1, mi2);
        }

        private static int OrderMz(RankedMI mi1, RankedMI mi2)
        {
            return (mi1.ObservedMz.CompareTo(mi2.ObservedMz));
        }

        public Target Sequence
        {
            get { return TargetInfoObj.LookupSequence; }
        }
        public const int MAX_MATCH = 6;

        private class MoleculeMasses : Immutable
        {
            public MoleculeMasses(double precursorMz, IonMasses ionMasses)
            {
                this.precursorMz = precursorMz;
                PredictIonMasses = MatchIonMasses = ionMasses;
            }
            public double precursorMz { get; private set; }
            public IonMasses MatchIonMasses { get; private set; }
            public IonMasses PredictIonMasses { get; private set; }

            public MoleculeMasses ChangePredictIonMasses(IonMasses predictIonMasses)
            {
                return ChangeProp(ImClone(this), im => im.PredictIonMasses = predictIonMasses);
            }
        }

        private MatchedFragmentIon MakeMatchedFragmentIon(IonType ionType, int ionIndex, Adduct adduct, TransitionLosses transitionLosses, out double matchMz)
        {
            var moleculeMasses = MoleculeMassesObj;
            int ordinal;
            int peptideLength = TargetInfoObj.LookupSequence.Sequence?.Length ?? 0;
            TypedMass matchMass, predictedMass;
            if (ionType == IonType.precursor)
            {
                matchMass = moleculeMasses.MatchIonMasses.PrecursorMass;
                predictedMass = moleculeMasses.PredictIonMasses.PrecursorMass;
                ordinal = peptideLength;
            }
            else
            {
                matchMass = moleculeMasses.MatchIonMasses.GetIonMass(ionType, ionIndex);
                predictedMass = moleculeMasses.PredictIonMasses.GetIonMass(ionType, ionIndex);
                ordinal = Transition.OffsetToOrdinal(ionType, ionIndex, peptideLength);
            }

            if (transitionLosses != null)
            {
                matchMass -= transitionLosses.Mass;
                predictedMass -= transitionLosses.Mass;
            }

            double predictedMz = SequenceMassCalc.GetMZ(predictedMass, adduct);
            matchMz = SequenceMassCalc.GetMZ(matchMass, adduct);
            return new MatchedFragmentIon(ionType, ordinal, adduct, null, transitionLosses, predictedMz);
        }

        private int OffsetToOrdinal(IonType ionType, int ionIndex)
        {
            switch (ionType)
            {
                case IonType.custom:
                    return ionIndex + 1;
                case IonType.precursor:
                    return TargetInfoObj.LookupSequence.Sequence.Length;
                default:
                    return Transition.OffsetToOrdinal(ionType, ionIndex, TargetInfoObj.LookupSequence.Sequence.Length);
            }
        }

        private int OrdinalToOffset(IonType ionType, int ordinal)
        {
            switch (ionType)
            {
                case IonType.custom:
                    return ordinal - 1;
                case IonType.precursor:
                    return ordinal - 1;
                default:
                    return Transition.OrdinalToOffset(ionType, ordinal, TargetInfoObj.LookupSequence.Sequence.Length);
            }
        }

        private class IonMasses : Immutable
        {
            public IonMasses(TypedMass precursorMass, IonTable<TypedMass> fragmentMasses)
            {
                PrecursorMass = precursorMass;
                FragmentMasses = fragmentMasses;
            }

            public TypedMass PrecursorMass { get; private set; }

            public IonMasses ChangePrecursorMass(TypedMass precursorMass)
            {
                return ChangeProp(ImClone(this), im => im.PrecursorMass = precursorMass);
            }
            public IonTable<TypedMass> FragmentMasses { get; private set; }

            public IonMasses ChangeFragmentMasses(IonTable<TypedMass> fragmentMasses)
            {
                return ChangeProp(ImClone(this), im => im.FragmentMasses = fragmentMasses);
            }
            public ImmutableList<MatchedFragmentIon> KnownFragments { get; private set; }

            public IonMasses ChangeKnownFragments(IEnumerable<MatchedFragmentIon> knownFragments)
            {
                return ChangeProp(ImClone(this), im => im.KnownFragments = ImmutableList.ValueOf(knownFragments));
            }

            public TypedMass GetIonMass(IonType ionType, int ionIndex)
            {
                switch (ionType)
                {
                    case IonType.precursor:
                        return PrecursorMass;
                    case IonType.custom:
                        Assume.Fail();
                        break;
                }

                return FragmentMasses[ionType, ionIndex];
            }
        }

        public class TargetInfo : Immutable
        {
            public TargetInfo(IsotopeLabelType spectrumLabelType, TransitionGroupDocNode transitionGroupDocNode,
                Target lookupSequence, ExplicitMods lookupMods)
            {
                SpectrumLabelType = spectrumLabelType;
                TransitionGroupDocNode = transitionGroupDocNode;
                LookupSequence = lookupSequence;
                LookupMods = lookupMods;
            }
            public IsotopeLabelType SpectrumLabelType { get; private set; }
            public TransitionGroupDocNode TransitionGroupDocNode { get; private set; }
            public Target LookupSequence { get; private set; }
            public ExplicitMods LookupMods { get; private set; }
        }

        public int? RankLimit { get; private set; }

        public ImmutableList<Adduct> Adducts
        {
            get { return FragmentFilterObj.AdductsToDisplay; }
        }
        public ImmutableList<IonType> Types
        {
            get { return FragmentFilterObj.IonTypesToDisplay; }
        }
        public ImmutableList<int> RankCharges
        {
            get { return FragmentFilterObj.RankedCharges; }
        }
        public ImmutableList<IonType> RankTypes
        {
            get { return FragmentFilterObj.RankedIonTypes; }
        }

        public bool HasLosses
        {
            get { return PotentialLosses != null && PotentialLosses.Count > 0; }
        }

        private class RankingState
        {
            public bool matchAll { get; set; }
            public bool matched { get; set; }

            private readonly HashSet<double> _seenMz = new HashSet<double>();
            private double _seenFirst;

            private int _rank = 1;
            public int Ranked { get { return _rank - 1; } }
            public int RankNext() { return _rank++; }
            public void Seen(double mz)
            {
                if (matchAll && _seenFirst == 0)
                    _seenFirst = mz;
                else
                    _seenMz.Add(mz);
            }
            public bool IsSeen(double mz)
            {
                return _seenMz.Contains(mz);
            }


            public void Clean()
            {
                if (_seenFirst != 0)
                    _seenMz.Add(_seenFirst);
                matched = false;
            }
        }
        private RankedMI CalculateRank(RankingState rankingState, RankedMI rankedMI)
        {
            // Rank based on filtered range, if the settings use it in picking
            bool filter = (Pick == TransitionLibraryPick.filter);

            var knownFragments = MoleculeMassesObj.MatchIonMasses.KnownFragments;
            if (knownFragments != null)
            {
                // Small molecule work - we only know about the fragments we're given, we can't predict others
                foreach (IonType type in Types)
                {
                    if (Transition.IsPrecursor(type))
                    {
                        var matchedFragmentIon = MakeMatchedFragmentIon(type, 0, PrecursorAdduct, null, out double matchMz);

                        if (!MatchNext(rankingState, matchMz, matchedFragmentIon, false, 0, 0, 0, ref rankedMI))
                        {
                            // If matched return.  Otherwise look for other ion types.
                            if (rankingState.matched)
                            {
                                rankingState.Clean();
                                return rankedMI;
                            }
                        }
                    }
                    else
                    {
                        for (var i = 0; i < knownFragments.Count; i++)
                        {
                            var fragment = knownFragments[i];
                            double matchMz = MoleculeMassesObj.PredictIonMasses.KnownFragments[i].PredictedMz;
                            if (!MatchNext(rankingState, matchMz, fragment, false, 0, 0, fragment.PredictedMz, ref rankedMI))
                            {
                                // If matched return.  Otherwise look for other ion types.
                                if (rankingState.matched)
                                {
                                    rankingState.Clean();
                                    return rankedMI;
                                }
                            }
                        }
                    }
                }
                return rankedMI;
            }

            // Look for a predicted match within the acceptable tolerance
            int len = MoleculeMassesObj.MatchIonMasses.FragmentMasses.GetLength(1);
            foreach (IonType type in Types)
            {
                if (Transition.IsPrecursor(type))
                {
                    foreach (var losses in TransitionGroup.CalcTransitionLosses(type, 0, MassType, PotentialLosses))
                    {
                        var matchedFragmentIon =
                            MakeMatchedFragmentIon(type, 0, PrecursorAdduct, losses, out double matchMz);
                        if (!MatchNext(rankingState, matchMz, matchedFragmentIon, filter, len, len, 0, ref rankedMI))
                        {
                            // If matched return.  Otherwise look for other ion types.
                            if (rankingState.matched)
                            {
                                rankingState.Clean();
                                return rankedMI;
                            }
                        }
                    }
                    continue;
                }

                foreach (var adduct in Adducts)
                {
                    // Precursor charge can never be lower than product ion charge.
                    if (Math.Abs(PrecursorAdduct.AdductCharge) < Math.Abs(adduct.AdductCharge))
                        continue;

                    int start = 0, end = 0;
                    double startMz = 0;
                    if (filter)
                    {
                        start = TransitionSettings.Filter.FragmentRangeFirst.FindStartFragment(
                            MoleculeMassesObj.MatchIonMasses.FragmentMasses, type, adduct,
                            MoleculeMassesObj.precursorMz, TransitionSettings.Filter.PrecursorMzWindow, out startMz);
                        end = TransitionSettings.Filter.FragmentRangeLast.FindEndFragment(type, start, len);
                        if (type.IsCTerminal())
                            Helpers.Swap(ref start, ref end);
                    }

                    // These inner loops are performance bottlenecks, and the following
                    // code duplication proved the fastest implementation under a
                    // profiler.  Apparently .NET failed to inline an attempt to put
                    // the loop contents in a function.
                    if (type.IsCTerminal())
                    {
                        for (int i = len - 1; i >= 0; i--)
                        {
                            foreach (var losses in TransitionGroup.CalcTransitionLosses(type, i, MassType, PotentialLosses))
                            {
                                var matchedFragmentIon =
                                    MakeMatchedFragmentIon(type, i, adduct, losses, out double matchMz);
                                if (!MatchNext(rankingState, matchMz, matchedFragmentIon, filter, end, start, startMz, ref rankedMI))
                                {
                                    if (rankingState.matched)
                                    {
                                        rankingState.Clean();
                                        return rankedMI;
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
                            foreach (var losses in TransitionGroup.CalcTransitionLosses(type, i, MassType, PotentialLosses))
                            {
                                var matchedFragmentIon =
                                    MakeMatchedFragmentIon(type, i, adduct, losses, out double matchMz);
                                if (!MatchNext(rankingState, matchMz, matchedFragmentIon, filter, end, start, startMz, ref rankedMI))
                                {
                                    if (rankingState.matched)
                                    {
                                        rankingState.Clean();
                                        return rankedMI;
                                    }
                                    i = len; // Terminate loop on i
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return rankedMI;
        }
        private bool MatchNext(RankingState rankingState, double ionMz, MatchedFragmentIon match, bool filter, int end, int start, double startMz, ref RankedMI rankedMI)
        {
            // Unless trying to match everything, stop looking outside the instrument range
            if (!rankingState.matchAll && !HasLosses && ionMz > MaxMz)
                return false;
            // Check filter properties, if appropriate
            if ((rankingState.matchAll || ionMz >= MinMz) && Math.Abs(ionMz - rankedMI.ObservedMz) < Libraries.IonMatchTolerance)
            {
                // Make sure each m/z value is only used for the most intense peak
                // that is within the tolerance range.
                if (rankingState.IsSeen(ionMz))
                    return true; // Keep looking
                rankingState.Seen(ionMz);

                // If this m/z already matched a different ion, just remember the second ion.
                if (rankedMI.MatchedIons != null)
                {
                    // If first type was excluded from causing a ranking, but second does, then make it the first
                    // Otherwise, this can cause very mysterious failures to rank transitions that appear in the
                    // document.
                    if (rankedMI.Rank == 0 && ApplyRanking(rankingState, ionMz, match, filter, start, end, startMz, ref rankedMI))
                    {
                        rankedMI = rankedMI.ChangeMatchedIons(rankedMI.MatchedIons.Prepend(match));
                    }
                    else
                    {
                        rankedMI = rankedMI.ChangeMatchedIons(rankedMI.MatchedIons.Append(match));
                    }
                    if (rankedMI.MatchedIons.Count < MAX_MATCH)
                        return true;

                    rankingState.matched = true;
                    return false;
                }

                double predictedMz = match.PredictedMz;
                // Avoid using the same predicted m/z on two different peaks
                if (predictedMz == ionMz || !rankingState.IsSeen(predictedMz))
                {
                    rankingState.Seen(predictedMz);

                    ApplyRanking(rankingState, ionMz, match, filter, start, end, startMz, ref rankedMI);
                    rankedMI = rankedMI.ChangeMatchedIons(ImmutableList.Singleton(match));
                    rankingState.matched = !rankingState.matchAll;
                    return rankingState.matchAll;
                }
            }
            // Stop looking once the mass has been passed, unless there are losses to consider
            if (HasLosses)
                return true;
            return (ionMz <= rankedMI.ObservedMz);
        }

        public bool ExcludePrecursorIsotopes
        {
            get { return TransitionSettings.FullScan.IsEnabledMs; }
        }
        private bool ApplyRanking(RankingState rankingState, double ionMz, MatchedFragmentIon match, bool filter, int start, int end, double startMz, ref RankedMI rankedMI)
        {
            // Avoid ranking precursor ions without losses, if the precursor isotopes will
            // not be taken from product ions
            if (!ExcludePrecursorIsotopes || match.IonType != IonType.precursor || match.Losses != null)
            {
                int offset = OrdinalToOffset(match.IonType, match.Ordinal);
                var type = match.IonType;
                if (filter)
                {
                    if (TargetInfoObj.LookupMods == null || !TargetInfoObj.LookupMods.HasCrosslinks)
                    {
                        if (!TransitionSettings.Accept(Sequence, MoleculeMassesObj.precursorMz, type, offset, ionMz, start,
                            end, startMz))
                        {
                            return false;
                        }
                    }
                }
                if (rankingState.matchAll)
                {
                    if (MinMz > ionMz || ionMz > MaxMz)
                        return false;

                    if (!RankTypes.Contains(type))
                        return false;

                    if (RankLimit.HasValue && rankingState.Ranked >= RankLimit)
                        return false;

                    if (type != IonType.precursor)
                    {
                        // CONSIDER(bspratt) we may eventually want adduct-level control for small molecules, not just abs charge
                        if (!RankCharges.Contains(Math.Abs(match.Charge.AdductCharge)))
                            return false;
                    }
                }

                rankedMI = rankedMI.ChangeRank(rankingState.RankNext());
                return true;
            }

            return false;
        }

        public class FragmentFilter : Immutable
        {
            public FragmentFilter(TransitionSettings transitionSettings, IEnumerable<Adduct> rankedAdducts, IEnumerable<IonType> rankedIonTypes)
            {
                TransitionSettings = transitionSettings;
                FragmentMatchCount = TransitionSettings.Libraries.IonCount;
                LibraryPick = TransitionSettings.Libraries.Pick;
                AdductsToDisplay = RankedAdducts = ImmutableList.ValueOf(rankedAdducts);
                IonTypesToDisplay = RankedIonTypes = ImmutableList.ValueOf(rankedIonTypes);
                RankedCharges = ImmutableList.ValueOf(RankedAdducts.Select(adduct=>Math.Abs(adduct.AdductCharge)).Distinct());
                UseFilter = true;
            }

            public TransitionSettings TransitionSettings { get; private set; }
            public ImmutableList<Adduct> RankedAdducts { get; private set; }
            public ImmutableList<int> RankedCharges { get; private set; }
            public ImmutableList<IonType> RankedIonTypes { get; private set; }
            public ImmutableList<Adduct> AdductsToDisplay { get; private set; }
            public int? FragmentMatchCount { get; private set; }

            public FragmentFilter ChangeFragmentMatchCount(int? fragmentMatchCount)
            {
                return ChangeProp(ImClone(this), im => im.FragmentMatchCount = fragmentMatchCount);
            }

            public TransitionLibraryPick LibraryPick { get; private set; }

            public FragmentFilter ChangeLibraryPick(TransitionLibraryPick libraryPick)
            {
                return ChangeProp(ImClone(this), im => im.LibraryPick = libraryPick);
            }

            public int? RankLimit { get; private set; }

            public FragmentFilter ChangeRankLimit(int? rankLimit)
            {
                return ChangeProp(ImClone(this), im => im.RankLimit = rankLimit);
            }

            public FragmentFilter ChangeAdductsToDisplay(IEnumerable<Adduct> adductsToDisplay)
            {
                return ChangeProp(ImClone(this), im=>im.AdductsToDisplay = ImmutableList.ValueOf(adductsToDisplay));
            }
            public ImmutableList<IonType> IonTypesToDisplay { get; private set; }

            public FragmentFilter ChangeIonTypesToDisplay(IEnumerable<IonType> ionTypesToDisplay)
            {
                return ChangeProp(ImClone(this), im => im.IonTypesToDisplay = ImmutableList.ValueOf(ionTypesToDisplay));
            }

            public bool UseFilter { get; private set; }

            public FragmentFilter ChangeUseFilter(bool useFilter)
            {
                return ChangeProp(ImClone(this), im => im.UseFilter = useFilter);
            }

            public bool MatchAll { get; private set; }

            public FragmentFilter ChangeMatchAll(bool matchAll)
            {
                return ChangeProp(ImClone(this), im => im.MatchAll = matchAll);
            }
        }
        private MoleculeMasses GetCrosslinkMasses(SrmSettings settings)
        {
            var predictDocNode = MakeTransitionGroupWithAllPossibleChildren(settings, TargetInfoObj.TransitionGroupDocNode.LabelType);
            TransitionGroupDocNode matchDocNode;
            if (Equals(TargetInfoObj.TransitionGroupDocNode.LabelType, TargetInfoObj.SpectrumLabelType))
            {
                matchDocNode = predictDocNode;
            }
            else
            {
                matchDocNode = MakeTransitionGroupWithAllPossibleChildren(settings, TargetInfoObj.SpectrumLabelType);
            }
               
            var matchTransitions = matchDocNode.Transitions.ToDictionary(child => child.Key(matchDocNode));


            var predictFragments = new List<MatchedFragmentIon>();
            var matchFragments = new List<MatchedFragmentIon>();
            foreach (var predictedTransition in predictDocNode.Transitions)
            {
                var key = predictedTransition.Key(null);
                TransitionDocNode matchTransition;
                if (!matchTransitions.TryGetValue(key, out matchTransition))
                {
                    continue;
                }

                var complexFragmentIonName = predictedTransition.ComplexFragmentIon.NeutralFragmentIon.GetName();
                var ionType = DecideIonType(complexFragmentIonName);
                string fragmentName = predictedTransition.ComplexFragmentIon.GetFragmentIonName();
                var predictedIon = new MatchedFragmentIon(ionType, predictFragments.Count + 1,
                        predictedTransition.Transition.Adduct, fragmentName, predictedTransition.Losses,
                        predictedTransition.Mz)
                    .ChangeComplexFragmentIonName(complexFragmentIonName);
                predictFragments.Add(predictedIon);
                matchFragments.Add(predictedIon.ChangePredictedMz(matchTransition.Mz));
            }

            var matchMasses = new IonMasses(
                    SequenceMassCalc.GetMH(matchDocNode.PrecursorMz, matchDocNode.PrecursorAdduct,
                        MassType.MonoisotopicMassH), IonTable<TypedMass>.EMPTY)
                .ChangeKnownFragments(matchFragments);
            var predictMasses = new IonMasses(
                    SequenceMassCalc.GetMH(predictDocNode.PrecursorMz, predictDocNode.PrecursorAdduct,
                        MassType.MonoisotopicMassH), IonTable<TypedMass>.EMPTY)
                .ChangeKnownFragments(predictFragments);
            return new MoleculeMasses(predictDocNode.PrecursorMz, matchMasses).ChangePredictIonMasses(predictMasses);
        }

        private IonType DecideIonType(IonChain complexFragmentIon)
        {
            var allIonTypes = complexFragmentIon.IonTypes.ToHashSet();
            foreach (var ionType in FragmentFilterObj.IonTypesToDisplay.Prepend(IonType.precursor))
            {
                allIonTypes.Remove(ionType);
                if (allIonTypes.Count == 0)
                {
                    return ionType;
                }
            }

            return allIonTypes.First();
        }

        private TransitionGroupDocNode MakeTransitionGroupWithAllPossibleChildren(SrmSettings settings, IsotopeLabelType labelType)
        {
            var peptide = new Peptide(TargetInfoObj.LookupSequence);
            var transitionGroup = new TransitionGroup(peptide, TargetInfoObj.TransitionGroupDocNode.PrecursorAdduct, labelType);
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, settings, TargetInfoObj.LookupMods, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var children = transitionGroupDocNode.GetTransitions(settings, TargetInfoObj.LookupMods,
                transitionGroupDocNode.PrecursorMz, null, null, null, FragmentFilterObj.UseFilter).Cast<DocNode>().ToList();
            return (TransitionGroupDocNode) transitionGroupDocNode.ChangeChildren(children);
        }
    }
}
