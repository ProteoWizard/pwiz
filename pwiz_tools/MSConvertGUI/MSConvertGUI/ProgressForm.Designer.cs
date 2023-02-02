//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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

namespace MSConvertGUI
{
    partial class ProgressForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
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
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.ProgressSplit = new System.Windows.Forms.SplitContainer();
            this.JobDataView = new System.Windows.Forms.DataGridView();
            this.NameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ProgressColumn = new CustomProgressCell.DataGridViewProgressColumn();
            this.ProgressSplit.Panel1.SuspendLayout();
            this.ProgressSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) (this.JobDataView)).BeginInit();
            this.SuspendLayout();
            // 
            // ProgressSplit
            // 
            this.ProgressSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ProgressSplit.Location = new System.Drawing.Point(0, 0);
            this.ProgressSplit.Name = "ProgressSplit";
            this.ProgressSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // ProgressSplit.Panel1
            // 
            this.ProgressSplit.Panel1.Controls.Add(this.JobDataView);
            this.ProgressSplit.Panel1MinSize = 45;
            this.ProgressSplit.Size = new System.Drawing.Size(713, 454);
            this.ProgressSplit.SplitterDistance = 333;
            this.ProgressSplit.TabIndex = 0;
            // 
            // JobDataView
            // 
            this.JobDataView.AllowUserToAddRows = false;
            this.JobDataView.AllowUserToDeleteRows = false;
            this.JobDataView.AllowUserToResizeRows = false;
            this.JobDataView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.JobDataView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.NameColumn,
            this.ProgressColumn});
            this.JobDataView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.JobDataView.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.JobDataView.Location = new System.Drawing.Point(0, 0);
            this.JobDataView.MultiSelect = false;
            this.JobDataView.Name = "JobDataView";
            this.JobDataView.RowHeadersVisible = false;
            this.JobDataView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.JobDataView.Size = new System.Drawing.Size(713, 333);
            this.JobDataView.TabIndex = 0;
            this.JobDataView.SelectionChanged += new System.EventHandler(this.JobDataView_SelectionChanged);
            // 
            // NameColumn
            // 
            this.NameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.NameColumn.HeaderText = "Name";
            this.NameColumn.Name = "NameColumn";
            // 
            // ProgressColumn
            // 
            this.ProgressColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.Black;
            this.ProgressColumn.DefaultCellStyle = dataGridViewCellStyle1;
            this.ProgressColumn.FillWeight = 60F;
            this.ProgressColumn.HeaderText = "Progress";
            this.ProgressColumn.MinimumWidth = 50;
            this.ProgressColumn.Name = "ProgressColumn";
            // 
            // ProgressForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(713, 454);
            this.Controls.Add(this.ProgressSplit);
            this.Name = "ProgressForm";
            this.Text = "ProgressForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.ProgressForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ProgressForm_FormClosing);
            this.ProgressSplit.Panel1.ResumeLayout(false);
            this.ProgressSplit.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize) (this.JobDataView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer ProgressSplit;
        private System.Windows.Forms.DataGridView JobDataView;
        private System.Windows.Forms.DataGridViewTextBoxColumn NameColumn;
        private CustomProgressCell.DataGridViewProgressColumn ProgressColumn;
    }
}