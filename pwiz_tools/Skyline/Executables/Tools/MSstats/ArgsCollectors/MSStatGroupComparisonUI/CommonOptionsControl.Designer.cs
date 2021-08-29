namespace MSStatArgsCollector
{
    partial class CommonOptionsControl
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
            this.components = new System.ComponentModel.Container();
            this.comboBoxNormalizeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.cboxSelectHighQualityFeatures = new System.Windows.Forms.CheckBox();
            this.lblQValueCutoff = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.comboQuantMsLevel = new System.Windows.Forms.ComboBox();
            this.lblMsLevel = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // comboBoxNormalizeTo
            // 
            this.comboBoxNormalizeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNormalizeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxNormalizeTo.FormattingEnabled = true;
            this.comboBoxNormalizeTo.Items.AddRange(new object[] {
            "None",
            "Equalize Medians",
            "Quantile",
            "Global Standards"});
            this.comboBoxNormalizeTo.Location = new System.Drawing.Point(12, 25);
            this.comboBoxNormalizeTo.Name = "comboBoxNormalizeTo";
            this.comboBoxNormalizeTo.Size = new System.Drawing.Size(172, 21);
            this.comboBoxNormalizeTo.TabIndex = 5;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(12, 8);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(111, 13);
            this.labelNormalizeTo.TabIndex = 4;
            this.labelNormalizeTo.Text = "Normalization method:";
            // 
            // cboxSelectHighQualityFeatures
            // 
            this.cboxSelectHighQualityFeatures.AutoSize = true;
            this.cboxSelectHighQualityFeatures.Location = new System.Drawing.Point(15, 132);
            this.cboxSelectHighQualityFeatures.Name = "cboxSelectHighQualityFeatures";
            this.cboxSelectHighQualityFeatures.Size = new System.Drawing.Size(153, 17);
            this.cboxSelectHighQualityFeatures.TabIndex = 6;
            this.cboxSelectHighQualityFeatures.Text = "Select high quality features";
            this.cboxSelectHighQualityFeatures.UseVisualStyleBackColor = true;
            // 
            // lblQValueCutoff
            // 
            this.lblQValueCutoff.AutoSize = true;
            this.lblQValueCutoff.Location = new System.Drawing.Point(12, 90);
            this.lblQValueCutoff.Name = "lblQValueCutoff";
            this.lblQValueCutoff.Size = new System.Drawing.Size(74, 13);
            this.lblQValueCutoff.TabIndex = 7;
            this.lblQValueCutoff.Text = "Q value cutoff";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(12, 106);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(172, 20);
            this.textBox1.TabIndex = 8;
            // 
            // comboQuantMsLevel
            // 
            this.comboQuantMsLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboQuantMsLevel.FormattingEnabled = true;
            this.comboQuantMsLevel.Items.AddRange(new object[] {
            "All",
            "1",
            "2"});
            this.comboQuantMsLevel.Location = new System.Drawing.Point(12, 64);
            this.comboQuantMsLevel.Name = "comboQuantMsLevel";
            this.comboQuantMsLevel.Size = new System.Drawing.Size(169, 21);
            this.comboQuantMsLevel.TabIndex = 10;
            // 
            // lblMsLevel
            // 
            this.lblMsLevel.AutoSize = true;
            this.lblMsLevel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblMsLevel.Location = new System.Drawing.Point(12, 49);
            this.lblMsLevel.Name = "lblMsLevel";
            this.lblMsLevel.Size = new System.Drawing.Size(48, 13);
            this.lblMsLevel.TabIndex = 9;
            this.lblMsLevel.Text = "&MS level";
            // 
            // CommonOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.comboQuantMsLevel);
            this.Controls.Add(this.lblMsLevel);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.lblQValueCutoff);
            this.Controls.Add(this.cboxSelectHighQualityFeatures);
            this.Controls.Add(this.comboBoxNormalizeTo);
            this.Controls.Add(this.labelNormalizeTo);
            this.Name = "CommonOptionsControl";
            this.Size = new System.Drawing.Size(203, 164);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboBoxNormalizeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.CheckBox cboxSelectHighQualityFeatures;
        private System.Windows.Forms.Label lblQValueCutoff;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.ComboBox comboQuantMsLevel;
        private System.Windows.Forms.Label lblMsLevel;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
