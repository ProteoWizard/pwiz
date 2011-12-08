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
            this.label1 = new System.Windows.Forms.Label();
            this.qonvertMethodComboBox = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.chargeStateHandlingComboBox = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.terminalSpecificityHandlingComboBox = new System.Windows.Forms.ComboBox();
            this.svmPanel = new System.Windows.Forms.Panel();
            this.kernelComboBox = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.missedCleavagesComboBox = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.massErrorHandlingComboBox = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.RankPanel = new System.Windows.Forms.Panel();
            this.rerankingCheckbox = new System.Windows.Forms.CheckBox();
            this.scoreGridView = new System.Windows.Forms.DataGridView();
            this.scoreNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.scoreWeightColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.scoreOrderColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.scoreNormalizationColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ProperLayoutPanel = new System.Windows.Forms.Panel();
            this.svmPanel.SuspendLayout();
            this.RankPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.scoreGridView)).BeginInit();
            this.ProperLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 8);
            this.label1.Margin = new System.Windows.Forms.Padding(8, 8, 3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(96, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Qonverter Method:";
            // 
            // qonvertMethodComboBox
            // 
            this.qonvertMethodComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.qonvertMethodComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.qonvertMethodComboBox.FormattingEnabled = true;
            this.qonvertMethodComboBox.Items.AddRange(new object[] {
            "Static Weights",
            "Optimized (SVM)"});
            this.qonvertMethodComboBox.Location = new System.Drawing.Point(107, 3);
            this.qonvertMethodComboBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.qonvertMethodComboBox.Name = "qonvertMethodComboBox";
            this.qonvertMethodComboBox.Size = new System.Drawing.Size(205, 21);
            this.qonvertMethodComboBox.TabIndex = 3;
            this.qonvertMethodComboBox.SelectedIndexChanged += new System.EventHandler(this.qonvertMethodComboBox_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(32, 32);
            this.label2.Margin = new System.Windows.Forms.Padding(32, 8, 3, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(72, 13);
            this.label2.TabIndex = 116;
            this.label2.Text = "Charge State:";
            // 
            // chargeStateHandlingComboBox
            // 
            this.chargeStateHandlingComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.chargeStateHandlingComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.chargeStateHandlingComboBox.FormattingEnabled = true;
            this.chargeStateHandlingComboBox.Items.AddRange(new object[] {
            "Ignore",
            "Partition",
            "Feature (SVM)"});
            this.chargeStateHandlingComboBox.Location = new System.Drawing.Point(107, 27);
            this.chargeStateHandlingComboBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.chargeStateHandlingComboBox.Name = "chargeStateHandlingComboBox";
            this.chargeStateHandlingComboBox.Size = new System.Drawing.Size(205, 21);
            this.chargeStateHandlingComboBox.TabIndex = 111;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 56);
            this.label3.Margin = new System.Windows.Forms.Padding(3, 8, 3, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(101, 13);
            this.label3.TabIndex = 117;
            this.label3.Text = "Terminal Specificity:";
            // 
            // terminalSpecificityHandlingComboBox
            // 
            this.terminalSpecificityHandlingComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.terminalSpecificityHandlingComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.terminalSpecificityHandlingComboBox.FormattingEnabled = true;
            this.terminalSpecificityHandlingComboBox.Items.AddRange(new object[] {
            "Ignore",
            "Partition",
            "Feature (SVM)"});
            this.terminalSpecificityHandlingComboBox.Location = new System.Drawing.Point(107, 51);
            this.terminalSpecificityHandlingComboBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.terminalSpecificityHandlingComboBox.Name = "terminalSpecificityHandlingComboBox";
            this.terminalSpecificityHandlingComboBox.Size = new System.Drawing.Size(205, 21);
            this.terminalSpecificityHandlingComboBox.TabIndex = 112;
            // 
            // svmPanel
            // 
            this.svmPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.svmPanel.Controls.Add(this.kernelComboBox);
            this.svmPanel.Controls.Add(this.label6);
            this.svmPanel.Controls.Add(this.missedCleavagesComboBox);
            this.svmPanel.Controls.Add(this.label5);
            this.svmPanel.Controls.Add(this.massErrorHandlingComboBox);
            this.svmPanel.Controls.Add(this.label4);
            this.svmPanel.Location = new System.Drawing.Point(3, 73);
            this.svmPanel.Name = "svmPanel";
            this.svmPanel.Size = new System.Drawing.Size(315, 76);
            this.svmPanel.TabIndex = 122;
            // 
            // kernelComboBox
            // 
            this.kernelComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.kernelComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.kernelComboBox.FormattingEnabled = true;
            this.kernelComboBox.Items.AddRange(new object[] {
            "Linear",
            "Polynomial",
            "RBF",
            "Sigmoid"});
            this.kernelComboBox.Location = new System.Drawing.Point(104, 50);
            this.kernelComboBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.kernelComboBox.Name = "kernelComboBox";
            this.kernelComboBox.Size = new System.Drawing.Size(205, 21);
            this.kernelComboBox.TabIndex = 115;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(35, 55);
            this.label6.Margin = new System.Windows.Forms.Padding(38, 8, 3, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(66, 13);
            this.label6.TabIndex = 120;
            this.label6.Text = "SVM Kernel:";
            // 
            // missedCleavagesComboBox
            // 
            this.missedCleavagesComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.missedCleavagesComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.missedCleavagesComboBox.FormattingEnabled = true;
            this.missedCleavagesComboBox.Items.AddRange(new object[] {
            "Ignore",
            "Feature (SVM)"});
            this.missedCleavagesComboBox.Location = new System.Drawing.Point(104, 26);
            this.missedCleavagesComboBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.missedCleavagesComboBox.Name = "missedCleavagesComboBox";
            this.missedCleavagesComboBox.Size = new System.Drawing.Size(205, 21);
            this.missedCleavagesComboBox.TabIndex = 114;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(5, 31);
            this.label5.Margin = new System.Windows.Forms.Padding(8, 8, 3, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(96, 13);
            this.label5.TabIndex = 119;
            this.label5.Text = "Missed Cleavages:";
            // 
            // massErrorHandlingComboBox
            // 
            this.massErrorHandlingComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.massErrorHandlingComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.massErrorHandlingComboBox.FormattingEnabled = true;
            this.massErrorHandlingComboBox.Items.AddRange(new object[] {
            "Ignore",
            "Feature (SVM)"});
            this.massErrorHandlingComboBox.Location = new System.Drawing.Point(104, 2);
            this.massErrorHandlingComboBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.massErrorHandlingComboBox.Name = "massErrorHandlingComboBox";
            this.massErrorHandlingComboBox.Size = new System.Drawing.Size(205, 21);
            this.massErrorHandlingComboBox.TabIndex = 113;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(41, 7);
            this.label4.Margin = new System.Windows.Forms.Padding(44, 8, 3, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(60, 13);
            this.label4.TabIndex = 118;
            this.label4.Text = "Mass Error:";
            // 
            // RankPanel
            // 
            this.RankPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.RankPanel.Controls.Add(this.rerankingCheckbox);
            this.RankPanel.Controls.Add(this.scoreGridView);
            this.RankPanel.Location = new System.Drawing.Point(3, 145);
            this.RankPanel.Name = "RankPanel";
            this.RankPanel.Size = new System.Drawing.Size(315, 158);
            this.RankPanel.TabIndex = 123;
            // 
            // rerankingCheckbox
            // 
            this.rerankingCheckbox.AutoSize = true;
            this.rerankingCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rerankingCheckbox.Location = new System.Drawing.Point(16, 3);
            this.rerankingCheckbox.Margin = new System.Windows.Forms.Padding(19, 3, 3, 3);
            this.rerankingCheckbox.Name = "rerankingCheckbox";
            this.rerankingCheckbox.Size = new System.Drawing.Size(102, 17);
            this.rerankingCheckbox.TabIndex = 110;
            this.rerankingCheckbox.Text = "Rerank Results:";
            this.rerankingCheckbox.UseVisualStyleBackColor = true;
            // 
            // scoreGridView
            // 
            this.scoreGridView.AllowUserToResizeRows = false;
            this.scoreGridView.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.scoreGridView.BackgroundColor = System.Drawing.SystemColors.Window;
            this.scoreGridView.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.scoreGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.scoreGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.scoreGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.scoreNameColumn,
            this.scoreWeightColumn,
            this.scoreOrderColumn,
            this.scoreNormalizationColumn});
            this.scoreGridView.Location = new System.Drawing.Point(3, 26);
            this.scoreGridView.MultiSelect = false;
            this.scoreGridView.Name = "scoreGridView";
            this.scoreGridView.RowHeadersVisible = false;
            this.scoreGridView.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.scoreGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.scoreGridView.Size = new System.Drawing.Size(309, 118);
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
            // ProperLayoutPanel
            // 
            this.ProperLayoutPanel.Controls.Add(this.label1);
            this.ProperLayoutPanel.Controls.Add(this.qonvertMethodComboBox);
            this.ProperLayoutPanel.Controls.Add(this.RankPanel);
            this.ProperLayoutPanel.Controls.Add(this.label2);
            this.ProperLayoutPanel.Controls.Add(this.svmPanel);
            this.ProperLayoutPanel.Controls.Add(this.chargeStateHandlingComboBox);
            this.ProperLayoutPanel.Controls.Add(this.terminalSpecificityHandlingComboBox);
            this.ProperLayoutPanel.Controls.Add(this.label3);
            this.ProperLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ProperLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.ProperLayoutPanel.Name = "ProperLayoutPanel";
            this.ProperLayoutPanel.Size = new System.Drawing.Size(321, 371);
            this.ProperLayoutPanel.TabIndex = 124;
            // 
            // QonverterSettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.ProperLayoutPanel);
            this.Name = "QonverterSettingsControl";
            this.Size = new System.Drawing.Size(321, 371);
            this.svmPanel.ResumeLayout(false);
            this.svmPanel.PerformLayout();
            this.RankPanel.ResumeLayout(false);
            this.RankPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.scoreGridView)).EndInit();
            this.ProperLayoutPanel.ResumeLayout(false);
            this.ProperLayoutPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox qonvertMethodComboBox;
        private System.Windows.Forms.CheckBox rerankingCheckbox;
        private System.Windows.Forms.DataGridView scoreGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn scoreNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn scoreWeightColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn scoreOrderColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn scoreNormalizationColumn;
        private System.Windows.Forms.ComboBox massErrorHandlingComboBox;
        private System.Windows.Forms.ComboBox missedCleavagesComboBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox kernelComboBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox terminalSpecificityHandlingComboBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox chargeStateHandlingComboBox;
        private System.Windows.Forms.Panel svmPanel;
        private System.Windows.Forms.Panel RankPanel;
        private System.Windows.Forms.Panel ProperLayoutPanel;

    }
}