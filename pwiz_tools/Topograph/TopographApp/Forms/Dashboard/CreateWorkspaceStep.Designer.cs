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
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panelWorkspaceOpen.SuspendLayout();
            this.groupBoxOpenRecent.SuspendLayout();
            this.panelNoWorkspace.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
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
            this.btnCloseWorkspace.Click += new System.EventHandler(this.btnCloseWorkspace_Click);
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
            this.btnOpenDifferentWorkspace.Click += new System.EventHandler(this.btnOpenDifferentWorkspace_Click);
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
            this.groupBoxOpenRecent.Location = new System.Drawing.Point(0, 32);
            this.groupBoxOpenRecent.Name = "groupBoxOpenRecent";
            this.groupBoxOpenRecent.Size = new System.Drawing.Size(1156, 100);
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
            this.listBoxRecentWorkspaces.Size = new System.Drawing.Size(1150, 78);
            this.listBoxRecentWorkspaces.TabIndex = 1;
            this.listBoxRecentWorkspaces.SelectedIndexChanged += new System.EventHandler(this.listBoxRecentWorkspaces_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.Dock = System.Windows.Forms.DockStyle.Top;
            this.label2.Location = new System.Drawing.Point(0, 132);
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
            // 
            // label3
            // 
            this.label3.Dock = System.Windows.Forms.DockStyle.Top;
            this.label3.Location = new System.Drawing.Point(0, 190);
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
            // 
            // panelNoWorkspace
            // 
            this.panelNoWorkspace.Controls.Add(this.panel2);
            this.panelNoWorkspace.Controls.Add(this.label3);
            this.panelNoWorkspace.Controls.Add(this.panel1);
            this.panelNoWorkspace.Controls.Add(this.label2);
            this.panelNoWorkspace.Controls.Add(this.groupBoxOpenRecent);
            this.panelNoWorkspace.Controls.Add(this.label1);
            this.panelNoWorkspace.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelNoWorkspace.Location = new System.Drawing.Point(0, 58);
            this.panelNoWorkspace.Name = "panelNoWorkspace";
            this.panelNoWorkspace.Size = new System.Drawing.Size(1156, 255);
            this.panelNoWorkspace.TabIndex = 3;
            // 
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.btnCreateLocalWorkspace);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 164);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1156, 26);
            this.panel1.TabIndex = 4;
            // 
            // panel2
            // 
            this.panel2.AutoSize = true;
            this.panel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel2.Controls.Add(this.btnCreateOnlineWorkspace);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 222);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(1156, 26);
            this.panel2.TabIndex = 4;
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
            this.Size = new System.Drawing.Size(1156, 520);
            this.panelWorkspaceOpen.ResumeLayout(false);
            this.panelWorkspaceOpen.PerformLayout();
            this.groupBoxOpenRecent.ResumeLayout(false);
            this.panelNoWorkspace.ResumeLayout(false);
            this.panelNoWorkspace.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
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
    }
}
