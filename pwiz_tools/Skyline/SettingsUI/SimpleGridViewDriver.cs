/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;
using Type = System.Type;

namespace pwiz.Skyline.SettingsUI
{
    public abstract class SimpleGridViewDriver<TItem>
    {
        private readonly DataGridViewEx _gridView;

        protected SimpleGridViewDriver(DataGridViewEx gridView, BindingSource bindingSource, SortableBindingList<TItem> items)
        {
            _gridView = gridView;
            _gridView.DataGridViewKey += gridView_KeyDown;

            Items = items;
            bindingSource.DataSource = items;

            _requiredColumns = new HashSet<int>();
        }

        protected DataGridView GridView { get { return _gridView; } }

        protected Control MessageParent { get { return FormEx.GetParentForm(GridView); } }

        private HashSet<int> _requiredColumns;

        /// <summary>
        /// Handles "peptide" -> "molecule" translation as required by current UI mode
        /// </summary>
        protected Helpers.ModeUIAwareFormHelper ModeUIHelper
        {
            get
            {
                var formex = MessageParent as FormEx;
                return formex?.GetModeUIHelper() ?? Helpers.ModeUIAwareFormHelper.DEFAULT;
            }
        }

        public SortableBindingList<TItem> Items { get; private set; }

        public void Populate(IEnumerable<TItem> values)
        {
            Items.RaiseListChangedEvents = false;
            try
            {
                Items.Clear();
                foreach (var value in values)
                {
                    Items.Add(value);
                }
            }
            finally
            {
                Items.RaiseListChangedEvents = true;
            }
            Items.ResetBindings();
        }

        private void gridView_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = HandleKeyDown(e.KeyCode, e.Control);
        }

        public bool HandleKeyDown(Keys keys, bool controlDown = false)
        {
            // Handle delete keys for single cell
            if (_gridView.SelectedRows.Count == 0 && (keys == Keys.Delete || keys == Keys.Back))
            {
                var cell = _gridView.CurrentCell;
                if (cell == null || cell.ReadOnly)
                    return false;

                if (_requiredColumns.Contains(cell.ColumnIndex))
                {
                    var editMode = _gridView.IsCurrentCellInEditMode;
                    if (_gridView.BeginEdit(false) && _gridView.EditingControl is TextBox textbox)
                    {
                        textbox.Clear();
                        return true;
                    }

                    if (!editMode)
                        _gridView.CancelEdit();
                }
                SetCellValue(string.Empty);
                return true;
            }

            // Handle Ctrl + V for paste
            else if (keys == Keys.V && controlDown && !_gridView.IsCurrentCellInEditMode)
            {
                OnPaste();
                return true;
            }

            else if (keys == Keys.Escape)
            {
                if (_gridView.IsCurrentCellInEditMode || _gridView.IsCurrentRowDirty)
                {
                    _gridView.CancelEdit();
                    _gridView.EndEdit();
                    return true;
                }
            }
            return false;
        }

        public void OnPaste()
        {
            DoPaste();
        }

        protected abstract void DoPaste();

        public bool IsColumnVisible(int col)
        {
            return _gridView.Columns[col].Visible;
        }

        public string GetCellFormattedValue(int col, int row)
        {
            return _gridView[col, row]?.FormattedValue?.ToString();
        }

        public string GetCellValue(int col, int row)
        {
            var cellValue = _gridView[col, row].Value;
            return (cellValue != null) ? cellValue.ToString() : string.Empty;
        }

        public void SelectCell(int col, int row)
        {
            _gridView.Focus();
            _gridView.ClearSelection();
            _gridView.CurrentCell = _gridView[col, row];
        }

        public void EditCell()
        {
            if (_gridView.CurrentCell == null)
                return;
            _gridView.Focus();
            _gridView.BeginEdit(true);
        }

        public void SetCellValue(object obj)
        {
            var cell = _gridView.CurrentCell;
            if (cell == null)
                return;

            _gridView.BeginEdit(true);
            SetCellValue(cell, obj);
            _gridView.NotifyCurrentCellDirty(true);
            _gridView.RefreshEdit();
            _gridView.EndEdit();
        }

        public void SetCellValue(int col, int row, object obj)
        {
            SetCellValue(_gridView[col, row], obj);
        }

        public void SetCellValue(DataGridViewCell cell, object obj)
        {
            cell.Value = ConvertValue(obj, cell.ValueType);
        }

        private static object ConvertValue(object obj, Type t)
        {
            if (t == null)
                return null;
            else if (obj == null || obj.GetType() == t)
                return obj;

            // Check if target type is Nullable.
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // If object is empty string, convert to null.
                if (string.Empty.Equals(obj))
                    return null;

                // Change target type to Nullable's underlying type.
                t = Nullable.GetUnderlyingType(t);
            }

            try
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return Convert.ChangeType(obj, t);
            }
            catch
            {
                // If object is empty string, convert to null.
                return string.Empty.Equals(obj) ? null : obj;
            }
        }

        public void SelectRow(int row)
        {
            _gridView.ClearSelection();
            _gridView.Rows[row].Selected = true;
        }

        public void Sort(int col)
        {
            _gridView.Sort(_gridView.Columns[col], ListSortDirection.Ascending);
        }

        public int RowCount
        {
            get { return _gridView.Rows.Count; }
        }

        public int VisibleColumnCount
        {
            get { return _gridView.Columns.Cast<DataGridViewColumn>().Count(column => column.Visible); }
        }

        public void SetRequiredColumns(params int[] columns)
        {
            _requiredColumns = new HashSet<int>(columns);
        }
    }
}
