namespace pwiz.Skyline.EditUI
{
    partial class GenerateDecoysDlg
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
            this.textNumberOfDecoys = new System.Windows.Forms.TextBox();
            this.comboDecoysLabelType = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.comboDecoysGenerationMethod = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(143, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Number of decoy precursors:";
            // 
            // textNumberOfDecoys
            // 
            this.textNumberOfDecoys.Location = new System.Drawing.Point(15, 28);
            this.textNumberOfDecoys.Name = "textNumberOfDecoys";
            this.textNumberOfDecoys.Size = new System.Drawing.Size(121, 20);
            this.textNumberOfDecoys.TabIndex = 1;
            // 
            // comboDecoysLabelType
            // 
            this.comboDecoysLabelType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDecoysLabelType.FormattingEnabled = true;
            this.comboDecoysLabelType.Location = new System.Drawing.Point(15, 87);
            this.comboDecoysLabelType.Name = "comboDecoysLabelType";
            this.comboDecoysLabelType.Size = new System.Drawing.Size(121, 21);
            this.comboDecoysLabelType.TabIndex = 12;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 71);
            this.label2.Name = "label2";
            this.label2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.label2.Size = new System.Drawing.Size(85, 13);
            this.label2.TabIndex = 13;
            this.label2.Text = "&Label type used:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(174, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 15;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(174, 12);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 14;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 130);
            this.label3.Name = "label3";
            this.label3.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.label3.Size = new System.Drawing.Size(132, 13);
            this.label3.TabIndex = 17;
            this.label3.Text = "Decoy generation &method:";
            // 
            // comboDecoysGenerationMethod
            // 
            this.comboDecoysGenerationMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDecoysGenerationMethod.FormattingEnabled = true;
            this.comboDecoysGenerationMethod.Location = new System.Drawing.Point(15, 146);
            this.comboDecoysGenerationMethod.Name = "comboDecoysGenerationMethod";
            this.comboDecoysGenerationMethod.Size = new System.Drawing.Size(121, 21);
            this.comboDecoysGenerationMethod.TabIndex = 16;
            // 
            // GenerateDecoysDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(261, 187);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.comboDecoysGenerationMethod);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.comboDecoysLabelType);
            this.Controls.Add(this.textNumberOfDecoys);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GenerateDecoysDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Add Decoy Peptides";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textNumberOfDecoys;
        private System.Windows.Forms.ComboBox comboDecoysLabelType;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboDecoysGenerationMethod;
    }
}