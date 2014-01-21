namespace pwiz.Skyline.EditUI
{
    partial class ReintegrateDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ReintegrateDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textBoxCutoff = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.reintegrateAllPeaks = new System.Windows.Forms.RadioButton();
            this.reintegrateQCutoff = new System.Windows.Forms.RadioButton();
            this.checkBoxOverwrite = new System.Windows.Forms.CheckBox();
            this.checkBoxAnnotation = new System.Windows.Forms.CheckBox();
            this.label36 = new System.Windows.Forms.Label();
            this.comboBoxScoringModel = new System.Windows.Forms.ComboBox();
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
            // textBoxCutoff
            // 
            resources.ApplyResources(this.textBoxCutoff, "textBoxCutoff");
            this.textBoxCutoff.Name = "textBoxCutoff";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // reintegrateAllPeaks
            // 
            resources.ApplyResources(this.reintegrateAllPeaks, "reintegrateAllPeaks");
            this.reintegrateAllPeaks.Checked = true;
            this.reintegrateAllPeaks.Name = "reintegrateAllPeaks";
            this.reintegrateAllPeaks.TabStop = true;
            this.reintegrateAllPeaks.UseVisualStyleBackColor = true;
            this.reintegrateAllPeaks.CheckedChanged += new System.EventHandler(this.reintegrateAllPeaks_CheckedChanged);
            // 
            // reintegrateQCutoff
            // 
            resources.ApplyResources(this.reintegrateQCutoff, "reintegrateQCutoff");
            this.reintegrateQCutoff.Name = "reintegrateQCutoff";
            this.reintegrateQCutoff.UseVisualStyleBackColor = true;
            this.reintegrateQCutoff.CheckedChanged += new System.EventHandler(this.reintegrateQCutoff_CheckedChanged);
            // 
            // checkBoxOverwrite
            // 
            resources.ApplyResources(this.checkBoxOverwrite, "checkBoxOverwrite");
            this.checkBoxOverwrite.Name = "checkBoxOverwrite";
            this.checkBoxOverwrite.UseVisualStyleBackColor = true;
            // 
            // checkBoxAnnotation
            // 
            resources.ApplyResources(this.checkBoxAnnotation, "checkBoxAnnotation");
            this.checkBoxAnnotation.Name = "checkBoxAnnotation";
            this.checkBoxAnnotation.UseVisualStyleBackColor = true;
            // 
            // label36
            // 
            resources.ApplyResources(this.label36, "label36");
            this.label36.Name = "label36";
            // 
            // comboBoxScoringModel
            // 
            this.comboBoxScoringModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxScoringModel.FormattingEnabled = true;
            resources.ApplyResources(this.comboBoxScoringModel, "comboBoxScoringModel");
            this.comboBoxScoringModel.Name = "comboBoxScoringModel";
            this.comboBoxScoringModel.SelectedIndexChanged += new System.EventHandler(this.comboBoxScoringModel_SelectedIndexChanged);
            // 
            // ReintegrateDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.comboBoxScoringModel);
            this.Controls.Add(this.label36);
            this.Controls.Add(this.checkBoxAnnotation);
            this.Controls.Add(this.checkBoxOverwrite);
            this.Controls.Add(this.reintegrateQCutoff);
            this.Controls.Add(this.reintegrateAllPeaks);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxCutoff);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ReintegrateDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textBoxCutoff;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.RadioButton reintegrateAllPeaks;
        private System.Windows.Forms.RadioButton reintegrateQCutoff;
        private System.Windows.Forms.CheckBox checkBoxOverwrite;
        private System.Windows.Forms.CheckBox checkBoxAnnotation;
        private System.Windows.Forms.Label label36;
        private System.Windows.Forms.ComboBox comboBoxScoringModel;
    }
}