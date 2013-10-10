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
            this.groupBoxRetentionTimeToKeep = new System.Windows.Forms.GroupBox();
            this.radioTimeAroundMs2Ids = new System.Windows.Forms.RadioButton();
            this.flowLayoutPanelTimeAroundMs2Ids = new System.Windows.Forms.FlowLayoutPanel();
            this.labelTimeAroundMs2Ids1 = new System.Windows.Forms.Label();
            this.tbxTimeAroundMs2Ids = new System.Windows.Forms.TextBox();
            this.labelTimeAroundMs2Ids2 = new System.Windows.Forms.Label();
            this.radioUseSchedulingWindow = new System.Windows.Forms.RadioButton();
            this.radioKeepAllTime = new System.Windows.Forms.RadioButton();
            this.groupBoxMS1 = new System.Windows.Forms.GroupBox();
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
            this.groupBoxRetentionTimeToKeep.SuspendLayout();
            this.flowLayoutPanelTimeAroundMs2Ids.SuspendLayout();
            this.groupBoxMS1.SuspendLayout();
            this.groupBoxMS2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxRetentionTimeToKeep
            // 
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.radioTimeAroundMs2Ids);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.flowLayoutPanelTimeAroundMs2Ids);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.radioUseSchedulingWindow);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.radioKeepAllTime);
            this.groupBoxRetentionTimeToKeep.Location = new System.Drawing.Point(17, 385);
            this.groupBoxRetentionTimeToKeep.Name = "groupBoxRetentionTimeToKeep";
            this.groupBoxRetentionTimeToKeep.Size = new System.Drawing.Size(326, 92);
            this.groupBoxRetentionTimeToKeep.TabIndex = 4;
            this.groupBoxRetentionTimeToKeep.TabStop = false;
            this.groupBoxRetentionTimeToKeep.Text = "Retention time filtering";
            // 
            // radioTimeAroundMs2Ids
            // 
            this.radioTimeAroundMs2Ids.AutoSize = true;
            this.radioTimeAroundMs2Ids.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.radioTimeAroundMs2Ids.Location = new System.Drawing.Point(14, 65);
            this.radioTimeAroundMs2Ids.Name = "radioTimeAroundMs2Ids";
            this.radioTimeAroundMs2Ids.Size = new System.Drawing.Size(14, 13);
            this.radioTimeAroundMs2Ids.TabIndex = 2;
            this.radioTimeAroundMs2Ids.TabStop = true;
            this.radioTimeAroundMs2Ids.UseVisualStyleBackColor = true;
            this.radioTimeAroundMs2Ids.CheckedChanged += new System.EventHandler(this.RadioNoiseAroundMs2IdsCheckedChanged);
            // 
            // flowLayoutPanelTimeAroundMs2Ids
            // 
            this.flowLayoutPanelTimeAroundMs2Ids.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flowLayoutPanelTimeAroundMs2Ids.Controls.Add(this.labelTimeAroundMs2Ids1);
            this.flowLayoutPanelTimeAroundMs2Ids.Controls.Add(this.tbxTimeAroundMs2Ids);
            this.flowLayoutPanelTimeAroundMs2Ids.Controls.Add(this.labelTimeAroundMs2Ids2);
            this.flowLayoutPanelTimeAroundMs2Ids.Location = new System.Drawing.Point(27, 63);
            this.flowLayoutPanelTimeAroundMs2Ids.Name = "flowLayoutPanelTimeAroundMs2Ids";
            this.flowLayoutPanelTimeAroundMs2Ids.Size = new System.Drawing.Size(303, 22);
            this.flowLayoutPanelTimeAroundMs2Ids.TabIndex = 2;
            // 
            // labelTimeAroundMs2Ids1
            // 
            this.labelTimeAroundMs2Ids1.AutoSize = true;
            this.labelTimeAroundMs2Ids1.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelTimeAroundMs2Ids1.Location = new System.Drawing.Point(3, 3);
            this.labelTimeAroundMs2Ids1.Margin = new System.Windows.Forms.Padding(3);
            this.labelTimeAroundMs2Ids1.Name = "labelTimeAroundMs2Ids1";
            this.labelTimeAroundMs2Ids1.Size = new System.Drawing.Size(109, 13);
            this.labelTimeAroundMs2Ids1.TabIndex = 3;
            this.labelTimeAroundMs2Ids1.Text = "Use only scans within";
            // 
            // tbxTimeAroundMs2Ids
            // 
            this.tbxTimeAroundMs2Ids.Location = new System.Drawing.Point(115, 0);
            this.tbxTimeAroundMs2Ids.Margin = new System.Windows.Forms.Padding(0);
            this.tbxTimeAroundMs2Ids.Name = "tbxTimeAroundMs2Ids";
            this.tbxTimeAroundMs2Ids.Size = new System.Drawing.Size(49, 20);
            this.tbxTimeAroundMs2Ids.TabIndex = 3;
            // 
            // labelTimeAroundMs2Ids2
            // 
            this.labelTimeAroundMs2Ids2.AutoSize = true;
            this.labelTimeAroundMs2Ids2.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelTimeAroundMs2Ids2.Location = new System.Drawing.Point(167, 3);
            this.labelTimeAroundMs2Ids2.Margin = new System.Windows.Forms.Padding(3);
            this.labelTimeAroundMs2Ids2.Name = "labelTimeAroundMs2Ids2";
            this.labelTimeAroundMs2Ids2.Size = new System.Drawing.Size(114, 13);
            this.labelTimeAroundMs2Ids2.TabIndex = 4;
            this.labelTimeAroundMs2Ids2.Text = "minutes of MS/MS IDs";
            // 
            // radioUseSchedulingWindow
            // 
            this.radioUseSchedulingWindow.AutoSize = true;
            this.radioUseSchedulingWindow.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.radioUseSchedulingWindow.Location = new System.Drawing.Point(14, 42);
            this.radioUseSchedulingWindow.Name = "radioUseSchedulingWindow";
            this.radioUseSchedulingWindow.Size = new System.Drawing.Size(272, 17);
            this.radioUseSchedulingWindow.TabIndex = 1;
            this.radioUseSchedulingWindow.TabStop = true;
            this.radioUseSchedulingWindow.Text = "Use only scans in retention time scheduling windows";
            this.radioUseSchedulingWindow.UseVisualStyleBackColor = true;
            // 
            // radioKeepAllTime
            // 
            this.radioKeepAllTime.AutoSize = true;
            this.radioKeepAllTime.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.radioKeepAllTime.Location = new System.Drawing.Point(14, 19);
            this.radioKeepAllTime.Name = "radioKeepAllTime";
            this.radioKeepAllTime.Size = new System.Drawing.Size(150, 17);
            this.radioKeepAllTime.TabIndex = 0;
            this.radioKeepAllTime.TabStop = true;
            this.radioKeepAllTime.Text = "Include all matching scans";
            this.radioKeepAllTime.UseVisualStyleBackColor = true;
            // 
            // groupBoxMS1
            // 
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
            this.groupBoxMS1.Location = new System.Drawing.Point(17, 13);
            this.groupBoxMS1.Name = "groupBoxMS1";
            this.groupBoxMS1.Size = new System.Drawing.Size(326, 211);
            this.groupBoxMS1.TabIndex = 2;
            this.groupBoxMS1.TabStop = false;
            this.groupBoxMS1.Text = "&MS1 filtering";
            // 
            // comboEnrichments
            // 
            this.comboEnrichments.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEnrichments.FormattingEnabled = true;
            this.comboEnrichments.Location = new System.Drawing.Point(14, 176);
            this.comboEnrichments.Name = "comboEnrichments";
            this.comboEnrichments.Size = new System.Drawing.Size(111, 21);
            this.comboEnrichments.TabIndex = 6;
            this.comboEnrichments.SelectedIndexChanged += new System.EventHandler(this.comboEnrichments_SelectedIndexChanged);
            // 
            // labelEnrichments
            // 
            this.labelEnrichments.AutoSize = true;
            this.labelEnrichments.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelEnrichments.Location = new System.Drawing.Point(14, 160);
            this.labelEnrichments.Name = "labelEnrichments";
            this.labelEnrichments.Size = new System.Drawing.Size(139, 13);
            this.labelEnrichments.TabIndex = 5;
            this.labelEnrichments.Text = "Isotope la&beling enrichment:";
            // 
            // labelPrecursorIsotopeFilterPercent
            // 
            this.labelPrecursorIsotopeFilterPercent.AutoSize = true;
            this.labelPrecursorIsotopeFilterPercent.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelPrecursorIsotopeFilterPercent.Location = new System.Drawing.Point(81, 114);
            this.labelPrecursorIsotopeFilterPercent.Name = "labelPrecursorIsotopeFilterPercent";
            this.labelPrecursorIsotopeFilterPercent.Size = new System.Drawing.Size(15, 13);
            this.labelPrecursorIsotopeFilterPercent.TabIndex = 4;
            this.labelPrecursorIsotopeFilterPercent.Text = "%";
            this.labelPrecursorIsotopeFilterPercent.Visible = false;
            // 
            // textPrecursorIsotopeFilter
            // 
            this.textPrecursorIsotopeFilter.Location = new System.Drawing.Point(14, 111);
            this.textPrecursorIsotopeFilter.Name = "textPrecursorIsotopeFilter";
            this.textPrecursorIsotopeFilter.Size = new System.Drawing.Size(65, 20);
            this.textPrecursorIsotopeFilter.TabIndex = 3;
            // 
            // labelPrecursorIsotopeFilter
            // 
            this.labelPrecursorIsotopeFilter.AutoSize = true;
            this.labelPrecursorIsotopeFilter.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelPrecursorIsotopeFilter.Location = new System.Drawing.Point(11, 94);
            this.labelPrecursorIsotopeFilter.Name = "labelPrecursorIsotopeFilter";
            this.labelPrecursorIsotopeFilter.Size = new System.Drawing.Size(40, 13);
            this.labelPrecursorIsotopeFilter.TabIndex = 2;
            this.labelPrecursorIsotopeFilter.Text = "Pea&ks:";
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label23.Location = new System.Drawing.Point(11, 28);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(120, 13);
            this.label23.TabIndex = 0;
            this.label23.Text = "&Isotope peaks included:";
            // 
            // comboPrecursorIsotopes
            // 
            this.comboPrecursorIsotopes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboPrecursorIsotopes.FormattingEnabled = true;
            this.comboPrecursorIsotopes.Location = new System.Drawing.Point(14, 45);
            this.comboPrecursorIsotopes.Name = "comboPrecursorIsotopes";
            this.comboPrecursorIsotopes.Size = new System.Drawing.Size(111, 21);
            this.comboPrecursorIsotopes.TabIndex = 1;
            this.comboPrecursorIsotopes.SelectedIndexChanged += new System.EventHandler(this.comboPrecursorIsotopes_SelectedIndexChanged);
            // 
            // labelPrecursorAt
            // 
            this.labelPrecursorAt.AutoSize = true;
            this.labelPrecursorAt.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelPrecursorAt.Location = new System.Drawing.Point(246, 95);
            this.labelPrecursorAt.Name = "labelPrecursorAt";
            this.labelPrecursorAt.Size = new System.Drawing.Size(20, 13);
            this.labelPrecursorAt.TabIndex = 11;
            this.labelPrecursorAt.Text = "&At:";
            // 
            // textPrecursorAt
            // 
            this.textPrecursorAt.Location = new System.Drawing.Point(244, 111);
            this.textPrecursorAt.Name = "textPrecursorAt";
            this.textPrecursorAt.Size = new System.Drawing.Size(44, 20);
            this.textPrecursorAt.TabIndex = 12;
            // 
            // labelPrecursorTh
            // 
            this.labelPrecursorTh.AutoSize = true;
            this.labelPrecursorTh.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelPrecursorTh.Location = new System.Drawing.Point(290, 114);
            this.labelPrecursorTh.Name = "labelPrecursorTh";
            this.labelPrecursorTh.Size = new System.Drawing.Size(20, 13);
            this.labelPrecursorTh.TabIndex = 13;
            this.labelPrecursorTh.Text = "Th";
            // 
            // textPrecursorRes
            // 
            this.textPrecursorRes.Enabled = false;
            this.textPrecursorRes.Location = new System.Drawing.Point(153, 111);
            this.textPrecursorRes.Name = "textPrecursorRes";
            this.textPrecursorRes.Size = new System.Drawing.Size(85, 20);
            this.textPrecursorRes.TabIndex = 10;
            // 
            // labelPrecursorRes
            // 
            this.labelPrecursorRes.AutoSize = true;
            this.labelPrecursorRes.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelPrecursorRes.Location = new System.Drawing.Point(150, 95);
            this.labelPrecursorRes.Name = "labelPrecursorRes";
            this.labelPrecursorRes.Size = new System.Drawing.Size(89, 13);
            this.labelPrecursorRes.TabIndex = 9;
            this.labelPrecursorRes.Text = "&Resolving power:";
            // 
            // comboPrecursorAnalyzerType
            // 
            this.comboPrecursorAnalyzerType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboPrecursorAnalyzerType.Enabled = false;
            this.comboPrecursorAnalyzerType.FormattingEnabled = true;
            this.comboPrecursorAnalyzerType.Location = new System.Drawing.Point(153, 45);
            this.comboPrecursorAnalyzerType.Name = "comboPrecursorAnalyzerType";
            this.comboPrecursorAnalyzerType.Size = new System.Drawing.Size(111, 21);
            this.comboPrecursorAnalyzerType.TabIndex = 8;
            this.comboPrecursorAnalyzerType.SelectedIndexChanged += new System.EventHandler(this.comboPrecursorAnalyzerType_SelectedIndexChanged);
            // 
            // label32
            // 
            this.label32.AutoSize = true;
            this.label32.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label32.Location = new System.Drawing.Point(150, 28);
            this.label32.Name = "label32";
            this.label32.Size = new System.Drawing.Size(124, 13);
            this.label32.TabIndex = 7;
            this.label32.Text = "&Precursor mass analyzer:";
            // 
            // groupBoxMS2
            // 
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
            this.groupBoxMS2.Location = new System.Drawing.Point(17, 232);
            this.groupBoxMS2.Name = "groupBoxMS2";
            this.groupBoxMS2.Size = new System.Drawing.Size(326, 145);
            this.groupBoxMS2.TabIndex = 3;
            this.groupBoxMS2.TabStop = false;
            this.groupBoxMS2.Text = "M&S/MS filtering";
            // 
            // comboIsolationScheme
            // 
            this.comboIsolationScheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIsolationScheme.FormattingEnabled = true;
            this.comboIsolationScheme.Location = new System.Drawing.Point(14, 110);
            this.comboIsolationScheme.Name = "comboIsolationScheme";
            this.comboIsolationScheme.Size = new System.Drawing.Size(111, 21);
            this.comboIsolationScheme.TabIndex = 14;
            this.comboIsolationScheme.SelectedIndexChanged += new System.EventHandler(this.comboIsolationScheme_SelectedIndexChanged);
            // 
            // labelProductAt
            // 
            this.labelProductAt.AutoSize = true;
            this.labelProductAt.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelProductAt.Location = new System.Drawing.Point(245, 94);
            this.labelProductAt.Name = "labelProductAt";
            this.labelProductAt.Size = new System.Drawing.Size(20, 13);
            this.labelProductAt.TabIndex = 11;
            this.labelProductAt.Text = "A&t:";
            // 
            // textProductAt
            // 
            this.textProductAt.Location = new System.Drawing.Point(244, 111);
            this.textProductAt.Name = "textProductAt";
            this.textProductAt.Size = new System.Drawing.Size(44, 20);
            this.textProductAt.TabIndex = 12;
            // 
            // labelProductTh
            // 
            this.labelProductTh.AutoSize = true;
            this.labelProductTh.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelProductTh.Location = new System.Drawing.Point(289, 114);
            this.labelProductTh.Name = "labelProductTh";
            this.labelProductTh.Size = new System.Drawing.Size(20, 13);
            this.labelProductTh.TabIndex = 13;
            this.labelProductTh.Text = "Th";
            // 
            // textProductRes
            // 
            this.textProductRes.Enabled = false;
            this.textProductRes.Location = new System.Drawing.Point(153, 111);
            this.textProductRes.Name = "textProductRes";
            this.textProductRes.Size = new System.Drawing.Size(85, 20);
            this.textProductRes.TabIndex = 10;
            // 
            // labelProductRes
            // 
            this.labelProductRes.AutoSize = true;
            this.labelProductRes.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelProductRes.Location = new System.Drawing.Point(150, 94);
            this.labelProductRes.Name = "labelProductRes";
            this.labelProductRes.Size = new System.Drawing.Size(89, 13);
            this.labelProductRes.TabIndex = 9;
            this.labelProductRes.Text = "Res&olving power:";
            // 
            // comboProductAnalyzerType
            // 
            this.comboProductAnalyzerType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboProductAnalyzerType.Enabled = false;
            this.comboProductAnalyzerType.FormattingEnabled = true;
            this.comboProductAnalyzerType.Location = new System.Drawing.Point(150, 45);
            this.comboProductAnalyzerType.Name = "comboProductAnalyzerType";
            this.comboProductAnalyzerType.Size = new System.Drawing.Size(111, 21);
            this.comboProductAnalyzerType.TabIndex = 8;
            this.comboProductAnalyzerType.SelectedIndexChanged += new System.EventHandler(this.comboProductAnalyzerType_SelectedIndexChanged);
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label22.Location = new System.Drawing.Point(147, 28);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(116, 13);
            this.label22.TabIndex = 7;
            this.label22.Text = "Product &mass analyzer:";
            // 
            // labelIsolationScheme
            // 
            this.labelIsolationScheme.AutoSize = true;
            this.labelIsolationScheme.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelIsolationScheme.Location = new System.Drawing.Point(11, 94);
            this.labelIsolationScheme.Name = "labelIsolationScheme";
            this.labelIsolationScheme.Size = new System.Drawing.Size(89, 13);
            this.labelIsolationScheme.TabIndex = 2;
            this.labelIsolationScheme.Text = "Iso&lation scheme:";
            // 
            // comboAcquisitionMethod
            // 
            this.comboAcquisitionMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAcquisitionMethod.FormattingEnabled = true;
            this.comboAcquisitionMethod.Location = new System.Drawing.Point(14, 45);
            this.comboAcquisitionMethod.Name = "comboAcquisitionMethod";
            this.comboAcquisitionMethod.Size = new System.Drawing.Size(111, 21);
            this.comboAcquisitionMethod.TabIndex = 1;
            this.comboAcquisitionMethod.SelectedIndexChanged += new System.EventHandler(this.comboAcquisitionMethod_SelectedIndexChanged);
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label20.Location = new System.Drawing.Point(11, 28);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(99, 13);
            this.label20.TabIndex = 0;
            this.label20.Text = "A&cquisition method:";
            // 
            // lblPrecursorCharges
            // 
            this.lblPrecursorCharges.AutoSize = true;
            this.lblPrecursorCharges.Location = new System.Drawing.Point(14, 480);
            this.lblPrecursorCharges.Name = "lblPrecursorCharges";
            this.lblPrecursorCharges.Size = new System.Drawing.Size(96, 13);
            this.lblPrecursorCharges.TabIndex = 0;
            this.lblPrecursorCharges.Text = "Precursor charges:";
            this.lblPrecursorCharges.Visible = false;
            // 
            // textPrecursorCharges
            // 
            this.textPrecursorCharges.Location = new System.Drawing.Point(17, 496);
            this.textPrecursorCharges.Name = "textPrecursorCharges";
            this.textPrecursorCharges.Size = new System.Drawing.Size(76, 20);
            this.textPrecursorCharges.TabIndex = 1;
            this.textPrecursorCharges.Visible = false;
            // 
            // FullScanSettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.textPrecursorCharges);
            this.Controls.Add(this.lblPrecursorCharges);
            this.Controls.Add(this.groupBoxRetentionTimeToKeep);
            this.Controls.Add(this.groupBoxMS1);
            this.Controls.Add(this.groupBoxMS2);
            this.Name = "FullScanSettingsControl";
            this.Size = new System.Drawing.Size(363, 521);
            this.groupBoxRetentionTimeToKeep.ResumeLayout(false);
            this.groupBoxRetentionTimeToKeep.PerformLayout();
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

    }
}
