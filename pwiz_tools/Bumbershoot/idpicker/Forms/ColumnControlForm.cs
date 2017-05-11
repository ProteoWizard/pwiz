//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
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
using IDPicker.DataModel;
using IDPicker.Controls;
using pwiz.Common.Collections;

namespace IDPicker.Forms
{
    public partial class ColumnControlForm : Form
    {
        public IDictionary<string, ColumnProperty> ColumnProperties { get; set; }

        public new Color? DefaultForeColor
        {
            get { Color color = defaultColorPreviewBox.ForeColor; return color.ToArgb() == SystemColors.WindowText.ToArgb() ? default(Color?) : color; }
            set { defaultColorPreviewBox.ForeColor = value ?? SystemColors.WindowText; }
        }

        public new Color? DefaultBackColor
        {
            get { Color color = defaultColorPreviewBox.BackColor; return color.ToArgb() == SystemColors.Window.ToArgb() ? default(Color?) : color; }
            set { defaultColorPreviewBox.BackColor = value ?? SystemColors.Window; }
        }

        public ColumnControlForm()
        {
            InitializeComponent();

            _visibilityValues = new Dictionary<string, bool?>
                                    {
                                        {"Auto", null},
                                        {"Always", true},
                                        {"Never", false}
                                    };
        }

        protected override void OnLoad (EventArgs e)
        {
            if (ColumnProperties.IsNullOrEmpty())
                throw new ArgumentException("ColumnProperties must be set and non-empty");

            foreach (var column in ColumnProperties)
            {
                // column name, precision, preview color text, visibility, data type, forecolor, backcolor
                int rowIndex = columnOptionsDGV.Rows.Add(new object[]
                {
                    column.Key,
                    column.Value.Precision.HasValue ? column.Value.Precision.Value.ToString() : column.Value.Type == typeof(float) ? "Auto" : null,
                    "Preview Text",
                    _visibilityValues.SingleOrDefault(o => o.Value == column.Value.Visible).Key,
                    column.Value.Type,
                    column.Value.ForeColor,
                    column.Value.BackColor
                });
            }

            colorColumn.DefaultCellStyle.ForeColor = DefaultForeColor ?? SystemColors.WindowText;
            colorColumn.DefaultCellStyle.BackColor = DefaultBackColor ?? SystemColors.Window;

            base.OnLoad(e);
        }

        readonly Dictionary<string, bool?> _visibilityValues;

        private void ok_Button_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in columnOptionsDGV.Rows)
            {
                var columnProperty = ColumnProperties[(string) row.Cells[0].Value];
                if (columnProperty.Type == typeof(float))
                    columnProperty.Precision = row.Cells[decimalColumn.Index].Value.ToString() == "Auto"
                                                    ? default(int?)
                                                    : Int32.Parse(row.Cells[decimalColumn.Index].Value.ToString());
                columnProperty.ForeColor = (Color?) row.Cells[foreColorColumn.Index].Value;
                columnProperty.BackColor = (Color?) row.Cells[backColorColumn.Index].Value;
                columnProperty.Visible = _visibilityValues[row.Cells[visibleColumn.Index].Value.ToString()];
            }
        }

        private void columnOptionsDGV_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex == decimalColumn.Index && (Type) columnOptionsDGV[typeColumn.Index, e.RowIndex].Value != typeof(float))
                e.Cancel = true;
            else if (e.ColumnIndex == visibleColumn.Index && e.RowIndex == 0)
                e.Cancel = true;
        }

        private void columnOptionsDGV_CellFormatting (object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex == decimalColumn.Index && (Type) columnOptionsDGV[typeColumn.Index, e.RowIndex].Value != typeof(float))
                e.CellStyle.BackColor = SystemColors.ControlDark;
            else if (e.ColumnIndex == visibleColumn.Index && e.RowIndex == 0)
                e.CellStyle.BackColor = SystemColors.ControlDark;
            else if (e.ColumnIndex == colorColumn.Index)
            {
                e.CellStyle.ForeColor = (Color?) columnOptionsDGV[foreColorColumn.Index, e.RowIndex].Value ?? DefaultForeColor ?? SystemColors.WindowText;
                e.CellStyle.BackColor = (Color?) columnOptionsDGV[backColorColumn.Index, e.RowIndex].Value ?? DefaultBackColor ?? SystemColors.Window;
            }
        }

        private void columnOptionsDGV_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != colorColumn.Index)
                return;

            var foreColor = (Color?) columnOptionsDGV[foreColorColumn.Index, e.RowIndex].Value;
            var backColor = (Color?) columnOptionsDGV[backColorColumn.Index, e.RowIndex].Value;
            using (var colorControl = new ForeBackColorControl() {ForeColor = foreColor, BackColor = backColor})
            {
                string caption = "Set colors for " + (string) columnOptionsDGV[nameColumn.Index, e.RowIndex].Value;
                if (UserDialog.Show(this, caption, colorControl, FormBorderStyle.FixedToolWindow) == DialogResult.OK)
                {
                    columnOptionsDGV[foreColorColumn.Index, e.RowIndex].Value = colorControl.ForeColor;
                    columnOptionsDGV[backColorColumn.Index, e.RowIndex].Value = colorControl.BackColor;
                    columnOptionsDGV.Refresh();
                }
            }
        }

        private void columnOptionsDGV_CellEnter (object sender, DataGridViewCellEventArgs e)
        {
            columnOptionsDGV[e.ColumnIndex, e.RowIndex].Selected = false;
        }

        private void columnOptionsDGV_CellMouseEnter (object sender, DataGridViewCellEventArgs e)
        {
            columnOptionsDGV.Cursor = e.RowIndex >= 0 && e.ColumnIndex == colorColumn.Index ? Cursors.Hand : Cursors.Default;
        }

        private void columnOptionsDGV_CellMouseLeave (object sender, DataGridViewCellEventArgs e)
        {
            columnOptionsDGV.Cursor = e.RowIndex >= 0 && e.ColumnIndex == colorColumn.Index ? Cursors.Hand : Cursors.Default;
        }

        private void columnOptionsDGV_ResetCursor (object sender, EventArgs e)
        {
            columnOptionsDGV.Cursor = Cursors.Default;
        }

        private void defaultColorPreviewBox_Click (object sender, EventArgs e)
        {
            using (var colorControl = new ForeBackColorControl() { ForeColor = DefaultForeColor, BackColor = DefaultBackColor })
            {
                if (UserDialog.Show(this, "Set default colors", colorControl, FormBorderStyle.FixedToolWindow) == DialogResult.OK)
                {
                    DefaultForeColor = colorControl.ForeColor;
                    DefaultBackColor = colorControl.BackColor;
                    columnOptionsDGV.Refresh();
                }
            }
        }

        private void columnOptionsDGV_DataError (object sender, DataGridViewDataErrorEventArgs e)
        {
            Program.HandleException(e.Exception);
            e.ThrowException = false;
        }

        private void defaultColorPreviewBox_Enter (object sender, EventArgs e)
        {
            columnOptionsDGV.Select();
        }
    }
}
