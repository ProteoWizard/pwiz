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
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.comboWindowType = new System.Windows.Forms.ComboBox();
            this.labelWindowType = new System.Windows.Forms.Label();
            this.textWindowsPerScan = new System.Windows.Forms.TextBox();
            this.labelWindowsPerScan = new System.Windows.Forms.Label();
            this.labelWindowCount = new System.Windows.Forms.Label();
            this.cbOptimizeWindowPlacement = new System.Windows.Forms.CheckBox();
            this.label8 = new System.Windows.Forms.Label();
            this.textMargin = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textWidth = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textEnd = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textStart = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.comboDeconv = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.textCERange = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // comboWindowType
            // 
            this.comboWindowType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboWindowType.FormattingEnabled = true;
            resources.ApplyResources(this.comboWindowType, "comboWindowType");
            this.comboWindowType.Name = "comboWindowType";
            this.comboWindowType.SelectedIndexChanged += new System.EventHandler(this.comboWindowType_SelectedIndexChanged);
            // 
            // labelWindowType
            // 
            resources.ApplyResources(this.labelWindowType, "labelWindowType");
            this.labelWindowType.Name = "labelWindowType";
            // 
            // textWindowsPerScan
            // 
            resources.ApplyResources(this.textWindowsPerScan, "textWindowsPerScan");
            this.textWindowsPerScan.Name = "textWindowsPerScan";
            this.textWindowsPerScan.TextChanged += new System.EventHandler(this.textWindowsPerScan_TextChanged);
            // 
            // labelWindowsPerScan
            // 
            resources.ApplyResources(this.labelWindowsPerScan, "labelWindowsPerScan");
            this.labelWindowsPerScan.Name = "labelWindowsPerScan";
            // 
            // labelWindowCount
            // 
            resources.ApplyResources(this.labelWindowCount, "labelWindowCount");
            this.labelWindowCount.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.labelWindowCount.Name = "labelWindowCount";
            // 
            // cbOptimizeWindowPlacement
            // 
            resources.ApplyResources(this.cbOptimizeWindowPlacement, "cbOptimizeWindowPlacement");
            this.cbOptimizeWindowPlacement.Name = "cbOptimizeWindowPlacement";
            this.cbOptimizeWindowPlacement.UseVisualStyleBackColor = true;
            this.cbOptimizeWindowPlacement.CheckedChanged += new System.EventHandler(this.cbOptimizeWindowPlacement_CheckedChanged);
            // 
            // label8
            // 
            resources.ApplyResources(this.label8, "label8");
            this.label8.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.label8.Name = "label8";
            // 
            // textMargin
            // 
            resources.ApplyResources(this.textMargin, "textMargin");
            this.textMargin.Name = "textMargin";
            this.textMargin.TextChanged += new System.EventHandler(this.textMargin_TextChanged);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // textWidth
            // 
            resources.ApplyResources(this.textWidth, "textWidth");
            this.textWidth.Name = "textWidth";
            this.textWidth.TextChanged += new System.EventHandler(this.textWidth_TextChanged);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textEnd
            // 
            resources.ApplyResources(this.textEnd, "textEnd");
            this.textEnd.Name = "textEnd";
            this.textEnd.TextChanged += new System.EventHandler(this.textEnd_TextChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textStart
            // 
            resources.ApplyResources(this.textStart, "textStart");
            this.textStart.Name = "textStart";
            this.textStart.TextChanged += new System.EventHandler(this.textStart_TextChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // comboDeconv
            // 
            this.comboDeconv.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDeconv.FormattingEnabled = true;
            resources.ApplyResources(this.comboDeconv, "comboDeconv");
            this.comboDeconv.Name = "comboDeconv";
            this.comboDeconv.SelectedIndexChanged += new System.EventHandler(this.comboDeconv_SelectedIndexChanged);
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // textCERange
            // 
            resources.ApplyResources(this.textCERange, "textCERange");
            this.textCERange.Name = "textCERange";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // CalculateIsolationSchemeDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.textCERange);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.comboDeconv);
            this.Controls.Add(this.comboWindowType);
            this.Controls.Add(this.labelWindowType);
            this.Controls.Add(this.textWindowsPerScan);
            this.Controls.Add(this.labelWindowsPerScan);
            this.Controls.Add(this.labelWindowCount);
            this.Controls.Add(this.cbOptimizeWindowPlacement);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.textMargin);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textWidth);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textEnd);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textStart);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CalculateIsolationSchemeDlg";
            this.ShowInTaskbar = false;
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
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textMargin;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.CheckBox cbOptimizeWindowPlacement;
        private System.Windows.Forms.Label labelWindowCount;
        private System.Windows.Forms.TextBox textWindowsPerScan;
        private System.Windows.Forms.Label labelWindowsPerScan;
        private System.Windows.Forms.Label labelWindowType;
        private System.Windows.Forms.ComboBox comboWindowType;
        private System.Windows.Forms.ComboBox comboDeconv;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textCERange;
        private System.Windows.Forms.Label label5;
    }
}
