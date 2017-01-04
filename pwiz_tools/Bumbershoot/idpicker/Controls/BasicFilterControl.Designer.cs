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
    partial class BasicFilterControl
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
            System.Windows.Forms.Label label4;
            System.Windows.Forms.Label label9;
            System.Windows.Forms.Label label11;
            this.proteinLevelFilterGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.filterByGeneCheckBox = new System.Windows.Forms.CheckBox();
            this.minSpectraTextBox = new System.Windows.Forms.TextBox();
            this.minDistinctPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.minAdditionalPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.psmLevelFilterGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.maxProteinGroupsTextBox = new System.Windows.Forms.TextBox();
            this.minSpectraPerMatchTextBox = new System.Windows.Forms.TextBox();
            this.maxQValueComboBox = new System.Windows.Forms.ComboBox();
            this.minSpectraPerPeptideTextBox = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.precursorMzToleranceTextBox = new System.Windows.Forms.TextBox();
            this.precursorMzToleranceUnitsComboBox = new System.Windows.Forms.ComboBox();
            this.CloseLabel = new System.Windows.Forms.LinkLabel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.CropAssemblyLabel = new System.Windows.Forms.LinkLabel();
            this.QonverterLabel = new System.Windows.Forms.LinkLabel();
            this.distinctMatchFormatGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.modificationRoundToMassTextBox = new System.Windows.Forms.TextBox();
            this.modificationsAreDistinctCheckbox = new System.Windows.Forms.CheckBox();
            this.analysisIsDistinctCheckBox = new System.Windows.Forms.CheckBox();
            this.chargeIsDistinctCheckBox = new System.Windows.Forms.CheckBox();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            label9 = new System.Windows.Forms.Label();
            label11 = new System.Windows.Forms.Label();
            this.proteinLevelFilterGroupBox.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.psmLevelFilterGroupBox.SuspendLayout();
            this.tableLayoutPanel4.SuspendLayout();
            this.panel1.SuspendLayout();
            this.distinctMatchFormatGroupBox.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label4
            // 
            label4.Anchor = System.Windows.Forms.AnchorStyles.Left;
            label4.AutoSize = true;
            label4.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label4.Location = new System.Drawing.Point(3, 5);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(132, 13);
            label4.TabIndex = 127;
            label4.Text = "Minimum distinct peptides:";
            // 
            // label9
            // 
            label9.Anchor = System.Windows.Forms.AnchorStyles.Left;
            label9.AutoSize = true;
            label9.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label9.Location = new System.Drawing.Point(3, 32);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(148, 13);
            label9.TabIndex = 127;
            label9.Text = "Minimum spectra per peptide:";
            // 
            // label11
            // 
            label11.Anchor = System.Windows.Forms.AnchorStyles.Left;
            label11.AutoSize = true;
            label11.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label11.Location = new System.Drawing.Point(3, 82);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(158, 13);
            label11.TabIndex = 148;
            label11.Text = "Mod. mass rounded to nearest:";
            // 
            // proteinLevelFilterGroupBox
            // 
            this.proteinLevelFilterGroupBox.Controls.Add(this.tableLayoutPanel2);
            this.proteinLevelFilterGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.proteinLevelFilterGroupBox.Location = new System.Drawing.Point(3, 164);
            this.proteinLevelFilterGroupBox.Name = "proteinLevelFilterGroupBox";
            this.proteinLevelFilterGroupBox.Size = new System.Drawing.Size(277, 123);
            this.proteinLevelFilterGroupBox.TabIndex = 1;
            this.proteinLevelFilterGroupBox.TabStop = false;
            this.proteinLevelFilterGroupBox.Text = "Protein/Gene Level Filters";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.Controls.Add(this.filterByGeneCheckBox, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this.minSpectraTextBox, 1, 2);
            this.tableLayoutPanel2.Controls.Add(label4, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.minDistinctPeptidesTextBox, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.minAdditionalPeptidesTextBox, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this.label5, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.label6, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.label15, 0, 3);
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 20);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 4;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(219, 97);
            this.tableLayoutPanel2.TabIndex = 141;
            // 
            // filterByGeneCheckBox
            // 
            this.filterByGeneCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.filterByGeneCheckBox.AutoSize = true;
            this.filterByGeneCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.filterByGeneCheckBox.Location = new System.Drawing.Point(170, 77);
            this.filterByGeneCheckBox.Name = "filterByGeneCheckBox";
            this.filterByGeneCheckBox.Size = new System.Drawing.Size(15, 14);
            this.filterByGeneCheckBox.TabIndex = 7;
            this.filterByGeneCheckBox.UseVisualStyleBackColor = true;
            this.filterByGeneCheckBox.CheckedChanged += new System.EventHandler(this.filterControl_CheckedChanged);
            // 
            // minSpectraTextBox
            // 
            this.minSpectraTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minSpectraTextBox.Location = new System.Drawing.Point(170, 51);
            this.minSpectraTextBox.Name = "minSpectraTextBox";
            this.minSpectraTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraTextBox.TabIndex = 6;
            this.minSpectraTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minSpectraTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // minDistinctPeptidesTextBox
            // 
            this.minDistinctPeptidesTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minDistinctPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minDistinctPeptidesTextBox.Location = new System.Drawing.Point(170, 3);
            this.minDistinctPeptidesTextBox.Name = "minDistinctPeptidesTextBox";
            this.minDistinctPeptidesTextBox.Size = new System.Drawing.Size(46, 21);
            this.minDistinctPeptidesTextBox.TabIndex = 4;
            this.minDistinctPeptidesTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minDistinctPeptidesTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // minAdditionalPeptidesTextBox
            // 
            this.minAdditionalPeptidesTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minAdditionalPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minAdditionalPeptidesTextBox.Location = new System.Drawing.Point(170, 27);
            this.minAdditionalPeptidesTextBox.Name = "minAdditionalPeptidesTextBox";
            this.minAdditionalPeptidesTextBox.Size = new System.Drawing.Size(46, 21);
            this.minAdditionalPeptidesTextBox.TabIndex = 5;
            this.minAdditionalPeptidesTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minAdditionalPeptidesTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // label5
            // 
            this.label5.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 53);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(90, 13);
            this.label5.TabIndex = 133;
            this.label5.Text = "Minimum spectra:";
            // 
            // label6
            // 
            this.label6.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(3, 29);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(144, 13);
            this.label6.TabIndex = 132;
            this.label6.Text = "Minimum additional peptides:";
            // 
            // label15
            // 
            this.label15.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(3, 78);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(138, 13);
            this.label15.TabIndex = 142;
            this.label15.Text = "Filter by gene (if possible): ";
            // 
            // psmLevelFilterGroupBox
            // 
            this.psmLevelFilterGroupBox.Controls.Add(this.tableLayoutPanel4);
            this.psmLevelFilterGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.psmLevelFilterGroupBox.Location = new System.Drawing.Point(3, 3);
            this.psmLevelFilterGroupBox.Name = "psmLevelFilterGroupBox";
            this.psmLevelFilterGroupBox.Size = new System.Drawing.Size(280, 158);
            this.psmLevelFilterGroupBox.TabIndex = 0;
            this.psmLevelFilterGroupBox.TabStop = false;
            this.psmLevelFilterGroupBox.Text = "Peptide-Spectrum-Match Filters";
            // 
            // tableLayoutPanel4
            // 
            this.tableLayoutPanel4.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel4.ColumnCount = 3;
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 55F));
            this.tableLayoutPanel4.Controls.Add(this.label7, 0, 3);
            this.tableLayoutPanel4.Controls.Add(this.label8, 0, 2);
            this.tableLayoutPanel4.Controls.Add(this.maxProteinGroupsTextBox, 1, 3);
            this.tableLayoutPanel4.Controls.Add(this.minSpectraPerMatchTextBox, 1, 2);
            this.tableLayoutPanel4.Controls.Add(this.maxQValueComboBox, 1, 0);
            this.tableLayoutPanel4.Controls.Add(this.minSpectraPerPeptideTextBox, 1, 1);
            this.tableLayoutPanel4.Controls.Add(label9, 0, 1);
            this.tableLayoutPanel4.Controls.Add(this.label10, 0, 0);
            this.tableLayoutPanel4.Controls.Add(this.label21, 2, 0);
            this.tableLayoutPanel4.Controls.Add(this.label1, 0, 4);
            this.tableLayoutPanel4.Controls.Add(this.precursorMzToleranceTextBox, 1, 4);
            this.tableLayoutPanel4.Controls.Add(this.precursorMzToleranceUnitsComboBox, 2, 4);
            this.tableLayoutPanel4.Location = new System.Drawing.Point(6, 20);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            this.tableLayoutPanel4.RowCount = 5;
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel4.Size = new System.Drawing.Size(274, 132);
            this.tableLayoutPanel4.TabIndex = 143;
            // 
            // label7
            // 
            this.label7.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(3, 84);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(128, 13);
            this.label7.TabIndex = 134;
            this.label7.Text = "Maximum protein groups:";
            // 
            // label8
            // 
            this.label8.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(3, 58);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(141, 13);
            this.label8.TabIndex = 132;
            this.label8.Text = "Minimum spectra per match:";
            // 
            // maxProteinGroupsTextBox
            // 
            this.maxProteinGroupsTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.maxProteinGroupsTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.maxProteinGroupsTextBox.Location = new System.Drawing.Point(170, 81);
            this.maxProteinGroupsTextBox.Name = "maxProteinGroupsTextBox";
            this.maxProteinGroupsTextBox.Size = new System.Drawing.Size(46, 21);
            this.maxProteinGroupsTextBox.TabIndex = 3;
            this.maxProteinGroupsTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.maxProteinGroupsTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // minSpectraPerMatchTextBox
            // 
            this.minSpectraPerMatchTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minSpectraPerMatchTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minSpectraPerMatchTextBox.Location = new System.Drawing.Point(170, 55);
            this.minSpectraPerMatchTextBox.Name = "minSpectraPerMatchTextBox";
            this.minSpectraPerMatchTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraPerMatchTextBox.TabIndex = 2;
            this.minSpectraPerMatchTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minSpectraPerMatchTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // maxQValueComboBox
            // 
            this.maxQValueComboBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.maxQValueComboBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.maxQValueComboBox.FormattingEnabled = true;
            this.maxQValueComboBox.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10"});
            this.maxQValueComboBox.Location = new System.Drawing.Point(170, 3);
            this.maxQValueComboBox.Name = "maxQValueComboBox";
            this.maxQValueComboBox.Size = new System.Drawing.Size(45, 21);
            this.maxQValueComboBox.TabIndex = 0;
            this.maxQValueComboBox.Text = "5";
            this.maxQValueComboBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.maxQValueComboBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.doubleTextBox_KeyDown);
            // 
            // minSpectraPerPeptideTextBox
            // 
            this.minSpectraPerPeptideTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minSpectraPerPeptideTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minSpectraPerPeptideTextBox.Location = new System.Drawing.Point(170, 29);
            this.minSpectraPerPeptideTextBox.Name = "minSpectraPerPeptideTextBox";
            this.minSpectraPerPeptideTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraPerPeptideTextBox.TabIndex = 1;
            this.minSpectraPerPeptideTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minSpectraPerPeptideTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // label10
            // 
            this.label10.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label10.AutoSize = true;
            this.label10.BackColor = System.Drawing.Color.Transparent;
            this.label10.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.Location = new System.Drawing.Point(3, 6);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(78, 13);
            this.label10.TabIndex = 125;
            this.label10.Text = "Maximum FDR:";
            // 
            // label21
            // 
            this.label21.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label21.AutoSize = true;
            this.label21.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label21.Location = new System.Drawing.Point(219, 6);
            this.label21.Margin = new System.Windows.Forms.Padding(0);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(18, 13);
            this.label21.TabIndex = 142;
            this.label21.Text = "%";
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(3, 111);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(111, 13);
            this.label1.TabIndex = 143;
            this.label1.Text = "Max. precursor error:";
            // 
            // precursorMzToleranceTextBox
            // 
            this.precursorMzToleranceTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.precursorMzToleranceTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.precursorMzToleranceTextBox.Location = new System.Drawing.Point(170, 107);
            this.precursorMzToleranceTextBox.Name = "precursorMzToleranceTextBox";
            this.precursorMzToleranceTextBox.Size = new System.Drawing.Size(46, 21);
            this.precursorMzToleranceTextBox.TabIndex = 144;
            this.precursorMzToleranceTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChangedOrEmpty);
            // 
            // precursorMzToleranceUnitsComboBox
            // 
            this.precursorMzToleranceUnitsComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.precursorMzToleranceUnitsComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.precursorMzToleranceUnitsComboBox.FormattingEnabled = true;
            this.precursorMzToleranceUnitsComboBox.Items.AddRange(new object[] {
            "m/z",
            "ppm"});
            this.precursorMzToleranceUnitsComboBox.Location = new System.Drawing.Point(224, 107);
            this.precursorMzToleranceUnitsComboBox.Name = "precursorMzToleranceUnitsComboBox";
            this.precursorMzToleranceUnitsComboBox.Size = new System.Drawing.Size(47, 21);
            this.precursorMzToleranceUnitsComboBox.TabIndex = 145;
            this.precursorMzToleranceUnitsComboBox.SelectedIndexChanged += new System.EventHandler(this.filterControl_SelectedIndexChanged);
            // 
            // CloseLabel
            // 
            this.CloseLabel.AutoSize = true;
            this.CloseLabel.Location = new System.Drawing.Point(192, 5);
            this.CloseLabel.Name = "CloseLabel";
            this.CloseLabel.Size = new System.Drawing.Size(82, 13);
            this.CloseLabel.TabIndex = 13;
            this.CloseLabel.TabStop = true;
            this.CloseLabel.Text = "Save and Close";
            this.CloseLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.CloseLabel_LinkClicked);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.CropAssemblyLabel);
            this.panel1.Controls.Add(this.QonverterLabel);
            this.panel1.Controls.Add(this.CloseLabel);
            this.panel1.Location = new System.Drawing.Point(3, 421);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(277, 47);
            this.panel1.TabIndex = 5;
            // 
            // CropAssemblyLabel
            // 
            this.CropAssemblyLabel.AutoSize = true;
            this.CropAssemblyLabel.Location = new System.Drawing.Point(43, 25);
            this.CropAssemblyLabel.Name = "CropAssemblyLabel";
            this.CropAssemblyLabel.Size = new System.Drawing.Size(191, 13);
            this.CropAssemblyLabel.TabIndex = 14;
            this.CropAssemblyLabel.TabStop = true;
            this.CropAssemblyLabel.Text = "Crop assembly to the current basic filter";
            this.CropAssemblyLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.CropAssemblyLabel_LinkClicked);
            // 
            // QonverterLabel
            // 
            this.QonverterLabel.AutoSize = true;
            this.QonverterLabel.Location = new System.Drawing.Point(3, 5);
            this.QonverterLabel.Name = "QonverterLabel";
            this.QonverterLabel.Size = new System.Drawing.Size(95, 13);
            this.QonverterLabel.TabIndex = 12;
            this.QonverterLabel.TabStop = true;
            this.QonverterLabel.Text = "Qonverter Settings";
            this.QonverterLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.QonverterLabel_LinkClicked);
            // 
            // distinctMatchFormatGroupBox
            // 
            this.distinctMatchFormatGroupBox.Controls.Add(this.tableLayoutPanel1);
            this.distinctMatchFormatGroupBox.Location = new System.Drawing.Point(3, 289);
            this.distinctMatchFormatGroupBox.Name = "distinctMatchFormatGroupBox";
            this.distinctMatchFormatGroupBox.Size = new System.Drawing.Size(277, 127);
            this.distinctMatchFormatGroupBox.TabIndex = 3;
            this.distinctMatchFormatGroupBox.TabStop = false;
            this.distinctMatchFormatGroupBox.Text = "Distinct Match Format";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(label11, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.modificationRoundToMassTextBox, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.modificationsAreDistinctCheckbox, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.analysisIsDistinctCheckBox, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.chargeIsDistinctCheckBox, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label12, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label13, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label14, 0, 2);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(6, 19);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(219, 102);
            this.tableLayoutPanel1.TabIndex = 140;
            // 
            // modificationRoundToMassTextBox
            // 
            this.modificationRoundToMassTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.modificationRoundToMassTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.modificationRoundToMassTextBox.Location = new System.Drawing.Point(170, 78);
            this.modificationRoundToMassTextBox.Name = "modificationRoundToMassTextBox";
            this.modificationRoundToMassTextBox.Size = new System.Drawing.Size(46, 21);
            this.modificationRoundToMassTextBox.TabIndex = 11;
            this.modificationRoundToMassTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.modificationRoundToMassTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.doubleTextBox_KeyDown);
            // 
            // modificationsAreDistinctCheckbox
            // 
            this.modificationsAreDistinctCheckbox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.modificationsAreDistinctCheckbox.AutoSize = true;
            this.modificationsAreDistinctCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.modificationsAreDistinctCheckbox.Location = new System.Drawing.Point(170, 55);
            this.modificationsAreDistinctCheckbox.Name = "modificationsAreDistinctCheckbox";
            this.modificationsAreDistinctCheckbox.Size = new System.Drawing.Size(15, 14);
            this.modificationsAreDistinctCheckbox.TabIndex = 10;
            this.modificationsAreDistinctCheckbox.UseVisualStyleBackColor = true;
            this.modificationsAreDistinctCheckbox.CheckedChanged += new System.EventHandler(this.filterControl_CheckedChanged);
            // 
            // analysisIsDistinctCheckBox
            // 
            this.analysisIsDistinctCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.analysisIsDistinctCheckBox.AutoSize = true;
            this.analysisIsDistinctCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.analysisIsDistinctCheckBox.Location = new System.Drawing.Point(170, 30);
            this.analysisIsDistinctCheckBox.Name = "analysisIsDistinctCheckBox";
            this.analysisIsDistinctCheckBox.Size = new System.Drawing.Size(15, 14);
            this.analysisIsDistinctCheckBox.TabIndex = 9;
            this.analysisIsDistinctCheckBox.UseVisualStyleBackColor = true;
            this.analysisIsDistinctCheckBox.CheckedChanged += new System.EventHandler(this.filterControl_CheckedChanged);
            // 
            // chargeIsDistinctCheckBox
            // 
            this.chargeIsDistinctCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chargeIsDistinctCheckBox.AutoSize = true;
            this.chargeIsDistinctCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chargeIsDistinctCheckBox.Location = new System.Drawing.Point(170, 5);
            this.chargeIsDistinctCheckBox.Name = "chargeIsDistinctCheckBox";
            this.chargeIsDistinctCheckBox.Size = new System.Drawing.Size(15, 14);
            this.chargeIsDistinctCheckBox.TabIndex = 8;
            this.chargeIsDistinctCheckBox.UseVisualStyleBackColor = true;
            this.chargeIsDistinctCheckBox.CheckedChanged += new System.EventHandler(this.filterControl_CheckedChanged);
            // 
            // label12
            // 
            this.label12.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(3, 6);
            this.label12.Margin = new System.Windows.Forms.Padding(3);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(90, 13);
            this.label12.TabIndex = 145;
            this.label12.Text = "Charge is distinct:";
            this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label13
            // 
            this.label13.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(3, 31);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(94, 13);
            this.label13.TabIndex = 146;
            this.label13.Text = "Analysis is distinct:";
            // 
            // label14
            // 
            this.label14.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(3, 56);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(126, 13);
            this.label14.TabIndex = 147;
            this.label14.Text = "Modifications are distinct:";
            // 
            // BasicFilterControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackColor = System.Drawing.SystemColors.Menu;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.distinctMatchFormatGroupBox);
            this.Controls.Add(this.psmLevelFilterGroupBox);
            this.Controls.Add(this.proteinLevelFilterGroupBox);
            this.Controls.Add(this.panel1);
            this.Name = "BasicFilterControl";
            this.Size = new System.Drawing.Size(286, 471);
            this.proteinLevelFilterGroupBox.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.psmLevelFilterGroupBox.ResumeLayout(false);
            this.tableLayoutPanel4.ResumeLayout(false);
            this.tableLayoutPanel4.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.distinctMatchFormatGroupBox.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox proteinLevelFilterGroupBox;
        private System.Windows.Forms.TextBox minSpectraTextBox;
        private System.Windows.Forms.TextBox minAdditionalPeptidesTextBox;
        private System.Windows.Forms.TextBox minDistinctPeptidesTextBox;
        private System.Windows.Forms.GroupBox psmLevelFilterGroupBox;
        private System.Windows.Forms.ComboBox maxQValueComboBox;
        private System.Windows.Forms.LinkLabel CloseLabel;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.LinkLabel QonverterLabel;
        private System.Windows.Forms.TextBox minSpectraPerPeptideTextBox;
        private System.Windows.Forms.TextBox maxProteinGroupsTextBox;
        private System.Windows.Forms.TextBox minSpectraPerMatchTextBox;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.CheckBox filterByGeneCheckBox;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel4;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.GroupBox distinctMatchFormatGroupBox;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox modificationRoundToMassTextBox;
        private System.Windows.Forms.CheckBox modificationsAreDistinctCheckbox;
        private System.Windows.Forms.CheckBox analysisIsDistinctCheckBox;
        private System.Windows.Forms.CheckBox chargeIsDistinctCheckBox;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.LinkLabel CropAssemblyLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox precursorMzToleranceTextBox;
        public System.Windows.Forms.ComboBox precursorMzToleranceUnitsComboBox;
    }
}
