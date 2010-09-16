namespace pwiz.Skyline.Alerts
{
    partial class FilterMatchedPeptidesDlg
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
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
            this.btnOk.Location = new System.Drawing.Point(137, 139);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 3;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(218, 139);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // msg
            // 
            this.msg.AutoSize = true;
            this.msg.Location = new System.Drawing.Point(3, 0);
            this.msg.MaximumSize = new System.Drawing.Size(250, 0);
            this.msg.Name = "msg";
            this.msg.Size = new System.Drawing.Size(35, 13);
            this.msg.TabIndex = 5;
            this.msg.Text = "label1";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.msg);
            this.panel1.Location = new System.Drawing.Point(12, 12);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(281, 49);
            this.panel1.TabIndex = 6;
            // 
            // FilterMatchedPeptidesDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(305, 174);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.radioButton3);
            this.Controls.Add(this.btnFirstOccurence);
            this.Controls.Add(this.btnNoDuplicates);
            this.Name = "FilterMatchedPeptidesDlg";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Form1";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
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
        private System.Windows.Forms.Panel panel1;
    }
}