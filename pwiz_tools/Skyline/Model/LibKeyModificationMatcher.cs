/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using System.Linq;

namespace pwiz.Skyline.Model
{
    public class LibKeyModificationMatcher : AbstractModificationMatcher
    {
        private IEnumerator<LibKey> _libKeys;
        private Dictionary<AATermKey, List<byte[]>> _dictAAMassPairs;
        private readonly bool?[] _aasConflictingMods = new bool?[128];

        public PeptideModifications MatcherPepMods { get; set; }

        public void CreateMatches(SrmSettings settings, IEnumerable<LibKey> libKeys,
            MappedList<string, StaticMod> defSetStatic, MappedList<string, StaticMod> defSetHeavy)
        {
            _dictAAMassPairs = new Dictionary<AATermKey, List<byte[]>>();
            _libKeys = libKeys.GetEnumerator();
            InitMatcherSettings(settings, defSetStatic, defSetHeavy);
            MatcherPepMods = CreateMatcherPeptideSettings();
        }

        /// <summary>
        ///  Create PeptideModifications matching the modifications indicated in the library.
        /// </summary>
        public PeptideModifications CreateMatcherPeptideSettings()
        {
            var lightMods = new List<StaticMod>();
            var heavyMods = new Dictionary<IsotopeLabelType, List<StaticMod>>();

            if (HasMatches)
            {
                foreach (var matchPair in Matches)
                {
                    var structuralMod = matchPair.Value.StructuralMod;
                        StaticMod mod1 = structuralMod;
                    if (structuralMod != null && !lightMods.Contains(mod => mod.Equivalent(mod1)))
                    {
                        // Make all found structural mods variable, unless they are preexisting modifications.
                        if (!UserDefinedTypedMods.ContainsKey(structuralMod) || structuralMod.IsUserSet)
                            structuralMod = structuralMod.ChangeVariable(true);
                        // Set modification to be implicit if it appears to be implicit in the library.
                        if (!UserDefinedTypedMods.ContainsKey(structuralMod) && !IsVariableMod(structuralMod))
                            structuralMod = structuralMod.ChangeExplicit(false);
                        lightMods.Add(structuralMod);
                    }
                    var heavyMod = matchPair.Value.HeavyMod;
                    if (heavyMod == null)
                        continue;
                    IsotopeLabelType labelType;
                    if (!UserDefinedTypedMods.TryGetValue(heavyMod, out labelType))
                    {
                        labelType = DocDefHeavyLabelType;
                    }
                    heavyMod = heavyMod.ChangeExplicit(false);
                    if (!heavyMods.ContainsKey(labelType))
                        heavyMods.Add(labelType, new List<StaticMod> { heavyMod });
                    else if (!heavyMods[labelType].Contains(mod => mod.Equivalent(heavyMod)))
                        heavyMods[labelType].Add(heavyMod);
                }
            }
            var typedModifications = new List<TypedModifications>();
            foreach (var labelType in heavyMods.Keys)
            {
                typedModifications.Add(new TypedModifications(labelType, heavyMods[labelType]));
            }
            return new PeptideModifications(lightMods, typedModifications);
        }


        public override bool MoveNextSequence()
        {
            return _libKeys.MoveNext();
        }

        public override IEnumerable<AAModInfo> GetCurrentSequenceInfos()
        {
            var sequence = _libKeys.Current.Key;
            return EnumerateSequenceInfos(sequence, false);
        }

