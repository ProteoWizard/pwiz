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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditLibraryDlg));
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
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textPath
            // 
            resources.ApplyResources(this.textPath, "textPath");
            this.textPath.Name = "textPath";
            this.textPath.TextChanged += new System.EventHandler(this.textPath_TextChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // labelSpecLibLinks
            // 
            resources.ApplyResources(this.labelSpecLibLinks, "labelSpecLibLinks");
            this.labelSpecLibLinks.Name = "labelSpecLibLinks";
            // 
            // linkPeptideAtlas
            // 
            resources.ApplyResources(this.linkPeptideAtlas, "linkPeptideAtlas");
            this.linkPeptideAtlas.Name = "linkPeptideAtlas";
            this.linkPeptideAtlas.TabStop = true;
            this.linkPeptideAtlas.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkPeptideAtlas_LinkClicked);
            // 
            // linkNIST
            // 
            resources.ApplyResources(this.linkNIST, "linkNIST");
            this.linkNIST.Name = "linkNIST";
            this.linkNIST.TabStop = true;
            this.linkNIST.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkNIST_LinkClicked);
            // 
            // linkGPM
            // 
            resources.ApplyResources(this.linkGPM, "linkGPM");
            this.linkGPM.Name = "linkGPM";
            this.linkGPM.TabStop = true;
            this.linkGPM.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkGPM_LinkClicked);
            // 
            // EditLibraryDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
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