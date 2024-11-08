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
            this.radioUseList = new System.Windows.Forms.RadioButton();
            this.textNewDatabase = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textImportText = new System.Windows.Forms.TextBox();
            this.btnBrowseText = new System.Windows.Forms.Button();
            this.radioUseProtein = new System.Windows.Forms.RadioButton();
            this.label6 = new System.Windows.Forms.Label();
            this.textNewDatabaseProteins = new System.Windows.Forms.TextBox();
            this.btnCreateDbProteins = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.comboBoxProteins = new System.Windows.Forms.ComboBox();
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
            this.radioUseExisting.Name = "radioUseExisting";
            this.radioUseExisting.UseVisualStyleBackColor = true;
            this.radioUseExisting.CheckedChanged += new System.EventHandler(this.radioUseExisting_CheckedChanged);
            // 
            // radioUseList
            // 
            resources.ApplyResources(this.radioUseList, "radioUseList");
            this.radioUseList.Name = "radioUseList";
            this.radioUseList.UseVisualStyleBackColor = true;
            this.radioUseList.CheckedChanged += new System.EventHandler(this.radioCreateNew_CheckedChanged);
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
            // radioUseProtein
            // 
            resources.ApplyResources(this.radioUseProtein, "radioUseProtein");
            this.radioUseProtein.Checked = true;
            this.radioUseProtein.Name = "radioUseProtein";
            this.radioUseProtein.TabStop = true;
            this.radioUseProtein.UseVisualStyleBackColor = true;
            this.radioUseProtein.CheckedChanged += new System.EventHandler(this.radioUseProtein_CheckedChanged);
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // textNewDatabaseProteins
            // 
            resources.ApplyResources(this.textNewDatabaseProteins, "textNewDatabaseProteins");
            this.textNewDatabaseProteins.Name = "textNewDatabaseProteins";
            // 
            // btnCreateDbProteins
            // 
            resources.ApplyResources(this.btnCreateDbProteins, "btnCreateDbProteins");
            this.btnCreateDbProteins.Name = "btnCreateDbProteins";
            this.btnCreateDbProteins.UseVisualStyleBackColor = true;
            this.btnCreateDbProteins.Click += new System.EventHandler(this.btnCreateDbProteins_Click);
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // comboBoxProteins
            // 
            this.comboBoxProteins.DisplayMember = "Name";
            this.comboBoxProteins.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxProteins.FormattingEnabled = true;
            resources.ApplyResources(this.comboBoxProteins, "comboBoxProteins");
            this.comboBoxProteins.Name = "comboBoxProteins";
            // 
            // CreateIrtCalculatorDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.comboBoxProteins);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textNewDatabaseProteins);
            this.Controls.Add(this.btnCreateDbProteins);
            this.Controls.Add(this.radioUseProtein);
            this.Controls.Add(this.btnBrowseText);
            this.Controls.Add(this.textImportText);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textNewDatabase);
            this.Controls.Add(this.radioUseList);
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
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CreateIrtCalculatorDlg";
            this.ShowIcon = false;
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
        private System.Windows.Forms.RadioButton radioUseList;
        private System.Windows.Forms.TextBox textNewDatabase;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textImportText;
        private System.Windows.Forms.Button btnBrowseText;
        private System.Windows.Forms.RadioButton radioUseProtein;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textNewDatabaseProteins;
        private System.Windows.Forms.Button btnCreateDbProteins;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox comboBoxProteins;
    }
}