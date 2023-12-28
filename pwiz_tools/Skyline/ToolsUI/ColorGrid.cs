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
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ColorGrid<T> : UserControl where T : RgbHexColor, new()
    {
        private IColorGridOwner _owner;

        public ColorGrid()
        {
            InitializeComponent();

            colorPickerDlg.FullOpen = true;
            colBtn.UseColumnTextForButtonValue = true;
            comboColorType.SelectedIndex = 0;
        }

        public DataGridView DataGridView
        {
            get { return dataGridViewColors; }
        }

        public int ButtonColumnIndex { get { return colBtn.Index; } }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public event KeyEventHandler OnDataGridKeyDown
        {
            add { dataGridViewColors.KeyDown += value; }
            remove { dataGridViewColors.KeyDown -= value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public event ListChangedEventHandler OnListChanged
        {
            add { bindingSource1.ListChanged += value; }
            remove { bindingSource1.ListChanged -= value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public event DataGridViewCellEventHandler OnCellValueChanged
        {
            add { dataGridViewColors.CellValueChanged += value; }
            remove { dataGridViewColors.CellValueChanged -= value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public event DataGridViewCellFormattingEventHandler OnCellFormatting
        {
            add { dataGridViewColors.CellFormatting += value; }
            remove { dataGridViewColors.CellFormatting -= value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public event DataGridViewCellEventHandler OnCellClick
        {
            add { dataGridViewColors.CellClick += value; }
            remove { dataGridViewColors.CellClick -= value; }
        }

        public IColorGridOwner Owner
        {
            set
            {
                _owner = value;
                bindingSource1.DataSource = _owner.GetCurrentBindingList();
            }
        }

        public DataGridViewCell GetCell(int columnIndex, int rowIndex)
        {
            return dataGridViewColors[columnIndex, rowIndex];
        }

        public BindingSource BindingSource
        {
            get { return bindingSource1; }
        }

        public bool AllowUserToOrderColumns
        {
            get { return dataGridViewColors.AllowUserToOrderColumns; }
            set { dataGridViewColors.AllowUserToOrderColumns = value; }
        }

        public bool AllowUserToAddRows
        {
            get { return dataGridViewColors.AllowUserToAddRows; }
            set { dataGridViewColors.AllowUserToAddRows = value; }
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

            ((T) bindingSource1[rowIndex]).Color = newColor;
        }

        public void UpdateBindingSource()
        {
            if(_owner != null)
                bindingSource1.DataSource = _owner.GetCurrentBindingList();
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
                    var color = RgbHexColor.ParseHtmlColor(line) ?? RgbHexColor.ParseRgb(line);
                    if (color == null)
                    {
                        MessageDlg.Show(this, string.Format(ToolsUIResources.EditCustomThemeDlg_DoPaste_Unable_to_parse_the_color___0____Use_HEX_or_RGB_format_, line));
                        return;
                    }
                    var colorRow = new T { Color = color.Value };
                    BindingSource.Insert(BindingSource.Position, colorRow);
                }
            }
        }

        public interface IColorGridOwner
        {
            BindingList<T> GetCurrentBindingList();
        }

        private void dataGridViewColors_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex != colorCol.Index || _owner == null)
                return;
            var currentBindings = _owner.GetCurrentBindingList();
            if (e.RowIndex >= 0 && e.RowIndex < currentBindings.Count)
            {
                var row = dataGridViewColors.Rows[e.RowIndex];
                var colorRow = currentBindings[e.RowIndex];
                var cell = row.Cells[e.ColumnIndex];
                cell.Style.SelectionBackColor = cell.Style.SelectionForeColor = cell.Style.BackColor = colorRow.Color;
            }
        }

        private void dataGridViewColors_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.Exception is FormatException)
            {
                MessageDlg.Show(this, ToolsUIResources.EditCustomThemeDlg_dataGridViewColors_DataError_Colors_must_be_entered_in_HEX_or_RGB_format_);
            }
        }

        private void dataGridViewColors_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == ButtonColumnIndex && e.RowIndex >= 0)
            {
                var rowIndex = e.RowIndex;
                var oldColor = ((T)bindingSource1[rowIndex]).Color;
                colorPickerDlg.Color = oldColor;

                if (colorPickerDlg.ShowDialog() == DialogResult.OK)
                {
                    changeRowColor(rowIndex, colorPickerDlg.Color);
                }
            }
        }

        private void dataGridViewColors_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            // https://stackoverflow.com/questions/5652957/what-event-catches-a-change-of-value-in-a-combobox-in-a-datagridviewcell
            if (!(dataGridViewColors.CurrentCell is DataGridViewTextBoxCell) && dataGridViewColors.IsCurrentCellDirty)
                dataGridViewColors.CommitEdit(DataGridViewDataErrorContexts.Commit);
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


        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public DataGridViewColumnCollection Columns
        {
            get { return dataGridViewColors.Columns; }
        }

        #region Functional Test Support

        public void ChangeToHex() { comboColorType.SelectedIndex = 1; }
        public void ChangeToRGB() { comboColorType.SelectedIndex = 0; }

        public DataGridView GetGrid()
        {
            return dataGridViewColors;
        }

        public void SetBindingPosition(int index)
        {
            bindingSource1.Position = index;
        }

        #endregion

        private void dataGridViewColors_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.V && e.Modifiers == Keys.Control)
            {
                DoPaste();
                e.Handled = true;
            }
        }
    }
}