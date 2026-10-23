/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Edits the list of Carafe models as a grid of what each was trained on (instrument,
    /// gradient, precursor m/z, NCE, fragmentation, training runs, held-out scores, date), so a user
    /// can sort and compare them to pick the one matching the library's instrument and method.
    /// The list editor behind the model combo's "Edit list..." in Build Library, in place of the
    /// generic name list, with the same Add, Edit, Remove, Up and Down.
    /// </summary>
    public partial class CarafeModelListDlg : FormEx
    {
        private readonly CarafeModelList _model;
        private readonly List<CarafeModelSpec> _list;

        public CarafeModelListDlg(CarafeModelList model)
        {
            _model = model;
            // The default (the pretrained models) is not edited here
            _list = model.Skip(model.ExcludeDefaults).ToList();

            InitializeComponent();

            Icon = Resources.Skyline;
            ReloadGrid(_list.FirstOrDefault());
        }

        /// <summary>The models the grid lists, in list order.</summary>
        public IList<CarafeModelSpec> Models
        {
            get { return _list.ToList(); }
        }

        /// <summary>The edited list, without the defaults, as the list editor contract returns it.</summary>
        public IEnumerable<CarafeModelSpec> GetAllEdited()
        {
            return _list;
        }

        public CarafeModelSpec SelectedModel
        {
            get { return gridModels.CurrentRow is { Selected: true } row ? (CarafeModelSpec)row.Tag : null; }
        }

        private int SelectedIndex
        {
            get
            {
                var selected = SelectedModel;
                return selected != null ? _list.IndexOf(selected) : -1;
            }
        }

        private IEnumerable<CarafeModelSpec> GetAll()
        {
            return _model.GetDefaults().Take(_model.ExcludeDefaults).Concat(_list);
        }

        /// <summary>
        /// Fills the grid from the list, reading each model's file for its columns; a file that is
        /// missing or unreadable leaves them blank and shows its name in gray.
        /// </summary>
        private void ReloadGrid(CarafeModelSpec selected)
        {
            gridModels.Rows.Clear();
            foreach (var spec in _list)
            {
                var choice = CarafeModelChoice.Read(spec.FilePath);
                int rowIndex = gridModels.Rows.Add(spec.Name, choice?.Instrument, choice?.GradientMinutes, choice?.PrecursorMzRange,
                    choice?.Nce, choice?.Fragmentation, choice?.TrainingRunCount, choice?.Ms2Cosine, choice?.RtR2, choice?.Saved);
                var row = gridModels.Rows[rowIndex];
                row.Tag = spec;
                row.Cells[colName.Index].ToolTipText = spec.FilePath;
                if (choice == null)
                    row.Cells[colName.Index].Style.ForeColor = SystemColors.GrayText;
            }
            SelectModel(selected);
        }

        private void SelectModel(CarafeModelSpec spec)
        {
            var row = gridModels.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => Equals(r.Tag, spec));
            if (row != null)
            {
                gridModels.CurrentCell = row.Cells[0];
                // Already the current cell after a ClearSelection, which setting it does not undo
                row.Selected = true;
            }
            else
            {
                gridModels.ClearSelection();
            }
            UpdateButtons();
        }

        public void SelectItem(string name)
        {
            SelectModel(_list.FirstOrDefault(spec => Equals(spec.Name, name)));
        }

        private void UpdateButtons()
        {
            int i = SelectedIndex;
            bool enable = i != -1;
            btnEdit.Enabled = enable;
            btnRemove.Enabled = enable;
            btnUp.Enabled = i > 0;
            btnDown.Enabled = enable && i < _list.Count - 1;
        }

        private void gridModels_SelectionChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void gridModels_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                EditItem();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddItem();
        }

        public void AddItem()
        {
            var item = _model.NewItem(this, GetAll(), null);
            if (item != null)
            {
                // Insert after the current selection
                _list.Insert(SelectedIndex + 1, item);
                ReloadGrid(item);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditItem();
        }

        public void EditItem()
        {
            int i = SelectedIndex;
            if (i == -1)
                return;
            var item = _model.EditItem(this, _list[i], GetAll(), null);
            if (item != null)
            {
                _list[i] = item;
                ReloadGrid(item);
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            RemoveItem();
        }

        public void RemoveItem()
        {
            int i = SelectedIndex;
            if (i == -1)
                return;
            _list.RemoveAt(i);
            ReloadGrid(_list.Count > 0 ? _list[Math.Min(i, _list.Count - 1)] : null);
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            MoveItemUp();
        }

        public void MoveItemUp()
        {
            MoveItem(-1);
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            MoveItemDown();
        }

        public void MoveItemDown()
        {
            MoveItem(1);
        }

        private void MoveItem(int step)
        {
            int i = SelectedIndex;
            int j = i + step;
            if (i == -1 || j < 0 || j >= _list.Count)
                return;
            var item = _list[i];
            _list[i] = _list[j];
            _list[j] = item;
            ReloadGrid(item);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
