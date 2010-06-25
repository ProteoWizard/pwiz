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
            this.clusterColumn = new BrightIdeasSoftware.OLVColumn();
            this.accessionColumn = new BrightIdeasSoftware.OLVColumn();
            this.proteinCountColumn = new BrightIdeasSoftware.OLVColumn();
            this.filteredPeptidesColumn = new BrightIdeasSoftware.OLVColumn();
            this.filteredVariantsColumn = new BrightIdeasSoftware.OLVColumn();
            this.filteredSpectraColumn = new BrightIdeasSoftware.OLVColumn();
            this.descriptionColumn = new BrightIdeasSoftware.OLVColumn();
            ((System.ComponentModel.ISupportInitialize) (this.treeListView)).BeginInit();
            this.SuspendLayout();
            // 
            // treeListView
            // 
            this.treeListView.AllColumns.Add(this.accessionColumn);
            this.treeListView.AllColumns.Add(this.clusterColumn);
            this.treeListView.AllColumns.Add(this.proteinCountColumn);
            this.treeListView.AllColumns.Add(this.filteredPeptidesColumn);
            this.treeListView.AllColumns.Add(this.filteredVariantsColumn);
            this.treeListView.AllColumns.Add(this.filteredSpectraColumn);
            this.treeListView.AllColumns.Add(this.descriptionColumn);
            this.treeListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.accessionColumn,
            this.clusterColumn,
            this.proteinCountColumn,
            this.filteredPeptidesColumn,
            this.filteredVariantsColumn,
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
            this.treeListView.Size = new System.Drawing.Size(548, 353);
            this.treeListView.TabIndex = 0;
            this.treeListView.UnfocusedHighlightBackgroundColor = System.Drawing.SystemColors.Highlight;
            this.treeListView.UnfocusedHighlightForegroundColor = System.Drawing.SystemColors.HighlightText;
            this.treeListView.UseCompatibleStateImageBehavior = false;
            this.treeListView.UseHyperlinks = true;
            this.treeListView.View = System.Windows.Forms.View.Details;
            this.treeListView.VirtualMode = true;
            // 
            // clusterColumn
            // 
            this.clusterColumn.Hyperlink = true;
            this.clusterColumn.Text = "Cluster";
            this.clusterColumn.Width = 55;
            // 
            // accessionColumn
            // 
            this.accessionColumn.Text = "Accession";
            this.accessionColumn.Width = 100;
            // 
            // proteinCountColumn
            // 
            this.proteinCountColumn.Text = "Count";
            // 
            // filteredPeptidesColumn
            // 
            this.filteredPeptidesColumn.Text = "Filtered Peptides";
            this.filteredPeptidesColumn.Width = 90;
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
            // descriptionColumn
            // 
            this.descriptionColumn.Text = "Description";
            this.descriptionColumn.Width = 1000;
            // 
            // ProteinTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(548, 353);
            this.Controls.Add(this.treeListView);
            this.Name = "ProteinTableForm";
            this.TabText = "ProteinTableForm";
            this.Text = "ProteinTableForm";
            ((System.ComponentModel.ISupportInitialize) (this.treeListView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private BrightIdeasSoftware.TreeListView treeListView;
        private BrightIdeasSoftware.OLVColumn accessionColumn;
        private BrightIdeasSoftware.OLVColumn filteredPeptidesColumn;
        private BrightIdeasSoftware.OLVColumn filteredSpectraColumn;
        private BrightIdeasSoftware.OLVColumn descriptionColumn;
        private BrightIdeasSoftware.OLVColumn proteinCountColumn;
        private BrightIdeasSoftware.OLVColumn filteredVariantsColumn;
        private BrightIdeasSoftware.OLVColumn clusterColumn;

    }
}