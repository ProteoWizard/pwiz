namespace pwiz.Skyline.Controls.Graphs
{
    partial class ImportResultsRetryCountdownDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportResultsRetryCountdownDlg));
            this.btnRetryNow = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblSeconds = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnRetryNow
            // 
            resources.ApplyResources(this.btnRetryNow, "btnRetryNow");
            this.btnRetryNow.Name = "btnRetryNow";
            this.btnRetryNow.UseVisualStyleBackColor = true;
            this.btnRetryNow.Click += new System.EventHandler(this.btnRetryNow_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // lblSeconds
            // 
            resources.ApplyResources(this.lblSeconds, "lblSeconds");
            this.lblSeconds.Name = "lblSeconds";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // ImportResultsRetryCountdownDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ControlBox = false;
            this.Controls.Add(this.btnRetryNow);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lblSeconds);
            this.Controls.Add(this.label1);
            this.Name = "ImportResultsRetryCountdownDlg";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblSeconds;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnRetryNow;
    }
}