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
// The Initial Developer of the Original Code is Jay Holman
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

namespace IDPicker.Forms
{
    partial class RTGroupingForm
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
            this.fileGroupingBox = new IDPicker.Controls.AutomationDataGridView();
            this.autoGroupBox = new System.Windows.Forms.GroupBox();
            this.autoDetectButton = new System.Windows.Forms.Button();
            this.backRadio = new System.Windows.Forms.RadioButton();
            this.frontRadio = new System.Windows.Forms.RadioButton();
            this.label2 = new System.Windows.Forms.Label();
            this.groupCountBox = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.okButton = new System.Windows.Forms.Button();
            this.sourceColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.RTGroupColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.fileGroupingBox)).BeginInit();
            this.autoGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.groupCountBox)).BeginInit();
            this.SuspendLayout();
            // 
            // fileGroupingBox
            // 
            this.fileGroupingBox.AllowUserToAddRows = false;
            this.fileGroupingBox.AllowUserToDeleteRows = false;
            this.fileGroupingBox.AllowUserToResizeRows = false;
            this.fileGroupingBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.fileGroupingBox.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.fileGroupingBox.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.sourceColumn,
            this.RTGroupColumn});
            this.fileGroupingBox.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.fileGroupingBox.Location = new System.Drawing.Point(0, 0);
            this.fileGroupingBox.Name = "fileGroupingBox";
            this.fileGroupingBox.RowHeadersVisible = false;
            this.fileGroupingBox.Size = new System.Drawing.Size(815, 427);
            this.fileGroupingBox.TabIndex = 1;
            // 
            // autoGroupBox
            // 
            this.autoGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.autoGroupBox.Controls.Add(this.autoDetectButton);
            this.autoGroupBox.Controls.Add(this.backRadio);
            this.autoGroupBox.Controls.Add(this.frontRadio);
            this.autoGroupBox.Controls.Add(this.label2);
            this.autoGroupBox.Controls.Add(this.groupCountBox);
            this.autoGroupBox.Controls.Add(this.label1);
            this.autoGroupBox.Location = new System.Drawing.Point(2, 431);
            this.autoGroupBox.Name = "autoGroupBox";
            this.autoGroupBox.Size = new System.Drawing.Size(376, 46);
            this.autoGroupBox.TabIndex = 2;
            this.autoGroupBox.TabStop = false;
            this.autoGroupBox.Text = "Auto-detect groups";
            // 
            // autoDetectButton
            // 
            this.autoDetectButton.Location = new System.Drawing.Point(291, 16);
            this.autoDetectButton.Name = "autoDetectButton";
            this.autoDetectButton.Size = new System.Drawing.Size(75, 23);
            this.autoDetectButton.TabIndex = 6;
            this.autoDetectButton.Text = "Detect";
            this.autoDetectButton.UseVisualStyleBackColor = true;
            this.autoDetectButton.Click += new System.EventHandler(this.autoDetectButton_Click);
            // 
            // backRadio
            // 
            this.backRadio.AutoSize = true;
            this.backRadio.Location = new System.Drawing.Point(235, 26);
            this.backRadio.Name = "backRadio";
            this.backRadio.Size = new System.Drawing.Size(50, 17);
            this.backRadio.TabIndex = 5;
            this.backRadio.TabStop = true;
            this.backRadio.Text = "Back";
            this.backRadio.UseVisualStyleBackColor = true;
            // 
            // frontRadio
            // 
            this.frontRadio.AutoSize = true;
            this.frontRadio.Checked = true;
            this.frontRadio.Location = new System.Drawing.Point(235, 11);
            this.frontRadio.Name = "frontRadio";
            this.frontRadio.Size = new System.Drawing.Size(49, 17);
            this.frontRadio.TabIndex = 4;
            this.frontRadio.TabStop = true;
            this.frontRadio.Text = "Front";
            this.frontRadio.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(151, 20);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(87, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Search direction:";
            // 
            // groupCountBox
            // 
            this.groupCountBox.Location = new System.Drawing.Point(100, 18);
            this.groupCountBox.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.groupCountBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.groupCountBox.Name = "groupCountBox";
            this.groupCountBox.Size = new System.Drawing.Size(45, 20);
            this.groupCountBox.TabIndex = 2;
            this.groupCountBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(94, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Number of groups:";
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(728, 447);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 3;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            // 
            // sourceColumn
            // 
            this.sourceColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.sourceColumn.HeaderText = "Spectrum Source";
            this.sourceColumn.Name = "sourceColumn";
            this.sourceColumn.ReadOnly = true;
            // 
            // RTGroupColumn
            // 
            this.RTGroupColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.RTGroupColumn.FillWeight = 50F;
            this.RTGroupColumn.HeaderText = "Retention Time Group";
            this.RTGroupColumn.Name = "RTGroupColumn";
            // 
            // RTGroupingForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(815, 480);
            this.ControlBox = false;
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.autoGroupBox);
            this.Controls.Add(this.fileGroupingBox);
            this.Name = "RTGroupingForm";
            this.Text = "Specify retention time groups";
            this.Load += new System.EventHandler(this.RTGroupingForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.fileGroupingBox)).EndInit();
            this.autoGroupBox.ResumeLayout(false);
            this.autoGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.groupCountBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private IDPicker.Controls.AutomationDataGridView fileGroupingBox;
        private System.Windows.Forms.GroupBox autoGroupBox;
        private System.Windows.Forms.Button autoDetectButton;
        private System.Windows.Forms.RadioButton backRadio;
        private System.Windows.Forms.RadioButton frontRadio;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown groupCountBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.DataGridViewTextBoxColumn sourceColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn RTGroupColumn;

    }
}