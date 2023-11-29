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
            this.progressSplitContainer = new System.Windows.Forms.SplitContainer();
            this.panelTextBoxBorder.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.progressSplitContainer)).BeginInit();
            this.progressSplitContainer.Panel2.SuspendLayout();
            this.progressSplitContainer.SuspendLayout();
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
            this.panelTextBoxBorder.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panelTextBoxBorder.Controls.Add(this.txtSearchProgress);
            resources.ApplyResources(this.panelTextBoxBorder, "panelTextBoxBorder");
            this.panelTextBoxBorder.Name = "panelTextBoxBorder";
            // 
            // progressSplitContainer
            // 
            resources.ApplyResources(this.progressSplitContainer, "progressSplitContainer");
            this.progressSplitContainer.Name = "progressSplitContainer";
            this.progressSplitContainer.Panel1Collapsed = true;
            // 
            // progressSplitContainer.Panel2
            // 
            this.progressSplitContainer.Panel2.Controls.Add(this.panelTextBoxBorder);
            // 
            // SearchControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.progressSplitContainer);
            this.Controls.Add(this.showTimestampsCheckbox);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblProgress);
            this.Controls.Add(this.btnCancel);
            this.DoubleBuffered = true;
            this.Name = "SearchControl";
            this.panelTextBoxBorder.ResumeLayout(false);
            this.panelTextBoxBorder.PerformLayout();
            this.progressSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.progressSplitContainer)).EndInit();
            this.progressSplitContainer.ResumeLayout(false);
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
        protected System.Windows.Forms.SplitContainer progressSplitContainer;
    }
}
