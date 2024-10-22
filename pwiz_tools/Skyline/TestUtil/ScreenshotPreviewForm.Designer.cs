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
            this.newScreenshotPictureBox = new System.Windows.Forms.PictureBox();
            this.oldScreenshotPictureBox = new System.Windows.Forms.PictureBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.newScreenshotLabel = new System.Windows.Forms.Label();
            this.oldScreenshotLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.newScreenshotPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.oldScreenshotPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // newScreenshotPictureBox
            // 
            this.newScreenshotPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.newScreenshotPictureBox.Location = new System.Drawing.Point(0, 0);
            this.newScreenshotPictureBox.Name = "newScreenshotPictureBox";
            this.newScreenshotPictureBox.Size = new System.Drawing.Size(308, 226);
            this.newScreenshotPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.newScreenshotPictureBox.TabIndex = 0;
            this.newScreenshotPictureBox.TabStop = false;
            // 
            // oldScreenshotPictureBox
            // 
            this.oldScreenshotPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.oldScreenshotPictureBox.Location = new System.Drawing.Point(0, 0);
            this.oldScreenshotPictureBox.Name = "oldScreenshotPictureBox";
            this.oldScreenshotPictureBox.Size = new System.Drawing.Size(328, 226);
            this.oldScreenshotPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.oldScreenshotPictureBox.TabIndex = 1;
            this.oldScreenshotPictureBox.TabStop = false;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.newScreenshotLabel);
            this.splitContainer1.Panel1.Controls.Add(this.newScreenshotPictureBox);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.oldScreenshotLabel);
            this.splitContainer1.Panel2.Controls.Add(this.oldScreenshotPictureBox);
            this.splitContainer1.Size = new System.Drawing.Size(637, 226);
            this.splitContainer1.SplitterDistance = 308;
            this.splitContainer1.SplitterWidth = 1;
            this.splitContainer1.TabIndex = 2;
            // 
            // newScreenshotLabel
            // 
            this.newScreenshotLabel.AutoSize = true;
            this.newScreenshotLabel.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.newScreenshotLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F);
            this.newScreenshotLabel.ForeColor = System.Drawing.SystemColors.Window;
            this.newScreenshotLabel.Location = new System.Drawing.Point(13, 13);
            this.newScreenshotLabel.Name = "newScreenshotLabel";
            this.newScreenshotLabel.Size = new System.Drawing.Size(221, 32);
            this.newScreenshotLabel.TabIndex = 1;
            this.newScreenshotLabel.Text = "New Screenshot";
            // 
            // oldScreenshotLabel
            // 
            this.oldScreenshotLabel.AutoSize = true;
            this.oldScreenshotLabel.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.oldScreenshotLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F);
            this.oldScreenshotLabel.ForeColor = System.Drawing.SystemColors.Window;
            this.oldScreenshotLabel.Location = new System.Drawing.Point(13, 13);
            this.oldScreenshotLabel.Name = "oldScreenshotLabel";
            this.oldScreenshotLabel.Size = new System.Drawing.Size(210, 32);
            this.oldScreenshotLabel.TabIndex = 2;
            this.oldScreenshotLabel.Text = "Old Screenshot";
            // 
            // ScreenshotPreviewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(637, 226);
            this.Controls.Add(this.splitContainer1);
            this.Name = "ScreenshotPreviewForm";
            this.Text = "ScreenshotPreviewForm";
            ((System.ComponentModel.ISupportInitialize)(this.newScreenshotPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.oldScreenshotPictureBox)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox newScreenshotPictureBox;
        private System.Windows.Forms.PictureBox oldScreenshotPictureBox;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Label newScreenshotLabel;
        private System.Windows.Forms.Label oldScreenshotLabel;
    }
}