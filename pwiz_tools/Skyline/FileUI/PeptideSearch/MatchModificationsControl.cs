/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class MatchModificationsControl : UserControl
    {
        public enum ModType { structural, heavy };

        private struct ListBoxModification
        {
            public StaticMod Mod { get; private set; }
            public ListBoxModification(StaticMod mod) : this()
            {
                Mod = mod;
            }
            public override string ToString()
            {
                char aa = (Mod.AAs != null) ? Mod.AAs.FirstOrDefault() : '\0';
                double modMass = AbstractModificationMatcher.GetDefaultModMass(aa, Mod);
                var sb = new StringBuilder();
                sb.Append(Mod.AAs);
                sb.Append('[');
                sb.Append(Math.Round(modMass, 1));
                sb.Append(']');
                return string.Format(Resources.AbstractModificationMatcherFoundMatches__0__equals__1__,
                                     Mod.Name, sb);
            }
        }

        public MatchModificationsControl(SkylineWindow skylineWindow, ImportPeptideSearch importPeptideSearch)
        {
            SkylineWindow = skylineWindow;
            ImportPeptideSearch = importPeptideSearch;

            InitializeComponent();
        }

        private SkylineWindow SkylineWindow { get; set; }
        private ImportPeptideSearch ImportPeptideSearch { get; set; }

        public IEnumerable<string> CheckedModifications
        {
            get
            {
                return (from ListBoxModification item in modificationsListBox.CheckedItems select item.ToString());
            }
            set
            {
                for (int i = 0; i < modificationsListBox.Items.Count; ++i)
                {
                    modificationsListBox.SetItemChecked(i, false);
                    if (value.Any(mod => ((ListBoxModification)modificationsListBox.Items[i]).Mod.Name == mod))
                    {
                        modificationsListBox.SetItemChecked(i, true);
                    }
                }
            }
        }

        public IEnumerable<string> MatchedModifications
        {
            get
            {
                return (from ListBoxModification item in modificationsListBox.Items select item.Mod.Name);
            }
        }

        public IEnumerable<string> UnmatchedModifications
        {
            get
            {
                return unmatchedListBox.Items.Cast<string>();
            }
        }

        public bool Initialize(SrmDocument document)
        {
            if (!ImportPeptideSearch.HasDocLib)
                return false;

            ImportPeptideSearch.InitializeModifications(document);
            FillLists(document);
            return (modificationsListBox.Items.Count > 1 || unmatchedListBox.Items.Count > 1);
        }

        public SrmSettings AddCheckedModifications(SrmDocument document)
        {
            if (modificationsListBox.CheckedItems.Count == 0)
                return document.Settings;

            // Find checked static mods
            var newStructuralMods = (from mod in ImportPeptideSearch.MatcherPepMods.StaticModifications
                                     from ListBoxModification checkedMod in modificationsListBox.CheckedItems
                                     where mod.Equivalent(checkedMod.Mod)
                                     select mod).ToList();

            // Find checked heavy mods
            var newHeavyMods = (from mod in ImportPeptideSearch.MatcherHeavyMods
                                from ListBoxModification checkedMod in modificationsListBox.CheckedItems
                                where mod.Equivalent(checkedMod.Mod)
                                select mod).ToList();

            // Update document modifications
            return ImportPeptideSearch.AddModifications(document,
                new PeptideModifications(newStructuralMods, new[] {new TypedModifications(IsotopeLabelType.heavy, newHeavyMods)}));
        }

        private void FillLists(SrmDocument document)
        {
            ImportPeptideSearch.UpdateModificationMatches(document);

            modificationsListBox.Items.Clear();
            unmatchedListBox.Items.Clear();

            foreach (var match in ImportPeptideSearch.GetMatchedMods())
                modificationsListBox.Items.Add(new ListBoxModification(match), CheckState.Unchecked);

            var unmatched = ImportPeptideSearch.GetUnmatchedSequences();
            if (unmatched.Any())
            {
                splitContainer.Panel2Collapsed = false;
                foreach (var uninterpretedMod in unmatched)
                    unmatchedListBox.Items.Add(uninterpretedMod);
            }
            else
            {
                splitContainer.Panel2Collapsed = true;
            }
            if (modificationsListBox.Items.Count <= 3)
            {
                cbSelectAll.Visible = false;
                modificationsListBox.Height += cbSelectAll.Bottom - modificationsListBox.Bottom;
            }
        }

        private void cbSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            ChangeAll(cbSelectAll.Checked);
        }

        private void btnAddModification_Click(object sender, EventArgs e)
        {
            menuAddModification.Show(btnAddModification, 0, btnAddModification.Height + 1);
        }

        private void menuItemAddStructuralModification_Click(object sender, EventArgs e)
        {
            AddModification(ModType.structural);
        }

        private void menuItemAddHeavyModification_Click(object sender, EventArgs e)
        {
            AddModification(ModType.heavy);
        }

        public void AddModification(StaticMod mod, ModType type)
        {
            if (mod == null)
                return;

            ImportPeptideSearch.UserDefinedTypedMods.Add(mod);

            PeptideSettings newPeptideSettings = SkylineWindow.Document.Settings.PeptideSettings;
            var newMods = new List<StaticMod>(
                    (type == ModType.structural ? newPeptideSettings.Modifications.StaticModifications : newPeptideSettings.Modifications.HeavyModifications)
                ) { mod };
            newPeptideSettings = (type == ModType.structural)
                                     ? newPeptideSettings.ChangeModifications(newPeptideSettings.Modifications.ChangeStaticModifications(newMods))
                                     : newPeptideSettings.ChangeModifications(newPeptideSettings.Modifications.ChangeHeavyModifications(newMods));

            SkylineWindow.ChangeSettings(SkylineWindow.Document.Settings.ChangePeptideSettings(newPeptideSettings), true,
                string.Format(Resources.MatchModificationsControl_AddModification_Add__0__modification__1_, type, mod.Name));
            SkylineWindow.Document.Settings.UpdateDefaultModifications(false);

            FillLists(SkylineWindow.Document);
        }

        public void AddModification(ModType type)
        {
            var newMod = (type == ModType.structural)
                             ? Settings.Default.StaticModList.EditItem(this, null, Settings.Default.StaticModList, null)
                             : Settings.Default.HeavyModList.EditItem(this, null, Settings.Default.HeavyModList, null);

            AddModification(newMod, type);
        }

        public void ChangeAll(bool check)
        {
            for (int i = 0; i < modificationsListBox.Items.Count; ++i)
                modificationsListBox.SetItemChecked(i, check);
        }
    }
}
