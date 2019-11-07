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
using System.Globalization;
using System.Linq;
using pwiz.Common.Chemistry;
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
                                         IsotopeLabelType labelType, TransitionGroupDocNode group,
                                         SrmSettings settings, Target lookupSequence, ExplicitMods lookupMods,
                                         IEnumerable<Adduct> charges, IEnumerable<IonType> types,
                                         IEnumerable<Adduct> rankCharges, IEnumerable<IonType> rankTypes, double? score)
            : this(info, labelType, group, settings, lookupSequence, lookupMods,
                   charges, types, rankCharges, rankTypes, score, false, true, -1)
        {
        }

        public LibraryRankedSpectrumInfo(SpectrumPeaksInfo info, IsotopeLabelType labelType,
                                         TransitionGroupDocNode group, SrmSettings settings, ExplicitMods lookupMods,
                                         bool useFilter, int minPeaks)
            : this(info, labelType, group, settings, group.Peptide.Target, lookupMods,
                   null, // charges
                   null, // types
                   // ReadOnlyCollection enumerators are too slow, and show under a profiler
                   group.IsCustomIon ? settings.TransitionSettings.Filter.SmallMoleculeFragmentAdducts.ToArray() : settings.TransitionSettings.Filter.PeptideProductCharges.ToArray(),
                   group.IsCustomIon ? settings.TransitionSettings.Filter.SmallMoleculeIonTypes.ToArray() : settings.TransitionSettings.Filter.PeptideIonTypes.ToArray(),
                   null, useFilter, false, minPeaks)
        {
        }

        private LibraryRankedSpectrumInfo(SpectrumPeaksInfo info, IsotopeLabelType labelType,
                                          TransitionGroupDocNode groupDocNode, SrmSettings settings,
                                          Target lookupSequence, ExplicitMods lookupMods,
                                          IEnumerable<Adduct> charges, IEnumerable<IonType> types,
                                          IEnumerable<Adduct> rankCharges, IEnumerable<IonType> rankTypes,
                                          double? score, bool useFilter, bool matchAll, int minPeaks)
        {
            LabelType = labelType;

            // Avoid ReSharper multiple enumeration warning
            var rankChargesArray = rankCharges.ToArray();
            var rankTypesArray = rankTypes.ToArray();

            TransitionGroup group = groupDocNode.TransitionGroup;
            bool isProteomic = group.IsProteomic;

            if (score == null && groupDocNode.HasLibInfo && groupDocNode.LibInfo is BiblioSpecSpectrumHeaderInfo libInfo)
            {
                Score = libInfo.Score;
            }
            else
            {
                Score = score;
            }

            if (!useFilter)
            {
                if (charges == null)
                    charges = GetRanked(rankChargesArray, isProteomic ? Transition.DEFAULT_PEPTIDE_CHARGES : Transition.DEFAULT_MOLECULE_CHARGES);
                if (types == null)
                    types = GetRanked(rankTypesArray, isProteomic ? Transition.PEPTIDE_ION_TYPES : Transition.MOLECULE_ION_TYPES);
                matchAll = true;
            }

            bool limitRanks =
                groupDocNode.IsCustomIon && // For small molecules, cap the number of ranked ions displayed if we don't have any peak metadata
                groupDocNode.Transitions.Any(t => string.IsNullOrEmpty(t.FragmentIonName));

            RankParams rp = new RankParams
                                {
                                    sequence = lookupSequence,
                                    precursorAdduct = group.PrecursorAdduct,
                                    adducts = charges ?? rankCharges,
                                    types = types ?? rankTypes,
                                    matchAll = matchAll,
                                    rankCharges = rankChargesArray.Select(a => Math.Abs(a.AdductCharge)).ToArray(),
                                    rankTypes = rankTypesArray,
                                    // Precursor isotopes will not be included in MS/MS, if they will be filtered
                                    // from MS1
                                    excludePrecursorIsotopes = settings.TransitionSettings.FullScan.IsEnabledMs,
                                    tranSettings = settings.TransitionSettings,
                                    rankLimit = limitRanks ? settings.TransitionSettings.Libraries.IonCount : (int?)null
                                };

            // Get necessary mass calculators and masses
            var calcMatchPre = settings.GetPrecursorCalc(labelType, lookupMods);
            var calcMatch = isProteomic ? settings.GetFragmentCalc(labelType, lookupMods) : settings.GetDefaultFragmentCalc();
            var calcPredict = isProteomic ? settings.GetFragmentCalc(group.LabelType, lookupMods) : calcMatch;
            if (isProteomic && rp.sequence.IsProteomic)
            {
                rp.precursorMz = SequenceMassCalc.GetMZ(calcMatchPre.GetPrecursorMass(rp.sequence), rp.precursorAdduct);
                rp.massPreMatch = calcMatch.GetPrecursorFragmentMass(rp.sequence);
                rp.massesMatch = calcMatch.GetFragmentIonMasses(rp.sequence);
                rp.knownFragments = null;
            }
            else if (!isProteomic && !rp.sequence.IsProteomic)
            {
                string isotopicForumla;
                rp.precursorMz = SequenceMassCalc.GetMZ(calcMatchPre.GetPrecursorMass(rp.sequence.Molecule, null, rp.precursorAdduct, out isotopicForumla), rp.precursorAdduct);
                rp.massPreMatch = calcMatch.GetPrecursorFragmentMass(rp.sequence);
                // rp.massesMatch = calcMatch.GetFragmentIonMasses(rp.molecule); CONSIDER, for some molecule types someday?
                // For small molecules we can't predict fragmentation, so just use those we have
                // Older Resharper code inspection implementations insist on warning here
                // Resharper disable PossibleMultipleEnumeration
                var existing = groupDocNode.Transitions.Where(tran => tran.Transition.IsNonPrecursorNonReporterCustomIon()).Select(t => t.Transition.CustomIon.GetMass(MassType.Monoisotopic)).ToArray();
                rp.massesMatch = new IonTable<TypedMass>(IonType.custom,  existing.Length);
                for (var i = 0; i < existing.Length; i++)
                {
                    rp.massesMatch[IonType.custom, i] = existing[i];
                }
                // Resharper restore PossibleMultipleEnumeration
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
                rp.massPreMatch = TypedMass.ZERO_MONO_MASSH;
                rp.massesMatch = IonTable<TypedMass>.EMPTY;
                rp.knownFragments = null;
            }
            rp.massPrePredict = rp.massPreMatch;
            rp.massesPredict = rp.massesMatch;
            if (!ReferenceEquals(calcPredict, calcMatch))
            {
                rp.massPrePredict = calcPredict.GetPrecursorFragmentMass(rp.sequence);
                if (rp.sequence.IsProteomic) // CONSIDER - eventually we may be able to predict fragments for small molecules?
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
            // by intensity, or m/z in case of a tie.
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

            // Is this a theoretical library with no intensity variation? If so it can't be ranked.
            // If it has any interesting peak annotations, pass those through
            if (rp.Ranked == 0 && arrayRMI.All(rmi => rmi.Intensity == arrayRMI[0].Intensity))
            {
                // Only do this if we have been asked to limit the ions matched, and there are any annotations
                if (ionMatchCount != -1 && arrayRMI.Any(rmi => rmi.HasAnnotations))
                {
                    // Pass through anything with an annotation as being of probable interest
                    arrayResult = arrayRMI.Where(rmi => rmi.HasAnnotations).ToArray();
                    ionMatchCount = -1;
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

        public double? Score { get; private set; }
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

        public class RankParams
        {
            public Target sequence { get; set; }
            public Adduct precursorAdduct { get; set; }
            public MassType massType { get; set; }
            public double precursorMz { get; set; }
            public TypedMass massPreMatch { get; set; }
            public TypedMass massPrePredict { get; set; }
            public IonTable<TypedMass> massesMatch { get; set; }
            public IonTable<TypedMass> massesPredict { get; set; }
            public IEnumerable<Adduct> adducts { get; set; }
            public IEnumerable<IonType> types { get; set; }
            public IEnumerable<int> rankCharges { get; set; } // For ranking and display purposes, use abs value of charge, ignoring adduct content
            public IEnumerable<IonType> rankTypes { get; set; }
            public List<KnownFragment> knownFragments { get; set; } // For small molecule use, where we can't predict fragments
            public bool excludePrecursorIsotopes { get; set; }
            public IList<IList<ExplicitLoss>> potentialLosses { get; set; }
            public IStartFragmentFinder startFinder { get; set; }
            public IEndFragmentFinder endFinder { get; set; }
            public TransitionSettings tranSettings { get; set; }
            public int? rankLimit { get; set; }
            public TransitionFilter filter { get { return tranSettings.Filter; } }
            public TransitionLibraryPick pick { get; set; }
            public double tolerance { get; set; }
            public double minMz { get; set; }
            public double maxMz { get; set; }
            public bool matchAll { get; set; }
            public bool matched { get; set; }
            public const int MAX_MATCH = 6;
            private readonly HashSet<double> _seenMz = new HashSet<double>();
            private double _seenFirst;
            public bool IsSeen(double mz)
            {
                return _seenMz.Contains(mz);
            }
            public bool HasSeenOnce { get { return _seenFirst != 0; } }
            public bool HasLosses { get { return potentialLosses != null && potentialLosses.Count > 0; } }
            public bool IsProteomic { get { return precursorAdduct.IsProteomic; } }

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
