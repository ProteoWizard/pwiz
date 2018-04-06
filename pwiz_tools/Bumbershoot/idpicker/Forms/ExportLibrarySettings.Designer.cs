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
    partial class ExportLibrarySettings
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
            this.label1 = new System.Windows.Forms.Label();
            this.methodBox = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.outputFormatBox = new System.Windows.Forms.ComboBox();
            this.decoysBox = new System.Windows.Forms.CheckBox();
            this.crossBox = new System.Windows.Forms.CheckBox();
            this.startButton = new System.Windows.Forms.Button();
            this.SpectrumNumBox = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.FragmentNumBox = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.PrecursorNumBox = new System.Windows.Forms.NumericUpDown();
            this.PrecursorLabel = new System.Windows.Forms.Label();
            this.ExportLibraryPanel = new System.Windows.Forms.Panel();
            this.ExportPSMsPanel = new System.Windows.Forms.Panel();
            this.dataGridView1 = new IDPicker.Controls.AutomationDataGridView();
            this.psmColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.psmIncludeColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.ExportPSMButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.SpectrumNumBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.FragmentNumBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.PrecursorNumBox)).BeginInit();
            this.ExportLibraryPanel.SuspendLayout();
            this.ExportPSMsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 88);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(130, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Spectrum picking method:";
            // 
            // methodBox
            // 
            this.methodBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.methodBox.FormattingEnabled = true;
            this.methodBox.Items.AddRange(new object[] {
            "Dot Product Compare"});
            this.methodBox.Location = new System.Drawing.Point(141, 85);
            this.methodBox.Name = "methodBox";
            this.methodBox.Size = new System.Drawing.Size(127, 21);
            this.methodBox.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(61, 115);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Output format:";
            // 
            // outputFormatBox
            // 
            this.outputFormatBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.outputFormatBox.FormattingEnabled = true;
            this.outputFormatBox.Items.AddRange(new object[] {
            ".sptxt"});
            this.outputFormatBox.Location = new System.Drawing.Point(141, 112);
            this.outputFormatBox.Name = "outputFormatBox";
            this.outputFormatBox.Size = new System.Drawing.Size(127, 21);
            this.outputFormatBox.TabIndex = 3;
            // 
            // decoysBox
            // 
            this.decoysBox.AutoSize = true;
            this.decoysBox.Checked = true;
            this.decoysBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.decoysBox.Location = new System.Drawing.Point(141, 139);
            this.decoysBox.Name = "decoysBox";
            this.decoysBox.Size = new System.Drawing.Size(84, 17);
            this.decoysBox.TabIndex = 4;
            this.decoysBox.Text = "Add Decoys";
            this.decoysBox.UseVisualStyleBackColor = true;
            // 
            // crossBox
            // 
            this.crossBox.AutoSize = true;
            this.crossBox.Location = new System.Drawing.Point(141, 162);
            this.crossBox.Name = "crossBox";
            this.crossBox.Size = new System.Drawing.Size(136, 17);
            this.crossBox.TabIndex = 5;
            this.crossBox.Text = "Cross-Peptide Compare";
            this.crossBox.UseVisualStyleBackColor = true;
            this.crossBox.CheckedChanged += new System.EventHandler(this.crossBox_CheckedChanged);
            // 
            // startButton
            // 
            this.startButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.startButton.Location = new System.Drawing.Point(193, 202);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(75, 23);
            this.startButton.TabIndex = 6;
            this.startButton.Text = "Start";
            this.startButton.UseVisualStyleBackColor = true;
            // 
            // SpectrumNumBox
            // 
            this.SpectrumNumBox.Location = new System.Drawing.Point(141, 59);
            this.SpectrumNumBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.SpectrumNumBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.SpectrumNumBox.Name = "SpectrumNumBox";
            this.SpectrumNumBox.Size = new System.Drawing.Size(127, 20);
            this.SpectrumNumBox.TabIndex = 10;
            this.SpectrumNumBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(20, 61);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(115, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Min spectra per match:";
            // 
            // FragmentNumBox
            // 
            this.FragmentNumBox.DecimalPlaces = 3;
            this.FragmentNumBox.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.FragmentNumBox.Location = new System.Drawing.Point(141, 33);
            this.FragmentNumBox.Name = "FragmentNumBox";
            this.FragmentNumBox.Size = new System.Drawing.Size(127, 20);
            this.FragmentNumBox.TabIndex = 8;
            this.FragmentNumBox.Value = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 35);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(122, 13);
            this.label4.TabIndex = 7;
            this.label4.Text = "Fragment m/z tolerance:";
            // 
            // PrecursorNumBox
            // 
            this.PrecursorNumBox.DecimalPlaces = 3;
            this.PrecursorNumBox.Enabled = false;
            this.PrecursorNumBox.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.PrecursorNumBox.Location = new System.Drawing.Point(141, 7);
            this.PrecursorNumBox.Name = "PrecursorNumBox";
            this.PrecursorNumBox.Size = new System.Drawing.Size(127, 20);
            this.PrecursorNumBox.TabIndex = 12;
            this.PrecursorNumBox.Value = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            // 
            // PrecursorLabel
            // 
            this.PrecursorLabel.AutoSize = true;
            this.PrecursorLabel.Enabled = false;
            this.PrecursorLabel.Location = new System.Drawing.Point(12, 9);
            this.PrecursorLabel.Name = "PrecursorLabel";
            this.PrecursorLabel.Size = new System.Drawing.Size(123, 13);
            this.PrecursorLabel.TabIndex = 11;
            this.PrecursorLabel.Text = "Precursor m/z tolerance:";
            // 
            // ExportLibraryPanel
            // 
            this.ExportLibraryPanel.Controls.Add(this.PrecursorLabel);
            this.ExportLibraryPanel.Controls.Add(this.PrecursorNumBox);
            this.ExportLibraryPanel.Controls.Add(this.label1);
            this.ExportLibraryPanel.Controls.Add(this.methodBox);
            this.ExportLibraryPanel.Controls.Add(this.SpectrumNumBox);
            this.ExportLibraryPanel.Controls.Add(this.label2);
            this.ExportLibraryPanel.Controls.Add(this.label3);
            this.ExportLibraryPanel.Controls.Add(this.outputFormatBox);
            this.ExportLibraryPanel.Controls.Add(this.FragmentNumBox);
            this.ExportLibraryPanel.Controls.Add(this.decoysBox);
            this.ExportLibraryPanel.Controls.Add(this.label4);
            this.ExportLibraryPanel.Controls.Add(this.crossBox);
            this.ExportLibraryPanel.Controls.Add(this.startButton);
            this.ExportLibraryPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ExportLibraryPanel.Location = new System.Drawing.Point(0, 0);
            this.ExportLibraryPanel.Name = "ExportLibraryPanel";
            this.ExportLibraryPanel.Size = new System.Drawing.Size(284, 241);
            this.ExportLibraryPanel.TabIndex = 13;
            // 
            // ExportPSMsPanel
            // 
            this.ExportPSMsPanel.Controls.Add(this.ExportPSMButton);
            this.ExportPSMsPanel.Controls.Add(this.dataGridView1);
            this.ExportPSMsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ExportPSMsPanel.Location = new System.Drawing.Point(0, 0);
            this.ExportPSMsPanel.Name = "ExportPSMsPanel";
            this.ExportPSMsPanel.Size = new System.Drawing.Size(284, 241);
            this.ExportPSMsPanel.TabIndex = 14;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToResizeRows = false;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.psmColumn,
            this.psmIncludeColumn});
            this.dataGridView1.Location = new System.Drawing.Point(3, 3);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.Size = new System.Drawing.Size(278, 176);
            this.dataGridView1.TabIndex = 0;
            // 
            // psmColumn
            // 
            this.psmColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.psmColumn.HeaderText = "PSM name";
            this.psmColumn.Name = "psmColumn";
            // 
            // psmIncludeColumn
            // 
            this.psmIncludeColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.psmIncludeColumn.FillWeight = 30F;
            this.psmIncludeColumn.HeaderText = "Include?";
            this.psmIncludeColumn.Name = "psmIncludeColumn";
            this.psmIncludeColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.psmIncludeColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // ExportPSMButton
            // 
            this.ExportPSMButton.Location = new System.Drawing.Point(197, 206);
            this.ExportPSMButton.Name = "ExportPSMButton";
            this.ExportPSMButton.Size = new System.Drawing.Size(75, 23);
            this.ExportPSMButton.TabIndex = 1;
            this.ExportPSMButton.Text = "Export";
            this.ExportPSMButton.UseVisualStyleBackColor = true;
            // 
            // ExportLibrarySettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 241);
            this.Controls.Add(this.ExportLibraryPanel);
            this.Controls.Add(this.ExportPSMsPanel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportLibrarySettings";
            this.Text = "ExportLibrarySettings";
            this.Load += new System.EventHandler(this.ExportLibrarySettings_Load);
            ((System.ComponentModel.ISupportInitialize)(this.SpectrumNumBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.FragmentNumBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.PrecursorNumBox)).EndInit();
            this.ExportLibraryPanel.ResumeLayout(false);
            this.ExportLibraryPanel.PerformLayout();
            this.ExportPSMsPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox methodBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox outputFormatBox;
        private System.Windows.Forms.CheckBox decoysBox;
        private System.Windows.Forms.CheckBox crossBox;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.NumericUpDown SpectrumNumBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown FragmentNumBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown PrecursorNumBox;
        private System.Windows.Forms.Label PrecursorLabel;
        private System.Windows.Forms.Panel ExportLibraryPanel;
        private System.Windows.Forms.Panel ExportPSMsPanel;
        private IDPicker.Controls.AutomationDataGridView dataGridView1;
        private System.Windows.Forms.DataGridViewTextBoxColumn psmColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn psmIncludeColumn;
        private System.Windows.Forms.Button ExportPSMButton;
    }
}