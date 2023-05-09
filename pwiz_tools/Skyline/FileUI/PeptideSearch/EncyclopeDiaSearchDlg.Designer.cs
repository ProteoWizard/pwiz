/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2022 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using pwiz.Skyline.Controls;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class EncyclopeDiaSearchDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EncyclopeDiaSearchDlg));
            this.wizardPages = new pwiz.Skyline.Controls.WizardPages();
            this.fastaPage = new System.Windows.Forms.TabPage();
            this.fastaSettingsPanel = new System.Windows.Forms.Panel();
            this.lblFastaSettings = new System.Windows.Forms.Label();
            this.prositPage = new System.Windows.Forms.TabPage();
            this.prositPanel = new System.Windows.Forms.Panel();
            this.lblPrositPrediction = new System.Windows.Forms.Label();
            this.panelFilesProps = new System.Windows.Forms.Panel();
            this.maxMzCombo = new System.Windows.Forms.TextBox();
            this.minMzCombo = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.maxChargeUpDown = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.defaultChargeUpDown = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.minChargeUpDown = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.ceLabel = new System.Windows.Forms.Label();
            this.ceCombo = new System.Windows.Forms.ComboBox();
            this.cbIncludeAmbiguousMatches = new System.Windows.Forms.CheckBox();
            this.cbKeepRedundant = new System.Windows.Forms.CheckBox();
            this.narrowWindowPage = new System.Windows.Forms.TabPage();
            this.narrowWindowPanel = new System.Windows.Forms.Panel();
            this.lblNarrowWindowFiles = new System.Windows.Forms.Label();
            this.wideWindowPage = new System.Windows.Forms.TabPage();
            this.wideWindowPanel = new System.Windows.Forms.Panel();
            this.lblWideWindowFiles = new System.Windows.Forms.Label();
            this.encyclopediaPage = new System.Windows.Forms.TabPage();
            this.encyclopediaPanel = new System.Windows.Forms.Panel();
            this.lblEncyclopeDiaSettings = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.btnAdditionalSettings = new System.Windows.Forms.Button();
            this.txtMS2Tolerance = new System.Windows.Forms.TextBox();
            this.lblMs2Tolerance = new System.Windows.Forms.Label();
            this.cbMS2TolUnit = new System.Windows.Forms.ComboBox();
            this.txtMS1Tolerance = new System.Windows.Forms.TextBox();
            this.lblMs1Tolerance = new System.Windows.Forms.Label();
            this.cbMS1TolUnit = new System.Windows.Forms.ComboBox();
            this.runSearchPage = new System.Windows.Forms.TabPage();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.btnBack = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.wizardPages.SuspendLayout();
            this.fastaPage.SuspendLayout();
            this.fastaSettingsPanel.SuspendLayout();
            this.prositPage.SuspendLayout();
            this.prositPanel.SuspendLayout();
            this.panelFilesProps.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxChargeUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.defaultChargeUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.minChargeUpDown)).BeginInit();
            this.narrowWindowPage.SuspendLayout();
            this.narrowWindowPanel.SuspendLayout();
            this.wideWindowPage.SuspendLayout();
            this.wideWindowPanel.SuspendLayout();
            this.encyclopediaPage.SuspendLayout();
            this.encyclopediaPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // wizardPages
            // 
            resources.ApplyResources(this.wizardPages, "wizardPages");
            this.wizardPages.Controls.Add(this.fastaPage);
            this.wizardPages.Controls.Add(this.prositPage);
            this.wizardPages.Controls.Add(this.narrowWindowPage);
            this.wizardPages.Controls.Add(this.wideWindowPage);
            this.wizardPages.Controls.Add(this.encyclopediaPage);
            this.wizardPages.Controls.Add(this.runSearchPage);
            this.wizardPages.Name = "wizardPages";
            this.wizardPages.SelectedIndex = 0;
            // 
            // fastaPage
            // 
            this.fastaPage.Controls.Add(this.fastaSettingsPanel);
            resources.ApplyResources(this.fastaPage, "fastaPage");
            this.fastaPage.Name = "fastaPage";
            this.fastaPage.UseVisualStyleBackColor = true;
            // 
            // fastaSettingsPanel
            // 
            this.fastaSettingsPanel.BackColor = System.Drawing.Color.GhostWhite;
            this.fastaSettingsPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.fastaSettingsPanel.Controls.Add(this.lblFastaSettings);
            resources.ApplyResources(this.fastaSettingsPanel, "fastaSettingsPanel");
            this.fastaSettingsPanel.Name = "fastaSettingsPanel";
            // 
            // lblFastaSettings
            // 
            resources.ApplyResources(this.lblFastaSettings, "lblFastaSettings");
            this.lblFastaSettings.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblFastaSettings.Name = "lblFastaSettings";
            // 
            // prositPage
            // 
            this.prositPage.Controls.Add(this.panelFilesProps);
            this.prositPage.Controls.Add(this.prositPanel);
            resources.ApplyResources(this.prositPage, "prositPage");
            this.prositPage.Name = "prositPage";
            this.prositPage.UseVisualStyleBackColor = true;
            // 
            // prositPanel
            // 
            this.prositPanel.BackColor = System.Drawing.Color.GhostWhite;
            this.prositPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.prositPanel.Controls.Add(this.lblPrositPrediction);
            resources.ApplyResources(this.prositPanel, "prositPanel");
            this.prositPanel.Name = "prositPanel";
            // 
            // lblPrositPrediction
            // 
            resources.ApplyResources(this.lblPrositPrediction, "lblPrositPrediction");
            this.lblPrositPrediction.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblPrositPrediction.Name = "lblPrositPrediction";
            // 
            // panelFilesProps
            // 
            this.panelFilesProps.Controls.Add(this.label2);
            this.panelFilesProps.Controls.Add(this.defaultChargeUpDown);
            this.panelFilesProps.Controls.Add(this.maxMzCombo);
            this.panelFilesProps.Controls.Add(this.minMzCombo);
            this.panelFilesProps.Controls.Add(this.label4);
            this.panelFilesProps.Controls.Add(this.label5);
            this.panelFilesProps.Controls.Add(this.maxChargeUpDown);
            this.panelFilesProps.Controls.Add(this.label3);
            this.panelFilesProps.Controls.Add(this.minChargeUpDown);
            this.panelFilesProps.Controls.Add(this.label1);
            this.panelFilesProps.Controls.Add(this.ceLabel);
            this.panelFilesProps.Controls.Add(this.ceCombo);
            this.panelFilesProps.Controls.Add(this.cbIncludeAmbiguousMatches);
            this.panelFilesProps.Controls.Add(this.cbKeepRedundant);
            resources.ApplyResources(this.panelFilesProps, "panelFilesProps");
            this.panelFilesProps.Name = "panelFilesProps";
            // 
            // maxMzCombo
            // 
            resources.ApplyResources(this.maxMzCombo, "maxMzCombo");
            this.maxMzCombo.Name = "maxMzCombo";
            // 
            // minMzCombo
            // 
            resources.ApplyResources(this.minMzCombo, "minMzCombo");
            this.minMzCombo.Name = "minMzCombo";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // maxChargeUpDown
            // 
            resources.ApplyResources(this.maxChargeUpDown, "maxChargeUpDown");
            this.maxChargeUpDown.Maximum = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this.maxChargeUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.maxChargeUpDown.Name = "maxChargeUpDown";
            this.maxChargeUpDown.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // defaultChargeUpDown
            // 
            resources.ApplyResources(this.defaultChargeUpDown, "defaultChargeUpDown");
            this.defaultChargeUpDown.Maximum = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this.defaultChargeUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.defaultChargeUpDown.Name = "defaultChargeUpDown";
            this.defaultChargeUpDown.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // minChargeUpDown
            // 
            resources.ApplyResources(this.minChargeUpDown, "minChargeUpDown");
            this.minChargeUpDown.Maximum = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this.minChargeUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.minChargeUpDown.Name = "minChargeUpDown";
            this.minChargeUpDown.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
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
            this.cbKeepRedundant.UseVisualStyleBackColor = true;
            // 
            // narrowWindowPage
            // 
            this.narrowWindowPage.Controls.Add(this.narrowWindowPanel);
            resources.ApplyResources(this.narrowWindowPage, "narrowWindowPage");
            this.narrowWindowPage.Name = "narrowWindowPage";
            this.narrowWindowPage.UseVisualStyleBackColor = true;
            // 
            // narrowWindowPanel
            // 
            this.narrowWindowPanel.BackColor = System.Drawing.Color.GhostWhite;
            this.narrowWindowPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.narrowWindowPanel.Controls.Add(this.lblNarrowWindowFiles);
            resources.ApplyResources(this.narrowWindowPanel, "narrowWindowPanel");
            this.narrowWindowPanel.Name = "narrowWindowPanel";
            // 
            // lblNarrowWindowFiles
            // 
            resources.ApplyResources(this.lblNarrowWindowFiles, "lblNarrowWindowFiles");
            this.lblNarrowWindowFiles.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblNarrowWindowFiles.Name = "lblNarrowWindowFiles";
            // 
            // wideWindowPage
            // 
            this.wideWindowPage.Controls.Add(this.wideWindowPanel);
            resources.ApplyResources(this.wideWindowPage, "wideWindowPage");
            this.wideWindowPage.Name = "wideWindowPage";
            this.wideWindowPage.UseVisualStyleBackColor = true;
            // 
            // wideWindowPanel
            // 
            this.wideWindowPanel.BackColor = System.Drawing.Color.GhostWhite;
            this.wideWindowPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.wideWindowPanel.Controls.Add(this.lblWideWindowFiles);
            resources.ApplyResources(this.wideWindowPanel, "wideWindowPanel");
            this.wideWindowPanel.Name = "wideWindowPanel";
            // 
            // lblWideWindowFiles
            // 
            resources.ApplyResources(this.lblWideWindowFiles, "lblWideWindowFiles");
            this.lblWideWindowFiles.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblWideWindowFiles.Name = "lblWideWindowFiles";
            // 
            // encyclopediaPage
            // 
            this.encyclopediaPage.Controls.Add(this.encyclopediaPanel);
            this.encyclopediaPage.Controls.Add(this.label6);
            this.encyclopediaPage.Controls.Add(this.label7);
            this.encyclopediaPage.Controls.Add(this.btnAdditionalSettings);
            this.encyclopediaPage.Controls.Add(this.txtMS2Tolerance);
            this.encyclopediaPage.Controls.Add(this.lblMs2Tolerance);
            this.encyclopediaPage.Controls.Add(this.cbMS2TolUnit);
            this.encyclopediaPage.Controls.Add(this.txtMS1Tolerance);
            this.encyclopediaPage.Controls.Add(this.lblMs1Tolerance);
            this.encyclopediaPage.Controls.Add(this.cbMS1TolUnit);
            resources.ApplyResources(this.encyclopediaPage, "encyclopediaPage");
            this.encyclopediaPage.Name = "encyclopediaPage";
            this.encyclopediaPage.UseVisualStyleBackColor = true;
            // 
            // encyclopediaPanel
            // 
            this.encyclopediaPanel.BackColor = System.Drawing.Color.GhostWhite;
            this.encyclopediaPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.encyclopediaPanel.Controls.Add(this.lblEncyclopeDiaSettings);
            resources.ApplyResources(this.encyclopediaPanel, "encyclopediaPanel");
            this.encyclopediaPanel.Name = "encyclopediaPanel";
            // 
            // lblEncyclopeDiaSettings
            // 
            resources.ApplyResources(this.lblEncyclopeDiaSettings, "lblEncyclopeDiaSettings");
            this.lblEncyclopeDiaSettings.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblEncyclopeDiaSettings.Name = "lblEncyclopeDiaSettings";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // btnAdditionalSettings
            // 
            resources.ApplyResources(this.btnAdditionalSettings, "btnAdditionalSettings");
            this.btnAdditionalSettings.Name = "btnAdditionalSettings";
            this.btnAdditionalSettings.UseVisualStyleBackColor = true;
            this.btnAdditionalSettings.Click += new System.EventHandler(this.btnAdditionalSettings_Click);
            // 
            // txtMS2Tolerance
            // 
            resources.ApplyResources(this.txtMS2Tolerance, "txtMS2Tolerance");
            this.txtMS2Tolerance.Name = "txtMS2Tolerance";
            this.txtMS2Tolerance.Leave += new System.EventHandler(this.txtMS2Tolerance_LostFocus);
            // 
            // lblMs2Tolerance
            // 
            resources.ApplyResources(this.lblMs2Tolerance, "lblMs2Tolerance");
            this.lblMs2Tolerance.Name = "lblMs2Tolerance";
            // 
            // cbMS2TolUnit
            // 
            this.cbMS2TolUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMS2TolUnit.FormattingEnabled = true;
            resources.ApplyResources(this.cbMS2TolUnit, "cbMS2TolUnit");
            this.cbMS2TolUnit.Name = "cbMS2TolUnit";
            // 
            // txtMS1Tolerance
            // 
            resources.ApplyResources(this.txtMS1Tolerance, "txtMS1Tolerance");
            this.txtMS1Tolerance.Name = "txtMS1Tolerance";
            this.txtMS1Tolerance.Leave += new System.EventHandler(this.txtMS1Tolerance_LostFocus);
            // 
            // lblMs1Tolerance
            // 
            resources.ApplyResources(this.lblMs1Tolerance, "lblMs1Tolerance");
            this.lblMs1Tolerance.Name = "lblMs1Tolerance";
            // 
            // cbMS1TolUnit
            // 
            this.cbMS1TolUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMS1TolUnit.FormattingEnabled = true;
            resources.ApplyResources(this.cbMS1TolUnit, "cbMS1TolUnit");
            this.cbMS1TolUnit.Name = "cbMS1TolUnit";
            // 
            // runSearchPage
            // 
            resources.ApplyResources(this.runSearchPage, "runSearchPage");
            this.runSearchPage.Name = "runSearchPage";
            this.runSearchPage.UseVisualStyleBackColor = true;
            // 
            // btnBack
            // 
            resources.ApplyResources(this.btnBack, "btnBack");
            this.btnBack.Name = "btnBack";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);
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
            // EncyclopeDiaSearchDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.wizardPages);
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnNext);
            this.DoubleBuffered = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EncyclopeDiaSearchDlg";
            this.ShowInTaskbar = false;
            this.wizardPages.ResumeLayout(false);
            this.fastaPage.ResumeLayout(false);
            this.fastaSettingsPanel.ResumeLayout(false);
            this.prositPage.ResumeLayout(false);
            this.prositPanel.ResumeLayout(false);
            this.panelFilesProps.ResumeLayout(false);
            this.panelFilesProps.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxChargeUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.defaultChargeUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.minChargeUpDown)).EndInit();
            this.narrowWindowPage.ResumeLayout(false);
            this.narrowWindowPanel.ResumeLayout(false);
            this.wideWindowPage.ResumeLayout(false);
            this.wideWindowPanel.ResumeLayout(false);
            this.encyclopediaPage.ResumeLayout(false);
            this.encyclopediaPage.PerformLayout();
            this.encyclopediaPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private WizardPages wizardPages;
        private System.Windows.Forms.TabPage fastaPage;
        private System.Windows.Forms.TabPage prositPage;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.Panel panelFilesProps;
        private System.Windows.Forms.NumericUpDown minChargeUpDown;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label ceLabel;
        private System.Windows.Forms.ComboBox ceCombo;
        private System.Windows.Forms.CheckBox cbIncludeAmbiguousMatches;
        private System.Windows.Forms.CheckBox cbKeepRedundant;
        private System.Windows.Forms.NumericUpDown maxChargeUpDown;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown defaultChargeUpDown;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox maxMzCombo;
        private System.Windows.Forms.TextBox minMzCombo;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TabPage narrowWindowPage;
        private System.Windows.Forms.TabPage wideWindowPage;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.TabPage runSearchPage;
        private System.Windows.Forms.TabPage encyclopediaPage;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button btnAdditionalSettings;
        private System.Windows.Forms.TextBox txtMS2Tolerance;
        private System.Windows.Forms.Label lblMs2Tolerance;
        private System.Windows.Forms.ComboBox cbMS2TolUnit;
        private System.Windows.Forms.TextBox txtMS1Tolerance;
        private System.Windows.Forms.Label lblMs1Tolerance;
        private System.Windows.Forms.ComboBox cbMS1TolUnit;
        private System.Windows.Forms.Panel fastaSettingsPanel;
        private System.Windows.Forms.Label lblFastaSettings;
        private System.Windows.Forms.Panel prositPanel;
        private System.Windows.Forms.Label lblPrositPrediction;
        private System.Windows.Forms.Panel narrowWindowPanel;
        private System.Windows.Forms.Label lblNarrowWindowFiles;
        private System.Windows.Forms.Panel wideWindowPanel;
        private System.Windows.Forms.Label lblWideWindowFiles;
        private System.Windows.Forms.Panel encyclopediaPanel;
        private System.Windows.Forms.Label lblEncyclopeDiaSettings;
    }
}