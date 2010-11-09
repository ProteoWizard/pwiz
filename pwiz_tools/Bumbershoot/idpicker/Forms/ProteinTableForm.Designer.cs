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
    partial class ProteinTableForm
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
            this.accessionColumn = new BrightIdeasSoftware.OLVColumn();
            this.clusterColumn = new BrightIdeasSoftware.OLVColumn();
            this.proteinCountColumn = new BrightIdeasSoftware.OLVColumn();
            this.coverageColumn = new BrightIdeasSoftware.OLVColumn();
            this.distinctPeptidesColumn = new BrightIdeasSoftware.OLVColumn();
            this.distinctMatchesColumn = new BrightIdeasSoftware.OLVColumn();
            this.filteredSpectraColumn = new BrightIdeasSoftware.OLVColumn();
            this.descriptionColumn = new BrightIdeasSoftware.OLVColumn();
            this.exportButton = new System.Windows.Forms.Button();
            this.exportMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.clipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel1 = new System.Windows.Forms.Panel();
            this.displayOptionsButton = new System.Windows.Forms.Button();
            this.pivotSetupButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize) (this.treeListView)).BeginInit();
            this.exportMenu.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // treeListView
            // 
            this.treeListView.AllColumns.Add(this.accessionColumn);
            this.treeListView.AllColumns.Add(this.clusterColumn);
            this.treeListView.AllColumns.Add(this.proteinCountColumn);
            this.treeListView.AllColumns.Add(this.coverageColumn);
            this.treeListView.AllColumns.Add(this.distinctPeptidesColumn);
            this.treeListView.AllColumns.Add(this.distinctMatchesColumn);
            this.treeListView.AllColumns.Add(this.filteredSpectraColumn);
            this.treeListView.AllColumns.Add(this.descriptionColumn);
            this.treeListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.accessionColumn,
            this.clusterColumn,
            this.proteinCountColumn,
            this.coverageColumn,
            this.distinctPeptidesColumn,
            this.distinctMatchesColumn,
            this.filteredSpectraColumn,
            this.descriptionColumn});
            this.treeListView.Cursor = System.Windows.Forms.Cursors.Default;
            this.treeListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeListView.FullRowSelect = true;
            this.treeListView.HideSelection = false;
            this.treeListView.Location = new System.Drawing.Point(0, 0);
            this.treeListView.Name = "treeListView";
            this.treeListView.OwnerDraw = true;
            this.treeListView.ShowGroups = false;
            this.treeListView.Size = new System.Drawing.Size(1029, 325);
            this.treeListView.TabIndex = 0;
            this.treeListView.UnfocusedHighlightBackgroundColor = System.Drawing.SystemColors.Highlight;
            this.treeListView.UnfocusedHighlightForegroundColor = System.Drawing.SystemColors.HighlightText;
            this.treeListView.UseCompatibleStateImageBehavior = false;
            this.treeListView.UseHyperlinks = true;
            this.treeListView.View = System.Windows.Forms.View.Details;
            this.treeListView.VirtualMode = true;
            // 
            // accessionColumn
            // 
            this.accessionColumn.Text = "Accession";
            this.accessionColumn.Width = 100;
            // 
            // clusterColumn
            // 
            this.clusterColumn.Hyperlink = true;
            this.clusterColumn.Text = "Cluster";
            this.clusterColumn.Width = 55;
            // 
            // proteinCountColumn
            // 
            this.proteinCountColumn.Text = "Count";
            // 
            // coverageColumn
            // 
            this.coverageColumn.Hyperlink = true;
            this.coverageColumn.Text = "Coverage";
            // 
            // distinctPeptidesColumn
            // 
            this.distinctPeptidesColumn.Text = "Distinct Peptides";
            this.distinctPeptidesColumn.Width = 95;
            // 
            // distinctMatchesColumn
            // 
            this.distinctMatchesColumn.Text = "Distinct Matches";
            this.distinctMatchesColumn.Width = 95;
            // 
            // filteredSpectraColumn
            // 
            this.filteredSpectraColumn.Text = "Filtered Spectra";
            this.filteredSpectraColumn.Width = 90;
            // 
            // descriptionColumn
            // 
            this.descriptionColumn.Text = "Description";
            this.descriptionColumn.Width = 1000;
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.Location = new System.Drawing.Point(986, 2);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(30, 23);
            this.exportButton.TabIndex = 3;
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // exportMenu
            // 
            this.exportMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clipboardToolStripMenuItem,
            this.fileToolStripMenuItem,
            this.showInExcelToolStripMenuItem});
            this.exportMenu.Name = "contextMenuStrip1";
            this.exportMenu.Size = new System.Drawing.Size(172, 70);
            // 
            // clipboardToolStripMenuItem
            // 
            this.clipboardToolStripMenuItem.Name = "clipboardToolStripMenuItem";
            this.clipboardToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.clipboardToolStripMenuItem.Text = "Copy to Clipboard";
            this.clipboardToolStripMenuItem.Click += new System.EventHandler(this.clipboardToolStripMenuItem_Click);
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.fileToolStripMenuItem.Text = "Export to File";
            this.fileToolStripMenuItem.Click += new System.EventHandler(this.fileToolStripMenuItem_Click);
            // 
            // showInExcelToolStripMenuItem
            // 
            this.showInExcelToolStripMenuItem.Name = "showInExcelToolStripMenuItem";
            this.showInExcelToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.showInExcelToolStripMenuItem.Text = "Show in Excel";
            this.showInExcelToolStripMenuItem.Click += new System.EventHandler(this.showInExcelToolStripMenuItem_Click);
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles) ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.treeListView);
            this.panel1.Location = new System.Drawing.Point(0, 27);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1029, 325);
            this.panel1.TabIndex = 5;
            // 
            // displayOptionsButton
            // 
            this.displayOptionsButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.displayOptionsButton.Location = new System.Drawing.Point(884, 2);
            this.displayOptionsButton.Name = "displayOptionsButton";
            this.displayOptionsButton.Size = new System.Drawing.Size(96, 23);
            this.displayOptionsButton.TabIndex = 6;
            this.displayOptionsButton.Text = "Display Options";
            this.displayOptionsButton.UseVisualStyleBackColor = true;
            this.displayOptionsButton.Click += new System.EventHandler(this.displayOptionsButton_Click);
            // 
            // pivotSetupButton
            // 
            this.pivotSetupButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pivotSetupButton.Location = new System.Drawing.Point(794, 2);
            this.pivotSetupButton.Name = "pivotSetupButton";
            this.pivotSetupButton.Size = new System.Drawing.Size(84, 23);
            this.pivotSetupButton.TabIndex = 7;
            this.pivotSetupButton.Text = "Pivot Options";
            this.pivotSetupButton.UseVisualStyleBackColor = true;
            this.pivotSetupButton.Click += new System.EventHandler(this.pivotSetupButton_Click);
            // 
            // ProteinTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1028, 353);
            this.Controls.Add(this.pivotSetupButton);
            this.Controls.Add(this.displayOptionsButton);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.panel1);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas) (((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right)
                        | DigitalRune.Windows.Docking.DockAreas.Top)
                        | DigitalRune.Windows.Docking.DockAreas.Bottom)
                        | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.Name = "ProteinTableForm";
            this.TabText = "ProteinTableForm";
            this.Text = "ProteinTableForm";
            ((System.ComponentModel.ISupportInitialize) (this.treeListView)).EndInit();
            this.exportMenu.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private BrightIdeasSoftware.TreeListView treeListView;
        private BrightIdeasSoftware.OLVColumn accessionColumn;
        private BrightIdeasSoftware.OLVColumn distinctPeptidesColumn;
        private BrightIdeasSoftware.OLVColumn filteredSpectraColumn;
        private BrightIdeasSoftware.OLVColumn descriptionColumn;
        private BrightIdeasSoftware.OLVColumn proteinCountColumn;
        private BrightIdeasSoftware.OLVColumn distinctMatchesColumn;
        private BrightIdeasSoftware.OLVColumn clusterColumn;
        private BrightIdeasSoftware.OLVColumn coverageColumn;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private System.Windows.Forms.ToolStripMenuItem clipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
        private System.Windows.Forms.Button displayOptionsButton;
        private System.Windows.Forms.Button pivotSetupButton;

    }
}