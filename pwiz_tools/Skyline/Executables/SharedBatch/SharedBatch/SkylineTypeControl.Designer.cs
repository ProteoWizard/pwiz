namespace SharedBatch
{
    partial class SkylineTypeControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SkylineTypeControl));
            this.textSkylineInstallationPath = new System.Windows.Forms.TextBox();
            this.radioButtonSkylineDaily = new System.Windows.Forms.RadioButton();
            this.radioButtonSpecifySkylinePath = new System.Windows.Forms.RadioButton();
            this.radioButtonSkyline = new System.Windows.Forms.RadioButton();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textSkylineInstallationPath
            // 
            resources.ApplyResources(this.textSkylineInstallationPath, "textSkylineInstallationPath");
            this.textSkylineInstallationPath.Name = "textSkylineInstallationPath";
            // 
            // radioButtonSkylineDaily
            // 
            resources.ApplyResources(this.radioButtonSkylineDaily, "radioButtonSkylineDaily");
            this.radioButtonSkylineDaily.Name = "radioButtonSkylineDaily";
            this.radioButtonSkylineDaily.TabStop = true;
            this.radioButtonSkylineDaily.UseVisualStyleBackColor = true;
            // 
            // radioButtonSpecifySkylinePath
            // 
            resources.ApplyResources(this.radioButtonSpecifySkylinePath, "radioButtonSpecifySkylinePath");
            this.radioButtonSpecifySkylinePath.Name = "radioButtonSpecifySkylinePath";
            this.radioButtonSpecifySkylinePath.TabStop = true;
            this.radioButtonSpecifySkylinePath.UseVisualStyleBackColor = true;
            this.radioButtonSpecifySkylinePath.CheckedChanged += new System.EventHandler(this.RadioButtonChanged);
            // 
            // radioButtonSkyline
            // 
            resources.ApplyResources(this.radioButtonSkyline, "radioButtonSkyline");
            this.radioButtonSkyline.Name = "radioButtonSkyline";
            this.radioButtonSkyline.TabStop = true;
            this.radioButtonSkyline.UseVisualStyleBackColor = true;
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // SkylineTypeControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.textSkylineInstallationPath);
            this.Controls.Add(this.radioButtonSkylineDaily);
            this.Controls.Add(this.radioButtonSpecifySkylinePath);
            this.Controls.Add(this.radioButtonSkyline);
            this.Controls.Add(this.btnBrowse);
            this.Name = "SkylineTypeControl";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textSkylineInstallationPath;
        private System.Windows.Forms.RadioButton radioButtonSkylineDaily;
        private System.Windows.Forms.RadioButton radioButtonSpecifySkylinePath;
        private System.Windows.Forms.RadioButton radioButtonSkyline;
        private System.Windows.Forms.Button btnBrowse;
    }
}
