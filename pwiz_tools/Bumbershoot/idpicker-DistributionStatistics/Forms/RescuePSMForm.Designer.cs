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
// The Initial Developer of the Original Code is Zeqiang Ma
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

namespace IDPicker.Forms
{
    partial class RescuePSMsForm
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
            this.btnClustering = new System.Windows.Forms.Button();
            this.tbStatus = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.progressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.lblStatusToolStrip = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblSimilarityThreshold = new System.Windows.Forms.Label();
            this.tbSimilarityThreshold = new System.Windows.Forms.TextBox();
            this.lblPrecursorMzTolerance = new System.Windows.Forms.Label();
            this.tbPrecursorMzTolerance = new System.Windows.Forms.TextBox();
            this.lblFragmentMzTolerance = new System.Windows.Forms.Label();
            this.tbFragmentMzTolerance = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tbRank = new System.Windows.Forms.TextBox();
            this.tbSearchScore1Threshold = new System.Windows.Forms.TextBox();
            this.cmbSearchScore1Name = new System.Windows.Forms.ComboBox();
            this.cmbSearchScore1Order = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cbBackupDB = new System.Windows.Forms.CheckBox();
            this.cbWriteLog = new System.Windows.Forms.CheckBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbMinClusterSize = new System.Windows.Forms.TextBox();
            this.cmbSearchScore2Order = new System.Windows.Forms.ComboBox();
            this.cmbSearchScore2Name = new System.Windows.Forms.ComboBox();
            this.tbSearchScore2Threshold = new System.Windows.Forms.TextBox();
            this.cmbSearchScore3Order = new System.Windows.Forms.ComboBox();
            this.cmbSearchScore3Name = new System.Windows.Forms.ComboBox();
            this.tbSearchScore3Threshold = new System.Windows.Forms.TextBox();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnClustering
            // 
            this.btnClustering.Location = new System.Drawing.Point(12, 12);
            this.btnClustering.Name = "btnClustering";
            this.btnClustering.Size = new System.Drawing.Size(65, 23);
            this.btnClustering.TabIndex = 0;
            this.btnClustering.Text = "Clustering";
            this.btnClustering.UseVisualStyleBackColor = true;
            this.btnClustering.Click += new System.EventHandler(this.btnClustering_Click);
            // 
            // tbStatus
            // 
            this.tbStatus.AcceptsReturn = true;
            this.tbStatus.AcceptsTab = true;
            this.tbStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbStatus.Location = new System.Drawing.Point(12, 94);
            this.tbStatus.Multiline = true;
            this.tbStatus.Name = "tbStatus";
            this.tbStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbStatus.Size = new System.Drawing.Size(611, 255);
            this.tbStatus.TabIndex = 1;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(12, 39);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(65, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.progressBar,
            this.lblStatusToolStrip});
            this.statusStrip1.Location = new System.Drawing.Point(0, 352);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(635, 22);
            this.statusStrip1.TabIndex = 3;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // progressBar
            // 
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(100, 16);
            // 
            // lblStatusToolStrip
            // 
            this.lblStatusToolStrip.Name = "lblStatusToolStrip";
            this.lblStatusToolStrip.Size = new System.Drawing.Size(39, 17);
            this.lblStatusToolStrip.Text = "Ready";
            // 
            // lblSimilarityThreshold
            // 
            this.lblSimilarityThreshold.AutoSize = true;
            this.lblSimilarityThreshold.Location = new System.Drawing.Point(82, 71);
            this.lblSimilarityThreshold.Name = "lblSimilarityThreshold";
            this.lblSimilarityThreshold.Size = new System.Drawing.Size(62, 13);
            this.lblSimilarityThreshold.TabIndex = 4;
            this.lblSimilarityThreshold.Text = "Similarity >=";
            // 
            // tbSimilarityThreshold
            // 
            this.tbSimilarityThreshold.Location = new System.Drawing.Point(150, 68);
            this.tbSimilarityThreshold.Name = "tbSimilarityThreshold";
            this.tbSimilarityThreshold.Size = new System.Drawing.Size(38, 20);
            this.tbSimilarityThreshold.TabIndex = 5;
            this.tbSimilarityThreshold.Text = "0.6";
            // 
            // lblPrecursorMzTolerance
            // 
            this.lblPrecursorMzTolerance.AutoSize = true;
            this.lblPrecursorMzTolerance.Location = new System.Drawing.Point(83, 17);
            this.lblPrecursorMzTolerance.Name = "lblPrecursorMzTolerance";
            this.lblPrecursorMzTolerance.Size = new System.Drawing.Size(86, 13);
            this.lblPrecursorMzTolerance.TabIndex = 6;
            this.lblPrecursorMzTolerance.Text = "PrecursorMZTol:";
            // 
            // tbPrecursorMzTolerance
            // 
            this.tbPrecursorMzTolerance.Location = new System.Drawing.Point(175, 14);
            this.tbPrecursorMzTolerance.Name = "tbPrecursorMzTolerance";
            this.tbPrecursorMzTolerance.Size = new System.Drawing.Size(40, 20);
            this.tbPrecursorMzTolerance.TabIndex = 7;
            this.tbPrecursorMzTolerance.Text = "1.25";
            // 
            // lblFragmentMzTolerance
            // 
            this.lblFragmentMzTolerance.AutoSize = true;
            this.lblFragmentMzTolerance.Location = new System.Drawing.Point(83, 44);
            this.lblFragmentMzTolerance.Name = "lblFragmentMzTolerance";
            this.lblFragmentMzTolerance.Size = new System.Drawing.Size(85, 13);
            this.lblFragmentMzTolerance.TabIndex = 8;
            this.lblFragmentMzTolerance.Text = "FragmentMZTol:";
            // 
            // tbFragmentMzTolerance
            // 
            this.tbFragmentMzTolerance.Location = new System.Drawing.Point(175, 41);
            this.tbFragmentMzTolerance.Name = "tbFragmentMzTolerance";
            this.tbFragmentMzTolerance.Size = new System.Drawing.Size(40, 20);
            this.tbFragmentMzTolerance.TabIndex = 9;
            this.tbFragmentMzTolerance.Text = "0.5";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(257, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(74, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "PSM Rank <=";
            // 
            // tbRank
            // 
            this.tbRank.Location = new System.Drawing.Point(341, 14);
            this.tbRank.Name = "tbRank";
            this.tbRank.Size = new System.Drawing.Size(25, 20);
            this.tbRank.TabIndex = 11;
            this.tbRank.Text = "5";
            // 
            // tbSearchScore1Threshold
            // 
            this.tbSearchScore1Threshold.Location = new System.Drawing.Point(592, 14);
            this.tbSearchScore1Threshold.Name = "tbSearchScore1Threshold";
            this.tbSearchScore1Threshold.Size = new System.Drawing.Size(28, 20);
            this.tbSearchScore1Threshold.TabIndex = 14;
            this.tbSearchScore1Threshold.Text = "10";
            // 
            // cmbSearchScore1Name
            // 
            this.cmbSearchScore1Name.FormattingEnabled = true;
            this.cmbSearchScore1Name.Items.AddRange(new object[] {
            "MyriMatch:MVH",
            "MyriMatch:Xcorr",
            "Sequest:xcorr",
            "Mascot:score",
            "Mascot:expectation value",
            "X!Tandem:expect",
            "X!Tandem:hyperscore"});
            this.cmbSearchScore1Name.Location = new System.Drawing.Point(461, 14);
            this.cmbSearchScore1Name.Name = "cmbSearchScore1Name";
            this.cmbSearchScore1Name.Size = new System.Drawing.Size(94, 21);
            this.cmbSearchScore1Name.TabIndex = 17;
            this.cmbSearchScore1Name.Text = "MyriMatch:MVH";
            // 
            // cmbSearchScore1Order
            // 
            this.cmbSearchScore1Order.FormattingEnabled = true;
            this.cmbSearchScore1Order.Items.AddRange(new object[] {
            ">=",
            "<="});
            this.cmbSearchScore1Order.Location = new System.Drawing.Point(556, 14);
            this.cmbSearchScore1Order.Name = "cmbSearchScore1Order";
            this.cmbSearchScore1Order.Size = new System.Drawing.Size(35, 21);
            this.cmbSearchScore1Order.TabIndex = 18;
            this.cmbSearchScore1Order.Text = ">=";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(383, 17);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(75, 13);
            this.label4.TabIndex = 19;
            this.label4.Text = "Search Score:";
            this.label4.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // cbBackupDB
            // 
            this.cbBackupDB.AutoSize = true;
            this.cbBackupDB.Checked = true;
            this.cbBackupDB.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbBackupDB.Location = new System.Drawing.Point(194, 70);
            this.cbBackupDB.Name = "cbBackupDB";
            this.cbBackupDB.Size = new System.Drawing.Size(95, 17);
            this.cbBackupDB.TabIndex = 20;
            this.cbBackupDB.Text = "Backup idpDB";
            this.cbBackupDB.UseVisualStyleBackColor = true;
            // 
            // cbWriteLog
            // 
            this.cbWriteLog.AutoSize = true;
            this.cbWriteLog.Checked = true;
            this.cbWriteLog.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbWriteLog.Location = new System.Drawing.Point(295, 70);
            this.cbWriteLog.Name = "cbWriteLog";
            this.cbWriteLog.Size = new System.Drawing.Size(92, 17);
            this.cbWriteLog.TabIndex = 21;
            this.cbWriteLog.Text = "Write Out Log";
            this.cbWriteLog.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(257, 44);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(77, 13);
            this.label5.TabIndex = 22;
            this.label5.Text = "Cluster Size >=";
            // 
            // tbMinClusterSize
            // 
            this.tbMinClusterSize.Location = new System.Drawing.Point(340, 41);
            this.tbMinClusterSize.Name = "tbMinClusterSize";
            this.tbMinClusterSize.Size = new System.Drawing.Size(26, 20);
            this.tbMinClusterSize.TabIndex = 23;
            this.tbMinClusterSize.Text = "2";
            // 
            // cmbSearchScore2Order
            // 
            this.cmbSearchScore2Order.FormattingEnabled = true;
            this.cmbSearchScore2Order.Items.AddRange(new object[] {
            ">=",
            "<="});
            this.cmbSearchScore2Order.Location = new System.Drawing.Point(556, 41);
            this.cmbSearchScore2Order.Name = "cmbSearchScore2Order";
            this.cmbSearchScore2Order.Size = new System.Drawing.Size(35, 21);
            this.cmbSearchScore2Order.TabIndex = 26;
            this.cmbSearchScore2Order.Text = ">=";
            // 
            // cmbSearchScore2Name
            // 
            this.cmbSearchScore2Name.FormattingEnabled = true;
            this.cmbSearchScore2Name.Items.AddRange(new object[] {
            "MyriMatch:MVH",
            "MyriMatch:Xcorr",
            "Sequest:xcorr",
            "Mascot:score",
            "Mascot:expectation value",
            "X!Tandem:expect",
            "X!Tandem:hyperscore"});
            this.cmbSearchScore2Name.Location = new System.Drawing.Point(461, 41);
            this.cmbSearchScore2Name.Name = "cmbSearchScore2Name";
            this.cmbSearchScore2Name.Size = new System.Drawing.Size(94, 21);
            this.cmbSearchScore2Name.TabIndex = 25;
            this.cmbSearchScore2Name.Text = "Sequest:xcorr";
            // 
            // tbSearchScore2Threshold
            // 
            this.tbSearchScore2Threshold.Location = new System.Drawing.Point(592, 41);
            this.tbSearchScore2Threshold.Name = "tbSearchScore2Threshold";
            this.tbSearchScore2Threshold.Size = new System.Drawing.Size(28, 20);
            this.tbSearchScore2Threshold.TabIndex = 24;
            this.tbSearchScore2Threshold.Text = "1";
            // 
            // cmbSearchScore3Order
            // 
            this.cmbSearchScore3Order.FormattingEnabled = true;
            this.cmbSearchScore3Order.Items.AddRange(new object[] {
            ">=",
            "<="});
            this.cmbSearchScore3Order.Location = new System.Drawing.Point(556, 68);
            this.cmbSearchScore3Order.Name = "cmbSearchScore3Order";
            this.cmbSearchScore3Order.Size = new System.Drawing.Size(35, 21);
            this.cmbSearchScore3Order.TabIndex = 29;
            this.cmbSearchScore3Order.Text = ">=";
            // 
            // cmbSearchScore3Name
            // 
            this.cmbSearchScore3Name.FormattingEnabled = true;
            this.cmbSearchScore3Name.Items.AddRange(new object[] {
            "MyriMatch:MVH",
            "MyriMatch:Xcorr",
            "Sequest:xcorr",
            "Mascot:score",
            "Mascot:expectation value",
            "X!Tandem:expect",
            "X!Tandem:hyperscore"});
            this.cmbSearchScore3Name.Location = new System.Drawing.Point(461, 68);
            this.cmbSearchScore3Name.Name = "cmbSearchScore3Name";
            this.cmbSearchScore3Name.Size = new System.Drawing.Size(94, 21);
            this.cmbSearchScore3Name.TabIndex = 28;
            this.cmbSearchScore3Name.Text = "X!Tandem:expect";
            // 
            // tbSearchScore3Threshold
            // 
            this.tbSearchScore3Threshold.Location = new System.Drawing.Point(592, 68);
            this.tbSearchScore3Threshold.Name = "tbSearchScore3Threshold";
            this.tbSearchScore3Threshold.Size = new System.Drawing.Size(28, 20);
            this.tbSearchScore3Threshold.TabIndex = 27;
            this.tbSearchScore3Threshold.Text = "0";
            // 
            // RescuePSMsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(635, 374);
            this.Controls.Add(this.cmbSearchScore3Order);
            this.Controls.Add(this.cmbSearchScore3Name);
            this.Controls.Add(this.tbSearchScore3Threshold);
            this.Controls.Add(this.cmbSearchScore2Order);
            this.Controls.Add(this.cmbSearchScore2Name);
            this.Controls.Add(this.tbSearchScore2Threshold);
            this.Controls.Add(this.tbMinClusterSize);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.cbWriteLog);
            this.Controls.Add(this.cbBackupDB);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.cmbSearchScore1Order);
            this.Controls.Add(this.cmbSearchScore1Name);
            this.Controls.Add(this.tbSearchScore1Threshold);
            this.Controls.Add(this.tbRank);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.tbFragmentMzTolerance);
            this.Controls.Add(this.lblFragmentMzTolerance);
            this.Controls.Add(this.tbPrecursorMzTolerance);
            this.Controls.Add(this.lblPrecursorMzTolerance);
            this.Controls.Add(this.tbSimilarityThreshold);
            this.Controls.Add(this.lblSimilarityThreshold);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tbStatus);
            this.Controls.Add(this.btnClustering);
            this.Name = "RescuePSMsForm";
            this.TabText = "Reassign PSMs";
            this.Text = "Reassign PSMs";
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnClustering;
        private System.Windows.Forms.TextBox tbStatus;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
        private System.Windows.Forms.ToolStripStatusLabel lblStatusToolStrip;
        private System.Windows.Forms.Label lblSimilarityThreshold;
        private System.Windows.Forms.TextBox tbSimilarityThreshold;
        private System.Windows.Forms.Label lblPrecursorMzTolerance;
        private System.Windows.Forms.TextBox tbPrecursorMzTolerance;
        private System.Windows.Forms.Label lblFragmentMzTolerance;
        private System.Windows.Forms.TextBox tbFragmentMzTolerance;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbRank;
        private System.Windows.Forms.TextBox tbSearchScore1Threshold;
        private System.Windows.Forms.ComboBox cmbSearchScore1Name;
        private System.Windows.Forms.ComboBox cmbSearchScore1Order;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox cbBackupDB;
        private System.Windows.Forms.CheckBox cbWriteLog;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbMinClusterSize;
        private System.Windows.Forms.ComboBox cmbSearchScore2Order;
        private System.Windows.Forms.ComboBox cmbSearchScore2Name;
        private System.Windows.Forms.TextBox tbSearchScore2Threshold;
        private System.Windows.Forms.ComboBox cmbSearchScore3Order;
        private System.Windows.Forms.ComboBox cmbSearchScore3Name;
        private System.Windows.Forms.TextBox tbSearchScore3Threshold;
    }
}