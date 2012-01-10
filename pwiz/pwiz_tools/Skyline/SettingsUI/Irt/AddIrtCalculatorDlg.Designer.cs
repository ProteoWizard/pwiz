namespace pwiz.Skyline.SettingsUI.Irt
{
    partial class AddIrtCalculatorDlg
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
            this.comboLibrary = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.radioSettings = new System.Windows.Forms.RadioButton();
            this.radioFile = new System.Windows.Forms.RadioButton();
            this.textFilePath = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnBrowseFile = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(290, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(290, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 8;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(171, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Import peptide retention times from:";
            // 
            // comboLibrary
            // 
            this.comboLibrary.DisplayMember = "Name";
            this.comboLibrary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLibrary.FormattingEnabled = true;
            this.comboLibrary.Location = new System.Drawing.Point(36, 81);
            this.comboLibrary.Name = "comboLibrary";
            this.comboLibrary.Size = new System.Drawing.Size(240, 21);
            this.comboLibrary.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(33, 62);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(86, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "&Calculator name:";
            // 
            // radioSettings
            // 
            this.radioSettings.AutoSize = true;
            this.radioSettings.Checked = true;
            this.radioSettings.Location = new System.Drawing.Point(16, 33);
            this.radioSettings.Name = "radioSettings";
            this.radioSettings.Size = new System.Drawing.Size(139, 17);
            this.radioSettings.TabIndex = 1;
            this.radioSettings.TabStop = true;
            this.radioSettings.Text = "A &named iRT calculator:";
            this.radioSettings.UseVisualStyleBackColor = true;
            this.radioSettings.CheckedChanged += new System.EventHandler(this.radioSettings_CheckedChanged);
            // 
            // radioFile
            // 
            this.radioFile.AutoSize = true;
            this.radioFile.Location = new System.Drawing.Point(16, 128);
            this.radioFile.Name = "radioFile";
            this.radioFile.Size = new System.Drawing.Size(124, 17);
            this.radioFile.TabIndex = 4;
            this.radioFile.TabStop = true;
            this.radioFile.Text = "An iRT &database file:";
            this.radioFile.UseVisualStyleBackColor = true;
            this.radioFile.CheckedChanged += new System.EventHandler(this.radioFile_CheckedChanged);
            // 
            // textFilePath
            // 
            this.textFilePath.Enabled = false;
            this.textFilePath.Location = new System.Drawing.Point(36, 178);
            this.textFilePath.Name = "textFilePath";
            this.textFilePath.Size = new System.Drawing.Size(240, 20);
            this.textFilePath.TabIndex = 6;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(36, 159);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(50, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "&File path:";
            // 
            // btnBrowseFile
            // 
            this.btnBrowseFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseFile.Enabled = false;
            this.btnBrowseFile.Location = new System.Drawing.Point(290, 178);
            this.btnBrowseFile.Name = "btnBrowseFile";
            this.btnBrowseFile.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseFile.TabIndex = 7;
            this.btnBrowseFile.Text = "&Browse...";
            this.btnBrowseFile.UseVisualStyleBackColor = true;
            this.btnBrowseFile.Click += new System.EventHandler(this.btnBrowseFile_Click);
            // 
            // AddIrtCalculatorDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(377, 219);
            this.Controls.Add(this.btnBrowseFile);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textFilePath);
            this.Controls.Add(this.radioFile);
            this.Controls.Add(this.radioSettings);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.comboLibrary);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AddIrtCalculatorDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Add iRT Database";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboLibrary;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.RadioButton radioSettings;
        private System.Windows.Forms.RadioButton radioFile;
        private System.Windows.Forms.TextBox textFilePath;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnBrowseFile;
    }
}