        private IEnumerable<AAModInfo> EnumerateSequenceInfos(byte[] sequence, bool allowDuplicates)
        {
            bool isSpecificHeavy = !AllAminoAcidsModified(sequence);
            byte prevAA = 0;
            int indexAA = -1;
            for (int index = 1; index < sequence.Length; index++)
            {
                byte b = sequence[index];
                if (b != (byte)'[') // L10N
                {
                    if (isSpecificHeavy)
                    {
                        if (index > 1 && prevAA != 0)
                            AddConflictKey(prevAA, null, true);
                        if (index == 2)
                            AddConflictKey(prevAA, ModTerminus.N, true);
                    }
                    prevAA = b;
                    indexAA++;
                }
                else
                {
                    ModTerminus? terminus = null;
                    int startIndex = index + 1;
                    int endIndex = sequence.IndexOf(seqByte => seqByte == (byte)']', startIndex);
                    if (endIndex == -1)
                        endIndex = sequence.Length;
                    if (index == 2)
                        terminus = ModTerminus.N;
                    if (endIndex == sequence.Length - 1)
                        terminus = ModTerminus.C;
                    // Only if prevAA is an amino acid character should AAModInfo be created
                    // Some libraries, for instance, have been created with the sequence starting
                    // with a modification before any amino acid, as an attempt at a n-terminal modification
                    if (AminoAcid.IsExAA((char)prevAA))
                    {
                        List<byte[]> listMasses;
                        if (!_dictAAMassPairs.TryGetValue(new AATermKey(prevAA, terminus), out listMasses))
                        {
                            listMasses = new List<byte[]>();
                            _dictAAMassPairs.Add(new AATermKey(prevAA, terminus), listMasses);
                        }
                        var modArr = new byte[endIndex - startIndex];
                        Array.Copy(sequence, startIndex, modArr, 0, endIndex - startIndex);
                        if (allowDuplicates || !listMasses.Contains(arr => ArrayUtil.EqualsDeep(arr, modArr)))
                        {
                            if (!allowDuplicates)
                                listMasses.Add(modArr);
                            double mass;
                            string massString = Encoding.UTF8.GetString(modArr);
                            if (double.TryParse(massString,
                                                NumberStyles.Float | NumberStyles.AllowThousands,
                                                CultureInfo.InvariantCulture,
                                                out mass))
                            {

                                yield return new AAModInfo
                                    {
                                        IndexAA = indexAA,
                                        ModKey = new AAModKey
                                            {
                                                AA = (char) prevAA,
                                                Terminus = terminus,
                                                AppearsToBeSpecificMod = isSpecificHeavy,
                                                Mass = mass,
                                                RoundedTo = 1
                                            }
                                    };
                            }
                            // NistLibraryBase writes [?] for any modification it does not understand
                            else if (!Equals("?", massString)) // Not L10N
                            {
                                // Get more information on a failure that was posted to the exception web page
                                throw new FormatException(string.Format(Resources.LibKeyModificationMatcher_EnumerateSequenceInfos_The_number___0___is_not_in_the_correct_format_, massString));
                            }
                        }
                    }
                    prevAA = 0;
                    index = endIndex;
                }
            }
            // If the last AA was not modified, we need to update the conflictingMods array.
            if (prevAA != 0)
            {
                AddConflictKey(prevAA, null, true);
                AddConflictKey(null, ModTerminus.C, true);
            }
        }

        /// <summary>
        /// Returns true if all amino acids in a sequence are followed by a modification mass in brackets.
        /// This needs to be as fast as possible to avoid being a profiler identified bottleneck.
        /// </summary>
        private static bool AllAminoAcidsModified(byte[] sequence)
        {
            for (int i = 0; i < sequence.Length; i++)
            {
                // Check each amino acid character to make sure it is followed by an opening bracket
                // The vast majority of sequences will fail this test on the second character
                byte b = sequence[i];
                if (((byte)'A' <= b || b <= (byte)'Z') // L10N
                        && (i == sequence.Length - 1 || sequence[i] != (byte)'[')) // L10N
                    return false;
            }
            return true;
        }

        protected override AAModMatch? GetMatch(AAModKey key)
        {
            var match = base.GetMatch(key);
            if (match == null || match.Value.StructuralMod == null 
                || UserDefinedTypedMods.Keys.Contains(mod => 
                    mod.Equivalent(match.Value.StructuralMod)))
                return match;
            // If the match has a structural modification that should be variable,
            // return a match that indicates that.
            return new AAModMatch
               {
                   // CONSIDER: Should we make all matches variable initially, instead of attempting to guess each time?
                   StructuralMod = match.Value.StructuralMod.ChangeVariable(IsVariableMod(match.Value.StructuralMod)), 
                   HeavyMod = match.Value.HeavyMod
               };
        }

        /// <summary>
        /// Look up the modification in the conflicting mod array.
        /// </summary>
        private bool IsVariableMod(StaticMod mod)
        {
            if (string.IsNullOrEmpty(mod.AAs))
            {
                bool? conflict = IsAAConflict(null, mod.Terminus);
                if (conflict.HasValue)
                    return conflict.Value;
            }
            else
            {
                foreach (char aa in mod.AminoAcids)
                {
                    bool? conflict = IsAAConflict((byte) aa, mod.Terminus);
                    if (conflict.HasValue)
                        return conflict.Value;
                }
            }
            return true;
        }

