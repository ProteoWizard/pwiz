namespace TFExportTool
{
    partial class TFExportDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TFExportDlg));
            this.exportButton = new System.Windows.Forms.Button();
            this.labelResult = new System.Windows.Forms.Label();
            this.cancelButton = new System.Windows.Forms.Button();
            this.groupBoxImportOptions = new System.Windows.Forms.GroupBox();
            this.groupBoxSort = new System.Windows.Forms.GroupBox();
            this.radioButtonIntensityAvg = new System.Windows.Forms.RadioButton();
            this.radioButtonIntensityFromFile = new System.Windows.Forms.RadioButton();
            this.comboBoxDataSetIntensitySort = new System.Windows.Forms.ComboBox();
            this.groupBoxRT = new System.Windows.Forms.GroupBox();
            this.radioButtonRTAvg = new System.Windows.Forms.RadioButton();
            this.radioButtonUseDataSet = new System.Windows.Forms.RadioButton();
            this.comboBoxRTFiles = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.numericUpDown1 = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.comboBoxStandard = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.radioButtonAll = new System.Windows.Forms.RadioButton();
            this.radioButtonOne = new System.Windows.Forms.RadioButton();
            this.label2 = new System.Windows.Forms.Label();
            this.flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
            this.radioButtonConfirming = new System.Windows.Forms.RadioButton();
            this.radioButtonFragment = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.labelDescription = new System.Windows.Forms.Label();
            this.groupBoxImportOptions.SuspendLayout();
            this.groupBoxSort.SuspendLayout();
            this.groupBoxRT.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).BeginInit();
            this.flowLayoutPanel1.SuspendLayout();
            this.flowLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.exportButton.Location = new System.Drawing.Point(258, 439);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(75, 23);
            this.exportButton.TabIndex = 2;
            this.exportButton.Text = "Export";
            this.exportButton.UseVisualStyleBackColor = true;
            // 
            // labelResult
            // 
            this.labelResult.AutoSize = true;
            this.labelResult.ForeColor = System.Drawing.Color.Red;
            this.labelResult.Location = new System.Drawing.Point(13, 13);
            this.labelResult.Name = "labelResult";
            this.labelResult.Size = new System.Drawing.Size(0, 13);
            this.labelResult.TabIndex = 1;
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(339, 439);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 3;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // groupBoxImportOptions
            // 
            this.groupBoxImportOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxImportOptions.Controls.Add(this.groupBoxSort);
            this.groupBoxImportOptions.Controls.Add(this.groupBoxRT);
            this.groupBoxImportOptions.Controls.Add(this.label4);
            this.groupBoxImportOptions.Controls.Add(this.comboBoxStandard);
            this.groupBoxImportOptions.Controls.Add(this.label3);
            this.groupBoxImportOptions.Controls.Add(this.flowLayoutPanel1);
            this.groupBoxImportOptions.Controls.Add(this.label2);
            this.groupBoxImportOptions.Controls.Add(this.flowLayoutPanel2);
            this.groupBoxImportOptions.Controls.Add(this.label1);
            this.groupBoxImportOptions.Location = new System.Drawing.Point(12, 46);
            this.groupBoxImportOptions.Name = "groupBoxImportOptions";
            this.groupBoxImportOptions.Size = new System.Drawing.Size(401, 387);
            this.groupBoxImportOptions.TabIndex = 1;
            this.groupBoxImportOptions.TabStop = false;
            this.groupBoxImportOptions.Text = "Export Options (optional)";
            // 
            // groupBoxSort
            // 
            this.groupBoxSort.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxSort.Controls.Add(this.radioButtonIntensityAvg);
            this.groupBoxSort.Controls.Add(this.radioButtonIntensityFromFile);
            this.groupBoxSort.Controls.Add(this.comboBoxDataSetIntensitySort);
            this.groupBoxSort.Location = new System.Drawing.Point(12, 284);
            this.groupBoxSort.Name = "groupBoxSort";
            this.groupBoxSort.Size = new System.Drawing.Size(378, 97);
            this.groupBoxSort.TabIndex = 8;
            this.groupBoxSort.TabStop = false;
            this.groupBoxSort.Text = "Sort Product Ions";
            // 
            // radioButtonIntensityAvg
            // 
            this.radioButtonIntensityAvg.AutoSize = true;
            this.radioButtonIntensityAvg.Checked = true;
            this.radioButtonIntensityAvg.Location = new System.Drawing.Point(6, 19);
            this.radioButtonIntensityAvg.Name = "radioButtonIntensityAvg";
            this.radioButtonIntensityAvg.Size = new System.Drawing.Size(127, 17);
            this.radioButtonIntensityAvg.TabIndex = 0;
            this.radioButtonIntensityAvg.TabStop = true;
            this.radioButtonIntensityAvg.Text = "Use intensity average";
            this.radioButtonIntensityAvg.UseVisualStyleBackColor = true;
            // 
            // radioButtonIntensityFromFile
            // 
            this.radioButtonIntensityFromFile.AutoSize = true;
            this.radioButtonIntensityFromFile.Location = new System.Drawing.Point(6, 42);
            this.radioButtonIntensityFromFile.Name = "radioButtonIntensityFromFile";
            this.radioButtonIntensityFromFile.Size = new System.Drawing.Size(181, 17);
            this.radioButtonIntensityFromFile.TabIndex = 1;
            this.radioButtonIntensityFromFile.Text = "Use values from a single data set";
            this.radioButtonIntensityFromFile.UseVisualStyleBackColor = true;
            this.radioButtonIntensityFromFile.CheckedChanged += new System.EventHandler(this.radioButton2_CheckedChanged);
            // 
            // comboBoxDataSetIntensitySort
            // 
            this.comboBoxDataSetIntensitySort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDataSetIntensitySort.Enabled = false;
            this.comboBoxDataSetIntensitySort.FormattingEnabled = true;
            this.comboBoxDataSetIntensitySort.Location = new System.Drawing.Point(17, 65);
            this.comboBoxDataSetIntensitySort.Name = "comboBoxDataSetIntensitySort";
            this.comboBoxDataSetIntensitySort.Size = new System.Drawing.Size(170, 21);
            this.comboBoxDataSetIntensitySort.TabIndex = 2;
            // 
            // groupBoxRT
            // 
            this.groupBoxRT.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxRT.Controls.Add(this.radioButtonRTAvg);
            this.groupBoxRT.Controls.Add(this.radioButtonUseDataSet);
            this.groupBoxRT.Controls.Add(this.comboBoxRTFiles);
            this.groupBoxRT.Controls.Add(this.label5);
            this.groupBoxRT.Controls.Add(this.numericUpDown1);
            this.groupBoxRT.Location = new System.Drawing.Point(12, 138);
            this.groupBoxRT.Name = "groupBoxRT";
            this.groupBoxRT.Size = new System.Drawing.Size(378, 143);
            this.groupBoxRT.TabIndex = 7;
            this.groupBoxRT.TabStop = false;
            this.groupBoxRT.Text = "Retention Time";
            // 
            // radioButtonRTAvg
            // 
            this.radioButtonRTAvg.AutoSize = true;
            this.radioButtonRTAvg.Checked = true;
            this.radioButtonRTAvg.Location = new System.Drawing.Point(6, 19);
            this.radioButtonRTAvg.Name = "radioButtonRTAvg";
            this.radioButtonRTAvg.Size = new System.Drawing.Size(152, 17);
            this.radioButtonRTAvg.TabIndex = 0;
            this.radioButtonRTAvg.TabStop = true;
            this.radioButtonRTAvg.Text = "Use retention time average";
            this.radioButtonRTAvg.UseVisualStyleBackColor = true;
            // 
            // radioButtonUseDataSet
            // 
            this.radioButtonUseDataSet.AutoSize = true;
            this.radioButtonUseDataSet.Location = new System.Drawing.Point(6, 42);
            this.radioButtonUseDataSet.Name = "radioButtonUseDataSet";
            this.radioButtonUseDataSet.Size = new System.Drawing.Size(181, 17);
            this.radioButtonUseDataSet.TabIndex = 1;
            this.radioButtonUseDataSet.Text = "Use values from a single data set";
            this.radioButtonUseDataSet.UseVisualStyleBackColor = true;
            this.radioButtonUseDataSet.CheckedChanged += new System.EventHandler(this.radioButton3_CheckedChanged);
            // 
            // comboBoxRTFiles
            // 
            this.comboBoxRTFiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRTFiles.Enabled = false;
            this.comboBoxRTFiles.FormattingEnabled = true;
            this.comboBoxRTFiles.Location = new System.Drawing.Point(17, 65);
            this.comboBoxRTFiles.Name = "comboBoxRTFiles";
            this.comboBoxRTFiles.Size = new System.Drawing.Size(170, 21);
            this.comboBoxRTFiles.TabIndex = 2;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 102);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(147, 13);
            this.label5.TabIndex = 3;
            this.label5.Text = "Retention Time Window (sec)";
            // 
            // numericUpDown1
            // 
            this.numericUpDown1.Location = new System.Drawing.Point(159, 99);
            this.numericUpDown1.Maximum = new decimal(new int[] {
            999,
            0,
            0,
            0});
            this.numericUpDown1.Name = "numericUpDown1";
            this.numericUpDown1.Size = new System.Drawing.Size(43, 20);
            this.numericUpDown1.TabIndex = 4;
            this.numericUpDown1.Value = new decimal(new int[] {
            120,
            0,
            0,
            0});
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(9, 104);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(118, 13);
            this.label4.TabIndex = 5;
            this.label4.Text = "Internal Standard Type:";
            // 
            // comboBoxStandard
            // 
            this.comboBoxStandard.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStandard.FormattingEnabled = true;
            this.comboBoxStandard.Items.AddRange(new object[] {
            "none",
            "light",
            "heavy"});
            this.comboBoxStandard.Location = new System.Drawing.Point(137, 101);
            this.comboBoxStandard.Name = "comboBoxStandard";
            this.comboBoxStandard.Size = new System.Drawing.Size(121, 21);
            this.comboBoxStandard.TabIndex = 0;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 16);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(86, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Auto Fill Options:";
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.radioButtonAll);
            this.flowLayoutPanel1.Controls.Add(this.radioButtonOne);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(138, 34);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(63, 54);
            this.flowLayoutPanel1.TabIndex = 2;
            // 
            // radioButtonAll
            // 
            this.radioButtonAll.AutoSize = true;
            this.radioButtonAll.Location = new System.Drawing.Point(3, 3);
            this.radioButtonAll.Name = "radioButtonAll";
            this.radioButtonAll.Size = new System.Drawing.Size(36, 17);
            this.radioButtonAll.TabIndex = 0;
            this.radioButtonAll.TabStop = true;
            this.radioButtonAll.Text = "All";
            this.radioButtonAll.UseVisualStyleBackColor = true;
            this.radioButtonAll.CheckedChanged += new System.EventHandler(this.radioButtonAll_CheckedChanged);
            // 
            // radioButtonOne
            // 
            this.radioButtonOne.AutoSize = true;
            this.radioButtonOne.Location = new System.Drawing.Point(3, 26);
            this.radioButtonOne.Name = "radioButtonOne";
            this.radioButtonOne.Size = new System.Drawing.Size(45, 17);
            this.radioButtonOne.TabIndex = 1;
            this.radioButtonOne.TabStop = true;
            this.radioButtonOne.Text = "One";
            this.radioButtonOne.UseVisualStyleBackColor = true;
            this.radioButtonOne.CheckedChanged += new System.EventHandler(this.radioButtonOne_CheckedChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(200, 36);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(70, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Set others to:";
            // 
            // flowLayoutPanel2
            // 
            this.flowLayoutPanel2.Controls.Add(this.radioButtonConfirming);
            this.flowLayoutPanel2.Controls.Add(this.radioButtonFragment);
            this.flowLayoutPanel2.Location = new System.Drawing.Point(270, 36);
            this.flowLayoutPanel2.Name = "flowLayoutPanel2";
            this.flowLayoutPanel2.Size = new System.Drawing.Size(104, 54);
            this.flowLayoutPanel2.TabIndex = 4;
            // 
            // radioButtonConfirming
            // 
            this.radioButtonConfirming.AutoSize = true;
            this.radioButtonConfirming.Enabled = false;
            this.radioButtonConfirming.Location = new System.Drawing.Point(3, 3);
            this.radioButtonConfirming.Name = "radioButtonConfirming";
            this.radioButtonConfirming.Size = new System.Drawing.Size(92, 17);
            this.radioButtonConfirming.TabIndex = 0;
            this.radioButtonConfirming.TabStop = true;
            this.radioButtonConfirming.Text = "Confirming Ion";
            this.radioButtonConfirming.UseVisualStyleBackColor = true;
            // 
            // radioButtonFragment
            // 
            this.radioButtonFragment.AutoSize = true;
            this.radioButtonFragment.Enabled = false;
            this.radioButtonFragment.Location = new System.Drawing.Point(3, 26);
            this.radioButtonFragment.Name = "radioButtonFragment";
            this.radioButtonFragment.Size = new System.Drawing.Size(69, 17);
            this.radioButtonFragment.TabIndex = 1;
            this.radioButtonFragment.TabStop = true;
            this.radioButtonFragment.Text = "Fragment";
            this.radioButtonFragment.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 36);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(126, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Number of Target Peaks:";
            // 
            // labelDescription
            // 
            this.labelDescription.AutoSize = true;
            this.labelDescription.Location = new System.Drawing.Point(20, 13);
            this.labelDescription.Name = "labelDescription";
            this.labelDescription.Size = new System.Drawing.Size(324, 26);
            this.labelDescription.TabIndex = 0;
            this.labelDescription.Text = "Values from Skyline\'s document grid are used by default,\r\nthe options below can b" +
    "e used to quickly populate certain columns.";
            // 
            // TFExportDlg
            // 
            this.AcceptButton = this.exportButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(429, 470);
            this.Controls.Add(this.labelDescription);
            this.Controls.Add(this.groupBoxImportOptions);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.labelResult);
            this.Controls.Add(this.exportButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "TFExportDlg";
            this.Text = "Trace Finder Export";
            this.groupBoxImportOptions.ResumeLayout(false);
            this.groupBoxImportOptions.PerformLayout();
            this.groupBoxSort.ResumeLayout(false);
            this.groupBoxSort.PerformLayout();
            this.groupBoxRT.ResumeLayout(false);
            this.groupBoxRT.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).EndInit();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.flowLayoutPanel2.ResumeLayout(false);
            this.flowLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Label labelResult;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.GroupBox groupBoxImportOptions;
        private System.Windows.Forms.RadioButton radioButtonFragment;
        private System.Windows.Forms.RadioButton radioButtonConfirming;
        private System.Windows.Forms.RadioButton radioButtonOne;
        private System.Windows.Forms.RadioButton radioButtonAll;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label labelDescription;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboBoxStandard;
        private System.Windows.Forms.RadioButton radioButtonRTAvg;
        private System.Windows.Forms.RadioButton radioButtonUseDataSet;
        private System.Windows.Forms.ComboBox comboBoxRTFiles;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown numericUpDown1;
        private System.Windows.Forms.GroupBox groupBoxSort;
        private System.Windows.Forms.RadioButton radioButtonIntensityAvg;
        private System.Windows.Forms.RadioButton radioButtonIntensityFromFile;
        private System.Windows.Forms.ComboBox comboBoxDataSetIntensitySort;
        private System.Windows.Forms.GroupBox groupBoxRT;
    }
}

