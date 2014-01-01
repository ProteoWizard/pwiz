namespace pwiz.Topograph.ui.Controls
{
    partial class HalfLifeSettingsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panelTitle = new System.Windows.Forms.Panel();
            this.imgExpandCollapse = new System.Windows.Forms.PictureBox();
            this.lblTitle = new System.Windows.Forms.Label();
            this.panelContent = new System.Windows.Forms.Panel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.cbxForceThroughOrigin = new System.Windows.Forms.CheckBox();
            this.cbxSimpleLinearRegression = new System.Windows.Forms.CheckBox();
            this.groupBoxAcceptanceCriteria = new System.Windows.Forms.GroupBox();
            this.comboEvviesFilter = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.tbxMinTurnoverScore = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.tbxMinimumDeconvolutionScore = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxMinAuc = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioIndividualPrecursorPool = new System.Windows.Forms.RadioButton();
            this.radioUseMedianPrecursorPool = new System.Windows.Forms.RadioButton();
            this.tbxCurrentPrecursorPool = new System.Windows.Forms.TextBox();
            this.radioFixedPrecursorPool = new System.Windows.Forms.RadioButton();
            this.label11 = new System.Windows.Forms.Label();
            this.tbxInitialPrecursorPool = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.groupBoxNewlySynthesizedCalculation = new System.Windows.Forms.GroupBox();
            this.radioUnlabeledPeptide = new System.Windows.Forms.RadioButton();
            this.radioLabelDistribution = new System.Windows.Forms.RadioButton();
            this.radioLabeledAminoAcid = new System.Windows.Forms.RadioButton();
            this.label8 = new System.Windows.Forms.Label();
            this.panelTitle.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imgExpandCollapse)).BeginInit();
            this.panelContent.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBoxAcceptanceCriteria.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBoxNewlySynthesizedCalculation.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelTitle
            // 
            this.panelTitle.BackColor = System.Drawing.SystemColors.ControlLight;
            this.panelTitle.Controls.Add(this.imgExpandCollapse);
            this.panelTitle.Controls.Add(this.lblTitle);
            this.panelTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTitle.Location = new System.Drawing.Point(0, 0);
            this.panelTitle.Name = "panelTitle";
            this.panelTitle.Size = new System.Drawing.Size(873, 20);
            this.panelTitle.TabIndex = 0;
            this.panelTitle.Click += new System.EventHandler(this.ImgExpandCollapseOnClick);
            // 
            // imgExpandCollapse
            // 
            this.imgExpandCollapse.Cursor = System.Windows.Forms.Cursors.Hand;
            this.imgExpandCollapse.Image = global::pwiz.Topograph.ui.Properties.Resources.Expand;
            this.imgExpandCollapse.Location = new System.Drawing.Point(3, 3);
            this.imgExpandCollapse.Name = "imgExpandCollapse";
            this.imgExpandCollapse.Size = new System.Drawing.Size(13, 13);
            this.imgExpandCollapse.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.imgExpandCollapse.TabIndex = 1;
            this.imgExpandCollapse.TabStop = false;
            this.imgExpandCollapse.Click += new System.EventHandler(this.ImgExpandCollapseOnClick);
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.Location = new System.Drawing.Point(23, 4);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(172, 13);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Half Life Calculation Settings";
            this.lblTitle.Click += new System.EventHandler(this.ImgExpandCollapseOnClick);
            // 
            // panelContent
            // 
            this.panelContent.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panelContent.Controls.Add(this.groupBox2);
            this.panelContent.Controls.Add(this.groupBoxAcceptanceCriteria);
            this.panelContent.Controls.Add(this.groupBox1);
            this.panelContent.Controls.Add(this.groupBoxNewlySynthesizedCalculation);
            this.panelContent.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelContent.Location = new System.Drawing.Point(0, 20);
            this.panelContent.Name = "panelContent";
            this.panelContent.Size = new System.Drawing.Size(873, 471);
            this.panelContent.TabIndex = 1;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.cbxForceThroughOrigin);
            this.groupBox2.Controls.Add(this.cbxSimpleLinearRegression);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox2.Location = new System.Drawing.Point(0, 362);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(869, 48);
            this.groupBox2.TabIndex = 3;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Curve Fitting";
            // 
            // cbxForceThroughOrigin
            // 
            this.cbxForceThroughOrigin.AutoSize = true;
            this.cbxForceThroughOrigin.Location = new System.Drawing.Point(12, 19);
            this.cbxForceThroughOrigin.Name = "cbxForceThroughOrigin";
            this.cbxForceThroughOrigin.Size = new System.Drawing.Size(150, 17);
            this.cbxForceThroughOrigin.TabIndex = 0;
            this.cbxForceThroughOrigin.Text = "Force curve through origin";
            this.cbxForceThroughOrigin.UseVisualStyleBackColor = true;
            this.cbxForceThroughOrigin.CheckedChanged += new System.EventHandler(this.UpdateSettings);
            // 
            // cbxSimpleLinearRegression
            // 
            this.cbxSimpleLinearRegression.AutoSize = true;
            this.cbxSimpleLinearRegression.Location = new System.Drawing.Point(168, 19);
            this.cbxSimpleLinearRegression.Name = "cbxSimpleLinearRegression";
            this.cbxSimpleLinearRegression.Size = new System.Drawing.Size(283, 17);
            this.cbxSimpleLinearRegression.TabIndex = 1;
            this.cbxSimpleLinearRegression.Text = "Simple Linear Regression with 95% confidence interval";
            this.cbxSimpleLinearRegression.UseVisualStyleBackColor = true;
            this.cbxSimpleLinearRegression.CheckedChanged += new System.EventHandler(this.UpdateSettings);
            // 
            // groupBoxAcceptanceCriteria
            // 
            this.groupBoxAcceptanceCriteria.Controls.Add(this.comboEvviesFilter);
            this.groupBoxAcceptanceCriteria.Controls.Add(this.label7);
            this.groupBoxAcceptanceCriteria.Controls.Add(this.tbxMinTurnoverScore);
            this.groupBoxAcceptanceCriteria.Controls.Add(this.label6);
            this.groupBoxAcceptanceCriteria.Controls.Add(this.tbxMinimumDeconvolutionScore);
            this.groupBoxAcceptanceCriteria.Controls.Add(this.label5);
            this.groupBoxAcceptanceCriteria.Controls.Add(this.tbxMinAuc);
            this.groupBoxAcceptanceCriteria.Controls.Add(this.label4);
            this.groupBoxAcceptanceCriteria.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBoxAcceptanceCriteria.Location = new System.Drawing.Point(0, 248);
            this.groupBoxAcceptanceCriteria.Name = "groupBoxAcceptanceCriteria";
            this.groupBoxAcceptanceCriteria.Size = new System.Drawing.Size(869, 114);
            this.groupBoxAcceptanceCriteria.TabIndex = 2;
            this.groupBoxAcceptanceCriteria.TabStop = false;
            this.groupBoxAcceptanceCriteria.Text = "Acceptance Criteria";
            // 
            // comboEvviesFilter
            // 
            this.comboEvviesFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEvviesFilter.FormattingEnabled = true;
            this.comboEvviesFilter.Location = new System.Drawing.Point(180, 90);
            this.comboEvviesFilter.Name = "comboEvviesFilter";
            this.comboEvviesFilter.Size = new System.Drawing.Size(205, 21);
            this.comboEvviesFilter.TabIndex = 7;
            this.comboEvviesFilter.SelectedIndexChanged += new System.EventHandler(this.UpdateSettings);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(11, 98);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(119, 13);
            this.label7.TabIndex = 6;
            this.label7.Text = "Outlier Filter (experimental):";
            // 
            // tbxMinTurnoverScore
            // 
            this.tbxMinTurnoverScore.Location = new System.Drawing.Point(180, 70);
            this.tbxMinTurnoverScore.Name = "tbxMinTurnoverScore";
            this.tbxMinTurnoverScore.Size = new System.Drawing.Size(205, 20);
            this.tbxMinTurnoverScore.TabIndex = 5;
            this.tbxMinTurnoverScore.Leave += new System.EventHandler(this.UpdateSettings);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(11, 71);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(125, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "Minimum Turnover Score";
            // 
            // tbxMinimumDeconvolutionScore
            // 
            this.tbxMinimumDeconvolutionScore.Location = new System.Drawing.Point(183, 44);
            this.tbxMinimumDeconvolutionScore.Name = "tbxMinimumDeconvolutionScore";
            this.tbxMinimumDeconvolutionScore.Size = new System.Drawing.Size(202, 20);
            this.tbxMinimumDeconvolutionScore.TabIndex = 3;
            this.tbxMinimumDeconvolutionScore.Leave += new System.EventHandler(this.UpdateSettings);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(11, 44);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(151, 13);
            this.label5.TabIndex = 2;
            this.label5.Text = "Minimum Deconvolution Score";
            // 
            // tbxMinAuc
            // 
            this.tbxMinAuc.Location = new System.Drawing.Point(183, 18);
            this.tbxMinAuc.Name = "tbxMinAuc";
            this.tbxMinAuc.Size = new System.Drawing.Size(202, 20);
            this.tbxMinAuc.TabIndex = 1;
            this.tbxMinAuc.Leave += new System.EventHandler(this.UpdateSettings);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(11, 21);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(90, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "Minimum Intensity";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioIndividualPrecursorPool);
            this.groupBox1.Controls.Add(this.radioUseMedianPrecursorPool);
            this.groupBox1.Controls.Add(this.tbxCurrentPrecursorPool);
            this.groupBox1.Controls.Add(this.radioFixedPrecursorPool);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Controls.Add(this.tbxInitialPrecursorPool);
            this.groupBox1.Controls.Add(this.label10);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(0, 98);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(869, 150);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Precursor Pools";
            // 
            // radioIndividualPrecursorPool
            // 
            this.radioIndividualPrecursorPool.AutoSize = true;
            this.radioIndividualPrecursorPool.Location = new System.Drawing.Point(16, 121);
            this.radioIndividualPrecursorPool.Name = "radioIndividualPrecursorPool";
            this.radioIndividualPrecursorPool.Size = new System.Drawing.Size(585, 17);
            this.radioIndividualPrecursorPool.TabIndex = 7;
            this.radioIndividualPrecursorPool.TabStop = true;
            this.radioIndividualPrecursorPool.Text = "Calculate a different precursor pool for each peptide.  Only include peptides wit" +
    "h multiple potentially labeled amino acids";
            this.radioIndividualPrecursorPool.UseVisualStyleBackColor = true;
            this.radioIndividualPrecursorPool.CheckedChanged += new System.EventHandler(this.UpdateSettings);
            // 
            // radioUseMedianPrecursorPool
            // 
            this.radioUseMedianPrecursorPool.AutoSize = true;
            this.radioUseMedianPrecursorPool.Location = new System.Drawing.Point(17, 98);
            this.radioUseMedianPrecursorPool.Name = "radioUseMedianPrecursorPool";
            this.radioUseMedianPrecursorPool.Size = new System.Drawing.Size(556, 17);
            this.radioUseMedianPrecursorPool.TabIndex = 6;
            this.radioUseMedianPrecursorPool.TabStop = true;
            this.radioUseMedianPrecursorPool.Text = "Use the median percursor pool computed for all peptides in the sample with multip" +
    "le potentially labeled amino acid";
            this.radioUseMedianPrecursorPool.UseVisualStyleBackColor = true;
            this.radioUseMedianPrecursorPool.CheckedChanged += new System.EventHandler(this.UpdateSettings);
            // 
            // tbxCurrentPrecursorPool
            // 
            this.tbxCurrentPrecursorPool.Location = new System.Drawing.Point(152, 74);
            this.tbxCurrentPrecursorPool.Name = "tbxCurrentPrecursorPool";
            this.tbxCurrentPrecursorPool.Size = new System.Drawing.Size(100, 20);
            this.tbxCurrentPrecursorPool.TabIndex = 5;
            this.tbxCurrentPrecursorPool.Leave += new System.EventHandler(this.UpdateSettings);
            // 
            // radioFixedPrecursorPool
            // 
            this.radioFixedPrecursorPool.AutoSize = true;
            this.radioFixedPrecursorPool.Location = new System.Drawing.Point(17, 75);
            this.radioFixedPrecursorPool.Name = "radioFixedPrecursorPool";
            this.radioFixedPrecursorPool.Size = new System.Drawing.Size(129, 17);
            this.radioFixedPrecursorPool.TabIndex = 4;
            this.radioFixedPrecursorPool.TabStop = true;
            this.radioFixedPrecursorPool.Text = "Always use this value:";
            this.radioFixedPrecursorPool.UseVisualStyleBackColor = true;
            this.radioFixedPrecursorPool.CheckedChanged += new System.EventHandler(this.UpdateSettings);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(13, 59);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(349, 13);
            this.label11.TabIndex = 3;
            this.label11.Text = "How should Topograph determine the precursor pool at later time points?";
            // 
            // tbxInitialPrecursorPool
            // 
            this.tbxInitialPrecursorPool.Location = new System.Drawing.Point(198, 36);
            this.tbxInitialPrecursorPool.Name = "tbxInitialPrecursorPool";
            this.tbxInitialPrecursorPool.Size = new System.Drawing.Size(100, 20);
            this.tbxInitialPrecursorPool.TabIndex = 2;
            this.tbxInitialPrecursorPool.Leave += new System.EventHandler(this.UpdateSettings);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(10, 40);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(182, 13);
            this.label10.TabIndex = 1;
            this.label10.Text = "Percent of label at start of experiment";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(13, 20);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(689, 13);
            this.label9.TabIndex = 0;
            this.label9.Text = "In order to calculate the fraction of peptide that is newly synthesized, Topograp" +
    "h needs to know the fraction of free amino acids that were labeled";
            // 
            // groupBoxNewlySynthesizedCalculation
            // 
            this.groupBoxNewlySynthesizedCalculation.Controls.Add(this.radioUnlabeledPeptide);
            this.groupBoxNewlySynthesizedCalculation.Controls.Add(this.radioLabelDistribution);
            this.groupBoxNewlySynthesizedCalculation.Controls.Add(this.radioLabeledAminoAcid);
            this.groupBoxNewlySynthesizedCalculation.Controls.Add(this.label8);
            this.groupBoxNewlySynthesizedCalculation.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBoxNewlySynthesizedCalculation.Location = new System.Drawing.Point(0, 0);
            this.groupBoxNewlySynthesizedCalculation.Name = "groupBoxNewlySynthesizedCalculation";
            this.groupBoxNewlySynthesizedCalculation.Size = new System.Drawing.Size(869, 98);
            this.groupBoxNewlySynthesizedCalculation.TabIndex = 0;
            this.groupBoxNewlySynthesizedCalculation.TabStop = false;
            this.groupBoxNewlySynthesizedCalculation.Text = "Calculating % Newly Synthesized";
            // 
            // radioUnlabeledPeptide
            // 
            this.radioUnlabeledPeptide.AutoSize = true;
            this.radioUnlabeledPeptide.Location = new System.Drawing.Point(14, 78);
            this.radioUnlabeledPeptide.Name = "radioUnlabeledPeptide";
            this.radioUnlabeledPeptide.Size = new System.Drawing.Size(186, 17);
            this.radioUnlabeledPeptide.TabIndex = 3;
            this.radioUnlabeledPeptide.TabStop = true;
            this.radioUnlabeledPeptide.Text = "The fraction of unlabeled peptides";
            this.radioUnlabeledPeptide.UseVisualStyleBackColor = true;
            this.radioUnlabeledPeptide.CheckedChanged += new System.EventHandler(this.UpdateSettings);
            // 
            // radioLabelDistribution
            // 
            this.radioLabelDistribution.AutoSize = true;
            this.radioLabelDistribution.Location = new System.Drawing.Point(14, 55);
            this.radioLabelDistribution.Name = "radioLabelDistribution";
            this.radioLabelDistribution.Size = new System.Drawing.Size(361, 17);
            this.radioLabelDistribution.TabIndex = 2;
            this.radioLabelDistribution.TabStop = true;
            this.radioLabelDistribution.Text = "The distribution of unlabeled, partially labeled, and fully labeled peptides";
            this.radioLabelDistribution.UseVisualStyleBackColor = true;
            this.radioLabelDistribution.CheckedChanged += new System.EventHandler(this.UpdateSettings);
            // 
            // radioLabeledAminoAcid
            // 
            this.radioLabeledAminoAcid.AutoSize = true;
            this.radioLabeledAminoAcid.Location = new System.Drawing.Point(14, 32);
            this.radioLabeledAminoAcid.Name = "radioLabeledAminoAcid";
            this.radioLabeledAminoAcid.Size = new System.Drawing.Size(296, 17);
            this.radioLabeledAminoAcid.TabIndex = 1;
            this.radioLabeledAminoAcid.TabStop = true;
            this.radioLabeledAminoAcid.Text = "The fraction of amino acids in the peptide that are labeled";
            this.radioLabeledAminoAcid.UseVisualStyleBackColor = true;
            this.radioLabeledAminoAcid.CheckedChanged += new System.EventHandler(this.UpdateSettings);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(11, 16);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(494, 13);
            this.label8.TabIndex = 0;
            this.label8.Text = "Which quantity should Topograph use to calculate the fraction of the peptide that" +
    " is newly synthesized?";
            // 
            // HalfLifeSettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelContent);
            this.Controls.Add(this.panelTitle);
            this.Name = "HalfLifeSettingsControl";
            this.Size = new System.Drawing.Size(873, 568);
            this.panelTitle.ResumeLayout(false);
            this.panelTitle.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imgExpandCollapse)).EndInit();
            this.panelContent.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBoxAcceptanceCriteria.ResumeLayout(false);
            this.groupBoxAcceptanceCriteria.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBoxNewlySynthesizedCalculation.ResumeLayout(false);
            this.groupBoxNewlySynthesizedCalculation.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelTitle;
        private System.Windows.Forms.PictureBox imgExpandCollapse;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.CheckBox cbxForceThroughOrigin;
        private System.Windows.Forms.GroupBox groupBoxAcceptanceCriteria;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxMinAuc;
        private System.Windows.Forms.TextBox tbxMinTurnoverScore;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox tbxMinimumDeconvolutionScore;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox cbxSimpleLinearRegression;
        private System.Windows.Forms.ComboBox comboEvviesFilter;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.GroupBox groupBoxNewlySynthesizedCalculation;
        private System.Windows.Forms.RadioButton radioLabelDistribution;
        private System.Windows.Forms.RadioButton radioLabeledAminoAcid;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.RadioButton radioUnlabeledPeptide;
        private System.Windows.Forms.RadioButton radioIndividualPrecursorPool;
        private System.Windows.Forms.RadioButton radioUseMedianPrecursorPool;
        private System.Windows.Forms.TextBox tbxCurrentPrecursorPool;
        private System.Windows.Forms.RadioButton radioFixedPrecursorPool;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox tbxInitialPrecursorPool;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.GroupBox groupBox2;


    }
}
