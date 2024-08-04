namespace pwiz.Skyline.ToolsUI
{
    partial class PythonInstaller
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonInstaller));
            this.btnInstall = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelMessage = new System.Windows.Forms.Label();
            this.clboxPackages = new System.Windows.Forms.CheckedListBox();
            this.tabs = new pwiz.Skyline.Controls.WizardPages();
            this.tabStandard = new System.Windows.Forms.TabPage();
            this.tabVirtualEnvironment = new System.Windows.Forms.TabPage();
            this.textBoxVirtualEnvironment = new System.Windows.Forms.RichTextBox();
            this.labelVirtualEnvironment = new System.Windows.Forms.Label();
            this.tabs.SuspendLayout();
            this.tabStandard.SuspendLayout();
            this.tabVirtualEnvironment.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnInstall
            // 
            resources.ApplyResources(this.btnInstall, "btnInstall");
            this.btnInstall.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnInstall.Name = "btnInstall";
            this.btnInstall.UseVisualStyleBackColor = true;
            this.btnInstall.Click += new System.EventHandler(this.btnInstall_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // labelMessage
            // 
            resources.ApplyResources(this.labelMessage, "labelMessage");
            this.labelMessage.Name = "labelMessage";
            // 
            // clboxPackages
            // 
            this.clboxPackages.CheckOnClick = true;
            resources.ApplyResources(this.clboxPackages, "clboxPackages");
            this.clboxPackages.FormattingEnabled = true;
            this.clboxPackages.Name = "clboxPackages";
            // 
            // tabs
            // 
            resources.ApplyResources(this.tabs, "tabs");
            this.tabs.Controls.Add(this.tabStandard);
            this.tabs.Controls.Add(this.tabVirtualEnvironment);
            this.tabs.Name = "tabs";
            this.tabs.SelectedIndex = 0;
            // 
            // tabStandard
            // 
            this.tabStandard.BackColor = System.Drawing.SystemColors.Control;
            this.tabStandard.Controls.Add(this.clboxPackages);
            this.tabStandard.Controls.Add(this.labelMessage);
            resources.ApplyResources(this.tabStandard, "tabStandard");
            this.tabStandard.Name = "tabStandard";
            // 
            // tabVirtualEnvironment
            // 
            this.tabVirtualEnvironment.BackColor = System.Drawing.SystemColors.Control;
            this.tabVirtualEnvironment.Controls.Add(this.textBoxVirtualEnvironment);
            this.tabVirtualEnvironment.Controls.Add(this.labelVirtualEnvironment);
            resources.ApplyResources(this.tabVirtualEnvironment, "tabVirtualEnvironment");
            this.tabVirtualEnvironment.Name = "tabVirtualEnvironment";
            // 
            // textBoxVirtualEnvironment
            // 
            this.textBoxVirtualEnvironment.BackColor = System.Drawing.SystemColors.Control;
            this.textBoxVirtualEnvironment.BorderStyle = System.Windows.Forms.BorderStyle.None;
            resources.ApplyResources(this.textBoxVirtualEnvironment, "textBoxVirtualEnvironment");
            this.textBoxVirtualEnvironment.Name = "textBoxVirtualEnvironment";
            // 
            // labelVirtualEnvironment
            // 
            resources.ApplyResources(this.labelVirtualEnvironment, "labelVirtualEnvironment");
            this.labelVirtualEnvironment.Name = "labelVirtualEnvironment";
            // 
            // PythonInstaller
            // 
            this.AcceptButton = this.btnInstall;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.tabs);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnInstall);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PythonInstaller";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.PythonInstaller_Load);
            this.tabs.ResumeLayout(false);
            this.tabStandard.ResumeLayout(false);
            this.tabVirtualEnvironment.ResumeLayout(false);
            this.tabVirtualEnvironment.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnInstall;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label labelMessage;
        private System.Windows.Forms.CheckedListBox clboxPackages;
        private Controls.WizardPages tabs;
        private System.Windows.Forms.TabPage tabStandard;
        private System.Windows.Forms.TabPage tabVirtualEnvironment;
        private System.Windows.Forms.Label labelVirtualEnvironment;
        private System.Windows.Forms.RichTextBox textBoxVirtualEnvironment;
    }
}