namespace pwiz.Skyline.FileUI
{
    partial class CreateIrtCalculatorDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateIrtCalculatorDlg));
            this.label2 = new System.Windows.Forms.Label();
            this.btnCreateDb = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnBrowseDb = new System.Windows.Forms.Button();
            this.textOpenDatabase = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textCalculatorName = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.radioUseExisting = new System.Windows.Forms.RadioButton();
            this.radioCreateNew = new System.Windows.Forms.RadioButton();
            this.textNewDatabase = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textImportText = new System.Windows.Forms.TextBox();
            this.btnBrowseText = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // btnCreateDb
            // 
            resources.ApplyResources(this.btnCreateDb, "btnCreateDb");
            this.btnCreateDb.Name = "btnCreateDb";
            this.btnCreateDb.UseVisualStyleBackColor = true;
            this.btnCreateDb.Click += new System.EventHandler(this.btnCreateDb_Click);
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
            // btnBrowseDb
            // 
            resources.ApplyResources(this.btnBrowseDb, "btnBrowseDb");
            this.btnBrowseDb.Name = "btnBrowseDb";
            this.btnBrowseDb.UseVisualStyleBackColor = true;
            this.btnBrowseDb.Click += new System.EventHandler(this.btnBrowseDb_Click);
            // 
            // textOpenDatabase
            // 
            resources.ApplyResources(this.textOpenDatabase, "textOpenDatabase");
            this.textOpenDatabase.Name = "textOpenDatabase";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // textCalculatorName
            // 
            resources.ApplyResources(this.textCalculatorName, "textCalculatorName");
            this.textCalculatorName.Name = "textCalculatorName";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // radioUseExisting
            // 
            resources.ApplyResources(this.radioUseExisting, "radioUseExisting");
            this.radioUseExisting.Checked = true;
            this.radioUseExisting.Name = "radioUseExisting";
            this.radioUseExisting.TabStop = true;
            this.radioUseExisting.UseVisualStyleBackColor = true;
            this.radioUseExisting.CheckedChanged += new System.EventHandler(this.radioUseExisting_CheckedChanged);
            // 
            // radioCreateNew
            // 
            resources.ApplyResources(this.radioCreateNew, "radioCreateNew");
            this.radioCreateNew.Name = "radioCreateNew";
            this.radioCreateNew.UseVisualStyleBackColor = true;
            this.radioCreateNew.CheckedChanged += new System.EventHandler(this.radioCreateNew_CheckedChanged);
            // 
            // textNewDatabase
            // 
            resources.ApplyResources(this.textNewDatabase, "textNewDatabase");
            this.textNewDatabase.Name = "textNewDatabase";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textImportText
            // 
            resources.ApplyResources(this.textImportText, "textImportText");
            this.textImportText.Name = "textImportText";
            // 
            // btnBrowseText
            // 
            resources.ApplyResources(this.btnBrowseText, "btnBrowseText");
            this.btnBrowseText.Name = "btnBrowseText";
            this.btnBrowseText.UseVisualStyleBackColor = true;
            this.btnBrowseText.Click += new System.EventHandler(this.btnBrowseText_Click);
            // 
            // CreateIrtCalculatorDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnBrowseText);
            this.Controls.Add(this.textImportText);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textNewDatabase);
            this.Controls.Add(this.radioCreateNew);
            this.Controls.Add(this.radioUseExisting);
            this.Controls.Add(this.btnCreateDb);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnBrowseDb);
            this.Controls.Add(this.textOpenDatabase);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textCalculatorName);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CreateIrtCalculatorDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnCreateDb;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnBrowseDb;
        private System.Windows.Forms.TextBox textOpenDatabase;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textCalculatorName;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.RadioButton radioUseExisting;
        private System.Windows.Forms.RadioButton radioCreateNew;
        private System.Windows.Forms.TextBox textNewDatabase;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textImportText;
        private System.Windows.Forms.Button btnBrowseText;
    }
}