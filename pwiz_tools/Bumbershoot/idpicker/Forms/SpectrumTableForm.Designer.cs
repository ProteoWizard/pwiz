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
            this.sourceOrScanColumn = new BrightIdeasSoftware.OLVColumn();
            this.totalSpectraColumn = new BrightIdeasSoftware.OLVColumn();
            this.confidentSpectraColumn = new BrightIdeasSoftware.OLVColumn();
            this.confidentPeptidesColumn = new BrightIdeasSoftware.OLVColumn();
            this.precursorMzColumn = new BrightIdeasSoftware.OLVColumn();
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
            this.topRankOnlyCheckBox = new System.Windows.Forms.CheckBox();
            this.exportButton = new System.Windows.Forms.Button();
            this.displayOptionsButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.treeListView)).BeginInit();
            this.exportMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // treeListView
            // 
            this.treeListView.AllColumns.Add(this.sourceOrScanColumn);
            this.treeListView.AllColumns.Add(this.totalSpectraColumn);
            this.treeListView.AllColumns.Add(this.confidentSpectraColumn);
            this.treeListView.AllColumns.Add(this.confidentPeptidesColumn);
            this.treeListView.AllColumns.Add(this.precursorMzColumn);
            this.treeListView.AllColumns.Add(this.chargeColumn);
            this.treeListView.AllColumns.Add(this.observedMassColumn);
            this.treeListView.AllColumns.Add(this.exactMassColumn);
            this.treeListView.AllColumns.Add(this.massErrorColumn);
            this.treeListView.AllColumns.Add(this.qvalueColumn);
            this.treeListView.AllColumns.Add(this.sequenceColumn);
            this.treeListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.treeListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.sourceOrScanColumn,
            this.confidentSpectraColumn,
            this.confidentPeptidesColumn});
            this.treeListView.Cursor = System.Windows.Forms.Cursors.Default;
            this.treeListView.FullRowSelect = true;
            this.treeListView.HideSelection = false;
            this.treeListView.Location = new System.Drawing.Point(0, 27);
            this.treeListView.Name = "treeListView";
            this.treeListView.OwnerDraw = true;
            this.treeListView.ShowGroups = false;
            this.treeListView.Size = new System.Drawing.Size(817, 326);
            this.treeListView.TabIndex = 0;
            this.treeListView.UnfocusedHighlightBackgroundColor = System.Drawing.SystemColors.Highlight;
            this.treeListView.UnfocusedHighlightForegroundColor = System.Drawing.SystemColors.HighlightText;
            this.treeListView.UseCompatibleStateImageBehavior = false;
            this.treeListView.View = System.Windows.Forms.View.Details;
            this.treeListView.VirtualMode = true;
            // 
            // sourceOrScanColumn
            // 
            this.sourceOrScanColumn.FillsFreeSpace = true;
            this.sourceOrScanColumn.MinimumWidth = 100;
            this.sourceOrScanColumn.Text = "Source";
            this.sourceOrScanColumn.Width = 100;
            // 
            // totalSpectraColumn
            // 
            this.totalSpectraColumn.AspectName = "";
            this.totalSpectraColumn.DisplayIndex = 1;
            this.totalSpectraColumn.IsVisible = false;
            this.totalSpectraColumn.Text = "Total Spectra";
            this.totalSpectraColumn.Width = 80;
            // 
            // confidentSpectraColumn
            // 
            this.confidentSpectraColumn.Text = "Filtered Spectra";
            this.confidentSpectraColumn.Width = 90;
            // 
            // confidentPeptidesColumn
            // 
            this.confidentPeptidesColumn.Text = "Filtered Peptides";
            this.confidentPeptidesColumn.Width = 90;
            // 
            // precursorMzColumn
            // 
            this.precursorMzColumn.DisplayIndex = 4;
            this.precursorMzColumn.IsVisible = false;
            this.precursorMzColumn.Text = "Precursor m/z";
            this.precursorMzColumn.Width = 80;
            // 
            // chargeColumn
            // 
            this.chargeColumn.DisplayIndex = 5;
            this.chargeColumn.IsVisible = false;
            this.chargeColumn.Text = "Charge";
            this.chargeColumn.Width = 50;
            // 
            // observedMassColumn
            // 
            this.observedMassColumn.DisplayIndex = 6;
            this.observedMassColumn.IsVisible = false;
            this.observedMassColumn.Text = "Observed Mass";
            this.observedMassColumn.Width = 90;
            // 
            // exactMassColumn
            // 
            this.exactMassColumn.DisplayIndex = 7;
            this.exactMassColumn.IsVisible = false;
            this.exactMassColumn.Text = "Exact Mass";
            this.exactMassColumn.Width = 70;
            // 
            // massErrorColumn
            // 
            this.massErrorColumn.DisplayIndex = 8;
            this.massErrorColumn.IsVisible = false;
            this.massErrorColumn.Text = "Mass Error";
            this.massErrorColumn.Width = 65;
            // 
            // qvalueColumn
            // 
            this.qvalueColumn.DisplayIndex = 9;
            this.qvalueColumn.IsVisible = false;
            this.qvalueColumn.Text = "Q Value";
            this.qvalueColumn.Width = 55;
            // 
            // sequenceColumn
            // 
            this.sequenceColumn.DisplayIndex = 10;
            this.sequenceColumn.FillsFreeSpace = true;
            this.sequenceColumn.IsVisible = false;
            this.sequenceColumn.Text = "Sequence";
            this.sequenceColumn.Width = 70;
            // 
            // editGroupsButton
            // 
            this.editGroupsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.editGroupsButton.Location = new System.Drawing.Point(586, 2);
            this.editGroupsButton.Name = "editGroupsButton";
            this.editGroupsButton.Size = new System.Drawing.Size(81, 23);
            this.editGroupsButton.TabIndex = 1;
            this.editGroupsButton.Text = "Edit Grouping";
            this.editGroupsButton.UseVisualStyleBackColor = true;
            this.editGroupsButton.Click += new System.EventHandler(this.editGroupsButton_Click);
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.Location = new System.Drawing.Point(775, 2);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(30, 23);
            this.exportButton.TabIndex = 2;
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
            // topRankOnlyCheckBox
            // 
            this.topRankOnlyCheckBox.AutoSize = true;
            this.topRankOnlyCheckBox.Checked = true;
            this.topRankOnlyCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.topRankOnlyCheckBox.Location = new System.Drawing.Point(12, 6);
            this.topRankOnlyCheckBox.Name = "topRankOnlyCheckBox";
            this.topRankOnlyCheckBox.Size = new System.Drawing.Size(98, 17);
            this.topRankOnlyCheckBox.TabIndex = 3;
            this.topRankOnlyCheckBox.Text = "Top Rank Only";
            this.topRankOnlyCheckBox.UseVisualStyleBackColor = true;
            this.topRankOnlyCheckBox.CheckedChanged += new System.EventHandler(this.topRankOnlyCheckBox_CheckedChanged);
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.Location = new System.Drawing.Point(775, 2);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(30, 23);
            this.exportButton.TabIndex = 2;
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // displayOptionsButton
            // 
            this.displayOptionsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.displayOptionsButton.Location = new System.Drawing.Point(673, 2);
            this.displayOptionsButton.Name = "displayOptionsButton";
            this.displayOptionsButton.Size = new System.Drawing.Size(96, 23);
            this.displayOptionsButton.TabIndex = 3;
            this.displayOptionsButton.Text = "Display Options";
            this.displayOptionsButton.UseVisualStyleBackColor = true;
            this.displayOptionsButton.Click += new System.EventHandler(this.displayOptionsButton_Click);
            // 
            // SpectrumTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(817, 353);
            this.Controls.Add(this.treeListView);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.topRankOnlyCheckBox);
            this.Controls.Add(this.displayOptionsButton);
            this.Controls.Add(this.editGroupsButton);
            this.Name = "SpectrumTableForm";
            this.TabText = "SpectrumTableForm";
            this.Text = "SpectrumTableForm";
            ((System.ComponentModel.ISupportInitialize)(this.treeListView)).EndInit();
            this.exportMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private BrightIdeasSoftware.TreeListView treeListView;
        private BrightIdeasSoftware.OLVColumn sourceOrScanColumn;
        private BrightIdeasSoftware.OLVColumn totalSpectraColumn;
        private BrightIdeasSoftware.OLVColumn confidentSpectraColumn;
        private BrightIdeasSoftware.OLVColumn confidentPeptidesColumn;
        private BrightIdeasSoftware.OLVColumn precursorMzColumn;
        private BrightIdeasSoftware.OLVColumn observedMassColumn;
        private BrightIdeasSoftware.OLVColumn chargeColumn;
        private BrightIdeasSoftware.OLVColumn massErrorColumn;
        private BrightIdeasSoftware.OLVColumn qvalueColumn;
        private BrightIdeasSoftware.OLVColumn exactMassColumn;
        private BrightIdeasSoftware.OLVColumn sequenceColumn;
        private System.Windows.Forms.Button editGroupsButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private System.Windows.Forms.ToolStripMenuItem clipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
        private System.Windows.Forms.CheckBox topRankOnlyCheckBox;
        private System.Windows.Forms.Button displayOptionsButton;

    }
}