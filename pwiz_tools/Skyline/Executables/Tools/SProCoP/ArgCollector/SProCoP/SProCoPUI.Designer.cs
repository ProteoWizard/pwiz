namespace SProCoP
{
    partial class SProCoPUI
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
            this.qcRunsNumUpDown = new System.Windows.Forms.NumericUpDown();
            this.qcRunsLabel = new System.Windows.Forms.Label();
            this.cBoxHighResolution = new System.Windows.Forms.CheckBox();
            this.cboxMetaFile = new System.Windows.Forms.CheckBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.labelMMAValue = new System.Windows.Forms.Label();
            this.textBoxMMAValue = new System.Windows.Forms.TextBox();
            this.labelUnitPPM = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.qcRunsNumUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // qcRunsNumUpDown
            // 
            this.qcRunsNumUpDown.Location = new System.Drawing.Point(15, 34);
            this.qcRunsNumUpDown.Maximum = new decimal(new int[] {
            15,
            0,
            0,
            0});
            this.qcRunsNumUpDown.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.qcRunsNumUpDown.Name = "qcRunsNumUpDown";
            this.qcRunsNumUpDown.Size = new System.Drawing.Size(44, 20);
            this.qcRunsNumUpDown.TabIndex = 1;
            this.qcRunsNumUpDown.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            // 
            // qcRunsLabel
            // 
            this.qcRunsLabel.AutoSize = true;
            this.qcRunsLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.qcRunsLabel.Location = new System.Drawing.Point(12, 15);
            this.qcRunsLabel.Name = "qcRunsLabel";
            this.qcRunsLabel.Size = new System.Drawing.Size(189, 13);
            this.qcRunsLabel.TabIndex = 0;
            this.qcRunsLabel.Text = "&Number of runs to establish thresholds:";
            // 
            // cBoxHighResolution
            // 
            this.cBoxHighResolution.AutoSize = true;
            this.cBoxHighResolution.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.cBoxHighResolution.Location = new System.Drawing.Point(15, 81);
            this.cBoxHighResolution.Name = "cBoxHighResolution";
            this.cBoxHighResolution.Size = new System.Drawing.Size(175, 17);
            this.cBoxHighResolution.TabIndex = 2;
            this.cBoxHighResolution.Text = "&Using high resolution instrument";
            this.cBoxHighResolution.UseVisualStyleBackColor = true;
            this.cBoxHighResolution.CheckedChanged += new System.EventHandler(this.cBoxHighResolution_CheckedChanged);
            // 
            // cboxMetaFile
            // 
            this.cboxMetaFile.AutoSize = true;
            this.cboxMetaFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.cboxMetaFile.Location = new System.Drawing.Point(15, 166);
            this.cboxMetaFile.Name = "cboxMetaFile";
            this.cboxMetaFile.Size = new System.Drawing.Size(89, 17);
            this.cboxMetaFile.TabIndex = 5;
            this.cboxMetaFile.Text = "&Save as PDF";
            this.cboxMetaFile.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(197, 201);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(116, 200);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 6;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.submitButton_Click);
            // 
            // labelMMAValue
            // 
            this.labelMMAValue.AutoSize = true;
            this.labelMMAValue.ForeColor = System.Drawing.Color.DimGray;
            this.labelMMAValue.Location = new System.Drawing.Point(32, 102);
            this.labelMMAValue.Name = "labelMMAValue";
            this.labelMMAValue.Size = new System.Drawing.Size(191, 13);
            this.labelMMAValue.TabIndex = 3;
            this.labelMMAValue.Text = "&Absolute mass measurement accuracy:";
            // 
            // textBoxMMAValue
            // 
            this.textBoxMMAValue.Enabled = false;
            this.textBoxMMAValue.Location = new System.Drawing.Point(34, 119);
            this.textBoxMMAValue.Name = "textBoxMMAValue";
            this.textBoxMMAValue.Size = new System.Drawing.Size(51, 20);
            this.textBoxMMAValue.TabIndex = 4;
            this.textBoxMMAValue.Text = "0";
            // 
            // labelUnitPPM
            // 
            this.labelUnitPPM.AutoSize = true;
            this.labelUnitPPM.ForeColor = System.Drawing.Color.DimGray;
            this.labelUnitPPM.Location = new System.Drawing.Point(88, 123);
            this.labelUnitPPM.Name = "labelUnitPPM";
            this.labelUnitPPM.Size = new System.Drawing.Size(27, 13);
            this.labelUnitPPM.TabIndex = 8;
            this.labelUnitPPM.Text = "ppm";
            // 
            // SProCoPUI
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(284, 236);
            this.Controls.Add(this.labelUnitPPM);
            this.Controls.Add(this.textBoxMMAValue);
            this.Controls.Add(this.labelMMAValue);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.cboxMetaFile);
            this.Controls.Add(this.cBoxHighResolution);
            this.Controls.Add(this.qcRunsLabel);
            this.Controls.Add(this.qcRunsNumUpDown);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SProCoPUI";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "SProCoP";
            ((System.ComponentModel.ISupportInitialize)(this.qcRunsNumUpDown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown qcRunsNumUpDown;
        private System.Windows.Forms.Label qcRunsLabel;
        private System.Windows.Forms.CheckBox cBoxHighResolution;
        private System.Windows.Forms.CheckBox cboxMetaFile;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label labelMMAValue;
        private System.Windows.Forms.TextBox textBoxMMAValue;
        private System.Windows.Forms.Label labelUnitPPM;
    }
}

