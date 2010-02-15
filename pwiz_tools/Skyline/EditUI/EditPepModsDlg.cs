/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class EditPepModsDlg : Form
    {
        private const int VSPACE = 3;
        private const string ADD_ITEM = "<Add...>";
        private const string EDIT_LIST_ITEM = "<Edit list...>";

        private const string PREFIX_STATIC_NAME = "comboStatic";
        private const string PREFIX_HEAVY_NAME = "comboHeavy";

        private readonly List<ComboBox> _listComboStatic = new List<ComboBox>();
        private readonly List<int> _listSelectedIndexStatic = new List<int>();
        private readonly List<Label> _listLabelAA = new List<Label>();
        private readonly List<ComboBox> _listComboHeavy = new List<ComboBox>();
        private readonly List<int> _listSelectedIndexHeavy = new List<int>();

        public EditPepModsDlg(SrmSettings settings, PeptideDocNode nodePeptide)
        {
            DocSettings = settings;
            NodePeptide = nodePeptide;
            ExplicitMods = nodePeptide.ExplicitMods;

            InitializeComponent();

            Icon = Resources.Skyline;

            SuspendLayout();
            ComboBox comboStaticLast = null, comboHeavyLast = null;
            Label labelAALast = null;
            string seq = nodePeptide.Peptide.Sequence;
            for (int i = 0; i < seq.Length; i++)
            {
                char aa = seq[i];

                if (comboStaticLast == null)
                {
                    labelAALast = labelAA1;
                    comboStaticLast = comboStatic1;
                    comboHeavyLast = comboHeavy1;
                }
                else
                {
                    int top = Top = comboStaticLast.Bottom + VSPACE;
                    panelMain.Controls.Add(labelAALast = new Label
                    {
                        Name = ("labelAA" + (_listLabelAA.Count + 1)),
                        AutoSize = true,
                        Font = labelAA1.Font,
                        Left = labelAA1.Left,
                        Top = top + (labelAALast.Top - comboStaticLast.Top),
                        Size = labelAA1.Size,
                        TabIndex = labelAALast.TabIndex + 3
                    });
                    panelMain.Controls.Add(comboStaticLast = new ComboBox
                    {
                        Name = (PREFIX_STATIC_NAME + (_listComboStatic.Count + 1)),
                        Left = comboStaticLast.Left,
                        Top = top,
                        Size = comboStaticLast.Size,
                        TabIndex = comboStaticLast.TabIndex + 3
                    });
                    panelMain.Controls.Add(comboHeavyLast = new ComboBox
                    {
                        Name = (PREFIX_HEAVY_NAME + (_listComboHeavy.Count + 1)),
                        Left = comboHeavyLast.Left,
                        Top = top,
                        Size = comboHeavyLast.Size,
                        TabIndex = comboHeavyLast.TabIndex + 3
                    });
                }
                labelAALast.Text = aa.ToString();
                _listLabelAA.Add(labelAALast);
                _listSelectedIndexStatic.Add(-1);
                _listComboStatic.Add(InitModificationCombo(comboStaticLast, i, IsotopeLabelType.light));
                _listSelectedIndexHeavy.Add(-1);
                _listComboHeavy.Add(InitModificationCombo(comboHeavyLast, i, IsotopeLabelType.heavy));
            }
            if (comboStaticLast != null && comboStaticLast != comboStatic1)
            {
                int heightDiff = comboStaticLast.Bottom - comboStatic1.Bottom;
                heightDiff += comboStatic1.Bottom - panelMain.Height;
                Height += heightDiff;
                btnOk.TabIndex = comboHeavyLast.TabIndex + 1;
                btnCancel.TabIndex = btnOk.TabIndex + 1;
            }
            ResumeLayout(true);
        }

        private SrmSettings DocSettings { get; set; }
        private PeptideDocNode NodePeptide { get; set; }

        /// <summary>
        /// Explicit modifications chosen by the user, if OK clicked.
        /// </summary>
        public ExplicitMods ExplicitMods { get; private set; }

        public void SetModification(int indexAA, IsotopeLabelType type, string modification)
        {
            ComboBox combo = (type == IsotopeLabelType.light ?
                _listComboStatic[indexAA] : _listComboHeavy[indexAA]);
            combo.SelectedItem = modification;
        }

        private static StaticModList StaticList { get { return Settings.Default.StaticModList; } }
        private static HeavyModList HeavyList { get { return Settings.Default.HeavyModList; } }

        private ComboBox InitModificationCombo(ComboBox combo, int indexAA, IsotopeLabelType type)
        {
            var modsDoc = DocSettings.PeptideSettings.Modifications;
            var modsExp = NodePeptide.ExplicitMods;
            if (type == IsotopeLabelType.heavy)
            {
                return InitModificationCombo(combo, modsDoc.HeavyModifications,
                    modsExp != null ? modsExp.HeavyModifications : null, HeavyList, indexAA);
            }
            else
            {
                return InitModificationCombo(combo, modsDoc.StaticModifications,
                    modsExp != null ? modsExp.StaticModifications : null, StaticList, indexAA);
            }
        }

        private ComboBox InitModificationCombo(ComboBox combo,
                                           IList<StaticMod> listDocMods,
                                           IList<ExplicitMod> listExplicitMods,
                                           IEnumerable<StaticMod> listSettingsMods,
                                           int indexAA)
        {       
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FormattingEnabled = true;
            int iSelected = UpdateComboItems(combo, listSettingsMods, listExplicitMods, listDocMods, indexAA, false);
            // Add event handler before changing selection, so that the handler will fire
            combo.SelectedIndexChanged += comboMod_SelectedIndexChangedEvent;
            // Change selection, and fire event handler
            combo.SelectedIndex = iSelected;
            return combo;
        }

        private int UpdateComboItems(ComboBox combo, IEnumerable<StaticMod> listSettingsMods,
            IList<ExplicitMod> listExplicitMods, IList<StaticMod> listDocMods, int indexAA, bool select)
        {
            string seq = NodePeptide.Peptide.Sequence;
            char aa = seq[indexAA];
            int iSelected = -1;
            string explicitName = null;
            if (ExplicitMods != null)
            {
                int indexMod = listExplicitMods.IndexOf(mod => mod.IndexAA == indexAA);
                if (indexMod != -1)
                    explicitName = listExplicitMods[indexMod].Modification.Name;
            }

            List<string> listItems = new List<string> {""};
            foreach (StaticMod mod in listSettingsMods)
            {
                if (!mod.IsMod(aa, indexAA, seq.Length))
                    continue;
                listItems.Add(mod.Name);

                // If the peptide is explicitly modified, then the explicit modifications
                // indicate the combo selections.
                if (ExplicitMods != null)
                {
                    if (Equals(explicitName, mod.Name))
                        iSelected = listItems.Count - 1;
                }
                else
                {
                    // If the modification is present in the document, then it should be selected by default.
                    StaticMod modCurrent = mod;
                    if (listDocMods != null && listDocMods.IndexOf(modDoc =>
                            !modDoc.IsExplicit && Equals(modDoc.Name, modCurrent.Name)) != -1)
                        iSelected = listItems.Count - 1;
                }
            }
            listItems.Add(ADD_ITEM);
            listItems.Add(EDIT_LIST_ITEM);
            if (!EqualsItems(combo, listItems))
            {
                combo.Items.Clear();
                listItems.ForEach(item => combo.Items.Add(item));
            }
            if (select)
                combo.SelectedIndex = iSelected;
            return iSelected;
        }

        private static bool EqualsItems(ComboBox combo, IList<string> listItems)
        {
            int count = combo.Items.Count;
            if (count != listItems.Count)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!Equals(combo.Items[i], listItems[i]))
                    return false;
            }
            return true;
        }

        private static bool AddItemSelected(ComboBox combo)
        {
            var selectedItem = combo.SelectedItem;
            return (selectedItem != null && ADD_ITEM == selectedItem.ToString());
        }

        private static bool EditListSelected(ComboBox combo)
        {
            var selectedItem = combo.SelectedItem;
            return (selectedItem != null && EDIT_LIST_ITEM == combo.SelectedItem.ToString());
        }

        public void comboMod_SelectedIndexChangedEvent(object sender, EventArgs e)
        {
            ComboBox combo = (ComboBox) sender;
            ExplicitMods modsExp = ExplicitMods;
            if (combo.Name.StartsWith(PREFIX_HEAVY_NAME))
            {
                int indexAA = int.Parse(combo.Name.Substring(PREFIX_HEAVY_NAME.Length)) - 1;
                SelectedIndexChangedEvent(combo, HeavyList, modsExp != null ? modsExp.HeavyModifications : null,
                    _listComboHeavy, _listSelectedIndexHeavy, indexAA);
                // Make text bold, if it has a heavy modification
                bool bold = !string.IsNullOrEmpty((string) combo.SelectedItem);
                var label = _listLabelAA[indexAA];
                if (label.Font.Bold != bold)
                {
                    label.Font = new Font(label.Font.Name, label.Font.SizeInPoints,
                        (bold ? FontStyle.Bold : FontStyle.Regular), GraphicsUnit.Point, 0);                    
                }
            }
            else
            {
                int indexAA = int.Parse(combo.Name.Substring(PREFIX_STATIC_NAME.Length)) - 1;
                SelectedIndexChangedEvent(combo, StaticList, modsExp != null ? modsExp.StaticModifications : null,
                    _listComboStatic, _listSelectedIndexStatic, indexAA);
                bool modified = !string.IsNullOrEmpty((string) combo.SelectedItem);
                _listLabelAA[indexAA].Text = NodePeptide.Peptide.Sequence[indexAA] + (modified ? "*" : "");
            }
        }

        private void SelectedIndexChangedEvent(ComboBox combo,
            SettingsList<StaticMod> listSettingsMods, IList<ExplicitMod> listExplicitMods,
            IList<ComboBox> listCombo, IList<int> listSelectedIndex, int indexAA)
        {
            int selectedIndexLast = listSelectedIndex[indexAA];
            if (AddItemSelected(combo))
            {
                StaticMod itemNew = listSettingsMods.NewItem(this, null, null);
                if (!Equals(itemNew, null))
                {
                    listSettingsMods.Add(itemNew);
                    LoadLists(listSettingsMods, listExplicitMods, listCombo, indexAA, itemNew.GetKey());
                }
                else
                {
                    // Reset the selected index before edit was chosen.
                    combo.SelectedIndex = selectedIndexLast;
                }
            }
            else if (EditListSelected(combo))
            {
                IEnumerable<StaticMod> listNew = listSettingsMods.EditList(this, null);
                if (listNew != null)
                {
                    listSettingsMods.Clear();
                    listSettingsMods.AddRange(listNew);

                    string selectedItemLast = combo.Items[selectedIndexLast].ToString();
                    LoadLists(listSettingsMods, listExplicitMods, listCombo, indexAA, selectedItemLast);
                }
                else
                {
                    // Reset the selected index before edit was chosen.
                    combo.SelectedIndex = selectedIndexLast;
                }
            }
            listSelectedIndex[indexAA] = combo.SelectedIndex;
        }

        private void LoadLists(IEnumerable<StaticMod> listSettingsMods, IList<ExplicitMod> listExplicitMods,
            IList<ComboBox> listCombo, int indexAA, string selectedItem)
        {
            for (int i = 0; i < listCombo.Count; i++)
            {
                ComboBox combo = listCombo[i];
                // Reset the combo to its current value, unless a different value was specified
                object selectedItemDesired = (i == indexAA ? selectedItem : combo.SelectedItem);
                UpdateComboItems(combo, listSettingsMods, listExplicitMods, null, i, false);
                if (!Equals(selectedItemDesired, combo.SelectedItem))
                    combo.SelectedItem = selectedItemDesired;
            }
        }

        private static IList<ExplicitMod> GetExplicitMods(IList<ComboBox> mods,
                MappedList<string, StaticMod> listSettingsMods)
        {
            List<ExplicitMod> listMods = new List<ExplicitMod>();
            for (int i = 0; i < mods.Count; i++)
            {
                string modName = (string) mods[i].SelectedItem;
                if (!string.IsNullOrEmpty(modName))
                {
                    StaticMod settingsMod;
                    if (listSettingsMods.TryGetValue(modName, out settingsMod))
                        listMods.Add(new ExplicitMod(i, settingsMod));
                }
            }
            return listMods.ToArray();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            ExplicitMods = null;
            var modifications = DocSettings.PeptideSettings.Modifications;
            for (int i = 0; i < _listComboStatic.Count; i++)
                UpdateComboItems(_listComboStatic[i], StaticList, null, modifications.StaticModifications, i, true);
            for (int i = 0; i < _listComboHeavy.Count; i++)
                UpdateComboItems(_listComboHeavy[i], HeavyList, null, modifications.HeavyModifications, i, true);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            IList<ExplicitMod> staticMods = GetExplicitMods(_listComboStatic, Settings.Default.StaticModList);
            IList<ExplicitMod> heavyMods = GetExplicitMods(_listComboHeavy, Settings.Default.HeavyModList);

            var modsDoc = DocSettings.PeptideSettings.Modifications;
            var explicitMods = new ExplicitMods(NodePeptide.Peptide, staticMods, heavyMods);
            var implicitMods = new ExplicitMods(NodePeptide.Peptide,
                modsDoc.StaticModifications, Settings.Default.StaticModList,
                modsDoc.HeavyModifications, Settings.Default.HeavyModList);
            // If currently chosen modifications equal the implicit document modifications,
            // then clear the explicit modification from this peptide.
            ExplicitMods = (Equals(explicitMods, implicitMods) ? null : explicitMods);
            DialogResult = DialogResult.OK;
            Close();            
        }
    }
}
