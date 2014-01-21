using System;
using System.Drawing;

namespace pwiz.Skyline.Alerts
{
    partial class AboutDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutDlg));
            this.linkProteome = new System.Windows.Forms.LinkLabel();
            this.btnOk = new System.Windows.Forms.Button();
            this.pictureSkylineIcon = new System.Windows.Forms.PictureBox();
            this.labelSoftwareVersion = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.pictureProteoWizardIcon = new System.Windows.Forms.PictureBox();
            this.linkProteoWizard = new System.Windows.Forms.LinkLabel();
            this.label3 = new System.Windows.Forms.Label();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureSkylineIcon)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureProteoWizardIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // linkProteome
            // 
            resources.ApplyResources(this.linkProteome, "linkProteome");
            this.linkProteome.Name = "linkProteome";
            this.linkProteome.TabStop = true;
            this.linkProteome.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkProteome_LinkClicked);
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // pictureSkylineIcon
            // 
            resources.ApplyResources(this.pictureSkylineIcon, "pictureSkylineIcon");
            this.pictureSkylineIcon.Name = "pictureSkylineIcon";
            this.pictureSkylineIcon.TabStop = false;
            // 
            // labelSoftwareVersion
            // 
            resources.ApplyResources(this.labelSoftwareVersion, "labelSoftwareVersion");
            this.labelSoftwareVersion.Name = "labelSoftwareVersion";
            // 
            // textBox1
            // 
            resources.ApplyResources(this.textBox1, "textBox1");
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // pictureProteoWizardIcon
            // 
            resources.ApplyResources(this.pictureProteoWizardIcon, "pictureProteoWizardIcon");
            this.pictureProteoWizardIcon.Name = "pictureProteoWizardIcon";
            this.pictureProteoWizardIcon.TabStop = false;
            // 
            // linkProteoWizard
            // 
            resources.ApplyResources(this.linkProteoWizard, "linkProteoWizard");
            this.linkProteoWizard.Name = "linkProteoWizard";
            this.linkProteoWizard.TabStop = true;
            this.linkProteoWizard.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkProteoWizard_LinkClicked);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // linkLabel1
            // 
            resources.ApplyResources(this.linkLabel1, "linkLabel1");
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.TabStop = true;
            this.linkLabel1.UseCompatibleTextRendering = true;
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // AboutDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.Controls.Add(this.label3);
            this.Controls.Add(this.linkProteoWizard);
            this.Controls.Add(this.pictureProteoWizardIcon);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.labelSoftwareVersion);
            this.Controls.Add(this.pictureSkylineIcon);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.linkProteome);
            this.Controls.Add(this.linkLabel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.pictureSkylineIcon)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureProteoWizardIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.LinkLabel linkProteome;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.PictureBox pictureSkylineIcon;
        private System.Windows.Forms.Label labelSoftwareVersion;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.PictureBox pictureProteoWizardIcon;
        private System.Windows.Forms.LinkLabel linkProteoWizard;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.LinkLabel linkLabel1;
    }
}