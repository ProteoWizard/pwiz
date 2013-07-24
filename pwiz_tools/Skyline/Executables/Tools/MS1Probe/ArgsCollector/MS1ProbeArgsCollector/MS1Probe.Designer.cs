namespace MS1ProbeArgsCollector
{
    partial class MS1Probe
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
            this.labelName = new System.Windows.Forms.Label();
            this.tboxName = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.gboxFactors = new System.Windows.Forms.GroupBox();
            this.tboxDenominator = new System.Windows.Forms.TextBox();
            this.tboxNumerator = new System.Windows.Forms.TextBox();
            this.labelDenominator = new System.Windows.Forms.Label();
            this.labelNumerator = new System.Windows.Forms.Label();
            this.gboxFactors.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(12, 15);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(68, 13);
            this.labelName.TabIndex = 0;
            this.labelName.Text = "Report name";
            // 
            // tboxName
            // 
            this.tboxName.Location = new System.Drawing.Point(86, 12);
            this.tboxName.Name = "tboxName";
            this.tboxName.Size = new System.Drawing.Size(136, 20);
            this.tboxName.TabIndex = 1;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(66, 186);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(147, 186);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // gboxFactors
            // 
            this.gboxFactors.Controls.Add(this.tboxDenominator);
            this.gboxFactors.Controls.Add(this.tboxNumerator);
            this.gboxFactors.Controls.Add(this.labelDenominator);
            this.gboxFactors.Controls.Add(this.labelNumerator);
            this.gboxFactors.Location = new System.Drawing.Point(15, 53);
            this.gboxFactors.Name = "gboxFactors";
            this.gboxFactors.Size = new System.Drawing.Size(207, 100);
            this.gboxFactors.TabIndex = 2;
            this.gboxFactors.TabStop = false;
            this.gboxFactors.Text = "Distinguishing factor";
            // 
            // tboxDenominator
            // 
            this.tboxDenominator.Location = new System.Drawing.Point(112, 52);
            this.tboxDenominator.Name = "tboxDenominator";
            this.tboxDenominator.Size = new System.Drawing.Size(71, 20);
            this.tboxDenominator.TabIndex = 3;
            // 
            // tboxNumerator
            // 
            this.tboxNumerator.Location = new System.Drawing.Point(23, 52);
            this.tboxNumerator.Name = "tboxNumerator";
            this.tboxNumerator.Size = new System.Drawing.Size(71, 20);
            this.tboxNumerator.TabIndex = 1;
            // 
            // labelDenominator
            // 
            this.labelDenominator.AutoSize = true;
            this.labelDenominator.Location = new System.Drawing.Point(108, 27);
            this.labelDenominator.Name = "labelDenominator";
            this.labelDenominator.Size = new System.Drawing.Size(67, 13);
            this.labelDenominator.TabIndex = 2;
            this.labelDenominator.Text = "Denominator";
            // 
            // labelNumerator
            // 
            this.labelNumerator.AutoSize = true;
            this.labelNumerator.Location = new System.Drawing.Point(20, 27);
            this.labelNumerator.Name = "labelNumerator";
            this.labelNumerator.Size = new System.Drawing.Size(56, 13);
            this.labelNumerator.TabIndex = 0;
            this.labelNumerator.Text = "Numerator";
            // 
            // MS1Probe
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(234, 221);
            this.Controls.Add(this.gboxFactors);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tboxName);
            this.Controls.Add(this.labelName);
            this.MaximizeBox = false;
            this.Name = "MS1Probe";
            this.ShowIcon = false;
            this.Text = "MS1 Probe";
            this.Load += new System.EventHandler(this.MS1Probe_Load);
            this.gboxFactors.ResumeLayout(false);
            this.gboxFactors.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.TextBox tboxName;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox gboxFactors;
        private System.Windows.Forms.TextBox tboxDenominator;
        private System.Windows.Forms.TextBox tboxNumerator;
        private System.Windows.Forms.Label labelDenominator;
        private System.Windows.Forms.Label labelNumerator;
    }
}

