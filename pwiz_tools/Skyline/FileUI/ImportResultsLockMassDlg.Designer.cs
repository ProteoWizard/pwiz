using System.Drawing;

namespace pwiz.Skyline.FileUI
{
    partial class ImportResultsLockMassDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportResultsLockMassDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textLockmassPositive = new System.Windows.Forms.TextBox();
            this.labelPositive = new System.Windows.Forms.Label();
            this.labelMzPos = new System.Windows.Forms.Label();
            this.labelMzNegative = new System.Windows.Forms.Label();
            this.textLockmassNegative = new System.Windows.Forms.TextBox();
            this.labelNegative = new System.Windows.Forms.Label();
            this.labelLockMassInstructions = new System.Windows.Forms.Label();
            this.labelTolerance = new System.Windows.Forms.Label();
            this.labelDaTolerance = new System.Windows.Forms.Label();
            this.textLockmassTolerance = new System.Windows.Forms.TextBox();
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
            // textLockmassPositive
            // 
            resources.ApplyResources(this.textLockmassPositive, "textLockmassPositive");
            this.textLockmassPositive.Name = "textLockmassPositive";
            // 
            // labelPositive
            // 
            resources.ApplyResources(this.labelPositive, "labelPositive");
            this.labelPositive.Name = "labelPositive";
            // 
            // labelMzPos
            // 
            resources.ApplyResources(this.labelMzPos, "labelMzPos");
            this.labelMzPos.Name = "labelMzPos";
            // 
            // labelMzNegative
            // 
            resources.ApplyResources(this.labelMzNegative, "labelMzNegative");
            this.labelMzNegative.Name = "labelMzNegative";
            // 
            // textLockmassNegative
            // 
            resources.ApplyResources(this.textLockmassNegative, "textLockmassNegative");
            this.textLockmassNegative.Name = "textLockmassNegative";
            // 
            // labelNegative
            // 
            resources.ApplyResources(this.labelNegative, "labelNegative");
            this.labelNegative.Name = "labelNegative";
            // 
            // labelLockMassInstructions
            // 
            resources.ApplyResources(this.labelLockMassInstructions, "labelLockMassInstructions");
            this.labelLockMassInstructions.Name = "labelLockMassInstructions";
            // 
            // labelTolerance
            // 
            resources.ApplyResources(this.labelTolerance, "labelTolerance");
            this.labelTolerance.Name = "labelTolerance";
            // 
            // labelDaTolerance
            // 
            resources.ApplyResources(this.labelDaTolerance, "labelDaTolerance");
            this.labelDaTolerance.Name = "labelDaTolerance";
            // 
            // textLockmassTolerance
            // 
            resources.ApplyResources(this.textLockmassTolerance, "textLockmassTolerance");
            this.textLockmassTolerance.Name = "textLockmassTolerance";
            // 
            // ImportResultsLockMassDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.labelDaTolerance);
            this.Controls.Add(this.textLockmassTolerance);
            this.Controls.Add(this.labelTolerance);
            this.Controls.Add(this.labelLockMassInstructions);
            this.Controls.Add(this.labelMzNegative);
            this.Controls.Add(this.textLockmassNegative);
            this.Controls.Add(this.labelNegative);
            this.Controls.Add(this.labelMzPos);
            this.Controls.Add(this.textLockmassPositive);
            this.Controls.Add(this.labelPositive);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportResultsLockMassDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textLockmassPositive;
        private System.Windows.Forms.Label labelPositive;
        private System.Windows.Forms.Label labelMzPos;
        private System.Windows.Forms.Label labelMzNegative;
        private System.Windows.Forms.TextBox textLockmassNegative;
        private System.Windows.Forms.Label labelNegative;
        private System.Windows.Forms.Label labelLockMassInstructions;
        private System.Windows.Forms.Label labelTolerance;
        private System.Windows.Forms.Label labelDaTolerance;
        private System.Windows.Forms.TextBox textLockmassTolerance;
    }
}