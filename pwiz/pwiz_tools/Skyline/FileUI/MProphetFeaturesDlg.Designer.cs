namespace pwiz.Skyline.FileUI
{
    partial class MProphetFeaturesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MProphetFeaturesDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.comboMainVar = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.checkedListVars = new System.Windows.Forms.CheckedListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.checkBoxBestOnly = new System.Windows.Forms.CheckBox();
            this.checkBoxTargetsOnly = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // comboMainVar
            // 
            resources.ApplyResources(this.comboMainVar, "comboMainVar");
            this.comboMainVar.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMainVar.FormattingEnabled = true;
            this.comboMainVar.Name = "comboMainVar";
            this.comboMainVar.SelectedIndexChanged += new System.EventHandler(this.comboMainVar_SelectedIndexChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // checkedListVars
            // 
            resources.ApplyResources(this.checkedListVars, "checkedListVars");
            this.checkedListVars.CheckOnClick = true;
            this.checkedListVars.FormattingEnabled = true;
            this.checkedListVars.Name = "checkedListVars";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // checkBoxBestOnly
            // 
            resources.ApplyResources(this.checkBoxBestOnly, "checkBoxBestOnly");
            this.checkBoxBestOnly.Name = "checkBoxBestOnly";
            this.checkBoxBestOnly.UseVisualStyleBackColor = true;
            // 
            // checkBoxTargetsOnly
            // 
            resources.ApplyResources(this.checkBoxTargetsOnly, "checkBoxTargetsOnly");
            this.checkBoxTargetsOnly.Name = "checkBoxTargetsOnly";
            this.checkBoxTargetsOnly.UseVisualStyleBackColor = true;
            // 
            // MProphetFeaturesDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.checkBoxTargetsOnly);
            this.Controls.Add(this.checkBoxBestOnly);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.checkedListVars);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboMainVar);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MProphetFeaturesDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.ComboBox comboMainVar;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckedListBox checkedListVars;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox checkBoxBestOnly;
        private System.Windows.Forms.CheckBox checkBoxTargetsOnly;
    }
}