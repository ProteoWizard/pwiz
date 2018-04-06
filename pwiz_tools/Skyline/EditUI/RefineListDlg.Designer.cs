﻿namespace pwiz.Skyline.EditUI
{
    partial class RefineListDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RefineListDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textPeptides = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.cbRemoveProteins = new System.Windows.Forms.CheckBox();
            this.cbMatchModified = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
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
            // textPeptides
            // 
            resources.ApplyResources(this.textPeptides, "textPeptides");
            this.textPeptides.Name = "textPeptides";
            this.textPeptides.Enter += new System.EventHandler(this.textPeptides_Enter);
            this.textPeptides.Leave += new System.EventHandler(this.textPeptides_Leave);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // cbRemoveProteins
            // 
            resources.ApplyResources(this.cbRemoveProteins, "cbRemoveProteins");
            this.cbRemoveProteins.Name = "cbRemoveProteins";
            this.cbRemoveProteins.UseVisualStyleBackColor = true;
            // 
            // cbMatchModified
            // 
            resources.ApplyResources(this.cbMatchModified, "cbMatchModified");
            this.cbMatchModified.Name = "cbMatchModified";
            this.cbMatchModified.UseVisualStyleBackColor = true;
            // 
            // RefineListDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.cbMatchModified);
            this.Controls.Add(this.cbRemoveProteins);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textPeptides);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RefineListDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textPeptides;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox cbRemoveProteins;
        private System.Windows.Forms.CheckBox cbMatchModified;
    }
}