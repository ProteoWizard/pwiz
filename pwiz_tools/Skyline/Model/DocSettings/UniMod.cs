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
using System.IO;
using System.Linq;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    public static class UniMod
    {
        public static Dictionary<string, StaticMod> DictStructuralModNames { get; private set; }
        public static Dictionary<string, StaticMod> DictHiddenStructuralModNames { get; private set; }
        public static Dictionary<string, StaticMod> DictIsotopeModNames { get; private set; }
        public static Dictionary<string, StaticMod> DictHiddenIsotopeModNames { get; private set; }
        public static Dictionary<UniModIdKey, StaticMod> DictUniModIds { get; private set; }
        public static HashSet<int> SetUniModIds { get; private set; }
        public static Dictionary<string, int> DictShortNamesToUniMod { get; private set; } 
        public static ModMassLookup MassLookup { get; private set; }

        public static readonly char[] AMINO_ACIDS = 
            {
                'A', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'K', 'L', 'M', 'N', 
                'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'Y' // Not L10N
            };
        private static readonly bool INITIALIZING;

        static UniMod()
        {
            DictStructuralModNames = new Dictionary<string, StaticMod>();
            DictHiddenStructuralModNames = new Dictionary<string, StaticMod>();
            DictIsotopeModNames = new Dictionary<string, StaticMod>();
            DictHiddenIsotopeModNames = new Dictionary<string, StaticMod>();
            DictUniModIds = new Dictionary<UniModIdKey, StaticMod>();
            SetUniModIds = new HashSet<int>();
            DictShortNamesToUniMod = new Dictionary<string, int>();
            MassLookup = new ModMassLookup();

            INITIALIZING = true;
            
            foreach(var data in UniModData.UNI_MOD_DATA)
            {
                AddMod(data);
            }

            MassLookup.Complete();

            INITIALIZING = false;
        }
        
        private static void AddMod(UniModModificationData data)
        {
            var newMod = new StaticMod(data.Name, data.AAs, data.Terminus, false, data.Formula, data.LabelAtoms,
                                       RelativeRT.Matching, null, null, data.Losses, data.ID,
                                       data.ShortName);
            if (data.ID.HasValue && data.ShortName != null)
            {
                int id;
                string shortName = data.ShortName.ToLower();
                if (!DictShortNamesToUniMod.TryGetValue(shortName, out id))
                {
                    DictShortNamesToUniMod.Add(shortName, data.ID.Value);    
                }
                // Short mods and unimod ID's need to match up
                // This error should never be seen by users
                else if (id != data.ID.Value)
                {
                    throw new InvalidDataException("Short mod names and unimod ID's must be consistent"); // Not L10N
                }
            }
            AddMod(newMod, data.ID, data.Structural, data.Hidden);
        }

        private static void AddMod(StaticMod mod, int? id, bool structural, bool hidden)
        {
            // Add to dictionary by name.
            Dictionary<string, StaticMod> dictNames;
            if (structural)
                dictNames = hidden ? DictHiddenStructuralModNames : DictStructuralModNames;
            else
                dictNames = hidden ? DictHiddenIsotopeModNames : DictIsotopeModNames;
            dictNames.Add(mod.Name, mod);
            
            bool allAas = mod.AAs == null;
            IEnumerable<char> aas = allAas ? AMINO_ACIDS : mod.AminoAcids;
            foreach(char aa in aas)
            {
                // Add to mass lookup.
                MassLookup.Add(aa, mod, structural, true);
                
                // Add to dictionary by ID.
                if (id == null)
                    continue;
                SetUniModIds.Add(id.Value);
                var idKey = new UniModIdKey
                {
                    Id = (int) id,
                    Aa = aa,
                    AllAas = allAas,
                    Terminus = mod.Terminus
                };
                if (!DictUniModIds.ContainsKey(idKey))
                    DictUniModIds.Add(idKey, mod);
            }
        }

        /// <summary>
        /// Searches for A UniMod modification by name.
        /// </summary>
        public static StaticMod GetModification(string modName, out bool structural)
        {
            structural = true;
            var mod = GetModification(modName, true);
            if(mod != null)
                return mod;
            structural = false;
            return GetModification(modName, false);
        }

        public static StaticMod GetModification(string modName, bool structural)
        {
            if (Equals(modName, StaticModList.LEGACY_DEFAULT_NAME))
                modName = StaticModList.DEFAULT_NAME;
            StaticMod mod;
            var dict = structural ? DictStructuralModNames : DictIsotopeModNames;
            var hiddenDict = structural ? DictHiddenStructuralModNames : DictHiddenIsotopeModNames;
            dict.TryGetValue(modName, out mod);
            if (mod == null)
                hiddenDict.TryGetValue(modName, out mod);
            return mod;
        }

        public static bool IsStructuralModification(string modName)
        {
            return DictStructuralModNames.ContainsKey(modName) || DictHiddenStructuralModNames.ContainsKey(modName);
        }

        public static bool IsValidUnimodId(int id)
        {
            return SetUniModIds.Contains(id);
        }

        /// <summary>
        /// Searches for a UniMod modification by modification definition.
        /// </summary>
        public static StaticMod FindMatchingStaticMod(StaticMod modToMatch, bool structural)
        {       
            var dict = structural ? DictStructuralModNames : DictIsotopeModNames;
            var hiddenDict = structural ? DictHiddenStructuralModNames : DictHiddenIsotopeModNames;
            foreach (StaticMod mod in dict.Values)
            {
                if (mod.Equivalent(modToMatch))
                    return mod;
            }
            foreach (StaticMod mod in hiddenDict.Values)
            {
                if (mod.Equivalent(modToMatch))
                    return mod;
            }
            return null;
        }

        public static bool ValidateID(StaticMod mod)
        {
            if (INITIALIZING || !mod.UnimodId.HasValue)
                return true;
            var idKey = new UniModIdKey
            {
                Aa = mod.AAs == null ? 'A' : mod.AminoAcids.First(), // Not L10N
                AllAas = mod.AAs == null,
                Id = mod.UnimodId.Value,
                Terminus = mod.Terminus
            };
            StaticMod unimod;
            return DictUniModIds.TryGetValue(idKey, out unimod) 
                && Equals(mod.Name, unimod.Name) 
                && mod.Equivalent(unimod);
        }

        public struct UniModIdKey
        {
            public int Id { get; set; }
            public char Aa { get; set; }
            public bool AllAas { get; set; }
            public ModTerminus? Terminus { get; set; }
        }
    }

    public struct UniModModificationData
    {
        public string Name { get; set; }
        public string AAs { get; set; }
        public ModTerminus? Terminus { get; set; }
        public LabelAtoms LabelAtoms { get; set; }
        public string Formula { get; set; }
        public FragmentLoss[] Losses { get; set; }
        public int? ID { get; set; }
        public bool Structural { get; set; }
        public bool Hidden { get; set; }
        public string ShortName { get; set; }
    }

    public class ModMassLookup
    {
        private static readonly SequenceMassCalc CALC = new SequenceMassCalc(MassType.Monoisotopic);
        private readonly AAMassLookup[] _aaMassLookups;
        private bool _completed;

        private int ToStructuralIndex(char aa)
        {
            char c = char.ToLowerInvariant(aa);
            // Check range, because we used to use Char.ToLower(), which had problems with Turkish I
            if ('a' > c || c > 'z')
                throw new ArgumentOutOfRangeException(string.Format("Error converting {0} to {1}.", aa, c));    // Not L10N
            return c;
        }

        private int ToIsotopeIndex(char aa)
        {
            char c = char.ToUpperInvariant(aa);
            // Check range, because we used to use Char.ToLower(), which had problems with Turkish i
            if ('A' > c || c > 'Z')
                throw new ArgumentOutOfRangeException(string.Format("Error converting {0} to {1}.", aa, c));    // Not L10N
            return c;
        }

        public ModMassLookup()
        {
            _aaMassLookups = new AAMassLookup[128];
            foreach (char aa in UniMod.AMINO_ACIDS)
            {
                _aaMassLookups[aa] = new AAMassLookup();
                _aaMassLookups[Char.ToLowerInvariant(aa)] = new AAMassLookup();
            }
        }

        public void Add(char aa, StaticMod mod, bool structural, bool allowDuplicates)
        {
            if (_completed)
                throw new InvalidOperationException(Resources.ModMassLookup_Add_Invalid_attempt_to_add_data_to_completed_MassLookup);
            // If structural, store in lowercase AA.
            _aaMassLookups[structural ? ToStructuralIndex(aa) : ToIsotopeIndex(aa)]
                .Add(CALC.GetModMass(aa, mod), mod, allowDuplicates);
        }

        public StaticMod MatchModificationMass(double mass, char aa, int roundTo, bool structural,
            ModTerminus? terminus, bool specific)
        {
            if (!_completed)
                throw new InvalidOperationException(Resources.ModMassLookup_MatchModificationMass_Invalid_attempt_to_access_incomplete_MassLookup);
            var massLookup = _aaMassLookups[structural ? ToStructuralIndex(aa) : ToIsotopeIndex(aa)];
            return massLookup != null ? massLookup.ClosestMatch(mass, roundTo, terminus, specific) : null;
        }

        public void Complete()
        {
            foreach (char aa in UniMod.AMINO_ACIDS)
            {
                _aaMassLookups[ToStructuralIndex(aa)].Sort();
                _aaMassLookups[ToIsotopeIndex(aa)].Sort();
            }
            _completed = true;
        }
    }

    public class AAMassLookup
    {
        private readonly List<KeyValuePair<double, StaticMod>> _listMasses =
            new List<KeyValuePair<double, StaticMod>>();
        private readonly List<KeyValuePair<double, StaticMod>> _listCTerminalMasses =
            new List<KeyValuePair<double, StaticMod>>();
        private readonly List<KeyValuePair<double, StaticMod>> _listNTerminalMasses =
            new List<KeyValuePair<double, StaticMod>>();
        private readonly List<KeyValuePair<double, StaticMod>> _listAllAAsMasses =
            new List<KeyValuePair<double, StaticMod>>();

        private static readonly IComparer<KeyValuePair<double, StaticMod>> MASS_COMPARER = new MassComparer();

        public void Add(double mass, StaticMod mod, bool allowDuplicates)
        {
            var list = _listMasses;
            switch (mod.Terminus)
            {
                case null:
                    if (string.IsNullOrEmpty(mod.AAs))
                        list = _listAllAAsMasses;
                    break;
                case ModTerminus.C:
                    list = _listCTerminalMasses;
                    break;
                case ModTerminus.N:
                    list = _listNTerminalMasses;
                    break;
            }
            if (allowDuplicates || !list.Contains(pair => pair.Key == mass))
                list.Add(new KeyValuePair<double, StaticMod>(mass, mod));

        }

        public void Sort()
        {
            _listMasses.Sort(MASS_COMPARER);
            _listCTerminalMasses.Sort(MASS_COMPARER);
            _listNTerminalMasses.Sort(MASS_COMPARER);
            _listAllAAsMasses.Sort(MASS_COMPARER);
        }

        public StaticMod ClosestMatch(double mass, int roundTo, ModTerminus? terminus, bool specific)
        {
            return specific
               ? ClosestMatchSpecific(mass, roundTo, terminus)
               : ClosestMatchGeneral(mass, roundTo, terminus);
        }

        public StaticMod ClosestMatchSpecific(double mass, int roundTo, ModTerminus? terminus)
        {
            // Order of preference: matches that specific amino acids
            StaticMod match = ClosestMatch(_listMasses, mass, roundTo);
            // Terminal matches
            if (match == null && terminus != null)
            {
                match = ClosestMatch(terminus == ModTerminus.C ? _listCTerminalMasses : _listNTerminalMasses, mass,
                                     roundTo);
            }
            // Matches that apply to all amino acids
            return match ?? ClosestMatch(_listAllAAsMasses, mass, roundTo);
        }

        public StaticMod ClosestMatchGeneral(double mass, int roundTo, ModTerminus? terminus)
        {
            StaticMod match = ClosestMatch(_listAllAAsMasses, mass, roundTo);
            if (match == null && terminus != null)
            {
                match = ClosestMatch(terminus == ModTerminus.C ? _listCTerminalMasses : _listNTerminalMasses, mass,
                                     roundTo);
            }
            return match ?? ClosestMatch(_listMasses, mass, roundTo);
        }

        private static StaticMod ClosestMatch(List<KeyValuePair<double, StaticMod>> listSearch, double mass, int roundTo)
        {
            if (listSearch.Count == 0)
                return null;
            int i = listSearch.BinarySearch(new KeyValuePair<double, StaticMod>(mass, null), MASS_COMPARER);
            i = i < 0 ? ~i : i;
            var match = listSearch[i == listSearch.Count ? i - 1 : i];
            if (Math.Round(match.Key, roundTo) == mass)
                return match.Value;
            if (i > 0)
            {
                match = listSearch[i - 1];
                if (Math.Round(match.Key, roundTo) == mass)
                    return match.Value;
            }
            return null;
        }

        internal class MassComparer : IComparer<KeyValuePair<double, StaticMod>>
        {
            public int Compare(KeyValuePair<double, StaticMod> s1, KeyValuePair<double, StaticMod> s2)
            {
                return Comparer<double>.Default.Compare(s1.Key, s2.Key);
            }
        }
    }
}
//Impossible Modification: Label:13C(8)15N(2) (R)
//Unable to match: Acetyl (Protein N-term)
//Unable to match: Amidated (Protein C-term)
//Unable to match: Formyl (Protein N-term)
//Unable to match: ICPL (Protein N-term)
//Unable to match: ICPL:13C(6) (Protein N-term)
//Unable to match: ICPL:13C(6)2H(4) (Protein N-term)
//Unable to match: ICPL:2H(4) (Protein N-term)
//Unable to match: AEBS (Protein N-term)
//Unable to match: BDMAPP (Protein N-term)
//Unable to match: Biotin-PEO-Amine (Protein C-term)
//Unable to match: CAMthiopropanoyl (Protein N-term)
//Unable to match: Cholesterol (Protein C-term)
//Unable to match: Delta:H(2)C(2) (Protein N-term)
//Unable to match: Delta:H(4)C(3) (Protein N-term)
//Unable to match: Dimethyl:2H(4) (Protein N-term)
//Unable to match: DTBP (Protein N-term)
//Unable to match: Ethyl (Protein N-term)
//Unable to match: FormylMet (Protein N-term)
//Unable to match: Glu (Protein C-term)
//Unable to match: Glucuronyl (Protein N-term)
//Unable to match: GluGlu (Protein C-term)
//Unable to match: GluGluGlu (Protein C-term)
//Unable to match: GluGluGluGlu (Protein C-term)
//Unable to match: GPIanchor (Protein C-term)
//Unable to match: LG-Hlactam-K (Protein N-term)
//Unable to match: LG-lactam-K (Protein N-term)
//Unable to match: Methyl (Protein N-term)
//Unable to match: Microcin (Protein C-term)
//Unable to match: MicrocinC7 (Protein C-term)
//Unable to match: Palmitoyl (Protein N-term)
//Unable to match: Succinyl (Protein N-term)
//Unable to match: Xlink:DMP (Protein N-term)
//Unable to match: Xlink:DMP-s (Protein N-term)
