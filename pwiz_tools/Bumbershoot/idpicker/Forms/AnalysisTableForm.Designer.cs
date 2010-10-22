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
// Contributor(s):
//

namespace IDPicker.Forms
{
    partial class AnalysisTableForm
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
            this.nameColumn = new BrightIdeasSoftware.OLVColumn();
            this.softwareColumn = new BrightIdeasSoftware.OLVColumn();
            this.parameterCountColumn = new BrightIdeasSoftware.OLVColumn();
            this.parameterValue = new BrightIdeasSoftware.OLVColumn();
            ((System.ComponentModel.ISupportInitialize) (this.treeListView)).BeginInit();
            this.SuspendLayout();
            // 
            // treeListView
            // 
            this.treeListView.AllColumns.Add(this.nameColumn);
            this.treeListView.AllColumns.Add(this.softwareColumn);
            this.treeListView.AllColumns.Add(this.parameterCountColumn);
            this.treeListView.AllColumns.Add(this.parameterValue);
            this.treeListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.nameColumn,
            this.softwareColumn,
            this.parameterCountColumn,
            this.parameterValue});
            this.treeListView.Cursor = System.Windows.Forms.Cursors.Default;
            this.treeListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeListView.FullRowSelect = true;
            this.treeListView.HideSelection = false;
            this.treeListView.Location = new System.Drawing.Point(0, 0);
            this.treeListView.Name = "treeListView";
            this.treeListView.OwnerDraw = true;
            this.treeListView.ShowGroups = false;
            this.treeListView.Size = new System.Drawing.Size(634, 466);
            this.treeListView.TabIndex = 1;
            this.treeListView.UnfocusedHighlightBackgroundColor = System.Drawing.SystemColors.Highlight;
            this.treeListView.UnfocusedHighlightForegroundColor = System.Drawing.SystemColors.HighlightText;
            this.treeListView.UseCompatibleStateImageBehavior = false;
            this.treeListView.View = System.Windows.Forms.View.Details;
            this.treeListView.VirtualMode = true;
            // 
            // nameColumn
            // 
            this.nameColumn.FillsFreeSpace = true;
            this.nameColumn.Text = "Name";
            // 
            // softwareColumn
            // 
            this.softwareColumn.Text = "Software";
            this.softwareColumn.Width = 95;
            // 
            // parameterCountColumn
            // 
            this.parameterCountColumn.Text = "Parameters";
            this.parameterCountColumn.Width = 90;
            // 
            // parameterValue
            // 
            this.parameterValue.FillsFreeSpace = true;
            this.parameterValue.Text = "Value";
            this.parameterValue.Width = 100;
            // 
            // AnalysisTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(634, 466);
            this.Controls.Add(this.treeListView);
            this.Name = "AnalysisTableForm";
            this.TabText = "AnalysisTableForm";
            this.Text = "AnalysisTableForm";
            ((System.ComponentModel.ISupportInitialize) (this.treeListView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private BrightIdeasSoftware.TreeListView treeListView;
        private BrightIdeasSoftware.OLVColumn nameColumn;
        private BrightIdeasSoftware.OLVColumn softwareColumn;
        private BrightIdeasSoftware.OLVColumn parameterCountColumn;
        private BrightIdeasSoftware.OLVColumn parameterValue;
    }
}