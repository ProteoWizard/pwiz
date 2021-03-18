﻿namespace AutoQC
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnCopy = new System.Windows.Forms.Button();
            this.lblNoConfigs = new System.Windows.Forms.Label();
            this.labelSavedConfigurations = new System.Windows.Forms.Label();
            this.btnViewLog = new System.Windows.Forms.Button();
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabFront = new System.Windows.Forms.TabPage();
            this.listViewConfigs = new System.Windows.Forms.ListView();
            this.columnName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnUser = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnCreated = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnAdd = new System.Windows.Forms.Button();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.btnDelete = new System.Windows.Forms.ToolStripButton();
            this.btnOpenResults = new System.Windows.Forms.ToolStripButton();
            this.btnOpenPanoramaFolder = new System.Windows.Forms.ToolStripButton();
            this.btnOpenFolder = new System.Windows.Forms.ToolStripButton();
            this.btnImportConfigs = new System.Windows.Forms.Button();
            this.btnExportConfigs = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnRun = new System.Windows.Forms.Button();
            this.tabLog = new System.Windows.Forms.TabPage();
            this.btnOpenLogFolder = new System.Windows.Forms.Button();
            this.lblConfigSelect = new System.Windows.Forms.Label();
            this.textBoxLog = new System.Windows.Forms.RichTextBox();
            this.comboConfigs = new System.Windows.Forms.ComboBox();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.groupBoxAutoQcSettings = new System.Windows.Forms.GroupBox();
            this.cb_minimizeToSysTray = new System.Windows.Forms.CheckBox();
            this.cb_keepRunning = new System.Windows.Forms.CheckBox();
            this.toolTip_MainForm = new System.Windows.Forms.ToolTip(this.components);
            this.systray_icon = new System.Windows.Forms.NotifyIcon(this.components);
            this.openFolderMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripFolderToWatch = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripLogFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.tabMain.SuspendLayout();
            this.tabFront.SuspendLayout();
            this.panel1.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.panel2.SuspendLayout();
            this.tabLog.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.groupBoxAutoQcSettings.SuspendLayout();
            this.openFolderMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnEdit
            // 
            resources.ApplyResources(this.btnEdit, "btnEdit");
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.UseVisualStyleBackColor = true;
            this.btnEdit.Click += new System.EventHandler(this.HandleEditEvent);
            // 
            // btnCopy
            // 
            resources.ApplyResources(this.btnCopy, "btnCopy");
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.UseVisualStyleBackColor = true;
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            // 
            // lblNoConfigs
            // 
            resources.ApplyResources(this.lblNoConfigs, "lblNoConfigs");
            this.lblNoConfigs.ForeColor = System.Drawing.Color.Blue;
            this.lblNoConfigs.Name = "lblNoConfigs";
            // 
            // labelSavedConfigurations
            // 
            resources.ApplyResources(this.labelSavedConfigurations, "labelSavedConfigurations");
            this.labelSavedConfigurations.Name = "labelSavedConfigurations";
            // 
            // btnViewLog
            // 
            resources.ApplyResources(this.btnViewLog, "btnViewLog");
            this.btnViewLog.Name = "btnViewLog";
            this.btnViewLog.UseVisualStyleBackColor = true;
            this.btnViewLog.Click += new System.EventHandler(this.btnViewLog_Click);
            // 
            // tabMain
            // 
            resources.ApplyResources(this.tabMain, "tabMain");
            this.tabMain.Controls.Add(this.tabFront);
            this.tabMain.Controls.Add(this.tabLog);
            this.tabMain.Controls.Add(this.tabSettings);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            // 
            // tabFront
            // 
            this.tabFront.BackColor = System.Drawing.Color.Transparent;
            this.tabFront.Controls.Add(this.listViewConfigs);
            this.tabFront.Controls.Add(this.labelSavedConfigurations);
            this.tabFront.Controls.Add(this.panel1);
            this.tabFront.Controls.Add(this.panel2);
            resources.ApplyResources(this.tabFront, "tabFront");
            this.tabFront.Name = "tabFront";
            // 
            // listViewConfigs
            // 
            resources.ApplyResources(this.listViewConfigs, "listViewConfigs");
            this.listViewConfigs.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnName,
            this.columnUser,
            this.columnCreated,
            this.columnStatus});
            this.listViewConfigs.FullRowSelect = true;
            this.listViewConfigs.HideSelection = false;
            this.listViewConfigs.MultiSelect = false;
            this.listViewConfigs.Name = "listViewConfigs";
            this.listViewConfigs.Scrollable = false;
            this.listViewConfigs.UseCompatibleStateImageBehavior = false;
            this.listViewConfigs.View = System.Windows.Forms.View.Details;
            this.listViewConfigs.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listViewConfigs_ColumnClick);
            this.listViewConfigs.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listViewConfigs_PreventItemSelectionChanged);
            this.listViewConfigs.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.HandleEditEvent);
            this.listViewConfigs.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listViewConfigs_MouseDown);
            this.listViewConfigs.Resize += new System.EventHandler(this.listViewConfigs_Resize);
            // 
            // columnName
            // 
            resources.ApplyResources(this.columnName, "columnName");
            // 
            // columnUser
            // 
            resources.ApplyResources(this.columnUser, "columnUser");
            // 
            // columnCreated
            // 
            resources.ApplyResources(this.columnCreated, "columnCreated");
            // 
            // columnStatus
            // 
            resources.ApplyResources(this.columnStatus, "columnStatus");
            // 
            // panel1
            // 
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Controls.Add(this.btnAdd);
            this.panel1.Controls.Add(this.toolStrip);
            this.panel1.Controls.Add(this.btnImportConfigs);
            this.panel1.Controls.Add(this.btnExportConfigs);
            this.panel1.Controls.Add(this.btnEdit);
            this.panel1.Controls.Add(this.btnCopy);
            this.panel1.Name = "panel1";
            // 
            // btnAdd
            // 
            resources.ApplyResources(this.btnAdd, "btnAdd");
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // toolStrip
            // 
            this.toolStrip.BackColor = System.Drawing.SystemColors.Control;
            resources.ApplyResources(this.toolStrip, "toolStrip");
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnDelete,
            this.btnOpenResults,
            this.btnOpenPanoramaFolder,
            this.btnOpenFolder});
            this.toolStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStrip.Name = "toolStrip";
            // 
            // btnDelete
            // 
            this.btnDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnDelete, "btnDelete");
            this.btnDelete.Image = global::AutoQC.Properties.Resources.Delete;
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnOpenResults
            // 
            this.btnOpenResults.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnOpenResults, "btnOpenResults");
            this.btnOpenResults.Image = global::AutoQC.Properties.Resources.SkylineData;
            this.btnOpenResults.Name = "btnOpenResults";
            this.btnOpenResults.Click += new System.EventHandler(this.btnOpenResults_Click);
            // 
            // btnOpenPanoramaFolder
            // 
            this.btnOpenPanoramaFolder.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnOpenPanoramaFolder, "btnOpenPanoramaFolder");
            this.btnOpenPanoramaFolder.Image = global::AutoQC.Properties.Resources.Panorama;
            this.btnOpenPanoramaFolder.Name = "btnOpenPanoramaFolder";
            this.btnOpenPanoramaFolder.Click += new System.EventHandler(this.btnOpenPanoramaFolder_Click);
            // 
            // btnOpenFolder
            // 
            this.btnOpenFolder.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnOpenFolder, "btnOpenFolder");
            this.btnOpenFolder.Image = global::AutoQC.Properties.Resources.OpenFolder;
            this.btnOpenFolder.Name = "btnOpenFolder";
            this.btnOpenFolder.Click += new System.EventHandler(this.btnOpenFolder_Click);
            // 
            // btnImportConfigs
            // 
            resources.ApplyResources(this.btnImportConfigs, "btnImportConfigs");
            this.btnImportConfigs.Name = "btnImportConfigs";
            this.toolTip_MainForm.SetToolTip(this.btnImportConfigs, resources.GetString("btnImportConfigs.ToolTip"));
            this.btnImportConfigs.UseVisualStyleBackColor = true;
            this.btnImportConfigs.Click += new System.EventHandler(this.btnImport_Click);
            // 
            // btnExportConfigs
            // 
            resources.ApplyResources(this.btnExportConfigs, "btnExportConfigs");
            this.btnExportConfigs.Name = "btnExportConfigs";
            this.toolTip_MainForm.SetToolTip(this.btnExportConfigs, resources.GetString("btnExportConfigs.ToolTip"));
            this.btnExportConfigs.UseVisualStyleBackColor = true;
            this.btnExportConfigs.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // panel2
            // 
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Controls.Add(this.btnStop);
            this.panel2.Controls.Add(this.btnRun);
            this.panel2.Controls.Add(this.btnViewLog);
            this.panel2.Controls.Add(this.lblNoConfigs);
            this.panel2.Name = "panel2";
            // 
            // btnStop
            // 
            resources.ApplyResources(this.btnStop, "btnStop");
            this.btnStop.Name = "btnStop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.MouseClick += new System.Windows.Forms.MouseEventHandler(this.btnStop_MouseClick);
            // 
            // btnRun
            // 
            resources.ApplyResources(this.btnRun, "btnRun");
            this.btnRun.Name = "btnRun";
            this.btnRun.UseVisualStyleBackColor = true;
            this.btnRun.MouseClick += new System.Windows.Forms.MouseEventHandler(this.btnRun_MouseClick);
            // 
            // tabLog
            // 
            this.tabLog.BackColor = System.Drawing.Color.Transparent;
            this.tabLog.Controls.Add(this.btnOpenLogFolder);
            this.tabLog.Controls.Add(this.lblConfigSelect);
            this.tabLog.Controls.Add(this.textBoxLog);
            this.tabLog.Controls.Add(this.comboConfigs);
            resources.ApplyResources(this.tabLog, "tabLog");
            this.tabLog.Name = "tabLog";
            // 
            // btnOpenLogFolder
            // 
            resources.ApplyResources(this.btnOpenLogFolder, "btnOpenLogFolder");
            this.btnOpenLogFolder.Name = "btnOpenLogFolder";
            this.btnOpenLogFolder.UseVisualStyleBackColor = true;
            this.btnOpenLogFolder.Click += new System.EventHandler(this.btnOpenLogFolder_Click);
            // 
            // lblConfigSelect
            // 
            resources.ApplyResources(this.lblConfigSelect, "lblConfigSelect");
            this.lblConfigSelect.Name = "lblConfigSelect";
            // 
            // textBoxLog
            // 
            resources.ApplyResources(this.textBoxLog, "textBoxLog");
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.ReadOnly = true;
            // 
            // comboConfigs
            // 
            this.comboConfigs.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboConfigs.FormattingEnabled = true;
            resources.ApplyResources(this.comboConfigs, "comboConfigs");
            this.comboConfigs.Name = "comboConfigs";
            this.comboConfigs.SelectedIndexChanged += new System.EventHandler(this.comboConfigs_SelectedIndexChanged);
            // 
            // tabSettings
            // 
            this.tabSettings.BackColor = System.Drawing.SystemColors.Control;
            this.tabSettings.Controls.Add(this.groupBoxAutoQcSettings);
            resources.ApplyResources(this.tabSettings, "tabSettings");
            this.tabSettings.Name = "tabSettings";
            // 
            // groupBoxAutoQcSettings
            // 
            resources.ApplyResources(this.groupBoxAutoQcSettings, "groupBoxAutoQcSettings");
            this.groupBoxAutoQcSettings.Controls.Add(this.cb_minimizeToSysTray);
            this.groupBoxAutoQcSettings.Controls.Add(this.cb_keepRunning);
            this.groupBoxAutoQcSettings.Name = "groupBoxAutoQcSettings";
            this.groupBoxAutoQcSettings.TabStop = false;
            // 
            // cb_minimizeToSysTray
            // 
            resources.ApplyResources(this.cb_minimizeToSysTray, "cb_minimizeToSysTray");
            this.cb_minimizeToSysTray.Name = "cb_minimizeToSysTray";
            this.cb_minimizeToSysTray.UseVisualStyleBackColor = true;
            // 
            // cb_keepRunning
            // 
            resources.ApplyResources(this.cb_keepRunning, "cb_keepRunning");
            this.cb_keepRunning.Name = "cb_keepRunning";
            this.toolTip_MainForm.SetToolTip(this.cb_keepRunning, resources.GetString("cb_keepRunning.ToolTip"));
            this.cb_keepRunning.UseVisualStyleBackColor = true;
            // 
            // systray_icon
            // 
            resources.ApplyResources(this.systray_icon, "systray_icon");
            this.systray_icon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.systray_icon_MouseDoubleClick);
            // 
            // openFolderMenuStrip
            // 
            this.openFolderMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripFolderToWatch,
            this.toolStripLogFolder});
            this.openFolderMenuStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Table;
            this.openFolderMenuStrip.Name = "openFolderMenuStrip";
            this.openFolderMenuStrip.ShowImageMargin = false;
            resources.ApplyResources(this.openFolderMenuStrip, "openFolderMenuStrip");
            // 
            // toolStripFolderToWatch
            // 
            this.toolStripFolderToWatch.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripFolderToWatch.Name = "toolStripFolderToWatch";
            resources.ApplyResources(this.toolStripFolderToWatch, "toolStripFolderToWatch");
            this.toolStripFolderToWatch.Click += new System.EventHandler(this.toolStripFolderToWatch_Click);
            // 
            // toolStripLogFolder
            // 
            this.toolStripLogFolder.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripLogFolder.Name = "toolStripLogFolder";
            resources.ApplyResources(this.toolStripLogFolder, "toolStripLogFolder");
            this.toolStripLogFolder.Click += new System.EventHandler(this.toolStripLogFolder_Click);
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tabMain);
            this.Name = "MainForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.tabMain.ResumeLayout(false);
            this.tabFront.ResumeLayout(false);
            this.tabFront.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.tabLog.ResumeLayout(false);
            this.tabLog.PerformLayout();
            this.tabSettings.ResumeLayout(false);
            this.groupBoxAutoQcSettings.ResumeLayout(false);
            this.groupBoxAutoQcSettings.PerformLayout();
            this.openFolderMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnCopy;
        private System.Windows.Forms.Label lblNoConfigs;
        private System.Windows.Forms.Label labelSavedConfigurations;
        private System.Windows.Forms.Button btnViewLog;
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabFront;
        private System.Windows.Forms.TabPage tabLog;
        private System.Windows.Forms.Label lblConfigSelect;
        private System.Windows.Forms.RichTextBox textBoxLog;
        private System.Windows.Forms.ComboBox comboConfigs;
        private System.Windows.Forms.Button btnOpenLogFolder;
        private System.Windows.Forms.Button btnImportConfigs;
        private System.Windows.Forms.Button btnExportConfigs;
        private System.Windows.Forms.ToolTip toolTip_MainForm;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.CheckBox cb_minimizeToSysTray;
        private System.Windows.Forms.GroupBox groupBoxAutoQcSettings;
        private System.Windows.Forms.CheckBox cb_keepRunning;
        private System.Windows.Forms.NotifyIcon systray_icon;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.ListView listViewConfigs;
        private System.Windows.Forms.ColumnHeader columnName;
        private System.Windows.Forms.ColumnHeader columnUser;
        private System.Windows.Forms.ColumnHeader columnCreated;
        private System.Windows.Forms.ColumnHeader columnStatus;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton btnDelete;
        private System.Windows.Forms.ToolStripButton btnOpenResults;
        private System.Windows.Forms.ToolStripButton btnOpenFolder;
        private System.Windows.Forms.ToolStripButton btnOpenPanoramaFolder;
        private System.Windows.Forms.ContextMenuStrip openFolderMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem toolStripFolderToWatch;
        private System.Windows.Forms.ToolStripMenuItem toolStripLogFolder;
        private System.Windows.Forms.Button btnAdd;
    }
}