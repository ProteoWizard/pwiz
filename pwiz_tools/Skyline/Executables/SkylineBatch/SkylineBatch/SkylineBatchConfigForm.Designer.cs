namespace SkylineBatch
{
    partial class SkylineBatchConfigForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SkylineBatchConfigForm));
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.textAnalysisPath = new System.Windows.Forms.TextBox();
            this.textSkylinePath = new System.Windows.Forms.TextBox();
            this.textDataPath = new System.Windows.Forms.TextBox();
            this.linkLabelRegex = new System.Windows.Forms.LinkLabel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabsConfig = new System.Windows.Forms.TabControl();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.textConfigName = new System.Windows.Forms.TextBox();
            this.labelConfigName = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.btnDataPath = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.btnSkylineFilePath = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.btnAnalysisPath = new System.Windows.Forms.Button();
            this.tabReports = new System.Windows.Forms.TabPage();
            this.toolBar = new System.Windows.Forms.ToolStrip();
            this.btnAddReport = new System.Windows.Forms.ToolStripButton();
            this.btnDeleteReport = new System.Windows.Forms.ToolStripButton();
            this.btnEditReport = new System.Windows.Forms.ToolStripButton();
            this.gridReportSettings = new System.Windows.Forms.DataGridView();
            this.columnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnPath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnScripts = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabSkyline = new System.Windows.Forms.TabPage();
            this.textSkylineInstallationPath = new System.Windows.Forms.TextBox();
            this.radioButtonSkylineDaily = new System.Windows.Forms.RadioButton();
            this.radioButtonSpecifySkylinePath = new System.Windows.Forms.RadioButton();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.radioButtonSkyline = new System.Windows.Forms.RadioButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnCancelConfig = new System.Windows.Forms.Button();
            this.btnSaveConfig = new System.Windows.Forms.Button();
            this.lblConfigRunning = new System.Windows.Forms.Label();
            this.btnOkConfig = new System.Windows.Forms.Button();
            this.imageListToolbarIcons = new System.Windows.Forms.ImageList(this.components);
            this.textNamingPattern = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabsConfig.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.tabReports.SuspendLayout();
            this.toolBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridReportSettings)).BeginInit();
            this.tabSkyline.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // textAnalysisPath
            // 
            resources.ApplyResources(this.textAnalysisPath, "textAnalysisPath");
            this.textAnalysisPath.Name = "textAnalysisPath";
            this.toolTip1.SetToolTip(this.textAnalysisPath, resources.GetString("textAnalysisPath.ToolTip"));
            // 
            // textSkylinePath
            // 
            resources.ApplyResources(this.textSkylinePath, "textSkylinePath");
            this.textSkylinePath.Name = "textSkylinePath";
            this.toolTip1.SetToolTip(this.textSkylinePath, resources.GetString("textSkylinePath.ToolTip"));
            // 
            // textDataPath
            // 
            resources.ApplyResources(this.textDataPath, "textDataPath");
            this.textDataPath.Name = "textDataPath";
            this.toolTip1.SetToolTip(this.textDataPath, resources.GetString("textDataPath.ToolTip"));
            // 
            // linkLabelRegex
            // 
            resources.ApplyResources(this.linkLabelRegex, "linkLabelRegex");
            this.linkLabelRegex.Name = "linkLabelRegex";
            this.linkLabelRegex.TabStop = true;
            this.toolTip1.SetToolTip(this.linkLabelRegex, resources.GetString("linkLabelRegex.ToolTip"));
            this.linkLabelRegex.UseCompatibleTextRendering = true;
            this.linkLabelRegex.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelRegex_LinkClicked);
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabsConfig);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.groupBox1);
            // 
            // tabsConfig
            // 
            this.tabsConfig.Controls.Add(this.tabSettings);
            this.tabsConfig.Controls.Add(this.tabReports);
            this.tabsConfig.Controls.Add(this.tabSkyline);
            resources.ApplyResources(this.tabsConfig, "tabsConfig");
            this.tabsConfig.Name = "tabsConfig";
            this.tabsConfig.SelectedIndex = 0;
            // 
            // tabSettings
            // 
            this.tabSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabSettings.Controls.Add(this.textNamingPattern);
            this.tabSettings.Controls.Add(this.linkLabelRegex);
            this.tabSettings.Controls.Add(this.textConfigName);
            this.tabSettings.Controls.Add(this.labelConfigName);
            this.tabSettings.Controls.Add(this.label3);
            this.tabSettings.Controls.Add(this.btnDataPath);
            this.tabSettings.Controls.Add(this.textDataPath);
            this.tabSettings.Controls.Add(this.textAnalysisPath);
            this.tabSettings.Controls.Add(this.label1);
            this.tabSettings.Controls.Add(this.btnAnalysisPath);
            this.tabSettings.Controls.Add(this.btnSkylineFilePath);
            this.tabSettings.Controls.Add(this.label2);
            this.tabSettings.Controls.Add(this.textSkylinePath);
            resources.ApplyResources(this.tabSettings, "tabSettings");
            this.tabSettings.Name = "tabSettings";
            // 
            // textConfigName
            // 
            resources.ApplyResources(this.textConfigName, "textConfigName");
            this.textConfigName.Name = "textConfigName";
            // 
            // labelConfigName
            // 
            resources.ApplyResources(this.labelConfigName, "labelConfigName");
            this.labelConfigName.Name = "labelConfigName";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // btnDataPath
            // 
            resources.ApplyResources(this.btnDataPath, "btnDataPath");
            this.btnDataPath.Name = "btnDataPath";
            this.btnDataPath.UseVisualStyleBackColor = true;
            this.btnDataPath.Click += new System.EventHandler(this.btnDataPath_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // btnSkylineFilePath
            // 
            resources.ApplyResources(this.btnSkylineFilePath, "btnSkylineFilePath");
            this.btnSkylineFilePath.Name = "btnSkylineFilePath";
            this.btnSkylineFilePath.UseVisualStyleBackColor = true;
            this.btnSkylineFilePath.Click += new System.EventHandler(this.btnSkylineFilePath_Click);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // btnAnalysisPath
            // 
            resources.ApplyResources(this.btnAnalysisPath, "btnAnalysisPath");
            this.btnAnalysisPath.Name = "btnAnalysisPath";
            this.btnAnalysisPath.UseVisualStyleBackColor = true;
            this.btnAnalysisPath.Click += new System.EventHandler(this.btnAnalysisFilePath_Click);
            // 
            // tabReports
            // 
            this.tabReports.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabReports.Controls.Add(this.toolBar);
            this.tabReports.Controls.Add(this.gridReportSettings);
            resources.ApplyResources(this.tabReports, "tabReports");
            this.tabReports.Name = "tabReports";
            // 
            // toolBar
            // 
            this.toolBar.AllowMerge = false;
            resources.ApplyResources(this.toolBar, "toolBar");
            this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnAddReport,
            this.btnDeleteReport,
            this.btnEditReport});
            this.toolBar.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolBar.Name = "toolBar";
            // 
            // btnAddReport
            // 
            this.btnAddReport.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnAddReport.Image = global::SkylineBatch.Properties.Resources.add;
            resources.ApplyResources(this.btnAddReport, "btnAddReport");
            this.btnAddReport.Name = "btnAddReport";
            this.btnAddReport.Click += new System.EventHandler(this.btnAddReport_Click);
            // 
            // btnDeleteReport
            // 
            this.btnDeleteReport.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnDeleteReport, "btnDeleteReport");
            this.btnDeleteReport.Image = global::SkylineBatch.Properties.Resources.Delete;
            this.btnDeleteReport.Name = "btnDeleteReport";
            this.btnDeleteReport.Click += new System.EventHandler(this.btnDeleteReport_Click);
            // 
            // btnEditReport
            // 
            this.btnEditReport.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnEditReport, "btnEditReport");
            this.btnEditReport.Image = global::SkylineBatch.Properties.Resources.Comment;
            this.btnEditReport.Name = "btnEditReport";
            this.btnEditReport.Click += new System.EventHandler(this.btnEditReport_Click);
            // 
            // gridReportSettings
            // 
            resources.ApplyResources(this.gridReportSettings, "gridReportSettings");
            this.gridReportSettings.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridReportSettings.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCells;
            this.gridReportSettings.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridReportSettings.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnName,
            this.columnPath,
            this.columnScripts});
            this.gridReportSettings.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.gridReportSettings.Name = "gridReportSettings";
            this.gridReportSettings.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridReportSettings.TabStop = false;
            this.gridReportSettings.SelectionChanged += new System.EventHandler(this.gridReportSettings_SelectionChanged);
            // 
            // columnName
            // 
            resources.ApplyResources(this.columnName, "columnName");
            this.columnName.Name = "columnName";
            // 
            // columnPath
            // 
            resources.ApplyResources(this.columnPath, "columnPath");
            this.columnPath.Name = "columnPath";
            // 
            // columnScripts
            // 
            resources.ApplyResources(this.columnScripts, "columnScripts");
            this.columnScripts.Name = "columnScripts";
            // 
            // tabSkyline
            // 
            this.tabSkyline.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabSkyline.Controls.Add(this.textSkylineInstallationPath);
            this.tabSkyline.Controls.Add(this.radioButtonSkylineDaily);
            this.tabSkyline.Controls.Add(this.radioButtonSpecifySkylinePath);
            this.tabSkyline.Controls.Add(this.radioButtonSkyline);
            this.tabSkyline.Controls.Add(this.btnBrowse);
            resources.ApplyResources(this.tabSkyline, "tabSkyline");
            this.tabSkyline.Name = "tabSkyline";
            // 
            // textSkylineInstallationPath
            // 
            resources.ApplyResources(this.textSkylineInstallationPath, "textSkylineInstallationPath");
            this.textSkylineInstallationPath.Name = "textSkylineInstallationPath";
            // 
            // radioButtonSkylineDaily
            // 
            resources.ApplyResources(this.radioButtonSkylineDaily, "radioButtonSkylineDaily");
            this.radioButtonSkylineDaily.Name = "radioButtonSkylineDaily";
            this.radioButtonSkylineDaily.TabStop = true;
            this.radioButtonSkylineDaily.UseVisualStyleBackColor = true;
            // 
            // radioButtonSpecifySkylinePath
            // 
            resources.ApplyResources(this.radioButtonSpecifySkylinePath, "radioButtonSpecifySkylinePath");
            this.radioButtonSpecifySkylinePath.Name = "radioButtonSpecifySkylinePath";
            this.radioButtonSpecifySkylinePath.TabStop = true;
            this.radioButtonSpecifySkylinePath.UseVisualStyleBackColor = true;
            this.radioButtonSpecifySkylinePath.CheckedChanged += new System.EventHandler(this.radioButtonSpecifySkylinePath_CheckChanged);
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // radioButtonSkyline
            // 
            resources.ApplyResources(this.radioButtonSkyline, "radioButtonSkyline");
            this.radioButtonSkyline.Name = "radioButtonSkyline";
            this.radioButtonSkyline.TabStop = true;
            this.radioButtonSkyline.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.BackColor = System.Drawing.SystemColors.ControlLight;
            this.groupBox1.Controls.Add(this.btnSaveConfig);
            this.groupBox1.Controls.Add(this.btnCancelConfig);
            this.groupBox1.Controls.Add(this.lblConfigRunning);
            this.groupBox1.Controls.Add(this.btnOkConfig);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // btnCancelConfig
            // 
            resources.ApplyResources(this.btnCancelConfig, "btnCancelConfig");
            this.btnCancelConfig.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancelConfig.Name = "btnCancelConfig";
            this.btnCancelConfig.UseVisualStyleBackColor = true;
            // 
            // btnSaveConfig
            // 
            resources.ApplyResources(this.btnSaveConfig, "btnSaveConfig");
            this.btnSaveConfig.Name = "btnSaveConfig";
            this.btnSaveConfig.UseVisualStyleBackColor = true;
            this.btnSaveConfig.Click += new System.EventHandler(this.btnSaveConfig_Click);
            // 
            // lblConfigRunning
            // 
            resources.ApplyResources(this.lblConfigRunning, "lblConfigRunning");
            this.lblConfigRunning.ForeColor = System.Drawing.Color.DarkRed;
            this.lblConfigRunning.Name = "lblConfigRunning";
            // 
            // btnOkConfig
            // 
            resources.ApplyResources(this.btnOkConfig, "btnOkConfig");
            this.btnOkConfig.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOkConfig.Name = "btnOkConfig";
            this.btnOkConfig.UseVisualStyleBackColor = true;
            // 
            // imageListToolbarIcons
            // 
            this.imageListToolbarIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListToolbarIcons.ImageStream")));
            this.imageListToolbarIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListToolbarIcons.Images.SetKeyName(0, "AddedIcon.ico");
            this.imageListToolbarIcons.Images.SetKeyName(1, "DeletedIcon.ico");
            // 
            // textNamingPattern
            // 
            resources.ApplyResources(this.textNamingPattern, "textNamingPattern");
            this.textNamingPattern.Name = "textNamingPattern";
            // 
            // SkylineBatchConfigForm
            // 
            this.AcceptButton = this.btnSaveConfig;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancelConfig;
            this.Controls.Add(this.splitContainer1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SkylineBatchConfigForm";
            this.ShowInTaskbar = false;
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabsConfig.ResumeLayout(false);
            this.tabSettings.ResumeLayout(false);
            this.tabSettings.PerformLayout();
            this.tabReports.ResumeLayout(false);
            this.tabReports.PerformLayout();
            this.toolBar.ResumeLayout(false);
            this.toolBar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridReportSettings)).EndInit();
            this.tabSkyline.ResumeLayout(false);
            this.tabSkyline.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btnSaveConfig;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnCancelConfig;
        private System.Windows.Forms.Label lblConfigRunning;
        private System.Windows.Forms.TabControl tabsConfig;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.TextBox textConfigName;
        private System.Windows.Forms.Label labelConfigName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnSkylineFilePath;
        private System.Windows.Forms.TextBox textSkylinePath;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnAnalysisPath;
        private System.Windows.Forms.TextBox textAnalysisPath;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnDataPath;
        private System.Windows.Forms.TextBox textDataPath;
        private System.Windows.Forms.LinkLabel linkLabelRegex;
        private System.Windows.Forms.TabPage tabReports;
        private System.Windows.Forms.DataGridView gridReportSettings;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnPath;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnScripts;
        private System.Windows.Forms.ImageList imageListToolbarIcons;
        private System.Windows.Forms.ToolStrip toolBar;
        private System.Windows.Forms.ToolStripButton btnAddReport;
        private System.Windows.Forms.ToolStripButton btnDeleteReport;
        private System.Windows.Forms.ToolStripButton btnEditReport;
        private System.Windows.Forms.Button btnOkConfig;
        private System.Windows.Forms.TabPage tabSkyline;
        private System.Windows.Forms.RadioButton radioButtonSkylineDaily;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.RadioButton radioButtonSkyline;
        private System.Windows.Forms.RadioButton radioButtonSpecifySkylinePath;
        private System.Windows.Forms.TextBox textSkylineInstallationPath;
        private System.Windows.Forms.TextBox textNamingPattern;
    }
}