        public override void UpdateMatcher(AAModInfo info, AAModMatch? match)
        {
            // Update unmatched sequences.
            if (match == null && !UnmatchedSequences.Contains(info.ModKey.ToString()))
                UnmatchedSequences.Add(info.ModKey.ToString());
            if (match == null || match.Value.StructuralMod == null)
                return;

            // Update the conflicting mod array.
            if (!string.IsNullOrEmpty(match.Value.StructuralMod.AAs))
            {
                AddConflictKey((byte) info.AA, info.Terminus, false);
            }
            if (info.Terminus != null)
            {
                AddConflictKey(null, info.Terminus, false);
            }
        }

        private void AddConflictKey(byte? aa, ModTerminus? terminus, bool startingValue)
        {
            int i = GetAAConflictIndex(aa, terminus);
            _aasConflictingMods[i] = startingValue || _aasConflictingMods[i].HasValue;
        }

        private bool? IsAAConflict(byte? aa, ModTerminus? terminus)
        {
            return _aasConflictingMods[GetAAConflictIndex(aa, terminus)];
        }

        private static int GetAAConflictIndex(byte? aa, ModTerminus? terminus)
        {
            int i = 0;
            if (terminus.HasValue)
                i = ((int)terminus + 1)*30;
            return aa.HasValue
                       ? aa.Value - i
                       : i;
        }

        public PeptideDocNode GetModifiedNode(LibKey key, string seqUnmod, SrmSettings settings, SrmSettingsDiff diff)
        {
            if (string.IsNullOrEmpty(seqUnmod))
                return null;

            var peptide = new Peptide(null, seqUnmod, null, null,
                                  settings.PeptideSettings.Enzyme.CountCleavagePoints(seqUnmod));
            // First try and create the match from the settings created to match the library explorer.
            Settings = HasMatches
                ? settings.ChangePeptideModifications(mods => MatcherPepMods)
                : settings;
            TransitionGroupDocNode nodeGroup;
            var nodePep = CreateDocNodeFromSettings(key.Sequence, peptide, diff, out nodeGroup);
            if (nodePep != null)
            {
                if (diff == null)
                {
                    nodePep = (PeptideDocNode)nodePep.ChangeAutoManageChildren(false);
                }
                else
                {
                    // Keep only the matching transition group, so that modifications
                    // will be highlighted differently for light and heavy forms.
                    // Only performed when getting peptides for display in the explorer.
                    nodePep = (PeptideDocNode)nodePep.ChangeChildrenChecked(
                        new DocNode[] { nodeGroup });
                }
                return nodePep;
            }
            else if (Matches == null)
                return null;
            bool hasHeavy;
            // Create explicit mods from the found matches.
            nodePep = CreateDocNodeFromMatches(new PeptideDocNode(peptide),
                                            EnumerateSequenceInfos(key.Key, true), false, out hasHeavy);

            if (nodePep == null)
                return null;

            // Call change settings with the matched modification settings to enumerate the children.
            nodePep = nodePep.ChangeSettings(settings.ChangePeptideModifications(mods =>
                !HasMatches ? settings.PeptideSettings.Modifications : MatcherPepMods), diff ?? SrmSettingsDiff.ALL);
            if (nodePep.Children.Count == 0)
                return null;
            // Select the correct child, only for use with the library explorer.
            if (diff != null && nodePep.Children.Count > 1)
            {
                nodePep =
                    (PeptideDocNode)
                    nodePep.ChangeChildrenChecked(new List<DocNode> { nodePep.Children[hasHeavy ? 1 : 0] });
            }
            if (diff == null)
            {
                nodePep = (PeptideDocNode)nodePep.ChangeAutoManageChildren(false);
            }
            return nodePep;
        }

        protected override bool IsMatch(string seqMod, PeptideDocNode nodePepMod, out TransitionGroupDocNode nodeGroup)
        {
            foreach(TransitionGroupDocNode nodeGroupChild in nodePepMod.Children)
            {
                nodeGroup = nodeGroupChild;
                var calc = Settings.TryGetPrecursorCalc(nodeGroupChild.TransitionGroup.LabelType, nodePepMod.ExplicitMods);
                if (calc == null)
                    return false;
                string modSequence = calc.GetModifiedSequence(nodePepMod.Peptide.Sequence, false);
                // If this sequence matches the sequence of the library peptide, a match has been found.
                if (Equals(seqMod, modSequence))
                    return true;
            }
            nodeGroup = null;
            return false;
        }

