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
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.okButton = new System.Windows.Forms.Button();
            this.embedAllButton = new System.Windows.Forms.Button();
            this.deleteAllButton = new System.Windows.Forms.Button();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.searchPathTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.extensionsTextBox = new System.Windows.Forms.TextBox();
            this.scanTimeOnlyCheckBox = new System.Windows.Forms.CheckBox();
            this.idColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.sourceNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.embeddedSourceColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.quantitationMethodColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.idColumn,
            this.sourceNameColumn,
            this.embeddedSourceColumn,
            this.quantitationMethodColumn});
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.RowHeadersVisible = false;
            this.dataGridView.Size = new System.Drawing.Size(784, 502);
            this.dataGridView.TabIndex = 1;
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.Location = new System.Drawing.Point(702, 534);
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
            this.embedAllButton.Location = new System.Drawing.Point(621, 534);
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
            this.deleteAllButton.Location = new System.Drawing.Point(540, 534);
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
            this.searchPathTextBox.Size = new System.Drawing.Size(696, 20);
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
            this.extensionsTextBox.Size = new System.Drawing.Size(240, 20);
            this.extensionsTextBox.TabIndex = 8;
            // 
            // scanTimeOnlyCheckBox
            // 
            this.scanTimeOnlyCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.scanTimeOnlyCheckBox.AutoSize = true;
            this.scanTimeOnlyCheckBox.Location = new System.Drawing.Point(392, 537);
            this.scanTimeOnlyCheckBox.Name = "scanTimeOnlyCheckBox";
            this.scanTimeOnlyCheckBox.Size = new System.Drawing.Size(142, 17);
            this.scanTimeOnlyCheckBox.TabIndex = 9;
            this.scanTimeOnlyCheckBox.Text = "Only Embed Scan Times";
            this.scanTimeOnlyCheckBox.UseVisualStyleBackColor = true;
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
            "TMT 6-plex"});
            this.quantitationMethodColumn.Name = "quantitationMethodColumn";
            this.quantitationMethodColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.quantitationMethodColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.quantitationMethodColumn.Width = 130;
            // 
            // EmbedderForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 562);
            this.Controls.Add(this.scanTimeOnlyCheckBox);
            this.Controls.Add(this.extensionsTextBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.searchPathTextBox);
            this.Controls.Add(this.deleteAllButton);
            this.Controls.Add(this.embedAllButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.dataGridView);
            this.Name = "EmbedderForm";
            this.Text = "Embed Subset Spectra";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button embedAllButton;
        private System.Windows.Forms.Button deleteAllButton;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.TextBox searchPathTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox extensionsTextBox;
        private System.Windows.Forms.CheckBox scanTimeOnlyCheckBox;
        private System.Windows.Forms.DataGridViewTextBoxColumn idColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn sourceNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn embeddedSourceColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn quantitationMethodColumn;
    }
}