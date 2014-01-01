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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RecalibrateIrtDlg));
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
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textMaxIrt
            // 
            resources.ApplyResources(this.textMaxIrt, "textMaxIrt");
            this.textMaxIrt.Name = "textMaxIrt";
            // 
            // textMinIrt
            // 
            resources.ApplyResources(this.textMinIrt, "textMinIrt");
            this.textMinIrt.Name = "textMinIrt";
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
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // comboFixedPoint1
            // 
            this.comboFixedPoint1.DisplayMember = "PeptideModSeq";
            this.comboFixedPoint1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFixedPoint1.FormattingEnabled = true;
            resources.ApplyResources(this.comboFixedPoint1, "comboFixedPoint1");
            this.comboFixedPoint1.Name = "comboFixedPoint1";
            this.comboFixedPoint1.SelectedIndexChanged += new System.EventHandler(this.comboFixedPoint1_SelectedIndexChanged);
            // 
            // comboFixedPoint2
            // 
            this.comboFixedPoint2.DisplayMember = "PeptideModSeq";
            this.comboFixedPoint2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFixedPoint2.FormattingEnabled = true;
            resources.ApplyResources(this.comboFixedPoint2, "comboFixedPoint2");
            this.comboFixedPoint2.Name = "comboFixedPoint2";
            this.comboFixedPoint2.SelectedIndexChanged += new System.EventHandler(this.comboFixedPoint2_SelectedIndexChanged);
            // 
            // RecalibrateIrtDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
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