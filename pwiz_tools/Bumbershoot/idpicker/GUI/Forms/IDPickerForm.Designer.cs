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
    partial class IDPickerForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( IDPickerForm ) );
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle15 = new System.Windows.Forms.DataGridViewCellStyle();
            this.dgvReports = new System.Windows.Forms.DataGridView();
            this.ID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DATE_REQUESTED = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cmRightClickReportName = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.viewMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.REPORT_NAME = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.SRC_FILES_DIR = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DATABASE_PATH = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.RESULTS_DIR = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewImageColumn();
            this.Column3 = new System.Windows.Forms.DataGridViewImageColumn();
            this.Delete = new System.Windows.Forms.DataGridViewImageColumn();
            this.cmRightClickTab = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.closeTabToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.closeAllButThiToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pluginsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabReportsView = new System.Windows.Forms.TabControl();
            this.tabMyReports = new System.Windows.Forms.TabPage();
            this.dgvPanelHolder = new System.Windows.Forms.Panel();
            this.lblStartHere = new System.Windows.Forms.Label();
            this.dataGridViewImageColumn3 = new System.Windows.Forms.DataGridViewImageColumn();
            this.menuStripMain = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.newReportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.showTipsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dataGridViewImageColumn1 = new System.Windows.Forms.DataGridViewImageColumn();
            this.dataGridViewImageColumn2 = new System.Windows.Forms.DataGridViewImageColumn();
            this.dataGridViewImageColumn4 = new System.Windows.Forms.DataGridViewImageColumn();
            this.openSourceDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.openResultsDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ( (System.ComponentModel.ISupportInitialize) ( this.dgvReports ) ).BeginInit();
            this.cmRightClickReportName.SuspendLayout();
            this.cmRightClickTab.SuspendLayout();
            this.tabReportsView.SuspendLayout();
            this.tabMyReports.SuspendLayout();
            this.dgvPanelHolder.SuspendLayout();
            this.menuStripMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // dgvReports
            // 
            this.dgvReports.AllowUserToAddRows = false;
            this.dgvReports.AllowUserToDeleteRows = false;
            this.dgvReports.AllowUserToResizeRows = false;
            this.dgvReports.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.dgvReports.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvReports.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dgvReports.BackgroundColor = System.Drawing.Color.White;
            this.dgvReports.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvReports.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            this.dgvReports.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.WhiteSmoke;
            dataGridViewCellStyle1.Font = new System.Drawing.Font( "Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvReports.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dgvReports.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvReports.Columns.AddRange( new System.Windows.Forms.DataGridViewColumn[] {
            this.ID,
            this.DATE_REQUESTED,
            this.REPORT_NAME,
            this.SRC_FILES_DIR,
            this.DATABASE_PATH,
            this.RESULTS_DIR,
            this.Column2,
            this.Column3,
            this.Delete} );
            this.dgvReports.ContextMenuStrip = this.cmRightClickReportName;
            this.dgvReports.Cursor = System.Windows.Forms.Cursors.Default;
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle11.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle11.Font = new System.Drawing.Font( "Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            dataGridViewCellStyle11.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle11.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle11.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle11.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvReports.DefaultCellStyle = dataGridViewCellStyle11;
            this.dgvReports.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dgvReports.GridColor = System.Drawing.Color.WhiteSmoke;
            this.dgvReports.Location = new System.Drawing.Point( 1, 1 );
            this.dgvReports.Margin = new System.Windows.Forms.Padding( 0 );
            this.dgvReports.MultiSelect = false;
            this.dgvReports.Name = "dgvReports";
            this.dgvReports.ReadOnly = true;
            this.dgvReports.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.dgvReports.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle12.Font = new System.Drawing.Font( "Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            dataGridViewCellStyle12.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle12.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle12.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle12.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvReports.RowHeadersDefaultCellStyle = dataGridViewCellStyle12;
            this.dgvReports.RowHeadersVisible = false;
            this.dgvReports.RowHeadersWidth = 45;
            this.dgvReports.RowTemplate.DefaultCellStyle.Font = new System.Drawing.Font( "Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.dgvReports.RowTemplate.DefaultCellStyle.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            this.dgvReports.RowTemplate.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.White;
            this.dgvReports.RowTemplate.ReadOnly = true;
            this.dgvReports.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvReports.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvReports.ShowEditingIcon = false;
            this.dgvReports.Size = new System.Drawing.Size( 593, 410 );
            this.dgvReports.TabIndex = 0;
            this.dgvReports.CellMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.dgvReports_CellMouseClick );
            this.dgvReports.CellMouseLeave += new System.Windows.Forms.DataGridViewCellEventHandler( this.dgvReports_CellMouseLeave );
            this.dgvReports.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler( this.dgvReports_CellDoubleClick );
            this.dgvReports.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.dgvReports_CellMouseDown );
            this.dgvReports.CellMouseEnter += new System.Windows.Forms.DataGridViewCellEventHandler( this.dgvReports_CellMouseEnter );
            this.dgvReports.CellToolTipTextNeeded += new System.Windows.Forms.DataGridViewCellToolTipTextNeededEventHandler( this.dgvReports_CellToolTipTextNeeded );
            // 
            // ID
            // 
            this.ID.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.ID.DataPropertyName = "ID";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.ID.DefaultCellStyle = dataGridViewCellStyle2;
            this.ID.HeaderText = "ID";
            this.ID.Name = "ID";
            this.ID.ReadOnly = true;
            this.ID.Visible = false;
            // 
            // DATE_REQUESTED
            // 
            this.DATE_REQUESTED.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.DATE_REQUESTED.ContextMenuStrip = this.cmRightClickReportName;
            this.DATE_REQUESTED.DataPropertyName = "DATE_RUN";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.Format = "d";
            dataGridViewCellStyle3.NullValue = null;
            this.DATE_REQUESTED.DefaultCellStyle = dataGridViewCellStyle3;
            this.DATE_REQUESTED.FillWeight = 30F;
            this.DATE_REQUESTED.HeaderText = "Date Run";
            this.DATE_REQUESTED.Name = "DATE_REQUESTED";
            this.DATE_REQUESTED.ReadOnly = true;
            // 
            // cmRightClickReportName
            // 
            this.cmRightClickReportName.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.viewMenuItem,
            this.newMenuItem,
            this.exportMenuItem,
            this.deleteMenuItem,
            this.toolStripSeparator3,
            this.openSourceDirectoryToolStripMenuItem,
            this.openResultsDirectoryToolStripMenuItem} );
            this.cmRightClickReportName.Name = "contextMenuStrip1";
            this.cmRightClickReportName.Size = new System.Drawing.Size( 187, 164 );
            this.cmRightClickReportName.Opening += new System.ComponentModel.CancelEventHandler( this.cmRightClickReportName_Opening );
            // 
            // viewMenuItem
            // 
            this.viewMenuItem.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Bold );
            this.viewMenuItem.Name = "viewMenuItem";
            this.viewMenuItem.Size = new System.Drawing.Size( 186, 22 );
            this.viewMenuItem.Text = "View";
            this.viewMenuItem.Click += new System.EventHandler( this.viewMenuItem_Click );
            // 
            // newMenuItem
            // 
            this.newMenuItem.Name = "newMenuItem";
            this.newMenuItem.Size = new System.Drawing.Size( 186, 22 );
            this.newMenuItem.Text = "New...";
            this.newMenuItem.Click += new System.EventHandler( this.newMenuItem_Click );
            // 
            // exportMenuItem
            // 
            this.exportMenuItem.Name = "exportMenuItem";
            this.exportMenuItem.Size = new System.Drawing.Size( 186, 22 );
            this.exportMenuItem.Text = "Export...";
            this.exportMenuItem.Click += new System.EventHandler( this.exportMenuItem_Click );
            // 
            // deleteMenuItem
            // 
            this.deleteMenuItem.Name = "deleteMenuItem";
            this.deleteMenuItem.Size = new System.Drawing.Size( 186, 22 );
            this.deleteMenuItem.Text = "Delete";
            this.deleteMenuItem.Click += new System.EventHandler( this.deleteMenuItem_Click );
            // 
            // REPORT_NAME
            // 
            this.REPORT_NAME.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.REPORT_NAME.ContextMenuStrip = this.cmRightClickReportName;
            this.REPORT_NAME.DataPropertyName = "REPORT_NAME";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            this.REPORT_NAME.DefaultCellStyle = dataGridViewCellStyle4;
            this.REPORT_NAME.FillWeight = 42.44141F;
            this.REPORT_NAME.HeaderText = "Report Name";
            this.REPORT_NAME.Name = "REPORT_NAME";
            this.REPORT_NAME.ReadOnly = true;
            // 
            // SRC_FILES_DIR
            // 
            this.SRC_FILES_DIR.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.SRC_FILES_DIR.ContextMenuStrip = this.cmRightClickReportName;
            this.SRC_FILES_DIR.DataPropertyName = "SRC_FILES_DIR";
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            this.SRC_FILES_DIR.DefaultCellStyle = dataGridViewCellStyle5;
            this.SRC_FILES_DIR.FillWeight = 42.44141F;
            this.SRC_FILES_DIR.HeaderText = "Source Directory";
            this.SRC_FILES_DIR.Name = "SRC_FILES_DIR";
            this.SRC_FILES_DIR.ReadOnly = true;
            // 
            // DATABASE_PATH
            // 
            this.DATABASE_PATH.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.DATABASE_PATH.ContextMenuStrip = this.cmRightClickReportName;
            this.DATABASE_PATH.DataPropertyName = "DATABASE_PATH";
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            this.DATABASE_PATH.DefaultCellStyle = dataGridViewCellStyle6;
            this.DATABASE_PATH.FillWeight = 42.44141F;
            this.DATABASE_PATH.HeaderText = "Database";
            this.DATABASE_PATH.Name = "DATABASE_PATH";
            this.DATABASE_PATH.ReadOnly = true;
            // 
            // RESULTS_DIR
            // 
            this.RESULTS_DIR.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.RESULTS_DIR.ContextMenuStrip = this.cmRightClickReportName;
            this.RESULTS_DIR.DataPropertyName = "RESULTS_DIR";
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            this.RESULTS_DIR.DefaultCellStyle = dataGridViewCellStyle7;
            this.RESULTS_DIR.FillWeight = 42.44141F;
            this.RESULTS_DIR.HeaderText = "Results Directory";
            this.RESULTS_DIR.Name = "RESULTS_DIR";
            this.RESULTS_DIR.ReadOnly = true;
            // 
            // Column2
            // 
            this.Column2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCellsExceptHeader;
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle8.NullValue = ( (object) ( resources.GetObject( "dataGridViewCellStyle8.NullValue" ) ) );
            dataGridViewCellStyle8.Padding = new System.Windows.Forms.Padding( 1 );
            this.Column2.DefaultCellStyle = dataGridViewCellStyle8;
            this.Column2.HeaderText = "";
            this.Column2.Image = global::IdPickerGui.Properties.Resources.openHS;
            this.Column2.MinimumWidth = 20;
            this.Column2.Name = "Column2";
            this.Column2.ReadOnly = true;
            this.Column2.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Column2.ToolTipText = "View";
            this.Column2.Width = 20;
            // 
            // Column3
            // 
            this.Column3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCellsExceptHeader;
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle9.NullValue = ( (object) ( resources.GetObject( "dataGridViewCellStyle9.NullValue" ) ) );
            dataGridViewCellStyle9.Padding = new System.Windows.Forms.Padding( 1 );
            this.Column3.DefaultCellStyle = dataGridViewCellStyle9;
            this.Column3.HeaderText = "";
            this.Column3.Image = global::IdPickerGui.Properties.Resources.MoveToFolderHS;
            this.Column3.MinimumWidth = 20;
            this.Column3.Name = "Column3";
            this.Column3.ReadOnly = true;
            this.Column3.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Column3.ToolTipText = "Export";
            this.Column3.Width = 20;
            // 
            // Delete
            // 
            this.Delete.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCellsExceptHeader;
            dataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle10.NullValue = ( (object) ( resources.GetObject( "dataGridViewCellStyle10.NullValue" ) ) );
            dataGridViewCellStyle10.Padding = new System.Windows.Forms.Padding( 1 );
            this.Delete.DefaultCellStyle = dataGridViewCellStyle10;
            this.Delete.HeaderText = "";
            this.Delete.Image = ( (System.Drawing.Image) ( resources.GetObject( "Delete.Image" ) ) );
            this.Delete.MinimumWidth = 20;
            this.Delete.Name = "Delete";
            this.Delete.ReadOnly = true;
            this.Delete.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Delete.ToolTipText = "Delete";
            this.Delete.Width = 20;
            // 
            // cmRightClickTab
            // 
            this.cmRightClickTab.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.closeTabToolStripMenuItem,
            this.closeAllButThiToolStripMenuItem} );
            this.cmRightClickTab.Name = "cmRightClickTab";
            this.cmRightClickTab.Size = new System.Drawing.Size( 118, 48 );
            this.cmRightClickTab.Opening += new System.ComponentModel.CancelEventHandler( this.cmRightClickTab_Opening );
            // 
            // closeTabToolStripMenuItem
            // 
            this.closeTabToolStripMenuItem.Name = "closeTabToolStripMenuItem";
            this.closeTabToolStripMenuItem.Size = new System.Drawing.Size( 117, 22 );
            this.closeTabToolStripMenuItem.Text = "Close";
            this.closeTabToolStripMenuItem.Click += new System.EventHandler( this.closeTabToolStripMenuItem_Click );
            // 
            // closeAllButThiToolStripMenuItem
            // 
            this.closeAllButThiToolStripMenuItem.Name = "closeAllButThiToolStripMenuItem";
            this.closeAllButThiToolStripMenuItem.Size = new System.Drawing.Size( 117, 22 );
            this.closeAllButThiToolStripMenuItem.Text = "Close All";
            this.closeAllButThiToolStripMenuItem.Click += new System.EventHandler( this.closeAllButThiToolStripMenuItem_Click );
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem} );
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size( 35, 20 );
            this.fileToolStripMenuItem.Text = "File";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size( 94, 22 );
            this.exitToolStripMenuItem.Text = "Exit";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.optionsToolStripMenuItem,
            this.pluginsToolStripMenuItem} );
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size( 44, 20 );
            this.toolsToolStripMenuItem.Text = "Tools";
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size( 113, 22 );
            this.optionsToolStripMenuItem.Text = "Options";
            this.optionsToolStripMenuItem.Click += new System.EventHandler( this.optionsToolStripMenuItem_Click );
            // 
            // pluginsToolStripMenuItem
            // 
            this.pluginsToolStripMenuItem.Name = "pluginsToolStripMenuItem";
            this.pluginsToolStripMenuItem.Size = new System.Drawing.Size( 113, 22 );
            this.pluginsToolStripMenuItem.Text = "Plugins";
            // 
            // tabReportsView
            // 
            this.tabReportsView.ContextMenuStrip = this.cmRightClickTab;
            this.tabReportsView.Controls.Add( this.tabMyReports );
            this.tabReportsView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabReportsView.Font = new System.Drawing.Font( "Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tabReportsView.Location = new System.Drawing.Point( 0, 24 );
            this.tabReportsView.Multiline = true;
            this.tabReportsView.Name = "tabReportsView";
            this.tabReportsView.SelectedIndex = 0;
            this.tabReportsView.ShowToolTips = true;
            this.tabReportsView.Size = new System.Drawing.Size( 630, 469 );
            this.tabReportsView.TabIndex = 0;
            // 
            // tabMyReports
            // 
            this.tabMyReports.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.tabMyReports.Controls.Add( this.dgvPanelHolder );
            this.tabMyReports.Location = new System.Drawing.Point( 4, 25 );
            this.tabMyReports.Name = "tabMyReports";
            this.tabMyReports.Padding = new System.Windows.Forms.Padding( 3 );
            this.tabMyReports.Size = new System.Drawing.Size( 622, 440 );
            this.tabMyReports.TabIndex = 0;
            this.tabMyReports.Text = "My Reports";
            this.tabMyReports.UseVisualStyleBackColor = true;
            // 
            // dgvPanelHolder
            // 
            this.dgvPanelHolder.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.dgvPanelHolder.BackColor = System.Drawing.SystemColors.ButtonShadow;
            this.dgvPanelHolder.Controls.Add( this.lblStartHere );
            this.dgvPanelHolder.Controls.Add( this.dgvReports );
            this.dgvPanelHolder.Location = new System.Drawing.Point( 14, 14 );
            this.dgvPanelHolder.Name = "dgvPanelHolder";
            this.dgvPanelHolder.Size = new System.Drawing.Size( 595, 412 );
            this.dgvPanelHolder.TabIndex = 1;
            // 
            // lblStartHere
            // 
            this.lblStartHere.AutoSize = true;
            this.lblStartHere.BackColor = System.Drawing.Color.White;
            this.lblStartHere.Font = new System.Drawing.Font( "Tahoma", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblStartHere.ForeColor = System.Drawing.Color.DarkGray;
            this.lblStartHere.Location = new System.Drawing.Point( 13, 34 );
            this.lblStartHere.Name = "lblStartHere";
            this.lblStartHere.Size = new System.Drawing.Size( 351, 19 );
            this.lblStartHere.TabIndex = 1;
            this.lblStartHere.Text = "To run a new report select File -> New Report...";
            this.lblStartHere.Visible = false;
            // 
            // dataGridViewImageColumn3
            // 
            this.dataGridViewImageColumn3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCellsExceptHeader;
            this.dataGridViewImageColumn3.HeaderText = "";
            this.dataGridViewImageColumn3.Name = "dataGridViewImageColumn3";
            this.dataGridViewImageColumn3.ReadOnly = true;
            this.dataGridViewImageColumn3.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewImageColumn3.ToolTipText = "Delete";
            // 
            // menuStripMain
            // 
            this.menuStripMain.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem1,
            this.toolsToolStripMenuItem1,
            this.helpMenuItem} );
            this.menuStripMain.Location = new System.Drawing.Point( 0, 0 );
            this.menuStripMain.Name = "menuStripMain";
            this.menuStripMain.Size = new System.Drawing.Size( 630, 24 );
            this.menuStripMain.TabIndex = 3;
            this.menuStripMain.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem1
            // 
            this.fileToolStripMenuItem1.DropDownItems.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.newReportToolStripMenuItem,
            this.toolStripSeparator1,
            this.exitToolStripMenuItem1} );
            this.fileToolStripMenuItem1.Name = "fileToolStripMenuItem1";
            this.fileToolStripMenuItem1.Size = new System.Drawing.Size( 35, 20 );
            this.fileToolStripMenuItem1.Text = "File";
            // 
            // newReportToolStripMenuItem
            // 
            this.newReportToolStripMenuItem.Name = "newReportToolStripMenuItem";
            this.newReportToolStripMenuItem.ShortcutKeys = ( (System.Windows.Forms.Keys) ( ( System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N ) ) );
            this.newReportToolStripMenuItem.Size = new System.Drawing.Size( 179, 22 );
            this.newReportToolStripMenuItem.Text = "&New Report...";
            this.newReportToolStripMenuItem.Click += new System.EventHandler( this.btnNewReport_Click );
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size( 176, 6 );
            // 
            // exitToolStripMenuItem1
            // 
            this.exitToolStripMenuItem1.Name = "exitToolStripMenuItem1";
            this.exitToolStripMenuItem1.Size = new System.Drawing.Size( 179, 22 );
            this.exitToolStripMenuItem1.Text = "E&xit";
            this.exitToolStripMenuItem1.Click += new System.EventHandler( this.exitToolStripMenuItem_Click );
            // 
            // toolsToolStripMenuItem1
            // 
            this.toolsToolStripMenuItem1.DropDownItems.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.optionsToolStripMenuItem1,
            this.showTipsToolStripMenuItem} );
            this.toolsToolStripMenuItem1.Name = "toolsToolStripMenuItem1";
            this.toolsToolStripMenuItem1.Size = new System.Drawing.Size( 45, 20 );
            this.toolsToolStripMenuItem1.Text = "Tools";
            // 
            // optionsToolStripMenuItem1
            // 
            this.optionsToolStripMenuItem1.Name = "optionsToolStripMenuItem1";
            this.optionsToolStripMenuItem1.Size = new System.Drawing.Size( 136, 22 );
            this.optionsToolStripMenuItem1.Text = "Options...";
            this.optionsToolStripMenuItem1.Click += new System.EventHandler( this.optionsToolStripMenuItem_Click );
            // 
            // showTipsToolStripMenuItem
            // 
            this.showTipsToolStripMenuItem.Name = "showTipsToolStripMenuItem";
            this.showTipsToolStripMenuItem.Size = new System.Drawing.Size( 136, 22 );
            this.showTipsToolStripMenuItem.Text = "Show Tips...";
            this.showTipsToolStripMenuItem.Click += new System.EventHandler( this.showTipsToolStripMenuItem_Click );
            // 
            // helpMenuItem
            // 
            this.helpMenuItem.DropDownItems.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.documentationToolStripMenuItem,
            this.toolStripSeparator2,
            this.aboutToolStripMenuItem} );
            this.helpMenuItem.Name = "helpMenuItem";
            this.helpMenuItem.Size = new System.Drawing.Size( 41, 20 );
            this.helpMenuItem.Text = "Help";
            // 
            // documentationToolStripMenuItem
            // 
            this.documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            this.documentationToolStripMenuItem.Size = new System.Drawing.Size( 149, 22 );
            this.documentationToolStripMenuItem.Text = "Documentation";
            this.documentationToolStripMenuItem.Click += new System.EventHandler( this.documentationToolStripMenuItem_Click );
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size( 146, 6 );
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size( 149, 22 );
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler( this.aboutToolStripMenuItem_Click );
            // 
            // dataGridViewImageColumn1
            // 
            this.dataGridViewImageColumn1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCellsExceptHeader;
            dataGridViewCellStyle13.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle13.NullValue = ( (object) ( resources.GetObject( "dataGridViewCellStyle13.NullValue" ) ) );
            dataGridViewCellStyle13.Padding = new System.Windows.Forms.Padding( 1 );
            this.dataGridViewImageColumn1.DefaultCellStyle = dataGridViewCellStyle13;
            this.dataGridViewImageColumn1.HeaderText = "";
            this.dataGridViewImageColumn1.Image = ( (System.Drawing.Image) ( resources.GetObject( "dataGridViewImageColumn1.Image" ) ) );
            this.dataGridViewImageColumn1.MinimumWidth = 20;
            this.dataGridViewImageColumn1.Name = "dataGridViewImageColumn1";
            this.dataGridViewImageColumn1.ReadOnly = true;
            this.dataGridViewImageColumn1.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewImageColumn1.ToolTipText = "View";
            // 
            // dataGridViewImageColumn2
            // 
            this.dataGridViewImageColumn2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCellsExceptHeader;
            dataGridViewCellStyle14.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle14.NullValue = ( (object) ( resources.GetObject( "dataGridViewCellStyle14.NullValue" ) ) );
            dataGridViewCellStyle14.Padding = new System.Windows.Forms.Padding( 1 );
            this.dataGridViewImageColumn2.DefaultCellStyle = dataGridViewCellStyle14;
            this.dataGridViewImageColumn2.HeaderText = "";
            this.dataGridViewImageColumn2.Image = ( (System.Drawing.Image) ( resources.GetObject( "dataGridViewImageColumn2.Image" ) ) );
            this.dataGridViewImageColumn2.MinimumWidth = 20;
            this.dataGridViewImageColumn2.Name = "dataGridViewImageColumn2";
            this.dataGridViewImageColumn2.ReadOnly = true;
            this.dataGridViewImageColumn2.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewImageColumn2.ToolTipText = "Export";
            // 
            // dataGridViewImageColumn4
            // 
            this.dataGridViewImageColumn4.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCellsExceptHeader;
            dataGridViewCellStyle15.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle15.NullValue = ( (object) ( resources.GetObject( "dataGridViewCellStyle15.NullValue" ) ) );
            dataGridViewCellStyle15.Padding = new System.Windows.Forms.Padding( 1 );
            this.dataGridViewImageColumn4.DefaultCellStyle = dataGridViewCellStyle15;
            this.dataGridViewImageColumn4.HeaderText = "";
            this.dataGridViewImageColumn4.Image = global::IdPickerGui.Properties.Resources.DeleteFolderHS;
            this.dataGridViewImageColumn4.MinimumWidth = 20;
            this.dataGridViewImageColumn4.Name = "dataGridViewImageColumn4";
            this.dataGridViewImageColumn4.ReadOnly = true;
            this.dataGridViewImageColumn4.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewImageColumn4.ToolTipText = "Delete";
            // 
            // openSourceDirectoryToolStripMenuItem
            // 
            this.openSourceDirectoryToolStripMenuItem.Name = "openSourceDirectoryToolStripMenuItem";
            this.openSourceDirectoryToolStripMenuItem.Size = new System.Drawing.Size( 186, 22 );
            this.openSourceDirectoryToolStripMenuItem.Text = "Open Source Directory";
            this.openSourceDirectoryToolStripMenuItem.Click += new System.EventHandler( this.openSourceDirectoryToolStripMenuItem_Click );
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size( 183, 6 );
            // 
            // openResultsDirectoryToolStripMenuItem
            // 
            this.openResultsDirectoryToolStripMenuItem.Name = "openResultsDirectoryToolStripMenuItem";
            this.openResultsDirectoryToolStripMenuItem.Size = new System.Drawing.Size( 186, 22 );
            this.openResultsDirectoryToolStripMenuItem.Text = "Open Results Directory";
            this.openResultsDirectoryToolStripMenuItem.Click += new System.EventHandler( this.openResultsDirectoryToolStripMenuItem_Click );
            // 
            // IDPickerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size( 630, 493 );
            this.Controls.Add( this.tabReportsView );
            this.Controls.Add( this.menuStripMain );
            this.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.MinimumSize = new System.Drawing.Size( 500, 300 );
            this.Name = "IDPickerForm";
            this.Text = "IDPicker";
            this.Load += new System.EventHandler( this.IDPickerForm_Load );
            this.Move += new System.EventHandler( this.IDPickerForm_LocationChanged );
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler( this.IDPickerForm_FormClosing );
            this.Resize += new System.EventHandler( this.IDPickerForm_Resize );
            ( (System.ComponentModel.ISupportInitialize) ( this.dgvReports ) ).EndInit();
            this.cmRightClickReportName.ResumeLayout( false );
            this.cmRightClickTab.ResumeLayout( false );
            this.tabReportsView.ResumeLayout( false );
            this.tabMyReports.ResumeLayout( false );
            this.dgvPanelHolder.ResumeLayout( false );
            this.dgvPanelHolder.PerformLayout();
            this.menuStripMain.ResumeLayout( false );
            this.menuStripMain.PerformLayout();
            this.ResumeLayout( false );
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pluginsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.TabControl tabReportsView;
        private System.Windows.Forms.TabPage tabMyReports;
        private System.Windows.Forms.ContextMenuStrip cmRightClickReportName;
        private System.Windows.Forms.ToolStripMenuItem viewMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteMenuItem;
        private System.Windows.Forms.DataGridView dgvReports;
        private System.Windows.Forms.Panel dgvPanelHolder;
        private System.Windows.Forms.DataGridViewImageColumn dataGridViewImageColumn1;
        private System.Windows.Forms.DataGridViewImageColumn dataGridViewImageColumn2;
        private System.Windows.Forms.DataGridViewImageColumn dataGridViewImageColumn3;
        private System.Windows.Forms.MenuStrip menuStripMain;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem1;
        private System.Windows.Forms.DataGridViewImageColumn dataGridViewImageColumn4;
        private System.Windows.Forms.ContextMenuStrip cmRightClickTab;
        private System.Windows.Forms.ToolStripMenuItem closeTabToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem closeAllButThiToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem newReportToolStripMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem newMenuItem;
        private System.Windows.Forms.Label lblStartHere;
        private System.Windows.Forms.DataGridViewTextBoxColumn ID;
        private System.Windows.Forms.DataGridViewTextBoxColumn DATE_REQUESTED;
        private System.Windows.Forms.DataGridViewTextBoxColumn REPORT_NAME;
        private System.Windows.Forms.DataGridViewTextBoxColumn SRC_FILES_DIR;
        private System.Windows.Forms.DataGridViewTextBoxColumn DATABASE_PATH;
        private System.Windows.Forms.DataGridViewTextBoxColumn RESULTS_DIR;
        private System.Windows.Forms.DataGridViewImageColumn Column2;
        private System.Windows.Forms.DataGridViewImageColumn Column3;
        private System.Windows.Forms.DataGridViewImageColumn Delete;
        private System.Windows.Forms.ToolStripMenuItem showTipsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem openSourceDirectoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openResultsDirectoryToolStripMenuItem;
    }
}

