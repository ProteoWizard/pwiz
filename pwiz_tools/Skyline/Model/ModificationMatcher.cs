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
using System.Linq;
using System.Text;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class ModificationMatcher
    {
        private SrmSettings _settings; 

        private Dictionary<StaticMod, IsotopeLabelType> _userDefinedTypedMods;
        private Dictionary<SequenceKey, StaticMod> _dictSeqMatches;
        private List<string> _unmatchedSequences;

        private MappedList<string, StaticMod> _defSetStatic;
        private MappedList<string, StaticMod> _defSetHeavy;

        private IsotopeLabelType _docDefHeavyLabelType;

        private static readonly SequenceMassCalc CALC_DEFAULT = new SequenceMassCalc(MassType.Monoisotopic);

        public static double GetDefaultModMass(char aa, StaticMod mod)
        {
            return CALC_DEFAULT.GetModMass(aa, mod);
        }

        private const int DEFAULT_ROUNDING_DIGITS = 6;

        public void CreateMatches(SrmSettings settings, List<string> sequences,
            MappedList<string, StaticMod> defSetStatic, MappedList<string, StaticMod> defSetHeavy)
        {
            _defSetStatic = defSetStatic;
            _defSetHeavy = defSetHeavy;

            _settings = settings;

            var modifications = settings.PeptideSettings.Modifications;

            _userDefinedTypedMods = new Dictionary<StaticMod, IsotopeLabelType>();
            // First add modifications found in document settings, then add modifications found in the global settings.
            foreach (var type in settings.PeptideSettings.Modifications.GetModificationTypes())
            {
                // Set the default heavy type to the first heavy type encountered.
                if(type != IsotopeLabelType.light && _docDefHeavyLabelType == null)
                    _docDefHeavyLabelType = type;
                InitUserDefTypedModDict(modifications.GetModificationsByName(type.Name), false);
            }
            InitUserDefTypedModDict(new TypedModifications(IsotopeLabelType.light, _defSetStatic), true);
            InitUserDefTypedModDict(new TypedModifications(_docDefHeavyLabelType, _defSetHeavy), true);

            _dictSeqMatches = new Dictionary<SequenceKey, StaticMod>();
            _unmatchedSequences = new List<string>();

            foreach (var seq in sequences)
            {
                var nodePep = CreateDocNodeFromSettings(seq, null);
                if (nodePep != null)
                    continue;
                // Match sequence modifications and update the dictionaries.
                InitExplicitModMatches(seq, true);
                InitExplicitModMatches(seq, false);
            }

            if(_unmatchedSequences.Count > 0)
            {
                _unmatchedSequences.Sort();
                throw new FormatException(String.Format("The following modifications could not be interpreted:\n\n{0}",
                    String.Join("\n", _unmatchedSequences.ToArray())));
            }
        }

        private void InitUserDefTypedModDict(TypedModifications typedModifications, bool addExplicit)
        {
            var type = typedModifications.LabelType;
            foreach (StaticMod mod in typedModifications.Modifications)
            {
                // Make all modifications explicit for later use.
                if ((addExplicit || !mod.IsExplicit) 
                        && !_userDefinedTypedMods.ContainsKey(mod.ChangeExplicit(true)))
                    _userDefinedTypedMods.Add(mod.ChangeExplicit(true), type);
            }
        }

        /// <summary>
        /// Finds explicit mods matching the modifications indicated in the sequence string,
        /// and adds them to either the dictionary of sequence matches or the list of 
        /// unmatched sequences.
        /// </summary> 
        private void InitExplicitModMatches(string seq, bool structural)
        {
            int prevIndexAA = -1;
            int countModsPerAA = 0;
            List<int> badSeqIndices = new List<int>();
            foreach (var key in GetSequenceKeys(seq, structural, false))
            {
                // Keys always start with information specific to the sequence they are found in.
                int indexAA = key.IndexAA ?? -1; //Resharper
                var indexAAinSeq = key.IndexAAInSeq ?? -1;
                // Clear information specific to the sequence before using these keys in the dictionary
                // of sequence matches.
                key.RemoveOrigSeqInfo();
                countModsPerAA = prevIndexAA != indexAA ? 1 : countModsPerAA + 1;
                prevIndexAA = indexAA;
                bool tooManyMods = countModsPerAA > 1;
                if (!tooManyMods && _dictSeqMatches.ContainsKey(key))
                    continue;
                // If the modification isn't indicated by a double, assume it must be the name of the modification.
                StaticMod match = GetMod(key);
                if (match != null && !tooManyMods)
                    _dictSeqMatches.Add(key, match.ChangeExplicit(true));
                else
                {
                    if(badSeqIndices.Contains(indexAAinSeq))
                        continue;

                    if (tooManyMods)
                        badSeqIndices.Add(indexAAinSeq);

                    var unmatchedSeq = GetSeqModStr(FastaSequence.StripModifications(seq.Substring(indexAAinSeq),
                        structural ? FastaSequence.RGX_HEAVY : FastaSequence.RGX_LIGHT));
                    if (!_unmatchedSequences.Contains(unmatchedSeq))
                        _unmatchedSequences.Add(unmatchedSeq);
                }
            }
        }


        private static string GetSeqModStr(string seq)
        {
            var result = new StringBuilder(seq[0].ToString());
            bool parenExpected = true;
            for (int i = 1; i < seq.Length; i++)
            {
                char c = seq[i];
                if (parenExpected && !(c == '[' || c == '{'))
                    return result.ToString();
                parenExpected = c == ']' || c == '}';
                result.Append(c);
            }
            return result.ToString();
        }

        private StaticMod GetMod(SequenceKey key)
        {
            return key.Mass != null ? GetModByMass(key) : GetModByName(key);
        }

        private StaticMod GetModByName(SequenceKey key)
        {
            var structural = key.Structural;
            StaticMod modMatch = null;
            // First, look in the document/global settings.
            foreach (var mod in _userDefinedTypedMods.Keys)
            {
                if (Equals(key.Name, mod.Name)
                        && structural == _userDefinedTypedMods[mod].IsLight)
                    modMatch = mod;
            }
            // If not found, then look in Unimod.
            modMatch = modMatch ?? UniMod.GetModification(key.Name, structural);
            return key.IsModMatch(modMatch) ? modMatch : null;
        }

        private StaticMod GetModByMass(SequenceKey key)
        {
            return GetModByMassInSettings(key) ??
                   UniMod.MassLookup.MatchModificationMass(key.Mass ?? -1,
                                                           key.AA,
                                                           key.RoundedTo,
                                                           key.Structural,
                                                           key.Terminus,
                                                           key.IsSpecificHeavy);
        }

        private StaticMod GetModByMassInSettings(SequenceKey key)
        {
            StaticMod firstMatch = null;
            foreach (var match in GetModMatchesInSettings(key))
            {
                firstMatch = firstMatch ?? match;
                if (key.Structural || (key.IsSpecificHeavy && (!string.IsNullOrEmpty(match.AAs) || match.Terminus != null))
                    || (!key.IsSpecificHeavy && string.IsNullOrEmpty(match.AAs) && match.Terminus == null))
                {
                    firstMatch = match;
                    break;
                }
            }
            return firstMatch;
        }

        private IEnumerable<StaticMod> GetModMatchesInSettings(SequenceKey key)
        {
            var modifications = _settings.PeptideSettings.Modifications;
            var labelTypes = key.Structural
                                ? new[] { IsotopeLabelType.light }
                                : modifications.GetHeavyModificationTypes();
            foreach (var type in labelTypes)
            {
                foreach (var mod in modifications.GetModifications(type))
                {
                    if (key.IsMassMatch(mod))
                    {
                        yield return mod;
                    }
                }
            }
            var listGlobalMods = key.Structural ? _defSetStatic : _defSetHeavy;
            foreach(var mod in listGlobalMods)
            {
                if (key.IsMassMatch(mod))
                    yield return mod;
            }
        }

        private PeptideDocNode CreateDocNodeFromSettings(string seq, Peptide peptide)
        {
            var seqLight = FastaSequence.StripModifications(seq, FastaSequence.RGX_HEAVY);
            var seqHeavy = FastaSequence.StripModifications(seq, FastaSequence.RGX_LIGHT);
            // Create all variations of this peptide matching the settings.
            // If the peptide is not null, use that peptide to create the docnodes, 
            // otherwise just use the sequence.
            IEnumerable<PeptideDocNode> enumNodePep = peptide != null
                ? Peptide.CreateAllDocNodes(_settings, peptide)
                : Peptide.CreateAllDocNodes(_settings, FastaSequence.StripModifications(seq));
            foreach (var nodePep in enumNodePep)
            {
                PeptideDocNode nodePepMod = nodePep.ChangeSettings(_settings, SrmSettingsDiff.ALL, false);
                var calcLight = _settings.GetPrecursorCalc(IsotopeLabelType.light, nodePepMod.ExplicitMods);
                foreach (TransitionGroupDocNode nodeGroup in nodePepMod.Children)
                {
                    if (nodeGroup.TransitionGroup.LabelType.IsLight)
                    {
                        // Light modifications must match.
                        if (!EqualsModifications(seqLight, calcLight, null))
                            break;
                        // If the sequence only has light modifications, a match has been found.
                        if (Equals(seqLight, seq))
                            return (PeptideDocNode)nodePepMod.ChangeChildren(new[] { nodeGroup });
                    }
                    else
                    {
                        var calc = _settings.GetPrecursorCalc(nodeGroup.TransitionGroup.LabelType, nodePepMod.ExplicitMods);
                        if (calc != null && EqualsModifications(seqHeavy, calc, calcLight))
                            return nodePepMod;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Compares the modifications indicated in the sequence string to the calculated masses.
        /// </summary>
        /// <param name="seq">The modified sequence.</param>
        /// <param name="calc">Calculator used to calculate the masses.</param>
        /// <param name="calcLight">
        /// Additional light calculator if necessary to isolate mass changes
        /// caused by heavy modifications alone.
        /// </param>
        /// <returns>
        /// True if the given calculators explain the modifications indicated on the sequence, 
        /// false otherwise.
        /// </returns>
        private bool EqualsModifications(string seq, IPrecursorMassCalc calc, IPrecursorMassCalc calcLight)
        {
            bool structural = calcLight == null;
            string aas = FastaSequence.StripModifications(seq);
            foreach (var key in GetSequenceKeys(seq, structural, true))
            {
                int indexAA = key.IndexAA ?? 0; // ReSharper
                var aa = aas[indexAA];
                var roundedTo = key.RoundedTo;
                // If the user has indicated the modification by name, find that modification 
                // and calculate the mass.
                double massKey;
                if (key.Mass != null)
                    massKey = (double) key.Mass;
                else
                {
                    StaticMod modMatch = GetModByName(key);
                    if (modMatch == null)
                        return false;
                    roundedTo = DEFAULT_ROUNDING_DIGITS;
                    massKey = Math.Round(GetDefaultModMass(aa, modMatch), roundedTo);
                }
                double massMod = Math.Round(calc.GetAAModMass(aas[indexAA], indexAA, aas.Length), roundedTo);
                // Subtract the mass difference of the light
                // modifications to isolate the masses of the heavy modifications.
                if (calcLight != null)
                    massMod -= Math.Round(calcLight.GetAAModMass(aas[indexAA], indexAA, aas.Length), roundedTo);
                if (!Equals(massKey, massMod))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Enumerates the SequenceKeys found by parsing a modified sequence.
        /// </summary>
        private static IEnumerable<SequenceKey> GetSequenceKeys(string seq, bool structural, bool includeUnmod)
        {
            seq = FastaSequence.StripModifications(seq, structural ? FastaSequence.RGX_HEAVY : FastaSequence.RGX_LIGHT);
            if (!structural)
                seq = seq.Replace('{', '[').Replace('}', ']');
            string aas = FastaSequence.StripModifications(seq);
            bool isSpecificHeavy = !structural && aas.Length > seq.Count(c => c == '[');
            int indexAA = 0;
            int indexAAInSeq = 0;
            int i = 0;
            while(i < seq.Length)
            {
                var aa = aas[indexAA];
                int indexBracket = i + 1;
                if (indexBracket < seq.Length && seq[indexBracket] == '[')
                {
                    int indexStart = indexBracket + 1;
                    int indexClose = seq.IndexOf(']', indexBracket);
                    string mod = seq.Substring(indexStart, indexClose - indexStart);
                    i = indexClose;
                    ModTerminus? modTerminus = null;
                    if (indexAA == 0)
                        modTerminus = ModTerminus.N;
                    if (indexAA == aas.Length - 1)
                        modTerminus = ModTerminus.C;
                    int decPlace = mod.IndexOf('.');
                    string name = null;
                    var roundedTo = Math.Min(decPlace == -1 ? 0 : mod.Length - decPlace - 1, DEFAULT_ROUNDING_DIGITS);
                    double? mass = null;
                    double result;
                    if(double.TryParse(mod, out result))
                        mass = Math.Round(result, roundedTo);
                    else
                        name = mod;
                    yield return new SequenceKey
                    {
                        Name = name,
                        Mass = mass,
                        AA = aa,
                        IndexAA = indexAA,
                        IndexAAInSeq = indexAAInSeq,
                        Terminus = modTerminus,
                        Structural = structural,
                        RoundedTo = roundedTo,
                        IsSpecificHeavy = isSpecificHeavy
                    };
                }
                else if (includeUnmod)
                {
                    // If need unmodified amino acids (as when 
                    // checking for equality), yield SequenceKeys for these AA's.
                    yield return new SequenceKey
                    {
                        AA = aa,
                        IndexAA = indexAA,
                        Structural = structural,
                        Mass = 0
                    };
                }
                // If the next character is a bracket, continue using the same amino
                // acid and leave i where it is.
                int iNext = i + 1;
                if (iNext >= seq.Length || seq[iNext] != '[')
                {
                    i = indexAAInSeq = iNext;
                    indexAA++;
                }
            }
        }

        public PeptideDocNode GetModifiedNode(string seq)
        {
            return GetModifiedNode(seq, null);
        }
         
        public PeptideDocNode GetModifiedNode(string seq, FastaSequence fastaSequence)
        {
            var seqNoMod = FastaSequence.StripModifications(seq);
            var peptide = fastaSequence != null
              ? fastaSequence.CreateFullPeptideDocNode(_settings, seq).Peptide
              : new Peptide(null, seqNoMod, null, null,
                            _settings.PeptideSettings.Enzyme.CountCleavagePoints(seq));
            // First, try to create the peptide using the current settings.
            var nodePep = CreateDocNodeFromSettings(seq, peptide);
            if(nodePep != null)
                return nodePep;
            // Create the peptideDocNode.
            nodePep = fastaSequence == null
              ? new PeptideDocNode(peptide, new TransitionGroupDocNode[0])
              : fastaSequence.CreateFullPeptideDocNode(_settings, seqNoMod);
            // Enumerate the necessary explicit modifications.
            List<ExplicitMod> listLightMods = new List<ExplicitMod>();
            foreach(var key in GetSequenceKeys(seq, true, false))
            {
                int indexAA = key.IndexAA ?? 0; // ReSharper
                key.RemoveOrigSeqInfo();
                listLightMods.Add(new ExplicitMod(indexAA, _dictSeqMatches[key]));                
            }
            var dictHeavyMods = new Dictionary<IsotopeLabelType, List<ExplicitMod>>();
            foreach(var key in GetSequenceKeys(seq, false, false))
            {
                int indexAA = key.IndexAA ?? 0; // ReSharper
                key.RemoveOrigSeqInfo();
                var mod = _dictSeqMatches[key];

                IsotopeLabelType type;
                if (!_userDefinedTypedMods.TryGetValue(mod, out type))
                    type = (key.Structural ? IsotopeLabelType.light : _docDefHeavyLabelType);

                List<ExplicitMod> listHeavyMods;
                if (!dictHeavyMods.TryGetValue(type, out listHeavyMods))
                {
                    listHeavyMods = new List<ExplicitMod>();
                    dictHeavyMods.Add(type, listHeavyMods);
                }
                listHeavyMods.Add(new ExplicitMod(indexAA, mod));
            }
            // Ensure mods.
            var targetImplicitMods = new ExplicitMods(nodePep,
                _settings.PeptideSettings.Modifications.StaticModifications,
                _defSetStatic,
                _settings.PeptideSettings.Modifications.GetHeavyModifications(),
                _defSetHeavy);
            // If no light modifications are present, this code assumes the user wants the 
            // default global light modifications.
            if(listLightMods.Count == 0 || 
                    ArrayUtil.EqualsDeep(listLightMods.ToArray(), targetImplicitMods.StaticModifications))
                listLightMods = null;
            var listTypedHeavyMods = new List<TypedExplicitModifications>();
            foreach (var targetDocMod in targetImplicitMods.GetHeavyModifications())
            {
                List<ExplicitMod> listMods;
                if(dictHeavyMods.TryGetValue(targetDocMod.LabelType, out listMods) 
                        && !ArrayUtil.EqualsDeep(listMods, targetDocMod.Modifications))
                    listTypedHeavyMods.Add(new TypedExplicitModifications(nodePep.Peptide, targetDocMod.LabelType, listMods));
            }
            // Put the explicit modifications on the peptide.
            ExplicitMods mods = (listLightMods != null || listTypedHeavyMods.Count > 0) 
                ? new ExplicitMods(nodePep.Peptide, listLightMods, listTypedHeavyMods)
                : null;
            return nodePep.ChangeExplicitMods(mods);
        }

        public string FoundMatches
        {
            get
            {
                var dictModKeys = new Dictionary<string, List<string>>();
                foreach (var seqKeyToMod in _dictSeqMatches)
                {
                    // Skip existing modifications.
                    var mod = seqKeyToMod.Value;
                    if (_userDefinedTypedMods.ContainsKey(seqKeyToMod.Value))
                        continue;

                    var key = seqKeyToMod.Key;
                    List<string> keyStrings;
                    if (!dictModKeys.TryGetValue(mod.Name, out keyStrings))
                    {
                        keyStrings = new List<string>();
                        dictModKeys.Add(mod.Name, keyStrings);
                    }
                    if (key.Mass != null)
                    {
                        var keyStr = key.ToString();
                        if (!keyStrings.Contains(keyStr))
                            keyStrings.Add(keyStr);
                    }
                }
                StringBuilder sb = new StringBuilder();
                foreach (var modKeysPair in dictModKeys)
                {
                    // Sort by amino acid.
                    var modName = modKeysPair.Key;
                    var keyStrings = modKeysPair.Value;
                    keyStrings.Sort();
                    sb.AppendLine(keyStrings.Count > 0
                                      ? string.Format("{0} = {1}", modName, string.Join(" ", keyStrings.ToArray()))
                                      : modName);
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Creates new peptide modifications settings based on the modifications
        /// found by matching.
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public PeptideModifications GetDocModifications(SrmDocument doc)
        {
            var lightMods = new MappedList<string, StaticMod>();
            lightMods.AddRange(_defSetStatic);
            var heavyMods = new MappedList<string, StaticMod>();
            heavyMods.AddRange(_defSetHeavy);
            foreach(var matchPair in _dictSeqMatches)
            {
                var labelType = matchPair.Key.Structural ? IsotopeLabelType.light : _docDefHeavyLabelType;
                var mod = matchPair.Value;
                if(labelType == IsotopeLabelType.light && !lightMods.Contains(mod))
                    lightMods.Add(mod.ChangeExplicit(true));
                if(labelType != IsotopeLabelType.light && !heavyMods.Contains(mod))
                    heavyMods.Add(mod.ChangeExplicit(true));
            }
            return doc.Settings.PeptideSettings.Modifications.DeclareExplicitMods(doc,
              lightMods, heavyMods);
        }

        private struct SequenceKey
        {
            private int? _indexAA; 
            private int? _indexSeq;
            public char AA { get; set; }
            public int? IndexAA
            {
                get { return _indexAA; }
                set { _indexAA = value; }
            }
            public int? IndexAAInSeq
            {
                get { return _indexSeq; }
                set { _indexSeq = value; }
            }
            public ModTerminus? Terminus { get; set; }
            public bool Structural { get; set; }
            public double? Mass { get; set; }
            public string Name { get; set; }
            public int RoundedTo { get; set; }
            public bool IsSpecificHeavy { get; set; }
            public void RemoveOrigSeqInfo()
            {
                _indexAA = null;
                _indexSeq = null;
            }
            public bool IsMassMatch(StaticMod mod)
            {
                return Equals(Math.Round(GetDefaultModMass(AA, mod), RoundedTo), Mass)
                    && IsModMatch(mod);
            }

            public bool IsModMatch(StaticMod mod)
            {
                return mod != null
                    && (string.IsNullOrEmpty(mod.AAs) || mod.AminoAcids.ContainsAA(AA.ToString()))
                    && ((mod.Terminus == null) || Equals(mod.Terminus, Terminus));
            }

            public override string ToString()
            {
                return string.Format(Structural ? "{0}[{1}]" : "{0}{{{1}}}", AA, Mass);
            }
        }
    }
}
