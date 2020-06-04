namespace pwiz.Skyline.SettingsUI
{
    partial class SearchSettingsControl
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
            this.cbMS1TolUnit = new System.Windows.Forms.ComboBox();
            this.lblMs1Tolerance = new System.Windows.Forms.Label();
            this.txtMS1Tolerance = new System.Windows.Forms.TextBox();
            this.txtMS2Tolerance = new System.Windows.Forms.TextBox();
            this.lblMs2Tolerance = new System.Windows.Forms.Label();
            this.cbMS2TolUnit = new System.Windows.Forms.ComboBox();
            this.lblFragmentIons = new System.Windows.Forms.Label();
            this.cbFragmentIons = new System.Windows.Forms.ComboBox();
            this.btnAdditionalSettings = new System.Windows.Forms.Button();
            this.lblSearchEngineName = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.pBLogo = new System.Windows.Forms.PictureBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pBLogo)).BeginInit();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // cbMS1TolUnit
            // 
            this.cbMS1TolUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMS1TolUnit.FormattingEnabled = true;
            this.cbMS1TolUnit.Location = new System.Drawing.Point(153, 70);
            this.cbMS1TolUnit.Name = "cbMS1TolUnit";
            this.cbMS1TolUnit.Size = new System.Drawing.Size(70, 21);
            this.cbMS1TolUnit.TabIndex = 2;
            // 
            // lblMs1Tolerance
            // 
            this.lblMs1Tolerance.AutoSize = true;
            this.lblMs1Tolerance.Location = new System.Drawing.Point(13, 53);
            this.lblMs1Tolerance.Name = "lblMs1Tolerance";
            this.lblMs1Tolerance.Size = new System.Drawing.Size(79, 13);
            this.lblMs1Tolerance.TabIndex = 9;
            this.lblMs1Tolerance.Text = "MS1 tolerance:";
            // 
            // txtMS1Tolerance
            // 
            this.txtMS1Tolerance.Location = new System.Drawing.Point(13, 70);
            this.txtMS1Tolerance.Name = "txtMS1Tolerance";
            this.txtMS1Tolerance.Size = new System.Drawing.Size(121, 20);
            this.txtMS1Tolerance.TabIndex = 1;
            // 
            // txtMS2Tolerance
            // 
            this.txtMS2Tolerance.Location = new System.Drawing.Point(13, 110);
            this.txtMS2Tolerance.Name = "txtMS2Tolerance";
            this.txtMS2Tolerance.Size = new System.Drawing.Size(121, 20);
            this.txtMS2Tolerance.TabIndex = 3;
            // 
            // lblMs2Tolerance
            // 
            this.lblMs2Tolerance.AutoSize = true;
            this.lblMs2Tolerance.Location = new System.Drawing.Point(13, 94);
            this.lblMs2Tolerance.Name = "lblMs2Tolerance";
            this.lblMs2Tolerance.Size = new System.Drawing.Size(79, 13);
            this.lblMs2Tolerance.TabIndex = 12;
            this.lblMs2Tolerance.Text = "MS2 tolerance:";
            // 
            // cbMS2TolUnit
            // 
            this.cbMS2TolUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMS2TolUnit.FormattingEnabled = true;
            this.cbMS2TolUnit.Location = new System.Drawing.Point(153, 110);
            this.cbMS2TolUnit.Name = "cbMS2TolUnit";
            this.cbMS2TolUnit.Size = new System.Drawing.Size(70, 21);
            this.cbMS2TolUnit.TabIndex = 4;
            // 
            // lblFragmentIons
            // 
            this.lblFragmentIons.AutoSize = true;
            this.lblFragmentIons.Location = new System.Drawing.Point(13, 138);
            this.lblFragmentIons.Name = "lblFragmentIons";
            this.lblFragmentIons.Size = new System.Drawing.Size(76, 13);
            this.lblFragmentIons.TabIndex = 14;
            this.lblFragmentIons.Text = "Fragment ions:";
            // 
            // cbFragmentIons
            // 
            this.cbFragmentIons.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbFragmentIons.FormattingEnabled = true;
            this.cbFragmentIons.Location = new System.Drawing.Point(13, 154);
            this.cbFragmentIons.Name = "cbFragmentIons";
            this.cbFragmentIons.Size = new System.Drawing.Size(121, 21);
            this.cbFragmentIons.TabIndex = 5;
            // 
            // btnAdditionalSettings
            // 
            this.btnAdditionalSettings.Enabled = false;
            this.btnAdditionalSettings.Location = new System.Drawing.Point(13, 399);
            this.btnAdditionalSettings.Name = "btnAdditionalSettings";
            this.btnAdditionalSettings.Size = new System.Drawing.Size(107, 23);
            this.btnAdditionalSettings.TabIndex = 7;
            this.btnAdditionalSettings.Text = "A&dditional Settings";
            this.btnAdditionalSettings.UseVisualStyleBackColor = true;
            // 
            // lblSearchEngineName
            // 
            this.lblSearchEngineName.AutoSize = true;
            this.lblSearchEngineName.Location = new System.Drawing.Point(3, 0);
            this.lblSearchEngineName.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.lblSearchEngineName.Name = "lblSearchEngineName";
            this.lblSearchEngineName.Size = new System.Drawing.Size(0, 13);
            this.lblSearchEngineName.TabIndex = 20;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 0);
            this.label2.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(81, 13);
            this.label2.TabIndex = 21;
            this.label2.Text = "search settings:";
            // 
            // pBLogo
            // 
            this.pBLogo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pBLogo.Location = new System.Drawing.Point(323, 4);
            this.pBLogo.Name = "pBLogo";
            this.pBLogo.Size = new System.Drawing.Size(55, 50);
            this.pBLogo.TabIndex = 22;
            this.pBLogo.TabStop = false;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.lblSearchEngineName);
            this.flowLayoutPanel1.Controls.Add(this.label2);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(3, 4);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(200, 29);
            this.flowLayoutPanel1.TabIndex = 25;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(153, 53);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(29, 13);
            this.label1.TabIndex = 26;
            this.label1.Text = "Unit:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(153, 94);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(29, 13);
            this.label3.TabIndex = 27;
            this.label3.Text = "Unit:";
            // 
            // SearchSettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.pBLogo);
            this.Controls.Add(this.btnAdditionalSettings);
            this.Controls.Add(this.cbFragmentIons);
            this.Controls.Add(this.lblFragmentIons);
            this.Controls.Add(this.txtMS2Tolerance);
            this.Controls.Add(this.lblMs2Tolerance);
            this.Controls.Add(this.cbMS2TolUnit);
            this.Controls.Add(this.txtMS1Tolerance);
            this.Controls.Add(this.lblMs1Tolerance);
            this.Controls.Add(this.cbMS1TolUnit);
            this.Name = "SearchSettingsControl";
            this.Size = new System.Drawing.Size(381, 425);
            ((System.ComponentModel.ISupportInitialize)(this.pBLogo)).EndInit();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ComboBox cbMS1TolUnit;
        private System.Windows.Forms.Label lblMs1Tolerance;
        private System.Windows.Forms.TextBox txtMS1Tolerance;
        private System.Windows.Forms.TextBox txtMS2Tolerance;
        private System.Windows.Forms.Label lblMs2Tolerance;
        private System.Windows.Forms.ComboBox cbMS2TolUnit;
        private System.Windows.Forms.Label lblFragmentIons;
        private System.Windows.Forms.ComboBox cbFragmentIons;
        private System.Windows.Forms.Button btnAdditionalSettings;
        private System.Windows.Forms.Label lblSearchEngineName;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.PictureBox pBLogo;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
    }
}