using pwiz.Skyline.Controls;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class OspreySearchDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OspreySearchDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnBack = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.wizardPages = new WizardPages();
            this.titlePanel = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.dataFilesPage = new System.Windows.Forms.TabPage();
            this.lblDataFiles = new System.Windows.Forms.Label();
            this.fastaPage = new System.Windows.Forms.TabPage();
            this.lblFasta = new System.Windows.Forms.Label();
            this.cbUseExistingLibrary = new System.Windows.Forms.CheckBox();
            this.tbxLibraryPath = new System.Windows.Forms.TextBox();
            this.btnBrowseLibrary = new System.Windows.Forms.Button();
            this.libraryModelPage = new System.Windows.Forms.TabPage();
            this.lblLibraryModel = new System.Windows.Forms.Label();
            this.lblModel = new System.Windows.Forms.Label();
            this.comboModel = new System.Windows.Forms.ComboBox();
            this.radioUseExistingModel = new System.Windows.Forms.RadioButton();
            this.radioTuneModel = new System.Windows.Forms.RadioButton();
            this.listTuningFiles = new System.Windows.Forms.CheckedListBox();
            this.btnBrowseTuningFiles = new System.Windows.Forms.Button();
            this.cbChooseFromSearchedFiles = new System.Windows.Forms.CheckBox();
            this.cbUseGpu = new System.Windows.Forms.CheckBox();
            this.modificationsPage = new System.Windows.Forms.TabPage();
            this.lblModifications = new System.Windows.Forms.Label();
            this.lblFixedMods = new System.Windows.Forms.Label();
            this.listFixedMods = new System.Windows.Forms.ListBox();
            this.btnAddFixedMod = new System.Windows.Forms.Button();
            this.btnRemoveFixedMod = new System.Windows.Forms.Button();
            this.lblVariableMods = new System.Windows.Forms.Label();
            this.listVariableMods = new System.Windows.Forms.ListBox();
            this.btnAddVariableMod = new System.Windows.Forms.Button();
            this.btnRemoveVariableMod = new System.Windows.Forms.Button();
            this.searchSettingsPage = new System.Windows.Forms.TabPage();
            this.lblSearchSettings = new System.Windows.Forms.Label();
            this.lblResolution = new System.Windows.Forms.Label();
            this.comboResolution = new System.Windows.Forms.ComboBox();
            this.lblFragmentTolerance = new System.Windows.Forms.Label();
            this.numFragmentTolerance = new System.Windows.Forms.NumericUpDown();
            this.comboFragmentUnit = new System.Windows.Forms.ComboBox();
            this.lblRunFdr = new System.Windows.Forms.Label();
            this.txtRunFdr = new System.Windows.Forms.TextBox();
            this.lblExperimentFdr = new System.Windows.Forms.Label();
            this.txtExperimentFdr = new System.Windows.Forms.TextBox();
            this.lblProteinFdr = new System.Windows.Forms.Label();
            this.txtProteinFdr = new System.Windows.Forms.TextBox();
            this.lblThreads = new System.Windows.Forms.Label();
            this.numThreads = new System.Windows.Forms.NumericUpDown();
            this.runSearchPage = new System.Windows.Forms.TabPage();
            this.wizardPages.SuspendLayout();
            this.titlePanel.SuspendLayout();
            this.fastaPage.SuspendLayout();
            this.libraryModelPage.SuspendLayout();
            this.modificationsPage.SuspendLayout();
            this.searchSettingsPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFragmentTolerance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThreads)).BeginInit();
            this.SuspendLayout();
            //
            // btnCancel
            //
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // btnBack
            //
            resources.ApplyResources(this.btnBack, "btnBack");
            this.btnBack.Name = "btnBack";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);
            //
            // btnNext
            //
            resources.ApplyResources(this.btnNext, "btnNext");
            this.btnNext.Name = "btnNext";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
            //
            // wizardPages
            //
            resources.ApplyResources(this.wizardPages, "wizardPages");
            this.wizardPages.Controls.Add(this.dataFilesPage);
            this.wizardPages.Controls.Add(this.fastaPage);
            this.wizardPages.Controls.Add(this.libraryModelPage);
            this.wizardPages.Controls.Add(this.modificationsPage);
            this.wizardPages.Controls.Add(this.searchSettingsPage);
            this.wizardPages.Controls.Add(this.runSearchPage);
            this.wizardPages.Name = "wizardPages";
            this.wizardPages.SelectedIndex = 0;
            this.wizardPages.SelectedIndexChanged += new System.EventHandler(this.wizardPages_SelectedIndexChanged);
            //
            // titlePanel
            //
            this.titlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.titlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.titlePanel.Controls.Add(this.lblTitle);
            resources.ApplyResources(this.titlePanel, "titlePanel");
            this.titlePanel.Name = "titlePanel";
            //
            // lblTitle
            //
            resources.ApplyResources(this.lblTitle, "lblTitle");
            this.lblTitle.Name = "lblTitle";
            //
            // dataFilesPage
            //
            this.dataFilesPage.Controls.Add(this.lblDataFiles);
            resources.ApplyResources(this.dataFilesPage, "dataFilesPage");
            this.dataFilesPage.Name = "dataFilesPage";
            this.dataFilesPage.UseVisualStyleBackColor = true;
            //
            // lblDataFiles
            //
            resources.ApplyResources(this.lblDataFiles, "lblDataFiles");
            this.lblDataFiles.Name = "lblDataFiles";
            //
            // fastaPage
            //
            this.fastaPage.Controls.Add(this.btnBrowseLibrary);
            this.fastaPage.Controls.Add(this.tbxLibraryPath);
            this.fastaPage.Controls.Add(this.cbUseExistingLibrary);
            this.fastaPage.Controls.Add(this.lblFasta);
            resources.ApplyResources(this.fastaPage, "fastaPage");
            this.fastaPage.Name = "fastaPage";
            this.fastaPage.UseVisualStyleBackColor = true;
            //
            // lblFasta
            //
            resources.ApplyResources(this.lblFasta, "lblFasta");
            this.lblFasta.Name = "lblFasta";
            //
            // cbUseExistingLibrary
            //
            resources.ApplyResources(this.cbUseExistingLibrary, "cbUseExistingLibrary");
            this.cbUseExistingLibrary.Name = "cbUseExistingLibrary";
            this.cbUseExistingLibrary.UseVisualStyleBackColor = true;
            this.cbUseExistingLibrary.CheckedChanged += new System.EventHandler(this.cbUseExistingLibrary_CheckedChanged);
            //
            // tbxLibraryPath
            //
            resources.ApplyResources(this.tbxLibraryPath, "tbxLibraryPath");
            this.tbxLibraryPath.Name = "tbxLibraryPath";
            //
            // btnBrowseLibrary
            //
            resources.ApplyResources(this.btnBrowseLibrary, "btnBrowseLibrary");
            this.btnBrowseLibrary.Name = "btnBrowseLibrary";
            this.btnBrowseLibrary.UseVisualStyleBackColor = true;
            this.btnBrowseLibrary.Click += new System.EventHandler(this.btnBrowseLibrary_Click);
            //
            // libraryModelPage
            //
            this.libraryModelPage.Controls.Add(this.cbUseGpu);
            this.libraryModelPage.Controls.Add(this.cbChooseFromSearchedFiles);
            this.libraryModelPage.Controls.Add(this.btnBrowseTuningFiles);
            this.libraryModelPage.Controls.Add(this.listTuningFiles);
            this.libraryModelPage.Controls.Add(this.radioTuneModel);
            this.libraryModelPage.Controls.Add(this.radioUseExistingModel);
            this.libraryModelPage.Controls.Add(this.comboModel);
            this.libraryModelPage.Controls.Add(this.lblModel);
            this.libraryModelPage.Controls.Add(this.lblLibraryModel);
            resources.ApplyResources(this.libraryModelPage, "libraryModelPage");
            this.libraryModelPage.Name = "libraryModelPage";
            this.libraryModelPage.UseVisualStyleBackColor = true;
            //
            // lblLibraryModel
            //
            resources.ApplyResources(this.lblLibraryModel, "lblLibraryModel");
            this.lblLibraryModel.Name = "lblLibraryModel";
            //
            // lblModel
            //
            resources.ApplyResources(this.lblModel, "lblModel");
            this.lblModel.Name = "lblModel";
            //
            // comboModel
            //
            resources.ApplyResources(this.comboModel, "comboModel");
            this.comboModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboModel.FormattingEnabled = true;
            this.comboModel.Name = "comboModel";
            this.comboModel.SelectedIndexChanged += new System.EventHandler(this.comboModel_SelectedIndexChanged);
            //
            // radioUseExistingModel
            //
            resources.ApplyResources(this.radioUseExistingModel, "radioUseExistingModel");
            this.radioUseExistingModel.Checked = true;
            this.radioUseExistingModel.Name = "radioUseExistingModel";
            this.radioUseExistingModel.TabStop = true;
            this.radioUseExistingModel.UseVisualStyleBackColor = true;
            this.radioUseExistingModel.CheckedChanged += new System.EventHandler(this.radioModel_CheckedChanged);
            //
            // radioTuneModel
            //
            resources.ApplyResources(this.radioTuneModel, "radioTuneModel");
            this.radioTuneModel.Name = "radioTuneModel";
            this.radioTuneModel.UseVisualStyleBackColor = true;
            this.radioTuneModel.CheckedChanged += new System.EventHandler(this.radioModel_CheckedChanged);
            //
            // listTuningFiles
            //
            resources.ApplyResources(this.listTuningFiles, "listTuningFiles");
            this.listTuningFiles.CheckOnClick = true;
            this.listTuningFiles.FormattingEnabled = true;
            this.listTuningFiles.Name = "listTuningFiles";
            //
            // btnBrowseTuningFiles
            //
            resources.ApplyResources(this.btnBrowseTuningFiles, "btnBrowseTuningFiles");
            this.btnBrowseTuningFiles.Name = "btnBrowseTuningFiles";
            this.btnBrowseTuningFiles.UseVisualStyleBackColor = true;
            this.btnBrowseTuningFiles.Click += new System.EventHandler(this.btnBrowseTuningFiles_Click);
            //
            // cbChooseFromSearchedFiles
            //
            resources.ApplyResources(this.cbChooseFromSearchedFiles, "cbChooseFromSearchedFiles");
            this.cbChooseFromSearchedFiles.Name = "cbChooseFromSearchedFiles";
            this.cbChooseFromSearchedFiles.UseVisualStyleBackColor = true;
            this.cbChooseFromSearchedFiles.CheckedChanged += new System.EventHandler(this.cbChooseFromSearchedFiles_CheckedChanged);
            //
            // cbUseGpu
            //
            resources.ApplyResources(this.cbUseGpu, "cbUseGpu");
            this.cbUseGpu.Name = "cbUseGpu";
            this.cbUseGpu.UseVisualStyleBackColor = true;
            this.cbUseGpu.CheckedChanged += new System.EventHandler(this.cbUseGpu_CheckedChanged);
            //
            // modificationsPage
            //
            this.modificationsPage.Controls.Add(this.lblModifications);
            this.modificationsPage.Controls.Add(this.lblFixedMods);
            this.modificationsPage.Controls.Add(this.listFixedMods);
            this.modificationsPage.Controls.Add(this.btnAddFixedMod);
            this.modificationsPage.Controls.Add(this.btnRemoveFixedMod);
            this.modificationsPage.Controls.Add(this.lblVariableMods);
            this.modificationsPage.Controls.Add(this.listVariableMods);
            this.modificationsPage.Controls.Add(this.btnAddVariableMod);
            this.modificationsPage.Controls.Add(this.btnRemoveVariableMod);
            resources.ApplyResources(this.modificationsPage, "modificationsPage");
            this.modificationsPage.Name = "modificationsPage";
            this.modificationsPage.UseVisualStyleBackColor = true;
            //
            // lblModifications
            //
            resources.ApplyResources(this.lblModifications, "lblModifications");
            this.lblModifications.Name = "lblModifications";
            //
            // lblFixedMods
            //
            resources.ApplyResources(this.lblFixedMods, "lblFixedMods");
            this.lblFixedMods.Name = "lblFixedMods";
            //
            // listFixedMods
            //
            resources.ApplyResources(this.listFixedMods, "listFixedMods");
            this.listFixedMods.Name = "listFixedMods";
            //
            // btnAddFixedMod
            //
            resources.ApplyResources(this.btnAddFixedMod, "btnAddFixedMod");
            this.btnAddFixedMod.Name = "btnAddFixedMod";
            this.btnAddFixedMod.UseVisualStyleBackColor = true;
            this.btnAddFixedMod.Click += new System.EventHandler(this.btnAddFixedMod_Click);
            //
            // btnRemoveFixedMod
            //
            resources.ApplyResources(this.btnRemoveFixedMod, "btnRemoveFixedMod");
            this.btnRemoveFixedMod.Name = "btnRemoveFixedMod";
            this.btnRemoveFixedMod.UseVisualStyleBackColor = true;
            this.btnRemoveFixedMod.Click += new System.EventHandler(this.btnRemoveFixedMod_Click);
            //
            // lblVariableMods
            //
            resources.ApplyResources(this.lblVariableMods, "lblVariableMods");
            this.lblVariableMods.Name = "lblVariableMods";
            //
            // listVariableMods
            //
            resources.ApplyResources(this.listVariableMods, "listVariableMods");
            this.listVariableMods.Name = "listVariableMods";
            //
            // btnAddVariableMod
            //
            resources.ApplyResources(this.btnAddVariableMod, "btnAddVariableMod");
            this.btnAddVariableMod.Name = "btnAddVariableMod";
            this.btnAddVariableMod.UseVisualStyleBackColor = true;
            this.btnAddVariableMod.Click += new System.EventHandler(this.btnAddVariableMod_Click);
            //
            // btnRemoveVariableMod
            //
            resources.ApplyResources(this.btnRemoveVariableMod, "btnRemoveVariableMod");
            this.btnRemoveVariableMod.Name = "btnRemoveVariableMod";
            this.btnRemoveVariableMod.UseVisualStyleBackColor = true;
            this.btnRemoveVariableMod.Click += new System.EventHandler(this.btnRemoveVariableMod_Click);
            //
            // searchSettingsPage
            //
            this.searchSettingsPage.Controls.Add(this.numThreads);
            this.searchSettingsPage.Controls.Add(this.lblThreads);
            this.searchSettingsPage.Controls.Add(this.txtProteinFdr);
            this.searchSettingsPage.Controls.Add(this.lblProteinFdr);
            this.searchSettingsPage.Controls.Add(this.txtExperimentFdr);
            this.searchSettingsPage.Controls.Add(this.lblExperimentFdr);
            this.searchSettingsPage.Controls.Add(this.txtRunFdr);
            this.searchSettingsPage.Controls.Add(this.lblRunFdr);
            this.searchSettingsPage.Controls.Add(this.comboFragmentUnit);
            this.searchSettingsPage.Controls.Add(this.numFragmentTolerance);
            this.searchSettingsPage.Controls.Add(this.lblFragmentTolerance);
            this.searchSettingsPage.Controls.Add(this.comboResolution);
            this.searchSettingsPage.Controls.Add(this.lblResolution);
            this.searchSettingsPage.Controls.Add(this.lblSearchSettings);
            resources.ApplyResources(this.searchSettingsPage, "searchSettingsPage");
            this.searchSettingsPage.Name = "searchSettingsPage";
            this.searchSettingsPage.UseVisualStyleBackColor = true;
            //
            // lblSearchSettings
            //
            resources.ApplyResources(this.lblSearchSettings, "lblSearchSettings");
            this.lblSearchSettings.Name = "lblSearchSettings";
            //
            // lblResolution
            //
            resources.ApplyResources(this.lblResolution, "lblResolution");
            this.lblResolution.Name = "lblResolution";
            //
            // comboResolution
            //
            resources.ApplyResources(this.comboResolution, "comboResolution");
            this.comboResolution.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboResolution.FormattingEnabled = true;
            this.comboResolution.Name = "comboResolution";
            this.comboResolution.SelectedIndexChanged += new System.EventHandler(this.comboResolution_SelectedIndexChanged);
            //
            // lblFragmentTolerance
            //
            resources.ApplyResources(this.lblFragmentTolerance, "lblFragmentTolerance");
            this.lblFragmentTolerance.Name = "lblFragmentTolerance";
            //
            // numFragmentTolerance
            //
            resources.ApplyResources(this.numFragmentTolerance, "numFragmentTolerance");
            this.numFragmentTolerance.DecimalPlaces = 2;
            this.numFragmentTolerance.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numFragmentTolerance.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
            this.numFragmentTolerance.Name = "numFragmentTolerance";
            this.numFragmentTolerance.Value = new decimal(new int[] { 10, 0, 0, 0 });
            //
            // comboFragmentUnit
            //
            resources.ApplyResources(this.comboFragmentUnit, "comboFragmentUnit");
            this.comboFragmentUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFragmentUnit.FormattingEnabled = true;
            this.comboFragmentUnit.Name = "comboFragmentUnit";
            //
            // lblRunFdr
            //
            resources.ApplyResources(this.lblRunFdr, "lblRunFdr");
            this.lblRunFdr.Name = "lblRunFdr";
            //
            // txtRunFdr
            //
            resources.ApplyResources(this.txtRunFdr, "txtRunFdr");
            this.txtRunFdr.Name = "txtRunFdr";
            //
            // lblExperimentFdr
            //
            resources.ApplyResources(this.lblExperimentFdr, "lblExperimentFdr");
            this.lblExperimentFdr.Name = "lblExperimentFdr";
            //
            // txtExperimentFdr
            //
            resources.ApplyResources(this.txtExperimentFdr, "txtExperimentFdr");
            this.txtExperimentFdr.Name = "txtExperimentFdr";
            //
            // lblProteinFdr
            //
            resources.ApplyResources(this.lblProteinFdr, "lblProteinFdr");
            this.lblProteinFdr.Name = "lblProteinFdr";
            //
            // txtProteinFdr
            //
            resources.ApplyResources(this.txtProteinFdr, "txtProteinFdr");
            this.txtProteinFdr.Name = "txtProteinFdr";
            //
            // lblThreads
            //
            resources.ApplyResources(this.lblThreads, "lblThreads");
            this.lblThreads.Name = "lblThreads";
            //
            // numThreads
            //
            resources.ApplyResources(this.numThreads, "numThreads");
            this.numThreads.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numThreads.Maximum = new decimal(new int[] { 128, 0, 0, 0 });
            this.numThreads.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this.numThreads.Name = "numThreads";
            //
            // runSearchPage
            //
            resources.ApplyResources(this.runSearchPage, "runSearchPage");
            this.runSearchPage.Name = "runSearchPage";
            this.runSearchPage.UseVisualStyleBackColor = true;
            //
            // OspreySearchDlg
            //
            this.AcceptButton = this.btnNext;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.titlePanel);
            this.Controls.Add(this.wizardPages);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.btnNext);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OspreySearchDlg";
            this.ShowInTaskbar = false;
            this.wizardPages.ResumeLayout(false);
            this.titlePanel.ResumeLayout(false);
            this.titlePanel.PerformLayout();
            this.fastaPage.ResumeLayout(false);
            this.fastaPage.PerformLayout();
            this.libraryModelPage.ResumeLayout(false);
            this.libraryModelPage.PerformLayout();
            this.modificationsPage.ResumeLayout(false);
            this.searchSettingsPage.ResumeLayout(false);
            this.searchSettingsPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFragmentTolerance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThreads)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnNext;
        private WizardPages wizardPages;
        private System.Windows.Forms.Panel titlePanel;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TabPage dataFilesPage;
        private System.Windows.Forms.Label lblDataFiles;
        private System.Windows.Forms.TabPage fastaPage;
        private System.Windows.Forms.Label lblFasta;
        private System.Windows.Forms.CheckBox cbUseExistingLibrary;
        private System.Windows.Forms.TextBox tbxLibraryPath;
        private System.Windows.Forms.Button btnBrowseLibrary;
        private System.Windows.Forms.TabPage libraryModelPage;
        private System.Windows.Forms.Label lblLibraryModel;
        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.ComboBox comboModel;
        private System.Windows.Forms.RadioButton radioUseExistingModel;
        private System.Windows.Forms.RadioButton radioTuneModel;
        private System.Windows.Forms.CheckedListBox listTuningFiles;
        private System.Windows.Forms.Button btnBrowseTuningFiles;
        private System.Windows.Forms.CheckBox cbChooseFromSearchedFiles;
        private System.Windows.Forms.CheckBox cbUseGpu;
        private System.Windows.Forms.TabPage modificationsPage;
        private System.Windows.Forms.Label lblModifications;
        private System.Windows.Forms.Label lblFixedMods;
        private System.Windows.Forms.ListBox listFixedMods;
        private System.Windows.Forms.Button btnAddFixedMod;
        private System.Windows.Forms.Button btnRemoveFixedMod;
        private System.Windows.Forms.Label lblVariableMods;
        private System.Windows.Forms.ListBox listVariableMods;
        private System.Windows.Forms.Button btnAddVariableMod;
        private System.Windows.Forms.Button btnRemoveVariableMod;
        private System.Windows.Forms.TabPage searchSettingsPage;
        private System.Windows.Forms.Label lblSearchSettings;
        private System.Windows.Forms.Label lblResolution;
        private System.Windows.Forms.ComboBox comboResolution;
        private System.Windows.Forms.Label lblFragmentTolerance;
        private System.Windows.Forms.NumericUpDown numFragmentTolerance;
        private System.Windows.Forms.ComboBox comboFragmentUnit;
        private System.Windows.Forms.Label lblRunFdr;
        private System.Windows.Forms.TextBox txtRunFdr;
        private System.Windows.Forms.Label lblExperimentFdr;
        private System.Windows.Forms.TextBox txtExperimentFdr;
        private System.Windows.Forms.Label lblProteinFdr;
        private System.Windows.Forms.TextBox txtProteinFdr;
        private System.Windows.Forms.Label lblThreads;
        private System.Windows.Forms.NumericUpDown numThreads;
        private System.Windows.Forms.TabPage runSearchPage;
    }
}
