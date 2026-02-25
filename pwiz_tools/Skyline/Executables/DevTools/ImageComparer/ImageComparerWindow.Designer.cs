namespace ImageComparer
{
    partial class ImageComparerWindow
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
            this.oldScreenshotPictureBox = new System.Windows.Forms.PictureBox();
            this.newScreenshotPictureBox = new System.Windows.Forms.PictureBox();
            this.previewSplitContainer = new System.Windows.Forms.SplitContainer();
            this.oldScreenshotLabelPanel = new System.Windows.Forms.Panel();
            this.pictureMatching = new System.Windows.Forms.PictureBox();
            this.labelOldSize = new System.Windows.Forms.Label();
            this.buttonImageSource = new System.Windows.Forms.Button();
            this.oldScreenshotLabel = new System.Windows.Forms.Label();
            this.newScreenshotLabelPanel = new System.Windows.Forms.Panel();
            this.labelNewSize = new System.Windows.Forms.Label();
            this.labelChangeCount = new System.Windows.Forms.Label();
            this.newScreenshotLabel = new System.Windows.Forms.Label();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolStripOpenFolder = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripPrevious = new System.Windows.Forms.ToolStripButton();
            this.toolStripNext = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripRevert = new System.Windows.Forms.ToolStripButton();
            this.toolStripFileList = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripAutoSize = new System.Windows.Forms.ToolStripButton();
            this.toolStripDiffOnly = new System.Windows.Forms.ToolStripButton();
            this.toolStripAmplify = new System.Windows.Forms.ToolStripButton();
            this.contextMenuImageSource = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuItemGit = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemWeb = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripGotoWeb = new System.Windows.Forms.ToolStripButton();
            this.toolStripPickColorButton = new ImageComparer.AlphaColorPickerButton();
            ((System.ComponentModel.ISupportInitialize)(this.oldScreenshotPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.newScreenshotPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.previewSplitContainer)).BeginInit();
            this.previewSplitContainer.Panel1.SuspendLayout();
            this.previewSplitContainer.Panel2.SuspendLayout();
            this.previewSplitContainer.SuspendLayout();
            this.oldScreenshotLabelPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureMatching)).BeginInit();
            this.newScreenshotLabelPanel.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // oldScreenshotPictureBox
            // 
            this.oldScreenshotPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.oldScreenshotPictureBox.Location = new System.Drawing.Point(0, 30);
            this.oldScreenshotPictureBox.Margin = new System.Windows.Forms.Padding(2);
            this.oldScreenshotPictureBox.Name = "oldScreenshotPictureBox";
            this.oldScreenshotPictureBox.Size = new System.Drawing.Size(488, 406);
            this.oldScreenshotPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.oldScreenshotPictureBox.TabIndex = 0;
            this.oldScreenshotPictureBox.TabStop = false;
            // 
            // newScreenshotPictureBox
            // 
            this.newScreenshotPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.newScreenshotPictureBox.Location = new System.Drawing.Point(0, 30);
            this.newScreenshotPictureBox.Margin = new System.Windows.Forms.Padding(2);
            this.newScreenshotPictureBox.Name = "newScreenshotPictureBox";
            this.newScreenshotPictureBox.Size = new System.Drawing.Size(487, 406);
            this.newScreenshotPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.newScreenshotPictureBox.TabIndex = 1;
            this.newScreenshotPictureBox.TabStop = false;
            // 
            // previewSplitContainer
            // 
            this.previewSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewSplitContainer.IsSplitterFixed = true;
            this.previewSplitContainer.Location = new System.Drawing.Point(0, 25);
            this.previewSplitContainer.Margin = new System.Windows.Forms.Padding(2);
            this.previewSplitContainer.Name = "previewSplitContainer";
            // 
            // previewSplitContainer.Panel1
            // 
            this.previewSplitContainer.Panel1.Controls.Add(this.oldScreenshotPictureBox);
            this.previewSplitContainer.Panel1.Controls.Add(this.oldScreenshotLabelPanel);
            // 
            // previewSplitContainer.Panel2
            // 
            this.previewSplitContainer.Panel2.Controls.Add(this.newScreenshotPictureBox);
            this.previewSplitContainer.Panel2.Controls.Add(this.newScreenshotLabelPanel);
            this.previewSplitContainer.Size = new System.Drawing.Size(976, 436);
            this.previewSplitContainer.SplitterDistance = 488;
            this.previewSplitContainer.SplitterWidth = 1;
            this.previewSplitContainer.TabIndex = 1;
            // 
            // oldScreenshotLabelPanel
            // 
            this.oldScreenshotLabelPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.oldScreenshotLabelPanel.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.oldScreenshotLabelPanel.Controls.Add(this.pictureMatching);
            this.oldScreenshotLabelPanel.Controls.Add(this.labelOldSize);
            this.oldScreenshotLabelPanel.Controls.Add(this.buttonImageSource);
            this.oldScreenshotLabelPanel.Controls.Add(this.oldScreenshotLabel);
            this.oldScreenshotLabelPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.oldScreenshotLabelPanel.Location = new System.Drawing.Point(0, 0);
            this.oldScreenshotLabelPanel.Margin = new System.Windows.Forms.Padding(2);
            this.oldScreenshotLabelPanel.Name = "oldScreenshotLabelPanel";
            this.oldScreenshotLabelPanel.Size = new System.Drawing.Size(488, 30);
            this.oldScreenshotLabelPanel.TabIndex = 1;
            // 
            // pictureMatching
            // 
            this.pictureMatching.Location = new System.Drawing.Point(5, 7);
            this.pictureMatching.Name = "pictureMatching";
            this.pictureMatching.Size = new System.Drawing.Size(16, 16);
            this.pictureMatching.TabIndex = 3;
            this.pictureMatching.TabStop = false;
            // 
            // labelOldSize
            // 
            this.labelOldSize.AutoSize = true;
            this.labelOldSize.Location = new System.Drawing.Point(26, 9);
            this.labelOldSize.Name = "labelOldSize";
            this.labelOldSize.Size = new System.Drawing.Size(42, 13);
            this.labelOldSize.TabIndex = 2;
            this.labelOldSize.Text = "old size";
            // 
            // buttonImageSource
            // 
            this.buttonImageSource.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonImageSource.Location = new System.Drawing.Point(455, 4);
            this.buttonImageSource.Margin = new System.Windows.Forms.Padding(1);
            this.buttonImageSource.Name = "buttonImageSource";
            this.buttonImageSource.Size = new System.Drawing.Size(22, 22);
            this.buttonImageSource.TabIndex = 1;
            this.buttonImageSource.TabStop = false;
            this.helpTip.SetToolTip(this.buttonImageSource, "Switch Source (Ctrl-Tab)\r\nCurrent: Git");
            this.buttonImageSource.UseVisualStyleBackColor = true;
            this.buttonImageSource.Click += new System.EventHandler(this.buttonImageSource_Click);
            // 
            // oldScreenshotLabel
            // 
            this.oldScreenshotLabel.BackColor = System.Drawing.Color.Transparent;
            this.oldScreenshotLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.oldScreenshotLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F);
            this.oldScreenshotLabel.ForeColor = System.Drawing.Color.Black;
            this.oldScreenshotLabel.Location = new System.Drawing.Point(0, 0);
            this.oldScreenshotLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.oldScreenshotLabel.Name = "oldScreenshotLabel";
            this.oldScreenshotLabel.Size = new System.Drawing.Size(488, 30);
            this.oldScreenshotLabel.TabIndex = 0;
            this.oldScreenshotLabel.Text = "Old Screenshot";
            this.oldScreenshotLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // newScreenshotLabelPanel
            // 
            this.newScreenshotLabelPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.newScreenshotLabelPanel.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.newScreenshotLabelPanel.Controls.Add(this.labelChangeCount);
            this.newScreenshotLabelPanel.Controls.Add(this.labelNewSize);
            this.newScreenshotLabelPanel.Controls.Add(this.newScreenshotLabel);
            this.newScreenshotLabelPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.newScreenshotLabelPanel.Location = new System.Drawing.Point(0, 0);
            this.newScreenshotLabelPanel.Margin = new System.Windows.Forms.Padding(2);
            this.newScreenshotLabelPanel.Name = "newScreenshotLabelPanel";
            this.newScreenshotLabelPanel.Size = new System.Drawing.Size(487, 30);
            this.newScreenshotLabelPanel.TabIndex = 3;
            //
            // labelNewSize
            //
            this.labelNewSize.AutoSize = true;
            this.labelNewSize.Location = new System.Drawing.Point(5, 9);
            this.labelNewSize.Name = "labelNewSize";
            this.labelNewSize.Size = new System.Drawing.Size(48, 13);
            this.labelNewSize.TabIndex = 2;
            this.labelNewSize.Text = "new size";
            //
            // labelChangeCount
            //
            this.labelChangeCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelChangeCount.Location = new System.Drawing.Point(387, 9);
            this.labelChangeCount.Name = "labelChangeCount";
            this.labelChangeCount.Size = new System.Drawing.Size(95, 13);
            this.labelChangeCount.TabIndex = 3;
            this.labelChangeCount.Text = "0/0 changes";
            this.labelChangeCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // newScreenshotLabel
            // 
            this.newScreenshotLabel.BackColor = System.Drawing.Color.Transparent;
            this.newScreenshotLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.newScreenshotLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F);
            this.newScreenshotLabel.ForeColor = System.Drawing.Color.Black;
            this.newScreenshotLabel.Location = new System.Drawing.Point(0, 0);
            this.newScreenshotLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.newScreenshotLabel.Name = "newScreenshotLabel";
            this.newScreenshotLabel.Size = new System.Drawing.Size(487, 30);
            this.newScreenshotLabel.TabIndex = 0;
            this.newScreenshotLabel.Text = "New Screenshot";
            this.newScreenshotLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // toolStrip
            // 
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripOpenFolder,
            this.toolStripSeparator1,
            this.toolStripPrevious,
            this.toolStripNext,
            this.toolStripSeparator2,
            this.toolStripRevert,
            this.toolStripFileList,
            this.toolStripAutoSize,
            this.toolStripPickColorButton,
            this.toolStripDiffOnly,
            this.toolStripAmplify,
            this.toolStripSeparator3,
            this.toolStripGotoWeb});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(976, 25);
            this.toolStrip.TabIndex = 2;
            // 
            // toolStripOpenFolder
            // 
            this.toolStripOpenFolder.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripOpenFolder.Image = global::ImageComparer.Properties.Resources.openfolder;
            this.toolStripOpenFolder.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripOpenFolder.Name = "toolStripOpenFolder";
            this.toolStripOpenFolder.Size = new System.Drawing.Size(23, 22);
            this.toolStripOpenFolder.ToolTipText = "Open Folder (Ctrl-O)";
            this.toolStripOpenFolder.Click += new System.EventHandler(this.toolStripOpenFolder_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // toolStripPrevious
            // 
            this.toolStripPrevious.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripPrevious.Image = global::ImageComparer.Properties.Resources.backwards;
            this.toolStripPrevious.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripPrevious.Name = "toolStripPrevious";
            this.toolStripPrevious.Size = new System.Drawing.Size(23, 22);
            this.toolStripPrevious.ToolTipText = "Previous (Shift-F11)";
            this.toolStripPrevious.Click += new System.EventHandler(this.toolStripPrevious_Click);
            // 
            // toolStripNext
            // 
            this.toolStripNext.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripNext.Image = global::ImageComparer.Properties.Resources.forwards;
            this.toolStripNext.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripNext.Name = "toolStripNext";
            this.toolStripNext.Size = new System.Drawing.Size(23, 22);
            this.toolStripNext.ToolTipText = "Next (F11)";
            this.toolStripNext.Click += new System.EventHandler(this.toolStripNext_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // toolStripRevert
            // 
            this.toolStripRevert.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripRevert.Image = global::ImageComparer.Properties.Resources.undo;
            this.toolStripRevert.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripRevert.Name = "toolStripRevert";
            this.toolStripRevert.Size = new System.Drawing.Size(23, 22);
            this.toolStripRevert.ToolTipText = "Revert (F12)";
            this.toolStripRevert.Click += new System.EventHandler(this.toolStripRevert_Click);
            // 
            // toolStripFileList
            // 
            this.toolStripFileList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.toolStripFileList.Name = "toolStripFileList";
            this.toolStripFileList.Size = new System.Drawing.Size(250, 25);
            this.toolStripFileList.SelectedIndexChanged += new System.EventHandler(this.toolStripFileList_SelectedIndexChanged);
            // 
            // toolStripAutoSize
            // 
            this.toolStripAutoSize.Checked = true;
            this.toolStripAutoSize.CheckOnClick = true;
            this.toolStripAutoSize.CheckState = System.Windows.Forms.CheckState.Checked;
            this.toolStripAutoSize.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripAutoSize.Image = global::ImageComparer.Properties.Resources.autosizeoptimize;
            this.toolStripAutoSize.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripAutoSize.Name = "toolStripAutoSize";
            this.toolStripAutoSize.Size = new System.Drawing.Size(23, 22);
            this.toolStripAutoSize.ToolTipText = "Auto-Size";
            this.toolStripAutoSize.CheckedChanged += new System.EventHandler(this.toolStripAutoSize_CheckedChanged);
            //
            // toolStripDiffOnly
            //
            this.toolStripDiffOnly.CheckOnClick = true;
            this.toolStripDiffOnly.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripDiffOnly.Image = global::ImageComparer.Properties.Resources.blank;
            this.toolStripDiffOnly.Name = "toolStripDiffOnly";
            this.toolStripDiffOnly.Size = new System.Drawing.Size(23, 22);
            this.toolStripDiffOnly.ToolTipText = "Show diff pixels only on white background (D)";
            this.toolStripDiffOnly.CheckedChanged += new System.EventHandler(this.toolStripDiffOnly_CheckedChanged);
            //
            // toolStripAmplify
            //
            this.toolStripAmplify.CheckOnClick = true;
            this.toolStripAmplify.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripAmplify.Name = "toolStripAmplify";
            this.toolStripAmplify.Size = new System.Drawing.Size(23, 22);
            this.toolStripAmplify.Text = "Amp";
            this.toolStripAmplify.ToolTipText = "Amplify diff pixels to 5x larger squares (A)";
            this.toolStripAmplify.CheckedChanged += new System.EventHandler(this.toolStripAmplify_CheckedChanged);
            //
            // contextMenuImageSource
            //
            this.contextMenuImageSource.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemGit,
            this.menuItemWeb});
            this.contextMenuImageSource.Name = "contextMenuImageSource";
            this.contextMenuImageSource.Size = new System.Drawing.Size(120, 70);
            //
            // menuItemGit
            //
            this.menuItemGit.Image = global::ImageComparer.Properties.Resources.gitsource;
            this.menuItemGit.Name = "menuItemGit";
            this.menuItemGit.Size = new System.Drawing.Size(119, 22);
            this.menuItemGit.Text = "Git HEAD";
            this.menuItemGit.Click += new System.EventHandler(this.menuItemGit_Click);
            //
            // menuItemWeb
            //
            this.menuItemWeb.Image = global::ImageComparer.Properties.Resources.websource;
            this.menuItemWeb.Name = "menuItemWeb";
            this.menuItemWeb.Size = new System.Drawing.Size(119, 22);
            this.menuItemWeb.Text = "Web";
            this.menuItemWeb.Click += new System.EventHandler(this.menuItemWeb_Click);
            //
            // toolStripSeparator3
            //
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 25);
            //
            // toolStripGotoWeb
            // 
            this.toolStripGotoWeb.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripGotoWeb.Image = global::ImageComparer.Properties.Resources.webdestination;
            this.toolStripGotoWeb.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripGotoWeb.Name = "toolStripGotoWeb";
            this.toolStripGotoWeb.Size = new System.Drawing.Size(23, 22);
            this.toolStripGotoWeb.ToolTipText = "Goto Web (Ctrl-G)";
            this.toolStripGotoWeb.Click += new System.EventHandler(this.toolStripGotoWeb_Click);
            // 
            // toolStripPickColorButton
            // 
            this.toolStripPickColorButton.BackColor = System.Drawing.SystemColors.Control;
            this.toolStripPickColorButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripPickColorButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripPickColorButton.Name = "toolStripPickColorButton";
            this.toolStripPickColorButton.Size = new System.Drawing.Size(29, 22);
            this.toolStripPickColorButton.ColorChanged += new System.EventHandler(this.toolStripPickColorButton_ColorChanged);
            // 
            // ImageComparerWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(976, 461);
            this.Controls.Add(this.previewSplitContainer);
            this.Controls.Add(this.toolStrip);
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MinimumSize = new System.Drawing.Size(400, 200);
            this.Name = "ImageComparerWindow";
            this.Text = "Compare Images";
            ((System.ComponentModel.ISupportInitialize)(this.oldScreenshotPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.newScreenshotPictureBox)).EndInit();
            this.previewSplitContainer.Panel1.ResumeLayout(false);
            this.previewSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.previewSplitContainer)).EndInit();
            this.previewSplitContainer.ResumeLayout(false);
            this.oldScreenshotLabelPanel.ResumeLayout(false);
            this.oldScreenshotLabelPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureMatching)).EndInit();
            this.newScreenshotLabelPanel.ResumeLayout(false);
            this.newScreenshotLabelPanel.PerformLayout();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox oldScreenshotPictureBox;
        private System.Windows.Forms.PictureBox newScreenshotPictureBox;
        private System.Windows.Forms.SplitContainer previewSplitContainer;
        private System.Windows.Forms.Label newScreenshotLabel;
        private System.Windows.Forms.Panel newScreenshotLabelPanel;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.Panel oldScreenshotLabelPanel;
        private System.Windows.Forms.Label oldScreenshotLabel;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton toolStripOpenFolder;
        private System.Windows.Forms.ToolStripButton toolStripPrevious;
        private System.Windows.Forms.ToolStripButton toolStripNext;
        private System.Windows.Forms.ToolStripButton toolStripRevert;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton toolStripGotoWeb;
        private System.Windows.Forms.ToolStripButton toolStripAutoSize;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.Button buttonImageSource;
        private System.Windows.Forms.Label labelOldSize;
        private System.Windows.Forms.Label labelNewSize;
        private System.Windows.Forms.Label labelChangeCount;
        private System.Windows.Forms.PictureBox pictureMatching;
        private System.Windows.Forms.ToolStripComboBox toolStripFileList;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private AlphaColorPickerButton toolStripPickColorButton;
        private System.Windows.Forms.ToolStripButton toolStripDiffOnly;
        private System.Windows.Forms.ToolStripButton toolStripAmplify;
        private System.Windows.Forms.ContextMenuStrip contextMenuImageSource;
        private System.Windows.Forms.ToolStripMenuItem menuItemGit;
        private System.Windows.Forms.ToolStripMenuItem menuItemWeb;
    }
}
