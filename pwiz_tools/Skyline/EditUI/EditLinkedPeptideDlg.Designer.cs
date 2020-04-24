namespace pwiz.Skyline.EditUI
{
    partial class EditLinkedPeptideDlg
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
            this.label1 = new System.Windows.Forms.Label();
            this.tbxPeptideSequence = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxAttachmentOrdinal = new System.Windows.Forms.TextBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnEditModifications = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(130, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Linked peptide sequence:";
            // 
            // tbxPeptideSequence
            // 
            this.tbxPeptideSequence.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxPeptideSequence.Location = new System.Drawing.Point(18, 35);
            this.tbxPeptideSequence.Name = "tbxPeptideSequence";
            this.tbxPeptideSequence.Size = new System.Drawing.Size(284, 20);
            this.tbxPeptideSequence.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 58);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(183, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Crosslink attaches at residue number:";
            // 
            // tbxAttachmentOrdinal
            // 
            this.tbxAttachmentOrdinal.Location = new System.Drawing.Point(18, 74);
            this.tbxAttachmentOrdinal.Name = "tbxAttachmentOrdinal";
            this.tbxAttachmentOrdinal.Size = new System.Drawing.Size(100, 20);
            this.tbxAttachmentOrdinal.TabIndex = 3;
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.Location = new System.Drawing.Point(18, 113);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(284, 74);
            this.textBox1.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 97);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(72, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Modifications:";
            // 
            // btnEditModifications
            // 
            this.btnEditModifications.Location = new System.Drawing.Point(18, 193);
            this.btnEditModifications.Name = "btnEditModifications";
            this.btnEditModifications.Size = new System.Drawing.Size(127, 23);
            this.btnEditModifications.TabIndex = 6;
            this.btnEditModifications.Text = "Edit Modifications...";
            this.btnEditModifications.UseVisualStyleBackColor = true;
            this.btnEditModifications.Click += new System.EventHandler(this.btnEditModifications_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(227, 278);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOk.Location = new System.Drawing.Point(146, 278);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 7;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // EditLinkedPeptideDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(314, 313);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnEditModifications);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.tbxAttachmentOrdinal);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.tbxPeptideSequence);
            this.Controls.Add(this.label1);
            this.Name = "EditLinkedPeptideDlg";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Linked Peptide";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxPeptideSequence;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxAttachmentOrdinal;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnEditModifications;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
    }
}