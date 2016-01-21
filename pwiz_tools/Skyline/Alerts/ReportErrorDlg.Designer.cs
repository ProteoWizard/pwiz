namespace pwiz.Skyline.Alerts
{
    partial class ReportErrorDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ReportErrorDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblError = new System.Windows.Forms.Label();
            this.lblReportError = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tbSourceCodeLocation = new System.Windows.Forms.TextBox();
            this.btnClipboard = new System.Windows.Forms.Button();
            this.tbErrorDescription = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // lblError
            // 
            resources.ApplyResources(this.lblError, "lblError");
            this.lblError.Name = "lblError";
            // 
            // lblReportError
            // 
            resources.ApplyResources(this.lblReportError, "lblReportError");
            this.lblReportError.Name = "lblReportError";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // tbSourceCodeLocation
            // 
            resources.ApplyResources(this.tbSourceCodeLocation, "tbSourceCodeLocation");
            this.tbSourceCodeLocation.Name = "tbSourceCodeLocation";
            this.tbSourceCodeLocation.ReadOnly = true;
            // 
            // btnClipboard
            // 
            resources.ApplyResources(this.btnClipboard, "btnClipboard");
            this.btnClipboard.Name = "btnClipboard";
            this.btnClipboard.UseVisualStyleBackColor = true;
            this.btnClipboard.Click += new System.EventHandler(this.btnClipboard_Click);
            // 
            // tbErrorDescription
            // 
            resources.ApplyResources(this.tbErrorDescription, "tbErrorDescription");
            this.tbErrorDescription.Name = "tbErrorDescription";
            this.tbErrorDescription.ReadOnly = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // ReportErrorDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tbErrorDescription);
            this.Controls.Add(this.btnClipboard);
            this.Controls.Add(this.tbSourceCodeLocation);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblReportError);
            this.Controls.Add(this.lblError);
            this.Controls.Add(this.btnCancel);
            this.Icon = global::pwiz.Skyline.Properties.Resources.Skyline;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ReportErrorDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblError;
        private System.Windows.Forms.Label lblReportError;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbSourceCodeLocation;
        private System.Windows.Forms.Button btnClipboard;
        private System.Windows.Forms.TextBox tbErrorDescription;
        private System.Windows.Forms.Button btnOK;
    }
}