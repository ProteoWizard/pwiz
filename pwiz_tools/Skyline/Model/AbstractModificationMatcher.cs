/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;


namespace pwiz.Skyline.Model
{
    public abstract class AbstractModificationMatcher
    {
        private List<StaticMod> _foundHeavyLabels;

        protected SrmSettings Settings { get; set; }
        protected bool Initialized { get; set; }
        protected MappedList<string, StaticMod> DefSetStatic { get; private set; }
        protected MappedList<string, StaticMod> DefSetHeavy { get; private set; }
        protected IsotopeLabelType DocDefHeavyLabelType { get; private set; }
        protected Dictionary<StaticMod, IsotopeLabelType> UserDefinedTypedMods { get; private set; }
        public Dictionary<AAModKey, AAModMatch> Matches { get; private set; }

        public List<string> UnmatchedSequences { get; private set; }

        private static readonly SequenceMassCalc CALC_DEFAULT = new SequenceMassCalc(MassType.MonoisotopicMassH);
        public static double GetDefaultModMass(char aa, StaticMod mod)
        {
            return CALC_DEFAULT.GetModMass(aa, mod);
        }
        
        internal void InitMatcherSettings(SrmSettings settings,
            MappedList<string, StaticMod> defSetStatic, MappedList<string, StaticMod> defSetHeavy)
        {
            DefSetStatic = defSetStatic;
            DefSetHeavy = defSetHeavy;

            Settings = settings;

            var modifications = settings.PeptideSettings.Modifications;

            UserDefinedTypedMods = new Dictionary<StaticMod, IsotopeLabelType>();
            // First add modifications found in document settings, then add modifications found in the global settings.
            foreach (var type in settings.PeptideSettings.Modifications.GetModificationTypes())
            {
                // Set the default heavy type to the first heavy type encountered.
                if (!ReferenceEquals(type, IsotopeLabelType.light) && DocDefHeavyLabelType == null)
                    DocDefHeavyLabelType = type;
                InitUserDefTypedModDict(modifications.GetModificationsByName(type.Name), false);
            }
            InitUserDefTypedModDict(new TypedModifications(IsotopeLabelType.light, DefSetStatic), true);
            InitUserDefTypedModDict(new TypedModifications(DocDefHeavyLabelType, DefSetHeavy), true);

            Matches = new Dictionary<AAModKey, AAModMatch>();
            UnmatchedSequences = new List<string>();
            _foundHeavyLabels = new List<StaticMod>();

            while (MoveNextSequence())
            {
                InitModMatches();
            }
            CleanUpMatches();

            Initialized = true;
        }

        private void InitUserDefTypedModDict(TypedModifications typedModifications, bool includeExplicit)
        {
            var type = typedModifications.LabelType;
            foreach (StaticMod mod in typedModifications.Modifications
                .Where(mod => includeExplicit || !mod.IsUserSet))
            {
                var modName = mod.Name;
                if (!UserDefinedTypedMods.Keys.Contains(key => Equals(key.Name, modName)))
                {
                    var newMod = mod;
                    // TODO: This appears to be problematic
                    if (includeExplicit && !mod.IsVariable)
                        newMod = mod.ChangeExplicit(true);
                    UserDefinedTypedMods.Add(newMod, type);
                }
            }
        }

        private void AddMatch(AAModKey key, AAModMatch match)
        {
            Matches.Add(key, match);
        }

        protected virtual AAModMatch? GetMatch(AAModKey key)
        {
            return Matches.TryGetValue(key, out var match)
                ? match
                : (AAModMatch?)null;
        }

        public bool HasMatches { get { return Matches != null; } }

        public void ClearMatches()
        {
            Matches = null;
        }

        /// <summary>
        /// Finds explicit mods matching the modifications indicated in the sequence string,
        /// and adds them to either the dictionary of sequence matches or the list of 
        /// unmatched sequences.
        /// </summary> 
        private void InitModMatches()
        {
            foreach (var info in GetCurrentSequenceInfos())
            {
                if (GetMatch(info.ModKey) != null)
                    continue;
                // Mass can't be 0.
                if (info.Mass != null && info.Mass == 0)
                {
                    UpdateMatcher(info, null);
                    continue;
                }
                AAModMatch? match = null;
                // If there is an Unimod ID try looking it up by name
                if (info.UniModId.HasValue)
                    match = GetModByName(info);
                if (match == null)
                {
                    // If the modification isn't indicated by a double, assume it must be the name of the modification.
                    if (!info.UserIndicatedHeavy || info.Mass == null)
                        match = info.Mass != null ? GetModByMass(info) : GetModByName(info); 
                    else
                    {
                        StaticMod mod = GetModByMassInSettings(info, (double)info.Mass, false) ??
                                        UniMod.MatchModificationMass((double)info.Mass, info.AA,
                                            info.RoundedTo, false, info.Terminus,
                                            info.AppearsToBeSpecificMod);
                        if (mod != null)
                        {
                            match = new AAModMatch { HeavyMod = mod };
                        }
                    }
                }
                if (match != null)
                {
                    AddMatch(info.ModKey, (AAModMatch)match);
                    var heavyMod = match.Value.HeavyMod;
                    if (!info.AppearsToBeSpecificMod && heavyMod != null && string.IsNullOrEmpty(heavyMod.AAs)
                        && !_foundHeavyLabels.Contains(heavyMod))
                    {
                        _foundHeavyLabels.Add(heavyMod);
                    }
                }
                UpdateMatcher(info, match);
            }
        }

        private AAModMatch? GetModByName(AAModInfo info)
        {
            bool structural = false;
            StaticMod modMatch = null;
            // First, look in the document/global settings.
            foreach (var mod in UserDefinedTypedMods.Keys)
            {
                bool matchStuctural = UserDefinedTypedMods[mod].IsLight;
                if (Equals(info.Name, mod.Name) && 
                    (!info.UserIndicatedHeavy || !matchStuctural))
                {
                    modMatch = mod;
                    structural = matchStuctural;
                }
            }
            // If not found, then look in Unimod.
            modMatch = modMatch ?? UniMod.GetModification(info.Name, out structural);
            if (!info.IsModMatch(modMatch) || (info.UserIndicatedHeavy && structural))
                return null;
            return new AAModMatch
            {
                StructuralMod = structural ? modMatch : null,
                HeavyMod = !structural ? modMatch : null
            };
        }

