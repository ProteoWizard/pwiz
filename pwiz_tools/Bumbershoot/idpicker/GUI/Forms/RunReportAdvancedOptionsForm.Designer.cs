//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Mike Litton.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Matt Chambers
//

namespace IdPickerGui
{
    partial class RunReportAdvancedOptionsForm
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
            System.Windows.Forms.Button btnOk;
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode( "Score names and weights" );
            System.Windows.Forms.TreeNode treeNode2 = new System.Windows.Forms.TreeNode( "Modifications" );
            this.btnCancel = new System.Windows.Forms.Button();
            this.tvAdvOptionsNav = new System.Windows.Forms.TreeView();
            this.pnlOptions = new System.Windows.Forms.Panel();
            this.gbMods = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.dgvModOverrides = new System.Windows.Forms.DataGridView();
            this.AminoAcid = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.Mass = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Type = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.rbIndistinct = new System.Windows.Forms.RadioButton();
            this.rbDistinct = new System.Windows.Forms.RadioButton();
            this.label3 = new System.Windows.Forms.Label();
            this.btnSaveModOptions = new System.Windows.Forms.Button();
            this.btnClearMods = new System.Windows.Forms.Button();
            this.btnAddMod = new System.Windows.Forms.Button();
            this.gbScores = new System.Windows.Forms.GroupBox();
            this.dgvScoreInfo = new System.Windows.Forms.DataGridView();
            this.ScoreName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ScoreWeight = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ScoreOrder = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.btnSaveScoresAndWeightsOptions = new System.Windows.Forms.Button();
            this.cbNormalizeScores = new System.Windows.Forms.CheckBox();
            this.cbApplyScoreOptimization = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tbOptimizeScorePermutations = new System.Windows.Forms.TextBox();
            this.btnClearScores = new System.Windows.Forms.Button();
            this.btnAddScore = new System.Windows.Forms.Button();
            btnOk = new System.Windows.Forms.Button();
            this.pnlOptions.SuspendLayout();
            this.gbMods.SuspendLayout();
            ( (System.ComponentModel.ISupportInitialize) ( this.dgvModOverrides ) ).BeginInit();
            this.gbScores.SuspendLayout();
            ( (System.ComponentModel.ISupportInitialize) ( this.dgvScoreInfo ) ).BeginInit();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            btnOk.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            btnOk.Location = new System.Drawing.Point( 383, 455 );
            btnOk.Name = "btnOk";
            btnOk.Size = new System.Drawing.Size( 75, 23 );
            btnOk.TabIndex = 114;
            btnOk.Text = "OK";
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += new System.EventHandler( this.btnOk_Click );
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnCancel.Location = new System.Drawing.Point( 302, 455 );
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size( 75, 23 );
            this.btnCancel.TabIndex = 113;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // tvAdvOptionsNav
            // 
            this.tvAdvOptionsNav.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tvAdvOptionsNav.FullRowSelect = true;
            this.tvAdvOptionsNav.HideSelection = false;
            this.tvAdvOptionsNav.Location = new System.Drawing.Point( 12, 20 );
            this.tvAdvOptionsNav.Name = "tvAdvOptionsNav";
            treeNode1.Name = "Scores";
            treeNode1.Text = "Score names and weights";
            treeNode2.Name = "Mods";
            treeNode2.Text = "Modifications";
            this.tvAdvOptionsNav.Nodes.AddRange( new System.Windows.Forms.TreeNode[] {
            treeNode1,
            treeNode2} );
            this.tvAdvOptionsNav.ShowLines = false;
            this.tvAdvOptionsNav.ShowPlusMinus = false;
            this.tvAdvOptionsNav.ShowRootLines = false;
            this.tvAdvOptionsNav.Size = new System.Drawing.Size( 145, 416 );
            this.tvAdvOptionsNav.TabIndex = 131;
            this.tvAdvOptionsNav.AfterSelect += new System.Windows.Forms.TreeViewEventHandler( this.tvAdvOptionsNav_AfterSelect );
            // 
            // pnlOptions
            // 
            this.pnlOptions.Controls.Add( this.gbMods );
            this.pnlOptions.Controls.Add( this.gbScores );
            this.pnlOptions.Location = new System.Drawing.Point( 163, 20 );
            this.pnlOptions.Name = "pnlOptions";
            this.pnlOptions.Size = new System.Drawing.Size( 294, 416 );
            this.pnlOptions.TabIndex = 132;
            // 
            // gbMods
            // 
            this.gbMods.Controls.Add( this.label4 );
            this.gbMods.Controls.Add( this.dgvModOverrides );
            this.gbMods.Controls.Add( this.rbIndistinct );
            this.gbMods.Controls.Add( this.rbDistinct );
            this.gbMods.Controls.Add( this.label3 );
            this.gbMods.Controls.Add( this.btnSaveModOptions );
            this.gbMods.Controls.Add( this.btnClearMods );
            this.gbMods.Controls.Add( this.btnAddMod );
            this.gbMods.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbMods.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.gbMods.Location = new System.Drawing.Point( 0, 0 );
            this.gbMods.Name = "gbMods";
            this.gbMods.Size = new System.Drawing.Size( 294, 416 );
            this.gbMods.TabIndex = 115;
            this.gbMods.TabStop = false;
            this.gbMods.Text = "Configure distinct/indistinct modifications";
            this.gbMods.Visible = false;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.label4.Location = new System.Drawing.Point( 11, 73 );
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size( 142, 13 );
            this.label4.TabIndex = 114;
            this.label4.Text = "Distinct/Indistinct overrides:";
            // 
            // dgvModOverrides
            // 
            this.dgvModOverrides.AllowUserToAddRows = false;
            this.dgvModOverrides.AllowUserToOrderColumns = true;
            this.dgvModOverrides.AllowUserToResizeColumns = false;
            this.dgvModOverrides.AllowUserToResizeRows = false;
            this.dgvModOverrides.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.dgvModOverrides.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvModOverrides.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dgvModOverrides.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.dgvModOverrides.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvModOverrides.Columns.AddRange( new System.Windows.Forms.DataGridViewColumn[] {
            this.AminoAcid,
            this.Mass,
            this.Type} );
            this.dgvModOverrides.Location = new System.Drawing.Point( 15, 89 );
            this.dgvModOverrides.MultiSelect = false;
            this.dgvModOverrides.Name = "dgvModOverrides";
            this.dgvModOverrides.RowHeadersVisible = false;
            this.dgvModOverrides.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dgvModOverrides.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvModOverrides.Size = new System.Drawing.Size( 265, 282 );
            this.dgvModOverrides.TabIndex = 113;
            this.dgvModOverrides.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler( this.dgvModOverrides_CellEndEdit );
            // 
            // AminoAcid
            // 
            this.AminoAcid.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.AminoAcid.HeaderText = "Amino Acid";
            this.AminoAcid.Items.AddRange( new object[] {
            "A",
            "R",
            "N",
            "D",
            "C",
            "Q",
            "E",
            "G",
            "H",
            "I",
            "L",
            "K",
            "M",
            "F",
            "P",
            "S",
            "T",
            "W",
            "Y",
            "V"} );
            this.AminoAcid.Name = "AminoAcid";
            this.AminoAcid.Width = 65;
            // 
            // Mass
            // 
            this.Mass.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.Mass.HeaderText = "Mass";
            this.Mass.Name = "Mass";
            this.Mass.Width = 56;
            // 
            // Type
            // 
            this.Type.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Type.FillWeight = 1F;
            this.Type.HeaderText = "Type";
            this.Type.Items.AddRange( new object[] {
            "Distinct",
            "Indistinct"} );
            this.Type.Name = "Type";
            // 
            // rbIndistinct
            // 
            this.rbIndistinct.AutoSize = true;
            this.rbIndistinct.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.rbIndistinct.Location = new System.Drawing.Point( 178, 42 );
            this.rbIndistinct.Name = "rbIndistinct";
            this.rbIndistinct.Size = new System.Drawing.Size( 69, 17 );
            this.rbIndistinct.TabIndex = 111;
            this.rbIndistinct.TabStop = true;
            this.rbIndistinct.Text = "Indistinct";
            this.rbIndistinct.UseVisualStyleBackColor = true;
            // 
            // rbDistinct
            // 
            this.rbDistinct.AutoSize = true;
            this.rbDistinct.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.rbDistinct.Location = new System.Drawing.Point( 178, 19 );
            this.rbDistinct.Name = "rbDistinct";
            this.rbDistinct.Size = new System.Drawing.Size( 60, 17 );
            this.rbDistinct.TabIndex = 110;
            this.rbDistinct.TabStop = true;
            this.rbDistinct.Text = "Distinct";
            this.rbDistinct.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.label3.Location = new System.Drawing.Point( 11, 29 );
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size( 148, 13 );
            this.label3.TabIndex = 109;
            this.label3.Text = "By default, modifications are:";
            // 
            // btnSaveModOptions
            // 
            this.btnSaveModOptions.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnSaveModOptions.Location = new System.Drawing.Point( 179, 377 );
            this.btnSaveModOptions.Name = "btnSaveModOptions";
            this.btnSaveModOptions.Size = new System.Drawing.Size( 101, 23 );
            this.btnSaveModOptions.TabIndex = 108;
            this.btnSaveModOptions.Text = "Save as defaults";
            this.btnSaveModOptions.UseVisualStyleBackColor = true;
            this.btnSaveModOptions.Click += new System.EventHandler( this.btnSaveModOptions_Click );
            // 
            // btnClearMods
            // 
            this.btnClearMods.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnClearMods.Location = new System.Drawing.Point( 113, 377 );
            this.btnClearMods.Name = "btnClearMods";
            this.btnClearMods.Size = new System.Drawing.Size( 60, 23 );
            this.btnClearMods.TabIndex = 39;
            this.btnClearMods.Text = "Clear";
            this.btnClearMods.UseVisualStyleBackColor = true;
            this.btnClearMods.Click += new System.EventHandler( this.btnClearMods_Click );
            // 
            // btnAddMod
            // 
            this.btnAddMod.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnAddMod.Location = new System.Drawing.Point( 47, 377 );
            this.btnAddMod.Name = "btnAddMod";
            this.btnAddMod.Size = new System.Drawing.Size( 60, 23 );
            this.btnAddMod.TabIndex = 37;
            this.btnAddMod.Text = "Add";
            this.btnAddMod.UseVisualStyleBackColor = true;
            this.btnAddMod.Click += new System.EventHandler( this.btnAddMod_Click );
            // 
            // gbScores
            // 
            this.gbScores.Controls.Add( this.dgvScoreInfo );
            this.gbScores.Controls.Add( this.btnSaveScoresAndWeightsOptions );
            this.gbScores.Controls.Add( this.cbNormalizeScores );
            this.gbScores.Controls.Add( this.cbApplyScoreOptimization );
            this.gbScores.Controls.Add( this.label2 );
            this.gbScores.Controls.Add( this.label1 );
            this.gbScores.Controls.Add( this.tbOptimizeScorePermutations );
            this.gbScores.Controls.Add( this.btnClearScores );
            this.gbScores.Controls.Add( this.btnAddScore );
            this.gbScores.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbScores.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.gbScores.Location = new System.Drawing.Point( 0, 0 );
            this.gbScores.Name = "gbScores";
            this.gbScores.Size = new System.Drawing.Size( 294, 416 );
            this.gbScores.TabIndex = 114;
            this.gbScores.TabStop = false;
            this.gbScores.Text = "Configure search scores and weights";
            // 
            // dgvScoreInfo
            // 
            this.dgvScoreInfo.AllowUserToAddRows = false;
            this.dgvScoreInfo.AllowUserToOrderColumns = true;
            this.dgvScoreInfo.AllowUserToResizeRows = false;
            this.dgvScoreInfo.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvScoreInfo.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dgvScoreInfo.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.dgvScoreInfo.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvScoreInfo.Columns.AddRange( new System.Windows.Forms.DataGridViewColumn[] {
            this.ScoreName,
            this.ScoreWeight,
            this.ScoreOrder} );
            this.dgvScoreInfo.Location = new System.Drawing.Point( 14, 122 );
            this.dgvScoreInfo.MultiSelect = false;
            this.dgvScoreInfo.Name = "dgvScoreInfo";
            this.dgvScoreInfo.RowHeadersVisible = false;
            this.dgvScoreInfo.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dgvScoreInfo.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvScoreInfo.Size = new System.Drawing.Size( 265, 249 );
            this.dgvScoreInfo.TabIndex = 108;
            this.dgvScoreInfo.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler( this.dgvScoreInfo_CellEndEdit );
            // 
            // ScoreName
            // 
            this.ScoreName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.ScoreName.HeaderText = "Name";
            this.ScoreName.Name = "ScoreName";
            this.ScoreName.ToolTipText = "The \"name\" of the score as it appears in the pepXML input";
            // 
            // ScoreWeight
            // 
            this.ScoreWeight.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.ScoreWeight.HeaderText = "Weight";
            this.ScoreWeight.Name = "ScoreWeight";
            this.ScoreWeight.ToolTipText = "A rational number applied to this score when calculating a total score. Zero mean" +
                "s that the score will have no impact";
            this.ScoreWeight.Width = 66;
            // 
            // ScoreOrder
            // 
            this.ScoreOrder.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.ScoreOrder.HeaderText = "Order";
            this.ScoreOrder.Items.AddRange( new object[] {
            "Ascending",
            "Descending"} );
            this.ScoreOrder.MaxDropDownItems = 2;
            this.ScoreOrder.Name = "ScoreOrder";
            this.ScoreOrder.ToolTipText = "\"Ascending\" means a higher score is better, \"descending\" means a lower score is b" +
                "etter";
            this.ScoreOrder.Width = 41;
            // 
            // btnSaveScoresAndWeightsOptions
            // 
            this.btnSaveScoresAndWeightsOptions.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnSaveScoresAndWeightsOptions.Location = new System.Drawing.Point( 178, 377 );
            this.btnSaveScoresAndWeightsOptions.Name = "btnSaveScoresAndWeightsOptions";
            this.btnSaveScoresAndWeightsOptions.Size = new System.Drawing.Size( 101, 23 );
            this.btnSaveScoresAndWeightsOptions.TabIndex = 107;
            this.btnSaveScoresAndWeightsOptions.Text = "Save as defaults";
            this.btnSaveScoresAndWeightsOptions.UseVisualStyleBackColor = true;
            this.btnSaveScoresAndWeightsOptions.Click += new System.EventHandler( this.btnSaveScoresAndWeightsOptions_Click );
            // 
            // cbNormalizeScores
            // 
            this.cbNormalizeScores.AutoSize = true;
            this.cbNormalizeScores.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cbNormalizeScores.Location = new System.Drawing.Point( 14, 76 );
            this.cbNormalizeScores.Name = "cbNormalizeScores";
            this.cbNormalizeScores.Size = new System.Drawing.Size( 161, 17 );
            this.cbNormalizeScores.TabIndex = 106;
            this.cbNormalizeScores.Text = "Combine scores as quantiles";
            this.cbNormalizeScores.UseVisualStyleBackColor = true;
            // 
            // cbApplyScoreOptimization
            // 
            this.cbApplyScoreOptimization.AutoSize = true;
            this.cbApplyScoreOptimization.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cbApplyScoreOptimization.Location = new System.Drawing.Point( 14, 53 );
            this.cbApplyScoreOptimization.Name = "cbApplyScoreOptimization";
            this.cbApplyScoreOptimization.Size = new System.Drawing.Size( 142, 17 );
            this.cbApplyScoreOptimization.TabIndex = 105;
            this.cbApplyScoreOptimization.Text = "Apply score optimization";
            this.cbApplyScoreOptimization.UseVisualStyleBackColor = true;
            this.cbApplyScoreOptimization.CheckedChanged += new System.EventHandler( this.cbApplyScoreOptimization_CheckedChanged );
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.label2.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label2.Location = new System.Drawing.Point( 11, 106 );
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size( 95, 13 );
            this.label2.TabIndex = 51;
            this.label2.Text = "Score information:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.label1.Location = new System.Drawing.Point( 11, 28 );
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size( 136, 13 );
            this.label1.TabIndex = 30;
            this.label1.Text = "Optimization permutations:";
            // 
            // tbOptimizeScorePermutations
            // 
            this.tbOptimizeScorePermutations.CausesValidation = false;
            this.tbOptimizeScorePermutations.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbOptimizeScorePermutations.Location = new System.Drawing.Point( 159, 25 );
            this.tbOptimizeScorePermutations.Name = "tbOptimizeScorePermutations";
            this.tbOptimizeScorePermutations.Size = new System.Drawing.Size( 41, 21 );
            this.tbOptimizeScorePermutations.TabIndex = 29;
            // 
            // btnClearScores
            // 
            this.btnClearScores.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnClearScores.Location = new System.Drawing.Point( 112, 377 );
            this.btnClearScores.Name = "btnClearScores";
            this.btnClearScores.Size = new System.Drawing.Size( 60, 23 );
            this.btnClearScores.TabIndex = 24;
            this.btnClearScores.Text = "Clear";
            this.btnClearScores.UseVisualStyleBackColor = true;
            this.btnClearScores.Click += new System.EventHandler( this.btnClearScores_Click );
            // 
            // btnAddScore
            // 
            this.btnAddScore.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnAddScore.Location = new System.Drawing.Point( 46, 377 );
            this.btnAddScore.Name = "btnAddScore";
            this.btnAddScore.Size = new System.Drawing.Size( 60, 23 );
            this.btnAddScore.TabIndex = 8;
            this.btnAddScore.Text = "Add";
            this.btnAddScore.UseVisualStyleBackColor = true;
            this.btnAddScore.Click += new System.EventHandler( this.btnAddScore_Click );
            // 
            // RunReportAdvancedOptionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 470, 490 );
            this.Controls.Add( this.pnlOptions );
            this.Controls.Add( this.tvAdvOptionsNav );
            this.Controls.Add( btnOk );
            this.Controls.Add( this.btnCancel );
            this.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RunReportAdvancedOptionsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Advanced Options";
            this.Load += new System.EventHandler( this.RunReportAdvancedOptionsForm_Load );
            this.pnlOptions.ResumeLayout( false );
            this.gbMods.ResumeLayout( false );
            this.gbMods.PerformLayout();
            ( (System.ComponentModel.ISupportInitialize) ( this.dgvModOverrides ) ).EndInit();
            this.gbScores.ResumeLayout( false );
            this.gbScores.PerformLayout();
            ( (System.ComponentModel.ISupportInitialize) ( this.dgvScoreInfo ) ).EndInit();
            this.ResumeLayout( false );

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TreeView tvAdvOptionsNav;
        private System.Windows.Forms.Panel pnlOptions;
        private System.Windows.Forms.GroupBox gbMods;
        private System.Windows.Forms.Button btnClearMods;
		private System.Windows.Forms.Button btnAddMod;
        private System.Windows.Forms.GroupBox gbScores;
        private System.Windows.Forms.Button btnSaveScoresAndWeightsOptions;
        private System.Windows.Forms.CheckBox cbNormalizeScores;
        private System.Windows.Forms.CheckBox cbApplyScoreOptimization;
		private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbOptimizeScorePermutations;
        private System.Windows.Forms.Button btnClearScores;
		private System.Windows.Forms.Button btnAddScore;
        private System.Windows.Forms.Button btnSaveModOptions;
		private System.Windows.Forms.RadioButton rbIndistinct;
		private System.Windows.Forms.RadioButton rbDistinct;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.DataGridView dgvModOverrides;
		private System.Windows.Forms.DataGridViewComboBoxColumn AminoAcid;
		private System.Windows.Forms.DataGridViewTextBoxColumn Mass;
		private System.Windows.Forms.DataGridViewComboBoxColumn Type;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.DataGridView dgvScoreInfo;
		private System.Windows.Forms.DataGridViewTextBoxColumn ScoreName;
		private System.Windows.Forms.DataGridViewTextBoxColumn ScoreWeight;
		private System.Windows.Forms.DataGridViewComboBoxColumn ScoreOrder;
    }
}