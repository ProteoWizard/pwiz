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

namespace IDPicker.Controls
{
    partial class QonverterSettingsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent ()
        {
            this.flowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.qonvertMethodComboBox = new System.Windows.Forms.ComboBox();
            this.rerankingCheckbox = new System.Windows.Forms.CheckBox();
            this.scoreGridView = new System.Windows.Forms.DataGridView();
            this.scoreNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.scoreWeightColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.scoreOrderColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.scoreNormalizationColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.flowLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) (this.scoreGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // flowLayoutPanel
            // 
            this.flowLayoutPanel.Controls.Add(this.label1);
            this.flowLayoutPanel.Controls.Add(this.qonvertMethodComboBox);
            this.flowLayoutPanel.Controls.Add(this.rerankingCheckbox);
            this.flowLayoutPanel.Controls.Add(this.scoreGridView);
            this.flowLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel.Name = "flowLayoutPanel";
            this.flowLayoutPanel.Size = new System.Drawing.Size(313, 187);
            this.flowLayoutPanel.TabIndex = 5;
            this.flowLayoutPanel.Resize += new System.EventHandler(this.flowLayoutPanel_Resize);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 8);
            this.label1.Margin = new System.Windows.Forms.Padding(3, 8, 3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(96, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Qonverter Method:";
            // 
            // qonvertMethodComboBox
            // 
            this.qonvertMethodComboBox.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.qonvertMethodComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.flowLayoutPanel.SetFlowBreak(this.qonvertMethodComboBox, true);
            this.qonvertMethodComboBox.FormattingEnabled = true;
            this.qonvertMethodComboBox.Items.AddRange(new object[] {
            "Static Weights",
            "Optimized (Monte Carlo)",
            "Optimized (Percolator)"});
            this.qonvertMethodComboBox.Location = new System.Drawing.Point(102, 3);
            this.qonvertMethodComboBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.qonvertMethodComboBox.Name = "qonvertMethodComboBox";
            this.qonvertMethodComboBox.Size = new System.Drawing.Size(208, 21);
            this.qonvertMethodComboBox.TabIndex = 3;
            // 
            // rerankingCheckbox
            // 
            this.rerankingCheckbox.AutoSize = true;
            this.rerankingCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.flowLayoutPanel.SetFlowBreak(this.rerankingCheckbox, true);
            this.rerankingCheckbox.Location = new System.Drawing.Point(14, 27);
            this.rerankingCheckbox.Margin = new System.Windows.Forms.Padding(14, 3, 3, 3);
            this.rerankingCheckbox.Name = "rerankingCheckbox";
            this.rerankingCheckbox.Size = new System.Drawing.Size(102, 17);
            this.rerankingCheckbox.TabIndex = 110;
            this.rerankingCheckbox.Text = "Rerank Results:";
            this.rerankingCheckbox.UseVisualStyleBackColor = true;
            // 
            // scoreGridView
            // 
            this.scoreGridView.AllowUserToResizeRows = false;
            this.scoreGridView.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.scoreGridView.BackgroundColor = System.Drawing.SystemColors.Window;
            this.scoreGridView.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.scoreGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.scoreGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.scoreGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.scoreNameColumn,
            this.scoreWeightColumn,
            this.scoreOrderColumn,
            this.scoreNormalizationColumn});
            this.flowLayoutPanel.SetFlowBreak(this.scoreGridView, true);
            this.scoreGridView.Location = new System.Drawing.Point(3, 50);
            this.scoreGridView.MultiSelect = false;
            this.scoreGridView.Name = "scoreGridView";
            this.scoreGridView.RowHeadersVisible = false;
            this.scoreGridView.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.scoreGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.scoreGridView.Size = new System.Drawing.Size(307, 118);
            this.scoreGridView.TabIndex = 109;
            // 
            // scoreNameColumn
            // 
            this.scoreNameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.scoreNameColumn.HeaderText = "Name";
            this.scoreNameColumn.Name = "scoreNameColumn";
            this.scoreNameColumn.ToolTipText = "The \"name\" of the score as it appears in the pepXML input";
            // 
            // scoreWeightColumn
            // 
            this.scoreWeightColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.scoreWeightColumn.HeaderText = "Weight";
            this.scoreWeightColumn.Name = "scoreWeightColumn";
            this.scoreWeightColumn.ToolTipText = "A rational number applied to this score when calculating a total score. Zero mean" +
                "s that the score will have no impact";
            this.scoreWeightColumn.Width = 66;
            // 
            // scoreOrderColumn
            // 
            this.scoreOrderColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.scoreOrderColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.scoreOrderColumn.HeaderText = "Order";
            this.scoreOrderColumn.MaxDropDownItems = 2;
            this.scoreOrderColumn.Name = "scoreOrderColumn";
            this.scoreOrderColumn.ToolTipText = "\"Ascending\" means a higher score is better, \"descending\" means a lower score is b" +
                "etter";
            this.scoreOrderColumn.Width = 39;
            // 
            // scoreNormalizationColumn
            // 
            this.scoreNormalizationColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.scoreNormalizationColumn.HeaderText = "Normalization";
            this.scoreNormalizationColumn.Name = "scoreNormalizationColumn";
            // 
            // QonverterSettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.flowLayoutPanel);
            this.Name = "QonverterSettingsControl";
            this.Size = new System.Drawing.Size(313, 187);
            this.flowLayoutPanel.ResumeLayout(false);
            this.flowLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize) (this.scoreGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox qonvertMethodComboBox;
        private System.Windows.Forms.CheckBox rerankingCheckbox;
        private System.Windows.Forms.DataGridView scoreGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn scoreNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn scoreWeightColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn scoreOrderColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn scoreNormalizationColumn;

    }
}