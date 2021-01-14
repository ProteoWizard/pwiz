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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.textAnalysisPath = new System.Windows.Forms.TextBox();
            this.textSkylinePath = new System.Windows.Forms.TextBox();
            this.textDataPath = new System.Windows.Forms.TextBox();
            this.linkLabelRegex = new System.Windows.Forms.LinkLabel();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabsConfig = new System.Windows.Forms.TabControl();
            this.tabFiles = new System.Windows.Forms.TabPage();
            this.textNamingPattern = new System.Windows.Forms.TextBox();
            this.textConfigName = new System.Windows.Forms.TextBox();
            this.labelConfigName = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.btnDataPath = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.btnAnalysisPath = new System.Windows.Forms.Button();
            this.btnSkylineFilePath = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.textResolvingPower = new System.Windows.Forms.TextBox();
            this.textRetentionTime = new System.Windows.Forms.TextBox();
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
            this.panelSkylineSettings = new System.Windows.Forms.Panel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnSaveConfig = new System.Windows.Forms.Button();
            this.btnCancelConfig = new System.Windows.Forms.Button();
            this.lblConfigRunning = new System.Windows.Forms.Label();
            this.btnOkConfig = new System.Windows.Forms.Button();
            this.imageListToolbarIcons = new System.Windows.Forms.ImageList(this.components);
            this.checkBoxDecoys = new System.Windows.Forms.CheckBox();
            this.radioReverseDecoys = new System.Windows.Forms.RadioButton();
            this.radioShuffleDecoys = new System.Windows.Forms.RadioButton();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabsConfig.SuspendLayout();
            this.tabFiles.SuspendLayout();
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
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            this.toolTip1.SetToolTip(this.label5, resources.GetString("label5.ToolTip"));
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            this.toolTip1.SetToolTip(this.label4, resources.GetString("label4.ToolTip"));
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
            this.tabsConfig.Controls.Add(this.tabFiles);
            this.tabsConfig.Controls.Add(this.tabSettings);
            this.tabsConfig.Controls.Add(this.tabReports);
            this.tabsConfig.Controls.Add(this.tabSkyline);
            resources.ApplyResources(this.tabsConfig, "tabsConfig");
            this.tabsConfig.Name = "tabsConfig";
            this.tabsConfig.SelectedIndex = 0;
            // 
            // tabFiles
            // 
            this.tabFiles.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabFiles.Controls.Add(this.textNamingPattern);
            this.tabFiles.Controls.Add(this.linkLabelRegex);
            this.tabFiles.Controls.Add(this.textConfigName);
            this.tabFiles.Controls.Add(this.labelConfigName);
            this.tabFiles.Controls.Add(this.label3);
            this.tabFiles.Controls.Add(this.btnDataPath);
            this.tabFiles.Controls.Add(this.textDataPath);
            this.tabFiles.Controls.Add(this.textAnalysisPath);
            this.tabFiles.Controls.Add(this.label1);
            this.tabFiles.Controls.Add(this.btnAnalysisPath);
            this.tabFiles.Controls.Add(this.btnSkylineFilePath);
            this.tabFiles.Controls.Add(this.label2);
            this.tabFiles.Controls.Add(this.textSkylinePath);
            resources.ApplyResources(this.tabFiles, "tabFiles");
            this.tabFiles.Name = "tabFiles";
            // 
            // textNamingPattern
            // 
            resources.ApplyResources(this.textNamingPattern, "textNamingPattern");
            this.textNamingPattern.Name = "textNamingPattern";
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
            // btnAnalysisPath
            // 
            resources.ApplyResources(this.btnAnalysisPath, "btnAnalysisPath");
            this.btnAnalysisPath.Name = "btnAnalysisPath";
            this.btnAnalysisPath.UseVisualStyleBackColor = true;
            this.btnAnalysisPath.Click += new System.EventHandler(this.btnAnalysisFilePath_Click);
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
            // tabSettings
            // 
            this.tabSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabSettings.Controls.Add(this.radioShuffleDecoys);
            this.tabSettings.Controls.Add(this.radioReverseDecoys);
            this.tabSettings.Controls.Add(this.checkBoxDecoys);
            this.tabSettings.Controls.Add(this.label7);
            this.tabSettings.Controls.Add(this.label6);
            this.tabSettings.Controls.Add(this.label5);
            this.tabSettings.Controls.Add(this.label4);
            this.tabSettings.Controls.Add(this.textResolvingPower);
            this.tabSettings.Controls.Add(this.textRetentionTime);
            resources.ApplyResources(this.tabSettings, "tabSettings");
            this.tabSettings.Name = "tabSettings";
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // textResolvingPower
            // 
            resources.ApplyResources(this.textResolvingPower, "textResolvingPower");
            this.textResolvingPower.Name = "textResolvingPower";
            // 
            // textRetentionTime
            // 
            resources.ApplyResources(this.textRetentionTime, "textRetentionTime");
            this.textRetentionTime.Name = "textRetentionTime";
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
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle7.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle7.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle7.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle7.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridReportSettings.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle7;
            this.gridReportSettings.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridReportSettings.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnName,
            this.columnPath,
            this.columnScripts});
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle8.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle8.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle8.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridReportSettings.DefaultCellStyle = dataGridViewCellStyle8;
            this.gridReportSettings.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.gridReportSettings.Name = "gridReportSettings";
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle9.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle9.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle9.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle9.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle9.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle9.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridReportSettings.RowHeadersDefaultCellStyle = dataGridViewCellStyle9;
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
            this.tabSkyline.Controls.Add(this.panelSkylineSettings);
            resources.ApplyResources(this.tabSkyline, "tabSkyline");
            this.tabSkyline.Name = "tabSkyline";
            // 
            // panelSkylineSettings
            // 
            resources.ApplyResources(this.panelSkylineSettings, "panelSkylineSettings");
            this.panelSkylineSettings.Name = "panelSkylineSettings";
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
            // btnSaveConfig
            // 
            resources.ApplyResources(this.btnSaveConfig, "btnSaveConfig");
            this.btnSaveConfig.Name = "btnSaveConfig";
            this.btnSaveConfig.UseVisualStyleBackColor = true;
            this.btnSaveConfig.Click += new System.EventHandler(this.btnSaveConfig_Click);
            // 
            // btnCancelConfig
            // 
            resources.ApplyResources(this.btnCancelConfig, "btnCancelConfig");
            this.btnCancelConfig.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancelConfig.Name = "btnCancelConfig";
            this.btnCancelConfig.UseVisualStyleBackColor = true;
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
            // checkBoxDecoys
            // 
            resources.ApplyResources(this.checkBoxDecoys, "checkBoxDecoys");
            this.checkBoxDecoys.Name = "checkBoxDecoys";
            this.checkBoxDecoys.UseVisualStyleBackColor = true;
            this.checkBoxDecoys.CheckedChanged += new System.EventHandler(this.checkBoxDecoys_CheckedChanged);
            // 
            // radioReverseDecoys
            // 
            resources.ApplyResources(this.radioReverseDecoys, "radioReverseDecoys");
            this.radioReverseDecoys.Checked = true;
            this.radioReverseDecoys.Name = "radioReverseDecoys";
            this.radioReverseDecoys.TabStop = true;
            this.radioReverseDecoys.UseVisualStyleBackColor = true;
            // 
            // radioShuffleDecoys
            // 
            resources.ApplyResources(this.radioShuffleDecoys, "radioShuffleDecoys");
            this.radioShuffleDecoys.Name = "radioShuffleDecoys";
            this.radioShuffleDecoys.UseVisualStyleBackColor = true;
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
            this.tabFiles.ResumeLayout(false);
            this.tabFiles.PerformLayout();
            this.tabSettings.ResumeLayout(false);
            this.tabSettings.PerformLayout();
            this.tabReports.ResumeLayout(false);
            this.tabReports.PerformLayout();
            this.toolBar.ResumeLayout(false);
            this.toolBar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridReportSettings)).EndInit();
            this.tabSkyline.ResumeLayout(false);
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
        private System.Windows.Forms.TabPage tabFiles;
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
        private System.Windows.Forms.TextBox textNamingPattern;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.TextBox textResolvingPower;
        private System.Windows.Forms.TextBox textRetentionTime;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Panel panelSkylineSettings;
        private System.Windows.Forms.RadioButton radioShuffleDecoys;
        private System.Windows.Forms.RadioButton radioReverseDecoys;
        private System.Windows.Forms.CheckBox checkBoxDecoys;
    }
}