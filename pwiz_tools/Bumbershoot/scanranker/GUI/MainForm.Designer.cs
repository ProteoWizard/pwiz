namespace ScanRanker
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.lblInputFileName = new System.Windows.Forms.Label();
            this.tbInputFile = new System.Windows.Forms.TextBox();
            this.btnSrcFileBrowse = new System.Windows.Forms.Button();
            this.gbAssessment = new System.Windows.Forms.GroupBox();
            this.tbOutputMetrics = new System.Windows.Forms.TextBox();
            this.lblOutputMetrics = new System.Windows.Forms.Label();
            this.gbDtgConfig = new System.Windows.Forms.GroupBox();
            this.cbWriteOutTags = new System.Windows.Forms.CheckBox();
            this.tbIsotopeTolerance = new System.Windows.Forms.TextBox();
            this.lblIsotopeTolerance = new System.Windows.Forms.Label();
            this.cbUseMultipleProcessors = new System.Windows.Forms.CheckBox();
            this.cbUseChargeStateFromMS = new System.Windows.Forms.CheckBox();
            this.rbMono = new System.Windows.Forms.RadioButton();
            this.tbNumChargeStates = new System.Windows.Forms.TextBox();
            this.rbAverage = new System.Windows.Forms.RadioButton();
            this.lblUseMass = new System.Windows.Forms.Label();
            this.lblNumChargeStates = new System.Windows.Forms.Label();
            this.tbStaticMods = new System.Windows.Forms.TextBox();
            this.lblStaticMods = new System.Windows.Forms.Label();
            this.tbFragmentTolerance = new System.Windows.Forms.TextBox();
            this.lblFragmentTolerance = new System.Windows.Forms.Label();
            this.tbPrecursorTolerance = new System.Windows.Forms.TextBox();
            this.lblPrecursorTolerance = new System.Windows.Forms.Label();
            this.cbAssessement = new System.Windows.Forms.CheckBox();
            this.gbRemoval = new System.Windows.Forms.GroupBox();
            this.btnMetricsFileBrowseForRemoval = new System.Windows.Forms.Button();
            this.cmbOutputFileFormat = new System.Windows.Forms.ComboBox();
            this.tbOutFileNameForRemoval = new System.Windows.Forms.TextBox();
            this.lblOutFileNameForRemoval = new System.Windows.Forms.Label();
            this.lblOutFileFormat = new System.Windows.Forms.Label();
            this.lblPercentSpectra = new System.Windows.Forms.Label();
            this.tbRemovalCutoff = new System.Windows.Forms.TextBox();
            this.lblRetain = new System.Windows.Forms.Label();
            this.tbMetricsFileForRemoval = new System.Windows.Forms.TextBox();
            this.lblMetricsFileForRemoval = new System.Windows.Forms.Label();
            this.cbRemoval = new System.Windows.Forms.CheckBox();
            this.gbRecovery = new System.Windows.Forms.GroupBox();
            this.btnMetricsFileBrowseForRecovery = new System.Windows.Forms.Button();
            this.btnSetIDPicker = new System.Windows.Forms.Button();
            this.tbOutFileNameForRecovery = new System.Windows.Forms.TextBox();
            this.lblOutFileNameForRecovery = new System.Windows.Forms.Label();
            this.tbMetricsFileForRecovery = new System.Windows.Forms.TextBox();
            this.lblMetricsFileForRecovery = new System.Windows.Forms.Label();
            this.cbRecovery = new System.Windows.Forms.CheckBox();
            this.btnRun = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblOutputDir = new System.Windows.Forms.Label();
            this.tbOutputDir = new System.Windows.Forms.TextBox();
            this.btnOutputDirBrowse = new System.Windows.Forms.Button();
            this.bgDirectagRun = new System.ComponentModel.BackgroundWorker();
            this.bgWriteSpectra = new System.ComponentModel.BackgroundWorker();
            this.bgAddLabels = new System.ComponentModel.BackgroundWorker();
            this.gbAssessment.SuspendLayout();
            this.gbDtgConfig.SuspendLayout();
            this.gbRemoval.SuspendLayout();
            this.gbRecovery.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblInputFileName
            // 
            this.lblInputFileName.AutoSize = true;
            this.lblInputFileName.Location = new System.Drawing.Point(23, 30);
            this.lblInputFileName.Name = "lblInputFileName";
            this.lblInputFileName.Size = new System.Drawing.Size(84, 13);
            this.lblInputFileName.TabIndex = 0;
            this.lblInputFileName.Text = "Input File Name:";
            // 
            // tbInputFile
            // 
            this.tbInputFile.Location = new System.Drawing.Point(113, 27);
            this.tbInputFile.Name = "tbInputFile";
            this.tbInputFile.Size = new System.Drawing.Size(496, 20);
            this.tbInputFile.TabIndex = 1;
            // 
            // btnSrcFileBrowse
            // 
            this.btnSrcFileBrowse.Location = new System.Drawing.Point(615, 26);
            this.btnSrcFileBrowse.Name = "btnSrcFileBrowse";
            this.btnSrcFileBrowse.Size = new System.Drawing.Size(73, 21);
            this.btnSrcFileBrowse.TabIndex = 2;
            this.btnSrcFileBrowse.Text = "Browse";
            this.btnSrcFileBrowse.UseVisualStyleBackColor = true;
            this.btnSrcFileBrowse.Click += new System.EventHandler(this.btnSrcFileBrowse_Click);
            // 
            // gbAssessment
            // 
            this.gbAssessment.Controls.Add(this.tbOutputMetrics);
            this.gbAssessment.Controls.Add(this.lblOutputMetrics);
            this.gbAssessment.Controls.Add(this.gbDtgConfig);
            this.gbAssessment.Controls.Add(this.cbAssessement);
            this.gbAssessment.Location = new System.Drawing.Point(26, 97);
            this.gbAssessment.Name = "gbAssessment";
            this.gbAssessment.Size = new System.Drawing.Size(662, 152);
            this.gbAssessment.TabIndex = 3;
            this.gbAssessment.TabStop = false;
            // 
            // tbOutputMetrics
            // 
            this.tbOutputMetrics.Location = new System.Drawing.Point(120, 129);
            this.tbOutputMetrics.Name = "tbOutputMetrics";
            this.tbOutputMetrics.Size = new System.Drawing.Size(521, 20);
            this.tbOutputMetrics.TabIndex = 4;
            // 
            // lblOutputMetrics
            // 
            this.lblOutputMetrics.AutoSize = true;
            this.lblOutputMetrics.Location = new System.Drawing.Point(16, 136);
            this.lblOutputMetrics.Name = "lblOutputMetrics";
            this.lblOutputMetrics.Size = new System.Drawing.Size(98, 13);
            this.lblOutputMetrics.TabIndex = 2;
            this.lblOutputMetrics.Text = "Output Metrics File:";
            // 
            // gbDtgConfig
            // 
            this.gbDtgConfig.Controls.Add(this.cbWriteOutTags);
            this.gbDtgConfig.Controls.Add(this.tbIsotopeTolerance);
            this.gbDtgConfig.Controls.Add(this.lblIsotopeTolerance);
            this.gbDtgConfig.Controls.Add(this.cbUseMultipleProcessors);
            this.gbDtgConfig.Controls.Add(this.cbUseChargeStateFromMS);
            this.gbDtgConfig.Controls.Add(this.rbMono);
            this.gbDtgConfig.Controls.Add(this.tbNumChargeStates);
            this.gbDtgConfig.Controls.Add(this.rbAverage);
            this.gbDtgConfig.Controls.Add(this.lblUseMass);
            this.gbDtgConfig.Controls.Add(this.lblNumChargeStates);
            this.gbDtgConfig.Controls.Add(this.tbStaticMods);
            this.gbDtgConfig.Controls.Add(this.lblStaticMods);
            this.gbDtgConfig.Controls.Add(this.tbFragmentTolerance);
            this.gbDtgConfig.Controls.Add(this.lblFragmentTolerance);
            this.gbDtgConfig.Controls.Add(this.tbPrecursorTolerance);
            this.gbDtgConfig.Controls.Add(this.lblPrecursorTolerance);
            this.gbDtgConfig.Location = new System.Drawing.Point(19, 23);
            this.gbDtgConfig.Name = "gbDtgConfig";
            this.gbDtgConfig.Size = new System.Drawing.Size(622, 99);
            this.gbDtgConfig.TabIndex = 1;
            this.gbDtgConfig.TabStop = false;
            this.gbDtgConfig.Text = "Sequence Tagging Configuration";
            // 
            // cbWriteOutTags
            // 
            this.cbWriteOutTags.AutoSize = true;
            this.cbWriteOutTags.Location = new System.Drawing.Point(424, 71);
            this.cbWriteOutTags.Name = "cbWriteOutTags";
            this.cbWriteOutTags.Size = new System.Drawing.Size(98, 17);
            this.cbWriteOutTags.TabIndex = 17;
            this.cbWriteOutTags.Text = "Write Out Tags";
            this.cbWriteOutTags.UseVisualStyleBackColor = true;
            // 
            // tbIsotopeTolerance
            // 
            this.tbIsotopeTolerance.Location = new System.Drawing.Point(152, 69);
            this.tbIsotopeTolerance.Name = "tbIsotopeTolerance";
            this.tbIsotopeTolerance.Size = new System.Drawing.Size(40, 20);
            this.tbIsotopeTolerance.TabIndex = 16;
            this.tbIsotopeTolerance.Text = "0.25";
            // 
            // lblIsotopeTolerance
            // 
            this.lblIsotopeTolerance.AutoSize = true;
            this.lblIsotopeTolerance.Location = new System.Drawing.Point(22, 72);
            this.lblIsotopeTolerance.Name = "lblIsotopeTolerance";
            this.lblIsotopeTolerance.Size = new System.Drawing.Size(117, 13);
            this.lblIsotopeTolerance.TabIndex = 15;
            this.lblIsotopeTolerance.Text = "Isotope m/z Tolerance:";
            // 
            // cbUseMultipleProcessors
            // 
            this.cbUseMultipleProcessors.AutoSize = true;
            this.cbUseMultipleProcessors.Location = new System.Drawing.Point(424, 47);
            this.cbUseMultipleProcessors.Name = "cbUseMultipleProcessors";
            this.cbUseMultipleProcessors.Size = new System.Drawing.Size(142, 17);
            this.cbUseMultipleProcessors.TabIndex = 10;
            this.cbUseMultipleProcessors.Text = "Use Multiple Processors ";
            this.cbUseMultipleProcessors.UseVisualStyleBackColor = true;
            // 
            // cbUseChargeStateFromMS
            // 
            this.cbUseChargeStateFromMS.AutoSize = true;
            this.cbUseChargeStateFromMS.Location = new System.Drawing.Point(424, 24);
            this.cbUseChargeStateFromMS.Name = "cbUseChargeStateFromMS";
            this.cbUseChargeStateFromMS.Size = new System.Drawing.Size(152, 17);
            this.cbUseChargeStateFromMS.TabIndex = 7;
            this.cbUseChargeStateFromMS.Text = "Use Charge State from MS";
            this.cbUseChargeStateFromMS.UseVisualStyleBackColor = true;
            // 
            // rbMono
            // 
            this.rbMono.AutoSize = true;
            this.rbMono.Location = new System.Drawing.Point(348, 71);
            this.rbMono.Name = "rbMono";
            this.rbMono.Size = new System.Drawing.Size(52, 17);
            this.rbMono.TabIndex = 6;
            this.rbMono.Text = "Mono";
            this.rbMono.UseVisualStyleBackColor = true;
            // 
            // tbNumChargeStates
            // 
            this.tbNumChargeStates.Location = new System.Drawing.Point(355, 46);
            this.tbNumChargeStates.Name = "tbNumChargeStates";
            this.tbNumChargeStates.Size = new System.Drawing.Size(40, 20);
            this.tbNumChargeStates.TabIndex = 12;
            this.tbNumChargeStates.Text = "3";
            // 
            // rbAverage
            // 
            this.rbAverage.AutoSize = true;
            this.rbAverage.Checked = true;
            this.rbAverage.Location = new System.Drawing.Point(285, 70);
            this.rbAverage.Name = "rbAverage";
            this.rbAverage.Size = new System.Drawing.Size(65, 17);
            this.rbAverage.TabIndex = 5;
            this.rbAverage.TabStop = true;
            this.rbAverage.Text = "Average";
            this.rbAverage.UseVisualStyleBackColor = true;
            // 
            // lblUseMass
            // 
            this.lblUseMass.AutoSize = true;
            this.lblUseMass.Location = new System.Drawing.Point(220, 72);
            this.lblUseMass.Name = "lblUseMass";
            this.lblUseMass.Size = new System.Drawing.Size(57, 13);
            this.lblUseMass.TabIndex = 4;
            this.lblUseMass.Text = "Use Mass:";
            // 
            // lblNumChargeStates
            // 
            this.lblNumChargeStates.AutoSize = true;
            this.lblNumChargeStates.Location = new System.Drawing.Point(220, 48);
            this.lblNumChargeStates.Name = "lblNumChargeStates";
            this.lblNumChargeStates.Size = new System.Drawing.Size(129, 13);
            this.lblNumChargeStates.TabIndex = 11;
            this.lblNumChargeStates.Text = "Number of Charge States:";
            // 
            // tbStaticMods
            // 
            this.tbStaticMods.Location = new System.Drawing.Point(292, 21);
            this.tbStaticMods.Name = "tbStaticMods";
            this.tbStaticMods.Size = new System.Drawing.Size(103, 20);
            this.tbStaticMods.TabIndex = 9;
            this.tbStaticMods.Text = "C 57.0215";
            // 
            // lblStaticMods
            // 
            this.lblStaticMods.AutoSize = true;
            this.lblStaticMods.Location = new System.Drawing.Point(220, 25);
            this.lblStaticMods.Name = "lblStaticMods";
            this.lblStaticMods.Size = new System.Drawing.Size(66, 13);
            this.lblStaticMods.TabIndex = 8;
            this.lblStaticMods.Text = "Static Mods:";
            // 
            // tbFragmentTolerance
            // 
            this.tbFragmentTolerance.Location = new System.Drawing.Point(152, 45);
            this.tbFragmentTolerance.Name = "tbFragmentTolerance";
            this.tbFragmentTolerance.Size = new System.Drawing.Size(40, 20);
            this.tbFragmentTolerance.TabIndex = 3;
            this.tbFragmentTolerance.Text = "0.5";
            // 
            // lblFragmentTolerance
            // 
            this.lblFragmentTolerance.AutoSize = true;
            this.lblFragmentTolerance.Location = new System.Drawing.Point(22, 49);
            this.lblFragmentTolerance.Name = "lblFragmentTolerance";
            this.lblFragmentTolerance.Size = new System.Drawing.Size(126, 13);
            this.lblFragmentTolerance.TabIndex = 2;
            this.lblFragmentTolerance.Text = "Fragment m/z Tolerance:";
            // 
            // tbPrecursorTolerance
            // 
            this.tbPrecursorTolerance.Location = new System.Drawing.Point(152, 22);
            this.tbPrecursorTolerance.Name = "tbPrecursorTolerance";
            this.tbPrecursorTolerance.Size = new System.Drawing.Size(40, 20);
            this.tbPrecursorTolerance.TabIndex = 1;
            this.tbPrecursorTolerance.Text = "1.25";
            // 
            // lblPrecursorTolerance
            // 
            this.lblPrecursorTolerance.AutoSize = true;
            this.lblPrecursorTolerance.Location = new System.Drawing.Point(22, 25);
            this.lblPrecursorTolerance.Name = "lblPrecursorTolerance";
            this.lblPrecursorTolerance.Size = new System.Drawing.Size(127, 13);
            this.lblPrecursorTolerance.TabIndex = 0;
            this.lblPrecursorTolerance.Text = "Precursor m/z Tolerance:";
            // 
            // cbAssessement
            // 
            this.cbAssessement.AutoSize = true;
            this.cbAssessement.Checked = true;
            this.cbAssessement.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbAssessement.Location = new System.Drawing.Point(0, 0);
            this.cbAssessement.Name = "cbAssessement";
            this.cbAssessement.Size = new System.Drawing.Size(159, 17);
            this.cbAssessement.TabIndex = 0;
            this.cbAssessement.Text = "Spectral Quality Assessment";
            this.cbAssessement.UseVisualStyleBackColor = true;
            this.cbAssessement.CheckedChanged += new System.EventHandler(this.cbAssessement_CheckedChanged);
            // 
            // gbRemoval
            // 
            this.gbRemoval.Controls.Add(this.btnMetricsFileBrowseForRemoval);
            this.gbRemoval.Controls.Add(this.cmbOutputFileFormat);
            this.gbRemoval.Controls.Add(this.tbOutFileNameForRemoval);
            this.gbRemoval.Controls.Add(this.lblOutFileNameForRemoval);
            this.gbRemoval.Controls.Add(this.lblOutFileFormat);
            this.gbRemoval.Controls.Add(this.lblPercentSpectra);
            this.gbRemoval.Controls.Add(this.tbRemovalCutoff);
            this.gbRemoval.Controls.Add(this.lblRetain);
            this.gbRemoval.Controls.Add(this.tbMetricsFileForRemoval);
            this.gbRemoval.Controls.Add(this.lblMetricsFileForRemoval);
            this.gbRemoval.Controls.Add(this.cbRemoval);
            this.gbRemoval.Location = new System.Drawing.Point(26, 273);
            this.gbRemoval.Name = "gbRemoval";
            this.gbRemoval.Size = new System.Drawing.Size(320, 204);
            this.gbRemoval.TabIndex = 4;
            this.gbRemoval.TabStop = false;
            // 
            // btnMetricsFileBrowseForRemoval
            // 
            this.btnMetricsFileBrowseForRemoval.Location = new System.Drawing.Point(228, 23);
            this.btnMetricsFileBrowseForRemoval.Name = "btnMetricsFileBrowseForRemoval";
            this.btnMetricsFileBrowseForRemoval.Size = new System.Drawing.Size(73, 21);
            this.btnMetricsFileBrowseForRemoval.TabIndex = 8;
            this.btnMetricsFileBrowseForRemoval.Text = "Browse";
            this.btnMetricsFileBrowseForRemoval.UseVisualStyleBackColor = true;
            this.btnMetricsFileBrowseForRemoval.Click += new System.EventHandler(this.btnMetricsFileBrowseForRemoval_Click);
            // 
            // cmbOutputFileFormat
            // 
            this.cmbOutputFileFormat.FormattingEnabled = true;
            this.cmbOutputFileFormat.Items.AddRange(new object[] {
            "mzML",
            "mzXML",
            "mgf"});
            this.cmbOutputFileFormat.Location = new System.Drawing.Point(145, 113);
            this.cmbOutputFileFormat.Name = "cmbOutputFileFormat";
            this.cmbOutputFileFormat.Size = new System.Drawing.Size(121, 21);
            this.cmbOutputFileFormat.TabIndex = 15;
            this.cmbOutputFileFormat.Text = "mzML";
            this.cmbOutputFileFormat.SelectedIndexChanged += new System.EventHandler(this.cmbOutputFileFormat_SelectedIndexChanged);
            // 
            // tbOutFileNameForRemoval
            // 
            this.tbOutFileNameForRemoval.Location = new System.Drawing.Point(19, 169);
            this.tbOutFileNameForRemoval.Name = "tbOutFileNameForRemoval";
            this.tbOutFileNameForRemoval.Size = new System.Drawing.Size(282, 20);
            this.tbOutFileNameForRemoval.TabIndex = 14;
            // 
            // lblOutFileNameForRemoval
            // 
            this.lblOutFileNameForRemoval.AutoSize = true;
            this.lblOutFileNameForRemoval.Location = new System.Drawing.Point(16, 146);
            this.lblOutFileNameForRemoval.Name = "lblOutFileNameForRemoval";
            this.lblOutFileNameForRemoval.Size = new System.Drawing.Size(92, 13);
            this.lblOutFileNameForRemoval.TabIndex = 13;
            this.lblOutFileNameForRemoval.Text = "Output File Name:";
            // 
            // lblOutFileFormat
            // 
            this.lblOutFileFormat.AutoSize = true;
            this.lblOutFileFormat.Location = new System.Drawing.Point(16, 115);
            this.lblOutFileFormat.Name = "lblOutFileFormat";
            this.lblOutFileFormat.Size = new System.Drawing.Size(96, 13);
            this.lblOutFileFormat.TabIndex = 10;
            this.lblOutFileFormat.Text = "Output File Format:";
            // 
            // lblPercentSpectra
            // 
            this.lblPercentSpectra.AutoSize = true;
            this.lblPercentSpectra.Location = new System.Drawing.Point(120, 84);
            this.lblPercentSpectra.Name = "lblPercentSpectra";
            this.lblPercentSpectra.Size = new System.Drawing.Size(180, 13);
            this.lblPercentSpectra.TabIndex = 9;
            this.lblPercentSpectra.Text = "% High Quality Spectra in Output File";
            // 
            // tbRemovalCutoff
            // 
            this.tbRemovalCutoff.Location = new System.Drawing.Point(80, 81);
            this.tbRemovalCutoff.Name = "tbRemovalCutoff";
            this.tbRemovalCutoff.Size = new System.Drawing.Size(36, 20);
            this.tbRemovalCutoff.TabIndex = 8;
            this.tbRemovalCutoff.Text = "60";
            this.tbRemovalCutoff.TextChanged += new System.EventHandler(this.tbRemovalCutoff_TextChanged);
            // 
            // lblRetain
            // 
            this.lblRetain.AutoSize = true;
            this.lblRetain.Location = new System.Drawing.Point(16, 84);
            this.lblRetain.Name = "lblRetain";
            this.lblRetain.Size = new System.Drawing.Size(60, 13);
            this.lblRetain.TabIndex = 7;
            this.lblRetain.Text = "Retain Top";
            // 
            // tbMetricsFileForRemoval
            // 
            this.tbMetricsFileForRemoval.Location = new System.Drawing.Point(19, 47);
            this.tbMetricsFileForRemoval.Name = "tbMetricsFileForRemoval";
            this.tbMetricsFileForRemoval.Size = new System.Drawing.Size(282, 20);
            this.tbMetricsFileForRemoval.TabIndex = 6;
            // 
            // lblMetricsFileForRemoval
            // 
            this.lblMetricsFileForRemoval.AutoSize = true;
            this.lblMetricsFileForRemoval.Location = new System.Drawing.Point(16, 27);
            this.lblMetricsFileForRemoval.Name = "lblMetricsFileForRemoval";
            this.lblMetricsFileForRemoval.Size = new System.Drawing.Size(98, 13);
            this.lblMetricsFileForRemoval.TabIndex = 5;
            this.lblMetricsFileForRemoval.Text = "Quality Metrics File:";
            // 
            // cbRemoval
            // 
            this.cbRemoval.AutoSize = true;
            this.cbRemoval.Checked = true;
            this.cbRemoval.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbRemoval.Location = new System.Drawing.Point(0, 0);
            this.cbRemoval.Name = "cbRemoval";
            this.cbRemoval.Size = new System.Drawing.Size(108, 17);
            this.cbRemoval.TabIndex = 0;
            this.cbRemoval.Text = "Spectra Removal";
            this.cbRemoval.UseVisualStyleBackColor = true;
            this.cbRemoval.CheckedChanged += new System.EventHandler(this.cbRemoval_CheckedChanged);
            // 
            // gbRecovery
            // 
            this.gbRecovery.Controls.Add(this.btnMetricsFileBrowseForRecovery);
            this.gbRecovery.Controls.Add(this.btnSetIDPicker);
            this.gbRecovery.Controls.Add(this.tbOutFileNameForRecovery);
            this.gbRecovery.Controls.Add(this.lblOutFileNameForRecovery);
            this.gbRecovery.Controls.Add(this.tbMetricsFileForRecovery);
            this.gbRecovery.Controls.Add(this.lblMetricsFileForRecovery);
            this.gbRecovery.Controls.Add(this.cbRecovery);
            this.gbRecovery.Location = new System.Drawing.Point(368, 273);
            this.gbRecovery.Name = "gbRecovery";
            this.gbRecovery.Size = new System.Drawing.Size(320, 204);
            this.gbRecovery.TabIndex = 5;
            this.gbRecovery.TabStop = false;
            this.gbRecovery.Text = " ";
            // 
            // btnMetricsFileBrowseForRecovery
            // 
            this.btnMetricsFileBrowseForRecovery.Location = new System.Drawing.Point(226, 23);
            this.btnMetricsFileBrowseForRecovery.Name = "btnMetricsFileBrowseForRecovery";
            this.btnMetricsFileBrowseForRecovery.Size = new System.Drawing.Size(73, 21);
            this.btnMetricsFileBrowseForRecovery.TabIndex = 16;
            this.btnMetricsFileBrowseForRecovery.Text = "Browse";
            this.btnMetricsFileBrowseForRecovery.UseVisualStyleBackColor = true;
            this.btnMetricsFileBrowseForRecovery.Click += new System.EventHandler(this.btnMetricsFileBrowseForRecovery_Click);
            // 
            // btnSetIDPicker
            // 
            this.btnSetIDPicker.Location = new System.Drawing.Point(17, 87);
            this.btnSetIDPicker.Name = "btnSetIDPicker";
            this.btnSetIDPicker.Size = new System.Drawing.Size(282, 40);
            this.btnSetIDPicker.TabIndex = 16;
            this.btnSetIDPicker.Text = "Click to Configurate IDPicker";
            this.btnSetIDPicker.UseVisualStyleBackColor = true;
            this.btnSetIDPicker.Click += new System.EventHandler(this.btnSetIDPicker_Click);
            // 
            // tbOutFileNameForRecovery
            // 
            this.tbOutFileNameForRecovery.Location = new System.Drawing.Point(17, 169);
            this.tbOutFileNameForRecovery.Name = "tbOutFileNameForRecovery";
            this.tbOutFileNameForRecovery.Size = new System.Drawing.Size(282, 20);
            this.tbOutFileNameForRecovery.TabIndex = 15;
            // 
            // lblOutFileNameForRecovery
            // 
            this.lblOutFileNameForRecovery.AutoSize = true;
            this.lblOutFileNameForRecovery.Location = new System.Drawing.Point(14, 146);
            this.lblOutFileNameForRecovery.Name = "lblOutFileNameForRecovery";
            this.lblOutFileNameForRecovery.Size = new System.Drawing.Size(92, 13);
            this.lblOutFileNameForRecovery.TabIndex = 15;
            this.lblOutFileNameForRecovery.Text = "Output File Name:";
            // 
            // tbMetricsFileForRecovery
            // 
            this.tbMetricsFileForRecovery.Location = new System.Drawing.Point(17, 47);
            this.tbMetricsFileForRecovery.Name = "tbMetricsFileForRecovery";
            this.tbMetricsFileForRecovery.Size = new System.Drawing.Size(282, 20);
            this.tbMetricsFileForRecovery.TabIndex = 15;
            // 
            // lblMetricsFileForRecovery
            // 
            this.lblMetricsFileForRecovery.AutoSize = true;
            this.lblMetricsFileForRecovery.Location = new System.Drawing.Point(14, 27);
            this.lblMetricsFileForRecovery.Name = "lblMetricsFileForRecovery";
            this.lblMetricsFileForRecovery.Size = new System.Drawing.Size(98, 13);
            this.lblMetricsFileForRecovery.TabIndex = 15;
            this.lblMetricsFileForRecovery.Text = "Quality Metrics File:";
            // 
            // cbRecovery
            // 
            this.cbRecovery.AutoSize = true;
            this.cbRecovery.Location = new System.Drawing.Point(0, 0);
            this.cbRecovery.Name = "cbRecovery";
            this.cbRecovery.Size = new System.Drawing.Size(112, 17);
            this.cbRecovery.TabIndex = 0;
            this.cbRecovery.Text = "Spectra Recovery";
            this.cbRecovery.UseVisualStyleBackColor = true;
            this.cbRecovery.CheckedChanged += new System.EventHandler(this.cbRecovery_CheckedChanged);
            // 
            // btnRun
            // 
            this.btnRun.Location = new System.Drawing.Point(510, 483);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(80, 23);
            this.btnRun.TabIndex = 6;
            this.btnRun.Text = "Run";
            this.btnRun.UseVisualStyleBackColor = true;
            this.btnRun.Click += new System.EventHandler(this.btnRun_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(608, 483);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(80, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // lblOutputDir
            // 
            this.lblOutputDir.AutoSize = true;
            this.lblOutputDir.Location = new System.Drawing.Point(23, 65);
            this.lblOutputDir.Name = "lblOutputDir";
            this.lblOutputDir.Size = new System.Drawing.Size(87, 13);
            this.lblOutputDir.TabIndex = 8;
            this.lblOutputDir.Text = "Output Directory:";
            // 
            // tbOutputDir
            // 
            this.tbOutputDir.Location = new System.Drawing.Point(113, 62);
            this.tbOutputDir.Name = "tbOutputDir";
            this.tbOutputDir.Size = new System.Drawing.Size(496, 20);
            this.tbOutputDir.TabIndex = 9;
            // 
            // btnOutputDirBrowse
            // 
            this.btnOutputDirBrowse.Location = new System.Drawing.Point(615, 61);
            this.btnOutputDirBrowse.Name = "btnOutputDirBrowse";
            this.btnOutputDirBrowse.Size = new System.Drawing.Size(73, 21);
            this.btnOutputDirBrowse.TabIndex = 10;
            this.btnOutputDirBrowse.Text = "Browse";
            this.btnOutputDirBrowse.UseVisualStyleBackColor = true;
            this.btnOutputDirBrowse.Click += new System.EventHandler(this.btnOutputDirBrowse_Click);
            // 
            // bgDirectagRun
            // 
            this.bgDirectagRun.DoWork += new System.ComponentModel.DoWorkEventHandler(this.bgDirectagRun_DoWork);
            // 
            // bgWriteSpectra
            // 
            this.bgWriteSpectra.DoWork += new System.ComponentModel.DoWorkEventHandler(this.bgWriteSpectra_DoWork);
            // 
            // bgAddLabels
            // 
            this.bgAddLabels.DoWork += new System.ComponentModel.DoWorkEventHandler(this.bgAddLabels_DoWork);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(718, 526);
            this.Controls.Add(this.btnOutputDirBrowse);
            this.Controls.Add(this.tbOutputDir);
            this.Controls.Add(this.lblOutputDir);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnRun);
            this.Controls.Add(this.gbRecovery);
            this.Controls.Add(this.gbRemoval);
            this.Controls.Add(this.gbAssessment);
            this.Controls.Add(this.btnSrcFileBrowse);
            this.Controls.Add(this.tbInputFile);
            this.Controls.Add(this.lblInputFileName);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "ScanRanker";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.gbAssessment.ResumeLayout(false);
            this.gbAssessment.PerformLayout();
            this.gbDtgConfig.ResumeLayout(false);
            this.gbDtgConfig.PerformLayout();
            this.gbRemoval.ResumeLayout(false);
            this.gbRemoval.PerformLayout();
            this.gbRecovery.ResumeLayout(false);
            this.gbRecovery.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblInputFileName;
        private System.Windows.Forms.TextBox tbInputFile;
        private System.Windows.Forms.Button btnSrcFileBrowse;
        private System.Windows.Forms.GroupBox gbAssessment;
        private System.Windows.Forms.CheckBox cbAssessement;
        private System.Windows.Forms.GroupBox gbDtgConfig;
        private System.Windows.Forms.TextBox tbOutputMetrics;
        private System.Windows.Forms.Label lblOutputMetrics;
        private System.Windows.Forms.GroupBox gbRemoval;
        private System.Windows.Forms.CheckBox cbRemoval;
        private System.Windows.Forms.GroupBox gbRecovery;
        private System.Windows.Forms.Label lblRetain;
        private System.Windows.Forms.TextBox tbMetricsFileForRemoval;
        private System.Windows.Forms.Label lblMetricsFileForRemoval;
        private System.Windows.Forms.CheckBox cbRecovery;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblOutFileFormat;
        private System.Windows.Forms.Label lblPercentSpectra;
        private System.Windows.Forms.TextBox tbRemovalCutoff;
        private System.Windows.Forms.Label lblOutFileNameForRemoval;
        private System.Windows.Forms.TextBox tbOutFileNameForRemoval;
        private System.Windows.Forms.TextBox tbMetricsFileForRecovery;
        private System.Windows.Forms.Label lblMetricsFileForRecovery;
        private System.Windows.Forms.TextBox tbOutFileNameForRecovery;
        private System.Windows.Forms.Label lblOutFileNameForRecovery;
        private System.Windows.Forms.Button btnSetIDPicker;
        private System.Windows.Forms.Label lblPrecursorTolerance;
        private System.Windows.Forms.Label lblUseMass;
        private System.Windows.Forms.TextBox tbFragmentTolerance;
        private System.Windows.Forms.Label lblFragmentTolerance;
        private System.Windows.Forms.TextBox tbPrecursorTolerance;
        private System.Windows.Forms.CheckBox cbUseChargeStateFromMS;
        private System.Windows.Forms.RadioButton rbMono;
        private System.Windows.Forms.RadioButton rbAverage;
        private System.Windows.Forms.TextBox tbNumChargeStates;
        private System.Windows.Forms.Label lblNumChargeStates;
        private System.Windows.Forms.CheckBox cbUseMultipleProcessors;
        private System.Windows.Forms.TextBox tbStaticMods;
        private System.Windows.Forms.Label lblStaticMods;
        private System.Windows.Forms.TextBox tbIsotopeTolerance;
        private System.Windows.Forms.Label lblIsotopeTolerance;
        private System.Windows.Forms.ComboBox cmbOutputFileFormat;
        private System.Windows.Forms.Button btnMetricsFileBrowseForRemoval;
        private System.Windows.Forms.Button btnMetricsFileBrowseForRecovery;
        private System.Windows.Forms.Label lblOutputDir;
        private System.Windows.Forms.TextBox tbOutputDir;
        private System.Windows.Forms.Button btnOutputDirBrowse;
        private System.Windows.Forms.CheckBox cbWriteOutTags;
        public System.ComponentModel.BackgroundWorker bgDirectagRun;
        public System.ComponentModel.BackgroundWorker bgWriteSpectra;
        public System.ComponentModel.BackgroundWorker bgAddLabels;


        
    }
}

