/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// The Modifications page of the Osprey Search wizard and of Build Library's fine-tune: the
    /// fixed and variable modifications the library Osprey predicts from the FASTA carries. The
    /// lists start as the host's document modifications; the user may add or remove from there.
    /// </summary>
    public partial class OspreyModificationsControl : UserControl
    {
        public OspreyModificationsControl()
        {
            InitializeComponent();
        }

        private Form WizardForm => FormEx.GetParentForm(this);

        /// <summary>Fills the lists from <paramref name="mods"/>: the variable ones in the variable list, the rest in the fixed one.</summary>
        public void SetModifications(IEnumerable<StaticMod> mods)
        {
            listFixedMods.Items.Clear();
            listVariableMods.Items.Clear();
            foreach (var mod in mods)
            {
                if (mod.IsVariable)
                    listVariableMods.Items.Add(mod);
                else
                    listFixedMods.Items.Add(mod);
            }
        }

        public IEnumerable<StaticMod> FixedMods => listFixedMods.Items.Cast<StaticMod>();
        public IEnumerable<StaticMod> VariableMods => listVariableMods.Items.Cast<StaticMod>();

        private void btnAddFixedMod_Click(object sender, EventArgs e)
        {
            AddModification(listFixedMods);
        }

        private void btnAddVariableMod_Click(object sender, EventArgs e)
        {
            AddModification(listVariableMods);
        }

        private void btnRemoveFixedMod_Click(object sender, EventArgs e)
        {
            RemoveSelectedMod(listFixedMods);
        }

        private void btnRemoveVariableMod_Click(object sender, EventArgs e)
        {
            RemoveSelectedMod(listVariableMods);
        }

        private void AddModification(ListBox listBox)
        {
            var existing = new HashSet<string>(FixedMods.Select(m => m.Name).Concat(VariableMods.Select(m => m.Name)));
            var newMod = Settings.Default.StaticModList.EditItem(WizardForm, null, Settings.Default.StaticModList, null);
            if (newMod != null && !existing.Contains(newMod.Name))
                listBox.Items.Add(newMod);
        }

        private static void RemoveSelectedMod(ListBox listBox)
        {
            if (listBox.SelectedIndex >= 0)
                listBox.Items.RemoveAt(listBox.SelectedIndex);
        }

        /// <summary>The page's modifications as Carafe's ids, for Osprey's command line: the one mapping both hosts use.</summary>
        public void ApplyTo(OspreyConfig config)
        {
            config.FixedModificationIds = OspreyHelpers.GetCarafeModificationIds(FixedMods, out _);
            config.VariableModificationIds = OspreyHelpers.GetCarafeModificationIds(VariableMods, out _);
        }

        /// <summary>
        /// Next from the page: every modification must be one Carafe's table describes, since the
        /// library is predicted with them.
        /// </summary>
        public bool ValidatePage()
        {
            OspreyHelpers.GetCarafeModificationIds(FixedMods.Concat(VariableMods), out var unsupported);
            if (unsupported.Count > 0)
            {
                MessageDlg.Show(WizardForm, TextUtil.LineSeparate(
                    PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Osprey_cannot_predict_a_library_with_these_modifications_,
                    TextUtil.LineSeparate(unsupported.Select(m => m.Name))));
                return false;
            }
            return true;
        }
    }
}
