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
        public static ModMassLookup MassLookup { get; private set; }

        public static readonly char[] AMINO_ACIDS = 
            {
                'A', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'K', 'L', 'M', 'N', 
                'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'Y'
            };
        private static readonly bool INITIALIZING;

        static UniMod()
        {
            DictStructuralModNames = new Dictionary<string, StaticMod>();
            DictHiddenStructuralModNames = new Dictionary<string, StaticMod>();
            DictIsotopeModNames = new Dictionary<string, StaticMod>();
            DictHiddenIsotopeModNames = new Dictionary<string, StaticMod>();
            DictUniModIds = new Dictionary<UniModIdKey, StaticMod>();
            MassLookup = new ModMassLookup();

            INITIALIZING = true;
            
            // ADD MODS.

            // Hardcoded Skyline Mods
            AddMod("Label:15N", null, null, LabelAtoms.N15, null, null, null, false, false);
            AddMod("Label:13C", null, null, LabelAtoms.C13, null, null, null, false, false);
            AddMod("Label:13C15N", null, null, LabelAtoms.N15 | LabelAtoms.C13, null, null, null, false, false);
            AddMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, LabelAtoms.C13 | LabelAtoms.N15, null, null, null, false, false);
            AddMod("Label:13C(6)15N(4) (C-term R)", "R", ModTerminus.C, LabelAtoms.C13 | LabelAtoms.N15, null, null, null, false, false);
            AddMod("Label:13C(6) (C-term K)", "K", ModTerminus.C, LabelAtoms.C13, null, null, null, false, false);
            AddMod("Label:13C(6) (C-term R)", "R", ModTerminus.C, LabelAtoms.C13, null, null, null, false, false);

            MassLookup.Complete();

            INITIALIZING = false;
        }
        
        private static void AddMod(string name, string aas, ModTerminus? terminus, LabelAtoms labelAtoms, string formula, FragmentLoss[] losses,
            int? id, bool structural, bool hidden)
        {
            var newMod =
                new StaticMod(name, aas, terminus, false, formula, labelAtoms, RelativeRT.Matching, null, null, losses, id);
            AddMod(newMod, id, structural, hidden);
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
        public static StaticMod GetModification(string modName, bool structural)
        {
            StaticMod mod;
            var dict = structural ? DictStructuralModNames : DictIsotopeModNames;
            var hiddenDict = structural ? DictHiddenStructuralModNames : DictHiddenIsotopeModNames;
            dict.TryGetValue(modName, out mod);
            if (mod == null)
                hiddenDict.TryGetValue(modName, out mod);
            return mod;
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
            if (INITIALIZING || mod.UnimodId == null)
                return true;
            var idKey = new UniModIdKey
            {
                Aa = mod.AAs == null ? 'A' : mod.AminoAcids.First(),
                AllAas = mod.AAs == null,
                Id = (int)mod.UnimodId,
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


    public class ModMassLookup
    {
        private static readonly SequenceMassCalc CALC = new SequenceMassCalc(MassType.Monoisotopic);
        private readonly AAMassLookup[] _aaMassLookups;
        private bool _completed;

        public ModMassLookup()
        {
            _aaMassLookups = new AAMassLookup[128];
            foreach (char aa in UniMod.AMINO_ACIDS)
            {
                _aaMassLookups[aa] = new AAMassLookup();
                _aaMassLookups[Char.ToLower(aa)] = new AAMassLookup();
            }
        }

        public void Add(char aa, StaticMod mod, bool structural, bool allowDuplicates)
        {
            if (_completed)
                throw new InvalidOperationException("Invalid attempt to add data to completed MassLookup.");
            // If structural, store in lowercase AA.
            _aaMassLookups[structural ? Char.ToLower(aa) : Char.ToUpper(aa)]
                .Add(CALC.GetModMass(aa, mod), mod, allowDuplicates);
        }

        public StaticMod MatchModificationMass(double mass, char aa, int roundTo, bool structural,
            ModTerminus? terminus, bool specific)
        {
            if (!_completed)
                throw new InvalidOperationException("Invalid attempt to access incomplete MassLookup.");
            return _aaMassLookups[structural ? Char.ToLower(aa) : Char.ToUpper(aa)]
                .ClosestMatch(mass, roundTo, terminus, specific);
        }

        public void Complete()
        {
            foreach (char aa in UniMod.AMINO_ACIDS)
            {
                _aaMassLookups[aa].Sort();
                _aaMassLookups[Char.ToLower(aa)].Sort();
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
            StaticMod match = null;
            if (terminus != null)
            {
                match = ClosestMatch(terminus == ModTerminus.C ? _listCTerminalMasses : _listNTerminalMasses, mass,
                                     roundTo);
            }
            return match
                ?? ClosestMatch(_listMasses, mass, roundTo)
                ?? ClosestMatch(_listAllAAsMasses, mass, roundTo);
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
