namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class DDASearchControl
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DDASearchControl));
            this.txtSearchProgress = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblProgress = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.showTimestampsCheckbox = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // txtSearchProgress
            // 
            this.txtSearchProgress.AcceptsReturn = true;
            resources.ApplyResources(this.txtSearchProgress, "txtSearchProgress");
            this.txtSearchProgress.Name = "txtSearchProgress";
            this.txtSearchProgress.ReadOnly = true;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // lblProgress
            // 
            resources.ApplyResources(this.lblProgress, "lblProgress");
            this.lblProgress.Name = "lblProgress";
            // 
            // progressBar
            // 
            resources.ApplyResources(this.progressBar, "progressBar");
            this.progressBar.Name = "progressBar";
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            // 
            // showTimestampsCheckbox
            // 
            resources.ApplyResources(this.showTimestampsCheckbox, "showTimestampsCheckbox");
            this.showTimestampsCheckbox.Name = "showTimestampsCheckbox";
            this.showTimestampsCheckbox.UseVisualStyleBackColor = true;
            this.showTimestampsCheckbox.CheckedChanged += new System.EventHandler(this.showTimestampsCheckbox_CheckedChanged);
            // 
            // DDASearchControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.showTimestampsCheckbox);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblProgress);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.txtSearchProgress);
            this.Name = "DDASearchControl";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtSearchProgress;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblProgress;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.CheckBox showTimestampsCheckbox;
    }
}
