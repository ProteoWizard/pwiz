namespace pwiz.Topograph.ui.Forms
{
    partial class DisplaySettingsForm
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
            this.cbxPeaksVerticalLines = new System.Windows.Forms.CheckBox();
            this.cbxPeaksHorizontalLines = new System.Windows.Forms.CheckBox();
            this.cbxSmoothChromatograms = new System.Windows.Forms.CheckBox();
            this.cbxShowDeconvolutionScore = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxFileMruLength = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxChromatogramLineWidth = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // cbxPeaksVerticalLines
            // 
            this.cbxPeaksVerticalLines.AutoSize = true;
            this.cbxPeaksVerticalLines.Location = new System.Drawing.Point(23, 24);
            this.cbxPeaksVerticalLines.Name = "cbxPeaksVerticalLines";
            this.cbxPeaksVerticalLines.Size = new System.Drawing.Size(167, 17);
            this.cbxPeaksVerticalLines.TabIndex = 0;
            this.cbxPeaksVerticalLines.Text = "Display peaks as vertical lines";
            this.cbxPeaksVerticalLines.UseVisualStyleBackColor = true;
            // 
            // cbxPeaksHorizontalLines
            // 
            this.cbxPeaksHorizontalLines.AutoSize = true;
            this.cbxPeaksHorizontalLines.Location = new System.Drawing.Point(269, 21);
            this.cbxPeaksHorizontalLines.Name = "cbxPeaksHorizontalLines";
            this.cbxPeaksHorizontalLines.Size = new System.Drawing.Size(178, 17);
            this.cbxPeaksHorizontalLines.TabIndex = 1;
            this.cbxPeaksHorizontalLines.Text = "Display peaks as horizontal lines";
            this.cbxPeaksHorizontalLines.UseVisualStyleBackColor = true;
            // 
            // cbxSmoothChromatograms
            // 
            this.cbxSmoothChromatograms.AutoSize = true;
            this.cbxSmoothChromatograms.Location = new System.Drawing.Point(25, 61);
            this.cbxSmoothChromatograms.Name = "cbxSmoothChromatograms";
            this.cbxSmoothChromatograms.Size = new System.Drawing.Size(138, 17);
            this.cbxSmoothChromatograms.TabIndex = 2;
            this.cbxSmoothChromatograms.Text = "Smooth Chromatograms";
            this.cbxSmoothChromatograms.UseVisualStyleBackColor = true;
            // 
            // cbxShowDeconvolutionScore
            // 
            this.cbxShowDeconvolutionScore.AutoSize = true;
            this.cbxShowDeconvolutionScore.Location = new System.Drawing.Point(273, 58);
            this.cbxShowDeconvolutionScore.Name = "cbxShowDeconvolutionScore";
            this.cbxShowDeconvolutionScore.Size = new System.Drawing.Size(156, 17);
            this.cbxShowDeconvolutionScore.TabIndex = 3;
            this.cbxShowDeconvolutionScore.Text = "Show Deconvolution Score";
            this.cbxShowDeconvolutionScore.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(23, 92);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(187, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "# of recent files to display on file menu";
            // 
            // tbxFileMruLength
            // 
            this.tbxFileMruLength.Location = new System.Drawing.Point(275, 93);
            this.tbxFileMruLength.Name = "tbxFileMruLength";
            this.tbxFileMruLength.Size = new System.Drawing.Size(100, 20);
            this.tbxFileMruLength.TabIndex = 5;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(422, 232);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 6;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOkOnClick);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(521, 230);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(23, 122);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(129, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "Chromatogram Line Width";
            // 
            // tbxChromatogramLineWidth
            // 
            this.tbxChromatogramLineWidth.Location = new System.Drawing.Point(279, 122);
            this.tbxChromatogramLineWidth.Name = "tbxChromatogramLineWidth";
            this.tbxChromatogramLineWidth.Size = new System.Drawing.Size(100, 20);
            this.tbxChromatogramLineWidth.TabIndex = 9;
            // 
            // DisplaySettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(609, 262);
            this.Controls.Add(this.tbxChromatogramLineWidth);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tbxFileMruLength);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbxShowDeconvolutionScore);
            this.Controls.Add(this.cbxSmoothChromatograms);
            this.Controls.Add(this.cbxPeaksHorizontalLines);
            this.Controls.Add(this.cbxPeaksVerticalLines);
            this.Name = "DisplaySettingsForm";
            this.Text = "DefaultSettingsForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbxPeaksVerticalLines;
        private System.Windows.Forms.CheckBox cbxPeaksHorizontalLines;
        private System.Windows.Forms.CheckBox cbxSmoothChromatograms;
        private System.Windows.Forms.CheckBox cbxShowDeconvolutionScore;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxFileMruLength;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxChromatogramLineWidth;
    }
}