namespace pwiz.Topograph.ui.Forms
{
    partial class MiscSettingsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MiscSettingsForm));
            this.label1 = new System.Windows.Forms.Label();
            this.tbxMassAccuracy = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.comboTracerCountType = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.cbxWeightSignalAbsenceMore = new System.Windows.Forms.CheckBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.tbxProteinDescriptionKey = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(236, 184);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(83, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Mass Accuracy:";
            // 
            // tbxMassAccuracy
            // 
            this.tbxMassAccuracy.Location = new System.Drawing.Point(346, 184);
            this.tbxMassAccuracy.Name = "tbxMassAccuracy";
            this.tbxMassAccuracy.Size = new System.Drawing.Size(136, 20);
            this.tbxMassAccuracy.TabIndex = 1;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(382, 443);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(76, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(464, 443);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(67, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(20, 97);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(503, 74);
            this.label2.TabIndex = 3;
            this.label2.Text = resources.GetString("label2.Text");
            // 
            // comboTracerCountType
            // 
            this.comboTracerCountType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTracerCountType.FormattingEnabled = true;
            this.comboTracerCountType.Location = new System.Drawing.Point(346, 73);
            this.comboTracerCountType.Name = "comboTracerCountType";
            this.comboTracerCountType.Size = new System.Drawing.Size(135, 21);
            this.comboTracerCountType.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(236, 73);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(88, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "More useful form:";
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(20, 9);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(503, 61);
            this.label4.TabIndex = 6;
            this.label4.Text = resources.GetString("label4.Text");
            // 
            // label6
            // 
            this.label6.Location = new System.Drawing.Point(20, 226);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(503, 76);
            this.label6.TabIndex = 8;
            this.label6.Text = resources.GetString("label6.Text");
            // 
            // cbxWeightSignalAbsenceMore
            // 
            this.cbxWeightSignalAbsenceMore.AutoSize = true;
            this.cbxWeightSignalAbsenceMore.Location = new System.Drawing.Point(23, 285);
            this.cbxWeightSignalAbsenceMore.Name = "cbxWeightSignalAbsenceMore";
            this.cbxWeightSignalAbsenceMore.Size = new System.Drawing.Size(249, 17);
            this.cbxWeightSignalAbsenceMore.TabIndex = 9;
            this.cbxWeightSignalAbsenceMore.Text = "Weight absence of signal higher than presence";
            this.cbxWeightSignalAbsenceMore.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(22, 316);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(501, 42);
            this.label5.TabIndex = 10;
            this.label5.Text = "What part of the protein description is most useful?  Enter a regular expression." +
                "  For example, for the CG Number enter: CG[0-9]*";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(169, 358);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(150, 13);
            this.label7.TabIndex = 11;
            this.label7.Text = "Key part of protein description:";
            // 
            // tbxProteinDescriptionKey
            // 
            this.tbxProteinDescriptionKey.Location = new System.Drawing.Point(337, 355);
            this.tbxProteinDescriptionKey.Name = "tbxProteinDescriptionKey";
            this.tbxProteinDescriptionKey.Size = new System.Drawing.Size(144, 20);
            this.tbxProteinDescriptionKey.TabIndex = 12;
            // 
            // MiscSettingsForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(550, 478);
            this.Controls.Add(this.tbxProteinDescriptionKey);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.cbxWeightSignalAbsenceMore);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.comboTracerCountType);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tbxMassAccuracy);
            this.Controls.Add(this.btnOK);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MiscSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "MachineSettingsForm";
            this.Text = "Miscellaneous Settings";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxMassAccuracy;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboTracerCountType;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.CheckBox cbxWeightSignalAbsenceMore;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox tbxProteinDescriptionKey;
    }
}