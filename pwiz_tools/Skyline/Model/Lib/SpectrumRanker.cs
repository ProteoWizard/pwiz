using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.Chemistry;
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
                rp.massesMatch = new IonTable<TypedMass>(IonType.custom, existing.Length);
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
            rp.tolerance = libraries.IonMatchTolerance;
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

                FindIntensityCutoff(listMI, 0, (float)(totalIntensity / len) * 2, minPeaks, 1, ref intensityCutoff, ref len);
            }

            // Create filtered peak array storing original index for m/z ordering
            // to avoid needing to sort to return to this order.
            LibraryRankedSpectrumInfo.RankedMI[] arrayRMI = new LibraryRankedSpectrumInfo.RankedMI[len];
            // Detect when m/z values are out of order, and use the expensive sort
            // by m/z to correct this.
            double lastMz = double.MinValue;
            bool sortMz = false;
            for (int i = 0, j = 0, lenOrig = listMI.Count; i < lenOrig; i++)
            {
                SpectrumPeaksInfo.MI mi = listMI[i];
                if (mi.Intensity >= intensityCutoff || intensityCutoff == 0)
                {
                    arrayRMI[j] = new LibraryRankedSpectrumInfo.RankedMI(mi, j);
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

            LibraryRankedSpectrumInfo.RankedMI[] arrayResult = new LibraryRankedSpectrumInfo.RankedMI[ionMatchCount != -1 ? ionMatchCount : arrayRMI.Length];

            foreach (LibraryRankedSpectrumInfo.RankedMI rmi in arrayRMI)
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
                    arrayResult[i] = LibraryRankedSpectrumInfo.RankedMI.EMPTY;
            }
            // If all ions are to be included, and some were found out of order, then
            // the expensive full sort by m/z is necesary.
            else if (sortMz)
            {
                Array.Sort(arrayResult, OrderMz);
            }

            double? spectrumScore;
            if (score == null && groupDocNode.HasLibInfo && groupDocNode.LibInfo is BiblioSpecSpectrumHeaderInfo libInfo)
            {
                spectrumScore = libInfo.Score;
            }
            else
            {
                spectrumScore = score;
            }

            return new LibraryRankedSpectrumInfo(labelType, libraries.IonMatchTolerance, arrayResult, spectrumScore);
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
            public int Ranked { get { return _rank - 1; } }
            public int RankNext() { return _rank++; }
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


    }
}
