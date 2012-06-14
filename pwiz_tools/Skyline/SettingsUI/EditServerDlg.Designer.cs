namespace pwiz.Skyline.SettingsUI
{
    partial class EditServerDlg
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
            this.lblProjectInfo = new System.Windows.Forms.Label();
            this.textServerName = new System.Windows.Forms.TextBox();
            this.textPassword = new System.Windows.Forms.TextBox();
            this.textUsername = new System.Windows.Forms.TextBox();
            this.lblPassword = new System.Windows.Forms.Label();
            this.lblUsername = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(242, 191);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(161, 191);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // lblProjectInfo
            // 
            this.lblProjectInfo.AutoSize = true;
            this.lblProjectInfo.Location = new System.Drawing.Point(12, 9);
            this.lblProjectInfo.Name = "lblProjectInfo";
            this.lblProjectInfo.Size = new System.Drawing.Size(239, 13);
            this.lblProjectInfo.TabIndex = 4;
            this.lblProjectInfo.Text = "&Server name (e.g. panorama.gs.washington.edu):";
            // 
            // textServerName
            // 
            this.textServerName.Location = new System.Drawing.Point(12, 25);
            this.textServerName.Name = "textServerName";
            this.textServerName.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textServerName.Size = new System.Drawing.Size(303, 20);
            this.textServerName.TabIndex = 5;
            // 
            // textPassword
            // 
            this.textPassword.Location = new System.Drawing.Point(12, 121);
            this.textPassword.Name = "textPassword";
            this.textPassword.PasswordChar = '*';
            this.textPassword.Size = new System.Drawing.Size(207, 20);
            this.textPassword.TabIndex = 9;
            this.textPassword.UseSystemPasswordChar = true;
            // 
            // textUsername
            // 
            this.textUsername.Location = new System.Drawing.Point(12, 73);
            this.textUsername.Name = "textUsername";
            this.textUsername.Size = new System.Drawing.Size(207, 20);
            this.textUsername.TabIndex = 8;
            // 
            // lblPassword
            // 
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new System.Drawing.Point(9, 105);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(56, 13);
            this.lblPassword.TabIndex = 6;
            this.lblPassword.Text = "Password:";
            // 
            // lblUsername
            // 
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new System.Drawing.Point(9, 57);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(61, 13);
            this.lblUsername.TabIndex = 7;
            this.lblUsername.Text = "Username: ";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(225, 76);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(50, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "(optional)";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(225, 124);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(50, 13);
            this.label2.TabIndex = 11;
            this.label2.Text = "(optional)";
            // 
            // EditServerDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(327, 223);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textPassword);
            this.Controls.Add(this.textUsername);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.textServerName);
            this.Controls.Add(this.lblProjectInfo);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditServerDlg";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Server";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label lblProjectInfo;
        private System.Windows.Forms.TextBox textServerName;
        internal System.Windows.Forms.TextBox textPassword;
        internal System.Windows.Forms.TextBox textUsername;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}