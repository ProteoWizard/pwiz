namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class ImportPeptideSearchDlg
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
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.cbMissedCleavages = new System.Windows.Forms.ComboBox();
            this.comboEnzyme = new System.Windows.Forms.ComboBox();
            this.comboBackgroundProteome = new System.Windows.Forms.ComboBox();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.comboBox2 = new System.Windows.Forms.ComboBox();
            this.comboBox3 = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.btnEarlyFinish = new System.Windows.Forms.Button();
            this.wizardPagesImportPeptideSearch = new pwiz.Skyline.Controls.WizardPages();
            this.buildSearchSpecLibPage = new System.Windows.Forms.TabPage();
            this.buildSpectralLibraryTitlePanel = new System.Windows.Forms.Panel();
            this.label14 = new System.Windows.Forms.Label();
            this.getChromatogramsPage = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label20 = new System.Windows.Forms.Label();
            this.matchModificationsPage = new System.Windows.Forms.TabPage();
            this.addModsTitlePanel = new System.Windows.Forms.Panel();
            this.label16 = new System.Windows.Forms.Label();
            this.ms1FullScanSettingsPage = new System.Windows.Forms.TabPage();
            this.ms1FullScanSettingsTitlePanel = new System.Windows.Forms.Panel();
            this.label19 = new System.Windows.Forms.Label();
            this.importFastaPage = new System.Windows.Forms.TabPage();
            this.importFASTATitlePanel = new System.Windows.Forms.Panel();
            this.lblImportFasta = new System.Windows.Forms.Label();
            this.wizardPagesImportPeptideSearch.SuspendLayout();
            this.buildSearchSpecLibPage.SuspendLayout();
            this.buildSpectralLibraryTitlePanel.SuspendLayout();
            this.getChromatogramsPage.SuspendLayout();
            this.panel1.SuspendLayout();
            this.matchModificationsPage.SuspendLayout();
            this.addModsTitlePanel.SuspendLayout();
            this.ms1FullScanSettingsPage.SuspendLayout();
            this.ms1FullScanSettingsTitlePanel.SuspendLayout();
            this.importFastaPage.SuspendLayout();
            this.importFASTATitlePanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(315, 401);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnNext
            // 
            this.btnNext.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNext.Location = new System.Drawing.Point(234, 401);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(75, 23);
            this.btnNext.TabIndex = 6;
            this.btnNext.Text = "&Next >";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 10000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // cbMissedCleavages
            // 
            this.cbMissedCleavages.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMissedCleavages.FormattingEnabled = true;
            this.cbMissedCleavages.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9"});
            this.cbMissedCleavages.Location = new System.Drawing.Point(28, 95);
            this.cbMissedCleavages.Name = "cbMissedCleavages";
            this.cbMissedCleavages.Size = new System.Drawing.Size(44, 21);
            this.cbMissedCleavages.TabIndex = 3;
            this.helpTip.SetToolTip(this.cbMissedCleavages, "The maximum number of missed cleavages allowed in a peptide when\r\nconsidering pro" +
        "tein digestion.");
            // 
            // comboEnzyme
            // 
            this.comboEnzyme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEnzyme.FormattingEnabled = true;
            this.comboEnzyme.Location = new System.Drawing.Point(28, 36);
            this.comboEnzyme.Name = "comboEnzyme";
            this.comboEnzyme.Size = new System.Drawing.Size(169, 21);
            this.comboEnzyme.TabIndex = 1;
            this.helpTip.SetToolTip(this.comboEnzyme, "The protease enzyme used for digesting proteins into peptides prior\r\nto injection" +
        " into the mass spectrometer\r\n");
            // 
            // comboBackgroundProteome
            // 
            this.comboBackgroundProteome.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBackgroundProteome.FormattingEnabled = true;
            this.comboBackgroundProteome.Location = new System.Drawing.Point(28, 158);
            this.comboBackgroundProteome.Name = "comboBackgroundProteome";
            this.comboBackgroundProteome.Size = new System.Drawing.Size(169, 21);
            this.comboBackgroundProteome.TabIndex = 5;
            this.helpTip.SetToolTip(this.comboBackgroundProteome, "Processed FASTA sequence information specifying the full\r\nset of proteins that ma" +
        "y be present in the sample matrix to be\r\ninjected into the mass spectrometer.");
            // 
            // comboBox1
            // 
            this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9"});
            this.comboBox1.Location = new System.Drawing.Point(28, 95);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(44, 21);
            this.comboBox1.TabIndex = 3;
            this.helpTip.SetToolTip(this.comboBox1, "The maximum number of missed cleavages allowed in a peptide when\r\nconsidering pro" +
        "tein digestion.");
            // 
            // comboBox2
            // 
            this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Location = new System.Drawing.Point(28, 36);
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new System.Drawing.Size(169, 21);
            this.comboBox2.TabIndex = 1;
            this.helpTip.SetToolTip(this.comboBox2, "The protease enzyme used for digesting proteins into peptides prior\r\nto injection" +
        " into the mass spectrometer\r\n");
            // 
            // comboBox3
            // 
            this.comboBox3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox3.FormattingEnabled = true;
            this.comboBox3.Location = new System.Drawing.Point(28, 158);
            this.comboBox3.Name = "comboBox3";
            this.comboBox3.Size = new System.Drawing.Size(169, 21);
            this.comboBox3.TabIndex = 5;
            this.helpTip.SetToolTip(this.comboBox3, "Processed FASTA sequence information specifying the full\r\nset of proteins that ma" +
        "y be present in the sample matrix to be\r\ninjected into the mass spectrometer.");
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(25, 79);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(117, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "&Max missed cleavages:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(25, 20);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(47, 13);
            this.label5.TabIndex = 0;
            this.label5.Text = "&Enzyme:";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(25, 142);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(115, 13);
            this.label15.TabIndex = 4;
            this.label15.Text = "&Background proteome:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(25, 79);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(117, 13);
            this.label6.TabIndex = 2;
            this.label6.Text = "&Max missed cleavages:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(25, 20);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(47, 13);
            this.label8.TabIndex = 0;
            this.label8.Text = "&Enzyme:";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(25, 142);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(115, 13);
            this.label9.TabIndex = 4;
            this.label9.Text = "&Background proteome:";
            // 
            // btnEarlyFinish
            // 
            this.btnEarlyFinish.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnEarlyFinish.Enabled = false;
            this.btnEarlyFinish.Location = new System.Drawing.Point(153, 401);
            this.btnEarlyFinish.Name = "btnEarlyFinish";
            this.btnEarlyFinish.Size = new System.Drawing.Size(75, 23);
            this.btnEarlyFinish.TabIndex = 9;
            this.btnEarlyFinish.Text = "&Finish";
            this.btnEarlyFinish.UseVisualStyleBackColor = true;
            this.btnEarlyFinish.Click += new System.EventHandler(this.btnEarlyFinish_Click);
            // 
            // wizardPagesImportPeptideSearch
            // 
            this.wizardPagesImportPeptideSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.wizardPagesImportPeptideSearch.Controls.Add(this.buildSearchSpecLibPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.getChromatogramsPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.matchModificationsPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.ms1FullScanSettingsPage);
            this.wizardPagesImportPeptideSearch.Controls.Add(this.importFastaPage);
            this.wizardPagesImportPeptideSearch.Location = new System.Drawing.Point(0, 0);
            this.wizardPagesImportPeptideSearch.Name = "wizardPagesImportPeptideSearch";
            this.wizardPagesImportPeptideSearch.SelectedIndex = 0;
            this.wizardPagesImportPeptideSearch.Size = new System.Drawing.Size(402, 395);
            this.wizardPagesImportPeptideSearch.TabIndex = 8;
            // 
            // buildSearchSpecLibPage
            // 
            this.buildSearchSpecLibPage.BackColor = System.Drawing.Color.Transparent;
            this.buildSearchSpecLibPage.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.buildSearchSpecLibPage.Controls.Add(this.buildSpectralLibraryTitlePanel);
            this.buildSearchSpecLibPage.Location = new System.Drawing.Point(4, 22);
            this.buildSearchSpecLibPage.Name = "buildSearchSpecLibPage";
            this.buildSearchSpecLibPage.Padding = new System.Windows.Forms.Padding(3);
            this.buildSearchSpecLibPage.Size = new System.Drawing.Size(394, 369);
            this.buildSearchSpecLibPage.TabIndex = 0;
            this.buildSearchSpecLibPage.Text = "1";
            this.buildSearchSpecLibPage.UseVisualStyleBackColor = true;
            // 
            // buildSpectralLibraryTitlePanel
            // 
            this.buildSpectralLibraryTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.buildSpectralLibraryTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.buildSpectralLibraryTitlePanel.Controls.Add(this.label14);
            this.buildSpectralLibraryTitlePanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.buildSpectralLibraryTitlePanel.Location = new System.Drawing.Point(3, 3);
            this.buildSpectralLibraryTitlePanel.Name = "buildSpectralLibraryTitlePanel";
            this.buildSpectralLibraryTitlePanel.Size = new System.Drawing.Size(388, 43);
            this.buildSpectralLibraryTitlePanel.TabIndex = 15;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Font = new System.Drawing.Font("Arial Rounded MT Bold", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label14.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label14.Location = new System.Drawing.Point(8, 9);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(181, 18);
            this.label14.TabIndex = 0;
            this.label14.Text = "Build Spectral Library";
            // 
            // getChromatogramsPage
            // 
            this.getChromatogramsPage.BackColor = System.Drawing.Color.Transparent;
            this.getChromatogramsPage.Controls.Add(this.panel1);
            this.getChromatogramsPage.Location = new System.Drawing.Point(4, 22);
            this.getChromatogramsPage.Name = "getChromatogramsPage";
            this.getChromatogramsPage.Padding = new System.Windows.Forms.Padding(3);
            this.getChromatogramsPage.Size = new System.Drawing.Size(394, 374);
            this.getChromatogramsPage.TabIndex = 1;
            this.getChromatogramsPage.Text = "2";
            this.getChromatogramsPage.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.GhostWhite;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel1.Controls.Add(this.label20);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(388, 43);
            this.panel1.TabIndex = 17;
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Font = new System.Drawing.Font("Arial Rounded MT Bold", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label20.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label20.Location = new System.Drawing.Point(12, 9);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(199, 18);
            this.label20.TabIndex = 0;
            this.label20.Text = "Extract Chromatograms";
            // 
            // matchModificationsPage
            // 
            this.matchModificationsPage.Controls.Add(this.addModsTitlePanel);
            this.matchModificationsPage.Location = new System.Drawing.Point(4, 22);
            this.matchModificationsPage.Name = "matchModificationsPage";
            this.matchModificationsPage.Padding = new System.Windows.Forms.Padding(3);
            this.matchModificationsPage.Size = new System.Drawing.Size(394, 374);
            this.matchModificationsPage.TabIndex = 3;
            this.matchModificationsPage.Text = "3";
            this.matchModificationsPage.UseVisualStyleBackColor = true;
            // 
            // addModsTitlePanel
            // 
            this.addModsTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.addModsTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.addModsTitlePanel.Controls.Add(this.label16);
            this.addModsTitlePanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.addModsTitlePanel.Location = new System.Drawing.Point(3, 3);
            this.addModsTitlePanel.Name = "addModsTitlePanel";
            this.addModsTitlePanel.Size = new System.Drawing.Size(388, 43);
            this.addModsTitlePanel.TabIndex = 0;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Font = new System.Drawing.Font("Arial Rounded MT Bold", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label16.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label16.Location = new System.Drawing.Point(12, 9);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(149, 18);
            this.label16.TabIndex = 0;
            this.label16.Text = "Add Modifications";
            // 
            // ms1FullScanSettingsPage
            // 
            this.ms1FullScanSettingsPage.Controls.Add(this.ms1FullScanSettingsTitlePanel);
            this.ms1FullScanSettingsPage.Location = new System.Drawing.Point(4, 22);
            this.ms1FullScanSettingsPage.Name = "ms1FullScanSettingsPage";
            this.ms1FullScanSettingsPage.Padding = new System.Windows.Forms.Padding(3);
            this.ms1FullScanSettingsPage.Size = new System.Drawing.Size(394, 374);
            this.ms1FullScanSettingsPage.TabIndex = 5;
            this.ms1FullScanSettingsPage.Text = "4";
            this.ms1FullScanSettingsPage.UseVisualStyleBackColor = true;
            // 
            // ms1FullScanSettingsTitlePanel
            // 
            this.ms1FullScanSettingsTitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.ms1FullScanSettingsTitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.ms1FullScanSettingsTitlePanel.Controls.Add(this.label19);
            this.ms1FullScanSettingsTitlePanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.ms1FullScanSettingsTitlePanel.Location = new System.Drawing.Point(3, 3);
            this.ms1FullScanSettingsTitlePanel.Name = "ms1FullScanSettingsTitlePanel";
            this.ms1FullScanSettingsTitlePanel.Size = new System.Drawing.Size(388, 43);
            this.ms1FullScanSettingsTitlePanel.TabIndex = 16;
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Font = new System.Drawing.Font("Arial Rounded MT Bold", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label19.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label19.Location = new System.Drawing.Point(12, 9);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(272, 18);
            this.label19.TabIndex = 0;
            this.label19.Text = "Configure MS1 Full-Scan Settings";
            // 
            // importFastaPage
            // 
            this.importFastaPage.Controls.Add(this.importFASTATitlePanel);
            this.importFastaPage.Location = new System.Drawing.Point(4, 22);
            this.importFastaPage.Name = "importFastaPage";
            this.importFastaPage.Padding = new System.Windows.Forms.Padding(3);
            this.importFastaPage.Size = new System.Drawing.Size(394, 374);
            this.importFastaPage.TabIndex = 4;
            this.importFastaPage.Text = "5";
            this.importFastaPage.UseVisualStyleBackColor = true;
            // 
            // importFASTATitlePanel
            // 
            this.importFASTATitlePanel.BackColor = System.Drawing.Color.GhostWhite;
            this.importFASTATitlePanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.importFASTATitlePanel.Controls.Add(this.lblImportFasta);
            this.importFASTATitlePanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.importFASTATitlePanel.Location = new System.Drawing.Point(3, 3);
            this.importFASTATitlePanel.Name = "importFASTATitlePanel";
            this.importFASTATitlePanel.Size = new System.Drawing.Size(388, 43);
            this.importFASTATitlePanel.TabIndex = 15;
            // 
            // lblImportFasta
            // 
            this.lblImportFasta.AutoSize = true;
            this.lblImportFasta.Font = new System.Drawing.Font("Arial Rounded MT Bold", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblImportFasta.Location = new System.Drawing.Point(12, 9);
            this.lblImportFasta.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblImportFasta.Name = "lblImportFasta";
            this.lblImportFasta.Size = new System.Drawing.Size(119, 18);
            this.lblImportFasta.TabIndex = 0;
            this.lblImportFasta.Text = "Import FASTA";
            // 
            // ImportPeptideSearchDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(402, 437);
            this.Controls.Add(this.btnEarlyFinish);
            this.Controls.Add(this.wizardPagesImportPeptideSearch);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnNext);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(416, 475);
            this.Name = "ImportPeptideSearchDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import Peptide Search";
            this.wizardPagesImportPeptideSearch.ResumeLayout(false);
            this.buildSearchSpecLibPage.ResumeLayout(false);
            this.buildSpectralLibraryTitlePanel.ResumeLayout(false);
            this.buildSpectralLibraryTitlePanel.PerformLayout();
            this.getChromatogramsPage.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.matchModificationsPage.ResumeLayout(false);
            this.addModsTitlePanel.ResumeLayout(false);
            this.addModsTitlePanel.PerformLayout();
            this.ms1FullScanSettingsPage.ResumeLayout(false);
            this.ms1FullScanSettingsTitlePanel.ResumeLayout(false);
            this.ms1FullScanSettingsTitlePanel.PerformLayout();
            this.importFastaPage.ResumeLayout(false);
            this.importFASTATitlePanel.ResumeLayout(false);
            this.importFASTATitlePanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.ToolTip helpTip;
        private pwiz.Skyline.Controls.WizardPages wizardPagesImportPeptideSearch;
        private System.Windows.Forms.TabPage buildSearchSpecLibPage;
        private System.Windows.Forms.TabPage getChromatogramsPage;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cbMissedCleavages;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboEnzyme;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.ComboBox comboBackgroundProteome;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox comboBox2;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox comboBox3;
        private System.Windows.Forms.TabPage matchModificationsPage;
        private System.Windows.Forms.TabPage importFastaPage;
        private System.Windows.Forms.TabPage ms1FullScanSettingsPage;
        private System.Windows.Forms.Panel addModsTitlePanel;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Panel importFASTATitlePanel;
        private System.Windows.Forms.Label lblImportFasta;
        private System.Windows.Forms.Panel ms1FullScanSettingsTitlePanel;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Button btnEarlyFinish;
        private System.Windows.Forms.Panel buildSpectralLibraryTitlePanel;
        private System.Windows.Forms.Label label14;

    }
}
