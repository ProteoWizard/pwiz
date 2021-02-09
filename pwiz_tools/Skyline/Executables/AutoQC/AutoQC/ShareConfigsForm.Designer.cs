namespace AutoQC
{
    partial class ShareConfigsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ShareConfigsForm));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textFileName = new System.Windows.Forms.TextBox();
            this.checkedSaveConfigs = new System.Windows.Forms.CheckedListBox();
            this.checkBoxSelectAll = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textFileName
            // 
            resources.ApplyResources(this.textFileName, "textFileName");
            this.textFileName.Name = "textFileName";
            // 
            // checkedSaveConfigs
            // 
            resources.ApplyResources(this.checkedSaveConfigs, "checkedSaveConfigs");
            this.checkedSaveConfigs.CheckOnClick = true;
            this.checkedSaveConfigs.FormattingEnabled = true;
            this.checkedSaveConfigs.Name = "checkedSaveConfigs";
            // 
            // checkBoxSelectAll
            // 
            resources.ApplyResources(this.checkBoxSelectAll, "checkBoxSelectAll");
            this.checkBoxSelectAll.Name = "checkBoxSelectAll";
            this.checkBoxSelectAll.UseVisualStyleBackColor = true;
            this.checkBoxSelectAll.Click += new System.EventHandler(this.checkBoxSelectAll_Click);
            // 
            // ShareConfigsForm
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textFileName);
            this.Controls.Add(this.checkedSaveConfigs);
            this.Controls.Add(this.checkBoxSelectAll);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShareConfigsForm";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textFileName;
        private System.Windows.Forms.CheckedListBox checkedSaveConfigs;
        private System.Windows.Forms.CheckBox checkBoxSelectAll;
    }
}