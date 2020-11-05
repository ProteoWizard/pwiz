namespace pwiz.Skyline.Controls.Graphs
{
    partial class FileProgressControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FileProgressControl));
            this.labelPercent = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.labelFileName = new System.Windows.Forms.Label();
            this.labelStatus = new System.Windows.Forms.Label();
            this.btnRetry = new System.Windows.Forms.Button();
            this.warningIcon = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.warningIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // labelPercent
            // 
            resources.ApplyResources(this.labelPercent, "labelPercent");
            this.labelPercent.Name = "labelPercent";
            // 
            // progressBar
            // 
            resources.ApplyResources(this.progressBar, "progressBar");
            this.progressBar.Name = "progressBar";
            // 
            // labelFileName
            // 
            resources.ApplyResources(this.labelFileName, "labelFileName");
            this.labelFileName.AutoEllipsis = true;
            this.labelFileName.Name = "labelFileName";
            // 
            // labelStatus
            // 
            resources.ApplyResources(this.labelStatus, "labelStatus");
            this.labelStatus.AutoEllipsis = true;
            this.labelStatus.ForeColor = System.Drawing.SystemColors.WindowText;
            this.labelStatus.Name = "labelStatus";
            // 
            // btnRetry
            // 
            resources.ApplyResources(this.btnRetry, "btnRetry");
            this.btnRetry.Name = "btnRetry";
            this.btnRetry.UseVisualStyleBackColor = true;
            this.btnRetry.Click += new System.EventHandler(this.btnRetry_Click);
            // 
            // warningIcon
            // 
            this.warningIcon.Image = global::pwiz.Skyline.Properties.Resources.warning;
            resources.ApplyResources(this.warningIcon, "warningIcon");
            this.warningIcon.Name = "warningIcon";
            this.warningIcon.TabStop = false;
            // 
            // FileProgressControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Controls.Add(this.warningIcon);
            this.Controls.Add(this.btnRetry);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.labelPercent);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.labelFileName);
            this.Name = "FileProgressControl";
            ((System.ComponentModel.ISupportInitialize)(this.warningIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelPercent;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label labelFileName;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Button btnRetry;
        private System.Windows.Forms.PictureBox warningIcon;


    }
}
