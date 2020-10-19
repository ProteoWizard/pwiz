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
    partial class IsobaricMappingForm
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
            this.saveButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.miReadAssembleTxt = new System.Windows.Forms.ToolStripMenuItem();
            this.msGroups = new System.Windows.Forms.MenuStrip();
            this.isobaricMappingDataGridView = new System.Windows.Forms.DataGridView();
            this.groupColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.quantitationMethodsTabPanel = new System.Windows.Forms.TabControl();
            this.isobaricSampleMappingTabPage = new System.Windows.Forms.TabPage();
            this.noIsobaricMethodsTabPage = new System.Windows.Forms.TabPage();
            this.noIsobaricMethodsLabel = new System.Windows.Forms.Label();
            this.msGroups.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.isobaricMappingDataGridView)).BeginInit();
            this.quantitationMethodsTabPanel.SuspendLayout();
            this.isobaricSampleMappingTabPage.SuspendLayout();
            this.noIsobaricMethodsTabPage.SuspendLayout();
            this.SuspendLayout();
            // 
            // saveButton
            // 
            this.saveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.saveButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.saveButton.Location = new System.Drawing.Point(1867, 363);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(75, 23);
            this.saveButton.TabIndex = 1;
            this.saveButton.Text = "Save";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(1948, 363);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 130;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // miReadAssembleTxt
            // 
            this.miReadAssembleTxt.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.miReadAssembleTxt.Image = global::IDPicker.Properties.Resources.file;
            this.miReadAssembleTxt.Name = "miReadAssembleTxt";
            this.miReadAssembleTxt.Size = new System.Drawing.Size(28, 20);
            this.miReadAssembleTxt.Text = "Read Assemble.txt";
            this.miReadAssembleTxt.ToolTipText = "Assign sources to groups from an assemble.txt file";
            this.miReadAssembleTxt.Click += new System.EventHandler(this.miReadAssembleTxt_Click);
            // 
            // msGroups
            // 
            this.msGroups.AutoSize = false;
            this.msGroups.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miReadAssembleTxt});
            this.msGroups.Location = new System.Drawing.Point(0, 0);
            this.msGroups.Name = "msGroups";
            this.msGroups.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.msGroups.ShowItemToolTips = true;
            this.msGroups.Size = new System.Drawing.Size(2030, 24);
            this.msGroups.TabIndex = 1;
            this.msGroups.Text = "menuStrip1";
            // 
            // isobaricMappingDataGridView
            // 
            this.isobaricMappingDataGridView.AllowUserToAddRows = false;
            this.isobaricMappingDataGridView.AllowUserToDeleteRows = false;
            this.isobaricMappingDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.isobaricMappingDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.groupColumn});
            this.isobaricMappingDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.isobaricMappingDataGridView.Location = new System.Drawing.Point(0, 0);
            this.isobaricMappingDataGridView.Margin = new System.Windows.Forms.Padding(0);
            this.isobaricMappingDataGridView.Name = "isobaricMappingDataGridView";
            this.isobaricMappingDataGridView.RowHeadersVisible = false;
            this.isobaricMappingDataGridView.Size = new System.Drawing.Size(2022, 304);
            this.isobaricMappingDataGridView.TabIndex = 3;
            this.isobaricMappingDataGridView.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.isobaricMappingDataGridView_EditingControlShowing);
            // 
            // groupColumn
            // 
            this.groupColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.groupColumn.HeaderText = "Source Group";
            this.groupColumn.Name = "groupColumn";
            this.groupColumn.Width = 98;
            // 
            // quantitationMethodsTabPanel
            // 
            this.quantitationMethodsTabPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.quantitationMethodsTabPanel.Controls.Add(this.isobaricSampleMappingTabPage);
            this.quantitationMethodsTabPanel.Controls.Add(this.noIsobaricMethodsTabPage);
            this.quantitationMethodsTabPanel.Location = new System.Drawing.Point(0, 27);
            this.quantitationMethodsTabPanel.Name = "quantitationMethodsTabPanel";
            this.quantitationMethodsTabPanel.SelectedIndex = 0;
            this.quantitationMethodsTabPanel.Size = new System.Drawing.Size(2030, 330);
            this.quantitationMethodsTabPanel.TabIndex = 3;
            // 
            // isobaricSampleMappingTabPage
            // 
            this.isobaricSampleMappingTabPage.Controls.Add(this.isobaricMappingDataGridView);
            this.isobaricSampleMappingTabPage.Location = new System.Drawing.Point(4, 22);
            this.isobaricSampleMappingTabPage.Name = "isobaricSampleMappingTabPage";
            this.isobaricSampleMappingTabPage.Size = new System.Drawing.Size(2022, 304);
            this.isobaricSampleMappingTabPage.TabIndex = 0;
            this.isobaricSampleMappingTabPage.Text = "Isobaric Sample Mapping Table";
            this.isobaricSampleMappingTabPage.UseVisualStyleBackColor = true;
            // 
            // noIsobaricMethodsTabPage
            // 
            this.noIsobaricMethodsTabPage.Controls.Add(this.noIsobaricMethodsLabel);
            this.noIsobaricMethodsTabPage.Location = new System.Drawing.Point(4, 22);
            this.noIsobaricMethodsTabPage.Name = "noIsobaricMethodsTabPage";
            this.noIsobaricMethodsTabPage.Size = new System.Drawing.Size(1097, 304);
            this.noIsobaricMethodsTabPage.TabIndex = 1;
            this.noIsobaricMethodsTabPage.Text = "No Isobaric Methods";
            this.noIsobaricMethodsTabPage.UseVisualStyleBackColor = true;
            // 
            // noIsobaricMethodsLabel
            // 
            this.noIsobaricMethodsLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.noIsobaricMethodsLabel.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Bold);
            this.noIsobaricMethodsLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.noIsobaricMethodsLabel.Location = new System.Drawing.Point(0, 0);
            this.noIsobaricMethodsLabel.Name = "noIsobaricMethodsLabel";
            this.noIsobaricMethodsLabel.Size = new System.Drawing.Size(1097, 304);
            this.noIsobaricMethodsLabel.TabIndex = 4;
            this.noIsobaricMethodsLabel.Text = "There are no source groups with an isobaric quantitation method in the current as" +
    "sembly.\r\n\r\nSelect \"Embed\" in the File menu to embed quantitation.";
            this.noIsobaricMethodsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // IsobaricMappingForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(2030, 398);
            this.Controls.Add(this.msGroups);
            this.Controls.Add(this.quantitationMethodsTabPanel);
            this.Controls.Add(this.saveButton);
            this.Controls.Add(this.cancelButton);
            this.MinimizeBox = false;
            this.Name = "IsobaricMappingForm";
            this.Text = "Assign sample names to reporter ion channels";
            this.msGroups.ResumeLayout(false);
            this.msGroups.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.isobaricMappingDataGridView)).EndInit();
            this.quantitationMethodsTabPanel.ResumeLayout(false);
            this.isobaricSampleMappingTabPage.ResumeLayout(false);
            this.noIsobaricMethodsTabPage.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.ToolStripMenuItem miReadAssembleTxt;
        private System.Windows.Forms.MenuStrip msGroups;
        private System.Windows.Forms.DataGridView isobaricMappingDataGridView;
        private System.Windows.Forms.TabControl quantitationMethodsTabPanel;
        private System.Windows.Forms.TabPage isobaricSampleMappingTabPage;
        private System.Windows.Forms.TabPage noIsobaricMethodsTabPage;
        private System.Windows.Forms.Label noIsobaricMethodsLabel;
        private System.Windows.Forms.DataGridViewTextBoxColumn groupColumn;

    }
}