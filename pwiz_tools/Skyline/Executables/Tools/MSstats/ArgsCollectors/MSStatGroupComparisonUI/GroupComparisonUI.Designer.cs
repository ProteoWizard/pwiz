namespace MSStatArgsCollector
{
    partial class GroupComparisonUi
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
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.comboBoxNormalizeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.comboControlGroup = new System.Windows.Forms.ComboBox();
            this.labelControlGroup = new System.Windows.Forms.Label();
            this.argsLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.cboxSelectHighQualityFeatures = new System.Windows.Forms.CheckBox();
            this.argsLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(205, 9);
            this.btnOK.Margin = new System.Windows.Forms.Padding(3, 3, 15, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 9;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(205, 38);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(3, 3, 15, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 0;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // comboBoxNormalizeTo
            // 
            this.comboBoxNormalizeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNormalizeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxNormalizeTo.FormattingEnabled = true;
            this.comboBoxNormalizeTo.Items.AddRange(new object[] {
            "None",
            "Equalize Medians",
            "Quantile",
            "Global Standards"});
            this.comboBoxNormalizeTo.Location = new System.Drawing.Point(12, 23);
            this.comboBoxNormalizeTo.Name = "comboBoxNormalizeTo";
            this.comboBoxNormalizeTo.Size = new System.Drawing.Size(172, 21);
            this.comboBoxNormalizeTo.TabIndex = 3;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(12, 6);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(111, 13);
            this.labelNormalizeTo.TabIndex = 2;
            this.labelNormalizeTo.Text = "Normalization method:";
            // 
            // comboControlGroup
            // 
            this.comboControlGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboControlGroup.FormattingEnabled = true;
            this.comboControlGroup.Location = new System.Drawing.Point(3, 26);
            this.comboControlGroup.Name = "comboControlGroup";
            this.comboControlGroup.Size = new System.Drawing.Size(169, 21);
            this.comboControlGroup.TabIndex = 3;
            // 
            // labelControlGroup
            // 
            this.labelControlGroup.AutoSize = true;
            this.labelControlGroup.Location = new System.Drawing.Point(3, 10);
            this.labelControlGroup.Margin = new System.Windows.Forms.Padding(3, 10, 3, 0);
            this.labelControlGroup.Name = "labelControlGroup";
            this.labelControlGroup.Size = new System.Drawing.Size(73, 13);
            this.labelControlGroup.TabIndex = 2;
            this.labelControlGroup.Text = "&Control group:";
            // 
            // argsLayoutPanel
            // 
            this.argsLayoutPanel.AutoSize = true;
            this.argsLayoutPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.argsLayoutPanel.Controls.Add(this.labelControlGroup);
            this.argsLayoutPanel.Controls.Add(this.comboControlGroup);
            this.argsLayoutPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.argsLayoutPanel.Location = new System.Drawing.Point(12, 187);
            this.argsLayoutPanel.Name = "argsLayoutPanel";
            this.argsLayoutPanel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 10);
            this.argsLayoutPanel.Size = new System.Drawing.Size(175, 60);
            this.argsLayoutPanel.TabIndex = 8;
            // 
            // cboxSelectHighQualityFeatures
            // 
            this.cboxSelectHighQualityFeatures.AutoSize = true;
            this.cboxSelectHighQualityFeatures.Location = new System.Drawing.Point(16, 55);
            this.cboxSelectHighQualityFeatures.Name = "cboxSelectHighQualityFeatures";
            this.cboxSelectHighQualityFeatures.Size = new System.Drawing.Size(153, 17);
            this.cboxSelectHighQualityFeatures.TabIndex = 5;
            this.cboxSelectHighQualityFeatures.Text = "Select high quality features";
            this.cboxSelectHighQualityFeatures.UseVisualStyleBackColor = true;
            // 
            // GroupComparisonUi
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(292, 459);
            this.Controls.Add(this.cboxSelectHighQualityFeatures);
            this.Controls.Add(this.comboBoxNormalizeTo);
            this.Controls.Add(this.labelNormalizeTo);
            this.Controls.Add(this.argsLayoutPanel);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Cursor = System.Windows.Forms.Cursors.Default;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GroupComparisonUi";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "MSstats Group Comparison";
            this.TransparencyKey = System.Drawing.Color.DarkRed;
            this.argsLayoutPanel.ResumeLayout(false);
            this.argsLayoutPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ComboBox comboBoxNormalizeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.ComboBox comboControlGroup;
        private System.Windows.Forms.Label labelControlGroup;
        private System.Windows.Forms.FlowLayoutPanel argsLayoutPanel;
        private System.Windows.Forms.CheckBox cboxSelectHighQualityFeatures;
    }
}
