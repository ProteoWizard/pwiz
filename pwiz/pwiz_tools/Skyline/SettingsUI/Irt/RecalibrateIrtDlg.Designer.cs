namespace pwiz.Skyline.SettingsUI.Irt
{
    partial class RecalibrateIrtDlg
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
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.textMaxIrt = new System.Windows.Forms.TextBox();
            this.textMinIrt = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.bindingSourceStandard = new System.Windows.Forms.BindingSource(this.components);
            this.label3 = new System.Windows.Forms.Label();
            this.comboFixedPoint1 = new System.Windows.Forms.ComboBox();
            this.comboFixedPoint2 = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(76, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Min iRT value:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 39);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(79, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Ma&x iRT value:";
            // 
            // textMaxIrt
            // 
            this.textMaxIrt.Location = new System.Drawing.Point(99, 36);
            this.textMaxIrt.Name = "textMaxIrt";
            this.textMaxIrt.Size = new System.Drawing.Size(100, 20);
            this.textMaxIrt.TabIndex = 3;
            this.textMaxIrt.Text = "100";
            // 
            // textMinIrt
            // 
            this.textMinIrt.Location = new System.Drawing.Point(99, 10);
            this.textMinIrt.Name = "textMinIrt";
            this.textMinIrt.Size = new System.Drawing.Size(100, 20);
            this.textMinIrt.TabIndex = 1;
            this.textMinIrt.Text = "0";
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(236, 8);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 7;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(236, 37);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 76);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(78, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "&Fixed peptides:";
            // 
            // comboFixedPoint1
            // 
            this.comboFixedPoint1.DisplayMember = "PeptideModSeq";
            this.comboFixedPoint1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFixedPoint1.FormattingEnabled = true;
            this.comboFixedPoint1.Location = new System.Drawing.Point(16, 93);
            this.comboFixedPoint1.Name = "comboFixedPoint1";
            this.comboFixedPoint1.Size = new System.Drawing.Size(295, 21);
            this.comboFixedPoint1.TabIndex = 5;
            this.comboFixedPoint1.SelectedIndexChanged += new System.EventHandler(this.comboFixedPoint1_SelectedIndexChanged);
            // 
            // comboFixedPoint2
            // 
            this.comboFixedPoint2.DisplayMember = "PeptideModSeq";
            this.comboFixedPoint2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFixedPoint2.FormattingEnabled = true;
            this.comboFixedPoint2.Location = new System.Drawing.Point(16, 121);
            this.comboFixedPoint2.Name = "comboFixedPoint2";
            this.comboFixedPoint2.Size = new System.Drawing.Size(295, 21);
            this.comboFixedPoint2.TabIndex = 6;
            this.comboFixedPoint2.SelectedIndexChanged += new System.EventHandler(this.comboFixedPoint2_SelectedIndexChanged);
            // 
            // RecalibrateIrtDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(323, 159);
            this.Controls.Add(this.comboFixedPoint2);
            this.Controls.Add(this.comboFixedPoint1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.textMinIrt);
            this.Controls.Add(this.textMaxIrt);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RecalibrateIrtDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Recalibrate iRT";
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textMaxIrt;
        private System.Windows.Forms.TextBox textMinIrt;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.BindingSource bindingSourceStandard;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboFixedPoint1;
        private System.Windows.Forms.ComboBox comboFixedPoint2;
    }
}