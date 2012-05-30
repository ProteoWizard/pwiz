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
            this.radioNoDuplicates.AutoSize = true;
            this.radioNoDuplicates.Checked = true;
            this.radioNoDuplicates.Location = new System.Drawing.Point(6, 26);
            this.radioNoDuplicates.Name = "radioNoDuplicates";
            this.radioNoDuplicates.Size = new System.Drawing.Size(78, 17);
            this.radioNoDuplicates.TabIndex = 0;
            this.radioNoDuplicates.TabStop = true;
            this.radioNoDuplicates.Text = "Do not add";
            this.radioNoDuplicates.UseVisualStyleBackColor = true;
            // 
            // radioFirstOccurence
            // 
            this.radioFirstOccurence.AutoSize = true;
            this.radioFirstOccurence.Location = new System.Drawing.Point(6, 49);
            this.radioFirstOccurence.Name = "radioFirstOccurence";
            this.radioFirstOccurence.Size = new System.Drawing.Size(196, 17);
            this.radioFirstOccurence.TabIndex = 1;
            this.radioFirstOccurence.Text = "Add to only the first matching protein";
            this.radioFirstOccurence.UseVisualStyleBackColor = true;
            // 
            // radioAddToAll
            // 
            this.radioAddToAll.AutoSize = true;
            this.radioAddToAll.Location = new System.Drawing.Point(6, 72);
            this.radioAddToAll.Name = "radioAddToAll";
            this.radioAddToAll.Size = new System.Drawing.Size(155, 17);
            this.radioAddToAll.TabIndex = 2;
            this.radioAddToAll.Text = "Add to all matching proteins";
            this.radioAddToAll.UseVisualStyleBackColor = true;
            // 
            // radioFilterUnmatched
            // 
            this.radioFilterUnmatched.AutoSize = true;
            this.radioFilterUnmatched.Checked = true;
            this.radioFilterUnmatched.Location = new System.Drawing.Point(6, 29);
            this.radioFilterUnmatched.Name = "radioFilterUnmatched";
            this.radioFilterUnmatched.Size = new System.Drawing.Size(78, 17);
            this.radioFilterUnmatched.TabIndex = 2;
            this.radioFilterUnmatched.TabStop = true;
            this.radioFilterUnmatched.Text = "Do not add";
            this.radioFilterUnmatched.UseVisualStyleBackColor = true;
            // 
            // radioAddUnmatched
            // 
            this.radioAddUnmatched.AutoSize = true;
            this.radioAddUnmatched.Location = new System.Drawing.Point(6, 52);
            this.radioAddUnmatched.Name = "radioAddUnmatched";
            this.radioAddUnmatched.Size = new System.Drawing.Size(118, 17);
            this.radioAddUnmatched.TabIndex = 1;
            this.radioAddUnmatched.Text = "Add to a peptide list";
            this.radioAddUnmatched.UseVisualStyleBackColor = true;
            // 
            // msgUnmatchedPeptides
            // 
            this.msgUnmatchedPeptides.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.msgUnmatchedPeptides.Location = new System.Drawing.Point(1, 0);
            this.msgUnmatchedPeptides.Name = "msgUnmatchedPeptides";
            this.msgUnmatchedPeptides.Size = new System.Drawing.Size(318, 26);
            this.msgUnmatchedPeptides.TabIndex = 0;
            this.msgUnmatchedPeptides.Text = "label2";
            // 
            // msgDuplicatePeptides
            // 
            this.msgDuplicatePeptides.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.msgDuplicatePeptides.Location = new System.Drawing.Point(1, 0);
            this.msgDuplicatePeptides.Name = "msgDuplicatePeptides";
            this.msgDuplicatePeptides.Size = new System.Drawing.Size(318, 23);
            this.msgDuplicatePeptides.TabIndex = 5;
            this.msgDuplicatePeptides.Text = "label1";
            // 
            // msgFilteredPeptides
            // 
            this.msgFilteredPeptides.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.msgFilteredPeptides.Location = new System.Drawing.Point(0, 0);
            this.msgFilteredPeptides.Name = "msgFilteredPeptides";
            this.msgFilteredPeptides.Size = new System.Drawing.Size(318, 25);
            this.msgFilteredPeptides.TabIndex = 0;
            this.msgFilteredPeptides.Text = "label3";
            // 
            // radioKeepFiltered
            // 
            this.radioKeepFiltered.AutoSize = true;
            this.radioKeepFiltered.Location = new System.Drawing.Point(6, 51);
            this.radioKeepFiltered.Name = "radioKeepFiltered";
            this.radioKeepFiltered.Size = new System.Drawing.Size(116, 17);
            this.radioKeepFiltered.TabIndex = 2;
            this.radioKeepFiltered.Text = "Include all peptides";
            this.radioKeepFiltered.UseVisualStyleBackColor = true;
            // 
            // radioDoNotAddFiltered
            // 
            this.radioDoNotAddFiltered.AutoSize = true;
            this.radioDoNotAddFiltered.Checked = true;
            this.radioDoNotAddFiltered.Location = new System.Drawing.Point(6, 28);
            this.radioDoNotAddFiltered.Name = "radioDoNotAddFiltered";
            this.radioDoNotAddFiltered.Size = new System.Drawing.Size(78, 17);
            this.radioDoNotAddFiltered.TabIndex = 1;
            this.radioDoNotAddFiltered.TabStop = true;
            this.radioDoNotAddFiltered.Text = "Do not add";
            this.radioDoNotAddFiltered.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(257, 330);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(177, 330);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // panelMultiple
            // 
            this.panelMultiple.Controls.Add(this.msgDuplicatePeptides);
            this.panelMultiple.Controls.Add(this.radioAddToAll);
            this.panelMultiple.Controls.Add(this.radioFirstOccurence);
            this.panelMultiple.Controls.Add(this.radioNoDuplicates);
            this.panelMultiple.Location = new System.Drawing.Point(11, 39);
            this.panelMultiple.Name = "panelMultiple";
            this.panelMultiple.Size = new System.Drawing.Size(324, 101);
            this.panelMultiple.TabIndex = 0;
            // 
            // panelUnmatched
            // 
            this.panelUnmatched.Controls.Add(this.radioAddUnmatched);
            this.panelUnmatched.Controls.Add(this.radioFilterUnmatched);
            this.panelUnmatched.Controls.Add(this.msgUnmatchedPeptides);
            this.panelUnmatched.Location = new System.Drawing.Point(11, 146);
            this.panelUnmatched.Name = "panelUnmatched";
            this.panelUnmatched.Size = new System.Drawing.Size(324, 80);
            this.panelUnmatched.TabIndex = 1;
            // 
            // panelFiltered
            // 
            this.panelFiltered.Controls.Add(this.msgFilteredPeptides);
            this.panelFiltered.Controls.Add(this.radioKeepFiltered);
            this.panelFiltered.Controls.Add(this.radioDoNotAddFiltered);
            this.panelFiltered.Location = new System.Drawing.Point(11, 232);
            this.panelFiltered.Name = "panelFiltered";
            this.panelFiltered.Size = new System.Drawing.Size(324, 83);
            this.panelFiltered.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(11, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(210, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Please specify how to handle the following:";
            // 
            // FilterMatchedPeptidesDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AccessibleRole = System.Windows.Forms.AccessibleRole.None;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(341, 366);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Filter Peptides";
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
