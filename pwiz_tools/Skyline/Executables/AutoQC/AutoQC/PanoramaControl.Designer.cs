namespace AutoQC
{
    partial class PanoramaControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.labelPanoramaFolder = new System.Windows.Forms.Label();
            this.textPanoramaFolder = new System.Windows.Forms.TextBox();
            this.lblPanoramaUrl = new System.Windows.Forms.Label();
            this.textPanoramaUrl = new System.Windows.Forms.TextBox();
            this.textPanoramaPasswd = new System.Windows.Forms.TextBox();
            this.lblPanoramaPasswd = new System.Windows.Forms.Label();
            this.lblPanoramaEmail = new System.Windows.Forms.Label();
            this.textPanoramaEmail = new System.Windows.Forms.TextBox();
            this.label_err_message = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelPanoramaFolder
            // 
            this.labelPanoramaFolder.AutoSize = true;
            this.labelPanoramaFolder.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelPanoramaFolder.Location = new System.Drawing.Point(-2, 156);
            this.labelPanoramaFolder.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelPanoramaFolder.Name = "labelPanoramaFolder";
            this.labelPanoramaFolder.Size = new System.Drawing.Size(204, 13);
            this.labelPanoramaFolder.TabIndex = 21;
            this.labelPanoramaFolder.Text = "&Folder on Panorama (e.g. /MacCoss/QC):";
            // 
            // textPanoramaFolder
            // 
            this.textPanoramaFolder.Location = new System.Drawing.Point(0, 172);
            this.textPanoramaFolder.Margin = new System.Windows.Forms.Padding(2);
            this.textPanoramaFolder.Name = "textPanoramaFolder";
            this.textPanoramaFolder.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textPanoramaFolder.Size = new System.Drawing.Size(202, 20);
            this.textPanoramaFolder.TabIndex = 17;
            // 
            // lblPanoramaUrl
            // 
            this.lblPanoramaUrl.AutoSize = true;
            this.lblPanoramaUrl.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblPanoramaUrl.Location = new System.Drawing.Point(-3, 24);
            this.lblPanoramaUrl.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPanoramaUrl.Name = "lblPanoramaUrl";
            this.lblPanoramaUrl.Size = new System.Drawing.Size(32, 13);
            this.lblPanoramaUrl.TabIndex = 18;
            this.lblPanoramaUrl.Text = "&URL:";
            // 
            // textPanoramaUrl
            // 
            this.textPanoramaUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textPanoramaUrl.Location = new System.Drawing.Point(0, 39);
            this.textPanoramaUrl.Margin = new System.Windows.Forms.Padding(2);
            this.textPanoramaUrl.Name = "textPanoramaUrl";
            this.textPanoramaUrl.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textPanoramaUrl.Size = new System.Drawing.Size(345, 20);
            this.textPanoramaUrl.TabIndex = 14;
            this.textPanoramaUrl.Text = "https://panoramaweb.org/";
            // 
            // textPanoramaPasswd
            // 
            this.textPanoramaPasswd.Location = new System.Drawing.Point(1, 127);
            this.textPanoramaPasswd.Margin = new System.Windows.Forms.Padding(2);
            this.textPanoramaPasswd.Name = "textPanoramaPasswd";
            this.textPanoramaPasswd.PasswordChar = '*';
            this.textPanoramaPasswd.Size = new System.Drawing.Size(201, 20);
            this.textPanoramaPasswd.TabIndex = 16;
            this.textPanoramaPasswd.UseSystemPasswordChar = true;
            // 
            // lblPanoramaPasswd
            // 
            this.lblPanoramaPasswd.AutoSize = true;
            this.lblPanoramaPasswd.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblPanoramaPasswd.Location = new System.Drawing.Point(-2, 111);
            this.lblPanoramaPasswd.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPanoramaPasswd.Name = "lblPanoramaPasswd";
            this.lblPanoramaPasswd.Size = new System.Drawing.Size(56, 13);
            this.lblPanoramaPasswd.TabIndex = 20;
            this.lblPanoramaPasswd.Text = "P&assword:";
            // 
            // lblPanoramaEmail
            // 
            this.lblPanoramaEmail.AutoSize = true;
            this.lblPanoramaEmail.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblPanoramaEmail.Location = new System.Drawing.Point(-2, 68);
            this.lblPanoramaEmail.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPanoramaEmail.Name = "lblPanoramaEmail";
            this.lblPanoramaEmail.Size = new System.Drawing.Size(35, 13);
            this.lblPanoramaEmail.TabIndex = 19;
            this.lblPanoramaEmail.Text = "&Email:";
            // 
            // textPanoramaEmail
            // 
            this.textPanoramaEmail.Location = new System.Drawing.Point(0, 84);
            this.textPanoramaEmail.Margin = new System.Windows.Forms.Padding(2);
            this.textPanoramaEmail.Name = "textPanoramaEmail";
            this.textPanoramaEmail.Size = new System.Drawing.Size(202, 20);
            this.textPanoramaEmail.TabIndex = 15;
            // 
            // label_err_message
            // 
            this.label_err_message.AutoSize = true;
            this.label_err_message.Location = new System.Drawing.Point(-2, 1);
            this.label_err_message.Name = "label_err_message";
            this.label_err_message.Size = new System.Drawing.Size(32, 13);
            this.label_err_message.TabIndex = 23;
            this.label_err_message.Text = "Error:";
            // 
            // PanoramaControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label_err_message);
            this.Controls.Add(this.labelPanoramaFolder);
            this.Controls.Add(this.textPanoramaFolder);
            this.Controls.Add(this.lblPanoramaUrl);
            this.Controls.Add(this.textPanoramaUrl);
            this.Controls.Add(this.textPanoramaPasswd);
            this.Controls.Add(this.lblPanoramaPasswd);
            this.Controls.Add(this.lblPanoramaEmail);
            this.Controls.Add(this.textPanoramaEmail);
            this.Name = "PanoramaControl";
            this.Size = new System.Drawing.Size(346, 195);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelPanoramaFolder;
        private System.Windows.Forms.TextBox textPanoramaFolder;
        private System.Windows.Forms.Label lblPanoramaUrl;
        private System.Windows.Forms.TextBox textPanoramaUrl;
        private System.Windows.Forms.TextBox textPanoramaPasswd;
        private System.Windows.Forms.Label lblPanoramaPasswd;
        private System.Windows.Forms.Label lblPanoramaEmail;
        private System.Windows.Forms.TextBox textPanoramaEmail;
        private System.Windows.Forms.Label label_err_message;
    }
}