        /// <summary>
        /// Merge the modifications found by the matcher with the modifications in the document, 
        /// checking for conflicts with peptides already in the document.
        /// </summary>
        public PeptideModifications SafeMergeImplicitMods(SrmDocument document)
        {
            var newMods = MatcherPepMods;
            var docModifications = document.Settings.PeptideSettings.Modifications;
            var docPeptides = document.Molecules.ToArray();
            List<StaticMod> lightMods = new List<StaticMod>(docModifications.StaticModifications);
            List<StaticMod> heavyMods = new List<StaticMod>(docModifications.HeavyModifications);
            // Merge light mods.
            foreach (var mod in newMods.StaticModifications)
            {
                StaticMod mod1 = mod;
                if (!lightMods.Contains(docMod => docMod.Equivalent(mod1))
                        && (!docPeptides.Any() || !ModAppliesToDoc(mod, true, true, docPeptides)))
                    lightMods.Add(mod1);
            }
            // Merge heavy mods.
            foreach (var mod in newMods.GetModifications(DocDefHeavyLabelType))
            {
                var mod1 = mod;
                if (!heavyMods.Contains(docMod => docMod.Equivalent(mod1))
                        && (!docPeptides.Any() || !ModAppliesToDoc(mod, false, true, docPeptides)))
                    heavyMods.Add(mod1);
            }
            return document.Settings.PeptideSettings.Modifications.ChangeStaticModifications(lightMods).
                ChangeModifications(DocDefHeavyLabelType, heavyMods);
        }

        public override PeptideModifications GetDocModifications(SrmDocument document)
        {
            var modsNew = base.GetDocModifications(document);
            var docPeptides = document.Molecules.ToArray();
            // Remove any new implicit mods that are not used.
            var listLightModsNew = new List<StaticMod>(modsNew.StaticModifications);
            foreach (var mod in modsNew.StaticModifications)
            {
                StaticMod mod1 = mod;
                if (UserDefinedTypedMods.Keys.Contains(userDefMod => userDefMod.Equivalent(mod1)))
                    continue;
                if (!mod.IsUserSet && !ModAppliesToDoc(mod, true, false, docPeptides))
                    listLightModsNew.Remove(mod);
            }
            if (!listLightModsNew.SequenceEqual(modsNew.StaticModifications))
                modsNew = modsNew.ChangeStaticModifications(listLightModsNew);
            var prevHeavyMods = Settings.PeptideSettings.Modifications.GetModifications(DocDefHeavyLabelType);
            var listHeavyModsNew = new List<StaticMod>(modsNew.GetModifications(DocDefHeavyLabelType));
            if (!Equals(prevHeavyMods, modsNew.GetModifications(DocDefHeavyLabelType)))
            {
                foreach (var mod in modsNew.GetModifications(DocDefHeavyLabelType))
                {
                    StaticMod mod1 = mod;
                    if (UserDefinedTypedMods.Keys.Contains(userDefMod => userDefMod.Equivalent(mod1)))
                        continue;
                    if (!mod.IsExplicit && !ModAppliesToDoc(mod, false, false, docPeptides))
                        listHeavyModsNew.Remove(mod);
                }
                modsNew = modsNew.ChangeModifications(DocDefHeavyLabelType, listHeavyModsNew);
            }
            return modsNew;
        }

