namespace pwiz.Skyline.Alerts
{
    partial class UpgradeDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UpgradeDlg));
            this.btnLater = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.linkReleaseNotes = new System.Windows.Forms.LinkLabel();
            this.labelDetailAutomatic = new System.Windows.Forms.Label();
            this.labelDirections = new System.Windows.Forms.Label();
            this.labelDetail = new System.Windows.Forms.Label();
            this.labelRelease = new System.Windows.Forms.Label();
            this.pictureSkyline = new System.Windows.Forms.PictureBox();
            this.btnInstall = new System.Windows.Forms.Button();
            this.cbAtStartup = new System.Windows.Forms.CheckBox();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureSkyline)).BeginInit();
            this.SuspendLayout();
            // 
            // btnLater
            // 
            resources.ApplyResources(this.btnLater, "btnLater");
            this.btnLater.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnLater.Name = "btnLater";
            this.btnLater.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.BackColor = System.Drawing.SystemColors.Window;
            this.panel1.Controls.Add(this.linkReleaseNotes);
            this.panel1.Controls.Add(this.labelDetailAutomatic);
            this.panel1.Controls.Add(this.labelDirections);
            this.panel1.Controls.Add(this.labelDetail);
            this.panel1.Controls.Add(this.labelRelease);
            this.panel1.Controls.Add(this.pictureSkyline);
            this.panel1.Name = "panel1";
            // 
            // linkReleaseNotes
            // 
            resources.ApplyResources(this.linkReleaseNotes, "linkReleaseNotes");
            this.linkReleaseNotes.Name = "linkReleaseNotes";
            this.linkReleaseNotes.TabStop = true;
            this.linkReleaseNotes.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkReleaseNotes_LinkClicked);
            // 
            // labelDetailAutomatic
            // 
            resources.ApplyResources(this.labelDetailAutomatic, "labelDetailAutomatic");
            this.labelDetailAutomatic.Name = "labelDetailAutomatic";
            // 
            // labelDirections
            // 
            resources.ApplyResources(this.labelDirections, "labelDirections");
            this.labelDirections.Name = "labelDirections";
            // 
            // labelDetail
            // 
            resources.ApplyResources(this.labelDetail, "labelDetail");
            this.labelDetail.Name = "labelDetail";
            // 
            // labelRelease
            // 
            resources.ApplyResources(this.labelRelease, "labelRelease");
            this.labelRelease.Name = "labelRelease";
            // 
            // pictureSkyline
            // 
            resources.ApplyResources(this.pictureSkyline, "pictureSkyline");
            this.pictureSkyline.Name = "pictureSkyline";
            this.pictureSkyline.TabStop = false;
            // 
            // btnInstall
            // 
            resources.ApplyResources(this.btnInstall, "btnInstall");
            this.btnInstall.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnInstall.Name = "btnInstall";
            this.btnInstall.UseVisualStyleBackColor = true;
            // 
            // cbAtStartup
            // 
            resources.ApplyResources(this.cbAtStartup, "cbAtStartup");
            this.cbAtStartup.Checked = true;
            this.cbAtStartup.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbAtStartup.Name = "cbAtStartup";
            this.cbAtStartup.UseVisualStyleBackColor = true;
            this.cbAtStartup.CheckedChanged += new System.EventHandler(this.cbAtStartup_CheckedChanged);
            // 
            // UpgradeDlg
            // 
            this.AcceptButton = this.btnInstall;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnLater;
            this.Controls.Add(this.cbAtStartup);
            this.Controls.Add(this.btnInstall);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.btnLater);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "UpgradeDlg";
            this.ShowInTaskbar = false;
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureSkyline)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnLater;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.PictureBox pictureSkyline;
        private System.Windows.Forms.Label labelDetail;
        private System.Windows.Forms.Label labelRelease;
        private System.Windows.Forms.Label labelDirections;
        private System.Windows.Forms.Button btnInstall;
        private System.Windows.Forms.CheckBox cbAtStartup;
        private System.Windows.Forms.Label labelDetailAutomatic;
        private System.Windows.Forms.LinkLabel linkReleaseNotes;
    }
}