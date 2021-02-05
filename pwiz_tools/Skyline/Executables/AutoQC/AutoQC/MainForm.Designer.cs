namespace AutoQC
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
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnCopy = new System.Windows.Forms.Button();
            this.lblNoConfigs = new System.Windows.Forms.Label();
            this.btnNewConfig = new System.Windows.Forms.Button();
            this.listViewConfigs = new System.Windows.Forms.ListView();
            this.listViewConfigName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewUser = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewCreated = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.labelSavedConfigurations = new System.Windows.Forms.Label();
            this.btnViewLog1 = new System.Windows.Forms.Button();
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabFront = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnImportConfigs = new System.Windows.Forms.Button();
            this.btnExportConfigs = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.tabLog = new System.Windows.Forms.TabPage();
            this.btnOpenFolder = new System.Windows.Forms.Button();
            this.btnViewLog2 = new System.Windows.Forms.Button();
            this.lblConfigSelect = new System.Windows.Forms.Label();
            this.textBoxLog = new System.Windows.Forms.RichTextBox();
            this.comboConfigs = new System.Windows.Forms.ComboBox();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.groupBoxSkylineSettings = new System.Windows.Forms.GroupBox();
            this.panelSkylineType = new System.Windows.Forms.Panel();
            this.radioButtonUseSkylineDaily = new System.Windows.Forms.RadioButton();
            this.radioButtonUseSkyline = new System.Windows.Forms.RadioButton();
            this.buttonFileDialogSkylineInstall = new System.Windows.Forms.Button();
            this.radioButtonWebBasedSkyline = new System.Windows.Forms.RadioButton();
            this.buttonApplySkylineSettings = new System.Windows.Forms.Button();
            this.radioButtonSpecifySkylinePath = new System.Windows.Forms.RadioButton();
            this.textBoxSkylinePath = new System.Windows.Forms.TextBox();
            this.label_Skylinecmd = new System.Windows.Forms.Label();
            this.groupBoxAutoQcSettings = new System.Windows.Forms.GroupBox();
            this.cb_minimizeToSysTray = new System.Windows.Forms.CheckBox();
            this.cb_keepRunning = new System.Windows.Forms.CheckBox();
            this.toolTip_MainForm = new System.Windows.Forms.ToolTip(this.components);
            this.systray_icon = new System.Windows.Forms.NotifyIcon(this.components);
            this.tabMain.SuspendLayout();
            this.tabFront.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.tabLog.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.groupBoxSkylineSettings.SuspendLayout();
            this.panelSkylineType.SuspendLayout();
            this.groupBoxAutoQcSettings.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnEdit
            // 
            resources.ApplyResources(this.btnEdit, "btnEdit");
            this.btnEdit.Name = "btnEdit";
            this.toolTip_MainForm.SetToolTip(this.btnEdit, resources.GetString("btnEdit.ToolTip"));
            this.btnEdit.UseVisualStyleBackColor = true;
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            // 
            // btnDelete
            // 
            resources.ApplyResources(this.btnDelete, "btnDelete");
            this.btnDelete.Name = "btnDelete";
            this.toolTip_MainForm.SetToolTip(this.btnDelete, resources.GetString("btnDelete.ToolTip"));
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnCopy
            // 
            resources.ApplyResources(this.btnCopy, "btnCopy");
            this.btnCopy.Name = "btnCopy";
            this.toolTip_MainForm.SetToolTip(this.btnCopy, resources.GetString("btnCopy.ToolTip"));
            this.btnCopy.UseVisualStyleBackColor = true;
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            // 
            // lblNoConfigs
            // 
            resources.ApplyResources(this.lblNoConfigs, "lblNoConfigs");
            this.lblNoConfigs.ForeColor = System.Drawing.Color.Blue;
            this.lblNoConfigs.Name = "lblNoConfigs";
            this.toolTip_MainForm.SetToolTip(this.lblNoConfigs, resources.GetString("lblNoConfigs.ToolTip"));
            // 
            // btnNewConfig
            // 
            resources.ApplyResources(this.btnNewConfig, "btnNewConfig");
            this.btnNewConfig.Name = "btnNewConfig";
            this.toolTip_MainForm.SetToolTip(this.btnNewConfig, resources.GetString("btnNewConfig.ToolTip"));
            this.btnNewConfig.UseVisualStyleBackColor = true;
            this.btnNewConfig.Click += new System.EventHandler(this.btnNewConfig_Click);
            // 
            // listViewConfigs
            // 
            resources.ApplyResources(this.listViewConfigs, "listViewConfigs");
            this.listViewConfigs.CheckBoxes = true;
            this.listViewConfigs.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.listViewConfigName,
            this.listViewUser,
            this.listViewCreated,
            this.listViewStatus});
            this.listViewConfigs.FullRowSelect = true;
            this.listViewConfigs.HideSelection = false;
            this.listViewConfigs.MultiSelect = false;
            this.listViewConfigs.Name = "listViewConfigs";
            this.toolTip_MainForm.SetToolTip(this.listViewConfigs, resources.GetString("listViewConfigs.ToolTip"));
            this.listViewConfigs.UseCompatibleStateImageBehavior = false;
            this.listViewConfigs.View = System.Windows.Forms.View.Details;
            this.listViewConfigs.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listViewConfigs_ColumnClick);
            this.listViewConfigs.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listViewConfigs_ItemCheck);
            this.listViewConfigs.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.listViewConfigs_ItemChecked);
            this.listViewConfigs.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listViewConfigs_ItemSelectionChanged);
            // 
            // listViewConfigName
            // 
            resources.ApplyResources(this.listViewConfigName, "listViewConfigName");
            // 
            // listViewUser
            // 
            resources.ApplyResources(this.listViewUser, "listViewUser");
            // 
            // listViewCreated
            // 
            resources.ApplyResources(this.listViewCreated, "listViewCreated");
            // 
            // listViewStatus
            // 
            resources.ApplyResources(this.listViewStatus, "listViewStatus");
            // 
            // labelSavedConfigurations
            // 
            resources.ApplyResources(this.labelSavedConfigurations, "labelSavedConfigurations");
            this.labelSavedConfigurations.Name = "labelSavedConfigurations";
            this.toolTip_MainForm.SetToolTip(this.labelSavedConfigurations, resources.GetString("labelSavedConfigurations.ToolTip"));
            // 
            // btnViewLog1
            // 
            resources.ApplyResources(this.btnViewLog1, "btnViewLog1");
            this.btnViewLog1.Name = "btnViewLog1";
            this.toolTip_MainForm.SetToolTip(this.btnViewLog1, resources.GetString("btnViewLog1.ToolTip"));
            this.btnViewLog1.UseVisualStyleBackColor = true;
            this.btnViewLog1.Click += new System.EventHandler(this.btnViewLog1_Click);
            // 
            // tabMain
            // 
            resources.ApplyResources(this.tabMain, "tabMain");
            this.tabMain.Controls.Add(this.tabFront);
            this.tabMain.Controls.Add(this.tabLog);
            this.tabMain.Controls.Add(this.tabSettings);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.toolTip_MainForm.SetToolTip(this.tabMain, resources.GetString("tabMain.ToolTip"));
            this.tabMain.Deselecting += new System.Windows.Forms.TabControlCancelEventHandler(this.TabMain_Deselecting);
            // 
            // tabFront
            // 
            resources.ApplyResources(this.tabFront, "tabFront");
            this.tabFront.BackColor = System.Drawing.Color.Transparent;
            this.tabFront.Controls.Add(this.listViewConfigs);
            this.tabFront.Controls.Add(this.labelSavedConfigurations);
            this.tabFront.Controls.Add(this.panel1);
            this.tabFront.Controls.Add(this.panel2);
            this.tabFront.Name = "tabFront";
            this.toolTip_MainForm.SetToolTip(this.tabFront, resources.GetString("tabFront.ToolTip"));
            // 
            // panel1
            // 
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Controls.Add(this.btnImportConfigs);
            this.panel1.Controls.Add(this.btnExportConfigs);
            this.panel1.Controls.Add(this.btnViewLog1);
            this.panel1.Controls.Add(this.btnEdit);
            this.panel1.Controls.Add(this.btnCopy);
            this.panel1.Controls.Add(this.btnDelete);
            this.panel1.Name = "panel1";
            this.toolTip_MainForm.SetToolTip(this.panel1, resources.GetString("panel1.ToolTip"));
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
            this.panel2.Controls.Add(this.lblNoConfigs);
            this.panel2.Controls.Add(this.btnNewConfig);
            this.panel2.Name = "panel2";
            this.toolTip_MainForm.SetToolTip(this.panel2, resources.GetString("panel2.ToolTip"));
            // 
            // tabLog
            // 
            resources.ApplyResources(this.tabLog, "tabLog");
            this.tabLog.BackColor = System.Drawing.Color.Transparent;
            this.tabLog.Controls.Add(this.btnOpenFolder);
            this.tabLog.Controls.Add(this.btnViewLog2);
            this.tabLog.Controls.Add(this.lblConfigSelect);
            this.tabLog.Controls.Add(this.textBoxLog);
            this.tabLog.Controls.Add(this.comboConfigs);
            this.tabLog.Name = "tabLog";
            this.toolTip_MainForm.SetToolTip(this.tabLog, resources.GetString("tabLog.ToolTip"));
            // 
            // btnOpenFolder
            // 
            resources.ApplyResources(this.btnOpenFolder, "btnOpenFolder");
            this.btnOpenFolder.Name = "btnOpenFolder";
            this.toolTip_MainForm.SetToolTip(this.btnOpenFolder, resources.GetString("btnOpenFolder.ToolTip"));
            this.btnOpenFolder.UseVisualStyleBackColor = true;
            this.btnOpenFolder.Click += new System.EventHandler(this.btnOpenFolder_Click);
            // 
            // btnViewLog2
            // 
            resources.ApplyResources(this.btnViewLog2, "btnViewLog2");
            this.btnViewLog2.Name = "btnViewLog2";
            this.toolTip_MainForm.SetToolTip(this.btnViewLog2, resources.GetString("btnViewLog2.ToolTip"));
            this.btnViewLog2.UseVisualStyleBackColor = true;
            this.btnViewLog2.Click += new System.EventHandler(this.btnViewLog2_Click);
            // 
            // lblConfigSelect
            // 
            resources.ApplyResources(this.lblConfigSelect, "lblConfigSelect");
            this.lblConfigSelect.Name = "lblConfigSelect";
            this.toolTip_MainForm.SetToolTip(this.lblConfigSelect, resources.GetString("lblConfigSelect.ToolTip"));
            // 
            // textBoxLog
            // 
            resources.ApplyResources(this.textBoxLog, "textBoxLog");
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.ReadOnly = true;
            this.toolTip_MainForm.SetToolTip(this.textBoxLog, resources.GetString("textBoxLog.ToolTip"));
            // 
            // comboConfigs
            // 
            resources.ApplyResources(this.comboConfigs, "comboConfigs");
            this.comboConfigs.FormattingEnabled = true;
            this.comboConfigs.Name = "comboConfigs";
            this.toolTip_MainForm.SetToolTip(this.comboConfigs, resources.GetString("comboConfigs.ToolTip"));
            // 
            // tabSettings
            // 
            resources.ApplyResources(this.tabSettings, "tabSettings");
            this.tabSettings.BackColor = System.Drawing.SystemColors.Control;
            this.tabSettings.Controls.Add(this.groupBoxSkylineSettings);
            this.tabSettings.Controls.Add(this.label_Skylinecmd);
            this.tabSettings.Controls.Add(this.groupBoxAutoQcSettings);
            this.tabSettings.Name = "tabSettings";
            this.toolTip_MainForm.SetToolTip(this.tabSettings, resources.GetString("tabSettings.ToolTip"));
            // 
            // groupBoxSkylineSettings
            // 
            resources.ApplyResources(this.groupBoxSkylineSettings, "groupBoxSkylineSettings");
            this.groupBoxSkylineSettings.Controls.Add(this.panelSkylineType);
            this.groupBoxSkylineSettings.Controls.Add(this.buttonFileDialogSkylineInstall);
            this.groupBoxSkylineSettings.Controls.Add(this.radioButtonWebBasedSkyline);
            this.groupBoxSkylineSettings.Controls.Add(this.buttonApplySkylineSettings);
            this.groupBoxSkylineSettings.Controls.Add(this.radioButtonSpecifySkylinePath);
            this.groupBoxSkylineSettings.Controls.Add(this.textBoxSkylinePath);
            this.groupBoxSkylineSettings.Name = "groupBoxSkylineSettings";
            this.groupBoxSkylineSettings.TabStop = false;
            this.toolTip_MainForm.SetToolTip(this.groupBoxSkylineSettings, resources.GetString("groupBoxSkylineSettings.ToolTip"));
            // 
            // panelSkylineType
            // 
            resources.ApplyResources(this.panelSkylineType, "panelSkylineType");
            this.panelSkylineType.Controls.Add(this.radioButtonUseSkylineDaily);
            this.panelSkylineType.Controls.Add(this.radioButtonUseSkyline);
            this.panelSkylineType.Name = "panelSkylineType";
            this.toolTip_MainForm.SetToolTip(this.panelSkylineType, resources.GetString("panelSkylineType.ToolTip"));
            // 
            // radioButtonUseSkylineDaily
            // 
            resources.ApplyResources(this.radioButtonUseSkylineDaily, "radioButtonUseSkylineDaily");
            this.radioButtonUseSkylineDaily.Name = "radioButtonUseSkylineDaily";
            this.radioButtonUseSkylineDaily.TabStop = true;
            this.toolTip_MainForm.SetToolTip(this.radioButtonUseSkylineDaily, resources.GetString("radioButtonUseSkylineDaily.ToolTip"));
            this.radioButtonUseSkylineDaily.UseVisualStyleBackColor = true;
            // 
            // radioButtonUseSkyline
            // 
            resources.ApplyResources(this.radioButtonUseSkyline, "radioButtonUseSkyline");
            this.radioButtonUseSkyline.Name = "radioButtonUseSkyline";
            this.radioButtonUseSkyline.TabStop = true;
            this.toolTip_MainForm.SetToolTip(this.radioButtonUseSkyline, resources.GetString("radioButtonUseSkyline.ToolTip"));
            this.radioButtonUseSkyline.UseVisualStyleBackColor = true;
            // 
            // buttonFileDialogSkylineInstall
            // 
            resources.ApplyResources(this.buttonFileDialogSkylineInstall, "buttonFileDialogSkylineInstall");
            this.buttonFileDialogSkylineInstall.Name = "buttonFileDialogSkylineInstall";
            this.toolTip_MainForm.SetToolTip(this.buttonFileDialogSkylineInstall, resources.GetString("buttonFileDialogSkylineInstall.ToolTip"));
            this.buttonFileDialogSkylineInstall.UseVisualStyleBackColor = true;
            this.buttonFileDialogSkylineInstall.Click += new System.EventHandler(this.buttonFileDialogSkylineInstall_click);
            // 
            // radioButtonWebBasedSkyline
            // 
            resources.ApplyResources(this.radioButtonWebBasedSkyline, "radioButtonWebBasedSkyline");
            this.radioButtonWebBasedSkyline.Name = "radioButtonWebBasedSkyline";
            this.radioButtonWebBasedSkyline.TabStop = true;
            this.toolTip_MainForm.SetToolTip(this.radioButtonWebBasedSkyline, resources.GetString("radioButtonWebBasedSkyline.ToolTip"));
            this.radioButtonWebBasedSkyline.UseVisualStyleBackColor = true;
            this.radioButtonWebBasedSkyline.CheckedChanged += new System.EventHandler(this.WebBasedInstall_Click);
            // 
            // buttonApplySkylineSettings
            // 
            resources.ApplyResources(this.buttonApplySkylineSettings, "buttonApplySkylineSettings");
            this.buttonApplySkylineSettings.Name = "buttonApplySkylineSettings";
            this.toolTip_MainForm.SetToolTip(this.buttonApplySkylineSettings, resources.GetString("buttonApplySkylineSettings.ToolTip"));
            this.buttonApplySkylineSettings.UseVisualStyleBackColor = true;
            this.buttonApplySkylineSettings.Click += new System.EventHandler(this.ApplySkylineSettings_Click);
            // 
            // radioButtonSpecifySkylinePath
            // 
            resources.ApplyResources(this.radioButtonSpecifySkylinePath, "radioButtonSpecifySkylinePath");
            this.radioButtonSpecifySkylinePath.Name = "radioButtonSpecifySkylinePath";
            this.radioButtonSpecifySkylinePath.TabStop = true;
            this.toolTip_MainForm.SetToolTip(this.radioButtonSpecifySkylinePath, resources.GetString("radioButtonSpecifySkylinePath.ToolTip"));
            this.radioButtonSpecifySkylinePath.UseVisualStyleBackColor = true;
            this.radioButtonSpecifySkylinePath.CheckedChanged += new System.EventHandler(this.SpecifyInstall_Click);
            // 
            // textBoxSkylinePath
            // 
            resources.ApplyResources(this.textBoxSkylinePath, "textBoxSkylinePath");
            this.textBoxSkylinePath.Name = "textBoxSkylinePath";
            this.toolTip_MainForm.SetToolTip(this.textBoxSkylinePath, resources.GetString("textBoxSkylinePath.ToolTip"));
            // 
            // label_Skylinecmd
            // 
            resources.ApplyResources(this.label_Skylinecmd, "label_Skylinecmd");
            this.label_Skylinecmd.Name = "label_Skylinecmd";
            this.toolTip_MainForm.SetToolTip(this.label_Skylinecmd, resources.GetString("label_Skylinecmd.ToolTip"));
            // 
            // groupBoxAutoQcSettings
            // 
            resources.ApplyResources(this.groupBoxAutoQcSettings, "groupBoxAutoQcSettings");
            this.groupBoxAutoQcSettings.Controls.Add(this.cb_minimizeToSysTray);
            this.groupBoxAutoQcSettings.Controls.Add(this.cb_keepRunning);
            this.groupBoxAutoQcSettings.Name = "groupBoxAutoQcSettings";
            this.groupBoxAutoQcSettings.TabStop = false;
            this.toolTip_MainForm.SetToolTip(this.groupBoxAutoQcSettings, resources.GetString("groupBoxAutoQcSettings.ToolTip"));
            // 
            // cb_minimizeToSysTray
            // 
            resources.ApplyResources(this.cb_minimizeToSysTray, "cb_minimizeToSysTray");
            this.cb_minimizeToSysTray.Name = "cb_minimizeToSysTray";
            this.toolTip_MainForm.SetToolTip(this.cb_minimizeToSysTray, resources.GetString("cb_minimizeToSysTray.ToolTip"));
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
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tabMain);
            this.Name = "MainForm";
            this.toolTip_MainForm.SetToolTip(this, resources.GetString("$this.ToolTip"));
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.tabMain.ResumeLayout(false);
            this.tabFront.ResumeLayout(false);
            this.tabFront.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.tabLog.ResumeLayout(false);
            this.tabLog.PerformLayout();
            this.tabSettings.ResumeLayout(false);
            this.tabSettings.PerformLayout();
            this.groupBoxSkylineSettings.ResumeLayout(false);
            this.groupBoxSkylineSettings.PerformLayout();
            this.panelSkylineType.ResumeLayout(false);
            this.panelSkylineType.PerformLayout();
            this.groupBoxAutoQcSettings.ResumeLayout(false);
            this.groupBoxAutoQcSettings.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnCopy;
        private System.Windows.Forms.Label lblNoConfigs;
        private System.Windows.Forms.Button btnNewConfig;
        private System.Windows.Forms.ListView listViewConfigs;
        private System.Windows.Forms.ColumnHeader listViewConfigName;
        private System.Windows.Forms.ColumnHeader listViewStatus;
        private System.Windows.Forms.ColumnHeader listViewUser;
        private System.Windows.Forms.ColumnHeader listViewCreated;
        private System.Windows.Forms.Label labelSavedConfigurations;
        private System.Windows.Forms.Button btnViewLog1;
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabFront;
        private System.Windows.Forms.TabPage tabLog;
        private System.Windows.Forms.Label lblConfigSelect;
        private System.Windows.Forms.RichTextBox textBoxLog;
        private System.Windows.Forms.ComboBox comboConfigs;
        private System.Windows.Forms.Button btnViewLog2;
        private System.Windows.Forms.Button btnOpenFolder;
        private System.Windows.Forms.Button btnImportConfigs;
        private System.Windows.Forms.Button btnExportConfigs;
        private System.Windows.Forms.ToolTip toolTip_MainForm;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.CheckBox cb_minimizeToSysTray;
        private System.Windows.Forms.GroupBox groupBoxAutoQcSettings;
        private System.Windows.Forms.CheckBox cb_keepRunning;
        private System.Windows.Forms.NotifyIcon systray_icon;
        private System.Windows.Forms.Button buttonApplySkylineSettings;
        private System.Windows.Forms.Label label_Skylinecmd;
        private System.Windows.Forms.TextBox textBoxSkylinePath;
        private System.Windows.Forms.Button buttonFileDialogSkylineInstall;
        private System.Windows.Forms.RadioButton radioButtonSpecifySkylinePath;
        private System.Windows.Forms.RadioButton radioButtonWebBasedSkyline;
        private System.Windows.Forms.RadioButton radioButtonUseSkylineDaily;
        private System.Windows.Forms.RadioButton radioButtonUseSkyline;
        private System.Windows.Forms.GroupBox groupBoxSkylineSettings;
        private System.Windows.Forms.Panel panelSkylineType;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel1;
    }
}