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
    partial class DefaultSettingsManagerForm
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
            System.Windows.Forms.Label label5;
            System.Windows.Forms.Label lblMinDistinctPeptides;
            System.Windows.Forms.Label label8;
            System.Windows.Forms.Label label11;
            this.gbSearchPaths = new System.Windows.Forms.GroupBox();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnAddRelative = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.searchPathsTabControl = new System.Windows.Forms.TabControl();
            this.tabFastaFilepaths = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.lbFastaPaths = new System.Windows.Forms.ListBox();
            this.tabSourceSearchPaths = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.lbSourcePaths = new System.Windows.Forms.ListBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.psmLevelFilterGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this.label3 = new System.Windows.Forms.Label();
            this.maxProteinGroupsTextBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.maxQValueComboBox = new System.Windows.Forms.ComboBox();
            this.minSpectraPerMatchTextBox = new System.Windows.Forms.TextBox();
            this.minSpectraPerPeptideTextBox = new System.Windows.Forms.TextBox();
            this.lblMaxFdr = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.proteinLevelFilterGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.minSpectraTextBox = new System.Windows.Forms.TextBox();
            this.filterByGeneCheckBox = new System.Windows.Forms.CheckBox();
            this.minAdditionalPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.minDistinctPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.lblMinSpectraPerProtein = new System.Windows.Forms.Label();
            this.lblParsimonyVariable = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.defaultDecoyPrefixTextBox = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.qonverterSettingsButton = new System.Windows.Forms.Button();
            this.importSettingsGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.maxImportRankTextBox = new System.Windows.Forms.TextBox();
            this.ignoreUnmappedPeptidesCheckBox = new System.Windows.Forms.CheckBox();
            this.label16 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.maxImportFdrComboBox = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.sourceExtensionsTextBox = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.chargeIsDistinctCheckBox = new System.Windows.Forms.CheckBox();
            this.distinctMatchFormatGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.modificationRoundToMassTextBox = new System.Windows.Forms.TextBox();
            this.modificationsAreDistinctCheckbox = new System.Windows.Forms.CheckBox();
            this.analysisIsDistinctCheckBox = new System.Windows.Forms.CheckBox();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.nonFixedDriveWarningCheckBox = new System.Windows.Forms.CheckBox();
            this.embedGeneMetadataWarningCheckBox = new System.Windows.Forms.CheckBox();
            label5 = new System.Windows.Forms.Label();
            lblMinDistinctPeptides = new System.Windows.Forms.Label();
            label8 = new System.Windows.Forms.Label();
            label11 = new System.Windows.Forms.Label();
            this.gbSearchPaths.SuspendLayout();
            this.searchPathsTabControl.SuspendLayout();
            this.tabFastaFilepaths.SuspendLayout();
            this.tabSourceSearchPaths.SuspendLayout();
            this.psmLevelFilterGroupBox.SuspendLayout();
            this.tableLayoutPanel4.SuspendLayout();
            this.proteinLevelFilterGroupBox.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.importSettingsGroupBox.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.distinctMatchFormatGroupBox.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label5
            // 
            label5.Anchor = System.Windows.Forms.AnchorStyles.Left;
            label5.AutoSize = true;
            label5.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label5.Location = new System.Drawing.Point(3, 32);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(148, 13);
            label5.TabIndex = 127;
            label5.Text = "Minimum spectra per peptide:";
            // 
            // lblMinDistinctPeptides
            // 
            lblMinDistinctPeptides.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblMinDistinctPeptides.AutoSize = true;
            lblMinDistinctPeptides.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            lblMinDistinctPeptides.Location = new System.Drawing.Point(3, 6);
            lblMinDistinctPeptides.Name = "lblMinDistinctPeptides";
            lblMinDistinctPeptides.Size = new System.Drawing.Size(132, 13);
            lblMinDistinctPeptides.TabIndex = 127;
            lblMinDistinctPeptides.Text = "Minimum distinct peptides:";
            // 
            // label8
            // 
            label8.Anchor = System.Windows.Forms.AnchorStyles.Left;
            label8.AutoSize = true;
            label8.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label8.Location = new System.Drawing.Point(3, 53);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(112, 13);
            label8.TabIndex = 138;
            label8.Text = "Maximum import rank:";
            // 
            // label11
            // 
            label11.Anchor = System.Windows.Forms.AnchorStyles.Left;
            label11.AutoSize = true;
            label11.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label11.Location = new System.Drawing.Point(3, 82);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(158, 13);
            label11.TabIndex = 143;
            label11.Text = "Mod. mass rounded to nearest:";
            // 
            // gbSearchPaths
            // 
            this.gbSearchPaths.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbSearchPaths.Controls.Add(this.btnRemove);
            this.gbSearchPaths.Controls.Add(this.btnAddRelative);
            this.gbSearchPaths.Controls.Add(this.btnClear);
            this.gbSearchPaths.Controls.Add(this.btnBrowse);
            this.gbSearchPaths.Controls.Add(this.searchPathsTabControl);
            this.gbSearchPaths.Location = new System.Drawing.Point(265, 12);
            this.gbSearchPaths.Name = "gbSearchPaths";
            this.gbSearchPaths.Size = new System.Drawing.Size(507, 509);
            this.gbSearchPaths.TabIndex = 112;
            this.gbSearchPaths.TabStop = false;
            this.gbSearchPaths.Text = "Search Paths";
            // 
            // btnRemove
            // 
            this.btnRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemove.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRemove.Location = new System.Drawing.Point(417, 100);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(79, 23);
            this.btnRemove.TabIndex = 52;
            this.btnRemove.Text = "Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnAddRelative
            // 
            this.btnAddRelative.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddRelative.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAddRelative.Location = new System.Drawing.Point(417, 71);
            this.btnAddRelative.Name = "btnAddRelative";
            this.btnAddRelative.Size = new System.Drawing.Size(79, 23);
            this.btnAddRelative.TabIndex = 110;
            this.btnAddRelative.Text = "Add Relative";
            this.btnAddRelative.UseVisualStyleBackColor = true;
            this.btnAddRelative.Click += new System.EventHandler(this.btnAddRelative_Click);
            // 
            // btnClear
            // 
            this.btnClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClear.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClear.Location = new System.Drawing.Point(417, 129);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(79, 23);
            this.btnClear.TabIndex = 53;
            this.btnClear.Text = "Clear";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // btnBrowse
            // 
            this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowse.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnBrowse.Location = new System.Drawing.Point(417, 42);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(79, 23);
            this.btnBrowse.TabIndex = 51;
            this.btnBrowse.Text = "Add Path";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // searchPathsTabControl
            // 
            this.searchPathsTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchPathsTabControl.Controls.Add(this.tabFastaFilepaths);
            this.searchPathsTabControl.Controls.Add(this.tabSourceSearchPaths);
            this.searchPathsTabControl.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.searchPathsTabControl.Location = new System.Drawing.Point(17, 20);
            this.searchPathsTabControl.Name = "searchPathsTabControl";
            this.searchPathsTabControl.SelectedIndex = 0;
            this.searchPathsTabControl.Size = new System.Drawing.Size(394, 477);
            this.searchPathsTabControl.TabIndex = 107;
            this.searchPathsTabControl.SelectedIndexChanged += new System.EventHandler(this.lbSearchPaths_SelectedIndexChanged);
            // 
            // tabFastaFilepaths
            // 
            this.tabFastaFilepaths.BackColor = System.Drawing.Color.White;
            this.tabFastaFilepaths.Controls.Add(this.label2);
            this.tabFastaFilepaths.Controls.Add(this.lbFastaPaths);
            this.tabFastaFilepaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabFastaFilepaths.Location = new System.Drawing.Point(4, 22);
            this.tabFastaFilepaths.Name = "tabFastaFilepaths";
            this.tabFastaFilepaths.Padding = new System.Windows.Forms.Padding(3);
            this.tabFastaFilepaths.Size = new System.Drawing.Size(386, 451);
            this.tabFastaFilepaths.TabIndex = 2;
            this.tabFastaFilepaths.Text = "FASTA";
            this.tabFastaFilepaths.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 6);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(271, 13);
            this.label2.TabIndex = 50;
            this.label2.Text = "Protein databases contain sequences in FASTA format.";
            // 
            // lbFastaPaths
            // 
            this.lbFastaPaths.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lbFastaPaths.BackColor = System.Drawing.Color.White;
            this.lbFastaPaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbFastaPaths.FormattingEnabled = true;
            this.lbFastaPaths.HorizontalScrollbar = true;
            this.lbFastaPaths.IntegralHeight = false;
            this.lbFastaPaths.Location = new System.Drawing.Point(10, 22);
            this.lbFastaPaths.Name = "lbFastaPaths";
            this.lbFastaPaths.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lbFastaPaths.Size = new System.Drawing.Size(380, 423);
            this.lbFastaPaths.TabIndex = 49;
            // 
            // tabSourceSearchPaths
            // 
            this.tabSourceSearchPaths.BackColor = System.Drawing.Color.White;
            this.tabSourceSearchPaths.Controls.Add(this.label1);
            this.tabSourceSearchPaths.Controls.Add(this.lbSourcePaths);
            this.tabSourceSearchPaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabSourceSearchPaths.Location = new System.Drawing.Point(4, 22);
            this.tabSourceSearchPaths.Name = "tabSourceSearchPaths";
            this.tabSourceSearchPaths.Padding = new System.Windows.Forms.Padding(3);
            this.tabSourceSearchPaths.Size = new System.Drawing.Size(386, 474);
            this.tabSourceSearchPaths.TabIndex = 1;
            this.tabSourceSearchPaths.Text = "Source";
            this.tabSourceSearchPaths.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(3, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(273, 13);
            this.label1.TabIndex = 50;
            this.label1.Text = "Source files contain profile or centroid data for spectra.";
            // 
            // lbSourcePaths
            // 
            this.lbSourcePaths.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lbSourcePaths.BackColor = System.Drawing.Color.White;
            this.lbSourcePaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbSourcePaths.FormattingEnabled = true;
            this.lbSourcePaths.HorizontalScrollbar = true;
            this.lbSourcePaths.IntegralHeight = false;
            this.lbSourcePaths.Location = new System.Drawing.Point(3, 25);
            this.lbSourcePaths.Name = "lbSourcePaths";
            this.lbSourcePaths.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lbSourcePaths.Size = new System.Drawing.Size(394, 297);
            this.lbSourcePaths.TabIndex = 49;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCancel.Location = new System.Drawing.Point(697, 550);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 114;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnOk.Location = new System.Drawing.Point(616, 550);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 113;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // psmLevelFilterGroupBox
            // 
            this.psmLevelFilterGroupBox.Controls.Add(this.tableLayoutPanel4);
            this.psmLevelFilterGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.psmLevelFilterGroupBox.Location = new System.Drawing.Point(12, 12);
            this.psmLevelFilterGroupBox.Name = "psmLevelFilterGroupBox";
            this.psmLevelFilterGroupBox.Size = new System.Drawing.Size(247, 133);
            this.psmLevelFilterGroupBox.TabIndex = 128;
            this.psmLevelFilterGroupBox.TabStop = false;
            this.psmLevelFilterGroupBox.Text = "Default Peptide-Spectrum-Match Filters";
            // 
            // tableLayoutPanel4
            // 
            this.tableLayoutPanel4.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel4.ColumnCount = 3;
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel4.Controls.Add(this.label3, 0, 3);
            this.tableLayoutPanel4.Controls.Add(this.maxProteinGroupsTextBox, 1, 3);
            this.tableLayoutPanel4.Controls.Add(this.label4, 0, 2);
            this.tableLayoutPanel4.Controls.Add(this.maxQValueComboBox, 1, 0);
            this.tableLayoutPanel4.Controls.Add(this.minSpectraPerMatchTextBox, 1, 2);
            this.tableLayoutPanel4.Controls.Add(label5, 0, 1);
            this.tableLayoutPanel4.Controls.Add(this.minSpectraPerPeptideTextBox, 1, 1);
            this.tableLayoutPanel4.Controls.Add(this.lblMaxFdr, 0, 0);
            this.tableLayoutPanel4.Controls.Add(this.label21, 2, 0);
            this.tableLayoutPanel4.Location = new System.Drawing.Point(6, 20);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            this.tableLayoutPanel4.RowCount = 4;
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel4.Size = new System.Drawing.Size(241, 107);
            this.tableLayoutPanel4.TabIndex = 142;
            // 
            // label3
            // 
            this.label3.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(3, 86);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(128, 13);
            this.label3.TabIndex = 134;
            this.label3.Text = "Maximum protein groups:";
            // 
            // maxProteinGroupsTextBox
            // 
            this.maxProteinGroupsTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.maxProteinGroupsTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.maxProteinGroupsTextBox.Location = new System.Drawing.Point(173, 82);
            this.maxProteinGroupsTextBox.Name = "maxProteinGroupsTextBox";
            this.maxProteinGroupsTextBox.Size = new System.Drawing.Size(45, 21);
            this.maxProteinGroupsTextBox.TabIndex = 133;
            this.maxProteinGroupsTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // label4
            // 
            this.label4.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(3, 58);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(141, 13);
            this.label4.TabIndex = 132;
            this.label4.Text = "Minimum spectra per match:";
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
            this.maxQValueComboBox.Location = new System.Drawing.Point(173, 3);
            this.maxQValueComboBox.Name = "maxQValueComboBox";
            this.maxQValueComboBox.Size = new System.Drawing.Size(45, 21);
            this.maxQValueComboBox.TabIndex = 4;
            this.maxQValueComboBox.Text = "5";
            this.maxQValueComboBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.doubleTextBox_KeyDown);
            // 
            // minSpectraPerMatchTextBox
            // 
            this.minSpectraPerMatchTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minSpectraPerMatchTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minSpectraPerMatchTextBox.Location = new System.Drawing.Point(173, 55);
            this.minSpectraPerMatchTextBox.Name = "minSpectraPerMatchTextBox";
            this.minSpectraPerMatchTextBox.Size = new System.Drawing.Size(45, 21);
            this.minSpectraPerMatchTextBox.TabIndex = 9;
            this.minSpectraPerMatchTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // minSpectraPerPeptideTextBox
            // 
            this.minSpectraPerPeptideTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minSpectraPerPeptideTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minSpectraPerPeptideTextBox.Location = new System.Drawing.Point(173, 29);
            this.minSpectraPerPeptideTextBox.Name = "minSpectraPerPeptideTextBox";
            this.minSpectraPerPeptideTextBox.Size = new System.Drawing.Size(45, 21);
            this.minSpectraPerPeptideTextBox.TabIndex = 7;
            this.minSpectraPerPeptideTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // lblMaxFdr
            // 
            this.lblMaxFdr.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMaxFdr.AutoSize = true;
            this.lblMaxFdr.BackColor = System.Drawing.Color.Transparent;
            this.lblMaxFdr.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMaxFdr.Location = new System.Drawing.Point(3, 6);
            this.lblMaxFdr.Name = "lblMaxFdr";
            this.lblMaxFdr.Size = new System.Drawing.Size(78, 13);
            this.lblMaxFdr.TabIndex = 125;
            this.lblMaxFdr.Text = "Maximum FDR:";
            // 
            // label21
            // 
            this.label21.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label21.AutoSize = true;
            this.label21.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label21.Location = new System.Drawing.Point(221, 6);
            this.label21.Margin = new System.Windows.Forms.Padding(0);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(18, 13);
            this.label21.TabIndex = 142;
            this.label21.Text = "%";
            // 
            // proteinLevelFilterGroupBox
            // 
            this.proteinLevelFilterGroupBox.Controls.Add(this.tableLayoutPanel2);
            this.proteinLevelFilterGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.proteinLevelFilterGroupBox.Location = new System.Drawing.Point(12, 284);
            this.proteinLevelFilterGroupBox.Name = "proteinLevelFilterGroupBox";
            this.proteinLevelFilterGroupBox.Size = new System.Drawing.Size(247, 126);
            this.proteinLevelFilterGroupBox.TabIndex = 127;
            this.proteinLevelFilterGroupBox.TabStop = false;
            this.proteinLevelFilterGroupBox.Text = "Default Protein/Gene Level Filters";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.Controls.Add(this.minSpectraTextBox, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this.filterByGeneCheckBox, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this.minAdditionalPeptidesTextBox, 1, 1);
            this.tableLayoutPanel2.Controls.Add(lblMinDistinctPeptides, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.minDistinctPeptidesTextBox, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.lblMinSpectraPerProtein, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.lblParsimonyVariable, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.label15, 0, 3);
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 20);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 4;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(222, 100);
            this.tableLayoutPanel2.TabIndex = 140;
            // 
            // minSpectraTextBox
            // 
            this.minSpectraTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.minSpectraTextBox.Location = new System.Drawing.Point(173, 53);
            this.minSpectraTextBox.Name = "minSpectraTextBox";
            this.minSpectraTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraTextBox.TabIndex = 11;
            this.minSpectraTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // filterByGeneCheckBox
            // 
            this.filterByGeneCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.filterByGeneCheckBox.AutoSize = true;
            this.filterByGeneCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.filterByGeneCheckBox.Location = new System.Drawing.Point(173, 80);
            this.filterByGeneCheckBox.Name = "filterByGeneCheckBox";
            this.filterByGeneCheckBox.Size = new System.Drawing.Size(15, 14);
            this.filterByGeneCheckBox.TabIndex = 141;
            this.filterByGeneCheckBox.UseVisualStyleBackColor = true;
            // 
            // minAdditionalPeptidesTextBox
            // 
            this.minAdditionalPeptidesTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.minAdditionalPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minAdditionalPeptidesTextBox.Location = new System.Drawing.Point(173, 28);
            this.minAdditionalPeptidesTextBox.Name = "minAdditionalPeptidesTextBox";
            this.minAdditionalPeptidesTextBox.Size = new System.Drawing.Size(46, 21);
            this.minAdditionalPeptidesTextBox.TabIndex = 9;
            this.minAdditionalPeptidesTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // minDistinctPeptidesTextBox
            // 
            this.minDistinctPeptidesTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.minDistinctPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minDistinctPeptidesTextBox.Location = new System.Drawing.Point(173, 3);
            this.minDistinctPeptidesTextBox.Name = "minDistinctPeptidesTextBox";
            this.minDistinctPeptidesTextBox.Size = new System.Drawing.Size(46, 21);
            this.minDistinctPeptidesTextBox.TabIndex = 7;
            this.minDistinctPeptidesTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // lblMinSpectraPerProtein
            // 
            this.lblMinSpectraPerProtein.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMinSpectraPerProtein.AutoSize = true;
            this.lblMinSpectraPerProtein.Location = new System.Drawing.Point(3, 56);
            this.lblMinSpectraPerProtein.Name = "lblMinSpectraPerProtein";
            this.lblMinSpectraPerProtein.Size = new System.Drawing.Size(90, 13);
            this.lblMinSpectraPerProtein.TabIndex = 133;
            this.lblMinSpectraPerProtein.Text = "Minimum spectra:";
            // 
            // lblParsimonyVariable
            // 
            this.lblParsimonyVariable.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblParsimonyVariable.AutoSize = true;
            this.lblParsimonyVariable.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblParsimonyVariable.Location = new System.Drawing.Point(3, 31);
            this.lblParsimonyVariable.Name = "lblParsimonyVariable";
            this.lblParsimonyVariable.Size = new System.Drawing.Size(144, 13);
            this.lblParsimonyVariable.TabIndex = 132;
            this.lblParsimonyVariable.Text = "Minimum additional peptides:";
            // 
            // label15
            // 
            this.label15.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(3, 81);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(138, 13);
            this.label15.TabIndex = 142;
            this.label15.Text = "Filter by gene (if possible): ";
            // 
            // defaultDecoyPrefixTextBox
            // 
            this.defaultDecoyPrefixTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tableLayoutPanel3.SetColumnSpan(this.defaultDecoyPrefixTextBox, 2);
            this.defaultDecoyPrefixTextBox.Location = new System.Drawing.Point(173, 3);
            this.defaultDecoyPrefixTextBox.Name = "defaultDecoyPrefixTextBox";
            this.defaultDecoyPrefixTextBox.Size = new System.Drawing.Size(45, 20);
            this.defaultDecoyPrefixTextBox.TabIndex = 129;
            // 
            // label6
            // 
            this.label6.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(3, 5);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(69, 13);
            this.label6.TabIndex = 134;
            this.label6.Text = "Decoy prefix:";
            // 
            // qonverterSettingsButton
            // 
            this.qonverterSettingsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.qonverterSettingsButton.Location = new System.Drawing.Point(12, 550);
            this.qonverterSettingsButton.Name = "qonverterSettingsButton";
            this.qonverterSettingsButton.Size = new System.Drawing.Size(115, 23);
            this.qonverterSettingsButton.TabIndex = 135;
            this.qonverterSettingsButton.Text = "Qonverter Settings";
            this.qonverterSettingsButton.UseVisualStyleBackColor = true;
            this.qonverterSettingsButton.Click += new System.EventHandler(this.qonverterSettingsButton_Click);
            // 
            // importSettingsGroupBox
            // 
            this.importSettingsGroupBox.Controls.Add(this.tableLayoutPanel3);
            this.importSettingsGroupBox.Location = new System.Drawing.Point(12, 420);
            this.importSettingsGroupBox.Name = "importSettingsGroupBox";
            this.importSettingsGroupBox.Size = new System.Drawing.Size(247, 124);
            this.importSettingsGroupBox.TabIndex = 136;
            this.importSettingsGroupBox.TabStop = false;
            this.importSettingsGroupBox.Text = "Default Import Settings";
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel3.ColumnCount = 3;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel3.Controls.Add(this.maxImportRankTextBox, 1, 2);
            this.tableLayoutPanel3.Controls.Add(this.ignoreUnmappedPeptidesCheckBox, 1, 3);
            this.tableLayoutPanel3.Controls.Add(this.label16, 0, 3);
            this.tableLayoutPanel3.Controls.Add(this.label9, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this.defaultDecoyPrefixTextBox, 1, 0);
            this.tableLayoutPanel3.Controls.Add(this.label6, 0, 0);
            this.tableLayoutPanel3.Controls.Add(label8, 0, 2);
            this.tableLayoutPanel3.Controls.Add(this.maxImportFdrComboBox, 1, 1);
            this.tableLayoutPanel3.Controls.Add(this.label7, 2, 1);
            this.tableLayoutPanel3.Location = new System.Drawing.Point(6, 19);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 4;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(241, 99);
            this.tableLayoutPanel3.TabIndex = 141;
            // 
            // maxImportRankTextBox
            // 
            this.maxImportRankTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tableLayoutPanel3.SetColumnSpan(this.maxImportRankTextBox, 2);
            this.maxImportRankTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.maxImportRankTextBox.Location = new System.Drawing.Point(173, 51);
            this.maxImportRankTextBox.Name = "maxImportRankTextBox";
            this.maxImportRankTextBox.Size = new System.Drawing.Size(45, 21);
            this.maxImportRankTextBox.TabIndex = 136;
            // 
            // ignoreUnmappedPeptidesCheckBox
            // 
            this.ignoreUnmappedPeptidesCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.ignoreUnmappedPeptidesCheckBox.AutoSize = true;
            this.ignoreUnmappedPeptidesCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.tableLayoutPanel3.SetColumnSpan(this.ignoreUnmappedPeptidesCheckBox, 2);
            this.ignoreUnmappedPeptidesCheckBox.Location = new System.Drawing.Point(173, 78);
            this.ignoreUnmappedPeptidesCheckBox.Name = "ignoreUnmappedPeptidesCheckBox";
            this.ignoreUnmappedPeptidesCheckBox.Size = new System.Drawing.Size(15, 14);
            this.ignoreUnmappedPeptidesCheckBox.TabIndex = 140;
            this.ignoreUnmappedPeptidesCheckBox.UseVisualStyleBackColor = true;
            // 
            // label16
            // 
            this.label16.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(3, 79);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(136, 13);
            this.label16.TabIndex = 141;
            this.label16.Text = "Ignore unmapped peptides:";
            // 
            // label9
            // 
            this.label9.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label9.AutoSize = true;
            this.label9.BackColor = System.Drawing.Color.Transparent;
            this.label9.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.Location = new System.Drawing.Point(3, 29);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(111, 13);
            this.label9.TabIndex = 137;
            this.label9.Text = "Maximum import FDR:";
            // 
            // maxImportFdrComboBox
            // 
            this.maxImportFdrComboBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.maxImportFdrComboBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.maxImportFdrComboBox.FormattingEnabled = true;
            this.maxImportFdrComboBox.Items.AddRange(new object[] {
            "5",
            "10",
            "15",
            "20",
            "25",
            "50",
            "75",
            "100"});
            this.maxImportFdrComboBox.Location = new System.Drawing.Point(173, 27);
            this.maxImportFdrComboBox.Name = "maxImportFdrComboBox";
            this.maxImportFdrComboBox.Size = new System.Drawing.Size(45, 21);
            this.maxImportFdrComboBox.TabIndex = 135;
            this.maxImportFdrComboBox.Text = "25";
            // 
            // label7
            // 
            this.label7.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(221, 29);
            this.label7.Margin = new System.Windows.Forms.Padding(0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(18, 13);
            this.label7.TabIndex = 142;
            this.label7.Text = "%";
            // 
            // sourceExtensionsTextBox
            // 
            this.sourceExtensionsTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sourceExtensionsTextBox.Location = new System.Drawing.Point(229, 552);
            this.sourceExtensionsTextBox.Name = "sourceExtensionsTextBox";
            this.sourceExtensionsTextBox.Size = new System.Drawing.Size(370, 20);
            this.sourceExtensionsTextBox.TabIndex = 137;
            // 
            // label10
            // 
            this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(133, 555);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(97, 13);
            this.label10.TabIndex = 138;
            this.label10.Text = "Source extensions:";
            // 
            // chargeIsDistinctCheckBox
            // 
            this.chargeIsDistinctCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chargeIsDistinctCheckBox.AutoSize = true;
            this.chargeIsDistinctCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chargeIsDistinctCheckBox.Location = new System.Drawing.Point(173, 5);
            this.chargeIsDistinctCheckBox.Name = "chargeIsDistinctCheckBox";
            this.chargeIsDistinctCheckBox.Size = new System.Drawing.Size(15, 14);
            this.chargeIsDistinctCheckBox.TabIndex = 142;
            this.chargeIsDistinctCheckBox.UseVisualStyleBackColor = true;
            // 
            // distinctMatchFormatGroupBox
            // 
            this.distinctMatchFormatGroupBox.Controls.Add(this.tableLayoutPanel1);
            this.distinctMatchFormatGroupBox.Location = new System.Drawing.Point(12, 151);
            this.distinctMatchFormatGroupBox.Name = "distinctMatchFormatGroupBox";
            this.distinctMatchFormatGroupBox.Size = new System.Drawing.Size(247, 127);
            this.distinctMatchFormatGroupBox.TabIndex = 139;
            this.distinctMatchFormatGroupBox.TabStop = false;
            this.distinctMatchFormatGroupBox.Text = "Default Distinct Match Format";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this.modificationRoundToMassTextBox, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.modificationsAreDistinctCheckbox, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.analysisIsDistinctCheckBox, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.chargeIsDistinctCheckBox, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label12, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label13, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label14, 0, 2);
            this.tableLayoutPanel1.Controls.Add(label11, 0, 3);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(6, 19);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(222, 102);
            this.tableLayoutPanel1.TabIndex = 140;
            // 
            // modificationRoundToMassTextBox
            // 
            this.modificationRoundToMassTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.modificationRoundToMassTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.modificationRoundToMassTextBox.Location = new System.Drawing.Point(173, 78);
            this.modificationRoundToMassTextBox.Name = "modificationRoundToMassTextBox";
            this.modificationRoundToMassTextBox.Size = new System.Drawing.Size(46, 21);
            this.modificationRoundToMassTextBox.TabIndex = 142;
            // 
            // modificationsAreDistinctCheckbox
            // 
            this.modificationsAreDistinctCheckbox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.modificationsAreDistinctCheckbox.AutoSize = true;
            this.modificationsAreDistinctCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.modificationsAreDistinctCheckbox.Location = new System.Drawing.Point(173, 55);
            this.modificationsAreDistinctCheckbox.Name = "modificationsAreDistinctCheckbox";
            this.modificationsAreDistinctCheckbox.Size = new System.Drawing.Size(15, 14);
            this.modificationsAreDistinctCheckbox.TabIndex = 144;
            this.modificationsAreDistinctCheckbox.UseVisualStyleBackColor = true;
            // 
            // analysisIsDistinctCheckBox
            // 
            this.analysisIsDistinctCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.analysisIsDistinctCheckBox.AutoSize = true;
            this.analysisIsDistinctCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.analysisIsDistinctCheckBox.Location = new System.Drawing.Point(173, 30);
            this.analysisIsDistinctCheckBox.Name = "analysisIsDistinctCheckBox";
            this.analysisIsDistinctCheckBox.Size = new System.Drawing.Size(15, 14);
            this.analysisIsDistinctCheckBox.TabIndex = 143;
            this.analysisIsDistinctCheckBox.UseVisualStyleBackColor = true;
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
            // nonFixedDriveWarningCheckBox
            // 
            this.nonFixedDriveWarningCheckBox.AutoSize = true;
            this.nonFixedDriveWarningCheckBox.Location = new System.Drawing.Point(292, 527);
            this.nonFixedDriveWarningCheckBox.Name = "nonFixedDriveWarningCheckBox";
            this.nonFixedDriveWarningCheckBox.Size = new System.Drawing.Size(216, 17);
            this.nonFixedDriveWarningCheckBox.TabIndex = 140;
            this.nonFixedDriveWarningCheckBox.Text = "Warn about non-fixed-drive performance";
            this.nonFixedDriveWarningCheckBox.UseVisualStyleBackColor = true;
            // 
            // embedGeneMetadataWarningCheckBox
            // 
            this.embedGeneMetadataWarningCheckBox.AutoSize = true;
            this.embedGeneMetadataWarningCheckBox.Location = new System.Drawing.Point(525, 527);
            this.embedGeneMetadataWarningCheckBox.Name = "embedGeneMetadataWarningCheckBox";
            this.embedGeneMetadataWarningCheckBox.Size = new System.Drawing.Size(211, 17);
            this.embedGeneMetadataWarningCheckBox.TabIndex = 141;
            this.embedGeneMetadataWarningCheckBox.Text = "Warn about embedding gene metadata";
            this.embedGeneMetadataWarningCheckBox.UseVisualStyleBackColor = true;
            // 
            // DefaultSettingsManagerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 577);
            this.Controls.Add(this.embedGeneMetadataWarningCheckBox);
            this.Controls.Add(this.nonFixedDriveWarningCheckBox);
            this.Controls.Add(this.distinctMatchFormatGroupBox);
            this.Controls.Add(this.sourceExtensionsTextBox);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.importSettingsGroupBox);
            this.Controls.Add(this.qonverterSettingsButton);
            this.Controls.Add(this.psmLevelFilterGroupBox);
            this.Controls.Add(this.proteinLevelFilterGroupBox);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.gbSearchPaths);
            this.MaximumSize = new System.Drawing.Size(2000, 615);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(800, 615);
            this.Name = "DefaultSettingsManagerForm";
            this.Text = "Default Settings Manager";
            this.gbSearchPaths.ResumeLayout(false);
            this.searchPathsTabControl.ResumeLayout(false);
            this.tabFastaFilepaths.ResumeLayout(false);
            this.tabFastaFilepaths.PerformLayout();
            this.tabSourceSearchPaths.ResumeLayout(false);
            this.tabSourceSearchPaths.PerformLayout();
            this.psmLevelFilterGroupBox.ResumeLayout(false);
            this.tableLayoutPanel4.ResumeLayout(false);
            this.tableLayoutPanel4.PerformLayout();
            this.proteinLevelFilterGroupBox.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.importSettingsGroupBox.ResumeLayout(false);
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.distinctMatchFormatGroupBox.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox gbSearchPaths;
        private System.Windows.Forms.TabControl searchPathsTabControl;
        private System.Windows.Forms.TabPage tabSourceSearchPaths;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox lbSourcePaths;
        private System.Windows.Forms.TabPage tabFastaFilepaths;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ListBox lbFastaPaths;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnAddRelative;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.GroupBox psmLevelFilterGroupBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox maxProteinGroupsTextBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox minSpectraPerMatchTextBox;
        private System.Windows.Forms.ComboBox maxQValueComboBox;
        private System.Windows.Forms.TextBox minSpectraPerPeptideTextBox;
        private System.Windows.Forms.Label lblMaxFdr;
        private System.Windows.Forms.GroupBox proteinLevelFilterGroupBox;
        private System.Windows.Forms.TextBox minSpectraTextBox;
        private System.Windows.Forms.Label lblMinSpectraPerProtein;
        private System.Windows.Forms.Label lblParsimonyVariable;
        private System.Windows.Forms.TextBox minAdditionalPeptidesTextBox;
        private System.Windows.Forms.TextBox minDistinctPeptidesTextBox;
        private System.Windows.Forms.TextBox defaultDecoyPrefixTextBox;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button qonverterSettingsButton;
        private System.Windows.Forms.GroupBox importSettingsGroupBox;
        private System.Windows.Forms.CheckBox ignoreUnmappedPeptidesCheckBox;
        private System.Windows.Forms.ComboBox maxImportFdrComboBox;
        private System.Windows.Forms.TextBox maxImportRankTextBox;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox sourceExtensionsTextBox;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.CheckBox filterByGeneCheckBox;
        private System.Windows.Forms.CheckBox chargeIsDistinctCheckBox;
        private System.Windows.Forms.GroupBox distinctMatchFormatGroupBox;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox modificationRoundToMassTextBox;
        private System.Windows.Forms.CheckBox modificationsAreDistinctCheckbox;
        private System.Windows.Forms.CheckBox analysisIsDistinctCheckBox;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel4;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.CheckBox nonFixedDriveWarningCheckBox;
        private System.Windows.Forms.CheckBox embedGeneMetadataWarningCheckBox;
    }
}