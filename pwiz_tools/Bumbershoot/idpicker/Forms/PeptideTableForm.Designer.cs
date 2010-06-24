//
// $Id: $
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
            this.terminalSpecificityColumn = new BrightIdeasSoftware.OLVColumn();
            this.missedCleavagesColumn = new BrightIdeasSoftware.OLVColumn();
            this.radioButton1 = new System.Windows.Forms.RadioButton();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.offsetColumn = new BrightIdeasSoftware.OLVColumn();
            ((System.ComponentModel.ISupportInitialize) (this.treeListView)).BeginInit();
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
            this.treeListView.Anchor = ((System.Windows.Forms.AnchorStyles) ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
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
            this.treeListView.Location = new System.Drawing.Point(0, 28);
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
            this.sequenceColumn.HeaderFont = null;
            this.sequenceColumn.Text = "Sequence";
            // 
            // filteredVariantsColumn
            // 
            this.filteredVariantsColumn.HeaderFont = null;
            this.filteredVariantsColumn.Text = "Filtered Variants";
            this.filteredVariantsColumn.Width = 90;
            // 
            // filteredSpectraColumn
            // 
            this.filteredSpectraColumn.HeaderFont = null;
            this.filteredSpectraColumn.Text = "Filtered Spectra";
            this.filteredSpectraColumn.Width = 90;
            // 
            // monoisotopicMassColumn
            // 
            this.monoisotopicMassColumn.HeaderFont = null;
            this.monoisotopicMassColumn.Text = "Monoisotopic Mass";
            this.monoisotopicMassColumn.Width = 110;
            // 
            // molecularWeightColumn
            // 
            this.molecularWeightColumn.HeaderFont = null;
            this.molecularWeightColumn.Text = "Molecular Weight";
            this.molecularWeightColumn.Width = 100;
            // 
            // terminalSpecificityColumn
            // 
            this.terminalSpecificityColumn.DisplayIndex = 4;
            this.terminalSpecificityColumn.HeaderFont = null;
            this.terminalSpecificityColumn.IsVisible = false;
            this.terminalSpecificityColumn.Text = "Terminal Specificity";
            this.terminalSpecificityColumn.Width = 110;
            // 
            // missedCleavagesColumn
            // 
            this.missedCleavagesColumn.DisplayIndex = 5;
            this.missedCleavagesColumn.HeaderFont = null;
            this.missedCleavagesColumn.IsVisible = false;
            this.missedCleavagesColumn.Text = "Missed Cleavages";
            this.missedCleavagesColumn.Width = 100;
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
            // offsetColumn
            // 
            this.offsetColumn.HeaderFont = null;
            this.offsetColumn.IsVisible = false;
            this.offsetColumn.Text = "Offset";
            // 
            // PeptideTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(722, 353);
            this.Controls.Add(this.radioButton2);
            this.Controls.Add(this.radioButton1);
            this.Controls.Add(this.treeListView);
            this.Name = "PeptideTableForm";
            this.Opacity = 0.25;
            this.TabText = "PeptideTableForm";
            this.Text = "PeptideTableForm";
            ((System.ComponentModel.ISupportInitialize) (this.treeListView)).EndInit();
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

    }
}