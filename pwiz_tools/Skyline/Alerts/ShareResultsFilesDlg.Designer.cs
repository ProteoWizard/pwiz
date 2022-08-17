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
            this.label1 = new System.Windows.Forms.Label();
            this.Btn_Accept = new System.Windows.Forms.Button();
            this.Btn_Cancel = new System.Windows.Forms.Button();
            this.checkedListBox = new System.Windows.Forms.CheckedListBox();
            this.checkboxSelectAll = new System.Windows.Forms.CheckBox();
            this.checkedStatus = new System.Windows.Forms.Label();
            this.missingListBox = new System.Windows.Forms.ListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btn_addFiles = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.findResultsFilesButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(20, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Files to include:";
            // 
            // Btn_Accept
            // 
            this.Btn_Accept.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Btn_Accept.Location = new System.Drawing.Point(356, 380);
            this.Btn_Accept.Name = "Btn_Accept";
            this.Btn_Accept.Size = new System.Drawing.Size(75, 23);
            this.Btn_Accept.TabIndex = 2;
            this.Btn_Accept.Text = "OK";
            this.Btn_Accept.UseVisualStyleBackColor = true;
            this.Btn_Accept.Click += new System.EventHandler(this.Btn_Accept_Click);
            // 
            // Btn_Cancel
            // 
            this.Btn_Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Btn_Cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Btn_Cancel.Location = new System.Drawing.Point(437, 380);
            this.Btn_Cancel.Name = "Btn_Cancel";
            this.Btn_Cancel.Size = new System.Drawing.Size(75, 23);
            this.Btn_Cancel.TabIndex = 3;
            this.Btn_Cancel.Text = "Cancel";
            this.Btn_Cancel.UseVisualStyleBackColor = true;
            // 
            // checkedListBox
            // 
            this.checkedListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListBox.CheckOnClick = true;
            this.checkedListBox.FormattingEnabled = true;
            this.checkedListBox.Location = new System.Drawing.Point(23, 25);
            this.checkedListBox.Name = "checkedListBox";
            this.checkedListBox.Size = new System.Drawing.Size(457, 124);
            this.checkedListBox.TabIndex = 4;
            this.checkedListBox.SelectedIndexChanged += new System.EventHandler(this.CheckedListBoxResults_SelectIndexChanged);
            // 
            // checkboxSelectAll
            // 
            this.checkboxSelectAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkboxSelectAll.AutoSize = true;
            this.checkboxSelectAll.Checked = true;
            this.checkboxSelectAll.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkboxSelectAll.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.checkboxSelectAll.Location = new System.Drawing.Point(23, 155);
            this.checkboxSelectAll.Name = "checkboxSelectAll";
            this.checkboxSelectAll.Size = new System.Drawing.Size(120, 17);
            this.checkboxSelectAll.TabIndex = 15;
            this.checkboxSelectAll.Text = "Select / deselect &all";
            this.checkboxSelectAll.UseVisualStyleBackColor = true;
            this.checkboxSelectAll.CheckedChanged += new System.EventHandler(this.CheckboxSelectAll_CheckedChanged);
            // 
            // checkedStatus
            // 
            this.checkedStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkedStatus.AutoSize = true;
            this.checkedStatus.Location = new System.Drawing.Point(12, 385);
            this.checkedStatus.Name = "checkedStatus";
            this.checkedStatus.Size = new System.Drawing.Size(0, 13);
            this.checkedStatus.TabIndex = 16;
            // 
            // missingListBox
            // 
            this.missingListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.missingListBox.FormattingEnabled = true;
            this.missingListBox.Location = new System.Drawing.Point(23, 24);
            this.missingListBox.Name = "missingListBox";
            this.missingListBox.Size = new System.Drawing.Size(457, 121);
            this.missingListBox.TabIndex = 17;
            this.missingListBox.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.MissingListBox_MouseDoubleClick);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(20, 8);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 13);
            this.label2.TabIndex = 18;
            this.label2.Text = "Missing files:";
            // 
            // btn_addFiles
            // 
            this.btn_addFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_addFiles.Location = new System.Drawing.Point(23, 149);
            this.btn_addFiles.Name = "btn_addFiles";
            this.btn_addFiles.Size = new System.Drawing.Size(81, 23);
            this.btn_addFiles.TabIndex = 19;
            this.btn_addFiles.Text = "&Locate Files...";
            this.btn_addFiles.UseVisualStyleBackColor = true;
            this.btn_addFiles.Click += new System.EventHandler(this.Btn_addFiles_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer1.Location = new System.Drawing.Point(12, 12);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.checkedListBox);
            this.splitContainer1.Panel1.Controls.Add(this.checkboxSelectAll);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.findResultsFilesButton);
            this.splitContainer1.Panel2.Controls.Add(this.missingListBox);
            this.splitContainer1.Panel2.Controls.Add(this.label2);
            this.splitContainer1.Panel2.Controls.Add(this.btn_addFiles);
            this.splitContainer1.Size = new System.Drawing.Size(500, 362);
            this.splitContainer1.SplitterDistance = 181;
            this.splitContainer1.TabIndex = 20;
            // 
            // findResultsFilesButton
            // 
            this.findResultsFilesButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.findResultsFilesButton.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.findResultsFilesButton.Location = new System.Drawing.Point(110, 149);
            this.findResultsFilesButton.Name = "findResultsFilesButton";
            this.findResultsFilesButton.Size = new System.Drawing.Size(89, 23);
            this.findResultsFilesButton.TabIndex = 20;
            this.findResultsFilesButton.Text = "&Find in Folder...";
            this.findResultsFilesButton.UseVisualStyleBackColor = true;
            this.findResultsFilesButton.Click += new System.EventHandler(this.findResultsFolder_Click);
            // 
            // ShareResultsFilesDlg
            // 
            this.AcceptButton = this.Btn_Accept;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.Btn_Cancel;
            this.ClientSize = new System.Drawing.Size(524, 411);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.checkedStatus);
            this.Controls.Add(this.Btn_Cancel);
            this.Controls.Add(this.Btn_Accept);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShareResultsFilesDlg";
            this.ShowInTaskbar = false;
            this.Text = "Share Results Files";
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
        private System.Windows.Forms.Button Btn_Accept;
        private System.Windows.Forms.Button Btn_Cancel;
        private System.Windows.Forms.CheckedListBox checkedListBox;
        private System.Windows.Forms.CheckBox checkboxSelectAll;
        private System.Windows.Forms.Label checkedStatus;
        private System.Windows.Forms.ListBox missingListBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btn_addFiles;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button findResultsFilesButton;
    }
}