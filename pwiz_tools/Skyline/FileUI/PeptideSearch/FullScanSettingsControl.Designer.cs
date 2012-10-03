namespace pwiz.Skyline.FileUI.PeptideSearch
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
            this.groupBoxMS1 = new System.Windows.Forms.GroupBox();
            this.comboEnrichments = new System.Windows.Forms.ComboBox();
            this.label29 = new System.Windows.Forms.Label();
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
            this.groupBoxRetentionTimeToKeep = new System.Windows.Forms.GroupBox();
            this.radioTimeAroundMs2Ids = new System.Windows.Forms.RadioButton();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.label35 = new System.Windows.Forms.Label();
            this.tbxTimeAroundMs2Ids = new System.Windows.Forms.TextBox();
            this.label27 = new System.Windows.Forms.Label();
            this.radioUseSchedulingWindow = new System.Windows.Forms.RadioButton();
            this.radioKeepAllTime = new System.Windows.Forms.RadioButton();
            this.groupBoxMS1.SuspendLayout();
            this.groupBoxRetentionTimeToKeep.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxMS1
            // 
            this.groupBoxMS1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxMS1.Controls.Add(this.comboEnrichments);
            this.groupBoxMS1.Controls.Add(this.label29);
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
            this.groupBoxMS1.Location = new System.Drawing.Point(17, 3);
            this.groupBoxMS1.Name = "groupBoxMS1";
            this.groupBoxMS1.Size = new System.Drawing.Size(345, 203);
            this.groupBoxMS1.TabIndex = 0;
            this.groupBoxMS1.TabStop = false;
            this.groupBoxMS1.Text = "&MS1 filtering";
            // 
            // comboEnrichments
            // 
            this.comboEnrichments.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEnrichments.FormattingEnabled = true;
            this.comboEnrichments.Location = new System.Drawing.Point(14, 168);
            this.comboEnrichments.Name = "comboEnrichments";
            this.comboEnrichments.Size = new System.Drawing.Size(111, 21);
            this.comboEnrichments.TabIndex = 6;
            this.comboEnrichments.SelectedIndexChanged += new System.EventHandler(this.comboEnrichments_SelectedIndexChanged);
            // 
            // label29
            // 
            this.label29.AutoSize = true;
            this.label29.Location = new System.Drawing.Point(11, 152);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(139, 13);
            this.label29.TabIndex = 5;
            this.label29.Text = "Isotope la&beling enrichment:";
            // 
            // labelPrecursorIsotopeFilterPercent
            // 
            this.labelPrecursorIsotopeFilterPercent.AutoSize = true;
            this.labelPrecursorIsotopeFilterPercent.Location = new System.Drawing.Point(81, 108);
            this.labelPrecursorIsotopeFilterPercent.Name = "labelPrecursorIsotopeFilterPercent";
            this.labelPrecursorIsotopeFilterPercent.Size = new System.Drawing.Size(15, 13);
            this.labelPrecursorIsotopeFilterPercent.TabIndex = 4;
            this.labelPrecursorIsotopeFilterPercent.Text = "%";
            this.labelPrecursorIsotopeFilterPercent.Visible = false;
            // 
            // textPrecursorIsotopeFilter
            // 
            this.textPrecursorIsotopeFilter.Location = new System.Drawing.Point(14, 105);
            this.textPrecursorIsotopeFilter.Name = "textPrecursorIsotopeFilter";
            this.textPrecursorIsotopeFilter.Size = new System.Drawing.Size(65, 20);
            this.textPrecursorIsotopeFilter.TabIndex = 3;
            // 
            // labelPrecursorIsotopeFilter
            // 
            this.labelPrecursorIsotopeFilter.AutoSize = true;
            this.labelPrecursorIsotopeFilter.Location = new System.Drawing.Point(11, 88);
            this.labelPrecursorIsotopeFilter.Name = "labelPrecursorIsotopeFilter";
            this.labelPrecursorIsotopeFilter.Size = new System.Drawing.Size(40, 13);
            this.labelPrecursorIsotopeFilter.TabIndex = 2;
            this.labelPrecursorIsotopeFilter.Text = "Pea&ks:";
            // 
            // label23
            // 
            this.label23.AutoSize = true;
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
            this.labelPrecursorAt.Location = new System.Drawing.Point(241, 89);
            this.labelPrecursorAt.Name = "labelPrecursorAt";
            this.labelPrecursorAt.Size = new System.Drawing.Size(20, 13);
            this.labelPrecursorAt.TabIndex = 11;
            this.labelPrecursorAt.Text = "&At:";
            // 
            // textPrecursorAt
            // 
            this.textPrecursorAt.Location = new System.Drawing.Point(244, 105);
            this.textPrecursorAt.Name = "textPrecursorAt";
            this.textPrecursorAt.Size = new System.Drawing.Size(44, 20);
            this.textPrecursorAt.TabIndex = 12;
            // 
            // labelPrecursorTh
            // 
            this.labelPrecursorTh.AutoSize = true;
            this.labelPrecursorTh.Location = new System.Drawing.Point(290, 108);
            this.labelPrecursorTh.Name = "labelPrecursorTh";
            this.labelPrecursorTh.Size = new System.Drawing.Size(20, 13);
            this.labelPrecursorTh.TabIndex = 13;
            this.labelPrecursorTh.Text = "Th";
            // 
            // textPrecursorRes
            // 
            this.textPrecursorRes.Enabled = false;
            this.textPrecursorRes.Location = new System.Drawing.Point(153, 105);
            this.textPrecursorRes.Name = "textPrecursorRes";
            this.textPrecursorRes.Size = new System.Drawing.Size(85, 20);
            this.textPrecursorRes.TabIndex = 10;
            // 
            // labelPrecursorRes
            // 
            this.labelPrecursorRes.AutoSize = true;
            this.labelPrecursorRes.Location = new System.Drawing.Point(150, 89);
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
            this.label32.Location = new System.Drawing.Point(150, 28);
            this.label32.Name = "label32";
            this.label32.Size = new System.Drawing.Size(124, 13);
            this.label32.TabIndex = 7;
            this.label32.Text = "&Precursor mass analyzer:";
            // 
            // groupBoxRetentionTimeToKeep
            // 
            this.groupBoxRetentionTimeToKeep.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.radioTimeAroundMs2Ids);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.flowLayoutPanel1);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.radioUseSchedulingWindow);
            this.groupBoxRetentionTimeToKeep.Controls.Add(this.radioKeepAllTime);
            this.groupBoxRetentionTimeToKeep.Location = new System.Drawing.Point(17, 208);
            this.groupBoxRetentionTimeToKeep.Name = "groupBoxRetentionTimeToKeep";
            this.groupBoxRetentionTimeToKeep.Size = new System.Drawing.Size(345, 96);
            this.groupBoxRetentionTimeToKeep.TabIndex = 1;
            this.groupBoxRetentionTimeToKeep.TabStop = false;
            this.groupBoxRetentionTimeToKeep.Text = "Retention time filtering";
            // 
            // radioTimeAroundMs2Ids
            // 
            this.radioTimeAroundMs2Ids.AutoSize = true;
            this.radioTimeAroundMs2Ids.Location = new System.Drawing.Point(14, 65);
            this.radioTimeAroundMs2Ids.Name = "radioTimeAroundMs2Ids";
            this.radioTimeAroundMs2Ids.Size = new System.Drawing.Size(14, 13);
            this.radioTimeAroundMs2Ids.TabIndex = 2;
            this.radioTimeAroundMs2Ids.TabStop = true;
            this.radioTimeAroundMs2Ids.UseVisualStyleBackColor = true;
            this.radioTimeAroundMs2Ids.CheckedChanged += new System.EventHandler(this.RadioNoiseAroundMs2IdsCheckedChanged);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flowLayoutPanel1.Controls.Add(this.label35);
            this.flowLayoutPanel1.Controls.Add(this.tbxTimeAroundMs2Ids);
            this.flowLayoutPanel1.Controls.Add(this.label27);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(27, 63);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(322, 22);
            this.flowLayoutPanel1.TabIndex = 2;
            // 
            // label35
            // 
            this.label35.AutoSize = true;
            this.label35.Location = new System.Drawing.Point(3, 3);
            this.label35.Margin = new System.Windows.Forms.Padding(3);
            this.label35.Name = "label35";
            this.label35.Size = new System.Drawing.Size(109, 13);
            this.label35.TabIndex = 3;
            this.label35.Text = "Use only scans within";
            // 
            // tbxTimeAroundMs2Ids
            // 
            this.tbxTimeAroundMs2Ids.Location = new System.Drawing.Point(115, 0);
            this.tbxTimeAroundMs2Ids.Margin = new System.Windows.Forms.Padding(0);
            this.tbxTimeAroundMs2Ids.Name = "tbxTimeAroundMs2Ids";
            this.tbxTimeAroundMs2Ids.Size = new System.Drawing.Size(49, 20);
            this.tbxTimeAroundMs2Ids.TabIndex = 3;
            // 
            // label27
            // 
            this.label27.AutoSize = true;
            this.label27.Location = new System.Drawing.Point(167, 3);
            this.label27.Margin = new System.Windows.Forms.Padding(3);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(114, 13);
            this.label27.TabIndex = 4;
            this.label27.Text = "minutes of MS/MS IDs";
            // 
            // radioUseSchedulingWindow
            // 
            this.radioUseSchedulingWindow.AutoSize = true;
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
            this.radioKeepAllTime.Location = new System.Drawing.Point(14, 19);
            this.radioKeepAllTime.Name = "radioKeepAllTime";
            this.radioKeepAllTime.Size = new System.Drawing.Size(150, 17);
            this.radioKeepAllTime.TabIndex = 0;
            this.radioKeepAllTime.TabStop = true;
            this.radioKeepAllTime.Text = "Include all matching scans";
            this.radioKeepAllTime.UseVisualStyleBackColor = true;
            // 
            // FullScanSettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.groupBoxRetentionTimeToKeep);
            this.Controls.Add(this.groupBoxMS1);
            this.Name = "FullScanSettingsControl";
            this.Size = new System.Drawing.Size(381, 307);
            this.groupBoxMS1.ResumeLayout(false);
            this.groupBoxMS1.PerformLayout();
            this.groupBoxRetentionTimeToKeep.ResumeLayout(false);
            this.groupBoxRetentionTimeToKeep.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxMS1;
        private System.Windows.Forms.ComboBox comboEnrichments;
        private System.Windows.Forms.Label label29;
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
        private System.Windows.Forms.GroupBox groupBoxRetentionTimeToKeep;
        private System.Windows.Forms.RadioButton radioTimeAroundMs2Ids;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Label label35;
        private System.Windows.Forms.TextBox tbxTimeAroundMs2Ids;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.RadioButton radioUseSchedulingWindow;
        private System.Windows.Forms.RadioButton radioKeepAllTime;
    }
}
