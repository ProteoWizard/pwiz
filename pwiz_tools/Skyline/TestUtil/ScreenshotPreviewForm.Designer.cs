using pwiz.Common.Controls;

namespace pwiz.SkylineTestUtil
{
    partial class ScreenshotPreviewForm
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
            this.progressBar = new pwiz.Common.Controls.CustomTextProgressBar();
            this.newScreenshotLabel = new System.Windows.Forms.Label();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolStripContinue = new System.Windows.Forms.ToolStripButton();
            this.toolStripLabelNext = new System.Windows.Forms.ToolStripLabel();
            this.toolStripTextBoxNext = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripRefresh = new System.Windows.Forms.ToolStripButton();
            this.toolStripSave = new System.Windows.Forms.ToolStripButton();
            this.toolStripSaveAndContinue = new System.Windows.Forms.ToolStripButton();
            this.toolStripRevert = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripAutoSize = new System.Windows.Forms.ToolStripButton();
            this.toolStripPickColorButton = new pwiz.SkylineTestUtil.AlphaColorPickerButton();
            this.toolStripDiffOnly = new System.Windows.Forms.ToolStripButton();
            this.toolStripAmplify = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuImageSource = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuItemDisk = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemGit = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemWeb = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripGotoWeb = new System.Windows.Forms.ToolStripButton();
            this.toolStripDescription = new System.Windows.Forms.ToolStripLabel();
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
            this.buttonImageSource.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonImageSource.Image = global::pwiz.SkylineTestUtil.Properties.Resources.fileunknown;
            this.buttonImageSource.Location = new System.Drawing.Point(455, 4);
            this.buttonImageSource.Margin = new System.Windows.Forms.Padding(1);
            this.buttonImageSource.Name = "buttonImageSource";
            this.buttonImageSource.Size = new System.Drawing.Size(23, 23);
            this.buttonImageSource.TabIndex = 1;
            this.buttonImageSource.TabStop = false;
            this.helpTip.SetToolTip(this.buttonImageSource, "Switch Source (Ctrl-Tab)\r\nCurrent: Disk");
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
            this.newScreenshotLabelPanel.Controls.Add(this.labelNewSize);
            this.newScreenshotLabelPanel.Controls.Add(this.progressBar);
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
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.CustomText = "Waiting on Skyline for next screenshot...";
            this.progressBar.DisplayStyle = pwiz.Common.Controls.ProgressBarDisplayText.CustomText;
            this.progressBar.Location = new System.Drawing.Point(408, 4);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(76, 21);
            this.progressBar.TabIndex = 1;
            this.progressBar.Visible = false;
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
            this.toolStripContinue,
            this.toolStripLabelNext,
            this.toolStripTextBoxNext,
            this.toolStripRefresh,
            this.toolStripSave,
            this.toolStripSaveAndContinue,
            this.toolStripRevert,
            this.toolStripSeparator1,
            this.toolStripAutoSize,
            this.toolStripPickColorButton,
            this.toolStripDiffOnly,
            this.toolStripAmplify,
            this.toolStripSeparator2,
            this.toolStripGotoWeb,
            this.toolStripDescription});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(976, 25);
            this.toolStrip.TabIndex = 2;
            // 
            // toolStripContinue
            // 
            this.toolStripContinue.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripContinue.Image = global::pwiz.SkylineTestUtil.Properties.Resources.continue_test;
            this.toolStripContinue.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripContinue.Name = "toolStripContinue";
            this.toolStripContinue.Size = new System.Drawing.Size(23, 22);
            this.toolStripContinue.ToolTipText = "Continue (F5)";
            this.toolStripContinue.Click += new System.EventHandler(this.toolStripContinue_Click);
            // 
            // toolStripLabelNext
            // 
            this.toolStripLabelNext.Name = "toolStripLabelNext";
            this.toolStripLabelNext.Size = new System.Drawing.Size(35, 22);
            this.toolStripLabelNext.Text = "&Next:";
            // 
            // toolStripTextBoxNext
            // 
            this.toolStripTextBoxNext.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.toolStripTextBoxNext.Name = "toolStripTextBoxNext";
            this.toolStripTextBoxNext.Size = new System.Drawing.Size(40, 25);
            this.toolStripTextBoxNext.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBoxNext_KeyDown);
            // 
            // toolStripRefresh
            // 
            this.toolStripRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripRefresh.Image = global::pwiz.SkylineTestUtil.Properties.Resources.refresh;
            this.toolStripRefresh.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripRefresh.Name = "toolStripRefresh";
            this.toolStripRefresh.Size = new System.Drawing.Size(23, 22);
            this.toolStripRefresh.ToolTipText = "Refresh (Ctrl-R)";
            this.toolStripRefresh.Click += new System.EventHandler(this.toolStripRefresh_Click);
            // 
            // toolStripSave
            // 
            this.toolStripSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripSave.Image = global::pwiz.SkylineTestUtil.Properties.Resources.save;
            this.toolStripSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSave.Name = "toolStripSave";
            this.toolStripSave.Size = new System.Drawing.Size(23, 22);
            this.toolStripSave.ToolTipText = "Save (Ctrl-S)";
            this.toolStripSave.Click += new System.EventHandler(this.toolStripSave_Click);
            // 
            // toolStripSaveAndContinue
            // 
            this.toolStripSaveAndContinue.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripSaveAndContinue.Image = global::pwiz.SkylineTestUtil.Properties.Resources.save_and_continue;
            this.toolStripSaveAndContinue.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSaveAndContinue.Name = "toolStripSaveAndContinue";
            this.toolStripSaveAndContinue.Size = new System.Drawing.Size(23, 22);
            this.toolStripSaveAndContinue.ToolTipText = "Save and Continue (F6)";
            this.toolStripSaveAndContinue.Click += new System.EventHandler(this.toolStripSaveAndContinue_Click);
            // 
            // toolStripRevert
            // 
            this.toolStripRevert.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripRevert.Image = global::pwiz.SkylineTestUtil.Properties.Resources.undo;
            this.toolStripRevert.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripRevert.Name = "toolStripRevert";
            this.toolStripRevert.Size = new System.Drawing.Size(23, 22);
            this.toolStripRevert.ToolTipText = "Revert (Ctrl-Z)";
            this.toolStripRevert.Click += new System.EventHandler(this.toolStripRevert_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // toolStripAutoSize
            // 
            this.toolStripAutoSize.Checked = true;
            this.toolStripAutoSize.CheckOnClick = true;
            this.toolStripAutoSize.CheckState = System.Windows.Forms.CheckState.Checked;
            this.toolStripAutoSize.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripAutoSize.Image = global::pwiz.SkylineTestUtil.Properties.Resources.autosizeoptimize;
            this.toolStripAutoSize.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripAutoSize.Name = "toolStripAutoSize";
            this.toolStripAutoSize.Size = new System.Drawing.Size(23, 22);
            this.toolStripAutoSize.ToolTipText = "Auto-Size";
            this.toolStripAutoSize.CheckedChanged += new System.EventHandler(this.toolStripAutoSize_CheckedChanged);
            // 
            // toolStripPickColorButton
            // 
            this.toolStripPickColorButton.BackColor = System.Drawing.SystemColors.Control;
            this.toolStripPickColorButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripPickColorButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripPickColorButton.Name = "toolStripPickColorButton";
            this.toolStripPickColorButton.SelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(255)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.toolStripPickColorButton.Size = new System.Drawing.Size(13, 22);
            this.toolStripPickColorButton.ToolTipText = "Selected Color: Color [A=128, R=255, G=0, B=0] (Alpha: 128)";
            this.toolStripPickColorButton.ColorChanged += new System.EventHandler(this.toolStripPickColorButton_ColorChanged);
            //
            // toolStripDiffOnly
            //
            this.toolStripDiffOnly.CheckOnClick = true;
            this.toolStripDiffOnly.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripDiffOnly.Image = global::pwiz.SkylineTestUtil.Properties.Resources.blank;
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
            // toolStripSeparator2
            //
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            //
            // contextMenuImageSource
            //
            this.contextMenuImageSource.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemDisk,
            this.menuItemGit,
            this.menuItemWeb});
            this.contextMenuImageSource.Name = "contextMenuImageSource";
            this.contextMenuImageSource.Size = new System.Drawing.Size(120, 70);
            //
            // menuItemDisk
            //
            this.menuItemDisk.Image = global::pwiz.SkylineTestUtil.Properties.Resources.save;
            this.menuItemDisk.Name = "menuItemDisk";
            this.menuItemDisk.Size = new System.Drawing.Size(119, 22);
            this.menuItemDisk.Text = "Disk";
            this.menuItemDisk.Click += new System.EventHandler(this.menuItemDisk_Click);
            //
            // menuItemGit
            //
            this.menuItemGit.Image = global::pwiz.SkylineTestUtil.Properties.Resources.gitsource;
            this.menuItemGit.Name = "menuItemGit";
            this.menuItemGit.Size = new System.Drawing.Size(119, 22);
            this.menuItemGit.Text = "Git HEAD";
            this.menuItemGit.Click += new System.EventHandler(this.menuItemGit_Click);
            //
            // menuItemWeb
            //
            this.menuItemWeb.Image = global::pwiz.SkylineTestUtil.Properties.Resources.websource;
            this.menuItemWeb.Name = "menuItemWeb";
            this.menuItemWeb.Size = new System.Drawing.Size(119, 22);
            this.menuItemWeb.Text = "Web";
            this.menuItemWeb.Click += new System.EventHandler(this.menuItemWeb_Click);
            //
            // toolStripGotoWeb
            // 
            this.toolStripGotoWeb.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripGotoWeb.Image = global::pwiz.SkylineTestUtil.Properties.Resources.webdestination;
            this.toolStripGotoWeb.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripGotoWeb.Name = "toolStripGotoWeb";
            this.toolStripGotoWeb.Size = new System.Drawing.Size(23, 22);
            this.toolStripGotoWeb.ToolTipText = "Goto Web (Ctrl-G)";
            this.toolStripGotoWeb.Click += new System.EventHandler(this.toolStripGotoWeb_Click);
            // 
            // toolStripDescription
            // 
            this.toolStripDescription.Name = "toolStripDescription";
            this.toolStripDescription.Size = new System.Drawing.Size(38, 22);
            this.toolStripDescription.Text = "s-1: ...";
            // 
            // ScreenshotPreviewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(976, 461);
            this.Controls.Add(this.previewSplitContainer);
            this.Controls.Add(this.toolStrip);
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MinimumSize = new System.Drawing.Size(400, 200);
            this.Name = "ScreenshotPreviewForm";
            this.Text = "Preview Screenshot";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ScreenshotPreviewForm_KeyDown);
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
        private pwiz.Common.Controls.CustomTextProgressBar progressBar;
        private System.Windows.Forms.Panel oldScreenshotLabelPanel;
        private System.Windows.Forms.Label oldScreenshotLabel;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton toolStripContinue;
        private System.Windows.Forms.ToolStripButton toolStripRefresh;
        private System.Windows.Forms.ToolStripButton toolStripSave;
        private System.Windows.Forms.ToolStripButton toolStripSaveAndContinue;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton toolStripGotoWeb;
        private System.Windows.Forms.ToolStripLabel toolStripDescription;
        private System.Windows.Forms.ToolStripButton toolStripAutoSize;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripLabel toolStripLabelNext;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBoxNext;
        private System.Windows.Forms.Button buttonImageSource;
        private System.Windows.Forms.Label labelOldSize;
        private System.Windows.Forms.Label labelNewSize;
        private System.Windows.Forms.PictureBox pictureMatching;
        private AlphaColorPickerButton toolStripPickColorButton;
        private System.Windows.Forms.ToolStripButton toolStripRevert;
        private System.Windows.Forms.ToolStripButton toolStripDiffOnly;
        private System.Windows.Forms.ToolStripButton toolStripAmplify;
        private System.Windows.Forms.ContextMenuStrip contextMenuImageSource;
        private System.Windows.Forms.ToolStripMenuItem menuItemDisk;
        private System.Windows.Forms.ToolStripMenuItem menuItemGit;
        private System.Windows.Forms.ToolStripMenuItem menuItemWeb;
    }
}