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
            this.labelName = new System.Windows.Forms.Label();
            this.textBoxName = new System.Windows.Forms.TextBox();
            this.comboBoxNormalizeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.cboxAllowMissingPeaks = new System.Windows.Forms.CheckBox();
            this.ComparisonGroups = new System.Windows.Forms.ListBox();
            this.labelComparisonGroups = new System.Windows.Forms.Label();
            this.ControlGroup = new System.Windows.Forms.ComboBox();
            this.labelControlGroup = new System.Windows.Forms.Label();
            this.argsLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.cboxSelectHighQualityFeatures = new System.Windows.Forms.CheckBox();
            this.cboxRemoveInterferedProteins = new System.Windows.Forms.CheckBox();
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
            this.btnOK.TabIndex = 3;
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
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(14, 6);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(107, 13);
            this.labelName.TabIndex = 0;
            this.labelName.Text = "&Name of comparison:";
            // 
            // textBoxName
            // 
            this.textBoxName.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.textBoxName.Location = new System.Drawing.Point(14, 23);
            this.textBoxName.Name = "textBoxName";
            this.textBoxName.Size = new System.Drawing.Size(169, 20);
            this.textBoxName.TabIndex = 1;
            // 
            // comboBoxNormalizeTo
            // 
            this.comboBoxNormalizeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNormalizeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxNormalizeTo.FormattingEnabled = true;
            this.comboBoxNormalizeTo.Items.AddRange(new object[] {
            "None",
            "Equalize medians",
            "Quantile",
            "Relative to global standards"});
            this.comboBoxNormalizeTo.Location = new System.Drawing.Point(13, 67);
            this.comboBoxNormalizeTo.Name = "comboBoxNormalizeTo";
            this.comboBoxNormalizeTo.Size = new System.Drawing.Size(172, 21);
            this.comboBoxNormalizeTo.TabIndex = 1;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(13, 50);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(111, 13);
            this.labelNormalizeTo.TabIndex = 0;
            this.labelNormalizeTo.Text = "Normalization method:";
            // 
            // cboxAllowMissingPeaks
            // 
            this.cboxAllowMissingPeaks.AutoSize = true;
            this.cboxAllowMissingPeaks.Location = new System.Drawing.Point(14, 92);
            this.cboxAllowMissingPeaks.Name = "cboxAllowMissingPeaks";
            this.cboxAllowMissingPeaks.Size = new System.Drawing.Size(120, 17);
            this.cboxAllowMissingPeaks.TabIndex = 2;
            this.cboxAllowMissingPeaks.Text = "&Allow missing peaks";
            this.cboxAllowMissingPeaks.UseVisualStyleBackColor = true;
            // 
            // ComparisonGroups
            // 
            this.ComparisonGroups.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.ComparisonGroups.FormattingEnabled = true;
            this.ComparisonGroups.Location = new System.Drawing.Point(3, 76);
            this.ComparisonGroups.Name = "ComparisonGroups";
            this.ComparisonGroups.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this.ComparisonGroups.Size = new System.Drawing.Size(169, 43);
            this.ComparisonGroups.TabIndex = 5;
            this.ComparisonGroups.Visible = false;
            // 
            // labelComparisonGroups
            // 
            this.labelComparisonGroups.AutoSize = true;
            this.labelComparisonGroups.Location = new System.Drawing.Point(3, 60);
            this.labelComparisonGroups.Margin = new System.Windows.Forms.Padding(3, 10, 3, 0);
            this.labelComparisonGroups.Name = "labelComparisonGroups";
            this.labelComparisonGroups.Size = new System.Drawing.Size(174, 13);
            this.labelComparisonGroups.TabIndex = 4;
            this.labelComparisonGroups.Text = "&Select group(s) to compare against:";
            this.labelComparisonGroups.Visible = false;
            // 
            // ControlGroup
            // 
            this.ControlGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ControlGroup.FormattingEnabled = true;
            this.ControlGroup.Location = new System.Drawing.Point(3, 26);
            this.ControlGroup.Name = "ControlGroup";
            this.ControlGroup.Size = new System.Drawing.Size(169, 21);
            this.ControlGroup.TabIndex = 3;
            this.ControlGroup.SelectedIndexChanged += new System.EventHandler(this.ControlGroup_SelectedIndexChanged);
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
            this.argsLayoutPanel.Controls.Add(this.ControlGroup);
            this.argsLayoutPanel.Controls.Add(this.labelComparisonGroups);
            this.argsLayoutPanel.Controls.Add(this.ComparisonGroups);
            this.argsLayoutPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.argsLayoutPanel.Location = new System.Drawing.Point(12, 187);
            this.argsLayoutPanel.Name = "argsLayoutPanel";
            this.argsLayoutPanel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 10);
            this.argsLayoutPanel.Size = new System.Drawing.Size(180, 132);
            this.argsLayoutPanel.TabIndex = 2;
            // 
            // cboxSelectHighQualityFeatures
            // 
            this.cboxSelectHighQualityFeatures.AutoSize = true;
            this.cboxSelectHighQualityFeatures.Location = new System.Drawing.Point(15, 115);
            this.cboxSelectHighQualityFeatures.Name = "cboxSelectHighQualityFeatures";
            this.cboxSelectHighQualityFeatures.Size = new System.Drawing.Size(153, 17);
            this.cboxSelectHighQualityFeatures.TabIndex = 5;
            this.cboxSelectHighQualityFeatures.Text = "Select high quality features";
            this.cboxSelectHighQualityFeatures.UseVisualStyleBackColor = true;
            this.cboxSelectHighQualityFeatures.CheckedChanged += new System.EventHandler(this.cboxSelectHighQualityFeatures_CheckedChanged);
            // 
            // cboxRemoveInterferedProteins
            // 
            this.cboxRemoveInterferedProteins.AutoSize = true;
            this.cboxRemoveInterferedProteins.Enabled = false;
            this.cboxRemoveInterferedProteins.Location = new System.Drawing.Point(30, 138);
            this.cboxRemoveInterferedProteins.Name = "cboxRemoveInterferedProteins";
            this.cboxRemoveInterferedProteins.Size = new System.Drawing.Size(231, 30);
            this.cboxRemoveInterferedProteins.TabIndex = 6;
            this.cboxRemoveInterferedProteins.Text = "Allow the algorithm to delete the whole \r\nprotein if all of its features have int" +
    "erference";
            this.cboxRemoveInterferedProteins.UseVisualStyleBackColor = true;
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
            this.Controls.Add(this.cboxRemoveInterferedProteins);
            this.Controls.Add(this.cboxSelectHighQualityFeatures);
            this.Controls.Add(this.labelName);
            this.Controls.Add(this.cboxAllowMissingPeaks);
            this.Controls.Add(this.textBoxName);
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
            this.Load += new System.EventHandler(this.MSstatsUI_Load);
            this.argsLayoutPanel.ResumeLayout(false);
            this.argsLayoutPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.TextBox textBoxName;
        private System.Windows.Forms.ComboBox comboBoxNormalizeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.CheckBox cboxAllowMissingPeaks;
        private System.Windows.Forms.ListBox ComparisonGroups;
        private System.Windows.Forms.Label labelComparisonGroups;
        private System.Windows.Forms.ComboBox ControlGroup;
        private System.Windows.Forms.Label labelControlGroup;
        private System.Windows.Forms.FlowLayoutPanel argsLayoutPanel;
        private System.Windows.Forms.CheckBox cboxSelectHighQualityFeatures;
        private System.Windows.Forms.CheckBox cboxRemoveInterferedProteins;
    }
}
