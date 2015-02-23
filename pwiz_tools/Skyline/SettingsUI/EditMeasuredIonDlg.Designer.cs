namespace pwiz.Skyline.SettingsUI
{
    partial class EditMeasuredIonDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditMeasuredIonDlg));
            this.label1 = new System.Windows.Forms.Label();
            this.textFragment = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textRestrict = new System.Windows.Forms.TextBox();
            this.comboDirection = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textName = new System.Windows.Forms.TextBox();
            this.labelName = new System.Windows.Forms.Label();
            this.radioFragment = new System.Windows.Forms.RadioButton();
            this.radioReporter = new System.Windows.Forms.RadioButton();
            this.textMinAas = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.labelCharge = new System.Windows.Forms.Label();
            this.textCharge = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textFragment
            // 
            resources.ApplyResources(this.textFragment, "textFragment");
            this.textFragment.Name = "textFragment";
            this.textFragment.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textAa_KeyPress);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textRestrict
            // 
            resources.ApplyResources(this.textRestrict, "textRestrict");
            this.textRestrict.Name = "textRestrict";
            this.textRestrict.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textAa_KeyPress);
            // 
            // comboDirection
            // 
            this.comboDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDirection.FormattingEnabled = true;
            this.comboDirection.Items.AddRange(new object[] {
            resources.GetString("comboDirection.Items"),
            resources.GetString("comboDirection.Items1")});
            resources.ApplyResources(this.comboDirection, "comboDirection");
            this.comboDirection.Name = "comboDirection";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
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
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            // 
            // labelName
            // 
            resources.ApplyResources(this.labelName, "labelName");
            this.labelName.Name = "labelName";
            // 
            // radioFragment
            // 
            resources.ApplyResources(this.radioFragment, "radioFragment");
            this.radioFragment.Checked = true;
            this.radioFragment.Name = "radioFragment";
            this.radioFragment.TabStop = true;
            this.radioFragment.UseVisualStyleBackColor = true;
            this.radioFragment.CheckedChanged += new System.EventHandler(this.radioFragment_CheckedChanged);
            // 
            // radioReporter
            // 
            resources.ApplyResources(this.radioReporter, "radioReporter");
            this.radioReporter.Name = "radioReporter";
            this.radioReporter.UseVisualStyleBackColor = true;
            // 
            // textMinAas
            // 
            resources.ApplyResources(this.textMinAas, "textMinAas");
            this.textMinAas.Name = "textMinAas";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // labelCharge
            // 
            resources.ApplyResources(this.labelCharge, "labelCharge");
            this.labelCharge.Name = "labelCharge";
            // 
            // textCharge
            // 
            resources.ApplyResources(this.textCharge, "textCharge");
            this.textCharge.Name = "textCharge";
            this.textCharge.TextChanged += new System.EventHandler(this.textCharge_TextChanged);
            // 
            // EditMeasuredIonDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.textCharge);
            this.Controls.Add(this.labelCharge);
            this.Controls.Add(this.textMinAas);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.radioReporter);
            this.Controls.Add(this.radioFragment);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.labelName);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.comboDirection);
            this.Controls.Add(this.textRestrict);
            this.Controls.Add(this.textFragment);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditMeasuredIonDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textFragment;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textRestrict;
        private System.Windows.Forms.ComboBox comboDirection;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.RadioButton radioFragment;
        private System.Windows.Forms.RadioButton radioReporter;
        private System.Windows.Forms.TextBox textMinAas;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label labelCharge;
        private System.Windows.Forms.TextBox textCharge;
        private System.Windows.Forms.Button btnOk;
    }
}