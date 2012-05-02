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
// Contributor(s):
//

namespace IDPicker.Controls
{
    partial class ImportSettingsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent ()
        {
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewFileBrowseColumn1 = new CustomFileCell.DataGridViewFileBrowseColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.analysisNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.databaseColumn = new CustomFileCell.DataGridViewFileBrowseColumn();
            this.decoyPrefixColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.maxRankColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.maxFDRColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ignoreUnmappedPeptidesColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.qonverterSettingsColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            ((System.ComponentModel.ISupportInitialize) (this.dataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AllowUserToResizeRows = false;
            this.dataGridView.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.analysisNameColumn,
            this.databaseColumn,
            this.decoyPrefixColumn,
            this.maxRankColumn,
            this.maxFDRColumn,
            this.ignoreUnmappedPeptidesColumn,
            this.qonverterSettingsColumn});
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.RowHeadersVisible = false;
            this.dataGridView.Size = new System.Drawing.Size(977, 144);
            this.dataGridView.TabIndex = 0;
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.HeaderText = "Analysis";
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            this.dataGridViewTextBoxColumn1.ReadOnly = true;
            this.dataGridViewTextBoxColumn1.Width = 150;
            // 
            // dataGridViewFileBrowseColumn1
            // 
            this.dataGridViewFileBrowseColumn1.HeaderText = "Database";
            this.dataGridViewFileBrowseColumn1.Name = "dataGridViewFileBrowseColumn1";
            this.dataGridViewFileBrowseColumn1.OpenFileDialog = null;
            this.dataGridViewFileBrowseColumn1.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewFileBrowseColumn1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.dataGridViewFileBrowseColumn1.Width = 200;
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.HeaderText = "Database";
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            this.dataGridViewTextBoxColumn2.Width = 200;
            // 
            // dataGridViewTextBoxColumn3
            // 
            this.dataGridViewTextBoxColumn3.HeaderText = "Decoy Prefix";
            this.dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
            this.dataGridViewTextBoxColumn3.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewTextBoxColumn3.Width = 80;
            // 
            // dataGridViewTextBoxColumn4
            // 
            this.dataGridViewTextBoxColumn4.HeaderText = "Max Rank";
            this.dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
            this.dataGridViewTextBoxColumn4.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewTextBoxColumn4.Width = 80;
            // 
            // dataGridViewTextBoxColumn5
            // 
            this.dataGridViewTextBoxColumn5.HeaderText = "Max FDR";
            this.dataGridViewTextBoxColumn5.Name = "dataGridViewTextBoxColumn5";
            this.dataGridViewTextBoxColumn5.Width = 80;
            // 
            // analysisNameColumn
            // 
            this.analysisNameColumn.HeaderText = "Analysis";
            this.analysisNameColumn.Name = "analysisNameColumn";
            this.analysisNameColumn.ReadOnly = true;
            this.analysisNameColumn.Width = 150;
            // 
            // databaseColumn
            // 
            this.databaseColumn.HeaderText = "Database";
            this.databaseColumn.Name = "databaseColumn";
            this.databaseColumn.OpenFileDialog = null;
            this.databaseColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.databaseColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.databaseColumn.Width = 200;
            // 
            // decoyPrefixColumn
            // 
            this.decoyPrefixColumn.HeaderText = "Decoy Prefix";
            this.decoyPrefixColumn.Name = "decoyPrefixColumn";
            // 
            // maxRankColumn
            // 
            this.maxRankColumn.HeaderText = "Max Rank";
            this.maxRankColumn.Name = "maxRankColumn";
            this.maxRankColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.maxRankColumn.Width = 80;
            // 
            // maxFDRColumn
            // 
            this.maxFDRColumn.HeaderText = "Max FDR";
            this.maxFDRColumn.Name = "maxFDRColumn";
            this.maxFDRColumn.Width = 80;
            // 
            // ignoreUnmappedPeptidesColumn
            // 
            this.ignoreUnmappedPeptidesColumn.HeaderText = "Unmapped";
            this.ignoreUnmappedPeptidesColumn.Name = "ignoreUnmappedPeptidesColumn";
            this.ignoreUnmappedPeptidesColumn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.ignoreUnmappedPeptidesColumn.ToolTipText = "Ignore Unmapped Peptides";
            this.ignoreUnmappedPeptidesColumn.Width = 65;
            // 
            // qonverterSettingsColumn
            // 
            this.qonverterSettingsColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.qonverterSettingsColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.qonverterSettingsColumn.HeaderText = "Qonverter Settings";
            this.qonverterSettingsColumn.Name = "qonverterSettingsColumn";
            // 
            // ImportSettingsControl
            // 
            this.Controls.Add(this.dataGridView);
            this.Name = "ImportSettingsControl";
            this.Size = new System.Drawing.Size(977, 144);
            ((System.ComponentModel.ISupportInitialize) (this.dataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn5;
        private System.Windows.Forms.DataGridViewTextBoxColumn analysisNameColumn;
        private CustomFileCell.DataGridViewFileBrowseColumn databaseColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn decoyPrefixColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn maxRankColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn maxFDRColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn ignoreUnmappedPeptidesColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn qonverterSettingsColumn;
        private CustomFileCell.DataGridViewFileBrowseColumn dataGridViewFileBrowseColumn1;
    }
}