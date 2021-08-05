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
// Copyright 2012 Vanderbilt University
//
// Contributor(s): 
//

namespace IDPicker.Forms
{
    partial class EmbedderForm
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
            this.okButton = new System.Windows.Forms.Button();
            this.embedAllButton = new System.Windows.Forms.Button();
            this.deleteAllButton = new System.Windows.Forms.Button();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.searchPathTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.extensionsTextBox = new System.Windows.Forms.TextBox();
            this.defaultQuantitationSettingsButton = new System.Windows.Forms.Button();
            this.defaultQuantitationMethodLabel = new System.Windows.Forms.Label();
            this.defaultQuantitationMethodBox = new System.Windows.Forms.ComboBox();
            this.EmbedNotice = new System.Windows.Forms.TextBox();
            this.ModeandDefaultPanel = new System.Windows.Forms.Panel();
            this.defaultQuantitationSettingsLabel = new System.Windows.Forms.Label();
            this.embedScanTimeOnlyBox = new System.Windows.Forms.CheckBox();
            this.dataGridView = new IDPicker.Controls.AutomationDataGridView();
            this.idColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.sourceNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.embeddedSourceColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.quantitationMethodColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.quantitationSettingsColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ModeandDefaultPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.Location = new System.Drawing.Point(778, 534);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 2;
            this.okButton.Text = "Close";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // embedAllButton
            // 
            this.embedAllButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.embedAllButton.Location = new System.Drawing.Point(697, 534);
            this.embedAllButton.Name = "embedAllButton";
            this.embedAllButton.Size = new System.Drawing.Size(75, 23);
            this.embedAllButton.TabIndex = 3;
            this.embedAllButton.Text = "Embed All";
            this.embedAllButton.UseVisualStyleBackColor = true;
            this.embedAllButton.Click += new System.EventHandler(this.embedAllButton_Click);
            // 
            // deleteAllButton
            // 
            this.deleteAllButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.deleteAllButton.Location = new System.Drawing.Point(616, 534);
            this.deleteAllButton.Name = "deleteAllButton";
            this.deleteAllButton.Size = new System.Drawing.Size(75, 23);
            this.deleteAllButton.TabIndex = 4;
            this.deleteAllButton.Text = "Delete All";
            this.deleteAllButton.UseVisualStyleBackColor = true;
            this.deleteAllButton.Click += new System.EventHandler(this.deleteAllButton_Click);
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn1.HeaderText = "Source";
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.dataGridViewTextBoxColumn2.HeaderText = "Embedded Status";
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            this.dataGridViewTextBoxColumn2.Width = 200;
            // 
            // searchPathTextBox
            // 
            this.searchPathTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchPathTextBox.Location = new System.Drawing.Point(81, 508);
            this.searchPathTextBox.Name = "searchPathTextBox";
            this.searchPathTextBox.Size = new System.Drawing.Size(772, 20);
            this.searchPathTextBox.TabIndex = 5;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 511);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(68, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "Search path:";
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 537);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(61, 13);
            this.label2.TabIndex = 7;
            this.label2.Text = "Extensions:";
            // 
            // extensionsTextBox
            // 
            this.extensionsTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.extensionsTextBox.Location = new System.Drawing.Point(81, 534);
            this.extensionsTextBox.Name = "extensionsTextBox";
            this.extensionsTextBox.Size = new System.Drawing.Size(316, 20);
            this.extensionsTextBox.TabIndex = 8;
            // 
            // defaultQuantitationSettingsButton
            // 
            this.defaultQuantitationSettingsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.defaultQuantitationSettingsButton.Location = new System.Drawing.Point(772, 4);
            this.defaultQuantitationSettingsButton.Name = "defaultQuantitationSettingsButton";
            this.defaultQuantitationSettingsButton.Size = new System.Drawing.Size(75, 23);
            this.defaultQuantitationSettingsButton.TabIndex = 12;
            this.defaultQuantitationSettingsButton.Text = "Define";
            this.defaultQuantitationSettingsButton.UseVisualStyleBackColor = true;
            this.defaultQuantitationSettingsButton.Click += new System.EventHandler(this.defaultXICSettingsButton_Click);
            // 
            // defaultQuantitationMethodLabel
            // 
            this.defaultQuantitationMethodLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.defaultQuantitationMethodLabel.AutoSize = true;
            this.defaultQuantitationMethodLabel.Location = new System.Drawing.Point(370, 9);
            this.defaultQuantitationMethodLabel.Name = "defaultQuantitationMethodLabel";
            this.defaultQuantitationMethodLabel.Size = new System.Drawing.Size(140, 13);
            this.defaultQuantitationMethodLabel.TabIndex = 13;
            this.defaultQuantitationMethodLabel.Text = "Default quantitation method:";
            // 
            // defaultQuantitationMethodBox
            // 
            this.defaultQuantitationMethodBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.defaultQuantitationMethodBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.defaultQuantitationMethodBox.FormattingEnabled = true;
            this.defaultQuantitationMethodBox.Items.AddRange(new object[] {
            "None",
            "Label free",
            "iTRAQ 4-plex",
            "iTRAQ 8-plex",
            "TMT duplex",
            "TMT 6-plex",
            "TMT 10-plex",
            "TMT 11-plex",
            "TMTpro 16-plex"});
            this.defaultQuantitationMethodBox.Location = new System.Drawing.Point(513, 5);
            this.defaultQuantitationMethodBox.Name = "defaultQuantitationMethodBox";
            this.defaultQuantitationMethodBox.Size = new System.Drawing.Size(111, 21);
            this.defaultQuantitationMethodBox.TabIndex = 14;
            this.defaultQuantitationMethodBox.SelectedIndexChanged += new System.EventHandler(this.defaultQuantitationMethodBox_SelectedIndexChanged);
            // 
            // EmbedNotice
            // 
            this.EmbedNotice.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.EmbedNotice.Location = new System.Drawing.Point(12, 7);
            this.EmbedNotice.Name = "EmbedNotice";
            this.EmbedNotice.Size = new System.Drawing.Size(836, 20);
            this.EmbedNotice.TabIndex = 15;
            this.EmbedNotice.Text = "Notice: XIC metrics are embedded based on current filters. If filters are changed" +
    " please repeat embed process.";
            this.EmbedNotice.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // ModeandDefaultPanel
            // 
            this.ModeandDefaultPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ModeandDefaultPanel.Controls.Add(this.defaultQuantitationSettingsButton);
            this.ModeandDefaultPanel.Controls.Add(this.defaultQuantitationMethodBox);
            this.ModeandDefaultPanel.Controls.Add(this.defaultQuantitationSettingsLabel);
            this.ModeandDefaultPanel.Controls.Add(this.defaultQuantitationMethodLabel);
            this.ModeandDefaultPanel.Controls.Add(this.embedScanTimeOnlyBox);
            this.ModeandDefaultPanel.Location = new System.Drawing.Point(1, 1);
            this.ModeandDefaultPanel.Name = "ModeandDefaultPanel";
            this.ModeandDefaultPanel.Size = new System.Drawing.Size(859, 31);
            this.ModeandDefaultPanel.TabIndex = 16;
            // 
            // defaultQuantitationSettingsLabel
            // 
            this.defaultQuantitationSettingsLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.defaultQuantitationSettingsLabel.AutoSize = true;
            this.defaultQuantitationSettingsLabel.Location = new System.Drawing.Point(630, 9);
            this.defaultQuantitationSettingsLabel.Name = "defaultQuantitationSettingsLabel";
            this.defaultQuantitationSettingsLabel.Size = new System.Drawing.Size(141, 13);
            this.defaultQuantitationSettingsLabel.TabIndex = 18;
            this.defaultQuantitationSettingsLabel.Text = "Default quantitation settings:";
            // 
            // embedScanTimeOnlyBox
            // 
            this.embedScanTimeOnlyBox.AutoSize = true;
            this.embedScanTimeOnlyBox.Location = new System.Drawing.Point(11, 9);
            this.embedScanTimeOnlyBox.Name = "embedScanTimeOnlyBox";
            this.embedScanTimeOnlyBox.Size = new System.Drawing.Size(134, 17);
            this.embedScanTimeOnlyBox.TabIndex = 15;
            this.embedScanTimeOnlyBox.Text = "Embed scan times only";
            this.embedScanTimeOnlyBox.UseVisualStyleBackColor = true;
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AllowUserToResizeRows = false;
            this.dataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.idColumn,
            this.sourceNameColumn,
            this.embeddedSourceColumn,
            this.quantitationMethodColumn,
            this.quantitationSettingsColumn});
            this.dataGridView.Location = new System.Drawing.Point(0, 33);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.RowHeadersVisible = false;
            this.dataGridView.Size = new System.Drawing.Size(860, 469);
            this.dataGridView.TabIndex = 1;
            this.dataGridView.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellDoubleClick);
            this.dataGridView.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellValueChanged);
            // 
            // idColumn
            // 
            this.idColumn.HeaderText = "Id";
            this.idColumn.Name = "idColumn";
            this.idColumn.ReadOnly = true;
            this.idColumn.Visible = false;
            // 
            // sourceNameColumn
            // 
            this.sourceNameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.sourceNameColumn.HeaderText = "Source";
            this.sourceNameColumn.Name = "sourceNameColumn";
            this.sourceNameColumn.ReadOnly = true;
            // 
            // embeddedSourceColumn
            // 
            this.embeddedSourceColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.embeddedSourceColumn.HeaderText = "Embedded Status";
            this.embeddedSourceColumn.Name = "embeddedSourceColumn";
            this.embeddedSourceColumn.ReadOnly = true;
            this.embeddedSourceColumn.Width = 250;
            // 
            // quantitationMethodColumn
            // 
            this.quantitationMethodColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.quantitationMethodColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.quantitationMethodColumn.HeaderText = "Quantitation Method";
            this.quantitationMethodColumn.Items.AddRange(new object[] {
            "None",
            "Label free",
            "iTRAQ 4-plex",
            "iTRAQ 8-plex",
            "TMT duplex",
            "TMT 6-plex",
            "TMT 10-plex",
            "TMT 11-plex",
            "TMTpro 16-plex"});
            this.quantitationMethodColumn.Name = "quantitationMethodColumn";
            this.quantitationMethodColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.quantitationMethodColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.quantitationMethodColumn.Width = 130;
            // 
            // quantitationSettingsColumn
            // 
            this.quantitationSettingsColumn.HeaderText = "Quantitation Settings";
            this.quantitationSettingsColumn.Name = "quantitationSettingsColumn";
            this.quantitationSettingsColumn.ReadOnly = true;
            this.quantitationSettingsColumn.Width = 225;
            // 
            // EmbedderForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(860, 562);
            this.Controls.Add(this.ModeandDefaultPanel);
            this.Controls.Add(this.EmbedNotice);
            this.Controls.Add(this.extensionsTextBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.searchPathTextBox);
            this.Controls.Add(this.deleteAllButton);
            this.Controls.Add(this.embedAllButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.dataGridView);
            this.Name = "EmbedderForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Embed";
            this.ModeandDefaultPanel.ResumeLayout(false);
            this.ModeandDefaultPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private IDPicker.Controls.AutomationDataGridView dataGridView;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button embedAllButton;
        private System.Windows.Forms.Button deleteAllButton;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.TextBox searchPathTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox extensionsTextBox;
        private System.Windows.Forms.Button defaultQuantitationSettingsButton;
        private System.Windows.Forms.Label defaultQuantitationMethodLabel;
        private System.Windows.Forms.ComboBox defaultQuantitationMethodBox;
        private System.Windows.Forms.TextBox EmbedNotice;
        private System.Windows.Forms.Panel ModeandDefaultPanel;
        private System.Windows.Forms.CheckBox embedScanTimeOnlyBox;
        private System.Windows.Forms.Label defaultQuantitationSettingsLabel;
        private System.Windows.Forms.DataGridViewTextBoxColumn idColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn sourceNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn embeddedSourceColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn quantitationMethodColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn quantitationSettingsColumn;
    }
}