        private AAModMatch? GetModByMass(AAModInfo info)
        {
            AAModMatch? match = null;
            // Enumerate all possible partial matches for the given AAModInfo, looking for
            // a complete match for the mass.
            foreach (var partialMatch in SearchPartialModMatches(info))
            {
                // If a partial match explains the entire mass, it is a complete match, so the search is done.
                if (partialMatch.UnexplainedMass == 0)
                    return new AAModMatch
                    {
                        StructuralMod = partialMatch.Structural ? partialMatch.Mod : null,
                        HeavyMod = !partialMatch.Structural ? partialMatch.Mod : null
                    };
                // Otherwise, first try to complete the match by looking in the document modifications and 
                // global modifications.
                var matchComplete = GetModByMassInSettings(info, partialMatch.UnexplainedMass, !partialMatch.Structural);
                if (matchComplete != null)
                {
                    // If we complete the match with a modification in the document or global settings, 
                    // return that match.
                    return new AAModMatch
                    {
                        StructuralMod = partialMatch.Structural ? partialMatch.Mod : matchComplete,
                        HeavyMod = !partialMatch.Structural ? partialMatch.Mod : matchComplete
                    };
                }
                // If we already have found a potential match in Unimod, don't continue searching Unimod.
                if (match != null)
                    continue;
                // Look in Unimod to complete the match.
                matchComplete = UniMod.MatchModificationMass(partialMatch.UnexplainedMass, info.AA,
                                                            info.RoundedTo, !partialMatch.Structural,
                                                            info.Terminus, info.AppearsToBeSpecificMod);
                if (matchComplete != null)
                {
                    // A match that is partially explained by unimod is not as good as a match in the document/gobals,
                    // so keep this match as a good candidate, but keep searching.
                    match = new AAModMatch
                    {
                        StructuralMod = partialMatch.Structural ? partialMatch.Mod : matchComplete,
                        HeavyMod = !partialMatch.Structural ? partialMatch.Mod : matchComplete
                    };
                }
            }
            return match;
        }

        /// <summary>
        /// Enumerates partial modifications matches for the given AAModInfo.
        /// 
        /// Enumerate matches in the following order:
        /// 1. Partials matches for any previously matched heavy labeling.
        /// 2. Partial structural match with a null structural modification, this means that any complete match
        ///    would have only a heavy modification.
        /// 3. Partial heavy match with a null heavy modification.
        /// 4. Partial matches from the document settings.
        /// 5. Partial heavy matches from UniMod.
        /// 6. Partial heavy matches from UniMod (hidden).
        /// </summary>
        private IEnumerable<PartialMassMatch> SearchPartialModMatches(AAModInfo info)
        {
            Dictionary<IEnumerable<StaticMod>, bool> listsToSearch = new Dictionary<IEnumerable<StaticMod>, bool>();
            if(!info.AppearsToBeSpecificMod)
                listsToSearch.Add(_foundHeavyLabels, false);
            listsToSearch.Add(new StaticMod[] { null }, true);
            listsToSearch.Add(new StaticMod[] { null }, false);
            foreach (var labelType in Settings.PeptideSettings.Modifications.GetModificationTypes())
            {
                listsToSearch.Add(Settings.PeptideSettings.Modifications.GetModifications(labelType).ToArray(), 
                    labelType.IsLight);
            }
            listsToSearch.Add(DefSetStatic, true);
            listsToSearch.Add(DefSetHeavy, false);
            listsToSearch.Add(UniMod.DictIsotopeModNames.Values, false);
            listsToSearch.Add(UniMod.DictHiddenIsotopeModNames.Values, false);
            return EnumeratePartialModMatches(listsToSearch, info);
        }

        private static IEnumerable<PartialMassMatch> 
            EnumeratePartialModMatches(Dictionary<IEnumerable<StaticMod>, bool> dictModLists, 
            AAModInfo info)
        {
            foreach (var dict in dictModLists)
            {
                var mods = dict.Key;
                // Mod can be null to force search for only light/heavy match.
                foreach (var mod in mods.Where(mod => mod == null || info.IsModMatch(mod)))
                {
                    double modMass = mod == null ? 0 : GetDefaultModMass(info.AA, mod);
                    double mass = (info.Mass ?? 0) - modMass;
                    yield return new PartialMassMatch
                                     {
                                         Mod = mod == null ? null : mod.ChangeExplicit(true),
                                         Structural = dict.Value,
                                         UnexplainedMass = Math.Round(mass,
                                                                      info.RoundedTo)
                                     };
                }
            }
        }

        private StaticMod GetModByMassInSettings(AAModInfo info, double mass, bool structural)
        {
            StaticMod firstMatch = null;
            foreach (var match in EnumerateModMatchesInSettings(info, mass, structural))
            {
                firstMatch = firstMatch ?? match;
                // Keep looking if it is an isotope modification of the 15N variant where it applies to all amino acids
                if (structural || (info.AppearsToBeSpecificMod && (!string.IsNullOrEmpty(match.AAs) || match.Terminus != null))
                               || (!info.AppearsToBeSpecificMod && string.IsNullOrEmpty(match.AAs) && match.Terminus == null))
                {
                    firstMatch = match;
                    break;
                }
            }
            if (firstMatch == null)
                return null;
            // TODO: Is this necessary
            if (!IsInSettings(firstMatch) && !firstMatch.IsVariable)
                firstMatch = firstMatch.ChangeExplicit(true);
            return firstMatch;
        }

        private bool IsInSettings(StaticMod staticMod)
        {
            return Settings.PeptideSettings.Modifications.HasModification(staticMod);
        }

