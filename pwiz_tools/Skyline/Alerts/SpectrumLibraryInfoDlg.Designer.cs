namespace pwiz.Skyline.Alerts
{
    partial class SpectrumLibraryInfoDlg
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
            this.labelLibInfo = new System.Windows.Forms.Label();
            this.linkSpecLibLinks = new System.Windows.Forms.LinkLabel();
            this.btnOk = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelLibInfo
            // 
            this.labelLibInfo.AutoSize = true;
            this.labelLibInfo.Location = new System.Drawing.Point(44, 21);
            this.labelLibInfo.Name = "labelLibInfo";
            this.labelLibInfo.Size = new System.Drawing.Size(115, 13);
            this.labelLibInfo.TabIndex = 0;
            this.labelLibInfo.Text = "Spectrum library details";
            // 
            // linkSpecLibLinks
            // 
            this.linkSpecLibLinks.AutoSize = true;
            this.linkSpecLibLinks.Location = new System.Drawing.Point(44, 90);
            this.linkSpecLibLinks.Name = "linkSpecLibLinks";
            this.linkSpecLibLinks.Size = new System.Drawing.Size(100, 13);
            this.linkSpecLibLinks.TabIndex = 1;
            this.linkSpecLibLinks.TabStop = true;
            this.linkSpecLibLinks.Text = "Spectral library links";
            this.linkSpecLibLinks.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Location = new System.Drawing.Point(127, 119);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 3;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // SpectrumLibraryInfoDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.ClientSize = new System.Drawing.Size(328, 154);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.linkSpecLibLinks);
            this.Controls.Add(this.labelLibInfo);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SpectrumLibraryInfoDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Library Details";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelLibInfo;
        private System.Windows.Forms.LinkLabel linkSpecLibLinks;
        private System.Windows.Forms.Button btnOk;
    }
}