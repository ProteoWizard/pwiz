namespace pwiz.Skyline.EditUI
{
    partial class AreaChartPropertyDlg
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
            this.textMaxArea = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cbDecimalCvs = new System.Windows.Forms.CheckBox();
            this.labelCvPercent = new System.Windows.Forms.Label();
            this.textMaxCv = new System.Windows.Forms.TextBox();
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
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(250, 9);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textMaxArea
            // 
            this.textMaxArea.Location = new System.Drawing.Point(26, 49);
            this.textMaxArea.Name = "textMaxArea";
            this.textMaxArea.Size = new System.Drawing.Size(107, 20);
            this.textMaxArea.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(23, 33);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(78, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "Maximum &area:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cbDecimalCvs);
            this.groupBox1.Controls.Add(this.labelCvPercent);
            this.groupBox1.Controls.Add(this.textMaxCv);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.textMaxArea);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(215, 185);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "&Graph area dimensions:";
            // 
            // cbDecimalCvs
            // 
            this.cbDecimalCvs.AutoSize = true;
            this.cbDecimalCvs.Location = new System.Drawing.Point(26, 152);
            this.cbDecimalCvs.Name = "cbDecimalCvs";
            this.cbDecimalCvs.Size = new System.Drawing.Size(150, 17);
            this.cbDecimalCvs.TabIndex = 5;
            this.cbDecimalCvs.Text = "&Display decimal CV values";
            this.cbDecimalCvs.UseVisualStyleBackColor = true;
            this.cbDecimalCvs.CheckedChanged += new System.EventHandler(this.cbDecimalCvs_CheckedChanged);
            // 
            // labelCvPercent
            // 
            this.labelCvPercent.AutoSize = true;
            this.labelCvPercent.Location = new System.Drawing.Point(137, 121);
            this.labelCvPercent.Name = "labelCvPercent";
            this.labelCvPercent.Size = new System.Drawing.Size(15, 13);
            this.labelCvPercent.TabIndex = 4;
            this.labelCvPercent.Text = "%";
            // 
            // textMaxCv
            // 
            this.textMaxCv.Location = new System.Drawing.Point(26, 115);
            this.textMaxCv.Name = "textMaxCv";
            this.textMaxCv.Size = new System.Drawing.Size(104, 20);
            this.textMaxCv.TabIndex = 3;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(23, 99);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(71, 13);
            this.label5.TabIndex = 2;
            this.label5.Text = "Maximum &CV:";
            // 
            // AreaChartPropertyDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(337, 209);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AreaChartPropertyDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Area Graph Properties";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textMaxArea;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textMaxCv;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label labelCvPercent;
        private System.Windows.Forms.CheckBox cbDecimalCvs;
    }
}