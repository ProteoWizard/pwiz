namespace pwiz.Skyline.EditUI
{
    partial class PeakImputationForm
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
            this.components = new System.ComponentModel.Container();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnImputeForCurrentRow = new System.Windows.Forms.Button();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.groupBoxResults = new System.Windows.Forms.GroupBox();
            this.tbxAvgRtShift = new System.Windows.Forms.TextBox();
            this.lblAvgRtShift = new System.Windows.Forms.Label();
            this.tbxRejected = new System.Windows.Forms.TextBox();
            this.lblRejected = new System.Windows.Forms.Label();
            this.tbxAccepted = new System.Windows.Forms.TextBox();
            this.lblAccepted = new System.Windows.Forms.Label();
            this.btnImputeBoundaries = new System.Windows.Forms.Button();
            this.cbxOverwriteManual = new System.Windows.Forms.CheckBox();
            this.tbxRtDeviationCutoff = new System.Windows.Forms.TextBox();
            this.lblSdCutoff = new System.Windows.Forms.Label();
            this.groupBoxCutoff = new System.Windows.Forms.GroupBox();
            this.radioPValue = new System.Windows.Forms.RadioButton();
            this.radioPercentile = new System.Windows.Forms.RadioButton();
            this.radioQValue = new System.Windows.Forms.RadioButton();
            this.radioScore = new System.Windows.Forms.RadioButton();
            this.tbxCoreScoreCutoff = new System.Windows.Forms.TextBox();
            this.comboRetentionTimeAlignment = new System.Windows.Forms.ComboBox();
            this.lblRetentionTimeAlignment = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.updateProgressTimer = new System.Windows.Forms.Timer(this.components);
            this.panel1.SuspendLayout();
            this.groupBoxResults.SuspendLayout();
            this.groupBoxCutoff.SuspendLayout();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            this.databoundGridControl.Location = new System.Drawing.Point(0, 163);
            this.databoundGridControl.Size = new System.Drawing.Size(800, 287);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnImputeForCurrentRow);
            this.panel1.Controls.Add(this.progressBar1);
            this.panel1.Controls.Add(this.groupBoxResults);
            this.panel1.Controls.Add(this.btnImputeBoundaries);
            this.panel1.Controls.Add(this.cbxOverwriteManual);
            this.panel1.Controls.Add(this.tbxRtDeviationCutoff);
            this.panel1.Controls.Add(this.lblSdCutoff);
            this.panel1.Controls.Add(this.groupBoxCutoff);
            this.panel1.Controls.Add(this.comboRetentionTimeAlignment);
            this.panel1.Controls.Add(this.lblRetentionTimeAlignment);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(800, 163);
            this.panel1.TabIndex = 1;
            // 
            // btnImputeForCurrentRow
            // 
            this.btnImputeForCurrentRow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnImputeForCurrentRow.Location = new System.Drawing.Point(589, 43);
            this.btnImputeForCurrentRow.Name = "btnImputeForCurrentRow";
            this.btnImputeForCurrentRow.Size = new System.Drawing.Size(199, 23);
            this.btnImputeForCurrentRow.TabIndex = 20;
            this.btnImputeForCurrentRow.Text = "Impute Boundaries for Current Row";
            this.btnImputeForCurrentRow.UseVisualStyleBackColor = true;
            this.btnImputeForCurrentRow.Click += new System.EventHandler(this.btnImputeForCurrentRow_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(3, 140);
            this.progressBar1.Maximum = 10000;
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(311, 23);
            this.progressBar1.TabIndex = 19;
            // 
            // groupBoxResults
            // 
            this.groupBoxResults.Controls.Add(this.tbxAvgRtShift);
            this.groupBoxResults.Controls.Add(this.lblAvgRtShift);
            this.groupBoxResults.Controls.Add(this.tbxRejected);
            this.groupBoxResults.Controls.Add(this.lblRejected);
            this.groupBoxResults.Controls.Add(this.tbxAccepted);
            this.groupBoxResults.Controls.Add(this.lblAccepted);
            this.groupBoxResults.Location = new System.Drawing.Point(336, 9);
            this.groupBoxResults.Name = "groupBoxResults";
            this.groupBoxResults.Size = new System.Drawing.Size(200, 148);
            this.groupBoxResults.TabIndex = 18;
            this.groupBoxResults.TabStop = false;
            this.groupBoxResults.Text = "Results";
            // 
            // tbxAvgRtShift
            // 
            this.tbxAvgRtShift.Location = new System.Drawing.Point(18, 118);
            this.tbxAvgRtShift.Name = "tbxAvgRtShift";
            this.tbxAvgRtShift.ReadOnly = true;
            this.tbxAvgRtShift.Size = new System.Drawing.Size(100, 20);
            this.tbxAvgRtShift.TabIndex = 5;
            // 
            // lblAvgRtShift
            // 
            this.lblAvgRtShift.AutoSize = true;
            this.lblAvgRtShift.Location = new System.Drawing.Point(15, 102);
            this.lblAvgRtShift.Name = "lblAvgRtShift";
            this.lblAvgRtShift.Size = new System.Drawing.Size(166, 13);
            this.lblAvgRtShift.TabIndex = 4;
            this.lblAvgRtShift.Text = "Average distance from best peak:";
            // 
            // tbxRejected
            // 
            this.tbxRejected.Location = new System.Drawing.Point(18, 75);
            this.tbxRejected.Name = "tbxRejected";
            this.tbxRejected.ReadOnly = true;
            this.tbxRejected.Size = new System.Drawing.Size(84, 20);
            this.tbxRejected.TabIndex = 3;
            // 
            // lblRejected
            // 
            this.lblRejected.AutoSize = true;
            this.lblRejected.Location = new System.Drawing.Point(15, 59);
            this.lblRejected.Name = "lblRejected";
            this.lblRejected.Size = new System.Drawing.Size(53, 13);
            this.lblRejected.TabIndex = 2;
            this.lblRejected.Text = "Rejected:";
            // 
            // tbxAccepted
            // 
            this.tbxAccepted.Location = new System.Drawing.Point(18, 36);
            this.tbxAccepted.Name = "tbxAccepted";
            this.tbxAccepted.ReadOnly = true;
            this.tbxAccepted.Size = new System.Drawing.Size(84, 20);
            this.tbxAccepted.TabIndex = 1;
            // 
            // lblAccepted
            // 
            this.lblAccepted.AutoSize = true;
            this.lblAccepted.Location = new System.Drawing.Point(15, 19);
            this.lblAccepted.Name = "lblAccepted";
            this.lblAccepted.Size = new System.Drawing.Size(56, 13);
            this.lblAccepted.TabIndex = 0;
            this.lblAccepted.Text = "Accepted:";
            // 
            // btnImputeBoundaries
            // 
            this.btnImputeBoundaries.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnImputeBoundaries.Location = new System.Drawing.Point(589, 12);
            this.btnImputeBoundaries.Name = "btnImputeBoundaries";
            this.btnImputeBoundaries.Size = new System.Drawing.Size(199, 23);
            this.btnImputeBoundaries.TabIndex = 17;
            this.btnImputeBoundaries.Text = "Impute Boundaries for All Rows";
            this.btnImputeBoundaries.UseVisualStyleBackColor = true;
            this.btnImputeBoundaries.Click += new System.EventHandler(this.btnImputeBoundaries_Click);
            // 
            // cbxOverwriteManual
            // 
            this.cbxOverwriteManual.AutoSize = true;
            this.cbxOverwriteManual.Location = new System.Drawing.Point(15, 111);
            this.cbxOverwriteManual.Name = "cbxOverwriteManual";
            this.cbxOverwriteManual.Size = new System.Drawing.Size(140, 17);
            this.cbxOverwriteManual.TabIndex = 16;
            this.cbxOverwriteManual.Text = "Overwrite manual peaks";
            this.cbxOverwriteManual.UseVisualStyleBackColor = true;
            this.cbxOverwriteManual.CheckedChanged += new System.EventHandler(this.SettingsControlChanged);
            // 
            // tbxRtDeviationCutoff
            // 
            this.tbxRtDeviationCutoff.Location = new System.Drawing.Point(14, 74);
            this.tbxRtDeviationCutoff.Name = "tbxRtDeviationCutoff";
            this.tbxRtDeviationCutoff.Size = new System.Drawing.Size(128, 20);
            this.tbxRtDeviationCutoff.TabIndex = 15;
            this.tbxRtDeviationCutoff.Text = "1";
            this.tbxRtDeviationCutoff.Leave += new System.EventHandler(this.SettingsControlChanged);
            // 
            // lblSdCutoff
            // 
            this.lblSdCutoff.AutoSize = true;
            this.lblSdCutoff.Location = new System.Drawing.Point(11, 58);
            this.lblSdCutoff.Name = "lblSdCutoff";
            this.lblSdCutoff.Size = new System.Drawing.Size(67, 13);
            this.lblSdCutoff.TabIndex = 14;
            this.lblSdCutoff.Text = "Max RT shift";
            // 
            // groupBoxCutoff
            // 
            this.groupBoxCutoff.Controls.Add(this.radioPValue);
            this.groupBoxCutoff.Controls.Add(this.radioPercentile);
            this.groupBoxCutoff.Controls.Add(this.radioQValue);
            this.groupBoxCutoff.Controls.Add(this.radioScore);
            this.groupBoxCutoff.Controls.Add(this.tbxCoreScoreCutoff);
            this.groupBoxCutoff.Location = new System.Drawing.Point(172, 3);
            this.groupBoxCutoff.Name = "groupBoxCutoff";
            this.groupBoxCutoff.Size = new System.Drawing.Size(142, 138);
            this.groupBoxCutoff.TabIndex = 13;
            this.groupBoxCutoff.TabStop = false;
            this.groupBoxCutoff.Text = "Cutoff";
            // 
            // radioPValue
            // 
            this.radioPValue.AutoSize = true;
            this.radioPValue.Location = new System.Drawing.Point(6, 42);
            this.radioPValue.Name = "radioPValue";
            this.radioPValue.Size = new System.Drawing.Size(61, 17);
            this.radioPValue.TabIndex = 10;
            this.radioPValue.TabStop = true;
            this.radioPValue.Text = "P-value";
            this.radioPValue.UseVisualStyleBackColor = true;
            this.radioPValue.CheckedChanged += new System.EventHandler(this.CutoffTypeChanged);
            // 
            // radioPercentile
            // 
            this.radioPercentile.AutoSize = true;
            this.radioPercentile.Location = new System.Drawing.Point(6, 89);
            this.radioPercentile.Name = "radioPercentile";
            this.radioPercentile.Size = new System.Drawing.Size(72, 17);
            this.radioPercentile.TabIndex = 2;
            this.radioPercentile.TabStop = true;
            this.radioPercentile.Text = "Percentile";
            this.radioPercentile.UseVisualStyleBackColor = true;
            this.radioPercentile.CheckedChanged += new System.EventHandler(this.CutoffTypeChanged);
            // 
            // radioQValue
            // 
            this.radioQValue.AutoSize = true;
            this.radioQValue.Location = new System.Drawing.Point(6, 66);
            this.radioQValue.Name = "radioQValue";
            this.radioQValue.Size = new System.Drawing.Size(62, 17);
            this.radioQValue.TabIndex = 1;
            this.radioQValue.TabStop = true;
            this.radioQValue.Text = "Q-value";
            this.radioQValue.UseVisualStyleBackColor = true;
            this.radioQValue.CheckedChanged += new System.EventHandler(this.CutoffTypeChanged);
            // 
            // radioScore
            // 
            this.radioScore.AutoSize = true;
            this.radioScore.Checked = true;
            this.radioScore.Location = new System.Drawing.Point(6, 19);
            this.radioScore.Name = "radioScore";
            this.radioScore.Size = new System.Drawing.Size(53, 17);
            this.radioScore.TabIndex = 0;
            this.radioScore.TabStop = true;
            this.radioScore.Text = "Score";
            this.radioScore.UseVisualStyleBackColor = true;
            this.radioScore.CheckedChanged += new System.EventHandler(this.CutoffTypeChanged);
            // 
            // tbxCoreScoreCutoff
            // 
            this.tbxCoreScoreCutoff.Location = new System.Drawing.Point(6, 112);
            this.tbxCoreScoreCutoff.Name = "tbxCoreScoreCutoff";
            this.tbxCoreScoreCutoff.Size = new System.Drawing.Size(128, 20);
            this.tbxCoreScoreCutoff.TabIndex = 9;
            this.tbxCoreScoreCutoff.Leave += new System.EventHandler(this.SettingsControlChanged);
            // 
            // comboRetentionTimeAlignment
            // 
            this.comboRetentionTimeAlignment.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRetentionTimeAlignment.FormattingEnabled = true;
            this.comboRetentionTimeAlignment.Location = new System.Drawing.Point(15, 25);
            this.comboRetentionTimeAlignment.Name = "comboRetentionTimeAlignment";
            this.comboRetentionTimeAlignment.Size = new System.Drawing.Size(127, 21);
            this.comboRetentionTimeAlignment.TabIndex = 1;
            this.comboRetentionTimeAlignment.SelectedIndexChanged += new System.EventHandler(this.SettingsControlChanged);
            // 
            // lblRetentionTimeAlignment
            // 
            this.lblRetentionTimeAlignment.AutoSize = true;
            this.lblRetentionTimeAlignment.Location = new System.Drawing.Point(12, 9);
            this.lblRetentionTimeAlignment.Name = "lblRetentionTimeAlignment";
            this.lblRetentionTimeAlignment.Size = new System.Drawing.Size(126, 13);
            this.lblRetentionTimeAlignment.TabIndex = 0;
            this.lblRetentionTimeAlignment.Text = "Retention time alignment:";
            // 
            // updateProgressTimer
            // 
            this.updateProgressTimer.Interval = 2000;
            this.updateProgressTimer.Tick += new System.EventHandler(this.updateProgressTimer_Tick);
            // 
            // PeakImputationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.panel1);
            this.Name = "PeakImputationForm";
            this.TabText = "Peak Imputation";
            this.Text = "Peak Imputation";
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.databoundGridControl, 0);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.groupBoxResults.ResumeLayout(false);
            this.groupBoxResults.PerformLayout();
            this.groupBoxCutoff.ResumeLayout(false);
            this.groupBoxCutoff.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblRetentionTimeAlignment;
        private System.Windows.Forms.ComboBox comboRetentionTimeAlignment;
        private System.Windows.Forms.TextBox tbxRtDeviationCutoff;
        private System.Windows.Forms.Label lblSdCutoff;
        private System.Windows.Forms.GroupBox groupBoxCutoff;
        private System.Windows.Forms.RadioButton radioPValue;
        private System.Windows.Forms.RadioButton radioPercentile;
        private System.Windows.Forms.RadioButton radioQValue;
        private System.Windows.Forms.RadioButton radioScore;
        private System.Windows.Forms.TextBox tbxCoreScoreCutoff;
        private System.Windows.Forms.CheckBox cbxOverwriteManual;
        private System.Windows.Forms.Button btnImputeBoundaries;
        private System.Windows.Forms.GroupBox groupBoxResults;
        private System.Windows.Forms.TextBox tbxAvgRtShift;
        private System.Windows.Forms.Label lblAvgRtShift;
        private System.Windows.Forms.TextBox tbxRejected;
        private System.Windows.Forms.Label lblRejected;
        private System.Windows.Forms.TextBox tbxAccepted;
        private System.Windows.Forms.Label lblAccepted;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Button btnImputeForCurrentRow;
        private System.Windows.Forms.Timer updateProgressTimer;
    }
}