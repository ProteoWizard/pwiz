﻿namespace pwiz.Skyline.SettingsUI
{
    partial class EditFragmentLossDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditFragmentLossDlg));
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tbxCharge = new System.Windows.Forms.TextBox();
            this.lblCharge = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.comboIncludeLoss = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.SuspendLayout();
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
            // tbxCharge
            // 
            resources.ApplyResources(this.tbxCharge, "tbxCharge");
            this.tbxCharge.Name = "tbxCharge";
            // 
            // lblCharge
            // 
            resources.ApplyResources(this.lblCharge, "lblCharge");
            this.lblCharge.Name = "lblCharge";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // comboIncludeLoss
            // 
            this.comboIncludeLoss.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIncludeLoss.FormattingEnabled = true;
            resources.ApplyResources(this.comboIncludeLoss, "comboIncludeLoss");
            this.comboIncludeLoss.Name = "comboIncludeLoss";
            // 
            // EditFragmentLossDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.tbxCharge);
            this.Controls.Add(this.lblCharge);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboIncludeLoss);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditFragmentLossDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboIncludeLoss;
        private System.Windows.Forms.Label lblCharge;
        private System.Windows.Forms.TextBox tbxCharge;
    }
}