namespace SkylineBatch
{
    partial class RScriptForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RScriptForm));
            this.comboRVersions = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnAddRLocation = new System.Windows.Forms.Button();
            this.panelPath = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // comboRVersions
            // 
            this.comboRVersions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRVersions.FormattingEnabled = true;
            resources.ApplyResources(this.comboRVersions, "comboRVersions");
            this.comboRVersions.Name = "comboRVersions";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnAddRLocation
            // 
            resources.ApplyResources(this.btnAddRLocation, "btnAddRLocation");
            this.btnAddRLocation.Name = "btnAddRLocation";
            this.btnAddRLocation.UseVisualStyleBackColor = true;
            this.btnAddRLocation.Click += new System.EventHandler(this.btnAddRLocation_Click);
            // 
            // panelPath
            // 
            resources.ApplyResources(this.panelPath, "panelPath");
            this.panelPath.Name = "panelPath";
            // 
            // RScriptForm
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnAddRLocation);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.comboRVersions);
            this.Controls.Add(this.panelPath);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RScriptForm";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ComboBox comboRVersions;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnAddRLocation;
        private System.Windows.Forms.Panel panelPath;
    }
}