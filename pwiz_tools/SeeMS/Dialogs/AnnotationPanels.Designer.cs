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

namespace seems
{
    partial class AnnotationPanels
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.annotationPanelsTabControl = new System.Windows.Forms.TabControl();
            this.peptideFragmentationTabPage = new System.Windows.Forms.TabPage();
            this.peptideFragmentationPanel = new System.Windows.Forms.Panel();
            this.fragmentMassTypeComboBox = new System.Windows.Forms.ComboBox();
            this.precursorMassTypeComboBox = new System.Windows.Forms.ComboBox();
            this.fragmentInfoGridView = new System.Windows.Forms.DataGridView();
            this.b = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.y = new System.Windows.Forms.DataGridViewTextBoxColumn();
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
            this.sequenceTextBox = new System.Windows.Forms.TextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.annotationPanelsTabControl.SuspendLayout();
            this.peptideFragmentationTabPage.SuspendLayout();
            this.peptideFragmentationPanel.SuspendLayout();
            ( (System.ComponentModel.ISupportInitialize) ( this.fragmentInfoGridView ) ).BeginInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.peptideInfoGridView ) ).BeginInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.maxChargeUpDown ) ).BeginInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.minChargeUpDown ) ).BeginInit();
            this.ionSeriesGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // annotationPanelsTabControl
            // 
            this.annotationPanelsTabControl.Controls.Add( this.peptideFragmentationTabPage );
            this.annotationPanelsTabControl.Controls.Add( this.tabPage2 );
            this.annotationPanelsTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.annotationPanelsTabControl.Location = new System.Drawing.Point( 0, 0 );
            this.annotationPanelsTabControl.Name = "annotationPanelsTabControl";
            this.annotationPanelsTabControl.SelectedIndex = 0;
            this.annotationPanelsTabControl.Size = new System.Drawing.Size( 716, 752 );
            this.annotationPanelsTabControl.TabIndex = 0;
            // 
            // peptideFragmentationTabPage
            // 
            this.peptideFragmentationTabPage.BackColor = System.Drawing.Color.DimGray;
            this.peptideFragmentationTabPage.Controls.Add( this.peptideFragmentationPanel );
            this.peptideFragmentationTabPage.Location = new System.Drawing.Point( 4, 22 );
            this.peptideFragmentationTabPage.Name = "peptideFragmentationTabPage";
            this.peptideFragmentationTabPage.Padding = new System.Windows.Forms.Padding( 3 );
            this.peptideFragmentationTabPage.Size = new System.Drawing.Size( 708, 726 );
            this.peptideFragmentationTabPage.TabIndex = 0;
            this.peptideFragmentationTabPage.Text = "Peptide Fragmentation";
            // 
            // peptideFragmentationPanel
            // 
            this.peptideFragmentationPanel.BackColor = System.Drawing.SystemColors.Control;
            this.peptideFragmentationPanel.Controls.Add( this.fragmentMassTypeComboBox );
            this.peptideFragmentationPanel.Controls.Add( this.precursorMassTypeComboBox );
            this.peptideFragmentationPanel.Controls.Add( this.fragmentInfoGridView );
            this.peptideFragmentationPanel.Controls.Add( this.peptideInfoGridView );
            this.peptideFragmentationPanel.Controls.Add( this.showFragmentationLaddersCheckBox );
            this.peptideFragmentationPanel.Controls.Add( this.showMissesCheckBox );
            this.peptideFragmentationPanel.Controls.Add( this.maxChargeUpDown );
            this.peptideFragmentationPanel.Controls.Add( this.minChargeUpDown );
            this.peptideFragmentationPanel.Controls.Add( this.label3 );
            this.peptideFragmentationPanel.Controls.Add( this.label2 );
            this.peptideFragmentationPanel.Controls.Add( this.ionSeriesGroupBox );
            this.peptideFragmentationPanel.Controls.Add( this.label1 );
            this.peptideFragmentationPanel.Controls.Add( this.sequenceTextBox );
            this.peptideFragmentationPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.peptideFragmentationPanel.Location = new System.Drawing.Point( 3, 3 );
            this.peptideFragmentationPanel.Name = "peptideFragmentationPanel";
            this.peptideFragmentationPanel.Size = new System.Drawing.Size( 702, 720 );
            this.peptideFragmentationPanel.TabIndex = 0;
            // 
            // fragmentMassTypeComboBox
            // 
            this.fragmentMassTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.fragmentMassTypeComboBox.FormattingEnabled = true;
            this.fragmentMassTypeComboBox.Items.AddRange( new object[] {
            "Monoisotopic fragment masses",
            "Average fragment masses"} );
            this.fragmentMassTypeComboBox.Location = new System.Drawing.Point( 362, 37 );
            this.fragmentMassTypeComboBox.MaxDropDownItems = 2;
            this.fragmentMassTypeComboBox.Name = "fragmentMassTypeComboBox";
            this.fragmentMassTypeComboBox.Size = new System.Drawing.Size( 180, 21 );
            this.fragmentMassTypeComboBox.TabIndex = 14;
            // 
            // precursorMassTypeComboBox
            // 
            this.precursorMassTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.precursorMassTypeComboBox.FormattingEnabled = true;
            this.precursorMassTypeComboBox.Items.AddRange( new object[] {
            "Monoisotopic precursor mass",
            "Average precursor mass"} );
            this.precursorMassTypeComboBox.Location = new System.Drawing.Point( 176, 37 );
            this.precursorMassTypeComboBox.MaxDropDownItems = 2;
            this.precursorMassTypeComboBox.Name = "precursorMassTypeComboBox";
            this.precursorMassTypeComboBox.Size = new System.Drawing.Size( 180, 21 );
            this.precursorMassTypeComboBox.TabIndex = 13;
            // 
            // fragmentInfoGridView
            // 
            this.fragmentInfoGridView.AllowUserToAddRows = false;
            this.fragmentInfoGridView.AllowUserToDeleteRows = false;
            this.fragmentInfoGridView.AllowUserToResizeColumns = false;
            this.fragmentInfoGridView.AllowUserToResizeRows = false;
            this.fragmentInfoGridView.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.fragmentInfoGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader;
            this.fragmentInfoGridView.BackgroundColor = System.Drawing.SystemColors.Control;
            this.fragmentInfoGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.fragmentInfoGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.fragmentInfoGridView.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            this.fragmentInfoGridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font( "Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.ButtonHighlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.fragmentInfoGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.fragmentInfoGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.fragmentInfoGridView.Columns.AddRange( new System.Windows.Forms.DataGridViewColumn[] {
            this.b,
            this.y} );
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle5.Font = new System.Drawing.Font( "Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.fragmentInfoGridView.DefaultCellStyle = dataGridViewCellStyle5;
            this.fragmentInfoGridView.Location = new System.Drawing.Point( 176, 104 );
            this.fragmentInfoGridView.MaximumSize = new System.Drawing.Size( 515, 613 );
            this.fragmentInfoGridView.Name = "fragmentInfoGridView";
            this.fragmentInfoGridView.ReadOnly = true;
            this.fragmentInfoGridView.RowHeadersVisible = false;
            this.fragmentInfoGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.fragmentInfoGridView.Size = new System.Drawing.Size( 515, 603 );
            this.fragmentInfoGridView.TabIndex = 12;
            // 
            // b
            // 
            this.b.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.b.HeaderText = "b";
            this.b.Name = "b";
            this.b.ReadOnly = true;
            this.b.Width = 38;
            // 
            // y
            // 
            this.y.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.y.HeaderText = "y";
            this.y.Name = "y";
            this.y.ReadOnly = true;
            this.y.Width = 37;
            // 
            // peptideInfoGridView
            // 
            this.peptideInfoGridView.AllowUserToAddRows = false;
            this.peptideInfoGridView.AllowUserToDeleteRows = false;
            this.peptideInfoGridView.AllowUserToResizeColumns = false;
            this.peptideInfoGridView.AllowUserToResizeRows = false;
            this.peptideInfoGridView.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.peptideInfoGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader;
            this.peptideInfoGridView.BackgroundColor = System.Drawing.SystemColors.Control;
            this.peptideInfoGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.peptideInfoGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.peptideInfoGridView.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            this.peptideInfoGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.peptideInfoGridView.ColumnHeadersVisible = false;
            this.peptideInfoGridView.Columns.AddRange( new System.Windows.Forms.DataGridViewColumn[] {
            this.MassType,
            this.Mass,
            this.MassErrorType,
            this.MassError} );
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle6.Font = new System.Drawing.Font( "Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.ButtonHighlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.peptideInfoGridView.DefaultCellStyle = dataGridViewCellStyle6;
            this.peptideInfoGridView.Location = new System.Drawing.Point( 176, 63 );
            this.peptideInfoGridView.Name = "peptideInfoGridView";
            this.peptideInfoGridView.ReadOnly = true;
            this.peptideInfoGridView.RowHeadersVisible = false;
            this.peptideInfoGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.peptideInfoGridView.Size = new System.Drawing.Size( 515, 35 );
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
            this.showFragmentationLaddersCheckBox.Location = new System.Drawing.Point( 7, 163 );
            this.showFragmentationLaddersCheckBox.Name = "showFragmentationLaddersCheckBox";
            this.showFragmentationLaddersCheckBox.Size = new System.Drawing.Size( 157, 17 );
            this.showFragmentationLaddersCheckBox.TabIndex = 9;
            this.showFragmentationLaddersCheckBox.Text = "Show fragmentation ladders";
            this.showFragmentationLaddersCheckBox.UseVisualStyleBackColor = true;
            // 
            // showMissesCheckBox
            // 
            this.showMissesCheckBox.AutoSize = true;
            this.showMissesCheckBox.Location = new System.Drawing.Point( 7, 182 );
            this.showMissesCheckBox.Name = "showMissesCheckBox";
            this.showMissesCheckBox.Size = new System.Drawing.Size( 139, 17 );
            this.showMissesCheckBox.TabIndex = 6;
            this.showMissesCheckBox.Text = "Show missing fragments";
            this.showMissesCheckBox.UseVisualStyleBackColor = true;
            // 
            // maxChargeUpDown
            // 
            this.maxChargeUpDown.Location = new System.Drawing.Point( 120, 63 );
            this.maxChargeUpDown.Minimum = new decimal( new int[] {
            1,
            0,
            0,
            0} );
            this.maxChargeUpDown.Name = "maxChargeUpDown";
            this.maxChargeUpDown.Size = new System.Drawing.Size( 41, 20 );
            this.maxChargeUpDown.TabIndex = 8;
            this.maxChargeUpDown.Value = new decimal( new int[] {
            1,
            0,
            0,
            0} );
            this.maxChargeUpDown.ValueChanged += new System.EventHandler( this.maxChargeUpDown_ValueChanged );
            // 
            // minChargeUpDown
            // 
            this.minChargeUpDown.Location = new System.Drawing.Point( 120, 37 );
            this.minChargeUpDown.Minimum = new decimal( new int[] {
            1,
            0,
            0,
            0} );
            this.minChargeUpDown.Name = "minChargeUpDown";
            this.minChargeUpDown.Size = new System.Drawing.Size( 41, 20 );
            this.minChargeUpDown.TabIndex = 7;
            this.minChargeUpDown.Value = new decimal( new int[] {
            1,
            0,
            0,
            0} );
            this.minChargeUpDown.ValueChanged += new System.EventHandler( this.minChargeUpDown_ValueChanged );
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point( 4, 64 );
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size( 113, 13 );
            this.label3.TabIndex = 6;
            this.label3.Text = "Max. fragment charge:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point( 4, 39 );
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size( 110, 13 );
            this.label2.TabIndex = 4;
            this.label2.Text = "Min. fragment charge:";
            // 
            // ionSeriesGroupBox
            // 
            this.ionSeriesGroupBox.Controls.Add( this.zRadicalCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.c2CheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.zCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.yCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.xCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.cCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.bCheckBox );
            this.ionSeriesGroupBox.Controls.Add( this.aCheckBox );
            this.ionSeriesGroupBox.Location = new System.Drawing.Point( 6, 89 );
            this.ionSeriesGroupBox.Name = "ionSeriesGroupBox";
            this.ionSeriesGroupBox.Size = new System.Drawing.Size( 164, 68 );
            this.ionSeriesGroupBox.TabIndex = 3;
            this.ionSeriesGroupBox.TabStop = false;
            this.ionSeriesGroupBox.Text = "Fragment Ion Series";
            // 
            // zRadicalCheckBox
            // 
            this.zRadicalCheckBox.AutoSize = true;
            this.zRadicalCheckBox.Location = new System.Drawing.Point( 124, 42 );
            this.zRadicalCheckBox.Name = "zRadicalCheckBox";
            this.zRadicalCheckBox.Size = new System.Drawing.Size( 35, 17 );
            this.zRadicalCheckBox.TabIndex = 7;
            this.zRadicalCheckBox.Text = "z*";
            this.zRadicalCheckBox.UseVisualStyleBackColor = true;
            // 
            // c2CheckBox
            // 
            this.c2CheckBox.AutoSize = true;
            this.c2CheckBox.Location = new System.Drawing.Point( 125, 19 );
            this.c2CheckBox.Name = "c2CheckBox";
            this.c2CheckBox.Size = new System.Drawing.Size( 32, 17 );
            this.c2CheckBox.TabIndex = 6;
            this.c2CheckBox.Text = "c";
            this.c2CheckBox.UseVisualStyleBackColor = true;
            this.c2CheckBox.Visible = false;
            // 
            // zCheckBox
            // 
            this.zCheckBox.AutoSize = true;
            this.zCheckBox.Location = new System.Drawing.Point( 87, 42 );
            this.zCheckBox.Name = "zCheckBox";
            this.zCheckBox.Size = new System.Drawing.Size( 31, 17 );
            this.zCheckBox.TabIndex = 5;
            this.zCheckBox.Text = "z";
            this.zCheckBox.UseVisualStyleBackColor = true;
            // 
            // yCheckBox
            // 
            this.yCheckBox.AutoSize = true;
            this.yCheckBox.Location = new System.Drawing.Point( 49, 42 );
            this.yCheckBox.Name = "yCheckBox";
            this.yCheckBox.Size = new System.Drawing.Size( 31, 17 );
            this.yCheckBox.TabIndex = 4;
            this.yCheckBox.Text = "y";
            this.yCheckBox.UseVisualStyleBackColor = true;
            // 
            // xCheckBox
            // 
            this.xCheckBox.AutoSize = true;
            this.xCheckBox.Location = new System.Drawing.Point( 11, 42 );
            this.xCheckBox.Name = "xCheckBox";
            this.xCheckBox.Size = new System.Drawing.Size( 31, 17 );
            this.xCheckBox.TabIndex = 3;
            this.xCheckBox.Text = "x";
            this.xCheckBox.UseVisualStyleBackColor = true;
            // 
            // cCheckBox
            // 
            this.cCheckBox.AutoSize = true;
            this.cCheckBox.Location = new System.Drawing.Point( 87, 19 );
            this.cCheckBox.Name = "cCheckBox";
            this.cCheckBox.Size = new System.Drawing.Size( 32, 17 );
            this.cCheckBox.TabIndex = 2;
            this.cCheckBox.Text = "c";
            this.cCheckBox.UseVisualStyleBackColor = true;
            // 
            // bCheckBox
            // 
            this.bCheckBox.AutoSize = true;
            this.bCheckBox.Location = new System.Drawing.Point( 49, 19 );
            this.bCheckBox.Name = "bCheckBox";
            this.bCheckBox.Size = new System.Drawing.Size( 32, 17 );
            this.bCheckBox.TabIndex = 1;
            this.bCheckBox.Text = "b";
            this.bCheckBox.UseVisualStyleBackColor = true;
            // 
            // aCheckBox
            // 
            this.aCheckBox.AutoSize = true;
            this.aCheckBox.Location = new System.Drawing.Point( 11, 19 );
            this.aCheckBox.Name = "aCheckBox";
            this.aCheckBox.Size = new System.Drawing.Size( 32, 17 );
            this.aCheckBox.TabIndex = 0;
            this.aCheckBox.Text = "a";
            this.aCheckBox.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point( 3, 14 );
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size( 46, 13 );
            this.label1.TabIndex = 2;
            this.label1.Text = "Peptide:";
            // 
            // sequenceTextBox
            // 
            this.sequenceTextBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.sequenceTextBox.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.sequenceTextBox.Location = new System.Drawing.Point( 55, 11 );
            this.sequenceTextBox.Name = "sequenceTextBox";
            this.sequenceTextBox.Size = new System.Drawing.Size( 636, 20 );
            this.sequenceTextBox.TabIndex = 1;
            this.sequenceTextBox.Text = "PEPTIDE";
            // 
            // tabPage2
            // 
            this.tabPage2.Location = new System.Drawing.Point( 4, 22 );
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding( 3 );
            this.tabPage2.Size = new System.Drawing.Size( 708, 726 );
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // AnnotationPanels
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add( this.annotationPanelsTabControl );
            this.Name = "AnnotationPanels";
            this.Size = new System.Drawing.Size( 716, 752 );
            this.annotationPanelsTabControl.ResumeLayout( false );
            this.peptideFragmentationTabPage.ResumeLayout( false );
            this.peptideFragmentationPanel.ResumeLayout( false );
            this.peptideFragmentationPanel.PerformLayout();
            ( (System.ComponentModel.ISupportInitialize) ( this.fragmentInfoGridView ) ).EndInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.peptideInfoGridView ) ).EndInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.maxChargeUpDown ) ).EndInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.minChargeUpDown ) ).EndInit();
            this.ionSeriesGroupBox.ResumeLayout( false );
            this.ionSeriesGroupBox.PerformLayout();
            this.ResumeLayout( false );

        }

        #endregion

        private System.Windows.Forms.TabControl annotationPanelsTabControl;
        private System.Windows.Forms.TabPage peptideFragmentationTabPage;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.GroupBox ionSeriesGroupBox;
        private System.Windows.Forms.Label label1;
        public System.Windows.Forms.Panel peptideFragmentationPanel;
        public System.Windows.Forms.CheckBox aCheckBox;
        public System.Windows.Forms.TextBox sequenceTextBox;
        public System.Windows.Forms.CheckBox zCheckBox;
        public System.Windows.Forms.CheckBox yCheckBox;
        public System.Windows.Forms.CheckBox xCheckBox;
        public System.Windows.Forms.CheckBox cCheckBox;
        public System.Windows.Forms.CheckBox bCheckBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        public System.Windows.Forms.NumericUpDown maxChargeUpDown;
        public System.Windows.Forms.NumericUpDown minChargeUpDown;
        public System.Windows.Forms.CheckBox showMissesCheckBox;
        public System.Windows.Forms.CheckBox zRadicalCheckBox;
        public System.Windows.Forms.CheckBox c2CheckBox;
        public System.Windows.Forms.CheckBox showFragmentationLaddersCheckBox;
        public System.Windows.Forms.DataGridView peptideInfoGridView;
        public System.Windows.Forms.DataGridView fragmentInfoGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn b;
        private System.Windows.Forms.DataGridViewTextBoxColumn y;
        public System.Windows.Forms.ComboBox precursorMassTypeComboBox;
        public System.Windows.Forms.ComboBox fragmentMassTypeComboBox;
        private System.Windows.Forms.DataGridViewTextBoxColumn MassType;
        private System.Windows.Forms.DataGridViewTextBoxColumn Mass;
        private System.Windows.Forms.DataGridViewTextBoxColumn MassErrorType;
        private System.Windows.Forms.DataGridViewTextBoxColumn MassError;
    }
}
