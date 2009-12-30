namespace pwiz.Skyline.SettingsUI
{
    partial class BuildLibraryDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BuildLibraryDlg));
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.textPath = new System.Windows.Forms.TextBox();
            this.comboAction = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textAuthority = new System.Windows.Forms.TextBox();
            this.textID = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.cbKeepRedundant = new System.Windows.Forms.CheckBox();
            this.textCutoff = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.panelProperties = new System.Windows.Forms.Panel();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.panelFiles = new System.Windows.Forms.Panel();
            this.cbSelect = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.btnAddDirectory = new System.Windows.Forms.Button();
            this.btnAddFile = new System.Windows.Forms.Button();
            this.listInputFiles = new System.Windows.Forms.CheckedListBox();
            this.btnPrevious = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.panelProperties.SuspendLayout();
            this.panelFiles.SuspendLayout();
            this.SuspendLayout();
            // 
            // textName
            // 
            this.textName.Location = new System.Drawing.Point(15, 23);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(267, 20);
            this.textName.TabIndex = 1;
            this.helpTip.SetToolTip(this.textName, "The name Skyline will use to refer to this library");
            this.textName.TextChanged += new System.EventHandler(this.textName_TextChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(15, 7);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Name:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(308, 395);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnNext
            // 
            this.btnNext.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNext.Location = new System.Drawing.Point(227, 395);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(75, 23);
            this.btnNext.TabIndex = 2;
            this.btnNext.Text = "&Next >";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 69);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(67, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "&Output Path:";
            // 
            // textPath
            // 
            this.textPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textPath.Location = new System.Drawing.Point(15, 85);
            this.textPath.Name = "textPath";
            this.textPath.Size = new System.Drawing.Size(267, 20);
            this.textPath.TabIndex = 3;
            this.helpTip.SetToolTip(this.textPath, "Location on disk at which the final non-redundant\r\nlibrary will be created.");
            this.textPath.TextChanged += new System.EventHandler(this.textPath_TextChanged);
            // 
            // comboAction
            // 
            this.comboAction.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAction.Enabled = false;
            this.comboAction.FormattingEnabled = true;
            this.comboAction.Items.AddRange(new object[] {
            "Create",
            "Append"});
            this.comboAction.Location = new System.Drawing.Point(15, 147);
            this.comboAction.Name = "comboAction";
            this.comboAction.Size = new System.Drawing.Size(121, 21);
            this.comboAction.TabIndex = 6;
            this.helpTip.SetToolTip(this.comboAction, "Choose to create a new library or append to an\r\nexisting library, if you have pre" +
                    "viously saved a copy\r\nof the redundant library data.");
            this.comboAction.SelectedIndexChanged += new System.EventHandler(this.comboAction_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 131);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(40, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "&Action:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 256);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(238, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "Lab A&uthority (e.g. proteome.gs.washington.edu):";
            // 
            // textAuthority
            // 
            this.textAuthority.Location = new System.Drawing.Point(15, 272);
            this.textAuthority.Name = "textAuthority";
            this.textAuthority.Size = new System.Drawing.Size(267, 20);
            this.textAuthority.TabIndex = 11;
            this.helpTip.SetToolTip(this.textAuthority, resources.GetString("textAuthority.ToolTip"));
            // 
            // textID
            // 
            this.textID.Location = new System.Drawing.Point(15, 334);
            this.textID.Name = "textID";
            this.textID.Size = new System.Drawing.Size(100, 20);
            this.textID.TabIndex = 12;
            this.helpTip.SetToolTip(this.textID, resources.GetString("textID.ToolTip"));
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(15, 318);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(55, 13);
            this.label5.TabIndex = 20;
            this.label5.Text = "Library &ID:";
            // 
            // cbKeepRedundant
            // 
            this.cbKeepRedundant.AutoSize = true;
            this.cbKeepRedundant.Location = new System.Drawing.Point(155, 149);
            this.cbKeepRedundant.Name = "cbKeepRedundant";
            this.cbKeepRedundant.Size = new System.Drawing.Size(132, 17);
            this.cbKeepRedundant.TabIndex = 7;
            this.cbKeepRedundant.Text = "&Keep redundant library";
            this.helpTip.SetToolTip(this.cbKeepRedundant, "Check to keep a copy of the redundant library to which\r\nyou can append more spect" +
                    "ra in the future.");
            this.cbKeepRedundant.UseVisualStyleBackColor = true;
            // 
            // textCutoff
            // 
            this.textCutoff.Location = new System.Drawing.Point(15, 210);
            this.textCutoff.Name = "textCutoff";
            this.textCutoff.Size = new System.Drawing.Size(100, 20);
            this.textCutoff.TabIndex = 9;
            this.helpTip.SetToolTip(this.textCutoff, resources.GetString("textCutoff.ToolTip"));
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(15, 194);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(70, 13);
            this.label6.TabIndex = 8;
            this.label6.Text = "&Cut-off score:";
            // 
            // panelProperties
            // 
            this.panelProperties.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panelProperties.Controls.Add(this.btnBrowse);
            this.panelProperties.Controls.Add(this.label6);
            this.panelProperties.Controls.Add(this.textCutoff);
            this.panelProperties.Controls.Add(this.cbKeepRedundant);
            this.panelProperties.Controls.Add(this.label5);
            this.panelProperties.Controls.Add(this.textID);
            this.panelProperties.Controls.Add(this.textAuthority);
            this.panelProperties.Controls.Add(this.label3);
            this.panelProperties.Controls.Add(this.label1);
            this.panelProperties.Controls.Add(this.comboAction);
            this.panelProperties.Controls.Add(this.label2);
            this.panelProperties.Controls.Add(this.textPath);
            this.panelProperties.Controls.Add(this.textName);
            this.panelProperties.Controls.Add(this.label4);
            this.panelProperties.Location = new System.Drawing.Point(2, 2);
            this.panelProperties.Name = "panelProperties";
            this.panelProperties.Size = new System.Drawing.Size(390, 387);
            this.panelProperties.TabIndex = 0;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowse.Location = new System.Drawing.Point(306, 83);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(75, 23);
            this.btnBrowse.TabIndex = 4;
            this.btnBrowse.Text = "&Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // panelFiles
            // 
            this.panelFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panelFiles.Controls.Add(this.cbSelect);
            this.panelFiles.Controls.Add(this.label7);
            this.panelFiles.Controls.Add(this.btnAddDirectory);
            this.panelFiles.Controls.Add(this.btnAddFile);
            this.panelFiles.Controls.Add(this.listInputFiles);
            this.panelFiles.Location = new System.Drawing.Point(2, 2);
            this.panelFiles.Name = "panelFiles";
            this.panelFiles.Size = new System.Drawing.Size(390, 387);
            this.panelFiles.TabIndex = 25;
            this.panelFiles.Visible = false;
            // 
            // cbSelect
            // 
            this.cbSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbSelect.AutoSize = true;
            this.cbSelect.Enabled = false;
            this.cbSelect.Location = new System.Drawing.Point(11, 356);
            this.cbSelect.Name = "cbSelect";
            this.cbSelect.Size = new System.Drawing.Size(120, 17);
            this.cbSelect.TabIndex = 4;
            this.cbSelect.Text = "&Select / deselect all";
            this.cbSelect.UseVisualStyleBackColor = true;
            this.cbSelect.CheckedChanged += new System.EventHandler(this.cbSelect_CheckedChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(11, 19);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(58, 13);
            this.label7.TabIndex = 0;
            this.label7.Text = "&Input Files:";
            // 
            // btnAddDirectory
            // 
            this.btnAddDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddDirectory.Location = new System.Drawing.Point(288, 67);
            this.btnAddDirectory.Name = "btnAddDirectory";
            this.btnAddDirectory.Size = new System.Drawing.Size(93, 23);
            this.btnAddDirectory.TabIndex = 3;
            this.btnAddDirectory.Text = "Add &Directory...";
            this.helpTip.SetToolTip(this.btnAddDirectory, "Add all acceptable spectrum files found below a root directory.\r\nYou can uncheck " +
                    "ones you do not want added to the library.");
            this.btnAddDirectory.UseVisualStyleBackColor = true;
            this.btnAddDirectory.Click += new System.EventHandler(this.btnAddDirectory_Click);
            // 
            // btnAddFile
            // 
            this.btnAddFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddFile.Location = new System.Drawing.Point(288, 38);
            this.btnAddFile.Name = "btnAddFile";
            this.btnAddFile.Size = new System.Drawing.Size(93, 23);
            this.btnAddFile.TabIndex = 2;
            this.btnAddFile.Text = "Add &Files...";
            this.helpTip.SetToolTip(this.btnAddFile, "Add a file or multiple files from a single directory.");
            this.btnAddFile.UseVisualStyleBackColor = true;
            this.btnAddFile.Click += new System.EventHandler(this.btnAddFile_Click);
            // 
            // listInputFiles
            // 
            this.listInputFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.listInputFiles.CheckOnClick = true;
            this.listInputFiles.FormattingEnabled = true;
            this.listInputFiles.Location = new System.Drawing.Point(11, 38);
            this.listInputFiles.Name = "listInputFiles";
            this.listInputFiles.Size = new System.Drawing.Size(271, 304);
            this.listInputFiles.TabIndex = 1;
            this.helpTip.SetToolTip(this.listInputFiles, "The spectra from checked files which meet the scoring\r\ncut-off will be added to t" +
                    "he redundant library, from which\r\nthe final non-redundant library is then built." +
                    "");
            this.listInputFiles.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listInputFiles_ItemCheck);
            // 
            // btnPrevious
            // 
            this.btnPrevious.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPrevious.Enabled = false;
            this.btnPrevious.Location = new System.Drawing.Point(146, 395);
            this.btnPrevious.Name = "btnPrevious";
            this.btnPrevious.Size = new System.Drawing.Size(75, 23);
            this.btnPrevious.TabIndex = 1;
            this.btnPrevious.Text = "< &Previous";
            this.btnPrevious.UseVisualStyleBackColor = true;
            this.btnPrevious.Click += new System.EventHandler(this.btnPrevious_Click);
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 10000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // BuildLibraryDlg
            // 
            this.AcceptButton = this.btnNext;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(395, 430);
            this.Controls.Add(this.btnPrevious);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnNext);
            this.Controls.Add(this.panelProperties);
            this.Controls.Add(this.panelFiles);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BuildLibraryDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Build Library";
            this.panelProperties.ResumeLayout(false);
            this.panelProperties.PerformLayout();
            this.panelFiles.ResumeLayout(false);
            this.panelFiles.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.ComboBox comboAction;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textAuthority;
        private System.Windows.Forms.TextBox textID;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox cbKeepRedundant;
        private System.Windows.Forms.TextBox textCutoff;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Panel panelProperties;
        private System.Windows.Forms.Panel panelFiles;
        private System.Windows.Forms.Button btnAddDirectory;
        private System.Windows.Forms.Button btnAddFile;
        private System.Windows.Forms.CheckedListBox listInputFiles;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.CheckBox cbSelect;
        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.ToolTip helpTip;
    }
}