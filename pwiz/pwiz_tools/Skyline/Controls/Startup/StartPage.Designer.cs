using System.Drawing;

namespace pwiz.Skyline.Controls.Startup
{
    partial class StartPage
    {
        /// &lt;summary&gt;
        /// Required designer variable.
        /// &lt;/summary&gt;
        private System.ComponentModel.IContainer components = null;

        /// &lt;summary&gt;
        /// Clean up any resources being used.
        /// &lt;/summary&gt;
        /// &lt;param name="disposing"&gt;true if managed resources should be disposed; otherwise, false.&lt;/param&gt;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>;
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(StartPage));
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.wizardTabPage = new System.Windows.Forms.TabPage();
            this.flowLayoutPanelWizard = new System.Windows.Forms.FlowLayoutPanel();
            this.tutorialsTabPage = new System.Windows.Forms.TabPage();
            this.flowLayoutPanelTutorials = new System.Windows.Forms.FlowLayoutPanel();
            this.leftPanel = new System.Windows.Forms.Panel();
            this.skyline = new System.Windows.Forms.Label();
            this.lblRecent = new System.Windows.Forms.Label();
            this.recentFilesPanel = new System.Windows.Forms.Panel();
            this.leftBottomPanel = new System.Windows.Forms.Panel();
            this.checkBoxShowStartup = new System.Windows.Forms.CheckBox();
            this.openFilePanel = new System.Windows.Forms.Panel();
            this.openFileIcon = new System.Windows.Forms.PictureBox();
            this.openFileLabel = new System.Windows.Forms.Label();
            this.tabControlMain.SuspendLayout();
            this.wizardTabPage.SuspendLayout();
            this.tutorialsTabPage.SuspendLayout();
            this.leftPanel.SuspendLayout();
            this.leftBottomPanel.SuspendLayout();
            this.openFilePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.openFileIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControlMain
            // 
            this.tabControlMain.Controls.Add(this.wizardTabPage);
            this.tabControlMain.Controls.Add(this.tutorialsTabPage);
            resources.ApplyResources(this.tabControlMain, "tabControlMain");
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            // 
            // wizardTabPage
            // 
            this.wizardTabPage.Controls.Add(this.flowLayoutPanelWizard);
            resources.ApplyResources(this.wizardTabPage, "wizardTabPage");
            this.wizardTabPage.Name = "wizardTabPage";
            this.wizardTabPage.UseVisualStyleBackColor = true;
            // 
            // flowLayoutPanelWizard
            // 
            resources.ApplyResources(this.flowLayoutPanelWizard, "flowLayoutPanelWizard");
            this.flowLayoutPanelWizard.Name = "flowLayoutPanelWizard";
            this.flowLayoutPanelWizard.MouseEnter += new System.EventHandler(this.wizardTab_MouseHover);
            // 
            // tutorialsTabPage
            // 
            this.tutorialsTabPage.Controls.Add(this.flowLayoutPanelTutorials);
            resources.ApplyResources(this.tutorialsTabPage, "tutorialsTabPage");
            this.tutorialsTabPage.Name = "tutorialsTabPage";
            this.tutorialsTabPage.UseVisualStyleBackColor = true;
            // 
            // flowLayoutPanelTutorials
            // 
            resources.ApplyResources(this.flowLayoutPanelTutorials, "flowLayoutPanelTutorials");
            this.flowLayoutPanelTutorials.Name = "flowLayoutPanelTutorials";
            this.flowLayoutPanelTutorials.MouseEnter += new System.EventHandler(this.tutorialTab_MouseHover);
            // 
            // leftPanel
            // 
            this.leftPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(67)))), ((int)(((byte)(122)))), ((int)(((byte)(197)))));
            this.leftPanel.Controls.Add(this.skyline);
            this.leftPanel.Controls.Add(this.lblRecent);
            this.leftPanel.Controls.Add(this.recentFilesPanel);
            this.leftPanel.Controls.Add(this.leftBottomPanel);
            resources.ApplyResources(this.leftPanel, "leftPanel");
            this.leftPanel.Name = "leftPanel";
            // 
            // skyline
            // 
            resources.ApplyResources(this.skyline, "skyline");
            this.skyline.BackColor = System.Drawing.Color.Transparent;
            this.skyline.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
            this.skyline.Name = "skyline";
            // 
            // lblRecent
            // 
            resources.ApplyResources(this.lblRecent, "lblRecent");
            this.lblRecent.BackColor = System.Drawing.Color.Transparent;
            this.lblRecent.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
            this.lblRecent.Name = "lblRecent";
            // 
            // recentFilesPanel
            // 
            resources.ApplyResources(this.recentFilesPanel, "recentFilesPanel");
            this.recentFilesPanel.BackColor = System.Drawing.Color.Transparent;
            this.recentFilesPanel.Name = "recentFilesPanel";
            // 
            // leftBottomPanel
            // 
            this.leftBottomPanel.BackColor = System.Drawing.Color.Transparent;
            this.leftBottomPanel.Controls.Add(this.checkBoxShowStartup);
            this.leftBottomPanel.Controls.Add(this.openFilePanel);
            resources.ApplyResources(this.leftBottomPanel, "leftBottomPanel");
            this.leftBottomPanel.Name = "leftBottomPanel";
            // 
            // checkBoxShowStartup
            // 
            resources.ApplyResources(this.checkBoxShowStartup, "checkBoxShowStartup");
            this.checkBoxShowStartup.BackColor = System.Drawing.Color.Transparent;
            this.checkBoxShowStartup.Checked = true;
            this.checkBoxShowStartup.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxShowStartup.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.checkBoxShowStartup.Name = "checkBoxShowStartup";
            this.checkBoxShowStartup.UseVisualStyleBackColor = false;
            // 
            // openFilePanel
            // 
            this.openFilePanel.Controls.Add(this.openFileIcon);
            this.openFilePanel.Controls.Add(this.openFileLabel);
            resources.ApplyResources(this.openFilePanel, "openFilePanel");
            this.openFilePanel.Name = "openFilePanel";
            this.openFilePanel.Click += new System.EventHandler(this.openFile_Click);
            this.openFilePanel.MouseEnter += new System.EventHandler(this.openFile_MouseHover);
            this.openFilePanel.MouseLeave += new System.EventHandler(this.openFile_MouseLeave);
            // 
            // openFileIcon
            // 
            this.openFileIcon.Image = global::pwiz.Skyline.Properties.Resources.directoryicon;
            resources.ApplyResources(this.openFileIcon, "openFileIcon");
            this.openFileIcon.Name = "openFileIcon";
            this.openFileIcon.TabStop = false;
            this.openFileIcon.Click += new System.EventHandler(this.openFile_Click);
            this.openFileIcon.MouseEnter += new System.EventHandler(this.openFile_MouseHover);
            this.openFileIcon.MouseLeave += new System.EventHandler(this.openFile_MouseLeave);
            // 
            // openFileLabel
            // 
            resources.ApplyResources(this.openFileLabel, "openFileLabel");
            this.openFileLabel.ForeColor = System.Drawing.Color.White;
            this.openFileLabel.Name = "openFileLabel";
            this.openFileLabel.Click += new System.EventHandler(this.openFile_Click);
            this.openFileLabel.MouseEnter += new System.EventHandler(this.openFile_MouseHover);
            this.openFileLabel.MouseLeave += new System.EventHandler(this.openFile_MouseLeave);
            // 
            // StartPage
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.tabControlMain);
            this.Controls.Add(this.leftPanel);
            this.Icon = global::pwiz.Skyline.Properties.Resources.Skyline;
            this.Name = "StartPage";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.StartPage_FormClosing);
            this.Move += new System.EventHandler(this.StartPage_Move);
            this.Resize += new System.EventHandler(this.StartPage_Resize);
            this.tabControlMain.ResumeLayout(false);
            this.wizardTabPage.ResumeLayout(false);
            this.tutorialsTabPage.ResumeLayout(false);
            this.leftPanel.ResumeLayout(false);
            this.leftPanel.PerformLayout();
            this.leftBottomPanel.ResumeLayout(false);
            this.leftBottomPanel.PerformLayout();
            this.openFilePanel.ResumeLayout(false);
            this.openFilePanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.openFileIcon)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel recentFilesPanel;
        private System.Windows.Forms.Label lblRecent;
        private System.Windows.Forms.CheckBox checkBoxShowStartup;
        private System.Windows.Forms.Panel leftPanel;
        private System.Windows.Forms.Label skyline;
        private System.Windows.Forms.PictureBox openFileIcon;
        private System.Windows.Forms.Label openFileLabel;
        private System.Windows.Forms.Panel openFilePanel;
        private System.Windows.Forms.Panel leftBottomPanel;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelWizard;
        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage wizardTabPage;
        private System.Windows.Forms.TabPage tutorialsTabPage;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelTutorials;
        private System.Windows.Forms.ToolTip toolTip;
    }
}