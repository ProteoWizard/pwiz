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
            this.numberPeptides = new System.Windows.Forms.NumericUpDown();
            this.numberTransitions = new System.Windows.Forms.NumericUpDown();
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
            this.rBtnPeptides = new System.Windows.Forms.RadioButton();
            this.rBtnTransitions = new System.Windows.Forms.RadioButton();
            this.rBtnPower = new System.Windows.Forms.RadioButton();
            this.groupBoxAuto = new System.Windows.Forms.GroupBox();
            this.comboBoxNoramilzeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.cboxAllowMissingPeaks = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.numberSamples)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numberPeptides)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numberTransitions)).BeginInit();
            this.gBoxFoldChange.SuspendLayout();
            this.groupBoxAuto.SuspendLayout();
            this.SuspendLayout();
            // 
            // numberSamples
            // 
            this.numberSamples.Location = new System.Drawing.Point(45, 47);
            this.numberSamples.Margin = new System.Windows.Forms.Padding(4, 4, 4, 15);
            this.numberSamples.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.numberSamples.Name = "numberSamples";
            this.numberSamples.Size = new System.Drawing.Size(80, 22);
            this.numberSamples.TabIndex = 1;
            this.numberSamples.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // numberPeptides
            // 
            this.numberPeptides.Location = new System.Drawing.Point(45, 118);
            this.numberPeptides.Margin = new System.Windows.Forms.Padding(4, 4, 4, 15);
            this.numberPeptides.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numberPeptides.Name = "numberPeptides";
            this.numberPeptides.Size = new System.Drawing.Size(80, 22);
            this.numberPeptides.TabIndex = 3;
            this.numberPeptides.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // numberTransitions
            // 
            this.numberTransitions.Location = new System.Drawing.Point(45, 190);
            this.numberTransitions.Margin = new System.Windows.Forms.Padding(4, 4, 4, 15);
            this.numberTransitions.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numberTransitions.Name = "numberTransitions";
            this.numberTransitions.Size = new System.Drawing.Size(80, 22);
            this.numberTransitions.TabIndex = 5;
            this.numberTransitions.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // labelFDR
            // 
            this.labelFDR.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelFDR.AutoSize = true;
            this.labelFDR.Location = new System.Drawing.Point(28, 414);
            this.labelFDR.Margin = new System.Windows.Forms.Padding(4, 0, 4, 10);
            this.labelFDR.Name = "labelFDR";
            this.labelFDR.Size = new System.Drawing.Size(40, 17);
            this.labelFDR.TabIndex = 4;
            this.labelFDR.Text = "FDR:";
            // 
            // labelLowerDesiredFC
            // 
            this.labelLowerDesiredFC.AutoSize = true;
            this.labelLowerDesiredFC.Location = new System.Drawing.Point(13, 32);
            this.labelLowerDesiredFC.Margin = new System.Windows.Forms.Padding(4, 0, 4, 10);
            this.labelLowerDesiredFC.Name = "labelLowerDesiredFC";
            this.labelLowerDesiredFC.Size = new System.Drawing.Size(50, 17);
            this.labelLowerDesiredFC.TabIndex = 0;
            this.labelLowerDesiredFC.Text = "Lower:";
            // 
            // labelUpperDesiredFC
            // 
            this.labelUpperDesiredFC.AutoSize = true;
            this.labelUpperDesiredFC.Location = new System.Drawing.Point(108, 32);
            this.labelUpperDesiredFC.Margin = new System.Windows.Forms.Padding(4, 0, 4, 10);
            this.labelUpperDesiredFC.Name = "labelUpperDesiredFC";
            this.labelUpperDesiredFC.Size = new System.Drawing.Size(51, 17);
            this.labelUpperDesiredFC.TabIndex = 1;
            this.labelUpperDesiredFC.Text = "Upper:";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(235, 15);
            this.btnOK.Margin = new System.Windows.Forms.Padding(4);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 28);
            this.btnOK.TabIndex = 8;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(235, 50);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 28);
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
            this.gBoxFoldChange.Location = new System.Drawing.Point(16, 476);
            this.gBoxFoldChange.Margin = new System.Windows.Forms.Padding(4);
            this.gBoxFoldChange.Name = "gBoxFoldChange";
            this.gBoxFoldChange.Padding = new System.Windows.Forms.Padding(4);
            this.gBoxFoldChange.Size = new System.Drawing.Size(205, 91);
            this.gBoxFoldChange.TabIndex = 6;
            this.gBoxFoldChange.TabStop = false;
            this.gBoxFoldChange.Text = "Desired fold change";
            // 
            // numberUDFC
            // 
            this.numberUDFC.Location = new System.Drawing.Point(112, 50);
            this.numberUDFC.Margin = new System.Windows.Forms.Padding(4, 4, 4, 15);
            this.numberUDFC.Name = "numberUDFC";
            this.numberUDFC.Size = new System.Drawing.Size(79, 22);
            this.numberUDFC.TabIndex = 3;
            this.numberUDFC.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // numberLDFC
            // 
            this.numberLDFC.Location = new System.Drawing.Point(16, 50);
            this.numberLDFC.Margin = new System.Windows.Forms.Padding(4, 4, 4, 15);
            this.numberLDFC.Name = "numberLDFC";
            this.numberLDFC.Size = new System.Drawing.Size(79, 22);
            this.numberLDFC.TabIndex = 2;
            this.numberLDFC.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // btnDefault
            // 
            this.btnDefault.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDefault.Location = new System.Drawing.Point(57, 574);
            this.btnDefault.Margin = new System.Windows.Forms.Padding(4);
            this.btnDefault.Name = "btnDefault";
            this.btnDefault.Size = new System.Drawing.Size(124, 28);
            this.btnDefault.TabIndex = 7;
            this.btnDefault.Text = "Use Defaults";
            this.btnDefault.UseVisualStyleBackColor = true;
            this.btnDefault.Click += new System.EventHandler(this.btnDefault_Click);
            // 
            // numberPower
            // 
            this.numberPower.Location = new System.Drawing.Point(45, 261);
            this.numberPower.Margin = new System.Windows.Forms.Padding(4);
            this.numberPower.Name = "numberPower";
            this.numberPower.Size = new System.Drawing.Size(79, 22);
            this.numberPower.TabIndex = 7;
            this.numberPower.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // numberFDR
            // 
            this.numberFDR.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.numberFDR.Location = new System.Drawing.Point(32, 433);
            this.numberFDR.Margin = new System.Windows.Forms.Padding(4, 4, 4, 15);
            this.numberFDR.Name = "numberFDR";
            this.numberFDR.Size = new System.Drawing.Size(79, 22);
            this.numberFDR.TabIndex = 5;
            this.numberFDR.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // rBtnSamples
            // 
            this.rBtnSamples.AutoSize = true;
            this.rBtnSamples.Location = new System.Drawing.Point(17, 23);
            this.rBtnSamples.Margin = new System.Windows.Forms.Padding(4);
            this.rBtnSamples.Name = "rBtnSamples";
            this.rBtnSamples.Size = new System.Drawing.Size(105, 21);
            this.rBtnSamples.TabIndex = 0;
            this.rBtnSamples.Text = "Sample size";
            this.rBtnSamples.UseVisualStyleBackColor = true;
            this.rBtnSamples.CheckedChanged += new System.EventHandler(this.rBtnSamples_CheckedChanged);
            // 
            // rBtnPeptides
            // 
            this.rBtnPeptides.AutoSize = true;
            this.rBtnPeptides.Location = new System.Drawing.Point(17, 95);
            this.rBtnPeptides.Margin = new System.Windows.Forms.Padding(4);
            this.rBtnPeptides.Name = "rBtnPeptides";
            this.rBtnPeptides.Size = new System.Drawing.Size(157, 21);
            this.rBtnPeptides.TabIndex = 2;
            this.rBtnPeptides.Text = "Peptides per protein";
            this.rBtnPeptides.UseVisualStyleBackColor = true;
            this.rBtnPeptides.CheckedChanged += new System.EventHandler(this.rBtnPeptides_CheckedChanged);
            // 
            // rBtnTransitions
            // 
            this.rBtnTransitions.AutoSize = true;
            this.rBtnTransitions.Location = new System.Drawing.Point(17, 166);
            this.rBtnTransitions.Margin = new System.Windows.Forms.Padding(4);
            this.rBtnTransitions.Name = "rBtnTransitions";
            this.rBtnTransitions.Size = new System.Drawing.Size(175, 21);
            this.rBtnTransitions.TabIndex = 4;
            this.rBtnTransitions.Text = "Transitions per peptide";
            this.rBtnTransitions.UseVisualStyleBackColor = true;
            this.rBtnTransitions.CheckedChanged += new System.EventHandler(this.rBtnTransitions_CheckedChanged);
            // 
            // rBtnPower
            // 
            this.rBtnPower.AutoSize = true;
            this.rBtnPower.Location = new System.Drawing.Point(17, 238);
            this.rBtnPower.Margin = new System.Windows.Forms.Padding(4);
            this.rBtnPower.Name = "rBtnPower";
            this.rBtnPower.Size = new System.Drawing.Size(68, 21);
            this.rBtnPower.TabIndex = 6;
            this.rBtnPower.Text = "Power";
            this.rBtnPower.UseVisualStyleBackColor = true;
            this.rBtnPower.CheckedChanged += new System.EventHandler(this.rBtnPower_CheckedChanged);
            // 
            // groupBoxAuto
            // 
            this.groupBoxAuto.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBoxAuto.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.groupBoxAuto.Controls.Add(this.numberPeptides);
            this.groupBoxAuto.Controls.Add(this.numberSamples);
            this.groupBoxAuto.Controls.Add(this.numberPower);
            this.groupBoxAuto.Controls.Add(this.rBtnSamples);
            this.groupBoxAuto.Controls.Add(this.rBtnPower);
            this.groupBoxAuto.Controls.Add(this.rBtnTransitions);
            this.groupBoxAuto.Controls.Add(this.rBtnPeptides);
            this.groupBoxAuto.Controls.Add(this.numberTransitions);
            this.groupBoxAuto.Location = new System.Drawing.Point(16, 97);
            this.groupBoxAuto.Margin = new System.Windows.Forms.Padding(4);
            this.groupBoxAuto.Name = "groupBoxAuto";
            this.groupBoxAuto.Padding = new System.Windows.Forms.Padding(4);
            this.groupBoxAuto.Size = new System.Drawing.Size(205, 299);
            this.groupBoxAuto.TabIndex = 3;
            this.groupBoxAuto.TabStop = false;
            this.groupBoxAuto.Text = "Automatically calculate";
            // 
            // comboBoxNoramilzeTo
            // 
            this.comboBoxNoramilzeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNoramilzeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxNoramilzeTo.FormattingEnabled = true;
            this.comboBoxNoramilzeTo.Items.AddRange(new object[] {
            "None",
            "Equalize medians",
            "Quantile",
            "Relative to global standards"});
            this.comboBoxNoramilzeTo.Location = new System.Drawing.Point(16, 37);
            this.comboBoxNoramilzeTo.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxNoramilzeTo.Name = "comboBoxNoramilzeTo";
            this.comboBoxNoramilzeTo.Size = new System.Drawing.Size(191, 24);
            this.comboBoxNoramilzeTo.TabIndex = 1;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(16, 15);
            this.labelNormalizeTo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(149, 17);
            this.labelNormalizeTo.TabIndex = 0;
            this.labelNormalizeTo.Text = "Normalization method:";
            this.labelNormalizeTo.UseMnemonic = false;
            // 
            // cboxAllowMissingPeaks
            // 
            this.cboxAllowMissingPeaks.AutoSize = true;
            this.cboxAllowMissingPeaks.Location = new System.Drawing.Point(17, 69);
            this.cboxAllowMissingPeaks.Margin = new System.Windows.Forms.Padding(4);
            this.cboxAllowMissingPeaks.Name = "cboxAllowMissingPeaks";
            this.cboxAllowMissingPeaks.Size = new System.Drawing.Size(155, 21);
            this.cboxAllowMissingPeaks.TabIndex = 2;
            this.cboxAllowMissingPeaks.Text = "&Allow missing peaks";
            this.cboxAllowMissingPeaks.UseVisualStyleBackColor = true;
            // 
            // SampleSizeUi
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(344, 615);
            this.Controls.Add(this.cboxAllowMissingPeaks);
            this.Controls.Add(this.comboBoxNoramilzeTo);
            this.Controls.Add(this.labelNormalizeTo);
            this.Controls.Add(this.groupBoxAuto);
            this.Controls.Add(this.numberFDR);
            this.Controls.Add(this.btnDefault);
            this.Controls.Add(this.gBoxFoldChange);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.labelFDR);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SampleSizeUi";
            this.ShowIcon = false;
            this.Text = "MSstats Design Sample Size";
            ((System.ComponentModel.ISupportInitialize)(this.numberSamples)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numberPeptides)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numberTransitions)).EndInit();
            this.gBoxFoldChange.ResumeLayout(false);
            this.gBoxFoldChange.PerformLayout();
            this.groupBoxAuto.ResumeLayout(false);
            this.groupBoxAuto.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown numberSamples;
        private System.Windows.Forms.NumericUpDown numberPeptides;
        private System.Windows.Forms.NumericUpDown numberTransitions;
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
        private System.Windows.Forms.RadioButton rBtnPeptides;
        private System.Windows.Forms.RadioButton rBtnTransitions;
        private System.Windows.Forms.RadioButton rBtnPower;
        private System.Windows.Forms.GroupBox groupBoxAuto;
        private System.Windows.Forms.ComboBox comboBoxNoramilzeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.CheckBox cboxAllowMissingPeaks;
    }
}
