namespace pwiz.Skyline.Alerts
{
    partial class DetailedReportErrorDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DetailedReportErrorDlg));
            this.lblReportError = new System.Windows.Forms.Label();
            this.textBoxEmail = new System.Windows.Forms.TextBox();
            this.lblEmail = new System.Windows.Forms.Label();
            this.lblCommentBox = new System.Windows.Forms.Label();
            this.textBoxMsg = new System.Windows.Forms.RichTextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.checkBoxSkyFile = new System.Windows.Forms.CheckBox();
            this.checkBoxScreenShot = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnOkAnon = new System.Windows.Forms.Button();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // lblReportError
            // 
            resources.ApplyResources(this.lblReportError, "lblReportError");
            this.lblReportError.Name = "lblReportError";
            // 
            // textBoxEmail
            // 
            resources.ApplyResources(this.textBoxEmail, "textBoxEmail");
            this.textBoxEmail.Name = "textBoxEmail";
            // 
            // lblEmail
            // 
            resources.ApplyResources(this.lblEmail, "lblEmail");
            this.lblEmail.Name = "lblEmail";
            // 
            // lblCommentBox
            // 
            resources.ApplyResources(this.lblCommentBox, "lblCommentBox");
            this.lblCommentBox.Name = "lblCommentBox";
            // 
            // textBoxMsg
            // 
            this.textBoxMsg.AcceptsTab = true;
            resources.ApplyResources(this.textBoxMsg, "textBoxMsg");
            this.textBoxMsg.Name = "textBoxMsg";
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // checkBoxSkyFile
            // 
            resources.ApplyResources(this.checkBoxSkyFile, "checkBoxSkyFile");
            this.checkBoxSkyFile.Name = "checkBoxSkyFile";
            this.checkBoxSkyFile.UseVisualStyleBackColor = true;
            // 
            // checkBoxScreenShot
            // 
            resources.ApplyResources(this.checkBoxScreenShot, "checkBoxScreenShot");
            this.checkBoxScreenShot.Name = "checkBoxScreenShot";
            this.checkBoxScreenShot.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // btnOkAnon
            // 
            resources.ApplyResources(this.btnOkAnon, "btnOkAnon");
            this.btnOkAnon.Name = "btnOkAnon";
            this.btnOkAnon.UseVisualStyleBackColor = true;
            this.btnOkAnon.Click += new System.EventHandler(this.btnOkAnon_Click);
            // 
            // pictureBox1
            // 
            resources.ApplyResources(this.pictureBox1, "pictureBox1");
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.TabStop = false;
            // 
            // DetailedReportErrorDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.btnOkAnon);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.checkBoxScreenShot);
            this.Controls.Add(this.checkBoxSkyFile);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.textBoxEmail);
            this.Controls.Add(this.lblEmail);
            this.Controls.Add(this.lblCommentBox);
            this.Controls.Add(this.textBoxMsg);
            this.Controls.Add(this.lblReportError);
            this.Icon = global::pwiz.Skyline.Properties.Resources.Skyline;
            this.MinimizeBox = false;
            this.Name = "DetailedReportErrorDlg";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SkippedReportErrorDlg_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblReportError;
        private System.Windows.Forms.TextBox textBoxEmail;
        private System.Windows.Forms.Label lblEmail;
        private System.Windows.Forms.Label lblCommentBox;
        private System.Windows.Forms.RichTextBox textBoxMsg;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.CheckBox checkBoxSkyFile;
        private System.Windows.Forms.CheckBox checkBoxScreenShot;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnOkAnon;
        private System.Windows.Forms.PictureBox pictureBox1;

    }
}