        private IEnumerable<StaticMod> EnumerateModMatchesInSettings(AAModInfo info, double mass, bool structural)
        {
            var modifications = Settings.PeptideSettings.Modifications;
            foreach (var type in modifications.GetModificationTypes())
            {
                foreach (var mod in modifications.GetModifications(type)
                    .Where(mod => info.IsMassMatch(mod, mass)))
                {
                    if (type.IsLight == structural)
                        yield return mod;
                }
            }
            if (!structural)
            {
                foreach (var mod in DefSetHeavy.Where(mod => info.IsMassMatch(mod, mass)))
                {
                    yield return mod;
                }
            }
            else
            {
                foreach (var mod in DefSetStatic.Where(mod => info.IsMassMatch(mod, mass)))
                {
                    yield return mod;
                }
            }
        }

        /// <summary>
        /// Merges any matches where both the terminus match and AA match were found for the
        /// same modification.
        /// </summary>
        private void CleanUpMatches()
        {
            var keys = new List<AAModKey>(Matches.Keys);
            foreach(var key in keys)
            {
                if(key.Terminus == null)
                    continue;
                var keyCopy = key;
                keyCopy.RemoveTerminus();
                if (Matches.ContainsKey(keyCopy))
                    Matches[key] = Matches[keyCopy];
            }
        }

        public string FoundMatches
        {
            get
            {
                var dictModEntries = new Dictionary<string, List<string>>();
                foreach (var seqKeyToMod in Matches)
                {
                    // Skip existing modifications.
                    var mod = seqKeyToMod.Value;
                    if (mod.StructuralMod != null
                        && !UserDefinedTypedMods.ContainsKey(mod.StructuralMod))
                    {
                        var key = seqKeyToMod.Key;
                        List<string> keyStrings;
                        if (!dictModEntries.TryGetValue(mod.StructuralMod.Name, out keyStrings))
                        {
                            keyStrings = new List<string>();
                            dictModEntries.Add(mod.StructuralMod.Name, keyStrings);
                        }
                        if (key.Mass != null)
                        {
                            var keyStr = GetMatchString(key, mod.StructuralMod, true);
                            if (!keyStrings.Contains(keyStr))
                                keyStrings.Add(keyStr);
                        }
                    }
                    if (mod.HeavyMod != null
                        && !UserDefinedTypedMods.ContainsKey(mod.HeavyMod))
                    {
                        var key = seqKeyToMod.Key;
                        List<string> keyStrings;
                        if (!dictModEntries.TryGetValue(mod.HeavyMod.Name, out keyStrings))
                        {
                            keyStrings = new List<string>();
                            dictModEntries.Add(mod.HeavyMod.Name, keyStrings);
                        }
                        if (key.Mass != null)
                        {
                            var keyStr = GetMatchString(key, mod.HeavyMod, false);
                            if (!keyStrings.Contains(keyStr))
                                keyStrings.Add(keyStr);
                        }
                    }
                }
                StringBuilder sb = new StringBuilder();
                foreach (var modKeysPair in dictModEntries.OrderBy(key => key.Key))
                {
                    // Sort by amino acid.
                    var modName = modKeysPair.Key;
                    var keyStrings = modKeysPair.Value;
                    keyStrings.Sort();
                    sb.AppendLine(keyStrings.Count > 0
                                      ? string.Format(Resources.AbstractModificationMatcherFoundMatches__0__equals__1__, modName,
                                          TextUtil.SpaceSeparate(keyStrings))
                                      : modName);
                }
                return sb.ToString();
            }
        }

        private static string GetMatchString(AAModKey key, StaticMod staticMod, bool structural)
        {
            string formatString = structural ? @"{0}[{1}{2}]" : @"{0}{{{1}{2}}}";
            var modMass = Math.Round(GetDefaultModMass(key.AA, staticMod), key.RoundedTo);
            return string.Format(formatString, key.AA, (modMass > 0 ? @"+" : string.Empty), modMass);
        }

        public string UninterpretedMods
        {
            get
            {

                return TextUtil.LineSeparate(ModelResources.AbstractModificationMatcher_UninterpretedMods_The_following_modifications_could_not_be_interpreted,
                                     string.Empty,
                                     TextUtil.SpaceSeparate(UnmatchedSequences.OrderBy(s => s)));
            }
        }

        public abstract PeptideDocNode GetModifiedNode(string sequence);

        public PeptideDocNode CreateDocNodeFromSettings(LibKey key, Peptide peptide, SrmSettingsDiff diff, out TransitionGroupDocNode nodeGroupMatched)
        {
            if (key.LibraryKey is CrosslinkLibraryKey)
            {
                return CreateCrosslinkDocNode(peptide, (CrosslinkLibraryKey) key.LibraryKey, diff,
                    out nodeGroupMatched);
            }
            if (!key.Target.IsProteomic)
            {
                // Scan the spectral lib entry for top N ranked (for now, that's just by intensity with high mz as tie breaker) fragments, 
                // add those as mass-only fragments, or with more detail if peak annotations are present.
                foreach (var nodePep in peptide.CreateDocNodes(Settings, new MaxModFilter(0)))
                {
                    SpectrumHeaderInfo libInfo;
                    if (nodePep != null && Settings.PeptideSettings.Libraries.TryGetLibInfo(key, out libInfo))
                    {
                        var isotopeLabelType = key.Adduct.HasIsotopeLabels ? IsotopeLabelType.heavy : IsotopeLabelType.light;
                        var group = new TransitionGroup(peptide, key.Adduct, isotopeLabelType);
                        nodeGroupMatched = new TransitionGroupDocNode(group, Annotations.EMPTY, Settings, null, libInfo, ExplicitTransitionGroupValues.EMPTY, null, null, false);
                        SpectrumPeaksInfo spectrum;
                        if (Settings.PeptideSettings.Libraries.TryLoadSpectrum(key, out spectrum))
                        {
                            // Add fragment and precursor transitions as needed
                            var transitionDocNodes =
                                Settings.TransitionSettings.Filter.SmallMoleculeIonTypes.Contains(IonType.precursor)
                                    ? nodeGroupMatched.GetPrecursorChoices(Settings, null, true) // Gives list of precursors
                                    : new List<DocNode>();

                            if (Settings.TransitionSettings.Filter.SmallMoleculeIonTypes.Contains(IonType.custom))
                            {
                                GetSmallMoleculeFragments(key, nodeGroupMatched, spectrum, transitionDocNodes);
                            }
                            nodeGroupMatched = (TransitionGroupDocNode)nodeGroupMatched.ChangeChildren(transitionDocNodes);
                            return (PeptideDocNode)nodePep.ChangeChildren(new List<DocNode>() { nodeGroupMatched });
                        }
                    }
                }
                nodeGroupMatched = null;
                return null;
            }
            return CreateDocNodeFromSettings(key.Target, peptide, diff, out nodeGroupMatched);
        }

