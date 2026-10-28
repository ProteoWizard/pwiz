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
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.btnPrevious = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.tabFiles = new System.Windows.Forms.TabPage();
            this.btnAddDirectory = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.btnAddFile = new System.Windows.Forms.Button();
            this.btnAddPaths = new System.Windows.Forms.Button();
            this.gridInputFiles = new pwiz.Skyline.FileUI.PeptideSearch.BuildLibraryGridView();
            this.tabProperties = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.iRTPeptidesLabel = new System.Windows.Forms.Label();
            this.dataSourceGroupBox = new System.Windows.Forms.GroupBox();
            this.radioFilesSource = new System.Windows.Forms.RadioButton();
            this.radioKoinaSource = new System.Windows.Forms.RadioButton();
            this.koinaInfoSettingsBtn = new System.Windows.Forms.LinkLabel();
            this.radioAlphaSource = new System.Windows.Forms.RadioButton();
            this.comboStandards = new System.Windows.Forms.ComboBox();
            this.textPath = new System.Windows.Forms.TextBox();
            this.tabControlDataSource = new pwiz.Skyline.Controls.WizardPages();
            this.tabKoinaSource = new System.Windows.Forms.TabPage();
            this.ceCombo = new System.Windows.Forms.ComboBox();
            this.ceLabel = new System.Windows.Forms.Label();
            this.tabAlphaSource = new System.Windows.Forms.TabPage();
            this.groupAlphaOptions = new System.Windows.Forms.GroupBox();
            this.labelCarafeModel = new System.Windows.Forms.Label();
            this.comboCarafeModel = new System.Windows.Forms.ComboBox();
            this.cbUseGpu = new System.Windows.Forms.CheckBox();
            this.labelCarafePredictFor = new System.Windows.Forms.Label();
            this.comboCarafePredictFor = new System.Windows.Forms.ComboBox();
            this.cbCarafeFineTune = new System.Windows.Forms.CheckBox();
            this.tabFilesSource = new System.Windows.Forms.TabPage();
            this.cbFilter = new System.Windows.Forms.CheckBox();
            this.cbKeepRedundant = new System.Windows.Forms.CheckBox();
            this.comboAction = new System.Windows.Forms.ComboBox();
            this.actionLabel = new System.Windows.Forms.Label();
            this.cbIncludeAmbiguousMatches = new System.Windows.Forms.CheckBox();
            this.tabControlMain = new pwiz.Skyline.Controls.WizardPages();
            this.tabPredictFor = new System.Windows.Forms.TabPage();
            this.labelPredictFor = new System.Windows.Forms.Label();
            this.labelFastaPath = new System.Windows.Forms.Label();
            this.textFastaPath = new System.Windows.Forms.TextBox();
            this.btnBrowseFasta = new System.Windows.Forms.Button();
            this.cbAddDecoys = new System.Windows.Forms.CheckBox();
            this.cbAddEntrapment = new System.Windows.Forms.CheckBox();
            this.labelEntrapmentMethod = new System.Windows.Forms.Label();
            this.comboEntrapmentMethod = new System.Windows.Forms.ComboBox();
            this.textEntrapmentFasta = new System.Windows.Forms.TextBox();
            this.btnBrowseEntrapment = new System.Windows.Forms.Button();
            this.labelModWarning = new System.Windows.Forms.Label();
            this.labelKoinaPredictFor = new System.Windows.Forms.Label();
            this.comboKoinaPredictFor = new System.Windows.Forms.ComboBox();
            this.tabLibraryModel = new System.Windows.Forms.TabPage();
            this.labelLibraryModel = new System.Windows.Forms.Label();
            this.tabFasta = new System.Windows.Forms.TabPage();
            this.labelFasta = new System.Windows.Forms.Label();
            this.tabModifications = new System.Windows.Forms.TabPage();
            this.labelModifications = new System.Windows.Forms.Label();
            this.tabSearchSettings = new System.Windows.Forms.TabPage();
            this.labelSearchSettings = new System.Windows.Forms.Label();
            this.tabPredictFor.SuspendLayout();
            this.tabLibraryModel.SuspendLayout();
            this.tabFasta.SuspendLayout();
            this.tabModifications.SuspendLayout();
            this.tabSearchSettings.SuspendLayout();
            this.tabFiles.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridInputFiles)).BeginInit();
            this.tabProperties.SuspendLayout();
            this.dataSourceGroupBox.SuspendLayout();
            this.tabControlDataSource.SuspendLayout();
            this.tabKoinaSource.SuspendLayout();
            this.tabAlphaSource.SuspendLayout();
            this.groupAlphaOptions.SuspendLayout();
            this.tabFilesSource.SuspendLayout();
            this.tabControlMain.SuspendLayout();
            this.SuspendLayout();
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
            // btnAddDirectory
            // 
            resources.ApplyResources(this.btnAddDirectory, "btnAddDirectory");
            this.btnAddDirectory.Name = "btnAddDirectory";
            this.helpTip.SetToolTip(this.btnAddDirectory, resources.GetString("btnAddDirectory.ToolTip"));
            this.btnAddDirectory.UseVisualStyleBackColor = true;
            this.btnAddDirectory.Click += new System.EventHandler(this.btnAddDirectory_Click);
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // btnAddFile
            // 
            resources.ApplyResources(this.btnAddFile, "btnAddFile");
            this.btnAddFile.Name = "btnAddFile";
            this.helpTip.SetToolTip(this.btnAddFile, resources.GetString("btnAddFile.ToolTip"));
            this.btnAddFile.UseVisualStyleBackColor = true;
            this.btnAddFile.Click += new System.EventHandler(this.btnAddFile_Click);
            // 
            // btnAddPaths
            // 
            resources.ApplyResources(this.btnAddPaths, "btnAddPaths");
            this.btnAddPaths.Name = "btnAddPaths";
            this.btnAddPaths.UseVisualStyleBackColor = true;
            this.btnAddPaths.Click += new System.EventHandler(this.btnAddPaths_Click);
            // 
            // gridInputFiles
            // 
            this.gridInputFiles.AllowUserToAddRows = false;
            resources.ApplyResources(this.gridInputFiles, "gridInputFiles");
            this.gridInputFiles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridInputFiles.Name = "gridInputFiles";
            // 
            // tabProperties
            // 
            this.tabProperties.BackColor = System.Drawing.SystemColors.Control;
            this.tabProperties.Controls.Add(this.tabControlDataSource);
            this.tabProperties.Controls.Add(this.textPath);
            this.tabProperties.Controls.Add(this.textName);
            this.tabProperties.Controls.Add(this.comboStandards);
            this.tabProperties.Controls.Add(this.dataSourceGroupBox);
            this.tabProperties.Controls.Add(this.iRTPeptidesLabel);
            this.tabProperties.Controls.Add(this.label4);
            this.tabProperties.Controls.Add(this.btnBrowse);
            this.tabProperties.Controls.Add(this.label2);
            resources.ApplyResources(this.tabProperties, "tabProperties");
            this.tabProperties.Name = "tabProperties";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            this.helpTip.SetToolTip(this.textName, resources.GetString("textName.ToolTip"));
            this.textName.TextChanged += new System.EventHandler(this.textName_TextChanged);
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // iRTPeptidesLabel
            // 
            resources.ApplyResources(this.iRTPeptidesLabel, "iRTPeptidesLabel");
            this.iRTPeptidesLabel.Name = "iRTPeptidesLabel";
            this.modeUIHandler.SetUIMode(this.iRTPeptidesLabel, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            // 
            // dataSourceGroupBox
            // 
            this.dataSourceGroupBox.Controls.Add(this.radioAlphaSource);
            this.dataSourceGroupBox.Controls.Add(this.koinaInfoSettingsBtn);
            this.dataSourceGroupBox.Controls.Add(this.radioKoinaSource);
            this.dataSourceGroupBox.Controls.Add(this.radioFilesSource);
            resources.ApplyResources(this.dataSourceGroupBox, "dataSourceGroupBox");
            this.dataSourceGroupBox.Name = "dataSourceGroupBox";
            this.dataSourceGroupBox.TabStop = false;
            this.modeUIHandler.SetUIMode(this.dataSourceGroupBox, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
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
            // radioKoinaSource
            // 
            resources.ApplyResources(this.radioKoinaSource, "radioKoinaSource");
            this.radioKoinaSource.Name = "radioKoinaSource";
            this.radioKoinaSource.UseVisualStyleBackColor = true;
            this.radioKoinaSource.CheckedChanged += new System.EventHandler(this.dataSourceRadioButton_CheckedChanged);
            // 
            // koinaInfoSettingsBtn
            // 
            resources.ApplyResources(this.koinaInfoSettingsBtn, "koinaInfoSettingsBtn");
            this.koinaInfoSettingsBtn.Name = "koinaInfoSettingsBtn";
            this.koinaInfoSettingsBtn.TabStop = true;
            this.koinaInfoSettingsBtn.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.koinaInfoSettingsBtn_LinkClicked);
            // 
            // radioAlphaSource
            // 
            resources.ApplyResources(this.radioAlphaSource, "radioAlphaSource");
            this.radioAlphaSource.Name = "radioAlphaSource";
            this.radioAlphaSource.TabStop = true;
            this.radioAlphaSource.UseVisualStyleBackColor = true;
            this.radioAlphaSource.CheckedChanged += new System.EventHandler(this.dataSourceRadioButton_CheckedChanged);
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
            // textPath
            // 
            resources.ApplyResources(this.textPath, "textPath");
            this.textPath.Name = "textPath";
            this.helpTip.SetToolTip(this.textPath, resources.GetString("textPath.ToolTip"));
            this.textPath.TextChanged += new System.EventHandler(this.textPath_TextChanged);
            // 
            // tabControlDataSource
            // 
            resources.ApplyResources(this.tabControlDataSource, "tabControlDataSource");
            this.tabControlDataSource.Controls.Add(this.tabFilesSource);
            this.tabControlDataSource.Controls.Add(this.tabAlphaSource);
            this.tabControlDataSource.Controls.Add(this.tabKoinaSource);
            this.tabControlDataSource.Name = "tabControlDataSource";
            this.tabControlDataSource.SelectedIndex = 0;
            this.tabControlDataSource.TabStop = false;
            // 
            // tabKoinaSource
            // 
            this.tabKoinaSource.BackColor = System.Drawing.SystemColors.Control;
            this.tabKoinaSource.Controls.Add(this.comboKoinaPredictFor);
            this.tabKoinaSource.Controls.Add(this.labelKoinaPredictFor);
            this.tabKoinaSource.Controls.Add(this.ceLabel);
            this.tabKoinaSource.Controls.Add(this.ceCombo);
            resources.ApplyResources(this.tabKoinaSource, "tabKoinaSource");
            this.tabKoinaSource.Name = "tabKoinaSource";
            // 
            // ceCombo
            // 
            this.ceCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ceCombo.FormattingEnabled = true;
            resources.ApplyResources(this.ceCombo, "ceCombo");
            this.ceCombo.Name = "ceCombo";
            // 
            // ceLabel
            // 
            resources.ApplyResources(this.ceLabel, "ceLabel");
            this.ceLabel.Name = "ceLabel";
            //
            // labelKoinaPredictFor
            //
            resources.ApplyResources(this.labelKoinaPredictFor, "labelKoinaPredictFor");
            this.labelKoinaPredictFor.Name = "labelKoinaPredictFor";
            //
            // comboKoinaPredictFor
            //
            this.comboKoinaPredictFor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboKoinaPredictFor.FormattingEnabled = true;
            this.comboKoinaPredictFor.Items.AddRange(new object[] {
            resources.GetString("comboKoinaPredictFor.Items"),
            resources.GetString("comboKoinaPredictFor.Items1")});
            resources.ApplyResources(this.comboKoinaPredictFor, "comboKoinaPredictFor");
            this.comboKoinaPredictFor.Name = "comboKoinaPredictFor";
            this.comboKoinaPredictFor.SelectedIndexChanged += new System.EventHandler(this.comboPredictFor_SelectedIndexChanged);
            // 
            // tabAlphaSource
            // 
            this.tabAlphaSource.BackColor = System.Drawing.SystemColors.Control;
            this.tabAlphaSource.Controls.Add(this.groupAlphaOptions);
            resources.ApplyResources(this.tabAlphaSource, "tabAlphaSource");
            this.tabAlphaSource.Name = "tabAlphaSource";
            //
            // groupAlphaOptions
            //
            this.groupAlphaOptions.Controls.Add(this.labelModWarning);
            this.groupAlphaOptions.Controls.Add(this.cbCarafeFineTune);
            this.groupAlphaOptions.Controls.Add(this.comboCarafePredictFor);
            this.groupAlphaOptions.Controls.Add(this.labelCarafePredictFor);
            this.groupAlphaOptions.Controls.Add(this.cbUseGpu);
            this.groupAlphaOptions.Controls.Add(this.comboCarafeModel);
            this.groupAlphaOptions.Controls.Add(this.labelCarafeModel);
            resources.ApplyResources(this.groupAlphaOptions, "groupAlphaOptions");
            this.groupAlphaOptions.Name = "groupAlphaOptions";
            this.groupAlphaOptions.TabStop = false;
            //
            // cbCarafeFineTune
            //
            resources.ApplyResources(this.cbCarafeFineTune, "cbCarafeFineTune");
            this.cbCarafeFineTune.Name = "cbCarafeFineTune";
            this.cbCarafeFineTune.UseVisualStyleBackColor = true;
            this.cbCarafeFineTune.CheckedChanged += new System.EventHandler(this.cbCarafeFineTune_CheckedChanged);
            //
            // labelModWarning
            //
            resources.ApplyResources(this.labelModWarning, "labelModWarning");
            this.labelModWarning.AutoEllipsis = true;
            this.labelModWarning.ForeColor = System.Drawing.Color.DarkRed;
            this.labelModWarning.Name = "labelModWarning";
            this.labelModWarning.Visible = false;
            //
            // comboCarafePredictFor
            //
            this.comboCarafePredictFor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCarafePredictFor.FormattingEnabled = true;
            this.comboCarafePredictFor.Items.AddRange(new object[] {
            resources.GetString("comboCarafePredictFor.Items"),
            resources.GetString("comboCarafePredictFor.Items1")});
            resources.ApplyResources(this.comboCarafePredictFor, "comboCarafePredictFor");
            this.comboCarafePredictFor.Name = "comboCarafePredictFor";
            this.comboCarafePredictFor.SelectedIndexChanged += new System.EventHandler(this.comboPredictFor_SelectedIndexChanged);
            //
            // labelCarafePredictFor
            //
            resources.ApplyResources(this.labelCarafePredictFor, "labelCarafePredictFor");
            this.labelCarafePredictFor.Name = "labelCarafePredictFor";
            //
            // cbUseGpu
            //
            resources.ApplyResources(this.cbUseGpu, "cbUseGpu");
            this.cbUseGpu.Name = "cbUseGpu";
            this.cbUseGpu.UseVisualStyleBackColor = true;
            this.cbUseGpu.CheckedChanged += new System.EventHandler(this.cbUseGpu_CheckedChanged);
            //
            // comboCarafeModel
            //
            this.comboCarafeModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCarafeModel.FormattingEnabled = true;
            resources.ApplyResources(this.comboCarafeModel, "comboCarafeModel");
            this.comboCarafeModel.Name = "comboCarafeModel";
            this.comboCarafeModel.SelectedIndexChanged += new System.EventHandler(this.comboCarafeModel_SelectedIndexChanged);
            //
            // labelCarafeModel
            //
            resources.ApplyResources(this.labelCarafeModel, "labelCarafeModel");
            this.labelCarafeModel.Name = "labelCarafeModel";
            //
            // tabPredictFor
            //
            this.tabPredictFor.BackColor = System.Drawing.SystemColors.Control;
            this.tabPredictFor.Controls.Add(this.btnBrowseEntrapment);
            this.tabPredictFor.Controls.Add(this.textEntrapmentFasta);
            this.tabPredictFor.Controls.Add(this.comboEntrapmentMethod);
            this.tabPredictFor.Controls.Add(this.labelEntrapmentMethod);
            this.tabPredictFor.Controls.Add(this.cbAddEntrapment);
            this.tabPredictFor.Controls.Add(this.cbAddDecoys);
            this.tabPredictFor.Controls.Add(this.btnBrowseFasta);
            this.tabPredictFor.Controls.Add(this.textFastaPath);
            this.tabPredictFor.Controls.Add(this.labelFastaPath);
            this.tabPredictFor.Controls.Add(this.labelPredictFor);
            resources.ApplyResources(this.tabPredictFor, "tabPredictFor");
            this.tabPredictFor.Name = "tabPredictFor";
            //
            // labelPredictFor
            //
            resources.ApplyResources(this.labelPredictFor, "labelPredictFor");
            this.labelPredictFor.Name = "labelPredictFor";
            //
            // labelFastaPath
            //
            resources.ApplyResources(this.labelFastaPath, "labelFastaPath");
            this.labelFastaPath.Name = "labelFastaPath";
            //
            // textFastaPath
            //
            resources.ApplyResources(this.textFastaPath, "textFastaPath");
            this.textFastaPath.Name = "textFastaPath";
            //
            // btnBrowseFasta
            //
            resources.ApplyResources(this.btnBrowseFasta, "btnBrowseFasta");
            this.btnBrowseFasta.Name = "btnBrowseFasta";
            this.btnBrowseFasta.UseVisualStyleBackColor = true;
            this.btnBrowseFasta.Click += new System.EventHandler(this.btnBrowseFasta_Click);
            //
            // cbAddDecoys
            //
            resources.ApplyResources(this.cbAddDecoys, "cbAddDecoys");
            this.cbAddDecoys.Name = "cbAddDecoys";
            this.cbAddDecoys.UseVisualStyleBackColor = true;
            //
            // cbAddEntrapment
            //
            resources.ApplyResources(this.cbAddEntrapment, "cbAddEntrapment");
            this.cbAddEntrapment.Name = "cbAddEntrapment";
            this.cbAddEntrapment.UseVisualStyleBackColor = true;
            this.cbAddEntrapment.CheckedChanged += new System.EventHandler(this.cbAddEntrapment_CheckedChanged);
            //
            // labelEntrapmentMethod
            //
            resources.ApplyResources(this.labelEntrapmentMethod, "labelEntrapmentMethod");
            this.labelEntrapmentMethod.Name = "labelEntrapmentMethod";
            //
            // comboEntrapmentMethod
            //
            this.comboEntrapmentMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEntrapmentMethod.FormattingEnabled = true;
            this.comboEntrapmentMethod.Items.AddRange(new object[] {
            resources.GetString("comboEntrapmentMethod.Items"),
            resources.GetString("comboEntrapmentMethod.Items1")});
            resources.ApplyResources(this.comboEntrapmentMethod, "comboEntrapmentMethod");
            this.comboEntrapmentMethod.Name = "comboEntrapmentMethod";
            this.comboEntrapmentMethod.SelectedIndexChanged += new System.EventHandler(this.comboEntrapmentMethod_SelectedIndexChanged);
            //
            // textEntrapmentFasta
            //
            resources.ApplyResources(this.textEntrapmentFasta, "textEntrapmentFasta");
            this.textEntrapmentFasta.Name = "textEntrapmentFasta";
            //
            // btnBrowseEntrapment
            //
            resources.ApplyResources(this.btnBrowseEntrapment, "btnBrowseEntrapment");
            this.btnBrowseEntrapment.Name = "btnBrowseEntrapment";
            this.btnBrowseEntrapment.UseVisualStyleBackColor = true;
            this.btnBrowseEntrapment.Click += new System.EventHandler(this.btnBrowseEntrapment_Click);
            //
            // tabLibraryModel
            //
            this.tabLibraryModel.BackColor = System.Drawing.SystemColors.Control;
            this.tabLibraryModel.Controls.Add(this.labelLibraryModel);
            resources.ApplyResources(this.tabLibraryModel, "tabLibraryModel");
            this.tabLibraryModel.Name = "tabLibraryModel";
            //
            // labelLibraryModel
            //
            resources.ApplyResources(this.labelLibraryModel, "labelLibraryModel");
            this.labelLibraryModel.Name = "labelLibraryModel";
            //
            // tabFasta
            //
            this.tabFasta.BackColor = System.Drawing.SystemColors.Control;
            this.tabFasta.Controls.Add(this.labelFasta);
            resources.ApplyResources(this.tabFasta, "tabFasta");
            this.tabFasta.Name = "tabFasta";
            //
            // labelFasta
            //
            resources.ApplyResources(this.labelFasta, "labelFasta");
            this.labelFasta.Name = "labelFasta";
            //
            // tabModifications
            //
            this.tabModifications.BackColor = System.Drawing.SystemColors.Control;
            this.tabModifications.Controls.Add(this.labelModifications);
            resources.ApplyResources(this.tabModifications, "tabModifications");
            this.tabModifications.Name = "tabModifications";
            //
            // labelModifications
            //
            resources.ApplyResources(this.labelModifications, "labelModifications");
            this.labelModifications.Name = "labelModifications";
            //
            // tabSearchSettings
            //
            this.tabSearchSettings.BackColor = System.Drawing.SystemColors.Control;
            this.tabSearchSettings.Controls.Add(this.labelSearchSettings);
            resources.ApplyResources(this.tabSearchSettings, "tabSearchSettings");
            this.tabSearchSettings.Name = "tabSearchSettings";
            //
            // labelSearchSettings
            //
            resources.ApplyResources(this.labelSearchSettings, "labelSearchSettings");
            this.labelSearchSettings.Name = "labelSearchSettings";
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
            // cbFilter
            // 
            resources.ApplyResources(this.cbFilter, "cbFilter");
            this.cbFilter.Name = "cbFilter";
            this.helpTip.SetToolTip(this.cbFilter, resources.GetString("cbFilter.ToolTip"));
            this.cbFilter.UseVisualStyleBackColor = true;
            // 
            // cbKeepRedundant
            // 
            resources.ApplyResources(this.cbKeepRedundant, "cbKeepRedundant");
            this.cbKeepRedundant.Name = "cbKeepRedundant";
            this.helpTip.SetToolTip(this.cbKeepRedundant, resources.GetString("cbKeepRedundant.ToolTip"));
            this.cbKeepRedundant.UseVisualStyleBackColor = true;
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
            // actionLabel
            // 
            resources.ApplyResources(this.actionLabel, "actionLabel");
            this.actionLabel.Name = "actionLabel";
            // 
            // cbIncludeAmbiguousMatches
            // 
            resources.ApplyResources(this.cbIncludeAmbiguousMatches, "cbIncludeAmbiguousMatches");
            this.cbIncludeAmbiguousMatches.Name = "cbIncludeAmbiguousMatches";
            this.helpTip.SetToolTip(this.cbIncludeAmbiguousMatches, resources.GetString("cbIncludeAmbiguousMatches.ToolTip"));
            this.cbIncludeAmbiguousMatches.UseVisualStyleBackColor = true;
            // 
            // tabControlMain
            // 
            resources.ApplyResources(this.tabControlMain, "tabControlMain");
            this.tabControlMain.Controls.Add(this.tabProperties);
            this.tabControlMain.Controls.Add(this.tabFiles);
            this.tabControlMain.Controls.Add(this.tabPredictFor);
            this.tabControlMain.Controls.Add(this.tabLibraryModel);
            this.tabControlMain.Controls.Add(this.tabFasta);
            this.tabControlMain.Controls.Add(this.tabModifications);
            this.tabControlMain.Controls.Add(this.tabSearchSettings);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.TabStop = false;
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
            this.tabFiles.ResumeLayout(false);
            this.tabFiles.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridInputFiles)).EndInit();
            this.tabProperties.ResumeLayout(false);
            this.tabProperties.PerformLayout();
            this.dataSourceGroupBox.ResumeLayout(false);
            this.dataSourceGroupBox.PerformLayout();
            this.tabControlDataSource.ResumeLayout(false);
            this.tabKoinaSource.ResumeLayout(false);
            this.tabKoinaSource.PerformLayout();
            this.tabAlphaSource.ResumeLayout(false);
            this.groupAlphaOptions.ResumeLayout(false);
            this.groupAlphaOptions.PerformLayout();
            this.tabFilesSource.ResumeLayout(false);
            this.tabFilesSource.PerformLayout();
            this.tabControlMain.ResumeLayout(false);
            this.tabPredictFor.ResumeLayout(false);
            this.tabPredictFor.PerformLayout();
            this.tabLibraryModel.ResumeLayout(false);
            this.tabFasta.ResumeLayout(false);
            this.tabModifications.ResumeLayout(false);
            this.tabSearchSettings.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.TabPage tabFiles;
        private FileUI.PeptideSearch.BuildLibraryGridView gridInputFiles;
        private System.Windows.Forms.Button btnAddPaths;
        private System.Windows.Forms.Button btnAddFile;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button btnAddDirectory;
        private System.Windows.Forms.TabPage tabProperties;
        private Controls.WizardPages tabControlDataSource;
        private System.Windows.Forms.TabPage tabFilesSource;
        private System.Windows.Forms.CheckBox cbIncludeAmbiguousMatches;
        private System.Windows.Forms.Label actionLabel;
        private System.Windows.Forms.ComboBox comboAction;
        private System.Windows.Forms.CheckBox cbKeepRedundant;
        private System.Windows.Forms.CheckBox cbFilter;
        private System.Windows.Forms.TabPage tabAlphaSource;
        private System.Windows.Forms.GroupBox groupAlphaOptions;
        private System.Windows.Forms.Label labelCarafeModel;
        private System.Windows.Forms.ComboBox comboCarafeModel;
        private System.Windows.Forms.CheckBox cbUseGpu;
        private System.Windows.Forms.Label labelCarafePredictFor;
        private System.Windows.Forms.ComboBox comboCarafePredictFor;
        private System.Windows.Forms.CheckBox cbCarafeFineTune;
        private System.Windows.Forms.TabPage tabKoinaSource;
        private System.Windows.Forms.Label ceLabel;
        private System.Windows.Forms.ComboBox ceCombo;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.ComboBox comboStandards;
        private System.Windows.Forms.GroupBox dataSourceGroupBox;
        private System.Windows.Forms.RadioButton radioAlphaSource;
        private System.Windows.Forms.LinkLabel koinaInfoSettingsBtn;
        private System.Windows.Forms.RadioButton radioKoinaSource;
        private System.Windows.Forms.RadioButton radioFilesSource;
        private System.Windows.Forms.Label iRTPeptidesLabel;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label label2;
        private Controls.WizardPages tabControlMain;
        private System.Windows.Forms.TabPage tabPredictFor;
        private System.Windows.Forms.Label labelPredictFor;
        private System.Windows.Forms.Label labelFastaPath;
        private System.Windows.Forms.TextBox textFastaPath;
        private System.Windows.Forms.Button btnBrowseFasta;
        private System.Windows.Forms.CheckBox cbAddDecoys;
        private System.Windows.Forms.CheckBox cbAddEntrapment;
        private System.Windows.Forms.Label labelEntrapmentMethod;
        private System.Windows.Forms.ComboBox comboEntrapmentMethod;
        private System.Windows.Forms.TextBox textEntrapmentFasta;
        private System.Windows.Forms.Button btnBrowseEntrapment;
        private System.Windows.Forms.Label labelModWarning;
        private System.Windows.Forms.Label labelKoinaPredictFor;
        private System.Windows.Forms.ComboBox comboKoinaPredictFor;
        private System.Windows.Forms.TabPage tabLibraryModel;
        private System.Windows.Forms.Label labelLibraryModel;
        private System.Windows.Forms.TabPage tabFasta;
        private System.Windows.Forms.Label labelFasta;
        private System.Windows.Forms.TabPage tabModifications;
        private System.Windows.Forms.Label labelModifications;
        private System.Windows.Forms.TabPage tabSearchSettings;
        private System.Windows.Forms.Label labelSearchSettings;
    }
}
