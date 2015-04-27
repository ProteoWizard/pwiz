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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public sealed class LibraryRankedSpectrumInfo : Immutable
    {
        private readonly ImmutableList<RankedMI> _spectrum;

        public LibraryRankedSpectrumInfo(SpectrumPeaksInfo info,
                                         IsotopeLabelType labelType, TransitionGroup group,
                                         SrmSettings settings, string lookupSequence, ExplicitMods lookupMods,
                                         IEnumerable<int> charges, IEnumerable<IonType> types,
                                         IEnumerable<int> rankCharges, IEnumerable<IonType> rankTypes)
            : this(info, labelType, group, settings, lookupSequence, lookupMods,
                   charges, types, rankCharges, rankTypes, false, true, -1)
        {
        }

        public LibraryRankedSpectrumInfo(SpectrumPeaksInfo info, IsotopeLabelType labelType,
                                         TransitionGroup group, SrmSettings settings, ExplicitMods lookupMods,
                                         bool useFilter, int minPeaks)
            : this(info, labelType, group, settings, group.Peptide.Sequence, lookupMods,
                   null, // charges
                   null, // types
                   // ReadOnlyCollection enumerators are too slow, and show under a profiler
                   settings.TransitionSettings.Filter.ProductCharges.ToArray(),
                   settings.TransitionSettings.Filter.IonTypes.ToArray(),
                   useFilter, false, minPeaks)
        {
        }

        private LibraryRankedSpectrumInfo(SpectrumPeaksInfo info, IsotopeLabelType labelType,
                                          TransitionGroup group, SrmSettings settings,
                                          string lookupSequence, ExplicitMods lookupMods,
                                          IEnumerable<int> charges, IEnumerable<IonType> types,
                                          IEnumerable<int> rankCharges, IEnumerable<IonType> rankTypes,
                                          bool useFilter, bool matchAll, int minPeaks)
        {
            LabelType = labelType;

            // Avoid ReSharper multiple enumeration warning
            var rankChargesArray = rankCharges.ToArray();
            var rankTypesArray = rankTypes.ToArray();

            if (!useFilter)
            {
                if (charges == null)
                    charges = GetRanked(rankChargesArray, Transition.ALL_CHARGES);
                if (types == null)
                    types = GetRanked(rankTypesArray, Transition.ALL_TYPES);
                matchAll = true;
            }

            RankParams rp = new RankParams
                                {
                                    sequence = lookupSequence,
                                    precursorCharge = group.PrecursorCharge,
                                    charges = charges ?? rankCharges,
                                    types = types ?? rankTypes,
                                    matchAll = matchAll,
                                    rankCharges = rankChargesArray,
                                    rankTypes = rankTypesArray,
                                    // Precursor isotopes will not be included in MS/MS, if they will be filtered
                                    // from MS1
                                    excludePrecursorIsotopes = settings.TransitionSettings.FullScan.IsEnabledMs,
                                    tranSettings = settings.TransitionSettings
                                };

            // Get necessary mass calculators and masses
            var calcMatchPre = settings.GetPrecursorCalc(labelType, lookupMods);
            var calcMatch = settings.GetFragmentCalc(labelType, lookupMods);
            var calcPredict = settings.GetFragmentCalc(group.LabelType, lookupMods);
            rp.precursorMz = SequenceMassCalc.GetMZ(calcMatchPre.GetPrecursorMass(rp.sequence),
                                                    rp.precursorCharge);
            rp.massPreMatch = calcMatch.GetPrecursorFragmentMass(rp.sequence);
            rp.massPrePredict = rp.massPreMatch;
            rp.massesMatch = calcMatch.GetFragmentIonMasses(rp.sequence);
            rp.massesPredict = rp.massesMatch;
            if (!ReferenceEquals(calcPredict, calcMatch))
            {
                rp.massPrePredict = calcPredict.GetPrecursorFragmentMass(rp.sequence);
                rp.massesPredict = calcPredict.GetFragmentIonMasses(rp.sequence);
            }

            // Get values of interest from the settings.
            var tranSettings = settings.TransitionSettings;
            var predict = tranSettings.Prediction;
            var filter = tranSettings.Filter;
            var libraries = tranSettings.Libraries;
            var instrument = tranSettings.Instrument;

            // Get potential losses to all fragments in this peptide
            rp.massType = predict.FragmentMassType;
            rp.potentialLosses = TransitionGroup.CalcPotentialLosses(rp.sequence,
                                                                     settings.PeptideSettings.Modifications, lookupMods,
                                                                     rp.massType);

            // Create arrays because ReadOnlyCollection enumerators are too slow
            // In some cases these collections must be enumerated for every ion
            // allowed in the library specturm.
            rp.startFinder = filter.FragmentRangeFirst;
            rp.endFinder = filter.FragmentRangeLast;

            // Get library settings
            Tolerance = libraries.IonMatchTolerance;
            rp.tolerance = Tolerance;
            rp.pick = tranSettings.Libraries.Pick;
            int ionMatchCount = libraries.IonCount;
            // If no library filtering will happen, return all rankings for view in the UI
            if (!useFilter || rp.pick == TransitionLibraryPick.none)
            {
                if (rp.pick == TransitionLibraryPick.none)
                    rp.pick = TransitionLibraryPick.all;
                ionMatchCount = -1;
            }

            // Get instrument settings
            rp.minMz = instrument.MinMz;
            rp.maxMz = instrument.MaxMz;

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

                FindIntensityCutoff(listMI, 0, (float) (totalIntensity/len)*2, minPeaks, 1, ref intensityCutoff, ref len);
            }

            // Create filtered peak array storing original index for m/z ordering
            // to avoid needing to sort to return to this order.
            RankedMI[] arrayRMI = new RankedMI[len];
            // Detect when m/z values are out of order, and use the expensive sort
            // by m/z to correct this.
            double lastMz = double.MinValue;
            bool sortMz = false;
            for (int i = 0, j = 0, lenOrig = listMI.Count; i < lenOrig ; i++)
            {
                SpectrumPeaksInfo.MI mi = listMI[i];
                if (mi.Intensity >= intensityCutoff || intensityCutoff == 0)
                {
                    arrayRMI[j] = new RankedMI(mi, j);
                    j++;
                }
                if (ionMatchCount == -1)
                {
                    if (mi.Mz < lastMz)
                        sortMz = true;
                    lastMz = mi.Mz;
                }
            }

            // The one expensive sort is used to determine rank order
            // by intensity.
            Array.Sort(arrayRMI, OrderIntensityDesc);

            RankedMI[] arrayResult = new RankedMI[ionMatchCount != -1 ? ionMatchCount : arrayRMI.Length];

            foreach (RankedMI rmi in arrayRMI)
            {
                rmi.CalculateRank(rp);

                // If not filtering for only the highest ionMatchCount ranks
                if (ionMatchCount == -1)
                {
                    // Put the ranked record back where it started in the
                    // m/z ordering to avoid a second sort.
                    arrayResult[rmi.IndexMz] = rmi;
                }
                    // Otherwise, if this ion was ranked, add it to the result array
                else if (rmi.Rank > 0)
                {
                    int countRanks = rmi.Rank;
                    arrayResult[countRanks - 1] = rmi;
                    // And stop when the array is full
                    if (countRanks == ionMatchCount)
                        break;
                }
            }

            // If not enough ranked ions were found, fill the rest of the results array
            if (ionMatchCount != -1)
            {
                for (int i = rp.Ranked; i < ionMatchCount; i++)
                    arrayResult[i] = RankedMI.EMPTY;
            }
                // If all ions are to be included, and some were found out of order, then
                // the expensive full sort by m/z is necesary.
            else if (sortMz)
            {
                Array.Sort(arrayResult, OrderMz);
            }

            _spectrum = MakeReadOnly(arrayResult);
        }

        /// <summary>
        /// Make sure array ordering starts with ranked items to avoid changing ranked items between
        /// filtered and unfiltered queries
        /// </summary>
        private IEnumerable<TItem> GetRanked<TItem>(IEnumerable<TItem> rankItems, IEnumerable<TItem> allItems)
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

        public IsotopeLabelType LabelType { get; private set; }
        public double Tolerance { get; private set; }

