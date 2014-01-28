namespace ExampleArgCollector
{
    partial class ExampleToolUI
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
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.checkBoxTest = new System.Windows.Forms.CheckBox();
            this.textBoxTest = new System.Windows.Forms.TextBox();
            this.labelTextBoxTest = new System.Windows.Forms.Label();
            this.comboBoxTest = new System.Windows.Forms.ComboBox();
            this.labelComboBoxTest = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(263, 12);
            this.btnOk.Margin = new System.Windows.Forms.Padding(4);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(100, 28);
            this.btnOk.TabIndex = 7;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(263, 48);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 28);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // checkBoxTest
            // 
            this.checkBoxTest.AutoSize = true;
            this.checkBoxTest.Location = new System.Drawing.Point(16, 15);
            this.checkBoxTest.Margin = new System.Windows.Forms.Padding(4);
            this.checkBoxTest.Name = "checkBoxTest";
            this.checkBoxTest.Size = new System.Drawing.Size(96, 21);
            this.checkBoxTest.TabIndex = 9;
            this.checkBoxTest.Text = "Check Box";
            this.checkBoxTest.UseVisualStyleBackColor = true;
            // 
            // textBoxTest
            // 
            this.textBoxTest.Location = new System.Drawing.Point(89, 46);
            this.textBoxTest.Margin = new System.Windows.Forms.Padding(4);
            this.textBoxTest.Name = "textBoxTest";
            this.textBoxTest.Size = new System.Drawing.Size(132, 22);
            this.textBoxTest.TabIndex = 10;
            // 
            // labelTextBoxTest
            // 
            this.labelTextBoxTest.AutoSize = true;
            this.labelTextBoxTest.Location = new System.Drawing.Point(12, 49);
            this.labelTextBoxTest.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelTextBoxTest.Name = "labelTextBoxTest";
            this.labelTextBoxTest.Size = new System.Drawing.Size(66, 17);
            this.labelTextBoxTest.TabIndex = 11;
            this.labelTextBoxTest.Text = "Text Box:";
            // 
            // comboBoxTest
            // 
            this.comboBoxTest.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTest.FormattingEnabled = true;
            this.comboBoxTest.Items.AddRange(new object[] {
            "Option 1",
            "Option 2",
            "Option 3",
            "Option 4"});
            this.comboBoxTest.Location = new System.Drawing.Point(108, 96);
            this.comboBoxTest.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxTest.Name = "comboBoxTest";
            this.comboBoxTest.Size = new System.Drawing.Size(112, 24);
            this.comboBoxTest.TabIndex = 12;
            // 
            // labelComboBoxTest
            // 
            this.labelComboBoxTest.AutoSize = true;
            this.labelComboBoxTest.Location = new System.Drawing.Point(15, 100);
            this.labelComboBoxTest.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelComboBoxTest.Name = "labelComboBoxTest";
            this.labelComboBoxTest.Size = new System.Drawing.Size(83, 17);
            this.labelComboBoxTest.TabIndex = 13;
            this.labelComboBoxTest.Text = "Combo Box:";
            // 
            // ExampleToolUI
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(379, 145);
            this.Controls.Add(this.labelComboBoxTest);
            this.Controls.Add(this.comboBoxTest);
            this.Controls.Add(this.labelTextBoxTest);
            this.Controls.Add(this.textBoxTest);
            this.Controls.Add(this.checkBoxTest);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExampleToolUI";
            this.ShowInTaskbar = false;
            this.Text = "ExampleToolUI";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.CheckBox checkBoxTest;
        private System.Windows.Forms.TextBox textBoxTest;
        private System.Windows.Forms.Label labelTextBoxTest;
        private System.Windows.Forms.ComboBox comboBoxTest;
        private System.Windows.Forms.Label labelComboBoxTest;
    }
}

