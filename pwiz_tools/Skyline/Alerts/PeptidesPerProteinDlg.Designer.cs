namespace pwiz.Skyline.Alerts
{
    partial class PeptidesPerProteinDlg
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PeptidesPerProteinDlg));
            this.label1 = new System.Windows.Forms.Label();
            this.numMinPeptides = new System.Windows.Forms.NumericUpDown();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblNew = new System.Windows.Forms.Label();
            this.radioKeepAll = new System.Windows.Forms.RadioButton();
            this.radioKeepMinPeptides = new System.Windows.Forms.RadioButton();
            this.lblRemaining = new System.Windows.Forms.Label();
            this.lblRemainingHeader = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.cbRemoveRepeated = new System.Windows.Forms.CheckBox();
            this.cbRemoveDuplicate = new System.Windows.Forms.CheckBox();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.panelDuplicates = new System.Windows.Forms.Panel();
            this.lblEmptyProteins = new System.Windows.Forms.Label();
            this.panelRemaining = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.numMinPeptides)).BeginInit();
            this.panelDuplicates.SuspendLayout();
            this.panelRemaining.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // numMinPeptides
            // 
            resources.ApplyResources(this.numMinPeptides, "numMinPeptides");
            this.numMinPeptides.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.numMinPeptides.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numMinPeptides.Name = "numMinPeptides";
            this.numMinPeptides.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numMinPeptides.ValueChanged += new System.EventHandler(this.UpdateRemaining);
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // lblNew
            // 
            resources.ApplyResources(this.lblNew, "lblNew");
            this.lblNew.Name = "lblNew";
            // 
            // radioKeepAll
            // 
            resources.ApplyResources(this.radioKeepAll, "radioKeepAll");
            this.radioKeepAll.Name = "radioKeepAll";
            this.radioKeepAll.UseVisualStyleBackColor = true;
            this.radioKeepAll.CheckedChanged += new System.EventHandler(this.UpdateRemaining);
            // 
            // radioKeepMinPeptides
            // 
            resources.ApplyResources(this.radioKeepMinPeptides, "radioKeepMinPeptides");
            this.radioKeepMinPeptides.Checked = true;
            this.radioKeepMinPeptides.Name = "radioKeepMinPeptides";
            this.radioKeepMinPeptides.TabStop = true;
            this.radioKeepMinPeptides.UseVisualStyleBackColor = true;
            this.radioKeepMinPeptides.CheckedChanged += new System.EventHandler(this.UpdateRemaining);
            // 
            // lblRemaining
            // 
            resources.ApplyResources(this.lblRemaining, "lblRemaining");
            this.lblRemaining.Name = "lblRemaining";
            // 
            // lblRemainingHeader
            // 
            resources.ApplyResources(this.lblRemainingHeader, "lblRemainingHeader");
            this.lblRemainingHeader.Name = "lblRemainingHeader";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // cbRemoveRepeated
            // 
            resources.ApplyResources(this.cbRemoveRepeated, "cbRemoveRepeated");
            this.cbRemoveRepeated.Name = "cbRemoveRepeated";
            this.helpTip.SetToolTip(this.cbRemoveRepeated, resources.GetString("cbRemoveRepeated.ToolTip"));
            this.cbRemoveRepeated.UseVisualStyleBackColor = true;
            this.cbRemoveRepeated.CheckedChanged += new System.EventHandler(this.UpdateRemaining);
            // 
            // cbRemoveDuplicate
            // 
            resources.ApplyResources(this.cbRemoveDuplicate, "cbRemoveDuplicate");
            this.cbRemoveDuplicate.Name = "cbRemoveDuplicate";
            this.helpTip.SetToolTip(this.cbRemoveDuplicate, resources.GetString("cbRemoveDuplicate.ToolTip"));
            this.cbRemoveDuplicate.UseVisualStyleBackColor = true;
            this.cbRemoveDuplicate.CheckedChanged += new System.EventHandler(this.UpdateRemaining);
            // 
            // panelDuplicates
            // 
            this.panelDuplicates.Controls.Add(this.cbRemoveRepeated);
            this.panelDuplicates.Controls.Add(this.cbRemoveDuplicate);
            resources.ApplyResources(this.panelDuplicates, "panelDuplicates");
            this.panelDuplicates.Name = "panelDuplicates";
            // 
            // lblEmptyProteins
            // 
            resources.ApplyResources(this.lblEmptyProteins, "lblEmptyProteins");
            this.lblEmptyProteins.ForeColor = System.Drawing.Color.Red;
            this.lblEmptyProteins.Name = "lblEmptyProteins";
            // 
            // panelRemaining
            // 
            this.panelRemaining.Controls.Add(this.lblRemainingHeader);
            this.panelRemaining.Controls.Add(this.lblEmptyProteins);
            this.panelRemaining.Controls.Add(this.lblRemaining);
            resources.ApplyResources(this.panelRemaining, "panelRemaining");
            this.panelRemaining.Name = "panelRemaining";
            // 
            // PeptidesPerProteinDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.panelRemaining);
            this.Controls.Add(this.panelDuplicates);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.radioKeepMinPeptides);
            this.Controls.Add(this.radioKeepAll);
            this.Controls.Add(this.lblNew);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.numMinPeptides);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PeptidesPerProteinDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.numMinPeptides)).EndInit();
            this.panelDuplicates.ResumeLayout(false);
            this.panelDuplicates.PerformLayout();
            this.panelRemaining.ResumeLayout(false);
            this.panelRemaining.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numMinPeptides;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblNew;
        private System.Windows.Forms.RadioButton radioKeepAll;
        private System.Windows.Forms.RadioButton radioKeepMinPeptides;
        private System.Windows.Forms.Label lblRemaining;
        private System.Windows.Forms.Label lblRemainingHeader;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox cbRemoveRepeated;
        private System.Windows.Forms.CheckBox cbRemoveDuplicate;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.Panel panelDuplicates;
        private System.Windows.Forms.Label lblEmptyProteins;
        private System.Windows.Forms.Panel panelRemaining;
    }
}