using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using RankedMI = pwiz.Skyline.Model.Lib.LibraryRankedSpectrumInfo.RankedMI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public class SpectrumRanker
    {
        public static LibraryRankedSpectrumInfo RankSpectrum(SpectrumPeaksInfo info,
            IsotopeLabelType labelType,
            TransitionGroupDocNode groupDocNode, SrmSettings settings,
            Target lookupSequence, ExplicitMods lookupMods,
            IEnumerable<Adduct> charges, IEnumerable<IonType> types,
            IEnumerable<Adduct> rankCharges, IEnumerable<IonType> rankTypes,
            double? score, bool useFilter, bool matchAll, int minPeaks)
        {
            var spectrumRanker = new SpectrumRanker(labelType, groupDocNode, settings, lookupSequence, lookupMods, charges,types, rankCharges, rankTypes, useFilter, matchAll);
            return spectrumRanker.RankSpectrum(info, minPeaks, score);
        }


        public SpectrumRanker(IsotopeLabelType labelType,
            TransitionGroupDocNode groupDocNode, SrmSettings settings,
            Target lookupSequence, ExplicitMods lookupMods,
            IEnumerable<Adduct> charges, IEnumerable<IonType> types,
            IEnumerable<Adduct> rankCharges, IEnumerable<IonType> rankTypes,
            bool useFilter, bool matchAll)
        {
            MatchAll = matchAll;
            GroupDocNode = groupDocNode;
            UseFilter = useFilter;
            // Avoid ReSharper multiple enumeration warning
            var rankChargesArray = rankCharges.ToArray();
            var rankTypesArray = rankTypes.ToArray();

            TransitionGroup group = groupDocNode.TransitionGroup;
            bool isProteomic = group.IsProteomic;

            if (!useFilter)
            {
                if (charges == null)
                    charges = GetRanked(rankChargesArray, isProteomic ? Transition.DEFAULT_PEPTIDE_CHARGES : Transition.DEFAULT_MOLECULE_CHARGES);
                if (types == null)
                    types = GetRanked(rankTypesArray, isProteomic ? Transition.PEPTIDE_ION_TYPES : Transition.MOLECULE_ION_TYPES);
                MatchAll = true;
            }

            bool limitRanks =
                groupDocNode.IsCustomIon && // For small molecules, cap the number of ranked ions displayed if we don't have any peak metadata
                groupDocNode.Transitions.Any(t => string.IsNullOrEmpty(t.FragmentIonName));
            rankLimit = limitRanks ? settings.TransitionSettings.Libraries.IonCount : (int?) null;
            Sequence = lookupSequence;
            Adducts = ImmutableList.ValueOf(charges ?? rankChargesArray);
            Types = ImmutableList.ValueOf(types ?? rankTypesArray);
            RankCharges = ImmutableList.ValueOf(rankChargesArray.Select(a=>Math.Abs(a.AdductCharge)));
            RankTypes = ImmutableList.ValueOf(rankTypesArray);

            rp = new RankParams();
            // Get necessary mass calculators and masses
            var calcMatchPre = settings.GetPrecursorCalc(labelType, lookupMods);
            var calcMatch = isProteomic ? settings.GetFragmentCalc(labelType, lookupMods) : settings.GetDefaultFragmentCalc();
            var calcPredict = isProteomic ? settings.GetFragmentCalc(group.LabelType, lookupMods) : calcMatch;
            if (isProteomic && Sequence.IsProteomic)
            {
                rp.precursorMz = SequenceMassCalc.GetMZ(calcMatchPre.GetPrecursorMass(Sequence), PrecursorAdduct);
                rp.MatchIonMasses = new IonMasses(calcMatch.GetPrecursorFragmentMass(Sequence), calcMatch.GetFragmentIonMasses(Sequence));
                rp.knownFragments = null;
            }
            else if (!isProteomic && !Sequence.IsProteomic)
            {
                string isotopicForumla;
                rp.precursorMz = SequenceMassCalc.GetMZ(calcMatchPre.GetPrecursorMass(Sequence.Molecule, null, PrecursorAdduct, out isotopicForumla), PrecursorAdduct);
                var existing = groupDocNode.Transitions.Where(tran => tran.Transition.IsNonPrecursorNonReporterCustomIon()).Select(t => t.Transition.CustomIon.GetMass(MassType.Monoisotopic)).ToArray();
                var ionTable = new IonTable<TypedMass>(IonType.custom, existing.Length);
                for (var i = 0; i < existing.Length; i++)
                {
                    ionTable[IonType.custom, i] = existing[i];
                }
                rp.MatchIonMasses = new IonMasses(calcMatch.GetPrecursorFragmentMass(Sequence), ionTable);
                rp.knownFragments = groupDocNode.Transitions.Where(tran => tran.Transition.IsNonPrecursorNonReporterCustomIon()).Select(t =>
                    new KnownFragment
                    {
                        Adduct = t.Transition.Adduct,
                        Name = t.GetFragmentIonName(CultureInfo.CurrentCulture, settings.TransitionSettings.Libraries.IonMatchTolerance),
                        Mz = t.Mz
                    }).ToList();
            }
            else
            {
                rp.precursorMz = 0.0;
                rp.MatchIonMasses = new IonMasses(TypedMass.ZERO_MONO_MASSH, IonTable<TypedMass>.EMPTY);
                rp.knownFragments = null;
            }

            rp.PredictIonMasses = rp.MatchIonMasses;
            if (!ReferenceEquals(calcPredict, calcMatch))
            {
                var ionTable = rp.MatchIonMasses.FragmentMasses;
                if (Sequence.IsProteomic) // CONSIDER - eventually we may be able to predict fragments for small molecules?
                    ionTable = calcPredict.GetFragmentIonMasses(Sequence);
                rp.PredictIonMasses = new IonMasses(calcPredict.GetPrecursorFragmentMass(Sequence), ionTable);
            }

            // Get values of interest from the settings.
            TransitionSettings = settings.TransitionSettings;

            // Get potential losses to all fragments in this peptide
            PotentialLosses = TransitionGroup.CalcPotentialLosses(Sequence,
                                                                     settings.PeptideSettings.Modifications, lookupMods,
                                                                     MassType);

            // Create arrays because ReadOnlyCollection enumerators are too slow
            // In some cases these collections must be enumerated for every ion
            // allowed in the library specturm.
            Pick = Libraries.Pick;
            IonMatchCount = Libraries.IonCount;
            // If no library filtering will happen, return all rankings for view in the UI
            if (!UseFilter || Pick == TransitionLibraryPick.none)
            {
                if (Pick == TransitionLibraryPick.none)
                    Pick = TransitionLibraryPick.all;
                IonMatchCount = -1;
            }
        }



        public int IonMatchCount { get; private set; }
        public TransitionLibraryPick Pick { get; private set; }

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

        public bool MatchAll { get; private set; }

        private RankParams rp { get; set; }

        public TransitionGroupDocNode GroupDocNode { get; private set; }
        public Adduct PrecursorAdduct
        {
            get { return GroupDocNode.PrecursorAdduct; }
        }
        public IsotopeLabelType PredictLabelType
        {
            get { return GroupDocNode.LabelType; }
        }

        public bool UseFilter { get; private set; }

        public IList<IList<ExplicitLoss>> PotentialLosses { get; }

        public LibraryRankedSpectrumInfo RankSpectrum(SpectrumPeaksInfo info, int minPeaks, double? score)
        {

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
                if (IonMatchCount == -1)
                {
                    if (mi.Mz < lastMz)
                        sortMz = true;
                    lastMz = mi.Mz;
                }
            }

            // The one expensive sort is used to determine rank order
            // by intensity, or m/z in case of a tie.
            Array.Sort(arrayRMI, OrderIntensityDesc);


            RankedMI[] arrayResult = new RankedMI[IonMatchCount != -1 ? IonMatchCount : arrayRMI.Length];

            foreach (RankedMI rmi in arrayRMI)
            {
                var rankedRmi = CalculateRank(rankingState, rmi);

                // If not filtering for only the highest ionMatchCount ranks
                if (IonMatchCount == -1)
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
                    if (countRanks == IonMatchCount)
                        break;
                }
            }

            // Is this a theoretical library with no intensity variation? If so it can't be ranked.
            // If it has any interesting peak annotations, pass those through
            if (rankingState.Ranked == 0 && arrayRMI.All(rmi => rmi.Intensity == arrayRMI[0].Intensity))
            {
                // Only do this if we have been asked to limit the ions matched, and there are any annotations
                if (IonMatchCount != -1 && arrayRMI.Any(rmi => rmi.HasAnnotations))
                {
                    // Pass through anything with an annotation as being of probable interest
                    arrayResult = arrayRMI.Where(rmi => rmi.HasAnnotations).ToArray();
                    IonMatchCount = -1;
                }
            }

            // If not enough ranked ions were found, fill the rest of the results array
            if (IonMatchCount != -1)
            {
                for (int i = rankingState.Ranked; i < IonMatchCount; i++)
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

        public Target Sequence { get; }
        public const int MAX_MATCH = 6;

        public class RankParams
        {
            public double precursorMz { get; set; }
            public IonMasses MatchIonMasses { get; set; }
            public IonMasses PredictIonMasses { get; set; }
            public List<KnownFragment> knownFragments { get; set; } // For small molecule use, where we can't predict fragments
        }

        public class IonMasses
        {
            public IonMasses(TypedMass precursorMass, IonTable<TypedMass> fragmentMasses)
            {
                PrecursorMass = precursorMass;
                FragmentMasses = fragmentMasses;
            }

            public TypedMass PrecursorMass { get; private set; }
            public IonTable<TypedMass> FragmentMasses { get; private set; }
        }

        public int? rankLimit { get; private set; }

        public IStartFragmentFinder startFinder
        {
            get { return TransitionSettings.Filter.FragmentRangeFirst; }
        }
        public IEndFragmentFinder endFinder
        {
            get { return TransitionSettings.Filter.FragmentRangeLast; }
        }


        public ImmutableList<Adduct> Adducts { get; }
        public ImmutableList<IonType> Types { get; }
        public ImmutableList<int> RankCharges { get; }
        public ImmutableList<IonType> RankTypes { get; }

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
        public class KnownFragment
        {
            public string Name { get; set; }
            public Adduct Adduct { get; set; }
            public SignedMz Mz { get; set; }

            public override string ToString()
            {
                return Mz + @" " + (Name ?? string.Empty) + @" " + Adduct;
            }
        }

        private RankedMI CalculateRank(RankingState rankingState, RankedMI rankedMI)
        {
            // Rank based on filtered range, if the settings use it in picking
            bool filter = (Pick == TransitionLibraryPick.filter);

            if (rp.knownFragments != null)
            {
                // Small molecule work - we only know about the fragments we're given, we can't predict others
                foreach (IonType type in Types)
                {
                    if (Transition.IsPrecursor(type))
                    {
                        if (!MatchNext(rankingState, type, 0, null, PrecursorAdduct, null, 0, filter, 0, 0, 0, ref rankedMI))
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
                        for (var i = 0; i < rp.knownFragments.Count; i++)
                        {
                            var fragment = rp.knownFragments[i];
                            if (!MatchNext(rankingState, IonType.custom, i, null, fragment.Adduct, fragment.Name, 0, filter, 0, 0, fragment.Mz, ref rankedMI))
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
            int len = rp.MatchIonMasses.FragmentMasses.GetLength(1);
            foreach (IonType type in Types)
            {
                if (Transition.IsPrecursor(type))
                {
                    foreach (var losses in TransitionGroup.CalcTransitionLosses(type, 0, MassType, PotentialLosses))
                    {
                        if (!MatchNext(rankingState, type, len, losses, PrecursorAdduct, null, len + 1, filter, len, len, 0, ref rankedMI))
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
                        start = TransitionSettings.Filter.FragmentRangeFirst.FindStartFragment(rp.MatchIonMasses.FragmentMasses, type, adduct,
                                                                 rp.precursorMz, TransitionSettings.Filter.PrecursorMzWindow, out startMz);
                        end = TransitionSettings.Filter.FragmentRangeLast.FindEndFragment(type, start, len);
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
                            foreach (var losses in TransitionGroup.CalcTransitionLosses(type, i, MassType, PotentialLosses))
                            {
                                if (!MatchNext(rankingState, type, i, losses, adduct, null, len, filter, end, start, startMz, ref rankedMI))
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
                                if (!MatchNext(rankingState, type, i, losses, adduct, null, len, filter, end, start, startMz, ref rankedMI))
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
        private bool MatchNext(RankingState rankingState, IonType type, int offset, TransitionLosses losses, Adduct adduct, string fragmentName, int len, bool filter, int end, int start, double startMz, ref RankedMI rankedMI)
        {
            bool isFragment = !Transition.IsPrecursor(type);
            var ionMass = isFragment ? rp.MatchIonMasses.FragmentMasses[type, offset] : rp.MatchIonMasses.PrecursorMass;
            if (losses != null)
                ionMass -= losses.Mass;
            double ionMz = SequenceMassCalc.GetMZ(ionMass, adduct);
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

                int ordinal = Transition.OffsetToOrdinal(type, offset, len + 1);
                // If this m/z aready matched a different ion, just remember the second ion.
                var predictedMass = isFragment ? rp.PredictIonMasses.FragmentMasses[type, offset] : rp.PredictIonMasses.PrecursorMass;
                if (losses != null)
                    predictedMass -= losses.Mass;
                double predictedMz = SequenceMassCalc.GetMZ(predictedMass, adduct);
                if (rankedMI.MatchedIons != null)
                {
                    // If first type was excluded from causing a ranking, but second does, then make it the first
                    // Otherwise, this can cause very mysterious failures to rank transitions that appear in the
                    // document.
                    var match = new MatchedFragmentIon(type, ordinal, adduct, fragmentName, losses, predictedMz);
                    if (rankedMI.Rank == 0 && ApplyRanking(rankingState, type, offset, losses, adduct, filter, start, end, startMz, ionMz, ref rankedMI))
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

                // Avoid using the same predicted m/z on two different peaks
                if (predictedMz == ionMz || !rankingState.IsSeen(predictedMz))
                {
                    rankingState.Seen(predictedMz);

                    ApplyRanking(rankingState, type, offset, losses, adduct, filter, start, end, startMz, ionMz, ref rankedMI);
                    rankedMI = rankedMI.ChangeMatchedIons(ImmutableList.Singleton(
                        new MatchedFragmentIon(type, ordinal, adduct, fragmentName, losses, predictedMz)));
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
        private bool ApplyRanking(RankingState rankingState, IonType type, int offset, TransitionLosses losses, Adduct adduct, bool filter,
            int start, int end, double startMz, double ionMz, ref RankedMI rankedMI)
        {
            // Avoid ranking precursor ions without losses, if the precursor isotopes will
            // not be taken from product ions
            if (!ExcludePrecursorIsotopes || type != IonType.precursor || losses != null)
            {
                if (!filter || TransitionSettings.Accept(Sequence, rp.precursorMz, type, offset, ionMz, start, end, startMz))
                {
                    if (!rankingState.matchAll || (MinMz <= ionMz && ionMz <= MaxMz &&
                                         RankTypes.Contains(type) &&
                                         (!rankLimit.HasValue || rankingState.Ranked < rankLimit) &&
                                         (RankCharges.Contains(Math.Abs(adduct.AdductCharge)) || type == IonType.precursor))) // CONSIDER(bspratt) we may eventually want adduct-level control for small molecules, not just abs charge
                    {
                        rankedMI = rankedMI.ChangeRank(rankingState.RankNext());
                        return true;
                    }
                }
            }
            return false;
        }

    }
}
