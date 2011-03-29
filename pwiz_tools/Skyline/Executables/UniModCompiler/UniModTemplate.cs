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
using System.Windows.Forms;
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
        public static Dictionary<ModMassKey, StaticMod> DictModMasses { get; private set; }
        public static Dictionary<ModMassKey, StaticMod> UserDefModMosses { get; private set; }

        private static readonly SequenceMassCalc CALC = new SequenceMassCalc(MassType.Monoisotopic);
        private static readonly char[] AMINO_ACIDS = 
            {
                'A', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'K', 'L', 'M', 'N', 
                'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'Y'
            };

        public static void Init()
        {
            DictStructuralModNames = new Dictionary<string, StaticMod>();
            DictHiddenStructuralModNames = new Dictionary<string, StaticMod>();
            DictIsotopeModNames = new Dictionary<string, StaticMod>();
            DictHiddenIsotopeModNames = new Dictionary<string, StaticMod>();
            DictUniModIds = new Dictionary<UniModIdKey, StaticMod>();
            DictModMasses = new Dictionary<ModMassKey, StaticMod>();
            UserDefModMosses = new Dictionary<ModMassKey, StaticMod>();

            // ADD MODS.

            // Hardcoded Skyline Mods
            AddMod("Label:15N", null, null, LabelAtoms.N15, null, null, -1, false, false);
            AddMod("Label:13C", null, null, LabelAtoms.C13, null, null, -1, false, false);
            AddMod("Label:13C15N", null, null, LabelAtoms.N15 | LabelAtoms.C13, null, null, -1, false, false);
            AddMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, LabelAtoms.C13 | LabelAtoms.N15, null, null, -1, false, false);
            AddMod("Label:13C(6)15N(4) (C-term R)", "R", ModTerminus.C, LabelAtoms.C13 | LabelAtoms.N15, null, null, -1, false, false);
            AddMod("Label:13C(6) (C-term KR)", "K, R", ModTerminus.C, LabelAtoms.C13, null, null, -1, false, false);

            SetUserDefinedMods();

        }
        
        private static void AddMod(string name, string aas, ModTerminus? terminus, LabelAtoms labelAtoms, string formula, FragmentLoss[] losses,
            int id, bool structural, bool hidden)
        {
            var newMod =
                new StaticMod(name, aas, terminus, false, formula, labelAtoms, RelativeRT.Matching, null, null, losses, id);
            AddMod(newMod, id, structural, hidden);
        }

        private static void AddMod(StaticMod mod, int id, bool structural, bool hidden)
        {
            // Add to dictionary by name.
            Dictionary<string, StaticMod> dictNames;
            if (structural)
                dictNames = hidden ? DictHiddenStructuralModNames : DictStructuralModNames;
            else
                dictNames = hidden ? DictHiddenIsotopeModNames : DictIsotopeModNames;
            dictNames.Add(mod.Name, mod);
            
            IEnumerable<char> aas = mod.AAs == null ? AMINO_ACIDS : mod.AminoAcids;
            foreach(char aa in aas)
            {
                // Add to dictionary by mass.
                var massKey = new ModMassKey
                     {
                          Aa = aa, 
                          Mass = Math.Round(CALC.GetModMass(aa, mod), 1),
                          Structural = structural,
                          Terminus = mod.Terminus
                     };
                StaticMod value;
                if (!DictModMasses.TryGetValue(massKey, out value))
                    DictModMasses.Add(massKey, mod);
                
                // Add to dictionary by ID.
                if (id == -1)
                    return;
                var idKey = new UniModIdKey
                {
                    Id = id,
                    Aa = aa,
                    Structural = structural,
                    Terminus = mod.Terminus
                };
                if (!DictUniModIds.ContainsKey(idKey))
                    DictUniModIds.Add(idKey, mod);
            }
        }

        private static void SetUserDefinedMods()
        {
            UserDefModMosses = new Dictionary<ModMassKey, StaticMod>();
            SetUserDefinedMods(Settings.Default.StaticModList, true);
            SetUserDefinedMods(Settings.Default.HeavyModList, false);
        }

        private static void SetUserDefinedMods(IEnumerable<StaticMod> mods, bool structural)
        {
            foreach (StaticMod mod in mods)
            {
                IEnumerable<char> aas = mod.AAs == null ? AMINO_ACIDS : mod.AminoAcids;
                foreach (char aa in aas)
                {
                    var massKey = new ModMassKey
                    {
                        Aa = aa,
                        Mass = Math.Round(CALC.GetModMass(aa, mod), 1),
                        Structural = structural,
                        Terminus = mod.Terminus
                    };
                    DictModMasses.Remove(massKey);
                    DictModMasses.Add(massKey, mod);
                }
            }
        }

        public static StaticMod FindMatchingStaticMod(bool isotope, StaticMod modToMatch)
        {
            var dict = isotope ? DictIsotopeModNames : DictStructuralModNames;
            var hiddenDict = isotope ? DictHiddenIsotopeModNames : DictHiddenStructuralModNames;
            foreach (StaticMod mod in dict.Values)
            {
                if (mod.Equivalent(modToMatch, false, false))
                    return mod;
            }
            foreach (StaticMod mod in hiddenDict.Values)
            {
                if (mod.Equivalent(modToMatch, false, false))
                    return mod;
            }
            return null;
        }

        public static bool ShowMatchedModDlg(List<StaticMod> structuralMods, List<StaticMod> isotopeMods, string action)
        {
            if (structuralMods.Count + isotopeMods.Count == 0)
                return true;
            var modNames = new List<String>(structuralMods.ConvertAll(mod => '\n' + mod.Name));
            modNames.AddRange(new List<String>(isotopeMods.ConvertAll(mod => '\n' + mod.Name)));
            var result =
                MessageBox.Show(
                string.Format("Skyline was able to match the following modifications:\n {0}\n\nContinue with {1}?",
                    string.Concat(modNames.ToArray()), action),
                Program.Name, MessageBoxButtons.OKCancel);
            return result == DialogResult.OK;
        }

        public struct UniModIdKey
        {
            public int Id { get; set; }
            public char Aa { get; set; }
            public bool Structural { get; set; }
            public ModTerminus? Terminus { get; set; }
        }

        public struct ModMassKey
        {
            public double Mass { get; set; }
            public char Aa { get; set; }
            public bool Structural { get; set; }
            public ModTerminus? Terminus { get; set; }
        }
    }
}
