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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class MatchModificationsControl : UserControl
    {
        public enum ModType { structural, heavy };

        public struct ListBoxModification
        {
            public StaticMod Mod { get; private set; }
            private bool IsDDASearch { get; set; }
            public ListBoxModification(StaticMod mod, bool isDDA = false) : this()
            {
                Mod = mod;
                IsDDASearch = isDDA;
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
                if (IsDDASearch)
                {
                    sb.Append(' ');
                    if (Mod.IsVariable)
                        sb.Append(Resources.ListBoxModification_ToString__variable_);
                    else if (Mod.LabelAtoms != LabelAtoms.None)
                        sb.Append(Resources.ListBoxModification_ToString__isotopic_label_);
                    else
                        sb.Append(Resources.ListBoxModification_ToString__fixed_);
                }
                return string.Format(Resources.AbstractModificationMatcherFoundMatches__0__equals__1__, Mod.Name, sb);
            }
        }

        public MatchModificationsSettings ModificationSettings
        {
            get { return new MatchModificationsSettings(CheckedModifications.ToList()); }
        }

        public class MatchModificationsSettings
        {
            public static readonly MatchModificationsSettings DEFAULT = new MatchModificationsSettings(new string[0]);

            public MatchModificationsSettings(IList<string> modifications)
            {
                Modifications = modifications;
            }

            [Track]
            public IList<string> Modifications { get; private set; }
        }

        public MatchModificationsControl(IModifyDocumentContainer documentContainer, ImportPeptideSearch importPeptideSearch)
        {
            DocumentContainer = documentContainer;
            ImportPeptideSearch = importPeptideSearch;

            InitializeComponent();
        }

        private IModifyDocumentContainer DocumentContainer { get; set; }
        private SrmDocument Document { get; set; }
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

        public IEnumerable<string> MatchedModifications => (from ListBoxModification item in modificationsListBox.Items select item.Mod.Name);

        public IEnumerable<string> UnmatchedModifications => unmatchedListBox.Items.Cast<string>();

        public bool Initialize(SrmDocument document)
        {
            Document = document;

            if (!ImportPeptideSearch.HasDocLib && !ImportPeptideSearch.IsDDASearch)
                return false;
            if (ImportPeptideSearch.IsDDASearch)
            {
                labelModifications.Text = Resources.MatchModificationsControl_ModificationLabelText_DDA_Search;
                btnAddModification.Text = Resources.MatchModificationsControl_Initialize__Edit_modifications;
                menuItemAddStructuralModification.Text = Resources.MatchModificationsControl_Initialize_Edit__structural_modifications___;
                menuItemAddHeavyModification.Text = Resources.MatchModificationsControl_Initialize_Edit__heavy_modifications___;
            }

            ImportPeptideSearch.InitializeModifications(document);
            FillLists(document);
            return modificationsListBox.Items.Count > 1 || unmatchedListBox.Items.Count > 1;
        }

        public SrmSettings AddCheckedModifications(SrmDocument document)
        {
            if (modificationsListBox.CheckedItems.Count == 0 && !ImportPeptideSearch.IsDDASearch)
            {
                if (!ImportPeptideSearch.IsDDASearch)
                    return document.Settings;
                else
                {
                    //document.Settings.PeptideSettings.Modifications.StaticModifications.Clear();
                    return document.Settings;
                }
            }

            // Find checked static mods
            List<StaticMod> structuralMods;
            List<StaticMod> newHeavyMods; 
            if (ImportPeptideSearch.IsDDASearch)
            {
                structuralMods = (from mod in Settings.Default.StaticModList
                    from ListBoxModification checkedMod in modificationsListBox.CheckedItems
                    where mod.Equivalent(checkedMod.Mod)
                    select mod).ToList();
                //TODO Check for amanda
        
                newHeavyMods = new List<StaticMod>();

            }
            else
            {
            structuralMods = (from mod in ImportPeptideSearch.MatcherPepMods.StaticModifications
                from ListBoxModification checkedMod in modificationsListBox.CheckedItems
                where mod.Equivalent(checkedMod.Mod)
                select mod).ToList();

            // Find checked heavy mods
            newHeavyMods = (from mod in ImportPeptideSearch.MatcherHeavyMods
                from ListBoxModification checkedMod in modificationsListBox.CheckedItems
                where mod.Equivalent(checkedMod.Mod)
                select mod).ToList();
          }

            // Update document modifications
            return ImportPeptideSearch.AddModifications(document,
                new PeptideModifications(structuralMods, new[] {new TypedModifications(IsotopeLabelType.heavy, newHeavyMods)}));
        }

        private void FillLists(SrmDocument document)
        {
            IList<StaticMod> modsToAdd, modsToCheck;
            if (ImportPeptideSearch.IsDDASearch)
            {
                modsToAdd = Settings.Default.StaticModList.ToList();
                modsToAdd.AddRange(Settings.Default.HeavyModList.Distinct());
                modsToCheck = document.Settings.PeptideSettings.Modifications.StaticModifications.ToList();
                modsToCheck.AddRange(document.Settings.PeptideSettings.Modifications.AllHeavyModifications.Distinct());
                //modsToAdd = ImportPeptideSearch.UserDefinedTypedMods;
            }
            else
            {
                ImportPeptideSearch.UpdateModificationMatches(document);
                modsToAdd = ImportPeptideSearch.GetMatchedMods().ToList();
                modsToCheck = modsToAdd;
            }

            modificationsListBox.Items.Clear();
            unmatchedListBox.Items.Clear();

            foreach (var match in modsToAdd)
                modificationsListBox.Items.Add(new ListBoxModification(match, ImportPeptideSearch.IsDDASearch), modsToCheck.Contains(match));

            var unmatched = ImportPeptideSearch.GetUnmatchedSequences();
            if (unmatched != null && unmatched.Any())
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
            if (ImportPeptideSearch.IsDDASearch)
            {
                var driverStaticMod = new SettingsListBoxDriver<StaticMod>(modificationsListBox, Settings.Default.StaticModList);
                driverStaticMod.EditList();
                FillLists(Document);
            }
            else
                AddModification(ModType.structural);
        }

        private void menuItemAddHeavyModification_Click(object sender, EventArgs e)
        {
            if (ImportPeptideSearch.IsDDASearch)
            {
                var driverHeavyMod = new SettingsListBoxDriver<StaticMod>(modificationsListBox, Settings.Default.HeavyModList);
                driverHeavyMod.EditList();
                FillLists(Document);
            }
            else
                AddModification(ModType.heavy);
        }

        public void AddModification(StaticMod mod, ModType type)
        {
            if (mod == null)
                return;

            ImportPeptideSearch.UserDefinedTypedMods.Add(mod);

            PeptideModifications peptideModifications = DocumentContainer.Document.Settings.PeptideSettings.Modifications;
            if (type == ModType.structural)
            {
                peptideModifications = peptideModifications.ChangeStaticModifications(
                    peptideModifications.StaticModifications.Concat(new[] {mod}).ToArray());
            }
            else
            {
                peptideModifications = peptideModifications.AddHeavyModifications(new[] {mod});
            }

            DocumentContainer.ModifyDocumentNoUndo(doc => doc.ChangeSettings(DocumentContainer.Document.Settings.ChangePeptideSettings(
                DocumentContainer.Document.Settings.PeptideSettings.ChangeModifications(peptideModifications))));

            DocumentContainer.Document.Settings.UpdateDefaultModifications(false);

            FillLists(DocumentContainer.Document);
        }

        public void AddModification(ModType type)
        {
            var newMod = type == ModType.structural
                ? Settings.Default.StaticModList.EditItem(this, null, Settings.Default.StaticModList, null)
                : Settings.Default.HeavyModList.EditItem(this, null, Settings.Default.HeavyModList, null);

            AddModification(newMod, type);
        }

        public void ChangeAll(bool check)
        {
            for (int i = 0; i < modificationsListBox.Items.Count; ++i)
                modificationsListBox.SetItemChecked(i, check);
        }

        public void ChangeItem(int index, bool check)
        {
            modificationsListBox.SetItemChecked(index, check);
        }

        public void ClickAddStructuralModification()
        {
            menuItemAddStructuralModification.PerformClick();
        }

        public void ClickAddHeavyModification()
        {
            menuItemAddHeavyModification.PerformClick();
        }
    }
}
