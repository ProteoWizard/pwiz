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
            this.textInterval = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboMargins = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.textMarginLeft = new System.Windows.Forms.TextBox();
            this.textMarginRight = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(174, 42);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 12;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(174, 13);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 11;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
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
            this.textStart.Size = new System.Drawing.Size(121, 20);
            this.textStart.TabIndex = 1;
            // 
            // textEnd
            // 
            this.textEnd.Location = new System.Drawing.Point(16, 79);
            this.textEnd.Name = "textEnd";
            this.textEnd.Size = new System.Drawing.Size(121, 20);
            this.textEnd.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 62);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(50, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "&End m/z:";
            // 
            // textInterval
            // 
            this.textInterval.Location = new System.Drawing.Point(16, 128);
            this.textInterval.Name = "textInterval";
            this.textInterval.Size = new System.Drawing.Size(121, 20);
            this.textInterval.TabIndex = 5;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 111);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(45, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "&Interval:";
            // 
            // comboMargins
            // 
            this.comboMargins.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMargins.FormattingEnabled = true;
            this.comboMargins.Items.AddRange(new object[] {
            "None",
            "Symmetric",
            "Asymmetric"});
            this.comboMargins.Location = new System.Drawing.Point(16, 181);
            this.comboMargins.Name = "comboMargins";
            this.comboMargins.Size = new System.Drawing.Size(121, 21);
            this.comboMargins.TabIndex = 7;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 165);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(47, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "&Margins:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(13, 215);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(70, 13);
            this.label5.TabIndex = 8;
            this.label5.Text = "Margin &width:";
            // 
            // textMarginLeft
            // 
            this.textMarginLeft.Location = new System.Drawing.Point(16, 231);
            this.textMarginLeft.Name = "textMarginLeft";
            this.textMarginLeft.Size = new System.Drawing.Size(55, 20);
            this.textMarginLeft.TabIndex = 9;
            // 
            // textMarginRight
            // 
            this.textMarginRight.Location = new System.Drawing.Point(82, 230);
            this.textMarginRight.Name = "textMarginRight";
            this.textMarginRight.Size = new System.Drawing.Size(55, 20);
            this.textMarginRight.TabIndex = 10;
            // 
            // CalculateIsolationSchemeDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(261, 270);
            this.Controls.Add(this.textMarginRight);
            this.Controls.Add(this.textMarginLeft);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.comboMargins);
            this.Controls.Add(this.textInterval);
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
        private System.Windows.Forms.TextBox textInterval;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboMargins;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textMarginLeft;
        private System.Windows.Forms.TextBox textMarginRight;
    }
}