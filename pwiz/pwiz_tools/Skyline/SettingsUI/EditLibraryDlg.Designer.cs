namespace pwiz.Skyline.SettingsUI
{
    partial class EditLibraryDlg
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
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textPath = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.labelSpecLibLinks = new System.Windows.Forms.Label();
            this.linkPeptideAtlas = new System.Windows.Forms.LinkLabel();
            this.linkNIST = new System.Windows.Forms.LinkLabel();
            this.linkGPM = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // textName
            // 
            this.textName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textName.Location = new System.Drawing.Point(15, 28);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(247, 20);
            this.textName.TabIndex = 3;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 12);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "&Name:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(288, 42);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(288, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 7;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textPath
            // 
            this.textPath.Location = new System.Drawing.Point(15, 95);
            this.textPath.Name = "textPath";
            this.textPath.Size = new System.Drawing.Size(245, 20);
            this.textPath.TabIndex = 5;
            this.textPath.TextChanged += new System.EventHandler(this.textPath_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 76);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(32, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "&Path:";
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(288, 93);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(75, 23);
            this.btnBrowse.TabIndex = 6;
            this.btnBrowse.Text = "&Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // labelSpecLibLinks
            // 
            this.labelSpecLibLinks.AutoSize = true;
            this.labelSpecLibLinks.Location = new System.Drawing.Point(12, 131);
            this.labelSpecLibLinks.Name = "labelSpecLibLinks";
            this.labelSpecLibLinks.Size = new System.Drawing.Size(111, 13);
            this.labelSpecLibLinks.TabIndex = 9;
            this.labelSpecLibLinks.Text = "Spectral Library Links:";
            // 
            // linkPeptideAtlas
            // 
            this.linkPeptideAtlas.AutoSize = true;
            this.linkPeptideAtlas.Location = new System.Drawing.Point(12, 150);
            this.linkPeptideAtlas.Name = "linkPeptideAtlas";
            this.linkPeptideAtlas.Size = new System.Drawing.Size(66, 13);
            this.linkPeptideAtlas.TabIndex = 10;
            this.linkPeptideAtlas.TabStop = true;
            this.linkPeptideAtlas.Text = "PeptideAtlas";
            this.linkPeptideAtlas.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkPeptideAtlas_LinkClicked);
            // 
            // linkNIST
            // 
            this.linkNIST.AutoSize = true;
            this.linkNIST.Location = new System.Drawing.Point(96, 150);
            this.linkNIST.Name = "linkNIST";
            this.linkNIST.Size = new System.Drawing.Size(32, 13);
            this.linkNIST.TabIndex = 11;
            this.linkNIST.TabStop = true;
            this.linkNIST.Text = "NIST";
            this.linkNIST.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkNIST_LinkClicked);
            // 
            // linkGPM
            // 
            this.linkGPM.AutoSize = true;
            this.linkGPM.Location = new System.Drawing.Point(146, 150);
            this.linkGPM.Name = "linkGPM";
            this.linkGPM.Size = new System.Drawing.Size(31, 13);
            this.linkGPM.TabIndex = 12;
            this.linkGPM.TabStop = true;
            this.linkGPM.Text = "GPM";
            this.linkGPM.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkGPM_LinkClicked);
            // 
            // EditLibraryDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(377, 184);
            this.Controls.Add(this.linkGPM);
            this.Controls.Add(this.linkNIST);
            this.Controls.Add(this.linkPeptideAtlas);
            this.Controls.Add(this.labelSpecLibLinks);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textPath);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditLibraryDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Library";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label labelSpecLibLinks;
        private System.Windows.Forms.LinkLabel linkPeptideAtlas;
        private System.Windows.Forms.LinkLabel linkNIST;
        private System.Windows.Forms.LinkLabel linkGPM;
    }
}