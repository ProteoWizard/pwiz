namespace pwiz.Skyline.Alerts
{
    partial class DiannDownloadDlg
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DiannDownloadDlg));
            this.lblSummary = new System.Windows.Forms.Label();
            this.linkLicense = new System.Windows.Forms.LinkLabel();
            this.cbAgreeToLicense = new System.Windows.Forms.CheckBox();
            this.btnAccept = new System.Windows.Forms.Button();
            this.btnSpecifyManually = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblSummary
            //
            resources.ApplyResources(this.lblSummary, "lblSummary");
            this.lblSummary.Name = "lblSummary";
            //
            // linkLicense
            //
            resources.ApplyResources(this.linkLicense, "linkLicense");
            this.linkLicense.Name = "linkLicense";
            this.linkLicense.TabStop = true;
            this.linkLicense.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLicense_LinkClicked);
            //
            // cbAgreeToLicense
            //
            resources.ApplyResources(this.cbAgreeToLicense, "cbAgreeToLicense");
            this.cbAgreeToLicense.Name = "cbAgreeToLicense";
            this.cbAgreeToLicense.UseVisualStyleBackColor = true;
            this.cbAgreeToLicense.CheckedChanged += new System.EventHandler(this.cbAgreeToLicense_CheckedChanged);
            //
            // btnAccept
            //
            resources.ApplyResources(this.btnAccept, "btnAccept");
            this.btnAccept.Name = "btnAccept";
            this.btnAccept.UseVisualStyleBackColor = true;
            this.btnAccept.Click += new System.EventHandler(this.btnAccept_Click);
            //
            // btnSpecifyManually
            //
            resources.ApplyResources(this.btnSpecifyManually, "btnSpecifyManually");
            this.btnSpecifyManually.Name = "btnSpecifyManually";
            this.btnSpecifyManually.UseVisualStyleBackColor = true;
            this.btnSpecifyManually.Click += new System.EventHandler(this.btnSpecifyManually_Click);
            //
            // btnCancel
            //
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // DiannDownloadDlg
            //
            this.AcceptButton = this.btnAccept;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSpecifyManually);
            this.Controls.Add(this.btnAccept);
            this.Controls.Add(this.cbAgreeToLicense);
            this.Controls.Add(this.linkLicense);
            this.Controls.Add(this.lblSummary);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DiannDownloadDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.LinkLabel linkLicense;
        private System.Windows.Forms.CheckBox cbAgreeToLicense;
        private System.Windows.Forms.Button btnAccept;
        private System.Windows.Forms.Button btnSpecifyManually;
        private System.Windows.Forms.Button btnCancel;
    }
}
