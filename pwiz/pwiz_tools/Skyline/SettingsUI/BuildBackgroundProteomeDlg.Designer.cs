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
            this.btnOpen = new System.Windows.Forms.Button();
            this.labelFile = new System.Windows.Forms.Label();
            this.textPath = new System.Windows.Forms.TextBox();
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnAddFastaFile = new System.Windows.Forms.Button();
            this.tbxStatus = new System.Windows.Forms.TextBox();
            this.labelFasta = new System.Windows.Forms.Label();
            this.listboxFasta = new System.Windows.Forms.ListBox();
            this.btnCreate = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnOpen
            // 
            resources.ApplyResources(this.btnOpen, "btnOpen");
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
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
            // btnCreate
            // 
            resources.ApplyResources(this.btnCreate, "btnCreate");
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);
            // 
            // BuildBackgroundProteomeDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.listboxFasta);
            this.Controls.Add(this.labelFasta);
            this.Controls.Add(this.tbxStatus);
            this.Controls.Add(this.btnAddFastaFile);
            this.Controls.Add(this.btnOpen);
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

        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.Label labelFile;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnAddFastaFile;
        private System.Windows.Forms.TextBox tbxStatus;
        private System.Windows.Forms.Label labelFasta;
        private System.Windows.Forms.ListBox listboxFasta;
        private System.Windows.Forms.Button btnCreate;

    }
}