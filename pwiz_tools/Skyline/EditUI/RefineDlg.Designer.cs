namespace pwiz.Skyline.EditUI
{
    partial class RefineDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RefineDlg));
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.cbRemovePeptidesMissingLibrary = new System.Windows.Forms.CheckBox();
            this.cbAutoTransitions = new System.Windows.Forms.CheckBox();
            this.cbAutoPrecursors = new System.Windows.Forms.CheckBox();
            this.cbAutoPeptides = new System.Windows.Forms.CheckBox();
            this.cbAdd = new System.Windows.Forms.CheckBox();
            this.comboRefineLabelType = new System.Windows.Forms.ComboBox();
            this.cbRemoveRepeatedPeptides = new System.Windows.Forms.CheckBox();
            this.cbRemoveDuplicatePeptides = new System.Windows.Forms.CheckBox();
            this.textMinTransitions = new System.Windows.Forms.TextBox();
            this.textMinPeptides = new System.Windows.Forms.TextBox();
            this.textMaxPepPeakRank = new System.Windows.Forms.TextBox();
            this.comboReplicateUse = new System.Windows.Forms.ComboBox();
            this.cbPreferLarger = new System.Windows.Forms.CheckBox();
            this.textMaxPeakRank = new System.Windows.Forms.TextBox();
            this.textMaxPeakFoundRatio = new System.Windows.Forms.TextBox();
            this.radioRemoveMissing = new System.Windows.Forms.RadioButton();
            this.radioIgnoreMissing = new System.Windows.Forms.RadioButton();
            this.textMinPeakFoundRatio = new System.Windows.Forms.TextBox();
            this.textMinIdotProduct = new System.Windows.Forms.TextBox();
            this.textMinDotProduct = new System.Windows.Forms.TextBox();
            this.textRTRegressionThreshold = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabDocument = new System.Windows.Forms.TabPage();
            this.label7 = new System.Windows.Forms.Label();
            this.labelLabelType = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tabResults = new System.Windows.Forms.TabPage();
            this.cbMaxPrecursorOnly = new System.Windows.Forms.CheckBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.labelMaxPeakRank = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.groupLibCorr = new System.Windows.Forms.GroupBox();
            this.labelMinIdotProduct = new System.Windows.Forms.Label();
            this.labelMinDotProduct = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tabConsistency = new System.Windows.Forms.TabPage();
            this.groupPeakArea = new System.Windows.Forms.GroupBox();
            this.labelTransType = new System.Windows.Forms.Label();
            this.comboTransType = new System.Windows.Forms.ComboBox();
            this.labelTransitions = new System.Windows.Forms.Label();
            this.comboTransitions = new System.Windows.Forms.ComboBox();
            this.labelCV = new System.Windows.Forms.Label();
            this.labelPercent = new System.Windows.Forms.Label();
            this.textCVCutoff = new System.Windows.Forms.TextBox();
            this.comboNormalizeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalize = new System.Windows.Forms.Label();
            this.groupDetection = new System.Windows.Forms.GroupBox();
            this.numericUpDownDetections = new System.Windows.Forms.NumericUpDown();
            this.labelReplicates = new System.Windows.Forms.Label();
            this.labelDetections = new System.Windows.Forms.Label();
            this.labelQVal = new System.Windows.Forms.Label();
            this.textQVal = new System.Windows.Forms.TextBox();
            this.tabGroupComparisons = new System.Windows.Forms.TabPage();
            this.comboMSGroupComparisons = new System.Windows.Forms.ComboBox();
            this.labelFoldChangeUnit = new System.Windows.Forms.Label();
            this.labelPValueUnit = new System.Windows.Forms.Label();
            this.checkBoxLog = new System.Windows.Forms.CheckBox();
            this.textFoldChange = new System.Windows.Forms.TextBox();
            this.textPValue = new System.Windows.Forms.TextBox();
            this.labelFoldChange = new System.Windows.Forms.Label();
            this.labelPValue = new System.Windows.Forms.Label();
            this.labelMSLevel = new System.Windows.Forms.Label();
            this.labelGroupComparison = new System.Windows.Forms.Label();
            this.btnEditGroupComparisons = new System.Windows.Forms.Button();
            this.checkedListBoxGroupComparisons = new System.Windows.Forms.CheckedListBox();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabDocument.SuspendLayout();
            this.tabResults.SuspendLayout();
            this.groupLibCorr.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabConsistency.SuspendLayout();
            this.groupPeakArea.SuspendLayout();
            this.groupDetection.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownDetections)).BeginInit();
            this.tabGroupComparisons.SuspendLayout();
            this.SuspendLayout();
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 32767;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // cbRemovePeptidesMissingLibrary
            // 
            resources.ApplyResources(this.cbRemovePeptidesMissingLibrary, "cbRemovePeptidesMissingLibrary");
            this.cbRemovePeptidesMissingLibrary.Name = "cbRemovePeptidesMissingLibrary";
            this.helpTip.SetToolTip(this.cbRemovePeptidesMissingLibrary, resources.GetString("cbRemovePeptidesMissingLibrary.ToolTip"));
            this.cbRemovePeptidesMissingLibrary.UseVisualStyleBackColor = true;
            // 
            // cbAutoTransitions
            // 
            resources.ApplyResources(this.cbAutoTransitions, "cbAutoTransitions");
            this.cbAutoTransitions.Name = "cbAutoTransitions";
            this.helpTip.SetToolTip(this.cbAutoTransitions, resources.GetString("cbAutoTransitions.ToolTip"));
            this.cbAutoTransitions.UseVisualStyleBackColor = true;
            // 
            // cbAutoPrecursors
            // 
            resources.ApplyResources(this.cbAutoPrecursors, "cbAutoPrecursors");
            this.cbAutoPrecursors.Name = "cbAutoPrecursors";
            this.helpTip.SetToolTip(this.cbAutoPrecursors, resources.GetString("cbAutoPrecursors.ToolTip"));
            this.cbAutoPrecursors.UseVisualStyleBackColor = true;
            // 
            // cbAutoPeptides
            // 
            resources.ApplyResources(this.cbAutoPeptides, "cbAutoPeptides");
            this.cbAutoPeptides.Name = "cbAutoPeptides";
            this.helpTip.SetToolTip(this.cbAutoPeptides, resources.GetString("cbAutoPeptides.ToolTip"));
            this.cbAutoPeptides.UseVisualStyleBackColor = true;
            // 
            // cbAdd
            // 
            resources.ApplyResources(this.cbAdd, "cbAdd");
            this.cbAdd.Name = "cbAdd";
            this.helpTip.SetToolTip(this.cbAdd, resources.GetString("cbAdd.ToolTip"));
            this.cbAdd.UseVisualStyleBackColor = true;
            this.cbAdd.CheckedChanged += new System.EventHandler(this.cbAdd_CheckedChanged);
            // 
            // comboRefineLabelType
            // 
            this.comboRefineLabelType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRefineLabelType.FormattingEnabled = true;
            resources.ApplyResources(this.comboRefineLabelType, "comboRefineLabelType");
            this.comboRefineLabelType.Name = "comboRefineLabelType";
            this.helpTip.SetToolTip(this.comboRefineLabelType, resources.GetString("comboRefineLabelType.ToolTip"));
            // 
            // cbRemoveRepeatedPeptides
            // 
            resources.ApplyResources(this.cbRemoveRepeatedPeptides, "cbRemoveRepeatedPeptides");
            this.cbRemoveRepeatedPeptides.Name = "cbRemoveRepeatedPeptides";
            this.helpTip.SetToolTip(this.cbRemoveRepeatedPeptides, resources.GetString("cbRemoveRepeatedPeptides.ToolTip"));
            this.cbRemoveRepeatedPeptides.UseVisualStyleBackColor = true;
            // 
            // cbRemoveDuplicatePeptides
            // 
            resources.ApplyResources(this.cbRemoveDuplicatePeptides, "cbRemoveDuplicatePeptides");
            this.cbRemoveDuplicatePeptides.Name = "cbRemoveDuplicatePeptides";
            this.helpTip.SetToolTip(this.cbRemoveDuplicatePeptides, resources.GetString("cbRemoveDuplicatePeptides.ToolTip"));
            this.cbRemoveDuplicatePeptides.UseVisualStyleBackColor = true;
            // 
            // textMinTransitions
            // 
            resources.ApplyResources(this.textMinTransitions, "textMinTransitions");
            this.textMinTransitions.Name = "textMinTransitions";
            this.helpTip.SetToolTip(this.textMinTransitions, resources.GetString("textMinTransitions.ToolTip"));
            // 
            // textMinPeptides
            // 
            resources.ApplyResources(this.textMinPeptides, "textMinPeptides");
            this.textMinPeptides.Name = "textMinPeptides";
            this.helpTip.SetToolTip(this.textMinPeptides, resources.GetString("textMinPeptides.ToolTip"));
            // 
            // textMaxPepPeakRank
            // 
            resources.ApplyResources(this.textMaxPepPeakRank, "textMaxPepPeakRank");
            this.textMaxPepPeakRank.Name = "textMaxPepPeakRank";
            this.helpTip.SetToolTip(this.textMaxPepPeakRank, resources.GetString("textMaxPepPeakRank.ToolTip"));
            // 
            // comboReplicateUse
            // 
            this.comboReplicateUse.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboReplicateUse.FormattingEnabled = true;
            this.comboReplicateUse.Items.AddRange(new object[] {
            resources.GetString("comboReplicateUse.Items"),
            resources.GetString("comboReplicateUse.Items1")});
            resources.ApplyResources(this.comboReplicateUse, "comboReplicateUse");
            this.comboReplicateUse.Name = "comboReplicateUse";
            this.helpTip.SetToolTip(this.comboReplicateUse, resources.GetString("comboReplicateUse.ToolTip"));
            // 
            // cbPreferLarger
            // 
            resources.ApplyResources(this.cbPreferLarger, "cbPreferLarger");
            this.cbPreferLarger.Name = "cbPreferLarger";
            this.helpTip.SetToolTip(this.cbPreferLarger, resources.GetString("cbPreferLarger.ToolTip"));
            this.cbPreferLarger.UseVisualStyleBackColor = true;
            // 
            // textMaxPeakRank
            // 
            resources.ApplyResources(this.textMaxPeakRank, "textMaxPeakRank");
            this.textMaxPeakRank.Name = "textMaxPeakRank";
            this.helpTip.SetToolTip(this.textMaxPeakRank, resources.GetString("textMaxPeakRank.ToolTip"));
            this.textMaxPeakRank.TextChanged += new System.EventHandler(this.textMaxPeakRank_TextChanged);
            // 
            // textMaxPeakFoundRatio
            // 
            resources.ApplyResources(this.textMaxPeakFoundRatio, "textMaxPeakFoundRatio");
            this.textMaxPeakFoundRatio.Name = "textMaxPeakFoundRatio";
            this.helpTip.SetToolTip(this.textMaxPeakFoundRatio, resources.GetString("textMaxPeakFoundRatio.ToolTip"));
            // 
            // radioRemoveMissing
            // 
            resources.ApplyResources(this.radioRemoveMissing, "radioRemoveMissing");
            this.radioRemoveMissing.Name = "radioRemoveMissing";
            this.radioRemoveMissing.TabStop = true;
            this.helpTip.SetToolTip(this.radioRemoveMissing, resources.GetString("radioRemoveMissing.ToolTip"));
            this.radioRemoveMissing.UseVisualStyleBackColor = true;
            // 
            // radioIgnoreMissing
            // 
            resources.ApplyResources(this.radioIgnoreMissing, "radioIgnoreMissing");
            this.radioIgnoreMissing.Checked = true;
            this.radioIgnoreMissing.Name = "radioIgnoreMissing";
            this.radioIgnoreMissing.TabStop = true;
            this.helpTip.SetToolTip(this.radioIgnoreMissing, resources.GetString("radioIgnoreMissing.ToolTip"));
            this.radioIgnoreMissing.UseVisualStyleBackColor = true;
            // 
            // textMinPeakFoundRatio
            // 
            resources.ApplyResources(this.textMinPeakFoundRatio, "textMinPeakFoundRatio");
            this.textMinPeakFoundRatio.Name = "textMinPeakFoundRatio";
            this.helpTip.SetToolTip(this.textMinPeakFoundRatio, resources.GetString("textMinPeakFoundRatio.ToolTip"));
            // 
            // textMinIdotProduct
            // 
            resources.ApplyResources(this.textMinIdotProduct, "textMinIdotProduct");
            this.textMinIdotProduct.Name = "textMinIdotProduct";
            this.helpTip.SetToolTip(this.textMinIdotProduct, resources.GetString("textMinIdotProduct.ToolTip"));
            // 
            // textMinDotProduct
            // 
            resources.ApplyResources(this.textMinDotProduct, "textMinDotProduct");
            this.textMinDotProduct.Name = "textMinDotProduct";
            this.helpTip.SetToolTip(this.textMinDotProduct, resources.GetString("textMinDotProduct.ToolTip"));
            // 
            // textRTRegressionThreshold
            // 
            resources.ApplyResources(this.textRTRegressionThreshold, "textRTRegressionThreshold");
            this.textRTRegressionThreshold.Name = "textRTRegressionThreshold";
            this.helpTip.SetToolTip(this.textRTRegressionThreshold, resources.GetString("textRTRegressionThreshold.ToolTip"));
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Controls.Add(this.tabDocument);
            this.tabControl1.Controls.Add(this.tabResults);
            this.tabControl1.Controls.Add(this.tabConsistency);
            this.tabControl1.Controls.Add(this.tabGroupComparisons);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            // 
            // tabDocument
            // 
            this.tabDocument.Controls.Add(this.cbRemovePeptidesMissingLibrary);
            this.tabDocument.Controls.Add(this.cbAutoTransitions);
            this.tabDocument.Controls.Add(this.cbAutoPrecursors);
            this.tabDocument.Controls.Add(this.cbAutoPeptides);
            this.tabDocument.Controls.Add(this.label7);
            this.tabDocument.Controls.Add(this.cbAdd);
            this.tabDocument.Controls.Add(this.labelLabelType);
            this.tabDocument.Controls.Add(this.comboRefineLabelType);
            this.tabDocument.Controls.Add(this.cbRemoveRepeatedPeptides);
            this.tabDocument.Controls.Add(this.cbRemoveDuplicatePeptides);
            this.tabDocument.Controls.Add(this.textMinTransitions);
            this.tabDocument.Controls.Add(this.label2);
            this.tabDocument.Controls.Add(this.textMinPeptides);
            this.tabDocument.Controls.Add(this.label1);
            resources.ApplyResources(this.tabDocument, "tabDocument");
            this.tabDocument.Name = "tabDocument";
            this.tabDocument.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // labelLabelType
            // 
            resources.ApplyResources(this.labelLabelType, "labelLabelType");
            this.labelLabelType.Name = "labelLabelType";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // tabResults
            // 
            this.tabResults.Controls.Add(this.cbMaxPrecursorOnly);
            this.tabResults.Controls.Add(this.textMaxPepPeakRank);
            this.tabResults.Controls.Add(this.label8);
            this.tabResults.Controls.Add(this.label4);
            this.tabResults.Controls.Add(this.comboReplicateUse);
            this.tabResults.Controls.Add(this.cbPreferLarger);
            this.tabResults.Controls.Add(this.textMaxPeakRank);
            this.tabResults.Controls.Add(this.labelMaxPeakRank);
            this.tabResults.Controls.Add(this.textMaxPeakFoundRatio);
            this.tabResults.Controls.Add(this.label6);
            this.tabResults.Controls.Add(this.radioRemoveMissing);
            this.tabResults.Controls.Add(this.radioIgnoreMissing);
            this.tabResults.Controls.Add(this.textMinPeakFoundRatio);
            this.tabResults.Controls.Add(this.label5);
            this.tabResults.Controls.Add(this.groupLibCorr);
            this.tabResults.Controls.Add(this.groupBox1);
            resources.ApplyResources(this.tabResults, "tabResults");
            this.tabResults.Name = "tabResults";
            this.tabResults.UseVisualStyleBackColor = true;
            // 
            // cbMaxPrecursorOnly
            // 
            resources.ApplyResources(this.cbMaxPrecursorOnly, "cbMaxPrecursorOnly");
            this.cbMaxPrecursorOnly.Name = "cbMaxPrecursorOnly";
            this.cbMaxPrecursorOnly.UseVisualStyleBackColor = true;
            // 
            // label8
            // 
            resources.ApplyResources(this.label8, "label8");
            this.label8.Name = "label8";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // labelMaxPeakRank
            // 
            resources.ApplyResources(this.labelMaxPeakRank, "labelMaxPeakRank");
            this.labelMaxPeakRank.Name = "labelMaxPeakRank";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // groupLibCorr
            // 
            this.groupLibCorr.Controls.Add(this.textMinIdotProduct);
            this.groupLibCorr.Controls.Add(this.labelMinIdotProduct);
            this.groupLibCorr.Controls.Add(this.textMinDotProduct);
            this.groupLibCorr.Controls.Add(this.labelMinDotProduct);
            resources.ApplyResources(this.groupLibCorr, "groupLibCorr");
            this.groupLibCorr.Name = "groupLibCorr";
            this.groupLibCorr.TabStop = false;
            // 
            // labelMinIdotProduct
            // 
            resources.ApplyResources(this.labelMinIdotProduct, "labelMinIdotProduct");
            this.labelMinIdotProduct.Name = "labelMinIdotProduct";
            // 
            // labelMinDotProduct
            // 
            resources.ApplyResources(this.labelMinDotProduct, "labelMinDotProduct");
            this.labelMinDotProduct.Name = "labelMinDotProduct";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textRTRegressionThreshold);
            this.groupBox1.Controls.Add(this.label3);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // tabConsistency
            // 
            this.tabConsistency.Controls.Add(this.groupPeakArea);
            this.tabConsistency.Controls.Add(this.groupDetection);
            resources.ApplyResources(this.tabConsistency, "tabConsistency");
            this.tabConsistency.Name = "tabConsistency";
            this.tabConsistency.UseVisualStyleBackColor = true;
            // 
            // groupPeakArea
            // 
            this.groupPeakArea.Controls.Add(this.labelTransType);
            this.groupPeakArea.Controls.Add(this.comboTransType);
            this.groupPeakArea.Controls.Add(this.labelTransitions);
            this.groupPeakArea.Controls.Add(this.comboTransitions);
            this.groupPeakArea.Controls.Add(this.labelCV);
            this.groupPeakArea.Controls.Add(this.labelPercent);
            this.groupPeakArea.Controls.Add(this.textCVCutoff);
            this.groupPeakArea.Controls.Add(this.comboNormalizeTo);
            this.groupPeakArea.Controls.Add(this.labelNormalize);
            resources.ApplyResources(this.groupPeakArea, "groupPeakArea");
            this.groupPeakArea.Name = "groupPeakArea";
            this.groupPeakArea.TabStop = false;
            // 
            // labelTransType
            // 
            resources.ApplyResources(this.labelTransType, "labelTransType");
            this.labelTransType.Name = "labelTransType";
            // 
            // comboTransType
            // 
            this.comboTransType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTransType.FormattingEnabled = true;
            resources.ApplyResources(this.comboTransType, "comboTransType");
            this.comboTransType.Name = "comboTransType";
            // 
            // labelTransitions
            // 
            resources.ApplyResources(this.labelTransitions, "labelTransitions");
            this.labelTransitions.Name = "labelTransitions";
            // 
            // comboTransitions
            // 
            this.comboTransitions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTransitions.FormattingEnabled = true;
            resources.ApplyResources(this.comboTransitions, "comboTransitions");
            this.comboTransitions.Name = "comboTransitions";
            // 
            // labelCV
            // 
            resources.ApplyResources(this.labelCV, "labelCV");
            this.labelCV.Name = "labelCV";
            // 
            // labelPercent
            // 
            resources.ApplyResources(this.labelPercent, "labelPercent");
            this.labelPercent.Name = "labelPercent";
            // 
            // textCVCutoff
            // 
            resources.ApplyResources(this.textCVCutoff, "textCVCutoff");
            this.textCVCutoff.Name = "textCVCutoff";
            // 
            // comboNormalizeTo
            // 
            this.comboNormalizeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboNormalizeTo.FormattingEnabled = true;
            resources.ApplyResources(this.comboNormalizeTo, "comboNormalizeTo");
            this.comboNormalizeTo.Name = "comboNormalizeTo";
            // 
            // labelNormalize
            // 
            resources.ApplyResources(this.labelNormalize, "labelNormalize");
            this.labelNormalize.Name = "labelNormalize";
            // 
            // groupDetection
            // 
            this.groupDetection.Controls.Add(this.numericUpDownDetections);
            this.groupDetection.Controls.Add(this.labelReplicates);
            this.groupDetection.Controls.Add(this.labelDetections);
            this.groupDetection.Controls.Add(this.labelQVal);
            this.groupDetection.Controls.Add(this.textQVal);
            resources.ApplyResources(this.groupDetection, "groupDetection");
            this.groupDetection.Name = "groupDetection";
            this.groupDetection.TabStop = false;
            // 
            // numericUpDownDetections
            // 
            resources.ApplyResources(this.numericUpDownDetections, "numericUpDownDetections");
            this.numericUpDownDetections.Name = "numericUpDownDetections";
            // 
            // labelReplicates
            // 
            resources.ApplyResources(this.labelReplicates, "labelReplicates");
            this.labelReplicates.Name = "labelReplicates";
            // 
            // labelDetections
            // 
            resources.ApplyResources(this.labelDetections, "labelDetections");
            this.labelDetections.Name = "labelDetections";
            // 
            // labelQVal
            // 
            resources.ApplyResources(this.labelQVal, "labelQVal");
            this.labelQVal.Name = "labelQVal";
            // 
            // textQVal
            // 
            resources.ApplyResources(this.textQVal, "textQVal");
            this.textQVal.Name = "textQVal";
            // 
            // tabGroupComparisons
            // 
            this.tabGroupComparisons.Controls.Add(this.comboMSGroupComparisons);
            this.tabGroupComparisons.Controls.Add(this.labelFoldChangeUnit);
            this.tabGroupComparisons.Controls.Add(this.labelPValueUnit);
            this.tabGroupComparisons.Controls.Add(this.checkBoxLog);
            this.tabGroupComparisons.Controls.Add(this.textFoldChange);
            this.tabGroupComparisons.Controls.Add(this.textPValue);
            this.tabGroupComparisons.Controls.Add(this.labelFoldChange);
            this.tabGroupComparisons.Controls.Add(this.labelPValue);
            this.tabGroupComparisons.Controls.Add(this.labelMSLevel);
            this.tabGroupComparisons.Controls.Add(this.labelGroupComparison);
            this.tabGroupComparisons.Controls.Add(this.btnEditGroupComparisons);
            this.tabGroupComparisons.Controls.Add(this.checkedListBoxGroupComparisons);
            resources.ApplyResources(this.tabGroupComparisons, "tabGroupComparisons");
            this.tabGroupComparisons.Name = "tabGroupComparisons";
            this.tabGroupComparisons.UseVisualStyleBackColor = true;
            // 
            // comboMSGroupComparisons
            // 
            this.comboMSGroupComparisons.FormattingEnabled = true;
            resources.ApplyResources(this.comboMSGroupComparisons, "comboMSGroupComparisons");
            this.comboMSGroupComparisons.Name = "comboMSGroupComparisons";
            // 
            // labelFoldChangeUnit
            // 
            resources.ApplyResources(this.labelFoldChangeUnit, "labelFoldChangeUnit");
            this.labelFoldChangeUnit.Name = "labelFoldChangeUnit";
            // 
            // labelPValueUnit
            // 
            resources.ApplyResources(this.labelPValueUnit, "labelPValueUnit");
            this.labelPValueUnit.Name = "labelPValueUnit";
            // 
            // checkBoxLog
            // 
            resources.ApplyResources(this.checkBoxLog, "checkBoxLog");
            this.checkBoxLog.Checked = true;
            this.checkBoxLog.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxLog.Name = "checkBoxLog";
            this.checkBoxLog.UseVisualStyleBackColor = true;
            this.checkBoxLog.CheckedChanged += new System.EventHandler(this.checkBoxLog_CheckedChanged);
            // 
            // textFoldChange
            // 
            resources.ApplyResources(this.textFoldChange, "textFoldChange");
            this.textFoldChange.Name = "textFoldChange";
            // 
            // textPValue
            // 
            resources.ApplyResources(this.textPValue, "textPValue");
            this.textPValue.Name = "textPValue";
            // 
            // labelFoldChange
            // 
            resources.ApplyResources(this.labelFoldChange, "labelFoldChange");
            this.labelFoldChange.Name = "labelFoldChange";
            // 
            // labelPValue
            // 
            resources.ApplyResources(this.labelPValue, "labelPValue");
            this.labelPValue.Name = "labelPValue";
            // 
            // labelMSLevel
            // 
            resources.ApplyResources(this.labelMSLevel, "labelMSLevel");
            this.labelMSLevel.Name = "labelMSLevel";
            // 
            // labelGroupComparison
            // 
            resources.ApplyResources(this.labelGroupComparison, "labelGroupComparison");
            this.labelGroupComparison.Name = "labelGroupComparison";
            // 
            // btnEditGroupComparisons
            // 
            resources.ApplyResources(this.btnEditGroupComparisons, "btnEditGroupComparisons");
            this.btnEditGroupComparisons.Name = "btnEditGroupComparisons";
            this.btnEditGroupComparisons.UseVisualStyleBackColor = true;
            this.btnEditGroupComparisons.Click += new System.EventHandler(this.btnEditGroupComparisons_Click);
            // 
            // checkedListBoxGroupComparisons
            // 
            this.checkedListBoxGroupComparisons.CheckOnClick = true;
            this.checkedListBoxGroupComparisons.FormattingEnabled = true;
            resources.ApplyResources(this.checkedListBoxGroupComparisons, "checkedListBoxGroupComparisons");
            this.checkedListBoxGroupComparisons.Name = "checkedListBoxGroupComparisons";
            // 
            // RefineDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RefineDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabDocument.ResumeLayout(false);
            this.tabDocument.PerformLayout();
            this.tabResults.ResumeLayout(false);
            this.tabResults.PerformLayout();
            this.groupLibCorr.ResumeLayout(false);
            this.groupLibCorr.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.tabConsistency.ResumeLayout(false);
            this.groupPeakArea.ResumeLayout(false);
            this.groupPeakArea.PerformLayout();
            this.groupDetection.ResumeLayout(false);
            this.groupDetection.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownDetections)).EndInit();
            this.tabGroupComparisons.ResumeLayout(false);
            this.tabGroupComparisons.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabDocument;
        private System.Windows.Forms.TabPage tabResults;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textMinTransitions;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textMinPeptides;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox cbRemoveRepeatedPeptides;
        private System.Windows.Forms.CheckBox cbRemoveDuplicatePeptides;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupLibCorr;
        private System.Windows.Forms.Label labelMinDotProduct;
        private System.Windows.Forms.TextBox textRTRegressionThreshold;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textMinDotProduct;
        private System.Windows.Forms.RadioButton radioRemoveMissing;
        private System.Windows.Forms.RadioButton radioIgnoreMissing;
        private System.Windows.Forms.TextBox textMinPeakFoundRatio;
        private System.Windows.Forms.TextBox textMaxPeakFoundRatio;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textMaxPeakRank;
        private System.Windows.Forms.Label labelMaxPeakRank;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.Label labelLabelType;
        private System.Windows.Forms.ComboBox comboRefineLabelType;
        private System.Windows.Forms.CheckBox cbAdd;
        private System.Windows.Forms.CheckBox cbPreferLarger;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboReplicateUse;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.CheckBox cbAutoTransitions;
        private System.Windows.Forms.CheckBox cbAutoPrecursors;
        private System.Windows.Forms.CheckBox cbAutoPeptides;
        private System.Windows.Forms.TextBox textMaxPepPeakRank;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox textMinIdotProduct;
        private System.Windows.Forms.Label labelMinIdotProduct;
        private System.Windows.Forms.CheckBox cbRemovePeptidesMissingLibrary;
        private System.Windows.Forms.CheckBox cbMaxPrecursorOnly;
        private System.Windows.Forms.TabPage tabConsistency;
        private System.Windows.Forms.ComboBox comboNormalizeTo;
        private System.Windows.Forms.GroupBox groupDetection;
        private System.Windows.Forms.Label labelReplicates;
        private System.Windows.Forms.Label labelDetections;
        private System.Windows.Forms.Label labelQVal;
        private System.Windows.Forms.TextBox textQVal;
        private System.Windows.Forms.Label labelNormalize;
        private System.Windows.Forms.Label labelCV;
        private System.Windows.Forms.TextBox textCVCutoff;
        private System.Windows.Forms.Label labelPercent;
        private System.Windows.Forms.NumericUpDown numericUpDownDetections;
        private System.Windows.Forms.GroupBox groupPeakArea;
        private System.Windows.Forms.Label labelTransitions;
        private System.Windows.Forms.ComboBox comboTransitions;
        private System.Windows.Forms.Label labelTransType;
        private System.Windows.Forms.ComboBox comboTransType;
        private System.Windows.Forms.TabPage tabGroupComparisons;
        private System.Windows.Forms.CheckedListBox checkedListBoxGroupComparisons;
        private System.Windows.Forms.Button btnEditGroupComparisons;
        private System.Windows.Forms.CheckBox checkBoxLog;
        private System.Windows.Forms.TextBox textFoldChange;
        private System.Windows.Forms.TextBox textPValue;
        private System.Windows.Forms.Label labelFoldChange;
        private System.Windows.Forms.Label labelPValue;
        private System.Windows.Forms.Label labelMSLevel;
        private System.Windows.Forms.Label labelGroupComparison;
        private System.Windows.Forms.Label labelFoldChangeUnit;
        private System.Windows.Forms.Label labelPValueUnit;
        private System.Windows.Forms.ComboBox comboMSGroupComparisons;
    }
}