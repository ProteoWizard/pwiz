using System;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AreaChartPropertyDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textMaxArea = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cbDecimalCvs = new System.Windows.Forms.CheckBox();
            this.labelCvPercent = new System.Windows.Forms.Label();
            this.textMaxCv = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textSizeComboBox = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.cbShowDotpCutoff = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.dataGridDotpCutoffValues = new pwiz.Skyline.Controls.DataGridViewEx();
            this.DotpType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DotpCutoff = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.comboDotpDisplayType = new System.Windows.Forms.ComboBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDotpCutoffValues)).BeginInit();
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
            // textMaxArea
            // 
            resources.ApplyResources(this.textMaxArea, "textMaxArea");
            this.textMaxArea.Name = "textMaxArea";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cbDecimalCvs);
            this.groupBox1.Controls.Add(this.labelCvPercent);
            this.groupBox1.Controls.Add(this.textMaxCv);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.textMaxArea);
            this.groupBox1.Controls.Add(this.label2);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // cbDecimalCvs
            // 
            resources.ApplyResources(this.cbDecimalCvs, "cbDecimalCvs");
            this.cbDecimalCvs.Name = "cbDecimalCvs";
            this.cbDecimalCvs.UseVisualStyleBackColor = true;
            this.cbDecimalCvs.CheckedChanged += new System.EventHandler(this.cbDecimalCvs_CheckedChanged);
            // 
            // labelCvPercent
            // 
            resources.ApplyResources(this.labelCvPercent, "labelCvPercent");
            this.labelCvPercent.Name = "labelCvPercent";
            // 
            // textMaxCv
            // 
            resources.ApplyResources(this.textMaxCv, "textMaxCv");
            this.textMaxCv.Name = "textMaxCv";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // textSizeComboBox
            // 
            this.textSizeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.textSizeComboBox.FormattingEnabled = true;
            this.textSizeComboBox.Items.AddRange(new object[] {
            resources.GetString("textSizeComboBox.Items"),
            resources.GetString("textSizeComboBox.Items1"),
            resources.GetString("textSizeComboBox.Items2"),
            resources.GetString("textSizeComboBox.Items3"),
            resources.GetString("textSizeComboBox.Items4")});
            resources.ApplyResources(this.textSizeComboBox, "textSizeComboBox");
            this.textSizeComboBox.Name = "textSizeComboBox";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.cbShowDotpCutoff);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.dataGridDotpCutoffValues);
            this.groupBox2.Controls.Add(this.comboDotpDisplayType);
            this.groupBox2.Controls.Add(this.label3);
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // cbShowDotpCutoff
            // 
            resources.ApplyResources(this.cbShowDotpCutoff, "cbShowDotpCutoff");
            this.cbShowDotpCutoff.Name = "cbShowDotpCutoff";
            this.cbShowDotpCutoff.UseVisualStyleBackColor = true;
            this.cbShowDotpCutoff.CheckedChanged += new System.EventHandler(this.cbShowDotpCutoff_CheckedChanged);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // dataGridDotpCutoffValues
            // 
            this.dataGridDotpCutoffValues.AllowUserToAddRows = false;
            this.dataGridDotpCutoffValues.AllowUserToDeleteRows = false;
            this.dataGridDotpCutoffValues.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridDotpCutoffValues.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.DotpType,
            this.DotpCutoff});
            resources.ApplyResources(this.dataGridDotpCutoffValues, "dataGridDotpCutoffValues");
            this.dataGridDotpCutoffValues.Name = "dataGridDotpCutoffValues";
            // 
            // DotpType
            // 
            resources.ApplyResources(this.DotpType, "DotpType");
            this.DotpType.Name = "DotpType";
            this.DotpType.ReadOnly = true;
            // 
            // DotpCutoff
            // 
            resources.ApplyResources(this.DotpCutoff, "DotpCutoff");
            this.DotpCutoff.Name = "DotpCutoff";
            // 
            // comboDotpDisplayType
            // 
            this.comboDotpDisplayType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDotpDisplayType.FormattingEnabled = true;
            resources.ApplyResources(this.comboDotpDisplayType, "comboDotpDisplayType");
            this.comboDotpDisplayType.Name = "comboDotpDisplayType";
            this.comboDotpDisplayType.SelectedIndexChanged += this.comboDotpDisplayType_SelectedIndexChanged;
            // 
            // AreaChartPropertyDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.textSizeComboBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AreaChartPropertyDlg";
            this.ShowInTaskbar = false;
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDotpCutoffValues)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

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
        private System.Windows.Forms.ComboBox textSizeComboBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label4;
        private Controls.DataGridViewEx dataGridDotpCutoffValues;
        private System.Windows.Forms.DataGridViewTextBoxColumn DotpType;
        private System.Windows.Forms.DataGridViewTextBoxColumn DotpCutoff;
        private System.Windows.Forms.ComboBox comboDotpDisplayType;
        private System.Windows.Forms.CheckBox cbShowDotpCutoff;
    }
}