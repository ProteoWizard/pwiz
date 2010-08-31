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
            this.btnNoDuplicates = new System.Windows.Forms.RadioButton();
            this.btnFirstOccurence = new System.Windows.Forms.RadioButton();
            this.radioButton3 = new System.Windows.Forms.RadioButton();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.msg = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnNoDuplicates
            // 
            this.btnNoDuplicates.AutoSize = true;
            this.btnNoDuplicates.Location = new System.Drawing.Point(10, 67);
            this.btnNoDuplicates.Name = "btnNoDuplicates";
            this.btnNoDuplicates.Size = new System.Drawing.Size(167, 17);
            this.btnNoDuplicates.TabIndex = 0;
            this.btnNoDuplicates.TabStop = true;
            this.btnNoDuplicates.Text = "Do not add duplicate peptides";
            this.btnNoDuplicates.UseVisualStyleBackColor = true;
            // 
            // btnFirstOccurence
            // 
            this.btnFirstOccurence.AutoSize = true;
            this.btnFirstOccurence.Location = new System.Drawing.Point(10, 90);
            this.btnFirstOccurence.Name = "btnFirstOccurence";
            this.btnFirstOccurence.Size = new System.Drawing.Size(157, 17);
            this.btnFirstOccurence.TabIndex = 1;
            this.btnFirstOccurence.TabStop = true;
            this.btnFirstOccurence.Text = "Add only the first occurence";
            this.btnFirstOccurence.UseVisualStyleBackColor = true;
            // 
            // radioButton3
            // 
            this.radioButton3.AutoSize = true;
            this.radioButton3.Location = new System.Drawing.Point(10, 113);
            this.radioButton3.Name = "radioButton3";
            this.radioButton3.Size = new System.Drawing.Size(155, 17);
            this.radioButton3.TabIndex = 2;
            this.radioButton3.TabStop = true;
            this.radioButton3.Text = "Add to all matching proteins";
            this.radioButton3.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(133, 139);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 3;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(214, 139);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // msg
            // 
            this.msg.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.msg.Location = new System.Drawing.Point(12, 9);
            this.msg.Name = "msg";
            this.msg.Size = new System.Drawing.Size(277, 49);
            this.msg.TabIndex = 5;
            this.msg.Text = "label1";
            // 
            // FilterMatchedPeptidesDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(301, 174);
            this.Controls.Add(this.msg);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.radioButton3);
            this.Controls.Add(this.btnFirstOccurence);
            this.Controls.Add(this.btnNoDuplicates);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FilterMatchedPeptidesDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton btnNoDuplicates;
        private System.Windows.Forms.RadioButton btnFirstOccurence;
        private System.Windows.Forms.RadioButton radioButton3;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label msg;
    }
}
