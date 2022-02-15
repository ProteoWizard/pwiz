﻿namespace SharedBatch
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
            this.checkBoxSelectAll = new System.Windows.Forms.CheckBox();
            this.checkedSaveConfigs = new System.Windows.Forms.CheckedListBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // checkBoxSelectAll
            // 
            resources.ApplyResources(this.checkBoxSelectAll, "checkBoxSelectAll");
            this.checkBoxSelectAll.Name = "checkBoxSelectAll";
            this.checkBoxSelectAll.UseVisualStyleBackColor = true;
            this.checkBoxSelectAll.Click += new System.EventHandler(this.checkBoxSelectAll_Click);
            // 
            // checkedSaveConfigs
            // 
            resources.ApplyResources(this.checkedSaveConfigs, "checkedSaveConfigs");
            this.checkedSaveConfigs.CheckOnClick = true;
            this.checkedSaveConfigs.FormattingEnabled = true;
            this.checkedSaveConfigs.Name = "checkedSaveConfigs";
            this.checkedSaveConfigs.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.checkedSaveConfigs_ItemCheck);
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
            // ShareConfigsForm
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
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

        private System.Windows.Forms.CheckBox checkBoxSelectAll;
        private System.Windows.Forms.CheckedListBox checkedSaveConfigs;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
    }
}