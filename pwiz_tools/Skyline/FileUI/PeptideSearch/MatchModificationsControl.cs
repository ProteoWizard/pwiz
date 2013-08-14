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
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
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

        public MatchModificationsControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;

            InitializeComponent();
        }

        private SkylineWindow SkylineWindow { get; set; }
        private Library _docLib;
        private LibKeyModificationMatcher _matcher;
        private HashSet<StaticMod> _userDefinedTypedMods;
        private IsotopeLabelType _defaultHeavyLabelType;

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
                    foreach (var mod in value)
                    {
                        if (((ListBoxModification)modificationsListBox.Items[i]).Mod.Name == mod)
                        {
                            modificationsListBox.SetItemChecked(i, true);
                            break;
                        }
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

        public bool Initialize(Library docLib)
        {
            if (docLib == null)
                return false;

            _docLib = docLib;
            _userDefinedTypedMods = new HashSet<StaticMod>();

            InitializeUserDefinedTypedMods();
            GetModificationMatches();
            
            FillLists();
            return (modificationsListBox.Items.Count > 1 || unmatchedListBox.Items.Count > 1);
        }

        private void InitializeUserDefinedTypedMods()
        {
            var mods = SkylineWindow.Document.Settings.PeptideSettings.Modifications;
            foreach (var type in mods.GetModificationTypes())
            {
                // Set the default heavy type to the first heavy type encountered.
                if (!ReferenceEquals(type, IsotopeLabelType.light) && _defaultHeavyLabelType == null)
                    _defaultHeavyLabelType = type;

                foreach (StaticMod mod in mods.GetModificationsByName(type.Name).Modifications.Where(m => !m.IsUserSet))
                    _userDefinedTypedMods.Add(mod);
            }

            var staticMods = new TypedModifications(IsotopeLabelType.light, mods.StaticModifications);
            var heavyMods = new TypedModifications(_defaultHeavyLabelType, mods.GetModifications(_defaultHeavyLabelType));

            foreach (StaticMod mod in staticMods.Modifications.Union(heavyMods.Modifications))
                _userDefinedTypedMods.Add(mod);
        }

        public void AddCheckedModifications()
        {
            if (_matcher == null || modificationsListBox.CheckedItems.Count == 0)
                return;

            // Get checked modifications
            var newStructuralMods = new List<StaticMod>();
            var newHeavyMods = new List<StaticMod>();
            PeptideModifications pepMods = _matcher.MatcherPepMods;

            // Find checked static mods
            foreach (var mod in pepMods.StaticModifications)
                foreach (ListBoxModification checkedMod in modificationsListBox.CheckedItems)
                    if (mod.Equivalent(checkedMod.Mod))
                        newStructuralMods.Add(mod);

            // Find checked heavy mods
            foreach (var mod in pepMods.GetModifications(_defaultHeavyLabelType))
                foreach (ListBoxModification checkedMod in modificationsListBox.CheckedItems)
                    if (mod.Equivalent(checkedMod.Mod))
                        newHeavyMods.Add(mod);

            // Update document modifications
            _matcher.MatcherPepMods = new PeptideModifications(newStructuralMods, new[] {new TypedModifications(IsotopeLabelType.heavy, newHeavyMods)} );
            SrmSettings newSettings = SkylineWindow.Document.Settings.ChangePeptideModifications(
                mods => _matcher.SafeMergeImplicitMods(SkylineWindow.Document));
            SkylineWindow.ChangeSettings(newSettings, true, Resources.MatchModificationsControl_AddCheckedModifications_Add_checked_modifications);
            SkylineWindow.Document.Settings.UpdateDefaultModifications(false);
        }

        private void GetModificationMatches()
        {
            if (_matcher == null)
                _matcher = new LibKeyModificationMatcher();
            else
                _matcher.ClearMatches();

            _matcher.CreateMatches(SkylineWindow.Document.Settings, _docLib.Keys, Settings.Default.StaticModList, Settings.Default.HeavyModList);
        }

        private void FillLists()
        {
            GetModificationMatches();

            modificationsListBox.Items.Clear();
            unmatchedListBox.Items.Clear();

            PeptideModifications pepMods = _matcher.MatcherPepMods;
            IEnumerable<StaticMod> allMods = pepMods.StaticModifications.Union(pepMods.GetModifications(_defaultHeavyLabelType));
            foreach (var mod in allMods)
            {
                bool skipThis = false;
                foreach (var userMod in _userDefinedTypedMods)
                {
                    if (mod.Equivalent(userMod))
                    {
                        skipThis = true;
                        break;
                    }
                }
                if (skipThis)
                    continue;

                modificationsListBox.Items.Add(new ListBoxModification(mod), CheckState.Checked);
            }

            foreach (var uninterpretedMod in _matcher.UnmatchedSequences)
                unmatchedListBox.Items.Add(uninterpretedMod);
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

            _userDefinedTypedMods.Add(mod);

            PeptideSettings newPeptideSettings = SkylineWindow.Document.Settings.PeptideSettings;
            var newMods = new List<StaticMod>(
                    (type == ModType.structural ? newPeptideSettings.Modifications.StaticModifications : newPeptideSettings.Modifications.HeavyModifications)
                ) { mod };
            newPeptideSettings = (type == ModType.structural)
                                     ? newPeptideSettings.ChangeModifications(newPeptideSettings.Modifications.ChangeStaticModifications(newMods))
                                     : newPeptideSettings.ChangeModifications(newPeptideSettings.Modifications.ChangeHeavyModifications(newMods));

            SkylineWindow.ChangeSettings(SkylineWindow.Document.Settings.ChangePeptideSettings(newPeptideSettings), true,
                string.Format(Resources.MatchModificationsControl_AddModification_Add__0__modification__1_, type.ToString(), mod.Name));
            SkylineWindow.Document.Settings.UpdateDefaultModifications(false);

            FillLists();
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
