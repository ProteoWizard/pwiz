namespace pwiz.Skyline.EditUI
{
    partial class FindPeptideDlg
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
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textSequence = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioDown = new System.Windows.Forms.RadioButton();
            this.radioUp = new System.Windows.Forms.RadioButton();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(293, 42);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Enabled = false;
            this.btnOk.Location = new System.Drawing.Point(293, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 3;
            this.btnOk.Text = "&Find Next";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(103, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Sequence fragment:";
            // 
            // textSequence
            // 
            this.textSequence.Location = new System.Drawing.Point(16, 29);
            this.textSequence.Name = "textSequence";
            this.textSequence.Size = new System.Drawing.Size(249, 20);
            this.textSequence.TabIndex = 1;
            this.textSequence.TextChanged += new System.EventHandler(this.textSequence_TextChanged);
            this.textSequence.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textSequence_KeyPress);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioDown);
            this.groupBox1.Controls.Add(this.radioUp);
            this.groupBox1.Location = new System.Drawing.Point(16, 66);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(130, 44);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Direction";
            // 
            // radioDown
            // 
            this.radioDown.AutoSize = true;
            this.radioDown.Checked = true;
            this.radioDown.Location = new System.Drawing.Point(53, 18);
            this.radioDown.Name = "radioDown";
            this.radioDown.Size = new System.Drawing.Size(53, 17);
            this.radioDown.TabIndex = 1;
            this.radioDown.TabStop = true;
            this.radioDown.Text = "&Down";
            this.radioDown.UseVisualStyleBackColor = true;
            // 
            // radioUp
            // 
            this.radioUp.AutoSize = true;
            this.radioUp.Location = new System.Drawing.Point(7, 18);
            this.radioUp.Name = "radioUp";
            this.radioUp.Size = new System.Drawing.Size(39, 17);
            this.radioUp.TabIndex = 0;
            this.radioUp.TabStop = true;
            this.radioUp.Text = "&Up";
            this.radioUp.UseVisualStyleBackColor = true;
            // 
            // FindPeptideDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(380, 122);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.textSequence);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindPeptideDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Find Peptide";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textSequence;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioDown;
        private System.Windows.Forms.RadioButton radioUp;
    }
}