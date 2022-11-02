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
            this.iRTPeptidesLabel = new System.Windows.Forms.Label();
            this.panelProperties = new System.Windows.Forms.Panel();
            this.panelFilesPrositProperties = new System.Windows.Forms.Panel();
            this.ceLabel = new System.Windows.Forms.Label();
            this.ceCombo = new System.Windows.Forms.ComboBox();
            this.panelFilesProps = new System.Windows.Forms.Panel();
            this.actionLabel = new System.Windows.Forms.Label();
            this.comboAction = new System.Windows.Forms.ComboBox();
            this.cbIncludeAmbiguousMatches = new System.Windows.Forms.CheckBox();
            this.cbKeepRedundant = new System.Windows.Forms.CheckBox();
            this.cbFilter = new System.Windows.Forms.CheckBox();
            this.comboStandards = new System.Windows.Forms.ComboBox();
            this.dataSourceGroupBox = new System.Windows.Forms.GroupBox();
            this.prositInfoSettingsBtn = new System.Windows.Forms.LinkLabel();
            this.prositDataSourceRadioButton = new System.Windows.Forms.RadioButton();
            this.dataSourceFilesRadioButton = new System.Windows.Forms.RadioButton();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.panelFiles = new System.Windows.Forms.Panel();
            this.gridInputFiles = new pwiz.Skyline.FileUI.PeptideSearch.BuildLibraryGridView();
            this.btnAddPaths = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.btnAddDirectory = new System.Windows.Forms.Button();
            this.btnAddFile = new System.Windows.Forms.Button();
            this.btnPrevious = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.panelProperties.SuspendLayout();
            this.panelFilesPrositProperties.SuspendLayout();
            this.panelFilesProps.SuspendLayout();
            this.dataSourceGroupBox.SuspendLayout();
            this.panelFiles.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridInputFiles)).BeginInit();
            this.SuspendLayout();
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            this.helpTip.SetToolTip(this.textName, resources.GetString("textName.ToolTip"));
            this.textName.TextChanged += new System.EventHandler(this.textName_TextChanged);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnNext
            // 
            resources.ApplyResources(this.btnNext, "btnNext");
            this.btnNext.Name = "btnNext";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textPath
            // 
            resources.ApplyResources(this.textPath, "textPath");
            this.textPath.Name = "textPath";
            this.helpTip.SetToolTip(this.textPath, resources.GetString("textPath.ToolTip"));
            this.textPath.TextChanged += new System.EventHandler(this.textPath_TextChanged);
            // 
            // iRTPeptidesLabel
            // 
            resources.ApplyResources(this.iRTPeptidesLabel, "iRTPeptidesLabel");
            this.iRTPeptidesLabel.Name = "iRTPeptidesLabel";
            // 
            // panelProperties
            // 
            resources.ApplyResources(this.panelProperties, "panelProperties");
            this.panelProperties.Controls.Add(this.panelFilesPrositProperties);
            this.panelProperties.Controls.Add(this.dataSourceGroupBox);
            this.panelProperties.Controls.Add(this.btnBrowse);
            this.panelProperties.Controls.Add(this.label2);
            this.panelProperties.Controls.Add(this.textPath);
            this.panelProperties.Controls.Add(this.textName);
            this.panelProperties.Controls.Add(this.label4);
            this.panelProperties.Name = "panelProperties";
            // 
            // panelFilesPrositProperties
            // 
            this.panelFilesPrositProperties.Controls.Add(this.ceLabel);
            this.panelFilesPrositProperties.Controls.Add(this.ceCombo);
            this.panelFilesPrositProperties.Controls.Add(this.panelFilesProps);
            this.panelFilesPrositProperties.Controls.Add(this.comboStandards);
            this.panelFilesPrositProperties.Controls.Add(this.iRTPeptidesLabel);
            resources.ApplyResources(this.panelFilesPrositProperties, "panelFilesPrositProperties");
            this.panelFilesPrositProperties.Name = "panelFilesPrositProperties";
            // 
            // ceLabel
            // 
            resources.ApplyResources(this.ceLabel, "ceLabel");
            this.ceLabel.Name = "ceLabel";
            // 
            // ceCombo
            // 
            this.ceCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ceCombo.FormattingEnabled = true;
            resources.ApplyResources(this.ceCombo, "ceCombo");
            this.ceCombo.Name = "ceCombo";
            // 
            // panelFilesProps
            // 
            this.panelFilesProps.Controls.Add(this.actionLabel);
            this.panelFilesProps.Controls.Add(this.comboAction);
            this.panelFilesProps.Controls.Add(this.cbIncludeAmbiguousMatches);
            this.panelFilesProps.Controls.Add(this.cbKeepRedundant);
            this.panelFilesProps.Controls.Add(this.cbFilter);
            resources.ApplyResources(this.panelFilesProps, "panelFilesProps");
            this.panelFilesProps.Name = "panelFilesProps";
            // 
            // actionLabel
            // 
            resources.ApplyResources(this.actionLabel, "actionLabel");
            this.actionLabel.Name = "actionLabel";
            // 
            // comboAction
            // 
            this.comboAction.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboAction, "comboAction");
            this.comboAction.FormattingEnabled = true;
            this.comboAction.Items.AddRange(new object[] {
            resources.GetString("comboAction.Items"),
            resources.GetString("comboAction.Items1")});
            this.comboAction.Name = "comboAction";
            this.helpTip.SetToolTip(this.comboAction, resources.GetString("comboAction.ToolTip"));
            // 
            // cbIncludeAmbiguousMatches
            // 
            resources.ApplyResources(this.cbIncludeAmbiguousMatches, "cbIncludeAmbiguousMatches");
            this.cbIncludeAmbiguousMatches.Name = "cbIncludeAmbiguousMatches";
            this.cbIncludeAmbiguousMatches.UseVisualStyleBackColor = true;
            // 
            // cbKeepRedundant
            // 
            resources.ApplyResources(this.cbKeepRedundant, "cbKeepRedundant");
            this.cbKeepRedundant.Name = "cbKeepRedundant";
            this.helpTip.SetToolTip(this.cbKeepRedundant, resources.GetString("cbKeepRedundant.ToolTip"));
            this.cbKeepRedundant.UseVisualStyleBackColor = true;
            // 
            // cbFilter
            // 
            resources.ApplyResources(this.cbFilter, "cbFilter");
            this.cbFilter.Name = "cbFilter";
            this.cbFilter.UseVisualStyleBackColor = true;
            // 
            // comboStandards
            // 
            this.comboStandards.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboStandards.FormattingEnabled = true;
            resources.ApplyResources(this.comboStandards, "comboStandards");
            this.comboStandards.Name = "comboStandards";
            this.comboStandards.SelectedIndexChanged += new System.EventHandler(this.comboStandards_SelectedIndexChanged);
            // 
            // dataSourceGroupBox
            // 
            this.dataSourceGroupBox.Controls.Add(this.prositInfoSettingsBtn);
            this.dataSourceGroupBox.Controls.Add(this.prositDataSourceRadioButton);
            this.dataSourceGroupBox.Controls.Add(this.dataSourceFilesRadioButton);
            resources.ApplyResources(this.dataSourceGroupBox, "dataSourceGroupBox");
            this.dataSourceGroupBox.Name = "dataSourceGroupBox";
            this.dataSourceGroupBox.TabStop = false;
            // 
            // prositInfoSettingsBtn
            // 
            resources.ApplyResources(this.prositInfoSettingsBtn, "prositInfoSettingsBtn");
            this.prositInfoSettingsBtn.Name = "prositInfoSettingsBtn";
            this.prositInfoSettingsBtn.TabStop = true;
            this.prositInfoSettingsBtn.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.prositInfoSettingsBtn_LinkClicked);
            // 
            // prositDataSourceRadioButton
            // 
            resources.ApplyResources(this.prositDataSourceRadioButton, "prositDataSourceRadioButton");
            this.prositDataSourceRadioButton.Name = "prositDataSourceRadioButton";
            this.prositDataSourceRadioButton.UseVisualStyleBackColor = true;
            // 
            // dataSourceFilesRadioButton
            // 
            resources.ApplyResources(this.dataSourceFilesRadioButton, "dataSourceFilesRadioButton");
            this.dataSourceFilesRadioButton.Checked = true;
            this.dataSourceFilesRadioButton.Name = "dataSourceFilesRadioButton";
            this.dataSourceFilesRadioButton.TabStop = true;
            this.dataSourceFilesRadioButton.UseVisualStyleBackColor = true;
            this.dataSourceFilesRadioButton.CheckedChanged += new System.EventHandler(this.dataSourceFilesRadioButton_CheckedChanged);
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // panelFiles
            // 
            resources.ApplyResources(this.panelFiles, "panelFiles");
            this.panelFiles.Controls.Add(this.gridInputFiles);
            this.panelFiles.Controls.Add(this.btnAddPaths);
            this.panelFiles.Controls.Add(this.label7);
            this.panelFiles.Controls.Add(this.btnAddDirectory);
            this.panelFiles.Controls.Add(this.btnAddFile);
            this.panelFiles.Name = "panelFiles";
            // 
            // gridInputFiles
            // 
            this.gridInputFiles.AllowUserToAddRows = false;
            resources.ApplyResources(this.gridInputFiles, "gridInputFiles");
            this.gridInputFiles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridInputFiles.Name = "gridInputFiles";
            // 
            // btnAddPaths
            // 
            resources.ApplyResources(this.btnAddPaths, "btnAddPaths");
            this.btnAddPaths.Name = "btnAddPaths";
            this.btnAddPaths.UseVisualStyleBackColor = true;
            this.btnAddPaths.Click += new System.EventHandler(this.btnAddPaths_Click);
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // btnAddDirectory
            // 
            resources.ApplyResources(this.btnAddDirectory, "btnAddDirectory");
            this.btnAddDirectory.Name = "btnAddDirectory";
            this.helpTip.SetToolTip(this.btnAddDirectory, resources.GetString("btnAddDirectory.ToolTip"));
            this.btnAddDirectory.UseVisualStyleBackColor = true;
            this.btnAddDirectory.Click += new System.EventHandler(this.btnAddDirectory_Click);
            // 
            // btnAddFile
            // 
            resources.ApplyResources(this.btnAddFile, "btnAddFile");
            this.btnAddFile.Name = "btnAddFile";
            this.helpTip.SetToolTip(this.btnAddFile, resources.GetString("btnAddFile.ToolTip"));
            this.btnAddFile.UseVisualStyleBackColor = true;
            this.btnAddFile.Click += new System.EventHandler(this.btnAddFile_Click);
            // 
            // btnPrevious
            // 
            resources.ApplyResources(this.btnPrevious, "btnPrevious");
            this.btnPrevious.Name = "btnPrevious";
            this.btnPrevious.UseVisualStyleBackColor = true;
            this.btnPrevious.Click += new System.EventHandler(this.btnPrevious_Click);
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 32767;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // BuildLibraryDlg
            // 
            this.AcceptButton = this.btnNext;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnPrevious);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnNext);
            this.Controls.Add(this.panelProperties);
            this.Controls.Add(this.panelFiles);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BuildLibraryDlg";
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BuildLibraryDlg_FormClosing);
            this.panelProperties.ResumeLayout(false);
            this.panelProperties.PerformLayout();
            this.panelFilesPrositProperties.ResumeLayout(false);
            this.panelFilesPrositProperties.PerformLayout();
            this.panelFilesProps.ResumeLayout(false);
            this.panelFilesProps.PerformLayout();
            this.dataSourceGroupBox.ResumeLayout(false);
            this.dataSourceGroupBox.PerformLayout();
            this.panelFiles.ResumeLayout(false);
            this.panelFiles.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridInputFiles)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.Label iRTPeptidesLabel;
        private System.Windows.Forms.Panel panelProperties;
        private System.Windows.Forms.Panel panelFiles;
        private System.Windows.Forms.Button btnAddDirectory;
        private System.Windows.Forms.Button btnAddFile;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.Button btnAddPaths;
        private System.Windows.Forms.ComboBox comboStandards;
        private System.Windows.Forms.GroupBox dataSourceGroupBox;
        private System.Windows.Forms.RadioButton prositDataSourceRadioButton;
        private System.Windows.Forms.RadioButton dataSourceFilesRadioButton;
        private System.Windows.Forms.LinkLabel prositInfoSettingsBtn;
        private System.Windows.Forms.Panel panelFilesPrositProperties;
        private System.Windows.Forms.Panel panelFilesProps;
        private System.Windows.Forms.Label actionLabel;
        private System.Windows.Forms.ComboBox comboAction;
        private System.Windows.Forms.CheckBox cbIncludeAmbiguousMatches;
        private System.Windows.Forms.CheckBox cbKeepRedundant;
        private System.Windows.Forms.CheckBox cbFilter;
        private System.Windows.Forms.Label ceLabel;
        private System.Windows.Forms.ComboBox ceCombo;
        private FileUI.PeptideSearch.BuildLibraryGridView gridInputFiles;
    }
}
