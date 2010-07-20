namespace pwiz.Skyline.EditUI
{
    partial class EditPepModsDlg
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
            this.labelHeavy1 = new System.Windows.Forms.Label();
            this.comboStatic1 = new System.Windows.Forms.ComboBox();
            this.comboHeavy1_1 = new System.Windows.Forms.ComboBox();
            this.labelAA1 = new System.Windows.Forms.Label();
            this.panelMain = new System.Windows.Forms.Panel();
            this.btnReset = new System.Windows.Forms.Button();
            this.cbCreateCopy = new System.Windows.Forms.CheckBox();
            this.panelMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(347, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(347, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 5);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(55, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Structural:";
            // 
            // labelHeavy1
            // 
            this.labelHeavy1.AutoSize = true;
            this.labelHeavy1.Location = new System.Drawing.Point(192, 5);
            this.labelHeavy1.Name = "labelHeavy1";
            this.labelHeavy1.Size = new System.Drawing.Size(77, 13);
            this.labelHeavy1.TabIndex = 1;
            this.labelHeavy1.Text = "Isotope heavy:";
            // 
            // comboStatic1
            // 
            this.comboStatic1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboStatic1.FormattingEnabled = true;
            this.comboStatic1.Location = new System.Drawing.Point(13, 21);
            this.comboStatic1.Name = "comboStatic1";
            this.comboStatic1.Size = new System.Drawing.Size(121, 21);
            this.comboStatic1.TabIndex = 2;
            // 
            // comboHeavy1_1
            // 
            this.comboHeavy1_1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboHeavy1_1.FormattingEnabled = true;
            this.comboHeavy1_1.Location = new System.Drawing.Point(195, 21);
            this.comboHeavy1_1.Name = "comboHeavy1_1";
            this.comboHeavy1_1.Size = new System.Drawing.Size(121, 21);
            this.comboHeavy1_1.TabIndex = 4;
            // 
            // labelAA1
            // 
            this.labelAA1.AutoSize = true;
            this.labelAA1.Location = new System.Drawing.Point(157, 24);
            this.labelAA1.Name = "labelAA1";
            this.labelAA1.Size = new System.Drawing.Size(14, 13);
            this.labelAA1.TabIndex = 3;
            this.labelAA1.Text = "A";
            // 
            // panelMain
            // 
            this.panelMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panelMain.AutoScroll = true;
            this.panelMain.Controls.Add(this.comboHeavy1_1);
            this.panelMain.Controls.Add(this.labelAA1);
            this.panelMain.Controls.Add(this.comboStatic1);
            this.panelMain.Controls.Add(this.label1);
            this.panelMain.Controls.Add(this.labelHeavy1);
            this.panelMain.Location = new System.Drawing.Point(2, 12);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(339, 135);
            this.panelMain.TabIndex = 0;
            // 
            // btnReset
            // 
            this.btnReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnReset.Location = new System.Drawing.Point(348, 124);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(75, 23);
            this.btnReset.TabIndex = 4;
            this.btnReset.Text = "&Reset";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // cbCreateCopy
            // 
            this.cbCreateCopy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbCreateCopy.AutoSize = true;
            this.cbCreateCopy.Location = new System.Drawing.Point(348, 84);
            this.cbCreateCopy.Name = "cbCreateCopy";
            this.cbCreateCopy.Size = new System.Drawing.Size(83, 17);
            this.cbCreateCopy.TabIndex = 3;
            this.cbCreateCopy.Text = "&Create copy";
            this.cbCreateCopy.UseVisualStyleBackColor = true;
            // 
            // EditPepModsDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(434, 159);
            this.Controls.Add(this.cbCreateCopy);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.panelMain);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPepModsDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Modifications";
            this.panelMain.ResumeLayout(false);
            this.panelMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelHeavy1;
        private System.Windows.Forms.ComboBox comboStatic1;
        private System.Windows.Forms.ComboBox comboHeavy1_1;
        private System.Windows.Forms.Label labelAA1;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.CheckBox cbCreateCopy;
    }
}