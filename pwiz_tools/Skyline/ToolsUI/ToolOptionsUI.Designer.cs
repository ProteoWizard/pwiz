namespace pwiz.Skyline.ToolsUI
{
    partial class ToolOptionsUI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ToolOptionsUI));
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabAlphaPeptDeep = new System.Windows.Forms.TabPage();
            this.btnAlphaPeptDeepUserSettings = new System.Windows.Forms.Button();
            this.tabCarafe = new System.Windows.Forms.TabPage();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabCarafeData = new System.Windows.Forms.TabPage();
            this.lnkCarafeTrainingDataHelp = new System.Windows.Forms.LinkLabel();
            this.tabCarafeModel = new System.Windows.Forms.TabPage();
            this.lnkCarafeModelTrainingHelp = new System.Windows.Forms.LinkLabel();
            this.tabCarafeLibrary = new System.Windows.Forms.TabPage();
            this.lnkCarafeLibraryHelp = new System.Windows.Forms.LinkLabel();
            this.btnCarafeLibraryGenerationSettings = new System.Windows.Forms.Button();
            this.btnCarafeModelTrainingSettings = new System.Windows.Forms.Button();
            this.btnCarafeTrainingDataGenerationSettings = new System.Windows.Forms.Button();
            this.tabDisplay = new System.Windows.Forms.TabPage();
            this.comboColorScheme = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.powerOfTenCheckBox = new System.Windows.Forms.CheckBox();
            this.tabKoina = new System.Windows.Forms.TabPage();
            this.tbxKoinaServer = new System.Windows.Forms.TextBox();
            this.ceLabel = new System.Windows.Forms.Label();
            this.ceCombo = new System.Windows.Forms.ComboBox();
            this.koinaServerStatusLabel = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.iRTModelCombo = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.intensityModelCombo = new System.Windows.Forms.ComboBox();
            this.koinaServerLabel = new System.Windows.Forms.Label();
            this.koinaDescrLabel = new System.Windows.Forms.LinkLabel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.tabLanguage = new System.Windows.Forms.TabPage();
            this.labelDisplayLanguage = new System.Windows.Forms.Label();
            this.listBoxLanguages = new System.Windows.Forms.ListBox();
            this.tabMisc = new System.Windows.Forms.TabPage();
            this.tbxSettingsFilePath = new System.Windows.Forms.TextBox();
            this.lblSettingsPath = new System.Windows.Forms.Label();
            this.btnResetSettings = new System.Windows.Forms.Button();
            this.comboCompactFormatOption = new System.Windows.Forms.ComboBox();
            this.lblCompactDocumentFormat = new System.Windows.Forms.Label();
            this.checkBoxShowWizard = new System.Windows.Forms.CheckBox();
            this.tabPanorama = new System.Windows.Forms.TabPage();
            this.listboxServers = new System.Windows.Forms.ListBox();
            this.lblServers = new System.Windows.Forms.Label();
            this.btnEditServers = new System.Windows.Forms.Button();
            this.tabRemote = new System.Windows.Forms.TabPage();
            this.listBoxRemoteAccounts = new System.Windows.Forms.ListBox();
            this.lblRemoteAccounts = new System.Windows.Forms.Label();
            this.btnEditRemoteAccountList = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.tabControl.SuspendLayout();
            this.tabAlphaPeptDeep.SuspendLayout();
            this.tabCarafe.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabCarafeData.SuspendLayout();
            this.tabCarafeModel.SuspendLayout();
            this.tabCarafeLibrary.SuspendLayout();
            this.tabDisplay.SuspendLayout();
            this.tabKoina.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.tabLanguage.SuspendLayout();
            this.tabMisc.SuspendLayout();
            this.tabPanorama.SuspendLayout();
            this.tabRemote.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            resources.ApplyResources(this.tabControl, "tabControl");
            this.tabControl.Controls.Add(this.tabAlphaPeptDeep);
            this.tabControl.Controls.Add(this.tabCarafe);
            this.tabControl.Controls.Add(this.tabDisplay);
            this.tabControl.Controls.Add(this.tabKoina);
            this.tabControl.Controls.Add(this.tabLanguage);
            this.tabControl.Controls.Add(this.tabMisc);
            this.tabControl.Controls.Add(this.tabPanorama);
            this.tabControl.Controls.Add(this.tabRemote);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            // 
            // tabAlphaPeptDeep
            // 
            this.tabAlphaPeptDeep.Controls.Add(this.btnAlphaPeptDeepUserSettings);
            resources.ApplyResources(this.tabAlphaPeptDeep, "tabAlphaPeptDeep");
            this.tabAlphaPeptDeep.Name = "tabAlphaPeptDeep";
            this.tabAlphaPeptDeep.UseVisualStyleBackColor = true;
            this.tabAlphaPeptDeep.Click += new System.EventHandler(this.tabAlphaPeptDeep_Click);
            // 
            // btnAlphaPeptDeepUserSettings
            // 
            resources.ApplyResources(this.btnAlphaPeptDeepUserSettings, "btnAlphaPeptDeepUserSettings");
            this.btnAlphaPeptDeepUserSettings.Name = "btnAlphaPeptDeepUserSettings";
            this.btnAlphaPeptDeepUserSettings.UseVisualStyleBackColor = true;
            this.btnAlphaPeptDeepUserSettings.Click += new System.EventHandler(this.btnAlphaPeptDeepUserSettings_Click);
            // 
            // tabCarafe
            // 
            this.tabCarafe.Controls.Add(this.tabControl1);
            this.tabCarafe.Controls.Add(this.btnCarafeLibraryGenerationSettings);
            this.tabCarafe.Controls.Add(this.btnCarafeModelTrainingSettings);
            this.tabCarafe.Controls.Add(this.btnCarafeTrainingDataGenerationSettings);
            resources.ApplyResources(this.tabCarafe, "tabCarafe");
            this.tabCarafe.Name = "tabCarafe";
            this.tabCarafe.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Controls.Add(this.tabCarafeData);
            this.tabControl1.Controls.Add(this.tabCarafeModel);
            this.tabControl1.Controls.Add(this.tabCarafeLibrary);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            // 
            // tabCarafeData
            // 
            this.tabCarafeData.Controls.Add(this.lnkCarafeTrainingDataHelp);
            resources.ApplyResources(this.tabCarafeData, "tabCarafeData");
            this.tabCarafeData.Name = "tabCarafeData";
            this.tabCarafeData.UseVisualStyleBackColor = true;
            // 
            // lnkCarafeTrainingDataHelp
            // 
            resources.ApplyResources(this.lnkCarafeTrainingDataHelp, "lnkCarafeTrainingDataHelp");
            this.lnkCarafeTrainingDataHelp.Name = "lnkCarafeTrainingDataHelp";
            this.lnkCarafeTrainingDataHelp.TabStop = true;
            this.lnkCarafeTrainingDataHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkCarafeTrainingDataHelp_LinkClicked);
            this.lnkCarafeTrainingDataHelp.Click += new System.EventHandler(this.lnkCarafeHelp_Clicked);
            // 
            // tabCarafeModel
            // 
            this.tabCarafeModel.Controls.Add(this.lnkCarafeModelTrainingHelp);
            resources.ApplyResources(this.tabCarafeModel, "tabCarafeModel");
            this.tabCarafeModel.Name = "tabCarafeModel";
            this.tabCarafeModel.UseVisualStyleBackColor = true;
            // 
            // lnkCarafeModelTrainingHelp
            // 
            resources.ApplyResources(this.lnkCarafeModelTrainingHelp, "lnkCarafeModelTrainingHelp");
            this.lnkCarafeModelTrainingHelp.Name = "lnkCarafeModelTrainingHelp";
            this.lnkCarafeModelTrainingHelp.TabStop = true;
            this.lnkCarafeModelTrainingHelp.Click += new System.EventHandler(this.lnkCarafeHelp_Clicked);
            // 
            // tabCarafeLibrary
            // 
            this.tabCarafeLibrary.Controls.Add(this.lnkCarafeLibraryHelp);
            resources.ApplyResources(this.tabCarafeLibrary, "tabCarafeLibrary");
            this.tabCarafeLibrary.Name = "tabCarafeLibrary";
            this.tabCarafeLibrary.UseVisualStyleBackColor = true;
            // 
            // lnkCarafeLibraryHelp
            // 
            resources.ApplyResources(this.lnkCarafeLibraryHelp, "lnkCarafeLibraryHelp");
            this.lnkCarafeLibraryHelp.Name = "lnkCarafeLibraryHelp";
            this.lnkCarafeLibraryHelp.TabStop = true;
            this.lnkCarafeLibraryHelp.Click += new System.EventHandler(this.lnkCarafeHelp_Clicked);
            // 
            // btnCarafeLibraryGenerationSettings
            // 
            resources.ApplyResources(this.btnCarafeLibraryGenerationSettings, "btnCarafeLibraryGenerationSettings");
            this.btnCarafeLibraryGenerationSettings.Name = "btnCarafeLibraryGenerationSettings";
            this.btnCarafeLibraryGenerationSettings.UseVisualStyleBackColor = true;
            this.btnCarafeLibraryGenerationSettings.Click += new System.EventHandler(this.btnCarafeLibrarySettings_Click);
            // 
            // btnCarafeModelTrainingSettings
            // 
            resources.ApplyResources(this.btnCarafeModelTrainingSettings, "btnCarafeModelTrainingSettings");
            this.btnCarafeModelTrainingSettings.Name = "btnCarafeModelTrainingSettings";
            this.btnCarafeModelTrainingSettings.UseVisualStyleBackColor = true;
            this.btnCarafeModelTrainingSettings.Click += new System.EventHandler(this.btnCarafeModelTrainingSettings_Click);
            // 
            // btnCarafeTrainingDataGenerationSettings
            // 
            resources.ApplyResources(this.btnCarafeTrainingDataGenerationSettings, "btnCarafeTrainingDataGenerationSettings");
            this.btnCarafeTrainingDataGenerationSettings.Name = "btnCarafeTrainingDataGenerationSettings";
            this.btnCarafeTrainingDataGenerationSettings.UseVisualStyleBackColor = true;
            this.btnCarafeTrainingDataGenerationSettings.Click += new System.EventHandler(this.btnCarafeTrainingDataGenerationSettings_Click);
            // 
            // tabDisplay
            // 
            this.tabDisplay.Controls.Add(this.comboColorScheme);
            this.tabDisplay.Controls.Add(this.label1);
            this.tabDisplay.Controls.Add(this.powerOfTenCheckBox);
            resources.ApplyResources(this.tabDisplay, "tabDisplay");
            this.tabDisplay.Name = "tabDisplay";
            this.tabDisplay.UseVisualStyleBackColor = true;
            // 
            // comboColorScheme
            // 
            this.comboColorScheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboColorScheme.FormattingEnabled = true;
            resources.ApplyResources(this.comboColorScheme, "comboColorScheme");
            this.comboColorScheme.Name = "comboColorScheme";
            this.comboColorScheme.SelectedIndexChanged += new System.EventHandler(this.comboColorScheme_SelectedIndexChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // powerOfTenCheckBox
            // 
            resources.ApplyResources(this.powerOfTenCheckBox, "powerOfTenCheckBox");
            this.powerOfTenCheckBox.Name = "powerOfTenCheckBox";
            this.powerOfTenCheckBox.UseVisualStyleBackColor = true;
            // 
            // tabKoina
            // 
            this.tabKoina.Controls.Add(this.tbxKoinaServer);
            this.tabKoina.Controls.Add(this.ceLabel);
            this.tabKoina.Controls.Add(this.ceCombo);
            this.tabKoina.Controls.Add(this.koinaServerStatusLabel);
            this.tabKoina.Controls.Add(this.label3);
            this.tabKoina.Controls.Add(this.iRTModelCombo);
            this.tabKoina.Controls.Add(this.label2);
            this.tabKoina.Controls.Add(this.intensityModelCombo);
            this.tabKoina.Controls.Add(this.koinaServerLabel);
            this.tabKoina.Controls.Add(this.koinaDescrLabel);
            this.tabKoina.Controls.Add(this.pictureBox1);
            resources.ApplyResources(this.tabKoina, "tabKoina");
            this.tabKoina.Name = "tabKoina";
            this.tabKoina.UseVisualStyleBackColor = true;
            // 
            // tbxKoinaServer
            // 
            resources.ApplyResources(this.tbxKoinaServer, "tbxKoinaServer");
            this.tbxKoinaServer.Name = "tbxKoinaServer";
            this.tbxKoinaServer.ReadOnly = true;
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
            // koinaServerStatusLabel
            // 
            resources.ApplyResources(this.koinaServerStatusLabel, "koinaServerStatusLabel");
            this.koinaServerStatusLabel.Name = "koinaServerStatusLabel";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // iRTModelCombo
            // 
            this.iRTModelCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.iRTModelCombo.FormattingEnabled = true;
            resources.ApplyResources(this.iRTModelCombo, "iRTModelCombo");
            this.iRTModelCombo.Name = "iRTModelCombo";
            this.iRTModelCombo.SelectedIndexChanged += new System.EventHandler(this.iRTModelCombo_SelectedIndexChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // intensityModelCombo
            // 
            this.intensityModelCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.intensityModelCombo.FormattingEnabled = true;
            resources.ApplyResources(this.intensityModelCombo, "intensityModelCombo");
            this.intensityModelCombo.Name = "intensityModelCombo";
            this.intensityModelCombo.SelectedIndexChanged += new System.EventHandler(this.intensityModelCombo_SelectedIndexChanged);
            // 
            // koinaServerLabel
            // 
            resources.ApplyResources(this.koinaServerLabel, "koinaServerLabel");
            this.koinaServerLabel.Name = "koinaServerLabel";
            // 
            // koinaDescrLabel
            // 
            resources.ApplyResources(this.koinaDescrLabel, "koinaDescrLabel");
            this.koinaDescrLabel.Name = "koinaDescrLabel";
            this.koinaDescrLabel.TabStop = true;
            this.koinaDescrLabel.UseCompatibleTextRendering = true;
            this.koinaDescrLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.koinaDescrLabel_LinkClicked);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::pwiz.Skyline.Properties.Resources.koina_logo_fdf731d5;
            resources.ApplyResources(this.pictureBox1, "pictureBox1");
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.TabStop = false;
            // 
            // tabLanguage
            // 
            this.tabLanguage.Controls.Add(this.labelDisplayLanguage);
            this.tabLanguage.Controls.Add(this.listBoxLanguages);
            resources.ApplyResources(this.tabLanguage, "tabLanguage");
            this.tabLanguage.Name = "tabLanguage";
            this.tabLanguage.UseVisualStyleBackColor = true;
            // 
            // labelDisplayLanguage
            // 
            resources.ApplyResources(this.labelDisplayLanguage, "labelDisplayLanguage");
            this.labelDisplayLanguage.Name = "labelDisplayLanguage";
            // 
            // listBoxLanguages
            // 
            resources.ApplyResources(this.listBoxLanguages, "listBoxLanguages");
            this.listBoxLanguages.FormattingEnabled = true;
            this.listBoxLanguages.Name = "listBoxLanguages";
            // 
            // tabMisc
            // 
            this.tabMisc.Controls.Add(this.tbxSettingsFilePath);
            this.tabMisc.Controls.Add(this.lblSettingsPath);
            this.tabMisc.Controls.Add(this.btnResetSettings);
            this.tabMisc.Controls.Add(this.comboCompactFormatOption);
            this.tabMisc.Controls.Add(this.lblCompactDocumentFormat);
            this.tabMisc.Controls.Add(this.checkBoxShowWizard);
            resources.ApplyResources(this.tabMisc, "tabMisc");
            this.tabMisc.Name = "tabMisc";
            this.tabMisc.UseVisualStyleBackColor = true;
            // 
            // tbxSettingsFilePath
            // 
            resources.ApplyResources(this.tbxSettingsFilePath, "tbxSettingsFilePath");
            this.tbxSettingsFilePath.Name = "tbxSettingsFilePath";
            this.tbxSettingsFilePath.ReadOnly = true;
            // 
            // lblSettingsPath
            // 
            resources.ApplyResources(this.lblSettingsPath, "lblSettingsPath");
            this.lblSettingsPath.Name = "lblSettingsPath";
            // 
            // btnResetSettings
            // 
            resources.ApplyResources(this.btnResetSettings, "btnResetSettings");
            this.btnResetSettings.Name = "btnResetSettings";
            this.btnResetSettings.UseVisualStyleBackColor = true;
            this.btnResetSettings.Click += new System.EventHandler(this.btnResetSettings_Click);
            // 
            // comboCompactFormatOption
            // 
            this.comboCompactFormatOption.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCompactFormatOption.FormattingEnabled = true;
            resources.ApplyResources(this.comboCompactFormatOption, "comboCompactFormatOption");
            this.comboCompactFormatOption.Name = "comboCompactFormatOption";
            // 
            // lblCompactDocumentFormat
            // 
            resources.ApplyResources(this.lblCompactDocumentFormat, "lblCompactDocumentFormat");
            this.lblCompactDocumentFormat.Name = "lblCompactDocumentFormat";
            // 
            // checkBoxShowWizard
            // 
            resources.ApplyResources(this.checkBoxShowWizard, "checkBoxShowWizard");
            this.checkBoxShowWizard.Name = "checkBoxShowWizard";
            this.checkBoxShowWizard.UseVisualStyleBackColor = true;
            // 
            // tabPanorama
            // 
            this.tabPanorama.Controls.Add(this.listboxServers);
            this.tabPanorama.Controls.Add(this.lblServers);
            this.tabPanorama.Controls.Add(this.btnEditServers);
            resources.ApplyResources(this.tabPanorama, "tabPanorama");
            this.tabPanorama.Name = "tabPanorama";
            this.tabPanorama.UseVisualStyleBackColor = true;
            // 
            // listboxServers
            // 
            resources.ApplyResources(this.listboxServers, "listboxServers");
            this.listboxServers.FormattingEnabled = true;
            this.listboxServers.Name = "listboxServers";
            this.listboxServers.SelectionMode = System.Windows.Forms.SelectionMode.None;
            // 
            // lblServers
            // 
            resources.ApplyResources(this.lblServers, "lblServers");
            this.lblServers.Name = "lblServers";
            // 
            // btnEditServers
            // 
            resources.ApplyResources(this.btnEditServers, "btnEditServers");
            this.btnEditServers.Name = "btnEditServers";
            this.btnEditServers.UseVisualStyleBackColor = true;
            this.btnEditServers.Click += new System.EventHandler(this.btnEditServers_Click);
            // 
            // tabRemote
            // 
            this.tabRemote.Controls.Add(this.listBoxRemoteAccounts);
            this.tabRemote.Controls.Add(this.lblRemoteAccounts);
            this.tabRemote.Controls.Add(this.btnEditRemoteAccountList);
            resources.ApplyResources(this.tabRemote, "tabRemote");
            this.tabRemote.Name = "tabRemote";
            this.tabRemote.UseVisualStyleBackColor = true;
            // 
            // listBoxRemoteAccounts
            // 
            resources.ApplyResources(this.listBoxRemoteAccounts, "listBoxRemoteAccounts");
            this.listBoxRemoteAccounts.FormattingEnabled = true;
            this.listBoxRemoteAccounts.Name = "listBoxRemoteAccounts";
            this.listBoxRemoteAccounts.SelectionMode = System.Windows.Forms.SelectionMode.None;
            // 
            // lblRemoteAccounts
            // 
            resources.ApplyResources(this.lblRemoteAccounts, "lblRemoteAccounts");
            this.lblRemoteAccounts.Name = "lblRemoteAccounts";
            // 
            // btnEditRemoteAccountList
            // 
            resources.ApplyResources(this.btnEditRemoteAccountList, "btnEditRemoteAccountList");
            this.btnEditRemoteAccountList.Name = "btnEditRemoteAccountList";
            this.btnEditRemoteAccountList.UseVisualStyleBackColor = true;
            this.btnEditRemoteAccountList.Click += new System.EventHandler(this.btnEditRemoteAccountList_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // ToolOptionsUI
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tabControl);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ToolOptionsUI";
            this.ShowInTaskbar = false;
            this.Shown += new System.EventHandler(this.ToolOptionsUI_Shown);
            this.tabControl.ResumeLayout(false);
            this.tabAlphaPeptDeep.ResumeLayout(false);
            this.tabCarafe.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabCarafeData.ResumeLayout(false);
            this.tabCarafeData.PerformLayout();
            this.tabCarafeModel.ResumeLayout(false);
            this.tabCarafeModel.PerformLayout();
            this.tabCarafeLibrary.ResumeLayout(false);
            this.tabCarafeLibrary.PerformLayout();
            this.tabDisplay.ResumeLayout(false);
            this.tabDisplay.PerformLayout();
            this.tabKoina.ResumeLayout(false);
            this.tabKoina.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.tabLanguage.ResumeLayout(false);
            this.tabMisc.ResumeLayout(false);
            this.tabMisc.PerformLayout();
            this.tabPanorama.ResumeLayout(false);
            this.tabPanorama.PerformLayout();
            this.tabRemote.ResumeLayout(false);
            this.tabRemote.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPanorama;
        private System.Windows.Forms.Button btnEditServers;
        private System.Windows.Forms.Label lblServers;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.ListBox listboxServers;
        private System.Windows.Forms.TabPage tabMisc;
        private System.Windows.Forms.TabPage tabLanguage;
        private System.Windows.Forms.ListBox listBoxLanguages;
        private System.Windows.Forms.Label labelDisplayLanguage;
        private System.Windows.Forms.TabPage tabRemote;
        private System.Windows.Forms.ListBox listBoxRemoteAccounts;
        private System.Windows.Forms.Label lblRemoteAccounts;
        private System.Windows.Forms.Button btnEditRemoteAccountList;
        private System.Windows.Forms.CheckBox checkBoxShowWizard;
        private System.Windows.Forms.TabPage tabDisplay;
        private System.Windows.Forms.CheckBox powerOfTenCheckBox;
        private System.Windows.Forms.ComboBox comboCompactFormatOption;
        private System.Windows.Forms.Label lblCompactDocumentFormat;
        private System.Windows.Forms.ComboBox comboColorScheme;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnResetSettings;
        private System.Windows.Forms.TabPage tabKoina;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.LinkLabel koinaDescrLabel;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox iRTModelCombo;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox intensityModelCombo;
        private System.Windows.Forms.Label koinaServerLabel;
        private System.Windows.Forms.Label koinaServerStatusLabel;
        private System.Windows.Forms.Label ceLabel;
        private System.Windows.Forms.ComboBox ceCombo;
        private System.Windows.Forms.TextBox tbxKoinaServer;
        private System.Windows.Forms.TextBox tbxSettingsFilePath;
        private System.Windows.Forms.Label lblSettingsPath;
        private System.Windows.Forms.TabPage tabCarafe;
        private System.Windows.Forms.Button btnCarafeTrainingDataGenerationSettings;
        private System.Windows.Forms.Button btnCarafeLibraryGenerationSettings;
        private System.Windows.Forms.Button btnCarafeModelTrainingSettings;
        private System.Windows.Forms.LinkLabel lnkCarafeTrainingDataHelp;
        private System.Windows.Forms.LinkLabel lnkCarafeModelTrainingHelp;
        private System.Windows.Forms.LinkLabel lnkCarafeLibraryHelp;
        private System.Windows.Forms.TabPage tabAlphaPeptDeep;
        private System.Windows.Forms.Button btnAlphaPeptDeepUserSettings;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabCarafeData;
        private System.Windows.Forms.TabPage tabCarafeModel;
        private System.Windows.Forms.TabPage tabCarafeLibrary;
    }
}