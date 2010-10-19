namespace BumberDash
{
    partial class ConfigForm
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
            System.Windows.Forms.ListViewItem listViewItem1 = new System.Windows.Forms.ListViewItem(new string[] {
            "Carboxymethylated Cysteine",
            "C",
            "57"}, -1);
            System.Windows.Forms.ListViewItem listViewItem2 = new System.Windows.Forms.ListViewItem(new string[] {
            "Oxidized Methione",
            "M",
            "15.995"}, -1);
            System.Windows.Forms.ListViewItem listViewItem3 = new System.Windows.Forms.ListViewItem(new string[] {
            "Phosphorylated Serine",
            "S",
            "79.966"}, -1);
            System.Windows.Forms.ListViewItem listViewItem4 = new System.Windows.Forms.ListViewItem(new string[] {
            "N terminal deamidation of glutamine",
            "(Q",
            "-17"}, -1);
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConfigForm));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.Gentab = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.SoftMessageLabel = new System.Windows.Forms.Label();
            this.ModGB = new System.Windows.Forms.GroupBox();
            this.AppliedModDGV = new System.Windows.Forms.DataGridView();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column3 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.MaxNumPreferredDeltaMassesPannel = new System.Windows.Forms.Panel();
            this.MaxNumPreferredDeltaMassesLabel = new System.Windows.Forms.Label();
            this.MaxNumPreferredDeltaMassesBox = new System.Windows.Forms.NumericUpDown();
            this.AppliedModLabel = new System.Windows.Forms.Label();
            this.AppliedModRemove = new System.Windows.Forms.Button();
            this.AppliedModAdd = new System.Windows.Forms.Button();
            this.StaticModsInfo = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.ModTypeBox = new System.Windows.Forms.ComboBox();
            this.ModListLabel = new System.Windows.Forms.Label();
            this.ResidueLabel = new System.Windows.Forms.Label();
            this.ModMassLabel = new System.Windows.Forms.Label();
            this.ModMassBox = new System.Windows.Forms.TextBox();
            this.MaxDynamicModsBox = new System.Windows.Forms.NumericUpDown();
            this.ResidueBox = new System.Windows.Forms.TextBox();
            this.ModList = new System.Windows.Forms.ListView();
            this.Description = new System.Windows.Forms.ColumnHeader();
            this.MaxDynamicModsInfo = new System.Windows.Forms.Label();
            this.MaxDynamicModsLabel = new System.Windows.Forms.Label();
            this.InstrumentGB = new System.Windows.Forms.GroupBox();
            this.InstrumentPannel = new System.Windows.Forms.Panel();
            this.InstrumentBox = new System.Windows.Forms.ComboBox();
            this.InstrumentLabel = new System.Windows.Forms.Label();
            this.UseAvgMassOfSequencesBox = new System.Windows.Forms.ComboBox();
            this.UseAvgMassOfSequencesInfo = new System.Windows.Forms.Label();
            this.UseAvgMassOfSequencesLabel = new System.Windows.Forms.Label();
            this.DigestionGB = new System.Windows.Forms.GroupBox();
            this.NumMaxMissedCleavagesAuto = new System.Windows.Forms.Label();
            this.NumMaxMissedCleavagesBox = new System.Windows.Forms.NumericUpDown();
            this.NumMinTerminiCleavagesBox = new System.Windows.Forms.ComboBox();
            this.CleavageRulesBox = new System.Windows.Forms.ComboBox();
            this.NumMaxMissedCleavagesInfo = new System.Windows.Forms.Label();
            this.NumMinTerminiCleavagesInfo = new System.Windows.Forms.Label();
            this.CleavageRulesInfo = new System.Windows.Forms.Label();
            this.CleavageRulesLabel = new System.Windows.Forms.Label();
            this.NumMaxMissedCleavagesLabel = new System.Windows.Forms.Label();
            this.NumMinTerminiCleavagesLabel = new System.Windows.Forms.Label();
            this.ToleranceGB = new System.Windows.Forms.GroupBox();
            this.PrecursorPannel = new System.Windows.Forms.Panel();
            this.PrecursorMzToleranceBox = new System.Windows.Forms.TextBox();
            this.PrecursorMzToleranceUnitsBox = new System.Windows.Forms.ComboBox();
            this.PrecursorMzToleranceLabel = new System.Windows.Forms.Label();
            this.PrecursorMzToleranceInfo = new System.Windows.Forms.Label();
            this.FragmentPannel = new System.Windows.Forms.Panel();
            this.FragmentMzToleranceBox = new System.Windows.Forms.TextBox();
            this.FragmentMzToleranceUnitsBox = new System.Windows.Forms.ComboBox();
            this.FragmentMzToleranceInfo = new System.Windows.Forms.Label();
            this.FragmentMzToleranceLabel = new System.Windows.Forms.Label();
            this.TagReconTolerancePanel = new System.Windows.Forms.Panel();
            this.CTerminusMzToleranceBox = new System.Windows.Forms.TextBox();
            this.NTerminusMzToleranceBox = new System.Windows.Forms.TextBox();
            this.CTerminusMzToleranceUnitsBox = new System.Windows.Forms.ComboBox();
            this.NTerminusMzToleranceUnitsBox = new System.Windows.Forms.ComboBox();
            this.CTerminusMzToleranceInfo = new System.Windows.Forms.Label();
            this.NTerminusMzToleranceInfo = new System.Windows.Forms.Label();
            this.CTerminusMzToleranceLabel = new System.Windows.Forms.Label();
            this.NTerminusMzToleranceLabel = new System.Windows.Forms.Label();
            this.AdvTab = new System.Windows.Forms.TabPage();
            this.ChargeGB = new System.Windows.Forms.GroupBox();
            this.NumChargeStatesBox = new System.Windows.Forms.NumericUpDown();
            this.DuplicateSpectraBox = new System.Windows.Forms.CheckBox();
            this.DuplicateSpectraInfo = new System.Windows.Forms.Label();
            this.NumChargeStatesInfo = new System.Windows.Forms.Label();
            this.UseChargeStateFromMSBox = new System.Windows.Forms.CheckBox();
            this.DuplicateSpectraLabel = new System.Windows.Forms.Label();
            this.UseChargeStateFromMSInfo = new System.Windows.Forms.Label();
            this.NumChargeStatesLabel = new System.Windows.Forms.Label();
            this.UseChargeStateFromMSLabel = new System.Windows.Forms.Label();
            this.SubsetGB = new System.Windows.Forms.GroupBox();
            this.EndSpectraScanNumAuto = new System.Windows.Forms.Label();
            this.EndProteinIndexAuto = new System.Windows.Forms.Label();
            this.EndProteinIndexBox = new System.Windows.Forms.NumericUpDown();
            this.EndProteinIndexInfo = new System.Windows.Forms.Label();
            this.StartProteinIndexBox = new System.Windows.Forms.NumericUpDown();
            this.StartProteinIndexInfo = new System.Windows.Forms.Label();
            this.EndSpectraScanNumBox = new System.Windows.Forms.NumericUpDown();
            this.EndSpectraScanNumInfo = new System.Windows.Forms.Label();
            this.StartSpectraScanNumBox = new System.Windows.Forms.NumericUpDown();
            this.StartSpectraScanNumInfo = new System.Windows.Forms.Label();
            this.EndProteinIndexLabel = new System.Windows.Forms.Label();
            this.StartProteinIndexLabel = new System.Windows.Forms.Label();
            this.StartSpectraScanNumLabel = new System.Windows.Forms.Label();
            this.EndSpectraScanNumLabel = new System.Windows.Forms.Label();
            this.SequenceGB = new System.Windows.Forms.GroupBox();
            this.MaxSequenceMassBox = new System.Windows.Forms.NumericUpDown();
            this.MinSequenceMassBox = new System.Windows.Forms.NumericUpDown();
            this.MaxSequenceMassInfo = new System.Windows.Forms.Label();
            this.MinSequenceMassInfo = new System.Windows.Forms.Label();
            this.MaxSequenceMassLabel = new System.Windows.Forms.Label();
            this.MinSequenceMassLabel = new System.Windows.Forms.Label();
            this.MinCandidateLengthBox = new System.Windows.Forms.NumericUpDown();
            this.MinCandidateLengthLabel = new System.Windows.Forms.Label();
            this.MiscGB = new System.Windows.Forms.GroupBox();
            this.MaxResultsPanel = new System.Windows.Forms.Panel();
            this.MaxResultsBox = new System.Windows.Forms.NumericUpDown();
            this.MaxResultsLabel = new System.Windows.Forms.Label();
            this.MaxResultsInfo = new System.Windows.Forms.Label();
            this.DTNewOptionsPanel = new System.Windows.Forms.Panel();
            this.TicCutoffPercentageBox = new System.Windows.Forms.NumericUpDown();
            this.TicCutoffPercentageLabel = new System.Windows.Forms.Label();
            this.DeisotopingModeBox = new System.Windows.Forms.ComboBox();
            this.DeisotopingModeLabel = new System.Windows.Forms.Label();
            this.DeisotopingModeInfo = new System.Windows.Forms.Label();
            this.TicCutoffPercentageInfo = new System.Windows.Forms.Label();
            this.ProteinSampleSizeBox = new System.Windows.Forms.NumericUpDown();
            this.UseSmartPlusThreeModelBox = new System.Windows.Forms.CheckBox();
            this.UseSmartPlusThreeModelInfo = new System.Windows.Forms.Label();
            this.ProteinSampleSizeInfo = new System.Windows.Forms.Label();
            this.UseSmartPlusThreeModelLabel = new System.Windows.Forms.Label();
            this.ProteinSampleSizeLabel = new System.Windows.Forms.Label();
            this.PrecursorGbox = new System.Windows.Forms.GroupBox();
            this.MaxPrecursorAdjustmentBox = new System.Windows.Forms.NumericUpDown();
            this.MinPrecursorAdjustmentBox = new System.Windows.Forms.NumericUpDown();
            this.MinPrecursorAdjustmentInfo = new System.Windows.Forms.Label();
            this.MaxPrecursorAdjustmentLabel = new System.Windows.Forms.Label();
            this.MinPrecursorAdjustmentLabel = new System.Windows.Forms.Label();
            this.AdjustPanel = new System.Windows.Forms.Panel();
            this.AdjustPrecursorMassBox = new System.Windows.Forms.CheckBox();
            this.AdjustPrecursorMassInfo = new System.Windows.Forms.Label();
            this.AdjustPrecursorMassLabel = new System.Windows.Forms.Label();
            this.TagReconGB = new System.Windows.Forms.GroupBox();
            this.MassReconModeBox = new System.Windows.Forms.CheckBox();
            this.MassReconModeInfo = new System.Windows.Forms.Label();
            this.MassReconModeLabel = new System.Windows.Forms.Label();
            this.ComputeXCorrPanel = new System.Windows.Forms.Panel();
            this.ComputeXCorrBox = new System.Windows.Forms.CheckBox();
            this.ComputeXCorrLabel = new System.Windows.Forms.Label();
            this.UseNETAdjustmentBox = new System.Windows.Forms.CheckBox();
            this.UseNETAdjustmentLabel = new System.Windows.Forms.Label();
            this.DirecTagGB = new System.Windows.Forms.GroupBox();
            this.MaxPeakPanel = new System.Windows.Forms.Panel();
            this.MaxPeakCountBox = new System.Windows.Forms.NumericUpDown();
            this.MaxPeakCountInfo = new System.Windows.Forms.Label();
            this.MaxPeakCountLabel = new System.Windows.Forms.Label();
            this.MaxTagCountBox = new System.Windows.Forms.NumericUpDown();
            this.MaxTagScoreBox = new System.Windows.Forms.TextBox();
            this.MaxTagCountLabel = new System.Windows.Forms.Label();
            this.MaxTagScoreLabel = new System.Windows.Forms.Label();
            this.IsotopeMzToleranceBox = new System.Windows.Forms.TextBox();
            this.ComplementMzToleranceBox = new System.Windows.Forms.TextBox();
            this.TagLengthBox = new System.Windows.Forms.NumericUpDown();
            this.ComplementMzToleranceInfo = new System.Windows.Forms.Label();
            this.IsotopeMzToleranceInfo = new System.Windows.Forms.Label();
            this.TagLengthInfo = new System.Windows.Forms.Label();
            this.ComplementMzToleranceLabel = new System.Windows.Forms.Label();
            this.TagLengthLabel = new System.Windows.Forms.Label();
            this.IsotopeMzToleranceLabel = new System.Windows.Forms.Label();
            this.TRModOptionsGB = new System.Windows.Forms.GroupBox();
            this.BlosumThresholdBox = new System.Windows.Forms.NumericUpDown();
            this.BlosumThresholdInfo = new System.Windows.Forms.Label();
            this.ExplainUnknownMassShiftsAsBox = new System.Windows.Forms.ComboBox();
            this.MaxModificationMassPlusBox = new System.Windows.Forms.NumericUpDown();
            this.BlosumBox = new System.Windows.Forms.TextBox();
            this.MaxModificationMassMinusBox = new System.Windows.Forms.NumericUpDown();
            this.UnimodXMLBox = new System.Windows.Forms.TextBox();
            this.ExplainUnknownMassShiftsAsLabel = new System.Windows.Forms.Label();
            this.BlosumThresholdLabel = new System.Windows.Forms.Label();
            this.BlosumInfo = new System.Windows.Forms.Label();
            this.UnimodXMLInfo = new System.Windows.Forms.Label();
            this.BlosumLabel = new System.Windows.Forms.Label();
            this.UnimodXMLBrowse = new System.Windows.Forms.Button();
            this.MaxModificationMassMinusLabel = new System.Windows.Forms.Label();
            this.MaxModificationMassPlusInfo = new System.Windows.Forms.Label();
            this.UnimodXMLLabel = new System.Windows.Forms.Label();
            this.MaxModificationMassPlusLabel = new System.Windows.Forms.Label();
            this.MaxModificationMassMinusInfo = new System.Windows.Forms.Label();
            this.BlosumBrowse = new System.Windows.Forms.Button();
            this.ScoringGB = new System.Windows.Forms.GroupBox();
            this.DTScorePanel = new System.Windows.Forms.Panel();
            this.ComplementScoreWeightBox = new System.Windows.Forms.NumericUpDown();
            this.MzFidelityScoreWeightBox = new System.Windows.Forms.NumericUpDown();
            this.IntensityScoreWeightInf3 = new System.Windows.Forms.Label();
            this.IntensityScoreWeightInf2 = new System.Windows.Forms.Label();
            this.IntensityScoreWeightBox = new System.Windows.Forms.NumericUpDown();
            this.IntensityScoreWeightInfo = new System.Windows.Forms.Label();
            this.ComplementScoreWeightLabel = new System.Windows.Forms.Label();
            this.IntensityScoreWeightLabel = new System.Windows.Forms.Label();
            this.MzFidelityScoreWeightLabel = new System.Windows.Forms.Label();
            this.ClassSizeMultiplierBox = new System.Windows.Forms.NumericUpDown();
            this.NumIntensityClassesBox = new System.Windows.Forms.NumericUpDown();
            this.ClassSizeMultiplierInfo = new System.Windows.Forms.Label();
            this.NumIntensityClassesInfo = new System.Windows.Forms.Label();
            this.NumIntensityClassesLabel = new System.Windows.Forms.Label();
            this.ClassSizeMultiplierLabel = new System.Windows.Forms.Label();
            this.AdvModeBox = new System.Windows.Forms.CheckBox();
            this.AdvModeLabel = new System.Windows.Forms.Label();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openConfigFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.programToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.myrimatchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.direcTagToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tagReconToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SoftMessageTimer = new System.Windows.Forms.Timer(this.components);
            this.SoftMessageFadeTimer = new System.Windows.Forms.Timer(this.components);
            this.CancelEditButton = new System.Windows.Forms.Button();
            this.SaveAsNewButton = new System.Windows.Forms.Button();
            this.SaveOverOldButton = new System.Windows.Forms.Button();
            this.SaveAsTemporaryButton = new System.Windows.Forms.Button();
            this.tabControl1.SuspendLayout();
            this.Gentab.SuspendLayout();
            this.panel1.SuspendLayout();
            this.ModGB.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.AppliedModDGV)).BeginInit();
            this.MaxNumPreferredDeltaMassesPannel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxNumPreferredDeltaMassesBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxDynamicModsBox)).BeginInit();
            this.InstrumentGB.SuspendLayout();
            this.InstrumentPannel.SuspendLayout();
            this.DigestionGB.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.NumMaxMissedCleavagesBox)).BeginInit();
            this.ToleranceGB.SuspendLayout();
            this.PrecursorPannel.SuspendLayout();
            this.FragmentPannel.SuspendLayout();
            this.TagReconTolerancePanel.SuspendLayout();
            this.AdvTab.SuspendLayout();
            this.ChargeGB.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.NumChargeStatesBox)).BeginInit();
            this.SubsetGB.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.EndProteinIndexBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.StartProteinIndexBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.EndSpectraScanNumBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.StartSpectraScanNumBox)).BeginInit();
            this.SequenceGB.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxSequenceMassBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MinSequenceMassBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MinCandidateLengthBox)).BeginInit();
            this.MiscGB.SuspendLayout();
            this.MaxResultsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxResultsBox)).BeginInit();
            this.DTNewOptionsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.TicCutoffPercentageBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ProteinSampleSizeBox)).BeginInit();
            this.PrecursorGbox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxPrecursorAdjustmentBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MinPrecursorAdjustmentBox)).BeginInit();
            this.AdjustPanel.SuspendLayout();
            this.TagReconGB.SuspendLayout();
            this.ComputeXCorrPanel.SuspendLayout();
            this.DirecTagGB.SuspendLayout();
            this.MaxPeakPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxPeakCountBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxTagCountBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.TagLengthBox)).BeginInit();
            this.TRModOptionsGB.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.BlosumThresholdBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxModificationMassPlusBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxModificationMassMinusBox)).BeginInit();
            this.ScoringGB.SuspendLayout();
            this.DTScorePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ComplementScoreWeightBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MzFidelityScoreWeightBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.IntensityScoreWeightBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ClassSizeMultiplierBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.NumIntensityClassesBox)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.Gentab);
            this.tabControl1.Controls.Add(this.AdvTab);
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(536, 565);
            this.tabControl1.TabIndex = 0;
            // 
            // Gentab
            // 
            this.Gentab.Controls.Add(this.panel1);
            this.Gentab.Controls.Add(this.ModGB);
            this.Gentab.Controls.Add(this.InstrumentGB);
            this.Gentab.Controls.Add(this.DigestionGB);
            this.Gentab.Controls.Add(this.ToleranceGB);
            this.Gentab.Location = new System.Drawing.Point(4, 22);
            this.Gentab.Name = "Gentab";
            this.Gentab.Padding = new System.Windows.Forms.Padding(3);
            this.Gentab.Size = new System.Drawing.Size(528, 539);
            this.Gentab.TabIndex = 0;
            this.Gentab.Text = "General";
            this.Gentab.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.SoftMessageLabel);
            this.panel1.Location = new System.Drawing.Point(0, 514);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(528, 25);
            this.panel1.TabIndex = 4;
            // 
            // SoftMessageLabel
            // 
            this.SoftMessageLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.SoftMessageLabel.AutoSize = true;
            this.SoftMessageLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SoftMessageLabel.ForeColor = System.Drawing.Color.Red;
            this.SoftMessageLabel.Location = new System.Drawing.Point(50, 2);
            this.SoftMessageLabel.Name = "SoftMessageLabel";
            this.SoftMessageLabel.Size = new System.Drawing.Size(439, 20);
            this.SoftMessageLabel.TabIndex = 82;
            this.SoftMessageLabel.Tag = "In 0";
            this.SoftMessageLabel.Text = "Some modifications had to be converted to \"Dynamic\"";
            this.SoftMessageLabel.Visible = false;
            // 
            // ModGB
            // 
            this.ModGB.Controls.Add(this.AppliedModDGV);
            this.ModGB.Controls.Add(this.MaxNumPreferredDeltaMassesPannel);
            this.ModGB.Controls.Add(this.AppliedModLabel);
            this.ModGB.Controls.Add(this.AppliedModRemove);
            this.ModGB.Controls.Add(this.AppliedModAdd);
            this.ModGB.Controls.Add(this.StaticModsInfo);
            this.ModGB.Controls.Add(this.label1);
            this.ModGB.Controls.Add(this.ModTypeBox);
            this.ModGB.Controls.Add(this.ModListLabel);
            this.ModGB.Controls.Add(this.ResidueLabel);
            this.ModGB.Controls.Add(this.ModMassLabel);
            this.ModGB.Controls.Add(this.ModMassBox);
            this.ModGB.Controls.Add(this.MaxDynamicModsBox);
            this.ModGB.Controls.Add(this.ResidueBox);
            this.ModGB.Controls.Add(this.ModList);
            this.ModGB.Controls.Add(this.MaxDynamicModsInfo);
            this.ModGB.Controls.Add(this.MaxDynamicModsLabel);
            this.ModGB.Location = new System.Drawing.Point(7, 211);
            this.ModGB.Name = "ModGB";
            this.ModGB.Size = new System.Drawing.Size(514, 283);
            this.ModGB.TabIndex = 3;
            this.ModGB.TabStop = false;
            this.ModGB.Text = "Modifications";
            // 
            // AppliedModDGV
            // 
            this.AppliedModDGV.AllowUserToAddRows = false;
            this.AppliedModDGV.AllowUserToDeleteRows = false;
            this.AppliedModDGV.AllowUserToResizeColumns = false;
            this.AppliedModDGV.AllowUserToResizeRows = false;
            this.AppliedModDGV.BackgroundColor = System.Drawing.SystemColors.Window;
            this.AppliedModDGV.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.AppliedModDGV.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.AppliedModDGV.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column1,
            this.Column2,
            this.Column3});
            this.AppliedModDGV.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.AppliedModDGV.Location = new System.Drawing.Point(282, 32);
            this.AppliedModDGV.MultiSelect = false;
            this.AppliedModDGV.Name = "AppliedModDGV";
            this.AppliedModDGV.RowHeadersVisible = false;
            this.AppliedModDGV.RowTemplate.Height = 24;
            this.AppliedModDGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.AppliedModDGV.ShowCellErrors = false;
            this.AppliedModDGV.Size = new System.Drawing.Size(226, 199);
            this.AppliedModDGV.TabIndex = 93;
            this.AppliedModDGV.RowValidating += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.AppliedModDGV_RowValidating);
            // 
            // Column1
            // 
            this.Column1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column1.HeaderText = "Motif";
            this.Column1.Name = "Column1";
            this.Column1.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // Column2
            // 
            this.Column2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column2.HeaderText = "Mass";
            this.Column2.Name = "Column2";
            this.Column2.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // Column3
            // 
            this.Column3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column3.FillWeight = 150F;
            this.Column3.HeaderText = "Type";
            this.Column3.Items.AddRange(new object[] {
            "Static",
            "Dynamic"});
            this.Column3.Name = "Column3";
            this.Column3.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.Column3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // MaxNumPreferredDeltaMassesPannel
            // 
            this.MaxNumPreferredDeltaMassesPannel.Controls.Add(this.MaxNumPreferredDeltaMassesLabel);
            this.MaxNumPreferredDeltaMassesPannel.Controls.Add(this.MaxNumPreferredDeltaMassesBox);
            this.MaxNumPreferredDeltaMassesPannel.Location = new System.Drawing.Point(278, 236);
            this.MaxNumPreferredDeltaMassesPannel.Name = "MaxNumPreferredDeltaMassesPannel";
            this.MaxNumPreferredDeltaMassesPannel.Size = new System.Drawing.Size(104, 40);
            this.MaxNumPreferredDeltaMassesPannel.TabIndex = 92;
            // 
            // MaxNumPreferredDeltaMassesLabel
            // 
            this.MaxNumPreferredDeltaMassesLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.MaxNumPreferredDeltaMassesLabel.AutoSize = true;
            this.MaxNumPreferredDeltaMassesLabel.Location = new System.Drawing.Point(0, -1);
            this.MaxNumPreferredDeltaMassesLabel.Name = "MaxNumPreferredDeltaMassesLabel";
            this.MaxNumPreferredDeltaMassesLabel.Size = new System.Drawing.Size(50, 39);
            this.MaxNumPreferredDeltaMassesLabel.TabIndex = 87;
            this.MaxNumPreferredDeltaMassesLabel.Text = "Max\r\nPreferred\r\nPTMs:";
            // 
            // MaxNumPreferredDeltaMassesBox
            // 
            this.MaxNumPreferredDeltaMassesBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.MaxNumPreferredDeltaMassesBox.Location = new System.Drawing.Point(56, 19);
            this.MaxNumPreferredDeltaMassesBox.Name = "MaxNumPreferredDeltaMassesBox";
            this.MaxNumPreferredDeltaMassesBox.Size = new System.Drawing.Size(45, 20);
            this.MaxNumPreferredDeltaMassesBox.TabIndex = 86;
            this.MaxNumPreferredDeltaMassesBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.MaxNumPreferredDeltaMassesBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MaxNumPreferredDeltaMassesBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // AppliedModLabel
            // 
            this.AppliedModLabel.AutoSize = true;
            this.AppliedModLabel.Location = new System.Drawing.Point(279, 16);
            this.AppliedModLabel.Name = "AppliedModLabel";
            this.AppliedModLabel.Size = new System.Drawing.Size(88, 13);
            this.AppliedModLabel.TabIndex = 91;
            this.AppliedModLabel.Text = "Applied Mod List:";
            // 
            // AppliedModRemove
            // 
            this.AppliedModRemove.Location = new System.Drawing.Point(244, 138);
            this.AppliedModRemove.Name = "AppliedModRemove";
            this.AppliedModRemove.Size = new System.Drawing.Size(32, 23);
            this.AppliedModRemove.TabIndex = 89;
            this.AppliedModRemove.Text = "<";
            this.AppliedModRemove.UseVisualStyleBackColor = true;
            this.AppliedModRemove.Click += new System.EventHandler(this.AppliedModRemove_Click);
            // 
            // AppliedModAdd
            // 
            this.AppliedModAdd.Location = new System.Drawing.Point(244, 109);
            this.AppliedModAdd.Name = "AppliedModAdd";
            this.AppliedModAdd.Size = new System.Drawing.Size(32, 23);
            this.AppliedModAdd.TabIndex = 88;
            this.AppliedModAdd.Text = ">";
            this.AppliedModAdd.UseVisualStyleBackColor = true;
            this.AppliedModAdd.Click += new System.EventHandler(this.AppliedModAdd_Click);
            // 
            // StaticModsInfo
            // 
            this.StaticModsInfo.AutoSize = true;
            this.StaticModsInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.StaticModsInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StaticModsInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.StaticModsInfo.Location = new System.Drawing.Point(198, 231);
            this.StaticModsInfo.Name = "StaticModsInfo";
            this.StaticModsInfo.Size = new System.Drawing.Size(13, 13);
            this.StaticModsInfo.TabIndex = 75;
            this.StaticModsInfo.Text = "?";
            this.StaticModsInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.StaticModsInfo.Click += new System.EventHandler(this.Info_Click);
            this.StaticModsInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(145, 239);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 82;
            this.label1.Text = "Mod Type:";
            // 
            // ModTypeBox
            // 
            this.ModTypeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ModTypeBox.FormattingEnabled = true;
            this.ModTypeBox.Items.AddRange(new object[] {
            "Static",
            "Dynamic"});
            this.ModTypeBox.Location = new System.Drawing.Point(148, 254);
            this.ModTypeBox.Name = "ModTypeBox";
            this.ModTypeBox.Size = new System.Drawing.Size(90, 21);
            this.ModTypeBox.TabIndex = 81;
            // 
            // ModListLabel
            // 
            this.ModListLabel.AutoSize = true;
            this.ModListLabel.Location = new System.Drawing.Point(6, 16);
            this.ModListLabel.Name = "ModListLabel";
            this.ModListLabel.Size = new System.Drawing.Size(50, 13);
            this.ModListLabel.TabIndex = 54;
            this.ModListLabel.Text = "Mod List:";
            // 
            // ResidueLabel
            // 
            this.ResidueLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ResidueLabel.AutoSize = true;
            this.ResidueLabel.Location = new System.Drawing.Point(4, 239);
            this.ResidueLabel.Name = "ResidueLabel";
            this.ResidueLabel.Size = new System.Drawing.Size(76, 13);
            this.ResidueLabel.TabIndex = 57;
            this.ResidueLabel.Text = "Redidue Motif:";
            // 
            // ModMassLabel
            // 
            this.ModMassLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ModMassLabel.AutoSize = true;
            this.ModMassLabel.Location = new System.Drawing.Point(83, 239);
            this.ModMassLabel.Name = "ModMassLabel";
            this.ModMassLabel.Size = new System.Drawing.Size(59, 13);
            this.ModMassLabel.TabIndex = 58;
            this.ModMassLabel.Text = "Mod Mass:";
            // 
            // ModMassBox
            // 
            this.ModMassBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ModMassBox.Location = new System.Drawing.Point(86, 255);
            this.ModMassBox.Name = "ModMassBox";
            this.ModMassBox.Size = new System.Drawing.Size(56, 20);
            this.ModMassBox.TabIndex = 78;
            this.ModMassBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.ModMassBox.Leave += new System.EventHandler(this.NumericTextBox_Leave);
            this.ModMassBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.ModMassBox_KeyPress);
            // 
            // MaxDynamicModsBox
            // 
            this.MaxDynamicModsBox.Location = new System.Drawing.Point(463, 255);
            this.MaxDynamicModsBox.Name = "MaxDynamicModsBox";
            this.MaxDynamicModsBox.Size = new System.Drawing.Size(45, 20);
            this.MaxDynamicModsBox.TabIndex = 10;
            this.MaxDynamicModsBox.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.MaxDynamicModsBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MaxDynamicModsBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // ResidueBox
            // 
            this.ResidueBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ResidueBox.Location = new System.Drawing.Point(7, 255);
            this.ResidueBox.Name = "ResidueBox";
            this.ResidueBox.Size = new System.Drawing.Size(73, 20);
            this.ResidueBox.TabIndex = 2;
            this.ResidueBox.TextChanged += new System.EventHandler(this.ResidueBox_TextChanged);
            // 
            // ModList
            // 
            this.ModList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)));
            this.ModList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Description});
            this.ModList.FullRowSelect = true;
            this.ModList.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            listViewItem1,
            listViewItem2,
            listViewItem3,
            listViewItem4});
            this.ModList.Location = new System.Drawing.Point(7, 32);
            this.ModList.MultiSelect = false;
            this.ModList.Name = "ModList";
            this.ModList.Size = new System.Drawing.Size(231, 199);
            this.ModList.TabIndex = 77;
            this.ModList.UseCompatibleStateImageBehavior = false;
            this.ModList.View = System.Windows.Forms.View.Details;
            this.ModList.SelectedIndexChanged += new System.EventHandler(this.ModList_SelectedValueChanged);
            this.ModList.Click += new System.EventHandler(this.ModList_SelectedValueChanged);
            // 
            // Description
            // 
            this.Description.Text = "Description";
            this.Description.Width = 205;
            // 
            // MaxDynamicModsInfo
            // 
            this.MaxDynamicModsInfo.AutoSize = true;
            this.MaxDynamicModsInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MaxDynamicModsInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaxDynamicModsInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.MaxDynamicModsInfo.Location = new System.Drawing.Point(452, 244);
            this.MaxDynamicModsInfo.Name = "MaxDynamicModsInfo";
            this.MaxDynamicModsInfo.Size = new System.Drawing.Size(13, 13);
            this.MaxDynamicModsInfo.TabIndex = 75;
            this.MaxDynamicModsInfo.Text = "?";
            this.MaxDynamicModsInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.MaxDynamicModsInfo.Click += new System.EventHandler(this.Info_Click);
            this.MaxDynamicModsInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // MaxDynamicModsLabel
            // 
            this.MaxDynamicModsLabel.AutoSize = true;
            this.MaxDynamicModsLabel.Location = new System.Drawing.Point(409, 238);
            this.MaxDynamicModsLabel.Name = "MaxDynamicModsLabel";
            this.MaxDynamicModsLabel.Size = new System.Drawing.Size(48, 39);
            this.MaxDynamicModsLabel.TabIndex = 52;
            this.MaxDynamicModsLabel.Text = "Max\r\nDynamic\r\nMods:";
            // 
            // InstrumentGB
            // 
            this.InstrumentGB.Controls.Add(this.InstrumentPannel);
            this.InstrumentGB.Controls.Add(this.UseAvgMassOfSequencesBox);
            this.InstrumentGB.Controls.Add(this.UseAvgMassOfSequencesInfo);
            this.InstrumentGB.Controls.Add(this.UseAvgMassOfSequencesLabel);
            this.InstrumentGB.Location = new System.Drawing.Point(7, 6);
            this.InstrumentGB.Name = "InstrumentGB";
            this.InstrumentGB.Size = new System.Drawing.Size(254, 117);
            this.InstrumentGB.TabIndex = 0;
            this.InstrumentGB.TabStop = false;
            this.InstrumentGB.Text = "Instrument Specific";
            // 
            // InstrumentPannel
            // 
            this.InstrumentPannel.Controls.Add(this.InstrumentBox);
            this.InstrumentPannel.Controls.Add(this.InstrumentLabel);
            this.InstrumentPannel.Location = new System.Drawing.Point(39, 30);
            this.InstrumentPannel.Name = "InstrumentPannel";
            this.InstrumentPannel.Size = new System.Drawing.Size(181, 29);
            this.InstrumentPannel.TabIndex = 69;
            // 
            // InstrumentBox
            // 
            this.InstrumentBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.InstrumentBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.InstrumentBox.FormattingEnabled = true;
            this.InstrumentBox.Location = new System.Drawing.Point(58, 4);
            this.InstrumentBox.Name = "InstrumentBox";
            this.InstrumentBox.Size = new System.Drawing.Size(121, 21);
            this.InstrumentBox.TabIndex = 0;
            this.InstrumentBox.SelectedIndexChanged += new System.EventHandler(this.InstrumentBox_SelectedValueChanged);
            // 
            // InstrumentLabel
            // 
            this.InstrumentLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.InstrumentLabel.AutoSize = true;
            this.InstrumentLabel.Location = new System.Drawing.Point(-1, 7);
            this.InstrumentLabel.Name = "InstrumentLabel";
            this.InstrumentLabel.Size = new System.Drawing.Size(59, 13);
            this.InstrumentLabel.TabIndex = 45;
            this.InstrumentLabel.Text = "Instrument:";
            // 
            // UseAvgMassOfSequencesBox
            // 
            this.UseAvgMassOfSequencesBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.UseAvgMassOfSequencesBox.FormattingEnabled = true;
            this.UseAvgMassOfSequencesBox.Items.AddRange(new object[] {
            "Mono-Isotopic",
            "Average"});
            this.UseAvgMassOfSequencesBox.Location = new System.Drawing.Point(126, 61);
            this.UseAvgMassOfSequencesBox.Name = "UseAvgMassOfSequencesBox";
            this.UseAvgMassOfSequencesBox.Size = new System.Drawing.Size(92, 21);
            this.UseAvgMassOfSequencesBox.TabIndex = 1;
            this.UseAvgMassOfSequencesBox.SelectedIndexChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // UseAvgMassOfSequencesInfo
            // 
            this.UseAvgMassOfSequencesInfo.AutoSize = true;
            this.UseAvgMassOfSequencesInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.UseAvgMassOfSequencesInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.UseAvgMassOfSequencesInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.UseAvgMassOfSequencesInfo.Location = new System.Drawing.Point(115, 56);
            this.UseAvgMassOfSequencesInfo.Name = "UseAvgMassOfSequencesInfo";
            this.UseAvgMassOfSequencesInfo.Size = new System.Drawing.Size(13, 13);
            this.UseAvgMassOfSequencesInfo.TabIndex = 68;
            this.UseAvgMassOfSequencesInfo.Text = "?";
            this.UseAvgMassOfSequencesInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.UseAvgMassOfSequencesInfo.Click += new System.EventHandler(this.Info_Click);
            this.UseAvgMassOfSequencesInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // UseAvgMassOfSequencesLabel
            // 
            this.UseAvgMassOfSequencesLabel.AutoSize = true;
            this.UseAvgMassOfSequencesLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.UseAvgMassOfSequencesLabel.Location = new System.Drawing.Point(37, 64);
            this.UseAvgMassOfSequencesLabel.Name = "UseAvgMassOfSequencesLabel";
            this.UseAvgMassOfSequencesLabel.Size = new System.Drawing.Size(83, 13);
            this.UseAvgMassOfSequencesLabel.TabIndex = 42;
            this.UseAvgMassOfSequencesLabel.Text = "Precursor Mass:";
            // 
            // DigestionGB
            // 
            this.DigestionGB.Controls.Add(this.NumMaxMissedCleavagesAuto);
            this.DigestionGB.Controls.Add(this.NumMaxMissedCleavagesBox);
            this.DigestionGB.Controls.Add(this.NumMinTerminiCleavagesBox);
            this.DigestionGB.Controls.Add(this.CleavageRulesBox);
            this.DigestionGB.Controls.Add(this.NumMaxMissedCleavagesInfo);
            this.DigestionGB.Controls.Add(this.NumMinTerminiCleavagesInfo);
            this.DigestionGB.Controls.Add(this.CleavageRulesInfo);
            this.DigestionGB.Controls.Add(this.CleavageRulesLabel);
            this.DigestionGB.Controls.Add(this.NumMaxMissedCleavagesLabel);
            this.DigestionGB.Controls.Add(this.NumMinTerminiCleavagesLabel);
            this.DigestionGB.Location = new System.Drawing.Point(267, 6);
            this.DigestionGB.Name = "DigestionGB";
            this.DigestionGB.Size = new System.Drawing.Size(254, 117);
            this.DigestionGB.TabIndex = 1;
            this.DigestionGB.TabStop = false;
            this.DigestionGB.Text = "Digestion";
            // 
            // NumMaxMissedCleavagesAuto
            // 
            this.NumMaxMissedCleavagesAuto.BackColor = System.Drawing.Color.White;
            this.NumMaxMissedCleavagesAuto.Enabled = false;
            this.NumMaxMissedCleavagesAuto.Font = new System.Drawing.Font("Calibri", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NumMaxMissedCleavagesAuto.Location = new System.Drawing.Point(183, 85);
            this.NumMaxMissedCleavagesAuto.Name = "NumMaxMissedCleavagesAuto";
            this.NumMaxMissedCleavagesAuto.Size = new System.Drawing.Size(23, 18);
            this.NumMaxMissedCleavagesAuto.TabIndex = 78;
            this.NumMaxMissedCleavagesAuto.Text = "∞";
            // 
            // NumMaxMissedCleavagesBox
            // 
            this.NumMaxMissedCleavagesBox.Enabled = false;
            this.NumMaxMissedCleavagesBox.Location = new System.Drawing.Point(182, 84);
            this.NumMaxMissedCleavagesBox.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.NumMaxMissedCleavagesBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.NumMaxMissedCleavagesBox.Name = "NumMaxMissedCleavagesBox";
            this.NumMaxMissedCleavagesBox.Size = new System.Drawing.Size(40, 20);
            this.NumMaxMissedCleavagesBox.TabIndex = 2;
            this.NumMaxMissedCleavagesBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.NumMaxMissedCleavagesBox.ValueChanged += new System.EventHandler(this.NumMaxMissedCleavagesBox_ValueChanged);
            // 
            // NumMinTerminiCleavagesBox
            // 
            this.NumMinTerminiCleavagesBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.NumMinTerminiCleavagesBox.FormattingEnabled = true;
            this.NumMinTerminiCleavagesBox.Items.AddRange(new object[] {
            "Non-Specific",
            "Semi-Specific",
            "Fully-Specific"});
            this.NumMinTerminiCleavagesBox.Location = new System.Drawing.Point(130, 51);
            this.NumMinTerminiCleavagesBox.Name = "NumMinTerminiCleavagesBox";
            this.NumMinTerminiCleavagesBox.Size = new System.Drawing.Size(92, 21);
            this.NumMinTerminiCleavagesBox.TabIndex = 1;
            this.NumMinTerminiCleavagesBox.SelectedIndexChanged += new System.EventHandler(this.NumMinTerminiCleavagesBox_SelectedIndexChanged);
            // 
            // CleavageRulesBox
            // 
            this.CleavageRulesBox.FormattingEnabled = true;
            this.CleavageRulesBox.Items.AddRange(new object[] {
            "Trypsin",
            "Trypsin/P",
            "Chymotrypsin",
            "TrypChymo",
            "Lys-C",
            "Lys-C/P",
            "Asp-N",
            "PepsinA",
            "CNBr",
            "Formic_acid",
            "NoEnzyme"});
            this.CleavageRulesBox.Location = new System.Drawing.Point(82, 19);
            this.CleavageRulesBox.Name = "CleavageRulesBox";
            this.CleavageRulesBox.Size = new System.Drawing.Size(140, 21);
            this.CleavageRulesBox.TabIndex = 0;
            this.CleavageRulesBox.SelectedIndexChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // NumMaxMissedCleavagesInfo
            // 
            this.NumMaxMissedCleavagesInfo.AutoSize = true;
            this.NumMaxMissedCleavagesInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.NumMaxMissedCleavagesInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NumMaxMissedCleavagesInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.NumMaxMissedCleavagesInfo.Location = new System.Drawing.Point(171, 78);
            this.NumMaxMissedCleavagesInfo.Name = "NumMaxMissedCleavagesInfo";
            this.NumMaxMissedCleavagesInfo.Size = new System.Drawing.Size(13, 13);
            this.NumMaxMissedCleavagesInfo.TabIndex = 71;
            this.NumMaxMissedCleavagesInfo.Text = "?";
            this.NumMaxMissedCleavagesInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.NumMaxMissedCleavagesInfo.Click += new System.EventHandler(this.Info_Click);
            this.NumMaxMissedCleavagesInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // NumMinTerminiCleavagesInfo
            // 
            this.NumMinTerminiCleavagesInfo.AutoSize = true;
            this.NumMinTerminiCleavagesInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.NumMinTerminiCleavagesInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NumMinTerminiCleavagesInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.NumMinTerminiCleavagesInfo.Location = new System.Drawing.Point(119, 47);
            this.NumMinTerminiCleavagesInfo.Name = "NumMinTerminiCleavagesInfo";
            this.NumMinTerminiCleavagesInfo.Size = new System.Drawing.Size(13, 13);
            this.NumMinTerminiCleavagesInfo.TabIndex = 70;
            this.NumMinTerminiCleavagesInfo.Text = "?";
            this.NumMinTerminiCleavagesInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.NumMinTerminiCleavagesInfo.Click += new System.EventHandler(this.Info_Click);
            this.NumMinTerminiCleavagesInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // CleavageRulesInfo
            // 
            this.CleavageRulesInfo.AutoSize = true;
            this.CleavageRulesInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.CleavageRulesInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CleavageRulesInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.CleavageRulesInfo.Location = new System.Drawing.Point(71, 14);
            this.CleavageRulesInfo.Name = "CleavageRulesInfo";
            this.CleavageRulesInfo.Size = new System.Drawing.Size(13, 13);
            this.CleavageRulesInfo.TabIndex = 69;
            this.CleavageRulesInfo.Text = "?";
            this.CleavageRulesInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.CleavageRulesInfo.Click += new System.EventHandler(this.Info_Click);
            this.CleavageRulesInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // CleavageRulesLabel
            // 
            this.CleavageRulesLabel.AutoSize = true;
            this.CleavageRulesLabel.Location = new System.Drawing.Point(29, 22);
            this.CleavageRulesLabel.Name = "CleavageRulesLabel";
            this.CleavageRulesLabel.Size = new System.Drawing.Size(47, 13);
            this.CleavageRulesLabel.TabIndex = 0;
            this.CleavageRulesLabel.Text = "Enzyme:";
            // 
            // NumMaxMissedCleavagesLabel
            // 
            this.NumMaxMissedCleavagesLabel.AutoSize = true;
            this.NumMaxMissedCleavagesLabel.Location = new System.Drawing.Point(57, 86);
            this.NumMaxMissedCleavagesLabel.Name = "NumMaxMissedCleavagesLabel";
            this.NumMaxMissedCleavagesLabel.Size = new System.Drawing.Size(119, 13);
            this.NumMaxMissedCleavagesLabel.TabIndex = 6;
            this.NumMaxMissedCleavagesLabel.Text = "Max Missed Cleavages:";
            // 
            // NumMinTerminiCleavagesLabel
            // 
            this.NumMinTerminiCleavagesLabel.AutoSize = true;
            this.NumMinTerminiCleavagesLabel.Location = new System.Drawing.Point(66, 55);
            this.NumMinTerminiCleavagesLabel.Name = "NumMinTerminiCleavagesLabel";
            this.NumMinTerminiCleavagesLabel.Size = new System.Drawing.Size(58, 13);
            this.NumMinTerminiCleavagesLabel.TabIndex = 1;
            this.NumMinTerminiCleavagesLabel.Text = "Specificity:";
            // 
            // ToleranceGB
            // 
            this.ToleranceGB.Controls.Add(this.PrecursorPannel);
            this.ToleranceGB.Controls.Add(this.FragmentPannel);
            this.ToleranceGB.Controls.Add(this.TagReconTolerancePanel);
            this.ToleranceGB.Location = new System.Drawing.Point(7, 129);
            this.ToleranceGB.Name = "ToleranceGB";
            this.ToleranceGB.Size = new System.Drawing.Size(514, 76);
            this.ToleranceGB.TabIndex = 2;
            this.ToleranceGB.TabStop = false;
            this.ToleranceGB.Text = "Tolerance";
            // 
            // PrecursorPannel
            // 
            this.PrecursorPannel.Controls.Add(this.PrecursorMzToleranceBox);
            this.PrecursorPannel.Controls.Add(this.PrecursorMzToleranceUnitsBox);
            this.PrecursorPannel.Controls.Add(this.PrecursorMzToleranceLabel);
            this.PrecursorPannel.Controls.Add(this.PrecursorMzToleranceInfo);
            this.PrecursorPannel.Location = new System.Drawing.Point(7, 10);
            this.PrecursorPannel.Name = "PrecursorPannel";
            this.PrecursorPannel.Size = new System.Drawing.Size(245, 26);
            this.PrecursorPannel.TabIndex = 81;
            // 
            // PrecursorMzToleranceBox
            // 
            this.PrecursorMzToleranceBox.Enabled = false;
            this.PrecursorMzToleranceBox.Location = new System.Drawing.Point(126, 4);
            this.PrecursorMzToleranceBox.Name = "PrecursorMzToleranceBox";
            this.PrecursorMzToleranceBox.Size = new System.Drawing.Size(54, 20);
            this.PrecursorMzToleranceBox.TabIndex = 73;
            this.PrecursorMzToleranceBox.Text = "1.25";
            this.PrecursorMzToleranceBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.PrecursorMzToleranceBox.Leave += new System.EventHandler(this.NumericTextBox_Leave);
            this.PrecursorMzToleranceBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // PrecursorMzToleranceUnitsBox
            // 
            this.PrecursorMzToleranceUnitsBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.PrecursorMzToleranceUnitsBox.Enabled = false;
            this.PrecursorMzToleranceUnitsBox.FormattingEnabled = true;
            this.PrecursorMzToleranceUnitsBox.Items.AddRange(new object[] {
            "daltons",
            "ppm"});
            this.PrecursorMzToleranceUnitsBox.Location = new System.Drawing.Point(186, 4);
            this.PrecursorMzToleranceUnitsBox.Name = "PrecursorMzToleranceUnitsBox";
            this.PrecursorMzToleranceUnitsBox.Size = new System.Drawing.Size(58, 21);
            this.PrecursorMzToleranceUnitsBox.TabIndex = 3;
            this.PrecursorMzToleranceUnitsBox.SelectionChangeCommitted += new System.EventHandler(this.ValueBox_Leave);
            // 
            // PrecursorMzToleranceLabel
            // 
            this.PrecursorMzToleranceLabel.AutoSize = true;
            this.PrecursorMzToleranceLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.PrecursorMzToleranceLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.PrecursorMzToleranceLabel.Location = new System.Drawing.Point(-3, 7);
            this.PrecursorMzToleranceLabel.Name = "PrecursorMzToleranceLabel";
            this.PrecursorMzToleranceLabel.Size = new System.Drawing.Size(123, 13);
            this.PrecursorMzToleranceLabel.TabIndex = 0;
            this.PrecursorMzToleranceLabel.Text = "Precursor m/z tolerance:";
            // 
            // PrecursorMzToleranceInfo
            // 
            this.PrecursorMzToleranceInfo.AutoSize = true;
            this.PrecursorMzToleranceInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.PrecursorMzToleranceInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.PrecursorMzToleranceInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.PrecursorMzToleranceInfo.Location = new System.Drawing.Point(115, -1);
            this.PrecursorMzToleranceInfo.Name = "PrecursorMzToleranceInfo";
            this.PrecursorMzToleranceInfo.Size = new System.Drawing.Size(13, 13);
            this.PrecursorMzToleranceInfo.TabIndex = 72;
            this.PrecursorMzToleranceInfo.Text = "?";
            this.PrecursorMzToleranceInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.PrecursorMzToleranceInfo.Click += new System.EventHandler(this.Info_Click);
            this.PrecursorMzToleranceInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // FragmentPannel
            // 
            this.FragmentPannel.Controls.Add(this.FragmentMzToleranceBox);
            this.FragmentPannel.Controls.Add(this.FragmentMzToleranceUnitsBox);
            this.FragmentPannel.Controls.Add(this.FragmentMzToleranceInfo);
            this.FragmentPannel.Controls.Add(this.FragmentMzToleranceLabel);
            this.FragmentPannel.Location = new System.Drawing.Point(262, 10);
            this.FragmentPannel.Name = "FragmentPannel";
            this.FragmentPannel.Size = new System.Drawing.Size(246, 26);
            this.FragmentPannel.TabIndex = 47;
            // 
            // FragmentMzToleranceBox
            // 
            this.FragmentMzToleranceBox.Enabled = false;
            this.FragmentMzToleranceBox.Location = new System.Drawing.Point(127, 3);
            this.FragmentMzToleranceBox.Name = "FragmentMzToleranceBox";
            this.FragmentMzToleranceBox.Size = new System.Drawing.Size(54, 20);
            this.FragmentMzToleranceBox.TabIndex = 74;
            this.FragmentMzToleranceBox.Text = "1.25";
            this.FragmentMzToleranceBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.FragmentMzToleranceBox.Leave += new System.EventHandler(this.NumericTextBox_Leave);
            this.FragmentMzToleranceBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // FragmentMzToleranceUnitsBox
            // 
            this.FragmentMzToleranceUnitsBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.FragmentMzToleranceUnitsBox.Enabled = false;
            this.FragmentMzToleranceUnitsBox.FormattingEnabled = true;
            this.FragmentMzToleranceUnitsBox.Items.AddRange(new object[] {
            "daltons",
            "ppm"});
            this.FragmentMzToleranceUnitsBox.Location = new System.Drawing.Point(188, 3);
            this.FragmentMzToleranceUnitsBox.Name = "FragmentMzToleranceUnitsBox";
            this.FragmentMzToleranceUnitsBox.Size = new System.Drawing.Size(58, 21);
            this.FragmentMzToleranceUnitsBox.TabIndex = 7;
            this.FragmentMzToleranceUnitsBox.SelectionChangeCommitted += new System.EventHandler(this.ValueBox_Leave);
            // 
            // FragmentMzToleranceInfo
            // 
            this.FragmentMzToleranceInfo.AutoSize = true;
            this.FragmentMzToleranceInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.FragmentMzToleranceInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FragmentMzToleranceInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.FragmentMzToleranceInfo.Location = new System.Drawing.Point(116, -1);
            this.FragmentMzToleranceInfo.Name = "FragmentMzToleranceInfo";
            this.FragmentMzToleranceInfo.Size = new System.Drawing.Size(13, 13);
            this.FragmentMzToleranceInfo.TabIndex = 73;
            this.FragmentMzToleranceInfo.Text = "?";
            this.FragmentMzToleranceInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.FragmentMzToleranceInfo.Click += new System.EventHandler(this.Info_Click);
            this.FragmentMzToleranceInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // FragmentMzToleranceLabel
            // 
            this.FragmentMzToleranceLabel.AutoSize = true;
            this.FragmentMzToleranceLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FragmentMzToleranceLabel.Location = new System.Drawing.Point(-1, 7);
            this.FragmentMzToleranceLabel.Name = "FragmentMzToleranceLabel";
            this.FragmentMzToleranceLabel.Size = new System.Drawing.Size(122, 13);
            this.FragmentMzToleranceLabel.TabIndex = 5;
            this.FragmentMzToleranceLabel.Text = "Fragment m/z tolerance:";
            // 
            // TagReconTolerancePanel
            // 
            this.TagReconTolerancePanel.Controls.Add(this.CTerminusMzToleranceBox);
            this.TagReconTolerancePanel.Controls.Add(this.NTerminusMzToleranceBox);
            this.TagReconTolerancePanel.Controls.Add(this.CTerminusMzToleranceUnitsBox);
            this.TagReconTolerancePanel.Controls.Add(this.NTerminusMzToleranceUnitsBox);
            this.TagReconTolerancePanel.Controls.Add(this.CTerminusMzToleranceInfo);
            this.TagReconTolerancePanel.Controls.Add(this.NTerminusMzToleranceInfo);
            this.TagReconTolerancePanel.Controls.Add(this.CTerminusMzToleranceLabel);
            this.TagReconTolerancePanel.Controls.Add(this.NTerminusMzToleranceLabel);
            this.TagReconTolerancePanel.Location = new System.Drawing.Point(3, 37);
            this.TagReconTolerancePanel.Name = "TagReconTolerancePanel";
            this.TagReconTolerancePanel.Size = new System.Drawing.Size(505, 30);
            this.TagReconTolerancePanel.TabIndex = 15;
            this.TagReconTolerancePanel.Visible = false;
            // 
            // CTerminusMzToleranceBox
            // 
            this.CTerminusMzToleranceBox.Enabled = false;
            this.CTerminusMzToleranceBox.Location = new System.Drawing.Point(385, 4);
            this.CTerminusMzToleranceBox.Name = "CTerminusMzToleranceBox";
            this.CTerminusMzToleranceBox.Size = new System.Drawing.Size(55, 20);
            this.CTerminusMzToleranceBox.TabIndex = 77;
            this.CTerminusMzToleranceBox.Text = "0.5";
            this.CTerminusMzToleranceBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.CTerminusMzToleranceBox.Leave += new System.EventHandler(this.NumericTextBox_Leave);
            this.CTerminusMzToleranceBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // NTerminusMzToleranceBox
            // 
            this.NTerminusMzToleranceBox.Enabled = false;
            this.NTerminusMzToleranceBox.Location = new System.Drawing.Point(130, 4);
            this.NTerminusMzToleranceBox.Name = "NTerminusMzToleranceBox";
            this.NTerminusMzToleranceBox.Size = new System.Drawing.Size(54, 20);
            this.NTerminusMzToleranceBox.TabIndex = 76;
            this.NTerminusMzToleranceBox.Text = "0.75";
            this.NTerminusMzToleranceBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.NTerminusMzToleranceBox.Leave += new System.EventHandler(this.NumericTextBox_Leave);
            this.NTerminusMzToleranceBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // CTerminusMzToleranceUnitsBox
            // 
            this.CTerminusMzToleranceUnitsBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CTerminusMzToleranceUnitsBox.Enabled = false;
            this.CTerminusMzToleranceUnitsBox.FormattingEnabled = true;
            this.CTerminusMzToleranceUnitsBox.Items.AddRange(new object[] {
            "daltons"});
            this.CTerminusMzToleranceUnitsBox.Location = new System.Drawing.Point(447, 4);
            this.CTerminusMzToleranceUnitsBox.Name = "CTerminusMzToleranceUnitsBox";
            this.CTerminusMzToleranceUnitsBox.Size = new System.Drawing.Size(58, 21);
            this.CTerminusMzToleranceUnitsBox.TabIndex = 75;
            // 
            // NTerminusMzToleranceUnitsBox
            // 
            this.NTerminusMzToleranceUnitsBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.NTerminusMzToleranceUnitsBox.Enabled = false;
            this.NTerminusMzToleranceUnitsBox.FormattingEnabled = true;
            this.NTerminusMzToleranceUnitsBox.Items.AddRange(new object[] {
            "daltons"});
            this.NTerminusMzToleranceUnitsBox.Location = new System.Drawing.Point(190, 4);
            this.NTerminusMzToleranceUnitsBox.Name = "NTerminusMzToleranceUnitsBox";
            this.NTerminusMzToleranceUnitsBox.Size = new System.Drawing.Size(58, 21);
            this.NTerminusMzToleranceUnitsBox.TabIndex = 73;
            // 
            // CTerminusMzToleranceInfo
            // 
            this.CTerminusMzToleranceInfo.AutoSize = true;
            this.CTerminusMzToleranceInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.CTerminusMzToleranceInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CTerminusMzToleranceInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.CTerminusMzToleranceInfo.Location = new System.Drawing.Point(375, 1);
            this.CTerminusMzToleranceInfo.Name = "CTerminusMzToleranceInfo";
            this.CTerminusMzToleranceInfo.Size = new System.Drawing.Size(13, 13);
            this.CTerminusMzToleranceInfo.TabIndex = 74;
            this.CTerminusMzToleranceInfo.Text = "?";
            this.CTerminusMzToleranceInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.CTerminusMzToleranceInfo.Click += new System.EventHandler(this.Info_Click);
            this.CTerminusMzToleranceInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // NTerminusMzToleranceInfo
            // 
            this.NTerminusMzToleranceInfo.AutoSize = true;
            this.NTerminusMzToleranceInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.NTerminusMzToleranceInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NTerminusMzToleranceInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.NTerminusMzToleranceInfo.Location = new System.Drawing.Point(119, -1);
            this.NTerminusMzToleranceInfo.Name = "NTerminusMzToleranceInfo";
            this.NTerminusMzToleranceInfo.Size = new System.Drawing.Size(13, 13);
            this.NTerminusMzToleranceInfo.TabIndex = 73;
            this.NTerminusMzToleranceInfo.Text = "?";
            this.NTerminusMzToleranceInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.NTerminusMzToleranceInfo.Click += new System.EventHandler(this.Info_Click);
            this.NTerminusMzToleranceInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // CTerminusMzToleranceLabel
            // 
            this.CTerminusMzToleranceLabel.AutoSize = true;
            this.CTerminusMzToleranceLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CTerminusMzToleranceLabel.Location = new System.Drawing.Point(254, 7);
            this.CTerminusMzToleranceLabel.Name = "CTerminusMzToleranceLabel";
            this.CTerminusMzToleranceLabel.Size = new System.Drawing.Size(126, 13);
            this.CTerminusMzToleranceLabel.TabIndex = 6;
            this.CTerminusMzToleranceLabel.Text = "C-Terminus m/z tolerance:";
            // 
            // NTerminusMzToleranceLabel
            // 
            this.NTerminusMzToleranceLabel.AutoSize = true;
            this.NTerminusMzToleranceLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NTerminusMzToleranceLabel.Location = new System.Drawing.Point(-2, 7);
            this.NTerminusMzToleranceLabel.Name = "NTerminusMzToleranceLabel";
            this.NTerminusMzToleranceLabel.Size = new System.Drawing.Size(126, 13);
            this.NTerminusMzToleranceLabel.TabIndex = 1;
            this.NTerminusMzToleranceLabel.Text = "N-Terminus m/z tolerance:";
            // 
            // AdvTab
            // 
            this.AdvTab.Controls.Add(this.ChargeGB);
            this.AdvTab.Controls.Add(this.SubsetGB);
            this.AdvTab.Controls.Add(this.SequenceGB);
            this.AdvTab.Controls.Add(this.MiscGB);
            this.AdvTab.Controls.Add(this.PrecursorGbox);
            this.AdvTab.Controls.Add(this.TagReconGB);
            this.AdvTab.Controls.Add(this.DirecTagGB);
            this.AdvTab.Controls.Add(this.TRModOptionsGB);
            this.AdvTab.Controls.Add(this.ScoringGB);
            this.AdvTab.Location = new System.Drawing.Point(4, 22);
            this.AdvTab.Name = "AdvTab";
            this.AdvTab.Padding = new System.Windows.Forms.Padding(3);
            this.AdvTab.Size = new System.Drawing.Size(528, 539);
            this.AdvTab.TabIndex = 1;
            this.AdvTab.Text = "Advanced";
            this.AdvTab.UseVisualStyleBackColor = true;
            // 
            // ChargeGB
            // 
            this.ChargeGB.Controls.Add(this.NumChargeStatesBox);
            this.ChargeGB.Controls.Add(this.DuplicateSpectraBox);
            this.ChargeGB.Controls.Add(this.DuplicateSpectraInfo);
            this.ChargeGB.Controls.Add(this.NumChargeStatesInfo);
            this.ChargeGB.Controls.Add(this.UseChargeStateFromMSBox);
            this.ChargeGB.Controls.Add(this.DuplicateSpectraLabel);
            this.ChargeGB.Controls.Add(this.UseChargeStateFromMSInfo);
            this.ChargeGB.Controls.Add(this.NumChargeStatesLabel);
            this.ChargeGB.Controls.Add(this.UseChargeStateFromMSLabel);
            this.ChargeGB.Location = new System.Drawing.Point(284, 452);
            this.ChargeGB.Name = "ChargeGB";
            this.ChargeGB.Size = new System.Drawing.Size(241, 80);
            this.ChargeGB.TabIndex = 75;
            this.ChargeGB.TabStop = false;
            this.ChargeGB.Text = "Charge State Handling";
            // 
            // NumChargeStatesBox
            // 
            this.NumChargeStatesBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.NumChargeStatesBox.Enabled = false;
            this.NumChargeStatesBox.Location = new System.Drawing.Point(188, 34);
            this.NumChargeStatesBox.Maximum = new decimal(new int[] {
            8,
            0,
            0,
            0});
            this.NumChargeStatesBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.NumChargeStatesBox.Name = "NumChargeStatesBox";
            this.NumChargeStatesBox.Size = new System.Drawing.Size(42, 20);
            this.NumChargeStatesBox.TabIndex = 1;
            this.NumChargeStatesBox.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.NumChargeStatesBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.NumChargeStatesBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // DuplicateSpectraBox
            // 
            this.DuplicateSpectraBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DuplicateSpectraBox.AutoSize = true;
            this.DuplicateSpectraBox.Checked = true;
            this.DuplicateSpectraBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.DuplicateSpectraBox.Enabled = false;
            this.DuplicateSpectraBox.Location = new System.Drawing.Point(216, 14);
            this.DuplicateSpectraBox.Name = "DuplicateSpectraBox";
            this.DuplicateSpectraBox.Size = new System.Drawing.Size(15, 14);
            this.DuplicateSpectraBox.TabIndex = 0;
            this.DuplicateSpectraBox.UseVisualStyleBackColor = true;
            this.DuplicateSpectraBox.CheckedChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // DuplicateSpectraInfo
            // 
            this.DuplicateSpectraInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DuplicateSpectraInfo.AutoSize = true;
            this.DuplicateSpectraInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.DuplicateSpectraInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DuplicateSpectraInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.DuplicateSpectraInfo.Location = new System.Drawing.Point(205, 7);
            this.DuplicateSpectraInfo.Name = "DuplicateSpectraInfo";
            this.DuplicateSpectraInfo.Size = new System.Drawing.Size(13, 13);
            this.DuplicateSpectraInfo.TabIndex = 103;
            this.DuplicateSpectraInfo.Text = "?";
            this.DuplicateSpectraInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.DuplicateSpectraInfo.Click += new System.EventHandler(this.Info_Click);
            this.DuplicateSpectraInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // NumChargeStatesInfo
            // 
            this.NumChargeStatesInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.NumChargeStatesInfo.AutoSize = true;
            this.NumChargeStatesInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.NumChargeStatesInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NumChargeStatesInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.NumChargeStatesInfo.Location = new System.Drawing.Point(177, 28);
            this.NumChargeStatesInfo.Name = "NumChargeStatesInfo";
            this.NumChargeStatesInfo.Size = new System.Drawing.Size(13, 13);
            this.NumChargeStatesInfo.TabIndex = 104;
            this.NumChargeStatesInfo.Text = "?";
            this.NumChargeStatesInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.NumChargeStatesInfo.Click += new System.EventHandler(this.Info_Click);
            this.NumChargeStatesInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // UseChargeStateFromMSBox
            // 
            this.UseChargeStateFromMSBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.UseChargeStateFromMSBox.AutoSize = true;
            this.UseChargeStateFromMSBox.Enabled = false;
            this.UseChargeStateFromMSBox.Location = new System.Drawing.Point(216, 62);
            this.UseChargeStateFromMSBox.Name = "UseChargeStateFromMSBox";
            this.UseChargeStateFromMSBox.Size = new System.Drawing.Size(15, 14);
            this.UseChargeStateFromMSBox.TabIndex = 0;
            this.UseChargeStateFromMSBox.UseVisualStyleBackColor = true;
            // 
            // DuplicateSpectraLabel
            // 
            this.DuplicateSpectraLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DuplicateSpectraLabel.AutoSize = true;
            this.DuplicateSpectraLabel.Location = new System.Drawing.Point(115, 15);
            this.DuplicateSpectraLabel.Name = "DuplicateSpectraLabel";
            this.DuplicateSpectraLabel.Size = new System.Drawing.Size(95, 13);
            this.DuplicateSpectraLabel.TabIndex = 100;
            this.DuplicateSpectraLabel.Text = "Duplicate Spectra:";
            // 
            // UseChargeStateFromMSInfo
            // 
            this.UseChargeStateFromMSInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.UseChargeStateFromMSInfo.AutoSize = true;
            this.UseChargeStateFromMSInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.UseChargeStateFromMSInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.UseChargeStateFromMSInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.UseChargeStateFromMSInfo.Location = new System.Drawing.Point(205, 54);
            this.UseChargeStateFromMSInfo.Name = "UseChargeStateFromMSInfo";
            this.UseChargeStateFromMSInfo.Size = new System.Drawing.Size(13, 13);
            this.UseChargeStateFromMSInfo.TabIndex = 100;
            this.UseChargeStateFromMSInfo.Text = "?";
            this.UseChargeStateFromMSInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.UseChargeStateFromMSInfo.Click += new System.EventHandler(this.Info_Click);
            this.UseChargeStateFromMSInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // NumChargeStatesLabel
            // 
            this.NumChargeStatesLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.NumChargeStatesLabel.AutoSize = true;
            this.NumChargeStatesLabel.Location = new System.Drawing.Point(53, 36);
            this.NumChargeStatesLabel.Name = "NumChargeStatesLabel";
            this.NumChargeStatesLabel.Size = new System.Drawing.Size(129, 13);
            this.NumChargeStatesLabel.TabIndex = 99;
            this.NumChargeStatesLabel.Text = "Number of Charge States:";
            // 
            // UseChargeStateFromMSLabel
            // 
            this.UseChargeStateFromMSLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.UseChargeStateFromMSLabel.AutoSize = true;
            this.UseChargeStateFromMSLabel.Location = new System.Drawing.Point(71, 62);
            this.UseChargeStateFromMSLabel.Name = "UseChargeStateFromMSLabel";
            this.UseChargeStateFromMSLabel.Size = new System.Drawing.Size(139, 13);
            this.UseChargeStateFromMSLabel.TabIndex = 98;
            this.UseChargeStateFromMSLabel.Text = "Use Charge State From MS:";
            // 
            // SubsetGB
            // 
            this.SubsetGB.Controls.Add(this.EndSpectraScanNumAuto);
            this.SubsetGB.Controls.Add(this.EndProteinIndexAuto);
            this.SubsetGB.Controls.Add(this.EndProteinIndexBox);
            this.SubsetGB.Controls.Add(this.EndProteinIndexInfo);
            this.SubsetGB.Controls.Add(this.StartProteinIndexBox);
            this.SubsetGB.Controls.Add(this.StartProteinIndexInfo);
            this.SubsetGB.Controls.Add(this.EndSpectraScanNumBox);
            this.SubsetGB.Controls.Add(this.EndSpectraScanNumInfo);
            this.SubsetGB.Controls.Add(this.StartSpectraScanNumBox);
            this.SubsetGB.Controls.Add(this.StartSpectraScanNumInfo);
            this.SubsetGB.Controls.Add(this.EndProteinIndexLabel);
            this.SubsetGB.Controls.Add(this.StartProteinIndexLabel);
            this.SubsetGB.Controls.Add(this.StartSpectraScanNumLabel);
            this.SubsetGB.Controls.Add(this.EndSpectraScanNumLabel);
            this.SubsetGB.Location = new System.Drawing.Point(282, 6);
            this.SubsetGB.Name = "SubsetGB";
            this.SubsetGB.Size = new System.Drawing.Size(242, 127);
            this.SubsetGB.TabIndex = 2;
            this.SubsetGB.TabStop = false;
            this.SubsetGB.Text = "Subset";
            // 
            // EndSpectraScanNumAuto
            // 
            this.EndSpectraScanNumAuto.BackColor = System.Drawing.Color.White;
            this.EndSpectraScanNumAuto.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EndSpectraScanNumAuto.Location = new System.Drawing.Point(169, 42);
            this.EndSpectraScanNumAuto.Name = "EndSpectraScanNumAuto";
            this.EndSpectraScanNumAuto.Size = new System.Drawing.Size(38, 15);
            this.EndSpectraScanNumAuto.TabIndex = 78;
            this.EndSpectraScanNumAuto.Text = "Auto";
            // 
            // EndProteinIndexAuto
            // 
            this.EndProteinIndexAuto.BackColor = System.Drawing.Color.White;
            this.EndProteinIndexAuto.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EndProteinIndexAuto.Location = new System.Drawing.Point(169, 94);
            this.EndProteinIndexAuto.Name = "EndProteinIndexAuto";
            this.EndProteinIndexAuto.Size = new System.Drawing.Size(38, 15);
            this.EndProteinIndexAuto.TabIndex = 77;
            this.EndProteinIndexAuto.Text = "Auto";
            // 
            // EndProteinIndexBox
            // 
            this.EndProteinIndexBox.Location = new System.Drawing.Point(168, 92);
            this.EndProteinIndexBox.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.EndProteinIndexBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.EndProteinIndexBox.Name = "EndProteinIndexBox";
            this.EndProteinIndexBox.Size = new System.Drawing.Size(62, 20);
            this.EndProteinIndexBox.TabIndex = 8;
            this.EndProteinIndexBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.EndProteinIndexBox.ValueChanged += new System.EventHandler(this.EndProteinIndexBox_ValueChanged);
            this.EndProteinIndexBox.Leave += new System.EventHandler(this.EndProteinIndexBox_Leave);
            // 
            // EndProteinIndexInfo
            // 
            this.EndProteinIndexInfo.AutoSize = true;
            this.EndProteinIndexInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.EndProteinIndexInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EndProteinIndexInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.EndProteinIndexInfo.Location = new System.Drawing.Point(157, 86);
            this.EndProteinIndexInfo.Name = "EndProteinIndexInfo";
            this.EndProteinIndexInfo.Size = new System.Drawing.Size(13, 13);
            this.EndProteinIndexInfo.TabIndex = 76;
            this.EndProteinIndexInfo.Text = "?";
            this.EndProteinIndexInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.EndProteinIndexInfo.Click += new System.EventHandler(this.Info_Click);
            this.EndProteinIndexInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // StartProteinIndexBox
            // 
            this.StartProteinIndexBox.Location = new System.Drawing.Point(168, 66);
            this.StartProteinIndexBox.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.StartProteinIndexBox.Name = "StartProteinIndexBox";
            this.StartProteinIndexBox.Size = new System.Drawing.Size(62, 20);
            this.StartProteinIndexBox.TabIndex = 4;
            this.StartProteinIndexBox.Leave += new System.EventHandler(this.StartProteinIndexBox_Leave);
            // 
            // StartProteinIndexInfo
            // 
            this.StartProteinIndexInfo.AutoSize = true;
            this.StartProteinIndexInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.StartProteinIndexInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StartProteinIndexInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.StartProteinIndexInfo.Location = new System.Drawing.Point(157, 60);
            this.StartProteinIndexInfo.Name = "StartProteinIndexInfo";
            this.StartProteinIndexInfo.Size = new System.Drawing.Size(13, 13);
            this.StartProteinIndexInfo.TabIndex = 75;
            this.StartProteinIndexInfo.Text = "?";
            this.StartProteinIndexInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.StartProteinIndexInfo.Click += new System.EventHandler(this.Info_Click);
            this.StartProteinIndexInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // EndSpectraScanNumBox
            // 
            this.EndSpectraScanNumBox.Location = new System.Drawing.Point(168, 40);
            this.EndSpectraScanNumBox.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.EndSpectraScanNumBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.EndSpectraScanNumBox.Name = "EndSpectraScanNumBox";
            this.EndSpectraScanNumBox.Size = new System.Drawing.Size(62, 20);
            this.EndSpectraScanNumBox.TabIndex = 2;
            this.EndSpectraScanNumBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.EndSpectraScanNumBox.ValueChanged += new System.EventHandler(this.EndSpectraScanNumBox_ValueChanged);
            this.EndSpectraScanNumBox.Leave += new System.EventHandler(this.EndSpectraScanNumBox_Leave);
            // 
            // EndSpectraScanNumInfo
            // 
            this.EndSpectraScanNumInfo.AutoSize = true;
            this.EndSpectraScanNumInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.EndSpectraScanNumInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EndSpectraScanNumInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.EndSpectraScanNumInfo.Location = new System.Drawing.Point(157, 34);
            this.EndSpectraScanNumInfo.Name = "EndSpectraScanNumInfo";
            this.EndSpectraScanNumInfo.Size = new System.Drawing.Size(13, 13);
            this.EndSpectraScanNumInfo.TabIndex = 74;
            this.EndSpectraScanNumInfo.Text = "?";
            this.EndSpectraScanNumInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.EndSpectraScanNumInfo.Click += new System.EventHandler(this.Info_Click);
            this.EndSpectraScanNumInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // StartSpectraScanNumBox
            // 
            this.StartSpectraScanNumBox.Location = new System.Drawing.Point(168, 14);
            this.StartSpectraScanNumBox.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.StartSpectraScanNumBox.Name = "StartSpectraScanNumBox";
            this.StartSpectraScanNumBox.Size = new System.Drawing.Size(62, 20);
            this.StartSpectraScanNumBox.TabIndex = 1;
            this.StartSpectraScanNumBox.Leave += new System.EventHandler(this.StartSpectraScanNumBox_Leave);
            // 
            // StartSpectraScanNumInfo
            // 
            this.StartSpectraScanNumInfo.AutoSize = true;
            this.StartSpectraScanNumInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.StartSpectraScanNumInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StartSpectraScanNumInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.StartSpectraScanNumInfo.Location = new System.Drawing.Point(157, 8);
            this.StartSpectraScanNumInfo.Name = "StartSpectraScanNumInfo";
            this.StartSpectraScanNumInfo.Size = new System.Drawing.Size(13, 13);
            this.StartSpectraScanNumInfo.TabIndex = 73;
            this.StartSpectraScanNumInfo.Text = "?";
            this.StartSpectraScanNumInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.StartSpectraScanNumInfo.Click += new System.EventHandler(this.Info_Click);
            this.StartSpectraScanNumInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // EndProteinIndexLabel
            // 
            this.EndProteinIndexLabel.AutoSize = true;
            this.EndProteinIndexLabel.Location = new System.Drawing.Point(68, 94);
            this.EndProteinIndexLabel.Name = "EndProteinIndexLabel";
            this.EndProteinIndexLabel.Size = new System.Drawing.Size(94, 13);
            this.EndProteinIndexLabel.TabIndex = 9;
            this.EndProteinIndexLabel.Text = "End Protein Index:";
            // 
            // StartProteinIndexLabel
            // 
            this.StartProteinIndexLabel.AutoSize = true;
            this.StartProteinIndexLabel.Location = new System.Drawing.Point(65, 68);
            this.StartProteinIndexLabel.Name = "StartProteinIndexLabel";
            this.StartProteinIndexLabel.Size = new System.Drawing.Size(97, 13);
            this.StartProteinIndexLabel.TabIndex = 5;
            this.StartProteinIndexLabel.Text = "Start Protein Index:";
            // 
            // StartSpectraScanNumLabel
            // 
            this.StartSpectraScanNumLabel.AutoSize = true;
            this.StartSpectraScanNumLabel.Location = new System.Drawing.Point(22, 16);
            this.StartSpectraScanNumLabel.Name = "StartSpectraScanNumLabel";
            this.StartSpectraScanNumLabel.Size = new System.Drawing.Size(140, 13);
            this.StartSpectraScanNumLabel.TabIndex = 0;
            this.StartSpectraScanNumLabel.Text = "Start Spectra Scan Number:";
            // 
            // EndSpectraScanNumLabel
            // 
            this.EndSpectraScanNumLabel.AutoSize = true;
            this.EndSpectraScanNumLabel.Location = new System.Drawing.Point(25, 42);
            this.EndSpectraScanNumLabel.Name = "EndSpectraScanNumLabel";
            this.EndSpectraScanNumLabel.Size = new System.Drawing.Size(137, 13);
            this.EndSpectraScanNumLabel.TabIndex = 3;
            this.EndSpectraScanNumLabel.Text = "End Spectra Scan Number:";
            // 
            // SequenceGB
            // 
            this.SequenceGB.Controls.Add(this.MaxSequenceMassBox);
            this.SequenceGB.Controls.Add(this.MinSequenceMassBox);
            this.SequenceGB.Controls.Add(this.MaxSequenceMassInfo);
            this.SequenceGB.Controls.Add(this.MinSequenceMassInfo);
            this.SequenceGB.Controls.Add(this.MaxSequenceMassLabel);
            this.SequenceGB.Controls.Add(this.MinSequenceMassLabel);
            this.SequenceGB.Controls.Add(this.MinCandidateLengthBox);
            this.SequenceGB.Controls.Add(this.MinCandidateLengthLabel);
            this.SequenceGB.Location = new System.Drawing.Point(282, 139);
            this.SequenceGB.Name = "SequenceGB";
            this.SequenceGB.Size = new System.Drawing.Size(242, 92);
            this.SequenceGB.TabIndex = 3;
            this.SequenceGB.TabStop = false;
            this.SequenceGB.Text = "Squence Adjustment";
            // 
            // MaxSequenceMassBox
            // 
            this.MaxSequenceMassBox.Location = new System.Drawing.Point(169, 42);
            this.MaxSequenceMassBox.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.MaxSequenceMassBox.Name = "MaxSequenceMassBox";
            this.MaxSequenceMassBox.Size = new System.Drawing.Size(62, 20);
            this.MaxSequenceMassBox.TabIndex = 6;
            this.MaxSequenceMassBox.Value = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.MaxSequenceMassBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MaxSequenceMassBox.Leave += new System.EventHandler(this.MaxSequenceMassBox_Leave);
            // 
            // MinSequenceMassBox
            // 
            this.MinSequenceMassBox.Location = new System.Drawing.Point(169, 16);
            this.MinSequenceMassBox.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.MinSequenceMassBox.Name = "MinSequenceMassBox";
            this.MinSequenceMassBox.Size = new System.Drawing.Size(62, 20);
            this.MinSequenceMassBox.TabIndex = 5;
            this.MinSequenceMassBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MinSequenceMassBox.Leave += new System.EventHandler(this.MinSequenceMassBox_Leave);
            // 
            // MaxSequenceMassInfo
            // 
            this.MaxSequenceMassInfo.AutoSize = true;
            this.MaxSequenceMassInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MaxSequenceMassInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaxSequenceMassInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.MaxSequenceMassInfo.Location = new System.Drawing.Point(158, 36);
            this.MaxSequenceMassInfo.Name = "MaxSequenceMassInfo";
            this.MaxSequenceMassInfo.Size = new System.Drawing.Size(13, 13);
            this.MaxSequenceMassInfo.TabIndex = 78;
            this.MaxSequenceMassInfo.Text = "?";
            this.MaxSequenceMassInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.MaxSequenceMassInfo.Click += new System.EventHandler(this.Info_Click);
            this.MaxSequenceMassInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // MinSequenceMassInfo
            // 
            this.MinSequenceMassInfo.AutoSize = true;
            this.MinSequenceMassInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MinSequenceMassInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MinSequenceMassInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.MinSequenceMassInfo.Location = new System.Drawing.Point(158, 10);
            this.MinSequenceMassInfo.Name = "MinSequenceMassInfo";
            this.MinSequenceMassInfo.Size = new System.Drawing.Size(13, 13);
            this.MinSequenceMassInfo.TabIndex = 77;
            this.MinSequenceMassInfo.Text = "?";
            this.MinSequenceMassInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.MinSequenceMassInfo.Click += new System.EventHandler(this.Info_Click);
            this.MinSequenceMassInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // MaxSequenceMassLabel
            // 
            this.MaxSequenceMassLabel.AutoSize = true;
            this.MaxSequenceMassLabel.Location = new System.Drawing.Point(53, 44);
            this.MaxSequenceMassLabel.Name = "MaxSequenceMassLabel";
            this.MaxSequenceMassLabel.Size = new System.Drawing.Size(110, 13);
            this.MaxSequenceMassLabel.TabIndex = 7;
            this.MaxSequenceMassLabel.Text = "Max Sequence Mass:";
            // 
            // MinSequenceMassLabel
            // 
            this.MinSequenceMassLabel.AutoSize = true;
            this.MinSequenceMassLabel.Location = new System.Drawing.Point(56, 18);
            this.MinSequenceMassLabel.Name = "MinSequenceMassLabel";
            this.MinSequenceMassLabel.Size = new System.Drawing.Size(107, 13);
            this.MinSequenceMassLabel.TabIndex = 4;
            this.MinSequenceMassLabel.Text = "Min Sequence Mass:";
            // 
            // MinCandidateLengthBox
            // 
            this.MinCandidateLengthBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.MinCandidateLengthBox.Location = new System.Drawing.Point(169, 68);
            this.MinCandidateLengthBox.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.MinCandidateLengthBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.MinCandidateLengthBox.Name = "MinCandidateLengthBox";
            this.MinCandidateLengthBox.Size = new System.Drawing.Size(62, 20);
            this.MinCandidateLengthBox.TabIndex = 2;
            this.MinCandidateLengthBox.Value = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.MinCandidateLengthBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MinCandidateLengthBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // MinCandidateLengthLabel
            // 
            this.MinCandidateLengthLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.MinCandidateLengthLabel.AutoSize = true;
            this.MinCandidateLengthLabel.Location = new System.Drawing.Point(49, 70);
            this.MinCandidateLengthLabel.Name = "MinCandidateLengthLabel";
            this.MinCandidateLengthLabel.Size = new System.Drawing.Size(114, 13);
            this.MinCandidateLengthLabel.TabIndex = 89;
            this.MinCandidateLengthLabel.Text = "Min Candidate Length:";
            // 
            // MiscGB
            // 
            this.MiscGB.Controls.Add(this.MaxResultsPanel);
            this.MiscGB.Controls.Add(this.DTNewOptionsPanel);
            this.MiscGB.Controls.Add(this.ProteinSampleSizeBox);
            this.MiscGB.Controls.Add(this.UseSmartPlusThreeModelBox);
            this.MiscGB.Controls.Add(this.UseSmartPlusThreeModelInfo);
            this.MiscGB.Controls.Add(this.ProteinSampleSizeInfo);
            this.MiscGB.Controls.Add(this.UseSmartPlusThreeModelLabel);
            this.MiscGB.Controls.Add(this.ProteinSampleSizeLabel);
            this.MiscGB.Location = new System.Drawing.Point(282, 237);
            this.MiscGB.Name = "MiscGB";
            this.MiscGB.Size = new System.Drawing.Size(242, 249);
            this.MiscGB.TabIndex = 4;
            this.MiscGB.TabStop = false;
            this.MiscGB.Text = "Misc";
            // 
            // MaxResultsPanel
            // 
            this.MaxResultsPanel.Controls.Add(this.MaxResultsBox);
            this.MaxResultsPanel.Controls.Add(this.MaxResultsLabel);
            this.MaxResultsPanel.Controls.Add(this.MaxResultsInfo);
            this.MaxResultsPanel.Location = new System.Drawing.Point(14, 37);
            this.MaxResultsPanel.Name = "MaxResultsPanel";
            this.MaxResultsPanel.Size = new System.Drawing.Size(221, 27);
            this.MaxResultsPanel.TabIndex = 98;
            // 
            // MaxResultsBox
            // 
            this.MaxResultsBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.MaxResultsBox.Location = new System.Drawing.Point(172, 6);
            this.MaxResultsBox.Name = "MaxResultsBox";
            this.MaxResultsBox.Size = new System.Drawing.Size(45, 20);
            this.MaxResultsBox.TabIndex = 1;
            this.MaxResultsBox.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.MaxResultsBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MaxResultsBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // MaxResultsLabel
            // 
            this.MaxResultsLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.MaxResultsLabel.AutoSize = true;
            this.MaxResultsLabel.Location = new System.Drawing.Point(98, 8);
            this.MaxResultsLabel.Name = "MaxResultsLabel";
            this.MaxResultsLabel.Size = new System.Drawing.Size(68, 13);
            this.MaxResultsLabel.TabIndex = 48;
            this.MaxResultsLabel.Text = "Max Results:";
            // 
            // MaxResultsInfo
            // 
            this.MaxResultsInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.MaxResultsInfo.AutoSize = true;
            this.MaxResultsInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MaxResultsInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaxResultsInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.MaxResultsInfo.Location = new System.Drawing.Point(161, 0);
            this.MaxResultsInfo.Name = "MaxResultsInfo";
            this.MaxResultsInfo.Size = new System.Drawing.Size(13, 13);
            this.MaxResultsInfo.TabIndex = 93;
            this.MaxResultsInfo.Text = "?";
            this.MaxResultsInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.MaxResultsInfo.Click += new System.EventHandler(this.Info_Click);
            this.MaxResultsInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // DTNewOptionsPanel
            // 
            this.DTNewOptionsPanel.Controls.Add(this.TicCutoffPercentageBox);
            this.DTNewOptionsPanel.Controls.Add(this.TicCutoffPercentageLabel);
            this.DTNewOptionsPanel.Controls.Add(this.DeisotopingModeBox);
            this.DTNewOptionsPanel.Controls.Add(this.DeisotopingModeLabel);
            this.DTNewOptionsPanel.Controls.Add(this.DeisotopingModeInfo);
            this.DTNewOptionsPanel.Controls.Add(this.TicCutoffPercentageInfo);
            this.DTNewOptionsPanel.Location = new System.Drawing.Point(8, 63);
            this.DTNewOptionsPanel.Name = "DTNewOptionsPanel";
            this.DTNewOptionsPanel.Size = new System.Drawing.Size(229, 55);
            this.DTNewOptionsPanel.TabIndex = 76;
            // 
            // TicCutoffPercentageBox
            // 
            this.TicCutoffPercentageBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.TicCutoffPercentageBox.DecimalPlaces = 2;
            this.TicCutoffPercentageBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this.TicCutoffPercentageBox.Location = new System.Drawing.Point(181, 33);
            this.TicCutoffPercentageBox.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.TicCutoffPercentageBox.Name = "TicCutoffPercentageBox";
            this.TicCutoffPercentageBox.Size = new System.Drawing.Size(45, 20);
            this.TicCutoffPercentageBox.TabIndex = 0;
            this.TicCutoffPercentageBox.Value = new decimal(new int[] {
            98,
            0,
            0,
            131072});
            this.TicCutoffPercentageBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // TicCutoffPercentageLabel
            // 
            this.TicCutoffPercentageLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.TicCutoffPercentageLabel.AutoSize = true;
            this.TicCutoffPercentageLabel.Location = new System.Drawing.Point(59, 35);
            this.TicCutoffPercentageLabel.Name = "TicCutoffPercentageLabel";
            this.TicCutoffPercentageLabel.Size = new System.Drawing.Size(116, 13);
            this.TicCutoffPercentageLabel.TabIndex = 57;
            this.TicCutoffPercentageLabel.Text = "TIC Cutoff Percentage:";
            // 
            // DeisotopingModeBox
            // 
            this.DeisotopingModeBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DeisotopingModeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.DeisotopingModeBox.FormattingEnabled = true;
            this.DeisotopingModeBox.Items.AddRange(new object[] {
            "Off",
            "Precursor Adj Only",
            "Also Candidate Scoring"});
            this.DeisotopingModeBox.Location = new System.Drawing.Point(106, 6);
            this.DeisotopingModeBox.Name = "DeisotopingModeBox";
            this.DeisotopingModeBox.Size = new System.Drawing.Size(120, 21);
            this.DeisotopingModeBox.TabIndex = 1;
            this.DeisotopingModeBox.Leave += new System.EventHandler(this.ValueBox_Leave);
            // 
            // DeisotopingModeLabel
            // 
            this.DeisotopingModeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DeisotopingModeLabel.AutoSize = true;
            this.DeisotopingModeLabel.Location = new System.Drawing.Point(4, 9);
            this.DeisotopingModeLabel.Name = "DeisotopingModeLabel";
            this.DeisotopingModeLabel.Size = new System.Drawing.Size(96, 13);
            this.DeisotopingModeLabel.TabIndex = 10;
            this.DeisotopingModeLabel.Text = "Deisotoping Mode:";
            // 
            // DeisotopingModeInfo
            // 
            this.DeisotopingModeInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DeisotopingModeInfo.AutoSize = true;
            this.DeisotopingModeInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.DeisotopingModeInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DeisotopingModeInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.DeisotopingModeInfo.Location = new System.Drawing.Point(95, 1);
            this.DeisotopingModeInfo.Name = "DeisotopingModeInfo";
            this.DeisotopingModeInfo.Size = new System.Drawing.Size(13, 13);
            this.DeisotopingModeInfo.TabIndex = 74;
            this.DeisotopingModeInfo.Text = "?";
            this.DeisotopingModeInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.DeisotopingModeInfo.Click += new System.EventHandler(this.Info_Click);
            this.DeisotopingModeInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // TicCutoffPercentageInfo
            // 
            this.TicCutoffPercentageInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.TicCutoffPercentageInfo.AutoSize = true;
            this.TicCutoffPercentageInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.TicCutoffPercentageInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TicCutoffPercentageInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.TicCutoffPercentageInfo.Location = new System.Drawing.Point(170, 27);
            this.TicCutoffPercentageInfo.Name = "TicCutoffPercentageInfo";
            this.TicCutoffPercentageInfo.Size = new System.Drawing.Size(13, 13);
            this.TicCutoffPercentageInfo.TabIndex = 91;
            this.TicCutoffPercentageInfo.Text = "?";
            this.TicCutoffPercentageInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.TicCutoffPercentageInfo.Click += new System.EventHandler(this.Info_Click);
            this.TicCutoffPercentageInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // ProteinSampleSizeBox
            // 
            this.ProteinSampleSizeBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ProteinSampleSizeBox.Location = new System.Drawing.Point(186, 124);
            this.ProteinSampleSizeBox.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.ProteinSampleSizeBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.ProteinSampleSizeBox.Name = "ProteinSampleSizeBox";
            this.ProteinSampleSizeBox.Size = new System.Drawing.Size(45, 20);
            this.ProteinSampleSizeBox.TabIndex = 3;
            this.ProteinSampleSizeBox.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.ProteinSampleSizeBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.ProteinSampleSizeBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // UseSmartPlusThreeModelBox
            // 
            this.UseSmartPlusThreeModelBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.UseSmartPlusThreeModelBox.AutoSize = true;
            this.UseSmartPlusThreeModelBox.Checked = true;
            this.UseSmartPlusThreeModelBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.UseSmartPlusThreeModelBox.Location = new System.Drawing.Point(216, 23);
            this.UseSmartPlusThreeModelBox.Name = "UseSmartPlusThreeModelBox";
            this.UseSmartPlusThreeModelBox.Size = new System.Drawing.Size(15, 14);
            this.UseSmartPlusThreeModelBox.TabIndex = 0;
            this.UseSmartPlusThreeModelBox.UseVisualStyleBackColor = true;
            this.UseSmartPlusThreeModelBox.CheckedChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // UseSmartPlusThreeModelInfo
            // 
            this.UseSmartPlusThreeModelInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.UseSmartPlusThreeModelInfo.AutoSize = true;
            this.UseSmartPlusThreeModelInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.UseSmartPlusThreeModelInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.UseSmartPlusThreeModelInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.UseSmartPlusThreeModelInfo.Location = new System.Drawing.Point(203, 14);
            this.UseSmartPlusThreeModelInfo.Name = "UseSmartPlusThreeModelInfo";
            this.UseSmartPlusThreeModelInfo.Size = new System.Drawing.Size(13, 13);
            this.UseSmartPlusThreeModelInfo.TabIndex = 96;
            this.UseSmartPlusThreeModelInfo.Text = "?";
            this.UseSmartPlusThreeModelInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.UseSmartPlusThreeModelInfo.Click += new System.EventHandler(this.Info_Click);
            this.UseSmartPlusThreeModelInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // ProteinSampleSizeInfo
            // 
            this.ProteinSampleSizeInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ProteinSampleSizeInfo.AutoSize = true;
            this.ProteinSampleSizeInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.ProteinSampleSizeInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ProteinSampleSizeInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.ProteinSampleSizeInfo.Location = new System.Drawing.Point(175, 118);
            this.ProteinSampleSizeInfo.Name = "ProteinSampleSizeInfo";
            this.ProteinSampleSizeInfo.Size = new System.Drawing.Size(13, 13);
            this.ProteinSampleSizeInfo.TabIndex = 97;
            this.ProteinSampleSizeInfo.Text = "?";
            this.ProteinSampleSizeInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.ProteinSampleSizeInfo.Click += new System.EventHandler(this.Info_Click);
            this.ProteinSampleSizeInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // UseSmartPlusThreeModelLabel
            // 
            this.UseSmartPlusThreeModelLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.UseSmartPlusThreeModelLabel.AutoSize = true;
            this.UseSmartPlusThreeModelLabel.Location = new System.Drawing.Point(65, 22);
            this.UseSmartPlusThreeModelLabel.Name = "UseSmartPlusThreeModelLabel";
            this.UseSmartPlusThreeModelLabel.Size = new System.Drawing.Size(145, 13);
            this.UseSmartPlusThreeModelLabel.TabIndex = 61;
            this.UseSmartPlusThreeModelLabel.Text = "Use Smart Plus Three Model:";
            // 
            // ProteinSampleSizeLabel
            // 
            this.ProteinSampleSizeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ProteinSampleSizeLabel.AutoSize = true;
            this.ProteinSampleSizeLabel.Location = new System.Drawing.Point(76, 126);
            this.ProteinSampleSizeLabel.Name = "ProteinSampleSizeLabel";
            this.ProteinSampleSizeLabel.Size = new System.Drawing.Size(104, 13);
            this.ProteinSampleSizeLabel.TabIndex = 19;
            this.ProteinSampleSizeLabel.Text = "Protein Sample Size:";
            // 
            // PrecursorGbox
            // 
            this.PrecursorGbox.Controls.Add(this.MaxPrecursorAdjustmentBox);
            this.PrecursorGbox.Controls.Add(this.MinPrecursorAdjustmentBox);
            this.PrecursorGbox.Controls.Add(this.MinPrecursorAdjustmentInfo);
            this.PrecursorGbox.Controls.Add(this.MaxPrecursorAdjustmentLabel);
            this.PrecursorGbox.Controls.Add(this.MinPrecursorAdjustmentLabel);
            this.PrecursorGbox.Controls.Add(this.AdjustPanel);
            this.PrecursorGbox.Location = new System.Drawing.Point(5, 6);
            this.PrecursorGbox.Name = "PrecursorGbox";
            this.PrecursorGbox.Size = new System.Drawing.Size(267, 69);
            this.PrecursorGbox.TabIndex = 0;
            this.PrecursorGbox.TabStop = false;
            this.PrecursorGbox.Text = "Precursor Adjustment";
            // 
            // MaxPrecursorAdjustmentBox
            // 
            this.MaxPrecursorAdjustmentBox.Location = new System.Drawing.Point(201, 36);
            this.MaxPrecursorAdjustmentBox.Name = "MaxPrecursorAdjustmentBox";
            this.MaxPrecursorAdjustmentBox.Size = new System.Drawing.Size(48, 20);
            this.MaxPrecursorAdjustmentBox.TabIndex = 81;
            this.MaxPrecursorAdjustmentBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MaxPrecursorAdjustmentBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // MinPrecursorAdjustmentBox
            // 
            this.MinPrecursorAdjustmentBox.Location = new System.Drawing.Point(127, 36);
            this.MinPrecursorAdjustmentBox.Maximum = new decimal(new int[] {
            0,
            0,
            0,
            0});
            this.MinPrecursorAdjustmentBox.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.MinPrecursorAdjustmentBox.Name = "MinPrecursorAdjustmentBox";
            this.MinPrecursorAdjustmentBox.Size = new System.Drawing.Size(48, 20);
            this.MinPrecursorAdjustmentBox.TabIndex = 80;
            this.MinPrecursorAdjustmentBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MinPrecursorAdjustmentBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // MinPrecursorAdjustmentInfo
            // 
            this.MinPrecursorAdjustmentInfo.AutoSize = true;
            this.MinPrecursorAdjustmentInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MinPrecursorAdjustmentInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MinPrecursorAdjustmentInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.MinPrecursorAdjustmentInfo.Location = new System.Drawing.Point(116, 29);
            this.MinPrecursorAdjustmentInfo.Name = "MinPrecursorAdjustmentInfo";
            this.MinPrecursorAdjustmentInfo.Size = new System.Drawing.Size(13, 13);
            this.MinPrecursorAdjustmentInfo.TabIndex = 68;
            this.MinPrecursorAdjustmentInfo.Text = "?";
            this.MinPrecursorAdjustmentInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.MinPrecursorAdjustmentInfo.Click += new System.EventHandler(this.Info_Click);
            this.MinPrecursorAdjustmentInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // MaxPrecursorAdjustmentLabel
            // 
            this.MaxPrecursorAdjustmentLabel.AutoSize = true;
            this.MaxPrecursorAdjustmentLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaxPrecursorAdjustmentLabel.Location = new System.Drawing.Point(181, 35);
            this.MaxPrecursorAdjustmentLabel.Name = "MaxPrecursorAdjustmentLabel";
            this.MaxPrecursorAdjustmentLabel.Size = new System.Drawing.Size(14, 20);
            this.MaxPrecursorAdjustmentLabel.TabIndex = 20;
            this.MaxPrecursorAdjustmentLabel.Text = "-";
            // 
            // MinPrecursorAdjustmentLabel
            // 
            this.MinPrecursorAdjustmentLabel.AutoSize = true;
            this.MinPrecursorAdjustmentLabel.Location = new System.Drawing.Point(18, 38);
            this.MinPrecursorAdjustmentLabel.Name = "MinPrecursorAdjustmentLabel";
            this.MinPrecursorAdjustmentLabel.Size = new System.Drawing.Size(103, 13);
            this.MinPrecursorAdjustmentLabel.TabIndex = 10;
            this.MinPrecursorAdjustmentLabel.Text = "Neutron Adjustment:";
            // 
            // AdjustPanel
            // 
            this.AdjustPanel.Controls.Add(this.AdjustPrecursorMassBox);
            this.AdjustPanel.Controls.Add(this.AdjustPrecursorMassInfo);
            this.AdjustPanel.Controls.Add(this.AdjustPrecursorMassLabel);
            this.AdjustPanel.Location = new System.Drawing.Point(53, 10);
            this.AdjustPanel.Name = "AdjustPanel";
            this.AdjustPanel.Size = new System.Drawing.Size(150, 25);
            this.AdjustPanel.TabIndex = 76;
            // 
            // AdjustPrecursorMassBox
            // 
            this.AdjustPrecursorMassBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.AdjustPrecursorMassBox.AutoSize = true;
            this.AdjustPrecursorMassBox.Location = new System.Drawing.Point(132, 6);
            this.AdjustPrecursorMassBox.Name = "AdjustPrecursorMassBox";
            this.AdjustPrecursorMassBox.Size = new System.Drawing.Size(15, 14);
            this.AdjustPrecursorMassBox.TabIndex = 4;
            this.AdjustPrecursorMassBox.UseVisualStyleBackColor = true;
            this.AdjustPrecursorMassBox.Click += new System.EventHandler(this.AdjustPrecursorMassBox_Click);
            this.AdjustPrecursorMassBox.CheckedChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // AdjustPrecursorMassInfo
            // 
            this.AdjustPrecursorMassInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.AdjustPrecursorMassInfo.AutoSize = true;
            this.AdjustPrecursorMassInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.AdjustPrecursorMassInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.AdjustPrecursorMassInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.AdjustPrecursorMassInfo.Location = new System.Drawing.Point(121, -2);
            this.AdjustPrecursorMassInfo.Name = "AdjustPrecursorMassInfo";
            this.AdjustPrecursorMassInfo.Size = new System.Drawing.Size(13, 13);
            this.AdjustPrecursorMassInfo.TabIndex = 67;
            this.AdjustPrecursorMassInfo.Text = "?";
            this.AdjustPrecursorMassInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.AdjustPrecursorMassInfo.Click += new System.EventHandler(this.Info_Click);
            this.AdjustPrecursorMassInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // AdjustPrecursorMassLabel
            // 
            this.AdjustPrecursorMassLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.AdjustPrecursorMassLabel.AutoSize = true;
            this.AdjustPrecursorMassLabel.Location = new System.Drawing.Point(11, 6);
            this.AdjustPrecursorMassLabel.Name = "AdjustPrecursorMassLabel";
            this.AdjustPrecursorMassLabel.Size = new System.Drawing.Size(115, 13);
            this.AdjustPrecursorMassLabel.TabIndex = 18;
            this.AdjustPrecursorMassLabel.Text = "Adjust Precursor Mass:";
            // 
            // TagReconGB
            // 
            this.TagReconGB.Controls.Add(this.MassReconModeBox);
            this.TagReconGB.Controls.Add(this.MassReconModeInfo);
            this.TagReconGB.Controls.Add(this.MassReconModeLabel);
            this.TagReconGB.Controls.Add(this.ComputeXCorrPanel);
            this.TagReconGB.Controls.Add(this.UseNETAdjustmentBox);
            this.TagReconGB.Controls.Add(this.UseNETAdjustmentLabel);
            this.TagReconGB.Location = new System.Drawing.Point(5, 357);
            this.TagReconGB.Name = "TagReconGB";
            this.TagReconGB.Size = new System.Drawing.Size(267, 78);
            this.TagReconGB.TabIndex = 48;
            this.TagReconGB.TabStop = false;
            this.TagReconGB.Text = "Tag Recon Options";
            this.TagReconGB.Visible = false;
            // 
            // MassReconModeBox
            // 
            this.MassReconModeBox.AutoSize = true;
            this.MassReconModeBox.Location = new System.Drawing.Point(183, 58);
            this.MassReconModeBox.Name = "MassReconModeBox";
            this.MassReconModeBox.Size = new System.Drawing.Size(15, 14);
            this.MassReconModeBox.TabIndex = 2;
            this.MassReconModeBox.UseVisualStyleBackColor = true;
            this.MassReconModeBox.CheckedChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // MassReconModeInfo
            // 
            this.MassReconModeInfo.AutoSize = true;
            this.MassReconModeInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MassReconModeInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MassReconModeInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.MassReconModeInfo.Location = new System.Drawing.Point(172, 50);
            this.MassReconModeInfo.Name = "MassReconModeInfo";
            this.MassReconModeInfo.Size = new System.Drawing.Size(13, 13);
            this.MassReconModeInfo.TabIndex = 66;
            this.MassReconModeInfo.Text = "?";
            this.MassReconModeInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.MassReconModeInfo.Click += new System.EventHandler(this.Info_Click);
            this.MassReconModeInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // MassReconModeLabel
            // 
            this.MassReconModeLabel.AutoSize = true;
            this.MassReconModeLabel.Location = new System.Drawing.Point(77, 58);
            this.MassReconModeLabel.Name = "MassReconModeLabel";
            this.MassReconModeLabel.Size = new System.Drawing.Size(100, 13);
            this.MassReconModeLabel.TabIndex = 67;
            this.MassReconModeLabel.Text = "Mass Recon Mode:";
            // 
            // ComputeXCorrPanel
            // 
            this.ComputeXCorrPanel.Controls.Add(this.ComputeXCorrBox);
            this.ComputeXCorrPanel.Controls.Add(this.ComputeXCorrLabel);
            this.ComputeXCorrPanel.Location = new System.Drawing.Point(39, 34);
            this.ComputeXCorrPanel.Name = "ComputeXCorrPanel";
            this.ComputeXCorrPanel.Size = new System.Drawing.Size(189, 18);
            this.ComputeXCorrPanel.TabIndex = 82;
            // 
            // ComputeXCorrBox
            // 
            this.ComputeXCorrBox.AutoSize = true;
            this.ComputeXCorrBox.Checked = true;
            this.ComputeXCorrBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ComputeXCorrBox.Location = new System.Drawing.Point(144, 3);
            this.ComputeXCorrBox.Name = "ComputeXCorrBox";
            this.ComputeXCorrBox.Size = new System.Drawing.Size(15, 14);
            this.ComputeXCorrBox.TabIndex = 1;
            this.ComputeXCorrBox.UseVisualStyleBackColor = true;
            // 
            // ComputeXCorrLabel
            // 
            this.ComputeXCorrLabel.AutoSize = true;
            this.ComputeXCorrLabel.Location = new System.Drawing.Point(60, 3);
            this.ComputeXCorrLabel.Name = "ComputeXCorrLabel";
            this.ComputeXCorrLabel.Size = new System.Drawing.Size(78, 13);
            this.ComputeXCorrLabel.TabIndex = 80;
            this.ComputeXCorrLabel.Text = "ComputeXCorr:";
            // 
            // UseNETAdjustmentBox
            // 
            this.UseNETAdjustmentBox.AutoSize = true;
            this.UseNETAdjustmentBox.Checked = true;
            this.UseNETAdjustmentBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.UseNETAdjustmentBox.Location = new System.Drawing.Point(183, 16);
            this.UseNETAdjustmentBox.Name = "UseNETAdjustmentBox";
            this.UseNETAdjustmentBox.Size = new System.Drawing.Size(15, 14);
            this.UseNETAdjustmentBox.TabIndex = 0;
            this.UseNETAdjustmentBox.UseVisualStyleBackColor = true;
            this.UseNETAdjustmentBox.CheckedChanged += new System.EventHandler(this.UseNETAdjustmentBox_CheckedChanged);
            // 
            // UseNETAdjustmentLabel
            // 
            this.UseNETAdjustmentLabel.AutoSize = true;
            this.UseNETAdjustmentLabel.Location = new System.Drawing.Point(68, 16);
            this.UseNETAdjustmentLabel.Name = "UseNETAdjustmentLabel";
            this.UseNETAdjustmentLabel.Size = new System.Drawing.Size(109, 13);
            this.UseNETAdjustmentLabel.TabIndex = 77;
            this.UseNETAdjustmentLabel.Text = "Use NET Adjustment:";
            // 
            // DirecTagGB
            // 
            this.DirecTagGB.Controls.Add(this.MaxPeakPanel);
            this.DirecTagGB.Controls.Add(this.MaxTagCountBox);
            this.DirecTagGB.Controls.Add(this.MaxTagScoreBox);
            this.DirecTagGB.Controls.Add(this.MaxTagCountLabel);
            this.DirecTagGB.Controls.Add(this.MaxTagScoreLabel);
            this.DirecTagGB.Controls.Add(this.IsotopeMzToleranceBox);
            this.DirecTagGB.Controls.Add(this.ComplementMzToleranceBox);
            this.DirecTagGB.Controls.Add(this.TagLengthBox);
            this.DirecTagGB.Controls.Add(this.ComplementMzToleranceInfo);
            this.DirecTagGB.Controls.Add(this.IsotopeMzToleranceInfo);
            this.DirecTagGB.Controls.Add(this.TagLengthInfo);
            this.DirecTagGB.Controls.Add(this.ComplementMzToleranceLabel);
            this.DirecTagGB.Controls.Add(this.TagLengthLabel);
            this.DirecTagGB.Controls.Add(this.IsotopeMzToleranceLabel);
            this.DirecTagGB.Location = new System.Drawing.Point(5, 86);
            this.DirecTagGB.Name = "DirecTagGB";
            this.DirecTagGB.Size = new System.Drawing.Size(267, 241);
            this.DirecTagGB.TabIndex = 74;
            this.DirecTagGB.TabStop = false;
            this.DirecTagGB.Text = "DirecTag Options";
            this.DirecTagGB.Visible = false;
            // 
            // MaxPeakPanel
            // 
            this.MaxPeakPanel.Controls.Add(this.MaxPeakCountBox);
            this.MaxPeakPanel.Controls.Add(this.MaxPeakCountInfo);
            this.MaxPeakPanel.Controls.Add(this.MaxPeakCountLabel);
            this.MaxPeakPanel.Location = new System.Drawing.Point(56, 53);
            this.MaxPeakPanel.Name = "MaxPeakPanel";
            this.MaxPeakPanel.Size = new System.Drawing.Size(191, 27);
            this.MaxPeakPanel.TabIndex = 92;
            // 
            // MaxPeakCountBox
            // 
            this.MaxPeakCountBox.Location = new System.Drawing.Point(136, 5);
            this.MaxPeakCountBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.MaxPeakCountBox.Name = "MaxPeakCountBox";
            this.MaxPeakCountBox.Size = new System.Drawing.Size(45, 20);
            this.MaxPeakCountBox.TabIndex = 1;
            this.MaxPeakCountBox.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            // 
            // MaxPeakCountInfo
            // 
            this.MaxPeakCountInfo.AutoSize = true;
            this.MaxPeakCountInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MaxPeakCountInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaxPeakCountInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.MaxPeakCountInfo.Location = new System.Drawing.Point(124, -1);
            this.MaxPeakCountInfo.Name = "MaxPeakCountInfo";
            this.MaxPeakCountInfo.Size = new System.Drawing.Size(13, 13);
            this.MaxPeakCountInfo.TabIndex = 79;
            this.MaxPeakCountInfo.Text = "?";
            // 
            // MaxPeakCountLabel
            // 
            this.MaxPeakCountLabel.AutoSize = true;
            this.MaxPeakCountLabel.Location = new System.Drawing.Point(40, 7);
            this.MaxPeakCountLabel.Name = "MaxPeakCountLabel";
            this.MaxPeakCountLabel.Size = new System.Drawing.Size(89, 13);
            this.MaxPeakCountLabel.TabIndex = 0;
            this.MaxPeakCountLabel.Text = "Max Peak Count:";
            // 
            // MaxTagCountBox
            // 
            this.MaxTagCountBox.Location = new System.Drawing.Point(192, 188);
            this.MaxTagCountBox.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.MaxTagCountBox.Name = "MaxTagCountBox";
            this.MaxTagCountBox.Size = new System.Drawing.Size(45, 20);
            this.MaxTagCountBox.TabIndex = 82;
            this.MaxTagCountBox.Value = new decimal(new int[] {
            50,
            0,
            0,
            0});
            // 
            // MaxTagScoreBox
            // 
            this.MaxTagScoreBox.Location = new System.Drawing.Point(192, 214);
            this.MaxTagScoreBox.Name = "MaxTagScoreBox";
            this.MaxTagScoreBox.Size = new System.Drawing.Size(45, 20);
            this.MaxTagScoreBox.TabIndex = 90;
            this.MaxTagScoreBox.Text = "20";
            this.MaxTagScoreBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MaxTagScoreBox.Leave += new System.EventHandler(this.NumericTextBox_Leave);
            this.MaxTagScoreBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // MaxTagCountLabel
            // 
            this.MaxTagCountLabel.AutoSize = true;
            this.MaxTagCountLabel.Location = new System.Drawing.Point(103, 191);
            this.MaxTagCountLabel.Name = "MaxTagCountLabel";
            this.MaxTagCountLabel.Size = new System.Drawing.Size(83, 13);
            this.MaxTagCountLabel.TabIndex = 86;
            this.MaxTagCountLabel.Text = "Max Tag Count:";
            // 
            // MaxTagScoreLabel
            // 
            this.MaxTagScoreLabel.AutoSize = true;
            this.MaxTagScoreLabel.Location = new System.Drawing.Point(103, 217);
            this.MaxTagScoreLabel.Name = "MaxTagScoreLabel";
            this.MaxTagScoreLabel.Size = new System.Drawing.Size(83, 13);
            this.MaxTagScoreLabel.TabIndex = 85;
            this.MaxTagScoreLabel.Text = "Max Tag Score:";
            // 
            // IsotopeMzToleranceBox
            // 
            this.IsotopeMzToleranceBox.Location = new System.Drawing.Point(192, 162);
            this.IsotopeMzToleranceBox.Name = "IsotopeMzToleranceBox";
            this.IsotopeMzToleranceBox.Size = new System.Drawing.Size(45, 20);
            this.IsotopeMzToleranceBox.TabIndex = 84;
            this.IsotopeMzToleranceBox.Text = "0.25";
            this.IsotopeMzToleranceBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.IsotopeMzToleranceBox.Leave += new System.EventHandler(this.NumericTextBox_Leave);
            this.IsotopeMzToleranceBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // ComplementMzToleranceBox
            // 
            this.ComplementMzToleranceBox.Location = new System.Drawing.Point(192, 136);
            this.ComplementMzToleranceBox.Name = "ComplementMzToleranceBox";
            this.ComplementMzToleranceBox.Size = new System.Drawing.Size(45, 20);
            this.ComplementMzToleranceBox.TabIndex = 83;
            this.ComplementMzToleranceBox.Text = "0.5";
            this.ComplementMzToleranceBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.ComplementMzToleranceBox.Leave += new System.EventHandler(this.NumericTextBox_Leave);
            this.ComplementMzToleranceBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // TagLengthBox
            // 
            this.TagLengthBox.Location = new System.Drawing.Point(192, 29);
            this.TagLengthBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.TagLengthBox.Name = "TagLengthBox";
            this.TagLengthBox.Size = new System.Drawing.Size(45, 20);
            this.TagLengthBox.TabIndex = 3;
            this.TagLengthBox.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.TagLengthBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.TagLengthBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // ComplementMzToleranceInfo
            // 
            this.ComplementMzToleranceInfo.AutoSize = true;
            this.ComplementMzToleranceInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.ComplementMzToleranceInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ComplementMzToleranceInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.ComplementMzToleranceInfo.Location = new System.Drawing.Point(181, 131);
            this.ComplementMzToleranceInfo.Name = "ComplementMzToleranceInfo";
            this.ComplementMzToleranceInfo.Size = new System.Drawing.Size(13, 13);
            this.ComplementMzToleranceInfo.TabIndex = 82;
            this.ComplementMzToleranceInfo.Text = "?";
            this.ComplementMzToleranceInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.ComplementMzToleranceInfo.Click += new System.EventHandler(this.Info_Click);
            this.ComplementMzToleranceInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // IsotopeMzToleranceInfo
            // 
            this.IsotopeMzToleranceInfo.AutoSize = true;
            this.IsotopeMzToleranceInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.IsotopeMzToleranceInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.IsotopeMzToleranceInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.IsotopeMzToleranceInfo.Location = new System.Drawing.Point(180, 157);
            this.IsotopeMzToleranceInfo.Name = "IsotopeMzToleranceInfo";
            this.IsotopeMzToleranceInfo.Size = new System.Drawing.Size(13, 13);
            this.IsotopeMzToleranceInfo.TabIndex = 81;
            this.IsotopeMzToleranceInfo.Text = "?";
            this.IsotopeMzToleranceInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.IsotopeMzToleranceInfo.Click += new System.EventHandler(this.Info_Click);
            this.IsotopeMzToleranceInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // TagLengthInfo
            // 
            this.TagLengthInfo.AutoSize = true;
            this.TagLengthInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.TagLengthInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TagLengthInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.TagLengthInfo.Location = new System.Drawing.Point(180, 23);
            this.TagLengthInfo.Name = "TagLengthInfo";
            this.TagLengthInfo.Size = new System.Drawing.Size(13, 13);
            this.TagLengthInfo.TabIndex = 80;
            this.TagLengthInfo.Text = "?";
            this.TagLengthInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.TagLengthInfo.Click += new System.EventHandler(this.Info_Click);
            this.TagLengthInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // ComplementMzToleranceLabel
            // 
            this.ComplementMzToleranceLabel.AutoSize = true;
            this.ComplementMzToleranceLabel.Location = new System.Drawing.Point(50, 139);
            this.ComplementMzToleranceLabel.Name = "ComplementMzToleranceLabel";
            this.ComplementMzToleranceLabel.Size = new System.Drawing.Size(136, 13);
            this.ComplementMzToleranceLabel.TabIndex = 15;
            this.ComplementMzToleranceLabel.Text = "Compliment m/z Tolerance:";
            // 
            // TagLengthLabel
            // 
            this.TagLengthLabel.AutoSize = true;
            this.TagLengthLabel.Location = new System.Drawing.Point(120, 31);
            this.TagLengthLabel.Name = "TagLengthLabel";
            this.TagLengthLabel.Size = new System.Drawing.Size(65, 13);
            this.TagLengthLabel.TabIndex = 2;
            this.TagLengthLabel.Text = "Tag Length:";
            // 
            // IsotopeMzToleranceLabel
            // 
            this.IsotopeMzToleranceLabel.AutoSize = true;
            this.IsotopeMzToleranceLabel.Location = new System.Drawing.Point(68, 165);
            this.IsotopeMzToleranceLabel.Name = "IsotopeMzToleranceLabel";
            this.IsotopeMzToleranceLabel.Size = new System.Drawing.Size(117, 13);
            this.IsotopeMzToleranceLabel.TabIndex = 13;
            this.IsotopeMzToleranceLabel.Text = "Isotope m/z Tolerance:";
            // 
            // TRModOptionsGB
            // 
            this.TRModOptionsGB.Controls.Add(this.BlosumThresholdBox);
            this.TRModOptionsGB.Controls.Add(this.BlosumThresholdInfo);
            this.TRModOptionsGB.Controls.Add(this.ExplainUnknownMassShiftsAsBox);
            this.TRModOptionsGB.Controls.Add(this.MaxModificationMassPlusBox);
            this.TRModOptionsGB.Controls.Add(this.BlosumBox);
            this.TRModOptionsGB.Controls.Add(this.MaxModificationMassMinusBox);
            this.TRModOptionsGB.Controls.Add(this.UnimodXMLBox);
            this.TRModOptionsGB.Controls.Add(this.ExplainUnknownMassShiftsAsLabel);
            this.TRModOptionsGB.Controls.Add(this.BlosumThresholdLabel);
            this.TRModOptionsGB.Controls.Add(this.BlosumInfo);
            this.TRModOptionsGB.Controls.Add(this.UnimodXMLInfo);
            this.TRModOptionsGB.Controls.Add(this.BlosumLabel);
            this.TRModOptionsGB.Controls.Add(this.UnimodXMLBrowse);
            this.TRModOptionsGB.Controls.Add(this.MaxModificationMassMinusLabel);
            this.TRModOptionsGB.Controls.Add(this.MaxModificationMassPlusInfo);
            this.TRModOptionsGB.Controls.Add(this.UnimodXMLLabel);
            this.TRModOptionsGB.Controls.Add(this.MaxModificationMassPlusLabel);
            this.TRModOptionsGB.Controls.Add(this.MaxModificationMassMinusInfo);
            this.TRModOptionsGB.Controls.Add(this.BlosumBrowse);
            this.TRModOptionsGB.Location = new System.Drawing.Point(5, 155);
            this.TRModOptionsGB.Name = "TRModOptionsGB";
            this.TRModOptionsGB.Size = new System.Drawing.Size(267, 196);
            this.TRModOptionsGB.TabIndex = 77;
            this.TRModOptionsGB.TabStop = false;
            this.TRModOptionsGB.Text = "Modification Options";
            // 
            // BlosumThresholdBox
            // 
            this.BlosumThresholdBox.Location = new System.Drawing.Point(159, 111);
            this.BlosumThresholdBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.BlosumThresholdBox.Minimum = new decimal(new int[] {
            10000,
            0,
            0,
            -2147483648});
            this.BlosumThresholdBox.Name = "BlosumThresholdBox";
            this.BlosumThresholdBox.Size = new System.Drawing.Size(77, 20);
            this.BlosumThresholdBox.TabIndex = 2;
            this.BlosumThresholdBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.BlosumThresholdBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // BlosumThresholdInfo
            // 
            this.BlosumThresholdInfo.AutoSize = true;
            this.BlosumThresholdInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.BlosumThresholdInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BlosumThresholdInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.BlosumThresholdInfo.Location = new System.Drawing.Point(148, 105);
            this.BlosumThresholdInfo.Name = "BlosumThresholdInfo";
            this.BlosumThresholdInfo.Size = new System.Drawing.Size(13, 13);
            this.BlosumThresholdInfo.TabIndex = 84;
            this.BlosumThresholdInfo.Text = "?";
            this.BlosumThresholdInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.BlosumThresholdInfo.Click += new System.EventHandler(this.Info_Click);
            this.BlosumThresholdInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // ExplainUnknownMassShiftsAsBox
            // 
            this.ExplainUnknownMassShiftsAsBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ExplainUnknownMassShiftsAsBox.FormattingEnabled = true;
            this.ExplainUnknownMassShiftsAsBox.Items.AddRange(new object[] {
            "",
            "BlindPTMs",
            "PreferredPTMs",
            "Mutations"});
            this.ExplainUnknownMassShiftsAsBox.Location = new System.Drawing.Point(116, 18);
            this.ExplainUnknownMassShiftsAsBox.Name = "ExplainUnknownMassShiftsAsBox";
            this.ExplainUnknownMassShiftsAsBox.Size = new System.Drawing.Size(131, 21);
            this.ExplainUnknownMassShiftsAsBox.TabIndex = 87;
            this.ExplainUnknownMassShiftsAsBox.SelectedIndexChanged += new System.EventHandler(this.ExplainUnknownMassShiftsAsBox_SelectedIndexChanged);
            // 
            // MaxModificationMassPlusBox
            // 
            this.MaxModificationMassPlusBox.Location = new System.Drawing.Point(185, 139);
            this.MaxModificationMassPlusBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.MaxModificationMassPlusBox.Name = "MaxModificationMassPlusBox";
            this.MaxModificationMassPlusBox.Size = new System.Drawing.Size(75, 20);
            this.MaxModificationMassPlusBox.TabIndex = 3;
            this.MaxModificationMassPlusBox.Value = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.MaxModificationMassPlusBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MaxModificationMassPlusBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // BlosumBox
            // 
            this.BlosumBox.Location = new System.Drawing.Point(108, 83);
            this.BlosumBox.Name = "BlosumBox";
            this.BlosumBox.Size = new System.Drawing.Size(120, 20);
            this.BlosumBox.TabIndex = 68;
            this.BlosumBox.Text = "blosum62.fas";
            this.BlosumBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.BlosumBox.Leave += new System.EventHandler(this.BlosumBox_Leave);
            // 
            // MaxModificationMassMinusBox
            // 
            this.MaxModificationMassMinusBox.Location = new System.Drawing.Point(185, 165);
            this.MaxModificationMassMinusBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.MaxModificationMassMinusBox.Name = "MaxModificationMassMinusBox";
            this.MaxModificationMassMinusBox.Size = new System.Drawing.Size(75, 20);
            this.MaxModificationMassMinusBox.TabIndex = 4;
            this.MaxModificationMassMinusBox.Value = new decimal(new int[] {
            150,
            0,
            0,
            0});
            this.MaxModificationMassMinusBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.MaxModificationMassMinusBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // UnimodXMLBox
            // 
            this.UnimodXMLBox.Location = new System.Drawing.Point(108, 54);
            this.UnimodXMLBox.Name = "UnimodXMLBox";
            this.UnimodXMLBox.Size = new System.Drawing.Size(120, 20);
            this.UnimodXMLBox.TabIndex = 72;
            this.UnimodXMLBox.Text = "unimod.xml";
            this.UnimodXMLBox.TextChanged += new System.EventHandler(this.ValueBox_Leave);
            this.UnimodXMLBox.Leave += new System.EventHandler(this.UnimodXMLBox_Leave);
            // 
            // ExplainUnknownMassShiftsAsLabel
            // 
            this.ExplainUnknownMassShiftsAsLabel.AutoSize = true;
            this.ExplainUnknownMassShiftsAsLabel.Location = new System.Drawing.Point(20, 18);
            this.ExplainUnknownMassShiftsAsLabel.Name = "ExplainUnknownMassShiftsAsLabel";
            this.ExplainUnknownMassShiftsAsLabel.Size = new System.Drawing.Size(90, 26);
            this.ExplainUnknownMassShiftsAsLabel.TabIndex = 74;
            this.ExplainUnknownMassShiftsAsLabel.Text = "Explain Unknown\r\nMass Shifts As:";
            // 
            // BlosumThresholdLabel
            // 
            this.BlosumThresholdLabel.AutoSize = true;
            this.BlosumThresholdLabel.Location = new System.Drawing.Point(59, 113);
            this.BlosumThresholdLabel.Name = "BlosumThresholdLabel";
            this.BlosumThresholdLabel.Size = new System.Drawing.Size(94, 13);
            this.BlosumThresholdLabel.TabIndex = 50;
            this.BlosumThresholdLabel.Text = "Blosum Threshold:";
            // 
            // BlosumInfo
            // 
            this.BlosumInfo.AutoSize = true;
            this.BlosumInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.BlosumInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BlosumInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.BlosumInfo.Location = new System.Drawing.Point(97, 78);
            this.BlosumInfo.Name = "BlosumInfo";
            this.BlosumInfo.Size = new System.Drawing.Size(13, 13);
            this.BlosumInfo.TabIndex = 83;
            this.BlosumInfo.Text = "?";
            this.BlosumInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.BlosumInfo.Click += new System.EventHandler(this.Info_Click);
            this.BlosumInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // UnimodXMLInfo
            // 
            this.UnimodXMLInfo.AutoSize = true;
            this.UnimodXMLInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.UnimodXMLInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.UnimodXMLInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.UnimodXMLInfo.Location = new System.Drawing.Point(97, 49);
            this.UnimodXMLInfo.Name = "UnimodXMLInfo";
            this.UnimodXMLInfo.Size = new System.Drawing.Size(13, 13);
            this.UnimodXMLInfo.TabIndex = 82;
            this.UnimodXMLInfo.Text = "?";
            this.UnimodXMLInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.UnimodXMLInfo.Click += new System.EventHandler(this.Info_Click);
            this.UnimodXMLInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // BlosumLabel
            // 
            this.BlosumLabel.AutoSize = true;
            this.BlosumLabel.Location = new System.Drawing.Point(58, 86);
            this.BlosumLabel.Name = "BlosumLabel";
            this.BlosumLabel.Size = new System.Drawing.Size(44, 13);
            this.BlosumLabel.TabIndex = 40;
            this.BlosumLabel.Text = "Blosum:";
            // 
            // UnimodXMLBrowse
            // 
            this.UnimodXMLBrowse.Image = global::BumberDash.Properties.Resources.SearchFolder;
            this.UnimodXMLBrowse.Location = new System.Drawing.Point(234, 52);
            this.UnimodXMLBrowse.Name = "UnimodXMLBrowse";
            this.UnimodXMLBrowse.Size = new System.Drawing.Size(26, 23);
            this.UnimodXMLBrowse.TabIndex = 0;
            this.UnimodXMLBrowse.UseVisualStyleBackColor = true;
            this.UnimodXMLBrowse.Click += new System.EventHandler(this.UnimodXMLBrowse_Click);
            // 
            // MaxModificationMassMinusLabel
            // 
            this.MaxModificationMassMinusLabel.AutoSize = true;
            this.MaxModificationMassMinusLabel.Location = new System.Drawing.Point(7, 167);
            this.MaxModificationMassMinusLabel.Name = "MaxModificationMassMinusLabel";
            this.MaxModificationMassMinusLabel.Size = new System.Drawing.Size(172, 13);
            this.MaxModificationMassMinusLabel.TabIndex = 53;
            this.MaxModificationMassMinusLabel.Text = "Max Modification Mass Minus (Da):";
            // 
            // MaxModificationMassPlusInfo
            // 
            this.MaxModificationMassPlusInfo.AutoSize = true;
            this.MaxModificationMassPlusInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MaxModificationMassPlusInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaxModificationMassPlusInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.MaxModificationMassPlusInfo.Location = new System.Drawing.Point(174, 133);
            this.MaxModificationMassPlusInfo.Name = "MaxModificationMassPlusInfo";
            this.MaxModificationMassPlusInfo.Size = new System.Drawing.Size(13, 13);
            this.MaxModificationMassPlusInfo.TabIndex = 85;
            this.MaxModificationMassPlusInfo.Text = "?";
            this.MaxModificationMassPlusInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.MaxModificationMassPlusInfo.Click += new System.EventHandler(this.Info_Click);
            this.MaxModificationMassPlusInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // UnimodXMLLabel
            // 
            this.UnimodXMLLabel.AutoSize = true;
            this.UnimodXMLLabel.Location = new System.Drawing.Point(31, 57);
            this.UnimodXMLLabel.Name = "UnimodXMLLabel";
            this.UnimodXMLLabel.Size = new System.Drawing.Size(71, 13);
            this.UnimodXMLLabel.TabIndex = 70;
            this.UnimodXMLLabel.Text = "Unimod XML:";
            // 
            // MaxModificationMassPlusLabel
            // 
            this.MaxModificationMassPlusLabel.AutoSize = true;
            this.MaxModificationMassPlusLabel.Location = new System.Drawing.Point(15, 141);
            this.MaxModificationMassPlusLabel.Name = "MaxModificationMassPlusLabel";
            this.MaxModificationMassPlusLabel.Size = new System.Drawing.Size(164, 13);
            this.MaxModificationMassPlusLabel.TabIndex = 52;
            this.MaxModificationMassPlusLabel.Text = "Max Modification Mass Plus (Da):";
            // 
            // MaxModificationMassMinusInfo
            // 
            this.MaxModificationMassMinusInfo.AutoSize = true;
            this.MaxModificationMassMinusInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MaxModificationMassMinusInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaxModificationMassMinusInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.MaxModificationMassMinusInfo.Location = new System.Drawing.Point(174, 159);
            this.MaxModificationMassMinusInfo.Name = "MaxModificationMassMinusInfo";
            this.MaxModificationMassMinusInfo.Size = new System.Drawing.Size(13, 13);
            this.MaxModificationMassMinusInfo.TabIndex = 86;
            this.MaxModificationMassMinusInfo.Text = "?";
            this.MaxModificationMassMinusInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.MaxModificationMassMinusInfo.Click += new System.EventHandler(this.Info_Click);
            this.MaxModificationMassMinusInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // BlosumBrowse
            // 
            this.BlosumBrowse.Image = global::BumberDash.Properties.Resources.SearchFolder;
            this.BlosumBrowse.Location = new System.Drawing.Point(234, 81);
            this.BlosumBrowse.Name = "BlosumBrowse";
            this.BlosumBrowse.Size = new System.Drawing.Size(26, 23);
            this.BlosumBrowse.TabIndex = 1;
            this.BlosumBrowse.UseVisualStyleBackColor = true;
            this.BlosumBrowse.Click += new System.EventHandler(this.BlosumBrowse_Click);
            // 
            // ScoringGB
            // 
            this.ScoringGB.Controls.Add(this.DTScorePanel);
            this.ScoringGB.Controls.Add(this.ClassSizeMultiplierBox);
            this.ScoringGB.Controls.Add(this.NumIntensityClassesBox);
            this.ScoringGB.Controls.Add(this.ClassSizeMultiplierInfo);
            this.ScoringGB.Controls.Add(this.NumIntensityClassesInfo);
            this.ScoringGB.Controls.Add(this.NumIntensityClassesLabel);
            this.ScoringGB.Controls.Add(this.ClassSizeMultiplierLabel);
            this.ScoringGB.Location = new System.Drawing.Point(5, 81);
            this.ScoringGB.Name = "ScoringGB";
            this.ScoringGB.Size = new System.Drawing.Size(267, 154);
            this.ScoringGB.TabIndex = 6;
            this.ScoringGB.TabStop = false;
            this.ScoringGB.Text = "Scoring Options";
            // 
            // DTScorePanel
            // 
            this.DTScorePanel.Controls.Add(this.ComplementScoreWeightBox);
            this.DTScorePanel.Controls.Add(this.MzFidelityScoreWeightBox);
            this.DTScorePanel.Controls.Add(this.IntensityScoreWeightInf3);
            this.DTScorePanel.Controls.Add(this.IntensityScoreWeightInf2);
            this.DTScorePanel.Controls.Add(this.IntensityScoreWeightBox);
            this.DTScorePanel.Controls.Add(this.IntensityScoreWeightInfo);
            this.DTScorePanel.Controls.Add(this.ComplementScoreWeightLabel);
            this.DTScorePanel.Controls.Add(this.IntensityScoreWeightLabel);
            this.DTScorePanel.Controls.Add(this.MzFidelityScoreWeightLabel);
            this.DTScorePanel.Location = new System.Drawing.Point(6, 62);
            this.DTScorePanel.Name = "DTScorePanel";
            this.DTScorePanel.Size = new System.Drawing.Size(230, 85);
            this.DTScorePanel.TabIndex = 76;
            this.DTScorePanel.Visible = false;
            // 
            // ComplementScoreWeightBox
            // 
            this.ComplementScoreWeightBox.DecimalPlaces = 1;
            this.ComplementScoreWeightBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ComplementScoreWeightBox.Location = new System.Drawing.Point(148, 57);
            this.ComplementScoreWeightBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.ComplementScoreWeightBox.Name = "ComplementScoreWeightBox";
            this.ComplementScoreWeightBox.Size = new System.Drawing.Size(76, 20);
            this.ComplementScoreWeightBox.TabIndex = 11;
            this.ComplementScoreWeightBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.ComplementScoreWeightBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // MzFidelityScoreWeightBox
            // 
            this.MzFidelityScoreWeightBox.DecimalPlaces = 1;
            this.MzFidelityScoreWeightBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.MzFidelityScoreWeightBox.Location = new System.Drawing.Point(148, 31);
            this.MzFidelityScoreWeightBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.MzFidelityScoreWeightBox.Name = "MzFidelityScoreWeightBox";
            this.MzFidelityScoreWeightBox.Size = new System.Drawing.Size(76, 20);
            this.MzFidelityScoreWeightBox.TabIndex = 1;
            this.MzFidelityScoreWeightBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.MzFidelityScoreWeightBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // IntensityScoreWeightInf3
            // 
            this.IntensityScoreWeightInf3.AutoSize = true;
            this.IntensityScoreWeightInf3.Cursor = System.Windows.Forms.Cursors.Hand;
            this.IntensityScoreWeightInf3.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.IntensityScoreWeightInf3.ForeColor = System.Drawing.Color.MediumBlue;
            this.IntensityScoreWeightInf3.Location = new System.Drawing.Point(137, 51);
            this.IntensityScoreWeightInf3.Name = "IntensityScoreWeightInf3";
            this.IntensityScoreWeightInf3.Size = new System.Drawing.Size(13, 13);
            this.IntensityScoreWeightInf3.TabIndex = 85;
            this.IntensityScoreWeightInf3.Text = "?";
            // 
            // IntensityScoreWeightInf2
            // 
            this.IntensityScoreWeightInf2.AutoSize = true;
            this.IntensityScoreWeightInf2.Cursor = System.Windows.Forms.Cursors.Hand;
            this.IntensityScoreWeightInf2.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.IntensityScoreWeightInf2.ForeColor = System.Drawing.Color.MediumBlue;
            this.IntensityScoreWeightInf2.Location = new System.Drawing.Point(137, 25);
            this.IntensityScoreWeightInf2.Name = "IntensityScoreWeightInf2";
            this.IntensityScoreWeightInf2.Size = new System.Drawing.Size(13, 13);
            this.IntensityScoreWeightInf2.TabIndex = 84;
            this.IntensityScoreWeightInf2.Text = "?";
            // 
            // IntensityScoreWeightBox
            // 
            this.IntensityScoreWeightBox.DecimalPlaces = 1;
            this.IntensityScoreWeightBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.IntensityScoreWeightBox.Location = new System.Drawing.Point(148, 5);
            this.IntensityScoreWeightBox.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.IntensityScoreWeightBox.Name = "IntensityScoreWeightBox";
            this.IntensityScoreWeightBox.Size = new System.Drawing.Size(76, 20);
            this.IntensityScoreWeightBox.TabIndex = 0;
            this.IntensityScoreWeightBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.IntensityScoreWeightBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            // 
            // IntensityScoreWeightInfo
            // 
            this.IntensityScoreWeightInfo.AutoSize = true;
            this.IntensityScoreWeightInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.IntensityScoreWeightInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.IntensityScoreWeightInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.IntensityScoreWeightInfo.Location = new System.Drawing.Point(137, -1);
            this.IntensityScoreWeightInfo.Name = "IntensityScoreWeightInfo";
            this.IntensityScoreWeightInfo.Size = new System.Drawing.Size(13, 13);
            this.IntensityScoreWeightInfo.TabIndex = 83;
            this.IntensityScoreWeightInfo.Text = "?";
            this.IntensityScoreWeightInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.IntensityScoreWeightInfo.Click += new System.EventHandler(this.Info_Click);
            this.IntensityScoreWeightInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // ComplementScoreWeightLabel
            // 
            this.ComplementScoreWeightLabel.AutoSize = true;
            this.ComplementScoreWeightLabel.Location = new System.Drawing.Point(6, 59);
            this.ComplementScoreWeightLabel.Name = "ComplementScoreWeightLabel";
            this.ComplementScoreWeightLabel.Size = new System.Drawing.Size(136, 13);
            this.ComplementScoreWeightLabel.TabIndex = 10;
            this.ComplementScoreWeightLabel.Text = "Complement Score Weight:";
            // 
            // IntensityScoreWeightLabel
            // 
            this.IntensityScoreWeightLabel.AutoSize = true;
            this.IntensityScoreWeightLabel.Location = new System.Drawing.Point(25, 7);
            this.IntensityScoreWeightLabel.Name = "IntensityScoreWeightLabel";
            this.IntensityScoreWeightLabel.Size = new System.Drawing.Size(117, 13);
            this.IntensityScoreWeightLabel.TabIndex = 6;
            this.IntensityScoreWeightLabel.Text = "Intensity Score Weight:";
            // 
            // MzFidelityScoreWeightLabel
            // 
            this.MzFidelityScoreWeightLabel.AutoSize = true;
            this.MzFidelityScoreWeightLabel.Location = new System.Drawing.Point(14, 33);
            this.MzFidelityScoreWeightLabel.Name = "MzFidelityScoreWeightLabel";
            this.MzFidelityScoreWeightLabel.Size = new System.Drawing.Size(128, 13);
            this.MzFidelityScoreWeightLabel.TabIndex = 8;
            this.MzFidelityScoreWeightLabel.Text = "m/z FidelityScore Weight:";
            // 
            // ClassSizeMultiplierBox
            // 
            this.ClassSizeMultiplierBox.Location = new System.Drawing.Point(185, 41);
            this.ClassSizeMultiplierBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.ClassSizeMultiplierBox.Name = "ClassSizeMultiplierBox";
            this.ClassSizeMultiplierBox.Size = new System.Drawing.Size(45, 20);
            this.ClassSizeMultiplierBox.TabIndex = 1;
            this.ClassSizeMultiplierBox.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.ClassSizeMultiplierBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.ClassSizeMultiplierBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // NumIntensityClassesBox
            // 
            this.NumIntensityClassesBox.Location = new System.Drawing.Point(185, 15);
            this.NumIntensityClassesBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.NumIntensityClassesBox.Name = "NumIntensityClassesBox";
            this.NumIntensityClassesBox.Size = new System.Drawing.Size(45, 20);
            this.NumIntensityClassesBox.TabIndex = 0;
            this.NumIntensityClassesBox.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.NumIntensityClassesBox.ValueChanged += new System.EventHandler(this.ValueBox_Leave);
            this.NumIntensityClassesBox.Leave += new System.EventHandler(this.NumUpDownBox_Leave);
            // 
            // ClassSizeMultiplierInfo
            // 
            this.ClassSizeMultiplierInfo.AutoSize = true;
            this.ClassSizeMultiplierInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.ClassSizeMultiplierInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ClassSizeMultiplierInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.ClassSizeMultiplierInfo.Location = new System.Drawing.Point(174, 35);
            this.ClassSizeMultiplierInfo.Name = "ClassSizeMultiplierInfo";
            this.ClassSizeMultiplierInfo.Size = new System.Drawing.Size(13, 13);
            this.ClassSizeMultiplierInfo.TabIndex = 73;
            this.ClassSizeMultiplierInfo.Text = "?";
            this.ClassSizeMultiplierInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.ClassSizeMultiplierInfo.Click += new System.EventHandler(this.Info_Click);
            this.ClassSizeMultiplierInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // NumIntensityClassesInfo
            // 
            this.NumIntensityClassesInfo.AutoSize = true;
            this.NumIntensityClassesInfo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.NumIntensityClassesInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NumIntensityClassesInfo.ForeColor = System.Drawing.Color.MediumBlue;
            this.NumIntensityClassesInfo.Location = new System.Drawing.Point(174, 9);
            this.NumIntensityClassesInfo.Name = "NumIntensityClassesInfo";
            this.NumIntensityClassesInfo.Size = new System.Drawing.Size(13, 13);
            this.NumIntensityClassesInfo.TabIndex = 72;
            this.NumIntensityClassesInfo.Text = "?";
            this.NumIntensityClassesInfo.MouseLeave += new System.EventHandler(this.Info_MouseLeave);
            this.NumIntensityClassesInfo.Click += new System.EventHandler(this.Info_Click);
            this.NumIntensityClassesInfo.MouseEnter += new System.EventHandler(this.Info_MouseEnter);
            // 
            // NumIntensityClassesLabel
            // 
            this.NumIntensityClassesLabel.AutoSize = true;
            this.NumIntensityClassesLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.NumIntensityClassesLabel.Location = new System.Drawing.Point(69, 17);
            this.NumIntensityClassesLabel.Name = "NumIntensityClassesLabel";
            this.NumIntensityClassesLabel.Size = new System.Drawing.Size(110, 13);
            this.NumIntensityClassesLabel.TabIndex = 8;
            this.NumIntensityClassesLabel.Text = "# of Intensity Classes:";
            // 
            // ClassSizeMultiplierLabel
            // 
            this.ClassSizeMultiplierLabel.AutoSize = true;
            this.ClassSizeMultiplierLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.ClassSizeMultiplierLabel.Location = new System.Drawing.Point(77, 43);
            this.ClassSizeMultiplierLabel.Name = "ClassSizeMultiplierLabel";
            this.ClassSizeMultiplierLabel.Size = new System.Drawing.Size(102, 13);
            this.ClassSizeMultiplierLabel.TabIndex = 11;
            this.ClassSizeMultiplierLabel.Text = "Class Size Multiplier:";
            // 
            // AdvModeBox
            // 
            this.AdvModeBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.AdvModeBox.AutoSize = true;
            this.AdvModeBox.Location = new System.Drawing.Point(14, 576);
            this.AdvModeBox.Name = "AdvModeBox";
            this.AdvModeBox.Size = new System.Drawing.Size(15, 14);
            this.AdvModeBox.TabIndex = 4;
            this.AdvModeBox.UseVisualStyleBackColor = true;
            this.AdvModeBox.CheckedChanged += new System.EventHandler(this.AdvModeBox_CheckedChanged);
            // 
            // AdvModeLabel
            // 
            this.AdvModeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.AdvModeLabel.AutoSize = true;
            this.AdvModeLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.AdvModeLabel.Location = new System.Drawing.Point(30, 576);
            this.AdvModeLabel.Name = "AdvModeLabel";
            this.AdvModeLabel.Size = new System.Drawing.Size(111, 13);
            this.AdvModeLabel.TabIndex = 16;
            this.AdvModeLabel.Text = "Use Advanced Mode:";
            this.AdvModeLabel.Click += new System.EventHandler(this.AdvModeLabel_Click);
            // 
            // menuStrip1
            // 
            this.menuStrip1.BackColor = System.Drawing.SystemColors.Control;
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(534, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openConfigFileToolStripMenuItem,
            this.toolStripSeparator1,
            this.saveToolStripMenuItem,
            this.saveAsToolStripMenuItem,
            this.toolStripSeparator2,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openConfigFileToolStripMenuItem
            // 
            this.openConfigFileToolStripMenuItem.Name = "openConfigFileToolStripMenuItem";
            this.openConfigFileToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(144, 6);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // saveAsToolStripMenuItem
            // 
            this.saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            this.saveAsToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.saveAsToolStripMenuItem.Text = "Save As";
            this.saveAsToolStripMenuItem.Click += new System.EventHandler(this.saveAsToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(144, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // programToolStripMenuItem
            // 
            this.programToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.myrimatchToolStripMenuItem,
            this.direcTagToolStripMenuItem,
            this.tagReconToolStripMenuItem});
            this.programToolStripMenuItem.Name = "programToolStripMenuItem";
            this.programToolStripMenuItem.Size = new System.Drawing.Size(59, 20);
            this.programToolStripMenuItem.Text = "Program";
            // 
            // myrimatchToolStripMenuItem
            // 
            this.myrimatchToolStripMenuItem.Checked = true;
            this.myrimatchToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.myrimatchToolStripMenuItem.Name = "myrimatchToolStripMenuItem";
            this.myrimatchToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            this.myrimatchToolStripMenuItem.Text = "MyriMatch";
            this.myrimatchToolStripMenuItem.Click += new System.EventHandler(this.myrimatchToolStripMenuItem_Click);
            // 
            // direcTagToolStripMenuItem
            // 
            this.direcTagToolStripMenuItem.Name = "direcTagToolStripMenuItem";
            this.direcTagToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            this.direcTagToolStripMenuItem.Text = "DirecTag";
            this.direcTagToolStripMenuItem.Click += new System.EventHandler(this.direcTagToolStripMenuItem_Click);
            // 
            // tagReconToolStripMenuItem
            // 
            this.tagReconToolStripMenuItem.Name = "tagReconToolStripMenuItem";
            this.tagReconToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            this.tagReconToolStripMenuItem.Text = "Tag Recon";
            this.tagReconToolStripMenuItem.Click += new System.EventHandler(this.tagReconToolStripMenuItem_Click);
            // 
            // SoftMessageTimer
            // 
            this.SoftMessageTimer.Interval = 5000;
            this.SoftMessageTimer.Tick += new System.EventHandler(this.SoftMessageTimer_Tick);
            // 
            // SoftMessageFadeTimer
            // 
            this.SoftMessageFadeTimer.Interval = 200;
            this.SoftMessageFadeTimer.Tick += new System.EventHandler(this.SoftMessageFadeTimer_Tick);
            // 
            // CancelEditButton
            // 
            this.CancelEditButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.CancelEditButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CancelEditButton.Location = new System.Drawing.Point(447, 571);
            this.CancelEditButton.Name = "CancelEditButton";
            this.CancelEditButton.Size = new System.Drawing.Size(75, 23);
            this.CancelEditButton.TabIndex = 17;
            this.CancelEditButton.Text = "Cancel";
            this.CancelEditButton.UseVisualStyleBackColor = true;
            // 
            // SaveAsNewButton
            // 
            this.SaveAsNewButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SaveAsNewButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.SaveAsNewButton.Location = new System.Drawing.Point(348, 571);
            this.SaveAsNewButton.Name = "SaveAsNewButton";
            this.SaveAsNewButton.Size = new System.Drawing.Size(93, 23);
            this.SaveAsNewButton.TabIndex = 18;
            this.SaveAsNewButton.Text = "Save As New";
            this.SaveAsNewButton.UseVisualStyleBackColor = true;
            this.SaveAsNewButton.Click += new System.EventHandler(this.SaveAsNewButton_Click);
            // 
            // SaveOverOldButton
            // 
            this.SaveOverOldButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SaveOverOldButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.SaveOverOldButton.Location = new System.Drawing.Point(172, 571);
            this.SaveOverOldButton.Name = "SaveOverOldButton";
            this.SaveOverOldButton.Size = new System.Drawing.Size(93, 23);
            this.SaveOverOldButton.TabIndex = 19;
            this.SaveOverOldButton.Text = "Save Changes";
            this.SaveOverOldButton.UseVisualStyleBackColor = true;
            this.SaveOverOldButton.Click += new System.EventHandler(this.SaveOverOldButton_Click);
            // 
            // SaveAsTemporaryButton
            // 
            this.SaveAsTemporaryButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SaveAsTemporaryButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.SaveAsTemporaryButton.Location = new System.Drawing.Point(271, 571);
            this.SaveAsTemporaryButton.Name = "SaveAsTemporaryButton";
            this.SaveAsTemporaryButton.Size = new System.Drawing.Size(71, 23);
            this.SaveAsTemporaryButton.TabIndex = 20;
            this.SaveAsTemporaryButton.Text = "Use Once";
            this.SaveAsTemporaryButton.UseVisualStyleBackColor = true;
            this.SaveAsTemporaryButton.Click += new System.EventHandler(this.SaveAsTemporaryButton_Click);
            // 
            // ConfigForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlLight;
            this.ClientSize = new System.Drawing.Size(534, 603);
            this.Controls.Add(this.SaveAsTemporaryButton);
            this.Controls.Add(this.SaveOverOldButton);
            this.Controls.Add(this.SaveAsNewButton);
            this.Controls.Add(this.CancelEditButton);
            this.Controls.Add(this.AdvModeLabel);
            this.Controls.Add(this.AdvModeBox);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "MyriMatch Config Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ConfigForm_FormClosing);
            this.tabControl1.ResumeLayout(false);
            this.Gentab.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ModGB.ResumeLayout(false);
            this.ModGB.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.AppliedModDGV)).EndInit();
            this.MaxNumPreferredDeltaMassesPannel.ResumeLayout(false);
            this.MaxNumPreferredDeltaMassesPannel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxNumPreferredDeltaMassesBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxDynamicModsBox)).EndInit();
            this.InstrumentGB.ResumeLayout(false);
            this.InstrumentGB.PerformLayout();
            this.InstrumentPannel.ResumeLayout(false);
            this.InstrumentPannel.PerformLayout();
            this.DigestionGB.ResumeLayout(false);
            this.DigestionGB.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.NumMaxMissedCleavagesBox)).EndInit();
            this.ToleranceGB.ResumeLayout(false);
            this.PrecursorPannel.ResumeLayout(false);
            this.PrecursorPannel.PerformLayout();
            this.FragmentPannel.ResumeLayout(false);
            this.FragmentPannel.PerformLayout();
            this.TagReconTolerancePanel.ResumeLayout(false);
            this.TagReconTolerancePanel.PerformLayout();
            this.AdvTab.ResumeLayout(false);
            this.ChargeGB.ResumeLayout(false);
            this.ChargeGB.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.NumChargeStatesBox)).EndInit();
            this.SubsetGB.ResumeLayout(false);
            this.SubsetGB.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.EndProteinIndexBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.StartProteinIndexBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.EndSpectraScanNumBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.StartSpectraScanNumBox)).EndInit();
            this.SequenceGB.ResumeLayout(false);
            this.SequenceGB.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxSequenceMassBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MinSequenceMassBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MinCandidateLengthBox)).EndInit();
            this.MiscGB.ResumeLayout(false);
            this.MiscGB.PerformLayout();
            this.MaxResultsPanel.ResumeLayout(false);
            this.MaxResultsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxResultsBox)).EndInit();
            this.DTNewOptionsPanel.ResumeLayout(false);
            this.DTNewOptionsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.TicCutoffPercentageBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ProteinSampleSizeBox)).EndInit();
            this.PrecursorGbox.ResumeLayout(false);
            this.PrecursorGbox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxPrecursorAdjustmentBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MinPrecursorAdjustmentBox)).EndInit();
            this.AdjustPanel.ResumeLayout(false);
            this.AdjustPanel.PerformLayout();
            this.TagReconGB.ResumeLayout(false);
            this.TagReconGB.PerformLayout();
            this.ComputeXCorrPanel.ResumeLayout(false);
            this.ComputeXCorrPanel.PerformLayout();
            this.DirecTagGB.ResumeLayout(false);
            this.DirecTagGB.PerformLayout();
            this.MaxPeakPanel.ResumeLayout(false);
            this.MaxPeakPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MaxPeakCountBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxTagCountBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.TagLengthBox)).EndInit();
            this.TRModOptionsGB.ResumeLayout(false);
            this.TRModOptionsGB.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.BlosumThresholdBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxModificationMassPlusBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxModificationMassMinusBox)).EndInit();
            this.ScoringGB.ResumeLayout(false);
            this.ScoringGB.PerformLayout();
            this.DTScorePanel.ResumeLayout(false);
            this.DTScorePanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ComplementScoreWeightBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MzFidelityScoreWeightBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.IntensityScoreWeightBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ClassSizeMultiplierBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.NumIntensityClassesBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage Gentab;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openConfigFileToolStripMenuItem;
        private System.Windows.Forms.Label NumMaxMissedCleavagesLabel;
        private System.Windows.Forms.ComboBox CleavageRulesBox;
        private System.Windows.Forms.Label NumMinTerminiCleavagesLabel;
        private System.Windows.Forms.Label CleavageRulesLabel;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private System.Windows.Forms.GroupBox DigestionGB;
        private System.Windows.Forms.GroupBox InstrumentGB;
        private System.Windows.Forms.NumericUpDown NumMaxMissedCleavagesBox;
        private System.Windows.Forms.GroupBox ToleranceGB;
        private System.Windows.Forms.ComboBox FragmentMzToleranceUnitsBox;
        private System.Windows.Forms.Label FragmentMzToleranceLabel;
        private System.Windows.Forms.ComboBox PrecursorMzToleranceUnitsBox;
        private System.Windows.Forms.Label PrecursorMzToleranceLabel;
        private System.Windows.Forms.Label UseAvgMassOfSequencesLabel;
        private System.Windows.Forms.GroupBox ModGB;
        private System.Windows.Forms.NumericUpDown MaxDynamicModsBox;
        private System.Windows.Forms.Label MaxDynamicModsLabel;
        private System.Windows.Forms.ToolStripMenuItem programToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem myrimatchToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem direcTagToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tagReconToolStripMenuItem;
        private System.Windows.Forms.Label ModListLabel;
        private System.Windows.Forms.TabPage AdvTab;
        private System.Windows.Forms.GroupBox TagReconGB;
        private System.Windows.Forms.NumericUpDown MaxModificationMassMinusBox;
        private System.Windows.Forms.NumericUpDown MaxModificationMassPlusBox;
        private System.Windows.Forms.Label MaxModificationMassPlusLabel;
        private System.Windows.Forms.Label MaxModificationMassMinusLabel;
        private System.Windows.Forms.NumericUpDown BlosumThresholdBox;
        private System.Windows.Forms.Label BlosumLabel;
        private System.Windows.Forms.Label BlosumThresholdLabel;
        private System.Windows.Forms.GroupBox MiscGB;
        private System.Windows.Forms.GroupBox SubsetGB;
        private System.Windows.Forms.Label EndProteinIndexLabel;
        private System.Windows.Forms.NumericUpDown EndProteinIndexBox;
        private System.Windows.Forms.Label StartProteinIndexLabel;
        private System.Windows.Forms.NumericUpDown StartSpectraScanNumBox;
        private System.Windows.Forms.NumericUpDown StartProteinIndexBox;
        private System.Windows.Forms.Label StartSpectraScanNumLabel;
        private System.Windows.Forms.Label EndSpectraScanNumLabel;
        private System.Windows.Forms.NumericUpDown EndSpectraScanNumBox;
        private System.Windows.Forms.GroupBox SequenceGB;
        private System.Windows.Forms.Label MaxSequenceMassLabel;
        private System.Windows.Forms.NumericUpDown MaxSequenceMassBox;
        private System.Windows.Forms.NumericUpDown MinSequenceMassBox;
        private System.Windows.Forms.Label MinSequenceMassLabel;
        private System.Windows.Forms.Label ProteinSampleSizeLabel;
        private System.Windows.Forms.NumericUpDown ProteinSampleSizeBox;
        private System.Windows.Forms.NumericUpDown MaxResultsBox;
        private System.Windows.Forms.Label MaxResultsLabel;
        private System.Windows.Forms.GroupBox PrecursorGbox;
        private System.Windows.Forms.Label MaxPrecursorAdjustmentLabel;
        private System.Windows.Forms.Label MinPrecursorAdjustmentLabel;
        private System.Windows.Forms.CheckBox AdjustPrecursorMassBox;
        private System.Windows.Forms.Label AdjustPrecursorMassLabel;
        private System.Windows.Forms.Label ComplementMzToleranceLabel;
        private System.Windows.Forms.Label IsotopeMzToleranceLabel;
        private System.Windows.Forms.Label DeisotopingModeLabel;
        private System.Windows.Forms.Label ClassSizeMultiplierLabel;
        private System.Windows.Forms.NumericUpDown NumIntensityClassesBox;
        private System.Windows.Forms.NumericUpDown ClassSizeMultiplierBox;
        private System.Windows.Forms.Label NumIntensityClassesLabel;
        private System.Windows.Forms.Label MassReconModeLabel;
        private System.Windows.Forms.CheckBox MassReconModeBox;
        private System.Windows.Forms.TextBox BlosumBox;
        private System.Windows.Forms.Button BlosumBrowse;
        private System.Windows.Forms.Button UnimodXMLBrowse;
        private System.Windows.Forms.TextBox UnimodXMLBox;
        private System.Windows.Forms.Label UnimodXMLLabel;
        private System.Windows.Forms.Label NTerminusMzToleranceLabel;
        private System.Windows.Forms.Label CTerminusMzToleranceLabel;
        private System.Windows.Forms.GroupBox DirecTagGB;
        private System.Windows.Forms.NumericUpDown TagLengthBox;
        private System.Windows.Forms.Label TagLengthLabel;
        private System.Windows.Forms.NumericUpDown ComplementScoreWeightBox;
        private System.Windows.Forms.Label ComplementScoreWeightLabel;
        private System.Windows.Forms.NumericUpDown MzFidelityScoreWeightBox;
        private System.Windows.Forms.Label MzFidelityScoreWeightLabel;
        private System.Windows.Forms.NumericUpDown IntensityScoreWeightBox;
        private System.Windows.Forms.Label IntensityScoreWeightLabel;
        private System.Windows.Forms.GroupBox ScoringGB;
        private System.Windows.Forms.ComboBox InstrumentBox;
        private System.Windows.Forms.Label InstrumentLabel;
        private System.Windows.Forms.Label ExplainUnknownMassShiftsAsLabel;
        private System.Windows.Forms.Label UseNETAdjustmentLabel;
        private System.Windows.Forms.CheckBox UseNETAdjustmentBox;
        private System.Windows.Forms.ComboBox NumMinTerminiCleavagesBox;
        private System.Windows.Forms.Label UseSmartPlusThreeModelLabel;
        private System.Windows.Forms.CheckBox UseSmartPlusThreeModelBox;
        private System.Windows.Forms.NumericUpDown TicCutoffPercentageBox;
        private System.Windows.Forms.Label TicCutoffPercentageLabel;
        private System.Windows.Forms.NumericUpDown MinCandidateLengthBox;
        private System.Windows.Forms.Label MinCandidateLengthLabel;
        private System.Windows.Forms.Label AdvModeLabel;
        private System.Windows.Forms.CheckBox AdvModeBox;
        private System.Windows.Forms.TextBox ResidueBox;
        private System.Windows.Forms.Label ModMassLabel;
        private System.Windows.Forms.Label ResidueLabel;
        private System.Windows.Forms.Label MassReconModeInfo;
        private System.Windows.Forms.Label MinPrecursorAdjustmentInfo;
        private System.Windows.Forms.Label AdjustPrecursorMassInfo;
        private System.Windows.Forms.Label ClassSizeMultiplierInfo;
        private System.Windows.Forms.Label NumIntensityClassesInfo;
        private System.Windows.Forms.Label EndProteinIndexInfo;
        private System.Windows.Forms.Label StartProteinIndexInfo;
        private System.Windows.Forms.Label EndSpectraScanNumInfo;
        private System.Windows.Forms.Label StartSpectraScanNumInfo;
        private System.Windows.Forms.Label MaxSequenceMassInfo;
        private System.Windows.Forms.Label MinSequenceMassInfo;
        private System.Windows.Forms.Label IntensityScoreWeightInfo;
        private System.Windows.Forms.Label ComplementMzToleranceInfo;
        private System.Windows.Forms.Label IsotopeMzToleranceInfo;
        private System.Windows.Forms.Label TagLengthInfo;
        private System.Windows.Forms.Label MaxModificationMassMinusInfo;
        private System.Windows.Forms.Label MaxModificationMassPlusInfo;
        private System.Windows.Forms.Label BlosumThresholdInfo;
        private System.Windows.Forms.Label BlosumInfo;
        private System.Windows.Forms.Label UnimodXMLInfo;
        private System.Windows.Forms.Label ProteinSampleSizeInfo;
        private System.Windows.Forms.Label UseSmartPlusThreeModelInfo;
        private System.Windows.Forms.Label MaxResultsInfo;
        private System.Windows.Forms.Label TicCutoffPercentageInfo;
        private System.Windows.Forms.Label DeisotopingModeInfo;
        private System.Windows.Forms.Label UseAvgMassOfSequencesInfo;
        private System.Windows.Forms.Label NumMaxMissedCleavagesInfo;
        private System.Windows.Forms.Label NumMinTerminiCleavagesInfo;
        private System.Windows.Forms.Label CleavageRulesInfo;
        private System.Windows.Forms.Label FragmentMzToleranceInfo;
        private System.Windows.Forms.Label PrecursorMzToleranceInfo;
        private System.Windows.Forms.Label CTerminusMzToleranceInfo;
        private System.Windows.Forms.Label NTerminusMzToleranceInfo;
        private System.Windows.Forms.Label MaxDynamicModsInfo;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ComboBox DeisotopingModeBox;
        private System.Windows.Forms.NumericUpDown NumChargeStatesBox;
        private System.Windows.Forms.CheckBox DuplicateSpectraBox;
        private System.Windows.Forms.Label NumChargeStatesInfo;
        private System.Windows.Forms.Label DuplicateSpectraInfo;
        private System.Windows.Forms.Label DuplicateSpectraLabel;
        private System.Windows.Forms.Label NumChargeStatesLabel;
        private System.Windows.Forms.Panel DTScorePanel;
        private System.Windows.Forms.Panel DTNewOptionsPanel;
        private System.Windows.Forms.Label IntensityScoreWeightInf3;
        private System.Windows.Forms.Label IntensityScoreWeightInf2;
        private System.Windows.Forms.Panel AdjustPanel;
        private System.Windows.Forms.ComboBox UseAvgMassOfSequencesBox;
        private System.Windows.Forms.ComboBox ExplainUnknownMassShiftsAsBox;
        private System.Windows.Forms.ComboBox CTerminusMzToleranceUnitsBox;
        private System.Windows.Forms.ComboBox NTerminusMzToleranceUnitsBox;
        private System.Windows.Forms.CheckBox UseChargeStateFromMSBox;
        private System.Windows.Forms.Label UseChargeStateFromMSInfo;
        private System.Windows.Forms.Label UseChargeStateFromMSLabel;
        private System.Windows.Forms.Label EndProteinIndexAuto;
        private System.Windows.Forms.Label EndSpectraScanNumAuto;
        private System.Windows.Forms.ListView ModList;
        private System.Windows.Forms.ColumnHeader Description;
        private System.Windows.Forms.TextBox ModMassBox;
        private System.Windows.Forms.Label NumMaxMissedCleavagesAuto;
        private System.Windows.Forms.TextBox PrecursorMzToleranceBox;
        private System.Windows.Forms.TextBox FragmentMzToleranceBox;
        private System.Windows.Forms.TextBox CTerminusMzToleranceBox;
        private System.Windows.Forms.TextBox NTerminusMzToleranceBox;
        private System.Windows.Forms.TextBox IsotopeMzToleranceBox;
        private System.Windows.Forms.TextBox ComplementMzToleranceBox;
        private System.Windows.Forms.GroupBox ChargeGB;
        private System.Windows.Forms.GroupBox TRModOptionsGB;
        private System.Windows.Forms.NumericUpDown MaxPrecursorAdjustmentBox;
        private System.Windows.Forms.NumericUpDown MinPrecursorAdjustmentBox;
        private System.Windows.Forms.ComboBox ModTypeBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label StaticModsInfo;
        private System.Windows.Forms.NumericUpDown MaxNumPreferredDeltaMassesBox;
        private System.Windows.Forms.Label MaxNumPreferredDeltaMassesLabel;
        private System.Windows.Forms.Button AppliedModRemove;
        private System.Windows.Forms.Button AppliedModAdd;
        private System.Windows.Forms.Label AppliedModLabel;
        private System.Windows.Forms.Panel MaxNumPreferredDeltaMassesPannel;
        private System.Windows.Forms.Label SoftMessageLabel;
        private System.Windows.Forms.Timer SoftMessageTimer;
        private System.Windows.Forms.Timer SoftMessageFadeTimer;
        private System.Windows.Forms.DataGridView AppliedModDGV;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel PrecursorPannel;
        private System.Windows.Forms.Panel FragmentPannel;
        private System.Windows.Forms.Panel TagReconTolerancePanel;
        private System.Windows.Forms.Panel InstrumentPannel;
        private System.Windows.Forms.TextBox MaxTagScoreBox;
        private System.Windows.Forms.Label MaxTagCountLabel;
        private System.Windows.Forms.Label MaxTagScoreLabel;
        private System.Windows.Forms.NumericUpDown MaxTagCountBox;
        private System.Windows.Forms.Button CancelEditButton;
        private System.Windows.Forms.Button SaveAsNewButton;
        private System.Windows.Forms.Button SaveOverOldButton;
        private System.Windows.Forms.Panel MaxResultsPanel;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.DataGridViewComboBoxColumn Column3;
        private System.Windows.Forms.Panel ComputeXCorrPanel;
        private System.Windows.Forms.CheckBox ComputeXCorrBox;
        private System.Windows.Forms.Label ComputeXCorrLabel;
        private System.Windows.Forms.Panel MaxPeakPanel;
        private System.Windows.Forms.NumericUpDown MaxPeakCountBox;
        private System.Windows.Forms.Label MaxPeakCountLabel;
        private System.Windows.Forms.Label MaxPeakCountInfo;
        internal System.Windows.Forms.Button SaveAsTemporaryButton;
    }
}

