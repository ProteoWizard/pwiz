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
            this.listExisting.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.listExisting.FormattingEnabled = true;
            this.listExisting.Location = new System.Drawing.Point(7, 19);
            this.listExisting.Name = "listExisting";
            this.listExisting.Size = new System.Drawing.Size(326, 108);
            this.listExisting.TabIndex = 1;
            // 
            // listOverwrite
            // 
            this.listOverwrite.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.listOverwrite.FormattingEnabled = true;
            this.listOverwrite.Location = new System.Drawing.Point(7, 32);
            this.listOverwrite.Name = "listOverwrite";
            this.listOverwrite.Size = new System.Drawing.Size(326, 82);
            this.listOverwrite.TabIndex = 1;
            // 
            // labelExisting
            // 
            this.labelExisting.AutoSize = true;
            this.labelExisting.Location = new System.Drawing.Point(4, 3);
            this.labelExisting.Name = "labelExisting";
            this.labelExisting.Size = new System.Drawing.Size(224, 13);
            this.labelExisting.TabIndex = 0;
            this.labelExisting.Text = "{0} peptides already have values in the library:";
            // 
            // labelChoice
            // 
            this.labelChoice.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelChoice.AutoSize = true;
            this.labelChoice.Location = new System.Drawing.Point(8, 136);
            this.labelChoice.Name = "labelChoice";
            this.labelChoice.Size = new System.Drawing.Size(258, 13);
            this.labelChoice.TabIndex = 2;
            this.labelChoice.Text = "Choose how you would like to handle these peptides:";
            // 
            // radioSkip
            // 
            this.radioSkip.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioSkip.AutoSize = true;
            this.radioSkip.Checked = true;
            this.radioSkip.Location = new System.Drawing.Point(11, 153);
            this.radioSkip.Name = "radioSkip";
            this.radioSkip.Size = new System.Drawing.Size(127, 17);
            this.radioSkip.TabIndex = 3;
            this.radioSkip.TabStop = true;
            this.radioSkip.Text = "Leave existing values";
            this.radioSkip.UseVisualStyleBackColor = true;
            // 
            // radioReplace
            // 
            this.radioReplace.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioReplace.AutoSize = true;
            this.radioReplace.Location = new System.Drawing.Point(11, 177);
            this.radioReplace.Name = "radioReplace";
            this.radioReplace.Size = new System.Drawing.Size(137, 17);
            this.radioReplace.TabIndex = 4;
            this.radioReplace.Text = "Replace existing values";
            this.radioReplace.UseVisualStyleBackColor = true;
            // 
            // radioAverage
            // 
            this.radioAverage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioAverage.AutoSize = true;
            this.radioAverage.Location = new System.Drawing.Point(11, 200);
            this.radioAverage.Name = "radioAverage";
            this.radioAverage.Size = new System.Drawing.Size(181, 17);
            this.radioAverage.TabIndex = 5;
            this.radioAverage.Text = "Average new and existing values";
            this.radioAverage.UseVisualStyleBackColor = true;
            // 
            // labelOverwrite
            // 
            this.labelOverwrite.AutoSize = true;
            this.labelOverwrite.Location = new System.Drawing.Point(4, 3);
            this.labelOverwrite.Name = "labelOverwrite";
            this.labelOverwrite.Size = new System.Drawing.Size(335, 26);
            this.labelOverwrite.TabIndex = 0;
            this.labelOverwrite.Text = "{0} peptide iRT values calculated from MS/MS scans will be replaced\r\nwith iRT val" +
                "ues calculated from chromatogram peaks:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(386, 44);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(386, 14);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // panelExisting
            // 
            this.panelExisting.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panelExisting.Controls.Add(this.radioAverage);
            this.panelExisting.Controls.Add(this.radioReplace);
            this.panelExisting.Controls.Add(this.radioSkip);
            this.panelExisting.Controls.Add(this.labelChoice);
            this.panelExisting.Controls.Add(this.labelExisting);
            this.panelExisting.Controls.Add(this.listExisting);
            this.panelExisting.Location = new System.Drawing.Point(5, 341);
            this.panelExisting.Name = "panelExisting";
            this.panelExisting.Size = new System.Drawing.Size(370, 224);
            this.panelExisting.TabIndex = 4;
            // 
            // panelOverwrite
            // 
            this.panelOverwrite.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panelOverwrite.Controls.Add(this.labelOverwrite);
            this.panelOverwrite.Controls.Add(this.listOverwrite);
            this.panelOverwrite.Location = new System.Drawing.Point(5, 75);
            this.panelOverwrite.Name = "panelOverwrite";
            this.panelOverwrite.Size = new System.Drawing.Size(370, 134);
            this.panelOverwrite.TabIndex = 3;
            // 
            // labelPeptidesAdded
            // 
            this.labelPeptidesAdded.AutoSize = true;
            this.labelPeptidesAdded.Location = new System.Drawing.Point(9, 14);
            this.labelPeptidesAdded.Name = "labelPeptidesAdded";
            this.labelPeptidesAdded.Size = new System.Drawing.Size(252, 13);
            this.labelPeptidesAdded.TabIndex = 0;
            this.labelPeptidesAdded.Text = "{0} new peptides will be added to the iRT database.";
            // 
            // labelRunsConverted
            // 
            this.labelRunsConverted.AutoSize = true;
            this.labelRunsConverted.Location = new System.Drawing.Point(9, 32);
            this.labelRunsConverted.Name = "labelRunsConverted";
            this.labelRunsConverted.Size = new System.Drawing.Size(184, 13);
            this.labelRunsConverted.TabIndex = 1;
            this.labelRunsConverted.Text = "{0} runs were successfully converted.";
            // 
            // labelRunsFailed
            // 
            this.labelRunsFailed.AutoSize = true;
            this.labelRunsFailed.Location = new System.Drawing.Point(9, 50);
            this.labelRunsFailed.Name = "labelRunsFailed";
            this.labelRunsFailed.Size = new System.Drawing.Size(280, 13);
            this.labelRunsFailed.TabIndex = 2;
            this.labelRunsFailed.Text = "{0} runs were not converted due to insufficient correlation.";
            // 
            // panelKeep
            // 
            this.panelKeep.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panelKeep.Controls.Add(this.labelKeep);
            this.panelKeep.Controls.Add(this.listKeep);
            this.panelKeep.Location = new System.Drawing.Point(5, 208);
            this.panelKeep.Name = "panelKeep";
            this.panelKeep.Size = new System.Drawing.Size(370, 134);
            this.panelKeep.TabIndex = 4;
            // 
            // labelKeep
            // 
            this.labelKeep.AutoSize = true;
            this.labelKeep.Location = new System.Drawing.Point(4, 3);
            this.labelKeep.Name = "labelKeep";
            this.labelKeep.Size = new System.Drawing.Size(346, 26);
            this.labelKeep.TabIndex = 0;
            this.labelKeep.Text = "{0} peptide iRT values calculated from chromatogram peaks will be kept\r\nand newer" +
                " iRT values calculated from MS/MS scans ignored:";
            // 
            // listKeep
            // 
            this.listKeep.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.listKeep.FormattingEnabled = true;
            this.listKeep.Location = new System.Drawing.Point(7, 32);
            this.listKeep.Name = "listKeep";
            this.listKeep.Size = new System.Drawing.Size(326, 82);
            this.listKeep.TabIndex = 1;
            // 
            // AddIrtPeptidesDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(473, 577);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Add Peptides";
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