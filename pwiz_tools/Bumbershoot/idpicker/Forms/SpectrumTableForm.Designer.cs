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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

namespace IDPicker.Forms
{
    partial class SpectrumTableForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent ()
        {
            this.components = new System.ComponentModel.Container();
            this.treeListView = new BrightIdeasSoftware.TreeListView();
            this.keyColumn = new BrightIdeasSoftware.OLVColumn();
            this.totalSpectraColumn = new BrightIdeasSoftware.OLVColumn();
            this.spectraColumn = new BrightIdeasSoftware.OLVColumn();
            this.distinctPeptidesColumn = new BrightIdeasSoftware.OLVColumn();
            this.distinctMatchesColumn = new BrightIdeasSoftware.OLVColumn();
            this.distinctAnalysesColumn = new BrightIdeasSoftware.OLVColumn();
            this.distinctChargesColumn = new BrightIdeasSoftware.OLVColumn();
            this.precursorMzColumn = new BrightIdeasSoftware.OLVColumn();
            this.analysisColumn = new BrightIdeasSoftware.OLVColumn();
            this.chargeColumn = new BrightIdeasSoftware.OLVColumn();
            this.observedMassColumn = new BrightIdeasSoftware.OLVColumn();
            this.exactMassColumn = new BrightIdeasSoftware.OLVColumn();
            this.massErrorColumn = new BrightIdeasSoftware.OLVColumn();
            this.qvalueColumn = new BrightIdeasSoftware.OLVColumn();
            this.sequenceColumn = new BrightIdeasSoftware.OLVColumn();
            this.editGroupsButton = new System.Windows.Forms.Button();
            this.exportMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.clipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSourcesInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupingSetupButton = new System.Windows.Forms.Button();
            this.displayOptionsButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.treeListView)).BeginInit();
            this.exportMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // treeListView
            // 
            this.treeListView.AllColumns.Add(this.keyColumn);
            this.treeListView.AllColumns.Add(this.totalSpectraColumn);
            this.treeListView.AllColumns.Add(this.spectraColumn);
            this.treeListView.AllColumns.Add(this.distinctPeptidesColumn);
            this.treeListView.AllColumns.Add(this.distinctMatchesColumn);
            this.treeListView.AllColumns.Add(this.distinctAnalysesColumn);
            this.treeListView.AllColumns.Add(this.distinctChargesColumn);
            this.treeListView.AllColumns.Add(this.precursorMzColumn);
            this.treeListView.AllColumns.Add(this.analysisColumn);
            this.treeListView.AllColumns.Add(this.chargeColumn);
            this.treeListView.AllColumns.Add(this.observedMassColumn);
            this.treeListView.AllColumns.Add(this.exactMassColumn);
            this.treeListView.AllColumns.Add(this.massErrorColumn);
            this.treeListView.AllColumns.Add(this.qvalueColumn);
            this.treeListView.AllColumns.Add(this.sequenceColumn);
            this.treeListView.AllowColumnReorder = true;
            this.treeListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.treeListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.keyColumn,
            this.spectraColumn,
            this.distinctPeptidesColumn,
            this.distinctMatchesColumn,
            this.distinctAnalysesColumn,
            this.distinctChargesColumn,
            this.precursorMzColumn,
            this.analysisColumn,
            this.chargeColumn,
            this.observedMassColumn,
            this.exactMassColumn,
            this.massErrorColumn,
            this.qvalueColumn,
            this.sequenceColumn});
            this.treeListView.Cursor = System.Windows.Forms.Cursors.Default;
            this.treeListView.FullRowSelect = true;
            this.treeListView.HideSelection = false;
            this.treeListView.Location = new System.Drawing.Point(-1, 27);
            this.treeListView.Name = "treeListView";
            this.treeListView.OwnerDraw = true;
            this.treeListView.ShowCommandMenuOnRightClick = true;
            this.treeListView.ShowGroups = false;
            this.treeListView.Size = new System.Drawing.Size(1029, 326);
            this.treeListView.TabIndex = 0;
            this.treeListView.UnfocusedHighlightBackgroundColor = System.Drawing.SystemColors.Highlight;
            this.treeListView.UnfocusedHighlightForegroundColor = System.Drawing.SystemColors.HighlightText;
            this.treeListView.UseCompatibleStateImageBehavior = false;
            this.treeListView.View = System.Windows.Forms.View.Details;
            this.treeListView.VirtualMode = true;
            // 
            // keyColumn
            // 
            this.keyColumn.MinimumWidth = 100;
            this.keyColumn.Text = "Key";
            this.keyColumn.Width = 100;
            // 
            // totalSpectraColumn
            // 
            this.totalSpectraColumn.AspectName = "";
            this.totalSpectraColumn.DisplayIndex = 1;
            this.totalSpectraColumn.IsVisible = false;
            this.totalSpectraColumn.Text = "Total Spectra";
            this.totalSpectraColumn.Width = 80;
            // 
            // spectraColumn
            // 
            this.spectraColumn.Text = "Filtered Spectra";
            this.spectraColumn.Width = 90;
            // 
            // distinctPeptidesColumn
            // 
            this.distinctPeptidesColumn.Text = "Distinct Peptides";
            this.distinctPeptidesColumn.Width = 94;
            // 
            // distinctMatchesColumn
            // 
            this.distinctMatchesColumn.Text = "Distinct Matches";
            this.distinctMatchesColumn.Width = 91;
            // 
            // distinctAnalysesColumn
            // 
            this.distinctAnalysesColumn.Text = "Distinct Analyses";
            this.distinctAnalysesColumn.Width = 95;
            // 
            // distinctChargesColumn
            // 
            this.distinctChargesColumn.Text = "Distinct Charges";
            this.distinctChargesColumn.Width = 90;
            // 
            // precursorMzColumn
            // 
            this.precursorMzColumn.Text = "Precursor m/z";
            this.precursorMzColumn.Width = 80;
            // 
            // analysisColumn
            // 
            this.analysisColumn.Text = "Analysis";
            // 
            // chargeColumn
            // 
            this.chargeColumn.Text = "Charge";
            this.chargeColumn.Width = 50;
            // 
            // observedMassColumn
            // 
            this.observedMassColumn.Text = "Observed Mass";
            this.observedMassColumn.Width = 90;
            // 
            // exactMassColumn
            // 
            this.exactMassColumn.Text = "Exact Mass";
            this.exactMassColumn.Width = 70;
            // 
            // massErrorColumn
            // 
            this.massErrorColumn.Text = "Mass Error";
            this.massErrorColumn.Width = 65;
            // 
            // qvalueColumn
            // 
            this.qvalueColumn.Text = "Q Value";
            this.qvalueColumn.Width = 55;
            // 
            // sequenceColumn
            // 
            this.sequenceColumn.FillsFreeSpace = true;
            this.sequenceColumn.Text = "Sequence";
            this.sequenceColumn.Width = 70;
            // 
            // editGroupsButton
            // 
            this.editGroupsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.editGroupsButton.Location = new System.Drawing.Point(779, 2);
            this.editGroupsButton.Name = "editGroupsButton";
            this.editGroupsButton.Size = new System.Drawing.Size(100, 23);
            this.editGroupsButton.TabIndex = 1;
            this.editGroupsButton.Text = "Source Grouping";
            this.editGroupsButton.UseVisualStyleBackColor = true;
            this.editGroupsButton.Click += new System.EventHandler(this.editGroupsButton_Click);
            // 
            // exportMenu
            // 
            this.exportMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clipboardToolStripMenuItem,
            this.fileToolStripMenuItem,
            this.showInExcelToolStripMenuItem,
            this.showSourcesInExcelToolStripMenuItem});
            this.exportMenu.Name = "contextMenuStrip1";
            this.exportMenu.Size = new System.Drawing.Size(192, 92);
            // 
            // clipboardToolStripMenuItem
            // 
            this.clipboardToolStripMenuItem.Name = "clipboardToolStripMenuItem";
            this.clipboardToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.clipboardToolStripMenuItem.Text = "Copy to Clipboard";
            this.clipboardToolStripMenuItem.Click += new System.EventHandler(this.clipboardToolStripMenuItem_Click);
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.fileToolStripMenuItem.Text = "Export to File";
            this.fileToolStripMenuItem.Click += new System.EventHandler(this.fileToolStripMenuItem_Click);
            // 
            // showInExcelToolStripMenuItem
            // 
            this.showInExcelToolStripMenuItem.Name = "showInExcelToolStripMenuItem";
            this.showInExcelToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.showInExcelToolStripMenuItem.Text = "Show Current in Excel";
            this.showInExcelToolStripMenuItem.Click += new System.EventHandler(this.showCurrentInExcelToolStripMenuItem_Click);
            // 
            // showSourcesInExcelToolStripMenuItem
            // 
            this.showSourcesInExcelToolStripMenuItem.Name = "showSourcesInExcelToolStripMenuItem";
            this.showSourcesInExcelToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.showSourcesInExcelToolStripMenuItem.Text = "Show Sources in Excel";
            this.showSourcesInExcelToolStripMenuItem.Click += new System.EventHandler(this.showSourcesInExcelToolStripMenuItem_Click);
            // 
            // groupingSetupButton
            // 
            this.groupingSetupButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.groupingSetupButton.Location = new System.Drawing.Point(673, 2);
            this.groupingSetupButton.Name = "groupingSetupButton";
            this.groupingSetupButton.Size = new System.Drawing.Size(100, 23);
            this.groupingSetupButton.TabIndex = 8;
            this.groupingSetupButton.Text = "Tree Grouping";
            this.groupingSetupButton.UseVisualStyleBackColor = true;
            this.groupingSetupButton.Click += new System.EventHandler(this.groupingSetupButton_Click);
            // 
            // displayOptionsButton
            // 
            this.displayOptionsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.displayOptionsButton.Location = new System.Drawing.Point(885, 2);
            this.displayOptionsButton.Name = "displayOptionsButton";
            this.displayOptionsButton.Size = new System.Drawing.Size(96, 23);
            this.displayOptionsButton.TabIndex = 3;
            this.displayOptionsButton.Text = "Display Options";
            this.displayOptionsButton.UseVisualStyleBackColor = true;
            this.displayOptionsButton.Click += new System.EventHandler(this.displayOptionsButton_Click);
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.Location = new System.Drawing.Point(987, 2);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(30, 23);
            this.exportButton.TabIndex = 2;
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // SpectrumTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1028, 353);
            this.Controls.Add(this.treeListView);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.editGroupsButton);
            this.Controls.Add(this.groupingSetupButton);
            this.Controls.Add(this.displayOptionsButton);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas)(((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right)
                        | DigitalRune.Windows.Docking.DockAreas.Top)
                        | DigitalRune.Windows.Docking.DockAreas.Bottom)
                        | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.Name = "SpectrumTableForm";
            this.TabText = "SpectrumTableForm";
            this.Text = "SpectrumTableForm";
            ((System.ComponentModel.ISupportInitialize)(this.treeListView)).EndInit();
            this.exportMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private BrightIdeasSoftware.TreeListView treeListView;
        private BrightIdeasSoftware.OLVColumn keyColumn;
        private BrightIdeasSoftware.OLVColumn totalSpectraColumn;
        private BrightIdeasSoftware.OLVColumn spectraColumn;
        private BrightIdeasSoftware.OLVColumn distinctPeptidesColumn;
        private BrightIdeasSoftware.OLVColumn precursorMzColumn;
        private BrightIdeasSoftware.OLVColumn observedMassColumn;
        private BrightIdeasSoftware.OLVColumn chargeColumn;
        private BrightIdeasSoftware.OLVColumn massErrorColumn;
        private BrightIdeasSoftware.OLVColumn qvalueColumn;
        private BrightIdeasSoftware.OLVColumn exactMassColumn;
        private BrightIdeasSoftware.OLVColumn sequenceColumn;
        private System.Windows.Forms.Button editGroupsButton;
        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private System.Windows.Forms.ToolStripMenuItem clipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
        private BrightIdeasSoftware.OLVColumn analysisColumn;
        private System.Windows.Forms.Button groupingSetupButton;
        private BrightIdeasSoftware.OLVColumn distinctMatchesColumn;
        private BrightIdeasSoftware.OLVColumn distinctAnalysesColumn;
        private BrightIdeasSoftware.OLVColumn distinctChargesColumn;
        private System.Windows.Forms.Button displayOptionsButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.ToolStripMenuItem showSourcesInExcelToolStripMenuItem;

    }
}