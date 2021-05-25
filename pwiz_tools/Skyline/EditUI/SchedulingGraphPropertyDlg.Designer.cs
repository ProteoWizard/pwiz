﻿namespace pwiz.Skyline.EditUI
{
    partial class SchedulingGraphPropertyDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SchedulingGraphPropertyDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textTimeWindows = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textPrimaryTransitionCount = new System.Windows.Forms.TextBox();
            this.textBrukerTemplate = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnBrukerTemplateBrowse = new System.Windows.Forms.Button();
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
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textTimeWindows
            // 
            resources.ApplyResources(this.textTimeWindows, "textTimeWindows");
            this.textTimeWindows.Name = "textTimeWindows";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textPrimaryTransitionCount
            // 
            resources.ApplyResources(this.textPrimaryTransitionCount, "textPrimaryTransitionCount");
            this.textPrimaryTransitionCount.Name = "textPrimaryTransitionCount";
            // 
            // textBrukerTemplate
            // 
            resources.ApplyResources(this.textBrukerTemplate, "textBrukerTemplate");
            this.textBrukerTemplate.Name = "textBrukerTemplate";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // btnBrukerTemplateBrowse
            // 
            resources.ApplyResources(this.btnBrukerTemplateBrowse, "btnBrukerTemplateBrowse");
            this.btnBrukerTemplateBrowse.Name = "btnBrukerTemplateBrowse";
            this.btnBrukerTemplateBrowse.UseVisualStyleBackColor = true;
            this.btnBrukerTemplateBrowse.Click += new System.EventHandler(this.btnBrukerTemplateBrowse_Click);
            // 
            // SchedulingGraphPropertyDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnBrukerTemplateBrowse);
            this.Controls.Add(this.textBrukerTemplate);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textPrimaryTransitionCount);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textTimeWindows);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SchedulingGraphPropertyDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textTimeWindows;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textPrimaryTransitionCount;
        private System.Windows.Forms.TextBox textBrukerTemplate;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnBrukerTemplateBrowse;
    }
}