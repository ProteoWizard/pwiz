//
// $Id: GroupingControlForm.cs 192 2010-09-23 19:53:08Z holmanjd $
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): 
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BrightIdeasSoftware;

namespace IDPicker.Forms
{
    public partial class ColumnControlForm : Form
    {
        public Dictionary<OLVColumn, object[]> _savedSettings;
        ColorDialog ColorBox;
        bool hasLockableColumns = false;
        Dictionary<string, bool> visibilityValues;

        public ColumnControlForm(Dictionary<OLVColumn, object[]> columnList, Color[] windowColors)
        {
            InitializeComponent();
            _savedSettings = columnList;
            ColorBox = new ColorDialog();

            visibilityValues = new Dictionary<string, bool>();
            visibilityValues.Add("Yes", true);
            visibilityValues.Add("No", false);
            visibilityValues.Add("Always", true);
            visibilityValues.Add("Never", false);

            //check if values are of type object[4] or object[5]
            //For some reason this.ParentForm, this.Parent, and this.Owner all return null at this point
            foreach (var kvp in columnList)
            {
                if (kvp.Value.Length == 5)
                    hasLockableColumns = true;
                break;
            }

            if (hasLockableColumns)
            {
                foreach (var column in columnList)
                {
                    //Name column, type column (hidden form user), decimal places or "Auto" if automatic (null if not a float), null for color, Current visibility
                    columnOptionsDGV.Rows.Add(new object[5] { column.Key.Text, column.Value[0],
                    ((int)column.Value[1] >= 0) ? column.Value[1].ToString() : ((string)column.Value[0] == "Float") ? "Auto" : null,
                    null,(bool)column.Value[4] ? ((bool)column.Value[3] ? "Always": "Never") : ((bool)column.Value[3] ? "Yes": "No")});

                    columnOptionsDGV[0, columnOptionsDGV.RowCount - 1].Tag = column.Key;
                    if (columnOptionsDGV[2, columnOptionsDGV.RowCount - 1].Value == null)
                        columnOptionsDGV[2, columnOptionsDGV.RowCount - 1].Style.BackColor = Color.LightGray;
                    columnOptionsDGV[3, columnOptionsDGV.RowCount - 1].Style.BackColor = (Color)column.Value[2];

                }
            }
            else
            {
                ((DataGridViewComboBoxColumn)columnOptionsDGV.Columns[4]).Items.Remove("Always");
                ((DataGridViewComboBoxColumn)columnOptionsDGV.Columns[4]).Items.Remove("Never");
                foreach (var column in columnList)
                {
                    //Name column, type column (hidden form user), decimal places or "Auto" if automatic (null if not a float), null for color, Current visibility
                    columnOptionsDGV.Rows.Add(new object[5] { column.Key.Text, column.Value[0],
                    ((int)column.Value[1] >= 0) ? column.Value[1].ToString() : ((string)column.Value[0] == "Float") ? "Auto" : null,
                    null,((bool)column.Value[3] ? "Yes" : "No") });
                    columnOptionsDGV[0, columnOptionsDGV.RowCount - 1].Tag = column.Key;
                    if (columnOptionsDGV[2, columnOptionsDGV.RowCount - 1].Value == null)
                        columnOptionsDGV[2, columnOptionsDGV.RowCount - 1].Style.BackColor = Color.LightGray;
                    columnOptionsDGV[3, columnOptionsDGV.RowCount - 1].Style.BackColor = (Color)column.Value[2];
                }
            }

            WindowBackColorBox.BackColor = windowColors[0];
            PreviewBox.BackColor = windowColors[0];
            WindowTextColorBox.BackColor = windowColors[1];
            PreviewBox.ForeColor = windowColors[1];
            columnOptionsDGV.Columns[3].DefaultCellStyle.ForeColor = windowColors[1];
        }