        private void GetSmallMoleculeFragments(LibKey key, TransitionGroupDocNode nodeGroupMatched, SpectrumPeaksInfo spectrum,
            IList<DocNode> transitionDocNodes)
        {
            // We usually don't know actual charge of fragments in the library, so just note + or - if
            // there are no peak annotations containing that info
            var fragmentCharge = key.Adduct.AdductCharge < 0 ? Adduct.M_MINUS : Adduct.M_PLUS;
            // Get list of possible transitions based on library spectrum
            var transitionsUnranked = new List<DocNode>();
            foreach (var peak in spectrum.Peaks)
            {
                try
                {
                    transitionsUnranked.Add(TransitionFromPeakAndAnnotations(key, nodeGroupMatched, fragmentCharge, peak, null));
                }
                catch (InvalidDataException)
                {
                    // Some kind of garbage in peaklist, e.g fragment mass is absurdly small or large - ignore
                    // TODO(bspratt) - address Brendan's comment from pull request:
                    // "This call should be paying attention to settings and the minimum value that causes the exception reported to initiate this fix.For peptide fragment
                    // annotation, we definitely consider the settings, and since we do not rank fragments outside the instrument range. This code also strikes me as odd that you wouldn't just create the precursor
                    // and then use a precursor.ChangeSettings(Settings, diff ?? SrmSettingsDiff.ALL) to materialize all of the transitions based on the settings. That way you only write the code once to materialize
                    // transitions based on settings."
                    // In particular not ranking things outside the machine range makes sense.
                } 
            }
            var nodeGroupUnranked = (TransitionGroupDocNode) nodeGroupMatched.ChangeChildren(transitionsUnranked);
            // Filter again, retain only those with rank info,  or at least an interesting name
            SpectrumHeaderInfo groupLibInfo = null;
            var transitionRanks = new Dictionary<double, LibraryRankedSpectrumInfo.RankedMI>();
            nodeGroupUnranked.GetLibraryInfo(Settings, ExplicitMods.EMPTY, true, ref groupLibInfo, transitionRanks);
            foreach (var ranked in transitionRanks)
            {
                transitionDocNodes.Add(TransitionFromPeakAndAnnotations(key, nodeGroupMatched, fragmentCharge, ranked.Value.MI, ranked.Value.Rank));
            }
            // And add any unranked that have names to display
            foreach (var unrankedT in nodeGroupUnranked.Transitions)
            {
                var unranked = unrankedT;
                if (!string.IsNullOrEmpty(unranked.Transition.CustomIon.Name) &&
                    !transitionDocNodes.Any(t => t is TransitionDocNode && unranked.Transition.Equivalent(((TransitionDocNode) t).Transition)))
                {
                    transitionDocNodes.Add(unranked);
                }
            }
        }

        private TransitionDocNode TransitionFromPeakAndAnnotations(LibKey key, TransitionGroupDocNode nodeGroup,
            Adduct fragmentCharge, SpectrumPeaksInfo.MI peak, int? rank)
        {
            var spectrumPeakAnnotationIon = peak.AnnotationsAggregateDescriptionIon;
            var charge = spectrumPeakAnnotationIon.Adduct.IsEmpty ? fragmentCharge : spectrumPeakAnnotationIon.Adduct;
            var monoisotopicMass = charge.MassFromMz(peak.Mz, MassType.Monoisotopic);
            var averageMass = charge.MassFromMz(peak.Mz, MassType.Average);
            // Caution here - library peak (observed) mz may not exactly match (theoretical) mz of the annotation

            // In the case of multiple annotations, produce single transition for display in library explorer
            var annotations = peak.GetAnnotationsEnumerator().ToArray();
            var molecule = spectrumPeakAnnotationIon.Adduct.IsEmpty
                ? new CustomMolecule(monoisotopicMass, averageMass)
                : spectrumPeakAnnotationIon;
            var note = (annotations.Length > 1) ? TextUtil.LineSeparate(annotations.Select(a => a.ToString())) : null;
            var noteIfAnnotationMzDisagrees = NoteIfAnnotationMzDisagrees(key, peak);
            if (noteIfAnnotationMzDisagrees != null)
            {
                if (note == null)
                {
                    note = noteIfAnnotationMzDisagrees;
                }
                else
                {
                    note = TextUtil.LineSeparate(note, noteIfAnnotationMzDisagrees);
                }
            }
            var transition = new Transition(nodeGroup.TransitionGroup,
                charge, 0, molecule);
            return new TransitionDocNode(transition, Annotations.EMPTY.ChangeNote(note), null, monoisotopicMass,
                rank.HasValue ?
                    new TransitionDocNode.TransitionQuantInfo(null,
                        new TransitionLibInfo(rank.Value, peak.Intensity), true) :
                    TransitionDocNode.TransitionQuantInfo.DEFAULT, ExplicitTransitionValues.EMPTY, null);
        }

