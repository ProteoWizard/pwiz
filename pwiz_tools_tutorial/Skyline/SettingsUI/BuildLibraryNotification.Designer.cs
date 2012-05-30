namespace pwiz.Skyline.SettingsUI
{
    partial class BuildLibraryNotification
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BuildLibraryNotification));
            this.NotificationCloseButton = new System.Windows.Forms.Button();
            this.TextPanel = new System.Windows.Forms.Panel();
            this.NotificationMessage = new System.Windows.Forms.Label();
            this.LibraryNameLabel = new System.Windows.Forms.Label();
            this.ViewLibraryLink = new System.Windows.Forms.LinkLabel();
            this.TextPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // NotificationCloseButton
            // 
            this.NotificationCloseButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("NotificationCloseButton.BackgroundImage")));
            this.NotificationCloseButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.NotificationCloseButton.FlatAppearance.BorderColor = System.Drawing.Color.Lavender;
            this.NotificationCloseButton.FlatAppearance.BorderSize = 0;
            this.NotificationCloseButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.CornflowerBlue;
            this.NotificationCloseButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.LightSteelBlue;
            this.NotificationCloseButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.NotificationCloseButton.ForeColor = System.Drawing.Color.Transparent;
            this.NotificationCloseButton.Location = new System.Drawing.Point(137, 3);
            this.NotificationCloseButton.Margin = new System.Windows.Forms.Padding(0);
            this.NotificationCloseButton.Name = "NotificationCloseButton";
            this.NotificationCloseButton.Size = new System.Drawing.Size(16, 16);
            this.NotificationCloseButton.TabIndex = 2;
            this.NotificationCloseButton.UseVisualStyleBackColor = false;
            this.NotificationCloseButton.Click += new System.EventHandler(this.NotificationCloseButton_Click);
            // 
            // TextPanel
            // 
            this.TextPanel.Controls.Add(this.NotificationMessage);
            this.TextPanel.Controls.Add(this.LibraryNameLabel);
            this.TextPanel.Controls.Add(this.ViewLibraryLink);
            this.TextPanel.Location = new System.Drawing.Point(24, 32);
            this.TextPanel.Name = "TextPanel";
            this.TextPanel.Size = new System.Drawing.Size(119, 53);
            this.TextPanel.TabIndex = 3;
            // 
            // NotificationMessage
            // 
            this.NotificationMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.NotificationMessage.AutoSize = true;
            this.NotificationMessage.ForeColor = System.Drawing.Color.Navy;
            this.NotificationMessage.Location = new System.Drawing.Point(0, 16);
            this.NotificationMessage.Name = "NotificationMessage";
            this.NotificationMessage.Size = new System.Drawing.Size(84, 13);
            this.NotificationMessage.TabIndex = 7;
            this.NotificationMessage.Text = "build completed.";
            // 
            // LibraryNameLabel
            // 
            this.LibraryNameLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.LibraryNameLabel.AutoEllipsis = true;
            this.LibraryNameLabel.ForeColor = System.Drawing.Color.Navy;
            this.LibraryNameLabel.Location = new System.Drawing.Point(0, 0);
            this.LibraryNameLabel.Name = "LibraryNameLabel";
            this.LibraryNameLabel.Size = new System.Drawing.Size(118, 16);
            this.LibraryNameLabel.TabIndex = 6;
            this.LibraryNameLabel.Text = "label1";
            // 
            // ViewLibraryLink
            // 
            this.ViewLibraryLink.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.ViewLibraryLink.AutoEllipsis = true;
            this.ViewLibraryLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ViewLibraryLink.Location = new System.Drawing.Point(0, 38);
            this.ViewLibraryLink.Name = "ViewLibraryLink";
            this.ViewLibraryLink.Size = new System.Drawing.Size(118, 16);
            this.ViewLibraryLink.TabIndex = 5;
            this.ViewLibraryLink.TabStop = true;
            this.ViewLibraryLink.Text = "Explore library...";
            this.ViewLibraryLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.ViewLibraryLink_LinkClicked);
            // 
            // BuildLibraryNotification
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
            this.BackColor = System.Drawing.Color.Lavender;
            this.ClientSize = new System.Drawing.Size(158, 106);
            this.ControlBox = false;
            this.Controls.Add(this.NotificationCloseButton);
            this.Controls.Add(this.TextPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BuildLibraryNotification";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.TopMost = true;
            this.TextPanel.ResumeLayout(false);
            this.TextPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button NotificationCloseButton;
        private System.Windows.Forms.Panel TextPanel;
        private System.Windows.Forms.Label NotificationMessage;
        private System.Windows.Forms.Label LibraryNameLabel;
        private System.Windows.Forms.LinkLabel ViewLibraryLink;
    }
}