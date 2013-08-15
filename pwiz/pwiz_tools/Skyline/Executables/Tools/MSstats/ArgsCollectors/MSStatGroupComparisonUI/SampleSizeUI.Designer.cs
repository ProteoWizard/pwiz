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
            ((System.ComponentModel.ISupportInitialize)(this.numberSamples)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numberPeptides)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numberTransitions)).BeginInit();
            this.gBoxFoldChange.SuspendLayout();
            this.groupBoxAuto.SuspendLayout();
            this.SuspendLayout();
            // 
            // numberSamples
            // 
            this.numberSamples.Location = new System.Drawing.Point(34, 38);
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
            // numberPeptides
            // 
            this.numberPeptides.Location = new System.Drawing.Point(34, 96);
            this.numberPeptides.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberPeptides.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numberPeptides.Name = "numberPeptides";
            this.numberPeptides.Size = new System.Drawing.Size(60, 20);
            this.numberPeptides.TabIndex = 3;
            this.numberPeptides.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // numberTransitions
            // 
            this.numberTransitions.Location = new System.Drawing.Point(34, 154);
            this.numberTransitions.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberTransitions.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numberTransitions.Name = "numberTransitions";
            this.numberTransitions.Size = new System.Drawing.Size(60, 20);
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
            this.labelFDR.Location = new System.Drawing.Point(21, 279);
            this.labelFDR.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);
            this.labelFDR.Name = "labelFDR";
            this.labelFDR.Size = new System.Drawing.Size(32, 13);
            this.labelFDR.TabIndex = 1;
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
            this.labelUpperDesiredFC.TabIndex = 2;
            this.labelUpperDesiredFC.Text = "Upper:";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(176, 12);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 5;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(176, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
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
            this.gBoxFoldChange.Location = new System.Drawing.Point(12, 329);
            this.gBoxFoldChange.Name = "gBoxFoldChange";
            this.gBoxFoldChange.Size = new System.Drawing.Size(154, 74);
            this.gBoxFoldChange.TabIndex = 3;
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
            this.numberUDFC.Text = "1.75";
            this.numberUDFC.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // numberLDFC
            // 
            this.numberLDFC.Location = new System.Drawing.Point(12, 41);
            this.numberLDFC.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberLDFC.Name = "numberLDFC";
            this.numberLDFC.Size = new System.Drawing.Size(60, 20);
            this.numberLDFC.TabIndex = 1;
            this.numberLDFC.Text = "1.25";
            this.numberLDFC.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // btnDefault
            // 
            this.btnDefault.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDefault.Location = new System.Drawing.Point(43, 409);
            this.btnDefault.Name = "btnDefault";
            this.btnDefault.Size = new System.Drawing.Size(93, 23);
            this.btnDefault.TabIndex = 4;
            this.btnDefault.Text = "Use Defaults";
            this.btnDefault.UseVisualStyleBackColor = true;
            this.btnDefault.Click += new System.EventHandler(this.btnDefault_Click);
            // 
            // numberPower
            // 
            this.numberPower.Location = new System.Drawing.Point(34, 212);
            this.numberPower.Name = "numberPower";
            this.numberPower.Size = new System.Drawing.Size(60, 20);
            this.numberPower.TabIndex = 7;
            this.numberPower.Text = "0.80";
            this.numberPower.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // numberFDR
            // 
            this.numberFDR.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.numberFDR.Location = new System.Drawing.Point(24, 294);
            this.numberFDR.Margin = new System.Windows.Forms.Padding(3, 3, 3, 12);
            this.numberFDR.Name = "numberFDR";
            this.numberFDR.Size = new System.Drawing.Size(60, 20);
            this.numberFDR.TabIndex = 2;
            this.numberFDR.Text = "0.05";
            this.numberFDR.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.numericTextBox_KeyPress);
            // 
            // rBtnSamples
            // 
            this.rBtnSamples.AutoSize = true;
            this.rBtnSamples.Location = new System.Drawing.Point(13, 19);
            this.rBtnSamples.Name = "rBtnSamples";
            this.rBtnSamples.Size = new System.Drawing.Size(81, 17);
            this.rBtnSamples.TabIndex = 0;
            this.rBtnSamples.Text = "Sample size";
            this.rBtnSamples.UseVisualStyleBackColor = true;
            this.rBtnSamples.CheckedChanged += new System.EventHandler(this.rBtnSamples_CheckedChanged);
            // 
            // rBtnPeptides
            // 
            this.rBtnPeptides.AutoSize = true;
            this.rBtnPeptides.Location = new System.Drawing.Point(13, 77);
            this.rBtnPeptides.Name = "rBtnPeptides";
            this.rBtnPeptides.Size = new System.Drawing.Size(119, 17);
            this.rBtnPeptides.TabIndex = 2;
            this.rBtnPeptides.Text = "Peptides per protein";
            this.rBtnPeptides.UseVisualStyleBackColor = true;
            this.rBtnPeptides.CheckedChanged += new System.EventHandler(this.rBtnPeptides_CheckedChanged);
            // 
            // rBtnTransitions
            // 
            this.rBtnTransitions.AutoSize = true;
            this.rBtnTransitions.Location = new System.Drawing.Point(13, 135);
            this.rBtnTransitions.Name = "rBtnTransitions";
            this.rBtnTransitions.Size = new System.Drawing.Size(132, 17);
            this.rBtnTransitions.TabIndex = 4;
            this.rBtnTransitions.Text = "Transitions per peptide";
            this.rBtnTransitions.UseVisualStyleBackColor = true;
            this.rBtnTransitions.CheckedChanged += new System.EventHandler(this.rBtnTransitions_CheckedChanged);
            // 
            // rBtnPower
            // 
            this.rBtnPower.AutoSize = true;
            this.rBtnPower.Location = new System.Drawing.Point(13, 193);
            this.rBtnPower.Name = "rBtnPower";
            this.rBtnPower.Size = new System.Drawing.Size(55, 17);
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
            this.groupBoxAuto.Location = new System.Drawing.Point(12, 12);
            this.groupBoxAuto.Name = "groupBoxAuto";
            this.groupBoxAuto.Size = new System.Drawing.Size(154, 247);
            this.groupBoxAuto.TabIndex = 0;
            this.groupBoxAuto.TabStop = false;
            this.groupBoxAuto.Text = "Automatically calculate";
            // 
            // SampleSizeUi
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(258, 448);
            this.Controls.Add(this.groupBoxAuto);
            this.Controls.Add(this.numberFDR);
            this.Controls.Add(this.btnDefault);
            this.Controls.Add(this.gBoxFoldChange);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.labelFDR);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SampleSizeUi";
            this.ShowIcon = false;
            this.Text = "Design Sample Size";
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
    }
}
