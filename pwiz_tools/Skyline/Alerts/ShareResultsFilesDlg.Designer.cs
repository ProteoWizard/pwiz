namespace pwiz.Skyline.Alerts
{
    partial class ShareResultsFilesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ShareResultsFilesDlg));
            this.label1 = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.checkedListBox = new System.Windows.Forms.CheckedListBox();
            this.checkboxSelectAll = new System.Windows.Forms.CheckBox();
            this.checkedStatus = new System.Windows.Forms.Label();
            this.listboxMissingFiles = new System.Windows.Forms.ListBox();
            this.labelMissingFiles = new System.Windows.Forms.Label();
            this.btnLocateFiles = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.btnFindInFolder = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.Btn_Accept_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // checkedListBox
            // 
            resources.ApplyResources(this.checkedListBox, "checkedListBox");
            this.checkedListBox.CheckOnClick = true;
            this.checkedListBox.FormattingEnabled = true;
            this.checkedListBox.Name = "checkedListBox";
            this.checkedListBox.SelectedIndexChanged += new System.EventHandler(this.CheckedListBoxResults_SelectIndexChanged);
            // 
            // checkboxSelectAll
            // 
            resources.ApplyResources(this.checkboxSelectAll, "checkboxSelectAll");
            this.checkboxSelectAll.Checked = true;
            this.checkboxSelectAll.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkboxSelectAll.Name = "checkboxSelectAll";
            this.checkboxSelectAll.UseVisualStyleBackColor = true;
            this.checkboxSelectAll.CheckedChanged += new System.EventHandler(this.CheckboxSelectAll_CheckedChanged);
            // 
            // checkedStatus
            // 
            resources.ApplyResources(this.checkedStatus, "checkedStatus");
            this.checkedStatus.Name = "checkedStatus";
            // 
            // listboxMissingFiles
            // 
            resources.ApplyResources(this.listboxMissingFiles, "listboxMissingFiles");
            this.listboxMissingFiles.FormattingEnabled = true;
            this.listboxMissingFiles.Name = "listboxMissingFiles";
            this.listboxMissingFiles.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.MissingListBox_MouseDoubleClick);
            // 
            // labelMissingFiles
            // 
            resources.ApplyResources(this.labelMissingFiles, "labelMissingFiles");
            this.labelMissingFiles.Name = "labelMissingFiles";
            // 
            // btnLocateFiles
            // 
            resources.ApplyResources(this.btnLocateFiles, "btnLocateFiles");
            this.btnLocateFiles.Name = "btnLocateFiles";
            this.btnLocateFiles.UseVisualStyleBackColor = true;
            this.btnLocateFiles.Click += new System.EventHandler(this.Btn_addFiles_Click);
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.checkedListBox);
            this.splitContainer1.Panel1.Controls.Add(this.checkboxSelectAll);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.btnFindInFolder);
            this.splitContainer1.Panel2.Controls.Add(this.listboxMissingFiles);
            this.splitContainer1.Panel2.Controls.Add(this.labelMissingFiles);
            this.splitContainer1.Panel2.Controls.Add(this.btnLocateFiles);
            // 
            // btnFindInFolder
            // 
            resources.ApplyResources(this.btnFindInFolder, "btnFindInFolder");
            this.btnFindInFolder.Name = "btnFindInFolder";
            this.btnFindInFolder.UseVisualStyleBackColor = true;
            this.btnFindInFolder.Click += new System.EventHandler(this.FindResultsFolder_Click);
            // 
            // ShareResultsFilesDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.checkedStatus);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShareResultsFilesDlg";
            this.ShowInTaskbar = false;
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.CheckedListBox checkedListBox;
        private System.Windows.Forms.CheckBox checkboxSelectAll;
        private System.Windows.Forms.Label checkedStatus;
        private System.Windows.Forms.ListBox listboxMissingFiles;
        private System.Windows.Forms.Label labelMissingFiles;
        private System.Windows.Forms.Button btnLocateFiles;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btnFindInFolder;
    }
}