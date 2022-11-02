namespace pwiz.Skyline.Controls.Graphs
{
    partial class DetectionToolbarProperties
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DetectionToolbarProperties));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.cmbTargetType = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cmbFontSize = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.rbQValueCustom = new System.Windows.Forms.RadioButton();
            this.txtQValueCustom = new System.Windows.Forms.TextBox();
            this.rbQValue01 = new System.Windows.Forms.RadioButton();
            this.cmbCountMultiple = new System.Windows.Forms.ComboBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.cbShowLegend = new System.Windows.Forms.CheckBox();
            this.cbShowSelection = new System.Windows.Forms.CheckBox();
            this.cbShowMeanStd = new System.Windows.Forms.CheckBox();
            this.cbShowAtLeastN = new System.Windows.Forms.CheckBox();
            this.tbAtLeastN = new System.Windows.Forms.TrackBar();
            this.gbAtLeastN = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbAtLeastN)).BeginInit();
            this.gbAtLeastN.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            resources.ApplyResources(this.btnCancel, "btnCancel");
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
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // cmbTargetType
            // 
            this.cmbTargetType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTargetType.FormattingEnabled = true;
            this.cmbTargetType.Items.AddRange(new object[] {
            resources.GetString("cmbTargetType.Items"),
            resources.GetString("cmbTargetType.Items1")});
            resources.ApplyResources(this.cmbTargetType, "cmbTargetType");
            this.cmbTargetType.Name = "cmbTargetType";
            this.cmbTargetType.SelectedIndexChanged += new System.EventHandler(this.cmbTargetType_SelectedIndexChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // cmbFontSize
            // 
            this.cmbFontSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFontSize.FormattingEnabled = true;
            this.cmbFontSize.Items.AddRange(new object[] {
            resources.GetString("cmbFontSize.Items"),
            resources.GetString("cmbFontSize.Items1"),
            resources.GetString("cmbFontSize.Items2"),
            resources.GetString("cmbFontSize.Items3"),
            resources.GetString("cmbFontSize.Items4")});
            resources.ApplyResources(this.cmbFontSize, "cmbFontSize");
            this.cmbFontSize.Name = "cmbFontSize";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.rbQValueCustom);
            this.groupBox1.Controls.Add(this.txtQValueCustom);
            this.groupBox1.Controls.Add(this.rbQValue01);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // rbQValueCustom
            // 
            resources.ApplyResources(this.rbQValueCustom, "rbQValueCustom");
            this.rbQValueCustom.Name = "rbQValueCustom";
            this.rbQValueCustom.TabStop = true;
            this.rbQValueCustom.UseVisualStyleBackColor = true;
            // 
            // txtQValueCustom
            // 
            resources.ApplyResources(this.txtQValueCustom, "txtQValueCustom");
            this.txtQValueCustom.Name = "txtQValueCustom";
            this.txtQValueCustom.Enter += new System.EventHandler(this.txtQValueCustom_Enter);
            // 
            // rbQValue01
            // 
            resources.ApplyResources(this.rbQValue01, "rbQValue01");
            this.rbQValue01.Name = "rbQValue01";
            this.rbQValue01.TabStop = true;
            this.rbQValue01.UseVisualStyleBackColor = true;
            // 
            // cmbCountMultiple
            // 
            this.cmbCountMultiple.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCountMultiple.FormattingEnabled = true;
            this.cmbCountMultiple.Items.AddRange(new object[] {
            resources.GetString("cmbCountMultiple.Items"),
            resources.GetString("cmbCountMultiple.Items1"),
            resources.GetString("cmbCountMultiple.Items2")});
            resources.ApplyResources(this.cmbCountMultiple, "cmbCountMultiple");
            this.cmbCountMultiple.Name = "cmbCountMultiple";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.cbShowLegend);
            this.groupBox2.Controls.Add(this.cbShowSelection);
            this.groupBox2.Controls.Add(this.cbShowMeanStd);
            this.groupBox2.Controls.Add(this.cbShowAtLeastN);
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // cbShowLegend
            // 
            resources.ApplyResources(this.cbShowLegend, "cbShowLegend");
            this.cbShowLegend.Name = "cbShowLegend";
            this.cbShowLegend.UseVisualStyleBackColor = true;
            // 
            // cbShowSelection
            // 
            resources.ApplyResources(this.cbShowSelection, "cbShowSelection");
            this.cbShowSelection.Name = "cbShowSelection";
            this.cbShowSelection.UseVisualStyleBackColor = true;
            // 
            // cbShowMeanStd
            // 
            resources.ApplyResources(this.cbShowMeanStd, "cbShowMeanStd");
            this.cbShowMeanStd.Name = "cbShowMeanStd";
            this.cbShowMeanStd.UseVisualStyleBackColor = true;
            // 
            // cbShowAtLeastN
            // 
            resources.ApplyResources(this.cbShowAtLeastN, "cbShowAtLeastN");
            this.cbShowAtLeastN.Name = "cbShowAtLeastN";
            this.cbShowAtLeastN.UseVisualStyleBackColor = true;
            // 
            // tbAtLeastN
            // 
            resources.ApplyResources(this.tbAtLeastN, "tbAtLeastN");
            this.tbAtLeastN.Minimum = 1;
            this.tbAtLeastN.Name = "tbAtLeastN";
            this.tbAtLeastN.TickStyle = System.Windows.Forms.TickStyle.Both;
            this.tbAtLeastN.Value = 1;
            this.tbAtLeastN.ValueChanged += new System.EventHandler(this.tbAtLeastN_ValueChanged);
            // 
            // gbAtLeastN
            // 
            this.gbAtLeastN.Controls.Add(this.tbAtLeastN);
            resources.ApplyResources(this.gbAtLeastN, "gbAtLeastN");
            this.gbAtLeastN.Name = "gbAtLeastN";
            this.gbAtLeastN.TabStop = false;
            // 
            // DetectionToolbarProperties
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ControlBox = false;
            this.Controls.Add(this.gbAtLeastN);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.cmbCountMultiple);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cmbFontSize);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cmbTargetType);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DetectionToolbarProperties";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.DetectionToolbarProperties_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbAtLeastN)).EndInit();
            this.gbAtLeastN.ResumeLayout(false);
            this.gbAtLeastN.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cmbTargetType;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cmbFontSize;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtQValueCustom;
        private System.Windows.Forms.RadioButton rbQValue01;
        private System.Windows.Forms.ComboBox cmbCountMultiple;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.CheckBox cbShowSelection;
        private System.Windows.Forms.CheckBox cbShowMeanStd;
        private System.Windows.Forms.CheckBox cbShowAtLeastN;
        private System.Windows.Forms.TrackBar tbAtLeastN;
        private System.Windows.Forms.GroupBox gbAtLeastN;
        private System.Windows.Forms.RadioButton rbQValueCustom;
        private System.Windows.Forms.CheckBox cbShowLegend;
    }
}