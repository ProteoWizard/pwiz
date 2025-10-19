namespace pwiz.Skyline.ToolsUI
{
    partial class ToolStoreDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ToolStoreDlg));
            this.pictureBoxTool = new System.Windows.Forms.PictureBox();
            this.labelAuthors = new System.Windows.Forms.Label();
            this.labelProvider = new System.Windows.Forms.Label();
            this.labelDescription = new System.Windows.Forms.Label();
            this.textBoxDescription = new System.Windows.Forms.TextBox();
            this.listBoxTools = new System.Windows.Forms.ListBox();
            this.buttonExit = new System.Windows.Forms.Button();
            this.buttonInstallUpdate = new System.Windows.Forms.Button();
            this.labelStatus = new System.Windows.Forms.Label();
            this.textBoxAuthors = new System.Windows.Forms.TextBox();
            this.linkLabelProvider = new System.Windows.Forms.LinkLabel();
            this.textBoxStatus = new System.Windows.Forms.TextBox();
            this.textBoxOrganization = new System.Windows.Forms.TextBox();
            this.labelOrganization = new System.Windows.Forms.Label();
            this.textBoxLanguages = new System.Windows.Forms.TextBox();
            this.labelLanguages = new System.Windows.Forms.Label();
            this.buttonToolStore = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxTool)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBoxTool
            // 
            resources.ApplyResources(this.pictureBoxTool, "pictureBoxTool");
            this.pictureBoxTool.Name = "pictureBoxTool";
            this.pictureBoxTool.TabStop = false;
            // 
            // labelAuthors
            // 
            resources.ApplyResources(this.labelAuthors, "labelAuthors");
            this.labelAuthors.Name = "labelAuthors";
            // 
            // labelProvider
            // 
            resources.ApplyResources(this.labelProvider, "labelProvider");
            this.labelProvider.Name = "labelProvider";
            // 
            // labelDescription
            // 
            resources.ApplyResources(this.labelDescription, "labelDescription");
            this.labelDescription.Name = "labelDescription";
            // 
            // textBoxDescription
            // 
            resources.ApplyResources(this.textBoxDescription, "textBoxDescription");
            this.textBoxDescription.Name = "textBoxDescription";
            this.textBoxDescription.ReadOnly = true;
            // 
            // listBoxTools
            // 
            resources.ApplyResources(this.listBoxTools, "listBoxTools");
            this.listBoxTools.FormattingEnabled = true;
            this.listBoxTools.Name = "listBoxTools";
            this.listBoxTools.SelectedIndexChanged += new System.EventHandler(this.listBoxTools_SelectedIndexChanged);
            // 
            // buttonExit
            // 
            resources.ApplyResources(this.buttonExit, "buttonExit");
            this.buttonExit.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonExit.Name = "buttonExit";
            this.buttonExit.UseVisualStyleBackColor = true;
            // 
            // buttonInstallUpdate
            // 
            resources.ApplyResources(this.buttonInstallUpdate, "buttonInstallUpdate");
            this.buttonInstallUpdate.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonInstallUpdate.Name = "buttonInstallUpdate";
            this.buttonInstallUpdate.UseVisualStyleBackColor = true;
            this.buttonInstallUpdate.Click += new System.EventHandler(this.buttonInstallUpdate_Click);
            // 
            // labelStatus
            // 
            resources.ApplyResources(this.labelStatus, "labelStatus");
            this.labelStatus.Name = "labelStatus";
            // 
            // textBoxAuthors
            // 
            resources.ApplyResources(this.textBoxAuthors, "textBoxAuthors");
            this.textBoxAuthors.Name = "textBoxAuthors";
            this.textBoxAuthors.ReadOnly = true;
            // 
            // linkLabelProvider
            // 
            resources.ApplyResources(this.linkLabelProvider, "linkLabelProvider");
            this.linkLabelProvider.Name = "linkLabelProvider";
            this.linkLabelProvider.TabStop = true;
            this.linkLabelProvider.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelProvider_LinkClicked);
            // 
            // textBoxStatus
            // 
            resources.ApplyResources(this.textBoxStatus, "textBoxStatus");
            this.textBoxStatus.Name = "textBoxStatus";
            this.textBoxStatus.ReadOnly = true;
            // 
            // textBoxOrganization
            // 
            resources.ApplyResources(this.textBoxOrganization, "textBoxOrganization");
            this.textBoxOrganization.Name = "textBoxOrganization";
            this.textBoxOrganization.ReadOnly = true;
            // 
            // labelOrganization
            // 
            resources.ApplyResources(this.labelOrganization, "labelOrganization");
            this.labelOrganization.Name = "labelOrganization";
            // 
            // textBoxLanguages
            // 
            resources.ApplyResources(this.textBoxLanguages, "textBoxLanguages");
            this.textBoxLanguages.Name = "textBoxLanguages";
            this.textBoxLanguages.ReadOnly = true;
            // 
            // labelLanguages
            // 
            resources.ApplyResources(this.labelLanguages, "labelLanguages");
            this.labelLanguages.Name = "labelLanguages";
            // 
            // buttonToolStore
            // 
            resources.ApplyResources(this.buttonToolStore, "buttonToolStore");
            this.buttonToolStore.Name = "buttonToolStore";
            this.buttonToolStore.UseVisualStyleBackColor = true;
            this.buttonToolStore.Click += new System.EventHandler(this.buttonToolStore_Click);
            // 
            // ToolStoreDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonExit;
            this.Controls.Add(this.buttonToolStore);
            this.Controls.Add(this.textBoxLanguages);
            this.Controls.Add(this.labelLanguages);
            this.Controls.Add(this.textBoxOrganization);
            this.Controls.Add(this.labelOrganization);
            this.Controls.Add(this.textBoxStatus);
            this.Controls.Add(this.linkLabelProvider);
            this.Controls.Add(this.textBoxAuthors);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.buttonInstallUpdate);
            this.Controls.Add(this.buttonExit);
            this.Controls.Add(this.listBoxTools);
            this.Controls.Add(this.textBoxDescription);
            this.Controls.Add(this.labelDescription);
            this.Controls.Add(this.labelProvider);
            this.Controls.Add(this.labelAuthors);
            this.Controls.Add(this.pictureBoxTool);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ToolStoreDlg";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.ToolStore_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxTool)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBoxTool;
        private System.Windows.Forms.Label labelAuthors;
        private System.Windows.Forms.Label labelProvider;
        private System.Windows.Forms.Label labelDescription;
        private System.Windows.Forms.TextBox textBoxDescription;
        private System.Windows.Forms.ListBox listBoxTools;
        private System.Windows.Forms.Button buttonExit;
        private System.Windows.Forms.Button buttonInstallUpdate;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.TextBox textBoxAuthors;
        private System.Windows.Forms.LinkLabel linkLabelProvider;
        private System.Windows.Forms.TextBox textBoxStatus;
        private System.Windows.Forms.TextBox textBoxOrganization;
        private System.Windows.Forms.Label labelOrganization;
        private System.Windows.Forms.TextBox textBoxLanguages;
        private System.Windows.Forms.Label labelLanguages;
        private System.Windows.Forms.Button buttonToolStore;
    }
}
