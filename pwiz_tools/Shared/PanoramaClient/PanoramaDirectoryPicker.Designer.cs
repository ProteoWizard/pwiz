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
            this.label3 = new System.Windows.Forms.Label();
            this.toolStrip.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // folderPanel
            // 
            this.folderPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.folderPanel.Location = new System.Drawing.Point(12, 54);
            this.folderPanel.Name = "folderPanel";
            this.folderPanel.Size = new System.Drawing.Size(423, 241);
            this.folderPanel.TabIndex = 1;
            // 
            // cancel
            // 
            this.cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(360, 304);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(75, 23);
            this.cancel.TabIndex = 4;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // open
            // 
            this.open.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.open.Location = new System.Drawing.Point(279, 304);
            this.open.Name = "open";
            this.open.Size = new System.Drawing.Size(75, 23);
            this.open.TabIndex = 3;
            this.open.Text = "Open";
            this.open.UseVisualStyleBackColor = true;
            this.open.Click += new System.EventHandler(this.open_Click);
            // 
            // toolStrip
            // 
            this.toolStrip.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.back,
            this.forward,
            this.up});
            this.toolStrip.Location = new System.Drawing.Point(12, 7);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(127, 31);
            this.toolStrip.TabIndex = 5;
            this.toolStrip.Text = "toolStrip1";
            // 
            // back
            // 
            this.back.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.back.Image = ((System.Drawing.Image)(resources.GetObject("back.Image")));
            this.back.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.back.Name = "back";
            this.back.Size = new System.Drawing.Size(28, 28);
            this.back.Text = "Back";
            this.back.Click += new System.EventHandler(this.Back_Click);
            // 
            // forward
            // 
            this.forward.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.forward.Image = ((System.Drawing.Image)(resources.GetObject("forward.Image")));
            this.forward.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.forward.Name = "forward";
            this.forward.Size = new System.Drawing.Size(28, 28);
            this.forward.Text = "Forward";
            this.forward.Click += new System.EventHandler(this.Forward_Click);
            // 
            // up
            // 
            this.up.Image = global::pwiz.PanoramaClient.Properties.Resources.Icojam_Blueberry_Basic_Arrow_up;
            this.up.Name = "up";
            this.up.Size = new System.Drawing.Size(28, 28);
            this.up.Click += new System.EventHandler(this.Up_Click);
            // 
            // urlLink
            // 
            this.urlLink.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.urlLink.AutoEllipsis = true;
            this.urlLink.Location = new System.Drawing.Point(12, 309);
            this.urlLink.Name = "urlLink";
            this.urlLink.Size = new System.Drawing.Size(261, 13);
            this.urlLink.TabIndex = 2;
            this.urlLink.TabStop = true;
            this.urlLink.Text = "<placeholder>";
            this.urlLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.UrlLink_LinkClicked);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyLinkAddressToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(168, 26);
            // 
            // copyLinkAddressToolStripMenuItem
            // 
            this.copyLinkAddressToolStripMenuItem.Name = "copyLinkAddressToolStripMenuItem";
            this.copyLinkAddressToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
            this.copyLinkAddressToolStripMenuItem.Text = "Copy link address";
            this.copyLinkAddressToolStripMenuItem.Click += new System.EventHandler(this.CopyLinkAddressToolStripMenuItem_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label3.Location = new System.Drawing.Point(9, 38);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(81, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "&Remote folders:";
            // 
            // PanoramaDirectoryPicker
            // 
            this.AcceptButton = this.open;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(447, 336);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.urlLink);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.open);
            this.Controls.Add(this.folderPanel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PanoramaDirectoryPicker";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Panorama Folders";
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
        private System.Windows.Forms.Label label3;
    }
}