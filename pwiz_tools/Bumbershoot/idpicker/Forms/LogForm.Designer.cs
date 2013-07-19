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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

namespace IDPicker.Forms
{
    partial class LogForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose (bool disposing)
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
        private void InitializeComponent ()
        {
            this.queryLogDataGridView = new System.Windows.Forms.DataGridView();
            this.timestampColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.sourceColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.entryColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.queryStatisticsDataGridView = new System.Windows.Forms.DataGridView();
            this.maxTimeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.rowCountColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.queryColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.queryLogDataGridView)).BeginInit();
            this.tabControl.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.queryStatisticsDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // queryLogDataGridView
            // 
            this.queryLogDataGridView.AllowUserToAddRows = false;
            this.queryLogDataGridView.AllowUserToDeleteRows = false;
            this.queryLogDataGridView.AllowUserToResizeRows = false;
            this.queryLogDataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.queryLogDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.queryLogDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.timestampColumn,
            this.sourceColumn,
            this.entryColumn});
            this.queryLogDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.queryLogDataGridView.Location = new System.Drawing.Point(3, 3);
            this.queryLogDataGridView.Name = "queryLogDataGridView";
            this.queryLogDataGridView.ReadOnly = true;
            this.queryLogDataGridView.RowHeadersVisible = false;
            this.queryLogDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.queryLogDataGridView.ShowEditingIcon = false;
            this.queryLogDataGridView.Size = new System.Drawing.Size(398, 337);
            this.queryLogDataGridView.TabIndex = 0;
            this.queryLogDataGridView.VirtualMode = true;
            this.queryLogDataGridView.CellValueNeeded += new System.Windows.Forms.DataGridViewCellValueEventHandler(this.queryLogDataGridView_CellValueNeeded);
            // 
            // timestampColumn
            // 
            this.timestampColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.timestampColumn.HeaderText = "Timestamp";
            this.timestampColumn.Name = "timestampColumn";
            this.timestampColumn.ReadOnly = true;
            this.timestampColumn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.timestampColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // sourceColumn
            // 
            this.sourceColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.sourceColumn.HeaderText = "Source";
            this.sourceColumn.Name = "sourceColumn";
            this.sourceColumn.ReadOnly = true;
            this.sourceColumn.Width = 200;
            // 
            // entryColumn
            // 
            this.entryColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.entryColumn.HeaderText = "Entry";
            this.entryColumn.Name = "entryColumn";
            this.entryColumn.ReadOnly = true;
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabPage1);
            this.tabControl.Controls.Add(this.tabPage2);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(412, 369);
            this.tabControl.TabIndex = 1;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.queryLogDataGridView);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(404, 343);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Query Log";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.queryStatisticsDataGridView);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(404, 343);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Query Statistics";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // queryStatisticsDataGridView
            // 
            this.queryStatisticsDataGridView.AllowUserToAddRows = false;
            this.queryStatisticsDataGridView.AllowUserToDeleteRows = false;
            this.queryStatisticsDataGridView.AllowUserToResizeRows = false;
            this.queryStatisticsDataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.queryStatisticsDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.queryStatisticsDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.maxTimeColumn,
            this.rowCountColumn,
            this.queryColumn});
            this.queryStatisticsDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.queryStatisticsDataGridView.Location = new System.Drawing.Point(3, 3);
            this.queryStatisticsDataGridView.Name = "queryStatisticsDataGridView";
            this.queryStatisticsDataGridView.ReadOnly = true;
            this.queryStatisticsDataGridView.RowHeadersVisible = false;
            this.queryStatisticsDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.queryStatisticsDataGridView.ShowEditingIcon = false;
            this.queryStatisticsDataGridView.Size = new System.Drawing.Size(398, 337);
            this.queryStatisticsDataGridView.TabIndex = 1;
            this.queryStatisticsDataGridView.VirtualMode = true;
            this.queryStatisticsDataGridView.CellValueNeeded += new System.Windows.Forms.DataGridViewCellValueEventHandler(this.queryStatisticsDataGridView_CellValueNeeded);
            // 
            // maxTimeColumn
            // 
            this.maxTimeColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.maxTimeColumn.HeaderText = "Max. Time";
            this.maxTimeColumn.Name = "maxTimeColumn";
            this.maxTimeColumn.ReadOnly = true;
            this.maxTimeColumn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // rowCountColumn
            // 
            this.rowCountColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.rowCountColumn.HeaderText = "Rows";
            this.rowCountColumn.Name = "rowCountColumn";
            this.rowCountColumn.ReadOnly = true;
            this.rowCountColumn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // queryColumn
            // 
            this.queryColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.queryColumn.HeaderText = "Query";
            this.queryColumn.Name = "queryColumn";
            this.queryColumn.ReadOnly = true;
            // 
            // LogForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(412, 369);
            this.Controls.Add(this.tabControl);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas)(((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right) 
            | DigitalRune.Windows.Docking.DockAreas.Top) 
            | DigitalRune.Windows.Docking.DockAreas.Bottom) 
            | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "LogForm";
            this.TabText = "LogForm";
            this.Text = "LogForm";
            ((System.ComponentModel.ISupportInitialize)(this.queryLogDataGridView)).EndInit();
            this.tabControl.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.queryStatisticsDataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView queryLogDataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn timestampColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn sourceColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn entryColumn;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.DataGridView queryStatisticsDataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn maxTimeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn rowCountColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn queryColumn;

    }
}