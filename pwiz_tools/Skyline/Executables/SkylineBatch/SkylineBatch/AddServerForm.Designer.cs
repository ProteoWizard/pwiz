namespace SkylineBatch
{
    partial class AddServerForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddServerForm));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textUrl = new System.Windows.Forms.TextBox();
            this.textPassword = new System.Windows.Forms.TextBox();
            this.textUserName = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.textNamingPattern = new System.Windows.Forms.TextBox();
            this.btnRemoveServer = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.linkLabelRegex = new System.Windows.Forms.LinkLabel();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.listBoxFileNames = new System.Windows.Forms.ListBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            this.toolTip1.SetToolTip(this.label1, resources.GetString("label1.ToolTip"));
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textUrl
            // 
            resources.ApplyResources(this.textUrl, "textUrl");
            this.textUrl.Name = "textUrl";
            this.textUrl.TextChanged += new System.EventHandler(this.text_TextChanged);
            // 
            // textPassword
            // 
            resources.ApplyResources(this.textPassword, "textPassword");
            this.textPassword.Name = "textPassword";
            this.textPassword.TextChanged += new System.EventHandler(this.text_TextChanged);
            // 
            // textUserName
            // 
            resources.ApplyResources(this.textUserName, "textUserName");
            this.textUserName.Name = "textUserName";
            this.textUserName.TextChanged += new System.EventHandler(this.text_TextChanged);
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
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.label2);
            this.splitContainer1.Panel1.Controls.Add(this.textUserName);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.label3);
            this.splitContainer1.Panel2.Controls.Add(this.textPassword);
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
            this.listBoxFileNames.FormattingEnabled = true;
            this.listBoxFileNames.Name = "listBoxFileNames";
            // 
            // AddServerForm
            // 
            this.AcceptButton = this.btnSave;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.listBoxFileNames);
            this.Controls.Add(this.btnUpdate);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.btnRemoveServer);
            this.Controls.Add(this.textNamingPattern);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.textUrl);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.linkLabelRegex);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AddServerForm";
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.AddServerForm_FormClosing);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textUrl;
        private System.Windows.Forms.TextBox textPassword;
        private System.Windows.Forms.TextBox textUserName;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TextBox textNamingPattern;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button btnRemoveServer;
        private System.Windows.Forms.LinkLabel linkLabelRegex;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.ListBox listBoxFileNames;
    }
}