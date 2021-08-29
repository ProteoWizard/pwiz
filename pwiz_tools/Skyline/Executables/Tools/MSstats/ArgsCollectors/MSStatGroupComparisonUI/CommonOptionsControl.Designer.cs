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
            this.cbxHighQualityFeatures = new System.Windows.Forms.CheckBox();
            this.lblQValueCutoff = new System.Windows.Forms.Label();
            this.tbxQValue = new System.Windows.Forms.TextBox();
            this.comboQuantMsLevel = new System.Windows.Forms.ComboBox();
            this.lblMsLevel = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.flowLayoutPanel1.SuspendLayout();
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
            this.comboBoxNormalizeTo.Location = new System.Drawing.Point(3, 16);
            this.comboBoxNormalizeTo.Name = "comboBoxNormalizeTo";
            this.comboBoxNormalizeTo.Size = new System.Drawing.Size(172, 21);
            this.comboBoxNormalizeTo.TabIndex = 5;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(3, 0);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(111, 13);
            this.labelNormalizeTo.TabIndex = 4;
            this.labelNormalizeTo.Text = "Normalization method:";
            // 
            // cbxHighQualityFeatures
            // 
            this.cbxHighQualityFeatures.AutoSize = true;
            this.cbxHighQualityFeatures.Location = new System.Drawing.Point(3, 122);
            this.cbxHighQualityFeatures.Name = "cbxHighQualityFeatures";
            this.cbxHighQualityFeatures.Size = new System.Drawing.Size(153, 17);
            this.cbxHighQualityFeatures.TabIndex = 6;
            this.cbxHighQualityFeatures.Text = "Select high quality features";
            this.cbxHighQualityFeatures.UseVisualStyleBackColor = true;
            // 
            // lblQValueCutoff
            // 
            this.lblQValueCutoff.AutoSize = true;
            this.lblQValueCutoff.Location = new System.Drawing.Point(3, 80);
            this.lblQValueCutoff.Name = "lblQValueCutoff";
            this.lblQValueCutoff.Size = new System.Drawing.Size(77, 13);
            this.lblQValueCutoff.TabIndex = 7;
            this.lblQValueCutoff.Text = "Q value cutoff:";
            // 
            // tbxQValue
            // 
            this.tbxQValue.Location = new System.Drawing.Point(3, 96);
            this.tbxQValue.Name = "tbxQValue";
            this.tbxQValue.Size = new System.Drawing.Size(172, 20);
            this.tbxQValue.TabIndex = 8;
            // 
            // comboQuantMsLevel
            // 
            this.comboQuantMsLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboQuantMsLevel.FormattingEnabled = true;
            this.comboQuantMsLevel.Items.AddRange(new object[] {
            "1",
            "2"});
            this.comboQuantMsLevel.Location = new System.Drawing.Point(3, 56);
            this.comboQuantMsLevel.Name = "comboQuantMsLevel";
            this.comboQuantMsLevel.Size = new System.Drawing.Size(169, 21);
            this.comboQuantMsLevel.TabIndex = 10;
            this.comboQuantMsLevel.Visible = false;
            // 
            // lblMsLevel
            // 
            this.lblMsLevel.AutoSize = true;
            this.lblMsLevel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblMsLevel.Location = new System.Drawing.Point(3, 40);
            this.lblMsLevel.Name = "lblMsLevel";
            this.lblMsLevel.Size = new System.Drawing.Size(51, 13);
            this.lblMsLevel.TabIndex = 9;
            this.lblMsLevel.Text = "&MS level:";
            this.lblMsLevel.Visible = false;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.AutoSize = true;
            this.flowLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanel1.Controls.Add(this.labelNormalizeTo);
            this.flowLayoutPanel1.Controls.Add(this.comboBoxNormalizeTo);
            this.flowLayoutPanel1.Controls.Add(this.lblMsLevel);
            this.flowLayoutPanel1.Controls.Add(this.comboQuantMsLevel);
            this.flowLayoutPanel1.Controls.Add(this.lblQValueCutoff);
            this.flowLayoutPanel1.Controls.Add(this.tbxQValue);
            this.flowLayoutPanel1.Controls.Add(this.cbxHighQualityFeatures);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(178, 142);
            this.flowLayoutPanel1.TabIndex = 11;
            // 
            // CommonOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "CommonOptionsControl";
            this.Size = new System.Drawing.Size(178, 142);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboBoxNormalizeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.CheckBox cbxHighQualityFeatures;
        private System.Windows.Forms.Label lblQValueCutoff;
        private System.Windows.Forms.TextBox tbxQValue;
        private System.Windows.Forms.ComboBox comboQuantMsLevel;
        private System.Windows.Forms.Label lblMsLevel;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
    }
}
