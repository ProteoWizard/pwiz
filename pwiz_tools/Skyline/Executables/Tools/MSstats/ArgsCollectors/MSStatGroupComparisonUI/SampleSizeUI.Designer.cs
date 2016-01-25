namespace MSStatArgsCollector
{
    partial class SampleSizeUi
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
            this.numberSamples = new System.Windows.Forms.NumericUpDown();
            this.labelFDR = new System.Windows.Forms.Label();
            this.labelLowerDesiredFC = new System.Windows.Forms.Label();
            this.labelUpperDesiredFC = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.gBoxFoldChange = new System.Windows.Forms.GroupBox();
            this.numberUDFC = new System.Windows.Forms.TextBox();
            this.numberLDFC = new System.Windows.Forms.TextBox();
            this.btnDefault = new System.Windows.Forms.Button();
            this.numberPower = new System.Windows.Forms.TextBox();
            this.numberFDR = new System.Windows.Forms.TextBox();
            this.rBtnSamples = new System.Windows.Forms.RadioButton();
            this.rBtnPower = new System.Windows.Forms.RadioButton();
            this.groupBoxAuto = new System.Windows.Forms.GroupBox();
            this.flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
            this.comboNormalizeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.cboxAllowMissingPeaks = new System.Windows.Forms.CheckBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.cbxSelectHighQualityFeatures = new System.Windows.Forms.CheckBox();
            this.cbxRemoveInterferedProteins = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.numberSamples)).BeginInit();
            this.gBoxFoldChange.SuspendLayout();
            this.groupBoxAuto.SuspendLayout();
            this.flowLayoutPanel2.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // numberSamples
            // 
            this.numberSamples.Location = new System.Drawing.Point(3, 26);
            this.numberSamples.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberSamples.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.numberSamples.Name = "numberSamples";
            this.numberSamples.Size = new System.Drawing.Size(60, 20);
            this.numberSamples.TabIndex = 1;
            this.numberSamples.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // labelFDR
            // 
            this.labelFDR.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelFDR.AutoSize = true;
            this.labelFDR.Location = new System.Drawing.Point(3, 252);
            this.labelFDR.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);
            this.labelFDR.Name = "labelFDR";
            this.labelFDR.Size = new System.Drawing.Size(32, 13);
            this.labelFDR.TabIndex = 4;
            this.labelFDR.Text = "FDR:";
            // 
            // labelLowerDesiredFC
            // 
            this.labelLowerDesiredFC.AutoSize = true;
            this.labelLowerDesiredFC.Location = new System.Drawing.Point(10, 26);
            this.labelLowerDesiredFC.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);
            this.labelLowerDesiredFC.Name = "labelLowerDesiredFC";
            this.labelLowerDesiredFC.Size = new System.Drawing.Size(39, 13);
            this.labelLowerDesiredFC.TabIndex = 0;
            this.labelLowerDesiredFC.Text = "Lower:";
            // 
            // labelUpperDesiredFC
            // 
            this.labelUpperDesiredFC.AutoSize = true;
            this.labelUpperDesiredFC.Location = new System.Drawing.Point(81, 26);
            this.labelUpperDesiredFC.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);
            this.labelUpperDesiredFC.Name = "labelUpperDesiredFC";
            this.labelUpperDesiredFC.Size = new System.Drawing.Size(39, 13);
            this.labelUpperDesiredFC.TabIndex = 1;
            this.labelUpperDesiredFC.Text = "Upper:";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(231, 12);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 8;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(231, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // gBoxFoldChange
            // 
            this.gBoxFoldChange.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.gBoxFoldChange.Controls.Add(this.numberUDFC);
            this.gBoxFoldChange.Controls.Add(this.numberLDFC);
            this.gBoxFoldChange.Controls.Add(this.labelLowerDesiredFC);
            this.gBoxFoldChange.Controls.Add(this.labelUpperDesiredFC);
            this.gBoxFoldChange.Location = new System.Drawing.Point(198, 3);
            this.gBoxFoldChange.Name = "gBoxFoldChange";
            this.gBoxFoldChange.Size = new System.Drawing.Size(154, 74);
            this.gBoxFoldChange.TabIndex = 6;
            this.gBoxFoldChange.TabStop = false;
            this.gBoxFoldChange.Text = "Desired fold change";
            // 
            // numberUDFC
            // 
            this.numberUDFC.Location = new System.Drawing.Point(84, 41);
            this.numberUDFC.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberUDFC.Name = "numberUDFC";
            this.numberUDFC.Size = new System.Drawing.Size(60, 20);
            this.numberUDFC.TabIndex = 3;
            this.numberUDFC.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // numberLDFC
            // 
            this.numberLDFC.Location = new System.Drawing.Point(12, 41);
            this.numberLDFC.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberLDFC.Name = "numberLDFC";
            this.numberLDFC.Size = new System.Drawing.Size(60, 20);
            this.numberLDFC.TabIndex = 2;
            this.numberLDFC.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // btnDefault
            // 
            this.btnDefault.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDefault.Location = new System.Drawing.Point(12, 414);
            this.btnDefault.Name = "btnDefault";
            this.btnDefault.Size = new System.Drawing.Size(93, 23);
            this.btnDefault.TabIndex = 7;
            this.btnDefault.Text = "Use Defaults";
            this.btnDefault.UseVisualStyleBackColor = true;
            this.btnDefault.Click += new System.EventHandler(this.btnDefault_Click);
            // 
            // numberPower
            // 
            this.numberPower.Location = new System.Drawing.Point(90, 3);
            this.numberPower.Name = "numberPower";
            this.numberPower.Size = new System.Drawing.Size(60, 20);
            this.numberPower.TabIndex = 7;
            this.numberPower.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // numberFDR
            // 
            this.numberFDR.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.numberFDR.Location = new System.Drawing.Point(3, 276);
            this.numberFDR.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberFDR.Name = "numberFDR";
            this.numberFDR.Size = new System.Drawing.Size(60, 20);
            this.numberFDR.TabIndex = 5;
            this.numberFDR.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // rBtnSamples
            // 
            this.rBtnSamples.AutoSize = true;
            this.rBtnSamples.Location = new System.Drawing.Point(3, 3);
            this.rBtnSamples.Name = "rBtnSamples";
            this.rBtnSamples.Size = new System.Drawing.Size(81, 17);
            this.rBtnSamples.TabIndex = 0;
            this.rBtnSamples.Text = "Sample size";
            this.rBtnSamples.UseVisualStyleBackColor = true;
            this.rBtnSamples.CheckedChanged += new System.EventHandler(this.rBtnSamples_CheckedChanged);
            // 
            // rBtnPower
            // 
            this.rBtnPower.AutoSize = true;
            this.rBtnPower.Location = new System.Drawing.Point(3, 61);
            this.rBtnPower.Name = "rBtnPower";
            this.rBtnPower.Size = new System.Drawing.Size(55, 17);
            this.rBtnPower.TabIndex = 6;
            this.rBtnPower.Text = "Power";
            this.rBtnPower.UseVisualStyleBackColor = true;
            this.rBtnPower.CheckedChanged += new System.EventHandler(this.rBtnPower_CheckedChanged);
            // 
            // groupBoxAuto
            // 
            this.groupBoxAuto.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.groupBoxAuto.Controls.Add(this.flowLayoutPanel2);
            this.groupBoxAuto.Location = new System.Drawing.Point(3, 138);
            this.groupBoxAuto.Name = "groupBoxAuto";
            this.groupBoxAuto.Size = new System.Drawing.Size(154, 111);
            this.groupBoxAuto.TabIndex = 3;
            this.groupBoxAuto.TabStop = false;
            this.groupBoxAuto.Text = "Automatically calculate";
            // 
            // flowLayoutPanel2
            // 
            this.flowLayoutPanel2.Controls.Add(this.rBtnSamples);
            this.flowLayoutPanel2.Controls.Add(this.numberSamples);
            this.flowLayoutPanel2.Controls.Add(this.rBtnPower);
            this.flowLayoutPanel2.Controls.Add(this.numberPower);
            this.flowLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel2.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanel2.Location = new System.Drawing.Point(3, 16);
            this.flowLayoutPanel2.Name = "flowLayoutPanel2";
            this.flowLayoutPanel2.Size = new System.Drawing.Size(148, 92);
            this.flowLayoutPanel2.TabIndex = 15;
            // 
            // comboNormalizeTo
            // 
            this.comboNormalizeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboNormalizeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboNormalizeTo.FormattingEnabled = true;
            this.comboNormalizeTo.Items.AddRange(new object[] {
            "None",
            "Equalize medians",
            "Quantile",
            "Relative to global standards"});
            this.comboNormalizeTo.Location = new System.Drawing.Point(3, 16);
            this.comboNormalizeTo.Name = "comboNormalizeTo";
            this.comboNormalizeTo.Size = new System.Drawing.Size(144, 21);
            this.comboNormalizeTo.TabIndex = 1;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(3, 0);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(111, 13);
            this.labelNormalizeTo.TabIndex = 0;
            this.labelNormalizeTo.Text = "Normalization method:";
            this.labelNormalizeTo.UseMnemonic = false;
            // 
            // cboxAllowMissingPeaks
            // 
            this.cboxAllowMissingPeaks.AutoSize = true;
            this.cboxAllowMissingPeaks.Location = new System.Drawing.Point(3, 43);
            this.cboxAllowMissingPeaks.Name = "cboxAllowMissingPeaks";
            this.cboxAllowMissingPeaks.Size = new System.Drawing.Size(120, 17);
            this.cboxAllowMissingPeaks.TabIndex = 2;
            this.cboxAllowMissingPeaks.Text = "&Allow missing peaks";
            this.cboxAllowMissingPeaks.UseVisualStyleBackColor = true;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.labelNormalizeTo);
            this.flowLayoutPanel1.Controls.Add(this.comboNormalizeTo);
            this.flowLayoutPanel1.Controls.Add(this.cboxAllowMissingPeaks);
            this.flowLayoutPanel1.Controls.Add(this.cbxSelectHighQualityFeatures);
            this.flowLayoutPanel1.Controls.Add(this.cbxRemoveInterferedProteins);
            this.flowLayoutPanel1.Controls.Add(this.groupBoxAuto);
            this.flowLayoutPanel1.Controls.Add(this.labelFDR);
            this.flowLayoutPanel1.Controls.Add(this.numberFDR);
            this.flowLayoutPanel1.Controls.Add(this.gBoxFoldChange);
            this.flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(12, 12);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(200, 385);
            this.flowLayoutPanel1.TabIndex = 14;
            // 
            // cbxSelectHighQualityFeatures
            // 
            this.cbxSelectHighQualityFeatures.AutoSize = true;
            this.cbxSelectHighQualityFeatures.Location = new System.Drawing.Point(3, 66);
            this.cbxSelectHighQualityFeatures.Name = "cbxSelectHighQualityFeatures";
            this.cbxSelectHighQualityFeatures.Size = new System.Drawing.Size(153, 17);
            this.cbxSelectHighQualityFeatures.TabIndex = 7;
            this.cbxSelectHighQualityFeatures.Text = "Select high quality features";
            this.cbxSelectHighQualityFeatures.UseVisualStyleBackColor = true;
            this.cbxSelectHighQualityFeatures.CheckedChanged += new System.EventHandler(this.cbxSelectHighQualityFeatures_CheckedChanged);
            // 
            // cbxRemoveInterferedProteins
            // 
            this.cbxRemoveInterferedProteins.AutoSize = true;
            this.cbxRemoveInterferedProteins.Enabled = false;
            this.cbxRemoveInterferedProteins.Location = new System.Drawing.Point(13, 89);
            this.cbxRemoveInterferedProteins.Margin = new System.Windows.Forms.Padding(13, 3, 3, 3);
            this.cbxRemoveInterferedProteins.Name = "cbxRemoveInterferedProteins";
            this.cbxRemoveInterferedProteins.Size = new System.Drawing.Size(179, 43);
            this.cbxRemoveInterferedProteins.TabIndex = 8;
            this.cbxRemoveInterferedProteins.Text = "Allow the algorithm to delete the \r\nwhole protein if all of its features \r\nhave i" +
    "nterference";
            this.cbxRemoveInterferedProteins.UseVisualStyleBackColor = true;
            // 
            // SampleSizeUi
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(313, 449);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.btnDefault);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SampleSizeUi";
            this.ShowIcon = false;
            this.Text = "MSstats Design Sample Size";
            ((System.ComponentModel.ISupportInitialize)(this.numberSamples)).EndInit();
            this.gBoxFoldChange.ResumeLayout(false);
            this.gBoxFoldChange.PerformLayout();
            this.groupBoxAuto.ResumeLayout(false);
            this.flowLayoutPanel2.ResumeLayout(false);
            this.flowLayoutPanel2.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NumericUpDown numberSamples;
        private System.Windows.Forms.Label labelFDR;
        private System.Windows.Forms.Label labelLowerDesiredFC;
        private System.Windows.Forms.Label labelUpperDesiredFC;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox gBoxFoldChange;
        private System.Windows.Forms.Button btnDefault;
        private System.Windows.Forms.TextBox numberPower;
        private System.Windows.Forms.TextBox numberFDR;
        private System.Windows.Forms.TextBox numberLDFC;
        private System.Windows.Forms.TextBox numberUDFC;
        private System.Windows.Forms.RadioButton rBtnSamples;
        private System.Windows.Forms.RadioButton rBtnPower;
        private System.Windows.Forms.GroupBox groupBoxAuto;
        private System.Windows.Forms.ComboBox comboNormalizeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.CheckBox cboxAllowMissingPeaks;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2;
        private System.Windows.Forms.CheckBox cbxSelectHighQualityFeatures;
        private System.Windows.Forms.CheckBox cbxRemoveInterferedProteins;
    }
}
