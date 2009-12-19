namespace pwiz.Skyline.EditUI
{
    partial class RefineDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RefineDlg));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabDocument = new System.Windows.Forms.TabPage();
            this.cbRemoveRepeatedPeptides = new System.Windows.Forms.CheckBox();
            this.cbRemoveDuplicatePeptides = new System.Windows.Forms.CheckBox();
            this.cbRemoveHeavy = new System.Windows.Forms.CheckBox();
            this.textMinTransitions = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textMinPeptides = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tabResults = new System.Windows.Forms.TabPage();
            this.textMaxPeakFoundRatio = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.radioRemoveMissing = new System.Windows.Forms.RadioButton();
            this.radioIgnoreMissing = new System.Windows.Forms.RadioButton();
            this.textMinPeakFoundRatio = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.groupLibCorr = new System.Windows.Forms.GroupBox();
            this.textMinDotProduct = new System.Windows.Forms.TextBox();
            this.labelMinDotProduct = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textRTRegressionThreshold = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tabControl1.SuspendLayout();
            this.tabDocument.SuspendLayout();
            this.tabResults.SuspendLayout();
            this.groupLibCorr.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabDocument);
            this.tabControl1.Controls.Add(this.tabResults);
            this.tabControl1.Location = new System.Drawing.Point(13, 13);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(395, 360);
            this.tabControl1.TabIndex = 0;
            // 
            // tabDocument
            // 
            this.tabDocument.Controls.Add(this.cbRemoveRepeatedPeptides);
            this.tabDocument.Controls.Add(this.cbRemoveDuplicatePeptides);
            this.tabDocument.Controls.Add(this.cbRemoveHeavy);
            this.tabDocument.Controls.Add(this.textMinTransitions);
            this.tabDocument.Controls.Add(this.label2);
            this.tabDocument.Controls.Add(this.textMinPeptides);
            this.tabDocument.Controls.Add(this.label1);
            this.tabDocument.Location = new System.Drawing.Point(4, 22);
            this.tabDocument.Name = "tabDocument";
            this.tabDocument.Padding = new System.Windows.Forms.Padding(3);
            this.tabDocument.Size = new System.Drawing.Size(387, 334);
            this.tabDocument.TabIndex = 0;
            this.tabDocument.Text = "Document";
            this.tabDocument.UseVisualStyleBackColor = true;
            // 
            // cbRemoveRepeatedPeptides
            // 
            this.cbRemoveRepeatedPeptides.AutoSize = true;
            this.cbRemoveRepeatedPeptides.Location = new System.Drawing.Point(205, 115);
            this.cbRemoveRepeatedPeptides.Name = "cbRemoveRepeatedPeptides";
            this.cbRemoveRepeatedPeptides.Size = new System.Drawing.Size(154, 17);
            this.cbRemoveRepeatedPeptides.TabIndex = 3;
            this.cbRemoveRepeatedPeptides.Text = "Remove repeated peptides";
            this.cbRemoveRepeatedPeptides.UseVisualStyleBackColor = true;
            // 
            // cbRemoveDuplicatePeptides
            // 
            this.cbRemoveDuplicatePeptides.AutoSize = true;
            this.cbRemoveDuplicatePeptides.Location = new System.Drawing.Point(19, 115);
            this.cbRemoveDuplicatePeptides.Name = "cbRemoveDuplicatePeptides";
            this.cbRemoveDuplicatePeptides.Size = new System.Drawing.Size(155, 17);
            this.cbRemoveDuplicatePeptides.TabIndex = 2;
            this.cbRemoveDuplicatePeptides.Text = "Remove duplicate peptides";
            this.cbRemoveDuplicatePeptides.UseVisualStyleBackColor = true;
            // 
            // cbRemoveHeavy
            // 
            this.cbRemoveHeavy.AutoSize = true;
            this.cbRemoveHeavy.Location = new System.Drawing.Point(19, 249);
            this.cbRemoveHeavy.Name = "cbRemoveHeavy";
            this.cbRemoveHeavy.Size = new System.Drawing.Size(194, 17);
            this.cbRemoveHeavy.TabIndex = 6;
            this.cbRemoveHeavy.Text = "Remove heavy precursors with light";
            this.cbRemoveHeavy.UseVisualStyleBackColor = true;
            // 
            // textMinTransitions
            // 
            this.textMinTransitions.Location = new System.Drawing.Point(19, 204);
            this.textMinTransitions.Name = "textMinTransitions";
            this.textMinTransitions.Size = new System.Drawing.Size(65, 20);
            this.textMinTransitions.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 187);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(142, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Min transitions per precursor:";
            // 
            // textMinPeptides
            // 
            this.textMinPeptides.Location = new System.Drawing.Point(19, 43);
            this.textMinPeptides.Name = "textMinPeptides";
            this.textMinPeptides.Size = new System.Drawing.Size(65, 20);
            this.textMinPeptides.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(123, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Min peptides per protein:";
            // 
            // tabResults
            // 
            this.tabResults.Controls.Add(this.textMaxPeakFoundRatio);
            this.tabResults.Controls.Add(this.label6);
            this.tabResults.Controls.Add(this.radioRemoveMissing);
            this.tabResults.Controls.Add(this.radioIgnoreMissing);
            this.tabResults.Controls.Add(this.textMinPeakFoundRatio);
            this.tabResults.Controls.Add(this.label5);
            this.tabResults.Controls.Add(this.groupLibCorr);
            this.tabResults.Controls.Add(this.groupBox1);
            this.tabResults.Location = new System.Drawing.Point(4, 22);
            this.tabResults.Name = "tabResults";
            this.tabResults.Padding = new System.Windows.Forms.Padding(3);
            this.tabResults.Size = new System.Drawing.Size(387, 334);
            this.tabResults.TabIndex = 1;
            this.tabResults.Text = "Results";
            this.tabResults.UseVisualStyleBackColor = true;
            // 
            // textMaxPeakFoundRatio
            // 
            this.textMaxPeakFoundRatio.Location = new System.Drawing.Point(171, 41);
            this.textMaxPeakFoundRatio.Name = "textMaxPeakFoundRatio";
            this.textMaxPeakFoundRatio.Size = new System.Drawing.Size(65, 20);
            this.textMaxPeakFoundRatio.TabIndex = 3;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(168, 25);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(110, 13);
            this.label6.TabIndex = 2;
            this.label6.Text = "Max peak found ratio:";
            // 
            // radioRemoveMissing
            // 
            this.radioRemoveMissing.AutoSize = true;
            this.radioRemoveMissing.Location = new System.Drawing.Point(25, 101);
            this.radioRemoveMissing.Name = "radioRemoveMissing";
            this.radioRemoveMissing.Size = new System.Drawing.Size(167, 17);
            this.radioRemoveMissing.TabIndex = 5;
            this.radioRemoveMissing.TabStop = true;
            this.radioRemoveMissing.Text = "Remove nodes missing results";
            this.radioRemoveMissing.UseVisualStyleBackColor = true;
            // 
            // radioIgnoreMissing
            // 
            this.radioIgnoreMissing.AutoSize = true;
            this.radioIgnoreMissing.Location = new System.Drawing.Point(25, 78);
            this.radioIgnoreMissing.Name = "radioIgnoreMissing";
            this.radioIgnoreMissing.Size = new System.Drawing.Size(157, 17);
            this.radioIgnoreMissing.TabIndex = 4;
            this.radioIgnoreMissing.TabStop = true;
            this.radioIgnoreMissing.Text = "Ignore nodes missing results";
            this.radioIgnoreMissing.UseVisualStyleBackColor = true;
            // 
            // textMinPeakFoundRatio
            // 
            this.textMinPeakFoundRatio.Location = new System.Drawing.Point(25, 41);
            this.textMinPeakFoundRatio.Name = "textMinPeakFoundRatio";
            this.textMinPeakFoundRatio.Size = new System.Drawing.Size(65, 20);
            this.textMinPeakFoundRatio.TabIndex = 1;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(22, 25);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(107, 13);
            this.label5.TabIndex = 0;
            this.label5.Text = "Min peak found ratio:";
            // 
            // groupLibCorr
            // 
            this.groupLibCorr.Controls.Add(this.textMinDotProduct);
            this.groupLibCorr.Controls.Add(this.labelMinDotProduct);
            this.groupLibCorr.Enabled = false;
            this.groupLibCorr.Location = new System.Drawing.Point(25, 236);
            this.groupLibCorr.Name = "groupLibCorr";
            this.groupLibCorr.Size = new System.Drawing.Size(332, 80);
            this.groupLibCorr.TabIndex = 7;
            this.groupLibCorr.TabStop = false;
            this.groupLibCorr.Text = "Spectral library correlation:";
            // 
            // textMinDotProduct
            // 
            this.textMinDotProduct.Enabled = false;
            this.textMinDotProduct.Location = new System.Drawing.Point(33, 41);
            this.textMinDotProduct.Name = "textMinDotProduct";
            this.textMinDotProduct.Size = new System.Drawing.Size(65, 20);
            this.textMinDotProduct.TabIndex = 1;
            // 
            // labelMinDotProduct
            // 
            this.labelMinDotProduct.AutoSize = true;
            this.labelMinDotProduct.Enabled = false;
            this.labelMinDotProduct.Location = new System.Drawing.Point(30, 25);
            this.labelMinDotProduct.Name = "labelMinDotProduct";
            this.labelMinDotProduct.Size = new System.Drawing.Size(84, 13);
            this.labelMinDotProduct.TabIndex = 0;
            this.labelMinDotProduct.Text = "Min dot-product:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textRTRegressionThreshold);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(25, 138);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(332, 80);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Retention time outliers:";
            // 
            // textRTRegressionThreshold
            // 
            this.textRTRegressionThreshold.Location = new System.Drawing.Point(33, 42);
            this.textRTRegressionThreshold.Name = "textRTRegressionThreshold";
            this.textRTRegressionThreshold.Size = new System.Drawing.Size(65, 20);
            this.textRTRegressionThreshold.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(30, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(170, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Target r value for linear regression:";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(252, 379);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(333, 379);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // RefineDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(420, 414);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tabControl1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RefineDlg";
            this.ShowInTaskbar = false;
            this.Text = "Refine";
            this.tabControl1.ResumeLayout(false);
            this.tabDocument.ResumeLayout(false);
            this.tabDocument.PerformLayout();
            this.tabResults.ResumeLayout(false);
            this.tabResults.PerformLayout();
            this.groupLibCorr.ResumeLayout(false);
            this.groupLibCorr.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabDocument;
        private System.Windows.Forms.TabPage tabResults;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.CheckBox cbRemoveHeavy;
        private System.Windows.Forms.TextBox textMinTransitions;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textMinPeptides;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox cbRemoveRepeatedPeptides;
        private System.Windows.Forms.CheckBox cbRemoveDuplicatePeptides;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupLibCorr;
        private System.Windows.Forms.Label labelMinDotProduct;
        private System.Windows.Forms.TextBox textRTRegressionThreshold;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textMinDotProduct;
        private System.Windows.Forms.RadioButton radioRemoveMissing;
        private System.Windows.Forms.RadioButton radioIgnoreMissing;
        private System.Windows.Forms.TextBox textMinPeakFoundRatio;
        private System.Windows.Forms.TextBox textMaxPeakFoundRatio;
        private System.Windows.Forms.Label label6;
    }
}