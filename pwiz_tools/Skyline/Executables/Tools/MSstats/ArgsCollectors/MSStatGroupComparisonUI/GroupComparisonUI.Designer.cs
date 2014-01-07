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
            this.labelName = new System.Windows.Forms.Label();
            this.textBoxName = new System.Windows.Forms.TextBox();
            this.ComparisonGroups = new System.Windows.Forms.ListBox();
            this.cboxEqualVariance = new System.Windows.Forms.CheckBox();
            this.groupBoxBioRep = new System.Windows.Forms.GroupBox();
            this.groupBoxTechRep = new System.Windows.Forms.GroupBox();
            this.comboBoxNoramilzeTo = new System.Windows.Forms.ComboBox();
            this.labelNormalizeTo = new System.Windows.Forms.Label();
            this.argsLayoutPanel.SuspendLayout();
            this.groupBoxBioRep.SuspendLayout();
            this.groupBoxTechRep.SuspendLayout();
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
            // bioRepRes
            // 
            this.bioRepRes.AutoSize = true;
            this.bioRepRes.Checked = true;
            this.bioRepRes.Location = new System.Drawing.Point(85, 19);
            this.bioRepRes.Name = "bioRepRes";
            this.bioRepRes.Size = new System.Drawing.Size(73, 17);
            this.bioRepRes.TabIndex = 1;
            this.bioRepRes.TabStop = true;
            this.bioRepRes.Text = "Restricted";
            this.bioRepRes.UseVisualStyleBackColor = true;
            // 
            // bioRepExp
            // 
            this.bioRepExp.AutoSize = true;
            this.bioRepExp.Location = new System.Drawing.Point(6, 19);
            this.bioRepExp.Name = "bioRepExp";
            this.bioRepExp.Size = new System.Drawing.Size(73, 17);
            this.bioRepExp.TabIndex = 0;
            this.bioRepExp.Text = "Expanded";
            this.bioRepExp.UseVisualStyleBackColor = true;
            // 
            // techRepRes
            // 
            this.techRepRes.AutoSize = true;
            this.techRepRes.Location = new System.Drawing.Point(85, 19);
            this.techRepRes.Name = "techRepRes";
            this.techRepRes.Size = new System.Drawing.Size(73, 17);
            this.techRepRes.TabIndex = 1;
            this.techRepRes.Text = "Restricted";
            this.techRepRes.UseVisualStyleBackColor = true;
            // 
            // techRepExp
            // 
            this.techRepExp.Checked = true;
            this.techRepExp.Location = new System.Drawing.Point(6, 19);
            this.techRepExp.Name = "techRepExp";
            this.techRepExp.Size = new System.Drawing.Size(73, 17);
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
            this.cboxLabelData.Location = new System.Drawing.Point(3, 171);
            this.cboxLabelData.Margin = new System.Windows.Forms.Padding(3, 10, 3, 3);
            this.cboxLabelData.Name = "cboxLabelData";
            this.cboxLabelData.Size = new System.Drawing.Size(76, 17);
            this.cboxLabelData.TabIndex = 6;
            this.cboxLabelData.Text = "&Label data";
            this.cboxLabelData.UseVisualStyleBackColor = true;
            // 
            // cboxInterferenceTransitions
            // 
            this.cboxInterferenceTransitions.AutoSize = true;
            this.cboxInterferenceTransitions.Checked = true;
            this.cboxInterferenceTransitions.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxInterferenceTransitions.Location = new System.Drawing.Point(3, 217);
            this.cboxInterferenceTransitions.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
            this.cboxInterferenceTransitions.Name = "cboxInterferenceTransitions";
            this.cboxInterferenceTransitions.Size = new System.Drawing.Size(170, 17);
            this.cboxInterferenceTransitions.TabIndex = 7;
            this.cboxInterferenceTransitions.Text = "I&nclude interference transitions";
            this.cboxInterferenceTransitions.UseVisualStyleBackColor = true;
            // 
            // labelControlGroup
            // 
            this.labelControlGroup.AutoSize = true;
            this.labelControlGroup.Location = new System.Drawing.Point(3, 49);
            this.labelControlGroup.Margin = new System.Windows.Forms.Padding(3, 10, 3, 0);
            this.labelControlGroup.Name = "labelControlGroup";
            this.labelControlGroup.Size = new System.Drawing.Size(73, 13);
            this.labelControlGroup.TabIndex = 2;
            this.labelControlGroup.Text = "&Control group:";
            // 
            // ControlGroup
            // 
            this.ControlGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ControlGroup.FormattingEnabled = true;
            this.ControlGroup.Location = new System.Drawing.Point(3, 65);
            this.ControlGroup.Name = "ControlGroup";
            this.ControlGroup.Size = new System.Drawing.Size(169, 21);
            this.ControlGroup.TabIndex = 3;
            this.ControlGroup.SelectedIndexChanged += new System.EventHandler(this.ControlGroup_SelectedIndexChanged);
            // 
            // labelComparisonGroups
            // 
            this.labelComparisonGroups.AutoSize = true;
            this.labelComparisonGroups.Location = new System.Drawing.Point(3, 99);
            this.labelComparisonGroups.Margin = new System.Windows.Forms.Padding(3, 10, 3, 0);
            this.labelComparisonGroups.Name = "labelComparisonGroups";
            this.labelComparisonGroups.Size = new System.Drawing.Size(174, 13);
            this.labelComparisonGroups.TabIndex = 4;
            this.labelComparisonGroups.Text = "&Select group(s) to compare against:";
            this.labelComparisonGroups.Visible = false;
            // 
            // argsLayoutPanel
            // 
            this.argsLayoutPanel.AutoSize = true;
            this.argsLayoutPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.argsLayoutPanel.Controls.Add(this.labelName);
            this.argsLayoutPanel.Controls.Add(this.textBoxName);
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
            this.argsLayoutPanel.Location = new System.Drawing.Point(12, 64);
            this.argsLayoutPanel.Name = "argsLayoutPanel";
            this.argsLayoutPanel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 10);
            this.argsLayoutPanel.Size = new System.Drawing.Size(180, 359);
            this.argsLayoutPanel.TabIndex = 2;
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(3, 0);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(107, 13);
            this.labelName.TabIndex = 0;
            this.labelName.Text = "&Name of comparison:";
            // 
            // textBoxName
            // 
            this.textBoxName.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.textBoxName.Location = new System.Drawing.Point(3, 16);
            this.textBoxName.Name = "textBoxName";
            this.textBoxName.Size = new System.Drawing.Size(169, 20);
            this.textBoxName.TabIndex = 1;
            // 
            // ComparisonGroups
            // 
            this.ComparisonGroups.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.ComparisonGroups.FormattingEnabled = true;
            this.ComparisonGroups.Location = new System.Drawing.Point(3, 115);
            this.ComparisonGroups.Name = "ComparisonGroups";
            this.ComparisonGroups.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this.ComparisonGroups.Size = new System.Drawing.Size(169, 43);
            this.ComparisonGroups.TabIndex = 5;
            this.ComparisonGroups.Visible = false;
            // 
            // cboxEqualVariance
            // 
            this.cboxEqualVariance.AutoSize = true;
            this.cboxEqualVariance.Checked = true;
            this.cboxEqualVariance.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxEqualVariance.Location = new System.Drawing.Point(3, 194);
            this.cboxEqualVariance.Name = "cboxEqualVariance";
            this.cboxEqualVariance.Size = new System.Drawing.Size(136, 17);
            this.cboxEqualVariance.TabIndex = 10;
            this.cboxEqualVariance.Text = "&Assume equal variance";
            this.cboxEqualVariance.UseVisualStyleBackColor = true;
            // 
            // groupBoxBioRep
            // 
            this.groupBoxBioRep.Controls.Add(this.bioRepRes);
            this.groupBoxBioRep.Controls.Add(this.bioRepExp);
            this.groupBoxBioRep.Location = new System.Drawing.Point(3, 245);
            this.groupBoxBioRep.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
            this.groupBoxBioRep.Name = "groupBoxBioRep";
            this.groupBoxBioRep.Size = new System.Drawing.Size(169, 45);
            this.groupBoxBioRep.TabIndex = 8;
            this.groupBoxBioRep.TabStop = false;
            this.groupBoxBioRep.Text = "Scope of &biological replicate";
            // 
            // groupBoxTechRep
            // 
            this.groupBoxTechRep.Controls.Add(this.techRepRes);
            this.groupBoxTechRep.Controls.Add(this.techRepExp);
            this.groupBoxTechRep.Location = new System.Drawing.Point(3, 301);
            this.groupBoxTechRep.Name = "groupBoxTechRep";
            this.groupBoxTechRep.Size = new System.Drawing.Size(169, 45);
            this.groupBoxTechRep.TabIndex = 9;
            this.groupBoxTechRep.TabStop = false;
            this.groupBoxTechRep.Text = "Scope of &technical replicate";
            // 
            // comboBoxNoramilzeTo
            // 
            this.comboBoxNoramilzeTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNoramilzeTo.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxNoramilzeTo.FormattingEnabled = true;
            this.comboBoxNoramilzeTo.Items.AddRange(new object[] {
            "None",
            "Constant",
            "Quantile",
            "Global Standards"});
            this.comboBoxNoramilzeTo.Location = new System.Drawing.Point(12, 32);
            this.comboBoxNoramilzeTo.Name = "comboBoxNoramilzeTo";
            this.comboBoxNoramilzeTo.Size = new System.Drawing.Size(172, 21);
            this.comboBoxNoramilzeTo.TabIndex = 1;
            // 
            // labelNormalizeTo
            // 
            this.labelNormalizeTo.AutoSize = true;
            this.labelNormalizeTo.Location = new System.Drawing.Point(12, 14);
            this.labelNormalizeTo.Name = "labelNormalizeTo";
            this.labelNormalizeTo.Size = new System.Drawing.Size(68, 13);
            this.labelNormalizeTo.TabIndex = 0;
            this.labelNormalizeTo.Text = "Normalize to:";
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
            this.Controls.Add(this.comboBoxNoramilzeTo);
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
    }
}
