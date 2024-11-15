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
            this.oldScreenshotLabel = new System.Windows.Forms.Label();
            this.newScreenshotLabelPanel = new System.Windows.Forms.Panel();
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
            this.progressBar = new pwiz.Common.Controls.CustomTextProgressBar();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
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
            this.SuspendLayout();
            // 
            // oldScreenshotPictureBox
            // 
            this.oldScreenshotPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.oldScreenshotPictureBox.Location = new System.Drawing.Point(0, 30);
            this.oldScreenshotPictureBox.Margin = new System.Windows.Forms.Padding(2);
            this.oldScreenshotPictureBox.Name = "oldScreenshotPictureBox";
            this.oldScreenshotPictureBox.Size = new System.Drawing.Size(464, 361);
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
            this.newScreenshotPictureBox.Size = new System.Drawing.Size(511, 361);
            this.newScreenshotPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.newScreenshotPictureBox.TabIndex = 1;
            this.newScreenshotPictureBox.TabStop = false;
            // 
            // previewSplitContainer
            // 
            this.previewSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewSplitContainer.Location = new System.Drawing.Point(0, 39);
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
            this.previewSplitContainer.Size = new System.Drawing.Size(976, 391);
            this.previewSplitContainer.SplitterDistance = 464;
            this.previewSplitContainer.SplitterWidth = 1;
            this.previewSplitContainer.TabIndex = 1;
            // 
            // oldScreenshotLabelPanel
            // 
            this.oldScreenshotLabelPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.oldScreenshotLabelPanel.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.oldScreenshotLabelPanel.Controls.Add(this.oldScreenshotLabel);
            this.oldScreenshotLabelPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.oldScreenshotLabelPanel.Location = new System.Drawing.Point(0, 0);
            this.oldScreenshotLabelPanel.Margin = new System.Windows.Forms.Padding(2);
            this.oldScreenshotLabelPanel.Name = "oldScreenshotLabelPanel";
            this.oldScreenshotLabelPanel.Size = new System.Drawing.Size(464, 30);
            this.oldScreenshotLabelPanel.TabIndex = 1;
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
            this.oldScreenshotLabel.Size = new System.Drawing.Size(464, 30);
            this.oldScreenshotLabel.TabIndex = 0;
            this.oldScreenshotLabel.Text = "Old Screenshot";
            this.oldScreenshotLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // newScreenshotLabelPanel
            // 
            this.newScreenshotLabelPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.newScreenshotLabelPanel.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.newScreenshotLabelPanel.Controls.Add(this.newScreenshotLabel);
            this.newScreenshotLabelPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.newScreenshotLabelPanel.Location = new System.Drawing.Point(0, 0);
            this.newScreenshotLabelPanel.Margin = new System.Windows.Forms.Padding(2);
            this.newScreenshotLabelPanel.Name = "newScreenshotLabelPanel";
            this.newScreenshotLabelPanel.Size = new System.Drawing.Size(511, 30);
            this.newScreenshotLabelPanel.TabIndex = 3;
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
            this.newScreenshotLabel.Size = new System.Drawing.Size(511, 30);
            this.newScreenshotLabel.TabIndex = 0;
            this.newScreenshotLabel.Text = "New Screenshot";
            this.newScreenshotLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
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
            this.splitBar.Location = new System.Drawing.Point(0, 0);
            this.splitBar.Name = "splitBar";
            // 
            // splitBar.Panel1
            // 
            this.splitBar.Panel1.Controls.Add(this.progressBar);
            this.splitBar.Panel1.Controls.Add(this.descriptionLinkLabel);
            // 
            // splitBar.Panel2
            // 
            this.splitBar.Panel2.Controls.Add(this.previewFlowLayoutControlPanel);
            this.splitBar.Size = new System.Drawing.Size(976, 39);
            this.splitBar.SplitterDistance = 280;
            this.splitBar.TabIndex = 0;
            this.splitBar.TabStop = false;
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.CustomText = null;
            this.progressBar.DisplayStyle = pwiz.Common.Controls.ProgressBarDisplayText.CustomText;
            this.progressBar.Location = new System.Drawing.Point(166, 10);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(100, 18);
            this.progressBar.TabIndex = 1;
            this.progressBar.Visible = false;
            // 
            // ScreenshotPreviewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(976, 430);
            this.Controls.Add(this.previewSplitContainer);
            this.Controls.Add(this.splitBar);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "ScreenshotPreviewForm";
            this.Text = "Preview Screenshot";
            ((System.ComponentModel.ISupportInitialize)(this.oldScreenshotPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.newScreenshotPictureBox)).EndInit();
            this.previewSplitContainer.Panel1.ResumeLayout(false);
            this.previewSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.previewSplitContainer)).EndInit();
            this.previewSplitContainer.ResumeLayout(false);
            this.oldScreenshotLabelPanel.ResumeLayout(false);
            this.newScreenshotLabelPanel.ResumeLayout(false);
            this.previewFlowLayoutControlPanel.ResumeLayout(false);
            this.previewFlowLayoutControlPanel.PerformLayout();
            this.splitBar.Panel1.ResumeLayout(false);
            this.splitBar.Panel1.PerformLayout();
            this.splitBar.Panel2.ResumeLayout(false);
            this.splitBar.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitBar)).EndInit();
            this.splitBar.ResumeLayout(false);
            this.ResumeLayout(false);

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
    }
}