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
            this.proteinGroupColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.distinctPeptidesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.distinctMatchesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.filteredSpectraColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.descriptionColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.peptideSequencesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.peptideGroupsColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
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
            this.clusterColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
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
            this.coverageColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            this.coverageColumn.TrackVisitedState = false;
            this.coverageColumn.Width = 78;
            // 
            // proteinGroupColumn
            // 
            this.proteinGroupColumn.HeaderText = "Protein Group";
            this.proteinGroupColumn.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.proteinGroupColumn.Name = "proteinGroupColumn";
            this.proteinGroupColumn.ReadOnly = true;
            this.proteinGroupColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            this.proteinGroupColumn.TrackVisitedState = false;
            this.proteinGroupColumn.Width = 80;
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
            // peptideSequencesColumn
            // 
            this.peptideSequencesColumn.HeaderText = "Peptide Sequences";
            this.peptideSequencesColumn.Name = "peptideSequencesColumn";
            this.peptideSequencesColumn.ReadOnly = true;
            this.peptideSequencesColumn.Width = 120;
            // 
            // peptideGroupsColumn
            // 
            this.peptideGroupsColumn.HeaderText = "Peptide Groups";
            this.peptideGroupsColumn.Name = "peptideGroupsColumn";
            this.peptideGroupsColumn.ReadOnly = true;
            this.peptideGroupsColumn.Width = 100;
            // 
            // treeDataGridView
            // 
            treeDataGridView.Columns.AddRange(keyColumn,
                                              clusterColumn,
                                              countColumn,
                                              coverageColumn,
                                              proteinGroupColumn,
                                              distinctPeptidesColumn,
                                              distinctMatchesColumn,
                                              filteredSpectraColumn,
                                              descriptionColumn,
                                              peptideSequencesColumn,
                                              peptideGroupsColumn);
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
        private System.Windows.Forms.DataGridViewLinkColumn proteinGroupColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctPeptidesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctMatchesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn filteredSpectraColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn descriptionColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn peptideSequencesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn peptideGroupsColumn;

    }
}