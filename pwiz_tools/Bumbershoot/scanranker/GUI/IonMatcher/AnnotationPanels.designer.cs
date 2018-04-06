//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

using System.Windows.Forms;

namespace Forms.Controls
{
    partial class AnnotationPanels : UserControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing )
        {
            if( disposing && ( components != null ) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle16 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle17 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle18 = new System.Windows.Forms.DataGridViewCellStyle();
            this.peptideFragmentationPanel = new System.Windows.Forms.Panel();
            this.toggleSecondaryPeptideCheckBox = new System.Windows.Forms.CheckBox();
            this.sequenceTextBox = new System.Windows.Forms.TextBox();
            this.fragmentMassTypeComboBox = new System.Windows.Forms.ComboBox();
            this.precursorMassTypeComboBox = new System.Windows.Forms.ComboBox();
            this.peptideInfoGridView = new System.Windows.Forms.DataGridView();
            this.MassType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Mass = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MassErrorType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MassError = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.showFragmentationLaddersCheckBox = new System.Windows.Forms.CheckBox();
            this.showMissesCheckBox = new System.Windows.Forms.CheckBox();
            this.maxChargeUpDown = new System.Windows.Forms.NumericUpDown();
            this.minChargeUpDown = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.ionSeriesGroupBox = new System.Windows.Forms.GroupBox();
            this.zRadicalCheckBox = new System.Windows.Forms.CheckBox();
            this.c2CheckBox = new System.Windows.Forms.CheckBox();
            this.zCheckBox = new System.Windows.Forms.CheckBox();
            this.yCheckBox = new System.Windows.Forms.CheckBox();
            this.xCheckBox = new System.Windows.Forms.CheckBox();
            this.cCheckBox = new System.Windows.Forms.CheckBox();
            this.bCheckBox = new System.Windows.Forms.CheckBox();
            this.aCheckBox = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.fragmentInfoGridView = new System.Windows.Forms.DataGridView();
            this.b = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.y = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.peptideFragmentationPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.peptideInfoGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxChargeUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.minChargeUpDown)).BeginInit();
            this.ionSeriesGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.fragmentInfoGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // peptideFragmentationPanel
            // 
            this.peptideFragmentationPanel.AutoScroll = true;
            this.peptideFragmentationPanel.BackColor = System.Drawing.SystemColors.Control;
            this.peptideFragmentationPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.peptideFragmentationPanel.Controls.Add(this.toggleSecondaryPeptideCheckBox);
            this.peptideFragmentationPanel.Controls.Add(this.sequenceTextBox);
            this.peptideFragmentationPanel.Controls.Add(this.fragmentMassTypeComboBox);
            this.peptideFragmentationPanel.Controls.Add(this.precursorMassTypeComboBox);
            this.peptideFragmentationPanel.Controls.Add(this.peptideInfoGridView);
            this.peptideFragmentationPanel.Controls.Add(this.showFragmentationLaddersCheckBox);
            this.peptideFragmentationPanel.Controls.Add(this.showMissesCheckBox);
            this.peptideFragmentationPanel.Controls.Add(this.maxChargeUpDown);
            this.peptideFragmentationPanel.Controls.Add(this.minChargeUpDown);
            this.peptideFragmentationPanel.Controls.Add(this.label3);
            this.peptideFragmentationPanel.Controls.Add(this.label2);
            this.peptideFragmentationPanel.Controls.Add(this.ionSeriesGroupBox);
            this.peptideFragmentationPanel.Controls.Add(this.label1);
            this.peptideFragmentationPanel.Controls.Add(this.fragmentInfoGridView);
            this.peptideFragmentationPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.peptideFragmentationPanel.Location = new System.Drawing.Point(0, 0);
            this.peptideFragmentationPanel.Margin = new System.Windows.Forms.Padding(4);
            this.peptideFragmentationPanel.Name = "peptideFragmentationPanel";
            this.peptideFragmentationPanel.Size = new System.Drawing.Size(883, 259);
            this.peptideFragmentationPanel.TabIndex = 0;
            // 
            // toggleSecondaryPeptideCheckBox
            // 
            this.toggleSecondaryPeptideCheckBox.Appearance = System.Windows.Forms.Appearance.Button;
            this.toggleSecondaryPeptideCheckBox.AutoSize = true;
            this.toggleSecondaryPeptideCheckBox.BackColor = System.Drawing.Color.Red;
            this.toggleSecondaryPeptideCheckBox.FlatAppearance.BorderColor = System.Drawing.Color.White;
            this.toggleSecondaryPeptideCheckBox.ForeColor = System.Drawing.SystemColors.ControlText;
            this.toggleSecondaryPeptideCheckBox.Location = new System.Drawing.Point(10, 18);
            this.toggleSecondaryPeptideCheckBox.MaximumSize = new System.Drawing.Size(12, 12);
            this.toggleSecondaryPeptideCheckBox.MinimumSize = new System.Drawing.Size(12, 12);
            this.toggleSecondaryPeptideCheckBox.Name = "toggleSecondaryPeptideCheckBox";
            this.toggleSecondaryPeptideCheckBox.Size = new System.Drawing.Size(12, 12);
            this.toggleSecondaryPeptideCheckBox.TabIndex = 16;
            this.toggleSecondaryPeptideCheckBox.UseVisualStyleBackColor = false;
            this.toggleSecondaryPeptideCheckBox.Visible = false;
            // 
            // sequenceTextBox
            // 
            this.sequenceTextBox.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.sequenceTextBox.Location = new System.Drawing.Point(86, 12);
            this.sequenceTextBox.Name = "sequenceTextBox";
            this.sequenceTextBox.Size = new System.Drawing.Size(766, 22);
            this.sequenceTextBox.TabIndex = 15;
            this.sequenceTextBox.Text = "PEPTIDE";
            // 
            // fragmentMassTypeComboBox
            // 
            this.fragmentMassTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.fragmentMassTypeComboBox.FormattingEnabled = true;
            this.fragmentMassTypeComboBox.Items.AddRange(new object[] {
            "Monoisotopic fragment masses",
            "Average fragment masses"});
            this.fragmentMassTypeComboBox.Location = new System.Drawing.Point(483, 46);
            this.fragmentMassTypeComboBox.Margin = new System.Windows.Forms.Padding(4);
            this.fragmentMassTypeComboBox.MaxDropDownItems = 2;
            this.fragmentMassTypeComboBox.Name = "fragmentMassTypeComboBox";
            this.fragmentMassTypeComboBox.Size = new System.Drawing.Size(239, 24);
            this.fragmentMassTypeComboBox.TabIndex = 14;
            // 
            // precursorMassTypeComboBox
            // 
            this.precursorMassTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.precursorMassTypeComboBox.FormattingEnabled = true;
            this.precursorMassTypeComboBox.Items.AddRange(new object[] {
            "Monoisotopic precursor mass",
            "Average precursor mass"});
            this.precursorMassTypeComboBox.Location = new System.Drawing.Point(235, 46);
            this.precursorMassTypeComboBox.Margin = new System.Windows.Forms.Padding(4);
            this.precursorMassTypeComboBox.MaxDropDownItems = 2;
            this.precursorMassTypeComboBox.Name = "precursorMassTypeComboBox";
            this.precursorMassTypeComboBox.Size = new System.Drawing.Size(239, 24);
            this.precursorMassTypeComboBox.TabIndex = 13;
            // 
            // peptideInfoGridView
            // 
            this.peptideInfoGridView.AllowUserToAddRows = false;
            this.peptideInfoGridView.AllowUserToDeleteRows = false;
            this.peptideInfoGridView.AllowUserToResizeColumns = false;
            this.peptideInfoGridView.AllowUserToResizeRows = false;
            this.peptideInfoGridView.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.peptideInfoGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader;
            this.peptideInfoGridView.BackgroundColor = System.Drawing.SystemColors.Control;
            this.peptideInfoGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.peptideInfoGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.peptideInfoGridView.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            this.peptideInfoGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.peptideInfoGridView.ColumnHeadersVisible = false;
            this.peptideInfoGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.MassType,
            this.Mass,
            this.MassErrorType,
            this.MassError});
            dataGridViewCellStyle16.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle16.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle16.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle16.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle16.SelectionBackColor = System.Drawing.SystemColors.ButtonHighlight;
            dataGridViewCellStyle16.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle16.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.peptideInfoGridView.DefaultCellStyle = dataGridViewCellStyle16;
            this.peptideInfoGridView.Location = new System.Drawing.Point(235, 78);
            this.peptideInfoGridView.Margin = new System.Windows.Forms.Padding(4);
            this.peptideInfoGridView.Name = "peptideInfoGridView";
            this.peptideInfoGridView.ReadOnly = true;
            this.peptideInfoGridView.RowHeadersVisible = false;
            this.peptideInfoGridView.RowTemplate.Height = 24;
            this.peptideInfoGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.peptideInfoGridView.Size = new System.Drawing.Size(602, 116);
            this.peptideInfoGridView.TabIndex = 11;
            // 
            // MassType
            // 
            this.MassType.HeaderText = "";
            this.MassType.Name = "MassType";
            this.MassType.ReadOnly = true;
            this.MassType.Width = 5;
            // 
            // Mass
            // 
            this.Mass.HeaderText = "";
            this.Mass.Name = "Mass";
            this.Mass.ReadOnly = true;
            this.Mass.Width = 5;
            // 
            // MassErrorType
            // 
            this.MassErrorType.HeaderText = "";
            this.MassErrorType.Name = "MassErrorType";
            this.MassErrorType.ReadOnly = true;
            this.MassErrorType.Width = 5;
            // 
            // MassError
            // 
            this.MassError.HeaderText = "";
            this.MassError.Name = "MassError";
            this.MassError.ReadOnly = true;
            this.MassError.Width = 5;
            // 
            // showFragmentationLaddersCheckBox
            // 
            this.showFragmentationLaddersCheckBox.AutoSize = true;
            this.showFragmentationLaddersCheckBox.Location = new System.Drawing.Point(9, 201);
            this.showFragmentationLaddersCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.showFragmentationLaddersCheckBox.Name = "showFragmentationLaddersCheckBox";
            this.showFragmentationLaddersCheckBox.Size = new System.Drawing.Size(203, 21);
            this.showFragmentationLaddersCheckBox.TabIndex = 9;
            this.showFragmentationLaddersCheckBox.Text = "Show fragmentation ladders";
            this.showFragmentationLaddersCheckBox.UseVisualStyleBackColor = true;
            // 
            // showMissesCheckBox
            // 
            this.showMissesCheckBox.AutoSize = true;
            this.showMissesCheckBox.Location = new System.Drawing.Point(9, 224);
            this.showMissesCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.showMissesCheckBox.Name = "showMissesCheckBox";
            this.showMissesCheckBox.Size = new System.Drawing.Size(179, 21);
            this.showMissesCheckBox.TabIndex = 6;
            this.showMissesCheckBox.Text = "Show missing fragments";
            this.showMissesCheckBox.UseVisualStyleBackColor = true;
            // 
            // maxChargeUpDown
            // 
            this.maxChargeUpDown.Location = new System.Drawing.Point(160, 78);
            this.maxChargeUpDown.Margin = new System.Windows.Forms.Padding(4);
            this.maxChargeUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.maxChargeUpDown.Name = "maxChargeUpDown";
            this.maxChargeUpDown.Size = new System.Drawing.Size(53, 22);
            this.maxChargeUpDown.TabIndex = 8;
            this.maxChargeUpDown.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // minChargeUpDown
            // 
            this.minChargeUpDown.Location = new System.Drawing.Point(160, 46);
            this.minChargeUpDown.Margin = new System.Windows.Forms.Padding(4);
            this.minChargeUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.minChargeUpDown.Name = "minChargeUpDown";
            this.minChargeUpDown.Size = new System.Drawing.Size(53, 22);
            this.minChargeUpDown.TabIndex = 7;
            this.minChargeUpDown.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(5, 79);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(149, 17);
            this.label3.TabIndex = 6;
            this.label3.Text = "Max. fragment charge:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(5, 48);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(146, 17);
            this.label2.TabIndex = 4;
            this.label2.Text = "Min. fragment charge:";
            // 
            // ionSeriesGroupBox
            // 
            this.ionSeriesGroupBox.Controls.Add(this.zRadicalCheckBox);
            this.ionSeriesGroupBox.Controls.Add(this.c2CheckBox);
            this.ionSeriesGroupBox.Controls.Add(this.zCheckBox);
            this.ionSeriesGroupBox.Controls.Add(this.yCheckBox);
            this.ionSeriesGroupBox.Controls.Add(this.xCheckBox);
            this.ionSeriesGroupBox.Controls.Add(this.cCheckBox);
            this.ionSeriesGroupBox.Controls.Add(this.bCheckBox);
            this.ionSeriesGroupBox.Controls.Add(this.aCheckBox);
            this.ionSeriesGroupBox.Location = new System.Drawing.Point(8, 110);
            this.ionSeriesGroupBox.Margin = new System.Windows.Forms.Padding(4);
            this.ionSeriesGroupBox.Name = "ionSeriesGroupBox";
            this.ionSeriesGroupBox.Padding = new System.Windows.Forms.Padding(4);
            this.ionSeriesGroupBox.Size = new System.Drawing.Size(219, 84);
            this.ionSeriesGroupBox.TabIndex = 3;
            this.ionSeriesGroupBox.TabStop = false;
            this.ionSeriesGroupBox.Text = "Fragment Ion Series";
            // 
            // zRadicalCheckBox
            // 
            this.zRadicalCheckBox.AutoSize = true;
            this.zRadicalCheckBox.Location = new System.Drawing.Point(165, 52);
            this.zRadicalCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.zRadicalCheckBox.Name = "zRadicalCheckBox";
            this.zRadicalCheckBox.Size = new System.Drawing.Size(39, 21);
            this.zRadicalCheckBox.TabIndex = 7;
            this.zRadicalCheckBox.Text = "z*";
            this.zRadicalCheckBox.UseVisualStyleBackColor = true;
            // 
            // c2CheckBox
            // 
            this.c2CheckBox.AutoSize = true;
            this.c2CheckBox.Location = new System.Drawing.Point(167, 23);
            this.c2CheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.c2CheckBox.Name = "c2CheckBox";
            this.c2CheckBox.Size = new System.Drawing.Size(34, 21);
            this.c2CheckBox.TabIndex = 6;
            this.c2CheckBox.Text = "c";
            this.c2CheckBox.UseVisualStyleBackColor = true;
            this.c2CheckBox.Visible = false;
            // 
            // zCheckBox
            // 
            this.zCheckBox.AutoSize = true;
            this.zCheckBox.Location = new System.Drawing.Point(116, 52);
            this.zCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.zCheckBox.Name = "zCheckBox";
            this.zCheckBox.Size = new System.Drawing.Size(34, 21);
            this.zCheckBox.TabIndex = 5;
            this.zCheckBox.Text = "z";
            this.zCheckBox.UseVisualStyleBackColor = true;
            // 
            // yCheckBox
            // 
            this.yCheckBox.AutoSize = true;
            this.yCheckBox.Location = new System.Drawing.Point(65, 52);
            this.yCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.yCheckBox.Name = "yCheckBox";
            this.yCheckBox.Size = new System.Drawing.Size(34, 21);
            this.yCheckBox.TabIndex = 4;
            this.yCheckBox.Text = "y";
            this.yCheckBox.UseVisualStyleBackColor = true;
            // 
            // xCheckBox
            // 
            this.xCheckBox.AutoSize = true;
            this.xCheckBox.Location = new System.Drawing.Point(15, 52);
            this.xCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.xCheckBox.Name = "xCheckBox";
            this.xCheckBox.Size = new System.Drawing.Size(33, 21);
            this.xCheckBox.TabIndex = 3;
            this.xCheckBox.Text = "x";
            this.xCheckBox.UseVisualStyleBackColor = true;
            // 
            // cCheckBox
            // 
            this.cCheckBox.AutoSize = true;
            this.cCheckBox.Location = new System.Drawing.Point(116, 23);
            this.cCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.cCheckBox.Name = "cCheckBox";
            this.cCheckBox.Size = new System.Drawing.Size(34, 21);
            this.cCheckBox.TabIndex = 2;
            this.cCheckBox.Text = "c";
            this.cCheckBox.UseVisualStyleBackColor = true;
            // 
            // bCheckBox
            // 
            this.bCheckBox.AutoSize = true;
            this.bCheckBox.Location = new System.Drawing.Point(65, 23);
            this.bCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.bCheckBox.Name = "bCheckBox";
            this.bCheckBox.Size = new System.Drawing.Size(35, 21);
            this.bCheckBox.TabIndex = 1;
            this.bCheckBox.Text = "b";
            this.bCheckBox.UseVisualStyleBackColor = true;
            // 
            // aCheckBox
            // 
            this.aCheckBox.AutoSize = true;
            this.aCheckBox.Location = new System.Drawing.Point(15, 23);
            this.aCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.aCheckBox.Name = "aCheckBox";
            this.aCheckBox.Size = new System.Drawing.Size(35, 21);
            this.aCheckBox.TabIndex = 0;
            this.aCheckBox.Text = "a";
            this.aCheckBox.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(25, 15);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 17);
            this.label1.TabIndex = 2;
            this.label1.Text = "Peptide:";
            // 
            // fragmentInfoGridView
            // 
            this.fragmentInfoGridView.AllowUserToAddRows = false;
            this.fragmentInfoGridView.AllowUserToDeleteRows = false;
            this.fragmentInfoGridView.AllowUserToResizeColumns = false;
            this.fragmentInfoGridView.AllowUserToResizeRows = false;
            this.fragmentInfoGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.fragmentInfoGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader;
            this.fragmentInfoGridView.BackgroundColor = System.Drawing.SystemColors.Control;
            this.fragmentInfoGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.fragmentInfoGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.fragmentInfoGridView.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            this.fragmentInfoGridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle17.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle17.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle17.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle17.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle17.SelectionBackColor = System.Drawing.SystemColors.ButtonHighlight;
            dataGridViewCellStyle17.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle17.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.fragmentInfoGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle17;
            this.fragmentInfoGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.fragmentInfoGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.b,
            this.y});
            dataGridViewCellStyle18.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle18.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle18.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle18.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle18.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle18.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle18.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.fragmentInfoGridView.DefaultCellStyle = dataGridViewCellStyle18;
            this.fragmentInfoGridView.Location = new System.Drawing.Point(235, 162);
            this.fragmentInfoGridView.Margin = new System.Windows.Forms.Padding(4);
            this.fragmentInfoGridView.MaximumSize = new System.Drawing.Size(687, 754);
            this.fragmentInfoGridView.Name = "fragmentInfoGridView";
            this.fragmentInfoGridView.ReadOnly = true;
            this.fragmentInfoGridView.RowHeadersVisible = false;
            this.fragmentInfoGridView.RowTemplate.Height = 24;
            this.fragmentInfoGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.fragmentInfoGridView.Size = new System.Drawing.Size(602, 67);
            this.fragmentInfoGridView.TabIndex = 12;
            // 
            // b
            // 
            this.b.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.b.HeaderText = "b";
            this.b.Name = "b";
            this.b.ReadOnly = true;
            this.b.Width = 39;
            // 
            // y
            // 
            this.y.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.y.HeaderText = "y";
            this.y.Name = "y";
            this.y.ReadOnly = true;
            this.y.Width = 38;
            // 
            // AnnotationPanels
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.peptideFragmentationPanel);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "AnnotationPanels";
            this.Size = new System.Drawing.Size(883, 259);
            this.peptideFragmentationPanel.ResumeLayout(false);
            this.peptideFragmentationPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.peptideInfoGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxChargeUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.minChargeUpDown)).EndInit();
            this.ionSeriesGroupBox.ResumeLayout(false);
            this.ionSeriesGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.fragmentInfoGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.Panel peptideFragmentationPanel;
        public System.Windows.Forms.ComboBox fragmentMassTypeComboBox;
        public System.Windows.Forms.ComboBox precursorMassTypeComboBox;
        public System.Windows.Forms.DataGridView peptideInfoGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn MassType;
        private System.Windows.Forms.DataGridViewTextBoxColumn Mass;
        private System.Windows.Forms.DataGridViewTextBoxColumn MassErrorType;
        private System.Windows.Forms.DataGridViewTextBoxColumn MassError;
        public System.Windows.Forms.CheckBox showFragmentationLaddersCheckBox;
        public System.Windows.Forms.CheckBox showMissesCheckBox;
        public System.Windows.Forms.NumericUpDown maxChargeUpDown;
        public System.Windows.Forms.NumericUpDown minChargeUpDown;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox ionSeriesGroupBox;
        public System.Windows.Forms.CheckBox zRadicalCheckBox;
        public System.Windows.Forms.CheckBox c2CheckBox;
        public System.Windows.Forms.CheckBox zCheckBox;
        public System.Windows.Forms.CheckBox yCheckBox;
        public System.Windows.Forms.CheckBox xCheckBox;
        public System.Windows.Forms.CheckBox cCheckBox;
        public System.Windows.Forms.CheckBox bCheckBox;
        public System.Windows.Forms.CheckBox aCheckBox;
        private System.Windows.Forms.Label label1;
        public TextBox sequenceTextBox;
        public DataGridView fragmentInfoGridView;
        private DataGridViewTextBoxColumn b;
        private DataGridViewTextBoxColumn y;
        public CheckBox toggleSecondaryPeptideCheckBox;

    }
}
