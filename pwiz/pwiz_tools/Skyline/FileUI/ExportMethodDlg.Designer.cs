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
            this.comboOptimizing = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
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
            this.btnCancel.Location = new System.Drawing.Point(200, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 19;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(200, 11);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 18;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 292);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(69, 13);
            this.label2.TabIndex = 10;
            this.label2.Text = "Method &type:";
            // 
            // comboTargetType
            // 
            this.comboTargetType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTargetType.FormattingEnabled = true;
            this.comboTargetType.Items.AddRange(new object[] {
            "Standard",
            "Scheduled"});
            this.comboTargetType.Location = new System.Drawing.Point(16, 308);
            this.comboTargetType.Name = "comboTargetType";
            this.comboTargetType.Size = new System.Drawing.Size(124, 21);
            this.comboTargetType.TabIndex = 11;
            this.comboTargetType.SelectedIndexChanged += new System.EventHandler(this.comboTargetType_SelectedIndexChanged);
            // 
            // textRunLength
            // 
            this.textRunLength.Location = new System.Drawing.Point(164, 308);
            this.textRunLength.Name = "textRunLength";
            this.textRunLength.Size = new System.Drawing.Size(100, 20);
            this.textRunLength.TabIndex = 10;
            this.textRunLength.Visible = false;
            // 
            // textDwellTime
            // 
            this.textDwellTime.Location = new System.Drawing.Point(164, 308);
            this.textDwellTime.Name = "textDwellTime";
            this.textDwellTime.Size = new System.Drawing.Size(100, 20);
            this.textDwellTime.TabIndex = 13;
            this.textDwellTime.Visible = false;
            // 
            // labelDwellTime
            // 
            this.labelDwellTime.AutoSize = true;
            this.labelDwellTime.Location = new System.Drawing.Point(164, 289);
            this.labelDwellTime.Name = "labelDwellTime";
            this.labelDwellTime.Size = new System.Drawing.Size(80, 13);
            this.labelDwellTime.TabIndex = 12;
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
            this.labelTemplateFile.Location = new System.Drawing.Point(13, 359);
            this.labelTemplateFile.Name = "labelTemplateFile";
            this.labelTemplateFile.Size = new System.Drawing.Size(70, 13);
            this.labelTemplateFile.TabIndex = 15;
            this.labelTemplateFile.Text = "T&emplate file:";
            // 
            // textTemplateFile
            // 
            this.textTemplateFile.Location = new System.Drawing.Point(16, 377);
            this.textTemplateFile.Name = "textTemplateFile";
            this.textTemplateFile.Size = new System.Drawing.Size(175, 20);
            this.textTemplateFile.TabIndex = 16;
            // 
            // btnBrowseTemplate
            // 
            this.btnBrowseTemplate.Location = new System.Drawing.Point(197, 375);
            this.btnBrowseTemplate.Name = "btnBrowseTemplate";
            this.btnBrowseTemplate.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseTemplate.TabIndex = 17;
            this.btnBrowseTemplate.Text = "&Browse...";
            this.btnBrowseTemplate.UseVisualStyleBackColor = true;
            this.btnBrowseTemplate.Click += new System.EventHandler(this.btnBrowseTemplate_Click);
            // 
            // cbIgnoreProteins
            // 
            this.cbIgnoreProteins.AutoSize = true;
            this.cbIgnoreProteins.Enabled = false;
            this.cbIgnoreProteins.Location = new System.Drawing.Point(167, 118);
            this.cbIgnoreProteins.Name = "cbIgnoreProteins";
            this.cbIgnoreProteins.Size = new System.Drawing.Size(96, 17);
            this.cbIgnoreProteins.TabIndex = 5;
            this.cbIgnoreProteins.Text = "Ignore p&roteins";
            this.helpTip.SetToolTip(this.cbIgnoreProteins, resources.GetString("cbIgnoreProteins.ToolTip"));
            this.cbIgnoreProteins.UseVisualStyleBackColor = true;
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 10000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // cbEnergyRamp
            // 
            this.cbEnergyRamp.AutoSize = true;
            this.cbEnergyRamp.Location = new System.Drawing.Point(164, 334);
            this.cbEnergyRamp.Name = "cbEnergyRamp";
            this.cbEnergyRamp.Size = new System.Drawing.Size(106, 17);
            this.cbEnergyRamp.TabIndex = 14;
            this.cbEnergyRamp.Text = "&Add energy ramp";
            this.cbEnergyRamp.UseVisualStyleBackColor = true;
            this.cbEnergyRamp.Visible = false;
            // 
            // comboOptimizing
            // 
            this.comboOptimizing.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOptimizing.FormattingEnabled = true;
            this.comboOptimizing.Location = new System.Drawing.Point(16, 239);
            this.comboOptimizing.Name = "comboOptimizing";
            this.comboOptimizing.Size = new System.Drawing.Size(121, 21);
            this.comboOptimizing.TabIndex = 9;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 225);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 8;
            this.label1.Text = "Optimi&zing:";
            // 
            // ExportMethodDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(287, 409);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboOptimizing);
            this.Controls.Add(this.cbEnergyRamp);
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
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportMethodDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export Method";
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
    }
}