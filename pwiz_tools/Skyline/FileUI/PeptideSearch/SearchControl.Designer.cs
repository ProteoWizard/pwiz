using pwiz.Common.Controls;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class SearchControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SearchControl));
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblProgress = new System.Windows.Forms.Label();
            this.progressBar = new pwiz.Common.Controls.CustomTextProgressBar();
            this.showTimestampsCheckbox = new System.Windows.Forms.CheckBox();
            this.txtSearchProgress = new pwiz.Common.Controls.AutoScrollTextBox();
            this.panelTextBoxBorder = new System.Windows.Forms.Panel();
            this.panelTextBoxBorder.SuspendLayout();
            this.SuspendLayout();
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
            this.progressBar.CustomText = null;
            this.progressBar.DisplayStyle = pwiz.Common.Controls.ProgressBarDisplayText.Percentage;
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
            // txtSearchProgress
            // 
            this.txtSearchProgress.AcceptsReturn = true;
            this.txtSearchProgress.BorderStyle = System.Windows.Forms.BorderStyle.None;
            resources.ApplyResources(this.txtSearchProgress, "txtSearchProgress");
            this.txtSearchProgress.Name = "txtSearchProgress";
            this.txtSearchProgress.ReadOnly = true;
            // 
            // panelTextBoxBorder
            // 
            resources.ApplyResources(this.panelTextBoxBorder, "panelTextBoxBorder");
            this.panelTextBoxBorder.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panelTextBoxBorder.Controls.Add(this.txtSearchProgress);
            this.panelTextBoxBorder.Name = "panelTextBoxBorder";
            // 
            // SearchControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.showTimestampsCheckbox);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblProgress);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.panelTextBoxBorder);
            this.DoubleBuffered = true;
            this.Name = "SearchControl";
            this.panelTextBoxBorder.ResumeLayout(false);
            this.panelTextBoxBorder.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label lblProgress;
        private System.Windows.Forms.CheckBox showTimestampsCheckbox;
        protected System.Windows.Forms.Button btnCancel;
        protected CustomTextProgressBar progressBar;
        protected AutoScrollTextBox txtSearchProgress;
        private System.Windows.Forms.Panel panelTextBoxBorder;
    }
}
