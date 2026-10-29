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
            this.modificationsPage = new System.Windows.Forms.TabPage();
            this.lblModifications = new System.Windows.Forms.Label();
            this.searchSettingsPage = new System.Windows.Forms.TabPage();
            this.lblSearchSettings = new System.Windows.Forms.Label();
            this.runSearchPage = new System.Windows.Forms.TabPage();
            this.wizardPages.SuspendLayout();
            this.titlePanel.SuspendLayout();
            this.fastaPage.SuspendLayout();
            this.libraryModelPage.SuspendLayout();
            this.modificationsPage.SuspendLayout();
            this.searchSettingsPage.SuspendLayout();
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
            // modificationsPage
            //
            this.modificationsPage.Controls.Add(this.lblModifications);
            resources.ApplyResources(this.modificationsPage, "modificationsPage");
            this.modificationsPage.Name = "modificationsPage";
            this.modificationsPage.UseVisualStyleBackColor = true;
            //
            // lblModifications
            //
            resources.ApplyResources(this.lblModifications, "lblModifications");
            this.lblModifications.Name = "lblModifications";
            //
            // searchSettingsPage
            //
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
        private System.Windows.Forms.TabPage modificationsPage;
        private System.Windows.Forms.Label lblModifications;
        private System.Windows.Forms.TabPage searchSettingsPage;
        private System.Windows.Forms.Label lblSearchSettings;
        private System.Windows.Forms.TabPage runSearchPage;
    }
}
