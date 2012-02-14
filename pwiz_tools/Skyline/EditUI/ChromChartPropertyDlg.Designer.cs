namespace pwiz.Skyline.EditUI
{
    partial class ChromChartPropertyDlg
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
            this.btnOk = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.textLineWidth = new System.Windows.Forms.TextBox();
            this.textFontSize = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textTimeRange = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.labelTimeUnits = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cbRelative = new System.Windows.Forms.CheckBox();
            this.textMaxIntensity = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(250, 39);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(250, 9);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 9);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(58, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "&Line width:";
            // 
            // textLineWidth
            // 
            this.textLineWidth.Location = new System.Drawing.Point(15, 25);
            this.textLineWidth.Name = "textLineWidth";
            this.textLineWidth.Size = new System.Drawing.Size(83, 20);
            this.textLineWidth.TabIndex = 1;
            // 
            // textFontSize
            // 
            this.textFontSize.Location = new System.Drawing.Point(147, 25);
            this.textFontSize.Name = "textFontSize";
            this.textFontSize.Size = new System.Drawing.Size(83, 20);
            this.textFontSize.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(144, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(52, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "&Font size:";
            // 
            // textTimeRange
            // 
            this.textTimeRange.Location = new System.Drawing.Point(26, 49);
            this.textTimeRange.Name = "textTimeRange";
            this.textTimeRange.Size = new System.Drawing.Size(116, 20);
            this.textTimeRange.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(23, 33);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(110, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "&Best peak time range:";
            // 
            // labelTimeUnits
            // 
            this.labelTimeUnits.AutoSize = true;
            this.labelTimeUnits.Location = new System.Drawing.Point(148, 52);
            this.labelTimeUnits.Name = "labelTimeUnits";
            this.labelTimeUnits.Size = new System.Drawing.Size(43, 13);
            this.labelTimeUnits.TabIndex = 2;
            this.labelTimeUnits.Text = "minutes";
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.cbRelative);
            this.groupBox1.Controls.Add(this.textMaxIntensity);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.textTimeRange);
            this.groupBox1.Controls.Add(this.labelTimeUnits);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Location = new System.Drawing.Point(15, 75);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(215, 187);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "&Graph area dimensions:";
            // 
            // cbRelative
            // 
            this.cbRelative.AutoSize = true;
            this.cbRelative.Location = new System.Drawing.Point(26, 72);
            this.cbRelative.Name = "cbRelative";
            this.cbRelative.Size = new System.Drawing.Size(116, 17);
            this.cbRelative.TabIndex = 5;
            this.cbRelative.Text = "&Peak width relative";
            this.cbRelative.UseVisualStyleBackColor = true;
            this.cbRelative.CheckedChanged += new System.EventHandler(this.cbRelative_CheckedChanged);
            // 
            // textMaxIntensity
            // 
            this.textMaxIntensity.Location = new System.Drawing.Point(26, 143);
            this.textMaxIntensity.Name = "textMaxIntensity";
            this.textMaxIntensity.Size = new System.Drawing.Size(116, 20);
            this.textMaxIntensity.TabIndex = 4;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(23, 127);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(96, 13);
            this.label5.TabIndex = 3;
            this.label5.Text = "&Maximum Intensity:";
            // 
            // ChromChartPropertyDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(337, 287);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.textFontSize);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textLineWidth);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ChromChartPropertyDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Chromatogram Graph Properties";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textLineWidth;
        private System.Windows.Forms.TextBox textFontSize;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textTimeRange;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label labelTimeUnits;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textMaxIntensity;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox cbRelative;
    }
}