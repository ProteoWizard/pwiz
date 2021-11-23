namespace pwiz.Skyline.SettingsUI
{
    partial class FullScanSettingsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FullScanSettingsControl));
            this.groupBoxRetentionTimeToKeep = new System.Windows.Forms.GroupBox();
            this.flowLayoutPanelUseSchedulingWindow = new System.Windows.Forms.FlowLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxTimeAroundPrediction = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.radioTimeAroundMs2Ids = new System.Windows.Forms.RadioButton();
            this.flowLayoutPanelTimeAroundMs2Ids = new System.Windows.Forms.FlowLayoutPanel();
            this.labelTimeAroundMs2Ids1 = new System.Windows.Forms.Label();
            this.tbxTimeAroundMs2Ids = new System.Windows.Forms.TextBox();
            this.labelTimeAroundMs2Ids2 = new System.Windows.Forms.Label();
            this.radioUseSchedulingWindow = new System.Windows.Forms.RadioButton();
            this.radioKeepAllTime = new System.Windows.Forms.RadioButton();
            this.groupBoxMS1 = new System.Windows.Forms.GroupBox();
            this.labelPrecursorPPM = new System.Windows.Forms.Label();
            this.comboEnrichments = new System.Windows.Forms.ComboBox();
            this.labelEnrichments = new System.Windows.Forms.Label();
            this.labelPrecursorIsotopeFilterPercent = new System.Windows.Forms.Label();
            this.textPrecursorIsotopeFilter = new System.Windows.Forms.TextBox();
            this.labelPrecursorIsotopeFilter = new System.Windows.Forms.Label();
            this.label23 = new System.Windows.Forms.Label();
            this.comboPrecursorIsotopes = new System.Windows.Forms.ComboBox();
            this.labelPrecursorAt = new System.Windows.Forms.Label();
            this.textPrecursorAt = new System.Windows.Forms.TextBox();
            this.labelPrecursorTh = new System.Windows.Forms.Label();
            this.textPrecursorRes = new System.Windows.Forms.TextBox();
            this.labelPrecursorRes = new System.Windows.Forms.Label();
            this.comboPrecursorAnalyzerType = new System.Windows.Forms.ComboBox();
            this.label32 = new System.Windows.Forms.Label();
            this.groupBoxMS2 = new System.Windows.Forms.GroupBox();
            this.labelProductPPM = new System.Windows.Forms.Label();
            this.comboIsolationScheme = new System.Windows.Forms.ComboBox();
            this.labelProductAt = new System.Windows.Forms.Label();
            this.textProductAt = new System.Windows.Forms.TextBox();
            this.labelProductTh = new System.Windows.Forms.Label();
            this.textProductRes = new System.Windows.Forms.TextBox();
            this.labelProductRes = new System.Windows.Forms.Label();
            this.comboProductAnalyzerType = new System.Windows.Forms.ComboBox();
            this.label22 = new System.Windows.Forms.Label();
            this.labelIsolationScheme = new System.Windows.Forms.Label();
            this.comboAcquisitionMethod = new System.Windows.Forms.ComboBox();
            this.label20 = new System.Windows.Forms.Label();
            this.lblPrecursorCharges = new System.Windows.Forms.Label();
            this.textPrecursorCharges = new System.Windows.Forms.TextBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.cbHighSelectivity = new System.Windows.Forms.CheckBox();
            this.usercontrolIonMobilityFiltering = new pwiz.Skyline.SettingsUI.IonMobility.IonMobilityFilteringUserControl();
            this.groupBoxRetentionTimeToKeep.SuspendLayout();
            this.flowLayoutPanelUseSchedulingWindow.SuspendLayout();
            this.flowLayoutPanelTimeAroundMs2Ids.SuspendLayout();
            this.groupBoxMS1.SuspendLayout();
            this.groupBoxMS2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxRetentionTimeToKeep
            // 
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.flowLayoutPanelUseSchedulingWindow);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.radioTimeAroundMs2Ids);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.flowLayoutPanelTimeAroundMs2Ids);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.radioUseSchedulingWindow);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.radioKeepAllTime);
            resources.ApplyResources(this.groupBoxRetentionTimeToKeep, "groupBoxRetentionTimeToKeep");
            this.groupBoxRetentionTimeToKeep.Name = "groupBoxRetentionTimeToKeep";
            this.groupBoxRetentionTimeToKeep.TabStop = false;
            // 
            // flowLayoutPanelUseSchedulingWindow
            // 
            resources.ApplyResources(this.flowLayoutPanelUseSchedulingWindow, "flowLayoutPanelUseSchedulingWindow");
            this.flowLayoutPanelUseSchedulingWindow.Controls.Add(this.label1);
            this.flowLayoutPanelUseSchedulingWindow.Controls.Add(this.tbxTimeAroundPrediction);
            this.flowLayoutPanelUseSchedulingWindow.Controls.Add(this.label2);
            this.flowLayoutPanelUseSchedulingWindow.Name = "flowLayoutPanelUseSchedulingWindow";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // tbxTimeAroundPrediction
            // 
            resources.ApplyResources(this.tbxTimeAroundPrediction, "tbxTimeAroundPrediction");
            this.tbxTimeAroundPrediction.Name = "tbxTimeAroundPrediction";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // radioTimeAroundMs2Ids
            // 
            resources.ApplyResources(this.radioTimeAroundMs2Ids, "radioTimeAroundMs2Ids");
            this.radioTimeAroundMs2Ids.Name = "radioTimeAroundMs2Ids";
            this.radioTimeAroundMs2Ids.TabStop = true;
            this.radioTimeAroundMs2Ids.UseVisualStyleBackColor = true;
            this.radioTimeAroundMs2Ids.CheckedChanged += new System.EventHandler(this.radioTimeAroundMs2Ids_CheckedChanged);
            // 
            // flowLayoutPanelTimeAroundMs2Ids
            // 
            resources.ApplyResources(this.flowLayoutPanelTimeAroundMs2Ids, "flowLayoutPanelTimeAroundMs2Ids");
            this.flowLayoutPanelTimeAroundMs2Ids.Controls.Add(this.labelTimeAroundMs2Ids1);
            this.flowLayoutPanelTimeAroundMs2Ids.Controls.Add(this.tbxTimeAroundMs2Ids);
            this.flowLayoutPanelTimeAroundMs2Ids.Controls.Add(this.labelTimeAroundMs2Ids2);
            this.flowLayoutPanelTimeAroundMs2Ids.Name = "flowLayoutPanelTimeAroundMs2Ids";
            // 
            // labelTimeAroundMs2Ids1
            // 
            resources.ApplyResources(this.labelTimeAroundMs2Ids1, "labelTimeAroundMs2Ids1");
            this.labelTimeAroundMs2Ids1.Name = "labelTimeAroundMs2Ids1";
            // 
            // tbxTimeAroundMs2Ids
            // 
            resources.ApplyResources(this.tbxTimeAroundMs2Ids, "tbxTimeAroundMs2Ids");
            this.tbxTimeAroundMs2Ids.Name = "tbxTimeAroundMs2Ids";
            // 
            // labelTimeAroundMs2Ids2
            // 
            resources.ApplyResources(this.labelTimeAroundMs2Ids2, "labelTimeAroundMs2Ids2");
            this.labelTimeAroundMs2Ids2.Name = "labelTimeAroundMs2Ids2";
            // 
            // radioUseSchedulingWindow
            // 
            resources.ApplyResources(this.radioUseSchedulingWindow, "radioUseSchedulingWindow");
            this.radioUseSchedulingWindow.Name = "radioUseSchedulingWindow";
            this.radioUseSchedulingWindow.TabStop = true;
            this.radioUseSchedulingWindow.UseVisualStyleBackColor = true;
            this.radioUseSchedulingWindow.CheckedChanged += new System.EventHandler(this.radioUseSchedulingWindow_CheckedChanged);
            // 
            // radioKeepAllTime
            // 
            resources.ApplyResources(this.radioKeepAllTime, "radioKeepAllTime");
            this.radioKeepAllTime.Name = "radioKeepAllTime";
            this.radioKeepAllTime.TabStop = true;
            this.radioKeepAllTime.UseVisualStyleBackColor = true;
            this.radioKeepAllTime.CheckedChanged += new System.EventHandler(this.radioKeepAllTime_CheckedChanged);
            // 
            // groupBoxMS1
            // 
            this.groupBoxMS1.Controls.Add(this.labelPrecursorPPM);
            this.groupBoxMS1.Controls.Add(this.comboEnrichments);
            this.groupBoxMS1.Controls.Add(this.labelEnrichments);
            this.groupBoxMS1.Controls.Add(this.labelPrecursorIsotopeFilterPercent);
            this.groupBoxMS1.Controls.Add(this.textPrecursorIsotopeFilter);
            this.groupBoxMS1.Controls.Add(this.labelPrecursorIsotopeFilter);
            this.groupBoxMS1.Controls.Add(this.label23);
            this.groupBoxMS1.Controls.Add(this.comboPrecursorIsotopes);
            this.groupBoxMS1.Controls.Add(this.labelPrecursorAt);
            this.groupBoxMS1.Controls.Add(this.textPrecursorAt);
            this.groupBoxMS1.Controls.Add(this.labelPrecursorTh);
            this.groupBoxMS1.Controls.Add(this.textPrecursorRes);
            this.groupBoxMS1.Controls.Add(this.labelPrecursorRes);
            this.groupBoxMS1.Controls.Add(this.comboPrecursorAnalyzerType);
            this.groupBoxMS1.Controls.Add(this.label32);
            resources.ApplyResources(this.groupBoxMS1, "groupBoxMS1");
            this.groupBoxMS1.Name = "groupBoxMS1";
            this.groupBoxMS1.TabStop = false;
            // 
            // labelPrecursorPPM
            // 
            resources.ApplyResources(this.labelPrecursorPPM, "labelPrecursorPPM");
            this.labelPrecursorPPM.Name = "labelPrecursorPPM";
            // 
            // comboEnrichments
            // 
            this.comboEnrichments.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEnrichments.FormattingEnabled = true;
            resources.ApplyResources(this.comboEnrichments, "comboEnrichments");
            this.comboEnrichments.Name = "comboEnrichments";
            this.comboEnrichments.SelectedIndexChanged += new System.EventHandler(this.comboEnrichments_SelectedIndexChanged);
            // 
            // labelEnrichments
            // 
            resources.ApplyResources(this.labelEnrichments, "labelEnrichments");
            this.labelEnrichments.Name = "labelEnrichments";
            // 
            // labelPrecursorIsotopeFilterPercent
            // 
            resources.ApplyResources(this.labelPrecursorIsotopeFilterPercent, "labelPrecursorIsotopeFilterPercent");
            this.labelPrecursorIsotopeFilterPercent.Name = "labelPrecursorIsotopeFilterPercent";
            // 
            // textPrecursorIsotopeFilter
            // 
            resources.ApplyResources(this.textPrecursorIsotopeFilter, "textPrecursorIsotopeFilter");
            this.textPrecursorIsotopeFilter.Name = "textPrecursorIsotopeFilter";
            // 
            // labelPrecursorIsotopeFilter
            // 
            resources.ApplyResources(this.labelPrecursorIsotopeFilter, "labelPrecursorIsotopeFilter");
            this.labelPrecursorIsotopeFilter.Name = "labelPrecursorIsotopeFilter";
            // 
            // label23
            // 
            resources.ApplyResources(this.label23, "label23");
            this.label23.Name = "label23";
            // 
            // comboPrecursorIsotopes
            // 
            this.comboPrecursorIsotopes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboPrecursorIsotopes.FormattingEnabled = true;
            resources.ApplyResources(this.comboPrecursorIsotopes, "comboPrecursorIsotopes");
            this.comboPrecursorIsotopes.Name = "comboPrecursorIsotopes";
            this.comboPrecursorIsotopes.SelectedIndexChanged += new System.EventHandler(this.comboPrecursorIsotopes_SelectedIndexChanged);
            // 
            // labelPrecursorAt
            // 
            resources.ApplyResources(this.labelPrecursorAt, "labelPrecursorAt");
            this.labelPrecursorAt.Name = "labelPrecursorAt";
            // 
            // textPrecursorAt
            // 
            resources.ApplyResources(this.textPrecursorAt, "textPrecursorAt");
            this.textPrecursorAt.Name = "textPrecursorAt";
            // 
            // labelPrecursorTh
            // 
            resources.ApplyResources(this.labelPrecursorTh, "labelPrecursorTh");
            this.labelPrecursorTh.Name = "labelPrecursorTh";
            // 
            // textPrecursorRes
            // 
            resources.ApplyResources(this.textPrecursorRes, "textPrecursorRes");
            this.textPrecursorRes.Name = "textPrecursorRes";
            // 
            // labelPrecursorRes
            // 
            resources.ApplyResources(this.labelPrecursorRes, "labelPrecursorRes");
            this.labelPrecursorRes.Name = "labelPrecursorRes";
            // 
            // comboPrecursorAnalyzerType
            // 
            this.comboPrecursorAnalyzerType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboPrecursorAnalyzerType, "comboPrecursorAnalyzerType");
            this.comboPrecursorAnalyzerType.FormattingEnabled = true;
            this.comboPrecursorAnalyzerType.Name = "comboPrecursorAnalyzerType";
            this.toolTip.SetToolTip(this.comboPrecursorAnalyzerType, resources.GetString("comboPrecursorAnalyzerType.ToolTip"));
            this.comboPrecursorAnalyzerType.SelectedIndexChanged += new System.EventHandler(this.comboPrecursorAnalyzerType_SelectedIndexChanged);
            // 
            // label32
            // 
            resources.ApplyResources(this.label32, "label32");
            this.label32.Name = "label32";
            // 
            // groupBoxMS2
            // 
            this.groupBoxMS2.Controls.Add(this.labelProductPPM);
            this.groupBoxMS2.Controls.Add(this.comboIsolationScheme);
            this.groupBoxMS2.Controls.Add(this.labelProductAt);
            this.groupBoxMS2.Controls.Add(this.textProductAt);
            this.groupBoxMS2.Controls.Add(this.labelProductTh);
            this.groupBoxMS2.Controls.Add(this.textProductRes);
            this.groupBoxMS2.Controls.Add(this.labelProductRes);
            this.groupBoxMS2.Controls.Add(this.comboProductAnalyzerType);
            this.groupBoxMS2.Controls.Add(this.label22);
            this.groupBoxMS2.Controls.Add(this.labelIsolationScheme);
            this.groupBoxMS2.Controls.Add(this.comboAcquisitionMethod);
            this.groupBoxMS2.Controls.Add(this.label20);
            resources.ApplyResources(this.groupBoxMS2, "groupBoxMS2");
            this.groupBoxMS2.Name = "groupBoxMS2";
            this.groupBoxMS2.TabStop = false;
            // 
            // labelProductPPM
            // 
            resources.ApplyResources(this.labelProductPPM, "labelProductPPM");
            this.labelProductPPM.Name = "labelProductPPM";
            // 
            // comboIsolationScheme
            // 
            this.comboIsolationScheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIsolationScheme.FormattingEnabled = true;
            resources.ApplyResources(this.comboIsolationScheme, "comboIsolationScheme");
            this.comboIsolationScheme.Name = "comboIsolationScheme";
            this.comboIsolationScheme.SelectedIndexChanged += new System.EventHandler(this.comboIsolationScheme_SelectedIndexChanged);
            // 
            // labelProductAt
            // 
            resources.ApplyResources(this.labelProductAt, "labelProductAt");
            this.labelProductAt.Name = "labelProductAt";
            // 
            // textProductAt
            // 
            resources.ApplyResources(this.textProductAt, "textProductAt");
            this.textProductAt.Name = "textProductAt";
            // 
            // labelProductTh
            // 
            resources.ApplyResources(this.labelProductTh, "labelProductTh");
            this.labelProductTh.Name = "labelProductTh";
            // 
            // textProductRes
            // 
            resources.ApplyResources(this.textProductRes, "textProductRes");
            this.textProductRes.Name = "textProductRes";
            // 
            // labelProductRes
            // 
            resources.ApplyResources(this.labelProductRes, "labelProductRes");
            this.labelProductRes.Name = "labelProductRes";
            // 
            // comboProductAnalyzerType
            // 
            this.comboProductAnalyzerType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboProductAnalyzerType, "comboProductAnalyzerType");
            this.comboProductAnalyzerType.FormattingEnabled = true;
            this.comboProductAnalyzerType.Name = "comboProductAnalyzerType";
            this.toolTip.SetToolTip(this.comboProductAnalyzerType, resources.GetString("comboProductAnalyzerType.ToolTip"));
            this.comboProductAnalyzerType.SelectedIndexChanged += new System.EventHandler(this.comboProductAnalyzerType_SelectedIndexChanged);
            // 
            // label22
            // 
            resources.ApplyResources(this.label22, "label22");
            this.label22.Name = "label22";
            // 
            // labelIsolationScheme
            // 
            resources.ApplyResources(this.labelIsolationScheme, "labelIsolationScheme");
            this.labelIsolationScheme.Name = "labelIsolationScheme";
            // 
            // comboAcquisitionMethod
            // 
            this.comboAcquisitionMethod.DisplayMember = "Label";
            this.comboAcquisitionMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAcquisitionMethod.FormattingEnabled = true;
            resources.ApplyResources(this.comboAcquisitionMethod, "comboAcquisitionMethod");
            this.comboAcquisitionMethod.Name = "comboAcquisitionMethod";
            this.comboAcquisitionMethod.SelectedIndexChanged += new System.EventHandler(this.comboAcquisitionMethod_SelectedIndexChanged);
            // 
            // label20
            // 
            resources.ApplyResources(this.label20, "label20");
            this.label20.Name = "label20";
            // 
            // lblPrecursorCharges
            // 
            resources.ApplyResources(this.lblPrecursorCharges, "lblPrecursorCharges");
            this.lblPrecursorCharges.Name = "lblPrecursorCharges";
            // 
            // textPrecursorCharges
            // 
            resources.ApplyResources(this.textPrecursorCharges, "textPrecursorCharges");
            this.textPrecursorCharges.Name = "textPrecursorCharges";
            // 
            // toolTip
            // 
            this.toolTip.AutoPopDelay = 32767;
            this.toolTip.InitialDelay = 500;
            this.toolTip.ReshowDelay = 100;
            // 
            // cbHighSelectivity
            // 
            resources.ApplyResources(this.cbHighSelectivity, "cbHighSelectivity");
            this.cbHighSelectivity.Name = "cbHighSelectivity";
            this.toolTip.SetToolTip(this.cbHighSelectivity, resources.GetString("cbHighSelectivity.ToolTip"));
            this.cbHighSelectivity.UseVisualStyleBackColor = true;
            // 
            // usercontrolIonMobilityFiltering
            // 
            this.usercontrolIonMobilityFiltering.IonMobilityFilterResolvingPower = null;
            this.usercontrolIonMobilityFiltering.IsUseSpectralLibraryIonMobilities = false;
            resources.ApplyResources(this.usercontrolIonMobilityFiltering, "usercontrolIonMobilityFiltering");
            this.usercontrolIonMobilityFiltering.Name = "usercontrolIonMobilityFiltering";
            this.usercontrolIonMobilityFiltering.WindowWidthType = pwiz.Skyline.Model.DocSettings.IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.none;
            // 
            // FullScanSettingsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.usercontrolIonMobilityFiltering);
            this.Controls.Add(this.cbHighSelectivity);
            this.Controls.Add(this.textPrecursorCharges);
            this.Controls.Add(this.lblPrecursorCharges);
            this.Controls.Add(this.groupBoxRetentionTimeToKeep);
            this.Controls.Add(this.groupBoxMS1);
            this.Controls.Add(this.groupBoxMS2);
            this.Name = "FullScanSettingsControl";
            this.groupBoxRetentionTimeToKeep.ResumeLayout(false);
            this.groupBoxRetentionTimeToKeep.PerformLayout();
            this.flowLayoutPanelUseSchedulingWindow.ResumeLayout(false);
            this.flowLayoutPanelUseSchedulingWindow.PerformLayout();
            this.flowLayoutPanelTimeAroundMs2Ids.ResumeLayout(false);
            this.flowLayoutPanelTimeAroundMs2Ids.PerformLayout();
            this.groupBoxMS1.ResumeLayout(false);
            this.groupBoxMS1.PerformLayout();
            this.groupBoxMS2.ResumeLayout(false);
            this.groupBoxMS2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxRetentionTimeToKeep;
        private System.Windows.Forms.RadioButton radioTimeAroundMs2Ids;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelTimeAroundMs2Ids;
        private System.Windows.Forms.Label labelTimeAroundMs2Ids1;
        private System.Windows.Forms.TextBox tbxTimeAroundMs2Ids;
        private System.Windows.Forms.Label labelTimeAroundMs2Ids2;
        private System.Windows.Forms.RadioButton radioUseSchedulingWindow;
        private System.Windows.Forms.RadioButton radioKeepAllTime;
        private System.Windows.Forms.GroupBox groupBoxMS1;
        private System.Windows.Forms.ComboBox comboEnrichments;
        private System.Windows.Forms.Label labelEnrichments;
        private System.Windows.Forms.Label labelPrecursorIsotopeFilterPercent;
        private System.Windows.Forms.TextBox textPrecursorIsotopeFilter;
        private System.Windows.Forms.Label labelPrecursorIsotopeFilter;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.ComboBox comboPrecursorIsotopes;
        private System.Windows.Forms.Label labelPrecursorAt;
        private System.Windows.Forms.TextBox textPrecursorAt;
        private System.Windows.Forms.Label labelPrecursorTh;
        private System.Windows.Forms.TextBox textPrecursorRes;
        private System.Windows.Forms.Label labelPrecursorRes;
        private System.Windows.Forms.ComboBox comboPrecursorAnalyzerType;
        private System.Windows.Forms.Label label32;
        private System.Windows.Forms.GroupBox groupBoxMS2;
        private System.Windows.Forms.ComboBox comboIsolationScheme;
        private System.Windows.Forms.Label labelProductAt;
        private System.Windows.Forms.TextBox textProductAt;
        private System.Windows.Forms.Label labelProductTh;
        private System.Windows.Forms.TextBox textProductRes;
        private System.Windows.Forms.Label labelProductRes;
        private System.Windows.Forms.ComboBox comboProductAnalyzerType;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.Label labelIsolationScheme;
        private System.Windows.Forms.ComboBox comboAcquisitionMethod;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Label lblPrecursorCharges;
        private System.Windows.Forms.TextBox textPrecursorCharges;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelUseSchedulingWindow;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxTimeAroundPrediction;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label labelPrecursorPPM;
        private System.Windows.Forms.Label labelProductPPM;
        private System.Windows.Forms.CheckBox cbHighSelectivity;
        private IonMobility.IonMobilityFilteringUserControl usercontrolIonMobilityFiltering;
    }
}
