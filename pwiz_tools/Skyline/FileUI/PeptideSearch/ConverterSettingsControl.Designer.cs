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
            this.converterTabControl = new System.Windows.Forms.TabControl();
            this.msconvertTabPage = new System.Windows.Forms.TabPage();
            this.diaUmpireTabPage = new System.Windows.Forms.TabPage();
            this.cbEstimateBg = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.cbInstrumentPreset = new System.Windows.Forms.ComboBox();
            this.lblInstrumentPreset = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.converterTabControl.SuspendLayout();
            this.diaUmpireTabPage.SuspendLayout();
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
            this.diaUmpireTabPage.Controls.Add(this.cbEstimateBg);
            this.diaUmpireTabPage.Controls.Add(this.label1);
            this.diaUmpireTabPage.Controls.Add(this.btnAdditionalSettings);
            this.diaUmpireTabPage.Controls.Add(this.cbInstrumentPreset);
            this.diaUmpireTabPage.Controls.Add(this.lblInstrumentPreset);
            resources.ApplyResources(this.diaUmpireTabPage, "diaUmpireTabPage");
            this.diaUmpireTabPage.Name = "diaUmpireTabPage";
            this.diaUmpireTabPage.UseVisualStyleBackColor = true;
            // 
            // cbEstimateBg
            // 
            resources.ApplyResources(this.cbEstimateBg, "cbEstimateBg");
            this.cbEstimateBg.Name = "cbEstimateBg";
            this.toolTip.SetToolTip(this.cbEstimateBg, resources.GetString("cbEstimateBg.ToolTip"));
            this.cbEstimateBg.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // cbInstrumentPreset
            // 
            this.cbInstrumentPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbInstrumentPreset.FormattingEnabled = true;
            resources.ApplyResources(this.cbInstrumentPreset, "cbInstrumentPreset");
            this.cbInstrumentPreset.Name = "cbInstrumentPreset";
            // 
            // lblInstrumentPreset
            // 
            resources.ApplyResources(this.lblInstrumentPreset, "lblInstrumentPreset");
            this.lblInstrumentPreset.Name = "lblInstrumentPreset";
            // 
            // ConverterSettingsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.converterTabControl);
            this.Name = "ConverterSettingsControl";
            this.converterTabControl.ResumeLayout(false);
            this.diaUmpireTabPage.ResumeLayout(false);
            this.diaUmpireTabPage.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnAdditionalSettings;
        private System.Windows.Forms.TabControl converterTabControl;
        private System.Windows.Forms.TabPage msconvertTabPage;
        private System.Windows.Forms.TabPage diaUmpireTabPage;
        private System.Windows.Forms.ComboBox cbInstrumentPreset;
        private System.Windows.Forms.Label lblInstrumentPreset;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox cbEstimateBg;
        private System.Windows.Forms.ToolTip toolTip;
    }
}