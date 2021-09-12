namespace SkylineBatch
{
    partial class DataServerForm
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DataServerForm));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.textNamingPattern = new System.Windows.Forms.TextBox();
            this.btnRemoveServer = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.linkLabelRegex = new System.Windows.Forms.LinkLabel();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.labelFileInfo = new System.Windows.Forms.Label();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.listBoxFileNames = new System.Windows.Forms.ListBox();
            this.panelRemoteFile = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            resources.ApplyResources(this.btnSave, "btnSave");
            this.btnSave.Name = "btnSave";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // textNamingPattern
            // 
            resources.ApplyResources(this.textNamingPattern, "textNamingPattern");
            this.textNamingPattern.Name = "textNamingPattern";
            this.textNamingPattern.TextChanged += new System.EventHandler(this.textNamingPattern_TextChanged);
            // 
            // btnRemoveServer
            // 
            resources.ApplyResources(this.btnRemoveServer, "btnRemoveServer");
            this.btnRemoveServer.Name = "btnRemoveServer";
            this.toolTip1.SetToolTip(this.btnRemoveServer, resources.GetString("btnRemoveServer.ToolTip"));
            this.btnRemoveServer.UseVisualStyleBackColor = true;
            this.btnRemoveServer.Click += new System.EventHandler(this.btnRemoveServer_Click);
            // 
            // linkLabelRegex
            // 
            resources.ApplyResources(this.linkLabelRegex, "linkLabelRegex");
            this.linkLabelRegex.Name = "linkLabelRegex";
            this.linkLabelRegex.TabStop = true;
            this.toolTip1.SetToolTip(this.linkLabelRegex, resources.GetString("linkLabelRegex.ToolTip"));
            this.linkLabelRegex.UseCompatibleTextRendering = true;
            this.linkLabelRegex.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelRegex_LinkClicked);
            // 
            // linkLabel1
            // 
            resources.ApplyResources(this.linkLabel1, "linkLabel1");
            this.linkLabel1.Name = "linkLabel1";
            this.toolTip1.SetToolTip(this.linkLabel1, resources.GetString("linkLabel1.ToolTip"));
            this.linkLabel1.UseCompatibleTextRendering = true;
            // 
            // labelFileInfo
            // 
            resources.ApplyResources(this.labelFileInfo, "labelFileInfo");
            this.labelFileInfo.Name = "labelFileInfo";
            this.toolTip1.SetToolTip(this.labelFileInfo, resources.GetString("labelFileInfo.ToolTip"));
            // 
            // btnUpdate
            // 
            resources.ApplyResources(this.btnUpdate, "btnUpdate");
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.UseVisualStyleBackColor = true;
            this.btnUpdate.Click += new System.EventHandler(this.btnUpdate_Click);
            // 
            // listBoxFileNames
            // 
            resources.ApplyResources(this.listBoxFileNames, "listBoxFileNames");
            this.listBoxFileNames.BackColor = System.Drawing.Color.White;
            this.listBoxFileNames.Name = "listBoxFileNames";
            // 
            // panelRemoteFile
            // 
            resources.ApplyResources(this.panelRemoteFile, "panelRemoteFile");
            this.panelRemoteFile.Name = "panelRemoteFile";
            // 
            // DataServerForm
            // 
            this.AcceptButton = this.btnSave;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.labelFileInfo);
            this.Controls.Add(this.listBoxFileNames);
            this.Controls.Add(this.btnUpdate);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.btnRemoveServer);
            this.Controls.Add(this.textNamingPattern);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.linkLabelRegex);
            this.Controls.Add(this.panelRemoteFile);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DataServerForm";
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.AddServerForm_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.TextBox textNamingPattern;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button btnRemoveServer;
        private System.Windows.Forms.LinkLabel linkLabelRegex;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.ListBox listBoxFileNames;
        private System.Windows.Forms.Label labelFileInfo;
        private System.Windows.Forms.Panel panelRemoteFile;
    }
}