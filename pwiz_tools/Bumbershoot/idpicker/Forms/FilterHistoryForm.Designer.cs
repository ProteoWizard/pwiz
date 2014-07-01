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
    partial class FilterHistoryForm
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FilterHistoryForm));
            this.dataGridView = new IDPicker.Controls.PreviewDataGridView();
            this.exportMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.clipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.copyToClipboardSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedCellsToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelSelectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.exportButton = new System.Windows.Forms.ToolStripButton();
            this.displayOptionsButton = new System.Windows.Forms.ToolStripButton();
            this.idColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.maxQValueColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.minPeptidesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.minSpectraColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.minAdditionalPeptidesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.geneLevelFilteringColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.distinctMatchFormatColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.minSpectraPerMatchColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.minSpectraPerPeptideColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.maxProteinGroupsColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clustersColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.proteinGroupsColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.proteinsColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.distinctPeptidesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.distinctMatchesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.filteredSpectraColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.proteinFdrColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.peptideFdrColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.spectrumFdrColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.exportMenu.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AllowUserToOrderColumns = true;
            this.dataGridView.AllowUserToResizeRows = false;
            this.dataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridView.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.idColumn,
            this.maxQValueColumn,
            this.minPeptidesColumn,
            this.minSpectraColumn,
            this.minAdditionalPeptidesColumn,
            this.geneLevelFilteringColumn,
            this.distinctMatchFormatColumn,
            this.minSpectraPerMatchColumn,
            this.minSpectraPerPeptideColumn,
            this.maxProteinGroupsColumn,
            this.clustersColumn,
            this.proteinGroupsColumn,
            this.proteinsColumn,
            this.distinctPeptidesColumn,
            this.distinctMatchesColumn,
            this.filteredSpectraColumn,
            this.proteinFdrColumn,
            this.peptideFdrColumn,
            this.spectrumFdrColumn});
            this.dataGridView.GridColor = System.Drawing.SystemColors.Window;
            this.dataGridView.Location = new System.Drawing.Point(0, 25);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.RowHeadersVisible = false;
            this.dataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridView.Size = new System.Drawing.Size(1181, 199);
            this.dataGridView.TabIndex = 0;
            // 
            // exportMenu
            // 
            this.exportMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clipboardToolStripMenuItem,
            this.fileToolStripMenuItem,
            this.showInExcelToolStripMenuItem,
            this.toolStripSeparator1,
            this.copyToClipboardSelectedToolStripMenuItem,
            this.exportSelectedCellsToFileToolStripMenuItem,
            this.showInExcelSelectToolStripMenuItem});
            this.exportMenu.Name = "contextMenuStrip1";
            this.exportMenu.Size = new System.Drawing.Size(247, 142);
            // 
            // clipboardToolStripMenuItem
            // 
            this.clipboardToolStripMenuItem.Name = "clipboardToolStripMenuItem";
            this.clipboardToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.clipboardToolStripMenuItem.Text = "Copy to Clipboard";
            this.clipboardToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.fileToolStripMenuItem.Text = "Export to File";
            this.fileToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // showInExcelToolStripMenuItem
            // 
            this.showInExcelToolStripMenuItem.Name = "showInExcelToolStripMenuItem";
            this.showInExcelToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.showInExcelToolStripMenuItem.Text = "Show in Excel";
            this.showInExcelToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(243, 6);
            // 
            // copyToClipboardSelectedToolStripMenuItem
            // 
            this.copyToClipboardSelectedToolStripMenuItem.Name = "copyToClipboardSelectedToolStripMenuItem";
            this.copyToClipboardSelectedToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.copyToClipboardSelectedToolStripMenuItem.Text = "Copy Selected Cells to Clipboard";
            this.copyToClipboardSelectedToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // exportSelectedCellsToFileToolStripMenuItem
            // 
            this.exportSelectedCellsToFileToolStripMenuItem.Name = "exportSelectedCellsToFileToolStripMenuItem";
            this.exportSelectedCellsToFileToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.exportSelectedCellsToFileToolStripMenuItem.Text = "Export Selected Cells to File";
            this.exportSelectedCellsToFileToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // showInExcelSelectToolStripMenuItem
            // 
            this.showInExcelSelectToolStripMenuItem.Name = "showInExcelSelectToolStripMenuItem";
            this.showInExcelSelectToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.showInExcelSelectToolStripMenuItem.Text = "Show Selected Cells in Excel";
            this.showInExcelSelectToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // toolStrip
            // 
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportButton,
            this.displayOptionsButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip.Size = new System.Drawing.Size(1181, 25);
            this.toolStrip.TabIndex = 16;
            this.toolStrip.Text = "Tools";
            // 
            // exportButton
            // 
            this.exportButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.exportButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(23, 22);
            this.exportButton.Text = "Export Options";
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // displayOptionsButton
            // 
            this.displayOptionsButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.displayOptionsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.displayOptionsButton.Image = ((System.Drawing.Image)(resources.GetObject("displayOptionsButton.Image")));
            this.displayOptionsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.displayOptionsButton.Name = "displayOptionsButton";
            this.displayOptionsButton.Size = new System.Drawing.Size(94, 22);
            this.displayOptionsButton.Text = "Display Options";
            this.displayOptionsButton.Click += new System.EventHandler(this.displayOptionsButton_Click);
            // 
            // idColumn
            // 
            this.idColumn.HeaderText = "Id";
            this.idColumn.Name = "idColumn";
            this.idColumn.ReadOnly = true;
            this.idColumn.Visible = false;
            // 
            // maxQValueColumn
            // 
            this.maxQValueColumn.HeaderText = "Max. Q Value";
            this.maxQValueColumn.Name = "maxQValueColumn";
            this.maxQValueColumn.ReadOnly = true;
            // 
            // minPeptidesColumn
            // 
            this.minPeptidesColumn.HeaderText = "Min. Peptides Per Protein/Gene";
            this.minPeptidesColumn.Name = "minPeptidesColumn";
            this.minPeptidesColumn.ReadOnly = true;
            this.minPeptidesColumn.Width = 95;
            // 
            // minSpectraColumn
            // 
            this.minSpectraColumn.HeaderText = "Min. Spectra Per Protein/Gene";
            this.minSpectraColumn.Name = "minSpectraColumn";
            this.minSpectraColumn.ReadOnly = true;
            this.minSpectraColumn.Width = 95;
            // 
            // minAdditionalPeptidesColumn
            // 
            this.minAdditionalPeptidesColumn.HeaderText = "Min. Additional Peptides";
            this.minAdditionalPeptidesColumn.Name = "minAdditionalPeptidesColumn";
            this.minAdditionalPeptidesColumn.ReadOnly = true;
            this.minAdditionalPeptidesColumn.Width = 105;
            // 
            // geneLevelFilteringColumn
            // 
            this.geneLevelFilteringColumn.HeaderText = "Filtering by Gene";
            this.geneLevelFilteringColumn.Name = "geneLevelFilteringColumn";
            this.geneLevelFilteringColumn.ReadOnly = true;
            // 
            // distinctMatchFormatColumn
            // 
            this.distinctMatchFormatColumn.HeaderText = "Distinct Match Format";
            this.distinctMatchFormatColumn.Name = "distinctMatchFormatColumn";
            this.distinctMatchFormatColumn.ReadOnly = true;
            // 
            // minSpectraPerMatchColumn
            // 
            this.minSpectraPerMatchColumn.HeaderText = "Min. Spectra Per Match";
            this.minSpectraPerMatchColumn.Name = "minSpectraPerMatchColumn";
            this.minSpectraPerMatchColumn.ReadOnly = true;
            // 
            // minSpectraPerPeptideColumn
            // 
            this.minSpectraPerPeptideColumn.HeaderText = "Min. Spectra Per Peptide";
            this.minSpectraPerPeptideColumn.Name = "minSpectraPerPeptideColumn";
            this.minSpectraPerPeptideColumn.ReadOnly = true;
            // 
            // maxProteinGroupsColumn
            // 
            this.maxProteinGroupsColumn.HeaderText = "Max. Protein Groups Per Peptide";
            this.maxProteinGroupsColumn.Name = "maxProteinGroupsColumn";
            this.maxProteinGroupsColumn.ReadOnly = true;
            this.maxProteinGroupsColumn.Width = 135;
            // 
            // clustersColumn
            // 
            this.clustersColumn.HeaderText = "Clusters";
            this.clustersColumn.Name = "clustersColumn";
            this.clustersColumn.ReadOnly = true;
            this.clustersColumn.Width = 80;
            // 
            // proteinGroupsColumn
            // 
            this.proteinGroupsColumn.HeaderText = "Protein Groups";
            this.proteinGroupsColumn.Name = "proteinGroupsColumn";
            this.proteinGroupsColumn.ReadOnly = true;
            this.proteinGroupsColumn.Width = 105;
            // 
            // proteinsColumn
            // 
            this.proteinsColumn.HeaderText = "Proteins";
            this.proteinsColumn.Name = "proteinsColumn";
            this.proteinsColumn.ReadOnly = true;
            this.proteinsColumn.Width = 80;
            // 
            // distinctPeptidesColumn
            // 
            this.distinctPeptidesColumn.HeaderText = "Distinct Peptides";
            this.distinctPeptidesColumn.Name = "distinctPeptidesColumn";
            this.distinctPeptidesColumn.ReadOnly = true;
            this.distinctPeptidesColumn.Width = 80;
            // 
            // distinctMatchesColumn
            // 
            this.distinctMatchesColumn.HeaderText = "Distinct Matches";
            this.distinctMatchesColumn.Name = "distinctMatchesColumn";
            this.distinctMatchesColumn.ReadOnly = true;
            this.distinctMatchesColumn.Width = 80;
            // 
            // filteredSpectraColumn
            // 
            this.filteredSpectraColumn.HeaderText = "Filtered Spectra";
            this.filteredSpectraColumn.Name = "filteredSpectraColumn";
            this.filteredSpectraColumn.ReadOnly = true;
            this.filteredSpectraColumn.Width = 80;
            // 
            // proteinFdrColumn
            // 
            this.proteinFdrColumn.HeaderText = "Protein FDR";
            this.proteinFdrColumn.Name = "proteinFdrColumn";
            this.proteinFdrColumn.ReadOnly = true;
            // 
            // peptideFdrColumn
            // 
            this.peptideFdrColumn.HeaderText = "Peptide FDR";
            this.peptideFdrColumn.Name = "peptideFdrColumn";
            this.peptideFdrColumn.ReadOnly = true;
            this.peptideFdrColumn.Visible = false;
            // 
            // spectrumFdrColumn
            // 
            this.spectrumFdrColumn.HeaderText = "Spectrum FDR";
            this.spectrumFdrColumn.Name = "spectrumFdrColumn";
            this.spectrumFdrColumn.ReadOnly = true;
            this.spectrumFdrColumn.Visible = false;
            // 
            // FilterHistoryForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1181, 224);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.dataGridView);
            this.Name = "FilterHistoryForm";
            this.TabText = "FilterHistoryForm";
            this.Text = "FilterHistoryForm";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.exportMenu.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private IDPicker.Controls.PreviewDataGridView dataGridView;
        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private System.Windows.Forms.ToolStripMenuItem clipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem copyToClipboardSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedCellsToFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelSelectToolStripMenuItem;
        protected System.Windows.Forms.ToolStrip toolStrip;
        protected System.Windows.Forms.ToolStripButton exportButton;
        protected System.Windows.Forms.ToolStripButton displayOptionsButton;
        private System.Windows.Forms.DataGridViewTextBoxColumn idColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn maxQValueColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn minPeptidesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn minSpectraColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn minAdditionalPeptidesColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn geneLevelFilteringColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctMatchFormatColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn minSpectraPerMatchColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn minSpectraPerPeptideColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn maxProteinGroupsColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn clustersColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn proteinGroupsColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn proteinsColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctPeptidesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctMatchesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn filteredSpectraColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn proteinFdrColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn peptideFdrColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn spectrumFdrColumn;
    }
}