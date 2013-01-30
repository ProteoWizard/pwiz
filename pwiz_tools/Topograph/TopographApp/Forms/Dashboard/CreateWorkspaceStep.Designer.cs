using System.Windows.Forms;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    partial class CreateWorkspaceStep
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panelWorkspaceOpen = new System.Windows.Forms.FlowLayoutPanel();
            this.labelCurrentWorkspace = new System.Windows.Forms.Label();
            this.btnCloseWorkspace = new System.Windows.Forms.Button();
            this.btnOpenDifferentWorkspace = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBoxOpenRecent = new System.Windows.Forms.GroupBox();
            this.listBoxRecentWorkspaces = new System.Windows.Forms.ListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnCreateLocalWorkspace = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.btnCreateOnlineWorkspace = new System.Windows.Forms.Button();
            this.panelNoWorkspace = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.btnConnectToOnlineWorkspace = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel4 = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.panelWorkspaceOpen.SuspendLayout();
            this.groupBoxOpenRecent.SuspendLayout();
            this.panelNoWorkspace.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelWorkspaceOpen
            // 
            this.panelWorkspaceOpen.AutoSize = true;
            this.panelWorkspaceOpen.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelWorkspaceOpen.Controls.Add(this.labelCurrentWorkspace);
            this.panelWorkspaceOpen.Controls.Add(this.btnCloseWorkspace);
            this.panelWorkspaceOpen.Controls.Add(this.btnOpenDifferentWorkspace);
            this.panelWorkspaceOpen.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelWorkspaceOpen.Location = new System.Drawing.Point(0, 0);
            this.panelWorkspaceOpen.Name = "panelWorkspaceOpen";
            this.panelWorkspaceOpen.Size = new System.Drawing.Size(1156, 58);
            this.panelWorkspaceOpen.TabIndex = 0;
            // 
            // labelCurrentWorkspace
            // 
            this.labelCurrentWorkspace.AutoSize = true;
            this.panelWorkspaceOpen.SetFlowBreak(this.labelCurrentWorkspace, true);
            this.labelCurrentWorkspace.Location = new System.Drawing.Point(3, 0);
            this.labelCurrentWorkspace.Name = "labelCurrentWorkspace";
            this.labelCurrentWorkspace.Size = new System.Drawing.Size(152, 13);
            this.labelCurrentWorkspace.TabIndex = 0;
            this.labelCurrentWorkspace.Text = "The current workspace is Xxxx";
            // 
            // btnCloseWorkspace
            // 
            this.btnCloseWorkspace.AutoSize = true;
            this.btnCloseWorkspace.Location = new System.Drawing.Point(3, 32);
            this.btnCloseWorkspace.Name = "btnCloseWorkspace";
            this.btnCloseWorkspace.Size = new System.Drawing.Size(101, 23);
            this.btnCloseWorkspace.TabIndex = 1;
            this.btnCloseWorkspace.Text = "Close Workspace";
            this.btnCloseWorkspace.UseVisualStyleBackColor = true;
            this.btnCloseWorkspace.Click += new System.EventHandler(this.BtnCloseWorkspaceOnClick);
            // 
            // btnOpenDifferentWorkspace
            // 
            this.btnOpenDifferentWorkspace.AutoSize = true;
            this.btnOpenDifferentWorkspace.Location = new System.Drawing.Point(110, 32);
            this.btnOpenDifferentWorkspace.Name = "btnOpenDifferentWorkspace";
            this.btnOpenDifferentWorkspace.Size = new System.Drawing.Size(157, 23);
            this.btnOpenDifferentWorkspace.TabIndex = 2;
            this.btnOpenDifferentWorkspace.Text = "Open a different workspace...";
            this.btnOpenDifferentWorkspace.UseVisualStyleBackColor = true;
            this.btnOpenDifferentWorkspace.Click += new System.EventHandler(this.BtnOpenDifferentWorkspaceOnClick);
            // 
            // label1
            // 
            this.label1.AutoEllipsis = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(1156, 32);
            this.label1.TabIndex = 0;
            this.label1.Text = "There is no workspace open.  You can either create a new workspace or open an exi" +
                "sting one.";
            // 
            // groupBoxOpenRecent
            // 
            this.groupBoxOpenRecent.Controls.Add(this.listBoxRecentWorkspaces);
            this.groupBoxOpenRecent.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBoxOpenRecent.Location = new System.Drawing.Point(0, 58);
            this.groupBoxOpenRecent.Name = "groupBoxOpenRecent";
            this.groupBoxOpenRecent.Size = new System.Drawing.Size(1156, 73);
            this.groupBoxOpenRecent.TabIndex = 2;
            this.groupBoxOpenRecent.TabStop = false;
            this.groupBoxOpenRecent.Text = "You can open one of the workspaces that you have recently used";
            // 
            // listBoxRecentWorkspaces
            // 
            this.listBoxRecentWorkspaces.BackColor = System.Drawing.SystemColors.Control;
            this.listBoxRecentWorkspaces.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listBoxRecentWorkspaces.Cursor = System.Windows.Forms.Cursors.Hand;
            this.listBoxRecentWorkspaces.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxRecentWorkspaces.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBoxRecentWorkspaces.ForeColor = System.Drawing.Color.Blue;
            this.listBoxRecentWorkspaces.FormattingEnabled = true;
            this.listBoxRecentWorkspaces.Location = new System.Drawing.Point(3, 16);
            this.listBoxRecentWorkspaces.Name = "listBoxRecentWorkspaces";
            this.listBoxRecentWorkspaces.Size = new System.Drawing.Size(1150, 52);
            this.listBoxRecentWorkspaces.TabIndex = 1;
            this.listBoxRecentWorkspaces.SelectedIndexChanged += new System.EventHandler(this.ListBoxRecentWorkspacesOnSelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.Dock = System.Windows.Forms.DockStyle.Top;
            this.label2.Location = new System.Drawing.Point(0, 131);
            this.label2.MinimumSize = new System.Drawing.Size(400, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(1156, 32);
            this.label2.TabIndex = 1;
            this.label2.Text = "If you would like to create a workspace that is stored on your computer, that wil" +
                "l only be used by one person at a time, you can create a workspace file.";
            // 
            // btnCreateLocalWorkspace
            // 
            this.btnCreateLocalWorkspace.AutoSize = true;
            this.btnCreateLocalWorkspace.Location = new System.Drawing.Point(0, 0);
            this.btnCreateLocalWorkspace.Name = "btnCreateLocalWorkspace";
            this.btnCreateLocalWorkspace.Size = new System.Drawing.Size(134, 23);
            this.btnCreateLocalWorkspace.TabIndex = 2;
            this.btnCreateLocalWorkspace.Text = "Create Workspace File...";
            this.btnCreateLocalWorkspace.UseVisualStyleBackColor = true;
            this.btnCreateLocalWorkspace.Click += new System.EventHandler(this.BtnCreateLocalWorkspaceOnClick);
            // 
            // label3
            // 
            this.label3.Dock = System.Windows.Forms.DockStyle.Top;
            this.label3.Location = new System.Drawing.Point(0, 189);
            this.label3.MinimumSize = new System.Drawing.Size(400, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(1156, 32);
            this.label3.TabIndex = 3;
            this.label3.Text = "If you would like to allow other people to use your workspace, and you have acces" +
                "s to a database (MySQL) server, you can create an online workspace.";
            // 
            // btnCreateOnlineWorkspace
            // 
            this.btnCreateOnlineWorkspace.AutoSize = true;
            this.btnCreateOnlineWorkspace.Location = new System.Drawing.Point(0, 0);
            this.btnCreateOnlineWorkspace.Name = "btnCreateOnlineWorkspace";
            this.btnCreateOnlineWorkspace.Size = new System.Drawing.Size(148, 23);
            this.btnCreateOnlineWorkspace.TabIndex = 4;
            this.btnCreateOnlineWorkspace.Text = "Create Online Workspace...";
            this.btnCreateOnlineWorkspace.UseVisualStyleBackColor = true;
            this.btnCreateOnlineWorkspace.Click += new System.EventHandler(this.BtnCreateOnlineWorkspaceOnClick);
            // 
            // panelNoWorkspace
            // 
            this.panelNoWorkspace.Controls.Add(this.panel3);
            this.panelNoWorkspace.Controls.Add(this.label4);
            this.panelNoWorkspace.Controls.Add(this.panel2);
            this.panelNoWorkspace.Controls.Add(this.label3);
            this.panelNoWorkspace.Controls.Add(this.panel1);
            this.panelNoWorkspace.Controls.Add(this.label2);
            this.panelNoWorkspace.Controls.Add(this.groupBoxOpenRecent);
            this.panelNoWorkspace.Controls.Add(this.panel4);
            this.panelNoWorkspace.Controls.Add(this.label1);
            this.panelNoWorkspace.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelNoWorkspace.Location = new System.Drawing.Point(0, 58);
            this.panelNoWorkspace.Name = "panelNoWorkspace";
            this.panelNoWorkspace.Size = new System.Drawing.Size(1156, 298);
            this.panelNoWorkspace.TabIndex = 3;
            // 
            // panel3
            // 
            this.panel3.AutoSize = true;
            this.panel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel3.Controls.Add(this.btnConnectToOnlineWorkspace);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel3.Location = new System.Drawing.Point(0, 270);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1156, 26);
            this.panel3.TabIndex = 6;
            // 
            // btnConnectToOnlineWorkspace
            // 
            this.btnConnectToOnlineWorkspace.AutoSize = true;
            this.btnConnectToOnlineWorkspace.Location = new System.Drawing.Point(0, 0);
            this.btnConnectToOnlineWorkspace.Name = "btnConnectToOnlineWorkspace";
            this.btnConnectToOnlineWorkspace.Size = new System.Drawing.Size(222, 23);
            this.btnConnectToOnlineWorkspace.TabIndex = 4;
            this.btnConnectToOnlineWorkspace.Text = "Connect to an existing Online Workspace...";
            this.btnConnectToOnlineWorkspace.UseVisualStyleBackColor = true;
            this.btnConnectToOnlineWorkspace.Click += new System.EventHandler(this.BtnConnectToOnlineWorkspaceOnClick);
            // 
            // label4
            // 
            this.label4.Dock = System.Windows.Forms.DockStyle.Top;
            this.label4.Location = new System.Drawing.Point(0, 247);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(1156, 23);
            this.label4.TabIndex = 5;
            this.label4.Text = "If you know the server name, user name, and password you can connect to an online" +
                " workspace.";
            // 
            // panel2
            // 
            this.panel2.AutoSize = true;
            this.panel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel2.Controls.Add(this.btnCreateOnlineWorkspace);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 221);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(1156, 26);
            this.panel2.TabIndex = 4;
            // 
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.btnCreateLocalWorkspace);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 163);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1156, 26);
            this.panel1.TabIndex = 4;
            // 
            // panel4
            // 
            this.panel4.AutoSize = true;
            this.panel4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel4.Controls.Add(this.label5);
            this.panel4.Controls.Add(this.btnBrowse);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel4.Location = new System.Drawing.Point(0, 32);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(1156, 26);
            this.panel4.TabIndex = 5;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 5);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(277, 13);
            this.label5.TabIndex = 3;
            this.label5.Text = "You can browse for a .tpg or .tpglnk file on your computer";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnBrowse
            // 
            this.btnBrowse.AutoSize = true;
            this.btnBrowse.Location = new System.Drawing.Point(286, 0);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(90, 23);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.BtnBrowseOnClick);
            // 
            // CreateWorkspaceStep
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.panelNoWorkspace);
            this.Controls.Add(this.panelWorkspaceOpen);
            this.MinimumSize = new System.Drawing.Size(400, 0);
            this.Name = "CreateWorkspaceStep";
            this.Size = new System.Drawing.Size(1156, 357);
            this.panelWorkspaceOpen.ResumeLayout(false);
            this.panelWorkspaceOpen.PerformLayout();
            this.groupBoxOpenRecent.ResumeLayout(false);
            this.panelNoWorkspace.ResumeLayout(false);
            this.panelNoWorkspace.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private FlowLayoutPanel panelWorkspaceOpen;
        private Label labelCurrentWorkspace;
        private Button btnCloseWorkspace;
        private Button btnOpenDifferentWorkspace;
        private Label label1;
        private Button btnCreateLocalWorkspace;
        private Label label2;
        private Label label3;
        private Button btnCreateOnlineWorkspace;
        private GroupBox groupBoxOpenRecent;
        private ListBox listBoxRecentWorkspaces;
        private Panel panelNoWorkspace;
        private Panel panel1;
        private Panel panel2;
        private Label label4;
        private Panel panel3;
        private Button btnConnectToOnlineWorkspace;
        private Panel panel4;
        private Label label5;
        private Button btnBrowse;
    }
}
