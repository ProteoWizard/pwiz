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
using pwiz.Common.Controls;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class EditPepModsDlg : FormEx
    {
        private const int VSPACE = 3;
        private const int HSPACE = 10;

        private const string PREFIX_STATIC_NAME = "comboStatic";
        private const string PREFIX_HEAVY_NAME = "comboHeavy";

        private const string PREFIX_LABEL_NAME = "labelHeavy1";

        private static readonly Regex REGEX_HEAVY_NAME = new Regex(PREFIX_HEAVY_NAME + @"(\d+)_(\d+)");

        private readonly List<LiteDropDownList> _listComboStatic = new List<LiteDropDownList>();
        private readonly List<int> _listSelectedIndexStatic = new List<int>();
        private readonly List<Label> _listLabelAA = new List<Label>();
        private readonly List<IsotopeLabelType> _listLabelTypeHeavy = new List<IsotopeLabelType>();
        private readonly List<List<LiteDropDownList>> _listListComboHeavy = new List<List<LiteDropDownList>>();
        private readonly List<List<int>> _listListSelectedIndexHeavy = new List<List<int>>();
        private readonly List<Button> _listEditLinkButtons = new List<Button>();

        public static string GetStaticName(int row)
        {
            return string.Format(@"{0}{1}", PREFIX_STATIC_NAME, row);
        }

        public static string GetEditLinkName(int row)
        {
            return string.Format(@"{0}{1}", @"btnEditLink", row);
        }

        public static string GetHeavyName(int row, int col)
        {
            return string.Format(@"{0}{1}_{2}", PREFIX_HEAVY_NAME, row, col);
        }

        private static string GetIsotopeLabelName(int col)
        {
            return string.Format(@"{0}{1}", PREFIX_LABEL_NAME, col);
        }

        private static string GetIsotopeLabelText(IsotopeLabelType labelType)
        {
            return string.Format(Resources.EditPepModsDlg_GetIsotopeLabelText_Isotope__0__, labelType);
        }

        public EditPepModsDlg(SrmSettings settings, PeptideDocNode nodePeptide, bool allowCopy)
        {
            InitializeComponent();
            Icon = Resources.Skyline;

            DocSettings = settings;
            NodePeptide = nodePeptide;
            ExplicitMods = nodePeptide.ExplicitMods;
            AllowCopy = allowCopy;
            if (!AllowCopy)
            {
                cbCreateCopy.Visible = false;
            }

            SuspendLayout();
            LiteDropDownList comboStaticLast = null;
            Button btnEditLinkLast = null;
            List<LiteDropDownList> listComboHeavyLast = null;
            List<Label> listLabelHeavyLast = null;
            Label labelAALast = null;
            var seq = nodePeptide.Peptide.Target.Sequence;
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
                    btnEditLinkLast = btnEditLink1;
                    foreach (var labelType in _listLabelTypeHeavy)
                    {
                        if (listComboHeavyLast == null)
                        {
                            listComboHeavyLast = new List<LiteDropDownList> { comboHeavy1_1 };
                            listLabelHeavyLast = new List<Label> { labelHeavy1 };
                            labelHeavy1.Text = GetIsotopeLabelText(labelType);
                        }
                        else
                        {
                            var comboHeavyLast = listComboHeavyLast[listComboHeavyLast.Count - 1];
                            panelMain.Controls.Add(comboHeavyLast = new LiteDropDownList
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
                    int controlsPerRow = 3 + listComboHeavyLast.Count;
                    int top = Top = comboStaticLast.Bottom + VSPACE;
                    panelMain.Controls.Add(labelAALast = new Label
                    {
                        Name = (@"labelAA" + row),
                        AutoSize = true,
                        Font = labelAA1.Font,
                        Left = labelAA1.Left,
                        Top = top + (labelAALast.Top - comboStaticLast.Top),
                        Size = labelAA1.Size,
                        TabIndex = labelAALast.TabIndex + controlsPerRow
                    });
                    panelMain.Controls.Add(comboStaticLast = new LiteDropDownList
                    {
                        Name = GetStaticName(row),
                        Left = comboStaticLast.Left,
                        Top = top,
                        Size = comboStaticLast.Size,
                        TabIndex = comboStaticLast.TabIndex + controlsPerRow
                    });
                    panelMain.Controls.Add(btnEditLinkLast = new Button
                    {
                        Name = GetEditLinkName(row),
                        Left = btnEditLinkLast.Left,
                        Top = top - 1,
                        Size = btnEditLinkLast.Size,
                        TabIndex = btnEditLinkLast.TabIndex + controlsPerRow,
                        Image = btnEditLinkLast.Image,
                        Enabled = AllowEditCrosslinks
                    });
                    foreach (var labelType in _listLabelTypeHeavy)
                    {
                        int col = labelType.SortOrder - 1;
                        var comboHeavyLast = listComboHeavyLast[col];
                        panelMain.Controls.Add(comboHeavyLast = new LiteDropDownList
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

                {
                    int indexAA = i;
                    btnEditLinkLast.Click += (sender, args) => EditLinkedPeptide(null, indexAA);
                }
                _listEditLinkButtons.Add(btnEditLinkLast);
                // Store static modification combos and selected indexes
                _listSelectedIndexStatic.Add(-1);
                _listComboStatic.Add(comboStaticLast);
                InitModificationCombo(IsotopeLabelType.light, i);
                // Store heavy modification combos and selected indexes
                if (listComboHeavyLast != null)   // ReSharper
                {
                    for (int j = 0; j < _listLabelTypeHeavy.Count; j++)
                    {
                        while (_listListComboHeavy.Count <= j)
                        {
                            _listListSelectedIndexHeavy.Add(new List<int>());
                            _listListComboHeavy.Add(new List<LiteDropDownList>());
                        }
                        var comboHeavyLast = listComboHeavyLast[j];
                        var labelType = _listLabelTypeHeavy[j];

                        _listListSelectedIndexHeavy[j].Add(-1);
                        _listListComboHeavy[j].Add(comboHeavyLast);
                        InitModificationCombo(labelType, i);
                    }
                }
                // Store amino acid labels
                labelAALast.Text = aa.ToString(CultureInfo.InvariantCulture);
                _listLabelAA.Add(labelAALast);
            }
            for (int i = 0; i < _listLabelAA.Count; i++)
            {
                UpdateAminoAcidLabel(i);
                UpdateEditLinkButton(i);
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

        public int SequenceLength
        {
            get { return NodePeptide.Peptide.Length; }
        }

        public CrosslinkStructure CrosslinkStructure
        {
            get { return ExplicitMods?.CrosslinkStructure ?? CrosslinkStructure.EMPTY; }
        }

        public bool AllowCopy { get; private set; }

        public bool AllowEditCrosslinks { get { return AllowCopy; } }

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
            LiteDropDownList combo;
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
        {
            LiteDropDownList combo;
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

        private void InitModificationCombo(IsotopeLabelType type, int indexAA)
        {
            var combo = GetComboBox(type, indexAA);
            int iSelected = UpdateComboItems(type, indexAA, false);
            // Add event handler before changing selection, so that the handler will fire
            combo.SelectedIndexChanged += comboMod_SelectedIndexChangedEvent;
            // Change selection, and fire event handler
            combo.SelectedIndex = iSelected;
        }

        public LiteDropDownList GetComboBox(IsotopeLabelType labelType, int indexAA)
        {
            return GetComboBoxList(labelType)[indexAA];
        }

        public IList<LiteDropDownList> GetComboBoxList(IsotopeLabelType labelType)
        {
            if (labelType.IsLight)
            {
                return _listComboStatic;
            }

            return _listListComboHeavy[_listLabelTypeHeavy.IndexOf(labelType)];
        }

        public SettingsList<StaticMod> GetSettingsModsList(IsotopeLabelType labelType)
        {
            return labelType.IsLight ? (SettingsList<StaticMod>) StaticList : HeavyList;
        }

        private int UpdateComboItems(IsotopeLabelType labelType, int indexAA, bool select)
        {
            bool selectEither = labelType.IsLight && ExplicitMods != null && ExplicitMods.IsVariableStaticMods;
            IEnumerable<StaticMod> listSettingsMods;
            IList<StaticMod> listDocMods;
            if (labelType.IsLight)
            {
                listSettingsMods = StaticList;
                listDocMods = DocSettings.PeptideSettings.Modifications.StaticModifications;
            }
            else
            {
                listSettingsMods = HeavyList;
                listDocMods = DocSettings.PeptideSettings.Modifications.GetModifications(labelType);
            }
            string seq = NodePeptide.Peptide.Target.Sequence;
            int iSelected = -1;
            string explicitName = null;
            var listExplicitMods = ExplicitMods?.GetModifications(labelType);
            bool crosslinkSelected = false;
            if (listExplicitMods != null)
            {
                int indexMod = listExplicitMods.IndexOf(mod => mod.IndexAA == indexAA);
                if (indexMod != -1)
                    explicitName = listExplicitMods[indexMod].Modification.Name;
                if (explicitName == null)
                {
                    explicitName = ExplicitMods.CrosslinkStructure.Crosslinks
                        .FirstOrDefault(crosslink => crosslink.Sites.Contains(new CrosslinkSite(0, indexAA)))
                        ?.Crosslinker.Name;
                    if (explicitName != null)
                    {
                        crosslinkSelected = true;
                    }
                }
            }

            List<string> listItems = new List<string> {string.Empty};
            foreach (StaticMod mod in listSettingsMods)
            {
                if (!mod.IsApplicableMod(seq, indexAA) && !mod.IsApplicableCrosslink(seq, indexAA))
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
                if ((listExplicitMods == null || selectEither && iSelected == -1) && !mod.IsCrosslinker)
                {
                    StaticMod modCurrent = mod;
                    // If the modification is present in the document, then it should be selected by default.
                    if (listDocMods != null && listDocMods.IndexOf(modDoc =>
                        !modDoc.IsExplicit && Equals(modDoc.Name, modCurrent.Name)) != -1)
                        iSelected = listItems.Count - 1;
                }
            }

            if (AllowCopy)
            {
                listItems.Add(Resources.SettingsListComboDriver_Add);
                listItems.Add(Resources.SettingsListComboDriver_Edit_current);
                listItems.Add(Resources.SettingsListComboDriver_Edit_list);
            }

            var combo = GetComboBox(labelType, indexAA);
            if (!EqualsItems(combo, listItems))
            {
                combo.Items.Clear();
                listItems.ForEach(item => combo.Items.Add(item));
            }
            if (select)
                combo.SelectedIndex = iSelected;
            combo.Enabled = AllowEditCrosslinks || !crosslinkSelected;
            return iSelected;
        }

        private static bool EqualsItems(LiteDropDownList combo, IList<string> listItems)
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

        private static bool AddItemSelected(LiteDropDownList combo)
        {
            var selectedItem = combo.SelectedItem;
            return (selectedItem != null && Resources.SettingsListComboDriver_Add == selectedItem.ToString());
        }

        private static bool EditCurrentSelected(LiteDropDownList combo)
        {
            var selectedItem = combo.SelectedItem;
            return (selectedItem != null && Resources.SettingsListComboDriver_Edit_current == combo.SelectedItem.ToString());
        }

        private static bool EditListSelected(LiteDropDownList combo)
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
                    comboBox.Focus();
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
                        comboBox.Focus();
                        comboBox.SelectedItem = modName;
                        return;
                    }
                }
            }
        }

        public void comboMod_SelectedIndexChangedEvent(object sender, EventArgs e)
        {
            var combo = (LiteDropDownList) sender;
            int indexAA;
            IsotopeLabelType labelType;
            if (combo.Name.StartsWith(PREFIX_HEAVY_NAME))
            {
                var matchName = REGEX_HEAVY_NAME.Match(combo.Name);
                indexAA = int.Parse(matchName.Groups[1].Value) - 1;
                int indexLabelType = int.Parse(matchName.Groups[2].Value) - 1;
                labelType = _listLabelTypeHeavy[indexLabelType];
            }
            else
            {
                indexAA = int.Parse(combo.Name.Substring(PREFIX_STATIC_NAME.Length)) - 1;
                labelType = IsotopeLabelType.light;
            }
            SelectedIndexChangedEvent(labelType, indexAA);
            if (labelType.IsLight)
            {
                UpdateEditLinkButton(indexAA);
            }
            UpdateAminoAcidLabel(indexAA);
        }

        private void UpdateAminoAcidLabel(int indexAA)
        {
            if (indexAA >= _listLabelAA.Count)
                return;

            FontStyle fontStyle = FontStyle.Regular;
            Color textColor = Color.Black;

            string lightModName = (string) _listComboStatic[indexAA].SelectedItem;
            if (!string.IsNullOrEmpty(lightModName))
            {
                var lightMod = StaticList[lightModName];
                if (lightMod != null && lightMod.HasMod)    // Avoid highlighting loss-only mods
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

        private void SelectedIndexChangedEvent(
            IsotopeLabelType labelType,
            int indexAA)
        {
            var listSelectedIndex = labelType.IsLight
                ? _listSelectedIndexStatic
                : _listListSelectedIndexHeavy[_listLabelTypeHeavy.IndexOf(labelType)];
            int selectedIndexLast = listSelectedIndex[indexAA];
            var listSettingsMods = GetSettingsModsList(labelType);
            var combo = GetComboBox(labelType, indexAA);
            if (AddItemSelected(combo))
            {
                StaticMod itemNew = listSettingsMods.NewItem(this, null, null);
                if (!Equals(itemNew, null))
                {
                    listSettingsMods.Add(itemNew);
                    string itemAdd = (string) combo.SelectedItem;
                    LoadLists(labelType, indexAA, itemNew.GetKey());
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
            else if (EditCurrentSelected(combo))
            {
                StaticMod itemEdit;
                if (listSettingsMods.TryGetValue((string) combo.Items[selectedIndexLast], out itemEdit))
                {
                    StaticMod itemNew = listSettingsMods.EditItem(this, itemEdit, listSettingsMods, null);
                    if (!Equals(itemNew, null))
                    {
                        int i = listSettingsMods.IndexOf(itemEdit);
                        listSettingsMods[i] = itemNew;
                        LoadLists(labelType, indexAA, itemNew.GetKey());
                    }
                }
                combo.SelectedIndex = selectedIndexLast;
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
                    LoadLists(labelType, indexAA, selectedItemLast);
                }
                else
                {
                    // Reset the selected index before edit was chosen.
                    combo.SelectedIndex = selectedIndexLast;
                }
            }
            else
            {
                string modName = (string)combo.SelectedItem;
                StaticMod staticMod;
                if (labelType.IsLight && !string.IsNullOrEmpty(modName) &&
                    listSettingsMods.TryGetValue(modName, out staticMod))
                {
                    if (!EnsureLinkedPeptide(staticMod, indexAA))
                    {
                        combo.SelectedIndex = selectedIndexLast;
                    }
                }
            }
            listSelectedIndex[indexAA] = combo.SelectedIndex;
        }

        private void LoadLists(IsotopeLabelType labelType, int indexAA, string selectedItem)
        {
            var listCombo = GetComboBoxList(labelType);
            for (int i = 0; i < listCombo.Count; i++)
            {
                LiteDropDownList combo = listCombo[i];
                // Reset the combo to its current value, unless a different value was specified
                object selectedItemDesired = (i == indexAA ? selectedItem : combo.SelectedItem);
                UpdateComboItems(labelType, i, false);
                if (!Equals(selectedItemDesired, combo.SelectedItem))
                    combo.SelectedItem = selectedItemDesired;
            }
        }

        public Crosslink FindCrosslinkAtAminoAcid(int indexAa)
        {
            return CrosslinkStructure.Crosslinks.FirstOrDefault(crosslink => crosslink.Sites
                .Any(site => site.PeptideIndex == 0 && site.AaIndex == indexAa));
        }

        public ExplicitMod GetChosenMod(IsotopeLabelType label, int indexAa)
        {
            return GetChosenMods(label).FirstOrDefault(mod => mod.IndexAA == indexAa);
        }

        public IList<ExplicitMod> GetChosenMods(IsotopeLabelType labelType)
        {
            if (IsotopeLabelType.light.Equals(labelType))
            {
                return GetExplicitMods(_listComboStatic, StaticList);
            }
            else
            {
                return GetExplicitMods(_listListComboHeavy[_listLabelTypeHeavy.IndexOf(labelType)], HeavyList);
            }
        }

        private static IList<ExplicitMod> GetExplicitMods(IList<LiteDropDownList> mods, 
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
            for (int i = 0; i < _listComboStatic.Count; i++)
            {
                UpdateComboItems(IsotopeLabelType.light, i, true);
            }
            for (int i = 0; i < _listLabelTypeHeavy.Count; i++)
            {
                var labelType = _listLabelTypeHeavy[i];
                var listComboHeavy = _listListComboHeavy[i];
                for (int j = 0; j < listComboHeavy.Count; j++)
                {
                    UpdateComboItems(labelType, j, true);
                }
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public ExplicitMods GetCurrentExplicitMods()
        {
            var peptide = NodePeptide.Peptide;
            var explicitModsCurrent = NodePeptide.ExplicitMods;
            var modsDoc = DocSettings.PeptideSettings.Modifications;
            var implicitMods = new ExplicitMods(NodePeptide,
                modsDoc.StaticModifications, StaticList,
                modsDoc.GetHeavyModifications(), HeavyList);

            // Get static modifications from the dialog, and check for equality with
            // the document implicit modifications.
            TypedExplicitModifications staticTypedMods = null;
            bool isVariableStaticMods = false;
            var staticMods =  GetExplicitMods(_listComboStatic, StaticList);
            staticMods = staticMods.Where(mod=>null == mod.Modification.CrosslinkerSettings).ToList();
            if (ArrayUtil.EqualsDeep(staticMods, implicitMods.StaticModifications) && !CrosslinkStructure.HasCrosslinks)
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
                var heavyMods = GetExplicitMods(_listListComboHeavy[i], HeavyList);

                if (ArrayUtil.EqualsDeep(heavyMods, implicitMods.GetModifications(labelType)))
                    continue;

                var heavyTypedMods = new TypedExplicitModifications(peptide, labelType, heavyMods);
                listHeavyTypedMods.Add(heavyTypedMods.AddModMasses(staticTypedMods));
            }

            ExplicitMods explicitMods = null;
            if (staticMods != null || listHeavyTypedMods.Count > 0 || !CrosslinkStructure.IsEmpty)
            {
                explicitMods = new ExplicitMods(peptide, staticMods, listHeavyTypedMods, isVariableStaticMods)
                    .ChangeCrosslinkStructure(CrosslinkStructure);
            }

            if (Equals(explicitMods, explicitModsCurrent))
            {
                return explicitModsCurrent;
            }

            return explicitMods;
        }

        public void OkDialog()
        {
            ExplicitMods = GetCurrentExplicitMods();
            if (ExplicitMods != null && !ExplicitMods.CrosslinkStructure.IsConnected())
            {
                MessageDlg.Show(this, Resources.EditPepModsDlg_OkDialog_One_or_more_of_the_crosslinked_peptides_are_no_longer_attached_to_this_peptide__);
                ShowEditLinkedPeptidesDlg(null, null);
            }

            DialogResult = DialogResult.OK;
            Close();            
        }

        public void EditLinkedPeptide(StaticMod mod, int indexAA)
        {
            if (mod == null)
            {
                mod = GetChosenMod(IsotopeLabelType.light, indexAA)?.Modification;
            }

            if (mod?.CrosslinkerSettings == null)
            {
                return;
            }
            ShowEditLinkedPeptidesDlg(mod, indexAA);
        }
        public void ShowEditLinkedPeptidesDlg(StaticMod mod, int? indexAa) {

            using (var editLinkedPeptidesDlg = new EditLinkedPeptidesDlg(DocSettings, new PeptideStructure(NodePeptide.Peptide, GetCurrentExplicitMods())))
            {
                if (indexAa.HasValue)
                {
                    editLinkedPeptidesDlg.SelectCrosslink(mod, 0, indexAa.Value);
                }
                if (editLinkedPeptidesDlg.ShowDialog(this) == DialogResult.OK)
                {
                    ExplicitMods = editLinkedPeptidesDlg.ExplicitMods;
                    UpdateComboBoxes(IsotopeLabelType.light);
                }
            }
        }

        private void UpdateComboBoxes(IsotopeLabelType labelType)
        {
            for (int i = 0; i < SequenceLength; i++)
            {
                UpdateComboItems(IsotopeLabelType.light, i, true);
            }
        }

        private void UpdateEditLinkButton(int indexAA)
        {
            var crosslink = FindCrosslinkAtAminoAcid(indexAA);
            var editLinkButton = _listEditLinkButtons[indexAA];
            if (crosslink == null)
            {
                editLinkButton.Visible = false;
                return;
            }
            editLinkButton.Visible = true;
            toolTip.SetToolTip(editLinkButton, GetTooltip(crosslink, indexAA));
        }

        private string GetTooltip(Crosslink crosslink, int indexAa)
        {
            var nullableOtherSite = crosslink.Sites.Where(site => site.PeptideIndex != 0 || site.AaIndex != indexAa)
                .Cast<CrosslinkSite?>().FirstOrDefault();
            if (!nullableOtherSite.HasValue)
            {
                return string.Format(Resources.EditPepModsDlg_GetTooltip_Invalid_crosslink___0_, crosslink);
            }

            var otherSite = nullableOtherSite.Value;
            Peptide peptide;
            if (otherSite.PeptideIndex == 0)
            {
                peptide = NodePeptide.Peptide;
                return string.Format(Resources.EditPepModsDlg_GetTooltip_Looplink___0____1__, peptide.Sequence[otherSite.AaIndex], otherSite.AaIndex + 1);
            }
            else
            {
                peptide = CrosslinkStructure.LinkedPeptides[otherSite.PeptideIndex - 1];
                return string.Format(Resources.EditPepModsDlg_GetTooltip_Crosslink_to__0____1____2__, peptide.Sequence,
                    peptide.Sequence[otherSite.AaIndex], otherSite.AaIndex + 1);
            }
        }

        private bool EnsureLinkedPeptide(StaticMod staticMod, int indexAA)
        {
            if (!IsHandleCreated)
            {
                // Combo boxes are still be constructed and added to the form.
                return true;
            }
            if (staticMod.CrosslinkerSettings == null)
            {
                var newCrosslinkStructure = CrosslinkStructure.RemoveCrosslinksAtSite(new CrosslinkSite(0, indexAA));
                if (Equals(newCrosslinkStructure, CrosslinkStructure))
                {
                    return true;
                }

                if (newCrosslinkStructure.IsConnected())
                {
                    ChangeCrosslinkStructure(newCrosslinkStructure);
                    return true;
                }

                switch (MultiButtonMsgDlg.Show(this,
                    Resources.EditPepModsDlg_EnsureLinkedPeptide_Discard_or_edit_disconnected_crosslinks,
                    Resources.EditPepModsDlg_EnsureLinkedPeptide_ButtonText_Edit_Crosslinks, Resources.EditPepModsDlg_EnsureLinkedPeptide_ButtonText_Discard, true))
                {
                    case DialogResult.Cancel:
                        return false;
                    case DialogResult.Yes:
                        ShowEditLinkedPeptidesDlg(null, null);
                        return true;
                    case DialogResult.No:
                        ChangeCrosslinkStructure(newCrosslinkStructure.RemoveDisconnectedPeptides());
                        return true;
                }
            }
            if (HasAppropriateLinkedPeptide(staticMod, indexAA))
            {
                return true;
            }
            EditLinkedPeptide(staticMod, indexAA);
            return HasAppropriateLinkedPeptide(staticMod, indexAA);
        }

        private void ChangeCrosslinkStructure(CrosslinkStructure newCrosslinkStructure)
        {
            ExplicitMods = GetCurrentExplicitMods().ChangeCrosslinkStructure(newCrosslinkStructure);
            UpdateComboBoxes(IsotopeLabelType.light);
        }

        private bool HasAppropriateLinkedPeptide(StaticMod staticMod, int indexAA)
        {
            var crosslink = FindCrosslinkAtAminoAcid(indexAA);
            if (crosslink == null)
            {
                return staticMod.CrosslinkerSettings == null;
            }

            if (Equals(crosslink.Crosslinker.Name, staticMod.Name))
            {
                return true;
            }

            return false;
        }
    }
}
