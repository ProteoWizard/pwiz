namespace pwiz.Skyline.SettingsUI
{
    partial class EditEnzymeDlg
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
            this.label1 = new System.Windows.Forms.Label();
            this.textCleavage = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textRestrict = new System.Windows.Forms.TextBox();
            this.comboDirection = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 72);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(77, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "&Cleavage AAs:";
            // 
            // textCleavage
            // 
            this.textCleavage.Location = new System.Drawing.Point(9, 89);
            this.textCleavage.Name = "textCleavage";
            this.textCleavage.Size = new System.Drawing.Size(160, 20);
            this.textCleavage.TabIndex = 3;
            this.helpTip.SetToolTip(this.textCleavage, "String of amino acid residues (without separator) at which cleavage occurs");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 128);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(87, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "&Unless next AAs:";
            // 
            // textRestrict
            // 
            this.textRestrict.Location = new System.Drawing.Point(9, 145);
            this.textRestrict.Name = "textRestrict";
            this.textRestrict.Size = new System.Drawing.Size(160, 20);
            this.textRestrict.TabIndex = 5;
            this.helpTip.SetToolTip(this.textRestrict, "List of amino acid residues (without separator) which\r\ninhibit cleavage when they" +
                    " are adjacent to the cleavage\r\namino acid residues");
            // 
            // comboDirection
            // 
            this.comboDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDirection.FormattingEnabled = true;
            this.comboDirection.Items.AddRange(new object[] {
            "C-terminus",
            "N-terminus"});
            this.comboDirection.Location = new System.Drawing.Point(12, 204);
            this.comboDirection.Name = "comboDirection";
            this.comboDirection.Size = new System.Drawing.Size(157, 21);
            this.comboDirection.TabIndex = 7;
            this.helpTip.SetToolTip(this.comboDirection, "Terminal side (N- or C-) of the cleavage amino acid residues\r\nat which cleavage o" +
                    "ccurs");
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 185);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(34, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "&Type:";
            // 
            // btnOk
            // 
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(190, 14);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 8;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(190, 44);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // textName
            // 
            this.textName.Location = new System.Drawing.Point(9, 31);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(160, 20);
            this.textName.TabIndex = 1;
            this.helpTip.SetToolTip(this.textName, "Name used to list this protease enzyme in the Peptide Settings form");
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 14);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Name:";
            // 
            // EditEnzymeDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(277, 237);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.comboDirection);
            this.Controls.Add(this.textRestrict);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textCleavage);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditEnzymeDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Enzyme";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textCleavage;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textRestrict;
        private System.Windows.Forms.ComboBox comboDirection;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ToolTip helpTip;
    }
}