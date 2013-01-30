namespace pwiz.Topograph.ui.Forms
{
    partial class MiscSettingsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MiscSettingsForm));
            this.label1 = new System.Windows.Forms.Label();
            this.tbxMassAccuracy = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.tbxProteinDescriptionKey = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbxMaxRetentionTimeShift = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxMinCorrelationCoefficient = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.tbxMinDeconvolutionScoreForAvgPrecursorPool = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(236, 184);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(83, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Mass Accuracy:";
            // 
            // tbxMassAccuracy
            // 
            this.tbxMassAccuracy.Location = new System.Drawing.Point(337, 184);
            this.tbxMassAccuracy.Name = "tbxMassAccuracy";
            this.tbxMassAccuracy.Size = new System.Drawing.Size(144, 20);
            this.tbxMassAccuracy.TabIndex = 1;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(366, 529);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(76, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOkOnClick);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(448, 529);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(67, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.BtnCancelOnClick);
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(20, 86);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(503, 85);
            this.label2.TabIndex = 3;
            this.label2.Text = resources.GetString("label2.Text");
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(22, 316);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(501, 42);
            this.label5.TabIndex = 10;
            this.label5.Text = "What part of the protein description is most useful?  Enter a regular expression." +
                "  For example, for the CG Number enter: CG[0-9]*";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(169, 358);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(150, 13);
            this.label7.TabIndex = 11;
            this.label7.Text = "Key part of protein description:";
            // 
            // tbxProteinDescriptionKey
            // 
            this.tbxProteinDescriptionKey.Location = new System.Drawing.Point(337, 355);
            this.tbxProteinDescriptionKey.Name = "tbxProteinDescriptionKey";
            this.tbxProteinDescriptionKey.Size = new System.Drawing.Size(144, 20);
            this.tbxProteinDescriptionKey.TabIndex = 12;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(29, 383);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(234, 13);
            this.label3.TabIndex = 13;
            this.label3.Text = "Maximum retention time per labeled amino acids:";
            // 
            // tbxMaxRetentionTimeShift
            // 
            this.tbxMaxRetentionTimeShift.Location = new System.Drawing.Point(337, 383);
            this.tbxMaxRetentionTimeShift.Name = "tbxMaxRetentionTimeShift";
            this.tbxMaxRetentionTimeShift.Size = new System.Drawing.Size(144, 20);
            this.tbxMaxRetentionTimeShift.TabIndex = 14;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(33, 412);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(349, 13);
            this.label4.TabIndex = 15;
            this.label4.Text = "Minimum acceptable correlation coefficient when finding matching peaks";
            // 
            // tbxMinCorrelationCoefficient
            // 
            this.tbxMinCorrelationCoefficient.Location = new System.Drawing.Point(337, 433);
            this.tbxMinCorrelationCoefficient.Name = "tbxMinCorrelationCoefficient";
            this.tbxMinCorrelationCoefficient.Size = new System.Drawing.Size(144, 20);
            this.tbxMinCorrelationCoefficient.TabIndex = 16;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(33, 220);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(444, 13);
            this.label6.TabIndex = 17;
            this.label6.Text = "Minimum Deconvolution Score for peptides to be used in average precursor pool cal" +
                "culations";
            // 
            // tbxMinDeconvolutionScoreForAvgPrecursorPool
            // 
            this.tbxMinDeconvolutionScoreForAvgPrecursorPool.Location = new System.Drawing.Point(339, 254);
            this.tbxMinDeconvolutionScoreForAvgPrecursorPool.Name = "tbxMinDeconvolutionScoreForAvgPrecursorPool";
            this.tbxMinDeconvolutionScoreForAvgPrecursorPool.Size = new System.Drawing.Size(138, 20);
            this.tbxMinDeconvolutionScoreForAvgPrecursorPool.TabIndex = 18;
            // 
            // MiscSettingsForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(534, 564);
            this.Controls.Add(this.tbxMinDeconvolutionScoreForAvgPrecursorPool);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.tbxMinCorrelationCoefficient);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.tbxMaxRetentionTimeShift);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.tbxProteinDescriptionKey);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tbxMassAccuracy);
            this.Controls.Add(this.btnOK);
            this.Name = "MiscSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "MachineSettingsForm";
            this.Text = "Miscellaneous Settings";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxMassAccuracy;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox tbxProteinDescriptionKey;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbxMaxRetentionTimeShift;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxMinCorrelationCoefficient;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox tbxMinDeconvolutionScoreForAvgPrecursorPool;
    }
}