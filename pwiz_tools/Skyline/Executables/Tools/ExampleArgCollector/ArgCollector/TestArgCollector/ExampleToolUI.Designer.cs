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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExampleToolUI));
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
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // checkBoxTest
            // 
            resources.ApplyResources(this.checkBoxTest, "checkBoxTest");
            this.checkBoxTest.Name = "checkBoxTest";
            this.checkBoxTest.UseVisualStyleBackColor = true;
            // 
            // textBoxTest
            // 
            resources.ApplyResources(this.textBoxTest, "textBoxTest");
            this.textBoxTest.Name = "textBoxTest";
            // 
            // labelTextBoxTest
            // 
            resources.ApplyResources(this.labelTextBoxTest, "labelTextBoxTest");
            this.labelTextBoxTest.Name = "labelTextBoxTest";
            // 
            // comboBoxTest
            // 
            this.comboBoxTest.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTest.FormattingEnabled = true;
            this.comboBoxTest.Items.AddRange(new object[] {
            resources.GetString("comboBoxTest.Items"),
            resources.GetString("comboBoxTest.Items1"),
            resources.GetString("comboBoxTest.Items2"),
            resources.GetString("comboBoxTest.Items3")});
            resources.ApplyResources(this.comboBoxTest, "comboBoxTest");
            this.comboBoxTest.Name = "comboBoxTest";
            // 
            // labelComboBoxTest
            // 
            resources.ApplyResources(this.labelComboBoxTest, "labelComboBoxTest");
            this.labelComboBoxTest.Name = "labelComboBoxTest";
            // 
            // ExampleToolUI
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.labelComboBoxTest);
            this.Controls.Add(this.comboBoxTest);
            this.Controls.Add(this.labelTextBoxTest);
            this.Controls.Add(this.textBoxTest);
            this.Controls.Add(this.checkBoxTest);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExampleToolUI";
            this.ShowInTaskbar = false;
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

