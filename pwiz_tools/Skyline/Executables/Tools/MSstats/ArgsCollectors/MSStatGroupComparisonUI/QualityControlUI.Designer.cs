namespace MSStatArgsCollector
{
    partial class QualityControlUI
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
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.comboBoxNormalizeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.cboxAllowMissingPeaks = new System.Windows.Forms.CheckBox();
            this.cboxEqualVariance = new System.Windows.Forms.CheckBox();
            this.comboBoxSummaryMethod = new System.Windows.Forms.ComboBox();
            this.labelSummaryMethod = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(182, 41);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(3, 3, 15, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(182, 12);
            this.btnOK.Margin = new System.Windows.Forms.Padding(3, 3, 15, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // comboBoxNormalizeTo
            // 
            this.comboBoxNormalizeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNormalizeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxNormalizeTo.FormattingEnabled = true;
            this.comboBoxNormalizeTo.Items.AddRange(new object[] {
            "None",
            "Equalize medians",
            "Quantile",
            "Relative to global standards"});
            this.comboBoxNormalizeTo.Location = new System.Drawing.Point(12, 30);
            this.comboBoxNormalizeTo.Name = "comboBoxNormalizeTo";
            this.comboBoxNormalizeTo.Size = new System.Drawing.Size(144, 21);
            this.comboBoxNormalizeTo.TabIndex = 1;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(12, 12);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(111, 13);
            this.labelNormalizeTo.TabIndex = 0;
            this.labelNormalizeTo.Text = "Normalization method:";
            // 
            // cboxAllowMissingPeaks
            // 
            this.cboxAllowMissingPeaks.AutoSize = true;
            this.cboxAllowMissingPeaks.Location = new System.Drawing.Point(13, 54);
            this.cboxAllowMissingPeaks.Name = "cboxAllowMissingPeaks";
            this.cboxAllowMissingPeaks.Size = new System.Drawing.Size(120, 17);
            this.cboxAllowMissingPeaks.TabIndex = 2;
            this.cboxAllowMissingPeaks.Text = "&Allow missing peaks";
            this.cboxAllowMissingPeaks.UseVisualStyleBackColor = true;
            // 
            // cboxEqualVariance
            // 
            this.cboxEqualVariance.AutoSize = true;
            this.cboxEqualVariance.Checked = true;
            this.cboxEqualVariance.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxEqualVariance.Location = new System.Drawing.Point(12, 118);
            this.cboxEqualVariance.Name = "cboxEqualVariance";
            this.cboxEqualVariance.Size = new System.Drawing.Size(136, 17);
            this.cboxEqualVariance.TabIndex = 10;
            this.cboxEqualVariance.Text = "&Assume equal variance";
            this.cboxEqualVariance.UseVisualStyleBackColor = true;
            // 
            // comboBoxSummaryMethod
            // 
            this.comboBoxSummaryMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSummaryMethod.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxSummaryMethod.FormattingEnabled = true;
            this.comboBoxSummaryMethod.Location = new System.Drawing.Point(12, 91);
            this.comboBoxSummaryMethod.Name = "comboBoxSummaryMethod";
            this.comboBoxSummaryMethod.Size = new System.Drawing.Size(172, 21);
            this.comboBoxSummaryMethod.TabIndex = 9;
            // 
            // labelSummaryMethod
            // 
            this.labelSummaryMethod.AutoSize = true;
            this.labelSummaryMethod.Location = new System.Drawing.Point(12, 74);
            this.labelSummaryMethod.Name = "labelSummaryMethod";
            this.labelSummaryMethod.Size = new System.Drawing.Size(91, 13);
            this.labelSummaryMethod.TabIndex = 8;
            this.labelSummaryMethod.Text = "Summary method:";
            // 
            // QualityControlUI
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(268, 202);
            this.Controls.Add(this.cboxEqualVariance);
            this.Controls.Add(this.comboBoxSummaryMethod);
            this.Controls.Add(this.labelSummaryMethod);
            this.Controls.Add(this.cboxAllowMissingPeaks);
            this.Controls.Add(this.comboBoxNormalizeTo);
            this.Controls.Add(this.labelNormalizeTo);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "QualityControlUI";
            this.ShowInTaskbar = false;
            this.Text = "MSstats QC";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.ComboBox comboBoxNormalizeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.CheckBox cboxAllowMissingPeaks;
        private System.Windows.Forms.CheckBox cboxEqualVariance;
        private System.Windows.Forms.ComboBox comboBoxSummaryMethod;
        private System.Windows.Forms.Label labelSummaryMethod;
    }
}