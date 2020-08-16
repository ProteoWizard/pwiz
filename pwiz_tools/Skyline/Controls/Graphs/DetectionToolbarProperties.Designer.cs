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
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbAtLeastN)).BeginInit();
            this.gbAtLeastN.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(270, 54);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOk.Location = new System.Drawing.Point(270, 25);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 9;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 11);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(64, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Target type:";
            // 
            // cmbTargetType
            // 
            this.cmbTargetType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTargetType.FormattingEnabled = true;
            this.cmbTargetType.Items.AddRange(new object[] {
            "Precursors",
            "Peptides"});
            this.cmbTargetType.Location = new System.Drawing.Point(12, 28);
            this.cmbTargetType.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.cmbTargetType.Name = "cmbTargetType";
            this.cmbTargetType.Size = new System.Drawing.Size(104, 21);
            this.cmbTargetType.TabIndex = 1;
            this.cmbTargetType.SelectedIndexChanged += new System.EventHandler(this.cmbTargetType_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(123, 11);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(52, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "&Font size:";
            // 
            // cmbFontSize
            // 
            this.cmbFontSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFontSize.FormattingEnabled = true;
            this.cmbFontSize.Items.AddRange(new object[] {
            "x-small",
            "small",
            "normal",
            "large",
            "x-large"});
            this.cmbFontSize.Location = new System.Drawing.Point(125, 28);
            this.cmbFontSize.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.cmbFontSize.Name = "cmbFontSize";
            this.cmbFontSize.Size = new System.Drawing.Size(132, 21);
            this.cmbFontSize.TabIndex = 6;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 171);
            this.label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(63, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "&Y axis units:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.rbQValueCustom);
            this.groupBox1.Controls.Add(this.txtQValueCustom);
            this.groupBox1.Controls.Add(this.rbQValue01);
            this.groupBox1.Location = new System.Drawing.Point(12, 61);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox1.Size = new System.Drawing.Size(104, 108);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Q value cutoff";
            // 
            // rbQValueCustom
            // 
            this.rbQValueCustom.AutoSize = true;
            this.rbQValueCustom.Location = new System.Drawing.Point(4, 38);
            this.rbQValueCustom.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.rbQValueCustom.Name = "rbQValueCustom";
            this.rbQValueCustom.Size = new System.Drawing.Size(63, 17);
            this.rbQValueCustom.TabIndex = 2;
            this.rbQValueCustom.TabStop = true;
            this.rbQValueCustom.Text = "&Custom:";
            this.rbQValueCustom.UseVisualStyleBackColor = true;
            // 
            // txtQValueCustom
            // 
            this.txtQValueCustom.Location = new System.Drawing.Point(22, 60);
            this.txtQValueCustom.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtQValueCustom.Name = "txtQValueCustom";
            this.txtQValueCustom.Size = new System.Drawing.Size(71, 20);
            this.txtQValueCustom.TabIndex = 3;
            this.txtQValueCustom.Enter += new System.EventHandler(this.txtQValueCustom_Enter);
            // 
            // rbQValue01
            // 
            this.rbQValue01.AutoSize = true;
            this.rbQValue01.Location = new System.Drawing.Point(4, 17);
            this.rbQValue01.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.rbQValue01.Name = "rbQValue01";
            this.rbQValue01.Size = new System.Drawing.Size(46, 17);
            this.rbQValue01.TabIndex = 0;
            this.rbQValue01.TabStop = true;
            this.rbQValue01.Text = "0.0&1";
            this.rbQValue01.UseVisualStyleBackColor = true;
            // 
            // cmbCountMultiple
            // 
            this.cmbCountMultiple.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCountMultiple.FormattingEnabled = true;
            this.cmbCountMultiple.Items.AddRange(new object[] {
            "Ones",
            "Hundreds",
            "Thousands"});
            this.cmbCountMultiple.Location = new System.Drawing.Point(12, 188);
            this.cmbCountMultiple.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.cmbCountMultiple.Name = "cmbCountMultiple";
            this.cmbCountMultiple.Size = new System.Drawing.Size(104, 21);
            this.cmbCountMultiple.TabIndex = 4;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.cbShowLegend);
            this.groupBox2.Controls.Add(this.cbShowSelection);
            this.groupBox2.Controls.Add(this.cbShowMeanStd);
            this.groupBox2.Controls.Add(this.cbShowAtLeastN);
            this.groupBox2.Location = new System.Drawing.Point(125, 61);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox2.Size = new System.Drawing.Size(131, 108);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Labels and lines";
            // 
            // cbShowLegend
            // 
            this.cbShowLegend.AutoSize = true;
            this.cbShowLegend.Location = new System.Drawing.Point(5, 80);
            this.cbShowLegend.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.cbShowLegend.Name = "cbShowLegend";
            this.cbShowLegend.Size = new System.Drawing.Size(62, 17);
            this.cbShowLegend.TabIndex = 3;
            this.cbShowLegend.Text = "Le&gend";
            this.cbShowLegend.UseVisualStyleBackColor = true;
            // 
            // cbShowSelection
            // 
            this.cbShowSelection.AutoSize = true;
            this.cbShowSelection.Location = new System.Drawing.Point(5, 39);
            this.cbShowSelection.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.cbShowSelection.Name = "cbShowSelection";
            this.cbShowSelection.Size = new System.Drawing.Size(70, 17);
            this.cbShowSelection.TabIndex = 1;
            this.cbShowSelection.Text = "&Selection";
            this.cbShowSelection.UseVisualStyleBackColor = true;
            // 
            // cbShowMeanStd
            // 
            this.cbShowMeanStd.AutoSize = true;
            this.cbShowMeanStd.Location = new System.Drawing.Point(5, 58);
            this.cbShowMeanStd.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.cbShowMeanStd.Name = "cbShowMeanStd";
            this.cbShowMeanStd.Size = new System.Drawing.Size(93, 17);
            this.cbShowMeanStd.TabIndex = 2;
            this.cbShowMeanStd.Text = "M&ean && Stdev";
            this.cbShowMeanStd.UseVisualStyleBackColor = true;
            // 
            // cbShowAtLeastN
            // 
            this.cbShowAtLeastN.AutoSize = true;
            this.cbShowAtLeastN.Location = new System.Drawing.Point(5, 18);
            this.cbShowAtLeastN.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.cbShowAtLeastN.Name = "cbShowAtLeastN";
            this.cbShowAtLeastN.Size = new System.Drawing.Size(120, 17);
            this.cbShowAtLeastN.TabIndex = 0;
            this.cbShowAtLeastN.Text = "At least &N replicates";
            this.cbShowAtLeastN.UseVisualStyleBackColor = true;
            // 
            // tbAtLeastN
            // 
            this.tbAtLeastN.Location = new System.Drawing.Point(8, 17);
            this.tbAtLeastN.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tbAtLeastN.Minimum = 1;
            this.tbAtLeastN.Name = "tbAtLeastN";
            this.tbAtLeastN.Size = new System.Drawing.Size(114, 45);
            this.tbAtLeastN.TabIndex = 0;
            this.tbAtLeastN.TickStyle = System.Windows.Forms.TickStyle.Both;
            this.tbAtLeastN.Value = 1;
            this.tbAtLeastN.ValueChanged += new System.EventHandler(this.tbAtLeastN_ValueChanged);
            // 
            // gbAtLeastN
            // 
            this.gbAtLeastN.Controls.Add(this.tbAtLeastN);
            this.gbAtLeastN.Location = new System.Drawing.Point(125, 174);
            this.gbAtLeastN.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.gbAtLeastN.Name = "gbAtLeastN";
            this.gbAtLeastN.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.gbAtLeastN.Size = new System.Drawing.Size(131, 64);
            this.gbAtLeastN.TabIndex = 8;
            this.gbAtLeastN.TabStop = false;
            this.gbAtLeastN.Text = "At least N &replicates";
            // 
            // DetectionToolbarProperties
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(356, 249);
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
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DetectionToolbarProperties";
            this.ShowInTaskbar = false;
            this.Text = "Detection Plot Properties";
            this.Load += new System.EventHandler(this.DetectionToolbarProperties_Load);
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
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