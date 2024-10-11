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
            this.lblMinutes = new System.Windows.Forms.Label();
            this.groupBoxDocumentStatistics = new System.Windows.Forms.GroupBox();
            this.tbxUnalignedDocRtStdDev = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBoxScope = new System.Windows.Forms.GroupBox();
            this.radioScopeDocument = new System.Windows.Forms.RadioButton();
            this.radioScopeSelection = new System.Windows.Forms.RadioButton();
            this.tbxScoringModel = new System.Windows.Forms.TextBox();
            this.lblScoringModel = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.groupBoxResults = new System.Windows.Forms.GroupBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.lblNeedRemoval = new System.Windows.Forms.Label();
            this.tbxMeanRtStdDev = new System.Windows.Forms.TextBox();
            this.lblMeanRtStdDev = new System.Windows.Forms.Label();
            this.tbxExemplary = new System.Windows.Forms.TextBox();
            this.lblExemplary = new System.Windows.Forms.Label();
            this.tbxRejected = new System.Windows.Forms.TextBox();
            this.lblRejected = new System.Windows.Forms.Label();
            this.tbxAccepted = new System.Windows.Forms.TextBox();
            this.lblAccepted = new System.Windows.Forms.Label();
            this.btnImputeBoundaries = new System.Windows.Forms.Button();
            this.cbxOverwriteManual = new System.Windows.Forms.CheckBox();
            this.tbxRtDeviationCutoff = new System.Windows.Forms.TextBox();
            this.lblSdCutoff = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.panel1.SuspendLayout();
            this.groupBoxDocumentStatistics.SuspendLayout();
            this.groupBoxScope.SuspendLayout();
            this.groupBoxResults.SuspendLayout();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            this.databoundGridControl.Location = new System.Drawing.Point(0, 223);
            this.databoundGridControl.Size = new System.Drawing.Size(800, 227);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.lblMinutes);
            this.panel1.Controls.Add(this.groupBoxDocumentStatistics);
            this.panel1.Controls.Add(this.groupBoxScope);
            this.panel1.Controls.Add(this.tbxScoringModel);
            this.panel1.Controls.Add(this.lblScoringModel);
            this.panel1.Controls.Add(this.progressBar1);
            this.panel1.Controls.Add(this.groupBoxResults);
            this.panel1.Controls.Add(this.btnImputeBoundaries);
            this.panel1.Controls.Add(this.cbxOverwriteManual);
            this.panel1.Controls.Add(this.tbxRtDeviationCutoff);
            this.panel1.Controls.Add(this.lblSdCutoff);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(800, 223);
            this.panel1.TabIndex = 1;
            // 
            // lblMinutes
            // 
            this.lblMinutes.AutoSize = true;
            this.lblMinutes.Location = new System.Drawing.Point(91, 79);
            this.lblMinutes.Name = "lblMinutes";
            this.lblMinutes.Size = new System.Drawing.Size(43, 13);
            this.lblMinutes.TabIndex = 28;
            this.lblMinutes.Text = "minutes";
            // 
            // groupBoxDocumentStatistics
            // 
            this.groupBoxDocumentStatistics.Controls.Add(this.tbxUnalignedDocRtStdDev);
            this.groupBoxDocumentStatistics.Controls.Add(this.label1);
            this.groupBoxDocumentStatistics.Location = new System.Drawing.Point(326, 12);
            this.groupBoxDocumentStatistics.Name = "groupBoxDocumentStatistics";
            this.groupBoxDocumentStatistics.Size = new System.Drawing.Size(164, 179);
            this.groupBoxDocumentStatistics.TabIndex = 24;
            this.groupBoxDocumentStatistics.TabStop = false;
            this.groupBoxDocumentStatistics.Text = "Document-wide statistics";
            // 
            // tbxUnalignedDocRtStdDev
            // 
            this.tbxUnalignedDocRtStdDev.Location = new System.Drawing.Point(9, 50);
            this.tbxUnalignedDocRtStdDev.Name = "tbxUnalignedDocRtStdDev";
            this.tbxUnalignedDocRtStdDev.ReadOnly = true;
            this.tbxUnalignedDocRtStdDev.Size = new System.Drawing.Size(100, 20);
            this.tbxUnalignedDocRtStdDev.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(6, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(144, 39);
            this.label1.TabIndex = 0;
            this.label1.Text = "Average retenion time standard deviation";
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
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(326, 194);
            this.progressBar1.Maximum = 10000;
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(471, 23);
            this.progressBar1.TabIndex = 19;
            // 
            // groupBoxResults
            // 
            this.groupBoxResults.Controls.Add(this.textBox1);
            this.groupBoxResults.Controls.Add(this.lblNeedRemoval);
            this.groupBoxResults.Controls.Add(this.tbxMeanRtStdDev);
            this.groupBoxResults.Controls.Add(this.lblMeanRtStdDev);
            this.groupBoxResults.Controls.Add(this.tbxExemplary);
            this.groupBoxResults.Controls.Add(this.lblExemplary);
            this.groupBoxResults.Controls.Add(this.tbxRejected);
            this.groupBoxResults.Controls.Add(this.lblRejected);
            this.groupBoxResults.Controls.Add(this.tbxAccepted);
            this.groupBoxResults.Controls.Add(this.lblAccepted);
            this.groupBoxResults.Location = new System.Drawing.Point(188, 9);
            this.groupBoxResults.Name = "groupBoxResults";
            this.groupBoxResults.Size = new System.Drawing.Size(121, 208);
            this.groupBoxResults.TabIndex = 18;
            this.groupBoxResults.TabStop = false;
            this.groupBoxResults.Text = "Results";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(6, 149);
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(84, 20);
            this.textBox1.TabIndex = 11;
            // 
            // lblNeedRemoval
            // 
            this.lblNeedRemoval.AutoSize = true;
            this.lblNeedRemoval.Location = new System.Drawing.Point(6, 133);
            this.lblNeedRemoval.Name = "lblNeedRemoval";
            this.lblNeedRemoval.Size = new System.Drawing.Size(76, 13);
            this.lblNeedRemoval.TabIndex = 10;
            this.lblNeedRemoval.Text = "Need removal:";
            // 
            // tbxMeanRtStdDev
            // 
            this.tbxMeanRtStdDev.Location = new System.Drawing.Point(6, 184);
            this.tbxMeanRtStdDev.Name = "tbxMeanRtStdDev";
            this.tbxMeanRtStdDev.ReadOnly = true;
            this.tbxMeanRtStdDev.Size = new System.Drawing.Size(84, 20);
            this.tbxMeanRtStdDev.TabIndex = 9;
            // 
            // lblMeanRtStdDev
            // 
            this.lblMeanRtStdDev.AutoSize = true;
            this.lblMeanRtStdDev.Location = new System.Drawing.Point(6, 168);
            this.lblMeanRtStdDev.Name = "lblMeanRtStdDev";
            this.lblMeanRtStdDev.Size = new System.Drawing.Size(107, 13);
            this.lblMeanRtStdDev.TabIndex = 8;
            this.lblMeanRtStdDev.Text = "Average RT StdDev:";
            // 
            // tbxExemplary
            // 
            this.tbxExemplary.Location = new System.Drawing.Point(6, 32);
            this.tbxExemplary.Name = "tbxExemplary";
            this.tbxExemplary.ReadOnly = true;
            this.tbxExemplary.Size = new System.Drawing.Size(84, 20);
            this.tbxExemplary.TabIndex = 7;
            // 
            // lblExemplary
            // 
            this.lblExemplary.AutoSize = true;
            this.lblExemplary.Location = new System.Drawing.Point(6, 16);
            this.lblExemplary.Name = "lblExemplary";
            this.lblExemplary.Size = new System.Drawing.Size(58, 13);
            this.lblExemplary.TabIndex = 6;
            this.lblExemplary.Text = "Exemplary:";
            // 
            // tbxRejected
            // 
            this.tbxRejected.Location = new System.Drawing.Point(6, 110);
            this.tbxRejected.Name = "tbxRejected";
            this.tbxRejected.ReadOnly = true;
            this.tbxRejected.Size = new System.Drawing.Size(84, 20);
            this.tbxRejected.TabIndex = 3;
            // 
            // lblRejected
            // 
            this.lblRejected.AutoSize = true;
            this.lblRejected.Location = new System.Drawing.Point(6, 94);
            this.lblRejected.Name = "lblRejected";
            this.lblRejected.Size = new System.Drawing.Size(90, 13);
            this.lblRejected.TabIndex = 2;
            this.lblRejected.Text = "Need adjustment:";
            // 
            // tbxAccepted
            // 
            this.tbxAccepted.Location = new System.Drawing.Point(6, 71);
            this.tbxAccepted.Name = "tbxAccepted";
            this.tbxAccepted.ReadOnly = true;
            this.tbxAccepted.Size = new System.Drawing.Size(84, 20);
            this.tbxAccepted.TabIndex = 1;
            // 
            // lblAccepted
            // 
            this.lblAccepted.AutoSize = true;
            this.lblAccepted.Location = new System.Drawing.Point(6, 55);
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
            this.cbxOverwriteManual.Location = new System.Drawing.Point(12, 103);
            this.cbxOverwriteManual.Name = "cbxOverwriteManual";
            this.cbxOverwriteManual.Size = new System.Drawing.Size(140, 17);
            this.cbxOverwriteManual.TabIndex = 16;
            this.cbxOverwriteManual.Text = "Overwrite manual peaks";
            this.cbxOverwriteManual.UseVisualStyleBackColor = true;
            this.cbxOverwriteManual.CheckedChanged += new System.EventHandler(this.SettingsControlChanged);
            // 
            // tbxRtDeviationCutoff
            // 
            this.tbxRtDeviationCutoff.Location = new System.Drawing.Point(15, 76);
            this.tbxRtDeviationCutoff.Name = "tbxRtDeviationCutoff";
            this.tbxRtDeviationCutoff.Size = new System.Drawing.Size(70, 20);
            this.tbxRtDeviationCutoff.TabIndex = 15;
            this.tbxRtDeviationCutoff.Text = "1";
            this.toolTip1.SetToolTip(this.tbxRtDeviationCutoff, "Peaks whose retention time is less than this distance from the accepted peaks wil" +
        "l also be assumed to be correct.");
            this.tbxRtDeviationCutoff.Leave += new System.EventHandler(this.SettingsControlChanged);
            // 
            // lblSdCutoff
            // 
            this.lblSdCutoff.AutoSize = true;
            this.lblSdCutoff.Location = new System.Drawing.Point(12, 59);
            this.lblSdCutoff.Name = "lblSdCutoff";
            this.lblSdCutoff.Size = new System.Drawing.Size(67, 13);
            this.lblSdCutoff.TabIndex = 14;
            this.lblSdCutoff.Text = "Max RT shift";
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
            this.groupBoxDocumentStatistics.ResumeLayout(false);
            this.groupBoxDocumentStatistics.PerformLayout();
            this.groupBoxScope.ResumeLayout(false);
            this.groupBoxScope.PerformLayout();
            this.groupBoxResults.ResumeLayout(false);
            this.groupBoxResults.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TextBox tbxRtDeviationCutoff;
        private System.Windows.Forms.Label lblSdCutoff;
        private System.Windows.Forms.CheckBox cbxOverwriteManual;
        private System.Windows.Forms.Button btnImputeBoundaries;
        private System.Windows.Forms.GroupBox groupBoxResults;
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
        private System.Windows.Forms.GroupBox groupBoxDocumentStatistics;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxUnalignedDocRtStdDev;
        private System.Windows.Forms.TextBox tbxMeanRtStdDev;
        private System.Windows.Forms.Label lblMeanRtStdDev;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label lblNeedRemoval;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.Label lblMinutes;
    }
}