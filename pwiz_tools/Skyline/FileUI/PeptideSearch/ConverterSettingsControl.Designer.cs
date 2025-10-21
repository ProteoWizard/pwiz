namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class ConverterSettingsControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConverterSettingsControl));
            this.btnAdditionalSettings = new System.Windows.Forms.Button();
            this.converterTabControl = new pwiz.Skyline.Controls.WizardPages();
            this.msconvertTabPage = new System.Windows.Forms.TabPage();
            this.diaUmpireTabPage = new System.Windows.Forms.TabPage();
            this.diaUmpireSettingsPanel = new System.Windows.Forms.Panel();
            this.lblInstrumentPreset = new System.Windows.Forms.Label();
            this.cbInstrumentPreset = new System.Windows.Forms.ComboBox();
            this.cbEstimateBg = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cbDiaUmpire = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.diaUmpireDescriptionPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.converterTabControl.SuspendLayout();
            this.diaUmpireTabPage.SuspendLayout();
            this.diaUmpireSettingsPanel.SuspendLayout();
            this.diaUmpireDescriptionPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnAdditionalSettings
            // 
            resources.ApplyResources(this.btnAdditionalSettings, "btnAdditionalSettings");
            this.btnAdditionalSettings.Name = "btnAdditionalSettings";
            this.btnAdditionalSettings.UseVisualStyleBackColor = true;
            this.btnAdditionalSettings.Click += new System.EventHandler(this.btnDiaUmpireAdditionalSettings_Click);
            // 
            // converterTabControl
            // 
            resources.ApplyResources(this.converterTabControl, "converterTabControl");
            this.converterTabControl.Controls.Add(this.msconvertTabPage);
            this.converterTabControl.Controls.Add(this.diaUmpireTabPage);
            this.converterTabControl.Name = "converterTabControl";
            this.converterTabControl.SelectedIndex = 0;
            // 
            // msconvertTabPage
            // 
            resources.ApplyResources(this.msconvertTabPage, "msconvertTabPage");
            this.msconvertTabPage.Name = "msconvertTabPage";
            this.msconvertTabPage.UseVisualStyleBackColor = true;
            // 
            // diaUmpireTabPage
            // 
            this.diaUmpireTabPage.Controls.Add(this.diaUmpireDescriptionPanel);
            this.diaUmpireTabPage.Controls.Add(this.diaUmpireSettingsPanel);
            resources.ApplyResources(this.diaUmpireTabPage, "diaUmpireTabPage");
            this.diaUmpireTabPage.Name = "diaUmpireTabPage";
            this.diaUmpireTabPage.UseVisualStyleBackColor = true;
            // 
            // diaUmpireSettingsPanel
            // 
            this.diaUmpireSettingsPanel.Controls.Add(this.btnAdditionalSettings);
            this.diaUmpireSettingsPanel.Controls.Add(this.lblInstrumentPreset);
            this.diaUmpireSettingsPanel.Controls.Add(this.cbInstrumentPreset);
            this.diaUmpireSettingsPanel.Controls.Add(this.cbEstimateBg);
            resources.ApplyResources(this.diaUmpireSettingsPanel, "diaUmpireSettingsPanel");
            this.diaUmpireSettingsPanel.Name = "diaUmpireSettingsPanel";
            // 
            // lblInstrumentPreset
            // 
            resources.ApplyResources(this.lblInstrumentPreset, "lblInstrumentPreset");
            this.lblInstrumentPreset.Name = "lblInstrumentPreset";
            // 
            // cbInstrumentPreset
            // 
            this.cbInstrumentPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbInstrumentPreset.FormattingEnabled = true;
            resources.ApplyResources(this.cbInstrumentPreset, "cbInstrumentPreset");
            this.cbInstrumentPreset.Name = "cbInstrumentPreset";
            // 
            // cbEstimateBg
            // 
            resources.ApplyResources(this.cbEstimateBg, "cbEstimateBg");
            this.cbEstimateBg.Name = "cbEstimateBg";
            this.toolTip.SetToolTip(this.cbEstimateBg, resources.GetString("cbEstimateBg.ToolTip"));
            this.cbEstimateBg.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // cbDiaUmpire
            // 
            this.diaUmpireDescriptionPanel.SetFlowBreak(this.cbDiaUmpire, true);
            resources.ApplyResources(this.cbDiaUmpire, "cbDiaUmpire");
            this.cbDiaUmpire.Name = "cbDiaUmpire";
            this.toolTip.SetToolTip(this.cbDiaUmpire, resources.GetString("cbDiaUmpire.ToolTip"));
            this.cbDiaUmpire.UseVisualStyleBackColor = true;
            this.cbDiaUmpire.CheckedChanged += new System.EventHandler(this.cbDiaUmpire_CheckedChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.diaUmpireDescriptionPanel.SetFlowBreak(this.label1, true);
            this.label1.Name = "label1";
            // 
            // diaUmpireDescriptionPanel
            // 
            resources.ApplyResources(this.diaUmpireDescriptionPanel, "diaUmpireDescriptionPanel");
            this.diaUmpireDescriptionPanel.Controls.Add(this.label1);
            this.diaUmpireDescriptionPanel.Controls.Add(this.cbDiaUmpire);
            this.diaUmpireDescriptionPanel.Controls.Add(this.label2);
            this.diaUmpireDescriptionPanel.Name = "diaUmpireDescriptionPanel";
            // 
            // ConverterSettingsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.converterTabControl);
            this.Name = "ConverterSettingsControl";
            this.converterTabControl.ResumeLayout(false);
            this.diaUmpireTabPage.ResumeLayout(false);
            this.diaUmpireSettingsPanel.ResumeLayout(false);
            this.diaUmpireSettingsPanel.PerformLayout();
            this.diaUmpireDescriptionPanel.ResumeLayout(false);
            this.diaUmpireDescriptionPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnAdditionalSettings;
        private pwiz.Skyline.Controls.WizardPages converterTabControl;
        private System.Windows.Forms.TabPage msconvertTabPage;
        private System.Windows.Forms.TabPage diaUmpireTabPage;
        private System.Windows.Forms.ComboBox cbInstrumentPreset;
        private System.Windows.Forms.Label lblInstrumentPreset;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox cbEstimateBg;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.CheckBox cbDiaUmpire;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel diaUmpireSettingsPanel;
        private System.Windows.Forms.FlowLayoutPanel diaUmpireDescriptionPanel;
    }
}