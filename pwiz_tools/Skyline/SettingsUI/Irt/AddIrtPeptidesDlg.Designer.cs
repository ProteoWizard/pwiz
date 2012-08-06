namespace pwiz.Skyline.SettingsUI.Irt
{
    partial class AddIrtPeptidesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddIrtPeptidesDlg));
            this.listExisting = new System.Windows.Forms.ListBox();
            this.listOverwrite = new System.Windows.Forms.ListBox();
            this.labelExisting = new System.Windows.Forms.Label();
            this.labelChoice = new System.Windows.Forms.Label();
            this.radioSkip = new System.Windows.Forms.RadioButton();
            this.radioReplace = new System.Windows.Forms.RadioButton();
            this.radioAverage = new System.Windows.Forms.RadioButton();
            this.labelOverwrite = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.panelExisting = new System.Windows.Forms.Panel();
            this.panelOverwrite = new System.Windows.Forms.Panel();
            this.labelPeptidesAdded = new System.Windows.Forms.Label();
            this.labelRunsConverted = new System.Windows.Forms.Label();
            this.labelRunsFailed = new System.Windows.Forms.Label();
            this.panelKeep = new System.Windows.Forms.Panel();
            this.labelKeep = new System.Windows.Forms.Label();
            this.listKeep = new System.Windows.Forms.ListBox();
            this.panelExisting.SuspendLayout();
            this.panelOverwrite.SuspendLayout();
            this.panelKeep.SuspendLayout();
            this.SuspendLayout();
            // 
            // listExisting
            // 
            resources.ApplyResources(this.listExisting, "listExisting");
            this.listExisting.FormattingEnabled = true;
            this.listExisting.Name = "listExisting";
            // 
            // listOverwrite
            // 
            resources.ApplyResources(this.listOverwrite, "listOverwrite");
            this.listOverwrite.FormattingEnabled = true;
            this.listOverwrite.Name = "listOverwrite";
            // 
            // labelExisting
            // 
            resources.ApplyResources(this.labelExisting, "labelExisting");
            this.labelExisting.Name = "labelExisting";
            // 
            // labelChoice
            // 
            resources.ApplyResources(this.labelChoice, "labelChoice");
            this.labelChoice.Name = "labelChoice";
            // 
            // radioSkip
            // 
            resources.ApplyResources(this.radioSkip, "radioSkip");
            this.radioSkip.Checked = true;
            this.radioSkip.Name = "radioSkip";
            this.radioSkip.TabStop = true;
            this.radioSkip.UseVisualStyleBackColor = true;
            // 
            // radioReplace
            // 
            resources.ApplyResources(this.radioReplace, "radioReplace");
            this.radioReplace.Name = "radioReplace";
            this.radioReplace.UseVisualStyleBackColor = true;
            // 
            // radioAverage
            // 
            resources.ApplyResources(this.radioAverage, "radioAverage");
            this.radioAverage.Name = "radioAverage";
            this.radioAverage.UseVisualStyleBackColor = true;
            // 
            // labelOverwrite
            // 
            resources.ApplyResources(this.labelOverwrite, "labelOverwrite");
            this.labelOverwrite.Name = "labelOverwrite";
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
            // panelOverwrite
            // 
            resources.ApplyResources(this.panelOverwrite, "panelOverwrite");
            this.panelOverwrite.Controls.Add(this.labelOverwrite);
            this.panelOverwrite.Controls.Add(this.listOverwrite);
            this.panelOverwrite.Name = "panelOverwrite";
            // 
            // labelPeptidesAdded
            // 
            resources.ApplyResources(this.labelPeptidesAdded, "labelPeptidesAdded");
            this.labelPeptidesAdded.Name = "labelPeptidesAdded";
            // 
            // labelRunsConverted
            // 
            resources.ApplyResources(this.labelRunsConverted, "labelRunsConverted");
            this.labelRunsConverted.Name = "labelRunsConverted";
            // 
            // labelRunsFailed
            // 
            resources.ApplyResources(this.labelRunsFailed, "labelRunsFailed");
            this.labelRunsFailed.Name = "labelRunsFailed";
            // 
            // panelKeep
            // 
            resources.ApplyResources(this.panelKeep, "panelKeep");
            this.panelKeep.Controls.Add(this.labelKeep);
            this.panelKeep.Controls.Add(this.listKeep);
            this.panelKeep.Name = "panelKeep";
            // 
            // labelKeep
            // 
            resources.ApplyResources(this.labelKeep, "labelKeep");
            this.labelKeep.Name = "labelKeep";
            // 
            // listKeep
            // 
            resources.ApplyResources(this.listKeep, "listKeep");
            this.listKeep.FormattingEnabled = true;
            this.listKeep.Name = "listKeep";
            // 
            // AddIrtPeptidesDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.panelKeep);
            this.Controls.Add(this.labelRunsFailed);
            this.Controls.Add(this.labelRunsConverted);
            this.Controls.Add(this.labelPeptidesAdded);
            this.Controls.Add(this.panelOverwrite);
            this.Controls.Add(this.panelExisting);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AddIrtPeptidesDlg";
            this.ShowInTaskbar = false;
            this.panelExisting.ResumeLayout(false);
            this.panelExisting.PerformLayout();
            this.panelOverwrite.ResumeLayout(false);
            this.panelOverwrite.PerformLayout();
            this.panelKeep.ResumeLayout(false);
            this.panelKeep.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listExisting;
        private System.Windows.Forms.ListBox listOverwrite;
        private System.Windows.Forms.Label labelExisting;
        private System.Windows.Forms.Label labelChoice;
        private System.Windows.Forms.RadioButton radioSkip;
        private System.Windows.Forms.RadioButton radioReplace;
        private System.Windows.Forms.RadioButton radioAverage;
        private System.Windows.Forms.Label labelOverwrite;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Panel panelExisting;
        private System.Windows.Forms.Panel panelOverwrite;
        private System.Windows.Forms.Label labelPeptidesAdded;
        private System.Windows.Forms.Label labelRunsConverted;
        private System.Windows.Forms.Label labelRunsFailed;
        private System.Windows.Forms.Panel panelKeep;
        private System.Windows.Forms.Label labelKeep;
        private System.Windows.Forms.ListBox listKeep;
    }
}