namespace MSStatArgsCollector
{
    partial class QualityControlUI
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
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.comboBoxNormalizeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.cboxAllowMissingPeaks = new System.Windows.Forms.CheckBox();
            this.groupBoxPlotSize = new System.Windows.Forms.GroupBox();
            this.tbxHeight = new System.Windows.Forms.TextBox();
            this.lblHeight = new System.Windows.Forms.Label();
            this.tbxWidth = new System.Windows.Forms.TextBox();
            this.lblWidth = new System.Windows.Forms.Label();
            this.cbxSelectHighQualityFeatures = new System.Windows.Forms.CheckBox();
            this.cbxRemoveInterferedProteins = new System.Windows.Forms.CheckBox();
            this.groupBoxPlotSize.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(182, 41);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(3, 3, 15, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(182, 12);
            this.btnOK.Margin = new System.Windows.Forms.Padding(3, 3, 15, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // comboBoxNormalizeTo
            // 
            this.comboBoxNormalizeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNormalizeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxNormalizeTo.FormattingEnabled = true;
            this.comboBoxNormalizeTo.Items.AddRange(new object[] {
            "None",
            "Equalize medians",
            "Quantile",
            "Relative to global standards"});
            this.comboBoxNormalizeTo.Location = new System.Drawing.Point(12, 30);
            this.comboBoxNormalizeTo.Name = "comboBoxNormalizeTo";
            this.comboBoxNormalizeTo.Size = new System.Drawing.Size(144, 21);
            this.comboBoxNormalizeTo.TabIndex = 1;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(12, 12);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(111, 13);
            this.labelNormalizeTo.TabIndex = 0;
            this.labelNormalizeTo.Text = "Normalization method:";
            // 
            // cboxAllowMissingPeaks
            // 
            this.cboxAllowMissingPeaks.AutoSize = true;
            this.cboxAllowMissingPeaks.Location = new System.Drawing.Point(13, 54);
            this.cboxAllowMissingPeaks.Name = "cboxAllowMissingPeaks";
            this.cboxAllowMissingPeaks.Size = new System.Drawing.Size(120, 17);
            this.cboxAllowMissingPeaks.TabIndex = 2;
            this.cboxAllowMissingPeaks.Text = "&Allow missing peaks";
            this.cboxAllowMissingPeaks.UseVisualStyleBackColor = true;
            // 
            // groupBoxPlotSize
            // 
            this.groupBoxPlotSize.Controls.Add(this.tbxHeight);
            this.groupBoxPlotSize.Controls.Add(this.lblHeight);
            this.groupBoxPlotSize.Controls.Add(this.tbxWidth);
            this.groupBoxPlotSize.Controls.Add(this.lblWidth);
            this.groupBoxPlotSize.Location = new System.Drawing.Point(13, 148);
            this.groupBoxPlotSize.Name = "groupBoxPlotSize";
            this.groupBoxPlotSize.Size = new System.Drawing.Size(241, 113);
            this.groupBoxPlotSize.TabIndex = 5;
            this.groupBoxPlotSize.TabStop = false;
            this.groupBoxPlotSize.Text = "Size of profile and QC plots";
            // 
            // tbxHeight
            // 
            this.tbxHeight.Location = new System.Drawing.Point(8, 79);
            this.tbxHeight.Name = "tbxHeight";
            this.tbxHeight.Size = new System.Drawing.Size(100, 20);
            this.tbxHeight.TabIndex = 3;
            this.tbxHeight.Text = "10";
            // 
            // lblHeight
            // 
            this.lblHeight.AutoSize = true;
            this.lblHeight.Location = new System.Drawing.Point(5, 60);
            this.lblHeight.Name = "lblHeight";
            this.lblHeight.Size = new System.Drawing.Size(38, 13);
            this.lblHeight.TabIndex = 2;
            this.lblHeight.Text = "Height";
            // 
            // tbxWidth
            // 
            this.tbxWidth.Location = new System.Drawing.Point(8, 37);
            this.tbxWidth.Name = "tbxWidth";
            this.tbxWidth.Size = new System.Drawing.Size(100, 20);
            this.tbxWidth.TabIndex = 1;
            this.tbxWidth.Text = "10";
            // 
            // lblWidth
            // 
            this.lblWidth.AutoSize = true;
            this.lblWidth.Location = new System.Drawing.Point(5, 21);
            this.lblWidth.Name = "lblWidth";
            this.lblWidth.Size = new System.Drawing.Size(35, 13);
            this.lblWidth.TabIndex = 0;
            this.lblWidth.Text = "Width";
            // 
            // cbxSelectHighQualityFeatures
            // 
            this.cbxSelectHighQualityFeatures.AutoSize = true;
            this.cbxSelectHighQualityFeatures.Location = new System.Drawing.Point(12, 77);
            this.cbxSelectHighQualityFeatures.Name = "cbxSelectHighQualityFeatures";
            this.cbxSelectHighQualityFeatures.Size = new System.Drawing.Size(153, 17);
            this.cbxSelectHighQualityFeatures.TabIndex = 6;
            this.cbxSelectHighQualityFeatures.Text = "Select high quality features";
            this.cbxSelectHighQualityFeatures.UseVisualStyleBackColor = true;
            this.cbxSelectHighQualityFeatures.CheckedChanged += new System.EventHandler(this.cbxSelectHighQualityFeatures_CheckedChanged);
            // 
            // cbxRemoveInterferedProteins
            // 
            this.cbxRemoveInterferedProteins.AutoSize = true;
            this.cbxRemoveInterferedProteins.Enabled = false;
            this.cbxRemoveInterferedProteins.Location = new System.Drawing.Point(31, 100);
            this.cbxRemoveInterferedProteins.Name = "cbxRemoveInterferedProteins";
            this.cbxRemoveInterferedProteins.Size = new System.Drawing.Size(179, 43);
            this.cbxRemoveInterferedProteins.TabIndex = 7;
            this.cbxRemoveInterferedProteins.Text = "Allow the algorithm to delete the \r\nwhole protein if all of its features \r\nhave i" +
    "nterference";
            this.cbxRemoveInterferedProteins.UseVisualStyleBackColor = true;
            // 
            // QualityControlUI
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(268, 273);
            this.Controls.Add(this.cbxRemoveInterferedProteins);
            this.Controls.Add(this.cbxSelectHighQualityFeatures);
            this.Controls.Add(this.groupBoxPlotSize);
            this.Controls.Add(this.cboxAllowMissingPeaks);
            this.Controls.Add(this.comboBoxNormalizeTo);
            this.Controls.Add(this.labelNormalizeTo);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "QualityControlUI";
            this.ShowInTaskbar = false;
            this.Text = "MSstats QC";
            this.groupBoxPlotSize.ResumeLayout(false);
            this.groupBoxPlotSize.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.ComboBox comboBoxNormalizeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.CheckBox cboxAllowMissingPeaks;
        private System.Windows.Forms.GroupBox groupBoxPlotSize;
        private System.Windows.Forms.TextBox tbxHeight;
        private System.Windows.Forms.Label lblHeight;
        private System.Windows.Forms.TextBox tbxWidth;
        private System.Windows.Forms.Label lblWidth;
        private System.Windows.Forms.CheckBox cbxSelectHighQualityFeatures;
        private System.Windows.Forms.CheckBox cbxRemoveInterferedProteins;
    }
}