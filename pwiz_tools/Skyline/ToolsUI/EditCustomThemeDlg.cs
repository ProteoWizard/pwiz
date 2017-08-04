/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    public partial class EditCustomThemeDlg : FormEx
    {
        private ColorScheme _newScheme;
        private readonly ColorScheme _oldScheme;
        private readonly bool _isNewTheme;
        private readonly ImmutableList<Color> DefaultSingletonColor = ImmutableList<Color>.Singleton(Color.Gray);
        private readonly string DefaultName = string.Empty;
        private readonly IEnumerable<ColorScheme> _existing;
        private readonly BindingList<ColorRow> _colorRowBindingListTransition;
        private readonly BindingList<ColorRow> _colorRowBindingListPrecursors;
        private readonly string _formatColorCount;
        private bool _updatingBindingLists;

        public enum ThemeCategory {precursors, transitions}
        public EditCustomThemeDlg(ColorScheme scheme, IEnumerable<ColorScheme> existing)
        {
            InitializeComponent();
            _existing = existing;
            _formatColorCount = lableColorCount.Text;
            colorPickerDlg.FullOpen = true;
            _colorRowBindingListTransition = new BindingList<ColorRow>();
            _colorRowBindingListPrecursors = new BindingList<ColorRow>();
            colBtn.UseColumnTextForButtonValue = true;
            if (scheme == null) // Add
            {
                Settings.Default.ColorSchemes.TryGetValue(Settings.Default.CurrentColorScheme, out _oldScheme);
                _newScheme = new ColorScheme("blank").ChangeTransitionColors(DefaultSingletonColor).ChangePrecursorColors(DefaultSingletonColor); // Not L10N
                LoadCategory(_newScheme);
                textBoxName.Text = DefaultName;
            }
            else // Edit
            {
                _newScheme = scheme;
                _oldScheme = scheme;
                LoadCategory(_newScheme);
                textBoxName.Text = scheme.Name;
            }
            _isNewTheme = scheme == null;
            comboColorType.SelectedIndex = 0;
            comboBoxCategory.SelectedIndex = 0;
            bindingSource1.DataSource = GetCurrentBindingList();
            UpdateColorCount(_newScheme);
        }

        public void LoadCategory(ColorScheme colorScheme)
        {
            try
            {
                _updatingBindingLists = true;
                _colorRowBindingListPrecursors.Clear();
                foreach (var color in colorScheme.PrecursorColors)
                {
                    _colorRowBindingListPrecursors.Add(new ColorRow(color));
                }
                _colorRowBindingListTransition.Clear();
                foreach (var color in colorScheme.TransitionColors)
                {
                    _colorRowBindingListTransition.Add(new ColorRow(color));
                }
            }
            finally
            {
                _updatingBindingLists = false;
            }
        }

        public ColorScheme NewScheme
        {
            get
            {
                return _newScheme.ChangePrecursorColors(ColorListFromRows(_colorRowBindingListPrecursors))
                    .ChangeTransitionColors(ColorListFromRows(_colorRowBindingListTransition));
            }
        }

        private IList<Color> ColorListFromRows(IList<ColorRow> colorRows)
        {
            var colors = ImmutableList.ValueOf(colorRows.Select(row=>row.Color).Where(color => !color.IsEmpty));
            if (colors.Count == 0)
            {
                return DefaultSingletonColor;
            }
            return colors;
        }

        private static string GetRgb(Color color)
        {
            return String.Format("{0}, {1}, {2}", color.R, color.G, color.B); // Not L10N
        }
        private static string GetHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2"); // Not L10N
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == colBtn.Index)
            {
                var rowIndex = e.RowIndex;
                var oldColor = ((ColorRow)bindingSource1[rowIndex]).Color;
                colorPickerDlg.Color = oldColor;
                if (colorPickerDlg.ShowDialog() == DialogResult.OK)
                {
                    changeRowColor(rowIndex, colorPickerDlg.Color);
                }
            }
        }

        public void changeRowColor(int rowIndex, Color newColor)
        {
            if (rowIndex == dataGridViewColors.NewRowIndex)
            {
                bindingSource1.EndEdit();
                dataGridViewColors.NotifyCurrentCellDirty(true);
                dataGridViewColors.EndEdit();
                dataGridViewColors.NotifyCurrentCellDirty(false);
            }
            bindingSource1[rowIndex] = new ColorRow(newColor);
        }

        private static Color? ParseHtmlColor(string value)
        {
            if (value.Length == 6 || value.Length == 3)
                value = "#" + value; // Not L10N
            Color color;
            try
            {
                color = ColorTranslator.FromHtml(value);
            }
            catch
            {
                return null;
            }
            return color;
        }

        private static Color? ParseRgb(string value)
        {
            if (value == null)
            {
                return null;
            }
            else
            {
                var RGB = value.Split(',');
                if (RGB.Length != 3)
                    return null;
                else
                {
                    bool isValid = true;
                    foreach (var s in RGB)
                    {
                        try
                        {
                            var num = int.Parse(s);
                            if (num < 0 || num > 255)
                                isValid = false;
                        }
                        catch
                        {
                            isValid = false;
                        }
                    }
                    if (isValid)
                        return Color.FromArgb(int.Parse(RGB[0]), int.Parse(RGB[1]), int.Parse(RGB[2]));
                    else
                        return null;
                }
            }
        }

        private void comboColorType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboColorType.SelectedIndex == 0)
            {
                dataGridViewColors.Columns[hexCol.Index].Visible = false;
                dataGridViewColors.Columns[rgbCol.Index].Visible = true;
            }
            else
            {
                dataGridViewColors.Columns[rgbCol.Index].Visible = false;
                dataGridViewColors.Columns[hexCol.Index].Visible = true;
            }
        }

        private void comboBoxCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            bindingSource1.DataSource = GetCurrentBindingList();
        }

        private ThemeCategory GetCurrentCategory()
        {
            switch (comboBoxCategory.SelectedIndex)
            {
                case 0:
                    return ThemeCategory.transitions;
                case 1:
                    return ThemeCategory.precursors;
                default:
                    return ThemeCategory.transitions;
            }
            
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            var messageBoxHelper = new MessageBoxHelper(this);
            string name;
            if (!messageBoxHelper.ValidateNameTextBox(textBoxName, out name))
                return;

            if (_isNewTheme || name != _oldScheme.Name)
            {
                if (_existing.Any(s => s.Name == name))
                {
                    messageBoxHelper.ShowTextBoxError(textBoxName, Resources.EditCustomThemeDlg_buttonSave_Click_The_color_scheme___0___already_exists_, name);
                    return;
                }
            }
            _newScheme = _newScheme.ChangeName(name);
            DialogResult = DialogResult.OK;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            ColorScheme.ColorSchemeDemo = null;
            Program.MainWindow.UpdateGraphPanes();
        }

        public class ColorRow
        {
            public ColorRow()
            {
                Color = Color.Empty;
            }
            public ColorRow(Color color)
            {
                Color = color;
            }

            public Color Color { get; set; }

            public string Rgb
            {
                get
                {
                    if (Color == Color.Empty)
                    {
                        return string.Empty;
                    }
                    return GetRgb(Color);
                }
                set
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        Color = Color.Empty;
                        return;
                    }
                    
                    var newColor = ParseRgb(value);
                    if (newColor == null)
                    {
                        throw new FormatException();
                    }
                    Color = newColor.Value;
                }
            }

            public string Hex
            {
                get
                {
                    if (Color == Color.Empty)
                    {
                        return string.Empty;
                    }
                    return GetHex(Color);
                }
                set
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        Color = Color.Empty;
                        return;
                    }

                    var newColor = ParseHtmlColor(value);
                    if (!newColor.HasValue)
                    {
                        throw new FormatException();
                    }
                    Color = newColor.Value;
                }
            }
        }

        private void dataGridViewColors_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex != colorCol.Index)
                return;
            var currentBindings = GetCurrentBindingList();
            if (e.RowIndex >= 0 && e.RowIndex < currentBindings.Count)
            {
                var row = dataGridViewColors.Rows[e.RowIndex];
                var colorRow = currentBindings[e.RowIndex];
                var cell = row.Cells[e.ColumnIndex];
                cell.Style.SelectionBackColor = cell.Style.SelectionForeColor = cell.Style.BackColor = colorRow.Color;
            }
        }

        private BindingList<ColorRow> GetCurrentBindingList()
        {
            if (GetCurrentCategory() == ThemeCategory.precursors)
                return _colorRowBindingListPrecursors;
            else
                return _colorRowBindingListTransition;
        }

        private void dataGridViewColors_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.Exception is FormatException)
            {
                MessageDlg.Show(this, Resources.EditCustomThemeDlg_dataGridViewColors_DataError_Colors_must_be_entered_in_HEX_or_RGB_format_); 
            }            
        }

        private void bindingSource1_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (_updatingBindingLists)
            {
                return;
            }
            UpdateColorCount(NewScheme);
            ColorScheme.ColorSchemeDemo = NewScheme;
            Program.MainWindow.ChangeColorScheme();
        }

        private void UpdateColorCount(ColorScheme scheme)
        {
            if (scheme == null || scheme.PrecursorColors == null || scheme.TransitionColors == null)
                return;
            var category = GetCurrentCategory();
            var ct = 0;
            if (category == ThemeCategory.precursors)
                ct = scheme.PrecursorColors.Count;
            else if (category == ThemeCategory.transitions)
                ct = scheme.TransitionColors.Count;

            lableColorCount.Text = string.Format(_formatColorCount, ct);
        }

        private void dataGridViewColors_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.V && e.Modifiers == Keys.Control)
            {
                DoPaste();
                e.Handled = true;
            }
        }

        public void DoPaste()
        {
            var clipboardText = ClipboardHelper.GetClipboardText(this);
            if (clipboardText == null)
            {
                return;
            }
            using (var reader = new StringReader(clipboardText))
            {
                string line;
                while (null != (line = reader.ReadLine()))
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    var color = ParseHtmlColor(line) ?? ParseRgb(line);
                    if (color == null)
                    {
                        MessageDlg.Show(this, string.Format(Resources.EditCustomThemeDlg_DoPaste_Unable_to_parse_the_color___0____Use_HEX_or_RGB_format_, line));
                        return;
                    }
                    var colorRow = new ColorRow(color.Value);
                    bindingSource1.Insert(bindingSource1.Position, colorRow);
                }
            }
        }

        // Testing functions below
        public DataGridView getGrid()
        {
            return dataGridViewColors;
        }

        public void changeCateogry(ThemeCategory category)
        {
            if (category == ThemeCategory.transitions)
                comboBoxCategory.SelectedIndex = 0;
            if (category == ThemeCategory.precursors)
                comboBoxCategory.SelectedIndex = 1;
        }

        public void changeToHex() { comboColorType.SelectedIndex = 1; }
        public void changeToRGB() { comboColorType.SelectedIndex = 0; }

        public void save()
        {
            buttonSave_Click(null, null);
        }

        public void changeName(string name)
        {
            textBoxName.Text = name;
        }

        public void cancel() { buttonCancel.PerformClick(); }

        public void setBindingPosition(int index)
        {
            bindingSource1.Position = index;
        }
    }
}
