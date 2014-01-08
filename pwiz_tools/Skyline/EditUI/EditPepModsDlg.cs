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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class EditPepModsDlg : FormEx
    {
        private const int VSPACE = 3;
        private const int HSPACE = 10;

        private const string PREFIX_STATIC_NAME = "comboStatic";    // Not L10N
        private const string PREFIX_HEAVY_NAME = "comboHeavy";    // Not L10N

        private const string PREFIX_LABEL_NAME = "labelHeavy1";    // Not L10N

        private static readonly Regex REGEX_HEAVY_NAME = new Regex(PREFIX_HEAVY_NAME + @"(\d+)_(\d+)"); // Not L10N

        private readonly List<ComboBox> _listComboStatic = new List<ComboBox>();
        private readonly List<int> _listSelectedIndexStatic = new List<int>();
        private readonly List<Label> _listLabelAA = new List<Label>();
        private readonly List<IsotopeLabelType> _listLabelTypeHeavy = new List<IsotopeLabelType>();
        private readonly List<List<ComboBox>> _listListComboHeavy = new List<List<ComboBox>>();
        private readonly List<List<int>> _listListSelectedIndexHeavy = new List<List<int>>();

        public static string GetStaticName(int row)
        {
            return string.Format("{0}{1}", PREFIX_STATIC_NAME, row); // Not L10N
        }

        public static string GetHeavyName(int row, int col)
        {
            return string.Format("{0}{1}_{2}", PREFIX_HEAVY_NAME, row, col); // Not L10N
        }

        private static string GetIsotopeLabelName(int col)
        {
            return string.Format("{0}{1}", PREFIX_LABEL_NAME, col); // Not L10N
        }

        private static string GetIsotopeLabelText(IsotopeLabelType labelType)
        {
            return string.Format(Resources.EditPepModsDlg_GetIsotopeLabelText_Isotope__0__, labelType);
        }

        public EditPepModsDlg(SrmSettings settings, PeptideDocNode nodePeptide)
        {
            DocSettings = settings;
            NodePeptide = nodePeptide;
            ExplicitMods = nodePeptide.ExplicitMods;

            InitializeComponent();

            Icon = Resources.Skyline;

            SuspendLayout();
            ComboBox comboStaticLast = null;
            List<ComboBox> listComboHeavyLast = null;
            List<Label> listLabelHeavyLast = null;
            Label labelAALast = null;
            string seq = nodePeptide.Peptide.Sequence;
            var modsDoc = DocSettings.PeptideSettings.Modifications;

            _listLabelTypeHeavy.AddRange(from typedMods in modsDoc.GetHeavyModifications()
                                         select typedMods.LabelType);

            for (int i = 0; i < seq.Length; i++)
            {
                char aa = seq[i];
                int row = i + 1;

                if (comboStaticLast == null || listComboHeavyLast == null)  // ReSharper
                {
                    labelAALast = labelAA1;
                    comboStaticLast = comboStatic1;
                    foreach (var labelType in _listLabelTypeHeavy)
                    {
                        if (listComboHeavyLast == null)
                        {
                            listComboHeavyLast = new List<ComboBox> { comboHeavy1_1 };
                            listLabelHeavyLast = new List<Label> { labelHeavy1 };
                            labelHeavy1.Text = GetIsotopeLabelText(labelType);
                        }
                        else
                        {
                            var comboHeavyLast = listComboHeavyLast[listComboHeavyLast.Count - 1];
                            panelMain.Controls.Add(comboHeavyLast = new ComboBox
                            {
                                Name = GetHeavyName(row, labelType.SortOrder),
                                Left = comboHeavyLast.Right + HSPACE,
                                Top = comboHeavyLast.Top,
                                Size = comboHeavyLast.Size,
                                TabIndex = comboHeavyLast.TabIndex + 1
                            });
                            listComboHeavyLast.Add(comboHeavyLast);
                            var labelHeavyLast = listLabelHeavyLast[listLabelHeavyLast.Count - 1];
                            panelMain.Controls.Add(labelHeavyLast = new Label
                            {
                                Text = GetIsotopeLabelText(labelType),
                                Name = GetIsotopeLabelName(labelType.SortOrder),
                                Left = comboHeavyLast.Left,
                                Top = labelHeavyLast.Top,
                                TabIndex = labelHeavyLast.TabIndex + 1
                            });
                            listLabelHeavyLast.Add(labelHeavyLast);
                        }
                    }
                }
                else
                {
                    int controlsPerRow = 2 + listComboHeavyLast.Count;
                    int top = Top = comboStaticLast.Bottom + VSPACE;
                    panelMain.Controls.Add(labelAALast = new Label
                    {
                        Name = ("labelAA" + row), // Not L10N
                        AutoSize = true,
                        Font = labelAA1.Font,
                        Left = labelAA1.Left,
                        Top = top + (labelAALast.Top - comboStaticLast.Top),
                        Size = labelAA1.Size,
                        TabIndex = labelAALast.TabIndex + controlsPerRow
                    });
                    panelMain.Controls.Add(comboStaticLast = new ComboBox
                    {
                        Name = GetStaticName(row),
                        Left = comboStaticLast.Left,
                        Top = top,
                        Size = comboStaticLast.Size,
                        TabIndex = comboStaticLast.TabIndex + controlsPerRow
                    });
                    foreach (var labelType in _listLabelTypeHeavy)
                    {
                        int col = labelType.SortOrder - 1;
                        var comboHeavyLast = listComboHeavyLast[col];
                        panelMain.Controls.Add(comboHeavyLast = new ComboBox
                        {
                            Name = GetHeavyName(row, labelType.SortOrder),
                            Left = comboHeavyLast.Left,
                            Top = top,
                            Size = comboHeavyLast.Size,
                            TabIndex = comboHeavyLast.TabIndex + controlsPerRow
                        });
                        listComboHeavyLast[col] = comboHeavyLast;
                    }
                }
                // Store static modification combos and selected indexes
                _listSelectedIndexStatic.Add(-1);
                _listComboStatic.Add(InitModificationCombo(comboStaticLast, i, IsotopeLabelType.light));
                // Store heavy moficiation combos and selected indexes
                if (listComboHeavyLast != null)   // ReSharper
                {
                    for (int j = 0; j < _listLabelTypeHeavy.Count; j++)
                    {
                        while (_listListComboHeavy.Count <= j)
                        {
                            _listListSelectedIndexHeavy.Add(new List<int>());
                            _listListComboHeavy.Add(new List<ComboBox>());
                        }
                        var comboHeavyLast = listComboHeavyLast[j];
                        var labelType = _listLabelTypeHeavy[j];

                        _listListSelectedIndexHeavy[j].Add(-1);
                        _listListComboHeavy[j].Add(InitModificationCombo(comboHeavyLast, i, labelType));
                    }
                }
                // Store amino acid labels
                labelAALast.Text = aa.ToString(CultureInfo.InvariantCulture);
                _listLabelAA.Add(labelAALast);
            }
            for (int i = 0; i < _listLabelAA.Count; i++)
            {
                UpdateAminoAcidLabel(i);
            }
            if (comboStaticLast != null && comboStaticLast != comboStatic1)
            {
                // Increase width by the delta from the left edges of the first and last
                // heavy combo box columns
                int widthDiff = _listListComboHeavy[_listListComboHeavy.Count - 1][0].Left -
                    _listListComboHeavy[0][0].Left;
                Width += widthDiff;
                // Increase height by the delta from the bottom edges of the first and last
                // amino acid labels
                if (comboStatic1 != null)   // ReSharper
                {
                    int heightDiff = comboStaticLast.Bottom - comboStatic1.Bottom;
                    heightDiff += comboStatic1.Bottom - panelMain.Height;
                    Height += heightDiff;
                }
                if (listComboHeavyLast != null) // ReSharper
                    btnOk.TabIndex = listComboHeavyLast[listComboHeavyLast.Count - 1].TabIndex + 1;
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

        /// <summary>
        /// True if a copy of the currently selected peptide should be
        /// made, with the explicit modifications applied.
        /// </summary>
        public bool IsCreateCopy
        {
            get { return cbCreateCopy.Checked; }
            set { cbCreateCopy.Checked = value; }
        }

        public void SetModification(int indexAA, IsotopeLabelType type, string modification)
        {
            ComboBox combo;
            if (type.IsLight)
                combo = _listComboStatic[indexAA];
            else
            {
                int indexHeavyList = _listLabelTypeHeavy.IndexOf(type);
                combo = _listListComboHeavy[indexHeavyList][indexAA];
            }
            combo.SelectedItem = modification;
        }

        public void AddNewModification(int indexAA, IsotopeLabelType type)
        {   ComboBox combo;
            if (type.IsLight)
                combo = _listComboStatic[indexAA];
            else
            {
                int indexHeavyList = _listLabelTypeHeavy.IndexOf(type);
                combo = _listListComboHeavy[indexHeavyList][indexAA];
            }
            combo.SelectedItem = Resources.SettingsListComboDriver_Add;
        }
        
        private static StaticModList StaticList { get { return Settings.Default.StaticModList; } }
        private static HeavyModList HeavyList { get { return Settings.Default.HeavyModList; } }

        private ComboBox InitModificationCombo(ComboBox combo, int indexAA, IsotopeLabelType type)
        {
            var modsDoc = DocSettings.PeptideSettings.Modifications;
            var modsExp = NodePeptide.ExplicitMods;
            return type.IsLight
                ? InitModificationCombo(combo, modsDoc.StaticModifications,
                    modsExp != null ? modsExp.StaticModifications : null, StaticList, indexAA,
                    modsExp != null && modsExp.IsVariableStaticMods)
                : InitModificationCombo(combo, modsDoc.GetModifications(type),
                    modsExp != null ? modsExp.GetModifications(type) : null, HeavyList, indexAA,
                    false);
        }

        private ComboBox InitModificationCombo(ComboBox combo,
                                           IList<StaticMod> listDocMods,
                                           IList<ExplicitMod> listExplicitMods,
                                           IEnumerable<StaticMod> listSettingsMods,
                                           int indexAA,
                                           bool selectEither)
        {       
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FormattingEnabled = true;
            int iSelected = UpdateComboItems(combo, listSettingsMods, listExplicitMods, listDocMods,
                indexAA, selectEither, false);
            // Add event handler before changing selection, so that the handler will fire
            combo.SelectedIndexChanged += comboMod_SelectedIndexChangedEvent;
            // Change selection, and fire event handler
            combo.SelectedIndex = iSelected;
            return combo;
        }

        private int UpdateComboItems(ComboBox combo, IEnumerable<StaticMod> listSettingsMods,
            IList<ExplicitMod> listExplicitMods, IList<StaticMod> listDocMods, int indexAA,
            bool selectEither, bool select)
        {
            string seq = NodePeptide.Peptide.Sequence;
            char aa = seq[indexAA];
            int iSelected = -1;
            string explicitName = null;
            if (listExplicitMods != null)
            {
                int indexMod = listExplicitMods.IndexOf(mod => mod.IndexAA == indexAA);
                if (indexMod != -1)
                    explicitName = listExplicitMods[indexMod].Modification.Name;
            }

            List<string> listItems = new List<string> {string.Empty};
            foreach (StaticMod mod in listSettingsMods)
            {
                if (!mod.IsMod(aa, indexAA, seq.Length))
                    continue;
                listItems.Add(mod.Name);

                // If the peptide is explicitly modified, then the explicit modifications
                // indicate the combo selections.
                if (listExplicitMods != null)
                {
                    if (Equals(explicitName, mod.Name))
                        iSelected = listItems.Count - 1;
                }
                // If it is not explicitly modified, or no modification was found in the
                // explicit set, and using the implicit modifications is allowed (variable mods)
                // check the implicit modifications for an applicable mod
                if (listExplicitMods == null || (selectEither && iSelected == -1))
                {
                    // If the modification is present in the document, then it should be selected by default.
                    StaticMod modCurrent = mod;
                    if (listDocMods != null && listDocMods.IndexOf(modDoc =>
                            !modDoc.IsExplicit && Equals(modDoc.Name, modCurrent.Name)) != -1)
                        iSelected = listItems.Count - 1;
                }
            }
            listItems.Add(Resources.SettingsListComboDriver_Add);
            listItems.Add(Resources.SettingsListComboDriver_Edit_list);
            if (!EqualsItems(combo, listItems))
            {
                combo.Items.Clear();
                listItems.ForEach(item => combo.Items.Add(item));
                // If not just the blank, add and edit items, make sure the drop-down is wid enough
                if (listItems.Count > 3)
                    ComboHelper.AutoSizeDropDown(combo);
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
            return (selectedItem != null && Resources.SettingsListComboDriver_Add == selectedItem.ToString());
        }

        private static bool EditListSelected(ComboBox combo)
        {
            var selectedItem = combo.SelectedItem;
            return (selectedItem != null && Resources.SettingsListComboDriver_Edit_list == combo.SelectedItem.ToString());
        }

        public void SelectModification(IsotopeLabelType labelType, int indexAA, string modName)
        {
            int row = indexAA + 1;
            string comboName = (labelType.IsLight ?
                GetStaticName(row) :
                GetHeavyName(row, labelType.SortOrder));

            foreach (var comboBox in _listComboStatic)
            {
                if (Equals(comboName, comboBox.Name))
                {
                    comboBox.SelectedItem = modName;
                    return;
                }
            }
            foreach (var listComboHeavy in _listListComboHeavy)
            {
                foreach (var comboBox in listComboHeavy)
                {
                    if (Equals(comboName, comboBox.Name))
                    {
                        comboBox.SelectedItem = modName;
                        return;
                    }
                }
            }
        }

        public void comboMod_SelectedIndexChangedEvent(object sender, EventArgs e)
        {
            ComboBox combo = (ComboBox) sender;
            ExplicitMods modsExp = ExplicitMods;
            int indexAA;
            if (combo.Name.StartsWith(PREFIX_HEAVY_NAME))
            {
                var matchName = REGEX_HEAVY_NAME.Match(combo.Name);
                indexAA = int.Parse(matchName.Groups[1].Value) - 1;
                int indexLabelType = int.Parse(matchName.Groups[2].Value) - 1;
                SelectedIndexChangedEvent(combo, HeavyList, modsExp != null ? modsExp.HeavyModifications : null,
                    _listListComboHeavy[indexLabelType], _listListSelectedIndexHeavy[indexLabelType], indexAA,
                    false);
            }
            else
            {
                indexAA = int.Parse(combo.Name.Substring(PREFIX_STATIC_NAME.Length)) - 1;
                SelectedIndexChangedEvent(combo, StaticList, modsExp != null ? modsExp.StaticModifications : null,
                    _listComboStatic, _listSelectedIndexStatic, indexAA,
                    modsExp != null && modsExp.IsVariableStaticMods);
            }
            UpdateAminoAcidLabel(indexAA);
        }

        private void UpdateAminoAcidLabel(int indexAA)
        {
            if (indexAA >= _listLabelAA.Count)
                return;

            FontStyle fontStyle = FontStyle.Regular;
            Color textColor = Color.Black;
            
            if (!string.IsNullOrEmpty((string) _listComboStatic[indexAA].SelectedItem))
            {
                fontStyle = FontStyle.Bold | FontStyle.Underline;
            }

            for (int i = 0; i < _listListComboHeavy.Count; i++)
            {
                if (!string.IsNullOrEmpty((string) _listListComboHeavy[i][indexAA].SelectedItem))
                {
                    fontStyle |= FontStyle.Bold;
                    textColor = ModFontHolder.GetModColor(_listLabelTypeHeavy[i]);
                    break;
                }
            }

            var label = _listLabelAA[indexAA];
            if (label.Font.Style != fontStyle)
            {
                label.Font = new Font(label.Font.Name, label.Font.SizeInPoints,
                    fontStyle, GraphicsUnit.Point, 0);
            }

// ReSharper disable RedundantCheckBeforeAssignment
            if (label.ForeColor != textColor)
            {
                label.ForeColor = textColor;
            }
// ReSharper restore RedundantCheckBeforeAssignment
        }

        private void SelectedIndexChangedEvent(ComboBox combo,
            SettingsList<StaticMod> listSettingsMods, IList<ExplicitMod> listExplicitMods,
            IList<ComboBox> listCombo, IList<int> listSelectedIndex, int indexAA,
            bool selectEither)
        {
            int selectedIndexLast = listSelectedIndex[indexAA];
            if (AddItemSelected(combo))
            {
                StaticMod itemNew = listSettingsMods.NewItem(this, null, null);
                if (!Equals(itemNew, null))
                {
                    listSettingsMods.Add(itemNew);
                    string itemAdd = (string) combo.SelectedItem;
                    LoadLists(listSettingsMods, listExplicitMods, listCombo, indexAA, itemNew.GetKey(),
                        selectEither);
                    // If the selection was not successfully set to the new modification,
                    // return to the previous selection.
                    if (Equals(combo.SelectedItem, itemAdd))
                        combo.SelectedIndex = selectedIndexLast;
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

                    string selectedItemLast = null;
                    if (selectedIndexLast != -1)
                        selectedItemLast = combo.Items[selectedIndexLast].ToString();
                    LoadLists(listSettingsMods, listExplicitMods, listCombo, indexAA, selectedItemLast,
                        selectEither);
                }
                else
                {
                    // Reset the selected index before edit was chosen.
                    combo.SelectedIndex = selectedIndexLast;
                }
            }
            listSelectedIndex[indexAA] = combo.SelectedIndex;
        }

        private void LoadLists(IList<StaticMod> listSettingsMods, IList<ExplicitMod> listExplicitMods,
            IList<ComboBox> listCombo, int indexAA, string selectedItem, bool selectEither)
        {
            for (int i = 0; i < listCombo.Count; i++)
            {
                ComboBox combo = listCombo[i];
                // Reset the combo to its current value, unless a different value was specified
                object selectedItemDesired = (i == indexAA ? selectedItem : combo.SelectedItem);
                UpdateComboItems(combo, listSettingsMods, listExplicitMods, null, i, selectEither, false);
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
            ResetMods();
        }

        public void ResetMods()
        {
            // CONSIDER: This means once a peptide with variable modifications is explicitly
            //           modified, it is then treated the same as an unmodified peptide for
            //           future resets.
            var explicitModsOrig = NodePeptide.ExplicitMods;
            if (!NodePeptide.HasVariableMods)
                ExplicitMods = null;
            else if (!explicitModsOrig.HasHeavyModifications)
                ExplicitMods = explicitModsOrig;
            else
            {
                // Construct a new explicit mods with only the variable modifications
                ExplicitMods = new ExplicitMods(NodePeptide.Peptide,
                                                explicitModsOrig.StaticModifications,
                                                new TypedExplicitModifications[0],
                                                true);
            }
            var modifications = DocSettings.PeptideSettings.Modifications;
            for (int i = 0; i < _listComboStatic.Count; i++)
            {
                UpdateComboItems(_listComboStatic[i],
                                 StaticList,
                                 ExplicitMods != null ? ExplicitMods.StaticModifications : null,
                                 modifications.StaticModifications,
                                 i,
                                 ExplicitMods != null && ExplicitMods.IsVariableStaticMods,
                                 true);
            }
            for (int i = 0; i < _listLabelTypeHeavy.Count; i++)
            {
                var labelType = _listLabelTypeHeavy[i];
                var listComboHeavy = _listListComboHeavy[i];
                for (int j = 0; j < listComboHeavy.Count; j++)
                {
                    UpdateComboItems(listComboHeavy[j], HeavyList, null,
                        modifications.GetModifications(labelType), j, false, true);
                }
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var peptide = NodePeptide.Peptide;
            var explicitModsCurrent = NodePeptide.ExplicitMods;
            var modsDoc = DocSettings.PeptideSettings.Modifications;
            var implicitMods = new ExplicitMods(NodePeptide,
                modsDoc.StaticModifications, Settings.Default.StaticModList,
                modsDoc.GetHeavyModifications(), Settings.Default.HeavyModList);
            
            // Get static modifications from the dialog, and check for equality with
            // the document implicit modifications.
            TypedExplicitModifications staticTypedMods = null;
            bool isVariableStaticMods = false;
            var staticMods = GetExplicitMods(_listComboStatic, Settings.Default.StaticModList);
            if (ArrayUtil.EqualsDeep(staticMods, implicitMods.StaticModifications))
            {
                if (!NodePeptide.HasVariableMods)
                    staticMods = null;  // Use implicit modifications                
                else
                {
                    staticMods = explicitModsCurrent.StaticModifications;
                    isVariableStaticMods = true;
                }
            }
            else if (explicitModsCurrent != null &&
                        ArrayUtil.EqualsDeep(staticMods, explicitModsCurrent.StaticModifications))
            {
                staticMods = explicitModsCurrent.StaticModifications;
            }
            if (staticMods != null)
            {
                staticTypedMods = new TypedExplicitModifications(peptide,
                    IsotopeLabelType.light, staticMods);
            }

            var listHeavyTypedMods = new List<TypedExplicitModifications>();
            for (int i = 0; i < _listLabelTypeHeavy.Count; i++)
            {
                var labelType = _listLabelTypeHeavy[i];
                var heavyMods = GetExplicitMods(_listListComboHeavy[i], Settings.Default.HeavyModList);

                if (ArrayUtil.EqualsDeep(heavyMods, implicitMods.GetModifications(labelType)))
                    continue;

                var heavyTypedMods = new TypedExplicitModifications(peptide, labelType, heavyMods);
                listHeavyTypedMods.Add(heavyTypedMods.AddModMasses(staticTypedMods));
            }

            ExplicitMods explicitMods = null;
            if (staticMods != null || listHeavyTypedMods.Count > 0)
                explicitMods = new ExplicitMods(peptide, staticMods, listHeavyTypedMods, isVariableStaticMods);
            Helpers.AssignIfEquals(ref explicitMods, explicitModsCurrent);
            ExplicitMods = explicitMods;

            DialogResult = DialogResult.OK;
            Close();            
        }
    }
}