        /// <summary>
        /// Checks if the given modification would impact the given peptides.
        /// </summary>
        /// <param name="mod">The modification to test</param>
        /// <param name="structural">True if this is a structural modification</param>
        /// <param name="includeUnmodVariable">True if variable modification should be counted</param>
        /// <param name="nodePeps">The list of peptides to test</param>
        /// <returns>True if the presence of this modification would change any of the peptides</returns>
        public static bool ModAppliesToDoc(StaticMod mod, bool structural, bool includeUnmodVariable,
            IEnumerable<PeptideDocNode> nodePeps)
        {
            // Enumerate all peptides where the modification matches the raw sequence.  Obviously,
            // it won't impact a peptide where the modification does not apply to the sequence.
            foreach (var nodePep in nodePeps.Where(nodePep => mod.IsMod(nodePep.Peptide.Sequence)))
            {
                // If the peptide has no explicit modifications
                if (!nodePep.HasExplicitMods)
                {
                    // And the modification is not variable, or variable modifications
                    // are being included, then the modification affects this peptide.
                    if (!mod.IsVariable || includeUnmodVariable)
                        return true;
                }
                else
                {
                    var expMods = nodePep.ExplicitMods;
                    // Otherwise, if this is a structural modification
                    if (structural)
                    {
                        // And there are no explicit structural modifications, or
                        // the structural modifications are variable and the test modification is not
                        // variable or the variable modifications contain this modification.
                        if (expMods.StaticModifications == null
                                || (expMods.IsVariableStaticMods
                                    && (!mod.IsVariable
                                        || expMods.StaticModifications.Contains(expMod => Equals(expMod.Modification, mod)))))
                        {
                            return true;
                        }
                    }
                    // Not structural and no explicit heavy modifications are present
                    else if (!nodePep.ExplicitMods.HasHeavyModifications
                            || nodePep.ExplicitMods.GetHeavyModifications().First() != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void UpdateMatches(PeptideModifications prevPepMods, PeptideModifications newPepMods)
        {
            var labelTypes = newPepMods.GetModificationTypes().ToList();
            labelTypes.AddRange(prevPepMods.GetModificationTypes());
            foreach (var labelType in labelTypes)
            {
                var newMods = newPepMods.GetModifications(labelType);
                var prevMods = prevPepMods.GetModifications(labelType);
                // Add new modifications.
                foreach (var mod in newMods.Where(newMod => !prevMods.Contains(prevMod => prevMod.Equivalent(newMod))))
                {
                    AddModification(mod, labelType);
                }
                // Remove old modifications
                foreach (var mod in prevMods.Where(prevMod => !newMods.Contains(newMod => newMod.Equivalent(prevMod))))
                {
                    RemoveModification(mod, labelType);
                }
            }
        }

        private bool _matchesUpdated;

        public bool MatchesUpdated
        {
            get
            {
                bool result = _matchesUpdated;
                _matchesUpdated = false;
                return result;
            }
            private set { _matchesUpdated = value; }
        }

        public void AddModification(StaticMod mod, IsotopeLabelType labelType)
        {
            if (MatcherPepMods.GetModificationTypes().Contains(labelType))
            {
                var newMods = MatcherPepMods.GetModifications(labelType)
                    .Where(existingMod => !existingMod.Equivalent(mod)).ToList();
                newMods.Add(mod);
                MatcherPepMods = MatcherPepMods.ChangeModifications(labelType, newMods);
            }
            else if (labelType.IsLight)
            {
                MatcherPepMods =
                    new PeptideModifications(new List<StaticMod> { mod }, MatcherPepMods.GetHeavyModifications().ToArray());
            }
            else
            {
                var typedHeavyMods = 
                    new List<TypedModifications>(MatcherPepMods.GetHeavyModifications())
                        { new TypedModifications(labelType, new List<StaticMod> { mod }) };
                MatcherPepMods = new PeptideModifications(MatcherPepMods.StaticModifications, typedHeavyMods);
            }
            MatchesUpdated = true;
        }

        public void RemoveModification(StaticMod mod, IsotopeLabelType labelType)
        {
            if (MatcherPepMods.GetModificationTypes().Contains(labelType))
            {
                var newMods = MatcherPepMods.GetModifications(labelType)
                    .Where(existingMod => !existingMod.Equivalent(mod)).ToArray();
                MatcherPepMods = MatcherPepMods.ChangeModifications(labelType, newMods);
            }
            MatchesUpdated = true;
        }

        /// <summary>
        /// Make all heavy modifications explicit.  The document insert code will force them
        /// back to implicit later, if the match the implicit modifications.
        /// </summary>
        public void ConvertAllHeavyModsExplicit()
        {
            MatcherPepMods = MatcherPepMods.ChangeHeavyModifications(MatcherPepMods.HeavyModifications.Select(
                mod => 
                UserDefinedTypedMods.Keys.Contains(userMod => userMod.Equivalent(mod)) 
                    ? mod
                    : mod.ChangeExplicit(true)).ToArray());

            foreach (var key in Matches.Keys.ToArray())
            {
                var match = Matches[key];
                if (match.HeavyMod != null && !UserDefinedTypedMods.Keys.Contains(match.HeavyMod))
                {
                    Matches[key] = new AAModMatch
                       {
                           StructuralMod = match.StructuralMod,
                           HeavyMod = match.HeavyMod.ChangeExplicit(true)
                       };
                }
            }
        }

        private struct AATermKey
        {
            public AATermKey(byte? aa, ModTerminus? terminus)
            {
                _aa = aa;
                _terminus = terminus;
            }

            private byte? _aa;
            private ModTerminus? _terminus;

            #region object overrides

            private bool Equals(AATermKey other)
            {
                return other._aa.Equals(_aa) &&
                    other._terminus.Equals(_terminus);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (obj.GetType() != typeof (AATermKey)) return false;
                return Equals((AATermKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_aa.HasValue ? _aa.Value.GetHashCode() : 0)*397) ^
                        (_terminus.HasValue ? _terminus.Value.GetHashCode() : 0);
                }
            }

            #endregion
        }
    }
}
