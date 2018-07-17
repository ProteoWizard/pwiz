namespace pwiz.Skyline.FileUI
{
    partial class OpenDataSourceDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing )
        {
            if( disposing && ( components != null ) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OpenDataSourceDialog));
            this.listView = new System.Windows.Forms.ListView();
            this.SourceName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.FileType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SourceSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.DateModified = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lookInComboBox = new System.Windows.Forms.ComboBox();
            this.labelLookIn = new System.Windows.Forms.Label();
            this.sourcePathTextBox = new System.Windows.Forms.TextBox();
            this.labelSourcePath = new System.Windows.Forms.Label();
            this.sourceTypeComboBox = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.openButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.navToolStrip = new System.Windows.Forms.ToolStrip();
            this.backButton = new System.Windows.Forms.ToolStripButton();
            this.upOneLevelButton = new System.Windows.Forms.ToolStripButton();
            this.viewsDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.tilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detailsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.chorusButton = new System.Windows.Forms.Button();
            this.lookInImageList = new System.Windows.Forms.ImageList(this.components);
            this.recentDocumentsButton = new System.Windows.Forms.Button();
            this.desktopButton = new System.Windows.Forms.Button();
            this.myDocumentsButton = new System.Windows.Forms.Button();
            this.myComputerButton = new System.Windows.Forms.Button();
            this.navToolStrip.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listView
            // 
            this.listView.AllowColumnReorder = true;
            resources.ApplyResources(this.listView, "listView");
            this.listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.SourceName,
            this.FileType,
            this.SourceSize,
            this.DateModified});
            this.listView.Name = "listView";
            this.listView.TileSize = new System.Drawing.Size(150, 35);
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = System.Windows.Forms.View.List;
            this.listView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listView_ColumnClick);
            this.listView.ItemActivate += new System.EventHandler(this.listView_ItemActivate);
            this.listView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listView_ItemSelectionChanged);
            this.listView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.listView_KeyDown);
            // 
            // SourceName
            // 
            resources.ApplyResources(this.SourceName, "SourceName");
            // 
            // FileType
            // 
            resources.ApplyResources(this.FileType, "FileType");
            // 
            // SourceSize
            // 
            resources.ApplyResources(this.SourceSize, "SourceSize");
            // 
            // DateModified
            // 
            resources.ApplyResources(this.DateModified, "DateModified");
            // 
            // lookInComboBox
            // 
            resources.ApplyResources(this.lookInComboBox, "lookInComboBox");
            this.lookInComboBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.lookInComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.lookInComboBox.FormattingEnabled = true;
            this.lookInComboBox.Name = "lookInComboBox";
            this.lookInComboBox.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.lookInComboBox_DrawItem);
            this.lookInComboBox.DropDown += new System.EventHandler(this.lookInComboBox_DropDown);
            this.lookInComboBox.MeasureItem += new System.Windows.Forms.MeasureItemEventHandler(this.lookInComboBox_MeasureItem);
            this.lookInComboBox.SelectionChangeCommitted += new System.EventHandler(this.lookInComboBox_SelectionChangeCommitted);
            // 
            // labelLookIn
            // 
            resources.ApplyResources(this.labelLookIn, "labelLookIn");
            this.labelLookIn.Name = "labelLookIn";
            // 
            // sourcePathTextBox
            // 
            resources.ApplyResources(this.sourcePathTextBox, "sourcePathTextBox");
            this.sourcePathTextBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.sourcePathTextBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.sourcePathTextBox.Name = "sourcePathTextBox";
            this.sourcePathTextBox.KeyUp += new System.Windows.Forms.KeyEventHandler(this.sourcePathTextBox_KeyUp);
            // 
            // labelSourcePath
            // 
            resources.ApplyResources(this.labelSourcePath, "labelSourcePath");
            this.labelSourcePath.Name = "labelSourcePath";
            // 
            // sourceTypeComboBox
            // 
            resources.ApplyResources(this.sourceTypeComboBox, "sourceTypeComboBox");
            this.sourceTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.sourceTypeComboBox.FormattingEnabled = true;
            this.sourceTypeComboBox.Name = "sourceTypeComboBox";
            this.sourceTypeComboBox.SelectionChangeCommitted += new System.EventHandler(this.sourceTypeComboBox_SelectionChangeCommitted);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // openButton
            // 
            resources.ApplyResources(this.openButton, "openButton");
            this.openButton.Name = "openButton";
            this.openButton.UseVisualStyleBackColor = true;
            this.openButton.Click += new System.EventHandler(this.openButton_Click);
            // 
            // cancelButton
            // 
            resources.ApplyResources(this.cancelButton, "cancelButton");
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // navToolStrip
            // 
            resources.ApplyResources(this.navToolStrip, "navToolStrip");
            this.navToolStrip.BackColor = System.Drawing.SystemColors.Control;
            this.navToolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.navToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.backButton,
            this.upOneLevelButton,
            this.viewsDropDownButton});
            this.navToolStrip.Name = "navToolStrip";
            this.navToolStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            // 
            // backButton
            // 
            this.backButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.backButton, "backButton");
            this.backButton.Name = "backButton";
            this.backButton.Click += new System.EventHandler(this.backButton_Click);
            // 
            // upOneLevelButton
            // 
            this.upOneLevelButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.upOneLevelButton, "upOneLevelButton");
            this.upOneLevelButton.Name = "upOneLevelButton";
            this.upOneLevelButton.Click += new System.EventHandler(this.upOneLevelButton_Click);
            // 
            // viewsDropDownButton
            // 
            this.viewsDropDownButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.viewsDropDownButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tilesToolStripMenuItem,
            this.listToolStripMenuItem,
            this.detailsToolStripMenuItem});
            resources.ApplyResources(this.viewsDropDownButton, "viewsDropDownButton");
            this.viewsDropDownButton.Name = "viewsDropDownButton";
            // 
            // tilesToolStripMenuItem
            // 
            this.tilesToolStripMenuItem.Name = "tilesToolStripMenuItem";
            resources.ApplyResources(this.tilesToolStripMenuItem, "tilesToolStripMenuItem");
            this.tilesToolStripMenuItem.Click += new System.EventHandler(this.tilesToolStripMenuItem_Click);
            // 
            // listToolStripMenuItem
            // 
            this.listToolStripMenuItem.Checked = true;
            this.listToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.listToolStripMenuItem.Name = "listToolStripMenuItem";
            resources.ApplyResources(this.listToolStripMenuItem, "listToolStripMenuItem");
            this.listToolStripMenuItem.Click += new System.EventHandler(this.listToolStripMenuItem_Click);
            // 
            // detailsToolStripMenuItem
            // 
            this.detailsToolStripMenuItem.Name = "detailsToolStripMenuItem";
            resources.ApplyResources(this.detailsToolStripMenuItem, "detailsToolStripMenuItem");
            this.detailsToolStripMenuItem.Click += new System.EventHandler(this.detailsToolStripMenuItem_Click);
            // 
            // flowLayoutPanel1
            // 
            resources.ApplyResources(this.flowLayoutPanel1, "flowLayoutPanel1");
            this.flowLayoutPanel1.BackColor = System.Drawing.SystemColors.Window;
            this.flowLayoutPanel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.flowLayoutPanel1.Controls.Add(this.chorusButton);
            this.flowLayoutPanel1.Controls.Add(this.recentDocumentsButton);
            this.flowLayoutPanel1.Controls.Add(this.desktopButton);
            this.flowLayoutPanel1.Controls.Add(this.myDocumentsButton);
            this.flowLayoutPanel1.Controls.Add(this.myComputerButton);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            // 
            // chorusButton
            // 
            this.chorusButton.FlatAppearance.BorderSize = 0;
            resources.ApplyResources(this.chorusButton, "chorusButton");
            this.chorusButton.ImageList = this.lookInImageList;
            this.chorusButton.Name = "chorusButton";
            this.chorusButton.UseVisualStyleBackColor = false;
            this.chorusButton.Click += new System.EventHandler(this.chorusButton_Click);
            // 
            // lookInImageList
            // 
            this.lookInImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("lookInImageList.ImageStream")));
            this.lookInImageList.TransparentColor = System.Drawing.Color.Transparent;
            this.lookInImageList.Images.SetKeyName(0, "RecentDocuments.png");
            this.lookInImageList.Images.SetKeyName(1, "Desktop.png");
            this.lookInImageList.Images.SetKeyName(2, "MyDocuments.png");
            this.lookInImageList.Images.SetKeyName(3, "MyComputer.png");
            this.lookInImageList.Images.SetKeyName(4, "MyNetworkPlaces.png");
            this.lookInImageList.Images.SetKeyName(5, "LocalDrive.png");
            this.lookInImageList.Images.SetKeyName(6, "OpticalDrive.png");
            this.lookInImageList.Images.SetKeyName(7, "NetworkDrive.png");
            this.lookInImageList.Images.SetKeyName(8, "folder.png");
            this.lookInImageList.Images.SetKeyName(9, "DataProcessing.png");
            this.lookInImageList.Images.SetKeyName(10, "File.png");
            this.lookInImageList.Images.SetKeyName(11, "Chorus.png");
            // 
            // recentDocumentsButton
            // 
            this.recentDocumentsButton.FlatAppearance.BorderSize = 0;
            resources.ApplyResources(this.recentDocumentsButton, "recentDocumentsButton");
            this.recentDocumentsButton.ImageList = this.lookInImageList;
            this.recentDocumentsButton.Name = "recentDocumentsButton";
            this.recentDocumentsButton.UseVisualStyleBackColor = false;
            this.recentDocumentsButton.Click += new System.EventHandler(this.recentDocumentsButton_Click);
            // 
            // desktopButton
            // 
            this.desktopButton.FlatAppearance.BorderSize = 0;
            resources.ApplyResources(this.desktopButton, "desktopButton");
            this.desktopButton.ImageList = this.lookInImageList;
            this.desktopButton.Name = "desktopButton";
            this.desktopButton.UseVisualStyleBackColor = false;
            this.desktopButton.Click += new System.EventHandler(this.desktopButton_Click);
            // 
            // myDocumentsButton
            // 
            this.myDocumentsButton.FlatAppearance.BorderSize = 0;
            resources.ApplyResources(this.myDocumentsButton, "myDocumentsButton");
            this.myDocumentsButton.ImageList = this.lookInImageList;
            this.myDocumentsButton.Name = "myDocumentsButton";
            this.myDocumentsButton.UseVisualStyleBackColor = false;
            this.myDocumentsButton.Click += new System.EventHandler(this.myDocumentsButton_Click);
            // 
            // myComputerButton
            // 
            this.myComputerButton.FlatAppearance.BorderSize = 0;
            resources.ApplyResources(this.myComputerButton, "myComputerButton");
            this.myComputerButton.ImageList = this.lookInImageList;
            this.myComputerButton.Name = "myComputerButton";
            this.myComputerButton.UseVisualStyleBackColor = false;
            this.myComputerButton.Click += new System.EventHandler(this.myComputerButton_Click);
            // 
            // OpenDataSourceDialog
            // 
            this.AcceptButton = this.openButton;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.navToolStrip);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.openButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.sourceTypeComboBox);
            this.Controls.Add(this.labelSourcePath);
            this.Controls.Add(this.sourcePathTextBox);
            this.Controls.Add(this.labelLookIn);
            this.Controls.Add(this.lookInComboBox);
            this.Controls.Add(this.listView);
            this.DoubleBuffered = true;
            this.HelpButton = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OpenDataSourceDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.navToolStrip.ResumeLayout(false);
            this.navToolStrip.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView listView;
        private System.Windows.Forms.ComboBox lookInComboBox;
        private System.Windows.Forms.Label labelLookIn;
        private System.Windows.Forms.TextBox sourcePathTextBox;
        private System.Windows.Forms.Label labelSourcePath;
        private System.Windows.Forms.ComboBox sourceTypeComboBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button openButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.ColumnHeader SourceName;
        private System.Windows.Forms.ColumnHeader SourceSize;
        private System.Windows.Forms.ColumnHeader DateModified;
        private System.Windows.Forms.ToolStrip navToolStrip;
        private System.Windows.Forms.ToolStripButton backButton;
        private System.Windows.Forms.ToolStripButton upOneLevelButton;
        private System.Windows.Forms.ToolStripDropDownButton viewsDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem tilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem listToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detailsToolStripMenuItem;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button chorusButton;
        private System.Windows.Forms.Button desktopButton;
        private System.Windows.Forms.Button myDocumentsButton;
        private System.Windows.Forms.Button myComputerButton;
        private System.Windows.Forms.ImageList lookInImageList;
        private System.Windows.Forms.ColumnHeader FileType;
        private System.Windows.Forms.Button recentDocumentsButton;
    }
}