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
            this.buttonImageSource = new System.Windows.Forms.Button();
            this.oldScreenshotLabel = new System.Windows.Forms.Label();
            this.newScreenshotLabelPanel = new System.Windows.Forms.Panel();
            this.progressBar = new pwiz.Common.Controls.CustomTextProgressBar();
            this.newScreenshotLabel = new System.Windows.Forms.Label();
            this.previewFlowLayoutControlPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.continueBtn = new System.Windows.Forms.Button();
            this.labelNext = new System.Windows.Forms.Label();
            this.textBoxNext = new System.Windows.Forms.TextBox();
            this.refreshBtn = new System.Windows.Forms.Button();
            this.saveScreenshotBtn = new System.Windows.Forms.Button();
            this.saveScreenshotAndContinueBtn = new System.Windows.Forms.Button();
            this.autoSizeWindowCheckbox = new System.Windows.Forms.CheckBox();
            this.descriptionLinkLabel = new System.Windows.Forms.LinkLabel();
            this.splitBar = new System.Windows.Forms.SplitContainer();
            this.buttonSwitchToToolStrip = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolStripContinue = new System.Windows.Forms.ToolStripButton();
            this.toolStripLabelNext = new System.Windows.Forms.ToolStripLabel();
            this.toolStripTextBoxNext = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripRefresh = new System.Windows.Forms.ToolStripButton();
            this.toolStripSave = new System.Windows.Forms.ToolStripButton();
            this.toolStripSaveAndContinue = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripAutoSize = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripGotoWeb = new System.Windows.Forms.ToolStripButton();
            this.toolStripDescription = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSwitchToToolbar = new System.Windows.Forms.ToolStripButton();
            this.labelOldSize = new System.Windows.Forms.Label();
            this.labelNewSize = new System.Windows.Forms.Label();
            this.pictureMatching = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.oldScreenshotPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.newScreenshotPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.previewSplitContainer)).BeginInit();
            this.previewSplitContainer.Panel1.SuspendLayout();
            this.previewSplitContainer.Panel2.SuspendLayout();
            this.previewSplitContainer.SuspendLayout();
            this.oldScreenshotLabelPanel.SuspendLayout();
            this.newScreenshotLabelPanel.SuspendLayout();
            this.previewFlowLayoutControlPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitBar)).BeginInit();
            this.splitBar.Panel1.SuspendLayout();
            this.splitBar.Panel2.SuspendLayout();
            this.splitBar.SuspendLayout();
            this.toolStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureMatching)).BeginInit();
            this.SuspendLayout();
            // 
            // oldScreenshotPictureBox
            // 
            this.oldScreenshotPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.oldScreenshotPictureBox.Location = new System.Drawing.Point(0, 30);
            this.oldScreenshotPictureBox.Margin = new System.Windows.Forms.Padding(2);
            this.oldScreenshotPictureBox.Name = "oldScreenshotPictureBox";
            this.oldScreenshotPictureBox.Size = new System.Drawing.Size(488, 367);
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
            this.newScreenshotPictureBox.Size = new System.Drawing.Size(487, 367);
            this.newScreenshotPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.newScreenshotPictureBox.TabIndex = 1;
            this.newScreenshotPictureBox.TabStop = false;
            // 
            // previewSplitContainer
            // 
            this.previewSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewSplitContainer.IsSplitterFixed = true;
            this.previewSplitContainer.Location = new System.Drawing.Point(0, 64);
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
            this.previewSplitContainer.Size = new System.Drawing.Size(976, 397);
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
            // buttonImageSource
            // 
            this.buttonImageSource.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonImageSource.Image = global::pwiz.SkylineTestUtil.Properties.Resources.save;
            this.buttonImageSource.Location = new System.Drawing.Point(455, 4);
            this.buttonImageSource.Margin = new System.Windows.Forms.Padding(1);
            this.buttonImageSource.Name = "buttonImageSource";
            this.buttonImageSource.Size = new System.Drawing.Size(22, 22);
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
            // previewFlowLayoutControlPanel
            // 
            this.previewFlowLayoutControlPanel.AutoSize = true;
            this.previewFlowLayoutControlPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.previewFlowLayoutControlPanel.Controls.Add(this.continueBtn);
            this.previewFlowLayoutControlPanel.Controls.Add(this.labelNext);
            this.previewFlowLayoutControlPanel.Controls.Add(this.textBoxNext);
            this.previewFlowLayoutControlPanel.Controls.Add(this.refreshBtn);
            this.previewFlowLayoutControlPanel.Controls.Add(this.saveScreenshotBtn);
            this.previewFlowLayoutControlPanel.Controls.Add(this.saveScreenshotAndContinueBtn);
            this.previewFlowLayoutControlPanel.Controls.Add(this.autoSizeWindowCheckbox);
            this.previewFlowLayoutControlPanel.Location = new System.Drawing.Point(2, 5);
            this.previewFlowLayoutControlPanel.Margin = new System.Windows.Forms.Padding(2);
            this.previewFlowLayoutControlPanel.Name = "previewFlowLayoutControlPanel";
            this.previewFlowLayoutControlPanel.Size = new System.Drawing.Size(516, 27);
            this.previewFlowLayoutControlPanel.TabIndex = 4;
            // 
            // continueBtn
            // 
            this.continueBtn.Location = new System.Drawing.Point(2, 2);
            this.continueBtn.Margin = new System.Windows.Forms.Padding(2);
            this.continueBtn.Name = "continueBtn";
            this.continueBtn.Size = new System.Drawing.Size(67, 23);
            this.continueBtn.TabIndex = 0;
            this.continueBtn.Text = "&Continue";
            this.continueBtn.UseVisualStyleBackColor = true;
            this.continueBtn.Click += new System.EventHandler(this.continueBtn_Click);
            // 
            // labelNext
            // 
            this.labelNext.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelNext.AutoSize = true;
            this.labelNext.Location = new System.Drawing.Point(74, 7);
            this.labelNext.Name = "labelNext";
            this.labelNext.Size = new System.Drawing.Size(32, 13);
            this.labelNext.TabIndex = 1;
            this.labelNext.Text = "&Next:";
            // 
            // textBoxNext
            // 
            this.textBoxNext.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.textBoxNext.Location = new System.Drawing.Point(112, 3);
            this.textBoxNext.Name = "textBoxNext";
            this.textBoxNext.Size = new System.Drawing.Size(41, 20);
            this.textBoxNext.TabIndex = 2;
            this.textBoxNext.TextChanged += new System.EventHandler(this.textBoxNext_TextChanged);
            this.textBoxNext.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBoxNext_KeyDown);
            // 
            // refreshBtn
            // 
            this.refreshBtn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.refreshBtn.Location = new System.Drawing.Point(158, 2);
            this.refreshBtn.Margin = new System.Windows.Forms.Padding(2);
            this.refreshBtn.Name = "refreshBtn";
            this.refreshBtn.Size = new System.Drawing.Size(55, 23);
            this.refreshBtn.TabIndex = 3;
            this.refreshBtn.Text = "&Refresh";
            this.refreshBtn.UseVisualStyleBackColor = true;
            this.refreshBtn.Click += new System.EventHandler(this.refreshBtn_Click);
            // 
            // saveScreenshotBtn
            // 
            this.saveScreenshotBtn.Location = new System.Drawing.Point(217, 2);
            this.saveScreenshotBtn.Margin = new System.Windows.Forms.Padding(2);
            this.saveScreenshotBtn.Name = "saveScreenshotBtn";
            this.saveScreenshotBtn.Size = new System.Drawing.Size(99, 23);
            this.saveScreenshotBtn.TabIndex = 4;
            this.saveScreenshotBtn.Text = "&Save Screenshot";
            this.saveScreenshotBtn.UseVisualStyleBackColor = true;
            this.saveScreenshotBtn.Click += new System.EventHandler(this.saveScreenshotBtn_Click);
            // 
            // saveScreenshotAndContinueBtn
            // 
            this.saveScreenshotAndContinueBtn.Location = new System.Drawing.Point(320, 2);
            this.saveScreenshotAndContinueBtn.Margin = new System.Windows.Forms.Padding(2);
            this.saveScreenshotAndContinueBtn.Name = "saveScreenshotAndContinueBtn";
            this.saveScreenshotAndContinueBtn.Size = new System.Drawing.Size(121, 23);
            this.saveScreenshotAndContinueBtn.TabIndex = 5;
            this.saveScreenshotAndContinueBtn.Text = "Sa&ve and Continue";
            this.saveScreenshotAndContinueBtn.UseVisualStyleBackColor = true;
            this.saveScreenshotAndContinueBtn.Click += new System.EventHandler(this.saveScreenshotAndContinueBtn_Click);
            // 
            // autoSizeWindowCheckbox
            // 
            this.autoSizeWindowCheckbox.AutoSize = true;
            this.autoSizeWindowCheckbox.Checked = true;
            this.autoSizeWindowCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.autoSizeWindowCheckbox.Location = new System.Drawing.Point(445, 2);
            this.autoSizeWindowCheckbox.Margin = new System.Windows.Forms.Padding(2);
            this.autoSizeWindowCheckbox.Name = "autoSizeWindowCheckbox";
            this.autoSizeWindowCheckbox.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.autoSizeWindowCheckbox.Size = new System.Drawing.Size(69, 21);
            this.autoSizeWindowCheckbox.TabIndex = 6;
            this.autoSizeWindowCheckbox.Text = "&Auto-size";
            this.autoSizeWindowCheckbox.UseVisualStyleBackColor = true;
            this.autoSizeWindowCheckbox.CheckedChanged += new System.EventHandler(this.autoSizeWindowCheckbox_CheckedChanged);
            // 
            // descriptionLinkLabel
            // 
            this.descriptionLinkLabel.AutoSize = true;
            this.descriptionLinkLabel.Location = new System.Drawing.Point(11, 12);
            this.descriptionLinkLabel.Margin = new System.Windows.Forms.Padding(2, 6, 2, 0);
            this.descriptionLinkLabel.Name = "descriptionLinkLabel";
            this.descriptionLinkLabel.Size = new System.Drawing.Size(79, 13);
            this.descriptionLinkLabel.TabIndex = 0;
            this.descriptionLinkLabel.TabStop = true;
            this.descriptionLinkLabel.Text = "Description link";
            this.descriptionLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.descriptionLinkLabel_LinkClicked);
            // 
            // splitBar
            // 
            this.splitBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitBar.Location = new System.Drawing.Point(0, 25);
            this.splitBar.Name = "splitBar";
            // 
            // splitBar.Panel1
            // 
            this.splitBar.Panel1.Controls.Add(this.descriptionLinkLabel);
            // 
            // splitBar.Panel2
            // 
            this.splitBar.Panel2.Controls.Add(this.buttonSwitchToToolStrip);
            this.splitBar.Panel2.Controls.Add(this.previewFlowLayoutControlPanel);
            this.splitBar.Size = new System.Drawing.Size(976, 39);
            this.splitBar.SplitterDistance = 280;
            this.splitBar.TabIndex = 0;
            this.splitBar.TabStop = false;
            // 
            // buttonSwitchToToolStrip
            // 
            this.buttonSwitchToToolStrip.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSwitchToToolStrip.Image = global::pwiz.SkylineTestUtil.Properties.Resources.imagebutton;
            this.buttonSwitchToToolStrip.Location = new System.Drawing.Point(665, 8);
            this.buttonSwitchToToolStrip.Name = "buttonSwitchToToolStrip";
            this.buttonSwitchToToolStrip.Size = new System.Drawing.Size(27, 23);
            this.buttonSwitchToToolStrip.TabIndex = 5;
            this.helpTip.SetToolTip(this.buttonSwitchToToolStrip, "Show Image Buttons");
            this.buttonSwitchToToolStrip.UseVisualStyleBackColor = true;
            this.buttonSwitchToToolStrip.Click += new System.EventHandler(this.buttonSwitchToToolStrip_Click);
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
            this.toolStripSeparator1,
            this.toolStripAutoSize,
            this.toolStripSeparator2,
            this.toolStripGotoWeb,
            this.toolStripDescription,
            this.toolStripSwitchToToolbar});
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
            this.toolStripTextBoxNext.TextChanged += new System.EventHandler(this.toolStripTextBoxNext_TextChanged);
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
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
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
            // toolStripSwitchToToolbar
            // 
            this.toolStripSwitchToToolbar.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripSwitchToToolbar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripSwitchToToolbar.Image = global::pwiz.SkylineTestUtil.Properties.Resources.textbutton;
            this.toolStripSwitchToToolbar.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSwitchToToolbar.Name = "toolStripSwitchToToolbar";
            this.toolStripSwitchToToolbar.Size = new System.Drawing.Size(23, 22);
            this.toolStripSwitchToToolbar.ToolTipText = "Show Text Buttons";
            this.toolStripSwitchToToolbar.Click += new System.EventHandler(this.toolStripSwitchToToolbar_Click);
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
            // labelNewSize
            // 
            this.labelNewSize.AutoSize = true;
            this.labelNewSize.Location = new System.Drawing.Point(5, 9);
            this.labelNewSize.Name = "labelNewSize";
            this.labelNewSize.Size = new System.Drawing.Size(48, 13);
            this.labelNewSize.TabIndex = 2;
            this.labelNewSize.Text = "new size";
            // 
            // pictureMatching
            // 
            this.pictureMatching.Location = new System.Drawing.Point(5, 7);
            this.pictureMatching.Name = "pictureMatching";
            this.pictureMatching.Size = new System.Drawing.Size(16, 16);
            this.pictureMatching.TabIndex = 3;
            this.pictureMatching.TabStop = false;
            // 
            // ScreenshotPreviewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(976, 461);
            this.Controls.Add(this.previewSplitContainer);
            this.Controls.Add(this.splitBar);
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
            this.newScreenshotLabelPanel.ResumeLayout(false);
            this.newScreenshotLabelPanel.PerformLayout();
            this.previewFlowLayoutControlPanel.ResumeLayout(false);
            this.previewFlowLayoutControlPanel.PerformLayout();
            this.splitBar.Panel1.ResumeLayout(false);
            this.splitBar.Panel1.PerformLayout();
            this.splitBar.Panel2.ResumeLayout(false);
            this.splitBar.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitBar)).EndInit();
            this.splitBar.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureMatching)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox oldScreenshotPictureBox;
        private System.Windows.Forms.PictureBox newScreenshotPictureBox;
        private System.Windows.Forms.SplitContainer previewSplitContainer;
        private System.Windows.Forms.Label newScreenshotLabel;
        private System.Windows.Forms.Panel newScreenshotLabelPanel;
        private System.Windows.Forms.Button continueBtn;
        private System.Windows.Forms.Button refreshBtn;
        private System.Windows.Forms.Button saveScreenshotAndContinueBtn;
        private System.Windows.Forms.Button saveScreenshotBtn;
        private System.Windows.Forms.FlowLayoutPanel previewFlowLayoutControlPanel;
        private System.Windows.Forms.CheckBox autoSizeWindowCheckbox;
        private System.Windows.Forms.LinkLabel descriptionLinkLabel;
        private System.Windows.Forms.SplitContainer splitBar;
        private System.Windows.Forms.ToolTip helpTip;
        private pwiz.Common.Controls.CustomTextProgressBar progressBar;
        private System.Windows.Forms.Panel oldScreenshotLabelPanel;
        private System.Windows.Forms.Label oldScreenshotLabel;
        private System.Windows.Forms.Label labelNext;
        private System.Windows.Forms.TextBox textBoxNext;
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
        private System.Windows.Forms.Button buttonSwitchToToolStrip;
        private System.Windows.Forms.ToolStripButton toolStripSwitchToToolbar;
        private System.Windows.Forms.Button buttonImageSource;
        private System.Windows.Forms.Label labelOldSize;
        private System.Windows.Forms.Label labelNewSize;
        private System.Windows.Forms.PictureBox pictureMatching;
    }
}