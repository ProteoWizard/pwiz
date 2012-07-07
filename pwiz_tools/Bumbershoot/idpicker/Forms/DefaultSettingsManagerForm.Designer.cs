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
            this.label3 = new System.Windows.Forms.Label();
            this.maxProteinGroupsTextBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.lblPercentSign = new System.Windows.Forms.Label();
            this.minSpectraPerMatchTextBox = new System.Windows.Forms.TextBox();
            this.maxQValueComboBox = new System.Windows.Forms.ComboBox();
            this.minSpectraPerPeptideTextBox = new System.Windows.Forms.TextBox();
            this.lblMaxFdr = new System.Windows.Forms.Label();
            this.proteinLevelFilterGroupBox = new System.Windows.Forms.GroupBox();
            this.minSpectraPerProteinTextBox = new System.Windows.Forms.TextBox();
            this.lblMinSpectraPerProtein = new System.Windows.Forms.Label();
            this.lblParsimonyVariable = new System.Windows.Forms.Label();
            this.minAdditionalPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.minDistinctPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.defaultDecoyPrefixTextBox = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.qonverterSettingsButton = new System.Windows.Forms.Button();
            this.importSettingsGroupBox = new System.Windows.Forms.GroupBox();
            this.label7 = new System.Windows.Forms.Label();
            this.maxImportFdrComboBox = new System.Windows.Forms.ComboBox();
            this.maxImportRankTextBox = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.ignoreUnmappedPeptidesCheckBox = new System.Windows.Forms.CheckBox();
            this.sourceExtensionsTextBox = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            label5 = new System.Windows.Forms.Label();
            lblMinDistinctPeptides = new System.Windows.Forms.Label();
            label8 = new System.Windows.Forms.Label();
            this.gbSearchPaths.SuspendLayout();
            this.searchPathsTabControl.SuspendLayout();
            this.tabFastaFilepaths.SuspendLayout();
            this.tabSourceSearchPaths.SuspendLayout();
            this.psmLevelFilterGroupBox.SuspendLayout();
            this.proteinLevelFilterGroupBox.SuspendLayout();
            this.importSettingsGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label5.Location = new System.Drawing.Point(15, 51);
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
            lblMinDistinctPeptides.Location = new System.Drawing.Point(15, 24);
            lblMinDistinctPeptides.Name = "lblMinDistinctPeptides";
            lblMinDistinctPeptides.Size = new System.Drawing.Size(132, 13);
            lblMinDistinctPeptides.TabIndex = 127;
            lblMinDistinctPeptides.Text = "Minimum distinct peptides:";
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
            this.gbSearchPaths.Location = new System.Drawing.Point(251, 12);
            this.gbSearchPaths.Name = "gbSearchPaths";
            this.gbSearchPaths.Size = new System.Drawing.Size(521, 383);
            this.gbSearchPaths.TabIndex = 112;
            this.gbSearchPaths.TabStop = false;
            this.gbSearchPaths.Text = "Search Paths";
            // 
            // btnRemove
            // 
            this.btnRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemove.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRemove.Location = new System.Drawing.Point(431, 100);
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
            this.btnAddRelative.Location = new System.Drawing.Point(431, 71);
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
            this.btnClear.Location = new System.Drawing.Point(431, 129);
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
            this.btnBrowse.Location = new System.Drawing.Point(431, 42);
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
            this.searchPathsTabControl.Size = new System.Drawing.Size(408, 351);
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
            this.tabFastaFilepaths.Size = new System.Drawing.Size(400, 325);
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
            this.lbFastaPaths.Location = new System.Drawing.Point(3, 25);
            this.lbFastaPaths.Name = "lbFastaPaths";
            this.lbFastaPaths.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lbFastaPaths.Size = new System.Drawing.Size(394, 297);
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
            this.tabSourceSearchPaths.Size = new System.Drawing.Size(400, 325);
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
            this.btnCancel.Location = new System.Drawing.Point(697, 400);
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
            this.btnOk.Location = new System.Drawing.Point(616, 400);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 113;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // psmLevelFilterGroupBox
            // 
            this.psmLevelFilterGroupBox.Controls.Add(this.label3);
            this.psmLevelFilterGroupBox.Controls.Add(this.maxProteinGroupsTextBox);
            this.psmLevelFilterGroupBox.Controls.Add(this.label4);
            this.psmLevelFilterGroupBox.Controls.Add(this.lblPercentSign);
            this.psmLevelFilterGroupBox.Controls.Add(this.minSpectraPerMatchTextBox);
            this.psmLevelFilterGroupBox.Controls.Add(this.maxQValueComboBox);
            this.psmLevelFilterGroupBox.Controls.Add(this.minSpectraPerPeptideTextBox);
            this.psmLevelFilterGroupBox.Controls.Add(label5);
            this.psmLevelFilterGroupBox.Controls.Add(this.lblMaxFdr);
            this.psmLevelFilterGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.psmLevelFilterGroupBox.Location = new System.Drawing.Point(12, 12);
            this.psmLevelFilterGroupBox.Name = "psmLevelFilterGroupBox";
            this.psmLevelFilterGroupBox.Size = new System.Drawing.Size(233, 139);
            this.psmLevelFilterGroupBox.TabIndex = 128;
            this.psmLevelFilterGroupBox.TabStop = false;
            this.psmLevelFilterGroupBox.Text = "Default Peptide-Spectrum-Match Filters";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(15, 105);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(128, 13);
            this.label3.TabIndex = 134;
            this.label3.Text = "Maximum protein groups:";
            // 
            // maxProteinGroupsTextBox
            // 
            this.maxProteinGroupsTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.maxProteinGroupsTextBox.Location = new System.Drawing.Point(167, 102);
            this.maxProteinGroupsTextBox.Name = "maxProteinGroupsTextBox";
            this.maxProteinGroupsTextBox.Size = new System.Drawing.Size(46, 21);
            this.maxProteinGroupsTextBox.TabIndex = 133;
            this.maxProteinGroupsTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(15, 78);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(141, 13);
            this.label4.TabIndex = 132;
            this.label4.Text = "Minimum spectra per match:";
            // 
            // lblPercentSign
            // 
            this.lblPercentSign.AutoSize = true;
            this.lblPercentSign.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPercentSign.Location = new System.Drawing.Point(214, 24);
            this.lblPercentSign.Margin = new System.Windows.Forms.Padding(0);
            this.lblPercentSign.Name = "lblPercentSign";
            this.lblPercentSign.Size = new System.Drawing.Size(18, 13);
            this.lblPercentSign.TabIndex = 129;
            this.lblPercentSign.Text = "%";
            // 
            // minSpectraPerMatchTextBox
            // 
            this.minSpectraPerMatchTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minSpectraPerMatchTextBox.Location = new System.Drawing.Point(167, 75);
            this.minSpectraPerMatchTextBox.Name = "minSpectraPerMatchTextBox";
            this.minSpectraPerMatchTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraPerMatchTextBox.TabIndex = 9;
            this.minSpectraPerMatchTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // maxQValueComboBox
            // 
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
            this.maxQValueComboBox.Location = new System.Drawing.Point(167, 21);
            this.maxQValueComboBox.Name = "maxQValueComboBox";
            this.maxQValueComboBox.Size = new System.Drawing.Size(45, 21);
            this.maxQValueComboBox.TabIndex = 4;
            this.maxQValueComboBox.Text = "5";
            this.maxQValueComboBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.doubleTextBox_KeyDown);
            // 
            // minSpectraPerPeptideTextBox
            // 
            this.minSpectraPerPeptideTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minSpectraPerPeptideTextBox.Location = new System.Drawing.Point(167, 48);
            this.minSpectraPerPeptideTextBox.Name = "minSpectraPerPeptideTextBox";
            this.minSpectraPerPeptideTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraPerPeptideTextBox.TabIndex = 7;
            this.minSpectraPerPeptideTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // lblMaxFdr
            // 
            this.lblMaxFdr.AutoSize = true;
            this.lblMaxFdr.BackColor = System.Drawing.Color.Transparent;
            this.lblMaxFdr.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMaxFdr.Location = new System.Drawing.Point(14, 24);
            this.lblMaxFdr.Name = "lblMaxFdr";
            this.lblMaxFdr.Size = new System.Drawing.Size(78, 13);
            this.lblMaxFdr.TabIndex = 125;
            this.lblMaxFdr.Text = "Maximum FDR:";
            // 
            // proteinLevelFilterGroupBox
            // 
            this.proteinLevelFilterGroupBox.Controls.Add(this.minSpectraPerProteinTextBox);
            this.proteinLevelFilterGroupBox.Controls.Add(this.lblMinSpectraPerProtein);
            this.proteinLevelFilterGroupBox.Controls.Add(this.lblParsimonyVariable);
            this.proteinLevelFilterGroupBox.Controls.Add(this.minAdditionalPeptidesTextBox);
            this.proteinLevelFilterGroupBox.Controls.Add(this.minDistinctPeptidesTextBox);
            this.proteinLevelFilterGroupBox.Controls.Add(lblMinDistinctPeptides);
            this.proteinLevelFilterGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.proteinLevelFilterGroupBox.Location = new System.Drawing.Point(12, 157);
            this.proteinLevelFilterGroupBox.Name = "proteinLevelFilterGroupBox";
            this.proteinLevelFilterGroupBox.Size = new System.Drawing.Size(233, 108);
            this.proteinLevelFilterGroupBox.TabIndex = 127;
            this.proteinLevelFilterGroupBox.TabStop = false;
            this.proteinLevelFilterGroupBox.Text = "Default Protein Level Filters";
            // 
            // minSpectraPerProteinTextBox
            // 
            this.minSpectraPerProteinTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minSpectraPerProteinTextBox.Location = new System.Drawing.Point(167, 74);
            this.minSpectraPerProteinTextBox.Name = "minSpectraPerProteinTextBox";
            this.minSpectraPerProteinTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraPerProteinTextBox.TabIndex = 11;
            this.minSpectraPerProteinTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // lblMinSpectraPerProtein
            // 
            this.lblMinSpectraPerProtein.AutoSize = true;
            this.lblMinSpectraPerProtein.Location = new System.Drawing.Point(15, 77);
            this.lblMinSpectraPerProtein.Name = "lblMinSpectraPerProtein";
            this.lblMinSpectraPerProtein.Size = new System.Drawing.Size(146, 13);
            this.lblMinSpectraPerProtein.TabIndex = 133;
            this.lblMinSpectraPerProtein.Text = "Minimum spectra per protein:";
            // 
            // lblParsimonyVariable
            // 
            this.lblParsimonyVariable.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblParsimonyVariable.AutoSize = true;
            this.lblParsimonyVariable.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblParsimonyVariable.Location = new System.Drawing.Point(15, 51);
            this.lblParsimonyVariable.Name = "lblParsimonyVariable";
            this.lblParsimonyVariable.Size = new System.Drawing.Size(144, 13);
            this.lblParsimonyVariable.TabIndex = 132;
            this.lblParsimonyVariable.Text = "Minimum additional peptides:";
            // 
            // minAdditionalPeptidesTextBox
            // 
            this.minAdditionalPeptidesTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minAdditionalPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minAdditionalPeptidesTextBox.Location = new System.Drawing.Point(167, 46);
            this.minAdditionalPeptidesTextBox.Name = "minAdditionalPeptidesTextBox";
            this.minAdditionalPeptidesTextBox.Size = new System.Drawing.Size(46, 21);
            this.minAdditionalPeptidesTextBox.TabIndex = 9;
            this.minAdditionalPeptidesTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // minDistinctPeptidesTextBox
            // 
            this.minDistinctPeptidesTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minDistinctPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minDistinctPeptidesTextBox.Location = new System.Drawing.Point(167, 19);
            this.minDistinctPeptidesTextBox.Name = "minDistinctPeptidesTextBox";
            this.minDistinctPeptidesTextBox.Size = new System.Drawing.Size(46, 21);
            this.minDistinctPeptidesTextBox.TabIndex = 7;
            this.minDistinctPeptidesTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // defaultDecoyPrefixTextBox
            // 
            this.defaultDecoyPrefixTextBox.Location = new System.Drawing.Point(167, 19);
            this.defaultDecoyPrefixTextBox.Name = "defaultDecoyPrefixTextBox";
            this.defaultDecoyPrefixTextBox.Size = new System.Drawing.Size(46, 20);
            this.defaultDecoyPrefixTextBox.TabIndex = 129;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(17, 22);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(69, 13);
            this.label6.TabIndex = 134;
            this.label6.Text = "Decoy prefix:";
            // 
            // qonverterSettingsButton
            // 
            this.qonverterSettingsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.qonverterSettingsButton.Location = new System.Drawing.Point(12, 400);
            this.qonverterSettingsButton.Name = "qonverterSettingsButton";
            this.qonverterSettingsButton.Size = new System.Drawing.Size(115, 23);
            this.qonverterSettingsButton.TabIndex = 135;
            this.qonverterSettingsButton.Text = "Qonverter Settings";
            this.qonverterSettingsButton.UseVisualStyleBackColor = true;
            this.qonverterSettingsButton.Click += new System.EventHandler(this.qonverterSettingsButton_Click);
            // 
            // importSettingsGroupBox
            // 
            this.importSettingsGroupBox.Controls.Add(this.ignoreUnmappedPeptidesCheckBox);
            this.importSettingsGroupBox.Controls.Add(this.label7);
            this.importSettingsGroupBox.Controls.Add(this.maxImportFdrComboBox);
            this.importSettingsGroupBox.Controls.Add(this.maxImportRankTextBox);
            this.importSettingsGroupBox.Controls.Add(label8);
            this.importSettingsGroupBox.Controls.Add(this.label9);
            this.importSettingsGroupBox.Controls.Add(this.defaultDecoyPrefixTextBox);
            this.importSettingsGroupBox.Controls.Add(this.label6);
            this.importSettingsGroupBox.Location = new System.Drawing.Point(12, 271);
            this.importSettingsGroupBox.Name = "importSettingsGroupBox";
            this.importSettingsGroupBox.Size = new System.Drawing.Size(233, 124);
            this.importSettingsGroupBox.TabIndex = 136;
            this.importSettingsGroupBox.TabStop = false;
            this.importSettingsGroupBox.Text = "Default Import Settings";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(214, 48);
            this.label7.Margin = new System.Windows.Forms.Padding(0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(18, 13);
            this.label7.TabIndex = 139;
            this.label7.Text = "%";
            // 
            // maxImportFdrComboBox
            // 
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
            this.maxImportFdrComboBox.Location = new System.Drawing.Point(167, 45);
            this.maxImportFdrComboBox.Name = "maxImportFdrComboBox";
            this.maxImportFdrComboBox.Size = new System.Drawing.Size(45, 21);
            this.maxImportFdrComboBox.TabIndex = 135;
            this.maxImportFdrComboBox.Text = "25";
            // 
            // maxImportRankTextBox
            // 
            this.maxImportRankTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.maxImportRankTextBox.Location = new System.Drawing.Point(167, 72);
            this.maxImportRankTextBox.Name = "maxImportRankTextBox";
            this.maxImportRankTextBox.Size = new System.Drawing.Size(46, 21);
            this.maxImportRankTextBox.TabIndex = 136;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label8.Location = new System.Drawing.Point(17, 75);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(112, 13);
            label8.TabIndex = 138;
            label8.Text = "Maximum import rank:";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.BackColor = System.Drawing.Color.Transparent;
            this.label9.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.Location = new System.Drawing.Point(17, 48);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(111, 13);
            this.label9.TabIndex = 137;
            this.label9.Text = "Maximum import FDR:";
            // 
            // ignoreUnmappedPeptidesCheckBox
            // 
            this.ignoreUnmappedPeptidesCheckBox.AutoSize = true;
            this.ignoreUnmappedPeptidesCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ignoreUnmappedPeptidesCheckBox.Location = new System.Drawing.Point(17, 99);
            this.ignoreUnmappedPeptidesCheckBox.Name = "ignoreUnmappedPeptidesCheckBox";
            this.ignoreUnmappedPeptidesCheckBox.Size = new System.Drawing.Size(164, 17);
            this.ignoreUnmappedPeptidesCheckBox.TabIndex = 140;
            this.ignoreUnmappedPeptidesCheckBox.Text = "Ignore unmapped peptides:   ";
            this.ignoreUnmappedPeptidesCheckBox.UseVisualStyleBackColor = true;
            // 
            // sourceExtensionsTextBox
            // 
            this.sourceExtensionsTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sourceExtensionsTextBox.Location = new System.Drawing.Point(229, 402);
            this.sourceExtensionsTextBox.Name = "sourceExtensionsTextBox";
            this.sourceExtensionsTextBox.Size = new System.Drawing.Size(370, 20);
            this.sourceExtensionsTextBox.TabIndex = 137;
            // 
            // label10
            // 
            this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(133, 405);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(97, 13);
            this.label10.TabIndex = 138;
            this.label10.Text = "Source extensions:";
            // 
            // DefaultSettingsManagerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 427);
            this.Controls.Add(this.sourceExtensionsTextBox);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.importSettingsGroupBox);
            this.Controls.Add(this.qonverterSettingsButton);
            this.Controls.Add(this.psmLevelFilterGroupBox);
            this.Controls.Add(this.proteinLevelFilterGroupBox);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.gbSearchPaths);
            this.MaximumSize = new System.Drawing.Size(2000, 465);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(800, 465);
            this.Name = "DefaultSettingsManagerForm";
            this.Text = "Default Settings Manager";
            this.gbSearchPaths.ResumeLayout(false);
            this.searchPathsTabControl.ResumeLayout(false);
            this.tabFastaFilepaths.ResumeLayout(false);
            this.tabFastaFilepaths.PerformLayout();
            this.tabSourceSearchPaths.ResumeLayout(false);
            this.tabSourceSearchPaths.PerformLayout();
            this.psmLevelFilterGroupBox.ResumeLayout(false);
            this.psmLevelFilterGroupBox.PerformLayout();
            this.proteinLevelFilterGroupBox.ResumeLayout(false);
            this.proteinLevelFilterGroupBox.PerformLayout();
            this.importSettingsGroupBox.ResumeLayout(false);
            this.importSettingsGroupBox.PerformLayout();
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
        private System.Windows.Forms.Label lblPercentSign;
        private System.Windows.Forms.TextBox minSpectraPerMatchTextBox;
        private System.Windows.Forms.ComboBox maxQValueComboBox;
        private System.Windows.Forms.TextBox minSpectraPerPeptideTextBox;
        private System.Windows.Forms.Label lblMaxFdr;
        private System.Windows.Forms.GroupBox proteinLevelFilterGroupBox;
        private System.Windows.Forms.TextBox minSpectraPerProteinTextBox;
        private System.Windows.Forms.Label lblMinSpectraPerProtein;
        private System.Windows.Forms.Label lblParsimonyVariable;
        private System.Windows.Forms.TextBox minAdditionalPeptidesTextBox;
        private System.Windows.Forms.TextBox minDistinctPeptidesTextBox;
        private System.Windows.Forms.TextBox defaultDecoyPrefixTextBox;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button qonverterSettingsButton;
        private System.Windows.Forms.GroupBox importSettingsGroupBox;
        private System.Windows.Forms.CheckBox ignoreUnmappedPeptidesCheckBox;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox maxImportFdrComboBox;
        private System.Windows.Forms.TextBox maxImportRankTextBox;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox sourceExtensionsTextBox;
        private System.Windows.Forms.Label label10;
    }
}