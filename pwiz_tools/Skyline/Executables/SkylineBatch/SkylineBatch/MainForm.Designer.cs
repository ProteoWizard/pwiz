namespace SkylineBatch
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
            this.toolTip_MainForm = new System.Windows.Forms.ToolTip(this.components);
            this.btnDeleteLogs = new System.Windows.Forms.Button();
            this.btnExportConfigs = new System.Windows.Forms.Button();
            this.btnImportConfigs = new System.Windows.Forms.Button();
            this.btnViewLog = new System.Windows.Forms.Button();
            this.btnRunBatch = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOpenFolder = new System.Windows.Forms.Button();
            this.systray_icon = new System.Windows.Forms.NotifyIcon(this.components);
            this.panelSkylineType = new System.Windows.Forms.Panel();
            this.batchRunDropDown = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.startFromStepOne = new System.Windows.Forms.ToolStripMenuItem();
            this.startFromStepTwo = new System.Windows.Forms.ToolStripMenuItem();
            this.startFromStepThree = new System.Windows.Forms.ToolStripMenuItem();
            this.startFromStepFour = new System.Windows.Forms.ToolStripMenuItem();
            this.tabLog = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.comboLogList = new System.Windows.Forms.ComboBox();
            this.textBoxLog = new System.Windows.Forms.RichTextBox();
            this.tabFront = new System.Windows.Forms.TabPage();
            this.labelSavedConfigurations = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnRunOptions = new System.Windows.Forms.Button();
            this.lblNoConfigs = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.button1 = new System.Windows.Forms.Button();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnUpArrow = new System.Windows.Forms.ToolStripButton();
            this.btnDownArrow = new System.Windows.Forms.ToolStripButton();
            this.btnDelete = new System.Windows.Forms.ToolStripButton();
            this.btnOpenAnalysis = new System.Windows.Forms.ToolStripButton();
            this.btnOpenTemplate = new System.Windows.Forms.ToolStripButton();
            this.btnOpenResults = new System.Windows.Forms.ToolStripButton();
            this.btnAddConfig = new System.Windows.Forms.Button();
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnCopy = new System.Windows.Forms.Button();
            this.tabMain = new System.Windows.Forms.TabControl();
            this.listViewConfigs = new SkylineBatch.MyListView();
            this.listViewConfigName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewModified = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.batchRunDropDown.SuspendLayout();
            this.tabLog.SuspendLayout();
            this.tabFront.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.tabMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnDeleteLogs
            // 
            resources.ApplyResources(this.btnDeleteLogs, "btnDeleteLogs");
            this.btnDeleteLogs.Name = "btnDeleteLogs";
            this.toolTip_MainForm.SetToolTip(this.btnDeleteLogs, resources.GetString("btnDeleteLogs.ToolTip"));
            this.btnDeleteLogs.UseVisualStyleBackColor = true;
            this.btnDeleteLogs.Click += new System.EventHandler(this.btnDeleteLogs_Click);
            // 
            // btnExportConfigs
            // 
            resources.ApplyResources(this.btnExportConfigs, "btnExportConfigs");
            this.btnExportConfigs.Name = "btnExportConfigs";
            this.toolTip_MainForm.SetToolTip(this.btnExportConfigs, resources.GetString("btnExportConfigs.ToolTip"));
            this.btnExportConfigs.UseVisualStyleBackColor = true;
            this.btnExportConfigs.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // btnImportConfigs
            // 
            resources.ApplyResources(this.btnImportConfigs, "btnImportConfigs");
            this.btnImportConfigs.Name = "btnImportConfigs";
            this.toolTip_MainForm.SetToolTip(this.btnImportConfigs, resources.GetString("btnImportConfigs.ToolTip"));
            this.btnImportConfigs.UseVisualStyleBackColor = true;
            this.btnImportConfigs.Click += new System.EventHandler(this.btnImport_Click);
            // 
            // btnViewLog
            // 
            resources.ApplyResources(this.btnViewLog, "btnViewLog");
            this.btnViewLog.Name = "btnViewLog";
            this.toolTip_MainForm.SetToolTip(this.btnViewLog, resources.GetString("btnViewLog.ToolTip"));
            this.btnViewLog.UseVisualStyleBackColor = true;
            this.btnViewLog.Click += new System.EventHandler(this.btnViewLog_Click);
            // 
            // btnRunBatch
            // 
            resources.ApplyResources(this.btnRunBatch, "btnRunBatch");
            this.btnRunBatch.Name = "btnRunBatch";
            this.toolTip_MainForm.SetToolTip(this.btnRunBatch, resources.GetString("btnRunBatch.ToolTip"));
            this.btnRunBatch.UseVisualStyleBackColor = true;
            this.btnRunBatch.Click += new System.EventHandler(this.btnRunBatch_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.Name = "btnCancel";
            this.toolTip_MainForm.SetToolTip(this.btnCancel, resources.GetString("btnCancel.ToolTip"));
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOpenFolder
            // 
            resources.ApplyResources(this.btnOpenFolder, "btnOpenFolder");
            this.btnOpenFolder.Name = "btnOpenFolder";
            this.toolTip_MainForm.SetToolTip(this.btnOpenFolder, resources.GetString("btnOpenFolder.ToolTip"));
            this.btnOpenFolder.UseVisualStyleBackColor = true;
            this.btnOpenFolder.Click += new System.EventHandler(this.btnOpenFolder_Click);
            // 
            // systray_icon
            // 
            resources.ApplyResources(this.systray_icon, "systray_icon");
            this.systray_icon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.systray_icon_MouseDoubleClick);
            // 
            // panelSkylineType
            // 
            resources.ApplyResources(this.panelSkylineType, "panelSkylineType");
            this.panelSkylineType.Name = "panelSkylineType";
            // 
            // batchRunDropDown
            // 
            this.batchRunDropDown.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startFromStepOne,
            this.startFromStepTwo,
            this.startFromStepThree,
            this.startFromStepFour});
            this.batchRunDropDown.Name = "batchRunDropDown";
            this.batchRunDropDown.ShowCheckMargin = true;
            this.batchRunDropDown.ShowImageMargin = false;
            resources.ApplyResources(this.batchRunDropDown, "batchRunDropDown");
            this.batchRunDropDown.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.batchRunDropDown_ItemClicked);
            // 
            // startFromStepOne
            // 
            this.startFromStepOne.Checked = true;
            this.startFromStepOne.CheckState = System.Windows.Forms.CheckState.Checked;
            this.startFromStepOne.Name = "startFromStepOne";
            resources.ApplyResources(this.startFromStepOne, "startFromStepOne");
            // 
            // startFromStepTwo
            // 
            this.startFromStepTwo.Name = "startFromStepTwo";
            resources.ApplyResources(this.startFromStepTwo, "startFromStepTwo");
            // 
            // startFromStepThree
            // 
            this.startFromStepThree.Name = "startFromStepThree";
            resources.ApplyResources(this.startFromStepThree, "startFromStepThree");
            // 
            // startFromStepFour
            // 
            this.startFromStepFour.Name = "startFromStepFour";
            resources.ApplyResources(this.startFromStepFour, "startFromStepFour");
            // 
            // tabLog
            // 
            this.tabLog.BackColor = System.Drawing.Color.Transparent;
            this.tabLog.Controls.Add(this.btnOpenFolder);
            this.tabLog.Controls.Add(this.btnDeleteLogs);
            this.tabLog.Controls.Add(this.label1);
            this.tabLog.Controls.Add(this.comboLogList);
            this.tabLog.Controls.Add(this.textBoxLog);
            resources.ApplyResources(this.tabLog, "tabLog");
            this.tabLog.Name = "tabLog";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // comboLogList
            // 
            this.comboLogList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLogList.FormattingEnabled = true;
            resources.ApplyResources(this.comboLogList, "comboLogList");
            this.comboLogList.Name = "comboLogList";
            this.comboLogList.SelectedIndexChanged += new System.EventHandler(this.comboLogList_SelectedIndexChanged);
            // 
            // textBoxLog
            // 
            resources.ApplyResources(this.textBoxLog, "textBoxLog");
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.ReadOnly = true;
            // 
            // tabFront
            // 
            this.tabFront.BackColor = System.Drawing.Color.Transparent;
            this.tabFront.Controls.Add(this.listViewConfigs);
            this.tabFront.Controls.Add(this.btnViewLog);
            this.tabFront.Controls.Add(this.labelSavedConfigurations);
            this.tabFront.Controls.Add(this.panel2);
            this.tabFront.Controls.Add(this.panel1);
            resources.ApplyResources(this.tabFront, "tabFront");
            this.tabFront.Name = "tabFront";
            // 
            // labelSavedConfigurations
            // 
            resources.ApplyResources(this.labelSavedConfigurations, "labelSavedConfigurations");
            this.labelSavedConfigurations.Name = "labelSavedConfigurations";
            // 
            // panel2
            // 
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Controls.Add(this.btnRunOptions);
            this.panel2.Controls.Add(this.btnCancel);
            this.panel2.Controls.Add(this.lblNoConfigs);
            this.panel2.Controls.Add(this.btnRunBatch);
            this.panel2.Name = "panel2";
            // 
            // btnRunOptions
            // 
            resources.ApplyResources(this.btnRunOptions, "btnRunOptions");
            this.btnRunOptions.Name = "btnRunOptions";
            this.btnRunOptions.UseVisualStyleBackColor = true;
            this.btnRunOptions.Click += new System.EventHandler(this.btnRunOptions_Click);
            // 
            // lblNoConfigs
            // 
            resources.ApplyResources(this.lblNoConfigs, "lblNoConfigs");
            this.lblNoConfigs.ForeColor = System.Drawing.Color.Blue;
            this.lblNoConfigs.Name = "lblNoConfigs";
            // 
            // panel1
            // 
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Controls.Add(this.button1);
            this.panel1.Controls.Add(this.toolStrip1);
            this.panel1.Controls.Add(this.btnImportConfigs);
            this.panel1.Controls.Add(this.btnExportConfigs);
            this.panel1.Controls.Add(this.btnAddConfig);
            this.panel1.Controls.Add(this.btnEdit);
            this.panel1.Controls.Add(this.btnCopy);
            this.panel1.Name = "panel1";
            // 
            // button1
            // 
            resources.ApplyResources(this.button1, "button1");
            this.button1.Name = "button1";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // toolStrip1
            // 
            resources.ApplyResources(this.toolStrip1, "toolStrip1");
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnUpArrow,
            this.btnDownArrow,
            this.btnDelete,
            this.btnOpenAnalysis,
            this.btnOpenTemplate,
            this.btnOpenResults});
            this.toolStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStrip1.Name = "toolStrip1";
            // 
            // btnUpArrow
            // 
            this.btnUpArrow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnUpArrow, "btnUpArrow");
            this.btnUpArrow.Image = global::SkylineBatch.Properties.Resources.uparrow;
            this.btnUpArrow.Name = "btnUpArrow";
            this.btnUpArrow.Click += new System.EventHandler(this.btnUpArrow_Click);
            // 
            // btnDownArrow
            // 
            this.btnDownArrow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnDownArrow, "btnDownArrow");
            this.btnDownArrow.Image = global::SkylineBatch.Properties.Resources.downarrow;
            this.btnDownArrow.Name = "btnDownArrow";
            this.btnDownArrow.Click += new System.EventHandler(this.btnDownArrow_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnDelete, "btnDelete");
            this.btnDelete.Image = global::SkylineBatch.Properties.Resources.Delete;
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnOpenAnalysis
            // 
            this.btnOpenAnalysis.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnOpenAnalysis, "btnOpenAnalysis");
            this.btnOpenAnalysis.Image = global::SkylineBatch.Properties.Resources.OpenFolder;
            this.btnOpenAnalysis.Name = "btnOpenAnalysis";
            this.btnOpenAnalysis.Click += new System.EventHandler(this.btnOpenAnalysis_Click);
            // 
            // btnOpenTemplate
            // 
            this.btnOpenTemplate.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnOpenTemplate, "btnOpenTemplate");
            this.btnOpenTemplate.Image = global::SkylineBatch.Properties.Resources.SkylineDoc;
            this.btnOpenTemplate.Name = "btnOpenTemplate";
            this.btnOpenTemplate.Click += new System.EventHandler(this.btnOpenTemplate_Click);
            // 
            // btnOpenResults
            // 
            this.btnOpenResults.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnOpenResults, "btnOpenResults");
            this.btnOpenResults.Image = global::SkylineBatch.Properties.Resources.SkylineData;
            this.btnOpenResults.Name = "btnOpenResults";
            this.btnOpenResults.Click += new System.EventHandler(this.btnOpenResults_Click);
            // 
            // btnAddConfig
            // 
            resources.ApplyResources(this.btnAddConfig, "btnAddConfig");
            this.btnAddConfig.Name = "btnAddConfig";
            this.btnAddConfig.UseVisualStyleBackColor = true;
            this.btnAddConfig.Click += new System.EventHandler(this.btnNewConfig_Click);
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
            // tabMain
            // 
            resources.ApplyResources(this.tabMain, "tabMain");
            this.tabMain.Controls.Add(this.tabFront);
            this.tabMain.Controls.Add(this.tabLog);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            // 
            // listViewConfigs
            // 
            resources.ApplyResources(this.listViewConfigs, "listViewConfigs");
            this.listViewConfigs.CheckBoxes = true;
            this.listViewConfigs.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.listViewConfigName,
            this.listViewModified,
            this.listViewStatus});
            this.listViewConfigs.FullRowSelect = true;
            this.listViewConfigs.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listViewConfigs.HideSelection = false;
            this.listViewConfigs.MultiSelect = false;
            this.listViewConfigs.Name = "listViewConfigs";
            this.listViewConfigs.UseCompatibleStateImageBehavior = false;
            this.listViewConfigs.View = System.Windows.Forms.View.Details;
            this.listViewConfigs.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listViewConfigs_ItemCheck);
            this.listViewConfigs.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listViewConfigs_PreventItemSelectionChanged);
            this.listViewConfigs.DoubleClick += new System.EventHandler(this.HandleEditEvent);
            this.listViewConfigs.MouseUp += new System.Windows.Forms.MouseEventHandler(this.listViewConfigs_MouseUp);
            // 
            // listViewConfigName
            // 
            resources.ApplyResources(this.listViewConfigName, "listViewConfigName");
            // 
            // listViewModified
            // 
            resources.ApplyResources(this.listViewModified, "listViewModified");
            // 
            // listViewStatus
            // 
            resources.ApplyResources(this.listViewStatus, "listViewStatus");
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tabMain);
            this.Name = "MainForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.batchRunDropDown.ResumeLayout(false);
            this.tabLog.ResumeLayout(false);
            this.tabLog.PerformLayout();
            this.tabFront.ResumeLayout(false);
            this.tabFront.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.tabMain.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ToolTip toolTip_MainForm;
        private System.Windows.Forms.NotifyIcon systray_icon;
        private System.Windows.Forms.Panel panelSkylineType;
        private System.Windows.Forms.ContextMenuStrip batchRunDropDown;
        private System.Windows.Forms.ToolStripMenuItem startFromStepOne;
        private System.Windows.Forms.ToolStripMenuItem startFromStepTwo;
        private System.Windows.Forms.ToolStripMenuItem startFromStepThree;
        private System.Windows.Forms.ToolStripMenuItem startFromStepFour;
        private System.Windows.Forms.TabPage tabLog;
        private System.Windows.Forms.Button btnDeleteLogs;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboLogList;
        private System.Windows.Forms.RichTextBox textBoxLog;
        private System.Windows.Forms.TabPage tabFront;
        private System.Windows.Forms.Label labelSavedConfigurations;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnRunOptions;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblNoConfigs;
        private System.Windows.Forms.Button btnRunBatch;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button btnViewLog;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnUpArrow;
        private System.Windows.Forms.ToolStripButton btnDownArrow;
        private System.Windows.Forms.Button btnImportConfigs;
        private System.Windows.Forms.Button btnExportConfigs;
        private System.Windows.Forms.Button btnAddConfig;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnCopy;
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.Button btnOpenFolder;
        private System.Windows.Forms.ToolStripButton btnDelete;
        private System.Windows.Forms.ToolStripButton btnOpenAnalysis;
        private System.Windows.Forms.ToolStripButton btnOpenTemplate;
        private System.Windows.Forms.ToolStripButton btnOpenResults;
        private MyListView listViewConfigs;
        private System.Windows.Forms.ColumnHeader listViewConfigName;
        private System.Windows.Forms.ColumnHeader listViewModified;
        private System.Windows.Forms.ColumnHeader listViewStatus;
    }
}