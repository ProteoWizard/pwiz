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
    partial class PeptideTableForm
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
            this.sequenceColumn = new BrightIdeasSoftware.OLVColumn();
            this.filteredVariantsColumn = new BrightIdeasSoftware.OLVColumn();
            this.filteredSpectraColumn = new BrightIdeasSoftware.OLVColumn();
            this.monoisotopicMassColumn = new BrightIdeasSoftware.OLVColumn();
            this.molecularWeightColumn = new BrightIdeasSoftware.OLVColumn();
            this.offsetColumn = new BrightIdeasSoftware.OLVColumn();
            this.terminalSpecificityColumn = new BrightIdeasSoftware.OLVColumn();
            this.missedCleavagesColumn = new BrightIdeasSoftware.OLVColumn();
            this.proteinsColumn = new BrightIdeasSoftware.OLVColumn();
            this.radioButton1 = new System.Windows.Forms.RadioButton();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.exportButton = new System.Windows.Forms.Button();
            this.exportMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.clipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.displayOptionsButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.treeListView)).BeginInit();
            this.exportMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // treeListView
            // 
            this.treeListView.AllColumns.Add(this.sequenceColumn);
            this.treeListView.AllColumns.Add(this.filteredVariantsColumn);
            this.treeListView.AllColumns.Add(this.filteredSpectraColumn);
            this.treeListView.AllColumns.Add(this.monoisotopicMassColumn);
            this.treeListView.AllColumns.Add(this.molecularWeightColumn);
            this.treeListView.AllColumns.Add(this.offsetColumn);
            this.treeListView.AllColumns.Add(this.terminalSpecificityColumn);
            this.treeListView.AllColumns.Add(this.missedCleavagesColumn);
            this.treeListView.AllColumns.Add(this.proteinsColumn);
            this.treeListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.treeListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.sequenceColumn,
            this.filteredVariantsColumn,
            this.filteredSpectraColumn,
            this.monoisotopicMassColumn,
            this.molecularWeightColumn});
            this.treeListView.Cursor = System.Windows.Forms.Cursors.Default;
            this.treeListView.FullRowSelect = true;
            this.treeListView.HideSelection = false;
            this.treeListView.Location = new System.Drawing.Point(0, 27);
            this.treeListView.Name = "treeListView";
            this.treeListView.OwnerDraw = true;
            this.treeListView.ShowGroups = false;
            this.treeListView.Size = new System.Drawing.Size(723, 326);
            this.treeListView.TabIndex = 0;
            this.treeListView.UnfocusedHighlightBackgroundColor = System.Drawing.SystemColors.Highlight;
            this.treeListView.UnfocusedHighlightForegroundColor = System.Drawing.SystemColors.HighlightText;
            this.treeListView.UseCompatibleStateImageBehavior = false;
            this.treeListView.View = System.Windows.Forms.View.Details;
            this.treeListView.VirtualMode = true;
            // 
            // sequenceColumn
            // 
            this.sequenceColumn.FillsFreeSpace = true;
            this.sequenceColumn.Text = "Sequence";
            // 
            // filteredVariantsColumn
            // 
            this.filteredVariantsColumn.Text = "Filtered Variants";
            this.filteredVariantsColumn.Width = 90;
            // 
            // filteredSpectraColumn
            // 
            this.filteredSpectraColumn.Text = "Filtered Spectra";
            this.filteredSpectraColumn.Width = 90;
            // 
            // monoisotopicMassColumn
            // 
            this.monoisotopicMassColumn.Text = "Monoisotopic Mass";
            this.monoisotopicMassColumn.Width = 110;
            // 
            // molecularWeightColumn
            // 
            this.molecularWeightColumn.Text = "Molecular Weight";
            this.molecularWeightColumn.Width = 100;
            // 
            // offsetColumn
            // 
            this.offsetColumn.IsVisible = false;
            this.offsetColumn.Text = "Offset";
            // 
            // terminalSpecificityColumn
            // 
            this.terminalSpecificityColumn.DisplayIndex = 4;
            this.terminalSpecificityColumn.IsVisible = false;
            this.terminalSpecificityColumn.Text = "Terminal Specificity";
            this.terminalSpecificityColumn.Width = 110;
            // 
            // missedCleavagesColumn
            // 
            this.missedCleavagesColumn.DisplayIndex = 5;
            this.missedCleavagesColumn.IsVisible = false;
            this.missedCleavagesColumn.Text = "Missed Cleavages";
            this.missedCleavagesColumn.Width = 100;
            // 
            // proteinsColumn
            // 
            this.proteinsColumn.DisplayIndex = 6;
            this.proteinsColumn.FillsFreeSpace = true;
            this.proteinsColumn.IsVisible = false;
            this.proteinsColumn.Text = "Proteins";
            this.proteinsColumn.Width = 80;
            // 
            // radioButton1
            // 
            this.radioButton1.AutoSize = true;
            this.radioButton1.Checked = true;
            this.radioButton1.Location = new System.Drawing.Point(5, 5);
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new System.Drawing.Size(178, 17);
            this.radioButton1.TabIndex = 1;
            this.radioButton1.TabStop = true;
            this.radioButton1.Text = "Expand to distinct interpretations";
            this.radioButton1.UseVisualStyleBackColor = true;
            // 
            // radioButton2
            // 
            this.radioButton2.AutoSize = true;
            this.radioButton2.Location = new System.Drawing.Point(189, 5);
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new System.Drawing.Size(159, 17);
            this.radioButton2.TabIndex = 2;
            this.radioButton2.Text = "Expand to peptide instances";
            this.radioButton2.UseVisualStyleBackColor = true;
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.Location = new System.Drawing.Point(680, 2);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(30, 23);
            this.exportButton.TabIndex = 4;
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
            // displayOptionsButton
            // 
            this.displayOptionsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.displayOptionsButton.Location = new System.Drawing.Point(578, 2);
            this.displayOptionsButton.Name = "displayOptionsButton";
            this.displayOptionsButton.Size = new System.Drawing.Size(96, 23);
            this.displayOptionsButton.TabIndex = 5;
            this.displayOptionsButton.Text = "Display Options";
            this.displayOptionsButton.UseVisualStyleBackColor = true;
            this.displayOptionsButton.Click += new System.EventHandler(this.displayOptionsButton_Click);
            // 
            // PeptideTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(722, 353);
            this.Controls.Add(this.displayOptionsButton);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.radioButton2);
            this.Controls.Add(this.radioButton1);
            this.Controls.Add(this.treeListView);
            this.Name = "PeptideTableForm";
            this.Opacity = 0.25;
            this.TabText = "PeptideTableForm";
            this.Text = "PeptideTableForm";
            ((System.ComponentModel.ISupportInitialize)(this.treeListView)).EndInit();
            this.exportMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private BrightIdeasSoftware.TreeListView treeListView;
        private BrightIdeasSoftware.OLVColumn sequenceColumn;
        private BrightIdeasSoftware.OLVColumn filteredVariantsColumn;
        private BrightIdeasSoftware.OLVColumn filteredSpectraColumn;
        private BrightIdeasSoftware.OLVColumn monoisotopicMassColumn;
        private BrightIdeasSoftware.OLVColumn molecularWeightColumn;
        private BrightIdeasSoftware.OLVColumn terminalSpecificityColumn;
        private BrightIdeasSoftware.OLVColumn missedCleavagesColumn;
        private System.Windows.Forms.RadioButton radioButton1;
        private System.Windows.Forms.RadioButton radioButton2;
        private BrightIdeasSoftware.OLVColumn offsetColumn;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private System.Windows.Forms.ToolStripMenuItem clipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
        private BrightIdeasSoftware.OLVColumn proteinsColumn;
        private System.Windows.Forms.Button displayOptionsButton;

    }
}