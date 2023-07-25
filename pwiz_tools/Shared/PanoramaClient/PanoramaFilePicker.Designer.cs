using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    partial class PanoramaFilePicker
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
       /*
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }*/

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PanoramaFilePicker));
            this.folderLabel = new System.Windows.Forms.Label();
            this.treeViewIcons = new System.Windows.Forms.ImageList(this.components);
            this.fileIcons = new System.Windows.Forms.ImageList(this.components);
            this.versionOptions = new System.Windows.Forms.ComboBox();
            this.open = new System.Windows.Forms.Button();
            this.cancel = new System.Windows.Forms.Button();
            this.versionLabel = new System.Windows.Forms.Label();
            this.browserSplitContainer = new System.Windows.Forms.SplitContainer();
            this.noFiles = new System.Windows.Forms.Label();
            this.listView = new System.Windows.Forms.ListView();
            this.colName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colVersions = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colReplacedBy = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colCreated = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.browserPanel = new System.Windows.Forms.Panel();
            this.navToolStrip = new System.Windows.Forms.ToolStrip();
            this.back = new System.Windows.Forms.ToolStripButton();
            this.forward = new System.Windows.Forms.ToolStripButton();
            this.up = new System.Windows.Forms.ToolStripButton();
            this.urlLink = new System.Windows.Forms.LinkLabel();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyLinkAddressToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.browserSplitContainer)).BeginInit();
            this.browserSplitContainer.Panel2.SuspendLayout();
            this.browserSplitContainer.SuspendLayout();
            this.browserPanel.SuspendLayout();
            this.navToolStrip.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // folderLabel
            // 
            resources.ApplyResources(this.folderLabel, "folderLabel");
            this.folderLabel.Name = "folderLabel";
            // 
            // treeViewIcons
            // 
            this.treeViewIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("treeViewIcons.ImageStream")));
            this.treeViewIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.treeViewIcons.Images.SetKeyName(0, "Panorama.bmp");
            this.treeViewIcons.Images.SetKeyName(1, "LabKey.bmp");
            this.treeViewIcons.Images.SetKeyName(2, "ChromLib.bmp");
            this.treeViewIcons.Images.SetKeyName(3, "Folder.png");
            // 
            // fileIcons
            // 
            this.fileIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("fileIcons.ImageStream")));
            this.fileIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.fileIcons.Images.SetKeyName(0, "File.png");
            this.fileIcons.Images.SetKeyName(1, "SkylineDoc.ico");
            // 
            // versionOptions
            // 
            resources.ApplyResources(this.versionOptions, "versionOptions");
            this.versionOptions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.versionOptions.FormattingEnabled = true;
            this.versionOptions.Items.AddRange(new object[] {
            resources.GetString("versionOptions.Items"),
            resources.GetString("versionOptions.Items1")});
            this.versionOptions.Name = "versionOptions";
            this.versionOptions.SelectedIndexChanged += new System.EventHandler(this.VersionOptions_SelectedIndexChanged);
            // 
            // open
            // 
            resources.ApplyResources(this.open, "open");
            this.open.Name = "open";
            this.open.UseVisualStyleBackColor = true;
            this.open.Click += new System.EventHandler(this.Open_Click);
            // 
            // cancel
            // 
            resources.ApplyResources(this.cancel, "cancel");
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Name = "cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // versionLabel
            // 
            resources.ApplyResources(this.versionLabel, "versionLabel");
            this.versionLabel.Name = "versionLabel";
            // 
            // browserSplitContainer
            // 
            resources.ApplyResources(this.browserSplitContainer, "browserSplitContainer");
            this.browserSplitContainer.Name = "browserSplitContainer";
            // 
            // browserSplitContainer.Panel2
            // 
            this.browserSplitContainer.Panel2.Controls.Add(this.noFiles);
            this.browserSplitContainer.Panel2.Controls.Add(this.listView);
            // 
            // noFiles
            // 
            resources.ApplyResources(this.noFiles, "noFiles");
            this.noFiles.Name = "noFiles";
            // 
            // listView
            // 
            this.listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colName,
            this.colSize,
            this.colVersions,
            this.colReplacedBy,
            this.colCreated});
            resources.ApplyResources(this.listView, "listView");
            this.listView.FullRowSelect = true;
            this.listView.HideSelection = false;
            this.listView.Name = "listView";
            this.listView.ShowItemToolTips = true;
            this.listView.SmallImageList = this.fileIcons;
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = System.Windows.Forms.View.Details;
            this.listView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.ListView_ColumnClick);
            this.listView.SizeChanged += new System.EventHandler(this.ListView_SizeChanged);
            this.listView.DoubleClick += new System.EventHandler(this.ListView_DoubleClick);
            // 
            // colName
            // 
            resources.ApplyResources(this.colName, "colName");
            // 
            // colSize
            // 
            resources.ApplyResources(this.colSize, "colSize");
            // 
            // colVersions
            // 
            resources.ApplyResources(this.colVersions, "colVersions");
            // 
            // colReplacedBy
            // 
            resources.ApplyResources(this.colReplacedBy, "colReplacedBy");
            // 
            // colCreated
            // 
            resources.ApplyResources(this.colCreated, "colCreated");
            // 
            // browserPanel
            // 
            resources.ApplyResources(this.browserPanel, "browserPanel");
            this.browserPanel.Controls.Add(this.browserSplitContainer);
            this.browserPanel.Name = "browserPanel";
            // 
            // navToolStrip
            // 
            this.navToolStrip.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.navToolStrip, "navToolStrip");
            this.navToolStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.navToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.back,
            this.forward,
            this.up});
            this.navToolStrip.Name = "navToolStrip";
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
            resources.ApplyResources(this.up, "up");
            this.up.Name = "up";
            this.up.Click += new System.EventHandler(this.UpButton_Click);
            // 
            // urlLink
            // 
            resources.ApplyResources(this.urlLink, "urlLink");
            this.urlLink.AutoEllipsis = true;
            this.urlLink.ContextMenuStrip = this.contextMenuStrip;
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
            // PanoramaFilePicker
            // 
            this.AcceptButton = this.open;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancel;
            this.Controls.Add(this.urlLink);
            this.Controls.Add(this.navToolStrip);
            this.Controls.Add(this.browserPanel);
            this.Controls.Add(this.versionLabel);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.open);
            this.Controls.Add(this.versionOptions);
            this.Controls.Add(this.folderLabel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PanoramaFilePicker";
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PanoramaFilePicker_FormClosing);
            this.Load += new System.EventHandler(this.FilePicker_Load);
            this.SizeChanged += new System.EventHandler(this.PanoramaFilePicker_SizeChanged);
            this.browserSplitContainer.Panel2.ResumeLayout(false);
            this.browserSplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.browserSplitContainer)).EndInit();
            this.browserSplitContainer.ResumeLayout(false);
            this.browserPanel.ResumeLayout(false);
            this.navToolStrip.ResumeLayout(false);
            this.navToolStrip.PerformLayout();
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private Label folderLabel;
        private ImageList treeViewIcons;
        private ImageList fileIcons;
        private ComboBox versionOptions;
        private Button open;
        private Button cancel;
        private Label versionLabel;
        private SplitContainer browserSplitContainer;
        private ListView listView;
        private Panel browserPanel;
        private ToolStrip navToolStrip;
        private ToolStripButton back;
        private ToolStripButton forward;
        private ToolStripButton up;
        private ColumnHeader colName;
        private ColumnHeader colSize;
        private ColumnHeader colVersions;
        private ColumnHeader colReplacedBy;
        private ColumnHeader colCreated;
        private Label noFiles;
        private LinkLabel urlLink;
        private ContextMenuStrip contextMenuStrip;
        private ToolStripMenuItem copyLinkAddressToolStripMenuItem;
    }
}