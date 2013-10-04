namespace pwiz.Skyline.EditUI
{
    partial class ReintegrateDlg
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
            this.btnOk = new System.Windows.Forms.Button();
            this.textBoxCutoff = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.reintegrateAllPeaks = new System.Windows.Forms.RadioButton();
            this.reintegrateQCutoff = new System.Windows.Forms.RadioButton();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(225, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOk.Location = new System.Drawing.Point(225, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 4;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textBoxCutoff
            // 
            this.textBoxCutoff.Enabled = false;
            this.textBoxCutoff.Location = new System.Drawing.Point(45, 90);
            this.textBoxCutoff.Name = "textBoxCutoff";
            this.textBoxCutoff.Size = new System.Drawing.Size(100, 20);
            this.textBoxCutoff.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(42, 72);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(77, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "&Q value cutoff:";
            // 
            // reintegrateAllPeaks
            // 
            this.reintegrateAllPeaks.AutoSize = true;
            this.reintegrateAllPeaks.Checked = true;
            this.reintegrateAllPeaks.Location = new System.Drawing.Point(12, 13);
            this.reintegrateAllPeaks.Name = "reintegrateAllPeaks";
            this.reintegrateAllPeaks.Size = new System.Drawing.Size(112, 17);
            this.reintegrateAllPeaks.TabIndex = 0;
            this.reintegrateAllPeaks.TabStop = true;
            this.reintegrateAllPeaks.Text = "Integrate &all peaks";
            this.reintegrateAllPeaks.UseVisualStyleBackColor = true;
            this.reintegrateAllPeaks.CheckedChanged += new System.EventHandler(this.reintegrateAllPeaks_CheckedChanged);
            // 
            // reintegrateQCutoff
            // 
            this.reintegrateQCutoff.AutoSize = true;
            this.reintegrateQCutoff.Location = new System.Drawing.Point(12, 41);
            this.reintegrateQCutoff.Name = "reintegrateQCutoff";
            this.reintegrateQCutoff.Size = new System.Drawing.Size(183, 17);
            this.reintegrateQCutoff.TabIndex = 1;
            this.reintegrateQCutoff.Text = "&Only integrate significant q values";
            this.reintegrateQCutoff.UseVisualStyleBackColor = true;
            this.reintegrateQCutoff.CheckedChanged += new System.EventHandler(this.reintegrateQCutoff_CheckedChanged);
            // 
            // ReintegrateDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(312, 128);
            this.Controls.Add(this.reintegrateQCutoff);
            this.Controls.Add(this.reintegrateAllPeaks);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxCutoff);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ReintegrateDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Reintegrate";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textBoxCutoff;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.RadioButton reintegrateAllPeaks;
        private System.Windows.Forms.RadioButton reintegrateQCutoff;
    }
}