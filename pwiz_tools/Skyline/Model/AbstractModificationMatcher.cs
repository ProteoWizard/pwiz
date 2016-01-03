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
using pwiz.Skyline.Model.DocSettings;
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

        private static readonly SequenceMassCalc CALC_DEFAULT = new SequenceMassCalc(MassType.Monoisotopic);
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
            return Matches.ContainsKey(key)
                ? Matches[key]
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
                // If the modification isn't indicated by a double, assume it must be the name of the modification.
                AAModMatch? match;
                if (!info.UserIndicatedHeavy || info.Mass == null)
                    match = info.Mass != null ? GetModByMass(info) : GetModByName(info); 
                else
                {
                    StaticMod mod = GetModByMassInSettings(info, (double) info.Mass, false) ??
                                    UniMod.MassLookup.MatchModificationMass((double) info.Mass, info.AA,
                                                                            info.RoundedTo, false, info.Terminus,
                                                                            info.AppearsToBeSpecificMod);
                    match = mod == null ? (AAModMatch?) null : new AAModMatch {HeavyMod = mod};
                }
                if (match != null)
                {
                    AddMatch(info.ModKey, (AAModMatch)match);
                    var heavyMod = match.Value.HeavyMod;
                    if (!info.AppearsToBeSpecificMod && heavyMod != null && string.IsNullOrEmpty(heavyMod.AAs)
                        && !_foundHeavyLabels.Contains(heavyMod))
                            _foundHeavyLabels.Add(heavyMod);
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
                matchComplete = UniMod.MassLookup.MatchModificationMass(partialMatch.UnexplainedMass, info.AA,
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
                            var keyStr = GetMatchString(key, mod.StructuralMod, true); // Not L10N
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
                            var keyStr = GetMatchString(key, mod.HeavyMod, false); // Not L10N
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
            string formatString = structural ? "{0}[{1}{2}]" : "{0}{{{1}{2}}}"; // Not L10N
            var modMass = Math.Round(GetDefaultModMass(key.AA, staticMod), key.RoundedTo);
            return string.Format(formatString, key.AA, (modMass > 0 ? "+" : string.Empty), modMass); // Not L10N
        }

        public string UninterpretedMods
        {
            get
            {

                return string.Format(TextUtil.LineSeparate(Resources.AbstractModificationMatcher_UninterpretedMods_The_following_modifications_could_not_be_interpreted,
                                     string.Empty,
                                     TextUtil.SpaceSeparate(UnmatchedSequences.OrderBy(s => s))));
            }
        }

        public PeptideDocNode CreateDocNodeFromSettings(string seq, Peptide peptide, SrmSettingsDiff diff,
             out TransitionGroupDocNode nodeGroupMatched)
        {
            seq = Transition.StripChargeIndicators(seq, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
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
            var filter = new MaxModFilter(Math.Min(seq.Count(c => c == '[' || c == '('),
                                                   Settings.PeptideSettings.Modifications.MaxVariableMods));
            foreach (var nodePep in peptide.CreateDocNodes(Settings, filter))
            {
                var nodePepMod = CreateDocNodeFromSettings(seq, nodePep, diff, out nodeGroupMatched);
                if (nodePepMod != null)
                    return nodePepMod;
            }
            nodeGroupMatched = null;
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

        private PeptideDocNode CreateDocNodeFromSettings(string seq, PeptideDocNode nodePep, SrmSettingsDiff diff,
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

        protected abstract bool IsMatch(string seq, PeptideDocNode nodePep, out TransitionGroupDocNode nodeGroup);

        public PeptideDocNode CreateDocNodeFromMatches(PeptideDocNode nodePep, IEnumerable<AAModInfo> infos)
        {
            bool hasHeavy;
            return CreateDocNodeFromMatches(nodePep, infos, true, out hasHeavy);
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
                    var type = UserDefinedTypedMods.ContainsKey(modMatch.HeavyMod)
                                   ? UserDefinedTypedMods[modMatch.HeavyMod]
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
                DefSetHeavy);
            // If no light modifications are present, this code assumes the user wants the 
            // default global light modifications.  Unless not stringPaste, in which case the target
            // static mods must also be empty
            if (listLightMods.Count == 0 && (stringPaste || targetImplicitMods.StaticModifications.Count == 0))
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
            public int RoundedTo { get { return ModKey.RoundedTo; } }
            public bool AppearsToBeSpecificMod { get { return ModKey.AppearsToBeSpecificMod; } }
            public bool IsMassMatch(StaticMod mod, double mass)
            {
                return Equals(Math.Round(GetDefaultModMass(AA, mod), RoundedTo), mass)
                    && IsModMatch(mod);
            }
            public bool IsModMatch(StaticMod mod)
            {
                return mod != null
                    && (string.IsNullOrEmpty(mod.AAs) ||
                        mod.AminoAcids.ContainsAA(AA.ToString(CultureInfo.InvariantCulture)))
                    && ((mod.Terminus == null) || Equals(mod.Terminus, Terminus));
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
            public int RoundedTo { get; set; }
            public bool AppearsToBeSpecificMod { get; set; }
            public bool UserIndicatedHeavy { get; set; }
            public void RemoveTerminus()
            {
                _terminus = null;
            }
            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, UserIndicatedHeavy ? "{0}{{{1}{2}}}" : "{0}[{1}{2}]",    // Not L10N
                    AA, Mass > 0 ? "+" : string.Empty, Mass); // Not L10N
            }
        }

        public struct AAModMatch
        {
            public StaticMod StructuralMod { get; set; }
            public StaticMod HeavyMod { get; set; }
        }

        public struct PartialMassMatch
        {
            public StaticMod Mod { get; set; }
            public bool Structural { get; set; }
            public double UnexplainedMass { get; set; }
        }
    }
}
