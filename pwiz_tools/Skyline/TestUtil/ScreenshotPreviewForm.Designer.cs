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
            this.oldScreenshotPictureBox = new System.Windows.Forms.PictureBox();
            this.newScreenshotPictureBox = new System.Windows.Forms.PictureBox();
            this.previewSplitContainer = new System.Windows.Forms.SplitContainer();
            this.oldScreenshotLabelPanel = new System.Windows.Forms.Panel();
            this.oldScreenshotLabel = new System.Windows.Forms.Label();
            this.newScreenshotLabelPanel = new System.Windows.Forms.Panel();
            this.newScreenshotLabel = new System.Windows.Forms.Label();
            this.previewTableLayoutControlPanel = new System.Windows.Forms.TableLayoutPanel();
            this.previewFlowLayoutControlPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.descriptionLinkLabel = new System.Windows.Forms.LinkLabel();
            this.continueBtn = new System.Windows.Forms.Button();
            this.saveScreenshotBtn = new System.Windows.Forms.Button();
            this.saveScreenshotAndContinueBtn = new System.Windows.Forms.Button();
            this.refreshBtn = new System.Windows.Forms.Button();
            this.autoSizeWindowCheckbox = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.oldScreenshotPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.newScreenshotPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.previewSplitContainer)).BeginInit();
            this.previewSplitContainer.Panel1.SuspendLayout();
            this.previewSplitContainer.Panel2.SuspendLayout();
            this.previewSplitContainer.SuspendLayout();
            this.oldScreenshotLabelPanel.SuspendLayout();
            this.newScreenshotLabelPanel.SuspendLayout();
            this.previewTableLayoutControlPanel.SuspendLayout();
            this.previewFlowLayoutControlPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // oldScreenshotPictureBox
            // 
            this.oldScreenshotPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.oldScreenshotPictureBox.Location = new System.Drawing.Point(0, 46);
            this.oldScreenshotPictureBox.Name = "oldScreenshotPictureBox";
            this.oldScreenshotPictureBox.Size = new System.Drawing.Size(699, 556);
            this.oldScreenshotPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.oldScreenshotPictureBox.TabIndex = 0;
            this.oldScreenshotPictureBox.TabStop = false;
            // 
            // newScreenshotPictureBox
            // 
            this.newScreenshotPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.newScreenshotPictureBox.Location = new System.Drawing.Point(0, 46);
            this.newScreenshotPictureBox.Name = "newScreenshotPictureBox";
            this.newScreenshotPictureBox.Size = new System.Drawing.Size(764, 556);
            this.newScreenshotPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.newScreenshotPictureBox.TabIndex = 1;
            this.newScreenshotPictureBox.TabStop = false;
            // 
            // previewSplitContainer
            // 
            this.previewSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewSplitContainer.Location = new System.Drawing.Point(0, 59);
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
            this.previewSplitContainer.Size = new System.Drawing.Size(1464, 602);
            this.previewSplitContainer.SplitterDistance = 699;
            this.previewSplitContainer.SplitterWidth = 1;
            this.previewSplitContainer.TabIndex = 2;
            // 
            // oldScreenshotLabelPanel
            // 
            this.oldScreenshotLabelPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.oldScreenshotLabelPanel.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.oldScreenshotLabelPanel.Controls.Add(this.oldScreenshotLabel);
            this.oldScreenshotLabelPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.oldScreenshotLabelPanel.Location = new System.Drawing.Point(0, 0);
            this.oldScreenshotLabelPanel.Name = "oldScreenshotLabelPanel";
            this.oldScreenshotLabelPanel.Size = new System.Drawing.Size(699, 46);
            this.oldScreenshotLabelPanel.TabIndex = 1;
            // 
            // oldScreenshotLabel
            // 
            this.oldScreenshotLabel.BackColor = System.Drawing.Color.Transparent;
            this.oldScreenshotLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.oldScreenshotLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F);
            this.oldScreenshotLabel.ForeColor = System.Drawing.Color.Black;
            this.oldScreenshotLabel.Location = new System.Drawing.Point(0, 0);
            this.oldScreenshotLabel.Name = "oldScreenshotLabel";
            this.oldScreenshotLabel.Size = new System.Drawing.Size(699, 46);
            this.oldScreenshotLabel.TabIndex = 2;
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
            this.newScreenshotLabelPanel.Name = "newScreenshotLabelPanel";
            this.newScreenshotLabelPanel.Size = new System.Drawing.Size(764, 46);
            this.newScreenshotLabelPanel.TabIndex = 3;
            // 
            // newScreenshotLabel
            // 
            this.newScreenshotLabel.BackColor = System.Drawing.Color.Transparent;
            this.newScreenshotLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.newScreenshotLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F);
            this.newScreenshotLabel.ForeColor = System.Drawing.Color.Black;
            this.newScreenshotLabel.Location = new System.Drawing.Point(0, 0);
            this.newScreenshotLabel.Name = "newScreenshotLabel";
            this.newScreenshotLabel.Size = new System.Drawing.Size(764, 46);
            this.newScreenshotLabel.TabIndex = 1;
            this.newScreenshotLabel.Text = "New Screenshot";
            this.newScreenshotLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // previewTableLayoutControlPanel
            // 
            this.previewTableLayoutControlPanel.ColumnCount = 1;
            this.previewTableLayoutControlPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.previewTableLayoutControlPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.previewTableLayoutControlPanel.Controls.Add(this.previewFlowLayoutControlPanel, 0, 0);
            this.previewTableLayoutControlPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.previewTableLayoutControlPanel.Location = new System.Drawing.Point(0, 0);
            this.previewTableLayoutControlPanel.Name = "previewTableLayoutControlPanel";
            this.previewTableLayoutControlPanel.RowCount = 1;
            this.previewTableLayoutControlPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.previewTableLayoutControlPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.previewTableLayoutControlPanel.Size = new System.Drawing.Size(1464, 59);
            this.previewTableLayoutControlPanel.TabIndex = 5;
            // 
            // previewFlowLayoutControlPanel
            // 
            this.previewFlowLayoutControlPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)));
            this.previewFlowLayoutControlPanel.AutoSize = true;
            this.previewFlowLayoutControlPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.previewFlowLayoutControlPanel.Controls.Add(this.descriptionLinkLabel);
            this.previewFlowLayoutControlPanel.Controls.Add(this.continueBtn);
            this.previewFlowLayoutControlPanel.Controls.Add(this.saveScreenshotBtn);
            this.previewFlowLayoutControlPanel.Controls.Add(this.saveScreenshotAndContinueBtn);
            this.previewFlowLayoutControlPanel.Controls.Add(this.refreshBtn);
            this.previewFlowLayoutControlPanel.Controls.Add(this.autoSizeWindowCheckbox);
            this.previewFlowLayoutControlPanel.Location = new System.Drawing.Point(325, 3);
            this.previewFlowLayoutControlPanel.Name = "previewFlowLayoutControlPanel";
            this.previewFlowLayoutControlPanel.Size = new System.Drawing.Size(814, 53);
            this.previewFlowLayoutControlPanel.TabIndex = 4;
            // 
            // descriptionLinkLabel
            // 
            this.descriptionLinkLabel.AutoSize = true;
            this.descriptionLinkLabel.Location = new System.Drawing.Point(3, 9);
            this.descriptionLinkLabel.Margin = new System.Windows.Forms.Padding(3, 9, 3, 0);
            this.descriptionLinkLabel.Name = "descriptionLinkLabel";
            this.descriptionLinkLabel.Size = new System.Drawing.Size(116, 20);
            this.descriptionLinkLabel.TabIndex = 6;
            this.descriptionLinkLabel.TabStop = true;
            this.descriptionLinkLabel.Text = "Description link";
            this.descriptionLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.descriptionLinkLabel_LinkClicked);
            // 
            // continueBtn
            // 
            this.continueBtn.Location = new System.Drawing.Point(125, 3);
            this.continueBtn.Name = "continueBtn";
            this.continueBtn.Size = new System.Drawing.Size(100, 36);
            this.continueBtn.TabIndex = 0;
            this.continueBtn.Text = "Continue";
            this.continueBtn.UseVisualStyleBackColor = true;
            this.continueBtn.Click += new System.EventHandler(this.continueBtn_Click);
            // 
            // saveScreenshotBtn
            // 
            this.saveScreenshotBtn.Location = new System.Drawing.Point(231, 3);
            this.saveScreenshotBtn.Name = "saveScreenshotBtn";
            this.saveScreenshotBtn.Size = new System.Drawing.Size(149, 36);
            this.saveScreenshotBtn.TabIndex = 1;
            this.saveScreenshotBtn.Text = "Save Screenshot";
            this.saveScreenshotBtn.UseVisualStyleBackColor = true;
            this.saveScreenshotBtn.Click += new System.EventHandler(this.saveScreenshotBtn_Click);
            // 
            // saveScreenshotAndContinueBtn
            // 
            this.saveScreenshotAndContinueBtn.Location = new System.Drawing.Point(386, 3);
            this.saveScreenshotAndContinueBtn.Name = "saveScreenshotAndContinueBtn";
            this.saveScreenshotAndContinueBtn.Size = new System.Drawing.Size(181, 36);
            this.saveScreenshotAndContinueBtn.TabIndex = 2;
            this.saveScreenshotAndContinueBtn.Text = "Save and Continue";
            this.saveScreenshotAndContinueBtn.UseVisualStyleBackColor = true;
            this.saveScreenshotAndContinueBtn.Click += new System.EventHandler(this.saveScreenshotAndContinueBtn_Click);
            // 
            // refreshBtn
            // 
            this.refreshBtn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.refreshBtn.Location = new System.Drawing.Point(573, 3);
            this.refreshBtn.Name = "refreshBtn";
            this.refreshBtn.Size = new System.Drawing.Size(82, 36);
            this.refreshBtn.TabIndex = 3;
            this.refreshBtn.Text = "Refresh";
            this.refreshBtn.UseVisualStyleBackColor = true;
            this.refreshBtn.Click += new System.EventHandler(this.refreshBtn_Click);
            // 
            // autoSizeWindowCheckbox
            // 
            this.autoSizeWindowCheckbox.AutoSize = true;
            this.autoSizeWindowCheckbox.Checked = true;
            this.autoSizeWindowCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.autoSizeWindowCheckbox.Location = new System.Drawing.Point(661, 3);
            this.autoSizeWindowCheckbox.Name = "autoSizeWindowCheckbox";
            this.autoSizeWindowCheckbox.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.autoSizeWindowCheckbox.Size = new System.Drawing.Size(150, 30);
            this.autoSizeWindowCheckbox.TabIndex = 5;
            this.autoSizeWindowCheckbox.Text = "Auto size window";
            this.autoSizeWindowCheckbox.UseVisualStyleBackColor = true;
            // 
            // ScreenshotPreviewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1464, 661);
            this.Controls.Add(this.previewSplitContainer);
            this.Controls.Add(this.previewTableLayoutControlPanel);
            this.Name = "ScreenshotPreviewForm";
            this.Text = "Preview";
            ((System.ComponentModel.ISupportInitialize)(this.oldScreenshotPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.newScreenshotPictureBox)).EndInit();
            this.previewSplitContainer.Panel1.ResumeLayout(false);
            this.previewSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.previewSplitContainer)).EndInit();
            this.previewSplitContainer.ResumeLayout(false);
            this.oldScreenshotLabelPanel.ResumeLayout(false);
            this.newScreenshotLabelPanel.ResumeLayout(false);
            this.previewTableLayoutControlPanel.ResumeLayout(false);
            this.previewTableLayoutControlPanel.PerformLayout();
            this.previewFlowLayoutControlPanel.ResumeLayout(false);
            this.previewFlowLayoutControlPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox oldScreenshotPictureBox;
        private System.Windows.Forms.PictureBox newScreenshotPictureBox;
        private System.Windows.Forms.SplitContainer previewSplitContainer;
        private System.Windows.Forms.Label newScreenshotLabel;
        private System.Windows.Forms.Label oldScreenshotLabel;
        private System.Windows.Forms.Panel oldScreenshotLabelPanel;
        private System.Windows.Forms.Panel newScreenshotLabelPanel;
        private System.Windows.Forms.Button continueBtn;
        private System.Windows.Forms.Button refreshBtn;
        private System.Windows.Forms.Button saveScreenshotAndContinueBtn;
        private System.Windows.Forms.Button saveScreenshotBtn;
        private System.Windows.Forms.FlowLayoutPanel previewFlowLayoutControlPanel;
        private System.Windows.Forms.TableLayoutPanel previewTableLayoutControlPanel;
        private System.Windows.Forms.CheckBox autoSizeWindowCheckbox;
        private System.Windows.Forms.LinkLabel descriptionLinkLabel;
    }
}