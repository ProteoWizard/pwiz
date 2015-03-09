namespace pwiz.Skyline.SettingsUI.Optimization
{
    partial class AddOptimizationsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddOptimizationsDlg));
            this.labelOptimizationsAdded = new System.Windows.Forms.Label();
            this.panelExisting = new System.Windows.Forms.Panel();
            this.radioAverage = new System.Windows.Forms.RadioButton();
            this.radioReplace = new System.Windows.Forms.RadioButton();
            this.radioSkip = new System.Windows.Forms.RadioButton();
            this.labelChoice = new System.Windows.Forms.Label();
            this.labelExisting = new System.Windows.Forms.Label();
            this.listExisting = new System.Windows.Forms.ListBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.panelExisting.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelOptimizationsAdded
            // 
            resources.ApplyResources(this.labelOptimizationsAdded, "labelOptimizationsAdded");
            this.labelOptimizationsAdded.Name = "labelOptimizationsAdded";
            // 
            // panelExisting
            // 
            resources.ApplyResources(this.panelExisting, "panelExisting");
            this.panelExisting.Controls.Add(this.radioAverage);
            this.panelExisting.Controls.Add(this.radioReplace);
            this.panelExisting.Controls.Add(this.radioSkip);
            this.panelExisting.Controls.Add(this.labelChoice);
            this.panelExisting.Controls.Add(this.labelExisting);
            this.panelExisting.Controls.Add(this.listExisting);
            this.panelExisting.Name = "panelExisting";
            // 
            // radioAverage
            // 
            resources.ApplyResources(this.radioAverage, "radioAverage");
            this.radioAverage.Name = "radioAverage";
            this.radioAverage.UseVisualStyleBackColor = true;
            // 
            // radioReplace
            // 
            resources.ApplyResources(this.radioReplace, "radioReplace");
            this.radioReplace.Name = "radioReplace";
            this.radioReplace.UseVisualStyleBackColor = true;
            // 
            // radioSkip
            // 
            resources.ApplyResources(this.radioSkip, "radioSkip");
            this.radioSkip.Checked = true;
            this.radioSkip.Name = "radioSkip";
            this.radioSkip.TabStop = true;
            this.radioSkip.UseVisualStyleBackColor = true;
            // 
            // labelChoice
            // 
            resources.ApplyResources(this.labelChoice, "labelChoice");
            this.labelChoice.Name = "labelChoice";
            // 
            // labelExisting
            // 
            resources.ApplyResources(this.labelExisting, "labelExisting");
            this.labelExisting.Name = "labelExisting";
            // 
            // listExisting
            // 
            resources.ApplyResources(this.listExisting, "listExisting");
            this.listExisting.FormattingEnabled = true;
            this.listExisting.Name = "listExisting";
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
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // AddOptimizationsDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.labelOptimizationsAdded);
            this.Controls.Add(this.panelExisting);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AddOptimizationsDlg";
            this.ShowInTaskbar = false;
            this.panelExisting.ResumeLayout(false);
            this.panelExisting.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelOptimizationsAdded;
        private System.Windows.Forms.Panel panelExisting;
        private System.Windows.Forms.RadioButton radioAverage;
        private System.Windows.Forms.RadioButton radioReplace;
        private System.Windows.Forms.RadioButton radioSkip;
        private System.Windows.Forms.Label labelChoice;
        private System.Windows.Forms.Label labelExisting;
        private System.Windows.Forms.ListBox listExisting;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;

    }
}