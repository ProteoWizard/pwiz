namespace pwiz.Skyline.ToolsUI
{
    partial class ImportSettingsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportSettingsDlg));
            this.labelInstallations = new System.Windows.Forms.Label();
            this.listInstallations = new System.Windows.Forms.ListBox();
            this.cbUninstall = new System.Windows.Forms.CheckBox();
            this.cbTrackChanges = new System.Windows.Forms.CheckBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // labelInstallations
            //
            resources.ApplyResources(this.labelInstallations, "labelInstallations");
            this.labelInstallations.Name = "labelInstallations";
            //
            // listInstallations
            //
            resources.ApplyResources(this.listInstallations, "listInstallations");
            this.listInstallations.FormattingEnabled = true;
            this.listInstallations.Name = "listInstallations";
            this.listInstallations.SelectedIndexChanged += new System.EventHandler(this.listInstallations_SelectedIndexChanged);
            //
            // cbUninstall
            //
            resources.ApplyResources(this.cbUninstall, "cbUninstall");
            this.cbUninstall.Name = "cbUninstall";
            this.cbUninstall.UseVisualStyleBackColor = true;
            this.cbUninstall.CheckedChanged += new System.EventHandler(this.cbUninstall_CheckedChanged);
            //
            // cbTrackChanges
            //
            resources.ApplyResources(this.cbTrackChanges, "cbTrackChanges");
            this.cbTrackChanges.Name = "cbTrackChanges";
            this.cbTrackChanges.UseVisualStyleBackColor = true;
            //
            // btnOk
            //
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            //
            // btnCancel
            //
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // ImportSettingsDlg
            //
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.cbTrackChanges);
            this.Controls.Add(this.cbUninstall);
            this.Controls.Add(this.listInstallations);
            this.Controls.Add(this.labelInstallations);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportSettingsDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelInstallations;
        private System.Windows.Forms.ListBox listInstallations;
        private System.Windows.Forms.CheckBox cbUninstall;
        private System.Windows.Forms.CheckBox cbTrackChanges;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
