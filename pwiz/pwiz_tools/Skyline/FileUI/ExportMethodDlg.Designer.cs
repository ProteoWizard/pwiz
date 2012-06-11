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
            this.comboOptimizing = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.labelMethods = new System.Windows.Forms.Label();
            this.labelMethodNum = new System.Windows.Forms.Label();
            this.panelThermoColumns = new System.Windows.Forms.Panel();
            this.panelAbSciexTOF = new System.Windows.Forms.Panel();
            this.cbExportMultiQuant = new System.Windows.Forms.CheckBox();
            this.panelThermoColumns.SuspendLayout();
            this.panelAbSciexTOF.SuspendLayout();
            this.SuspendLayout();
            // 
            // radioSingle
            // 
            this.radioSingle.AutoSize = true;
            this.radioSingle.Location = new System.Drawing.Point(13, 69);
            this.radioSingle.Name = "radioSingle";
            this.radioSingle.Size = new System.Drawing.Size(92, 17);
            this.radioSingle.TabIndex = 2;
            this.radioSingle.TabStop = true;
            this.radioSingle.Text = "&Single method";
            this.helpTip.SetToolTip(this.radioSingle, "Create a single file including all transitions.  If this is not just for referenc" +
        "e,\r\nmake sure your instrument can handle the full set of transitions.");
            this.radioSingle.UseVisualStyleBackColor = true;
            this.radioSingle.CheckedChanged += new System.EventHandler(this.radioSingle_CheckedChanged);
            // 
            // radioProtein
            // 
            this.radioProtein.AutoSize = true;
            this.radioProtein.Location = new System.Drawing.Point(13, 93);
            this.radioProtein.Name = "radioProtein";
            this.radioProtein.Size = new System.Drawing.Size(136, 17);
            this.radioProtein.TabIndex = 3;
            this.radioProtein.TabStop = true;
            this.radioProtein.Text = "&One method per protein";
            this.helpTip.SetToolTip(this.radioProtein, "Split methods along protein boundaries.  If this is not just for reference,\r\nmake" +
        " sure your instrument can handle the number of transitions in each file.");
            this.radioProtein.UseVisualStyleBackColor = true;
            this.radioProtein.CheckedChanged += new System.EventHandler(this.radioProtein_CheckedChanged);
            // 
            // radioBuckets
            // 
            this.radioBuckets.AutoSize = true;
            this.radioBuckets.Location = new System.Drawing.Point(13, 117);
            this.radioBuckets.Name = "radioBuckets";
            this.radioBuckets.Size = new System.Drawing.Size(104, 17);
            this.radioBuckets.TabIndex = 4;
            this.radioBuckets.TabStop = true;
            this.radioBuckets.Text = "&Multiple methods";
            this.helpTip.SetToolTip(this.radioBuckets, "Create as many files as needed, given constraints on transition count.");
            this.radioBuckets.UseVisualStyleBackColor = true;
            this.radioBuckets.CheckedChanged += new System.EventHandler(this.radioBuckets_CheckedChanged);
            // 
            // textMaxTransitions
            // 
            this.textMaxTransitions.Location = new System.Drawing.Point(16, 174);
            this.textMaxTransitions.Name = "textMaxTransitions";
            this.textMaxTransitions.Size = new System.Drawing.Size(124, 20);
            this.textMaxTransitions.TabIndex = 7;
            this.helpTip.SetToolTip(this.textMaxTransitions, "Each file created will have at most this number of transitions, but may have fewe" +
        "r,\r\nif peptide or protein boundaries do not allow the maximum.");
            this.textMaxTransitions.TextChanged += new System.EventHandler(this.textMaxTransitions_TextChanged);
            // 
            // labelMaxTransitions
            // 
            this.labelMaxTransitions.AutoSize = true;
            this.labelMaxTransitions.Location = new System.Drawing.Point(13, 158);
            this.labelMaxTransitions.Name = "labelMaxTransitions";
            this.labelMaxTransitions.Size = new System.Drawing.Size(176, 13);
            this.labelMaxTransitions.TabIndex = 6;
            this.labelMaxTransitions.Text = "Ma&x transitions per sample injection:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(218, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 22;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(218, 11);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 21;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 303);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(69, 13);
            this.label2.TabIndex = 12;
            this.label2.Text = "Method &type:";
            // 
            // comboTargetType
            // 
            this.comboTargetType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTargetType.FormattingEnabled = true;
            this.comboTargetType.Items.AddRange(new object[] {
            "Standard",
            "Scheduled"});
            this.comboTargetType.Location = new System.Drawing.Point(16, 319);
            this.comboTargetType.Name = "comboTargetType";
            this.comboTargetType.Size = new System.Drawing.Size(124, 21);
            this.comboTargetType.TabIndex = 13;
            this.comboTargetType.SelectedIndexChanged += new System.EventHandler(this.comboTargetType_SelectedIndexChanged);
            // 
            // textRunLength
            // 
            this.textRunLength.Location = new System.Drawing.Point(174, 322);
            this.textRunLength.Name = "textRunLength";
            this.textRunLength.Size = new System.Drawing.Size(100, 20);
            this.textRunLength.TabIndex = 15;
            this.textRunLength.Visible = false;
            // 
            // textDwellTime
            // 
            this.textDwellTime.Location = new System.Drawing.Point(174, 319);
            this.textDwellTime.Name = "textDwellTime";
            this.textDwellTime.Size = new System.Drawing.Size(100, 20);
            this.textDwellTime.TabIndex = 15;
            this.textDwellTime.Visible = false;
            // 
            // labelDwellTime
            // 
            this.labelDwellTime.AutoSize = true;
            this.labelDwellTime.Location = new System.Drawing.Point(174, 303);
            this.labelDwellTime.Name = "labelDwellTime";
            this.labelDwellTime.Size = new System.Drawing.Size(80, 13);
            this.labelDwellTime.TabIndex = 14;
            this.labelDwellTime.Text = "&Dwell time (ms):";
            this.labelDwellTime.Visible = false;
            // 
            // comboInstrument
            // 
            this.comboInstrument.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboInstrument.FormattingEnabled = true;
            this.comboInstrument.Items.AddRange(new object[] {
            "ABI",
            "Agilent",
            "Thermo",
            "Waters"});
            this.comboInstrument.Location = new System.Drawing.Point(16, 27);
            this.comboInstrument.Name = "comboInstrument";
            this.comboInstrument.Size = new System.Drawing.Size(121, 21);
            this.comboInstrument.TabIndex = 1;
            this.helpTip.SetToolTip(this.comboInstrument, "Instrument type on which these settings will run");
            this.comboInstrument.SelectedIndexChanged += new System.EventHandler(this.comboInstrument_SelectedIndexChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 11);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(82, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Instrument type:";
            // 
            // labelTemplateFile
            // 
            this.labelTemplateFile.AutoSize = true;
            this.labelTemplateFile.Location = new System.Drawing.Point(13, 370);
            this.labelTemplateFile.Name = "labelTemplateFile";
            this.labelTemplateFile.Size = new System.Drawing.Size(70, 13);
            this.labelTemplateFile.TabIndex = 18;
            this.labelTemplateFile.Text = "T&emplate file:";
            // 
            // textTemplateFile
            // 
            this.textTemplateFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textTemplateFile.Location = new System.Drawing.Point(16, 388);
            this.textTemplateFile.Name = "textTemplateFile";
            this.textTemplateFile.Size = new System.Drawing.Size(196, 20);
            this.textTemplateFile.TabIndex = 19;
            // 
            // btnBrowseTemplate
            // 
            this.btnBrowseTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseTemplate.Location = new System.Drawing.Point(218, 387);
            this.btnBrowseTemplate.Name = "btnBrowseTemplate";
            this.btnBrowseTemplate.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseTemplate.TabIndex = 20;
            this.btnBrowseTemplate.Text = "&Browse...";
            this.btnBrowseTemplate.UseVisualStyleBackColor = true;
            this.btnBrowseTemplate.Click += new System.EventHandler(this.btnBrowseTemplate_Click);
            // 
            // cbIgnoreProteins
            // 
            this.cbIgnoreProteins.AutoSize = true;
            this.cbIgnoreProteins.Enabled = false;
            this.cbIgnoreProteins.Location = new System.Drawing.Point(179, 117);
            this.cbIgnoreProteins.Name = "cbIgnoreProteins";
            this.cbIgnoreProteins.Size = new System.Drawing.Size(96, 17);
            this.cbIgnoreProteins.TabIndex = 5;
            this.cbIgnoreProteins.Text = "Ignore p&roteins";
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
            this.cbEnergyRamp.AutoSize = true;
            this.cbEnergyRamp.Location = new System.Drawing.Point(5, 7);
            this.cbEnergyRamp.Name = "cbEnergyRamp";
            this.cbEnergyRamp.Size = new System.Drawing.Size(106, 17);
            this.cbEnergyRamp.TabIndex = 0;
            this.cbEnergyRamp.Text = "&Add energy ramp";
            this.helpTip.SetToolTip(this.cbEnergyRamp, "Add Energy Ramp column required by some version of\r\nThermo TSQ software");
            this.cbEnergyRamp.UseVisualStyleBackColor = true;
            // 
            // cbTriggerRefColumns
            // 
            this.cbTriggerRefColumns.AutoSize = true;
            this.cbTriggerRefColumns.Location = new System.Drawing.Point(5, 30);
            this.cbTriggerRefColumns.Name = "cbTriggerRefColumns";
            this.cbTriggerRefColumns.Size = new System.Drawing.Size(134, 17);
            this.cbTriggerRefColumns.TabIndex = 1;
            this.cbTriggerRefColumns.Text = "Add trigger && refere&nce";
            this.helpTip.SetToolTip(this.cbTriggerRefColumns, "Add Trigger and Reference columns required by some version of\r\nThermo TSQ softwar" +
        "e");
            this.cbTriggerRefColumns.UseVisualStyleBackColor = true;
            // 
            // comboOptimizing
            // 
            this.comboOptimizing.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOptimizing.FormattingEnabled = true;
            this.comboOptimizing.Location = new System.Drawing.Point(16, 250);
            this.comboOptimizing.Name = "comboOptimizing";
            this.comboOptimizing.Size = new System.Drawing.Size(121, 21);
            this.comboOptimizing.TabIndex = 11;
            this.comboOptimizing.SelectedIndexChanged += new System.EventHandler(this.comboOptimizing_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 236);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "Optimi&zing:";
            // 
            // labelMethods
            // 
            this.labelMethods.AutoSize = true;
            this.labelMethods.Location = new System.Drawing.Point(13, 201);
            this.labelMethods.Name = "labelMethods";
            this.labelMethods.Size = new System.Drawing.Size(51, 13);
            this.labelMethods.TabIndex = 8;
            this.labelMethods.Text = "Methods:";
            // 
            // labelMethodNum
            // 
            this.labelMethodNum.AutoSize = true;
            this.labelMethodNum.Location = new System.Drawing.Point(70, 201);
            this.labelMethodNum.Name = "labelMethodNum";
            this.labelMethodNum.Size = new System.Drawing.Size(21, 13);
            this.labelMethodNum.TabIndex = 9;
            this.labelMethodNum.Text = "##";
            // 
            // panelThermoColumns
            // 
            this.panelThermoColumns.Controls.Add(this.cbEnergyRamp);
            this.panelThermoColumns.Controls.Add(this.cbTriggerRefColumns);
            this.panelThermoColumns.Location = new System.Drawing.Point(148, 337);
            this.panelThermoColumns.Name = "panelThermoColumns";
            this.panelThermoColumns.Size = new System.Drawing.Size(138, 54);
            this.panelThermoColumns.TabIndex = 16;
            this.panelThermoColumns.Visible = false;
            // 
            // panelAbSciexTOF
            // 
            this.panelAbSciexTOF.Controls.Add(this.cbExportMultiQuant);
            this.panelAbSciexTOF.Location = new System.Drawing.Point(148, 348);
            this.panelAbSciexTOF.Name = "panelAbSciexTOF";
            this.panelAbSciexTOF.Size = new System.Drawing.Size(155, 28);
            this.panelAbSciexTOF.TabIndex = 17;
            // 
            // cbExportMultiQuant
            // 
            this.cbExportMultiQuant.AutoSize = true;
            this.cbExportMultiQuant.Location = new System.Drawing.Point(5, 6);
            this.cbExportMultiQuant.Name = "cbExportMultiQuant";
            this.cbExportMultiQuant.Size = new System.Drawing.Size(152, 17);
            this.cbExportMultiQuant.TabIndex = 0;
            this.cbExportMultiQuant.Text = "Create Multi&Quant  method";
            this.helpTip.SetToolTip(this.cbExportMultiQuant, "Exports a MultiQuant compatible analysis method to the same directory\r\nand base n" +
        "ame as the Analyst acquisition method.");
            this.cbExportMultiQuant.UseVisualStyleBackColor = true;
            // 
            // ExportMethodDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(305, 422);
            this.Controls.Add(this.labelMethodNum);
            this.Controls.Add(this.labelMethods);
            this.Controls.Add(this.label1);
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
            this.Controls.Add(this.labelMaxTransitions);
            this.Controls.Add(this.textMaxTransitions);
            this.Controls.Add(this.radioBuckets);
            this.Controls.Add(this.radioProtein);
            this.Controls.Add(this.radioSingle);
            this.Controls.Add(this.panelAbSciexTOF);
            this.Controls.Add(this.panelThermoColumns);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportMethodDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export Method";
            this.panelThermoColumns.ResumeLayout(false);
            this.panelThermoColumns.PerformLayout();
            this.panelAbSciexTOF.ResumeLayout(false);
            this.panelAbSciexTOF.PerformLayout();
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
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelMethods;
        private System.Windows.Forms.Label labelMethodNum;
        private System.Windows.Forms.CheckBox cbTriggerRefColumns;
        private System.Windows.Forms.Panel panelThermoColumns;
        private System.Windows.Forms.Panel panelAbSciexTOF;
        private System.Windows.Forms.CheckBox cbExportMultiQuant;
    }
}
