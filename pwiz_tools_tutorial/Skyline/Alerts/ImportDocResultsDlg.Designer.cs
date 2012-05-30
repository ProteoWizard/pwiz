namespace pwiz.Skyline.Alerts
{
    partial class ImportDocResultsDlg
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
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.radioRemove = new System.Windows.Forms.RadioButton();
            this.radioMergeByName = new System.Windows.Forms.RadioButton();
            this.radioMergeByIndex = new System.Windows.Forms.RadioButton();
            this.radioAdd = new System.Windows.Forms.RadioButton();
            this.cbMergePeptides = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(205, 176);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 6;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(285, 176);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(291, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "How do you want to handle document results for this import?";
            // 
            // radioRemove
            // 
            this.radioRemove.AutoSize = true;
            this.radioRemove.Checked = true;
            this.radioRemove.Location = new System.Drawing.Point(16, 39);
            this.radioRemove.Name = "radioRemove";
            this.radioRemove.Size = new System.Drawing.Size(152, 17);
            this.radioRemove.TabIndex = 1;
            this.radioRemove.TabStop = true;
            this.radioRemove.Text = "Remove results information";
            this.radioRemove.UseVisualStyleBackColor = true;
            // 
            // radioMergeByName
            // 
            this.radioMergeByName.AutoSize = true;
            this.radioMergeByName.Location = new System.Drawing.Point(16, 62);
            this.radioMergeByName.Name = "radioMergeByName";
            this.radioMergeByName.Size = new System.Drawing.Size(234, 17);
            this.radioMergeByName.TabIndex = 2;
            this.radioMergeByName.Text = "Merge with existing results by replicate name";
            this.radioMergeByName.UseVisualStyleBackColor = true;
            // 
            // radioMergeByIndex
            // 
            this.radioMergeByIndex.AutoSize = true;
            this.radioMergeByIndex.Location = new System.Drawing.Point(16, 87);
            this.radioMergeByIndex.Name = "radioMergeByIndex";
            this.radioMergeByIndex.Size = new System.Drawing.Size(232, 17);
            this.radioMergeByIndex.TabIndex = 3;
            this.radioMergeByIndex.Text = "Merge with existing results by replicate order";
            this.radioMergeByIndex.UseVisualStyleBackColor = true;
            // 
            // radioAdd
            // 
            this.radioAdd.AutoSize = true;
            this.radioAdd.Location = new System.Drawing.Point(16, 111);
            this.radioAdd.Name = "radioAdd";
            this.radioAdd.Size = new System.Drawing.Size(115, 17);
            this.radioAdd.TabIndex = 4;
            this.radioAdd.Text = "Add new replicates";
            this.radioAdd.UseVisualStyleBackColor = true;
            // 
            // cbMergePeptides
            // 
            this.cbMergePeptides.AutoSize = true;
            this.cbMergePeptides.Location = new System.Drawing.Point(16, 146);
            this.cbMergePeptides.Name = "cbMergePeptides";
            this.cbMergePeptides.Size = new System.Drawing.Size(145, 17);
            this.cbMergePeptides.TabIndex = 5;
            this.cbMergePeptides.Text = "Merge matching peptides";
            this.cbMergePeptides.UseVisualStyleBackColor = true;
            // 
            // ImportDocResultsDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(372, 211);
            this.Controls.Add(this.cbMergePeptides);
            this.Controls.Add(this.radioAdd);
            this.Controls.Add(this.radioMergeByIndex);
            this.Controls.Add(this.radioMergeByName);
            this.Controls.Add(this.radioRemove);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportDocResultsDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton radioRemove;
        private System.Windows.Forms.RadioButton radioMergeByName;
        private System.Windows.Forms.RadioButton radioMergeByIndex;
        private System.Windows.Forms.RadioButton radioAdd;
        private System.Windows.Forms.CheckBox cbMergePeptides;
    }
}