        private string NoteIfAnnotationMzDisagrees(LibKey key, SpectrumPeaksInfo.MI peak)
        {
            foreach (var peakAnnotation in peak.GetAnnotationsEnumerator())
            {
                var charge = peakAnnotation.Ion.Adduct;
                var monoisotopicMass = charge.MassFromMz(peak.Mz, MassType.Monoisotopic);
                var averageMass = charge.MassFromMz(peak.Mz, MassType.Average);

                if (!(peakAnnotation.Ion.MonoisotopicMass.Equals(monoisotopicMass,
                          Settings.TransitionSettings.Instrument.MzMatchTolerance) ||
                      peakAnnotation.Ion.AverageMass.Equals(averageMass,
                          Settings.TransitionSettings.Instrument.MzMatchTolerance)))
                {

                    return string.Format(
                        @"annotated observed ({0}) and theoretical ({1}) masses differ for peak {2} of library entry {3} by more than the current instrument mz match tolerance of {4}",
                        peak.Mz, peakAnnotation.Ion.MonoisotopicMassMz, peakAnnotation,
                        key,
                        Settings.TransitionSettings.Instrument.MzMatchTolerance);
                }
            }
            return null;
        }

        protected bool HasMods(string sequence)
        {
            return FastaSequence.RGX_ALL.IsMatch(sequence);
        }

        public PeptideDocNode CreateDocNodeFromSettings(Target target, Peptide peptide, SrmSettingsDiff diff,
                out TransitionGroupDocNode nodeGroupMatched)
        {
            if (!target.IsProteomic)
            {
                nodeGroupMatched = null; 
                return null;
            }

            var crosslinkLibraryKey = CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(target.Sequence, 0);

            if (null != crosslinkLibraryKey)
            {
                return CreateCrosslinkDocNode(peptide, crosslinkLibraryKey, diff, out nodeGroupMatched);
            }
            var seq = target.Sequence;
            seq = Transition.StripChargeIndicators(seq, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, true);
            if (peptide == null)
            {
                string seqUnmod = FastaSequence.StripModifications(seq);
                try
                {
                    peptide = new Peptide(null, seqUnmod, null, null,
                        Settings.PeptideSettings.Enzyme.CountCleavagePoints(seqUnmod));
                }
                catch (InvalidDataException)
                {
                    nodeGroupMatched = null;
                    return null;
                }
            }

            // Use the number of modifications as the maximum, if it is less than the current
            // settings to keep from over enumerating, which can be slow.
            int seqModCount = seq.Count(c => c == '[' || c == '(');
            var filterMaxMod = new MaxModFilter(Math.Min(seqModCount,
                Settings.PeptideSettings.Modifications.MaxVariableMods));
            var filterMod = new VariableModLocationFilter(seq);
            var newTarget = new Target(seq);
            foreach (var nodePep in peptide.CreateDocNodes(Settings, filterMaxMod, filterMod))
            {
                var nodePepMod = CreateDocNodeFromSettings(newTarget, nodePep, diff, out nodeGroupMatched);
                if (nodePepMod != null)
                    return nodePepMod;
            }
            nodeGroupMatched = null;
            return null;
        }

        public PeptideDocNode CreateCrosslinkDocNode(Peptide peptide, CrosslinkLibraryKey crosslinkLibraryKey,
            SrmSettingsDiff diff,
            out TransitionGroupDocNode nodeGroupMatched)
        {
            if (!crosslinkLibraryKey.IsSupportedBySkyline())
            {
                nodeGroupMatched = null;
                return null;
            }
            nodeGroupMatched = null;
            var mainPeptide = MakePeptideDocNode(crosslinkLibraryKey.PeptideLibraryKeys[0]);
            if (mainPeptide == null)
            {
                return null;
            }

            var crosslinkStructure = MakeCrosslinkStructure(mainPeptide.Peptide.Sequence, crosslinkLibraryKey);
            if (crosslinkStructure == null)
            {
                return null;
            }

            var staticMods = new List<ExplicitMod>();
            if (null != mainPeptide.ExplicitMods)
            {
                staticMods.AddRange(mainPeptide.ExplicitMods.StaticModifications);
            }

            var newMods = new ExplicitMods(mainPeptide.Peptide, staticMods,
                mainPeptide.ExplicitMods?.GetHeavyModifications()).ChangeCrosslinkStructure(crosslinkStructure);
            var crosslinkedPeptide = mainPeptide.ChangeExplicitMods(newMods).ChangeSettings(Settings, diff ?? SrmSettingsDiff.ALL);
            if (!crosslinkLibraryKey.Adduct.IsEmpty)
            {
                nodeGroupMatched = new TransitionGroupDocNode(
                    new TransitionGroup(mainPeptide.Peptide, crosslinkLibraryKey.Adduct, IsotopeLabelType.light),
                    Annotations.EMPTY,
                    Settings, newMods, null, ExplicitTransitionGroupValues.EMPTY, null, null, true);
                crosslinkedPeptide = (PeptideDocNode)crosslinkedPeptide.ChangeChildren(new DocNode[] { nodeGroupMatched });
            }

            return crosslinkedPeptide;
        }

        public CrosslinkStructure MakeCrosslinkStructure(string mainSequence, CrosslinkLibraryKey crosslinkLibraryKey)
        {
            var linkedPeptides = new List<Peptide>();
            var linkedExplicitMods = new List<ExplicitMods>();
            for (int i = 1; i < crosslinkLibraryKey.PeptideLibraryKeys.Count; i++)
            {
                var peptideDocNode = MakePeptideDocNode(crosslinkLibraryKey.PeptideLibraryKeys[i]);
                if (peptideDocNode == null)
                {
                    return null;
                }
                linkedPeptides.Add(peptideDocNode.Peptide);
                linkedExplicitMods.Add(peptideDocNode.ExplicitMods ?? new ExplicitMods(peptideDocNode.Peptide, null, null));
            }

            var peptideSequences = new List<string> {mainSequence};
            peptideSequences.AddRange(linkedPeptides.Select(pep=>pep.Sequence));
            var crosslinks = new List<Crosslink>();
            foreach (var crosslink in crosslinkLibraryKey.Crosslinks)
            {
                var sites = crosslink.CrosslinkSites.ToList();
                if (sites.Count != 2)
                {
                    return null;
                }

                var crosslinker = FindCrosslinkMod(crosslink.Name, peptideSequences[sites[0].PeptideIndex],
                    sites[0].AaIndex, peptideSequences[sites[1].PeptideIndex], sites[1].AaIndex);
                if (crosslinker == null)
                {
                    return null;
                }
                crosslinks.Add(new Crosslink(crosslinker, sites));
            }
            return new CrosslinkStructure(linkedPeptides, linkedExplicitMods, crosslinks);
        }

