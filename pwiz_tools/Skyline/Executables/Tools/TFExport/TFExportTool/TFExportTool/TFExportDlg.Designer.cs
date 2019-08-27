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
            resources.ApplyResources(this.exportButton, "exportButton");
            this.exportButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.exportButton.Name = "exportButton";
            this.exportButton.UseVisualStyleBackColor = true;
            // 
            // labelResult
            // 
            resources.ApplyResources(this.labelResult, "labelResult");
            this.labelResult.ForeColor = System.Drawing.Color.Red;
            this.labelResult.Name = "labelResult";
            // 
            // cancelButton
            // 
            resources.ApplyResources(this.cancelButton, "cancelButton");
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // groupBoxImportOptions
            // 
            resources.ApplyResources(this.groupBoxImportOptions, "groupBoxImportOptions");
            this.groupBoxImportOptions.Controls.Add(this.groupBoxSort);
            this.groupBoxImportOptions.Controls.Add(this.groupBoxRT);
            this.groupBoxImportOptions.Controls.Add(this.label4);
            this.groupBoxImportOptions.Controls.Add(this.comboBoxStandard);
            this.groupBoxImportOptions.Controls.Add(this.label3);
            this.groupBoxImportOptions.Controls.Add(this.flowLayoutPanel1);
            this.groupBoxImportOptions.Controls.Add(this.label2);
            this.groupBoxImportOptions.Controls.Add(this.flowLayoutPanel2);
            this.groupBoxImportOptions.Controls.Add(this.label1);
            this.groupBoxImportOptions.Name = "groupBoxImportOptions";
            this.groupBoxImportOptions.TabStop = false;
            // 
            // groupBoxSort
            // 
            resources.ApplyResources(this.groupBoxSort, "groupBoxSort");
            this.groupBoxSort.Controls.Add(this.radioButtonIntensityAvg);
            this.groupBoxSort.Controls.Add(this.radioButtonIntensityFromFile);
            this.groupBoxSort.Controls.Add(this.comboBoxDataSetIntensitySort);
            this.groupBoxSort.Name = "groupBoxSort";
            this.groupBoxSort.TabStop = false;
            // 
            // radioButtonIntensityAvg
            // 
            resources.ApplyResources(this.radioButtonIntensityAvg, "radioButtonIntensityAvg");
            this.radioButtonIntensityAvg.Checked = true;
            this.radioButtonIntensityAvg.Name = "radioButtonIntensityAvg";
            this.radioButtonIntensityAvg.TabStop = true;
            this.radioButtonIntensityAvg.UseVisualStyleBackColor = true;
            // 
            // radioButtonIntensityFromFile
            // 
            resources.ApplyResources(this.radioButtonIntensityFromFile, "radioButtonIntensityFromFile");
            this.radioButtonIntensityFromFile.Name = "radioButtonIntensityFromFile";
            this.radioButtonIntensityFromFile.UseVisualStyleBackColor = true;
            this.radioButtonIntensityFromFile.CheckedChanged += new System.EventHandler(this.radioButton2_CheckedChanged);
            // 
            // comboBoxDataSetIntensitySort
            // 
            this.comboBoxDataSetIntensitySort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboBoxDataSetIntensitySort, "comboBoxDataSetIntensitySort");
            this.comboBoxDataSetIntensitySort.FormattingEnabled = true;
            this.comboBoxDataSetIntensitySort.Name = "comboBoxDataSetIntensitySort";
            // 
            // groupBoxRT
            // 
            resources.ApplyResources(this.groupBoxRT, "groupBoxRT");
            this.groupBoxRT.Controls.Add(this.radioButtonRTAvg);
            this.groupBoxRT.Controls.Add(this.radioButtonUseDataSet);
            this.groupBoxRT.Controls.Add(this.comboBoxRTFiles);
            this.groupBoxRT.Controls.Add(this.label5);
            this.groupBoxRT.Controls.Add(this.numericUpDown1);
            this.groupBoxRT.Name = "groupBoxRT";
            this.groupBoxRT.TabStop = false;
            // 
            // radioButtonRTAvg
            // 
            resources.ApplyResources(this.radioButtonRTAvg, "radioButtonRTAvg");
            this.radioButtonRTAvg.Checked = true;
            this.radioButtonRTAvg.Name = "radioButtonRTAvg";
            this.radioButtonRTAvg.TabStop = true;
            this.radioButtonRTAvg.UseVisualStyleBackColor = true;
            // 
            // radioButtonUseDataSet
            // 
            resources.ApplyResources(this.radioButtonUseDataSet, "radioButtonUseDataSet");
            this.radioButtonUseDataSet.Name = "radioButtonUseDataSet";
            this.radioButtonUseDataSet.UseVisualStyleBackColor = true;
            this.radioButtonUseDataSet.CheckedChanged += new System.EventHandler(this.radioButton3_CheckedChanged);
            // 
            // comboBoxRTFiles
            // 
            this.comboBoxRTFiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboBoxRTFiles, "comboBoxRTFiles");
            this.comboBoxRTFiles.FormattingEnabled = true;
            this.comboBoxRTFiles.Name = "comboBoxRTFiles";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // numericUpDown1
            // 
            resources.ApplyResources(this.numericUpDown1, "numericUpDown1");
            this.numericUpDown1.Maximum = new decimal(new int[] {
            999,
            0,
            0,
            0});
            this.numericUpDown1.Name = "numericUpDown1";
            this.numericUpDown1.Value = new decimal(new int[] {
            120,
            0,
            0,
            0});
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // comboBoxStandard
            // 
            this.comboBoxStandard.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStandard.FormattingEnabled = true;
            this.comboBoxStandard.Items.AddRange(new object[] {
            resources.GetString("comboBoxStandard.Items"),
            resources.GetString("comboBoxStandard.Items1"),
            resources.GetString("comboBoxStandard.Items2")});
            resources.ApplyResources(this.comboBoxStandard, "comboBoxStandard");
            this.comboBoxStandard.Name = "comboBoxStandard";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.radioButtonAll);
            this.flowLayoutPanel1.Controls.Add(this.radioButtonOne);
            resources.ApplyResources(this.flowLayoutPanel1, "flowLayoutPanel1");
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            // 
            // radioButtonAll
            // 
            resources.ApplyResources(this.radioButtonAll, "radioButtonAll");
            this.radioButtonAll.Name = "radioButtonAll";
            this.radioButtonAll.TabStop = true;
            this.radioButtonAll.UseVisualStyleBackColor = true;
            this.radioButtonAll.CheckedChanged += new System.EventHandler(this.radioButtonAll_CheckedChanged);
            // 
            // radioButtonOne
            // 
            resources.ApplyResources(this.radioButtonOne, "radioButtonOne");
            this.radioButtonOne.Name = "radioButtonOne";
            this.radioButtonOne.TabStop = true;
            this.radioButtonOne.UseVisualStyleBackColor = true;
            this.radioButtonOne.CheckedChanged += new System.EventHandler(this.radioButtonOne_CheckedChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // flowLayoutPanel2
            // 
            this.flowLayoutPanel2.Controls.Add(this.radioButtonConfirming);
            this.flowLayoutPanel2.Controls.Add(this.radioButtonFragment);
            resources.ApplyResources(this.flowLayoutPanel2, "flowLayoutPanel2");
            this.flowLayoutPanel2.Name = "flowLayoutPanel2";
            // 
            // radioButtonConfirming
            // 
            resources.ApplyResources(this.radioButtonConfirming, "radioButtonConfirming");
            this.radioButtonConfirming.Name = "radioButtonConfirming";
            this.radioButtonConfirming.TabStop = true;
            this.radioButtonConfirming.UseVisualStyleBackColor = true;
            // 
            // radioButtonFragment
            // 
            resources.ApplyResources(this.radioButtonFragment, "radioButtonFragment");
            this.radioButtonFragment.Name = "radioButtonFragment";
            this.radioButtonFragment.TabStop = true;
            this.radioButtonFragment.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // labelDescription
            // 
            resources.ApplyResources(this.labelDescription, "labelDescription");
            this.labelDescription.Name = "labelDescription";
            // 
            // TFExportDlg
            // 
            this.AcceptButton = this.exportButton;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.Controls.Add(this.labelDescription);
            this.Controls.Add(this.groupBoxImportOptions);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.labelResult);
            this.Controls.Add(this.exportButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TFExportDlg";
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

