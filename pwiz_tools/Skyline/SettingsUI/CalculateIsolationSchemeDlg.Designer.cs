namespace pwiz.Skyline.SettingsUI
{
    partial class CalculateIsolationSchemeDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CalculateIsolationSchemeDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textStart = new System.Windows.Forms.TextBox();
            this.textEnd = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textWidth = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboMargins = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.textMarginLeft = new System.Windows.Forms.TextBox();
            this.textMarginRight = new System.Windows.Forms.TextBox();
            this.textOverlap = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.cbOptimizeWindowPlacement = new System.Windows.Forms.CheckBox();
            this.cbGenerateMethodTarget = new System.Windows.Forms.CheckBox();
            this.labelWindowCount = new System.Windows.Forms.Label();
            this.cbMultiplexed = new System.Windows.Forms.CheckBox();
            this.textWindowsPerScan = new System.Windows.Forms.TextBox();
            this.labelWindowsPerScan = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(250, 42);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 22;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(250, 13);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 21;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Start m/z:";
            // 
            // textStart
            // 
            this.textStart.Location = new System.Drawing.Point(16, 30);
            this.textStart.Name = "textStart";
            this.textStart.Size = new System.Drawing.Size(55, 20);
            this.textStart.TabIndex = 1;
            this.textStart.TextChanged += new System.EventHandler(this.textStart_TextChanged);
            // 
            // textEnd
            // 
            this.textEnd.Location = new System.Drawing.Point(106, 30);
            this.textEnd.Name = "textEnd";
            this.textEnd.Size = new System.Drawing.Size(55, 20);
            this.textEnd.TabIndex = 3;
            this.textEnd.TextChanged += new System.EventHandler(this.textEnd_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(103, 13);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(50, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "&End m/z:";
            // 
            // textWidth
            // 
            this.textWidth.Location = new System.Drawing.Point(16, 79);
            this.textWidth.Name = "textWidth";
            this.textWidth.Size = new System.Drawing.Size(55, 20);
            this.textWidth.TabIndex = 5;
            this.textWidth.TextChanged += new System.EventHandler(this.textWidth_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 62);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(77, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "&Window width:";
            // 
            // comboMargins
            // 
            this.comboMargins.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.comboMargins.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMargins.FormattingEnabled = true;
            this.comboMargins.Location = new System.Drawing.Point(204, 185);
            this.comboMargins.Name = "comboMargins";
            this.comboMargins.Size = new System.Drawing.Size(121, 21);
            this.comboMargins.TabIndex = 15;
            this.comboMargins.SelectedIndexChanged += new System.EventHandler(this.comboMargins_SelectedIndexChanged);
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(201, 169);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(47, 13);
            this.label4.TabIndex = 14;
            this.label4.Text = "&Margins:";
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(201, 220);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(70, 13);
            this.label5.TabIndex = 16;
            this.label5.Text = "Mar&gin width:";
            // 
            // textMarginLeft
            // 
            this.textMarginLeft.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.textMarginLeft.Location = new System.Drawing.Point(204, 236);
            this.textMarginLeft.Name = "textMarginLeft";
            this.textMarginLeft.Size = new System.Drawing.Size(55, 20);
            this.textMarginLeft.TabIndex = 17;
            // 
            // textMarginRight
            // 
            this.textMarginRight.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.textMarginRight.Location = new System.Drawing.Point(270, 236);
            this.textMarginRight.Name = "textMarginRight";
            this.textMarginRight.Size = new System.Drawing.Size(55, 20);
            this.textMarginRight.TabIndex = 18;
            // 
            // textOverlap
            // 
            this.textOverlap.Location = new System.Drawing.Point(106, 79);
            this.textOverlap.Name = "textOverlap";
            this.textOverlap.Size = new System.Drawing.Size(42, 20);
            this.textOverlap.TabIndex = 7;
            this.textOverlap.TextChanged += new System.EventHandler(this.textOverlap_TextChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(103, 62);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(47, 13);
            this.label6.TabIndex = 6;
            this.label6.Text = "&Overlap:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(149, 82);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(15, 13);
            this.label7.TabIndex = 8;
            this.label7.Text = "%";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.label8.Location = new System.Drawing.Point(13, 112);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(79, 13);
            this.label8.TabIndex = 9;
            this.label8.Text = "Window count:";
            // 
            // cbOptimizeWindowPlacement
            // 
            this.cbOptimizeWindowPlacement.AutoSize = true;
            this.cbOptimizeWindowPlacement.Location = new System.Drawing.Point(16, 278);
            this.cbOptimizeWindowPlacement.Name = "cbOptimizeWindowPlacement";
            this.cbOptimizeWindowPlacement.Size = new System.Drawing.Size(157, 17);
            this.cbOptimizeWindowPlacement.TabIndex = 19;
            this.cbOptimizeWindowPlacement.Text = "Optimize window &placement";
            this.cbOptimizeWindowPlacement.UseVisualStyleBackColor = true;
            this.cbOptimizeWindowPlacement.CheckedChanged += new System.EventHandler(this.cbOptimizeWindowPlacement_CheckedChanged);
            // 
            // cbGenerateMethodTarget
            // 
            this.cbGenerateMethodTarget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbGenerateMethodTarget.AutoSize = true;
            this.cbGenerateMethodTarget.Location = new System.Drawing.Point(204, 278);
            this.cbGenerateMethodTarget.Name = "cbGenerateMethodTarget";
            this.cbGenerateMethodTarget.Size = new System.Drawing.Size(100, 17);
            this.cbGenerateMethodTarget.TabIndex = 20;
            this.cbGenerateMethodTarget.Text = "Generate &target";
            this.cbGenerateMethodTarget.UseVisualStyleBackColor = true;
            // 
            // labelWindowCount
            // 
            this.labelWindowCount.AutoSize = true;
            this.labelWindowCount.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.labelWindowCount.Location = new System.Drawing.Point(90, 112);
            this.labelWindowCount.Name = "labelWindowCount";
            this.labelWindowCount.Size = new System.Drawing.Size(0, 13);
            this.labelWindowCount.TabIndex = 10;
            // 
            // cbMultiplexed
            // 
            this.cbMultiplexed.AutoSize = true;
            this.cbMultiplexed.Location = new System.Drawing.Point(16, 168);
            this.cbMultiplexed.Name = "cbMultiplexed";
            this.cbMultiplexed.Size = new System.Drawing.Size(132, 17);
            this.cbMultiplexed.TabIndex = 11;
            this.cbMultiplexed.Text = "Multiplexed &acquisition";
            this.cbMultiplexed.UseVisualStyleBackColor = true;
            this.cbMultiplexed.CheckedChanged += new System.EventHandler(this.cbMultiplexed_CheckedChanged);
            // 
            // textWindowsPerScan
            // 
            this.textWindowsPerScan.Enabled = false;
            this.textWindowsPerScan.Location = new System.Drawing.Point(35, 205);
            this.textWindowsPerScan.Name = "textWindowsPerScan";
            this.textWindowsPerScan.Size = new System.Drawing.Size(39, 20);
            this.textWindowsPerScan.TabIndex = 13;
            this.textWindowsPerScan.TextChanged += new System.EventHandler(this.textWindowsPerScan_TextChanged);
            // 
            // labelWindowsPerScan
            // 
            this.labelWindowsPerScan.AutoSize = true;
            this.labelWindowsPerScan.Enabled = false;
            this.labelWindowsPerScan.Location = new System.Drawing.Point(32, 189);
            this.labelWindowsPerScan.Name = "labelWindowsPerScan";
            this.labelWindowsPerScan.Size = new System.Drawing.Size(98, 13);
            this.labelWindowsPerScan.TabIndex = 12;
            this.labelWindowsPerScan.Text = "Win&dows per scan:";
            // 
            // CalculateIsolationSchemeDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(337, 311);
            this.Controls.Add(this.cbMultiplexed);
            this.Controls.Add(this.textWindowsPerScan);
            this.Controls.Add(this.labelWindowsPerScan);
            this.Controls.Add(this.labelWindowCount);
            this.Controls.Add(this.cbGenerateMethodTarget);
            this.Controls.Add(this.cbOptimizeWindowPlacement);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.textOverlap);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textMarginRight);
            this.Controls.Add(this.textMarginLeft);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.comboMargins);
            this.Controls.Add(this.textWidth);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textEnd);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textStart);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CalculateIsolationSchemeDlg";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Calculate Isolation Scheme";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textStart;
        private System.Windows.Forms.TextBox textEnd;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textWidth;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboMargins;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textMarginLeft;
        private System.Windows.Forms.TextBox textMarginRight;
        private System.Windows.Forms.TextBox textOverlap;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.CheckBox cbOptimizeWindowPlacement;
        private System.Windows.Forms.CheckBox cbGenerateMethodTarget;
        private System.Windows.Forms.Label labelWindowCount;
        private System.Windows.Forms.CheckBox cbMultiplexed;
        private System.Windows.Forms.TextBox textWindowsPerScan;
        private System.Windows.Forms.Label labelWindowsPerScan;
    }
}
