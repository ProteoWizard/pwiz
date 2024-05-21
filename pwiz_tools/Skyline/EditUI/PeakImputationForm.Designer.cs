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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PeakImputationForm));
            this.panel1 = new System.Windows.Forms.Panel();
            this.groupBoxScope = new System.Windows.Forms.GroupBox();
            this.radioScopeDocument = new System.Windows.Forms.RadioButton();
            this.radioScopeSelection = new System.Windows.Forms.RadioButton();
            this.tbxScoringModel = new System.Windows.Forms.TextBox();
            this.lblScoringModel = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.groupBoxResults = new System.Windows.Forms.GroupBox();
            this.tbxExemplary = new System.Windows.Forms.TextBox();
            this.lblExemplary = new System.Windows.Forms.Label();
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
            this.lblPercent = new System.Windows.Forms.Label();
            this.radioPValue = new System.Windows.Forms.RadioButton();
            this.radioPercentile = new System.Windows.Forms.RadioButton();
            this.radioQValue = new System.Windows.Forms.RadioButton();
            this.radioScore = new System.Windows.Forms.RadioButton();
            this.tbxCoreScoreCutoff = new System.Windows.Forms.TextBox();
            this.comboRetentionTimeAlignment = new System.Windows.Forms.ComboBox();
            this.lblRetentionTimeAlignment = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panel1.SuspendLayout();
            this.groupBoxScope.SuspendLayout();
            this.groupBoxResults.SuspendLayout();
            this.groupBoxCutoff.SuspendLayout();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            this.databoundGridControl.Location = new System.Drawing.Point(0, 193);
            this.databoundGridControl.Size = new System.Drawing.Size(800, 257);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.groupBoxScope);
            this.panel1.Controls.Add(this.tbxScoringModel);
            this.panel1.Controls.Add(this.lblScoringModel);
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
            this.panel1.Size = new System.Drawing.Size(800, 193);
            this.panel1.TabIndex = 1;
            // 
            // groupBoxScope
            // 
            this.groupBoxScope.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxScope.Controls.Add(this.radioScopeDocument);
            this.groupBoxScope.Controls.Add(this.radioScopeSelection);
            this.groupBoxScope.Location = new System.Drawing.Point(656, 43);
            this.groupBoxScope.Name = "groupBoxScope";
            this.groupBoxScope.Size = new System.Drawing.Size(141, 68);
            this.groupBoxScope.TabIndex = 23;
            this.groupBoxScope.TabStop = false;
            this.groupBoxScope.Text = "Scope";
            // 
            // radioScopeDocument
            // 
            this.radioScopeDocument.AutoSize = true;
            this.radioScopeDocument.Location = new System.Drawing.Point(10, 39);
            this.radioScopeDocument.Name = "radioScopeDocument";
            this.radioScopeDocument.Size = new System.Drawing.Size(74, 17);
            this.radioScopeDocument.TabIndex = 1;
            this.radioScopeDocument.Text = "Document";
            this.radioScopeDocument.UseVisualStyleBackColor = true;
            this.radioScopeDocument.Click += new System.EventHandler(this.SettingsControlChanged);
            // 
            // radioScopeSelection
            // 
            this.radioScopeSelection.AutoSize = true;
            this.radioScopeSelection.Checked = true;
            this.radioScopeSelection.Location = new System.Drawing.Point(10, 16);
            this.radioScopeSelection.Name = "radioScopeSelection";
            this.radioScopeSelection.Size = new System.Drawing.Size(69, 17);
            this.radioScopeSelection.TabIndex = 0;
            this.radioScopeSelection.TabStop = true;
            this.radioScopeSelection.Text = "Selection";
            this.radioScopeSelection.UseVisualStyleBackColor = true;
            this.radioScopeSelection.Click += new System.EventHandler(this.SettingsControlChanged);
            // 
            // tbxScoringModel
            // 
            this.tbxScoringModel.Location = new System.Drawing.Point(12, 26);
            this.tbxScoringModel.Name = "tbxScoringModel";
            this.tbxScoringModel.ReadOnly = true;
            this.tbxScoringModel.Size = new System.Drawing.Size(142, 20);
            this.tbxScoringModel.TabIndex = 22;
            this.toolTip1.SetToolTip(this.tbxScoringModel, "Scoring model used to determine best peaks.\r\nUse the \"Refine > Reintegrate\" menu " +
        "item to choose a different model.");
            // 
            // lblScoringModel
            // 
            this.lblScoringModel.AutoSize = true;
            this.lblScoringModel.Location = new System.Drawing.Point(12, 9);
            this.lblScoringModel.Name = "lblScoringModel";
            this.lblScoringModel.Size = new System.Drawing.Size(77, 13);
            this.lblScoringModel.TabIndex = 21;
            this.lblScoringModel.Text = "Scoring model:";
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(551, 164);
            this.progressBar1.Maximum = 10000;
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(246, 23);
            this.progressBar1.TabIndex = 19;
            // 
            // groupBoxResults
            // 
            this.groupBoxResults.Controls.Add(this.tbxExemplary);
            this.groupBoxResults.Controls.Add(this.lblExemplary);
            this.groupBoxResults.Controls.Add(this.tbxAvgRtShift);
            this.groupBoxResults.Controls.Add(this.lblAvgRtShift);
            this.groupBoxResults.Controls.Add(this.tbxRejected);
            this.groupBoxResults.Controls.Add(this.lblRejected);
            this.groupBoxResults.Controls.Add(this.tbxAccepted);
            this.groupBoxResults.Controls.Add(this.lblAccepted);
            this.groupBoxResults.Location = new System.Drawing.Point(336, 9);
            this.groupBoxResults.Name = "groupBoxResults";
            this.groupBoxResults.Size = new System.Drawing.Size(209, 183);
            this.groupBoxResults.TabIndex = 18;
            this.groupBoxResults.TabStop = false;
            this.groupBoxResults.Text = "Results";
            // 
            // tbxExemplary
            // 
            this.tbxExemplary.Location = new System.Drawing.Point(6, 39);
            this.tbxExemplary.Name = "tbxExemplary";
            this.tbxExemplary.ReadOnly = true;
            this.tbxExemplary.Size = new System.Drawing.Size(84, 20);
            this.tbxExemplary.TabIndex = 7;
            // 
            // lblExemplary
            // 
            this.lblExemplary.AutoSize = true;
            this.lblExemplary.Location = new System.Drawing.Point(9, 23);
            this.lblExemplary.Name = "lblExemplary";
            this.lblExemplary.Size = new System.Drawing.Size(58, 13);
            this.lblExemplary.TabIndex = 6;
            this.lblExemplary.Text = "Exemplary:";
            // 
            // tbxAvgRtShift
            // 
            this.tbxAvgRtShift.Location = new System.Drawing.Point(6, 156);
            this.tbxAvgRtShift.Name = "tbxAvgRtShift";
            this.tbxAvgRtShift.ReadOnly = true;
            this.tbxAvgRtShift.Size = new System.Drawing.Size(100, 20);
            this.tbxAvgRtShift.TabIndex = 5;
            // 
            // lblAvgRtShift
            // 
            this.lblAvgRtShift.AutoSize = true;
            this.lblAvgRtShift.Location = new System.Drawing.Point(6, 140);
            this.lblAvgRtShift.Name = "lblAvgRtShift";
            this.lblAvgRtShift.Size = new System.Drawing.Size(115, 13);
            this.lblAvgRtShift.TabIndex = 4;
            this.lblAvgRtShift.Text = "Average RT difference";
            // 
            // tbxRejected
            // 
            this.tbxRejected.Location = new System.Drawing.Point(9, 117);
            this.tbxRejected.Name = "tbxRejected";
            this.tbxRejected.ReadOnly = true;
            this.tbxRejected.Size = new System.Drawing.Size(84, 20);
            this.tbxRejected.TabIndex = 3;
            // 
            // lblRejected
            // 
            this.lblRejected.AutoSize = true;
            this.lblRejected.Location = new System.Drawing.Point(9, 101);
            this.lblRejected.Name = "lblRejected";
            this.lblRejected.Size = new System.Drawing.Size(53, 13);
            this.lblRejected.TabIndex = 2;
            this.lblRejected.Text = "Rejected:";
            // 
            // tbxAccepted
            // 
            this.tbxAccepted.Location = new System.Drawing.Point(6, 78);
            this.tbxAccepted.Name = "tbxAccepted";
            this.tbxAccepted.ReadOnly = true;
            this.tbxAccepted.Size = new System.Drawing.Size(84, 20);
            this.tbxAccepted.TabIndex = 1;
            // 
            // lblAccepted
            // 
            this.lblAccepted.AutoSize = true;
            this.lblAccepted.Location = new System.Drawing.Point(9, 62);
            this.lblAccepted.Name = "lblAccepted";
            this.lblAccepted.Size = new System.Drawing.Size(56, 13);
            this.lblAccepted.TabIndex = 0;
            this.lblAccepted.Text = "Accepted:";
            // 
            // btnImputeBoundaries
            // 
            this.btnImputeBoundaries.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnImputeBoundaries.Location = new System.Drawing.Point(656, 12);
            this.btnImputeBoundaries.Name = "btnImputeBoundaries";
            this.btnImputeBoundaries.Size = new System.Drawing.Size(132, 23);
            this.btnImputeBoundaries.TabIndex = 17;
            this.btnImputeBoundaries.Text = "Impute Boundaries";
            this.toolTip1.SetToolTip(this.btnImputeBoundaries, "Choose new peak boundaries for the rejected peaks in the displayed rows");
            this.btnImputeBoundaries.UseVisualStyleBackColor = true;
            this.btnImputeBoundaries.Click += new System.EventHandler(this.btnImputeBoundaries_Click);
            // 
            // cbxOverwriteManual
            // 
            this.cbxOverwriteManual.AutoSize = true;
            this.cbxOverwriteManual.Location = new System.Drawing.Point(181, 106);
            this.cbxOverwriteManual.Name = "cbxOverwriteManual";
            this.cbxOverwriteManual.Size = new System.Drawing.Size(140, 17);
            this.cbxOverwriteManual.TabIndex = 16;
            this.cbxOverwriteManual.Text = "Overwrite manual peaks";
            this.cbxOverwriteManual.UseVisualStyleBackColor = true;
            this.cbxOverwriteManual.CheckedChanged += new System.EventHandler(this.SettingsControlChanged);
            // 
            // tbxRtDeviationCutoff
            // 
            this.tbxRtDeviationCutoff.Location = new System.Drawing.Point(181, 75);
            this.tbxRtDeviationCutoff.Name = "tbxRtDeviationCutoff";
            this.tbxRtDeviationCutoff.Size = new System.Drawing.Size(127, 20);
            this.tbxRtDeviationCutoff.TabIndex = 15;
            this.tbxRtDeviationCutoff.Text = "1";
            this.toolTip1.SetToolTip(this.tbxRtDeviationCutoff, "Peaks whose retention time is less than this distance from the accepted peaks wil" +
        "l also be assumed to be correct.");
            this.tbxRtDeviationCutoff.Leave += new System.EventHandler(this.SettingsControlChanged);
            // 
            // lblSdCutoff
            // 
            this.lblSdCutoff.AutoSize = true;
            this.lblSdCutoff.Location = new System.Drawing.Point(178, 55);
            this.lblSdCutoff.Name = "lblSdCutoff";
            this.lblSdCutoff.Size = new System.Drawing.Size(106, 13);
            this.lblSdCutoff.TabIndex = 14;
            this.lblSdCutoff.Text = "Max RT shift minutes";
            // 
            // groupBoxCutoff
            // 
            this.groupBoxCutoff.Controls.Add(this.lblPercent);
            this.groupBoxCutoff.Controls.Add(this.radioPValue);
            this.groupBoxCutoff.Controls.Add(this.radioPercentile);
            this.groupBoxCutoff.Controls.Add(this.radioQValue);
            this.groupBoxCutoff.Controls.Add(this.radioScore);
            this.groupBoxCutoff.Controls.Add(this.tbxCoreScoreCutoff);
            this.groupBoxCutoff.Location = new System.Drawing.Point(12, 52);
            this.groupBoxCutoff.Name = "groupBoxCutoff";
            this.groupBoxCutoff.Size = new System.Drawing.Size(142, 138);
            this.groupBoxCutoff.TabIndex = 13;
            this.groupBoxCutoff.TabStop = false;
            this.groupBoxCutoff.Text = "Exemplary Cutoff";
            // 
            // lblPercent
            // 
            this.lblPercent.AutoSize = true;
            this.lblPercent.Location = new System.Drawing.Point(118, 116);
            this.lblPercent.Name = "lblPercent";
            this.lblPercent.Size = new System.Drawing.Size(15, 13);
            this.lblPercent.TabIndex = 11;
            this.lblPercent.Text = "%";
            this.lblPercent.Visible = false;
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
            this.radioQValue.Size = new System.Drawing.Size(94, 17);
            this.radioQValue.TabIndex = 1;
            this.radioQValue.TabStop = true;
            this.radioQValue.Text = "Library q-value";
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
            this.tbxCoreScoreCutoff.Size = new System.Drawing.Size(106, 20);
            this.tbxCoreScoreCutoff.TabIndex = 9;
            this.toolTip1.SetToolTip(this.tbxCoreScoreCutoff, resources.GetString("tbxCoreScoreCutoff.ToolTip"));
            this.tbxCoreScoreCutoff.Leave += new System.EventHandler(this.SettingsControlChanged);
            // 
            // comboRetentionTimeAlignment
            // 
            this.comboRetentionTimeAlignment.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRetentionTimeAlignment.FormattingEnabled = true;
            this.comboRetentionTimeAlignment.Location = new System.Drawing.Point(181, 25);
            this.comboRetentionTimeAlignment.Name = "comboRetentionTimeAlignment";
            this.comboRetentionTimeAlignment.Size = new System.Drawing.Size(127, 21);
            this.comboRetentionTimeAlignment.TabIndex = 1;
            this.toolTip1.SetToolTip(this.comboRetentionTimeAlignment, "The retention time alignment setting controls how the times from the accepted pea" +
        "ks are mapped onto the runs where a new peak needs to be chosen.");
            this.comboRetentionTimeAlignment.SelectedIndexChanged += new System.EventHandler(this.SettingsControlChanged);
            // 
            // lblRetentionTimeAlignment
            // 
            this.lblRetentionTimeAlignment.AutoSize = true;
            this.lblRetentionTimeAlignment.Location = new System.Drawing.Point(178, 9);
            this.lblRetentionTimeAlignment.Name = "lblRetentionTimeAlignment";
            this.lblRetentionTimeAlignment.Size = new System.Drawing.Size(126, 13);
            this.lblRetentionTimeAlignment.TabIndex = 0;
            this.lblRetentionTimeAlignment.Text = "Retention time alignment:";
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
            this.groupBoxScope.ResumeLayout(false);
            this.groupBoxScope.PerformLayout();
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
        private System.Windows.Forms.Label lblExemplary;
        private System.Windows.Forms.TextBox tbxExemplary;
        private System.Windows.Forms.TextBox tbxScoringModel;
        private System.Windows.Forms.Label lblScoringModel;
        private System.Windows.Forms.GroupBox groupBoxScope;
        private System.Windows.Forms.RadioButton radioScopeDocument;
        private System.Windows.Forms.RadioButton radioScopeSelection;
        private System.Windows.Forms.Label lblPercent;
    }
}