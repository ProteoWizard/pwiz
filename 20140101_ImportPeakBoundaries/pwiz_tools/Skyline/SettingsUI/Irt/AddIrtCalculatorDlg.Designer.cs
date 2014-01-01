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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddIrtCalculatorDlg));
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
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // comboLibrary
            // 
            resources.ApplyResources(this.comboLibrary, "comboLibrary");
            this.comboLibrary.DisplayMember = "Name";
            this.comboLibrary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLibrary.FormattingEnabled = true;
            this.comboLibrary.Name = "comboLibrary";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // radioSettings
            // 
            resources.ApplyResources(this.radioSettings, "radioSettings");
            this.radioSettings.Checked = true;
            this.radioSettings.Name = "radioSettings";
            this.radioSettings.TabStop = true;
            this.radioSettings.UseVisualStyleBackColor = true;
            this.radioSettings.CheckedChanged += new System.EventHandler(this.radioSettings_CheckedChanged);
            // 
            // radioFile
            // 
            resources.ApplyResources(this.radioFile, "radioFile");
            this.radioFile.Name = "radioFile";
            this.radioFile.TabStop = true;
            this.radioFile.UseVisualStyleBackColor = true;
            this.radioFile.CheckedChanged += new System.EventHandler(this.radioFile_CheckedChanged);
            // 
            // textFilePath
            // 
            resources.ApplyResources(this.textFilePath, "textFilePath");
            this.textFilePath.Name = "textFilePath";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // btnBrowseFile
            // 
            resources.ApplyResources(this.btnBrowseFile, "btnBrowseFile");
            this.btnBrowseFile.Name = "btnBrowseFile";
            this.btnBrowseFile.UseVisualStyleBackColor = true;
            this.btnBrowseFile.Click += new System.EventHandler(this.btnBrowseFile_Click);
            // 
            // AddIrtCalculatorDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
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