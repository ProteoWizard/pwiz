using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI
{
    partial class PeptideSettingsUI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PeptideSettingsUI));
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.comboStandardType = new System.Windows.Forms.ComboBox();
            this.comboLabelType = new System.Windows.Forms.ComboBox();
            this.listHeavyMods = new System.Windows.Forms.CheckedListBox();
            this.listStaticMods = new System.Windows.Forms.CheckedListBox();
            this.listStandardTypes = new System.Windows.Forms.CheckedListBox();
            this.textMaxVariableMods = new System.Windows.Forms.TextBox();
            this.textMaxNeutralLosses = new System.Windows.Forms.TextBox();
            this.comboMissedCleavages = new System.Windows.Forms.ComboBox();
            this.comboEnzyme = new System.Windows.Forms.ComboBox();
            this.comboBackgroundProteome = new System.Windows.Forms.ComboBox();
            this.textMeasureRTWindow = new System.Windows.Forms.TextBox();
            this.cbUseMeasuredRT = new System.Windows.Forms.CheckBox();
            this.comboRetentionTime = new System.Windows.Forms.ComboBox();
            this.cbAutoSelect = new System.Windows.Forms.CheckBox();
            this.textExcludeAAs = new System.Windows.Forms.TextBox();
            this.cbRaggedEnds = new System.Windows.Forms.CheckBox();
            this.btnEditExlusions = new System.Windows.Forms.Button();
            this.listboxExclusions = new System.Windows.Forms.CheckedListBox();
            this.textMaxLength = new System.Windows.Forms.TextBox();
            this.textMinLength = new System.Windows.Forms.TextBox();
            this.btnExplore = new System.Windows.Forms.Button();
            this.btnBuildLibrary = new System.Windows.Forms.Button();
            this.comboRank = new System.Windows.Forms.ComboBox();
            this.textPeptideCount = new System.Windows.Forms.TextBox();
            this.comboMatching = new System.Windows.Forms.ComboBox();
            this.cbLimitPeptides = new System.Windows.Forms.CheckBox();
            this.editLibraries = new System.Windows.Forms.Button();
            this.listLibraries = new System.Windows.Forms.CheckedListBox();
            this.btnUpdateCalculator = new System.Windows.Forms.Button();
            this.comboPeakScoringModel = new System.Windows.Forms.ComboBox();
            this.btnUpdateIonMobilityLibraries = new System.Windows.Forms.Button();
            this.comboDriftTimePredictor = new System.Windows.Forms.ComboBox();
            this.textSpectralLibraryDriftTimesResolvingPower = new System.Windows.Forms.TextBox();
            this.cbUseSpectralLibraryDriftTimes = new System.Windows.Forms.CheckBox();
            this.comboBoxPeptideUniquenessConstraint = new System.Windows.Forms.ComboBox();
            this.textSpectralLibraryDriftTimesWidthAtDtMax = new System.Windows.Forms.TextBox();
            this.textSpectralLibraryDriftTimesWidthAtDt0 = new System.Windows.Forms.TextBox();
            this.btnFilter = new System.Windows.Forms.Button();
            this.tbxMaxLoqBias = new System.Windows.Forms.TextBox();
            this.comboLodMethod = new System.Windows.Forms.ComboBox();
            this.tbxMaxLoqCv = new System.Windows.Forms.TextBox();
            this.tbxQuantUnits = new System.Windows.Forms.TextBox();
            this.comboQuantMsLevel = new System.Windows.Forms.ComboBox();
            this.comboNormalizationMethod = new System.Windows.Forms.ComboBox();
            this.comboRegressionFit = new System.Windows.Forms.ComboBox();
            this.comboWeighting = new System.Windows.Forms.ComboBox();
            this.listBoxSmallMolInternalStandardTypes = new System.Windows.Forms.CheckedListBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabDigestion = new System.Windows.Forms.TabPage();
            this.labelPeptideUniquenessConstraint = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.tabPrediction = new System.Windows.Forms.TabPage();
            this.labelWidthDtMax = new System.Windows.Forms.Label();
            this.labelWidthDtZero = new System.Windows.Forms.Label();
            this.cbLinear = new System.Windows.Forms.CheckBox();
            this.labelResolvingPower = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.tabFilter = new System.Windows.Forms.TabPage();
            this.label3 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.tabLibrary = new System.Windows.Forms.TabPage();
            this.panelPick = new System.Windows.Forms.Panel();
            this.labelPeptides = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.tabModifications = new System.Windows.Forms.TabPage();
            this.label18 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.labelStandardType = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.btnEditHeavyMods = new System.Windows.Forms.Button();
            this.label10 = new System.Windows.Forms.Label();
            this.btnEditStaticMods = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            this.tabLabels = new System.Windows.Forms.TabPage();
            this.buttonEditListSmallMolInternalStandardTypes = new System.Windows.Forms.Button();
            this.labelSmallMolInternalStandardTypes = new System.Windows.Forms.Label();
            this.tabIntegration = new System.Windows.Forms.TabPage();
            this.label36 = new System.Windows.Forms.Label();
            this.tabQuantification = new System.Windows.Forms.TabPage();
            this.groupBoxFiguresOfMerit = new System.Windows.Forms.GroupBox();
            this.lblCaclulateLodBy = new System.Windows.Forms.Label();
            this.lblMaxLoqBias = new System.Windows.Forms.Label();
            this.lblMaxLoqBiasPct = new System.Windows.Forms.Label();
            this.lblMaxLoqCvPct = new System.Windows.Forms.Label();
            this.lblMaxLoqCv = new System.Windows.Forms.Label();
            this.lblQuantUnits = new System.Windows.Forms.Label();
            this.lblMsLevel = new System.Windows.Forms.Label();
            this.lblNormalizationMethod = new System.Windows.Forms.Label();
            this.lblRegressionFit = new System.Windows.Forms.Label();
            this.lblRegressionWeighting = new System.Windows.Forms.Label();
            this.contextMenuCalculator = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addCalculatorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editCalculatorCurrentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editCalculatorListContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuIonMobilityLibraries = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addIonMobilityLibraryContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editIonMobilityLibraryCurrentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editIonMobilityLibraryListContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabDigestion.SuspendLayout();
            this.tabPrediction.SuspendLayout();
            this.tabFilter.SuspendLayout();
            this.tabLibrary.SuspendLayout();
            this.panelPick.SuspendLayout();
            this.tabModifications.SuspendLayout();
            this.tabLabels.SuspendLayout();
            this.tabIntegration.SuspendLayout();
            this.tabQuantification.SuspendLayout();
            this.groupBoxFiguresOfMerit.SuspendLayout();
            this.contextMenuCalculator.SuspendLayout();
            this.contextMenuIonMobilityLibraries.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.helpTip.SetToolTip(this.btnOk, resources.GetString("btnOk.ToolTip"));
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.helpTip.SetToolTip(this.btnCancel, resources.GetString("btnCancel.ToolTip"));
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 32767;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // comboStandardType
            // 
            resources.ApplyResources(this.comboStandardType, "comboStandardType");
            this.comboStandardType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboStandardType.FormattingEnabled = true;
            this.comboStandardType.Name = "comboStandardType";
            this.helpTip.SetToolTip(this.comboStandardType, resources.GetString("comboStandardType.ToolTip"));
            // 
            // comboLabelType
            // 
            resources.ApplyResources(this.comboLabelType, "comboLabelType");
            this.comboLabelType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLabelType.FormattingEnabled = true;
            this.comboLabelType.Name = "comboLabelType";
            this.helpTip.SetToolTip(this.comboLabelType, resources.GetString("comboLabelType.ToolTip"));
            this.comboLabelType.SelectedIndexChanged += new System.EventHandler(this.comboLabelType_SelectedIndexChanged);
            // 
            // listHeavyMods
            // 
            resources.ApplyResources(this.listHeavyMods, "listHeavyMods");
            this.listHeavyMods.CheckOnClick = true;
            this.listHeavyMods.FormattingEnabled = true;
            this.listHeavyMods.Name = "listHeavyMods";
            this.helpTip.SetToolTip(this.listHeavyMods, resources.GetString("listHeavyMods.ToolTip"));
            this.modeUIHandler.SetUIMode(this.listHeavyMods, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            // 
            // listStaticMods
            // 
            resources.ApplyResources(this.listStaticMods, "listStaticMods");
            this.listStaticMods.CheckOnClick = true;
            this.listStaticMods.FormattingEnabled = true;
            this.listStaticMods.Name = "listStaticMods";
            this.helpTip.SetToolTip(this.listStaticMods, resources.GetString("listStaticMods.ToolTip"));
            this.modeUIHandler.SetUIMode(this.listStaticMods, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            // 
            // listStandardTypes
            // 
            resources.ApplyResources(this.listStandardTypes, "listStandardTypes");
            this.listStandardTypes.CheckOnClick = true;
            this.listStandardTypes.FormattingEnabled = true;
            this.listStandardTypes.Name = "listStandardTypes";
            this.helpTip.SetToolTip(this.listStandardTypes, resources.GetString("listStandardTypes.ToolTip"));
            // 
            // textMaxVariableMods
            // 
            resources.ApplyResources(this.textMaxVariableMods, "textMaxVariableMods");
            this.textMaxVariableMods.Name = "textMaxVariableMods";
            this.helpTip.SetToolTip(this.textMaxVariableMods, resources.GetString("textMaxVariableMods.ToolTip"));
            this.modeUIHandler.SetUIMode(this.textMaxVariableMods, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            // 
            // textMaxNeutralLosses
            // 
            resources.ApplyResources(this.textMaxNeutralLosses, "textMaxNeutralLosses");
            this.textMaxNeutralLosses.Name = "textMaxNeutralLosses";
            this.helpTip.SetToolTip(this.textMaxNeutralLosses, resources.GetString("textMaxNeutralLosses.ToolTip"));
            this.modeUIHandler.SetUIMode(this.textMaxNeutralLosses, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            // 
            // comboMissedCleavages
            // 
            resources.ApplyResources(this.comboMissedCleavages, "comboMissedCleavages");
            this.comboMissedCleavages.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMissedCleavages.FormattingEnabled = true;
            this.comboMissedCleavages.Name = "comboMissedCleavages";
            this.helpTip.SetToolTip(this.comboMissedCleavages, resources.GetString("comboMissedCleavages.ToolTip"));
            // 
            // comboEnzyme
            // 
            resources.ApplyResources(this.comboEnzyme, "comboEnzyme");
            this.comboEnzyme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEnzyme.FormattingEnabled = true;
            this.comboEnzyme.Name = "comboEnzyme";
            this.helpTip.SetToolTip(this.comboEnzyme, resources.GetString("comboEnzyme.ToolTip"));
            this.comboEnzyme.SelectedIndexChanged += new System.EventHandler(this.enzyme_SelectedIndexChanged);
            // 
            // comboBackgroundProteome
            // 
            resources.ApplyResources(this.comboBackgroundProteome, "comboBackgroundProteome");
            this.comboBackgroundProteome.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBackgroundProteome.FormattingEnabled = true;
            this.comboBackgroundProteome.Name = "comboBackgroundProteome";
            this.helpTip.SetToolTip(this.comboBackgroundProteome, resources.GetString("comboBackgroundProteome.ToolTip"));
            this.comboBackgroundProteome.SelectedIndexChanged += new System.EventHandler(this.comboBackgroundProteome_SelectedIndexChanged);
            // 
            // textMeasureRTWindow
            // 
            resources.ApplyResources(this.textMeasureRTWindow, "textMeasureRTWindow");
            this.textMeasureRTWindow.Name = "textMeasureRTWindow";
            this.helpTip.SetToolTip(this.textMeasureRTWindow, resources.GetString("textMeasureRTWindow.ToolTip"));
            // 
            // cbUseMeasuredRT
            // 
            resources.ApplyResources(this.cbUseMeasuredRT, "cbUseMeasuredRT");
            this.cbUseMeasuredRT.Name = "cbUseMeasuredRT";
            this.helpTip.SetToolTip(this.cbUseMeasuredRT, resources.GetString("cbUseMeasuredRT.ToolTip"));
            this.cbUseMeasuredRT.UseVisualStyleBackColor = true;
            this.cbUseMeasuredRT.CheckedChanged += new System.EventHandler(this.cbUseMeasuredRT_CheckedChanged);
            // 
            // comboRetentionTime
            // 
            resources.ApplyResources(this.comboRetentionTime, "comboRetentionTime");
            this.comboRetentionTime.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRetentionTime.FormattingEnabled = true;
            this.comboRetentionTime.Name = "comboRetentionTime";
            this.helpTip.SetToolTip(this.comboRetentionTime, resources.GetString("comboRetentionTime.ToolTip"));
            this.comboRetentionTime.SelectedIndexChanged += new System.EventHandler(this.comboRetentionTime_SelectedIndexChanged);
            // 
            // cbAutoSelect
            // 
            resources.ApplyResources(this.cbAutoSelect, "cbAutoSelect");
            this.cbAutoSelect.Name = "cbAutoSelect";
            this.helpTip.SetToolTip(this.cbAutoSelect, resources.GetString("cbAutoSelect.ToolTip"));
            this.cbAutoSelect.UseVisualStyleBackColor = true;
            // 
            // textExcludeAAs
            // 
            resources.ApplyResources(this.textExcludeAAs, "textExcludeAAs");
            this.textExcludeAAs.Name = "textExcludeAAs";
            this.helpTip.SetToolTip(this.textExcludeAAs, resources.GetString("textExcludeAAs.ToolTip"));
            // 
            // cbRaggedEnds
            // 
            resources.ApplyResources(this.cbRaggedEnds, "cbRaggedEnds");
            this.cbRaggedEnds.Name = "cbRaggedEnds";
            this.helpTip.SetToolTip(this.cbRaggedEnds, resources.GetString("cbRaggedEnds.ToolTip"));
            this.cbRaggedEnds.UseVisualStyleBackColor = true;
            // 
            // btnEditExlusions
            // 
            resources.ApplyResources(this.btnEditExlusions, "btnEditExlusions");
            this.btnEditExlusions.Name = "btnEditExlusions";
            this.helpTip.SetToolTip(this.btnEditExlusions, resources.GetString("btnEditExlusions.ToolTip"));
            this.btnEditExlusions.UseVisualStyleBackColor = true;
            this.btnEditExlusions.Click += new System.EventHandler(this.btnEditExlusions_Click);
            // 
            // listboxExclusions
            // 
            resources.ApplyResources(this.listboxExclusions, "listboxExclusions");
            this.listboxExclusions.CheckOnClick = true;
            this.listboxExclusions.FormattingEnabled = true;
            this.listboxExclusions.Name = "listboxExclusions";
            this.helpTip.SetToolTip(this.listboxExclusions, resources.GetString("listboxExclusions.ToolTip"));
            // 
            // textMaxLength
            // 
            resources.ApplyResources(this.textMaxLength, "textMaxLength");
            this.textMaxLength.Name = "textMaxLength";
            this.helpTip.SetToolTip(this.textMaxLength, resources.GetString("textMaxLength.ToolTip"));
            // 
            // textMinLength
            // 
            resources.ApplyResources(this.textMinLength, "textMinLength");
            this.textMinLength.Name = "textMinLength";
            this.helpTip.SetToolTip(this.textMinLength, resources.GetString("textMinLength.ToolTip"));
            // 
            // btnExplore
            // 
            resources.ApplyResources(this.btnExplore, "btnExplore");
            this.btnExplore.Name = "btnExplore";
            this.helpTip.SetToolTip(this.btnExplore, resources.GetString("btnExplore.ToolTip"));
            this.btnExplore.UseVisualStyleBackColor = true;
            this.btnExplore.Click += new System.EventHandler(this.btnExplore_Click);
            // 
            // btnBuildLibrary
            // 
            resources.ApplyResources(this.btnBuildLibrary, "btnBuildLibrary");
            this.btnBuildLibrary.Name = "btnBuildLibrary";
            this.helpTip.SetToolTip(this.btnBuildLibrary, resources.GetString("btnBuildLibrary.ToolTip"));
            this.btnBuildLibrary.UseVisualStyleBackColor = true;
            this.btnBuildLibrary.Click += new System.EventHandler(this.btnBuildLibrary_Click);
            // 
            // comboRank
            // 
            resources.ApplyResources(this.comboRank, "comboRank");
            this.comboRank.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRank.FormattingEnabled = true;
            this.comboRank.Name = "comboRank";
            this.helpTip.SetToolTip(this.comboRank, resources.GetString("comboRank.ToolTip"));
            this.comboRank.SelectedIndexChanged += new System.EventHandler(this.comboRank_SelectedIndexChanged);
            // 
            // textPeptideCount
            // 
            resources.ApplyResources(this.textPeptideCount, "textPeptideCount");
            this.textPeptideCount.Name = "textPeptideCount";
            this.helpTip.SetToolTip(this.textPeptideCount, resources.GetString("textPeptideCount.ToolTip"));
            this.textPeptideCount.TextChanged += new System.EventHandler(this.textPeptideCount_TextChanged);
            // 
            // comboMatching
            // 
            resources.ApplyResources(this.comboMatching, "comboMatching");
            this.comboMatching.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMatching.FormattingEnabled = true;
            this.comboMatching.Items.AddRange(new object[] {
            resources.GetString("comboMatching.Items"),
            resources.GetString("comboMatching.Items1"),
            resources.GetString("comboMatching.Items2"),
            resources.GetString("comboMatching.Items3")});
            this.comboMatching.Name = "comboMatching";
            this.helpTip.SetToolTip(this.comboMatching, resources.GetString("comboMatching.ToolTip"));
            this.comboMatching.SelectedIndexChanged += new System.EventHandler(this.comboMatching_SelectedIndexChanged);
            // 
            // cbLimitPeptides
            // 
            resources.ApplyResources(this.cbLimitPeptides, "cbLimitPeptides");
            this.cbLimitPeptides.Name = "cbLimitPeptides";
            this.helpTip.SetToolTip(this.cbLimitPeptides, resources.GetString("cbLimitPeptides.ToolTip"));
            this.cbLimitPeptides.UseVisualStyleBackColor = true;
            this.cbLimitPeptides.CheckedChanged += new System.EventHandler(this.cbLimitPeptides_CheckedChanged);
            // 
            // editLibraries
            // 
            resources.ApplyResources(this.editLibraries, "editLibraries");
            this.editLibraries.Name = "editLibraries";
            this.helpTip.SetToolTip(this.editLibraries, resources.GetString("editLibraries.ToolTip"));
            this.editLibraries.UseVisualStyleBackColor = true;
            this.editLibraries.Click += new System.EventHandler(this.editLibraries_Click);
            // 
            // listLibraries
            // 
            resources.ApplyResources(this.listLibraries, "listLibraries");
            this.listLibraries.CheckOnClick = true;
            this.listLibraries.FormattingEnabled = true;
            this.listLibraries.Name = "listLibraries";
            this.helpTip.SetToolTip(this.listLibraries, resources.GetString("listLibraries.ToolTip"));
            this.listLibraries.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listLibraries_ItemCheck);
            // 
            // btnUpdateCalculator
            // 
            resources.ApplyResources(this.btnUpdateCalculator, "btnUpdateCalculator");
            this.btnUpdateCalculator.Image = global::pwiz.Skyline.Properties.Resources.Calculator;
            this.btnUpdateCalculator.Name = "btnUpdateCalculator";
            this.helpTip.SetToolTip(this.btnUpdateCalculator, resources.GetString("btnUpdateCalculator.ToolTip"));
            this.btnUpdateCalculator.UseVisualStyleBackColor = true;
            this.btnUpdateCalculator.Click += new System.EventHandler(this.btnUpdateCalculator_Click);
            // 
            // comboPeakScoringModel
            // 
            resources.ApplyResources(this.comboPeakScoringModel, "comboPeakScoringModel");
            this.comboPeakScoringModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboPeakScoringModel.FormattingEnabled = true;
            this.comboPeakScoringModel.Name = "comboPeakScoringModel";
            this.helpTip.SetToolTip(this.comboPeakScoringModel, resources.GetString("comboPeakScoringModel.ToolTip"));
            this.comboPeakScoringModel.SelectedIndexChanged += new System.EventHandler(this.comboPeakScoringModel_SelectedIndexChanged);
            // 
            // btnUpdateIonMobilityLibraries
            // 
            resources.ApplyResources(this.btnUpdateIonMobilityLibraries, "btnUpdateIonMobilityLibraries");
            this.btnUpdateIonMobilityLibraries.Image = global::pwiz.Skyline.Properties.Resources.Calculator;
            this.btnUpdateIonMobilityLibraries.Name = "btnUpdateIonMobilityLibraries";
            this.helpTip.SetToolTip(this.btnUpdateIonMobilityLibraries, resources.GetString("btnUpdateIonMobilityLibraries.ToolTip"));
            this.btnUpdateIonMobilityLibraries.UseVisualStyleBackColor = true;
            this.btnUpdateIonMobilityLibraries.Click += new System.EventHandler(this.btnUpdateIonMobilityLibrary_Click);
            // 
            // comboDriftTimePredictor
            // 
            resources.ApplyResources(this.comboDriftTimePredictor, "comboDriftTimePredictor");
            this.comboDriftTimePredictor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDriftTimePredictor.FormattingEnabled = true;
            this.comboDriftTimePredictor.Name = "comboDriftTimePredictor";
            this.helpTip.SetToolTip(this.comboDriftTimePredictor, resources.GetString("comboDriftTimePredictor.ToolTip"));
            this.comboDriftTimePredictor.SelectedIndexChanged += new System.EventHandler(this.comboDriftTime_SelectedIndexChanged);
            // 
            // textSpectralLibraryDriftTimesResolvingPower
            // 
            resources.ApplyResources(this.textSpectralLibraryDriftTimesResolvingPower, "textSpectralLibraryDriftTimesResolvingPower");
            this.textSpectralLibraryDriftTimesResolvingPower.Name = "textSpectralLibraryDriftTimesResolvingPower";
            this.helpTip.SetToolTip(this.textSpectralLibraryDriftTimesResolvingPower, resources.GetString("textSpectralLibraryDriftTimesResolvingPower.ToolTip"));
            // 
            // cbUseSpectralLibraryDriftTimes
            // 
            resources.ApplyResources(this.cbUseSpectralLibraryDriftTimes, "cbUseSpectralLibraryDriftTimes");
            this.cbUseSpectralLibraryDriftTimes.Name = "cbUseSpectralLibraryDriftTimes";
            this.helpTip.SetToolTip(this.cbUseSpectralLibraryDriftTimes, resources.GetString("cbUseSpectralLibraryDriftTimes.ToolTip"));
            this.cbUseSpectralLibraryDriftTimes.UseVisualStyleBackColor = true;
            this.cbUseSpectralLibraryDriftTimes.CheckedChanged += new System.EventHandler(this.cbUseSpectralLibraryDriftTimes_CheckChanged);
            // 
            // comboBoxPeptideUniquenessConstraint
            // 
            resources.ApplyResources(this.comboBoxPeptideUniquenessConstraint, "comboBoxPeptideUniquenessConstraint");
            this.comboBoxPeptideUniquenessConstraint.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxPeptideUniquenessConstraint.FormattingEnabled = true;
            this.comboBoxPeptideUniquenessConstraint.Items.AddRange(new object[] {
            resources.GetString("comboBoxPeptideUniquenessConstraint.Items"),
            resources.GetString("comboBoxPeptideUniquenessConstraint.Items1"),
            resources.GetString("comboBoxPeptideUniquenessConstraint.Items2"),
            resources.GetString("comboBoxPeptideUniquenessConstraint.Items3")});
            this.comboBoxPeptideUniquenessConstraint.Name = "comboBoxPeptideUniquenessConstraint";
            this.helpTip.SetToolTip(this.comboBoxPeptideUniquenessConstraint, resources.GetString("comboBoxPeptideUniquenessConstraint.ToolTip"));
            // 
            // textSpectralLibraryDriftTimesWidthAtDtMax
            // 
            resources.ApplyResources(this.textSpectralLibraryDriftTimesWidthAtDtMax, "textSpectralLibraryDriftTimesWidthAtDtMax");
            this.textSpectralLibraryDriftTimesWidthAtDtMax.Name = "textSpectralLibraryDriftTimesWidthAtDtMax";
            this.helpTip.SetToolTip(this.textSpectralLibraryDriftTimesWidthAtDtMax, resources.GetString("textSpectralLibraryDriftTimesWidthAtDtMax.ToolTip"));
            // 
            // textSpectralLibraryDriftTimesWidthAtDt0
            // 
            resources.ApplyResources(this.textSpectralLibraryDriftTimesWidthAtDt0, "textSpectralLibraryDriftTimesWidthAtDt0");
            this.textSpectralLibraryDriftTimesWidthAtDt0.Name = "textSpectralLibraryDriftTimesWidthAtDt0";
            this.helpTip.SetToolTip(this.textSpectralLibraryDriftTimesWidthAtDt0, resources.GetString("textSpectralLibraryDriftTimesWidthAtDt0.ToolTip"));
            // 
            // btnFilter
            // 
            resources.ApplyResources(this.btnFilter, "btnFilter");
            this.btnFilter.Name = "btnFilter";
            this.helpTip.SetToolTip(this.btnFilter, resources.GetString("btnFilter.ToolTip"));
            this.btnFilter.UseVisualStyleBackColor = true;
            this.btnFilter.Click += new System.EventHandler(this.btnFilter_Click);
            // 
            // tbxMaxLoqBias
            // 
            resources.ApplyResources(this.tbxMaxLoqBias, "tbxMaxLoqBias");
            this.tbxMaxLoqBias.Name = "tbxMaxLoqBias";
            this.helpTip.SetToolTip(this.tbxMaxLoqBias, resources.GetString("tbxMaxLoqBias.ToolTip"));
            // 
            // comboLodMethod
            // 
            resources.ApplyResources(this.comboLodMethod, "comboLodMethod");
            this.comboLodMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLodMethod.FormattingEnabled = true;
            this.comboLodMethod.Name = "comboLodMethod";
            this.helpTip.SetToolTip(this.comboLodMethod, resources.GetString("comboLodMethod.ToolTip"));
            // 
            // tbxMaxLoqCv
            // 
            resources.ApplyResources(this.tbxMaxLoqCv, "tbxMaxLoqCv");
            this.tbxMaxLoqCv.Name = "tbxMaxLoqCv";
            this.helpTip.SetToolTip(this.tbxMaxLoqCv, resources.GetString("tbxMaxLoqCv.ToolTip"));
            // 
            // tbxQuantUnits
            // 
            resources.ApplyResources(this.tbxQuantUnits, "tbxQuantUnits");
            this.tbxQuantUnits.Name = "tbxQuantUnits";
            this.helpTip.SetToolTip(this.tbxQuantUnits, resources.GetString("tbxQuantUnits.ToolTip"));
            // 
            // comboQuantMsLevel
            // 
            resources.ApplyResources(this.comboQuantMsLevel, "comboQuantMsLevel");
            this.comboQuantMsLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboQuantMsLevel.FormattingEnabled = true;
            this.comboQuantMsLevel.Items.AddRange(new object[] {
            resources.GetString("comboQuantMsLevel.Items"),
            resources.GetString("comboQuantMsLevel.Items1"),
            resources.GetString("comboQuantMsLevel.Items2")});
            this.comboQuantMsLevel.Name = "comboQuantMsLevel";
            this.helpTip.SetToolTip(this.comboQuantMsLevel, resources.GetString("comboQuantMsLevel.ToolTip"));
            // 
            // comboNormalizationMethod
            // 
            resources.ApplyResources(this.comboNormalizationMethod, "comboNormalizationMethod");
            this.comboNormalizationMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboNormalizationMethod.FormattingEnabled = true;
            this.comboNormalizationMethod.Name = "comboNormalizationMethod";
            this.helpTip.SetToolTip(this.comboNormalizationMethod, resources.GetString("comboNormalizationMethod.ToolTip"));
            // 
            // comboRegressionFit
            // 
            resources.ApplyResources(this.comboRegressionFit, "comboRegressionFit");
            this.comboRegressionFit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRegressionFit.FormattingEnabled = true;
            this.comboRegressionFit.Name = "comboRegressionFit";
            this.helpTip.SetToolTip(this.comboRegressionFit, resources.GetString("comboRegressionFit.ToolTip"));
            // 
            // comboWeighting
            // 
            resources.ApplyResources(this.comboWeighting, "comboWeighting");
            this.comboWeighting.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboWeighting.FormattingEnabled = true;
            this.comboWeighting.Name = "comboWeighting";
            this.helpTip.SetToolTip(this.comboWeighting, resources.GetString("comboWeighting.ToolTip"));
            // 
            // checkedListBoxSmallMolInternalStandardTypes
            // 
            this.listBoxSmallMolInternalStandardTypes.FormattingEnabled = true;
            resources.ApplyResources(this.listBoxSmallMolInternalStandardTypes, "listBoxSmallMolInternalStandardTypes");
            this.listBoxSmallMolInternalStandardTypes.Name = "listBoxSmallMolInternalStandardTypes";
            this.modeUIHandler.SetUIMode(this.listBoxSmallMolInternalStandardTypes, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.small_mol);
            // 
            // tabControl1
            // 
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Controls.Add(this.tabDigestion);
            this.tabControl1.Controls.Add(this.tabPrediction);
            this.tabControl1.Controls.Add(this.tabFilter);
            this.tabControl1.Controls.Add(this.tabLibrary);
            this.tabControl1.Controls.Add(this.tabModifications);
            this.tabControl1.Controls.Add(this.tabLabels);
            this.tabControl1.Controls.Add(this.tabIntegration);
            this.tabControl1.Controls.Add(this.tabQuantification);
            this.tabControl1.Name = "tabControl1";
            this.helpTip.SetToolTip(this.tabControl1, resources.GetString("tabControl1.ToolTip"));
            // 
            // tabDigestion
            // 
            resources.ApplyResources(this.tabDigestion, "tabDigestion");
            this.tabDigestion.Controls.Add(this.labelPeptideUniquenessConstraint);
            this.tabDigestion.Controls.Add(this.comboBoxPeptideUniquenessConstraint);
            this.tabDigestion.Controls.Add(this.label2);
            this.tabDigestion.Controls.Add(this.comboMissedCleavages);
            this.tabDigestion.Controls.Add(this.label1);
            this.tabDigestion.Controls.Add(this.comboEnzyme);
            this.tabDigestion.Controls.Add(this.label15);
            this.tabDigestion.Controls.Add(this.comboBackgroundProteome);
            this.tabDigestion.Name = "tabDigestion";
            this.modeUIHandler.SetUIMode(this.tabDigestion, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.tabDigestion.UseVisualStyleBackColor = true;
            // 
            // labelPeptideUniquenessConstraint
            // 
            resources.ApplyResources(this.labelPeptideUniquenessConstraint, "labelPeptideUniquenessConstraint");
            this.labelPeptideUniquenessConstraint.Name = "labelPeptideUniquenessConstraint";
            this.helpTip.SetToolTip(this.labelPeptideUniquenessConstraint, resources.GetString("labelPeptideUniquenessConstraint.ToolTip"));
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            this.helpTip.SetToolTip(this.label2, resources.GetString("label2.ToolTip"));
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            this.helpTip.SetToolTip(this.label1, resources.GetString("label1.ToolTip"));
            // 
            // label15
            // 
            resources.ApplyResources(this.label15, "label15");
            this.label15.Name = "label15";
            this.helpTip.SetToolTip(this.label15, resources.GetString("label15.ToolTip"));
            // 
            // tabPrediction
            // 
            resources.ApplyResources(this.tabPrediction, "tabPrediction");
            this.tabPrediction.Controls.Add(this.textSpectralLibraryDriftTimesWidthAtDtMax);
            this.tabPrediction.Controls.Add(this.textSpectralLibraryDriftTimesWidthAtDt0);
            this.tabPrediction.Controls.Add(this.labelWidthDtMax);
            this.tabPrediction.Controls.Add(this.labelWidthDtZero);
            this.tabPrediction.Controls.Add(this.cbLinear);
            this.tabPrediction.Controls.Add(this.textSpectralLibraryDriftTimesResolvingPower);
            this.tabPrediction.Controls.Add(this.cbUseSpectralLibraryDriftTimes);
            this.tabPrediction.Controls.Add(this.labelResolvingPower);
            this.tabPrediction.Controls.Add(this.btnUpdateIonMobilityLibraries);
            this.tabPrediction.Controls.Add(this.comboDriftTimePredictor);
            this.tabPrediction.Controls.Add(this.label19);
            this.tabPrediction.Controls.Add(this.btnUpdateCalculator);
            this.tabPrediction.Controls.Add(this.label14);
            this.tabPrediction.Controls.Add(this.textMeasureRTWindow);
            this.tabPrediction.Controls.Add(this.cbUseMeasuredRT);
            this.tabPrediction.Controls.Add(this.label13);
            this.tabPrediction.Controls.Add(this.comboRetentionTime);
            this.tabPrediction.Controls.Add(this.label9);
            this.tabPrediction.Name = "tabPrediction";
            this.helpTip.SetToolTip(this.tabPrediction, resources.GetString("tabPrediction.ToolTip"));
            this.tabPrediction.UseVisualStyleBackColor = true;
            // 
            // labelWidthDtMax
            // 
            resources.ApplyResources(this.labelWidthDtMax, "labelWidthDtMax");
            this.labelWidthDtMax.Name = "labelWidthDtMax";
            this.helpTip.SetToolTip(this.labelWidthDtMax, resources.GetString("labelWidthDtMax.ToolTip"));
            // 
            // labelWidthDtZero
            // 
            resources.ApplyResources(this.labelWidthDtZero, "labelWidthDtZero");
            this.labelWidthDtZero.Name = "labelWidthDtZero";
            this.helpTip.SetToolTip(this.labelWidthDtZero, resources.GetString("labelWidthDtZero.ToolTip"));
            // 
            // cbLinear
            // 
            resources.ApplyResources(this.cbLinear, "cbLinear");
            this.cbLinear.Name = "cbLinear";
            this.helpTip.SetToolTip(this.cbLinear, resources.GetString("cbLinear.ToolTip"));
            this.cbLinear.UseVisualStyleBackColor = true;
            this.cbLinear.CheckedChanged += new System.EventHandler(this.cbLinear_CheckedChanged);
            // 
            // labelResolvingPower
            // 
            resources.ApplyResources(this.labelResolvingPower, "labelResolvingPower");
            this.labelResolvingPower.Name = "labelResolvingPower";
            this.helpTip.SetToolTip(this.labelResolvingPower, resources.GetString("labelResolvingPower.ToolTip"));
            // 
            // label19
            // 
            resources.ApplyResources(this.label19, "label19");
            this.label19.Name = "label19";
            this.helpTip.SetToolTip(this.label19, resources.GetString("label19.ToolTip"));
            // 
            // label14
            // 
            resources.ApplyResources(this.label14, "label14");
            this.label14.Name = "label14";
            this.helpTip.SetToolTip(this.label14, resources.GetString("label14.ToolTip"));
            // 
            // label13
            // 
            resources.ApplyResources(this.label13, "label13");
            this.label13.Name = "label13";
            this.helpTip.SetToolTip(this.label13, resources.GetString("label13.ToolTip"));
            // 
            // label9
            // 
            resources.ApplyResources(this.label9, "label9");
            this.label9.Name = "label9";
            this.helpTip.SetToolTip(this.label9, resources.GetString("label9.ToolTip"));
            // 
            // tabFilter
            // 
            resources.ApplyResources(this.tabFilter, "tabFilter");
            this.tabFilter.Controls.Add(this.cbAutoSelect);
            this.tabFilter.Controls.Add(this.label3);
            this.tabFilter.Controls.Add(this.textExcludeAAs);
            this.tabFilter.Controls.Add(this.cbRaggedEnds);
            this.tabFilter.Controls.Add(this.btnEditExlusions);
            this.tabFilter.Controls.Add(this.listboxExclusions);
            this.tabFilter.Controls.Add(this.label6);
            this.tabFilter.Controls.Add(this.label5);
            this.tabFilter.Controls.Add(this.label4);
            this.tabFilter.Controls.Add(this.textMaxLength);
            this.tabFilter.Controls.Add(this.textMinLength);
            this.tabFilter.Name = "tabFilter";
            this.helpTip.SetToolTip(this.tabFilter, resources.GetString("tabFilter.ToolTip"));
            this.modeUIHandler.SetUIMode(this.tabFilter, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.tabFilter.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            this.helpTip.SetToolTip(this.label3, resources.GetString("label3.ToolTip"));
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            this.helpTip.SetToolTip(this.label6, resources.GetString("label6.ToolTip"));
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            this.helpTip.SetToolTip(this.label5, resources.GetString("label5.ToolTip"));
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            this.helpTip.SetToolTip(this.label4, resources.GetString("label4.ToolTip"));
            // 
            // tabLibrary
            // 
            resources.ApplyResources(this.tabLibrary, "tabLibrary");
            this.tabLibrary.Controls.Add(this.btnFilter);
            this.tabLibrary.Controls.Add(this.btnExplore);
            this.tabLibrary.Controls.Add(this.btnBuildLibrary);
            this.tabLibrary.Controls.Add(this.panelPick);
            this.tabLibrary.Controls.Add(this.editLibraries);
            this.tabLibrary.Controls.Add(this.label11);
            this.tabLibrary.Controls.Add(this.listLibraries);
            this.tabLibrary.Name = "tabLibrary";
            this.helpTip.SetToolTip(this.tabLibrary, resources.GetString("tabLibrary.ToolTip"));
            this.tabLibrary.UseVisualStyleBackColor = true;
            // 
            // panelPick
            // 
            resources.ApplyResources(this.panelPick, "panelPick");
            this.panelPick.Controls.Add(this.comboRank);
            this.panelPick.Controls.Add(this.labelPeptides);
            this.panelPick.Controls.Add(this.label12);
            this.panelPick.Controls.Add(this.textPeptideCount);
            this.panelPick.Controls.Add(this.comboMatching);
            this.panelPick.Controls.Add(this.cbLimitPeptides);
            this.panelPick.Controls.Add(this.label7);
            this.panelPick.Name = "panelPick";
            this.helpTip.SetToolTip(this.panelPick, resources.GetString("panelPick.ToolTip"));
            // 
            // labelPeptides
            // 
            resources.ApplyResources(this.labelPeptides, "labelPeptides");
            this.labelPeptides.Name = "labelPeptides";
            this.helpTip.SetToolTip(this.labelPeptides, resources.GetString("labelPeptides.ToolTip"));
            // 
            // label12
            // 
            resources.ApplyResources(this.label12, "label12");
            this.label12.Name = "label12";
            this.helpTip.SetToolTip(this.label12, resources.GetString("label12.ToolTip"));
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            this.helpTip.SetToolTip(this.label7, resources.GetString("label7.ToolTip"));
            // 
            // label11
            // 
            resources.ApplyResources(this.label11, "label11");
            this.label11.Name = "label11";
            this.helpTip.SetToolTip(this.label11, resources.GetString("label11.ToolTip"));
            // 
            // tabModifications
            // 
            resources.ApplyResources(this.tabModifications, "tabModifications");
            this.tabModifications.Controls.Add(this.label18);
            this.tabModifications.Controls.Add(this.textMaxNeutralLosses);
            this.tabModifications.Controls.Add(this.label17);
            this.tabModifications.Controls.Add(this.textMaxVariableMods);
            this.tabModifications.Controls.Add(this.comboStandardType);
            this.tabModifications.Controls.Add(this.labelStandardType);
            this.tabModifications.Controls.Add(this.label16);
            this.tabModifications.Controls.Add(this.comboLabelType);
            this.tabModifications.Controls.Add(this.btnEditHeavyMods);
            this.tabModifications.Controls.Add(this.label10);
            this.tabModifications.Controls.Add(this.listHeavyMods);
            this.tabModifications.Controls.Add(this.btnEditStaticMods);
            this.tabModifications.Controls.Add(this.label8);
            this.tabModifications.Controls.Add(this.listStaticMods);
            this.tabModifications.Controls.Add(this.listStandardTypes);
            this.tabModifications.Name = "tabModifications";
            this.helpTip.SetToolTip(this.tabModifications, resources.GetString("tabModifications.ToolTip"));
            this.modeUIHandler.SetUIMode(this.tabModifications, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.tabModifications.UseVisualStyleBackColor = true;
            // 
            // label18
            // 
            resources.ApplyResources(this.label18, "label18");
            this.label18.Name = "label18";
            this.helpTip.SetToolTip(this.label18, resources.GetString("label18.ToolTip"));
            // 
            // label17
            // 
            resources.ApplyResources(this.label17, "label17");
            this.label17.Name = "label17";
            this.helpTip.SetToolTip(this.label17, resources.GetString("label17.ToolTip"));
            // 
            // labelStandardType
            // 
            resources.ApplyResources(this.labelStandardType, "labelStandardType");
            this.labelStandardType.Name = "labelStandardType";
            this.helpTip.SetToolTip(this.labelStandardType, resources.GetString("labelStandardType.ToolTip"));
            // 
            // label16
            // 
            resources.ApplyResources(this.label16, "label16");
            this.label16.Name = "label16";
            this.helpTip.SetToolTip(this.label16, resources.GetString("label16.ToolTip"));
            // 
            // btnEditHeavyMods
            // 
            resources.ApplyResources(this.btnEditHeavyMods, "btnEditHeavyMods");
            this.btnEditHeavyMods.Name = "btnEditHeavyMods";
            this.helpTip.SetToolTip(this.btnEditHeavyMods, resources.GetString("btnEditHeavyMods.ToolTip"));
            this.btnEditHeavyMods.UseVisualStyleBackColor = true;
            this.btnEditHeavyMods.Click += new System.EventHandler(this.btnEditHeavyMods_Click);
            // 
            // label10
            // 
            resources.ApplyResources(this.label10, "label10");
            this.label10.Name = "label10";
            this.helpTip.SetToolTip(this.label10, resources.GetString("label10.ToolTip"));
            // 
            // btnEditStaticMods
            // 
            resources.ApplyResources(this.btnEditStaticMods, "btnEditStaticMods");
            this.btnEditStaticMods.Name = "btnEditStaticMods";
            this.helpTip.SetToolTip(this.btnEditStaticMods, resources.GetString("btnEditStaticMods.ToolTip"));
            this.btnEditStaticMods.UseVisualStyleBackColor = true;
            this.btnEditStaticMods.Click += new System.EventHandler(this.btnEditStaticMods_Click);
            // 
            // label8
            // 
            resources.ApplyResources(this.label8, "label8");
            this.label8.Name = "label8";
            this.helpTip.SetToolTip(this.label8, resources.GetString("label8.ToolTip"));
            // 
            // tabLabels
            // 
            this.tabLabels.Controls.Add(this.buttonEditListSmallMolInternalStandardTypes);
            this.tabLabels.Controls.Add(this.labelSmallMolInternalStandardTypes);
            this.tabLabels.Controls.Add(this.listBoxSmallMolInternalStandardTypes);
            resources.ApplyResources(this.tabLabels, "tabLabels");
            this.tabLabels.Name = "tabLabels";
            this.modeUIHandler.SetUIMode(this.tabLabels, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.small_mol_only);
            this.tabLabels.UseVisualStyleBackColor = true;
            // 
            // buttonEditListSmallMolInternalStandardTypes
            // 
            resources.ApplyResources(this.buttonEditListSmallMolInternalStandardTypes, "buttonEditListSmallMolInternalStandardTypes");
            this.buttonEditListSmallMolInternalStandardTypes.Name = "buttonEditListSmallMolInternalStandardTypes";
            this.buttonEditListSmallMolInternalStandardTypes.UseVisualStyleBackColor = true;
            this.buttonEditListSmallMolInternalStandardTypes.Click += new System.EventHandler(this.btnEditSmallMoleculeInternalStandards_Click);
            // 
            // labelSmallMolInternalStandardTypes
            // 
            resources.ApplyResources(this.labelSmallMolInternalStandardTypes, "labelSmallMolInternalStandardTypes");
            this.labelSmallMolInternalStandardTypes.Name = "labelSmallMolInternalStandardTypes";
            this.modeUIHandler.SetUIMode(this.labelSmallMolInternalStandardTypes, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.small_mol);
            // 
            // tabIntegration
            // 
            resources.ApplyResources(this.tabIntegration, "tabIntegration");
            this.tabIntegration.Controls.Add(this.comboPeakScoringModel);
            this.tabIntegration.Controls.Add(this.label36);
            this.tabIntegration.Name = "tabIntegration";
            this.helpTip.SetToolTip(this.tabIntegration, resources.GetString("tabIntegration.ToolTip"));
            this.tabIntegration.UseVisualStyleBackColor = true;
            // 
            // label36
            // 
            resources.ApplyResources(this.label36, "label36");
            this.label36.Name = "label36";
            this.helpTip.SetToolTip(this.label36, resources.GetString("label36.ToolTip"));
            // 
            // tabQuantification
            // 
            resources.ApplyResources(this.tabQuantification, "tabQuantification");
            this.tabQuantification.Controls.Add(this.groupBoxFiguresOfMerit);
            this.tabQuantification.Controls.Add(this.tbxQuantUnits);
            this.tabQuantification.Controls.Add(this.lblQuantUnits);
            this.tabQuantification.Controls.Add(this.comboQuantMsLevel);
            this.tabQuantification.Controls.Add(this.lblMsLevel);
            this.tabQuantification.Controls.Add(this.comboNormalizationMethod);
            this.tabQuantification.Controls.Add(this.lblNormalizationMethod);
            this.tabQuantification.Controls.Add(this.comboRegressionFit);
            this.tabQuantification.Controls.Add(this.lblRegressionFit);
            this.tabQuantification.Controls.Add(this.comboWeighting);
            this.tabQuantification.Controls.Add(this.lblRegressionWeighting);
            this.tabQuantification.Name = "tabQuantification";
            this.helpTip.SetToolTip(this.tabQuantification, resources.GetString("tabQuantification.ToolTip"));
            this.tabQuantification.UseVisualStyleBackColor = true;
            // 
            // groupBoxFiguresOfMerit
            // 
            resources.ApplyResources(this.groupBoxFiguresOfMerit, "groupBoxFiguresOfMerit");
            this.groupBoxFiguresOfMerit.Controls.Add(this.tbxMaxLoqBias);
            this.groupBoxFiguresOfMerit.Controls.Add(this.comboLodMethod);
            this.groupBoxFiguresOfMerit.Controls.Add(this.lblCaclulateLodBy);
            this.groupBoxFiguresOfMerit.Controls.Add(this.lblMaxLoqBias);
            this.groupBoxFiguresOfMerit.Controls.Add(this.lblMaxLoqBiasPct);
            this.groupBoxFiguresOfMerit.Controls.Add(this.lblMaxLoqCvPct);
            this.groupBoxFiguresOfMerit.Controls.Add(this.tbxMaxLoqCv);
            this.groupBoxFiguresOfMerit.Controls.Add(this.lblMaxLoqCv);
            this.groupBoxFiguresOfMerit.Name = "groupBoxFiguresOfMerit";
            this.groupBoxFiguresOfMerit.TabStop = false;
            this.helpTip.SetToolTip(this.groupBoxFiguresOfMerit, resources.GetString("groupBoxFiguresOfMerit.ToolTip"));
            // 
            // lblCaclulateLodBy
            // 
            resources.ApplyResources(this.lblCaclulateLodBy, "lblCaclulateLodBy");
            this.lblCaclulateLodBy.Name = "lblCaclulateLodBy";
            this.helpTip.SetToolTip(this.lblCaclulateLodBy, resources.GetString("lblCaclulateLodBy.ToolTip"));
            // 
            // lblMaxLoqBias
            // 
            resources.ApplyResources(this.lblMaxLoqBias, "lblMaxLoqBias");
            this.lblMaxLoqBias.Name = "lblMaxLoqBias";
            this.helpTip.SetToolTip(this.lblMaxLoqBias, resources.GetString("lblMaxLoqBias.ToolTip"));
            // 
            // lblMaxLoqBiasPct
            // 
            resources.ApplyResources(this.lblMaxLoqBiasPct, "lblMaxLoqBiasPct");
            this.lblMaxLoqBiasPct.Name = "lblMaxLoqBiasPct";
            this.helpTip.SetToolTip(this.lblMaxLoqBiasPct, resources.GetString("lblMaxLoqBiasPct.ToolTip"));
            // 
            // lblMaxLoqCvPct
            // 
            resources.ApplyResources(this.lblMaxLoqCvPct, "lblMaxLoqCvPct");
            this.lblMaxLoqCvPct.Name = "lblMaxLoqCvPct";
            this.helpTip.SetToolTip(this.lblMaxLoqCvPct, resources.GetString("lblMaxLoqCvPct.ToolTip"));
            // 
            // lblMaxLoqCv
            // 
            resources.ApplyResources(this.lblMaxLoqCv, "lblMaxLoqCv");
            this.lblMaxLoqCv.Name = "lblMaxLoqCv";
            this.helpTip.SetToolTip(this.lblMaxLoqCv, resources.GetString("lblMaxLoqCv.ToolTip"));
            // 
            // lblQuantUnits
            // 
            resources.ApplyResources(this.lblQuantUnits, "lblQuantUnits");
            this.lblQuantUnits.Name = "lblQuantUnits";
            this.helpTip.SetToolTip(this.lblQuantUnits, resources.GetString("lblQuantUnits.ToolTip"));
            // 
            // lblMsLevel
            // 
            resources.ApplyResources(this.lblMsLevel, "lblMsLevel");
            this.lblMsLevel.Name = "lblMsLevel";
            this.helpTip.SetToolTip(this.lblMsLevel, resources.GetString("lblMsLevel.ToolTip"));
            // 
            // lblNormalizationMethod
            // 
            resources.ApplyResources(this.lblNormalizationMethod, "lblNormalizationMethod");
            this.lblNormalizationMethod.Name = "lblNormalizationMethod";
            this.helpTip.SetToolTip(this.lblNormalizationMethod, resources.GetString("lblNormalizationMethod.ToolTip"));
            // 
            // lblRegressionFit
            // 
            resources.ApplyResources(this.lblRegressionFit, "lblRegressionFit");
            this.lblRegressionFit.Name = "lblRegressionFit";
            this.helpTip.SetToolTip(this.lblRegressionFit, resources.GetString("lblRegressionFit.ToolTip"));
            // 
            // lblRegressionWeighting
            // 
            resources.ApplyResources(this.lblRegressionWeighting, "lblRegressionWeighting");
            this.lblRegressionWeighting.Name = "lblRegressionWeighting";
            this.helpTip.SetToolTip(this.lblRegressionWeighting, resources.GetString("lblRegressionWeighting.ToolTip"));
            // 
            // contextMenuCalculator
            // 
            resources.ApplyResources(this.contextMenuCalculator, "contextMenuCalculator");
            this.contextMenuCalculator.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addCalculatorContextMenuItem,
            this.editCalculatorCurrentContextMenuItem,
            this.editCalculatorListContextMenuItem});
            this.contextMenuCalculator.Name = "contextMenuCalculator";
            this.helpTip.SetToolTip(this.contextMenuCalculator, resources.GetString("contextMenuCalculator.ToolTip"));
            // 
            // addCalculatorContextMenuItem
            // 
            resources.ApplyResources(this.addCalculatorContextMenuItem, "addCalculatorContextMenuItem");
            this.addCalculatorContextMenuItem.Name = "addCalculatorContextMenuItem";
            this.addCalculatorContextMenuItem.Click += new System.EventHandler(this.addCalculatorContextMenuItem_Click);
            // 
            // editCalculatorCurrentContextMenuItem
            // 
            resources.ApplyResources(this.editCalculatorCurrentContextMenuItem, "editCalculatorCurrentContextMenuItem");
            this.editCalculatorCurrentContextMenuItem.Name = "editCalculatorCurrentContextMenuItem";
            this.editCalculatorCurrentContextMenuItem.Click += new System.EventHandler(this.editCalculatorCurrentContextMenuItem_Click);
            // 
            // editCalculatorListContextMenuItem
            // 
            resources.ApplyResources(this.editCalculatorListContextMenuItem, "editCalculatorListContextMenuItem");
            this.editCalculatorListContextMenuItem.Name = "editCalculatorListContextMenuItem";
            this.editCalculatorListContextMenuItem.Click += new System.EventHandler(this.editCalculatorListContextMenuItem_Click);
            // 
            // contextMenuIonMobilityLibraries
            // 
            resources.ApplyResources(this.contextMenuIonMobilityLibraries, "contextMenuIonMobilityLibraries");
            this.contextMenuIonMobilityLibraries.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addIonMobilityLibraryContextMenuItem,
            this.editIonMobilityLibraryCurrentContextMenuItem,
            this.editIonMobilityLibraryListContextMenuItem});
            this.contextMenuIonMobilityLibraries.Name = "contextMenuIonMobilityLibraries";
            this.helpTip.SetToolTip(this.contextMenuIonMobilityLibraries, resources.GetString("contextMenuIonMobilityLibraries.ToolTip"));
            // 
            // addIonMobilityLibraryContextMenuItem
            // 
            resources.ApplyResources(this.addIonMobilityLibraryContextMenuItem, "addIonMobilityLibraryContextMenuItem");
            this.addIonMobilityLibraryContextMenuItem.Name = "addIonMobilityLibraryContextMenuItem";
            this.addIonMobilityLibraryContextMenuItem.Click += new System.EventHandler(this.addIonMobilityLibraryContextMenuItem_Click);
            // 
            // editIonMobilityLibraryCurrentContextMenuItem
            // 
            resources.ApplyResources(this.editIonMobilityLibraryCurrentContextMenuItem, "editIonMobilityLibraryCurrentContextMenuItem");
            this.editIonMobilityLibraryCurrentContextMenuItem.Name = "editIonMobilityLibraryCurrentContextMenuItem";
            this.editIonMobilityLibraryCurrentContextMenuItem.Click += new System.EventHandler(this.editIonMobilityLibraryCurrentContextMenuItem_Click);
            // 
            // editIonMobilityLibraryListContextMenuItem
            // 
            resources.ApplyResources(this.editIonMobilityLibraryListContextMenuItem, "editIonMobilityLibraryListContextMenuItem");
            this.editIonMobilityLibraryListContextMenuItem.Name = "editIonMobilityLibraryListContextMenuItem";
            this.editIonMobilityLibraryListContextMenuItem.Click += new System.EventHandler(this.editIonMobilityLibraryListContextMenuItem_Click);
            // 
            // PeptideSettingsUI
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PeptideSettingsUI";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabDigestion.ResumeLayout(false);
            this.tabDigestion.PerformLayout();
            this.tabPrediction.ResumeLayout(false);
            this.tabPrediction.PerformLayout();
            this.tabFilter.ResumeLayout(false);
            this.tabFilter.PerformLayout();
            this.tabLibrary.ResumeLayout(false);
            this.tabLibrary.PerformLayout();
            this.panelPick.ResumeLayout(false);
            this.panelPick.PerformLayout();
            this.tabModifications.ResumeLayout(false);
            this.tabModifications.PerformLayout();
            this.tabLabels.ResumeLayout(false);
            this.tabLabels.PerformLayout();
            this.tabIntegration.ResumeLayout(false);
            this.tabIntegration.PerformLayout();
            this.tabQuantification.ResumeLayout(false);
            this.tabQuantification.PerformLayout();
            this.groupBoxFiguresOfMerit.ResumeLayout(false);
            this.groupBoxFiguresOfMerit.PerformLayout();
            this.contextMenuCalculator.ResumeLayout(false);
            this.contextMenuIonMobilityLibraries.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.TabPage tabFilter;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textExcludeAAs;
        private System.Windows.Forms.Button btnEditExlusions;
        private System.Windows.Forms.CheckedListBox listboxExclusions;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textMaxLength;
        private System.Windows.Forms.TextBox textMinLength;
        private System.Windows.Forms.TabPage tabDigestion;
        private System.Windows.Forms.CheckBox cbRaggedEnds;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboMissedCleavages;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboEnzyme;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.CheckBox cbAutoSelect;
        private System.Windows.Forms.TabPage tabModifications;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.CheckedListBox listStaticMods;
        private System.Windows.Forms.Button btnEditStaticMods;
        private System.Windows.Forms.TabPage tabPrediction;
        private System.Windows.Forms.ComboBox comboRetentionTime;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button btnEditHeavyMods;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.CheckedListBox listHeavyMods;
        private System.Windows.Forms.TabPage tabLibrary;
        private System.Windows.Forms.Button editLibraries;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.CheckedListBox listLibraries;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox comboMatching;
        private System.Windows.Forms.ComboBox comboRank;
        private System.Windows.Forms.CheckBox cbLimitPeptides;
        private System.Windows.Forms.TextBox textPeptideCount;
        private System.Windows.Forms.Label labelPeptides;
        private System.Windows.Forms.Panel panelPick;
        private System.Windows.Forms.Button btnBuildLibrary;
        private System.Windows.Forms.CheckBox cbUseMeasuredRT;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox textMeasureRTWindow;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.ComboBox comboBackgroundProteome;
        private System.Windows.Forms.Button btnExplore;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.ComboBox comboLabelType;
        private System.Windows.Forms.ComboBox comboStandardType;
        private System.Windows.Forms.Label labelStandardType;
        private System.Windows.Forms.CheckedListBox listStandardTypes;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.TextBox textMaxNeutralLosses;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.TextBox textMaxVariableMods;
        private System.Windows.Forms.Button btnUpdateCalculator;
        private System.Windows.Forms.ContextMenuStrip contextMenuCalculator;
        private System.Windows.Forms.ToolStripMenuItem addCalculatorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editCalculatorCurrentContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editCalculatorListContextMenuItem;
        private System.Windows.Forms.TabPage tabIntegration;
        private System.Windows.Forms.ComboBox comboPeakScoringModel;
        private System.Windows.Forms.Label label36;
        private System.Windows.Forms.Button btnUpdateIonMobilityLibraries;
        private System.Windows.Forms.ContextMenuStrip contextMenuIonMobilityLibraries;
        private System.Windows.Forms.ToolStripMenuItem addIonMobilityLibraryContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editIonMobilityLibraryCurrentContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editIonMobilityLibraryListContextMenuItem;
        private System.Windows.Forms.ComboBox comboDriftTimePredictor;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.TextBox textSpectralLibraryDriftTimesResolvingPower;
        private System.Windows.Forms.CheckBox cbUseSpectralLibraryDriftTimes;
        private System.Windows.Forms.Label labelResolvingPower;
        private System.Windows.Forms.TabPage tabQuantification;
        private System.Windows.Forms.ComboBox comboWeighting;
        private System.Windows.Forms.Label lblRegressionWeighting;
        private System.Windows.Forms.ComboBox comboRegressionFit;
        private System.Windows.Forms.Label lblRegressionFit;
        private System.Windows.Forms.ComboBox comboNormalizationMethod;
        private System.Windows.Forms.Label lblNormalizationMethod;
        private System.Windows.Forms.ComboBox comboQuantMsLevel;
        private System.Windows.Forms.Label lblMsLevel;
        private System.Windows.Forms.TextBox tbxQuantUnits;
        private System.Windows.Forms.Label lblQuantUnits;
        private System.Windows.Forms.Label labelPeptideUniquenessConstraint;
        private System.Windows.Forms.ComboBox comboBoxPeptideUniquenessConstraint;
        private System.Windows.Forms.CheckBox cbLinear;
        private System.Windows.Forms.TextBox textSpectralLibraryDriftTimesWidthAtDtMax;
        private System.Windows.Forms.TextBox textSpectralLibraryDriftTimesWidthAtDt0;
        private System.Windows.Forms.Label labelWidthDtMax;
        private System.Windows.Forms.Label labelWidthDtZero;
        private System.Windows.Forms.Button btnFilter;
        private System.Windows.Forms.Label lblCaclulateLodBy;
        private System.Windows.Forms.Label lblMaxLoqCvPct;
        private System.Windows.Forms.TextBox tbxMaxLoqCv;
        private System.Windows.Forms.Label lblMaxLoqCv;
        private System.Windows.Forms.Label lblMaxLoqBiasPct;
        private System.Windows.Forms.TextBox tbxMaxLoqBias;
        private System.Windows.Forms.Label lblMaxLoqBias;
        private System.Windows.Forms.ComboBox comboLodMethod;
        private System.Windows.Forms.GroupBox groupBoxFiguresOfMerit;
        private System.Windows.Forms.TabPage tabLabels;
        private System.Windows.Forms.Button buttonEditListSmallMolInternalStandardTypes;
        private System.Windows.Forms.Label labelSmallMolInternalStandardTypes;
        private System.Windows.Forms.CheckedListBox listBoxSmallMolInternalStandardTypes;
    }
}