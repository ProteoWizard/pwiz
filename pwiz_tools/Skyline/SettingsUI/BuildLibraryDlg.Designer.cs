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
            this.ceLabel = new System.Windows.Forms.Label();
            this.ceCombo = new System.Windows.Forms.ComboBox();
            this.actionLabel = new System.Windows.Forms.Label();
            this.comboAction = new System.Windows.Forms.ComboBox();
            this.cbIncludeAmbiguousMatches = new System.Windows.Forms.CheckBox();
            this.cbKeepRedundant = new System.Windows.Forms.CheckBox();
            this.cbFilter = new System.Windows.Forms.CheckBox();
            this.comboStandards = new System.Windows.Forms.ComboBox();
            this.dataSourceGroupBox = new System.Windows.Forms.GroupBox();
            this.radioCarafeSource = new System.Windows.Forms.RadioButton();
            this.radioAlphaSource = new System.Windows.Forms.RadioButton();
            this.koinaInfoSettingsBtn = new System.Windows.Forms.LinkLabel();
            this.radioKoinaSource = new System.Windows.Forms.RadioButton();
            this.radioFilesSource = new System.Windows.Forms.RadioButton();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.gridInputFiles = new pwiz.Skyline.FileUI.PeptideSearch.BuildLibraryGridView();
            this.btnAddPaths = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.btnAddDirectory = new System.Windows.Forms.Button();
            this.btnAddFile = new System.Windows.Forms.Button();
            this.btnPrevious = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.tabControlMain = new pwiz.Skyline.Controls.WizardPages();
            this.tabProperties = new System.Windows.Forms.TabPage();
            this.tabControlDataSource = new pwiz.Skyline.Controls.WizardPages();
            this.tabFilesSource = new System.Windows.Forms.TabPage();
            this.tabCarafeSource = new System.Windows.Forms.TabPage();
            this.tabKoinaSource = new System.Windows.Forms.TabPage();
            this.tabFiles = new System.Windows.Forms.TabPage();
            this.tabLearn = new System.Windows.Forms.TabPage();
            this.tabControlLearning = new pwiz.Skyline.Controls.WizardPages();
            this.tabPageDocument = new System.Windows.Forms.TabPage();
            this.btnLearningDocBrowse = new System.Windows.Forms.Button();
            this.textLearningDoc = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tabPageLibraries = new System.Windows.Forms.TabPage();
            this.listLibraries = new System.Windows.Forms.CheckedListBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboLearnFrom = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tabAlphaSource = new System.Windows.Forms.TabPage();
            this.dataSourceGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridInputFiles)).BeginInit();
            this.tabControlMain.SuspendLayout();
            this.tabProperties.SuspendLayout();
            this.tabControlDataSource.SuspendLayout();
            this.tabFilesSource.SuspendLayout();
            this.tabKoinaSource.SuspendLayout();
            this.tabFiles.SuspendLayout();
            this.tabLearn.SuspendLayout();
            this.tabControlLearning.SuspendLayout();
            this.tabPageDocument.SuspendLayout();
            this.tabPageLibraries.SuspendLayout();
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
            this.modeUIHandler.SetUIMode(this.iRTPeptidesLabel, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
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
            this.helpTip.SetToolTip(this.cbIncludeAmbiguousMatches, resources.GetString("cbIncludeAmbiguousMatches.ToolTip"));
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
            this.helpTip.SetToolTip(this.cbFilter, resources.GetString("cbFilter.ToolTip"));
            this.cbFilter.UseVisualStyleBackColor = true;
            // 
            // comboStandards
            // 
            this.comboStandards.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboStandards.FormattingEnabled = true;
            resources.ApplyResources(this.comboStandards, "comboStandards");
            this.comboStandards.Name = "comboStandards";
            this.modeUIHandler.SetUIMode(this.comboStandards, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.comboStandards.SelectedIndexChanged += new System.EventHandler(this.comboStandards_SelectedIndexChanged);
            // 
            // dataSourceGroupBox
            // 
            this.dataSourceGroupBox.Controls.Add(this.radioCarafeSource);
            this.dataSourceGroupBox.Controls.Add(this.radioAlphaSource);
            this.dataSourceGroupBox.Controls.Add(this.koinaInfoSettingsBtn);
            this.dataSourceGroupBox.Controls.Add(this.radioKoinaSource);
            this.dataSourceGroupBox.Controls.Add(this.radioFilesSource);
            resources.ApplyResources(this.dataSourceGroupBox, "dataSourceGroupBox");
            this.dataSourceGroupBox.Name = "dataSourceGroupBox";
            this.dataSourceGroupBox.TabStop = false;
            this.modeUIHandler.SetUIMode(this.dataSourceGroupBox, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            // 
            // radioCarafeSource
            // 
            resources.ApplyResources(this.radioCarafeSource, "radioCarafeSource");
            this.radioCarafeSource.Name = "radioCarafeSource";
            this.radioCarafeSource.TabStop = true;
            this.radioCarafeSource.UseVisualStyleBackColor = true;
            this.radioCarafeSource.CheckedChanged += new System.EventHandler(this.dataSourceRadioButton_CheckedChanged);
            // 
            // radioAlphaSource
            // 
            resources.ApplyResources(this.radioAlphaSource, "radioAlphaSource");
            this.radioAlphaSource.Name = "radioAlphaSource";
            this.radioAlphaSource.TabStop = true;
            this.radioAlphaSource.UseVisualStyleBackColor = true;
            this.radioAlphaSource.CheckedChanged += new System.EventHandler(this.dataSourceRadioButton_CheckedChanged);
            // 
            // koinaInfoSettingsBtn
            // 
            resources.ApplyResources(this.koinaInfoSettingsBtn, "koinaInfoSettingsBtn");
            this.koinaInfoSettingsBtn.Name = "koinaInfoSettingsBtn";
            this.koinaInfoSettingsBtn.TabStop = true;
            this.koinaInfoSettingsBtn.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.koinaInfoSettingsBtn_LinkClicked);
            // 
            // radioKoinaSource
            // 
            resources.ApplyResources(this.radioKoinaSource, "radioKoinaSource");
            this.radioKoinaSource.Name = "radioKoinaSource";
            this.radioKoinaSource.UseVisualStyleBackColor = true;
            this.radioKoinaSource.CheckedChanged += new System.EventHandler(this.dataSourceRadioButton_CheckedChanged);
            // 
            // radioFilesSource
            // 
            resources.ApplyResources(this.radioFilesSource, "radioFilesSource");
            this.radioFilesSource.Checked = true;
            this.radioFilesSource.Name = "radioFilesSource";
            this.radioFilesSource.TabStop = true;
            this.radioFilesSource.UseVisualStyleBackColor = true;
            this.radioFilesSource.CheckedChanged += new System.EventHandler(this.dataSourceRadioButton_CheckedChanged);
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
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
            // tabControlMain
            // 
            resources.ApplyResources(this.tabControlMain, "tabControlMain");
            this.tabControlMain.Controls.Add(this.tabProperties);
            this.tabControlMain.Controls.Add(this.tabFiles);
            this.tabControlMain.Controls.Add(this.tabLearn);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.TabStop = false;
            // 
            // tabProperties
            // 
            this.tabProperties.BackColor = System.Drawing.SystemColors.Control;
            this.tabProperties.Controls.Add(this.tabControlDataSource);
            this.tabProperties.Controls.Add(this.textPath);
            this.tabProperties.Controls.Add(this.comboStandards);
            this.tabProperties.Controls.Add(this.dataSourceGroupBox);
            this.tabProperties.Controls.Add(this.iRTPeptidesLabel);
            this.tabProperties.Controls.Add(this.label4);
            this.tabProperties.Controls.Add(this.btnBrowse);
            this.tabProperties.Controls.Add(this.textName);
            this.tabProperties.Controls.Add(this.label2);
            resources.ApplyResources(this.tabProperties, "tabProperties");
            this.tabProperties.Name = "tabProperties";
            // 
            // tabControlDataSource
            // 
            resources.ApplyResources(this.tabControlDataSource, "tabControlDataSource");
            this.tabControlDataSource.Controls.Add(this.tabFilesSource);
            this.tabControlDataSource.Controls.Add(this.tabAlphaSource);
            this.tabControlDataSource.Controls.Add(this.tabCarafeSource);
            this.tabControlDataSource.Controls.Add(this.tabKoinaSource);
            this.tabControlDataSource.Name = "tabControlDataSource";
            this.tabControlDataSource.SelectedIndex = 0;
            this.tabControlDataSource.TabStop = false;
            // 
            // tabFilesSource
            // 
            this.tabFilesSource.BackColor = System.Drawing.SystemColors.Control;
            this.tabFilesSource.Controls.Add(this.cbIncludeAmbiguousMatches);
            this.tabFilesSource.Controls.Add(this.actionLabel);
            this.tabFilesSource.Controls.Add(this.comboAction);
            this.tabFilesSource.Controls.Add(this.cbKeepRedundant);
            this.tabFilesSource.Controls.Add(this.cbFilter);
            resources.ApplyResources(this.tabFilesSource, "tabFilesSource");
            this.tabFilesSource.Name = "tabFilesSource";
            // 
            // tabCarafeSource
            // 
            this.tabCarafeSource.BackColor = System.Drawing.SystemColors.Control;
            resources.ApplyResources(this.tabCarafeSource, "tabCarafeSource");
            this.tabCarafeSource.Name = "tabCarafeSource";
            // 
            // tabKoinaSource
            // 
            this.tabKoinaSource.BackColor = System.Drawing.SystemColors.Control;
            this.tabKoinaSource.Controls.Add(this.ceLabel);
            this.tabKoinaSource.Controls.Add(this.ceCombo);
            resources.ApplyResources(this.tabKoinaSource, "tabKoinaSource");
            this.tabKoinaSource.Name = "tabKoinaSource";
            // 
            // tabFiles
            // 
            this.tabFiles.BackColor = System.Drawing.SystemColors.Control;
            this.tabFiles.Controls.Add(this.gridInputFiles);
            this.tabFiles.Controls.Add(this.btnAddPaths);
            this.tabFiles.Controls.Add(this.btnAddFile);
            this.tabFiles.Controls.Add(this.label7);
            this.tabFiles.Controls.Add(this.btnAddDirectory);
            resources.ApplyResources(this.tabFiles, "tabFiles");
            this.tabFiles.Name = "tabFiles";
            // 
            // tabLearn
            // 
            this.tabLearn.BackColor = System.Drawing.SystemColors.Control;
            this.tabLearn.Controls.Add(this.tabControlLearning);
            this.tabLearn.Controls.Add(this.comboLearnFrom);
            this.tabLearn.Controls.Add(this.label1);
            resources.ApplyResources(this.tabLearn, "tabLearn");
            this.tabLearn.Name = "tabLearn";
            // 
            // tabControlLearning
            // 
            resources.ApplyResources(this.tabControlLearning, "tabControlLearning");
            this.tabControlLearning.Controls.Add(this.tabPageDocument);
            this.tabControlLearning.Controls.Add(this.tabPageLibraries);
            this.tabControlLearning.Name = "tabControlLearning";
            this.tabControlLearning.SelectedIndex = 0;
            this.tabControlLearning.TabStop = false;
            // 
            // tabPageDocument
            // 
            this.tabPageDocument.BackColor = System.Drawing.SystemColors.Control;
            this.tabPageDocument.Controls.Add(this.btnLearningDocBrowse);
            this.tabPageDocument.Controls.Add(this.textLearningDoc);
            this.tabPageDocument.Controls.Add(this.label5);
            resources.ApplyResources(this.tabPageDocument, "tabPageDocument");
            this.tabPageDocument.Name = "tabPageDocument";
            // 
            // btnLearningDocBrowse
            // 
            resources.ApplyResources(this.btnLearningDocBrowse, "btnLearningDocBrowse");
            this.btnLearningDocBrowse.Name = "btnLearningDocBrowse";
            this.btnLearningDocBrowse.UseVisualStyleBackColor = true;
            this.btnLearningDocBrowse.Click += new System.EventHandler(this.btnLearningDocBrowse_Click);
            // 
            // textLearningDoc
            // 
            resources.ApplyResources(this.textLearningDoc, "textLearningDoc");
            this.textLearningDoc.Name = "textLearningDoc";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // tabPageLibraries
            // 
            this.tabPageLibraries.BackColor = System.Drawing.SystemColors.Control;
            this.tabPageLibraries.Controls.Add(this.listLibraries);
            this.tabPageLibraries.Controls.Add(this.label3);
            resources.ApplyResources(this.tabPageLibraries, "tabPageLibraries");
            this.tabPageLibraries.Name = "tabPageLibraries";
            // 
            // listLibraries
            // 
            resources.ApplyResources(this.listLibraries, "listLibraries");
            this.listLibraries.FormattingEnabled = true;
            this.listLibraries.Name = "listLibraries";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // comboLearnFrom
            // 
            this.comboLearnFrom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLearnFrom.FormattingEnabled = true;
            this.comboLearnFrom.Items.AddRange(new object[] {
            resources.GetString("comboLearnFrom.Items"),
            resources.GetString("comboLearnFrom.Items1")});
            resources.ApplyResources(this.comboLearnFrom, "comboLearnFrom");
            this.comboLearnFrom.Name = "comboLearnFrom";
            this.comboLearnFrom.SelectedIndexChanged += new System.EventHandler(this.comboLearnFrom_SelectedIndexChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // tabAlphaSource
            // 
            this.tabAlphaSource.BackColor = System.Drawing.SystemColors.Control;
            resources.ApplyResources(this.tabAlphaSource, "tabAlphaSource");
            this.tabAlphaSource.Name = "tabAlphaSource";
            // 
            // BuildLibraryDlg
            // 
            this.AcceptButton = this.btnNext;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.tabControlMain);
            this.Controls.Add(this.btnPrevious);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnNext);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BuildLibraryDlg";
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BuildLibraryDlg_FormClosing);
            this.dataSourceGroupBox.ResumeLayout(false);
            this.dataSourceGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridInputFiles)).EndInit();
            this.tabControlMain.ResumeLayout(false);
            this.tabProperties.ResumeLayout(false);
            this.tabProperties.PerformLayout();
            this.tabControlDataSource.ResumeLayout(false);
            this.tabFilesSource.ResumeLayout(false);
            this.tabFilesSource.PerformLayout();
            this.tabKoinaSource.ResumeLayout(false);
            this.tabKoinaSource.PerformLayout();
            this.tabFiles.ResumeLayout(false);
            this.tabFiles.PerformLayout();
            this.tabLearn.ResumeLayout(false);
            this.tabLearn.PerformLayout();
            this.tabControlLearning.ResumeLayout(false);
            this.tabPageDocument.ResumeLayout(false);
            this.tabPageDocument.PerformLayout();
            this.tabPageLibraries.ResumeLayout(false);
            this.tabPageLibraries.PerformLayout();
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
        private System.Windows.Forms.Button btnAddDirectory;
        private System.Windows.Forms.Button btnAddFile;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.Button btnAddPaths;
        private System.Windows.Forms.ComboBox comboStandards;
        private System.Windows.Forms.GroupBox dataSourceGroupBox;
        private System.Windows.Forms.RadioButton radioKoinaSource;
        private System.Windows.Forms.RadioButton radioFilesSource;
        private System.Windows.Forms.LinkLabel koinaInfoSettingsBtn;
        private System.Windows.Forms.Label actionLabel;
        private System.Windows.Forms.ComboBox comboAction;
        private System.Windows.Forms.CheckBox cbIncludeAmbiguousMatches;
        private System.Windows.Forms.CheckBox cbKeepRedundant;
        private System.Windows.Forms.CheckBox cbFilter;
        private System.Windows.Forms.Label ceLabel;
        private System.Windows.Forms.ComboBox ceCombo;
        private FileUI.PeptideSearch.BuildLibraryGridView gridInputFiles;
        private pwiz.Skyline.Controls.WizardPages tabControlMain;
        private System.Windows.Forms.TabPage tabProperties;
        private System.Windows.Forms.TabPage tabFiles;
        private pwiz.Skyline.Controls.WizardPages tabControlDataSource;
        private System.Windows.Forms.TabPage tabFilesSource;
        private System.Windows.Forms.TabPage tabCarafeSource;
        private System.Windows.Forms.TabPage tabKoinaSource;
        private System.Windows.Forms.RadioButton radioAlphaSource;
        private System.Windows.Forms.TabPage tabLearn;
        private System.Windows.Forms.ComboBox comboLearnFrom;
        private System.Windows.Forms.Label label1;
        private pwiz.Skyline.Controls.WizardPages tabControlLearning;
        private System.Windows.Forms.TabPage tabPageLibraries;
        private System.Windows.Forms.TabPage tabPageDocument;
        private System.Windows.Forms.CheckedListBox listLibraries;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnLearningDocBrowse;
        private System.Windows.Forms.TextBox textLearningDoc;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.RadioButton radioCarafeSource;
        private System.Windows.Forms.TabPage tabAlphaSource;
    }
}
