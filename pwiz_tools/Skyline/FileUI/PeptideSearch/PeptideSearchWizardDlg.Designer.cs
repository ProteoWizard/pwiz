namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class PeptideSearchWizardDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PeptideSearchWizardDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.btnEarlyFinish = new System.Windows.Forms.Button();
            this.wizardPagesImportPeptideSearch = new pwiz.Skyline.Controls.WizardPages();
            this.buildSearchSpecLibPage = new System.Windows.Forms.TabPage();
            this.buildLibraryPanel = new System.Windows.Forms.Panel();
            this.buildSpectralLibraryTitlePanel = new System.Windows.Forms.Panel();
            this.label14 = new System.Windows.Forms.Label();
            this.getChromatogramsPage = new System.Windows.Forms.TabPage();
            this.extractChromatogramsTitlePanel = new System.Windows.Forms.Panel();
            this.label20 = new System.Windows.Forms.Label();
            this.matchModificationsPage = new System.Windows.Forms.TabPage();
            this.addModsTitlePanel = new System.Windows.Forms.Panel();
            this.label16 = new System.Windows.Forms.Label();
            this.transitionSettingsUiPage = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.ms1FullScanSettingsPage = new System.Windows.Forms.TabPage();
            this.ms1FullScanSettingsTitlePanel = new System.Windows.Forms.Panel();
            this.label19 = new System.Windows.Forms.Label();
            this.importFastaPage = new System.Windows.Forms.TabPage();
            this.importFASTATitlePanel = new System.Windows.Forms.Panel();
            this.lblFasta = new System.Windows.Forms.Label();
            this.converterSettingsPage = new System.Windows.Forms.TabPage();
            this.converterSettingsTitlePanel = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.ddaSearchSettingsPage = new System.Windows.Forms.TabPage();
            this.searchSettingsTitlePanel = new System.Windows.Forms.Panel();
            this.lblSearchSettings = new System.Windows.Forms.Label();
            this.ddaSearchPage = new System.Windows.Forms.TabPage();
            this.ddaSearchTitlePanel = new System.Windows.Forms.Panel();
            this.lblDDASearch = new System.Windows.Forms.Label();
            this.btnBack = new System.Windows.Forms.Button();
            this.lblSettingsPreset = new System.Windows.Forms.Label();
            this.cbSettingsPreset = new System.Windows.Forms.ComboBox();
            this.btnSavePreset = new System.Windows.Forms.Button();
            this.wizardPagesImportPeptideSearch.SuspendLayout();
            this.buildSearchSpecLibPage.SuspendLayout();
            this.buildSpectralLibraryTitlePanel.SuspendLayout();
            this.getChromatogramsPage.SuspendLayout();
            this.extractChromatogramsTitlePanel.SuspendLayout();
            this.matchModificationsPage.SuspendLayout();
            this.addModsTitlePanel.SuspendLayout();
            this.transitionSettingsUiPage.SuspendLayout();
            this.panel1.SuspendLayout();
            this.ms1FullScanSettingsPage.SuspendLayout();
            this.ms1FullScanSettingsTitlePanel.SuspendLayout();
            this.importFastaPage.SuspendLayout();
            this.importFASTATitlePanel.SuspendLayout();
            this.converterSettingsPage.SuspendLayout();
            this.converterSettingsTitlePanel.SuspendLayout();
            this.ddaSearchSettingsPage.SuspendLayout();
            this.searchSettingsTitlePanel.SuspendLayout();
            this.ddaSearchPage.SuspendLayout();
            this.ddaSearchTitlePanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnNext
            // 
            resources.ApplyResources(this.btnNext, "btnNext");
            this.btnNext.Name = "btnNext";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 10000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // btnEarlyFinish
            // 
            resources.ApplyResources(this.btnEarlyFinish, "btnEarlyFinish");
            this.btnEarlyFinish.Name = "btnEarlyFinish";
            this.btnEarlyFinish.UseVisualStyleBackColor = true;
            this.btnEarlyFinish.Click += new System.EventHandler(this.btnEarlyFinish_Click);
            // 
            // wizardPagesImportPeptideSearch
            // 
            resources.ApplyResources(this.wizardPagesImportPeptideSearch, "wizardPagesImportPeptideSearch");
            this.wizardPagesImportPeptideSearch.Controls.Add(this.buildSearchSpecLibPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.getChromatogramsPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.matchModificationsPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.transitionSettingsUiPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.ms1FullScanSettingsPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.importFastaPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.converterSettingsPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.ddaSearchSettingsPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.ddaSearchPage);
            this.wizardPagesImportPeptideSearch.Name = "wizardPagesImportPeptideSearch";
            this.wizardPagesImportPeptideSearch.SelectedIndex = 0;
            this.wizardPagesImportPeptideSearch.TabStop = false;
            // 
            // buildSearchSpecLibPage
            // 
            this.buildSearchSpecLibPage.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.buildSearchSpecLibPage, "buildSearchSpecLibPage");
            this.buildSearchSpecLibPage.Controls.Add(this.buildLibraryPanel);
            this.buildSearchSpecLibPage.Controls.Add(this.buildSpectralLibraryTitlePanel);
            this.buildSearchSpecLibPage.Name = "buildSearchSpecLibPage";
            this.buildSearchSpecLibPage.UseVisualStyleBackColor = true;
            // 
            // buildLibraryPanel
            // 
            resources.ApplyResources(this.buildLibraryPanel, "buildLibraryPanel");
            this.buildLibraryPanel.Name = "buildLibraryPanel";
            // 
            // buildSpectralLibraryTitlePanel
            // 
            this.buildSpectralLibraryTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.buildSpectralLibraryTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.buildSpectralLibraryTitlePanel.Controls.Add(this.label14);
            resources.ApplyResources(this.buildSpectralLibraryTitlePanel, "buildSpectralLibraryTitlePanel");
            this.buildSpectralLibraryTitlePanel.Name = "buildSpectralLibraryTitlePanel";
            // 
            // label14
            // 
            resources.ApplyResources(this.label14, "label14");
            this.label14.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label14.Name = "label14";
            // 
            // getChromatogramsPage
            // 
            this.getChromatogramsPage.BackColor = System.Drawing.Color.Transparent;
            this.getChromatogramsPage.Controls.Add(this.extractChromatogramsTitlePanel);
            resources.ApplyResources(this.getChromatogramsPage, "getChromatogramsPage");
            this.getChromatogramsPage.Name = "getChromatogramsPage";
            this.getChromatogramsPage.UseVisualStyleBackColor = true;
            // 
            // extractChromatogramsTitlePanel
            // 
            this.extractChromatogramsTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.extractChromatogramsTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.extractChromatogramsTitlePanel.Controls.Add(this.label20);
            resources.ApplyResources(this.extractChromatogramsTitlePanel, "extractChromatogramsTitlePanel");
            this.extractChromatogramsTitlePanel.Name = "extractChromatogramsTitlePanel";
            // 
            // label20
            // 
            resources.ApplyResources(this.label20, "label20");
            this.label20.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label20.Name = "label20";
            // 
            // matchModificationsPage
            // 
            this.matchModificationsPage.Controls.Add(this.addModsTitlePanel);
            resources.ApplyResources(this.matchModificationsPage, "matchModificationsPage");
            this.matchModificationsPage.Name = "matchModificationsPage";
            this.modeUIHandler.SetUIMode(this.matchModificationsPage, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.matchModificationsPage.UseVisualStyleBackColor = true;
            // 
            // addModsTitlePanel
            // 
            this.addModsTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.addModsTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.addModsTitlePanel.Controls.Add(this.label16);
            resources.ApplyResources(this.addModsTitlePanel, "addModsTitlePanel");
            this.addModsTitlePanel.Name = "addModsTitlePanel";
            // 
            // label16
            // 
            resources.ApplyResources(this.label16, "label16");
            this.label16.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label16.Name = "label16";
            // 
            // transitionSettingsUiPage
            // 
            this.transitionSettingsUiPage.Controls.Add(this.panel1);
            resources.ApplyResources(this.transitionSettingsUiPage, "transitionSettingsUiPage");
            this.transitionSettingsUiPage.Name = "transitionSettingsUiPage";
            this.modeUIHandler.SetUIMode(this.transitionSettingsUiPage, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.transitionSettingsUiPage.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.GhostWhite;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel1.Controls.Add(this.label1);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label1.Name = "label1";
            // 
            // ms1FullScanSettingsPage
            // 
            this.ms1FullScanSettingsPage.Controls.Add(this.ms1FullScanSettingsTitlePanel);
            resources.ApplyResources(this.ms1FullScanSettingsPage, "ms1FullScanSettingsPage");
            this.ms1FullScanSettingsPage.Name = "ms1FullScanSettingsPage";
            this.ms1FullScanSettingsPage.UseVisualStyleBackColor = true;
            // 
            // ms1FullScanSettingsTitlePanel
            // 
            this.ms1FullScanSettingsTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.ms1FullScanSettingsTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.ms1FullScanSettingsTitlePanel.Controls.Add(this.label19);
            resources.ApplyResources(this.ms1FullScanSettingsTitlePanel, "ms1FullScanSettingsTitlePanel");
            this.ms1FullScanSettingsTitlePanel.Name = "ms1FullScanSettingsTitlePanel";
            // 
            // label19
            // 
            resources.ApplyResources(this.label19, "label19");
            this.label19.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label19.Name = "label19";
            // 
            // importFastaPage
            // 
            this.importFastaPage.Controls.Add(this.importFASTATitlePanel);
            resources.ApplyResources(this.importFastaPage, "importFastaPage");
            this.importFastaPage.Name = "importFastaPage";
            this.modeUIHandler.SetUIMode(this.importFastaPage, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.importFastaPage.UseVisualStyleBackColor = true;
            // 
            // importFASTATitlePanel
            // 
            this.importFASTATitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.importFASTATitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.importFASTATitlePanel.Controls.Add(this.lblFasta);
            resources.ApplyResources(this.importFASTATitlePanel, "importFASTATitlePanel");
            this.importFASTATitlePanel.Name = "importFASTATitlePanel";
            // 
            // lblFasta
            // 
            resources.ApplyResources(this.lblFasta, "lblFasta");
            this.lblFasta.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblFasta.Name = "lblFasta";
            // 
            // converterSettingsPage
            // 
            this.converterSettingsPage.Controls.Add(this.converterSettingsTitlePanel);
            resources.ApplyResources(this.converterSettingsPage, "converterSettingsPage");
            this.converterSettingsPage.Name = "converterSettingsPage";
            this.converterSettingsPage.UseVisualStyleBackColor = true;
            // 
            // converterSettingsTitlePanel
            // 
            this.converterSettingsTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.converterSettingsTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.converterSettingsTitlePanel.Controls.Add(this.label2);
            resources.ApplyResources(this.converterSettingsTitlePanel, "converterSettingsTitlePanel");
            this.converterSettingsTitlePanel.Name = "converterSettingsTitlePanel";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label2.Name = "label2";
            // 
            // ddaSearchSettingsPage
            // 
            this.ddaSearchSettingsPage.Controls.Add(this.searchSettingsTitlePanel);
            resources.ApplyResources(this.ddaSearchSettingsPage, "ddaSearchSettingsPage");
            this.ddaSearchSettingsPage.Name = "ddaSearchSettingsPage";
            this.ddaSearchSettingsPage.UseVisualStyleBackColor = true;
            // 
            // searchSettingsTitlePanel
            // 
            this.searchSettingsTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.searchSettingsTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.searchSettingsTitlePanel.Controls.Add(this.lblSearchSettings);
            resources.ApplyResources(this.searchSettingsTitlePanel, "searchSettingsTitlePanel");
            this.searchSettingsTitlePanel.Name = "searchSettingsTitlePanel";
            // 
            // lblSearchSettings
            // 
            resources.ApplyResources(this.lblSearchSettings, "lblSearchSettings");
            this.lblSearchSettings.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblSearchSettings.Name = "lblSearchSettings";
            // 
            // ddaSearchPage
            // 
            this.ddaSearchPage.Controls.Add(this.ddaSearchTitlePanel);
            resources.ApplyResources(this.ddaSearchPage, "ddaSearchPage");
            this.ddaSearchPage.Name = "ddaSearchPage";
            this.ddaSearchPage.UseVisualStyleBackColor = true;
            // 
            // ddaSearchTitlePanel
            // 
            this.ddaSearchTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.ddaSearchTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.ddaSearchTitlePanel.Controls.Add(this.lblDDASearch);
            resources.ApplyResources(this.ddaSearchTitlePanel, "ddaSearchTitlePanel");
            this.ddaSearchTitlePanel.Name = "ddaSearchTitlePanel";
            // 
            // lblDDASearch
            // 
            resources.ApplyResources(this.lblDDASearch, "lblDDASearch");
            this.lblDDASearch.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblDDASearch.Name = "lblDDASearch";
            // 
            // btnBack
            // 
            resources.ApplyResources(this.btnBack, "btnBack");
            this.btnBack.Name = "btnBack";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);
            // 
            // lblSettingsPreset
            // 
            resources.ApplyResources(this.lblSettingsPreset, "lblSettingsPreset");
            this.lblSettingsPreset.Name = "lblSettingsPreset";
            // 
            // cbSettingsPreset
            // 
            resources.ApplyResources(this.cbSettingsPreset, "cbSettingsPreset");
            this.cbSettingsPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbSettingsPreset.FormattingEnabled = true;
            this.cbSettingsPreset.Name = "cbSettingsPreset";
            // 
            // btnSavePreset
            // 
            resources.ApplyResources(this.btnSavePreset, "btnSavePreset");
            this.btnSavePreset.Name = "btnSavePreset";
            this.btnSavePreset.UseVisualStyleBackColor = true;
            // 
            // PeptideSearchWizardDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnSavePreset);
            this.Controls.Add(this.cbSettingsPreset);
            this.Controls.Add(this.lblSettingsPreset);
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.btnEarlyFinish);
            this.Controls.Add(this.wizardPagesImportPeptideSearch);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnNext);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PeptideSearchWizardDlg";
            this.ShowInTaskbar = false;
            this.wizardPagesImportPeptideSearch.ResumeLayout(false);
            this.buildSearchSpecLibPage.ResumeLayout(false);
            this.buildSpectralLibraryTitlePanel.ResumeLayout(false);
            this.buildSpectralLibraryTitlePanel.PerformLayout();
            this.getChromatogramsPage.ResumeLayout(false);
            this.extractChromatogramsTitlePanel.ResumeLayout(false);
            this.extractChromatogramsTitlePanel.PerformLayout();
            this.matchModificationsPage.ResumeLayout(false);
            this.addModsTitlePanel.ResumeLayout(false);
            this.addModsTitlePanel.PerformLayout();
            this.transitionSettingsUiPage.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ms1FullScanSettingsPage.ResumeLayout(false);
            this.ms1FullScanSettingsTitlePanel.ResumeLayout(false);
            this.ms1FullScanSettingsTitlePanel.PerformLayout();
            this.importFastaPage.ResumeLayout(false);
            this.importFASTATitlePanel.ResumeLayout(false);
            this.importFASTATitlePanel.PerformLayout();
            this.converterSettingsPage.ResumeLayout(false);
            this.converterSettingsTitlePanel.ResumeLayout(false);
            this.converterSettingsTitlePanel.PerformLayout();
            this.ddaSearchSettingsPage.ResumeLayout(false);
            this.searchSettingsTitlePanel.ResumeLayout(false);
            this.searchSettingsTitlePanel.PerformLayout();
            this.ddaSearchPage.ResumeLayout(false);
            this.ddaSearchTitlePanel.ResumeLayout(false);
            this.ddaSearchTitlePanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        protected System.Windows.Forms.Button btnCancel;
        protected System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.ToolTip helpTip;
        private pwiz.Skyline.Controls.WizardPages wizardPagesImportPeptideSearch;
        private System.Windows.Forms.TabPage buildSearchSpecLibPage;
        private System.Windows.Forms.TabPage getChromatogramsPage;
        private System.Windows.Forms.TabPage matchModificationsPage;
        private System.Windows.Forms.TabPage importFastaPage;
        private System.Windows.Forms.TabPage ms1FullScanSettingsPage;
        private System.Windows.Forms.Panel addModsTitlePanel;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Panel importFASTATitlePanel;
        private System.Windows.Forms.Label lblFasta;
        private System.Windows.Forms.Panel ms1FullScanSettingsTitlePanel;
        protected System.Windows.Forms.Label label19;
        private System.Windows.Forms.Panel extractChromatogramsTitlePanel;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Button btnEarlyFinish;
        protected System.Windows.Forms.Panel buildSpectralLibraryTitlePanel;
        protected System.Windows.Forms.Label label14;
        private System.Windows.Forms.TabPage transitionSettingsUiPage;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        protected System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Panel buildLibraryPanel;
        protected System.Windows.Forms.TabPage ddaSearchSettingsPage;
        private System.Windows.Forms.Panel searchSettingsTitlePanel;
        private System.Windows.Forms.Label lblSearchSettings;
        protected System.Windows.Forms.TabPage ddaSearchPage;
        private System.Windows.Forms.Panel ddaSearchTitlePanel;
        protected System.Windows.Forms.Label lblDDASearch;
        protected System.Windows.Forms.TabPage converterSettingsPage;
        private System.Windows.Forms.Panel converterSettingsTitlePanel;
        private System.Windows.Forms.Label label2;
        protected System.Windows.Forms.Label lblSettingsPreset;
        protected System.Windows.Forms.ComboBox cbSettingsPreset;
        protected System.Windows.Forms.Button btnSavePreset;
    }
}
