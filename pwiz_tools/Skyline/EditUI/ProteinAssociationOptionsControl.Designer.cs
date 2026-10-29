namespace pwiz.Skyline.EditUI
{
    partial class ProteinAssociationOptionsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ProteinAssociationOptionsControl));
            this.cbGroupProteins = new System.Windows.Forms.CheckBox();
            this.comboSharedPeptides = new System.Windows.Forms.ComboBox();
            this.gbParsimonyOptions = new System.Windows.Forms.GroupBox();
            this.flowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.lblGroupProteins = new System.Windows.Forms.Label();
            this.lnkHelpProteinGroups = new System.Windows.Forms.LinkLabel();
            this.cbGeneLevel = new System.Windows.Forms.CheckBox();
            this.lblGroupAtGeneLevel = new System.Windows.Forms.Label();
            this.lblSharedPeptides = new System.Windows.Forms.Label();
            this.lnkHelpSharedPeptides = new System.Windows.Forms.LinkLabel();
            this.cbMinimalProteinList = new System.Windows.Forms.CheckBox();
            this.lblMinimalProteinList = new System.Windows.Forms.Label();
            this.lnkHelpFindMinimalProteinList = new System.Windows.Forms.LinkLabel();
            this.cbRemoveSubsetProteins = new System.Windows.Forms.CheckBox();
            this.lblRemoveSubsetProteins = new System.Windows.Forms.Label();
            this.lnkHelpRemoveSubsetProteins = new System.Windows.Forms.LinkLabel();
            this.lblMinPeptides = new System.Windows.Forms.Label();
            this.numMinPeptides = new System.Windows.Forms.NumericUpDown();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.gbParsimonyOptions.SuspendLayout();
            this.flowLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMinPeptides)).BeginInit();
            this.SuspendLayout();
            // 
            // cbGroupProteins
            // 
            resources.ApplyResources(this.cbGroupProteins, "cbGroupProteins");
            this.cbGroupProteins.Name = "cbGroupProteins";
            this.cbGroupProteins.UseVisualStyleBackColor = true;
            this.cbGroupProteins.CheckedChanged += new System.EventHandler(this.cbGroupProteins_CheckedChanged);
            // 
            // comboSharedPeptides
            // 
            this.comboSharedPeptides.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.flowLayoutPanel.SetFlowBreak(this.comboSharedPeptides, true);
            this.comboSharedPeptides.FormattingEnabled = true;
            resources.ApplyResources(this.comboSharedPeptides, "comboSharedPeptides");
            this.comboSharedPeptides.Name = "comboSharedPeptides";
            // 
            // gbParsimonyOptions
            // 
            resources.ApplyResources(this.gbParsimonyOptions, "gbParsimonyOptions");
            this.gbParsimonyOptions.Controls.Add(this.flowLayoutPanel);
            this.gbParsimonyOptions.Name = "gbParsimonyOptions";
            this.gbParsimonyOptions.TabStop = false;
            // 
            // flowLayoutPanel
            // 
            this.flowLayoutPanel.Controls.Add(this.cbGroupProteins);
            this.flowLayoutPanel.Controls.Add(this.lblGroupProteins);
            this.flowLayoutPanel.Controls.Add(this.lnkHelpProteinGroups);
            this.flowLayoutPanel.Controls.Add(this.cbGeneLevel);
            this.flowLayoutPanel.Controls.Add(this.lblGroupAtGeneLevel);
            this.flowLayoutPanel.Controls.Add(this.lblSharedPeptides);
            this.flowLayoutPanel.Controls.Add(this.lnkHelpSharedPeptides);
            this.flowLayoutPanel.Controls.Add(this.comboSharedPeptides);
            this.flowLayoutPanel.Controls.Add(this.cbMinimalProteinList);
            this.flowLayoutPanel.Controls.Add(this.lblMinimalProteinList);
            this.flowLayoutPanel.Controls.Add(this.lnkHelpFindMinimalProteinList);
            this.flowLayoutPanel.Controls.Add(this.cbRemoveSubsetProteins);
            this.flowLayoutPanel.Controls.Add(this.lblRemoveSubsetProteins);
            this.flowLayoutPanel.Controls.Add(this.lnkHelpRemoveSubsetProteins);
            this.flowLayoutPanel.Controls.Add(this.lblMinPeptides);
            this.flowLayoutPanel.Controls.Add(this.numMinPeptides);
            resources.ApplyResources(this.flowLayoutPanel, "flowLayoutPanel");
            this.flowLayoutPanel.Name = "flowLayoutPanel";
            // 
            // lblGroupProteins
            // 
            resources.ApplyResources(this.lblGroupProteins, "lblGroupProteins");
            this.lblGroupProteins.Name = "lblGroupProteins";
            this.helpTip.SetToolTip(this.lblGroupProteins, resources.GetString("lblGroupProteins.ToolTip"));
            this.lblGroupProteins.Click += new System.EventHandler(this.lblGroupProtein_Click);
            // 
            // lnkHelpProteinGroups
            // 
            resources.ApplyResources(this.lnkHelpProteinGroups, "lnkHelpProteinGroups");
            this.flowLayoutPanel.SetFlowBreak(this.lnkHelpProteinGroups, true);
            this.lnkHelpProteinGroups.Name = "lnkHelpProteinGroups";
            this.lnkHelpProteinGroups.TabStop = true;
            this.lnkHelpProteinGroups.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkHelp_LinkClicked);
            // 
            // cbGeneLevel
            // 
            resources.ApplyResources(this.cbGeneLevel, "cbGeneLevel");
            this.cbGeneLevel.Name = "cbGeneLevel";
            this.cbGeneLevel.UseVisualStyleBackColor = true;
            this.cbGeneLevel.CheckedChanged += new System.EventHandler(this.cbGeneLevel_CheckedChanged);
            // 
            // lblGroupAtGeneLevel
            // 
            resources.ApplyResources(this.lblGroupAtGeneLevel, "lblGroupAtGeneLevel");
            this.flowLayoutPanel.SetFlowBreak(this.lblGroupAtGeneLevel, true);
            this.lblGroupAtGeneLevel.Name = "lblGroupAtGeneLevel";
            this.helpTip.SetToolTip(this.lblGroupAtGeneLevel, resources.GetString("lblGroupAtGeneLevel.ToolTip"));
            this.lblGroupAtGeneLevel.Click += new System.EventHandler(this.lblGroupAtGeneLevel_Click);
            // 
            // lblSharedPeptides
            // 
            resources.ApplyResources(this.lblSharedPeptides, "lblSharedPeptides");
            this.lblSharedPeptides.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.lblSharedPeptides.Name = "lblSharedPeptides";
            this.helpTip.SetToolTip(this.lblSharedPeptides, resources.GetString("lblSharedPeptides.ToolTip"));
            // 
            // lnkHelpSharedPeptides
            // 
            resources.ApplyResources(this.lnkHelpSharedPeptides, "lnkHelpSharedPeptides");
            this.flowLayoutPanel.SetFlowBreak(this.lnkHelpSharedPeptides, true);
            this.lnkHelpSharedPeptides.Name = "lnkHelpSharedPeptides";
            this.lnkHelpSharedPeptides.TabStop = true;
            this.lnkHelpSharedPeptides.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkHelp_LinkClicked);
            // 
            // cbMinimalProteinList
            // 
            resources.ApplyResources(this.cbMinimalProteinList, "cbMinimalProteinList");
            this.cbMinimalProteinList.Name = "cbMinimalProteinList";
            this.cbMinimalProteinList.UseVisualStyleBackColor = true;
            this.cbMinimalProteinList.CheckedChanged += new System.EventHandler(this.checkBoxParsimony_CheckedChanged);
            // 
            // lblMinimalProteinList
            // 
            resources.ApplyResources(this.lblMinimalProteinList, "lblMinimalProteinList");
            this.lblMinimalProteinList.Name = "lblMinimalProteinList";
            this.helpTip.SetToolTip(this.lblMinimalProteinList, resources.GetString("lblMinimalProteinList.ToolTip"));
            this.lblMinimalProteinList.Click += new System.EventHandler(this.lblMinimalProteinList_Click);
            // 
            // lnkHelpFindMinimalProteinList
            // 
            resources.ApplyResources(this.lnkHelpFindMinimalProteinList, "lnkHelpFindMinimalProteinList");
            this.flowLayoutPanel.SetFlowBreak(this.lnkHelpFindMinimalProteinList, true);
            this.lnkHelpFindMinimalProteinList.Name = "lnkHelpFindMinimalProteinList";
            this.lnkHelpFindMinimalProteinList.TabStop = true;
            this.lnkHelpFindMinimalProteinList.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkHelp_LinkClicked);
            // 
            // cbRemoveSubsetProteins
            // 
            resources.ApplyResources(this.cbRemoveSubsetProteins, "cbRemoveSubsetProteins");
            this.cbRemoveSubsetProteins.Name = "cbRemoveSubsetProteins";
            this.cbRemoveSubsetProteins.UseVisualStyleBackColor = true;
            this.cbRemoveSubsetProteins.CheckedChanged += new System.EventHandler(this.checkBoxParsimony_CheckedChanged);
            // 
            // lblRemoveSubsetProteins
            // 
            resources.ApplyResources(this.lblRemoveSubsetProteins, "lblRemoveSubsetProteins");
            this.lblRemoveSubsetProteins.Name = "lblRemoveSubsetProteins";
            this.helpTip.SetToolTip(this.lblRemoveSubsetProteins, resources.GetString("lblRemoveSubsetProteins.ToolTip"));
            this.lblRemoveSubsetProteins.Click += new System.EventHandler(this.lblRemoveSubsetProtein_Click);
            // 
            // lnkHelpRemoveSubsetProteins
            // 
            resources.ApplyResources(this.lnkHelpRemoveSubsetProteins, "lnkHelpRemoveSubsetProteins");
            this.flowLayoutPanel.SetFlowBreak(this.lnkHelpRemoveSubsetProteins, true);
            this.lnkHelpRemoveSubsetProteins.Name = "lnkHelpRemoveSubsetProteins";
            this.lnkHelpRemoveSubsetProteins.TabStop = true;
            this.lnkHelpRemoveSubsetProteins.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkHelp_LinkClicked);
            // 
            // lblMinPeptides
            // 
            resources.ApplyResources(this.lblMinPeptides, "lblMinPeptides");
            this.lblMinPeptides.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.flowLayoutPanel.SetFlowBreak(this.lblMinPeptides, true);
            this.lblMinPeptides.Name = "lblMinPeptides";
            this.helpTip.SetToolTip(this.lblMinPeptides, resources.GetString("lblMinPeptides.ToolTip"));
            // 
            // numMinPeptides
            // 
            this.flowLayoutPanel.SetFlowBreak(this.numMinPeptides, true);
            resources.ApplyResources(this.numMinPeptides, "numMinPeptides");
            this.numMinPeptides.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.numMinPeptides.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numMinPeptides.Name = "numMinPeptides";
            this.numMinPeptides.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numMinPeptides.ValueChanged += new System.EventHandler(this.numMinPeptides_ValueChanged);
            this.numMinPeptides.Enter += new System.EventHandler(this.numMinPeptides_Enter);
            this.numMinPeptides.Leave += new System.EventHandler(this.numMinPeptides_Leave);
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 32767;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // ProteinAssociationOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.gbParsimonyOptions);
            this.Name = "ProteinAssociationOptionsControl";
            this.gbParsimonyOptions.ResumeLayout(false);
            this.flowLayoutPanel.ResumeLayout(false);
            this.flowLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMinPeptides)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckBox cbGroupProteins;
        private System.Windows.Forms.ComboBox comboSharedPeptides;
        private System.Windows.Forms.GroupBox gbParsimonyOptions;
        private System.Windows.Forms.Label lblSharedPeptides;
        private System.Windows.Forms.Label lblMinPeptides;
        private System.Windows.Forms.NumericUpDown numMinPeptides;
        private System.Windows.Forms.CheckBox cbMinimalProteinList;
        private System.Windows.Forms.CheckBox cbRemoveSubsetProteins;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel;
        private System.Windows.Forms.LinkLabel lnkHelpProteinGroups;
        private System.Windows.Forms.LinkLabel lnkHelpFindMinimalProteinList;
        private System.Windows.Forms.LinkLabel lnkHelpRemoveSubsetProteins;
        private System.Windows.Forms.LinkLabel lnkHelpSharedPeptides;
        private System.Windows.Forms.Label lblGroupProteins;
        private System.Windows.Forms.Label lblMinimalProteinList;
        private System.Windows.Forms.Label lblRemoveSubsetProteins;
        private System.Windows.Forms.CheckBox cbGeneLevel;
        private System.Windows.Forms.Label lblGroupAtGeneLevel;
        private System.Windows.Forms.ToolTip helpTip;
    }
}
