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
    partial class QuantitationSettingsForm
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
            this.RTAlignInfo = new System.Windows.Forms.LinkLabel();
            this.RTAlignBox = new System.Windows.Forms.CheckBox();
            this.label9 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.RTTolLowerBox = new System.Windows.Forms.NumericUpDown();
            this.label7 = new System.Windows.Forms.Label();
            this.RTTolUpperBox = new System.Windows.Forms.NumericUpDown();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.labelFreeSettingsTabPage = new System.Windows.Forms.TabPage();
            this.isobaricQuantSettingsTabPage = new System.Windows.Forms.TabPage();
            this.label10 = new System.Windows.Forms.Label();
            this.normalizeReporterIonsCheckBox = new System.Windows.Forms.CheckBox();
            this.reporterIonToleranceUnits = new System.Windows.Forms.ComboBox();
            this.reporterIonToleranceUpDown = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.MonoisotopicAdjustmentMinBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MonoisotopicAdjustmentMaxBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ChromatogramMzLowerOffsetValueBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ChromatogramMzUpperOffsetValueBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.RTTolLowerBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.RTTolUpperBox)).BeginInit();
            this.tabControl.SuspendLayout();
            this.labelFreeSettingsTabPage.SuspendLayout();
            this.isobaricQuantSettingsTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.reporterIonToleranceUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // StartButton
            // 
            this.StartButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.StartButton.Location = new System.Drawing.Point(411, 237);
            this.StartButton.Name = "StartButton";
            this.StartButton.Size = new System.Drawing.Size(75, 23);
            this.StartButton.TabIndex = 12;
            this.StartButton.Text = "OK";
            this.StartButton.UseVisualStyleBackColor = true;
            this.StartButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(127, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(127, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Monoisotopic adjustment:";
            // 
            // MonoisotopicAdjustmentMinBox
            // 
            this.MonoisotopicAdjustmentMinBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.MonoisotopicAdjustmentMinBox.Location = new System.Drawing.Point(260, 23);
            this.MonoisotopicAdjustmentMinBox.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.MonoisotopicAdjustmentMinBox.Name = "MonoisotopicAdjustmentMinBox";
            this.MonoisotopicAdjustmentMinBox.Size = new System.Drawing.Size(47, 20);
            this.MonoisotopicAdjustmentMinBox.TabIndex = 1;
            this.MonoisotopicAdjustmentMinBox.Enter += new System.EventHandler(this.NumericUpDownEnter);
            // 
            // label2
            // 
            this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(313, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(10, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "-";
            // 
            // MonoisotopicAdjustmentMaxBox
            // 
            this.MonoisotopicAdjustmentMaxBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.MonoisotopicAdjustmentMaxBox.Location = new System.Drawing.Point(329, 23);
            this.MonoisotopicAdjustmentMaxBox.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.MonoisotopicAdjustmentMaxBox.Name = "MonoisotopicAdjustmentMaxBox";
            this.MonoisotopicAdjustmentMaxBox.Size = new System.Drawing.Size(47, 20);
            this.MonoisotopicAdjustmentMaxBox.TabIndex = 2;
            this.MonoisotopicAdjustmentMaxBox.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.MonoisotopicAdjustmentMaxBox.Enter += new System.EventHandler(this.NumericUpDownEnter);
            // 
            // label3
            // 
            this.label3.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(98, 103);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(156, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Lower chromatogram tolerance:";
            // 
            // ChromatogramMzLowerOffsetValueBox
            // 
            this.ChromatogramMzLowerOffsetValueBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.ChromatogramMzLowerOffsetValueBox.DecimalPlaces = 2;
            this.ChromatogramMzLowerOffsetValueBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ChromatogramMzLowerOffsetValueBox.Location = new System.Drawing.Point(260, 101);
            this.ChromatogramMzLowerOffsetValueBox.Name = "ChromatogramMzLowerOffsetValueBox";
            this.ChromatogramMzLowerOffsetValueBox.Size = new System.Drawing.Size(59, 20);
            this.ChromatogramMzLowerOffsetValueBox.TabIndex = 5;
            this.ChromatogramMzLowerOffsetValueBox.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.ChromatogramMzLowerOffsetValueBox.Enter += new System.EventHandler(this.NumericUpDownEnter);
            // 
            // ChromatogramMzLowerOffsetUnitsBox
            // 
            this.ChromatogramMzLowerOffsetUnitsBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.ChromatogramMzLowerOffsetUnitsBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ChromatogramMzLowerOffsetUnitsBox.FormattingEnabled = true;
            this.ChromatogramMzLowerOffsetUnitsBox.Items.AddRange(new object[] {
            "MZ",
            "PPM"});
            this.ChromatogramMzLowerOffsetUnitsBox.Location = new System.Drawing.Point(327, 100);
            this.ChromatogramMzLowerOffsetUnitsBox.Name = "ChromatogramMzLowerOffsetUnitsBox";
            this.ChromatogramMzLowerOffsetUnitsBox.Size = new System.Drawing.Size(69, 21);
            this.ChromatogramMzLowerOffsetUnitsBox.TabIndex = 6;
            // 
            // ChromatogramMzUpperOffsetUnitsBox
            // 
            this.ChromatogramMzUpperOffsetUnitsBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.ChromatogramMzUpperOffsetUnitsBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ChromatogramMzUpperOffsetUnitsBox.FormattingEnabled = true;
            this.ChromatogramMzUpperOffsetUnitsBox.Items.AddRange(new object[] {
            "MZ",
            "PPM"});
            this.ChromatogramMzUpperOffsetUnitsBox.Location = new System.Drawing.Point(327, 126);
            this.ChromatogramMzUpperOffsetUnitsBox.Name = "ChromatogramMzUpperOffsetUnitsBox";
            this.ChromatogramMzUpperOffsetUnitsBox.Size = new System.Drawing.Size(69, 21);
            this.ChromatogramMzUpperOffsetUnitsBox.TabIndex = 8;
            // 
            // ChromatogramMzUpperOffsetValueBox
            // 
            this.ChromatogramMzUpperOffsetValueBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.ChromatogramMzUpperOffsetValueBox.DecimalPlaces = 2;
            this.ChromatogramMzUpperOffsetValueBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ChromatogramMzUpperOffsetValueBox.Location = new System.Drawing.Point(260, 127);
            this.ChromatogramMzUpperOffsetValueBox.Name = "ChromatogramMzUpperOffsetValueBox";
            this.ChromatogramMzUpperOffsetValueBox.Size = new System.Drawing.Size(59, 20);
            this.ChromatogramMzUpperOffsetValueBox.TabIndex = 7;
            this.ChromatogramMzUpperOffsetValueBox.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.ChromatogramMzUpperOffsetValueBox.Enter += new System.EventHandler(this.NumericUpDownEnter);
            // 
            // label4
            // 
            this.label4.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(98, 129);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(156, 13);
            this.label4.TabIndex = 8;
            this.label4.Text = "Upper chromatogram tolerance:";
            // 
            // RTAlignInfo
            // 
            this.RTAlignInfo.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.RTAlignInfo.AutoSize = true;
            this.RTAlignInfo.Location = new System.Drawing.Point(237, 159);
            this.RTAlignInfo.Name = "RTAlignInfo";
            this.RTAlignInfo.Size = new System.Drawing.Size(142, 13);
            this.RTAlignInfo.TabIndex = 22;
            this.RTAlignInfo.TabStop = true;
            this.RTAlignInfo.Text = "(Disabled if R is not installed)";
            this.RTAlignInfo.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.RTAlignInfo_LinkClicked);
            // 
            // RTAlignBox
            // 
            this.RTAlignBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.RTAlignBox.AutoSize = true;
            this.RTAlignBox.Location = new System.Drawing.Point(116, 158);
            this.RTAlignBox.Name = "RTAlignBox";
            this.RTAlignBox.Size = new System.Drawing.Size(115, 17);
            this.RTAlignBox.TabIndex = 9;
            this.RTAlignBox.Text = "Align retention time";
            this.RTAlignBox.UseVisualStyleBackColor = true;
            // 
            // label9
            // 
            this.label9.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(325, 77);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(47, 13);
            this.label9.TabIndex = 20;
            this.label9.Text = "seconds";
            // 
            // label8
            // 
            this.label8.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(102, 77);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(152, 13);
            this.label8.TabIndex = 19;
            this.label8.Text = "Upper retention time tolerance:";
            // 
            // label6
            // 
            this.label6.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(102, 51);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(152, 13);
            this.label6.TabIndex = 15;
            this.label6.Text = "Lower retention time tolerance:";
            // 
            // RTTolLowerBox
            // 
            this.RTTolLowerBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.RTTolLowerBox.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.RTTolLowerBox.Location = new System.Drawing.Point(260, 49);
            this.RTTolLowerBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.RTTolLowerBox.Name = "RTTolLowerBox";
            this.RTTolLowerBox.Size = new System.Drawing.Size(59, 20);
            this.RTTolLowerBox.TabIndex = 3;
            this.RTTolLowerBox.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this.RTTolLowerBox.Enter += new System.EventHandler(this.NumericUpDownEnter);
            // 
            // label7
            // 
            this.label7.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(325, 51);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(47, 13);
            this.label7.TabIndex = 17;
            this.label7.Text = "seconds";
            // 
            // RTTolUpperBox
            // 
            this.RTTolUpperBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.RTTolUpperBox.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.RTTolUpperBox.Location = new System.Drawing.Point(260, 75);
            this.RTTolUpperBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.RTTolUpperBox.Name = "RTTolUpperBox";
            this.RTTolUpperBox.Size = new System.Drawing.Size(59, 20);
            this.RTTolUpperBox.TabIndex = 4;
            this.RTTolUpperBox.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this.RTTolUpperBox.Enter += new System.EventHandler(this.NumericUpDownEnter);
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.labelFreeSettingsTabPage);
            this.tabControl.Controls.Add(this.isobaricQuantSettingsTabPage);
            this.tabControl.Location = new System.Drawing.Point(1, 3);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(498, 222);
            this.tabControl.TabIndex = 16;
            // 
            // labelFreeSettingsTabPage
            // 
            this.labelFreeSettingsTabPage.BackColor = System.Drawing.SystemColors.Control;
            this.labelFreeSettingsTabPage.Controls.Add(this.RTAlignInfo);
            this.labelFreeSettingsTabPage.Controls.Add(this.RTAlignBox);
            this.labelFreeSettingsTabPage.Controls.Add(this.label1);
            this.labelFreeSettingsTabPage.Controls.Add(this.label9);
            this.labelFreeSettingsTabPage.Controls.Add(this.ChromatogramMzLowerOffsetUnitsBox);
            this.labelFreeSettingsTabPage.Controls.Add(this.label8);
            this.labelFreeSettingsTabPage.Controls.Add(this.ChromatogramMzLowerOffsetValueBox);
            this.labelFreeSettingsTabPage.Controls.Add(this.label6);
            this.labelFreeSettingsTabPage.Controls.Add(this.label4);
            this.labelFreeSettingsTabPage.Controls.Add(this.RTTolLowerBox);
            this.labelFreeSettingsTabPage.Controls.Add(this.label3);
            this.labelFreeSettingsTabPage.Controls.Add(this.label7);
            this.labelFreeSettingsTabPage.Controls.Add(this.ChromatogramMzUpperOffsetValueBox);
            this.labelFreeSettingsTabPage.Controls.Add(this.RTTolUpperBox);
            this.labelFreeSettingsTabPage.Controls.Add(this.MonoisotopicAdjustmentMaxBox);
            this.labelFreeSettingsTabPage.Controls.Add(this.ChromatogramMzUpperOffsetUnitsBox);
            this.labelFreeSettingsTabPage.Controls.Add(this.label2);
            this.labelFreeSettingsTabPage.Controls.Add(this.MonoisotopicAdjustmentMinBox);
            this.labelFreeSettingsTabPage.Location = new System.Drawing.Point(4, 22);
            this.labelFreeSettingsTabPage.Name = "labelFreeSettingsTabPage";
            this.labelFreeSettingsTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.labelFreeSettingsTabPage.Size = new System.Drawing.Size(490, 196);
            this.labelFreeSettingsTabPage.TabIndex = 0;
            this.labelFreeSettingsTabPage.Text = "Label-Free";
            // 
            // isobaricQuantSettingsTabPage
            // 
            this.isobaricQuantSettingsTabPage.BackColor = System.Drawing.SystemColors.Control;
            this.isobaricQuantSettingsTabPage.Controls.Add(this.label10);
            this.isobaricQuantSettingsTabPage.Controls.Add(this.normalizeReporterIonsCheckBox);
            this.isobaricQuantSettingsTabPage.Controls.Add(this.reporterIonToleranceUnits);
            this.isobaricQuantSettingsTabPage.Controls.Add(this.reporterIonToleranceUpDown);
            this.isobaricQuantSettingsTabPage.Controls.Add(this.label5);
            this.isobaricQuantSettingsTabPage.Location = new System.Drawing.Point(4, 22);
            this.isobaricQuantSettingsTabPage.Name = "isobaricQuantSettingsTabPage";
            this.isobaricQuantSettingsTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.isobaricQuantSettingsTabPage.Size = new System.Drawing.Size(490, 196);
            this.isobaricQuantSettingsTabPage.TabIndex = 1;
            this.isobaricQuantSettingsTabPage.Text = "iTRAQ/TMT";
            // 
            // label10
            // 
            this.label10.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(264, 50);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(177, 13);
            this.label10.TabIndex = 11;
            this.label10.Text = "(assume backround ratio of 1:1:1:...)";
            // 
            // normalizeReporterIonsCheckBox
            // 
            this.normalizeReporterIonsCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.normalizeReporterIonsCheckBox.AutoSize = true;
            this.normalizeReporterIonsCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.normalizeReporterIonsCheckBox.Location = new System.Drawing.Point(80, 49);
            this.normalizeReporterIonsCheckBox.Name = "normalizeReporterIonsCheckBox";
            this.normalizeReporterIonsCheckBox.Size = new System.Drawing.Size(180, 17);
            this.normalizeReporterIonsCheckBox.TabIndex = 10;
            this.normalizeReporterIonsCheckBox.Text = "Normalize reporter ion intensities:";
            this.normalizeReporterIonsCheckBox.UseVisualStyleBackColor = true;
            // 
            // reporterIonToleranceUnits
            // 
            this.reporterIonToleranceUnits.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.reporterIonToleranceUnits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.reporterIonToleranceUnits.FormattingEnabled = true;
            this.reporterIonToleranceUnits.Items.AddRange(new object[] {
            "MZ",
            "PPM"});
            this.reporterIonToleranceUnits.Location = new System.Drawing.Point(315, 22);
            this.reporterIonToleranceUnits.Name = "reporterIonToleranceUnits";
            this.reporterIonToleranceUnits.Size = new System.Drawing.Size(69, 21);
            this.reporterIonToleranceUnits.TabIndex = 9;
            // 
            // reporterIonToleranceUpDown
            // 
            this.reporterIonToleranceUpDown.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.reporterIonToleranceUpDown.DecimalPlaces = 4;
            this.reporterIonToleranceUpDown.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.reporterIonToleranceUpDown.Location = new System.Drawing.Point(248, 23);
            this.reporterIonToleranceUpDown.Name = "reporterIonToleranceUpDown";
            this.reporterIonToleranceUpDown.Size = new System.Drawing.Size(59, 20);
            this.reporterIonToleranceUpDown.TabIndex = 7;
            this.reporterIonToleranceUpDown.Value = new decimal(new int[] {
            15,
            0,
            0,
            196608});
            // 
            // label5
            // 
            this.label5.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(106, 25);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(136, 13);
            this.label5.TabIndex = 8;
            this.label5.Text = "Reporter ion m/z tolerance:";
            // 
            // QuantitationSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(498, 272);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.StartButton);
            this.Location = new System.Drawing.Point(406, 262);
            this.Name = "QuantitationSettingsForm";
            this.ShowIcon = false;
            this.Text = "Quantitation Settings";
            this.Load += new System.EventHandler(this.XICForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.MonoisotopicAdjustmentMinBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MonoisotopicAdjustmentMaxBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ChromatogramMzLowerOffsetValueBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ChromatogramMzUpperOffsetValueBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.RTTolLowerBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.RTTolUpperBox)).EndInit();
            this.tabControl.ResumeLayout(false);
            this.labelFreeSettingsTabPage.ResumeLayout(false);
            this.labelFreeSettingsTabPage.PerformLayout();
            this.isobaricQuantSettingsTabPage.ResumeLayout(false);
            this.isobaricQuantSettingsTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.reporterIonToleranceUpDown)).EndInit();
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
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.NumericUpDown RTTolLowerBox;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown RTTolUpperBox;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.LinkLabel RTAlignInfo;
        private System.Windows.Forms.CheckBox RTAlignBox;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage labelFreeSettingsTabPage;
        private System.Windows.Forms.TabPage isobaricQuantSettingsTabPage;
        private System.Windows.Forms.ComboBox reporterIonToleranceUnits;
        private System.Windows.Forms.NumericUpDown reporterIonToleranceUpDown;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox normalizeReporterIonsCheckBox;
        private System.Windows.Forms.Label label10;
    }
}