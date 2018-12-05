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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    public partial class EditCustomThemeDlg : FormEx, ColorGrid<RgbHexColor>.IColorGridOwner
    {
        private ColorScheme _newScheme;
        private readonly ColorScheme _oldScheme;
        private readonly bool _isNewTheme;
        private readonly ImmutableList<Color> DefaultSingletonColor = ImmutableList<Color>.Singleton(Color.Gray);
        private readonly string DefaultName = string.Empty;
        private readonly IEnumerable<ColorScheme> _existing;
        private readonly BindingList<RgbHexColor> _colorRowBindingListTransition;
        private readonly BindingList<RgbHexColor> _colorRowBindingListPrecursors;
        private readonly string _formatColorCount;
        private bool _updatingBindingLists;

        public enum ThemeCategory {precursors, transitions}
        public EditCustomThemeDlg(ColorScheme scheme, IEnumerable<ColorScheme> existing)
        {
            InitializeComponent();

            colorGrid1.Owner = this;
            colorGrid1.AllowUserToOrderColumns = true;
            colorGrid1.AllowUserToAddRows = true;

            _existing = existing;
            _formatColorCount = lableColorCount.Text;
            _colorRowBindingListTransition = new BindingList<RgbHexColor>();
            _colorRowBindingListPrecursors = new BindingList<RgbHexColor>();
            if (scheme == null) // Add
            {
                Settings.Default.ColorSchemes.TryGetValue(Settings.Default.CurrentColorScheme, out _oldScheme);
                _newScheme = new ColorScheme(@"blank").ChangeTransitionColors(DefaultSingletonColor).ChangePrecursorColors(DefaultSingletonColor);
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
            comboBoxCategory.SelectedIndex = 0;
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
                    _colorRowBindingListPrecursors.Add(new RgbHexColor(color));
                }
                _colorRowBindingListTransition.Clear();
                foreach (var color in colorScheme.TransitionColors)
                {
                    _colorRowBindingListTransition.Add(new RgbHexColor(color));
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

        private IList<Color> ColorListFromRows(IList<RgbHexColor> colorRows)
        {
            var colors = ImmutableList.ValueOf(colorRows.Select(row=>row.Color).Where(color => !color.IsEmpty));
            if (colors.Count == 0)
            {
                return DefaultSingletonColor;
            }
            return colors;
        }

        private void comboBoxCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            colorGrid1.UpdateBindingSource();
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

        BindingList<RgbHexColor> ColorGrid<RgbHexColor>.IColorGridOwner.GetCurrentBindingList()
        {
            if (GetCurrentCategory() == ThemeCategory.precursors)
                return _colorRowBindingListPrecursors;
            else
                return _colorRowBindingListTransition;
        }

        public void OnListChanged(object sender, ListChangedEventArgs e)
        {
            if (_updatingBindingLists)
            {
                return;
            }
            UpdateColorCount(NewScheme);
            ColorScheme.ColorSchemeDemo = NewScheme;
            Program.MainWindow.ChangeColorScheme();
        }

        // Testing functions below
        public DataGridView getGrid()
        {
            return colorGrid1.GetGrid();
        }

        public void DoPaste()
        {
            colorGrid1.DoPaste();
        }

        public void changeRowColor(int rowIndex, Color newColor)
        {
            colorGrid1.changeRowColor(rowIndex, newColor);
        }

        public void changeCateogry(ThemeCategory category)
        {
            if (category == ThemeCategory.transitions)
                comboBoxCategory.SelectedIndex = 0;
            if (category == ThemeCategory.precursors)
                comboBoxCategory.SelectedIndex = 1;
        }

        public void changeToHex() { colorGrid1.ChangeToHex(); }
        public void changeToRGB() { colorGrid1.ChangeToRGB(); }

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
            colorGrid1.SetBindingPosition(index);
        }
    }
}
