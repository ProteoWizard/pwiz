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
            this.tabControl = new System.Windows.Forms.TabControl();
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
            this.btnAddReport = new System.Windows.Forms.Button();
            this.gridReportSettings = new System.Windows.Forms.DataGridView();
            this.columnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnPath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnScripts = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnOkConfig = new System.Windows.Forms.Button();
            this.lblConfigRunning = new System.Windows.Forms.Label();
            this.btnCancelConfig = new System.Windows.Forms.Button();
            this.btnSaveConfig = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.groupBoxMain.SuspendLayout();
            this.tabReports.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridReportSettings)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // textAnalysisPath
            // 
            this.textAnalysisPath.Location = new System.Drawing.Point(21, 106);
            this.textAnalysisPath.Name = "textAnalysisPath";
            this.textAnalysisPath.Size = new System.Drawing.Size(430, 20);
            this.textAnalysisPath.TabIndex = 6;
            this.toolTip1.SetToolTip(this.textAnalysisPath, "Path to a Skyline docuement where results will be imported");
            // 
            // textSkylinePath
            // 
            this.textSkylinePath.Location = new System.Drawing.Point(21, 46);
            this.textSkylinePath.Name = "textSkylinePath";
            this.textSkylinePath.Size = new System.Drawing.Size(430, 20);
            this.textSkylinePath.TabIndex = 3;
            this.toolTip1.SetToolTip(this.textSkylinePath, "Path to a Skyline docuement where results will be imported");
            // 
            // textDataPath
            // 
            this.textDataPath.Location = new System.Drawing.Point(21, 171);
            this.textDataPath.Name = "textDataPath";
            this.textDataPath.Size = new System.Drawing.Size(430, 20);
            this.textDataPath.TabIndex = 9;
            this.toolTip1.SetToolTip(this.textDataPath, "Path to a Skyline docuement where results will be imported");
            // 
            // textNamingPattern
            // 
            this.textNamingPattern.Location = new System.Drawing.Point(21, 234);
            this.textNamingPattern.Name = "textNamingPattern";
            this.textNamingPattern.Size = new System.Drawing.Size(230, 20);
            this.textNamingPattern.TabIndex = 12;
            this.toolTip1.SetToolTip(this.textNamingPattern, "Path to a Skyline docuement where results will be imported");
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.LinkArea = new System.Windows.Forms.LinkArea(26, 18);
            this.linkLabel1.Location = new System.Drawing.Point(21, 214);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(237, 17);
            this.linkLabel1.TabIndex = 11;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "&Replicate naming pattern (regular expression):";
            this.toolTip1.SetToolTip(this.linkLabel1, "A regular expression from which the first group will be used to name replicates i" +
        "n an ‑‑import‑all operation (e.g. [^_]_(.*) for everything after the first under" +
        "score).");
            this.linkLabel1.UseCompatibleTextRendering = true;
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabControl);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.groupBox1);
            this.splitContainer1.Size = new System.Drawing.Size(544, 555);
            this.splitContainer1.SplitterDistance = 465;
            this.splitContainer1.TabIndex = 0;
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabSettings);
            this.tabControl.Controls.Add(this.tabReports);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.Padding = new System.Drawing.Point(20, 6);
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(544, 465);
            this.tabControl.TabIndex = 0;
            // 
            // tabSettings
            // 
            this.tabSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabSettings.Controls.Add(this.textConfigName);
            this.tabSettings.Controls.Add(this.labelConfigName);
            this.tabSettings.Controls.Add(this.groupBoxMain);
            this.tabSettings.Location = new System.Drawing.Point(4, 28);
            this.tabSettings.Margin = new System.Windows.Forms.Padding(4);
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.Padding = new System.Windows.Forms.Padding(4);
            this.tabSettings.Size = new System.Drawing.Size(536, 433);
            this.tabSettings.TabIndex = 1;
            this.tabSettings.Text = "Settings";
            // 
            // textConfigName
            // 
            this.textConfigName.Location = new System.Drawing.Point(30, 31);
            this.textConfigName.Name = "textConfigName";
            this.textConfigName.Size = new System.Drawing.Size(430, 20);
            this.textConfigName.TabIndex = 1;
            // 
            // labelConfigName
            // 
            this.labelConfigName.AutoSize = true;
            this.labelConfigName.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelConfigName.Location = new System.Drawing.Point(29, 15);
            this.labelConfigName.Name = "labelConfigName";
            this.labelConfigName.Size = new System.Drawing.Size(116, 13);
            this.labelConfigName.TabIndex = 0;
            this.labelConfigName.Text = "&Configuration name";
            // 
            // groupBoxMain
            // 
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
            this.groupBoxMain.Location = new System.Drawing.Point(9, 57);
            this.groupBoxMain.Name = "groupBoxMain";
            this.groupBoxMain.Size = new System.Drawing.Size(519, 369);
            this.groupBoxMain.TabIndex = 53;
            this.groupBoxMain.TabStop = false;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(20, 152);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(85, 15);
            this.label3.TabIndex = 8;
            this.label3.Text = "&Data directory:";
            // 
            // btnDataPath
            // 
            this.btnDataPath.Location = new System.Drawing.Point(470, 170);
            this.btnDataPath.Name = "btnDataPath";
            this.btnDataPath.Size = new System.Drawing.Size(29, 23);
            this.btnDataPath.TabIndex = 10;
            this.btnDataPath.Text = "...";
            this.btnDataPath.UseVisualStyleBackColor = true;
            this.btnDataPath.Click += new System.EventHandler(this.btnDataPath_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(20, 27);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(146, 15);
            this.label1.TabIndex = 2;
            this.label1.Text = "Skyline &template file path:";
            // 
            // btnSkylineFilePath
            // 
            this.btnSkylineFilePath.Location = new System.Drawing.Point(470, 45);
            this.btnSkylineFilePath.Name = "btnSkylineFilePath";
            this.btnSkylineFilePath.Size = new System.Drawing.Size(29, 23);
            this.btnSkylineFilePath.TabIndex = 4;
            this.btnSkylineFilePath.Text = "...";
            this.btnSkylineFilePath.UseVisualStyleBackColor = true;
            this.btnSkylineFilePath.Click += new System.EventHandler(this.btnSkylineFilePath_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(20, 87);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(115, 15);
            this.label2.TabIndex = 5;
            this.label2.Text = "&Analysis folder path:";
            // 
            // btnAnalysisPath
            // 
            this.btnAnalysisPath.Location = new System.Drawing.Point(470, 105);
            this.btnAnalysisPath.Name = "btnAnalysisPath";
            this.btnAnalysisPath.Size = new System.Drawing.Size(29, 23);
            this.btnAnalysisPath.TabIndex = 7;
            this.btnAnalysisPath.Text = "...";
            this.btnAnalysisPath.UseVisualStyleBackColor = true;
            this.btnAnalysisPath.Click += new System.EventHandler(this.btnAnalysisFilePath_Click);
            // 
            // tabReports
            // 
            this.tabReports.Controls.Add(this.btnAddReport);
            this.tabReports.Controls.Add(this.gridReportSettings);
            this.tabReports.Location = new System.Drawing.Point(4, 28);
            this.tabReports.Name = "tabReports";
            this.tabReports.Padding = new System.Windows.Forms.Padding(3);
            this.tabReports.Size = new System.Drawing.Size(536, 433);
            this.tabReports.TabIndex = 2;
            this.tabReports.Text = "Reports";
            this.tabReports.UseVisualStyleBackColor = true;
            // 
            // btnAddReport
            // 
            this.btnAddReport.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAddReport.Location = new System.Drawing.Point(223, 401);
            this.btnAddReport.Name = "btnAddReport";
            this.btnAddReport.Size = new System.Drawing.Size(93, 26);
            this.btnAddReport.TabIndex = 1;
            this.btnAddReport.Text = "&Add Report";
            this.btnAddReport.UseVisualStyleBackColor = true;
            this.btnAddReport.Click += new System.EventHandler(this.btnAddReport_Click);
            // 
            // gridReportSettings
            // 
            this.gridReportSettings.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridReportSettings.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCells;
            this.gridReportSettings.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridReportSettings.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnName,
            this.columnPath,
            this.columnScripts});
            this.gridReportSettings.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.gridReportSettings.Location = new System.Drawing.Point(3, 6);
            this.gridReportSettings.Name = "gridReportSettings";
            this.gridReportSettings.Size = new System.Drawing.Size(530, 375);
            this.gridReportSettings.TabIndex = 0;
            this.gridReportSettings.TabStop = false;
            // 
            // columnName
            // 
            this.columnName.HeaderText = "Name";
            this.columnName.Name = "columnName";
            // 
            // columnPath
            // 
            this.columnPath.HeaderText = "Path";
            this.columnPath.Name = "columnPath";
            // 
            // columnScripts
            // 
            this.columnScripts.HeaderText = "Scripts";
            this.columnScripts.Name = "columnScripts";
            // 
            // groupBox1
            // 
            this.groupBox1.BackColor = System.Drawing.SystemColors.ControlLight;
            this.groupBox1.Controls.Add(this.btnOkConfig);
            this.groupBox1.Controls.Add(this.lblConfigRunning);
            this.groupBox1.Controls.Add(this.btnCancelConfig);
            this.groupBox1.Controls.Add(this.btnSaveConfig);
            this.groupBox1.Location = new System.Drawing.Point(4, -3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(537, 85);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            // 
            // btnOkConfig
            // 
            this.btnOkConfig.Location = new System.Drawing.Point(223, 28);
            this.btnOkConfig.Name = "btnOkConfig";
            this.btnOkConfig.Size = new System.Drawing.Size(75, 23);
            this.btnOkConfig.TabIndex = 4;
            this.btnOkConfig.Text = "&OK";
            this.btnOkConfig.UseVisualStyleBackColor = true;
            this.btnOkConfig.Click += new System.EventHandler(this.btnOkConfig_Click);
            // 
            // lblConfigRunning
            // 
            this.lblConfigRunning.AutoSize = true;
            this.lblConfigRunning.ForeColor = System.Drawing.Color.DarkRed;
            this.lblConfigRunning.Location = new System.Drawing.Point(130, 62);
            this.lblConfigRunning.Name = "lblConfigRunning";
            this.lblConfigRunning.Size = new System.Drawing.Size(243, 13);
            this.lblConfigRunning.TabIndex = 58;
            this.lblConfigRunning.Text = "This configuration is running and cannot be edited\r\n";
            // 
            // btnCancelConfig
            // 
            this.btnCancelConfig.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancelConfig.Location = new System.Drawing.Point(275, 26);
            this.btnCancelConfig.Name = "btnCancelConfig";
            this.btnCancelConfig.Size = new System.Drawing.Size(75, 26);
            this.btnCancelConfig.TabIndex = 3;
            this.btnCancelConfig.Text = "&Cancel";
            this.btnCancelConfig.UseVisualStyleBackColor = true;
            // 
            // btnSaveConfig
            // 
            this.btnSaveConfig.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSaveConfig.Location = new System.Drawing.Point(181, 26);
            this.btnSaveConfig.Name = "btnSaveConfig";
            this.btnSaveConfig.Size = new System.Drawing.Size(79, 26);
            this.btnSaveConfig.TabIndex = 2;
            this.btnSaveConfig.Text = "&Save";
            this.btnSaveConfig.UseVisualStyleBackColor = true;
            this.btnSaveConfig.Click += new System.EventHandler(this.btnSaveConfig_Click);
            // 
            // SkylineBatchConfigForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancelConfig;
            this.ClientSize = new System.Drawing.Size(544, 555);
            this.Controls.Add(this.splitContainer1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SkylineBatchConfigForm";
            this.Text = "SkylineBatch Configuration";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.tabSettings.ResumeLayout(false);
            this.tabSettings.PerformLayout();
            this.groupBoxMain.ResumeLayout(false);
            this.groupBoxMain.PerformLayout();
            this.tabReports.ResumeLayout(false);
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
        private System.Windows.Forms.Button btnOkConfig;
        private System.Windows.Forms.TabControl tabControl;
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
        private System.Windows.Forms.Button btnAddReport;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnPath;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnScripts;
    }
}