// ReSharper disable ParameterTypeCanBeEnumerable.Local
        private static void FindIntensityCutoff(IList<SpectrumPeaksInfo.MI> listMI, float left, float right, int minPeaks, int calls, ref float cutoff, ref int len)
// ReSharper restore ParameterTypeCanBeEnumerable.Local
        {
            if (calls < 3)
            {
                float mid = (left + right)/2;
                int count = FilterPeaks(listMI, mid);
                if (count < minPeaks)
                    FindIntensityCutoff(listMI, left, mid, minPeaks, calls + 1, ref cutoff, ref len);
                else
                {
                    cutoff = mid;
                    len = count;
                    if (count > minPeaks*1.5)
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
                    if (rmi.Ordinal > 0)
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

        public class RankParams
        {
            public string sequence { get; set; }
            public int precursorCharge { get; set; }
            public MassType massType { get; set; }
            public double precursorMz { get; set; }
            public double massPreMatch { get; set; }
            public double massPrePredict { get; set; }
            public double[,] massesMatch { get; set; }
            public double[,] massesPredict { get; set; }
            public IEnumerable<int> charges { get; set; }
            public IEnumerable<IonType> types { get; set; }
            public IEnumerable<int> rankCharges { get; set; }
            public IEnumerable<IonType> rankTypes { get; set; }
            public bool excludePrecursorIsotopes { get; set; }
            public IList<IList<ExplicitLoss>> potentialLosses { get; set; }
            public IStartFragmentFinder startFinder { get; set; }
            public IEndFragmentFinder endFinder { get; set; }
            public TransitionSettings tranSettings { get; set; }
            public TransitionFilter filter { get { return tranSettings.Filter; } }
            public TransitionLibraryPick pick { get; set; }
            public double tolerance { get; set; }
            public double minMz { get; set; }
            public double maxMz { get; set; }
            public bool matchAll { get; set; }
            public bool matched { get; set; }
            private readonly HashSet<double> _seenMz = new HashSet<double>();
            private double _seenFirst;
            public bool IsSeen(double mz)
            {
                return _seenMz.Contains(mz);
            }
            public bool HasSeenOnce { get { return _seenFirst != 0; } }
            public bool HasLosses { get { return potentialLosses != null; } }

            public void Seen(double mz)
            {
                if (matchAll && _seenFirst == 0)
                    _seenFirst = mz;
                else
                    _seenMz.Add(mz);
            }

            public void Clean()
            {
                if (_seenFirst != 0)
                    _seenMz.Add(_seenFirst);
                matched = false;
            }

            private int _rank = 1;
            public int Ranked { get { return _rank - 1;  }}
            public int RankNext() { return _rank++; }
        }

        public sealed class RankedMI
        {
            public override string ToString()
            {
                return string.Format("i={0}, mz={1}", _mi.Intensity, _mi.Mz); // Not L10N
            }

            private SpectrumPeaksInfo.MI _mi;

            public static readonly RankedMI EMPTY = new RankedMI(new SpectrumPeaksInfo.MI(), 0);

            public RankedMI(SpectrumPeaksInfo.MI mi, int indexMz)
            {
                _mi = mi;

                IndexMz = indexMz;
            }

            public int Rank { get; private set; }

            public IonType IonType { get; private set; }
            public IonType IonType2 { get; private set; }

            public int Ordinal { get; private set; }
            public int Ordinal2 { get; private set; }

            public int Charge { get; private set; }
            public int Charge2 { get; private set; }

            public TransitionLosses Losses { get; private set; }
            public TransitionLosses Losses2 { get; private set; }

            public int IndexMz { get; private set; }

            public float Intensity { get { return _mi.Intensity; } }

            public double ObservedMz { get { return _mi.Mz; } }

            public double PredictedMz { get; private set; }
            public double PredictedMz2 { get; private set; }

            public void CalculateRank(RankParams rp)
            {
                // Rank based on filtered range, if the settings use it in picking
                bool filter = (rp.pick == TransitionLibraryPick.filter);

                // Look for a predicted match within the acceptable tolerance
                int len = rp.massesMatch.GetLength(1);
                foreach (IonType type in rp.types)
                {
                    if (Transition.IsPrecursor(type))
                    {
                        foreach (var losses in TransitionGroup.CalcTransitionLosses(type, 0, rp.massType, rp.potentialLosses))
                        {
                            if (!MatchNext(rp, type, len, losses, rp.precursorCharge, len + 1, filter, len, len, 0))
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

                    foreach (int charge in rp.charges)
                    {
                        // Precursor charge can never be lower than product ion charge.
                        if (rp.precursorCharge < charge)
                            continue;

                        int start = 0, end = 0;
                        double startMz = 0;
                        if (filter)
                        {
                            start = rp.startFinder.FindStartFragment(rp.massesMatch, type, charge,
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
                                    if (!MatchNext(rp, type, i, losses, charge, len, filter, end, start, startMz))
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
                                    if (!MatchNext(rp, type, i, losses, charge, len, filter, end, start, startMz))
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

            private bool MatchNext(RankParams rp, IonType type, int offset, TransitionLosses losses, int charge, int len, bool filter, int end, int start, double startMz)
            {
                bool precursorMatch = Transition.IsPrecursor(type);
                double ionMass = !precursorMatch ? rp.massesMatch[(int)type, offset] : rp.massPreMatch;
                if (losses != null)
                    ionMass -= losses.Mass;
                double ionMz = SequenceMassCalc.GetMZ(ionMass, charge);
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
                    double predictedMass = !precursorMatch ? rp.massesPredict[(int)type, offset] : rp.massPrePredict;
                    if (losses != null)
                        predictedMass -= losses.Mass;
                    double predictedMz = SequenceMassCalc.GetMZ(predictedMass, charge);
                    if (Ordinal > 0)
                    {
                        IonType2 = type;
                        Charge2 = charge;
                        Ordinal2 = ordinal;
                        Losses2 = losses;
                        PredictedMz2 = predictedMz;
                        rp.matched = true;
                        return false;
                    }
                    
                    // Avoid using the same predicted m/z on two different peaks
                    if (predictedMz == ionMz || !rp.IsSeen(predictedMz))
                    {
                        rp.Seen(predictedMz);

                        // Avoid ranking precursor ions without losses, if the precursor isotopes will
                        // not be taken from product ions
                        if (!rp.excludePrecursorIsotopes || type != IonType.precursor || losses != null)
                        {
                            if (!filter || rp.tranSettings.Accept(rp.sequence, rp.precursorMz, type, offset, ionMz, start, end, startMz))
                            {
                                if (!rp.matchAll || (rp.minMz <= ionMz && ionMz <= rp.maxMz &&
                                                     rp.rankTypes.Contains(type) &&
                                                     (rp.rankCharges.Contains(charge) || type == IonType.precursor)))
                                    Rank = rp.RankNext();
                            }
                        }
                        IonType = type;
                        Charge = charge;
                        Ordinal = ordinal;
                        Losses = losses;
                        PredictedMz = predictedMz;
                        rp.matched = (!rp.matchAll);
                        return rp.matchAll;
                    }
                }
                // Stop looking once the mass has been passed, unless there are losses to consider
                if (rp.HasLosses)
                    return true;
                return (ionMz <= ObservedMz);
            }
        }

        private static int OrderMz(RankedMI mi1, RankedMI mi2)
        {
            return (mi1.ObservedMz.CompareTo(mi2.ObservedMz));
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
    }
}