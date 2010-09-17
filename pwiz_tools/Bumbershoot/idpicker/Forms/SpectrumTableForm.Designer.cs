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
            ((System.ComponentModel.ISupportInitialize)(this.treeListView)).BeginInit();
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
            this.totalSpectraColumn,
            this.confidentSpectraColumn,
            this.confidentPeptidesColumn});
            this.treeListView.Cursor = System.Windows.Forms.Cursors.Default;
            this.treeListView.FullRowSelect = true;
            this.treeListView.HideSelection = false;
            this.treeListView.Location = new System.Drawing.Point(0, 31);
            this.treeListView.Name = "treeListView";
            this.treeListView.OwnerDraw = true;
            this.treeListView.ShowGroups = false;
            this.treeListView.Size = new System.Drawing.Size(817, 322);
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
            this.qvalueColumn.Text = "Q Key";
            this.qvalueColumn.Width = 55;
            // 
            // sequenceColumn
            // 
            this.sequenceColumn.DisplayIndex = 4;
            this.sequenceColumn.FillsFreeSpace = true;
            this.sequenceColumn.IsVisible = false;
            this.sequenceColumn.Text = "Sequence";
            this.sequenceColumn.Width = 70;
            // 
            // editGroupsButton
            // 
            this.editGroupsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.editGroupsButton.Location = new System.Drawing.Point(732, 4);
            this.editGroupsButton.Name = "editGroupsButton";
            this.editGroupsButton.Size = new System.Drawing.Size(81, 23);
            this.editGroupsButton.TabIndex = 1;
            this.editGroupsButton.Text = "Edit Grouping";
            this.editGroupsButton.UseVisualStyleBackColor = true;
            this.editGroupsButton.Click += new System.EventHandler(this.editGroupsButton_Click);
            // 
            // SpectrumTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(817, 353);
            this.Controls.Add(this.treeListView);
            this.Controls.Add(this.editGroupsButton);
            this.Name = "SpectrumTableForm";
            this.TabText = "SpectrumTableForm";
            this.Text = "SpectrumTableForm";
            ((System.ComponentModel.ISupportInitialize)(this.treeListView)).EndInit();
            this.ResumeLayout(false);

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

    }
}