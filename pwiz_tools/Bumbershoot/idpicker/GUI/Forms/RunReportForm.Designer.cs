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
    partial class RunReportForm
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
            System.Windows.Forms.Label lblReportName;
            System.Windows.Forms.Button btnAdvancedStep1;
            System.Windows.Forms.Button btnCancelStep1;
            System.Windows.Forms.Label lblMinDistinctPeptides;
            System.Windows.Forms.SplitContainer splitcontGroups;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( RunReportForm ) );
            this.cmWhatsThis = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.whatsThisToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gbGroups = new System.Windows.Forms.GroupBox();
            this.pnlBgGroups = new System.Windows.Forms.Panel();
            this.pnlGroupsSpacer = new System.Windows.Forms.Panel();
            this.msGroups = new System.Windows.Forms.MenuStrip();
            this.miResetFiles = new System.Windows.Forms.ToolStripMenuItem();
            this.miExpandGroups = new System.Windows.Forms.ToolStripMenuItem();
            this.miCollapseGroups = new System.Windows.Forms.ToolStripMenuItem();
            this.tvGroups = new System.Windows.Forms.TreeView();
            this.gbFiles = new System.Windows.Forms.GroupBox();
            this.pnlUngroupedFiles = new System.Windows.Forms.Panel();
            this.pnlUngroupedSpacer = new System.Windows.Forms.Panel();
            this.tbStartHere = new System.Windows.Forms.TextBox();
            this.msUngroupedFiles = new System.Windows.Forms.MenuStrip();
            this.miDefaultGroups = new System.Windows.Forms.ToolStripMenuItem();
            this.lvNonGroupedFiles = new System.Windows.Forms.ListView();
            this.pnlBottomGroupsTreeView = new System.Windows.Forms.Panel();
            this.btnExpandGroups = new System.Windows.Forms.Button();
            this.btnCollapseGroups = new System.Windows.Forms.Button();
            this.btnAddGroup = new System.Windows.Forms.Button();
            this.btnRemoveGroups = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnCancel2 = new System.Windows.Forms.Button();
            this.btnFinish = new System.Windows.Forms.Button();
            this.tbMaxAmbigIds = new System.Windows.Forms.TextBox();
            this.pnlRunReportStep1 = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.panel5 = new System.Windows.Forms.Panel();
            this.btnNextStep1 = new System.Windows.Forms.Button();
            this.gbReportSetup = new System.Windows.Forms.GroupBox();
            this.tbDecoyPrefix = new System.Windows.Forms.TextBox();
            this.lblDecoyPrefix = new System.Windows.Forms.Label();
            this.lblDestDir = new System.Windows.Forms.Label();
            this.btnBrowseDestDir = new System.Windows.Forms.Button();
            this.tbResultsDir = new System.Windows.Forms.TextBox();
            this.tbReportName = new System.Windows.Forms.TextBox();
            this.gbInputFiles = new System.Windows.Forms.GroupBox();
            this.cbShowFileErrors = new System.Windows.Forms.CheckBox();
            this.cbInclSubDirs = new System.Windows.Forms.CheckBox();
            this.cboDbsInFiles = new System.Windows.Forms.ComboBox();
            this.lblDbInSelFiles = new System.Windows.Forms.Label();
            this.lblFilter = new System.Windows.Forms.Label();
            this.tbFilter = new System.Windows.Forms.TextBox();
            this.tvSelDirs = new System.Windows.Forms.TreeView();
            this.btnGetFileNames = new System.Windows.Forms.Button();
            this.tbSrcDir = new System.Windows.Forms.TextBox();
            this.lblSourceDir = new System.Windows.Forms.Label();
            this.btnBrowseSrcDir = new System.Windows.Forms.Button();
            this.pnlRunReportStep2 = new System.Windows.Forms.Panel();
            this.splitOptionsStep2 = new System.Windows.Forms.SplitContainer();
            this.gbPeptideDetails = new System.Windows.Forms.GroupBox();
            this.lblPercentSign = new System.Windows.Forms.Label();
            this.tbMinPepLength = new System.Windows.Forms.TextBox();
            this.lblMinPeptideLength = new System.Windows.Forms.Label();
            this.lblMaxAmbigIds = new System.Windows.Forms.Label();
            this.cboMaxFdr = new System.Windows.Forms.ComboBox();
            this.lblMaxFdr = new System.Windows.Forms.Label();
            this.gbProteinDetails = new System.Windows.Forms.GroupBox();
            this.tbMinSpectraPerProtein = new System.Windows.Forms.TextBox();
            this.lblMinSpectraPerProtein = new System.Windows.Forms.Label();
            this.lblParsimonyVariable = new System.Windows.Forms.Label();
            this.tbMinAdditionalPeptides = new System.Windows.Forms.TextBox();
            this.tbMinDistinctPeptides = new System.Windows.Forms.TextBox();
            this.btnBackStep2 = new System.Windows.Forms.Button();
            this.cmRightClickGroupNode = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.addGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeGroupToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.renameGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cmRightClickFileNode = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.removeFileNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoExportCheckBox = new System.Windows.Forms.CheckBox();
            lblReportName = new System.Windows.Forms.Label();
            btnAdvancedStep1 = new System.Windows.Forms.Button();
            btnCancelStep1 = new System.Windows.Forms.Button();
            lblMinDistinctPeptides = new System.Windows.Forms.Label();
            splitcontGroups = new System.Windows.Forms.SplitContainer();
            this.cmWhatsThis.SuspendLayout();
            splitcontGroups.Panel1.SuspendLayout();
            splitcontGroups.Panel2.SuspendLayout();
            splitcontGroups.SuspendLayout();
            this.gbGroups.SuspendLayout();
            this.pnlBgGroups.SuspendLayout();
            this.msGroups.SuspendLayout();
            this.gbFiles.SuspendLayout();
            this.pnlUngroupedFiles.SuspendLayout();
            this.msUngroupedFiles.SuspendLayout();
            this.pnlBottomGroupsTreeView.SuspendLayout();
            this.pnlRunReportStep1.SuspendLayout();
            this.gbReportSetup.SuspendLayout();
            this.gbInputFiles.SuspendLayout();
            this.pnlRunReportStep2.SuspendLayout();
            this.splitOptionsStep2.Panel1.SuspendLayout();
            this.splitOptionsStep2.Panel2.SuspendLayout();
            this.splitOptionsStep2.SuspendLayout();
            this.gbPeptideDetails.SuspendLayout();
            this.gbProteinDetails.SuspendLayout();
            this.cmRightClickGroupNode.SuspendLayout();
            this.cmRightClickFileNode.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblReportName
            // 
            lblReportName.AutoSize = true;
            lblReportName.BackColor = System.Drawing.Color.Transparent;
            lblReportName.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            lblReportName.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            lblReportName.Location = new System.Drawing.Point( 15, 22 );
            lblReportName.Name = "lblReportName";
            lblReportName.Size = new System.Drawing.Size( 74, 13 );
            lblReportName.TabIndex = 95;
            lblReportName.Text = "Report Name:";
            // 
            // btnAdvancedStep1
            // 
            btnAdvancedStep1.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            btnAdvancedStep1.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            btnAdvancedStep1.Location = new System.Drawing.Point( 534, 611 );
            btnAdvancedStep1.Name = "btnAdvancedStep1";
            btnAdvancedStep1.Size = new System.Drawing.Size( 75, 23 );
            btnAdvancedStep1.TabIndex = 123;
            btnAdvancedStep1.Text = "Advanced";
            btnAdvancedStep1.UseVisualStyleBackColor = true;
            btnAdvancedStep1.Click += new System.EventHandler( this.btnAdvancedStep1_Click );
            // 
            // btnCancelStep1
            // 
            btnCancelStep1.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            btnCancelStep1.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancelStep1.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            btnCancelStep1.Location = new System.Drawing.Point( 453, 611 );
            btnCancelStep1.Name = "btnCancelStep1";
            btnCancelStep1.Size = new System.Drawing.Size( 75, 23 );
            btnCancelStep1.TabIndex = 108;
            btnCancelStep1.Text = "Cancel";
            btnCancelStep1.UseVisualStyleBackColor = true;
            // 
            // lblMinDistinctPeptides
            // 
            lblMinDistinctPeptides.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblMinDistinctPeptides.AutoSize = true;
            lblMinDistinctPeptides.ContextMenuStrip = this.cmWhatsThis;
            lblMinDistinctPeptides.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            lblMinDistinctPeptides.Location = new System.Drawing.Point( 15, 24 );
            lblMinDistinctPeptides.Name = "lblMinDistinctPeptides";
            lblMinDistinctPeptides.Size = new System.Drawing.Size( 188, 13 );
            lblMinDistinctPeptides.TabIndex = 127;
            lblMinDistinctPeptides.Text = "Minimum distinct peptides per protein:";
            // 
            // cmWhatsThis
            // 
            this.cmWhatsThis.DropShadowEnabled = false;
            this.cmWhatsThis.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cmWhatsThis.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.whatsThisToolStripMenuItem} );
            this.cmWhatsThis.Name = "cmWhatsThis";
            this.cmWhatsThis.Size = new System.Drawing.Size( 144, 26 );
            this.cmWhatsThis.Click += new System.EventHandler( this.cmWhatsThis_Click );
            // 
            // whatsThisToolStripMenuItem
            // 
            this.whatsThisToolStripMenuItem.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.whatsThisToolStripMenuItem.Name = "whatsThisToolStripMenuItem";
            this.whatsThisToolStripMenuItem.Size = new System.Drawing.Size( 143, 22 );
            this.whatsThisToolStripMenuItem.Text = "What\'s this?";
            // 
            // splitcontGroups
            // 
            splitcontGroups.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            splitcontGroups.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            splitcontGroups.Location = new System.Drawing.Point( 12, 12 );
            splitcontGroups.Name = "splitcontGroups";
            // 
            // splitcontGroups.Panel1
            // 
            splitcontGroups.Panel1.Controls.Add( this.gbGroups );
            splitcontGroups.Panel1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            // 
            // splitcontGroups.Panel2
            // 
            splitcontGroups.Panel2.Controls.Add( this.gbFiles );
            splitcontGroups.Panel2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            splitcontGroups.Size = new System.Drawing.Size( 683, 465 );
            splitcontGroups.SplitterDistance = 340;
            splitcontGroups.TabIndex = 126;
            // 
            // gbGroups
            // 
            this.gbGroups.BackColor = System.Drawing.Color.Transparent;
            this.gbGroups.ContextMenuStrip = this.cmWhatsThis;
            this.gbGroups.Controls.Add( this.pnlBgGroups );
            this.gbGroups.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbGroups.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.gbGroups.Location = new System.Drawing.Point( 0, 0 );
            this.gbGroups.Name = "gbGroups";
            this.gbGroups.Size = new System.Drawing.Size( 340, 465 );
            this.gbGroups.TabIndex = 126;
            this.gbGroups.TabStop = false;
            this.gbGroups.Text = "Group Hierarchy";
            // 
            // pnlBgGroups
            // 
            this.pnlBgGroups.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.pnlBgGroups.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.pnlBgGroups.Controls.Add( this.pnlGroupsSpacer );
            this.pnlBgGroups.Controls.Add( this.msGroups );
            this.pnlBgGroups.Controls.Add( this.tvGroups );
            this.pnlBgGroups.Location = new System.Drawing.Point( 13, 22 );
            this.pnlBgGroups.Name = "pnlBgGroups";
            this.pnlBgGroups.Size = new System.Drawing.Size( 314, 431 );
            this.pnlBgGroups.TabIndex = 9;
            // 
            // pnlGroupsSpacer
            // 
            this.pnlGroupsSpacer.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.pnlGroupsSpacer.BackColor = System.Drawing.SystemColors.Window;
            this.pnlGroupsSpacer.Location = new System.Drawing.Point( 1, 26 );
            this.pnlGroupsSpacer.Name = "pnlGroupsSpacer";
            this.pnlGroupsSpacer.Size = new System.Drawing.Size( 312, 5 );
            this.pnlGroupsSpacer.TabIndex = 2;
            // 
            // msGroups
            // 
            this.msGroups.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.msGroups.AutoSize = false;
            this.msGroups.Dock = System.Windows.Forms.DockStyle.None;
            this.msGroups.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.miResetFiles,
            this.miExpandGroups,
            this.miCollapseGroups} );
            this.msGroups.Location = new System.Drawing.Point( 1, 1 );
            this.msGroups.Name = "msGroups";
            this.msGroups.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.msGroups.ShowItemToolTips = true;
            this.msGroups.Size = new System.Drawing.Size( 312, 24 );
            this.msGroups.TabIndex = 1;
            this.msGroups.Text = "menuStrip1";
            // 
            // miResetFiles
            // 
            this.miResetFiles.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.miResetFiles.Image = global::IdPickerGui.Properties.Resources.FillRightHS;
            this.miResetFiles.Name = "miResetFiles";
            this.miResetFiles.Size = new System.Drawing.Size( 28, 20 );
            this.miResetFiles.Text = "Reset files";
            this.miResetFiles.ToolTipText = "Remove all groups";
            this.miResetFiles.Click += new System.EventHandler( this.miResetFiles_Click );
            // 
            // miExpandGroups
            // 
            this.miExpandGroups.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.miExpandGroups.Image = global::IdPickerGui.Properties.Resources.Expand_large;
            this.miExpandGroups.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.miExpandGroups.Name = "miExpandGroups";
            this.miExpandGroups.Size = new System.Drawing.Size( 25, 20 );
            this.miExpandGroups.Text = "Expand groups";
            this.miExpandGroups.ToolTipText = "Expand groups";
            this.miExpandGroups.Click += new System.EventHandler( this.miExpandGroups_Click );
            // 
            // miCollapseGroups
            // 
            this.miCollapseGroups.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.miCollapseGroups.Image = global::IdPickerGui.Properties.Resources.Collapse_large;
            this.miCollapseGroups.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.miCollapseGroups.Name = "miCollapseGroups";
            this.miCollapseGroups.Size = new System.Drawing.Size( 25, 20 );
            this.miCollapseGroups.Text = "miCollapseGroups";
            this.miCollapseGroups.TextDirection = System.Windows.Forms.ToolStripTextDirection.Horizontal;
            this.miCollapseGroups.ToolTipText = "Collapse groups";
            this.miCollapseGroups.Click += new System.EventHandler( this.miCollapseGroups_Click );
            // 
            // tvGroups
            // 
            this.tvGroups.AllowDrop = true;
            this.tvGroups.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.tvGroups.BackColor = System.Drawing.SystemColors.Window;
            this.tvGroups.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tvGroups.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tvGroups.HideSelection = false;
            this.tvGroups.LabelEdit = true;
            this.tvGroups.Location = new System.Drawing.Point( 1, 31 );
            this.tvGroups.Name = "tvGroups";
            this.tvGroups.PathSeparator = "/";
            this.tvGroups.ShowNodeToolTips = true;
            this.tvGroups.Size = new System.Drawing.Size( 312, 399 );
            this.tvGroups.TabIndex = 0;
            this.tvGroups.AfterLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler( this.tvGroups_AfterLabelEdit );
            this.tvGroups.DragDrop += new System.Windows.Forms.DragEventHandler( this.tvGroups_DragDrop );
            this.tvGroups.AfterSelect += new System.Windows.Forms.TreeViewEventHandler( this.tvGroups_AfterSelect );
            this.tvGroups.MouseDown += new System.Windows.Forms.MouseEventHandler( this.tvGroups_MouseDown );
            this.tvGroups.BeforeLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler( this.tvGroups_BeforeLabelEdit );
            this.tvGroups.KeyDown += new System.Windows.Forms.KeyEventHandler( this.tvGroups_KeyDown );
            this.tvGroups.ItemDrag += new System.Windows.Forms.ItemDragEventHandler( this.tvGroups_ItemDrag );
            this.tvGroups.DragOver += new System.Windows.Forms.DragEventHandler( this.tvGroups_DragOver );
            // 
            // gbFiles
            // 
            this.gbFiles.ContextMenuStrip = this.cmWhatsThis;
            this.gbFiles.Controls.Add( this.pnlUngroupedFiles );
            this.gbFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbFiles.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.gbFiles.Location = new System.Drawing.Point( 0, 0 );
            this.gbFiles.Name = "gbFiles";
            this.gbFiles.Size = new System.Drawing.Size( 339, 465 );
            this.gbFiles.TabIndex = 127;
            this.gbFiles.TabStop = false;
            this.gbFiles.Text = "Non-Grouped Files";
            // 
            // pnlUngroupedFiles
            // 
            this.pnlUngroupedFiles.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.pnlUngroupedFiles.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.pnlUngroupedFiles.Controls.Add( this.pnlUngroupedSpacer );
            this.pnlUngroupedFiles.Controls.Add( this.tbStartHere );
            this.pnlUngroupedFiles.Controls.Add( this.msUngroupedFiles );
            this.pnlUngroupedFiles.Controls.Add( this.lvNonGroupedFiles );
            this.pnlUngroupedFiles.Location = new System.Drawing.Point( 12, 22 );
            this.pnlUngroupedFiles.Name = "pnlUngroupedFiles";
            this.pnlUngroupedFiles.Size = new System.Drawing.Size( 314, 431 );
            this.pnlUngroupedFiles.TabIndex = 10;
            // 
            // pnlUngroupedSpacer
            // 
            this.pnlUngroupedSpacer.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.pnlUngroupedSpacer.BackColor = System.Drawing.SystemColors.Window;
            this.pnlUngroupedSpacer.Location = new System.Drawing.Point( 1, 26 );
            this.pnlUngroupedSpacer.Name = "pnlUngroupedSpacer";
            this.pnlUngroupedSpacer.Size = new System.Drawing.Size( 312, 5 );
            this.pnlUngroupedSpacer.TabIndex = 2;
            this.pnlUngroupedSpacer.EnabledChanged += new System.EventHandler( this.pnlUngroupedSpacer_EnabledChanged );
            // 
            // tbStartHere
            // 
            this.tbStartHere.AllowDrop = true;
            this.tbStartHere.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.tbStartHere.BackColor = System.Drawing.SystemColors.Window;
            this.tbStartHere.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tbStartHere.Enabled = false;
            this.tbStartHere.Font = new System.Drawing.Font( "Tahoma", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbStartHere.ForeColor = System.Drawing.Color.Gray;
            this.tbStartHere.Location = new System.Drawing.Point( 16, 153 );
            this.tbStartHere.Multiline = true;
            this.tbStartHere.Name = "tbStartHere";
            this.tbStartHere.ReadOnly = true;
            this.tbStartHere.Size = new System.Drawing.Size( 287, 101 );
            this.tbStartHere.TabIndex = 3;
            this.tbStartHere.Text = resources.GetString( "tbStartHere.Text" );
            this.tbStartHere.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tbStartHere.Visible = false;
            this.tbStartHere.EnabledChanged += new System.EventHandler( this.tbStartHere_EnabledChanged_1 );
            this.tbStartHere.DragOver += new System.Windows.Forms.DragEventHandler( this.tbStartHere_DragOver );
            // 
            // msUngroupedFiles
            // 
            this.msUngroupedFiles.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.msUngroupedFiles.AutoSize = false;
            this.msUngroupedFiles.Dock = System.Windows.Forms.DockStyle.None;
            this.msUngroupedFiles.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.miDefaultGroups} );
            this.msUngroupedFiles.Location = new System.Drawing.Point( 1, 1 );
            this.msUngroupedFiles.Name = "msUngroupedFiles";
            this.msUngroupedFiles.ShowItemToolTips = true;
            this.msUngroupedFiles.Size = new System.Drawing.Size( 312, 24 );
            this.msUngroupedFiles.TabIndex = 1;
            this.msUngroupedFiles.Text = "menuStrip1";
            // 
            // miDefaultGroups
            // 
            this.miDefaultGroups.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.miDefaultGroups.Image = global::IdPickerGui.Properties.Resources.FillLeftHS;
            this.miDefaultGroups.Name = "miDefaultGroups";
            this.miDefaultGroups.Size = new System.Drawing.Size( 28, 20 );
            this.miDefaultGroups.Text = "Reset files";
            this.miDefaultGroups.ToolTipText = "Apply default groups";
            this.miDefaultGroups.Click += new System.EventHandler( this.miDefaultGroups_Click );
            // 
            // lvNonGroupedFiles
            // 
            this.lvNonGroupedFiles.AllowDrop = true;
            this.lvNonGroupedFiles.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.lvNonGroupedFiles.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lvNonGroupedFiles.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lvNonGroupedFiles.LabelWrap = false;
            this.lvNonGroupedFiles.Location = new System.Drawing.Point( 1, 31 );
            this.lvNonGroupedFiles.Name = "lvNonGroupedFiles";
            this.lvNonGroupedFiles.ShowGroups = false;
            this.lvNonGroupedFiles.Size = new System.Drawing.Size( 312, 399 );
            this.lvNonGroupedFiles.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.lvNonGroupedFiles.TabIndex = 3;
            this.lvNonGroupedFiles.UseCompatibleStateImageBehavior = false;
            this.lvNonGroupedFiles.View = System.Windows.Forms.View.List;
            this.lvNonGroupedFiles.DragDrop += new System.Windows.Forms.DragEventHandler( this.lvFiles_DragDrop );
            this.lvNonGroupedFiles.DragEnter += new System.Windows.Forms.DragEventHandler( this.lvFiles_DragEnter );
            this.lvNonGroupedFiles.KeyDown += new System.Windows.Forms.KeyEventHandler( this.lvFiles_KeyDown );
            this.lvNonGroupedFiles.ItemDrag += new System.Windows.Forms.ItemDragEventHandler( this.lvFiles_ItemDrag );
            this.lvNonGroupedFiles.DragOver += new System.Windows.Forms.DragEventHandler( this.lvFiles_DragOver );
            // 
            // pnlBottomGroupsTreeView
            // 
            this.pnlBottomGroupsTreeView.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.pnlBottomGroupsTreeView.BackColor = System.Drawing.SystemColors.Control;
            this.pnlBottomGroupsTreeView.Controls.Add( this.btnExpandGroups );
            this.pnlBottomGroupsTreeView.Controls.Add( this.btnCollapseGroups );
            this.pnlBottomGroupsTreeView.Controls.Add( this.btnAddGroup );
            this.pnlBottomGroupsTreeView.Controls.Add( this.btnRemoveGroups );
            this.pnlBottomGroupsTreeView.Controls.Add( this.btnReset );
            this.pnlBottomGroupsTreeView.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.pnlBottomGroupsTreeView.Location = new System.Drawing.Point( 12, 601 );
            this.pnlBottomGroupsTreeView.Name = "pnlBottomGroupsTreeView";
            this.pnlBottomGroupsTreeView.Size = new System.Drawing.Size( 314, 33 );
            this.pnlBottomGroupsTreeView.TabIndex = 7;
            this.pnlBottomGroupsTreeView.Visible = false;
            // 
            // btnExpandGroups
            // 
            this.btnExpandGroups.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnExpandGroups.Location = new System.Drawing.Point( 247, 3 );
            this.btnExpandGroups.Name = "btnExpandGroups";
            this.btnExpandGroups.Size = new System.Drawing.Size( 26, 23 );
            this.btnExpandGroups.TabIndex = 130;
            this.btnExpandGroups.Text = "+";
            this.btnExpandGroups.UseVisualStyleBackColor = true;
            this.btnExpandGroups.MouseHover += new System.EventHandler( this.btnExpandGroups_MouseHover );
            // 
            // btnCollapseGroups
            // 
            this.btnCollapseGroups.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnCollapseGroups.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnCollapseGroups.Location = new System.Drawing.Point( 279, 3 );
            this.btnCollapseGroups.Name = "btnCollapseGroups";
            this.btnCollapseGroups.Size = new System.Drawing.Size( 26, 23 );
            this.btnCollapseGroups.TabIndex = 4;
            this.btnCollapseGroups.Text = "-";
            this.btnCollapseGroups.UseVisualStyleBackColor = true;
            this.btnCollapseGroups.MouseHover += new System.EventHandler( this.btnCollapseGroups_MouseHover );
            // 
            // btnAddGroup
            // 
            this.btnAddGroup.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnAddGroup.Enabled = false;
            this.btnAddGroup.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnAddGroup.Location = new System.Drawing.Point( 168, 3 );
            this.btnAddGroup.Name = "btnAddGroup";
            this.btnAddGroup.Size = new System.Drawing.Size( 73, 23 );
            this.btnAddGroup.TabIndex = 3;
            this.btnAddGroup.Text = "Add Group";
            this.btnAddGroup.UseVisualStyleBackColor = true;
            this.btnAddGroup.Click += new System.EventHandler( this.btnAddGroup_Click );
            // 
            // btnRemoveGroups
            // 
            this.btnRemoveGroups.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnRemoveGroups.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnRemoveGroups.Location = new System.Drawing.Point( 89, 3 );
            this.btnRemoveGroups.Name = "btnRemoveGroups";
            this.btnRemoveGroups.Size = new System.Drawing.Size( 73, 23 );
            this.btnRemoveGroups.TabIndex = 2;
            this.btnRemoveGroups.Text = "Remove All";
            this.btnRemoveGroups.UseVisualStyleBackColor = true;
            this.btnRemoveGroups.Click += new System.EventHandler( this.btnRemoveGroups_Click );
            // 
            // btnReset
            // 
            this.btnReset.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnReset.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnReset.Location = new System.Drawing.Point( 10, 3 );
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size( 73, 23 );
            this.btnReset.TabIndex = 1;
            this.btnReset.Text = "Default";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler( this.btnReset_Click );
            // 
            // btnCancel2
            // 
            this.btnCancel2.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.btnCancel2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel2.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnCancel2.Location = new System.Drawing.Point( 542, 611 );
            this.btnCancel2.Name = "btnCancel2";
            this.btnCancel2.Size = new System.Drawing.Size( 75, 23 );
            this.btnCancel2.TabIndex = 11;
            this.btnCancel2.Text = "Cancel";
            this.btnCancel2.UseVisualStyleBackColor = true;
            // 
            // btnFinish
            // 
            this.btnFinish.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.btnFinish.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnFinish.Location = new System.Drawing.Point( 623, 611 );
            this.btnFinish.Name = "btnFinish";
            this.btnFinish.Size = new System.Drawing.Size( 75, 23 );
            this.btnFinish.TabIndex = 12;
            this.btnFinish.Text = "Run Report";
            this.btnFinish.UseVisualStyleBackColor = true;
            this.btnFinish.Click += new System.EventHandler( this.btnFinish_Click );
            // 
            // tbMaxAmbigIds
            // 
            this.tbMaxAmbigIds.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tbMaxAmbigIds.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbMaxAmbigIds.Location = new System.Drawing.Point( 143, 48 );
            this.tbMaxAmbigIds.Name = "tbMaxAmbigIds";
            this.tbMaxAmbigIds.Size = new System.Drawing.Size( 45, 21 );
            this.tbMaxAmbigIds.TabIndex = 5;
            // 
            // pnlRunReportStep1
            // 
            this.pnlRunReportStep1.BackColor = System.Drawing.Color.Transparent;
            this.pnlRunReportStep1.Controls.Add( this.lblStatus );
            this.pnlRunReportStep1.Controls.Add( btnAdvancedStep1 );
            this.pnlRunReportStep1.Controls.Add( this.panel5 );
            this.pnlRunReportStep1.Controls.Add( btnCancelStep1 );
            this.pnlRunReportStep1.Controls.Add( this.btnNextStep1 );
            this.pnlRunReportStep1.Controls.Add( this.gbReportSetup );
            this.pnlRunReportStep1.Controls.Add( this.gbInputFiles );
            this.pnlRunReportStep1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRunReportStep1.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.pnlRunReportStep1.Location = new System.Drawing.Point( 0, 0 );
            this.pnlRunReportStep1.Name = "pnlRunReportStep1";
            this.pnlRunReportStep1.Size = new System.Drawing.Size( 710, 646 );
            this.pnlRunReportStep1.TabIndex = 0;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblStatus.Location = new System.Drawing.Point( 3, 545 );
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size( 0, 13 );
            this.lblStatus.TabIndex = 124;
            // 
            // panel5
            // 
            this.panel5.BackColor = System.Drawing.Color.Transparent;
            this.panel5.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel5.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.panel5.Location = new System.Drawing.Point( 34, 29 );
            this.panel5.Name = "panel5";
            this.panel5.Size = new System.Drawing.Size( 541, 2 );
            this.panel5.TabIndex = 110;
            // 
            // btnNextStep1
            // 
            this.btnNextStep1.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.btnNextStep1.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnNextStep1.Location = new System.Drawing.Point( 615, 611 );
            this.btnNextStep1.Name = "btnNextStep1";
            this.btnNextStep1.Size = new System.Drawing.Size( 75, 23 );
            this.btnNextStep1.TabIndex = 107;
            this.btnNextStep1.Text = "Next";
            this.btnNextStep1.UseVisualStyleBackColor = true;
            this.btnNextStep1.Click += new System.EventHandler( this.btnNextStep1_Click );
            // 
            // gbReportSetup
            // 
            this.gbReportSetup.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.gbReportSetup.Controls.Add( this.tbDecoyPrefix );
            this.gbReportSetup.Controls.Add( this.lblDecoyPrefix );
            this.gbReportSetup.Controls.Add( this.lblDestDir );
            this.gbReportSetup.Controls.Add( this.btnBrowseDestDir );
            this.gbReportSetup.Controls.Add( this.tbResultsDir );
            this.gbReportSetup.Controls.Add( this.tbReportName );
            this.gbReportSetup.Controls.Add( lblReportName );
            this.gbReportSetup.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.gbReportSetup.Location = new System.Drawing.Point( 12, 12 );
            this.gbReportSetup.Name = "gbReportSetup";
            this.gbReportSetup.Size = new System.Drawing.Size( 678, 114 );
            this.gbReportSetup.TabIndex = 112;
            this.gbReportSetup.TabStop = false;
            this.gbReportSetup.Text = "Report Setup";
            // 
            // tbDecoyPrefix
            // 
            this.tbDecoyPrefix.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.tbDecoyPrefix.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbDecoyPrefix.Location = new System.Drawing.Point( 143, 77 );
            this.tbDecoyPrefix.Name = "tbDecoyPrefix";
            this.tbDecoyPrefix.Size = new System.Drawing.Size( 516, 21 );
            this.tbDecoyPrefix.TabIndex = 104;
            // 
            // lblDecoyPrefix
            // 
            this.lblDecoyPrefix.AutoSize = true;
            this.lblDecoyPrefix.ContextMenuStrip = this.cmWhatsThis;
            this.lblDecoyPrefix.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblDecoyPrefix.Location = new System.Drawing.Point( 15, 80 );
            this.lblDecoyPrefix.Name = "lblDecoyPrefix";
            this.lblDecoyPrefix.Size = new System.Drawing.Size( 72, 13 );
            this.lblDecoyPrefix.TabIndex = 103;
            this.lblDecoyPrefix.Text = "Decoy prefix:";
            // 
            // lblDestDir
            // 
            this.lblDestDir.AutoSize = true;
            this.lblDestDir.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblDestDir.Location = new System.Drawing.Point( 15, 52 );
            this.lblDestDir.Name = "lblDestDir";
            this.lblDestDir.Size = new System.Drawing.Size( 128, 13 );
            this.lblDestDir.TabIndex = 102;
            this.lblDestDir.Text = "Report Output Directory:";
            // 
            // btnBrowseDestDir
            // 
            this.btnBrowseDestDir.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.btnBrowseDestDir.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnBrowseDestDir.Location = new System.Drawing.Point( 631, 49 );
            this.btnBrowseDestDir.Name = "btnBrowseDestDir";
            this.btnBrowseDestDir.Size = new System.Drawing.Size( 28, 21 );
            this.btnBrowseDestDir.TabIndex = 101;
            this.btnBrowseDestDir.Text = "...";
            this.btnBrowseDestDir.UseVisualStyleBackColor = true;
            this.btnBrowseDestDir.Click += new System.EventHandler( this.btnBrowseDestDir_Click );
            // 
            // tbResultsDir
            // 
            this.tbResultsDir.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.tbResultsDir.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.tbResultsDir.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbResultsDir.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbResultsDir.Location = new System.Drawing.Point( 143, 49 );
            this.tbResultsDir.Name = "tbResultsDir";
            this.tbResultsDir.Size = new System.Drawing.Size( 481, 21 );
            this.tbResultsDir.TabIndex = 100;
            // 
            // tbReportName
            // 
            this.tbReportName.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.tbReportName.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbReportName.Location = new System.Drawing.Point( 143, 21 );
            this.tbReportName.Name = "tbReportName";
            this.tbReportName.Size = new System.Drawing.Size( 516, 21 );
            this.tbReportName.TabIndex = 99;
            this.tbReportName.TextChanged += new System.EventHandler( this.tbReportName_TextChanged );
            this.tbReportName.KeyPress += new System.Windows.Forms.KeyPressEventHandler( this.tbReportName_KeyPress );
            // 
            // gbInputFiles
            // 
            this.gbInputFiles.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.gbInputFiles.BackColor = System.Drawing.Color.Transparent;
            this.gbInputFiles.Controls.Add( this.cbShowFileErrors );
            this.gbInputFiles.Controls.Add( this.cbInclSubDirs );
            this.gbInputFiles.Controls.Add( this.cboDbsInFiles );
            this.gbInputFiles.Controls.Add( this.lblDbInSelFiles );
            this.gbInputFiles.Controls.Add( this.lblFilter );
            this.gbInputFiles.Controls.Add( this.tbFilter );
            this.gbInputFiles.Controls.Add( this.tvSelDirs );
            this.gbInputFiles.Controls.Add( this.btnGetFileNames );
            this.gbInputFiles.Controls.Add( this.tbSrcDir );
            this.gbInputFiles.Controls.Add( this.lblSourceDir );
            this.gbInputFiles.Controls.Add( this.btnBrowseSrcDir );
            this.gbInputFiles.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.gbInputFiles.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.gbInputFiles.Location = new System.Drawing.Point( 12, 132 );
            this.gbInputFiles.Name = "gbInputFiles";
            this.gbInputFiles.Size = new System.Drawing.Size( 678, 459 );
            this.gbInputFiles.TabIndex = 111;
            this.gbInputFiles.TabStop = false;
            this.gbInputFiles.Text = "Select pepXML Input Files";
            // 
            // cbShowFileErrors
            // 
            this.cbShowFileErrors.AutoSize = true;
            this.cbShowFileErrors.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cbShowFileErrors.Location = new System.Drawing.Point( 260, 76 );
            this.cbShowFileErrors.Name = "cbShowFileErrors";
            this.cbShowFileErrors.Size = new System.Drawing.Size( 101, 17 );
            this.cbShowFileErrors.TabIndex = 107;
            this.cbShowFileErrors.Text = "Show file errors";
            this.cbShowFileErrors.UseVisualStyleBackColor = true;
            // 
            // cbInclSubDirs
            // 
            this.cbInclSubDirs.AutoSize = true;
            this.cbInclSubDirs.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cbInclSubDirs.Location = new System.Drawing.Point( 143, 76 );
            this.cbInclSubDirs.Name = "cbInclSubDirs";
            this.cbInclSubDirs.Size = new System.Drawing.Size( 117, 17 );
            this.cbInclSubDirs.TabIndex = 102;
            this.cbInclSubDirs.Text = "Include sub folders";
            this.cbInclSubDirs.UseVisualStyleBackColor = true;
            // 
            // cboDbsInFiles
            // 
            this.cboDbsInFiles.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.cboDbsInFiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboDbsInFiles.Enabled = false;
            this.cboDbsInFiles.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cboDbsInFiles.FormattingEnabled = true;
            this.cboDbsInFiles.Location = new System.Drawing.Point( 124, 152 );
            this.cboDbsInFiles.Name = "cboDbsInFiles";
            this.cboDbsInFiles.Size = new System.Drawing.Size( 534, 21 );
            this.cboDbsInFiles.TabIndex = 106;
            this.cboDbsInFiles.SelectedIndexChanged += new System.EventHandler( this.cboDbsInFiles_SelectedIndexChanged );
            // 
            // lblDbInSelFiles
            // 
            this.lblDbInSelFiles.AutoSize = true;
            this.lblDbInSelFiles.ContextMenuStrip = this.cmWhatsThis;
            this.lblDbInSelFiles.Enabled = false;
            this.lblDbInSelFiles.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblDbInSelFiles.Location = new System.Drawing.Point( 15, 155 );
            this.lblDbInSelFiles.Name = "lblDbInSelFiles";
            this.lblDbInSelFiles.Size = new System.Drawing.Size( 57, 13 );
            this.lblDbInSelFiles.TabIndex = 101;
            this.lblDbInSelFiles.Text = "Database:";
            // 
            // lblFilter
            // 
            this.lblFilter.AutoSize = true;
            this.lblFilter.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblFilter.Location = new System.Drawing.Point( 15, 53 );
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Size = new System.Drawing.Size( 73, 13 );
            this.lblFilter.TabIndex = 105;
            this.lblFilter.Text = "Filter File List:";
            // 
            // tbFilter
            // 
            this.tbFilter.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.tbFilter.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbFilter.Location = new System.Drawing.Point( 143, 50 );
            this.tbFilter.Name = "tbFilter";
            this.tbFilter.Size = new System.Drawing.Size( 516, 21 );
            this.tbFilter.TabIndex = 104;
            // 
            // tvSelDirs
            // 
            this.tvSelDirs.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.tvSelDirs.BackColor = System.Drawing.Color.White;
            this.tvSelDirs.CheckBoxes = true;
            this.tvSelDirs.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tvSelDirs.Location = new System.Drawing.Point( 17, 179 );
            this.tvSelDirs.Name = "tvSelDirs";
            this.tvSelDirs.ShowNodeToolTips = true;
            this.tvSelDirs.Size = new System.Drawing.Size( 642, 261 );
            this.tvSelDirs.TabIndex = 103;
            this.tvSelDirs.AfterCheck += new System.Windows.Forms.TreeViewEventHandler( this.tvSelDirs_AfterCheck );
            this.tvSelDirs.BeforeCheck += new System.Windows.Forms.TreeViewCancelEventHandler( this.tvSelDirs_BeforeCheck );
            this.tvSelDirs.BeforeSelect += new System.Windows.Forms.TreeViewCancelEventHandler( this.tvSelDirs_BeforeSelect );
            // 
            // btnGetFileNames
            // 
            this.btnGetFileNames.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnGetFileNames.AutoSize = true;
            this.btnGetFileNames.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnGetFileNames.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnGetFileNames.Location = new System.Drawing.Point( 311, 108 );
            this.btnGetFileNames.Name = "btnGetFileNames";
            this.btnGetFileNames.Size = new System.Drawing.Size( 57, 23 );
            this.btnGetFileNames.TabIndex = 101;
            this.btnGetFileNames.Text = "List Files";
            this.btnGetFileNames.UseVisualStyleBackColor = true;
            this.btnGetFileNames.Click += new System.EventHandler( this.btnGetFileNames_Click );
            // 
            // tbSrcDir
            // 
            this.tbSrcDir.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.tbSrcDir.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.tbSrcDir.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbSrcDir.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbSrcDir.Location = new System.Drawing.Point( 143, 23 );
            this.tbSrcDir.Name = "tbSrcDir";
            this.tbSrcDir.Size = new System.Drawing.Size( 482, 21 );
            this.tbSrcDir.TabIndex = 100;
            this.tbSrcDir.TextChanged += new System.EventHandler( this.tbSrcDir_TextChanged );
            // 
            // lblSourceDir
            // 
            this.lblSourceDir.AutoSize = true;
            this.lblSourceDir.BackColor = System.Drawing.Color.Transparent;
            this.lblSourceDir.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblSourceDir.Location = new System.Drawing.Point( 15, 27 );
            this.lblSourceDir.Name = "lblSourceDir";
            this.lblSourceDir.Size = new System.Drawing.Size( 110, 13 );
            this.lblSourceDir.TabIndex = 98;
            this.lblSourceDir.Text = "Root Input Directory:";
            // 
            // btnBrowseSrcDir
            // 
            this.btnBrowseSrcDir.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.btnBrowseSrcDir.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnBrowseSrcDir.Location = new System.Drawing.Point( 631, 22 );
            this.btnBrowseSrcDir.Name = "btnBrowseSrcDir";
            this.btnBrowseSrcDir.Size = new System.Drawing.Size( 28, 21 );
            this.btnBrowseSrcDir.TabIndex = 97;
            this.btnBrowseSrcDir.Text = "...";
            this.btnBrowseSrcDir.UseVisualStyleBackColor = true;
            this.btnBrowseSrcDir.Click += new System.EventHandler( this.btnBrowseSrcDir_Click );
            // 
            // pnlRunReportStep2
            // 
            this.pnlRunReportStep2.BackColor = System.Drawing.Color.Transparent;
            this.pnlRunReportStep2.Controls.Add( this.autoExportCheckBox );
            this.pnlRunReportStep2.Controls.Add( this.splitOptionsStep2 );
            this.pnlRunReportStep2.Controls.Add( splitcontGroups );
            this.pnlRunReportStep2.Controls.Add( this.pnlBottomGroupsTreeView );
            this.pnlRunReportStep2.Controls.Add( this.btnBackStep2 );
            this.pnlRunReportStep2.Controls.Add( this.btnFinish );
            this.pnlRunReportStep2.Controls.Add( this.btnCancel2 );
            this.pnlRunReportStep2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRunReportStep2.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.pnlRunReportStep2.Location = new System.Drawing.Point( 0, 0 );
            this.pnlRunReportStep2.Name = "pnlRunReportStep2";
            this.pnlRunReportStep2.Size = new System.Drawing.Size( 710, 646 );
            this.pnlRunReportStep2.TabIndex = 107;
            this.pnlRunReportStep2.Visible = false;
            // 
            // splitOptionsStep2
            // 
            this.splitOptionsStep2.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.splitOptionsStep2.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.splitOptionsStep2.IsSplitterFixed = true;
            this.splitOptionsStep2.Location = new System.Drawing.Point( 12, 483 );
            this.splitOptionsStep2.Name = "splitOptionsStep2";
            // 
            // splitOptionsStep2.Panel1
            // 
            this.splitOptionsStep2.Panel1.Controls.Add( this.gbPeptideDetails );
            this.splitOptionsStep2.Panel1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            // 
            // splitOptionsStep2.Panel2
            // 
            this.splitOptionsStep2.Panel2.Controls.Add( this.gbProteinDetails );
            this.splitOptionsStep2.Panel2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.splitOptionsStep2.Size = new System.Drawing.Size( 683, 108 );
            this.splitOptionsStep2.SplitterDistance = 340;
            this.splitOptionsStep2.TabIndex = 127;
            // 
            // gbPeptideDetails
            // 
            this.gbPeptideDetails.Controls.Add( this.lblPercentSign );
            this.gbPeptideDetails.Controls.Add( this.tbMinPepLength );
            this.gbPeptideDetails.Controls.Add( this.tbMaxAmbigIds );
            this.gbPeptideDetails.Controls.Add( this.lblMinPeptideLength );
            this.gbPeptideDetails.Controls.Add( this.lblMaxAmbigIds );
            this.gbPeptideDetails.Controls.Add( this.cboMaxFdr );
            this.gbPeptideDetails.Controls.Add( this.lblMaxFdr );
            this.gbPeptideDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbPeptideDetails.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.gbPeptideDetails.Location = new System.Drawing.Point( 0, 0 );
            this.gbPeptideDetails.Name = "gbPeptideDetails";
            this.gbPeptideDetails.Size = new System.Drawing.Size( 340, 108 );
            this.gbPeptideDetails.TabIndex = 125;
            this.gbPeptideDetails.TabStop = false;
            this.gbPeptideDetails.Text = "Peptide Level Filters";
            // 
            // lblPercentSign
            // 
            this.lblPercentSign.AutoSize = true;
            this.lblPercentSign.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblPercentSign.Location = new System.Drawing.Point( 194, 24 );
            this.lblPercentSign.Name = "lblPercentSign";
            this.lblPercentSign.Size = new System.Drawing.Size( 18, 13 );
            this.lblPercentSign.TabIndex = 129;
            this.lblPercentSign.Text = "%";
            // 
            // tbMinPepLength
            // 
            this.tbMinPepLength.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tbMinPepLength.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbMinPepLength.Location = new System.Drawing.Point( 143, 75 );
            this.tbMinPepLength.Name = "tbMinPepLength";
            this.tbMinPepLength.Size = new System.Drawing.Size( 45, 21 );
            this.tbMinPepLength.TabIndex = 6;
            // 
            // lblMinPeptideLength
            // 
            this.lblMinPeptideLength.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMinPeptideLength.AutoSize = true;
            this.lblMinPeptideLength.ContextMenuStrip = this.cmWhatsThis;
            this.lblMinPeptideLength.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblMinPeptideLength.Location = new System.Drawing.Point( 14, 75 );
            this.lblMinPeptideLength.Name = "lblMinPeptideLength";
            this.lblMinPeptideLength.Size = new System.Drawing.Size( 123, 13 );
            this.lblMinPeptideLength.TabIndex = 127;
            this.lblMinPeptideLength.Text = "Minimum peptide length:";
            // 
            // lblMaxAmbigIds
            // 
            this.lblMaxAmbigIds.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMaxAmbigIds.AutoSize = true;
            this.lblMaxAmbigIds.ContextMenuStrip = this.cmWhatsThis;
            this.lblMaxAmbigIds.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblMaxAmbigIds.Location = new System.Drawing.Point( 14, 49 );
            this.lblMaxAmbigIds.Name = "lblMaxAmbigIds";
            this.lblMaxAmbigIds.Size = new System.Drawing.Size( 125, 13 );
            this.lblMaxAmbigIds.TabIndex = 122;
            this.lblMaxAmbigIds.Text = "Maximum ambiguous ids:";
            // 
            // cboMaxFdr
            // 
            this.cboMaxFdr.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.cboMaxFdr.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cboMaxFdr.FormattingEnabled = true;
            this.cboMaxFdr.Items.AddRange( new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10"} );
            this.cboMaxFdr.Location = new System.Drawing.Point( 143, 21 );
            this.cboMaxFdr.Name = "cboMaxFdr";
            this.cboMaxFdr.Size = new System.Drawing.Size( 45, 21 );
            this.cboMaxFdr.TabIndex = 4;
            this.cboMaxFdr.Text = "5%";
            // 
            // lblMaxFdr
            // 
            this.lblMaxFdr.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMaxFdr.AutoSize = true;
            this.lblMaxFdr.BackColor = System.Drawing.Color.Transparent;
            this.lblMaxFdr.ContextMenuStrip = this.cmWhatsThis;
            this.lblMaxFdr.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblMaxFdr.Location = new System.Drawing.Point( 14, 24 );
            this.lblMaxFdr.Name = "lblMaxFdr";
            this.lblMaxFdr.Size = new System.Drawing.Size( 78, 13 );
            this.lblMaxFdr.TabIndex = 125;
            this.lblMaxFdr.Text = "Maximum FDR:";
            // 
            // gbProteinDetails
            // 
            this.gbProteinDetails.Controls.Add( this.tbMinSpectraPerProtein );
            this.gbProteinDetails.Controls.Add( this.lblMinSpectraPerProtein );
            this.gbProteinDetails.Controls.Add( this.lblParsimonyVariable );
            this.gbProteinDetails.Controls.Add( this.tbMinAdditionalPeptides );
            this.gbProteinDetails.Controls.Add( this.tbMinDistinctPeptides );
            this.gbProteinDetails.Controls.Add( lblMinDistinctPeptides );
            this.gbProteinDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbProteinDetails.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.gbProteinDetails.Location = new System.Drawing.Point( 0, 0 );
            this.gbProteinDetails.Name = "gbProteinDetails";
            this.gbProteinDetails.Size = new System.Drawing.Size( 339, 108 );
            this.gbProteinDetails.TabIndex = 124;
            this.gbProteinDetails.TabStop = false;
            this.gbProteinDetails.Text = "Protein Level Filters";
            // 
            // tbMinSpectraPerProtein
            // 
            this.tbMinSpectraPerProtein.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tbMinSpectraPerProtein.Location = new System.Drawing.Point( 251, 76 );
            this.tbMinSpectraPerProtein.Name = "tbMinSpectraPerProtein";
            this.tbMinSpectraPerProtein.Size = new System.Drawing.Size( 46, 21 );
            this.tbMinSpectraPerProtein.TabIndex = 11;
            // 
            // lblMinSpectraPerProtein
            // 
            this.lblMinSpectraPerProtein.AutoSize = true;
            this.lblMinSpectraPerProtein.Location = new System.Drawing.Point( 15, 77 );
            this.lblMinSpectraPerProtein.Name = "lblMinSpectraPerProtein";
            this.lblMinSpectraPerProtein.Size = new System.Drawing.Size( 146, 13 );
            this.lblMinSpectraPerProtein.TabIndex = 133;
            this.lblMinSpectraPerProtein.Text = "Minimum spectra per protein:";
            // 
            // lblParsimonyVariable
            // 
            this.lblParsimonyVariable.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblParsimonyVariable.AutoSize = true;
            this.lblParsimonyVariable.ContextMenuStrip = this.cmWhatsThis;
            this.lblParsimonyVariable.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblParsimonyVariable.Location = new System.Drawing.Point( 15, 51 );
            this.lblParsimonyVariable.Name = "lblParsimonyVariable";
            this.lblParsimonyVariable.Size = new System.Drawing.Size( 231, 13 );
            this.lblParsimonyVariable.TabIndex = 132;
            this.lblParsimonyVariable.Text = "Minimum additional peptides per protein group:";
            // 
            // tbMinAdditionalPeptides
            // 
            this.tbMinAdditionalPeptides.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tbMinAdditionalPeptides.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbMinAdditionalPeptides.Location = new System.Drawing.Point( 251, 48 );
            this.tbMinAdditionalPeptides.Name = "tbMinAdditionalPeptides";
            this.tbMinAdditionalPeptides.Size = new System.Drawing.Size( 46, 21 );
            this.tbMinAdditionalPeptides.TabIndex = 9;
            // 
            // tbMinDistinctPeptides
            // 
            this.tbMinDistinctPeptides.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tbMinDistinctPeptides.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbMinDistinctPeptides.Location = new System.Drawing.Point( 251, 21 );
            this.tbMinDistinctPeptides.Name = "tbMinDistinctPeptides";
            this.tbMinDistinctPeptides.Size = new System.Drawing.Size( 46, 21 );
            this.tbMinDistinctPeptides.TabIndex = 7;
            // 
            // btnBackStep2
            // 
            this.btnBackStep2.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.btnBackStep2.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnBackStep2.Location = new System.Drawing.Point( 461, 611 );
            this.btnBackStep2.Name = "btnBackStep2";
            this.btnBackStep2.Size = new System.Drawing.Size( 75, 23 );
            this.btnBackStep2.TabIndex = 10;
            this.btnBackStep2.Text = "Back";
            this.btnBackStep2.UseVisualStyleBackColor = true;
            this.btnBackStep2.Click += new System.EventHandler( this.btnBackStep2_Click );
            // 
            // cmRightClickGroupNode
            // 
            this.cmRightClickGroupNode.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cmRightClickGroupNode.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.addGroupToolStripMenuItem,
            this.removeGroupToolStripMenuItem1,
            this.renameGroupToolStripMenuItem} );
            this.cmRightClickGroupNode.Name = "cmRightClickGroupNode";
            this.cmRightClickGroupNode.Size = new System.Drawing.Size( 125, 70 );
            // 
            // addGroupToolStripMenuItem
            // 
            this.addGroupToolStripMenuItem.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.addGroupToolStripMenuItem.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.addGroupToolStripMenuItem.Name = "addGroupToolStripMenuItem";
            this.addGroupToolStripMenuItem.Size = new System.Drawing.Size( 124, 22 );
            this.addGroupToolStripMenuItem.Text = "Add";
            this.addGroupToolStripMenuItem.Click += new System.EventHandler( this.addGroupToolStripMenuItem_Click );
            // 
            // removeGroupToolStripMenuItem1
            // 
            this.removeGroupToolStripMenuItem1.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.removeGroupToolStripMenuItem1.Name = "removeGroupToolStripMenuItem1";
            this.removeGroupToolStripMenuItem1.Size = new System.Drawing.Size( 124, 22 );
            this.removeGroupToolStripMenuItem1.Text = "Remove";
            this.removeGroupToolStripMenuItem1.Click += new System.EventHandler( this.removeGroupToolStripMenuItem1_Click );
            // 
            // renameGroupToolStripMenuItem
            // 
            this.renameGroupToolStripMenuItem.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.renameGroupToolStripMenuItem.Name = "renameGroupToolStripMenuItem";
            this.renameGroupToolStripMenuItem.Size = new System.Drawing.Size( 124, 22 );
            this.renameGroupToolStripMenuItem.Text = "Rename";
            this.renameGroupToolStripMenuItem.Click += new System.EventHandler( this.renameGroupToolStripMenuItem_Click );
            // 
            // cmRightClickFileNode
            // 
            this.cmRightClickFileNode.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cmRightClickFileNode.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.removeFileNodeToolStripMenuItem} );
            this.cmRightClickFileNode.Name = "cmRightClickFileNode";
            this.cmRightClickFileNode.Size = new System.Drawing.Size( 125, 26 );
            // 
            // removeFileNodeToolStripMenuItem
            // 
            this.removeFileNodeToolStripMenuItem.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.removeFileNodeToolStripMenuItem.Name = "removeFileNodeToolStripMenuItem";
            this.removeFileNodeToolStripMenuItem.Size = new System.Drawing.Size( 124, 22 );
            this.removeFileNodeToolStripMenuItem.Text = "Remove";
            this.removeFileNodeToolStripMenuItem.Click += new System.EventHandler( this.removeFileNodeToolStripMenuItem_Click );
            // 
            // autoExportCheckBox
            // 
            this.autoExportCheckBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.autoExportCheckBox.AutoSize = true;
            this.autoExportCheckBox.Location = new System.Drawing.Point( 169, 615 );
            this.autoExportCheckBox.Name = "autoExportCheckBox";
            this.autoExportCheckBox.Size = new System.Drawing.Size( 146, 17 );
            this.autoExportCheckBox.TabIndex = 128;
            this.autoExportCheckBox.Text = "Automatically export TSV";
            this.autoExportCheckBox.UseVisualStyleBackColor = true;
            // 
            // RunReportForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size( 710, 646 );
            this.Controls.Add( this.pnlRunReportStep2 );
            this.Controls.Add( this.pnlRunReportStep1 );
            this.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.MainMenuStrip = this.msGroups;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size( 500, 500 );
            this.Name = "RunReportForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Load and Qonvert pepXML Search Results";
            this.Load += new System.EventHandler( this.RunReportForm_Load );
            this.Move += new System.EventHandler( this.RunReportForm_Move );
            this.cmWhatsThis.ResumeLayout( false );
            splitcontGroups.Panel1.ResumeLayout( false );
            splitcontGroups.Panel2.ResumeLayout( false );
            splitcontGroups.ResumeLayout( false );
            this.gbGroups.ResumeLayout( false );
            this.pnlBgGroups.ResumeLayout( false );
            this.msGroups.ResumeLayout( false );
            this.msGroups.PerformLayout();
            this.gbFiles.ResumeLayout( false );
            this.pnlUngroupedFiles.ResumeLayout( false );
            this.pnlUngroupedFiles.PerformLayout();
            this.msUngroupedFiles.ResumeLayout( false );
            this.msUngroupedFiles.PerformLayout();
            this.pnlBottomGroupsTreeView.ResumeLayout( false );
            this.pnlRunReportStep1.ResumeLayout( false );
            this.pnlRunReportStep1.PerformLayout();
            this.gbReportSetup.ResumeLayout( false );
            this.gbReportSetup.PerformLayout();
            this.gbInputFiles.ResumeLayout( false );
            this.gbInputFiles.PerformLayout();
            this.pnlRunReportStep2.ResumeLayout( false );
            this.splitOptionsStep2.Panel1.ResumeLayout( false );
            this.splitOptionsStep2.Panel2.ResumeLayout( false );
            this.splitOptionsStep2.ResumeLayout( false );
            this.gbPeptideDetails.ResumeLayout( false );
            this.gbPeptideDetails.PerformLayout();
            this.gbProteinDetails.ResumeLayout( false );
            this.gbProteinDetails.PerformLayout();
            this.cmRightClickGroupNode.ResumeLayout( false );
            this.cmRightClickFileNode.ResumeLayout( false );
            this.ResumeLayout( false );

        }

        #endregion

        private System.Windows.Forms.Panel pnlRunReportStep1;
        private System.Windows.Forms.GroupBox gbReportSetup;
        private System.Windows.Forms.TextBox tbReportName;
        private System.Windows.Forms.Panel panel5;
        private System.Windows.Forms.Button btnNextStep1;
        private System.Windows.Forms.Panel pnlRunReportStep2;
        private System.Windows.Forms.Button btnBackStep2;
        private System.Windows.Forms.Label lblMaxAmbigIds;
        private System.Windows.Forms.ComboBox cboMaxFdr;
        private System.Windows.Forms.Label lblMaxFdr;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox tbMaxAmbigIds;
        private System.Windows.Forms.Label lblMinPeptideLength;
        private System.Windows.Forms.TextBox tbMinPepLength;
        private System.Windows.Forms.Button btnCancel2;
        private System.Windows.Forms.Button btnFinish;
        private System.Windows.Forms.GroupBox gbPeptideDetails;
        private System.Windows.Forms.GroupBox gbProteinDetails;
        private System.Windows.Forms.TextBox tbMinDistinctPeptides;
        private System.Windows.Forms.GroupBox gbInputFiles;
        private System.Windows.Forms.CheckBox cbInclSubDirs;
        private System.Windows.Forms.ComboBox cboDbsInFiles;
        private System.Windows.Forms.Label lblDbInSelFiles;
        private System.Windows.Forms.Label lblFilter;
        private System.Windows.Forms.TextBox tbFilter;
        private System.Windows.Forms.TreeView tvSelDirs;
        private System.Windows.Forms.Button btnGetFileNames;
        private System.Windows.Forms.TextBox tbSrcDir;
        private System.Windows.Forms.Label lblSourceDir;
        private System.Windows.Forms.Button btnBrowseSrcDir;
        private System.Windows.Forms.Label lblDestDir;
        private System.Windows.Forms.Button btnBrowseDestDir;
        private System.Windows.Forms.TextBox tbResultsDir;
		private System.Windows.Forms.Label lblParsimonyVariable;
        private System.Windows.Forms.TextBox tbMinAdditionalPeptides;
        private System.Windows.Forms.ContextMenuStrip cmRightClickGroupNode;
        private System.Windows.Forms.ToolStripMenuItem addGroupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeGroupToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem renameGroupToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip cmRightClickFileNode;
        private System.Windows.Forms.ToolStripMenuItem removeFileNodeToolStripMenuItem;
        private System.Windows.Forms.GroupBox gbGroups;
        private System.Windows.Forms.TreeView tvGroups;
        private System.Windows.Forms.GroupBox gbFiles;
        private System.Windows.Forms.SplitContainer splitOptionsStep2;
        private System.Windows.Forms.Label lblPercentSign;
        private System.Windows.Forms.CheckBox cbShowFileErrors;
        private System.Windows.Forms.Button btnAddGroup;
        private System.Windows.Forms.Button btnRemoveGroups;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Panel pnlBottomGroupsTreeView;
        private System.Windows.Forms.TextBox tbDecoyPrefix;
        private System.Windows.Forms.Label lblDecoyPrefix;
        private System.Windows.Forms.TextBox tbStartHere;
        public System.Windows.Forms.ContextMenuStrip cmWhatsThis;
        private System.Windows.Forms.ToolStripMenuItem whatsThisToolStripMenuItem;
        private System.Windows.Forms.Button btnCollapseGroups;
        private System.Windows.Forms.Button btnExpandGroups;
        private System.Windows.Forms.Panel pnlBgGroups;
        private System.Windows.Forms.Panel pnlGroupsSpacer;
        private System.Windows.Forms.Panel pnlUngroupedFiles;
        private System.Windows.Forms.Panel pnlUngroupedSpacer;
        private System.Windows.Forms.MenuStrip msUngroupedFiles;
        private System.Windows.Forms.ToolStripMenuItem miDefaultGroups;
        private System.Windows.Forms.ListView lvNonGroupedFiles;
        private System.Windows.Forms.MenuStrip msGroups;
        private System.Windows.Forms.ToolStripMenuItem miResetFiles;
        private System.Windows.Forms.ToolStripMenuItem miExpandGroups;
        private System.Windows.Forms.ToolStripMenuItem miCollapseGroups;
        public System.Windows.Forms.CheckBox autoExportCheckBox;
        private System.Windows.Forms.TextBox tbMinSpectraPerProtein;
        private System.Windows.Forms.Label lblMinSpectraPerProtein;





    }
}
