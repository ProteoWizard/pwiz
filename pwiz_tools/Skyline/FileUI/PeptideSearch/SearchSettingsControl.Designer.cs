namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class SearchSettingsControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SearchSettingsControl));
            this.cbMS1TolUnit = new System.Windows.Forms.ComboBox();
            this.lblMs1Tolerance = new System.Windows.Forms.Label();
            this.txtMS1Tolerance = new System.Windows.Forms.TextBox();
            this.txtMS2Tolerance = new System.Windows.Forms.TextBox();
            this.lblMs2Tolerance = new System.Windows.Forms.Label();
            this.cbMS2TolUnit = new System.Windows.Forms.ComboBox();
            this.lblFragmentIons = new System.Windows.Forms.Label();
            this.cbFragmentIons = new System.Windows.Forms.ComboBox();
            this.btnAdditionalSettings = new System.Windows.Forms.Button();
            this.pBLogo = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.lblMaxVariableMods = new System.Windows.Forms.Label();
            this.cbMaxVariableMods = new System.Windows.Forms.ComboBox();
            this.searchEngineComboBox = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cbMs2Analyzer = new System.Windows.Forms.ComboBox();
            this.lblMs2Analyzer = new System.Windows.Forms.Label();
            this.groupBoxHardklor = new System.Windows.Forms.GroupBox();
            this.labelPPM = new System.Windows.Forms.Label();
            this.textHardklorMinIntensityPPM = new System.Windows.Forms.TextBox();
            this.labelMinIntensityPPM = new System.Windows.Forms.Label();
            this.textHardklorMinIdotP = new System.Windows.Forms.TextBox();
            this.labelHardklorMinIdotP = new System.Windows.Forms.Label();
            this.textHardklorSignalToNoise = new System.Windows.Forms.TextBox();
            this.lblHardklorSignalToNoise = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.textCutoff = new System.Windows.Forms.TextBox();
            this.labelCutoff = new System.Windows.Forms.Label();
            this.lblSearchEngineBlurb = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pBLogo)).BeginInit();
            this.groupBoxHardklor.SuspendLayout();
            this.SuspendLayout();
            // 
            // cbMS1TolUnit
            // 
            this.cbMS1TolUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMS1TolUnit.FormattingEnabled = true;
            resources.ApplyResources(this.cbMS1TolUnit, "cbMS1TolUnit");
            this.cbMS1TolUnit.Name = "cbMS1TolUnit";
            // 
            // lblMs1Tolerance
            // 
            resources.ApplyResources(this.lblMs1Tolerance, "lblMs1Tolerance");
            this.lblMs1Tolerance.Name = "lblMs1Tolerance";
            // 
            // txtMS1Tolerance
            // 
            resources.ApplyResources(this.txtMS1Tolerance, "txtMS1Tolerance");
            this.txtMS1Tolerance.Name = "txtMS1Tolerance";
            // 
            // txtMS2Tolerance
            // 
            resources.ApplyResources(this.txtMS2Tolerance, "txtMS2Tolerance");
            this.txtMS2Tolerance.Name = "txtMS2Tolerance";
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
            // lblFragmentIons
            // 
            resources.ApplyResources(this.lblFragmentIons, "lblFragmentIons");
            this.lblFragmentIons.Name = "lblFragmentIons";
            // 
            // cbFragmentIons
            // 
            this.cbFragmentIons.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbFragmentIons.FormattingEnabled = true;
            resources.ApplyResources(this.cbFragmentIons, "cbFragmentIons");
            this.cbFragmentIons.Name = "cbFragmentIons";
            // 
            // btnAdditionalSettings
            // 
            resources.ApplyResources(this.btnAdditionalSettings, "btnAdditionalSettings");
            this.btnAdditionalSettings.Name = "btnAdditionalSettings";
            this.btnAdditionalSettings.UseVisualStyleBackColor = true;
            this.btnAdditionalSettings.Click += new System.EventHandler(this.btnAdditionalSettings_Click);
            // 
            // pBLogo
            // 
            resources.ApplyResources(this.pBLogo, "pBLogo");
            this.pBLogo.Name = "pBLogo";
            this.pBLogo.TabStop = false;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // lblMaxVariableMods
            // 
            resources.ApplyResources(this.lblMaxVariableMods, "lblMaxVariableMods");
            this.lblMaxVariableMods.Name = "lblMaxVariableMods";
            // 
            // cbMaxVariableMods
            // 
            this.cbMaxVariableMods.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMaxVariableMods.FormattingEnabled = true;
            this.cbMaxVariableMods.Items.AddRange(new object[] {
            resources.GetString("cbMaxVariableMods.Items"),
            resources.GetString("cbMaxVariableMods.Items1"),
            resources.GetString("cbMaxVariableMods.Items2"),
            resources.GetString("cbMaxVariableMods.Items3"),
            resources.GetString("cbMaxVariableMods.Items4"),
            resources.GetString("cbMaxVariableMods.Items5"),
            resources.GetString("cbMaxVariableMods.Items6"),
            resources.GetString("cbMaxVariableMods.Items7"),
            resources.GetString("cbMaxVariableMods.Items8"),
            resources.GetString("cbMaxVariableMods.Items9")});
            resources.ApplyResources(this.cbMaxVariableMods, "cbMaxVariableMods");
            this.cbMaxVariableMods.Name = "cbMaxVariableMods";
            // 
            // searchEngineComboBox
            // 
            this.searchEngineComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.searchEngineComboBox.FormattingEnabled = true;
            this.searchEngineComboBox.Items.AddRange(new object[] {
            resources.GetString("searchEngineComboBox.Items"),
            resources.GetString("searchEngineComboBox.Items1"),
            resources.GetString("searchEngineComboBox.Items2")});
            resources.ApplyResources(this.searchEngineComboBox, "searchEngineComboBox");
            this.searchEngineComboBox.Name = "searchEngineComboBox";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // cbMs2Analyzer
            // 
            this.cbMs2Analyzer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMs2Analyzer.FormattingEnabled = true;
            resources.ApplyResources(this.cbMs2Analyzer, "cbMs2Analyzer");
            this.cbMs2Analyzer.Name = "cbMs2Analyzer";
            // 
            // lblMs2Analyzer
            // 
            resources.ApplyResources(this.lblMs2Analyzer, "lblMs2Analyzer");
            this.lblMs2Analyzer.Name = "lblMs2Analyzer";
            // 
            // groupBoxHardklor
            // 
            this.groupBoxHardklor.Controls.Add(this.labelPPM);
            this.groupBoxHardklor.Controls.Add(this.textHardklorMinIntensityPPM);
            this.groupBoxHardklor.Controls.Add(this.labelMinIntensityPPM);
            this.groupBoxHardklor.Controls.Add(this.textHardklorMinIdotP);
            this.groupBoxHardklor.Controls.Add(this.labelHardklorMinIdotP);
            this.groupBoxHardklor.Controls.Add(this.textHardklorSignalToNoise);
            this.groupBoxHardklor.Controls.Add(this.lblHardklorSignalToNoise);
            resources.ApplyResources(this.groupBoxHardklor, "groupBoxHardklor");
            this.groupBoxHardklor.Name = "groupBoxHardklor";
            this.groupBoxHardklor.TabStop = false;
            // 
            // labelPPM
            // 
            resources.ApplyResources(this.labelPPM, "labelPPM");
            this.labelPPM.Name = "labelPPM";
            // 
            // textHardklorMinIntensityPPM
            // 
            resources.ApplyResources(this.textHardklorMinIntensityPPM, "textHardklorMinIntensityPPM");
            this.textHardklorMinIntensityPPM.Name = "textHardklorMinIntensityPPM";
            this.toolTip1.SetToolTip(this.textHardklorMinIntensityPPM, resources.GetString("textHardklorMinIntensityPPM.ToolTip"));
            // 
            // labelMinIntensityPPM
            // 
            resources.ApplyResources(this.labelMinIntensityPPM, "labelMinIntensityPPM");
            this.labelMinIntensityPPM.Name = "labelMinIntensityPPM";
            // 
            // textHardklorMinIdotP
            // 
            resources.ApplyResources(this.textHardklorMinIdotP, "textHardklorMinIdotP");
            this.textHardklorMinIdotP.Name = "textHardklorMinIdotP";
            // 
            // labelHardklorMinIdotP
            // 
            resources.ApplyResources(this.labelHardklorMinIdotP, "labelHardklorMinIdotP");
            this.labelHardklorMinIdotP.Name = "labelHardklorMinIdotP";
            // 
            // textHardklorSignalToNoise
            // 
            resources.ApplyResources(this.textHardklorSignalToNoise, "textHardklorSignalToNoise");
            this.textHardklorSignalToNoise.Name = "textHardklorSignalToNoise";
            this.toolTip1.SetToolTip(this.textHardklorSignalToNoise, resources.GetString("textHardklorSignalToNoise.ToolTip"));
            // 
            // lblHardklorSignalToNoise
            // 
            resources.ApplyResources(this.lblHardklorSignalToNoise, "lblHardklorSignalToNoise");
            this.lblHardklorSignalToNoise.Name = "lblHardklorSignalToNoise";
            // 
            // textCutoff
            // 
            resources.ApplyResources(this.textCutoff, "textCutoff");
            this.textCutoff.Name = "textCutoff";
            this.toolTip1.SetToolTip(this.textCutoff, resources.GetString("textCutoff.ToolTip"));
            // 
            // labelCutoff
            // 
            resources.ApplyResources(this.labelCutoff, "labelCutoff");
            this.labelCutoff.Name = "labelCutoff";
            // 
            // lblSearchEngineBlurb
            // 
            resources.ApplyResources(this.lblSearchEngineBlurb, "lblSearchEngineBlurb");
            this.lblSearchEngineBlurb.Name = "lblSearchEngineBlurb";
            // 
            // SearchSettingsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.labelCutoff);
            this.Controls.Add(this.textCutoff);
            this.Controls.Add(this.lblSearchEngineBlurb);
            this.Controls.Add(this.groupBoxHardklor);
            this.Controls.Add(this.cbMs2Analyzer);
            this.Controls.Add(this.lblMs2Analyzer);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.searchEngineComboBox);
            this.Controls.Add(this.cbMaxVariableMods);
            this.Controls.Add(this.lblMaxVariableMods);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pBLogo);
            this.Controls.Add(this.btnAdditionalSettings);
            this.Controls.Add(this.cbFragmentIons);
            this.Controls.Add(this.lblFragmentIons);
            this.Controls.Add(this.txtMS2Tolerance);
            this.Controls.Add(this.lblMs2Tolerance);
            this.Controls.Add(this.cbMS2TolUnit);
            this.Controls.Add(this.txtMS1Tolerance);
            this.Controls.Add(this.lblMs1Tolerance);
            this.Controls.Add(this.cbMS1TolUnit);
            this.Name = "SearchSettingsControl";
            ((System.ComponentModel.ISupportInitialize)(this.pBLogo)).EndInit();
            this.groupBoxHardklor.ResumeLayout(false);
            this.groupBoxHardklor.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ComboBox cbMS1TolUnit;
        private System.Windows.Forms.Label lblMs1Tolerance;
        private System.Windows.Forms.TextBox txtMS1Tolerance;
        private System.Windows.Forms.TextBox txtMS2Tolerance;
        private System.Windows.Forms.Label lblMs2Tolerance;
        private System.Windows.Forms.ComboBox cbMS2TolUnit;
        private System.Windows.Forms.Label lblFragmentIons;
        private System.Windows.Forms.ComboBox cbFragmentIons;
        private System.Windows.Forms.Button btnAdditionalSettings;
        private System.Windows.Forms.PictureBox pBLogo;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblMaxVariableMods;
        private System.Windows.Forms.ComboBox cbMaxVariableMods;
        private System.Windows.Forms.ComboBox searchEngineComboBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cbMs2Analyzer;
        private System.Windows.Forms.Label lblMs2Analyzer;
        private System.Windows.Forms.GroupBox groupBoxHardklor;
        private System.Windows.Forms.TextBox textHardklorMinIdotP;
        private System.Windows.Forms.Label labelHardklorMinIdotP;
        private System.Windows.Forms.TextBox textHardklorSignalToNoise;
        private System.Windows.Forms.Label lblHardklorSignalToNoise;
        private System.Windows.Forms.Label labelMinIntensityPPM;
        private System.Windows.Forms.TextBox textHardklorMinIntensityPPM;
        private System.Windows.Forms.Label labelPPM;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Label labelCutoff;
        private System.Windows.Forms.TextBox textCutoff;
        private System.Windows.Forms.Label lblSearchEngineBlurb;
    }
}