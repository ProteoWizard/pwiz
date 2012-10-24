namespace pwiz.Skyline.SettingsUI
{
    partial class BuildBackgroundProteomeDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BuildBackgroundProteomeDlg));
            this.btnBrowse = new System.Windows.Forms.Button();
            this.labelFile = new System.Windows.Forms.Label();
            this.textPath = new System.Windows.Forms.TextBox();
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnAddFastaFile = new System.Windows.Forms.Button();
            this.tbxStatus = new System.Windows.Forms.TextBox();
            this.btnBuild = new System.Windows.Forms.Button();
            this.labelFasta = new System.Windows.Forms.Label();
            this.listboxFasta = new System.Windows.Forms.ListBox();
            this.labelFileNew = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // labelFile
            // 
            resources.ApplyResources(this.labelFile, "labelFile");
            this.labelFile.Name = "labelFile";
            // 
            // textPath
            // 
            resources.ApplyResources(this.textPath, "textPath");
            this.textPath.Name = "textPath";
            this.textPath.TextChanged += new System.EventHandler(this.textPath_TextChanged);
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            this.textName.TextChanged += new System.EventHandler(this.textName_TextChanged);
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
            // btnAddFastaFile
            // 
            resources.ApplyResources(this.btnAddFastaFile, "btnAddFastaFile");
            this.btnAddFastaFile.Name = "btnAddFastaFile";
            this.btnAddFastaFile.UseVisualStyleBackColor = true;
            this.btnAddFastaFile.Click += new System.EventHandler(this.btnAddFastaFile_Click);
            // 
            // tbxStatus
            // 
            resources.ApplyResources(this.tbxStatus, "tbxStatus");
            this.tbxStatus.Name = "tbxStatus";
            this.tbxStatus.ReadOnly = true;
            // 
            // btnBuild
            // 
            resources.ApplyResources(this.btnBuild, "btnBuild");
            this.btnBuild.Name = "btnBuild";
            this.btnBuild.UseVisualStyleBackColor = true;
            this.btnBuild.Click += new System.EventHandler(this.btnBuild_Click);
            // 
            // labelFasta
            // 
            resources.ApplyResources(this.labelFasta, "labelFasta");
            this.labelFasta.Name = "labelFasta";
            // 
            // listboxFasta
            // 
            resources.ApplyResources(this.listboxFasta, "listboxFasta");
            this.listboxFasta.FormattingEnabled = true;
            this.listboxFasta.Name = "listboxFasta";
            // 
            // labelFileNew
            // 
            resources.ApplyResources(this.labelFileNew, "labelFileNew");
            this.labelFileNew.Name = "labelFileNew";
            // 
            // BuildBackgroundProteomeDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.labelFileNew);
            this.Controls.Add(this.listboxFasta);
            this.Controls.Add(this.labelFasta);
            this.Controls.Add(this.btnBuild);
            this.Controls.Add(this.tbxStatus);
            this.Controls.Add(this.btnAddFastaFile);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.labelFile);
            this.Controls.Add(this.textPath);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BuildBackgroundProteomeDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label labelFile;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnAddFastaFile;
        private System.Windows.Forms.TextBox tbxStatus;
        private System.Windows.Forms.Button btnBuild;
        private System.Windows.Forms.Label labelFasta;
        private System.Windows.Forms.ListBox listboxFasta;
        private System.Windows.Forms.Label labelFileNew;

    }
}