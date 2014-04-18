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
            this.comboBoxNoramilzeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.cboxAllowMissingPeaks = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(243, 50);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 4, 20, 4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 28);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(243, 15);
            this.btnOK.Margin = new System.Windows.Forms.Padding(4, 4, 20, 4);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 28);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // comboBoxNoramilzeTo
            // 
            this.comboBoxNoramilzeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNoramilzeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxNoramilzeTo.FormattingEnabled = true;
            this.comboBoxNoramilzeTo.Items.AddRange(new object[] {
            "None",
            "Equalize medians",
            "Quantile",
            "Relative to global standards"});
            this.comboBoxNoramilzeTo.Location = new System.Drawing.Point(16, 37);
            this.comboBoxNoramilzeTo.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxNoramilzeTo.Name = "comboBoxNoramilzeTo";
            this.comboBoxNoramilzeTo.Size = new System.Drawing.Size(191, 24);
            this.comboBoxNoramilzeTo.TabIndex = 1;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(16, 15);
            this.labelNormalizeTo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(149, 17);
            this.labelNormalizeTo.TabIndex = 0;
            this.labelNormalizeTo.Text = "Normalization method:";
            // 
            // cboxAllowMissingPeaks
            // 
            this.cboxAllowMissingPeaks.AutoSize = true;
            this.cboxAllowMissingPeaks.Location = new System.Drawing.Point(17, 67);
            this.cboxAllowMissingPeaks.Margin = new System.Windows.Forms.Padding(4);
            this.cboxAllowMissingPeaks.Name = "cboxAllowMissingPeaks";
            this.cboxAllowMissingPeaks.Size = new System.Drawing.Size(155, 21);
            this.cboxAllowMissingPeaks.TabIndex = 2;
            this.cboxAllowMissingPeaks.Text = "&Allow missing peaks";
            this.cboxAllowMissingPeaks.UseVisualStyleBackColor = true;
            // 
            // QualityControlUI
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(357, 97);
            this.Controls.Add(this.cboxAllowMissingPeaks);
            this.Controls.Add(this.comboBoxNoramilzeTo);
            this.Controls.Add(this.labelNormalizeTo);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
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
        private System.Windows.Forms.ComboBox comboBoxNoramilzeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.CheckBox cboxAllowMissingPeaks;
    }
}