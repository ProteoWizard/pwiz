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
            ((System.ComponentModel.ISupportInitialize)(this.SpectrumNumBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.FragmentNumBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.PrecursorNumBox)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 92);
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
            this.methodBox.Location = new System.Drawing.Point(145, 89);
            this.methodBox.Name = "methodBox";
            this.methodBox.Size = new System.Drawing.Size(127, 21);
            this.methodBox.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(65, 119);
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
            this.outputFormatBox.Location = new System.Drawing.Point(145, 116);
            this.outputFormatBox.Name = "outputFormatBox";
            this.outputFormatBox.Size = new System.Drawing.Size(127, 21);
            this.outputFormatBox.TabIndex = 3;
            // 
            // decoysBox
            // 
            this.decoysBox.AutoSize = true;
            this.decoysBox.Checked = true;
            this.decoysBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.decoysBox.Location = new System.Drawing.Point(145, 143);
            this.decoysBox.Name = "decoysBox";
            this.decoysBox.Size = new System.Drawing.Size(84, 17);
            this.decoysBox.TabIndex = 4;
            this.decoysBox.Text = "Add Decoys";
            this.decoysBox.UseVisualStyleBackColor = true;
            // 
            // crossBox
            // 
            this.crossBox.AutoSize = true;
            this.crossBox.Location = new System.Drawing.Point(145, 166);
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
            this.startButton.Location = new System.Drawing.Point(197, 206);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(75, 23);
            this.startButton.TabIndex = 6;
            this.startButton.Text = "Start";
            this.startButton.UseVisualStyleBackColor = true;
            // 
            // SpectrumNumBox
            // 
            this.SpectrumNumBox.Location = new System.Drawing.Point(145, 63);
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
            this.label3.Location = new System.Drawing.Point(18, 65);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(121, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Min spectra per peptide:";
            // 
            // FragmentNumBox
            // 
            this.FragmentNumBox.DecimalPlaces = 3;
            this.FragmentNumBox.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.FragmentNumBox.Location = new System.Drawing.Point(145, 37);
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
            this.label4.Location = new System.Drawing.Point(17, 39);
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
            this.PrecursorNumBox.Location = new System.Drawing.Point(145, 11);
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
            this.PrecursorLabel.Location = new System.Drawing.Point(16, 13);
            this.PrecursorLabel.Name = "PrecursorLabel";
            this.PrecursorLabel.Size = new System.Drawing.Size(123, 13);
            this.PrecursorLabel.TabIndex = 11;
            this.PrecursorLabel.Text = "Precursor m/z tolerance:";
            // 
            // ExportLibrarySettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 241);
            this.Controls.Add(this.PrecursorNumBox);
            this.Controls.Add(this.PrecursorLabel);
            this.Controls.Add(this.SpectrumNumBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.FragmentNumBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.crossBox);
            this.Controls.Add(this.decoysBox);
            this.Controls.Add(this.outputFormatBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.methodBox);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportLibrarySettings";
            this.Text = "ExportLibrarySettings";
            this.Load += new System.EventHandler(this.ExportLibrarySettings_Load);
            ((System.ComponentModel.ISupportInitialize)(this.SpectrumNumBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.FragmentNumBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.PrecursorNumBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

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
    }
}