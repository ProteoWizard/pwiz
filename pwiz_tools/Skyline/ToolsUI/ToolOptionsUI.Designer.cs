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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ToolOptionsUI));
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
            resources.ApplyResources(this.PanaramaTabControl, "PanaramaTabControl");
            this.PanaramaTabControl.Controls.Add(this.tabPanorama);
            this.PanaramaTabControl.Name = "PanaramaTabControl";
            this.PanaramaTabControl.SelectedIndex = 0;
            // 
            // tabPanorama
            // 
            this.tabPanorama.Controls.Add(this.listboxServers);
            this.tabPanorama.Controls.Add(this.lblServers);
            this.tabPanorama.Controls.Add(this.btnEditServers);
            resources.ApplyResources(this.tabPanorama, "tabPanorama");
            this.tabPanorama.Name = "tabPanorama";
            this.tabPanorama.UseVisualStyleBackColor = true;
            // 
            // listboxServers
            // 
            resources.ApplyResources(this.listboxServers, "listboxServers");
            this.listboxServers.FormattingEnabled = true;
            this.listboxServers.Name = "listboxServers";
            this.listboxServers.SelectionMode = System.Windows.Forms.SelectionMode.None;
            // 
            // lblServers
            // 
            resources.ApplyResources(this.lblServers, "lblServers");
            this.lblServers.Name = "lblServers";
            // 
            // btnEditServers
            // 
            resources.ApplyResources(this.btnEditServers, "btnEditServers");
            this.btnEditServers.Name = "btnEditServers";
            this.btnEditServers.UseVisualStyleBackColor = true;
            this.btnEditServers.Click += new System.EventHandler(this.btnEditServers_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // ToolOptionsUI
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.PanaramaTabControl);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ToolOptionsUI";
            this.ShowInTaskbar = false;
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