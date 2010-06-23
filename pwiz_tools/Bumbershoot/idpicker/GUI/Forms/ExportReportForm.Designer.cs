//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Mike Litton.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Matt Chambers
//

namespace IdPickerGui
{
    partial class ExportReportForm
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
            this.gbCSVOptions = new System.Windows.Forms.GroupBox();
            this.cbQuantitationMethod = new System.Windows.Forms.ComboBox();
            this.cbPeptideProteinGroupAssociationTableCheckBox = new System.Windows.Forms.CheckBox();
            this.cbSpectrumTable = new System.Windows.Forms.CheckBox();
            this.cbSpectraPerProteinTable = new System.Windows.Forms.CheckBox();
            this.cbSequencesPerProteinTable = new System.Windows.Forms.CheckBox();
            this.cbSpectraPerPeptideTable = new System.Windows.Forms.CheckBox();
            this.cbSummaryTable = new System.Windows.Forms.CheckBox();
            this.gbZipOptions = new System.Windows.Forms.GroupBox();
            this.cbSubsetSpectra = new System.Windows.Forms.CheckBox();
            this.cbSubsetProteinDatabase = new System.Windows.Forms.CheckBox();
            this.cboSourceIncludeMode = new System.Windows.Forms.ComboBox();
            this.lblSourceIncludeMode = new System.Windows.Forms.Label();
            this.lblSourceExtensions = new System.Windows.Forms.Label();
            this.tbSourceExtensions = new System.Windows.Forms.TextBox();
            this.cbSearchFiles = new System.Windows.Forms.CheckBox();
            this.cbReportFiles = new System.Windows.Forms.CheckBox();
            this.cbSourceFiles = new System.Windows.Forms.CheckBox();
            this.cbIncludeDatabase = new System.Windows.Forms.CheckBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlButtons = new System.Windows.Forms.Panel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.cboExportType = new System.Windows.Forms.ComboBox();
            this.lblExportType = new System.Windows.Forms.Label();
            this.lblReportNameDesc = new System.Windows.Forms.Label();
            this.cbOpenExplorer = new System.Windows.Forms.CheckBox();
            this.lblReportNameValue = new System.Windows.Forms.Label();
            this.cbSpectraPerPeptideGroupTable = new System.Windows.Forms.CheckBox();
            this.gbCSVOptions.SuspendLayout();
            this.gbZipOptions.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.pnlButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // gbCSVOptions
            // 
            this.gbCSVOptions.AutoSize = true;
            this.gbCSVOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.gbCSVOptions.Controls.Add(this.cbSpectraPerPeptideGroupTable);
            this.gbCSVOptions.Controls.Add(this.cbQuantitationMethod);
            this.gbCSVOptions.Controls.Add(this.cbPeptideProteinGroupAssociationTableCheckBox);
            this.gbCSVOptions.Controls.Add(this.cbSpectrumTable);
            this.gbCSVOptions.Controls.Add(this.cbSpectraPerProteinTable);
            this.gbCSVOptions.Controls.Add(this.cbSequencesPerProteinTable);
            this.gbCSVOptions.Controls.Add(this.cbSpectraPerPeptideTable);
            this.gbCSVOptions.Controls.Add(this.cbSummaryTable);
            this.gbCSVOptions.Enabled = false;
            this.flowLayoutPanel1.SetFlowBreak(this.gbCSVOptions, true);
            this.gbCSVOptions.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.gbCSVOptions.Location = new System.Drawing.Point(3, 154);
            this.gbCSVOptions.Name = "gbCSVOptions";
            this.gbCSVOptions.Padding = new System.Windows.Forms.Padding(5);
            this.gbCSVOptions.Size = new System.Drawing.Size(258, 201);
            this.gbCSVOptions.TabIndex = 15;
            this.gbCSVOptions.TabStop = false;
            this.gbCSVOptions.Text = "TSV (tables)";
            // 
            // cbQuantitationMethod
            // 
            this.cbQuantitationMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbQuantitationMethod.FormattingEnabled = true;
            this.cbQuantitationMethod.Items.AddRange(new object[] {
            "No quantitation",
            "ITRAQ 4-Plex",
            "ITRAQ 8-Plex"});
            this.cbQuantitationMethod.Location = new System.Drawing.Point(8, 158);
            this.cbQuantitationMethod.Name = "cbQuantitationMethod";
            this.cbQuantitationMethod.Size = new System.Drawing.Size(210, 21);
            this.cbQuantitationMethod.TabIndex = 19;
            // 
            // cbPeptideProteinGroupAssociationTableCheckBox
            // 
            this.cbPeptideProteinGroupAssociationTableCheckBox.AutoSize = true;
            this.cbPeptideProteinGroupAssociationTableCheckBox.Checked = true;
            this.cbPeptideProteinGroupAssociationTableCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbPeptideProteinGroupAssociationTableCheckBox.Location = new System.Drawing.Point(8, 112);
            this.cbPeptideProteinGroupAssociationTableCheckBox.Name = "cbPeptideProteinGroupAssociationTableCheckBox";
            this.cbPeptideProteinGroupAssociationTableCheckBox.Size = new System.Drawing.Size(218, 17);
            this.cbPeptideProteinGroupAssociationTableCheckBox.TabIndex = 18;
            this.cbPeptideProteinGroupAssociationTableCheckBox.Text = "Protein/Peptide Group Association Table";
            this.cbPeptideProteinGroupAssociationTableCheckBox.UseVisualStyleBackColor = true;
            // 
            // cbSpectrumTable
            // 
            this.cbSpectrumTable.AutoSize = true;
            this.cbSpectrumTable.Checked = true;
            this.cbSpectrumTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbSpectrumTable.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbSpectrumTable.Location = new System.Drawing.Point(131, 21);
            this.cbSpectrumTable.Name = "cbSpectrumTable";
            this.cbSpectrumTable.Size = new System.Drawing.Size(100, 17);
            this.cbSpectrumTable.TabIndex = 4;
            this.cbSpectrumTable.Text = "Spectrum Table";
            this.cbSpectrumTable.UseVisualStyleBackColor = true;
            // 
            // cbSpectraPerProteinTable
            // 
            this.cbSpectraPerProteinTable.AutoSize = true;
            this.cbSpectraPerProteinTable.Checked = true;
            this.cbSpectraPerProteinTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbSpectraPerProteinTable.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbSpectraPerProteinTable.Location = new System.Drawing.Point(8, 67);
            this.cbSpectraPerProteinTable.Name = "cbSpectraPerProteinTable";
            this.cbSpectraPerProteinTable.Size = new System.Drawing.Size(208, 17);
            this.cbSpectraPerProteinTable.TabIndex = 3;
            this.cbSpectraPerProteinTable.Text = "Spectra per Protein (by source group)";
            this.cbSpectraPerProteinTable.UseVisualStyleBackColor = true;
            // 
            // cbSequencesPerProteinTable
            // 
            this.cbSequencesPerProteinTable.AutoSize = true;
            this.cbSequencesPerProteinTable.Checked = true;
            this.cbSequencesPerProteinTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbSequencesPerProteinTable.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbSequencesPerProteinTable.Location = new System.Drawing.Point(8, 44);
            this.cbSequencesPerProteinTable.Name = "cbSequencesPerProteinTable";
            this.cbSequencesPerProteinTable.Size = new System.Drawing.Size(223, 17);
            this.cbSequencesPerProteinTable.TabIndex = 2;
            this.cbSequencesPerProteinTable.Text = "Sequences per Protein (by source group)";
            this.cbSequencesPerProteinTable.UseVisualStyleBackColor = true;
            // 
            // cbSpectraPerPeptideTable
            // 
            this.cbSpectraPerPeptideTable.AutoSize = true;
            this.cbSpectraPerPeptideTable.Checked = true;
            this.cbSpectraPerPeptideTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbSpectraPerPeptideTable.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbSpectraPerPeptideTable.Location = new System.Drawing.Point(8, 90);
            this.cbSpectraPerPeptideTable.Name = "cbSpectraPerPeptideTable";
            this.cbSpectraPerPeptideTable.Size = new System.Drawing.Size(210, 17);
            this.cbSpectraPerPeptideTable.TabIndex = 1;
            this.cbSpectraPerPeptideTable.Text = "Spectra per Peptide (by source group)";
            this.cbSpectraPerPeptideTable.UseVisualStyleBackColor = true;
            // 
            // cbSummaryTable
            // 
            this.cbSummaryTable.AutoSize = true;
            this.cbSummaryTable.Checked = true;
            this.cbSummaryTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbSummaryTable.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbSummaryTable.Location = new System.Drawing.Point(8, 21);
            this.cbSummaryTable.Name = "cbSummaryTable";
            this.cbSummaryTable.Size = new System.Drawing.Size(107, 17);
            this.cbSummaryTable.TabIndex = 0;
            this.cbSummaryTable.Text = "Overall Summary";
            this.cbSummaryTable.UseVisualStyleBackColor = true;
            // 
            // gbZipOptions
            // 
            this.gbZipOptions.Controls.Add(this.cbSubsetSpectra);
            this.gbZipOptions.Controls.Add(this.cbSubsetProteinDatabase);
            this.gbZipOptions.Controls.Add(this.cboSourceIncludeMode);
            this.gbZipOptions.Controls.Add(this.lblSourceIncludeMode);
            this.gbZipOptions.Controls.Add(this.lblSourceExtensions);
            this.gbZipOptions.Controls.Add(this.tbSourceExtensions);
            this.gbZipOptions.Controls.Add(this.cbSearchFiles);
            this.gbZipOptions.Controls.Add(this.cbReportFiles);
            this.gbZipOptions.Controls.Add(this.cbSourceFiles);
            this.gbZipOptions.Controls.Add(this.cbIncludeDatabase);
            this.gbZipOptions.Enabled = false;
            this.flowLayoutPanel1.SetFlowBreak(this.gbZipOptions, true);
            this.gbZipOptions.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.gbZipOptions.Location = new System.Drawing.Point(3, 3);
            this.gbZipOptions.Name = "gbZipOptions";
            this.gbZipOptions.Padding = new System.Windows.Forms.Padding(5);
            this.gbZipOptions.Size = new System.Drawing.Size(258, 145);
            this.gbZipOptions.TabIndex = 14;
            this.gbZipOptions.TabStop = false;
            this.gbZipOptions.Text = "ZIP (entire report)";
            // 
            // cbSubsetSpectra
            // 
            this.cbSubsetSpectra.AutoSize = true;
            this.cbSubsetSpectra.Enabled = false;
            this.cbSubsetSpectra.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbSubsetSpectra.Location = new System.Drawing.Point(8, 68);
            this.cbSubsetSpectra.Name = "cbSubsetSpectra";
            this.cbSubsetSpectra.Size = new System.Drawing.Size(88, 17);
            this.cbSubsetSpectra.TabIndex = 10;
            this.cbSubsetSpectra.Text = "Subset mzML";
            this.cbSubsetSpectra.UseVisualStyleBackColor = true;
            this.cbSubsetSpectra.CheckedChanged += new System.EventHandler(this.checkStateChanged);
            // 
            // cbSubsetProteinDatabase
            // 
            this.cbSubsetProteinDatabase.AutoSize = true;
            this.cbSubsetProteinDatabase.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbSubsetProteinDatabase.Location = new System.Drawing.Point(117, 67);
            this.cbSubsetProteinDatabase.Name = "cbSubsetProteinDatabase";
            this.cbSubsetProteinDatabase.Size = new System.Drawing.Size(94, 17);
            this.cbSubsetProteinDatabase.TabIndex = 9;
            this.cbSubsetProteinDatabase.Text = "Subset FASTA";
            this.cbSubsetProteinDatabase.UseVisualStyleBackColor = true;
            this.cbSubsetProteinDatabase.CheckedChanged += new System.EventHandler(this.checkStateChanged);
            // 
            // cboSourceIncludeMode
            // 
            this.cboSourceIncludeMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboSourceIncludeMode.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cboSourceIncludeMode.FormattingEnabled = true;
            this.cboSourceIncludeMode.Items.AddRange(new object[] {
            "all matching files",
            "first matching file"});
            this.cboSourceIncludeMode.Location = new System.Drawing.Point(110, 118);
            this.cboSourceIncludeMode.Name = "cboSourceIncludeMode";
            this.cboSourceIncludeMode.Size = new System.Drawing.Size(140, 21);
            this.cboSourceIncludeMode.TabIndex = 8;
            // 
            // lblSourceIncludeMode
            // 
            this.lblSourceIncludeMode.AutoSize = true;
            this.lblSourceIncludeMode.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.lblSourceIncludeMode.Location = new System.Drawing.Point(61, 123);
            this.lblSourceIncludeMode.Name = "lblSourceIncludeMode";
            this.lblSourceIncludeMode.Size = new System.Drawing.Size(46, 13);
            this.lblSourceIncludeMode.TabIndex = 7;
            this.lblSourceIncludeMode.Text = "Include:";
            // 
            // lblSourceExtensions
            // 
            this.lblSourceExtensions.AutoSize = true;
            this.lblSourceExtensions.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.lblSourceExtensions.Location = new System.Drawing.Point(9, 97);
            this.lblSourceExtensions.Name = "lblSourceExtensions";
            this.lblSourceExtensions.Size = new System.Drawing.Size(99, 13);
            this.lblSourceExtensions.TabIndex = 6;
            this.lblSourceExtensions.Text = "Source Extensions:";
            // 
            // tbSourceExtensions
            // 
            this.tbSourceExtensions.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.tbSourceExtensions.Location = new System.Drawing.Point(110, 92);
            this.tbSourceExtensions.Name = "tbSourceExtensions";
            this.tbSourceExtensions.Size = new System.Drawing.Size(140, 21);
            this.tbSourceExtensions.TabIndex = 5;
            // 
            // cbSearchFiles
            // 
            this.cbSearchFiles.AutoSize = true;
            this.cbSearchFiles.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbSearchFiles.Location = new System.Drawing.Point(117, 21);
            this.cbSearchFiles.Name = "cbSearchFiles";
            this.cbSearchFiles.Size = new System.Drawing.Size(83, 17);
            this.cbSearchFiles.TabIndex = 2;
            this.cbSearchFiles.Text = "Search Files";
            this.cbSearchFiles.UseVisualStyleBackColor = true;
            this.cbSearchFiles.CheckStateChanged += new System.EventHandler(this.cbSearchFiles_CheckStateChanged);
            this.cbSearchFiles.CheckedChanged += new System.EventHandler(this.checkStateChanged);
            // 
            // cbReportFiles
            // 
            this.cbReportFiles.AutoSize = true;
            this.cbReportFiles.Checked = true;
            this.cbReportFiles.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbReportFiles.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbReportFiles.Location = new System.Drawing.Point(8, 21);
            this.cbReportFiles.Name = "cbReportFiles";
            this.cbReportFiles.Size = new System.Drawing.Size(83, 17);
            this.cbReportFiles.TabIndex = 0;
            this.cbReportFiles.Text = "Report Files";
            this.cbReportFiles.UseVisualStyleBackColor = true;
            this.cbReportFiles.CheckStateChanged += new System.EventHandler(this.cbEntireReport_CheckStateChanged);
            this.cbReportFiles.CheckedChanged += new System.EventHandler(this.checkStateChanged);
            // 
            // cbSourceFiles
            // 
            this.cbSourceFiles.AutoSize = true;
            this.cbSourceFiles.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbSourceFiles.Location = new System.Drawing.Point(8, 45);
            this.cbSourceFiles.Name = "cbSourceFiles";
            this.cbSourceFiles.Size = new System.Drawing.Size(83, 17);
            this.cbSourceFiles.TabIndex = 1;
            this.cbSourceFiles.Text = "Source Files";
            this.cbSourceFiles.UseVisualStyleBackColor = true;
            this.cbSourceFiles.CheckStateChanged += new System.EventHandler(this.cbSourceFiles_CheckStateChanged);
            this.cbSourceFiles.CheckedChanged += new System.EventHandler(this.checkStateChanged);
            // 
            // cbIncludeDatabase
            // 
            this.cbIncludeDatabase.AutoSize = true;
            this.cbIncludeDatabase.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbIncludeDatabase.Location = new System.Drawing.Point(117, 44);
            this.cbIncludeDatabase.Name = "cbIncludeDatabase";
            this.cbIncludeDatabase.Size = new System.Drawing.Size(109, 17);
            this.cbIncludeDatabase.TabIndex = 3;
            this.cbIncludeDatabase.Text = "Protein Database";
            this.cbIncludeDatabase.UseVisualStyleBackColor = true;
            this.cbIncludeDatabase.CheckedChanged += new System.EventHandler(this.checkStateChanged);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.AutoSize = true;
            this.flowLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanel1.Controls.Add(this.gbZipOptions);
            this.flowLayoutPanel1.Controls.Add(this.gbCSVOptions);
            this.flowLayoutPanel1.Controls.Add(this.pnlButtons);
            this.flowLayoutPanel1.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.flowLayoutPanel1.Location = new System.Drawing.Point(12, 95);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(10);
            this.flowLayoutPanel1.MaximumSize = new System.Drawing.Size(300, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(264, 398);
            this.flowLayoutPanel1.TabIndex = 17;
            // 
            // pnlButtons
            // 
            this.pnlButtons.Controls.Add(this.btnCancel);
            this.pnlButtons.Controls.Add(this.btnExport);
            this.pnlButtons.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.pnlButtons.Location = new System.Drawing.Point(3, 361);
            this.pnlButtons.Name = "pnlButtons";
            this.pnlButtons.Size = new System.Drawing.Size(258, 34);
            this.pnlButtons.TabIndex = 16;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.btnCancel.Location = new System.Drawing.Point(132, 6);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnExport
            // 
            this.btnExport.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnExport.Enabled = false;
            this.btnExport.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.btnExport.Location = new System.Drawing.Point(51, 6);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(75, 23);
            this.btnExport.TabIndex = 0;
            this.btnExport.Text = "Export";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // cboExportType
            // 
            this.cboExportType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboExportType.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cboExportType.FormattingEnabled = true;
            this.cboExportType.Items.AddRange(new object[] {
            "ZIP",
            "TSV",
            "XML"});
            this.cboExportType.Location = new System.Drawing.Point(88, 38);
            this.cboExportType.Name = "cboExportType";
            this.cboExportType.Size = new System.Drawing.Size(70, 21);
            this.cboExportType.TabIndex = 9;
            this.cboExportType.SelectedIndexChanged += new System.EventHandler(this.cboExportType_SelectedIndexChanged);
            // 
            // lblExportType
            // 
            this.lblExportType.AutoSize = true;
            this.lblExportType.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.lblExportType.Location = new System.Drawing.Point(9, 41);
            this.lblExportType.Name = "lblExportType";
            this.lblExportType.Size = new System.Drawing.Size(70, 13);
            this.lblExportType.TabIndex = 12;
            this.lblExportType.Text = "Export Type:";
            // 
            // lblReportNameDesc
            // 
            this.lblReportNameDesc.AutoSize = true;
            this.lblReportNameDesc.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.lblReportNameDesc.Location = new System.Drawing.Point(9, 15);
            this.lblReportNameDesc.Name = "lblReportNameDesc";
            this.lblReportNameDesc.Size = new System.Drawing.Size(74, 13);
            this.lblReportNameDesc.TabIndex = 8;
            this.lblReportNameDesc.Text = "Report Name:";
            // 
            // cbOpenExplorer
            // 
            this.cbOpenExplorer.AutoSize = true;
            this.cbOpenExplorer.Checked = true;
            this.cbOpenExplorer.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbOpenExplorer.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.cbOpenExplorer.Location = new System.Drawing.Point(15, 67);
            this.cbOpenExplorer.Name = "cbOpenExplorer";
            this.cbOpenExplorer.Size = new System.Drawing.Size(202, 17);
            this.cbOpenExplorer.TabIndex = 16;
            this.cbOpenExplorer.Text = "Show export directory when finished";
            this.cbOpenExplorer.UseVisualStyleBackColor = true;
            // 
            // lblReportNameValue
            // 
            this.lblReportNameValue.AutoSize = true;
            this.lblReportNameValue.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.lblReportNameValue.Location = new System.Drawing.Point(88, 15);
            this.lblReportNameValue.Name = "lblReportNameValue";
            this.lblReportNameValue.Size = new System.Drawing.Size(35, 13);
            this.lblReportNameValue.TabIndex = 13;
            this.lblReportNameValue.Text = "label1";
            // 
            // cbSpectraPerPeptideGroupTable
            // 
            this.cbSpectraPerPeptideGroupTable.AutoSize = true;
            this.cbSpectraPerPeptideGroupTable.Checked = true;
            this.cbSpectraPerPeptideGroupTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbSpectraPerPeptideGroupTable.Location = new System.Drawing.Point(8, 135);
            this.cbSpectraPerPeptideGroupTable.Name = "cbSpectraPerPeptideGroupTable";
            this.cbSpectraPerPeptideGroupTable.Size = new System.Drawing.Size(242, 17);
            this.cbSpectraPerPeptideGroupTable.TabIndex = 20;
            this.cbSpectraPerPeptideGroupTable.Text = "Spectra per Peptide Group (by source group)";
            this.cbSpectraPerPeptideGroupTable.UseVisualStyleBackColor = true;
            // 
            // ExportReportForm
            // 
            this.AccessibleRole = System.Windows.Forms.AccessibleRole.None;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(286, 492);
            this.Controls.Add(this.cbOpenExplorer);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.lblReportNameValue);
            this.Controls.Add(this.lblReportNameDesc);
            this.Controls.Add(this.lblExportType);
            this.Controls.Add(this.cboExportType);
            this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportReportForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export";
            this.gbCSVOptions.ResumeLayout(false);
            this.gbCSVOptions.PerformLayout();
            this.gbZipOptions.ResumeLayout(false);
            this.gbZipOptions.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.pnlButtons.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox gbCSVOptions;
        private System.Windows.Forms.CheckBox cbSpectraPerProteinTable;
        private System.Windows.Forms.CheckBox cbSequencesPerProteinTable;
        private System.Windows.Forms.CheckBox cbSpectraPerPeptideTable;
        private System.Windows.Forms.CheckBox cbSummaryTable;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.GroupBox gbZipOptions;
        private System.Windows.Forms.ComboBox cboSourceIncludeMode;
        private System.Windows.Forms.Label lblSourceIncludeMode;
        private System.Windows.Forms.Label lblSourceExtensions;
        private System.Windows.Forms.TextBox tbSourceExtensions;
        private System.Windows.Forms.CheckBox cbSearchFiles;
        private System.Windows.Forms.CheckBox cbReportFiles;
        private System.Windows.Forms.CheckBox cbSourceFiles;
        private System.Windows.Forms.CheckBox cbIncludeDatabase;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ComboBox cboExportType;
        private System.Windows.Forms.Label lblExportType;
        private System.Windows.Forms.Label lblReportNameDesc;
        private System.Windows.Forms.CheckBox cbOpenExplorer;
        private System.Windows.Forms.Label lblReportNameValue;
        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.CheckBox cbSpectrumTable;
        private System.Windows.Forms.CheckBox cbPeptideProteinGroupAssociationTableCheckBox;
        private System.Windows.Forms.CheckBox cbSubsetSpectra;
        private System.Windows.Forms.CheckBox cbSubsetProteinDatabase;
        private System.Windows.Forms.ComboBox cbQuantitationMethod;
        private System.Windows.Forms.CheckBox cbSpectraPerPeptideGroupTable;
    }
}
