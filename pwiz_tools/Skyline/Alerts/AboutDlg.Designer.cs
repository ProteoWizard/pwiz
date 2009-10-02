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
            this.label1 = new System.Windows.Forms.Label();
            this.labelSoftwareVersion = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureSkylineIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // linkProteome
            // 
            this.linkProteome.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.linkProteome.AutoSize = true;
            this.linkProteome.Location = new System.Drawing.Point(12, 284);
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
            this.btnOk.Location = new System.Drawing.Point(401, 279);
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
            this.pictureSkylineIcon.Size = new System.Drawing.Size(115, 150);
            this.pictureSkylineIcon.TabIndex = 2;
            this.pictureSkylineIcon.TabStop = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(139, 35);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(339, 91);
            this.label1.TabIndex = 3;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // labelSoftwareVersion
            // 
            this.labelSoftwareVersion.AutoSize = true;
            this.labelSoftwareVersion.Location = new System.Drawing.Point(139, 12);
            this.labelSoftwareVersion.Name = "labelSoftwareVersion";
            this.labelSoftwareVersion.Size = new System.Drawing.Size(41, 13);
            this.labelSoftwareVersion.TabIndex = 4;
            this.labelSoftwareVersion.Text = "Skyline";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(139, 163);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBox1.Size = new System.Drawing.Size(322, 101);
            this.textBox1.TabIndex = 5;
            this.textBox1.Text = resources.GetString("textBox1.Text");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(139, 148);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(151, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Software Dependency Credits:";
            // 
            // AboutDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.ClientSize = new System.Drawing.Size(488, 314);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.labelSoftwareVersion);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pictureSkylineIcon);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.linkProteome);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About Skyline";
            ((System.ComponentModel.ISupportInitialize)(this.pictureSkylineIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.LinkLabel linkProteome;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.PictureBox pictureSkylineIcon;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelSoftwareVersion;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label2;
    }
}