        protected AAModInfo MakeCrosslinkAaModInfo(CrosslinkLibraryKey crosslinkLibraryKey, CrosslinkLibraryKey.Crosslink crosslink)
        {
            var firstCrosslinkSite = crosslink.CrosslinkSites.First();
            AAModKey modKey = new AAModKey()
            {
                IsCrosslinker = true,
                AA = crosslinkLibraryKey.PeptideLibraryKeys[firstCrosslinkSite.PeptideIndex]
                    .UnmodifiedSequence[firstCrosslinkSite.AaIndex]
            };
            var massModification = MassModification.Parse(crosslink.Name);
            if (massModification == null)
            {
                modKey.Name = crosslink.Name;
            }
            else
            {
                modKey.Mass = massModification.Mass;
                modKey.RoundedTo = massModification.Precision;
            }

            AAModInfo modInfo = new AAModInfo
            {
                IndexAA = firstCrosslinkSite.AaIndex,
                ModKey = modKey
            };
            return modInfo;
        }

        private StaticMod FindCrosslinkMod(string crosslinkName, string sequence1, int indexAa1, String sequence2,
            int indexAa2)
        {
            IEnumerable<StaticMod> allMods = Settings.PeptideSettings.Modifications.StaticModifications;
            if (null != DefSetStatic)
            {
                allMods = allMods.Concat(DefSetStatic);
            }
            var massModification = MassModification.Parse(crosslinkName);
            foreach (var mod in allMods.Where(mod=>null != mod.CrosslinkerSettings))
            {
                if (crosslinkName == mod.Name)
                {
                    return mod;
                }

                if (!mod.MonoisotopicMass.HasValue || massModification == null)
                {
                    continue;
                }

                if (!massModification.Matches(MassModification.FromMass(mod.MonoisotopicMass.Value)))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(mod.AAs))
                {
                    if (!mod.AAs.Contains(sequence1[indexAa1]) || !mod.AAs.Contains(sequence2[indexAa2]))
                    {
                        continue;
                    }
                }

                return mod;
            }

            return null;
        }

        protected virtual PeptideDocNode MakePeptideDocNode(PeptideLibraryKey peptideLibraryKey)
        {
            var peptide = new Peptide(peptideLibraryKey.UnmodifiedSequence);
            var explicitModList = new List<ExplicitMod>();
            foreach (var mod in peptideLibraryKey.GetModifications())
            {
                var aaModKey = new AAModKey()
                {
                    AA = peptide.Sequence[mod.Key],
                    AppearsToBeSpecificMod = true
                };
                MassModification massModification = MassModification.Parse(mod.Value);
                if (massModification != null)
                {
                    aaModKey.Mass = massModification.Mass;
                }
                else
                {
                    aaModKey.Name = mod.Value;
                }

                if (mod.Key == 0)
                {
                    aaModKey.Terminus = ModTerminus.N;
                }
                else if (mod.Key == peptide.Sequence.Length - 1)
                {
                    aaModKey.Terminus = ModTerminus.C;
                }

                var staticMod = FindModification(aaModKey);
                if (staticMod == null)
                {
                    return null;
                }
                explicitModList.Add(new ExplicitMod(mod.Key, staticMod));
            }
            return new PeptideDocNode(peptide, new ExplicitMods(peptide, explicitModList, null));
        }

        private StaticMod FindModification(AAModKey aaModKey)
        {
            if (Matches != null)
            {
                var match = GetMatch(aaModKey);
                if (match != null)
                {
                    return match.Value.StructuralMod;
                }
            }

            MassModification massModification = aaModKey.Mass.HasValue
                ? MassModification.FromMass(aaModKey.Mass.Value)
                : null;

            foreach (var staticMod in Settings.PeptideSettings.Modifications.StaticModifications)
            {
                if (massModification != null)
                {
                    if (!staticMod.MonoisotopicMass.HasValue)
                    {
                        continue;
                    }

                    if (!massModification.Matches(MassModification.FromMass(staticMod.MonoisotopicMass.Value)))
                    {
                        continue;
                    }
                }
                else if (staticMod.Name != aaModKey.Name)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(staticMod.AAs))
                {
                    if (!staticMod.AAs.Contains(aaModKey.AA))
                    {
                        continue;
                    }
                }

                if (staticMod.Terminus.HasValue)
                {
                    if (staticMod.Terminus != aaModKey.Terminus)
                    {
                        continue;
                    }
                }

                return staticMod;
            }

