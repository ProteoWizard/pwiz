//
// $Id$
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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

namespace IDPicker.Controls
{
    partial class QonverterSettingsByAnalysisControl
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
            this.analysisNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.decoyPrefixColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
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
            this.decoyPrefixColumn,
            this.qonverterSettingsColumn});
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.RowHeadersVisible = false;
            this.dataGridView.Size = new System.Drawing.Size(593, 144);
            this.dataGridView.TabIndex = 0;
            // 
            // analysisNameColumn
            // 
            this.analysisNameColumn.HeaderText = "Analysis";
            this.analysisNameColumn.Name = "analysisNameColumn";
            this.analysisNameColumn.ReadOnly = true;
            this.analysisNameColumn.Width = 150;
            // 
            // decoyPrefixColumn
            // 
            this.decoyPrefixColumn.HeaderText = "Decoy Prefix";
            this.decoyPrefixColumn.Name = "decoyPrefixColumn";
            // 
            // qonverterSettingsColumn
            // 
            this.qonverterSettingsColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.qonverterSettingsColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.qonverterSettingsColumn.HeaderText = "Qonverter Settings";
            this.qonverterSettingsColumn.Name = "qonverterSettingsColumn";
            // 
            // QonverterSettingsByAnalysisControl
            // 
            this.Controls.Add(this.dataGridView);
            this.Name = "QonverterSettingsByAnalysisControl";
            this.Size = new System.Drawing.Size(593, 144);
            ((System.ComponentModel.ISupportInitialize) (this.dataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn analysisNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn decoyPrefixColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn qonverterSettingsColumn;
    }
}