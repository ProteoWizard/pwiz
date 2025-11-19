namespace pwiz.Skyline.Alerts
{
    partial class MsFraggerDownloadDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MsFraggerDownloadDlg));
            this.cbAgreeToLicense = new System.Windows.Forms.CheckBox();
            this.rtbAgreeToLicense = new System.Windows.Forms.RichTextBox();
            this.tbFirstName = new System.Windows.Forms.TextBox();
            this.lblFirstName = new System.Windows.Forms.Label();
            this.lblEmail = new System.Windows.Forms.Label();
            this.tbEmail = new System.Windows.Forms.TextBox();
            this.lblInstitution = new System.Windows.Forms.Label();
            this.tbInstitution = new System.Windows.Forms.TextBox();
            this.btnAccept = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tbLastName = new System.Windows.Forms.TextBox();
            this.lblLastName = new System.Windows.Forms.Label();
            this.tbVerificationCode = new System.Windows.Forms.TextBox();
            this.lblVerificationCode = new System.Windows.Forms.Label();
            this.btnRequestVerificationCode = new System.Windows.Forms.Button();
            this.rtbUsageConditions = new System.Windows.Forms.RichTextBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // cbAgreeToLicense
            // 
            resources.ApplyResources(this.cbAgreeToLicense, "cbAgreeToLicense");
            this.cbAgreeToLicense.Name = "cbAgreeToLicense";
            this.cbAgreeToLicense.UseVisualStyleBackColor = true;
            this.cbAgreeToLicense.CheckedChanged += new System.EventHandler(this.cbAgreeToLicense_CheckedChanged);
            // 
            // rtbAgreeToLicense
            // 
            resources.ApplyResources(this.rtbAgreeToLicense, "rtbAgreeToLicense");
            this.rtbAgreeToLicense.BackColor = System.Drawing.SystemColors.Control;
            this.rtbAgreeToLicense.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbAgreeToLicense.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rtbAgreeToLicense.Name = "rtbAgreeToLicense";
            this.rtbAgreeToLicense.ReadOnly = true;
            this.rtbAgreeToLicense.TabStop = false;
            // 
            // tbFirstName
            // 
            resources.ApplyResources(this.tbFirstName, "tbFirstName");
            this.tbFirstName.Name = "tbFirstName";
            this.tbFirstName.TextChanged += new System.EventHandler(this.tbTextChanged);
            // 
            // lblFirstName
            // 
            resources.ApplyResources(this.lblFirstName, "lblFirstName");
            this.lblFirstName.Name = "lblFirstName";
            // 
            // lblEmail
            // 
            resources.ApplyResources(this.lblEmail, "lblEmail");
            this.lblEmail.Name = "lblEmail";
            // 
            // tbEmail
            // 
            resources.ApplyResources(this.tbEmail, "tbEmail");
            this.tbEmail.Name = "tbEmail";
            this.tbEmail.TextChanged += new System.EventHandler(this.tbTextChanged);
            // 
            // lblInstitution
            // 
            resources.ApplyResources(this.lblInstitution, "lblInstitution");
            this.lblInstitution.Name = "lblInstitution";
            // 
            // tbInstitution
            // 
            resources.ApplyResources(this.tbInstitution, "tbInstitution");
            this.tbInstitution.Name = "tbInstitution";
            this.tbInstitution.TextChanged += new System.EventHandler(this.tbTextChanged);
            // 
            // btnAccept
            // 
            resources.ApplyResources(this.btnAccept, "btnAccept");
            this.btnAccept.Name = "btnAccept";
            this.btnAccept.UseVisualStyleBackColor = true;
            this.btnAccept.Click += new System.EventHandler(this.btnAccept_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.tbLastName, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.lblLastName, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.lblFirstName, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.lblEmail, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.lblInstitution, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbFirstName, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbInstitution, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbEmail, 1, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // tbLastName
            // 
            resources.ApplyResources(this.tbLastName, "tbLastName");
            this.tbLastName.Name = "tbLastName";
            this.tbLastName.TextChanged += new System.EventHandler(this.tbTextChanged);
            // 
            // lblLastName
            // 
            resources.ApplyResources(this.lblLastName, "lblLastName");
            this.lblLastName.Name = "lblLastName";
            // 
            // tbVerificationCode
            // 
            resources.ApplyResources(this.tbVerificationCode, "tbVerificationCode");
            this.tbVerificationCode.Name = "tbVerificationCode";
            this.tbVerificationCode.TextChanged += new System.EventHandler(this.tbVerificationCodeChanged);
            // 
            // lblVerificationCode
            // 
            resources.ApplyResources(this.lblVerificationCode, "lblVerificationCode");
            this.lblVerificationCode.Name = "lblVerificationCode";
            // 
            // btnRequestVerificationCode
            // 
            resources.ApplyResources(this.btnRequestVerificationCode, "btnRequestVerificationCode");
            this.btnRequestVerificationCode.Name = "btnRequestVerificationCode";
            this.btnRequestVerificationCode.UseVisualStyleBackColor = true;
            this.btnRequestVerificationCode.Click += new System.EventHandler(this.btnRequestVerificationCode_Click);
            // 
            // rtbUsageConditions
            // 
            resources.ApplyResources(this.rtbUsageConditions, "rtbUsageConditions");
            this.rtbUsageConditions.BackColor = System.Drawing.SystemColors.Control;
            this.rtbUsageConditions.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbUsageConditions.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rtbUsageConditions.Name = "rtbUsageConditions";
            this.rtbUsageConditions.ReadOnly = true;
            this.rtbUsageConditions.TabStop = false;
            // 
            // MsFraggerDownloadDlg
            // 
            this.AcceptButton = this.btnAccept;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnRequestVerificationCode);
            this.Controls.Add(this.lblVerificationCode);
            this.Controls.Add(this.tbVerificationCode);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnAccept);
            this.Controls.Add(this.rtbAgreeToLicense);
            this.Controls.Add(this.cbAgreeToLicense);
            this.Controls.Add(this.rtbUsageConditions);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MsFraggerDownloadDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.CheckBox cbAgreeToLicense;
        private System.Windows.Forms.RichTextBox rtbAgreeToLicense;
        private System.Windows.Forms.TextBox tbFirstName;
        private System.Windows.Forms.Label lblFirstName;
        private System.Windows.Forms.Label lblEmail;
        private System.Windows.Forms.TextBox tbEmail;
        private System.Windows.Forms.Label lblInstitution;
        private System.Windows.Forms.TextBox tbInstitution;
        private System.Windows.Forms.Button btnAccept;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label lblLastName;
        private System.Windows.Forms.TextBox tbLastName;
        private System.Windows.Forms.TextBox tbVerificationCode;
        private System.Windows.Forms.Label lblVerificationCode;
        private System.Windows.Forms.Button btnRequestVerificationCode;
        private System.Windows.Forms.RichTextBox rtbUsageConditions;
    }
}