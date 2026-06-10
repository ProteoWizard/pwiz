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
            this.rb2x = new System.Windows.Forms.RadioButton();
            this.linkLabel2x = new System.Windows.Forms.LinkLabel();
            this.rb191 = new System.Windows.Forms.RadioButton();
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
            // rb2x
            //
            resources.ApplyResources(this.rb2x, "rb2x");
            this.rb2x.Name = "rb2x";
            this.rb2x.TabStop = true;
            this.rb2x.UseVisualStyleBackColor = true;
            this.rb2x.CheckedChanged += new System.EventHandler(this.versionRadio_CheckedChanged);
            //
            // linkLabel2x
            //
            resources.ApplyResources(this.linkLabel2x, "linkLabel2x");
            this.linkLabel2x.Name = "linkLabel2x";
            this.linkLabel2x.TabStop = true;
            this.linkLabel2x.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel2x_LinkClicked);
            //
            // rb191
            //
            resources.ApplyResources(this.rb191, "rb191");
            this.rb191.Name = "rb191";
            this.rb191.TabStop = true;
            this.rb191.UseVisualStyleBackColor = true;
            this.rb191.CheckedChanged += new System.EventHandler(this.versionRadio_CheckedChanged);
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
            this.Controls.Add(this.rb191);
            this.Controls.Add(this.linkLabel2x);
            this.Controls.Add(this.rb2x);
            this.Controls.Add(this.lblSummary);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
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
        private System.Windows.Forms.RadioButton rb2x;
        private System.Windows.Forms.LinkLabel linkLabel2x;
        private System.Windows.Forms.RadioButton rb191;
        private System.Windows.Forms.CheckBox cbAgreeToLicense;
        private System.Windows.Forms.Button btnAccept;
        private System.Windows.Forms.Button btnSpecifyManually;
        private System.Windows.Forms.Button btnCancel;
    }
}
