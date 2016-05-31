namespace pwiz.Skyline.EditUI
{
    partial class AssociateProteinsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AssociateProteinsDlg));
            this.btnBackgroundProteome = new System.Windows.Forms.Button();
            this.btnUseFasta = new System.Windows.Forms.Button();
            this.checkBoxListMatches = new System.Windows.Forms.CheckedListBox();
            this.btnApplyChanges = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblDescription = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnBackgroundProteome
            // 
            resources.ApplyResources(this.btnBackgroundProteome, "btnBackgroundProteome");
            this.btnBackgroundProteome.Name = "btnBackgroundProteome";
            this.btnBackgroundProteome.UseVisualStyleBackColor = true;
            this.btnBackgroundProteome.Click += new System.EventHandler(this.btnBackgroundProteomeClick);
            // 
            // btnUseFasta
            // 
            resources.ApplyResources(this.btnUseFasta, "btnUseFasta");
            this.btnUseFasta.Name = "btnUseFasta";
            this.btnUseFasta.UseVisualStyleBackColor = true;
            this.btnUseFasta.Click += new System.EventHandler(this.btnUseFasta_Click);
            // 
            // checkBoxListMatches
            // 
            resources.ApplyResources(this.checkBoxListMatches, "checkBoxListMatches");
            this.checkBoxListMatches.FormattingEnabled = true;
            this.checkBoxListMatches.Name = "checkBoxListMatches";
            this.checkBoxListMatches.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.checkBoxListMatches_ItemCheck);
            // 
            // btnApplyChanges
            // 
            resources.ApplyResources(this.btnApplyChanges, "btnApplyChanges");
            this.btnApplyChanges.Name = "btnApplyChanges";
            this.btnApplyChanges.UseVisualStyleBackColor = true;
            this.btnApplyChanges.Click += new System.EventHandler(this.btnApplyChanges_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // lblDescription
            // 
            resources.ApplyResources(this.lblDescription, "lblDescription");
            this.lblDescription.Name = "lblDescription";
            // 
            // AssociateProteinsDlg
            // 
            this.AcceptButton = this.btnApplyChanges;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnApplyChanges);
            this.Controls.Add(this.checkBoxListMatches);
            this.Controls.Add(this.btnUseFasta);
            this.Controls.Add(this.btnBackgroundProteome);
            this.Name = "AssociateProteinsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnBackgroundProteome;
        private System.Windows.Forms.Button btnUseFasta;
        private System.Windows.Forms.CheckedListBox checkBoxListMatches;
        private System.Windows.Forms.Button btnApplyChanges;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblDescription;
    }
}