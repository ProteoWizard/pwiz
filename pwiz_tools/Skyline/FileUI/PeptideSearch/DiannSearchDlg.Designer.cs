using pwiz.Skyline.Controls;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class DiannSearchDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DiannSearchDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnBack = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.wizardPages = new WizardPages();
            this.dataFilesPage = new System.Windows.Forms.TabPage();
            this.lblDataFiles = new System.Windows.Forms.Label();
            this.fastaPage = new System.Windows.Forms.TabPage();
            this.lblFasta = new System.Windows.Forms.Label();
            this.searchSettingsPage = new System.Windows.Forms.TabPage();
            this.lblSearchSettings = new System.Windows.Forms.Label();
            this.cbMetExcision = new System.Windows.Forms.CheckBox();
            this.numMissedCleavages = new System.Windows.Forms.NumericUpDown();
            this.lblMissedCleavages = new System.Windows.Forms.Label();
            this.numThreads = new System.Windows.Forms.NumericUpDown();
            this.lblThreads = new System.Windows.Forms.Label();
            this.txtQValue = new System.Windows.Forms.TextBox();
            this.lblQValue = new System.Windows.Forms.Label();
            this.numMs2Tolerance = new System.Windows.Forms.NumericUpDown();
            this.lblMs2Tolerance = new System.Windows.Forms.Label();
            this.numMs1Tolerance = new System.Windows.Forms.NumericUpDown();
            this.lblMs1Tolerance = new System.Windows.Forms.Label();
            this.runSearchPage = new System.Windows.Forms.TabPage();
            this.wizardPages.SuspendLayout();
            this.searchSettingsPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMissedCleavages)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThreads)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMs2Tolerance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMs1Tolerance)).BeginInit();
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
            this.wizardPages.Controls.Add(this.searchSettingsPage);
            this.wizardPages.Controls.Add(this.runSearchPage);
            this.wizardPages.Name = "wizardPages";
            this.wizardPages.SelectedIndex = 0;
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
            // searchSettingsPage
            //
            this.searchSettingsPage.Controls.Add(this.cbMetExcision);
            this.searchSettingsPage.Controls.Add(this.numMissedCleavages);
            this.searchSettingsPage.Controls.Add(this.lblMissedCleavages);
            this.searchSettingsPage.Controls.Add(this.numThreads);
            this.searchSettingsPage.Controls.Add(this.lblThreads);
            this.searchSettingsPage.Controls.Add(this.txtQValue);
            this.searchSettingsPage.Controls.Add(this.lblQValue);
            this.searchSettingsPage.Controls.Add(this.numMs2Tolerance);
            this.searchSettingsPage.Controls.Add(this.lblMs2Tolerance);
            this.searchSettingsPage.Controls.Add(this.numMs1Tolerance);
            this.searchSettingsPage.Controls.Add(this.lblMs1Tolerance);
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
            // lblMs1Tolerance
            //
            resources.ApplyResources(this.lblMs1Tolerance, "lblMs1Tolerance");
            this.lblMs1Tolerance.Name = "lblMs1Tolerance";
            //
            // numMs1Tolerance
            //
            resources.ApplyResources(this.numMs1Tolerance, "numMs1Tolerance");
            this.numMs1Tolerance.DecimalPlaces = 1;
            this.numMs1Tolerance.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numMs1Tolerance.Name = "numMs1Tolerance";
            //
            // lblMs2Tolerance
            //
            resources.ApplyResources(this.lblMs2Tolerance, "lblMs2Tolerance");
            this.lblMs2Tolerance.Name = "lblMs2Tolerance";
            //
            // numMs2Tolerance
            //
            resources.ApplyResources(this.numMs2Tolerance, "numMs2Tolerance");
            this.numMs2Tolerance.DecimalPlaces = 1;
            this.numMs2Tolerance.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numMs2Tolerance.Name = "numMs2Tolerance";
            //
            // lblQValue
            //
            resources.ApplyResources(this.lblQValue, "lblQValue");
            this.lblQValue.Name = "lblQValue";
            //
            // txtQValue
            //
            resources.ApplyResources(this.txtQValue, "txtQValue");
            this.txtQValue.Name = "txtQValue";
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
            // lblMissedCleavages
            //
            resources.ApplyResources(this.lblMissedCleavages, "lblMissedCleavages");
            this.lblMissedCleavages.Name = "lblMissedCleavages";
            //
            // numMissedCleavages
            //
            resources.ApplyResources(this.numMissedCleavages, "numMissedCleavages");
            this.numMissedCleavages.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numMissedCleavages.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this.numMissedCleavages.Name = "numMissedCleavages";
            //
            // cbMetExcision
            //
            resources.ApplyResources(this.cbMetExcision, "cbMetExcision");
            this.cbMetExcision.Checked = true;
            this.cbMetExcision.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbMetExcision.Name = "cbMetExcision";
            this.cbMetExcision.UseVisualStyleBackColor = true;
            //
            // runSearchPage
            //
            resources.ApplyResources(this.runSearchPage, "runSearchPage");
            this.runSearchPage.Name = "runSearchPage";
            this.runSearchPage.UseVisualStyleBackColor = true;
            //
            // DiannSearchDlg
            //
            this.AcceptButton = this.btnNext;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.wizardPages);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.btnNext);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DiannSearchDlg";
            this.ShowInTaskbar = false;
            this.wizardPages.ResumeLayout(false);
            this.searchSettingsPage.ResumeLayout(false);
            this.searchSettingsPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMissedCleavages)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThreads)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMs2Tolerance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMs1Tolerance)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnNext;
        private WizardPages wizardPages;
        private System.Windows.Forms.TabPage dataFilesPage;
        private System.Windows.Forms.Label lblDataFiles;
        private System.Windows.Forms.TabPage fastaPage;
        private System.Windows.Forms.Label lblFasta;
        private System.Windows.Forms.TabPage searchSettingsPage;
        private System.Windows.Forms.Label lblSearchSettings;
        private System.Windows.Forms.Label lblMs1Tolerance;
        private System.Windows.Forms.NumericUpDown numMs1Tolerance;
        private System.Windows.Forms.Label lblMs2Tolerance;
        private System.Windows.Forms.NumericUpDown numMs2Tolerance;
        private System.Windows.Forms.Label lblQValue;
        private System.Windows.Forms.TextBox txtQValue;
        private System.Windows.Forms.Label lblThreads;
        private System.Windows.Forms.NumericUpDown numThreads;
        private System.Windows.Forms.Label lblMissedCleavages;
        private System.Windows.Forms.NumericUpDown numMissedCleavages;
        private System.Windows.Forms.CheckBox cbMetExcision;
        private System.Windows.Forms.TabPage runSearchPage;
    }
}
