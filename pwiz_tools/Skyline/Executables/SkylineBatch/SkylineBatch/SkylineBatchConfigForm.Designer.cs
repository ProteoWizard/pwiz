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
            this.textNamingPattern = new System.Windows.Forms.TextBox();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabsConfig = new System.Windows.Forms.TabControl();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.textConfigName = new System.Windows.Forms.TextBox();
            this.labelConfigName = new System.Windows.Forms.Label();
            this.groupBoxMain = new System.Windows.Forms.GroupBox();
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnCancelConfig = new System.Windows.Forms.Button();
            this.btnSaveConfig = new System.Windows.Forms.Button();
            this.lblConfigRunning = new System.Windows.Forms.Label();
            this.imageListToolbarIcons = new System.Windows.Forms.ImageList(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabsConfig.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.groupBoxMain.SuspendLayout();
            this.tabReports.SuspendLayout();
            this.toolBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridReportSettings)).BeginInit();
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
            // textNamingPattern
            // 
            resources.ApplyResources(this.textNamingPattern, "textNamingPattern");
            this.textNamingPattern.Name = "textNamingPattern";
            this.toolTip1.SetToolTip(this.textNamingPattern, resources.GetString("textNamingPattern.ToolTip"));
            // 
            // linkLabel1
            // 
            resources.ApplyResources(this.linkLabel1, "linkLabel1");
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.TabStop = true;
            this.toolTip1.SetToolTip(this.linkLabel1, resources.GetString("linkLabel1.ToolTip"));
            this.linkLabel1.UseCompatibleTextRendering = true;
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
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
            resources.ApplyResources(this.tabsConfig, "tabsConfig");
            this.tabsConfig.Name = "tabsConfig";
            this.tabsConfig.SelectedIndex = 0;
            // 
            // tabSettings
            // 
            this.tabSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabSettings.Controls.Add(this.textConfigName);
            this.tabSettings.Controls.Add(this.labelConfigName);
            this.tabSettings.Controls.Add(this.groupBoxMain);
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
            // groupBoxMain
            // 
            resources.ApplyResources(this.groupBoxMain, "groupBoxMain");
            this.groupBoxMain.Controls.Add(this.linkLabel1);
            this.groupBoxMain.Controls.Add(this.textNamingPattern);
            this.groupBoxMain.Controls.Add(this.label3);
            this.groupBoxMain.Controls.Add(this.btnDataPath);
            this.groupBoxMain.Controls.Add(this.textDataPath);
            this.groupBoxMain.Controls.Add(this.label1);
            this.groupBoxMain.Controls.Add(this.btnSkylineFilePath);
            this.groupBoxMain.Controls.Add(this.textSkylinePath);
            this.groupBoxMain.Controls.Add(this.label2);
            this.groupBoxMain.Controls.Add(this.btnAnalysisPath);
            this.groupBoxMain.Controls.Add(this.textAnalysisPath);
            this.groupBoxMain.Name = "groupBoxMain";
            this.groupBoxMain.TabStop = false;
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
            this.tabReports.Controls.Add(this.toolBar);
            this.tabReports.Controls.Add(this.gridReportSettings);
            resources.ApplyResources(this.tabReports, "tabReports");
            this.tabReports.Name = "tabReports";
            this.tabReports.UseVisualStyleBackColor = true;
            // 
            // toolBar
            // 
            this.toolBar.AllowMerge = false;
            resources.ApplyResources(this.toolBar, "toolBar");
            this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBar.ImageScalingSize = new System.Drawing.Size(20, 20);
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
            // groupBox1
            // 
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.BackColor = System.Drawing.SystemColors.ControlLight;
            this.groupBox1.Controls.Add(this.btnCancelConfig);
            this.groupBox1.Controls.Add(this.btnSaveConfig);
            this.groupBox1.Controls.Add(this.lblConfigRunning);
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
            // imageListToolbarIcons
            // 
            this.imageListToolbarIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListToolbarIcons.ImageStream")));
            this.imageListToolbarIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListToolbarIcons.Images.SetKeyName(0, "AddedIcon.ico");
            this.imageListToolbarIcons.Images.SetKeyName(1, "DeletedIcon.ico");
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
            this.groupBoxMain.ResumeLayout(false);
            this.groupBoxMain.PerformLayout();
            this.tabReports.ResumeLayout(false);
            this.tabReports.PerformLayout();
            this.toolBar.ResumeLayout(false);
            this.toolBar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridReportSettings)).EndInit();
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
        private System.Windows.Forms.GroupBox groupBoxMain;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnSkylineFilePath;
        private System.Windows.Forms.TextBox textSkylinePath;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnAnalysisPath;
        private System.Windows.Forms.TextBox textAnalysisPath;
        private System.Windows.Forms.TextBox textNamingPattern;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnDataPath;
        private System.Windows.Forms.TextBox textDataPath;
        private System.Windows.Forms.LinkLabel linkLabel1;
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
    }
}