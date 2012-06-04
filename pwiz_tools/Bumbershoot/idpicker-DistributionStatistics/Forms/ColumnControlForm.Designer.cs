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

namespace IDPicker.Forms
{
    partial class ColumnControlForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.columnOptionsDGV = new System.Windows.Forms.DataGridView();
            this.nameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.decimalColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colorColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.visibleColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.typeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.foreColorColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.backColorColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cancel_Button = new System.Windows.Forms.Button();
            this.ok_Button = new System.Windows.Forms.Button();
            this.defaultColorPreviewBox = new System.Windows.Forms.TextBox();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize) (this.columnOptionsDGV)).BeginInit();
            this.SuspendLayout();
            // 
            // columnOptionsDGV
            // 
            this.columnOptionsDGV.AllowUserToAddRows = false;
            this.columnOptionsDGV.AllowUserToDeleteRows = false;
            this.columnOptionsDGV.AllowUserToResizeRows = false;
            this.columnOptionsDGV.Anchor = ((System.Windows.Forms.AnchorStyles) ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.columnOptionsDGV.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.columnOptionsDGV.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.nameColumn,
            this.decimalColumn,
            this.colorColumn,
            this.visibleColumn,
            this.typeColumn,
            this.foreColorColumn,
            this.backColorColumn});
            this.columnOptionsDGV.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.columnOptionsDGV.Location = new System.Drawing.Point(12, 12);
            this.columnOptionsDGV.MultiSelect = false;
            this.columnOptionsDGV.Name = "columnOptionsDGV";
            this.columnOptionsDGV.RowHeadersVisible = false;
            this.columnOptionsDGV.Size = new System.Drawing.Size(483, 291);
            this.columnOptionsDGV.TabIndex = 0;
            this.columnOptionsDGV.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.columnOptionsDGV_CellBeginEdit);
            this.columnOptionsDGV.CellMouseLeave += new System.Windows.Forms.DataGridViewCellEventHandler(this.columnOptionsDGV_CellMouseLeave);
            this.columnOptionsDGV.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.columnOptionsDGV_CellFormatting);
            this.columnOptionsDGV.CellMouseEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.columnOptionsDGV_CellMouseEnter);
            this.columnOptionsDGV.MouseLeave += new System.EventHandler(this.columnOptionsDGV_ResetCursor);
            this.columnOptionsDGV.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.columnOptionsDGV_CellClick);
            this.columnOptionsDGV.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.columnOptionsDGV_DataError);
            this.columnOptionsDGV.CellEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.columnOptionsDGV_CellEnter);
            this.columnOptionsDGV.MouseEnter += new System.EventHandler(this.columnOptionsDGV_ResetCursor);
            // 
            // nameColumn
            // 
            this.nameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.nameColumn.HeaderText = "Column Name";
            this.nameColumn.MinimumWidth = 90;
            this.nameColumn.Name = "nameColumn";
            this.nameColumn.ReadOnly = true;
            this.nameColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // decimalColumn
            // 
            this.decimalColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            dataGridViewCellStyle4.NullValue = "n/a";
            this.decimalColumn.DefaultCellStyle = dataGridViewCellStyle4;
            this.decimalColumn.FillWeight = 1F;
            this.decimalColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.decimalColumn.HeaderText = "Decimal Places";
            this.decimalColumn.Items.AddRange(new object[] {
            "Auto",
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "11",
            "12",
            "13",
            "14",
            "15"});
            this.decimalColumn.MinimumWidth = 90;
            this.decimalColumn.Name = "decimalColumn";
            this.decimalColumn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.decimalColumn.Width = 90;
            // 
            // colorColumn
            // 
            this.colorColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            dataGridViewCellStyle5.NullValue = "Text";
            this.colorColumn.DefaultCellStyle = dataGridViewCellStyle5;
            this.colorColumn.FillWeight = 1F;
            this.colorColumn.HeaderText = "Color";
            this.colorColumn.MinimumWidth = 75;
            this.colorColumn.Name = "colorColumn";
            this.colorColumn.ReadOnly = true;
            this.colorColumn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.colorColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colorColumn.Width = 75;
            // 
            // visibleColumn
            // 
            this.visibleColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.visibleColumn.FillWeight = 1F;
            this.visibleColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.visibleColumn.HeaderText = "Visibility";
            this.visibleColumn.Items.AddRange(new object[] {
            "Auto",
            "Always",
            "Never"});
            this.visibleColumn.MinimumWidth = 70;
            this.visibleColumn.Name = "visibleColumn";
            this.visibleColumn.Width = 70;
            // 
            // typeColumn
            // 
            this.typeColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.typeColumn.HeaderText = "Data Type";
            this.typeColumn.MinimumWidth = 50;
            this.typeColumn.Name = "typeColumn";
            this.typeColumn.ReadOnly = true;
            this.typeColumn.Visible = false;
            // 
            // foreColorColumn
            // 
            this.foreColorColumn.HeaderText = "ForeColor";
            this.foreColorColumn.Name = "foreColorColumn";
            this.foreColorColumn.Visible = false;
            // 
            // backColorColumn
            // 
            this.backColorColumn.HeaderText = "BackColor";
            this.backColorColumn.Name = "backColorColumn";
            this.backColorColumn.Visible = false;
            // 
            // cancel_Button
            // 
            this.cancel_Button.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancel_Button.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel_Button.Location = new System.Drawing.Point(420, 312);
            this.cancel_Button.Name = "cancel_Button";
            this.cancel_Button.Size = new System.Drawing.Size(75, 23);
            this.cancel_Button.TabIndex = 1;
            this.cancel_Button.Text = "Cancel";
            this.cancel_Button.UseVisualStyleBackColor = true;
            // 
            // ok_Button
            // 
            this.ok_Button.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ok_Button.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ok_Button.Location = new System.Drawing.Point(339, 312);
            this.ok_Button.Name = "ok_Button";
            this.ok_Button.Size = new System.Drawing.Size(75, 23);
            this.ok_Button.TabIndex = 2;
            this.ok_Button.Text = "OK";
            this.ok_Button.UseVisualStyleBackColor = true;
            this.ok_Button.Click += new System.EventHandler(this.ok_Button_Click);
            // 
            // defaultColorPreviewBox
            // 
            this.defaultColorPreviewBox.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.defaultColorPreviewBox.BackColor = System.Drawing.SystemColors.Window;
            this.defaultColorPreviewBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.defaultColorPreviewBox.Location = new System.Drawing.Point(221, 314);
            this.defaultColorPreviewBox.Name = "defaultColorPreviewBox";
            this.defaultColorPreviewBox.ReadOnly = true;
            this.defaultColorPreviewBox.Size = new System.Drawing.Size(112, 20);
            this.defaultColorPreviewBox.TabIndex = 7;
            this.defaultColorPreviewBox.TabStop = false;
            this.defaultColorPreviewBox.Text = "Default Color Preview";
            this.defaultColorPreviewBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.defaultColorPreviewBox.Click += new System.EventHandler(this.defaultColorPreviewBox_Click);
            this.defaultColorPreviewBox.Enter += new System.EventHandler(this.defaultColorPreviewBox_Enter);
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn1.HeaderText = "Column Name";
            this.dataGridViewTextBoxColumn1.MinimumWidth = 90;
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            this.dataGridViewTextBoxColumn1.ReadOnly = true;
            this.dataGridViewTextBoxColumn1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            dataGridViewCellStyle6.NullValue = "Text";
            this.dataGridViewTextBoxColumn2.DefaultCellStyle = dataGridViewCellStyle6;
            this.dataGridViewTextBoxColumn2.FillWeight = 1F;
            this.dataGridViewTextBoxColumn2.HeaderText = "Color";
            this.dataGridViewTextBoxColumn2.MinimumWidth = 75;
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            this.dataGridViewTextBoxColumn2.ReadOnly = true;
            this.dataGridViewTextBoxColumn2.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewTextBoxColumn2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.dataGridViewTextBoxColumn2.Width = 75;
            // 
            // dataGridViewTextBoxColumn3
            // 
            this.dataGridViewTextBoxColumn3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn3.HeaderText = "Data Type";
            this.dataGridViewTextBoxColumn3.MinimumWidth = 50;
            this.dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
            this.dataGridViewTextBoxColumn3.ReadOnly = true;
            this.dataGridViewTextBoxColumn3.Visible = false;
            // 
            // dataGridViewTextBoxColumn4
            // 
            this.dataGridViewTextBoxColumn4.HeaderText = "ForeColor";
            this.dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
            this.dataGridViewTextBoxColumn4.Visible = false;
            // 
            // dataGridViewTextBoxColumn5
            // 
            this.dataGridViewTextBoxColumn5.HeaderText = "BackColor";
            this.dataGridViewTextBoxColumn5.Name = "dataGridViewTextBoxColumn5";
            this.dataGridViewTextBoxColumn5.Visible = false;
            // 
            // ColumnControlForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(507, 347);
            this.Controls.Add(this.defaultColorPreviewBox);
            this.Controls.Add(this.ok_Button);
            this.Controls.Add(this.cancel_Button);
            this.Controls.Add(this.columnOptionsDGV);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.MinimumSize = new System.Drawing.Size(500, 145);
            this.Name = "ColumnControlForm";
            this.Text = "Display Options";
            ((System.ComponentModel.ISupportInitialize) (this.columnOptionsDGV)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView columnOptionsDGV;
        private System.Windows.Forms.Button cancel_Button;
        private System.Windows.Forms.Button ok_Button;
        private System.Windows.Forms.TextBox defaultColorPreviewBox;
        private System.Windows.Forms.DataGridViewTextBoxColumn nameColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn decimalColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn colorColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn visibleColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn typeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn foreColorColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn backColorColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn5;
    }
}