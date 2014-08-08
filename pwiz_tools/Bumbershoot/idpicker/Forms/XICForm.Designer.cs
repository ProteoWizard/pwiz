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
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

namespace IDPicker.Forms
{
    partial class XICForm
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
            this.StartButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.MonoisotopicAdjustmentMinBox = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.MonoisotopicAdjustmentMaxBox = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.ChromatogramMzLowerOffsetValueBox = new System.Windows.Forms.NumericUpDown();
            this.ChromatogramMzLowerOffsetUnitsBox = new System.Windows.Forms.ComboBox();
            this.ChromatogramMzUpperOffsetUnitsBox = new System.Windows.Forms.ComboBox();
            this.ChromatogramMzUpperOffsetValueBox = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.SourceLocationLabel = new System.Windows.Forms.Label();
            this.SourceLocationBox = new System.Windows.Forms.TextBox();
            this.SourceLocationBrowse = new System.Windows.Forms.Button();
            this.ContentPanel = new System.Windows.Forms.Panel();
            this.label9 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.RTTolLowerBox = new System.Windows.Forms.NumericUpDown();
            this.label7 = new System.Windows.Forms.Label();
            this.RTTolUpperBox = new System.Windows.Forms.NumericUpDown();
            this.RTGroupingPanel = new System.Windows.Forms.Panel();
            this.RTAlignBox = new System.Windows.Forms.CheckBox();
            this.RTAlignInfo = new System.Windows.Forms.LinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.MonoisotopicAdjustmentMinBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MonoisotopicAdjustmentMaxBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ChromatogramMzLowerOffsetValueBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ChromatogramMzUpperOffsetValueBox)).BeginInit();
            this.ContentPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.RTTolLowerBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.RTTolUpperBox)).BeginInit();
            this.SuspendLayout();
            // 
            // StartButton
            // 
            this.StartButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.StartButton.Location = new System.Drawing.Point(374, 205);
            this.StartButton.Name = "StartButton";
            this.StartButton.Size = new System.Drawing.Size(75, 23);
            this.StartButton.TabIndex = 0;
            this.StartButton.Text = "Start";
            this.StartButton.UseVisualStyleBackColor = true;
            this.StartButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(111, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(127, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Monoisotopic adjustment:";
            // 
            // MonoisotopicAdjustmentMinBox
            // 
            this.MonoisotopicAdjustmentMinBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.MonoisotopicAdjustmentMinBox.Location = new System.Drawing.Point(244, 15);
            this.MonoisotopicAdjustmentMinBox.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.MonoisotopicAdjustmentMinBox.Name = "MonoisotopicAdjustmentMinBox";
            this.MonoisotopicAdjustmentMinBox.Size = new System.Drawing.Size(47, 20);
            this.MonoisotopicAdjustmentMinBox.TabIndex = 2;
            this.MonoisotopicAdjustmentMinBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            // 
            // label2
            // 
            this.label2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(297, 17);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(10, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "-";
            // 
            // MonoisotopicAdjustmentMaxBox
            // 
            this.MonoisotopicAdjustmentMaxBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.MonoisotopicAdjustmentMaxBox.Location = new System.Drawing.Point(313, 15);
            this.MonoisotopicAdjustmentMaxBox.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.MonoisotopicAdjustmentMaxBox.Name = "MonoisotopicAdjustmentMaxBox";
            this.MonoisotopicAdjustmentMaxBox.Size = new System.Drawing.Size(47, 20);
            this.MonoisotopicAdjustmentMaxBox.TabIndex = 4;
            this.MonoisotopicAdjustmentMaxBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label3
            // 
            this.label3.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(86, 95);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(152, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Lower chromatigram tolerance:";
            // 
            // ChromatogramMzLowerOffsetValueBox
            // 
            this.ChromatogramMzLowerOffsetValueBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.ChromatogramMzLowerOffsetValueBox.DecimalPlaces = 2;
            this.ChromatogramMzLowerOffsetValueBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ChromatogramMzLowerOffsetValueBox.Location = new System.Drawing.Point(244, 93);
            this.ChromatogramMzLowerOffsetValueBox.Name = "ChromatogramMzLowerOffsetValueBox";
            this.ChromatogramMzLowerOffsetValueBox.Size = new System.Drawing.Size(59, 20);
            this.ChromatogramMzLowerOffsetValueBox.TabIndex = 6;
            this.ChromatogramMzLowerOffsetValueBox.Value = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            // 
            // ChromatogramMzLowerOffsetUnitsBox
            // 
            this.ChromatogramMzLowerOffsetUnitsBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.ChromatogramMzLowerOffsetUnitsBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ChromatogramMzLowerOffsetUnitsBox.FormattingEnabled = true;
            this.ChromatogramMzLowerOffsetUnitsBox.Items.AddRange(new object[] {
            "MZ",
            "PPM"});
            this.ChromatogramMzLowerOffsetUnitsBox.Location = new System.Drawing.Point(311, 92);
            this.ChromatogramMzLowerOffsetUnitsBox.Name = "ChromatogramMzLowerOffsetUnitsBox";
            this.ChromatogramMzLowerOffsetUnitsBox.Size = new System.Drawing.Size(69, 21);
            this.ChromatogramMzLowerOffsetUnitsBox.TabIndex = 7;
            // 
            // ChromatogramMzUpperOffsetUnitsBox
            // 
            this.ChromatogramMzUpperOffsetUnitsBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.ChromatogramMzUpperOffsetUnitsBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ChromatogramMzUpperOffsetUnitsBox.FormattingEnabled = true;
            this.ChromatogramMzUpperOffsetUnitsBox.Items.AddRange(new object[] {
            "MZ",
            "PPM"});
            this.ChromatogramMzUpperOffsetUnitsBox.Location = new System.Drawing.Point(311, 118);
            this.ChromatogramMzUpperOffsetUnitsBox.Name = "ChromatogramMzUpperOffsetUnitsBox";
            this.ChromatogramMzUpperOffsetUnitsBox.Size = new System.Drawing.Size(69, 21);
            this.ChromatogramMzUpperOffsetUnitsBox.TabIndex = 10;
            // 
            // ChromatogramMzUpperOffsetValueBox
            // 
            this.ChromatogramMzUpperOffsetValueBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.ChromatogramMzUpperOffsetValueBox.DecimalPlaces = 2;
            this.ChromatogramMzUpperOffsetValueBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ChromatogramMzUpperOffsetValueBox.Location = new System.Drawing.Point(244, 119);
            this.ChromatogramMzUpperOffsetValueBox.Name = "ChromatogramMzUpperOffsetValueBox";
            this.ChromatogramMzUpperOffsetValueBox.Size = new System.Drawing.Size(59, 20);
            this.ChromatogramMzUpperOffsetValueBox.TabIndex = 9;
            this.ChromatogramMzUpperOffsetValueBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label4
            // 
            this.label4.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(86, 121);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(152, 13);
            this.label4.TabIndex = 8;
            this.label4.Text = "Upper chromatigram tolerance:";
            // 
            // SourceLocationLabel
            // 
            this.SourceLocationLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SourceLocationLabel.AutoSize = true;
            this.SourceLocationLabel.Location = new System.Drawing.Point(11, 182);
            this.SourceLocationLabel.Name = "SourceLocationLabel";
            this.SourceLocationLabel.Size = new System.Drawing.Size(130, 13);
            this.SourceLocationLabel.TabIndex = 12;
            this.SourceLocationLabel.Text = "Spectrum source location:";
            // 
            // SourceLocationBox
            // 
            this.SourceLocationBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.SourceLocationBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.SourceLocationBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.SourceLocationBox.Location = new System.Drawing.Point(147, 179);
            this.SourceLocationBox.Name = "SourceLocationBox";
            this.SourceLocationBox.Size = new System.Drawing.Size(246, 20);
            this.SourceLocationBox.TabIndex = 13;
            this.SourceLocationBox.Text = "<Default>";
            // 
            // SourceLocationBrowse
            // 
            this.SourceLocationBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.SourceLocationBrowse.Location = new System.Drawing.Point(399, 177);
            this.SourceLocationBrowse.Name = "SourceLocationBrowse";
            this.SourceLocationBrowse.Size = new System.Drawing.Size(50, 23);
            this.SourceLocationBrowse.TabIndex = 14;
            this.SourceLocationBrowse.Text = "Browse";
            this.SourceLocationBrowse.UseVisualStyleBackColor = true;
            this.SourceLocationBrowse.Click += new System.EventHandler(this.SourceLocationBrowse_Click);
            // 
            // ContentPanel
            // 
            this.ContentPanel.Controls.Add(this.RTAlignInfo);
            this.ContentPanel.Controls.Add(this.RTAlignBox);
            this.ContentPanel.Controls.Add(this.label9);
            this.ContentPanel.Controls.Add(this.label8);
            this.ContentPanel.Controls.Add(this.label6);
            this.ContentPanel.Controls.Add(this.RTTolLowerBox);
            this.ContentPanel.Controls.Add(this.label7);
            this.ContentPanel.Controls.Add(this.RTTolUpperBox);
            this.ContentPanel.Controls.Add(this.SourceLocationBrowse);
            this.ContentPanel.Controls.Add(this.label1);
            this.ContentPanel.Controls.Add(this.SourceLocationBox);
            this.ContentPanel.Controls.Add(this.StartButton);
            this.ContentPanel.Controls.Add(this.SourceLocationLabel);
            this.ContentPanel.Controls.Add(this.MonoisotopicAdjustmentMinBox);
            this.ContentPanel.Controls.Add(this.label2);
            this.ContentPanel.Controls.Add(this.ChromatogramMzUpperOffsetUnitsBox);
            this.ContentPanel.Controls.Add(this.MonoisotopicAdjustmentMaxBox);
            this.ContentPanel.Controls.Add(this.ChromatogramMzUpperOffsetValueBox);
            this.ContentPanel.Controls.Add(this.label3);
            this.ContentPanel.Controls.Add(this.label4);
            this.ContentPanel.Controls.Add(this.ChromatogramMzLowerOffsetValueBox);
            this.ContentPanel.Controls.Add(this.ChromatogramMzLowerOffsetUnitsBox);
            this.ContentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ContentPanel.Location = new System.Drawing.Point(0, 0);
            this.ContentPanel.Name = "ContentPanel";
            this.ContentPanel.Size = new System.Drawing.Size(461, 240);
            this.ContentPanel.TabIndex = 15;
            // 
            // label9
            // 
            this.label9.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(309, 69);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(47, 13);
            this.label9.TabIndex = 20;
            this.label9.Text = "seconds";
            // 
            // label8
            // 
            this.label8.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(81, 69);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(157, 13);
            this.label8.TabIndex = 19;
            this.label8.Text = "Upper Retention time tolerance:";
            // 
            // label6
            // 
            this.label6.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(81, 43);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(157, 13);
            this.label6.TabIndex = 15;
            this.label6.Text = "Lower Retention time tolerance:";
            // 
            // RTTolLowerBox
            // 
            this.RTTolLowerBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.RTTolLowerBox.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.RTTolLowerBox.Location = new System.Drawing.Point(244, 41);
            this.RTTolLowerBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.RTTolLowerBox.Name = "RTTolLowerBox";
            this.RTTolLowerBox.Size = new System.Drawing.Size(59, 20);
            this.RTTolLowerBox.TabIndex = 16;
            this.RTTolLowerBox.Value = new decimal(new int[] {
            120,
            0,
            0,
            0});
            // 
            // label7
            // 
            this.label7.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(309, 43);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(47, 13);
            this.label7.TabIndex = 17;
            this.label7.Text = "seconds";
            // 
            // RTTolUpperBox
            // 
            this.RTTolUpperBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.RTTolUpperBox.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.RTTolUpperBox.Location = new System.Drawing.Point(244, 67);
            this.RTTolUpperBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.RTTolUpperBox.Name = "RTTolUpperBox";
            this.RTTolUpperBox.Size = new System.Drawing.Size(59, 20);
            this.RTTolUpperBox.TabIndex = 18;
            this.RTTolUpperBox.Value = new decimal(new int[] {
            120,
            0,
            0,
            0});
            // 
            // RTGroupingPanel
            // 
            this.RTGroupingPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RTGroupingPanel.Location = new System.Drawing.Point(0, 0);
            this.RTGroupingPanel.Name = "RTGroupingPanel";
            this.RTGroupingPanel.Size = new System.Drawing.Size(461, 240);
            this.RTGroupingPanel.TabIndex = 16;
            // 
            // RTAlignBox
            // 
            this.RTAlignBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.RTAlignBox.AutoSize = true;
            this.RTAlignBox.Location = new System.Drawing.Point(100, 148);
            this.RTAlignBox.Name = "RTAlignBox";
            this.RTAlignBox.Size = new System.Drawing.Size(124, 17);
            this.RTAlignBox.TabIndex = 21;
            this.RTAlignBox.Text = "Align Retention Time";
            this.RTAlignBox.UseVisualStyleBackColor = true;
            this.RTAlignBox.Visible = false;
            // 
            // RTAlignInfo
            // 
            this.RTAlignInfo.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.RTAlignInfo.AutoSize = true;
            this.RTAlignInfo.Location = new System.Drawing.Point(219, 149);
            this.RTAlignInfo.Name = "RTAlignInfo";
            this.RTAlignInfo.Size = new System.Drawing.Size(142, 13);
            this.RTAlignInfo.TabIndex = 22;
            this.RTAlignInfo.TabStop = true;
            this.RTAlignInfo.Text = "(Disabled if R is not installed)";
            this.RTAlignInfo.Visible = false;
            this.RTAlignInfo.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.RTAlignInfo_LinkClicked);
            // 
            // XICForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(461, 240);
            this.Controls.Add(this.ContentPanel);
            this.Controls.Add(this.RTGroupingPanel);
            this.MinimumSize = new System.Drawing.Size(343, 235);
            this.Name = "XICForm";
            this.Text = "Embed XIC Metrics";
            this.Load += new System.EventHandler(this.XICForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.MonoisotopicAdjustmentMinBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MonoisotopicAdjustmentMaxBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ChromatogramMzLowerOffsetValueBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ChromatogramMzUpperOffsetValueBox)).EndInit();
            this.ContentPanel.ResumeLayout(false);
            this.ContentPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.RTTolLowerBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.RTTolUpperBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button StartButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown MonoisotopicAdjustmentMinBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown MonoisotopicAdjustmentMaxBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown ChromatogramMzLowerOffsetValueBox;
        private System.Windows.Forms.ComboBox ChromatogramMzLowerOffsetUnitsBox;
        private System.Windows.Forms.ComboBox ChromatogramMzUpperOffsetUnitsBox;
        private System.Windows.Forms.NumericUpDown ChromatogramMzUpperOffsetValueBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label SourceLocationLabel;
        private System.Windows.Forms.TextBox SourceLocationBox;
        private System.Windows.Forms.Button SourceLocationBrowse;
        private System.Windows.Forms.Panel ContentPanel;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.NumericUpDown RTTolLowerBox;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown RTTolUpperBox;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.LinkLabel RTAlignInfo;
        private System.Windows.Forms.CheckBox RTAlignBox;
        private System.Windows.Forms.Panel RTGroupingPanel;
    }
}