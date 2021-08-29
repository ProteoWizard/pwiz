namespace MSStatArgsCollector
{
    partial class SampleSizeUi
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
            this.labelFDR = new System.Windows.Forms.Label();
            this.labelLowerDesiredFC = new System.Windows.Forms.Label();
            this.labelUpperDesiredFC = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.gBoxFoldChange = new System.Windows.Forms.GroupBox();
            this.numberUDFC = new System.Windows.Forms.TextBox();
            this.numberLDFC = new System.Windows.Forms.TextBox();
            this.btnDefault = new System.Windows.Forms.Button();
            this.numberFDR = new System.Windows.Forms.TextBox();
            this.commonOptionsControl1 = new MSStatArgsCollector.CommonOptionsControl();
            this.lblSampleSize = new System.Windows.Forms.Label();
            this.tbxSampleSize = new System.Windows.Forms.TextBox();
            this.lblPower = new System.Windows.Forms.Label();
            this.tbxPower = new System.Windows.Forms.TextBox();
            this.gBoxFoldChange.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelFDR
            // 
            this.labelFDR.AutoSize = true;
            this.labelFDR.Location = new System.Drawing.Point(15, 267);
            this.labelFDR.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);
            this.labelFDR.Name = "labelFDR";
            this.labelFDR.Size = new System.Drawing.Size(32, 13);
            this.labelFDR.TabIndex = 4;
            this.labelFDR.Text = "FDR:";
            // 
            // labelLowerDesiredFC
            // 
            this.labelLowerDesiredFC.AutoSize = true;
            this.labelLowerDesiredFC.Location = new System.Drawing.Point(10, 26);
            this.labelLowerDesiredFC.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);
            this.labelLowerDesiredFC.Name = "labelLowerDesiredFC";
            this.labelLowerDesiredFC.Size = new System.Drawing.Size(39, 13);
            this.labelLowerDesiredFC.TabIndex = 0;
            this.labelLowerDesiredFC.Text = "Lower:";
            // 
            // labelUpperDesiredFC
            // 
            this.labelUpperDesiredFC.AutoSize = true;
            this.labelUpperDesiredFC.Location = new System.Drawing.Point(81, 26);
            this.labelUpperDesiredFC.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);
            this.labelUpperDesiredFC.Name = "labelUpperDesiredFC";
            this.labelUpperDesiredFC.Size = new System.Drawing.Size(39, 13);
            this.labelUpperDesiredFC.TabIndex = 1;
            this.labelUpperDesiredFC.Text = "Upper:";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(182, 484);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 8;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(263, 484);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // gBoxFoldChange
            // 
            this.gBoxFoldChange.Controls.Add(this.numberUDFC);
            this.gBoxFoldChange.Controls.Add(this.numberLDFC);
            this.gBoxFoldChange.Controls.Add(this.labelLowerDesiredFC);
            this.gBoxFoldChange.Controls.Add(this.labelUpperDesiredFC);
            this.gBoxFoldChange.Location = new System.Drawing.Point(18, 326);
            this.gBoxFoldChange.Name = "gBoxFoldChange";
            this.gBoxFoldChange.Size = new System.Drawing.Size(154, 74);
            this.gBoxFoldChange.TabIndex = 6;
            this.gBoxFoldChange.TabStop = false;
            this.gBoxFoldChange.Text = "Desired fold change";
            // 
            // numberUDFC
            // 
            this.numberUDFC.Location = new System.Drawing.Point(84, 41);
            this.numberUDFC.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberUDFC.Name = "numberUDFC";
            this.numberUDFC.Size = new System.Drawing.Size(60, 20);
            this.numberUDFC.TabIndex = 3;
            // 
            // numberLDFC
            // 
            this.numberLDFC.Location = new System.Drawing.Point(12, 41);
            this.numberLDFC.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberLDFC.Name = "numberLDFC";
            this.numberLDFC.Size = new System.Drawing.Size(60, 20);
            this.numberLDFC.TabIndex = 2;
            // 
            // btnDefault
            // 
            this.btnDefault.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDefault.Location = new System.Drawing.Point(12, 425);
            this.btnDefault.Name = "btnDefault";
            this.btnDefault.Size = new System.Drawing.Size(93, 23);
            this.btnDefault.TabIndex = 7;
            this.btnDefault.Text = "Reset Defaults";
            this.btnDefault.UseVisualStyleBackColor = true;
            this.btnDefault.Click += new System.EventHandler(this.btnDefault_Click);
            // 
            // numberFDR
            // 
            this.numberFDR.Location = new System.Drawing.Point(18, 291);
            this.numberFDR.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberFDR.Name = "numberFDR";
            this.numberFDR.Size = new System.Drawing.Size(60, 20);
            this.numberFDR.TabIndex = 5;
            // 
            // commonOptionsControl1
            // 
            this.commonOptionsControl1.Location = new System.Drawing.Point(12, 12);
            this.commonOptionsControl1.Name = "commonOptionsControl1";
            this.commonOptionsControl1.Size = new System.Drawing.Size(203, 164);
            this.commonOptionsControl1.TabIndex = 10;
            // 
            // lblSampleSize
            // 
            this.lblSampleSize.AutoSize = true;
            this.lblSampleSize.Location = new System.Drawing.Point(15, 179);
            this.lblSampleSize.Name = "lblSampleSize";
            this.lblSampleSize.Size = new System.Drawing.Size(68, 13);
            this.lblSampleSize.TabIndex = 11;
            this.lblSampleSize.Text = "Sample Size:";
            // 
            // tbxSampleSize
            // 
            this.tbxSampleSize.Location = new System.Drawing.Point(18, 195);
            this.tbxSampleSize.Name = "tbxSampleSize";
            this.tbxSampleSize.Size = new System.Drawing.Size(100, 20);
            this.tbxSampleSize.TabIndex = 12;
            // 
            // lblPower
            // 
            this.lblPower.AutoSize = true;
            this.lblPower.Location = new System.Drawing.Point(15, 218);
            this.lblPower.Name = "lblPower";
            this.lblPower.Size = new System.Drawing.Size(40, 13);
            this.lblPower.TabIndex = 13;
            this.lblPower.Text = "Power:";
            // 
            // tbxPower
            // 
            this.tbxPower.Location = new System.Drawing.Point(18, 234);
            this.tbxPower.Name = "tbxPower";
            this.tbxPower.Size = new System.Drawing.Size(100, 20);
            this.tbxPower.TabIndex = 14;
            // 
            // SampleSizeUi
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(350, 519);
            this.Controls.Add(this.tbxPower);
            this.Controls.Add(this.lblSampleSize);
            this.Controls.Add(this.lblPower);
            this.Controls.Add(this.tbxSampleSize);
            this.Controls.Add(this.commonOptionsControl1);
            this.Controls.Add(this.gBoxFoldChange);
            this.Controls.Add(this.numberFDR);
            this.Controls.Add(this.labelFDR);
            this.Controls.Add(this.btnDefault);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SampleSizeUi";
            this.ShowIcon = false;
            this.Text = "MSstats Design Sample Size";
            this.gBoxFoldChange.ResumeLayout(false);
            this.gBoxFoldChange.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label labelFDR;
        private System.Windows.Forms.Label labelLowerDesiredFC;
        private System.Windows.Forms.Label labelUpperDesiredFC;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox gBoxFoldChange;
        private System.Windows.Forms.Button btnDefault;
        private System.Windows.Forms.TextBox numberFDR;
        private System.Windows.Forms.TextBox numberLDFC;
        private System.Windows.Forms.TextBox numberUDFC;
        private CommonOptionsControl commonOptionsControl1;
        private System.Windows.Forms.Label lblSampleSize;
        private System.Windows.Forms.TextBox tbxSampleSize;
        private System.Windows.Forms.Label lblPower;
        private System.Windows.Forms.TextBox tbxPower;
    }
}
