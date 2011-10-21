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
            this.keyColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clusterColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.countColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.coverageColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.distinctPeptidesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.distinctMatchesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.filteredSpectraColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.descriptionColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.SuspendLayout();
            // 
            // keyColumn
            // 
            this.keyColumn.HeaderText = "Accession";
            this.keyColumn.Name = "keyColumn";
            this.keyColumn.ReadOnly = true;
            this.keyColumn.Width = 81;
            // 
            // clusterColumn
            // 
            this.clusterColumn.HeaderText = "Cluster";
            this.clusterColumn.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.clusterColumn.Name = "clusterColumn";
            this.clusterColumn.ReadOnly = true;
            this.clusterColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.clusterColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.clusterColumn.TrackVisitedState = false;
            this.clusterColumn.Width = 64;
            // 
            // countColumn
            // 
            this.countColumn.HeaderText = "Count";
            this.countColumn.Name = "countColumn";
            this.countColumn.ReadOnly = true;
            this.countColumn.Width = 60;
            // 
            // coverageColumn
            // 
            this.coverageColumn.HeaderText = "Coverage";
            this.coverageColumn.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.coverageColumn.Name = "coverageColumn";
            this.coverageColumn.ReadOnly = true;
            this.coverageColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.coverageColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.coverageColumn.TrackVisitedState = false;
            this.coverageColumn.Width = 78;
            // 
            // distinctPeptidesColumn
            // 
            this.distinctPeptidesColumn.HeaderText = "Distinct Peptides";
            this.distinctPeptidesColumn.Name = "distinctPeptidesColumn";
            this.distinctPeptidesColumn.ReadOnly = true;
            this.distinctPeptidesColumn.Width = 111;
            // 
            // distinctMatchesColumn
            // 
            this.distinctMatchesColumn.HeaderText = "Distinct Matches";
            this.distinctMatchesColumn.Name = "distinctMatchesColumn";
            this.distinctMatchesColumn.ReadOnly = true;
            this.distinctMatchesColumn.Width = 111;
            // 
            // filteredSpectraColumn
            // 
            this.filteredSpectraColumn.HeaderText = "Filtered Spectra";
            this.filteredSpectraColumn.Name = "filteredSpectraColumn";
            this.filteredSpectraColumn.ReadOnly = true;
            this.filteredSpectraColumn.Width = 106;
            // 
            // descriptionColumn
            // 
            this.descriptionColumn.HeaderText = "Description";
            this.descriptionColumn.Name = "descriptionColumn";
            this.descriptionColumn.ReadOnly = true;
            this.descriptionColumn.Width = 85;
            // 
            // treeDataGridView
            // 
            treeDataGridView.Columns.AddRange(keyColumn,
                                              clusterColumn,
                                              countColumn,
                                              coverageColumn,
                                              distinctPeptidesColumn,
                                              distinctMatchesColumn,
                                              filteredSpectraColumn,
                                              descriptionColumn);
            // 
            // ProteinTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1064, 362);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas) (((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right)
                        | DigitalRune.Windows.Docking.DockAreas.Top)
                        | DigitalRune.Windows.Docking.DockAreas.Bottom)
                        | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.Name = "ProteinTableForm";
            this.TabText = "ProteinTableForm";
            this.Text = "ProteinTableForm";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridViewTextBoxColumn keyColumn;
        private System.Windows.Forms.DataGridViewLinkColumn clusterColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn countColumn;
        private System.Windows.Forms.DataGridViewLinkColumn coverageColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctPeptidesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctMatchesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn filteredSpectraColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn descriptionColumn;

    }
}