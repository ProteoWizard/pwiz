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
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(234, 42);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(234, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // comboReplicateNames
            // 
            this.comboReplicateNames.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.comboReplicateNames.DisplayMember = "Name";
            this.comboReplicateNames.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboReplicateNames.Enabled = false;
            this.comboReplicateNames.FormattingEnabled = true;
            this.comboReplicateNames.Location = new System.Drawing.Point(41, 94);
            this.comboReplicateNames.Name = "comboReplicateNames";
            this.comboReplicateNames.Size = new System.Drawing.Size(162, 21);
            this.comboReplicateNames.TabIndex = 3;
            // 
            // radioRTavg
            // 
            this.radioRTavg.AutoSize = true;
            this.radioRTavg.Checked = true;
            this.radioRTavg.Location = new System.Drawing.Point(22, 18);
            this.radioRTavg.Name = "radioRTavg";
            this.radioRTavg.Size = new System.Drawing.Size(152, 17);
            this.radioRTavg.TabIndex = 0;
            this.radioRTavg.TabStop = true;
            this.radioRTavg.Text = "Use retention time average";
            this.radioRTavg.UseVisualStyleBackColor = true;
            // 
            // radioSingleDataSet
            // 
            this.radioSingleDataSet.AutoSize = true;
            this.radioSingleDataSet.Location = new System.Drawing.Point(22, 48);
            this.radioSingleDataSet.Name = "radioSingleDataSet";
            this.radioSingleDataSet.Size = new System.Drawing.Size(181, 17);
            this.radioSingleDataSet.TabIndex = 1;
            this.radioSingleDataSet.Text = "Use values from a single data set";
            this.radioSingleDataSet.UseVisualStyleBackColor = true;
            this.radioSingleDataSet.CheckedChanged += new System.EventHandler(this.radioSingleDataSet_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(38, 78);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(55, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Replicate:";
            // 
            // radioTrends
            // 
            this.radioTrends.AutoSize = true;
            this.radioTrends.Location = new System.Drawing.Point(201, 121);
            this.radioTrends.Name = "radioTrends";
            this.radioTrends.Size = new System.Drawing.Size(108, 17);
            this.radioTrends.TabIndex = 4;
            this.radioTrends.TabStop = true;
            this.radioTrends.Text = "Use trends option";
            this.radioTrends.UseVisualStyleBackColor = true;
            this.radioTrends.Visible = false;
            // 
            // SchedulingOptionsDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(321, 138);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Scheduling Data";
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