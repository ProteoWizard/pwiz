namespace pwiz.Skyline.FileUI
{
    partial class SchedulingOptionsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SchedulingOptionsDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.comboReplicateNames = new System.Windows.Forms.ComboBox();
            this.radioRTavg = new System.Windows.Forms.RadioButton();
            this.radioSingleDataSet = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.radioTrends = new System.Windows.Forms.RadioButton();
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
            // comboReplicateNames
            // 
            resources.ApplyResources(this.comboReplicateNames, "comboReplicateNames");
            this.comboReplicateNames.DisplayMember = "Name";
            this.comboReplicateNames.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboReplicateNames.FormattingEnabled = true;
            this.comboReplicateNames.Name = "comboReplicateNames";
            // 
            // radioRTavg
            // 
            resources.ApplyResources(this.radioRTavg, "radioRTavg");
            this.radioRTavg.Checked = true;
            this.radioRTavg.Name = "radioRTavg";
            this.radioRTavg.TabStop = true;
            this.radioRTavg.UseVisualStyleBackColor = true;
            // 
            // radioSingleDataSet
            // 
            resources.ApplyResources(this.radioSingleDataSet, "radioSingleDataSet");
            this.radioSingleDataSet.Name = "radioSingleDataSet";
            this.radioSingleDataSet.UseVisualStyleBackColor = true;
            this.radioSingleDataSet.CheckedChanged += new System.EventHandler(this.radioSingleDataSet_CheckedChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // radioTrends
            // 
            resources.ApplyResources(this.radioTrends, "radioTrends");
            this.radioTrends.Name = "radioTrends";
            this.radioTrends.TabStop = true;
            this.radioTrends.UseVisualStyleBackColor = true;
            // 
            // SchedulingOptionsDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.radioTrends);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.radioSingleDataSet);
            this.Controls.Add(this.radioRTavg);
            this.Controls.Add(this.comboReplicateNames);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SchedulingOptionsDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.ComboBox comboReplicateNames;
        private System.Windows.Forms.RadioButton radioRTavg;
        private System.Windows.Forms.RadioButton radioSingleDataSet;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton radioTrends;
    }
}