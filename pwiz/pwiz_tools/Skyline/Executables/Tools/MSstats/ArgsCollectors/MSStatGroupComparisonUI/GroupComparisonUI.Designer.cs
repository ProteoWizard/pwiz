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
            this.bioRepRes = new System.Windows.Forms.RadioButton();
            this.bioRepExp = new System.Windows.Forms.RadioButton();
            this.techRepRes = new System.Windows.Forms.RadioButton();
            this.techRepExp = new System.Windows.Forms.RadioButton();
            this.cboxLabelData = new System.Windows.Forms.CheckBox();
            this.cboxInterferenceTransitions = new System.Windows.Forms.CheckBox();
            this.labelControlGroup = new System.Windows.Forms.Label();
            this.ControlGroup = new System.Windows.Forms.ComboBox();
            this.labelComparisonGroups = new System.Windows.Forms.Label();
            this.argsLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.ComparisonGroups = new System.Windows.Forms.ListBox();
            this.cboxEqualVariance = new System.Windows.Forms.CheckBox();
            this.groupBoxBioRep = new System.Windows.Forms.GroupBox();
            this.groupBoxTechRep = new System.Windows.Forms.GroupBox();
            this.labelName = new System.Windows.Forms.Label();
            this.textBoxName = new System.Windows.Forms.TextBox();
            this.comboBoxNoramilzeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.cboxAllowMissingPeaks = new System.Windows.Forms.CheckBox();
            this.argsLayoutPanel.SuspendLayout();
            this.groupBoxBioRep.SuspendLayout();
            this.groupBoxTechRep.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(273, 11);
            this.btnOK.Margin = new System.Windows.Forms.Padding(4, 4, 20, 4);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 28);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(273, 47);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 4, 20, 4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 28);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // bioRepRes
            // 
            this.bioRepRes.AutoSize = true;
            this.bioRepRes.Checked = true;
            this.bioRepRes.Location = new System.Drawing.Point(113, 23);
            this.bioRepRes.Margin = new System.Windows.Forms.Padding(4);
            this.bioRepRes.Name = "bioRepRes";
            this.bioRepRes.Size = new System.Drawing.Size(93, 21);
            this.bioRepRes.TabIndex = 1;
            this.bioRepRes.TabStop = true;
            this.bioRepRes.Text = "Restricted";
            this.bioRepRes.UseVisualStyleBackColor = true;
            // 
            // bioRepExp
            // 
            this.bioRepExp.AutoSize = true;
            this.bioRepExp.Location = new System.Drawing.Point(8, 23);
            this.bioRepExp.Margin = new System.Windows.Forms.Padding(4);
            this.bioRepExp.Name = "bioRepExp";
            this.bioRepExp.Size = new System.Drawing.Size(92, 21);
            this.bioRepExp.TabIndex = 0;
            this.bioRepExp.Text = "Expanded";
            this.bioRepExp.UseVisualStyleBackColor = true;
            // 
            // techRepRes
            // 
            this.techRepRes.AutoSize = true;
            this.techRepRes.Location = new System.Drawing.Point(113, 23);
            this.techRepRes.Margin = new System.Windows.Forms.Padding(4);
            this.techRepRes.Name = "techRepRes";
            this.techRepRes.Size = new System.Drawing.Size(93, 21);
            this.techRepRes.TabIndex = 1;
            this.techRepRes.Text = "Restricted";
            this.techRepRes.UseVisualStyleBackColor = true;
            // 
            // techRepExp
            // 
            this.techRepExp.Checked = true;
            this.techRepExp.Location = new System.Drawing.Point(8, 23);
            this.techRepExp.Margin = new System.Windows.Forms.Padding(4);
            this.techRepExp.Name = "techRepExp";
            this.techRepExp.Size = new System.Drawing.Size(97, 21);
            this.techRepExp.TabIndex = 0;
            this.techRepExp.TabStop = true;
            this.techRepExp.Text = "Expanded";
            this.techRepExp.UseVisualStyleBackColor = true;
            // 
            // cboxLabelData
            // 
            this.cboxLabelData.AutoSize = true;
            this.cboxLabelData.Checked = true;
            this.cboxLabelData.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxLabelData.Location = new System.Drawing.Point(4, 162);
            this.cboxLabelData.Margin = new System.Windows.Forms.Padding(4, 12, 4, 4);
            this.cboxLabelData.Name = "cboxLabelData";
            this.cboxLabelData.Size = new System.Drawing.Size(207, 21);
            this.cboxLabelData.TabIndex = 6;
            this.cboxLabelData.Text = "&Include reference standards";
            this.cboxLabelData.UseVisualStyleBackColor = true;
            // 
            // cboxInterferenceTransitions
            // 
            this.cboxInterferenceTransitions.AutoSize = true;
            this.cboxInterferenceTransitions.Checked = true;
            this.cboxInterferenceTransitions.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxInterferenceTransitions.Location = new System.Drawing.Point(4, 220);
            this.cboxInterferenceTransitions.Margin = new System.Windows.Forms.Padding(4, 4, 4, 10);
            this.cboxInterferenceTransitions.Name = "cboxInterferenceTransitions";
            this.cboxInterferenceTransitions.Size = new System.Drawing.Size(224, 21);
            this.cboxInterferenceTransitions.TabIndex = 8;
            this.cboxInterferenceTransitions.Text = "I&nclude interference transitions";
            this.cboxInterferenceTransitions.UseVisualStyleBackColor = true;
            // 
            // labelControlGroup
            // 
            this.labelControlGroup.AutoSize = true;
            this.labelControlGroup.Location = new System.Drawing.Point(4, 12);
            this.labelControlGroup.Margin = new System.Windows.Forms.Padding(4, 12, 4, 0);
            this.labelControlGroup.Name = "labelControlGroup";
            this.labelControlGroup.Size = new System.Drawing.Size(98, 17);
            this.labelControlGroup.TabIndex = 2;
            this.labelControlGroup.Text = "&Control group:";
            // 
            // ControlGroup
            // 
            this.ControlGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ControlGroup.FormattingEnabled = true;
            this.ControlGroup.Location = new System.Drawing.Point(4, 33);
            this.ControlGroup.Margin = new System.Windows.Forms.Padding(4);
            this.ControlGroup.Name = "ControlGroup";
            this.ControlGroup.Size = new System.Drawing.Size(224, 24);
            this.ControlGroup.TabIndex = 3;
            this.ControlGroup.SelectedIndexChanged += new System.EventHandler(this.ControlGroup_SelectedIndexChanged);
            // 
            // labelComparisonGroups
            // 
            this.labelComparisonGroups.AutoSize = true;
            this.labelComparisonGroups.Location = new System.Drawing.Point(4, 73);
            this.labelComparisonGroups.Margin = new System.Windows.Forms.Padding(4, 12, 4, 0);
            this.labelComparisonGroups.Name = "labelComparisonGroups";
            this.labelComparisonGroups.Size = new System.Drawing.Size(234, 17);
            this.labelComparisonGroups.TabIndex = 4;
            this.labelComparisonGroups.Text = "&Select group(s) to compare against:";
            this.labelComparisonGroups.Visible = false;
            // 
            // argsLayoutPanel
            // 
            this.argsLayoutPanel.AutoSize = true;
            this.argsLayoutPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.argsLayoutPanel.Controls.Add(this.labelControlGroup);
            this.argsLayoutPanel.Controls.Add(this.ControlGroup);
            this.argsLayoutPanel.Controls.Add(this.labelComparisonGroups);
            this.argsLayoutPanel.Controls.Add(this.ComparisonGroups);
            this.argsLayoutPanel.Controls.Add(this.cboxLabelData);
            this.argsLayoutPanel.Controls.Add(this.cboxEqualVariance);
            this.argsLayoutPanel.Controls.Add(this.cboxInterferenceTransitions);
            this.argsLayoutPanel.Controls.Add(this.groupBoxBioRep);
            this.argsLayoutPanel.Controls.Add(this.groupBoxTechRep);
            this.argsLayoutPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.argsLayoutPanel.Location = new System.Drawing.Point(17, 150);
            this.argsLayoutPanel.Margin = new System.Windows.Forms.Padding(4);
            this.argsLayoutPanel.Name = "argsLayoutPanel";
            this.argsLayoutPanel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 12);
            this.argsLayoutPanel.Size = new System.Drawing.Size(242, 395);
            this.argsLayoutPanel.TabIndex = 2;
            // 
            // ComparisonGroups
            // 
            this.ComparisonGroups.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.ComparisonGroups.FormattingEnabled = true;
            this.ComparisonGroups.ItemHeight = 16;
            this.ComparisonGroups.Location = new System.Drawing.Point(4, 94);
            this.ComparisonGroups.Margin = new System.Windows.Forms.Padding(4);
            this.ComparisonGroups.Name = "ComparisonGroups";
            this.ComparisonGroups.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this.ComparisonGroups.Size = new System.Drawing.Size(224, 52);
            this.ComparisonGroups.TabIndex = 5;
            this.ComparisonGroups.Visible = false;
            // 
            // cboxEqualVariance
            // 
            this.cboxEqualVariance.AutoSize = true;
            this.cboxEqualVariance.Checked = true;
            this.cboxEqualVariance.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxEqualVariance.Location = new System.Drawing.Point(4, 191);
            this.cboxEqualVariance.Margin = new System.Windows.Forms.Padding(4);
            this.cboxEqualVariance.Name = "cboxEqualVariance";
            this.cboxEqualVariance.Size = new System.Drawing.Size(177, 21);
            this.cboxEqualVariance.TabIndex = 7;
            this.cboxEqualVariance.Text = "&Assume equal variance";
            this.cboxEqualVariance.UseVisualStyleBackColor = true;
            // 
            // groupBoxBioRep
            // 
            this.groupBoxBioRep.Controls.Add(this.bioRepRes);
            this.groupBoxBioRep.Controls.Add(this.bioRepExp);
            this.groupBoxBioRep.Location = new System.Drawing.Point(4, 255);
            this.groupBoxBioRep.Margin = new System.Windows.Forms.Padding(4, 4, 4, 10);
            this.groupBoxBioRep.Name = "groupBoxBioRep";
            this.groupBoxBioRep.Padding = new System.Windows.Forms.Padding(4);
            this.groupBoxBioRep.Size = new System.Drawing.Size(225, 55);
            this.groupBoxBioRep.TabIndex = 9;
            this.groupBoxBioRep.TabStop = false;
            this.groupBoxBioRep.Text = "Scope of &biological replicate";
            // 
            // groupBoxTechRep
            // 
            this.groupBoxTechRep.Controls.Add(this.techRepRes);
            this.groupBoxTechRep.Controls.Add(this.techRepExp);
            this.groupBoxTechRep.Location = new System.Drawing.Point(4, 324);
            this.groupBoxTechRep.Margin = new System.Windows.Forms.Padding(4);
            this.groupBoxTechRep.Name = "groupBoxTechRep";
            this.groupBoxTechRep.Padding = new System.Windows.Forms.Padding(4);
            this.groupBoxTechRep.Size = new System.Drawing.Size(225, 55);
            this.groupBoxTechRep.TabIndex = 10;
            this.groupBoxTechRep.TabStop = false;
            this.groupBoxTechRep.Text = "Scope of &technical replicate";
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(18, 7);
            this.labelName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(142, 17);
            this.labelName.TabIndex = 0;
            this.labelName.Text = "&Name of comparison:";
            // 
            // textBoxName
            // 
            this.textBoxName.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.textBoxName.Location = new System.Drawing.Point(18, 28);
            this.textBoxName.Margin = new System.Windows.Forms.Padding(4);
            this.textBoxName.Name = "textBoxName";
            this.textBoxName.Size = new System.Drawing.Size(224, 22);
            this.textBoxName.TabIndex = 1;
            // 
            // comboBoxNoramilzeTo
            // 
            this.comboBoxNoramilzeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNoramilzeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxNoramilzeTo.FormattingEnabled = true;
            this.comboBoxNoramilzeTo.Items.AddRange(new object[] {
            "None",
            "Equalize medians",
            "Quantile",
            "Relative to global standards"});
            this.comboBoxNoramilzeTo.Location = new System.Drawing.Point(17, 83);
            this.comboBoxNoramilzeTo.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxNoramilzeTo.Name = "comboBoxNoramilzeTo";
            this.comboBoxNoramilzeTo.Size = new System.Drawing.Size(228, 24);
            this.comboBoxNoramilzeTo.TabIndex = 1;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(17, 61);
            this.labelNormalizeTo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(149, 17);
            this.labelNormalizeTo.TabIndex = 0;
            this.labelNormalizeTo.Text = "Normalization method:";
            // 
            // cboxAllowMissingPeaks
            // 
            this.cboxAllowMissingPeaks.AutoSize = true;
            this.cboxAllowMissingPeaks.Location = new System.Drawing.Point(19, 113);
            this.cboxAllowMissingPeaks.Margin = new System.Windows.Forms.Padding(4);
            this.cboxAllowMissingPeaks.Name = "cboxAllowMissingPeaks";
            this.cboxAllowMissingPeaks.Size = new System.Drawing.Size(155, 21);
            this.cboxAllowMissingPeaks.TabIndex = 2;
            this.cboxAllowMissingPeaks.Text = "&Allow missing peaks";
            this.cboxAllowMissingPeaks.UseVisualStyleBackColor = true;
            // 
            // GroupComparisonUi
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(389, 565);
            this.Controls.Add(this.labelName);
            this.Controls.Add(this.cboxAllowMissingPeaks);
            this.Controls.Add(this.textBoxName);
            this.Controls.Add(this.comboBoxNoramilzeTo);
            this.Controls.Add(this.labelNormalizeTo);
            this.Controls.Add(this.argsLayoutPanel);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Cursor = System.Windows.Forms.Cursors.Default;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
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
            this.groupBoxBioRep.ResumeLayout(false);
            this.groupBoxBioRep.PerformLayout();
            this.groupBoxTechRep.ResumeLayout(false);
            this.groupBoxTechRep.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.RadioButton bioRepRes;
        private System.Windows.Forms.RadioButton bioRepExp;
        private System.Windows.Forms.RadioButton techRepRes;
        private System.Windows.Forms.RadioButton techRepExp;
        private System.Windows.Forms.CheckBox cboxLabelData;
        private System.Windows.Forms.CheckBox cboxInterferenceTransitions;
        private System.Windows.Forms.Label labelControlGroup;
        private System.Windows.Forms.ComboBox ControlGroup;
        private System.Windows.Forms.Label labelComparisonGroups;
        private System.Windows.Forms.FlowLayoutPanel argsLayoutPanel;
        private System.Windows.Forms.ListBox ComparisonGroups;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.TextBox textBoxName;
        private System.Windows.Forms.GroupBox groupBoxBioRep;
        private System.Windows.Forms.GroupBox groupBoxTechRep;
        private System.Windows.Forms.CheckBox cboxEqualVariance;
        private System.Windows.Forms.ComboBox comboBoxNoramilzeTo;
        private System.Windows.Forms.Label labelNormalizeTo;
        private System.Windows.Forms.CheckBox cboxAllowMissingPeaks;
    }
}
