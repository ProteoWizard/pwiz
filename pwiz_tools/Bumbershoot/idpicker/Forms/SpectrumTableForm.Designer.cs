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

using System.Windows.Forms;

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
            this.keyColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.totalSpectraColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.filteredSpectraColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.distinctPeptidesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.distinctMatchesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.distinctAnalysesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.distinctChargesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.proteinGroupsColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.precursorMzColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.scanTimeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.analysisColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.chargeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.observedMassColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.exactMassColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.massErrorColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.qvalueColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.sequenceColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.SuspendLayout();
            // 
            // keyColumn
            // 
            this.keyColumn.MinimumWidth = 100;
            this.keyColumn.HeaderText = "Key";
            this.keyColumn.Name = "keyColumn";
            this.keyColumn.ReadOnly = true;
            this.keyColumn.Width = 200;
            // 
            // totalSpectraColumn
            // 
            this.totalSpectraColumn.DisplayIndex = 1;
            this.totalSpectraColumn.Visible = false;
            this.totalSpectraColumn.HeaderText = "Total Spectra";
            this.totalSpectraColumn.Name = "totalSpectraColumn";
            this.totalSpectraColumn.ReadOnly = true;
            this.totalSpectraColumn.Width = 80;
            // 
            // spectraColumn
            // 
            this.filteredSpectraColumn.HeaderText = "Filtered Spectra";
            this.filteredSpectraColumn.Name = "filteredSpectraColumn";
            this.filteredSpectraColumn.ReadOnly = true;
            this.filteredSpectraColumn.Width = 100;
            // 
            // distinctPeptidesColumn
            // 
            this.distinctPeptidesColumn.HeaderText = "Distinct Peptides";
            this.distinctPeptidesColumn.Name = "distinctPeptidesColumn";
            this.distinctPeptidesColumn.ReadOnly = true;
            this.distinctPeptidesColumn.Width = 100;
            // 
            // distinctMatchesColumn
            // 
            this.distinctMatchesColumn.HeaderText = "Distinct Matches";
            this.distinctMatchesColumn.Name = "distinctMatchesColumn";
            this.distinctMatchesColumn.ReadOnly = true;
            this.distinctMatchesColumn.Width = 100;
            // 
            // distinctAnalysesColumn
            // 
            this.distinctAnalysesColumn.HeaderText = "Distinct Analyses";
            this.distinctAnalysesColumn.Name = "distinctAnalysesColumn";
            this.distinctAnalysesColumn.ReadOnly = true;
            this.distinctAnalysesColumn.Width = 100;
            // 
            // distinctChargesColumn
            // 
            this.distinctChargesColumn.HeaderText = "Distinct Charges";
            this.distinctChargesColumn.Name = "distinctChargesColumn";
            this.distinctChargesColumn.ReadOnly = true;
            this.distinctChargesColumn.Width = 95;
            // 
            // proteinGroupsColumn
            // 
            this.proteinGroupsColumn.HeaderText = "Protein Groups";
            this.proteinGroupsColumn.Name = "proteinGroupsColumn";
            this.proteinGroupsColumn.ReadOnly = true;
            this.proteinGroupsColumn.Width = 100;
            // 
            // precursorMzColumn
            // 
            this.precursorMzColumn.HeaderText = "Precursor m/z";
            this.precursorMzColumn.Name = "precursorMzColumn";
            this.precursorMzColumn.ReadOnly = true;
            this.precursorMzColumn.Width = 80;
            // 
            // scanTimeColumn
            // 
            this.scanTimeColumn.HeaderText = "Scan Time";
            this.scanTimeColumn.Name = "scanTimeColumn";
            this.scanTimeColumn.ReadOnly = true;
            this.scanTimeColumn.Width = 80;
            // 
            // analysisColumn
            // 
            this.analysisColumn.HeaderText = "Analysis";
            this.analysisColumn.Name = "analysisColumn";
            this.analysisColumn.ReadOnly = true;
            // 
            // chargeColumn
            // 
            this.chargeColumn.HeaderText = "Charge";
            this.chargeColumn.Name = "chargeColumn";
            this.chargeColumn.ReadOnly = true;
            this.chargeColumn.Width = 50;
            // 
            // observedMassColumn
            // 
            this.observedMassColumn.HeaderText = "Observed Mass";
            this.observedMassColumn.Name = "observedMassColumn";
            this.observedMassColumn.ReadOnly = true;
            this.observedMassColumn.Width = 90;
            // 
            // exactMassColumn
            // 
            this.exactMassColumn.HeaderText = "Exact Mass";
            this.exactMassColumn.Name = "exactMassColumn";
            this.exactMassColumn.ReadOnly = true;
            this.exactMassColumn.Width = 70;
            // 
            // massErrorColumn
            // 
            this.massErrorColumn.HeaderText = "Mass Error";
            this.massErrorColumn.Name = "massErrorColumn";
            this.massErrorColumn.ReadOnly = true;
            this.massErrorColumn.Width = 65;
            // 
            // qvalueColumn
            // 
            this.qvalueColumn.HeaderText = "Q Value";
            this.qvalueColumn.Name = "qvalueColumn";
            this.qvalueColumn.ReadOnly = true;
            this.qvalueColumn.Width = 55;
            // 
            // sequenceColumn
            // 
            this.sequenceColumn.FillWeight = 100;
            this.sequenceColumn.HeaderText = "Sequence";
            this.sequenceColumn.Name = "sequenceColumn";
            this.sequenceColumn.ReadOnly = true;
            this.sequenceColumn.Width = 300;
            // 
            // treeDataGridView
            // 
            treeDataGridView.Columns.AddRange(keyColumn,
                                              distinctPeptidesColumn,
                                              distinctMatchesColumn,
                                              filteredSpectraColumn,
                                              distinctAnalysesColumn,
                                              distinctChargesColumn,
                                              proteinGroupsColumn,
                                              scanTimeColumn,
                                              precursorMzColumn,
                                              observedMassColumn,
                                              exactMassColumn,
                                              massErrorColumn,
                                              analysisColumn,
                                              chargeColumn,
                                              qvalueColumn,
                                              sequenceColumn);
            // 
            // SpectrumTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1064, 362);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas)(((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right)
                        | DigitalRune.Windows.Docking.DockAreas.Top)
                        | DigitalRune.Windows.Docking.DockAreas.Bottom)
                        | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.Name = "SpectrumTableForm";
            this.TabText = "SpectrumTableForm";
            this.Text = "SpectrumTableForm";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridViewTextBoxColumn keyColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn totalSpectraColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctPeptidesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctMatchesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn filteredSpectraColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctAnalysesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distinctChargesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn proteinGroupsColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn precursorMzColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn scanTimeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn observedMassColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn exactMassColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn massErrorColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn analysisColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn chargeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn qvalueColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn sequenceColumn;
    }
}