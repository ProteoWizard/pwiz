namespace pwiz.Skyline.Alerts
{
    sealed partial class FilterMatchedPeptidesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FilterMatchedPeptidesDlg));
            this.radioNoDuplicates = new System.Windows.Forms.RadioButton();
            this.radioFirstOccurence = new System.Windows.Forms.RadioButton();
            this.radioAddToAll = new System.Windows.Forms.RadioButton();
            this.radioFilterUnmatched = new System.Windows.Forms.RadioButton();
            this.radioAddUnmatched = new System.Windows.Forms.RadioButton();
            this.msgUnmatchedPeptides = new System.Windows.Forms.Label();
            this.msgDuplicatePeptides = new System.Windows.Forms.Label();
            this.msgFilteredPeptides = new System.Windows.Forms.Label();
            this.radioKeepFiltered = new System.Windows.Forms.RadioButton();
            this.radioDoNotAddFiltered = new System.Windows.Forms.RadioButton();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.panelMultiple = new System.Windows.Forms.Panel();
            this.panelUnmatched = new System.Windows.Forms.Panel();
            this.panelFiltered = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.panelMultiple.SuspendLayout();
            this.panelUnmatched.SuspendLayout();
            this.panelFiltered.SuspendLayout();
            this.SuspendLayout();
            // 
            // radioNoDuplicates
            // 
            resources.ApplyResources(this.radioNoDuplicates, "radioNoDuplicates");
            this.radioNoDuplicates.Checked = true;
            this.radioNoDuplicates.Name = "radioNoDuplicates";
            this.radioNoDuplicates.TabStop = true;
            this.radioNoDuplicates.UseVisualStyleBackColor = true;
            // 
            // radioFirstOccurence
            // 
            resources.ApplyResources(this.radioFirstOccurence, "radioFirstOccurence");
            this.radioFirstOccurence.Name = "radioFirstOccurence";
            this.radioFirstOccurence.UseVisualStyleBackColor = true;
            // 
            // radioAddToAll
            // 
            resources.ApplyResources(this.radioAddToAll, "radioAddToAll");
            this.radioAddToAll.Name = "radioAddToAll";
            this.radioAddToAll.UseVisualStyleBackColor = true;
            // 
            // radioFilterUnmatched
            // 
            resources.ApplyResources(this.radioFilterUnmatched, "radioFilterUnmatched");
            this.radioFilterUnmatched.Checked = true;
            this.radioFilterUnmatched.Name = "radioFilterUnmatched";
            this.radioFilterUnmatched.TabStop = true;
            this.radioFilterUnmatched.UseVisualStyleBackColor = true;
            // 
            // radioAddUnmatched
            // 
            resources.ApplyResources(this.radioAddUnmatched, "radioAddUnmatched");
            this.radioAddUnmatched.Name = "radioAddUnmatched";
            this.radioAddUnmatched.UseVisualStyleBackColor = true;
            // 
            // msgUnmatchedPeptides
            // 
            resources.ApplyResources(this.msgUnmatchedPeptides, "msgUnmatchedPeptides");
            this.msgUnmatchedPeptides.Name = "msgUnmatchedPeptides";
            // 
            // msgDuplicatePeptides
            // 
            resources.ApplyResources(this.msgDuplicatePeptides, "msgDuplicatePeptides");
            this.msgDuplicatePeptides.Name = "msgDuplicatePeptides";
            // 
            // msgFilteredPeptides
            // 
            resources.ApplyResources(this.msgFilteredPeptides, "msgFilteredPeptides");
            this.msgFilteredPeptides.Name = "msgFilteredPeptides";
            // 
            // radioKeepFiltered
            // 
            resources.ApplyResources(this.radioKeepFiltered, "radioKeepFiltered");
            this.radioKeepFiltered.Name = "radioKeepFiltered";
            this.radioKeepFiltered.UseVisualStyleBackColor = true;
            // 
            // radioDoNotAddFiltered
            // 
            resources.ApplyResources(this.radioDoNotAddFiltered, "radioDoNotAddFiltered");
            this.radioDoNotAddFiltered.Checked = true;
            this.radioDoNotAddFiltered.Name = "radioDoNotAddFiltered";
            this.radioDoNotAddFiltered.TabStop = true;
            this.radioDoNotAddFiltered.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // panelMultiple
            // 
            this.panelMultiple.Controls.Add(this.msgDuplicatePeptides);
            this.panelMultiple.Controls.Add(this.radioAddToAll);
            this.panelMultiple.Controls.Add(this.radioFirstOccurence);
            this.panelMultiple.Controls.Add(this.radioNoDuplicates);
            resources.ApplyResources(this.panelMultiple, "panelMultiple");
            this.panelMultiple.Name = "panelMultiple";
            // 
            // panelUnmatched
            // 
            this.panelUnmatched.Controls.Add(this.radioAddUnmatched);
            this.panelUnmatched.Controls.Add(this.radioFilterUnmatched);
            this.panelUnmatched.Controls.Add(this.msgUnmatchedPeptides);
            resources.ApplyResources(this.panelUnmatched, "panelUnmatched");
            this.panelUnmatched.Name = "panelUnmatched";
            // 
            // panelFiltered
            // 
            this.panelFiltered.Controls.Add(this.msgFilteredPeptides);
            this.panelFiltered.Controls.Add(this.radioKeepFiltered);
            this.panelFiltered.Controls.Add(this.radioDoNotAddFiltered);
            resources.ApplyResources(this.panelFiltered, "panelFiltered");
            this.panelFiltered.Name = "panelFiltered";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // FilterMatchedPeptidesDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AccessibleRole = System.Windows.Forms.AccessibleRole.None;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.label1);
            this.Controls.Add(this.panelFiltered);
            this.Controls.Add(this.panelUnmatched);
            this.Controls.Add(this.panelMultiple);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FilterMatchedPeptidesDlg";
            this.ShowInTaskbar = false;
            this.panelMultiple.ResumeLayout(false);
            this.panelMultiple.PerformLayout();
            this.panelUnmatched.ResumeLayout(false);
            this.panelUnmatched.PerformLayout();
            this.panelFiltered.ResumeLayout(false);
            this.panelFiltered.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton radioNoDuplicates;
        private System.Windows.Forms.RadioButton radioFirstOccurence;
        private System.Windows.Forms.RadioButton radioAddToAll;
        private System.Windows.Forms.RadioButton radioFilterUnmatched;
        private System.Windows.Forms.RadioButton radioAddUnmatched;
        private System.Windows.Forms.RadioButton radioKeepFiltered;
        private System.Windows.Forms.RadioButton radioDoNotAddFiltered;
        private System.Windows.Forms.Label msgUnmatchedPeptides;
        private System.Windows.Forms.Label msgDuplicatePeptides;
        private System.Windows.Forms.Label msgFilteredPeptides;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Panel panelMultiple;
        private System.Windows.Forms.Panel panelUnmatched;
        private System.Windows.Forms.Panel panelFiltered;
        private System.Windows.Forms.Label label1;
    }
}