        private void ok_Button_Click(object sender, EventArgs e)
        {

            if (hasLockableColumns)
            {
                foreach (DataGridViewRow row in columnOptionsDGV.Rows)
                {
                    _savedSettings[(OLVColumn)row.Cells[0].Tag][1] = (row.Cells[2].Value == null || row.Cells[2].Value.ToString() == "Auto") ? -1 : int.Parse(row.Cells[2].Value.ToString());
                    _savedSettings[(OLVColumn)row.Cells[0].Tag][2] = row.Cells[3].Style.BackColor;
                    _savedSettings[(OLVColumn)row.Cells[0].Tag][3] = visibilityValues[row.Cells[4].Value.ToString()];
                    _savedSettings[(OLVColumn)row.Cells[0].Tag][4] = (row.Cells[4].Value.ToString() == "Always" || row.Cells[4].Value.ToString() == "Never");
                }
            }
            else
            {
                foreach (DataGridViewRow row in columnOptionsDGV.Rows)
                {
                    _savedSettings[(OLVColumn)row.Cells[0].Tag][1] = (row.Cells[2].Value == null || row.Cells[2].Value.ToString() == "Auto") ? -1 : int.Parse(row.Cells[2].Value.ToString());
                    _savedSettings[(OLVColumn)row.Cells[0].Tag][2] = row.Cells[3].Style.BackColor;
                    _savedSettings[(OLVColumn)row.Cells[0].Tag][3] = visibilityValues[row.Cells[4].Value.ToString()];
                }
            }
        }

        private void columnOptionsDGV_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex == 2 && (string)columnOptionsDGV[1, e.RowIndex].Value != "Float")
                e.Cancel = true;
            else if (e.ColumnIndex == 3 && (string)columnOptionsDGV[1, e.RowIndex].Value == "Key")
                e.Cancel = true;
            else if ((e.ColumnIndex == 4) && (string)columnOptionsDGV[1, e.RowIndex].Value == "Key")
                e.Cancel = true;
            
        }

        private void WindowBackColorBox_Click(object sender, EventArgs e)
        {
            Color oldColor = WindowBackColorBox.BackColor;

            ColorBox.AllowFullOpen = true;
            ColorBox.AnyColor = true;
            ColorBox.FullOpen = true;
            ColorBox.Color = oldColor;
            if (ColorBox.ShowDialog() == DialogResult.OK)
            {
                WindowBackColorBox.BackColor = ColorBox.Color;
                PreviewBox.BackColor = ColorBox.Color;

                foreach (DataGridViewRow row in columnOptionsDGV.Rows)
                {
                    if (row.Cells[3].Style.BackColor == oldColor)
                        row.Cells[3].Style.BackColor = ColorBox.Color;
                }
            }
        }

        private void Unselectable(object sender, EventArgs e)
        {
            columnOptionsDGV.Select();
        }

        private void WindowTextColorBox_Click(object sender, EventArgs e)
        {
            ColorBox.AllowFullOpen = true;
            ColorBox.AnyColor = true;
            ColorBox.FullOpen = true;
            ColorBox.Color = WindowTextColorBox.BackColor;
            if (ColorBox.ShowDialog() == DialogResult.OK)
            {
                WindowTextColorBox.BackColor = ColorBox.Color;
                PreviewBox.ForeColor = ColorBox.Color;
                columnOptionsDGV.Columns[3].DefaultCellStyle.ForeColor = ColorBox.Color;
            }
        }

        private void columnOptionsDGV_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 3)
            {
                ColorBox.AllowFullOpen = true;
                ColorBox.AnyColor = true;
                ColorBox.FullOpen = true;
                ColorBox.Color = columnOptionsDGV[e.ColumnIndex, e.RowIndex].Style.BackColor;
                if (ColorBox.ShowDialog() == DialogResult.OK)
                    columnOptionsDGV[e.ColumnIndex,e.RowIndex].Style.BackColor = ColorBox.Color;
            }
        }

        private void columnOptionsDGV_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            columnOptionsDGV[e.ColumnIndex, e.RowIndex].Selected = false;
        }
    }
}