            return null;
        }

        private class MaxModFilter : IPeptideFilter
        {
            public MaxModFilter(int max)
            {
                MaxVariableMods = max;
            }

            public bool Accept(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods, out bool allowVariableMods)
            {
                return PeptideFilter.UNFILTERED.Accept(settings, peptide, explicitMods, out allowVariableMods);
            }

            public int? MaxVariableMods { get; private set; }
        }

        public class VariableModLocationFilter : IVariableModFilter
        {
            private struct ModMass
            {
                public ModMass(double mass, int precision) : this()
                {
                    Precision = Math.Min(15, precision);    // Math.Round can only handle up to 15
                    Mass = Math.Round(mass, Precision);
                }

                public double Mass { get; private set; }
                public int Precision { get; private set; }

                public override string ToString()
                {
                    string formatMass = @"{0:F0" + Precision + @"}";
                    return string.Format(formatMass, Mass);
                }
            }

            private readonly ModMass?[] _mods;

            public VariableModLocationFilter(string seq)
            {
                _mods = new ModMass?[seq.Length];    // Overallocation should not matter
                int aaIndex = 0;
                for (int i = 0; i < seq.Length - 1; i++)
                {
                    string closeChar = GetCloseChar(seq[i + 1]);
                    if (closeChar != null)
                    {
                        int startIndex = i + 2;
                        string modText;
                        bool candidateSeen;
                        int closeIndex = GetCloseIndex(seq, startIndex, closeChar, out modText, out candidateSeen);
                        if (candidateSeen)
                        {
                            // If seen candidate(s) not expressible as text
                            if (modText == null)
                                _mods[aaIndex] = new ModMass(); // Add wildcard
                            // Otherwise try determine an acceptable mass and precision
                            else
                            {
                                int dotIndex = modText.IndexOf(@".", StringComparison.Ordinal);
                                if (dotIndex == -1)
                                    dotIndex = modText.Length - 1;  // Just before the close index for zero precision
                                double mass;
                                if (double.TryParse(modText,
                                    NumberStyles.Float | NumberStyles.AllowThousands,
                                    CultureInfo.InvariantCulture,
                                    out mass))
                                {
                                    _mods[aaIndex] = new ModMass(mass, modText.Length - dotIndex - 1);
                                }
                                else
                                {
                                    _mods[aaIndex] = new ModMass(); // Add wildcard
                                }
                            }
                        }

                        i = closeIndex;
                    }
                    aaIndex++;
                }
            }

            private const string HEAVY_LABEL_CLOSE = "}";

            private string GetCloseChar(char c)
            {
                switch (c)
                {
                    case '{': return HEAVY_LABEL_CLOSE;  // Heavy label
                    case '(': return @")";  // Unimod modification
                    case '[': return @"]";  // Custome mod - delta mass or name
                }
                return null;
            }

            /// <summary>
            /// Gets the closing character for a mod, handling strings of mods on after the other
            /// </summary>
            /// <param name="seq">The sequence with embedded mods</param>
            /// <param name="startIndex">The start index of the mod text beyond the opening character</param>
            /// <param name="closeChar">The character that closes the mod</param>
            /// <param name="modText">The text that is the candidate modification</param>
            /// <param name="candidateSeen">Set to true if a variable modification candidate was seen</param>
            /// <returns>Index of the closing character</returns>
            private int GetCloseIndex(string seq, int startIndex, string closeChar,
                out string modText, out bool candidateSeen)
            {
                candidateSeen = false;
                modText = null;
                int closeIndex = -1;
                while (closeChar != null)
                {
                    closeIndex = seq.IndexOf(closeChar, startIndex, StringComparison.Ordinal);
                    if (closeIndex == -1)
                        closeIndex = seq.Length;
                    if (closeChar != HEAVY_LABEL_CLOSE)
                    {
                        // Only non-heavy label mods are candidates for variable modifications
                        modText = !candidateSeen ? seq.Substring(startIndex, closeIndex - startIndex) : null;
                        // Record the first occurance, and otherwise return no text indicating
                        // a wild card modification
                        candidateSeen = true;
                    }
                    if (closeIndex >= seq.Length - 1)
                        break;
                    startIndex = closeIndex + 1;
                    closeChar = GetCloseChar(seq[startIndex]);
                }
                return closeIndex;
            }

            public bool IsModIndex(int index)
            {
                return _mods[index].HasValue;
            }

            public bool IsModMass(int index, double mass)
            {
                var modNullable = _mods[index];
                if (!modNullable.HasValue)
                    return false;
                var mod = modNullable.Value;
                // Zero mass modification acts as a wildcard
                if (mod.Mass == 0)
                    return true;
                // Otherwise, masses must match to the specified precision
                return Math.Round(mass, mod.Precision) == Math.Round(mod.Mass, mod.Precision);
            }
        }

        private PeptideDocNode CreateDocNodeFromSettings(Target seq, PeptideDocNode nodePep, SrmSettingsDiff diff,
            out TransitionGroupDocNode nodeGroupMatched)
        {
            PeptideDocNode nodePepMod = nodePep.ChangeSettings(Settings, diff ?? SrmSettingsDiff.ALL, false);
            TransitionGroupDocNode nodeGroupMatchedFound;
            if (IsMatch(seq, nodePepMod, out nodeGroupMatchedFound))
            {
                nodeGroupMatched = nodeGroupMatchedFound;
                return nodePepMod;
            }
            nodeGroupMatched = null;
            return null;
        }

        protected abstract bool IsMatch(Target seq, PeptideDocNode nodePep, out TransitionGroupDocNode nodeGroup);

        public PeptideDocNode CreateDocNodeFromMatches(PeptideDocNode nodePep, IEnumerable<AAModInfo> infos)
        {
            return CreateDocNodeFromMatches(nodePep, infos, true, out _);
        }

        public PeptideDocNode CreateDocNodeFromMatches(PeptideDocNode nodePep, IEnumerable<AAModInfo> infos, bool stringPaste, out bool hasHeavy)
        {
            hasHeavy = false;
            List<ExplicitMod> listLightMods = new List<ExplicitMod>();
            var dictHeavyMods = new Dictionary<IsotopeLabelType, List<ExplicitMod>>();
            foreach (var info in infos)
            {
                var match = GetMatch(info.ModKey);
                if (match == null)
                    return null;
                AAModMatch modMatch = (AAModMatch)match;
                var lightMod = modMatch.StructuralMod;
                if (lightMod != null)
                {
                    // Make sure all mods are explicit for ensure mods.
                    if (stringPaste)
                        lightMod = lightMod.ChangeExplicit(true);
                    listLightMods.Add(new ExplicitMod(info.IndexAA, lightMod));
                }
                var heavyMod = modMatch.HeavyMod;
                if (heavyMod != null)
                {
                    var type = UserDefinedTypedMods.TryGetValue(modMatch.HeavyMod, out var mod)
                        ? mod
                        : DocDefHeavyLabelType;
                    List<ExplicitMod> listHeavyMods;
                    if (!dictHeavyMods.TryGetValue(type, out listHeavyMods))
                    {
                        listHeavyMods = new List<ExplicitMod>();
                        dictHeavyMods.Add(type, listHeavyMods);
                    }
                    listHeavyMods.Add(new ExplicitMod(info.IndexAA, heavyMod));
                }
            }
            
            // Build the set of explicit modifications for the peptide.
            // If ensure mods is set to true, then perform the work here to ensure
            // that the mods persist corectly with the current settings.
            var targetImplicitMods = new ExplicitMods(nodePep,
                Settings.PeptideSettings.Modifications.StaticModifications,
                DefSetStatic,
                Settings.PeptideSettings.Modifications.GetHeavyModifications(),
                DefSetHeavy).ChangeCrosslinkStructure(nodePep.CrosslinkStructure);
            // If no light modifications are present, this code assumes the user wants the 
            // default global light modifications.  Unless not stringPaste, in which case the target
            // static mods must also be empty
            if (listLightMods.Count(m => m.Modification.HasMod) == 0 && (stringPaste || targetImplicitMods.StaticModifications.Count == 0))
                listLightMods = null;
            else if (stringPaste && ArrayUtil.EqualsDeep(listLightMods.ToArray(), targetImplicitMods.StaticModifications))
                listLightMods = null;
            var listTypedHeavyMods = new List<TypedExplicitModifications>();
            foreach (var targetDocMod in targetImplicitMods.GetHeavyModifications())
            {
                List<ExplicitMod> listMods;
                if (dictHeavyMods.TryGetValue(targetDocMod.LabelType, out listMods)
                        && (!stringPaste || !ArrayUtil.EqualsDeep(listMods, targetDocMod.Modifications)))
                    listTypedHeavyMods.Add(new TypedExplicitModifications(nodePep.Peptide, targetDocMod.LabelType, listMods)
                        .AddModMasses(listLightMods == null ? null : new TypedExplicitModifications(nodePep.Peptide, IsotopeLabelType.light, listLightMods)));
            }
            // Put the explicit modifications on the peptide.
            ExplicitMods mods = (listLightMods != null || listTypedHeavyMods.Count > 0)
                ? new ExplicitMods(nodePep.Peptide, listLightMods, listTypedHeavyMods, 
                    listLightMods != null && listLightMods.Contains(mod => mod.Modification.IsVariable))
                : null;
            hasHeavy = dictHeavyMods.Keys.Count > 0;
            return nodePep.ChangeExplicitMods(mods).ChangeSettings(Settings, SrmSettingsDiff.PROPS);
        }

        public virtual PeptideModifications GetDocModifications(SrmDocument document)
        {
            var lightMods = new MappedList<string, StaticMod>();
            lightMods.AddRange(DefSetStatic);
            var heavyMods = new MappedList<string, StaticMod>();
            heavyMods.AddRange(DefSetHeavy);
            foreach (var matchPair in Matches)
            {
                var lightMod = matchPair.Value.StructuralMod;
                if (lightMod != null && !lightMods.Contains(lightMod))
                    lightMods.Add(lightMod.ChangeExplicit(true));
                var heavyMod = matchPair.Value.HeavyMod;
                if (heavyMod != null && !heavyMods.Contains(heavyMod))
                    heavyMods.Add(heavyMod.ChangeExplicit(true));
            }
            return document.Settings.PeptideSettings.Modifications.DeclareExplicitMods(document, lightMods, heavyMods);
        }

        public abstract bool MoveNextSequence();
        public abstract IEnumerable<AAModInfo> GetCurrentSequenceInfos();
        public abstract void UpdateMatcher(AAModInfo info, AAModMatch? match);

        public struct AAModInfo
        {
            public AAModKey ModKey { get; set; }
            public int IndexAA { get; set; }
            public int IndexAAInSeq { get; set; }
            public char AA { get { return ModKey.AA; } }
            public ModTerminus? Terminus { get { return ModKey.Terminus; } }
            public bool UserIndicatedHeavy { get { return ModKey.UserIndicatedHeavy; } }
            public double? Mass { get { return ModKey.Mass; } }
            public string Name { get { return ModKey.Name; } }
            public int? UniModId { get { return ModKey.UniModId; } }
            public int RoundedTo { get { return ModKey.RoundedTo; } }
            public bool AppearsToBeSpecificMod { get { return ModKey.AppearsToBeSpecificMod; } }
            public bool IsMassMatch(StaticMod mod, double mass)
            {
                var mod1 = new MassModification(GetDefaultModMass(AA, mod), RoundedTo);
                var mod2 = new MassModification(mass, RoundedTo);
                if (!mod1.Matches(mod2))
                {
                    return false;
                }
                return IsModMatch(mod);
            }
            public bool IsModMatch(StaticMod mod)
            {
                return mod != null
                       && (string.IsNullOrEmpty(mod.AAs) ||
                           mod.AminoAcids.ContainsAA(AA.ToString(CultureInfo.InvariantCulture)))
                       && ((mod.Terminus == null) || Equals(mod.Terminus, Terminus))
                       && mod.IsCrosslinker == ModKey.IsCrosslinker;
            }
        }

        public struct AAModKey
        {
            public char AA { get; set; }
            private ModTerminus? _terminus;
            public ModTerminus? Terminus
            {
                get { return _terminus; }
                set { _terminus = value; }
            }
            public double? Mass { get; set; }
            public string Name { get; set; }
            public int? UniModId { get; set; }
            public int RoundedTo { get; set; }
            public bool AppearsToBeSpecificMod { get; set; }
            public bool UserIndicatedHeavy { get; set; }
            public bool IsCrosslinker { get; set; }
            public void RemoveTerminus()
            {
                _terminus = null;
            }
            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, UserIndicatedHeavy ? @"{0}{{{1}{2}}}" : @"{0}[{1}{2}]",
                    AA, Mass > 0 ? @"+" : string.Empty, Mass) + TerminusText;
            }

            public string TerminusText => Terminus.HasValue ? @"-" + Terminus.Value : string.Empty;
        }

        public struct AAModMatch
        {
            public StaticMod StructuralMod { get; set; }
            public StaticMod HeavyMod { get; set; }

            public override string ToString()
            {
                return (StructuralMod ?? HeavyMod)?.ToString() ?? string.Empty;
            }
        }

        public struct PartialMassMatch
        {
            public StaticMod Mod { get; set; }
            public bool Structural { get; set; }
            public double UnexplainedMass { get; set; }
        }
    }
}
