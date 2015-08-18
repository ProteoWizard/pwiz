namespace pwiz.Skyline.FileUI
{
    sealed partial class ExportMethodDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExportMethodDlg));
            this.radioSingle = new System.Windows.Forms.RadioButton();
            this.radioProtein = new System.Windows.Forms.RadioButton();
            this.radioBuckets = new System.Windows.Forms.RadioButton();
            this.textMaxTransitions = new System.Windows.Forms.TextBox();
            this.labelMaxTransitions = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.comboTargetType = new System.Windows.Forms.ComboBox();
            this.textRunLength = new System.Windows.Forms.TextBox();
            this.textDwellTime = new System.Windows.Forms.TextBox();
            this.labelDwellTime = new System.Windows.Forms.Label();
            this.comboInstrument = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.labelTemplateFile = new System.Windows.Forms.Label();
            this.textTemplateFile = new System.Windows.Forms.TextBox();
            this.btnBrowseTemplate = new System.Windows.Forms.Button();
            this.cbIgnoreProteins = new System.Windows.Forms.CheckBox();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.cbEnergyRamp = new System.Windows.Forms.CheckBox();
            this.cbTriggerRefColumns = new System.Windows.Forms.CheckBox();
            this.cbExportMultiQuant = new System.Windows.Forms.CheckBox();
            this.cbUseStartAndEndRts = new System.Windows.Forms.CheckBox();
            this.cbSlens = new System.Windows.Forms.CheckBox();
            this.comboOptimizing = new System.Windows.Forms.ComboBox();
            this.labelOptimizing = new System.Windows.Forms.Label();
            this.labelMethods = new System.Windows.Forms.Label();
            this.labelMethodNum = new System.Windows.Forms.Label();
            this.panelThermoColumns = new System.Windows.Forms.Panel();
            this.panelAbSciexTOF = new System.Windows.Forms.Panel();
            this.panelTriggered = new System.Windows.Forms.Panel();
            this.textPrimaryCount = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.panelThermoRt = new System.Windows.Forms.Panel();
            this.comboTuning = new System.Windows.Forms.ComboBox();
            this.panelSciexTune = new System.Windows.Forms.Panel();
            this.label6 = new System.Windows.Forms.Label();
            this.panelWaters = new System.Windows.Forms.Panel();
            this.cbExportEdcMass = new System.Windows.Forms.CheckBox();
            this.panelThermoColumns.SuspendLayout();
            this.panelAbSciexTOF.SuspendLayout();
            this.panelTriggered.SuspendLayout();
            this.panelThermoRt.SuspendLayout();
            this.panelSciexTune.SuspendLayout();
            this.panelWaters.SuspendLayout();
            this.SuspendLayout();
            // 
            // radioSingle
            // 
            resources.ApplyResources(this.radioSingle, "radioSingle");
            this.radioSingle.Name = "radioSingle";
            this.radioSingle.TabStop = true;
            this.helpTip.SetToolTip(this.radioSingle, resources.GetString("radioSingle.ToolTip"));
            this.radioSingle.UseVisualStyleBackColor = true;
            this.radioSingle.CheckedChanged += new System.EventHandler(this.radioSingle_CheckedChanged);
            // 
            // radioProtein
            // 
            resources.ApplyResources(this.radioProtein, "radioProtein");
            this.radioProtein.Name = "radioProtein";
            this.radioProtein.TabStop = true;
            this.helpTip.SetToolTip(this.radioProtein, resources.GetString("radioProtein.ToolTip"));
            this.radioProtein.UseVisualStyleBackColor = true;
            this.radioProtein.CheckedChanged += new System.EventHandler(this.radioProtein_CheckedChanged);
            // 
            // radioBuckets
            // 
            resources.ApplyResources(this.radioBuckets, "radioBuckets");
            this.radioBuckets.Name = "radioBuckets";
            this.radioBuckets.TabStop = true;
            this.helpTip.SetToolTip(this.radioBuckets, resources.GetString("radioBuckets.ToolTip"));
            this.radioBuckets.UseVisualStyleBackColor = true;
            this.radioBuckets.CheckedChanged += new System.EventHandler(this.radioBuckets_CheckedChanged);
            // 
            // textMaxTransitions
            // 
            resources.ApplyResources(this.textMaxTransitions, "textMaxTransitions");
            this.textMaxTransitions.Name = "textMaxTransitions";
            this.helpTip.SetToolTip(this.textMaxTransitions, resources.GetString("textMaxTransitions.ToolTip"));
            this.textMaxTransitions.TextChanged += new System.EventHandler(this.textMaxTransitions_TextChanged);
            // 
            // labelMaxTransitions
            // 
            resources.ApplyResources(this.labelMaxTransitions, "labelMaxTransitions");
            this.labelMaxTransitions.Name = "labelMaxTransitions";
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // comboTargetType
            // 
            this.comboTargetType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTargetType.FormattingEnabled = true;
            resources.ApplyResources(this.comboTargetType, "comboTargetType");
            this.comboTargetType.Name = "comboTargetType";
            this.comboTargetType.SelectedIndexChanged += new System.EventHandler(this.comboTargetType_SelectedIndexChanged);
            // 
            // textRunLength
            // 
            resources.ApplyResources(this.textRunLength, "textRunLength");
            this.textRunLength.Name = "textRunLength";
            // 
            // textDwellTime
            // 
            resources.ApplyResources(this.textDwellTime, "textDwellTime");
            this.textDwellTime.Name = "textDwellTime";
            // 
            // labelDwellTime
            // 
            resources.ApplyResources(this.labelDwellTime, "labelDwellTime");
            this.labelDwellTime.Name = "labelDwellTime";
            // 
            // comboInstrument
            // 
            this.comboInstrument.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboInstrument.FormattingEnabled = true;
            this.comboInstrument.Items.AddRange(new object[] {
            resources.GetString("comboInstrument.Items"),
            resources.GetString("comboInstrument.Items1"),
            resources.GetString("comboInstrument.Items2"),
            resources.GetString("comboInstrument.Items3")});
            resources.ApplyResources(this.comboInstrument, "comboInstrument");
            this.comboInstrument.Name = "comboInstrument";
            this.helpTip.SetToolTip(this.comboInstrument, resources.GetString("comboInstrument.ToolTip"));
            this.comboInstrument.SelectedIndexChanged += new System.EventHandler(this.comboInstrument_SelectedIndexChanged);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // labelTemplateFile
            // 
            resources.ApplyResources(this.labelTemplateFile, "labelTemplateFile");
            this.labelTemplateFile.Name = "labelTemplateFile";
            // 
            // textTemplateFile
            // 
            resources.ApplyResources(this.textTemplateFile, "textTemplateFile");
            this.textTemplateFile.Name = "textTemplateFile";
            // 
            // btnBrowseTemplate
            // 
            resources.ApplyResources(this.btnBrowseTemplate, "btnBrowseTemplate");
            this.btnBrowseTemplate.Name = "btnBrowseTemplate";
            this.btnBrowseTemplate.UseVisualStyleBackColor = true;
            this.btnBrowseTemplate.Click += new System.EventHandler(this.btnBrowseTemplate_Click);
            // 
            // cbIgnoreProteins
            // 
            resources.ApplyResources(this.cbIgnoreProteins, "cbIgnoreProteins");
            this.cbIgnoreProteins.Name = "cbIgnoreProteins";
            this.helpTip.SetToolTip(this.cbIgnoreProteins, resources.GetString("cbIgnoreProteins.ToolTip"));
            this.cbIgnoreProteins.UseVisualStyleBackColor = true;
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // cbEnergyRamp
            // 
            resources.ApplyResources(this.cbEnergyRamp, "cbEnergyRamp");
            this.cbEnergyRamp.Name = "cbEnergyRamp";
            this.helpTip.SetToolTip(this.cbEnergyRamp, resources.GetString("cbEnergyRamp.ToolTip"));
            this.cbEnergyRamp.UseVisualStyleBackColor = true;
            // 
            // cbTriggerRefColumns
            // 
            resources.ApplyResources(this.cbTriggerRefColumns, "cbTriggerRefColumns");
            this.cbTriggerRefColumns.Name = "cbTriggerRefColumns";
            this.helpTip.SetToolTip(this.cbTriggerRefColumns, resources.GetString("cbTriggerRefColumns.ToolTip"));
            this.cbTriggerRefColumns.UseVisualStyleBackColor = true;
            // 
            // cbExportMultiQuant
            // 
            resources.ApplyResources(this.cbExportMultiQuant, "cbExportMultiQuant");
            this.cbExportMultiQuant.Name = "cbExportMultiQuant";
            this.helpTip.SetToolTip(this.cbExportMultiQuant, resources.GetString("cbExportMultiQuant.ToolTip"));
            this.cbExportMultiQuant.UseVisualStyleBackColor = true;
            // 
            // cbUseStartAndEndRts
            // 
            resources.ApplyResources(this.cbUseStartAndEndRts, "cbUseStartAndEndRts");
            this.cbUseStartAndEndRts.Name = "cbUseStartAndEndRts";
            this.helpTip.SetToolTip(this.cbUseStartAndEndRts, resources.GetString("cbUseStartAndEndRts.ToolTip"));
            this.cbUseStartAndEndRts.UseVisualStyleBackColor = true;
            // 
            // cbSlens
            // 
            resources.ApplyResources(this.cbSlens, "cbSlens");
            this.cbSlens.Name = "cbSlens";
            this.cbSlens.UseVisualStyleBackColor = true;
            // 
            // comboOptimizing
            // 
            this.comboOptimizing.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOptimizing.FormattingEnabled = true;
            resources.ApplyResources(this.comboOptimizing, "comboOptimizing");
            this.comboOptimizing.Name = "comboOptimizing";
            this.comboOptimizing.SelectedIndexChanged += new System.EventHandler(this.comboOptimizing_SelectedIndexChanged);
            // 
            // labelOptimizing
            // 
            resources.ApplyResources(this.labelOptimizing, "labelOptimizing");
            this.labelOptimizing.Name = "labelOptimizing";
            // 
            // labelMethods
            // 
            resources.ApplyResources(this.labelMethods, "labelMethods");
            this.labelMethods.Name = "labelMethods";
            // 
            // labelMethodNum
            // 
            resources.ApplyResources(this.labelMethodNum, "labelMethodNum");
            this.labelMethodNum.Name = "labelMethodNum";
            // 
            // panelThermoColumns
            // 
            this.panelThermoColumns.Controls.Add(this.cbEnergyRamp);
            this.panelThermoColumns.Controls.Add(this.cbTriggerRefColumns);
            resources.ApplyResources(this.panelThermoColumns, "panelThermoColumns");
            this.panelThermoColumns.Name = "panelThermoColumns";
            // 
            // panelAbSciexTOF
            // 
            this.panelAbSciexTOF.Controls.Add(this.cbExportMultiQuant);
            resources.ApplyResources(this.panelAbSciexTOF, "panelAbSciexTOF");
            this.panelAbSciexTOF.Name = "panelAbSciexTOF";
            // 
            // panelTriggered
            // 
            this.panelTriggered.Controls.Add(this.textPrimaryCount);
            this.panelTriggered.Controls.Add(this.label5);
            this.panelTriggered.Controls.Add(this.label3);
            resources.ApplyResources(this.panelTriggered, "panelTriggered");
            this.panelTriggered.Name = "panelTriggered";
            // 
            // textPrimaryCount
            // 
            resources.ApplyResources(this.textPrimaryCount, "textPrimaryCount");
            this.textPrimaryCount.Name = "textPrimaryCount";
            this.textPrimaryCount.TextChanged += new System.EventHandler(this.textPrimaryCount_TextChanged);
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // panelThermoRt
            // 
            this.panelThermoRt.Controls.Add(this.cbUseStartAndEndRts);
            resources.ApplyResources(this.panelThermoRt, "panelThermoRt");
            this.panelThermoRt.Name = "panelThermoRt";
            // 
            // comboTuning
            // 
            this.comboTuning.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTuning.FormattingEnabled = true;
            resources.ApplyResources(this.comboTuning, "comboTuning");
            this.comboTuning.Name = "comboTuning";
            // 
            // panelSciexTune
            // 
            this.panelSciexTune.Controls.Add(this.label6);
            this.panelSciexTune.Controls.Add(this.comboTuning);
            resources.ApplyResources(this.panelSciexTune, "panelSciexTune");
            this.panelSciexTune.Name = "panelSciexTune";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // panelWaters
            // 
            this.panelWaters.Controls.Add(this.cbExportEdcMass);
            resources.ApplyResources(this.panelWaters, "panelWaters");
            this.panelWaters.Name = "panelWaters";
            // 
            // cbExportEdcMass
            // 
            resources.ApplyResources(this.cbExportEdcMass, "cbExportEdcMass");
            this.cbExportEdcMass.Name = "cbExportEdcMass";
            this.cbExportEdcMass.UseVisualStyleBackColor = true;
            // 
            // ExportMethodDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.cbSlens);
            this.Controls.Add(this.labelMaxTransitions);
            this.Controls.Add(this.panelSciexTune);
            this.Controls.Add(this.panelTriggered);
            this.Controls.Add(this.labelMethodNum);
            this.Controls.Add(this.labelMethods);
            this.Controls.Add(this.labelOptimizing);
            this.Controls.Add(this.comboOptimizing);
            this.Controls.Add(this.cbIgnoreProteins);
            this.Controls.Add(this.btnBrowseTemplate);
            this.Controls.Add(this.textTemplateFile);
            this.Controls.Add(this.labelTemplateFile);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.comboInstrument);
            this.Controls.Add(this.labelDwellTime);
            this.Controls.Add(this.textDwellTime);
            this.Controls.Add(this.textRunLength);
            this.Controls.Add(this.comboTargetType);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.textMaxTransitions);
            this.Controls.Add(this.radioBuckets);
            this.Controls.Add(this.radioProtein);
            this.Controls.Add(this.radioSingle);
            this.Controls.Add(this.panelAbSciexTOF);
            this.Controls.Add(this.panelThermoColumns);
            this.Controls.Add(this.panelThermoRt);
            this.Controls.Add(this.panelWaters);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportMethodDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.panelThermoColumns.ResumeLayout(false);
            this.panelThermoColumns.PerformLayout();
            this.panelAbSciexTOF.ResumeLayout(false);
            this.panelAbSciexTOF.PerformLayout();
            this.panelTriggered.ResumeLayout(false);
            this.panelTriggered.PerformLayout();
            this.panelThermoRt.ResumeLayout(false);
            this.panelThermoRt.PerformLayout();
            this.panelSciexTune.ResumeLayout(false);
            this.panelSciexTune.PerformLayout();
            this.panelWaters.ResumeLayout(false);
            this.panelWaters.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton radioSingle;
        private System.Windows.Forms.RadioButton radioProtein;
        private System.Windows.Forms.RadioButton radioBuckets;
        private System.Windows.Forms.TextBox textMaxTransitions;
        private System.Windows.Forms.Label labelMaxTransitions;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboTargetType;
        private System.Windows.Forms.TextBox textRunLength;
        private System.Windows.Forms.TextBox textDwellTime;
        private System.Windows.Forms.Label labelDwellTime;
        private System.Windows.Forms.ComboBox comboInstrument;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label labelTemplateFile;
        private System.Windows.Forms.TextBox textTemplateFile;
        private System.Windows.Forms.Button btnBrowseTemplate;
        private System.Windows.Forms.CheckBox cbIgnoreProteins;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.CheckBox cbEnergyRamp;
        private System.Windows.Forms.ComboBox comboOptimizing;
        private System.Windows.Forms.Label labelOptimizing;
        private System.Windows.Forms.Label labelMethods;
        private System.Windows.Forms.Label labelMethodNum;
        private System.Windows.Forms.CheckBox cbTriggerRefColumns;
        private System.Windows.Forms.Panel panelThermoColumns;
        private System.Windows.Forms.Panel panelAbSciexTOF;
        private System.Windows.Forms.CheckBox cbExportMultiQuant;
        private System.Windows.Forms.Panel panelTriggered;
        private System.Windows.Forms.TextBox textPrimaryCount;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox cbUseStartAndEndRts;
        private System.Windows.Forms.Panel panelThermoRt;
        private System.Windows.Forms.ComboBox comboTuning;
        private System.Windows.Forms.Panel panelSciexTune;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Panel panelWaters;
        private System.Windows.Forms.CheckBox cbExportEdcMass;
        private System.Windows.Forms.CheckBox cbSlens;
    }
}
