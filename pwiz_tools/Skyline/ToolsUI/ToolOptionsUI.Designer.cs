namespace pwiz.Skyline.ToolsUI
{
    partial class ToolOptionsUI
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
            this.PanaramaTabControl = new System.Windows.Forms.TabControl();
            this.tabPanorama = new System.Windows.Forms.TabPage();
            this.listboxServers = new System.Windows.Forms.ListBox();
            this.lblServers = new System.Windows.Forms.Label();
            this.btnEditServers = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.PanaramaTabControl.SuspendLayout();
            this.tabPanorama.SuspendLayout();
            this.SuspendLayout();
            // 
            // PanaramaTabControl
            // 
            this.PanaramaTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.PanaramaTabControl.Controls.Add(this.tabPanorama);
            this.PanaramaTabControl.Location = new System.Drawing.Point(12, 12);
            this.PanaramaTabControl.Name = "PanaramaTabControl";
            this.PanaramaTabControl.SelectedIndex = 0;
            this.PanaramaTabControl.Size = new System.Drawing.Size(397, 274);
            this.PanaramaTabControl.TabIndex = 0;
            // 
            // tabPanorama
            // 
            this.tabPanorama.Controls.Add(this.listboxServers);
            this.tabPanorama.Controls.Add(this.lblServers);
            this.tabPanorama.Controls.Add(this.btnEditServers);
            this.tabPanorama.Location = new System.Drawing.Point(4, 22);
            this.tabPanorama.Name = "tabPanorama";
            this.tabPanorama.Padding = new System.Windows.Forms.Padding(3);
            this.tabPanorama.Size = new System.Drawing.Size(389, 248);
            this.tabPanorama.TabIndex = 0;
            this.tabPanorama.Text = "Panorama";
            this.tabPanorama.UseVisualStyleBackColor = true;
            // 
            // listboxServers
            // 
            this.listboxServers.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listboxServers.FormattingEnabled = true;
            this.listboxServers.Location = new System.Drawing.Point(9, 22);
            this.listboxServers.Name = "listboxServers";
            this.listboxServers.SelectionMode = System.Windows.Forms.SelectionMode.None;
            this.listboxServers.Size = new System.Drawing.Size(293, 199);
            this.listboxServers.TabIndex = 1;
            // 
            // lblServers
            // 
            this.lblServers.AutoSize = true;
            this.lblServers.Location = new System.Drawing.Point(6, 6);
            this.lblServers.Name = "lblServers";
            this.lblServers.Size = new System.Drawing.Size(46, 13);
            this.lblServers.TabIndex = 0;
            this.lblServers.Text = "Servers:";
            // 
            // btnEditServers
            // 
            this.btnEditServers.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnEditServers.Location = new System.Drawing.Point(308, 22);
            this.btnEditServers.Name = "btnEditServers";
            this.btnEditServers.Size = new System.Drawing.Size(75, 23);
            this.btnEditServers.TabIndex = 2;
            this.btnEditServers.Text = "E&dit list...";
            this.btnEditServers.UseVisualStyleBackColor = true;
            this.btnEditServers.Click += new System.EventHandler(this.btnEditServers_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(334, 292);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(253, 292);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // ToolOptionsUI
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(421, 320);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.PanaramaTabControl);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ToolOptionsUI";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Options";
            this.PanaramaTabControl.ResumeLayout(false);
            this.tabPanorama.ResumeLayout(false);
            this.tabPanorama.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl PanaramaTabControl;
        private System.Windows.Forms.TabPage tabPanorama;
        private System.Windows.Forms.Button btnEditServers;
        private System.Windows.Forms.Label lblServers;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.ListBox listboxServers;
    }
}