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
            this.linkProteome.AutoSize = true;
            this.linkProteome.Location = new System.Drawing.Point(14, 201);
            this.linkProteome.Name = "linkProteome";
            this.linkProteome.Size = new System.Drawing.Size(143, 13);
            this.linkProteome.TabIndex = 0;
            this.linkProteome.TabStop = true;
            this.linkProteome.Text = "proteome.gs.washington.edu";
            this.linkProteome.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkProteome_LinkClicked);
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Location = new System.Drawing.Point(434, 305);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // pictureSkylineIcon
            // 
            this.pictureSkylineIcon.Image = global::pwiz.Skyline.Properties.Resources.SkylineImg;
            this.pictureSkylineIcon.Location = new System.Drawing.Point(15, 12);
            this.pictureSkylineIcon.Name = "pictureSkylineIcon";
            this.pictureSkylineIcon.Size = new System.Drawing.Size(142, 186);
            this.pictureSkylineIcon.TabIndex = 2;
            this.pictureSkylineIcon.TabStop = false;
            // 
            // labelSoftwareVersion
            // 
            this.labelSoftwareVersion.AutoSize = true;
            this.labelSoftwareVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelSoftwareVersion.Location = new System.Drawing.Point(162, 12);
            this.labelSoftwareVersion.Name = "labelSoftwareVersion";
            this.labelSoftwareVersion.Size = new System.Drawing.Size(66, 20);
            this.labelSoftwareVersion.TabIndex = 4;
            this.labelSoftwareVersion.Text = "Skyline";
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.Location = new System.Drawing.Point(163, 168);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBox1.Size = new System.Drawing.Size(346, 112);
            this.textBox1.TabIndex = 5;
            this.textBox1.Text = resources.GetString("textBox1.Text");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(163, 153);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(151, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Software Dependency Credits:";
            // 
            // pictureProteoWizardIcon
            // 
            this.pictureProteoWizardIcon.Image = global::pwiz.Skyline.Properties.Resources.ProteoWizard;
            this.pictureProteoWizardIcon.Location = new System.Drawing.Point(15, 240);
            this.pictureProteoWizardIcon.Name = "pictureProteoWizardIcon";
            this.pictureProteoWizardIcon.Size = new System.Drawing.Size(142, 72);
            this.pictureProteoWizardIcon.TabIndex = 7;
            this.pictureProteoWizardIcon.TabStop = false;
            // 
            // linkProteoWizard
            // 
            this.linkProteoWizard.AutoSize = true;
            this.linkProteoWizard.Location = new System.Drawing.Point(14, 315);
            this.linkProteoWizard.Name = "linkProteoWizard";
            this.linkProteoWizard.Size = new System.Drawing.Size(144, 13);
            this.linkProteoWizard.TabIndex = 8;
            this.linkProteoWizard.TabStop = true;
            this.linkProteoWizard.Text = "proteowizard.sourceforge.net";
            this.linkProteoWizard.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkProteoWizard_LinkClicked);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(166, 298);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(136, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "A ProteoWizard Application";
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.LinkArea = new System.Windows.Forms.LinkArea(246, 15);
            this.linkLabel1.Location = new System.Drawing.Point(164, 36);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(351, 92);
            this.linkLabel1.TabIndex = 10;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = resources.GetString("linkLabel1.Text");
            this.linkLabel1.UseCompatibleTextRendering = true;
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // AboutDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.ClientSize = new System.Drawing.Size(521, 344);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About Skyline";
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