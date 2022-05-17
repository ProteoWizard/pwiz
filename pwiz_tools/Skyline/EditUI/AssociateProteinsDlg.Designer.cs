namespace pwiz.Skyline.EditUI
{
    partial class AssociateProteinsDlg
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AssociateProteinsDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblDescription = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.cbGroupProteins = new System.Windows.Forms.CheckBox();
            this.comboSharedPeptides = new System.Windows.Forms.ComboBox();
            this.gbParsimonyOptions = new System.Windows.Forms.GroupBox();
            this.flowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.lblGroupProteins = new System.Windows.Forms.Label();
            this.lnkHelpProteinGroups = new System.Windows.Forms.LinkLabel();
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
            this.rbFASTA = new System.Windows.Forms.RadioButton();
            this.rbBackgroundProteome = new System.Windows.Forms.RadioButton();
            this.comboBackgroundProteome = new System.Windows.Forms.ComboBox();
            this.tbxFastaTargets = new System.Windows.Forms.TextBox();
            this.browseFastaTargetsBtn = new System.Windows.Forms.Button();
            this.dgvAssociateResults = new pwiz.Common.Controls.CommonDataGridView();
            this.headerColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.mappedColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.unmappedColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.targetsColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblResults = new System.Windows.Forms.Label();
            this.lblStatusBarResult = new System.Windows.Forms.Label();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelStatusBarResult = new System.Windows.Forms.FlowLayoutPanel();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.gbParsimonyOptions.SuspendLayout();
            this.flowLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMinPeptides)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAssociateResults)).BeginInit();
            this.panelStatusBarResult.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // lblDescription
            // 
            resources.ApplyResources(this.lblDescription, "lblDescription");
            this.lblDescription.Name = "lblDescription";
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
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
            resources.ApplyResources(this.comboSharedPeptides, "comboSharedPeptides");
            this.comboSharedPeptides.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.flowLayoutPanel.SetFlowBreak(this.comboSharedPeptides, true);
            this.comboSharedPeptides.FormattingEnabled = true;
            this.comboSharedPeptides.Name = "comboSharedPeptides";
            this.comboSharedPeptides.SelectedIndexChanged += new System.EventHandler(this.comboParsimony_SelectedIndexChanged);
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
            // rbFASTA
            // 
            resources.ApplyResources(this.rbFASTA, "rbFASTA");
            this.rbFASTA.Checked = true;
            this.rbFASTA.Name = "rbFASTA";
            this.rbFASTA.TabStop = true;
            this.rbFASTA.UseVisualStyleBackColor = true;
            this.rbFASTA.CheckedChanged += new System.EventHandler(this.rbCheckedChanged);
            // 
            // rbBackgroundProteome
            // 
            resources.ApplyResources(this.rbBackgroundProteome, "rbBackgroundProteome");
            this.rbBackgroundProteome.Name = "rbBackgroundProteome";
            this.rbBackgroundProteome.UseVisualStyleBackColor = true;
            this.rbBackgroundProteome.CheckedChanged += new System.EventHandler(this.rbCheckedChanged);
            // 
            // comboBackgroundProteome
            // 
            this.comboBackgroundProteome.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboBackgroundProteome, "comboBackgroundProteome");
            this.comboBackgroundProteome.FormattingEnabled = true;
            this.comboBackgroundProteome.Name = "comboBackgroundProteome";
            this.comboBackgroundProteome.SelectedIndexChanged += new System.EventHandler(this.comboBackgroundProteome_SelectedIndexChanged);
            // 
            // tbxFastaTargets
            // 
            resources.ApplyResources(this.tbxFastaTargets, "tbxFastaTargets");
            this.tbxFastaTargets.Name = "tbxFastaTargets";
            this.tbxFastaTargets.TextChanged += new System.EventHandler(this.tbxFastaTargets_TextChanged);
            // 
            // browseFastaTargetsBtn
            // 
            resources.ApplyResources(this.browseFastaTargetsBtn, "browseFastaTargetsBtn");
            this.browseFastaTargetsBtn.Name = "browseFastaTargetsBtn";
            this.browseFastaTargetsBtn.UseVisualStyleBackColor = true;
            this.browseFastaTargetsBtn.Click += new System.EventHandler(this.btnUseFasta_Click);
            // 
            // dgvAssociateResults
            // 
            this.dgvAssociateResults.AllowUserToAddRows = false;
            this.dgvAssociateResults.AllowUserToDeleteRows = false;
            this.dgvAssociateResults.AllowUserToResizeColumns = false;
            this.dgvAssociateResults.AllowUserToResizeRows = false;
            resources.ApplyResources(this.dgvAssociateResults, "dgvAssociateResults");
            this.dgvAssociateResults.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvAssociateResults.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dgvAssociateResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAssociateResults.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.headerColumn,
            this.mappedColumn,
            this.unmappedColumn,
            this.targetsColumn});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvAssociateResults.DefaultCellStyle = dataGridViewCellStyle2;
            this.dgvAssociateResults.Name = "dgvAssociateResults";
            this.dgvAssociateResults.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvAssociateResults.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dgvAssociateResults.RowHeadersVisible = false;
            this.dgvAssociateResults.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            this.dgvAssociateResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvAssociateResults.ShowEditingIcon = false;
            this.dgvAssociateResults.VirtualMode = true;
            this.dgvAssociateResults.CellValueNeeded += new System.Windows.Forms.DataGridViewCellValueEventHandler(this.dgvAssociateResults_CellValueNeeded);
            // 
            // headerColumn
            // 
            resources.ApplyResources(this.headerColumn, "headerColumn");
            this.headerColumn.Name = "headerColumn";
            this.headerColumn.ReadOnly = true;
            // 
            // mappedColumn
            // 
            resources.ApplyResources(this.mappedColumn, "mappedColumn");
            this.mappedColumn.Name = "mappedColumn";
            this.mappedColumn.ReadOnly = true;
            // 
            // unmappedColumn
            // 
            resources.ApplyResources(this.unmappedColumn, "unmappedColumn");
            this.unmappedColumn.Name = "unmappedColumn";
            this.unmappedColumn.ReadOnly = true;
            // 
            // targetsColumn
            // 
            resources.ApplyResources(this.targetsColumn, "targetsColumn");
            this.targetsColumn.Name = "targetsColumn";
            this.targetsColumn.ReadOnly = true;
            // 
            // lblResults
            // 
            resources.ApplyResources(this.lblResults, "lblResults");
            this.lblResults.Name = "lblResults";
            // 
            // lblStatusBarResult
            // 
            resources.ApplyResources(this.lblStatusBarResult, "lblStatusBarResult");
            this.lblStatusBarResult.Name = "lblStatusBarResult";
            // 
            // dataGridViewTextBoxColumn1
            // 
            resources.ApplyResources(this.dataGridViewTextBoxColumn1, "dataGridViewTextBoxColumn1");
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            // 
            // dataGridViewTextBoxColumn2
            // 
            resources.ApplyResources(this.dataGridViewTextBoxColumn2, "dataGridViewTextBoxColumn2");
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            // 
            // dataGridViewTextBoxColumn3
            // 
            resources.ApplyResources(this.dataGridViewTextBoxColumn3, "dataGridViewTextBoxColumn3");
            this.dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
            // 
            // dataGridViewTextBoxColumn4
            // 
            resources.ApplyResources(this.dataGridViewTextBoxColumn4, "dataGridViewTextBoxColumn4");
            this.dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
            // 
            // panelStatusBarResult
            // 
            resources.ApplyResources(this.panelStatusBarResult, "panelStatusBarResult");
            this.panelStatusBarResult.Controls.Add(this.lblStatusBarResult);
            this.panelStatusBarResult.Name = "panelStatusBarResult";
            // 
            // AssociateProteinsDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.panelStatusBarResult);
            this.Controls.Add(this.lblResults);
            this.Controls.Add(this.dgvAssociateResults);
            this.Controls.Add(this.tbxFastaTargets);
            this.Controls.Add(this.browseFastaTargetsBtn);
            this.Controls.Add(this.comboBackgroundProteome);
            this.Controls.Add(this.rbBackgroundProteome);
            this.Controls.Add(this.rbFASTA);
            this.Controls.Add(this.gbParsimonyOptions);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.btnCancel);
            this.HelpButton = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AssociateProteinsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.HelpButtonClicked += new System.ComponentModel.CancelEventHandler(this.AssociateProteinsDlg_HelpButtonClicked);
            this.gbParsimonyOptions.ResumeLayout(false);
            this.flowLayoutPanel.ResumeLayout(false);
            this.flowLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMinPeptides)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAssociateResults)).EndInit();
            this.panelStatusBarResult.ResumeLayout(false);
            this.panelStatusBarResult.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.CheckBox cbGroupProteins;
        private System.Windows.Forms.ComboBox comboSharedPeptides;
        private System.Windows.Forms.GroupBox gbParsimonyOptions;
        private System.Windows.Forms.Label lblSharedPeptides;
        private System.Windows.Forms.RadioButton rbFASTA;
        private System.Windows.Forms.RadioButton rbBackgroundProteome;
        private System.Windows.Forms.ComboBox comboBackgroundProteome;
        private System.Windows.Forms.TextBox tbxFastaTargets;
        private System.Windows.Forms.Button browseFastaTargetsBtn;
        private pwiz.Common.Controls.CommonDataGridView dgvAssociateResults;
        private System.Windows.Forms.Label lblResults;
        private System.Windows.Forms.DataGridViewTextBoxColumn headerColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn mappedColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn unmappedColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn targetsColumn;
        private System.Windows.Forms.Label lblMinPeptides;
        private System.Windows.Forms.NumericUpDown numMinPeptides;
        private System.Windows.Forms.CheckBox cbMinimalProteinList;
        private System.Windows.Forms.CheckBox cbRemoveSubsetProteins;
        private System.Windows.Forms.Label lblStatusBarResult;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;
        private System.Windows.Forms.FlowLayoutPanel panelStatusBarResult;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel;
        private System.Windows.Forms.LinkLabel lnkHelpProteinGroups;
        private System.Windows.Forms.LinkLabel lnkHelpFindMinimalProteinList;
        private System.Windows.Forms.LinkLabel lnkHelpRemoveSubsetProteins;
        private System.Windows.Forms.LinkLabel lnkHelpSharedPeptides;
        private System.Windows.Forms.Label lblGroupProteins;
        private System.Windows.Forms.Label lblMinimalProteinList;
        private System.Windows.Forms.Label lblRemoveSubsetProteins;
        private System.Windows.Forms.ToolTip helpTip;
    }
}