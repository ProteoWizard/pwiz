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
            resources.ApplyResources(this.NotificationCloseButton, "NotificationCloseButton");
            this.NotificationCloseButton.FlatAppearance.BorderColor = System.Drawing.Color.Lavender;
            this.NotificationCloseButton.FlatAppearance.BorderSize = 0;
            this.NotificationCloseButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.CornflowerBlue;
            this.NotificationCloseButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.LightSteelBlue;
            this.NotificationCloseButton.ForeColor = System.Drawing.Color.Transparent;
            this.NotificationCloseButton.Name = "NotificationCloseButton";
            this.NotificationCloseButton.UseVisualStyleBackColor = false;
            this.NotificationCloseButton.Click += new System.EventHandler(this.NotificationCloseButton_Click);
            // 
            // TextPanel
            // 
            this.TextPanel.Controls.Add(this.NotificationMessage);
            this.TextPanel.Controls.Add(this.LibraryNameLabel);
            this.TextPanel.Controls.Add(this.ViewLibraryLink);
            resources.ApplyResources(this.TextPanel, "TextPanel");
            this.TextPanel.Name = "TextPanel";
            // 
            // NotificationMessage
            // 
            resources.ApplyResources(this.NotificationMessage, "NotificationMessage");
            this.NotificationMessage.ForeColor = System.Drawing.Color.Navy;
            this.NotificationMessage.Name = "NotificationMessage";
            // 
            // LibraryNameLabel
            // 
            resources.ApplyResources(this.LibraryNameLabel, "LibraryNameLabel");
            this.LibraryNameLabel.AutoEllipsis = true;
            this.LibraryNameLabel.ForeColor = System.Drawing.Color.Navy;
            this.LibraryNameLabel.Name = "LibraryNameLabel";
            // 
            // ViewLibraryLink
            // 
            resources.ApplyResources(this.ViewLibraryLink, "ViewLibraryLink");
            this.ViewLibraryLink.AutoEllipsis = true;
            this.ViewLibraryLink.Name = "ViewLibraryLink";
            this.ViewLibraryLink.TabStop = true;
            this.ViewLibraryLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.ViewLibraryLink_LinkClicked);
            // 
            // BuildLibraryNotification
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
            this.BackColor = System.Drawing.Color.Lavender;
            this.ControlBox = false;
            this.Controls.Add(this.NotificationCloseButton);
            this.Controls.Add(this.TextPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BuildLibraryNotification";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
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