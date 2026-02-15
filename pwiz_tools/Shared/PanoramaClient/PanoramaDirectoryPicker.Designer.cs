namespace pwiz.PanoramaClient
{
    partial class PanoramaDirectoryPicker
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PanoramaDirectoryPicker));
            this.folderPanel = new System.Windows.Forms.Panel();
            this.cancel = new System.Windows.Forms.Button();
            this.open = new System.Windows.Forms.Button();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.back = new System.Windows.Forms.ToolStripButton();
            this.forward = new System.Windows.Forms.ToolStripButton();
            this.up = new System.Windows.Forms.ToolStripButton();
            this.urlLink = new System.Windows.Forms.LinkLabel();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyLinkAddressToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.folderLabel = new System.Windows.Forms.Label();
            this.toolStrip.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // folderPanel
            // 
            resources.ApplyResources(this.folderPanel, "folderPanel");
            this.folderPanel.Name = "folderPanel";
            // 
            // cancel
            // 
            resources.ApplyResources(this.cancel, "cancel");
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Name = "cancel";
            this.cancel.UseVisualStyleBackColor = true;
            // 
            // open
            // 
            resources.ApplyResources(this.open, "open");
            this.open.Name = "open";
            this.open.UseVisualStyleBackColor = true;
            this.open.Click += new System.EventHandler(this.Open_Click);
            // 
            // toolStrip
            // 
            resources.ApplyResources(this.toolStrip, "toolStrip");
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.back,
            this.forward,
            this.up});
            this.toolStrip.Name = "toolStrip";
            // 
            // back
            // 
            this.back.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.back, "back");
            this.back.Name = "back";
            this.back.Click += new System.EventHandler(this.Back_Click);
            // 
            // forward
            // 
            this.forward.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.forward, "forward");
            this.forward.Name = "forward";
            this.forward.Click += new System.EventHandler(this.Forward_Click);
            // 
            // up
            // 
            this.up.Image = global::pwiz.PanoramaClient.Properties.Resources.Icojam_Blueberry_Basic_Arrow_up;
            this.up.Name = "up";
            resources.ApplyResources(this.up, "up");
            this.up.Click += new System.EventHandler(this.Up_Click);
            // 
            // urlLink
            // 
            resources.ApplyResources(this.urlLink, "urlLink");
            this.urlLink.AutoEllipsis = true;
            this.urlLink.Name = "urlLink";
            this.urlLink.TabStop = true;
            this.urlLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.UrlLink_LinkClicked);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyLinkAddressToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            resources.ApplyResources(this.contextMenuStrip, "contextMenuStrip");
            // 
            // copyLinkAddressToolStripMenuItem
            // 
            this.copyLinkAddressToolStripMenuItem.Name = "copyLinkAddressToolStripMenuItem";
            resources.ApplyResources(this.copyLinkAddressToolStripMenuItem, "copyLinkAddressToolStripMenuItem");
            this.copyLinkAddressToolStripMenuItem.Click += new System.EventHandler(this.CopyLinkAddressToolStripMenuItem_Click);
            // 
            // folderLabel
            // 
            resources.ApplyResources(this.folderLabel, "folderLabel");
            this.folderLabel.Name = "folderLabel";
            // 
            // PanoramaDirectoryPicker
            // 
            this.AcceptButton = this.open;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancel;
            this.Controls.Add(this.folderLabel);
            this.Controls.Add(this.urlLink);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.open);
            this.Controls.Add(this.folderPanel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PanoramaDirectoryPicker";
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DirectoryPicker_FormClosing);
            this.Load += new System.EventHandler(this.DirectoryPicker_Load);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel folderPanel;
        private System.Windows.Forms.Button cancel;
        private System.Windows.Forms.Button open;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton back;
        private System.Windows.Forms.ToolStripButton forward;
        private System.Windows.Forms.ToolStripButton up;
        private System.Windows.Forms.LinkLabel urlLink;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem copyLinkAddressToolStripMenuItem;
        private System.Windows.Forms.Label folderLabel;
    }
}