using System.Globalization;

namespace AutoQC
{
    partial class AutoQcConfigForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AutoQcConfigForm));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.groupBoxMain = new System.Windows.Forms.GroupBox();
            this.btnAnnotationsFile = new System.Windows.Forms.Button();
            this.labelAnnotationsFile = new System.Windows.Forms.Label();
            this.textAnnotationsFilePath = new System.Windows.Forms.TextBox();
            this.textConfigName = new System.Windows.Forms.TextBox();
            this.labelConfigName = new System.Windows.Forms.Label();
            this.checkBoxRemoveResults = new System.Windows.Forms.CheckBox();
            this.labelQcFilePattern = new System.Windows.Forms.Label();
            this.comboBoxFileFilter = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.includeSubfoldersCb = new System.Windows.Forms.CheckBox();
            this.textQCFilePattern = new System.Windows.Forms.TextBox();
            this.labelMinutes = new System.Windows.Forms.Label();
            this.labelAquisitionTime = new System.Windows.Forms.Label();
            this.textAquisitionTime = new System.Windows.Forms.TextBox();
            this.labelDays = new System.Windows.Forms.Label();
            this.textResultsTimeWindow = new System.Windows.Forms.TextBox();
            this.labelAccumulationTimeWindow = new System.Windows.Forms.Label();
            this.labelInstrumentType = new System.Windows.Forms.Label();
            this.comboBoxInstrumentType = new System.Windows.Forms.ComboBox();
            this.btnFolderToWatch = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textFolderToWatchPath = new System.Windows.Forms.TextBox();
            this.btnSkylineFilePath = new System.Windows.Forms.Button();
            this.textSkylinePath = new System.Windows.Forms.TextBox();
            this.tabPanoramaSettings = new System.Windows.Forms.TabPage();
            this.cbPublishToPanorama = new System.Windows.Forms.CheckBox();
            this.groupBoxPanorama = new System.Windows.Forms.GroupBox();
            this.labelPanoramaFolder = new System.Windows.Forms.Label();
            this.textPanoramaFolder = new System.Windows.Forms.TextBox();
            this.lblPanoramaUrl = new System.Windows.Forms.Label();
            this.textPanoramaUrl = new System.Windows.Forms.TextBox();
            this.textPanoramaPasswd = new System.Windows.Forms.TextBox();
            this.lblPanoramaPasswd = new System.Windows.Forms.Label();
            this.lblPanoramaEmail = new System.Windows.Forms.Label();
            this.textPanoramaEmail = new System.Windows.Forms.TextBox();
            this.tabSkylineSettings = new System.Windows.Forms.TabPage();
            this.panelSkylineSettings = new System.Windows.Forms.Panel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.lblConfigRunning = new System.Windows.Forms.Label();
            this.btnCancelConfig = new System.Windows.Forms.Button();
            this.btnSaveConfig = new System.Windows.Forms.Button();
            this.btnOkConfig = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.groupBoxMain.SuspendLayout();
            this.tabPanoramaSettings.SuspendLayout();
            this.groupBoxPanorama.SuspendLayout();
            this.tabSkylineSettings.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabControl);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.groupBox1);
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabSettings);
            this.tabControl.Controls.Add(this.tabPanoramaSettings);
            this.tabControl.Controls.Add(this.tabSkylineSettings);
            resources.ApplyResources(this.tabControl, "tabControl");
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            // 
            // tabSettings
            // 
            this.tabSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabSettings.Controls.Add(this.groupBoxMain);
            resources.ApplyResources(this.tabSettings, "tabSettings");
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.Enter += new System.EventHandler(this.TabEnter);
            // 
            // groupBoxMain
            // 
            resources.ApplyResources(this.groupBoxMain, "groupBoxMain");
            this.groupBoxMain.Controls.Add(this.btnAnnotationsFile);
            this.groupBoxMain.Controls.Add(this.labelAnnotationsFile);
            this.groupBoxMain.Controls.Add(this.textAnnotationsFilePath);
            this.groupBoxMain.Controls.Add(this.textConfigName);
            this.groupBoxMain.Controls.Add(this.labelConfigName);
            this.groupBoxMain.Controls.Add(this.checkBoxRemoveResults);
            this.groupBoxMain.Controls.Add(this.labelQcFilePattern);
            this.groupBoxMain.Controls.Add(this.comboBoxFileFilter);
            this.groupBoxMain.Controls.Add(this.label5);
            this.groupBoxMain.Controls.Add(this.includeSubfoldersCb);
            this.groupBoxMain.Controls.Add(this.textQCFilePattern);
            this.groupBoxMain.Controls.Add(this.labelMinutes);
            this.groupBoxMain.Controls.Add(this.labelAquisitionTime);
            this.groupBoxMain.Controls.Add(this.textAquisitionTime);
            this.groupBoxMain.Controls.Add(this.labelDays);
            this.groupBoxMain.Controls.Add(this.textResultsTimeWindow);
            this.groupBoxMain.Controls.Add(this.labelAccumulationTimeWindow);
            this.groupBoxMain.Controls.Add(this.labelInstrumentType);
            this.groupBoxMain.Controls.Add(this.comboBoxInstrumentType);
            this.groupBoxMain.Controls.Add(this.btnFolderToWatch);
            this.groupBoxMain.Controls.Add(this.label2);
            this.groupBoxMain.Controls.Add(this.label3);
            this.groupBoxMain.Controls.Add(this.textFolderToWatchPath);
            this.groupBoxMain.Controls.Add(this.btnSkylineFilePath);
            this.groupBoxMain.Controls.Add(this.textSkylinePath);
            this.groupBoxMain.Name = "groupBoxMain";
            this.groupBoxMain.TabStop = false;
            // 
            // btnAnnotationsFile
            // 
            resources.ApplyResources(this.btnAnnotationsFile, "btnAnnotationsFile");
            this.btnAnnotationsFile.AutoEllipsis = true;
            this.btnAnnotationsFile.Name = "btnAnnotationsFile";
            this.btnAnnotationsFile.UseVisualStyleBackColor = true;
            this.btnAnnotationsFile.Click += new System.EventHandler(this.btnAnnotationsFilePath_Click);
            // 
            // labelAnnotationsFile
            // 
            resources.ApplyResources(this.labelAnnotationsFile, "labelAnnotationsFile");
            this.labelAnnotationsFile.Name = "labelAnnotationsFile";
            // 
            // textAnnotationsFilePath
            // 
            resources.ApplyResources(this.textAnnotationsFilePath, "textAnnotationsFilePath");
            this.textAnnotationsFilePath.Name = "textAnnotationsFilePath";
            this.toolTip1.SetToolTip(this.textAnnotationsFilePath, resources.GetString("textAnnotationsFilePath.ToolTip"));
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
            // checkBoxRemoveResults
            // 
            resources.ApplyResources(this.checkBoxRemoveResults, "checkBoxRemoveResults");
            this.checkBoxRemoveResults.Checked = true;
            this.checkBoxRemoveResults.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxRemoveResults.Name = "checkBoxRemoveResults";
            this.toolTip1.SetToolTip(this.checkBoxRemoveResults, resources.GetString("checkBoxRemoveResults.ToolTip"));
            this.checkBoxRemoveResults.UseVisualStyleBackColor = true;
            this.checkBoxRemoveResults.CheckedChanged += new System.EventHandler(this.checkBoxRemoveResults_CheckedChanged);
            // 
            // labelQcFilePattern
            // 
            resources.ApplyResources(this.labelQcFilePattern, "labelQcFilePattern");
            this.labelQcFilePattern.Name = "labelQcFilePattern";
            // 
            // comboBoxFileFilter
            // 
            this.comboBoxFileFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFileFilter.FormattingEnabled = true;
            resources.ApplyResources(this.comboBoxFileFilter, "comboBoxFileFilter");
            this.comboBoxFileFilter.Name = "comboBoxFileFilter";
            this.comboBoxFileFilter.SelectedIndexChanged += new System.EventHandler(this.comboBoxFileFilter_SelectedIndexChanged);
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // includeSubfoldersCb
            // 
            resources.ApplyResources(this.includeSubfoldersCb, "includeSubfoldersCb");
            this.includeSubfoldersCb.Name = "includeSubfoldersCb";
            this.includeSubfoldersCb.UseVisualStyleBackColor = true;
            // 
            // textQCFilePattern
            // 
            resources.ApplyResources(this.textQCFilePattern, "textQCFilePattern");
            this.textQCFilePattern.Name = "textQCFilePattern";
            this.toolTip1.SetToolTip(this.textQCFilePattern, resources.GetString("textQCFilePattern.ToolTip"));
            // 
            // labelMinutes
            // 
            resources.ApplyResources(this.labelMinutes, "labelMinutes");
            this.labelMinutes.Name = "labelMinutes";
            // 
            // labelAquisitionTime
            // 
            resources.ApplyResources(this.labelAquisitionTime, "labelAquisitionTime");
            this.labelAquisitionTime.Name = "labelAquisitionTime";
            this.toolTip1.SetToolTip(this.labelAquisitionTime, resources.GetString("labelAquisitionTime.ToolTip"));
            // 
            // textAquisitionTime
            // 
            resources.ApplyResources(this.textAquisitionTime, "textAquisitionTime");
            this.textAquisitionTime.Name = "textAquisitionTime";
            this.toolTip1.SetToolTip(this.textAquisitionTime, resources.GetString("textAquisitionTime.ToolTip"));
            // 
            // labelDays
            // 
            resources.ApplyResources(this.labelDays, "labelDays");
            this.labelDays.Name = "labelDays";
            // 
            // textResultsTimeWindow
            // 
            resources.ApplyResources(this.textResultsTimeWindow, "textResultsTimeWindow");
            this.textResultsTimeWindow.Name = "textResultsTimeWindow";
            this.toolTip1.SetToolTip(this.textResultsTimeWindow, resources.GetString("textResultsTimeWindow.ToolTip"));
            // 
            // labelAccumulationTimeWindow
            // 
            resources.ApplyResources(this.labelAccumulationTimeWindow, "labelAccumulationTimeWindow");
            this.labelAccumulationTimeWindow.Name = "labelAccumulationTimeWindow";
            // 
            // labelInstrumentType
            // 
            resources.ApplyResources(this.labelInstrumentType, "labelInstrumentType");
            this.labelInstrumentType.Name = "labelInstrumentType";
            // 
            // comboBoxInstrumentType
            // 
            this.comboBoxInstrumentType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxInstrumentType.FormattingEnabled = true;
            this.comboBoxInstrumentType.Items.AddRange(new object[] {
            resources.GetString("comboBoxInstrumentType.Items"),
            resources.GetString("comboBoxInstrumentType.Items1"),
            resources.GetString("comboBoxInstrumentType.Items2"),
            resources.GetString("comboBoxInstrumentType.Items3"),
            resources.GetString("comboBoxInstrumentType.Items4"),
            resources.GetString("comboBoxInstrumentType.Items5"),
            resources.GetString("comboBoxInstrumentType.Items6")});
            resources.ApplyResources(this.comboBoxInstrumentType, "comboBoxInstrumentType");
            this.comboBoxInstrumentType.Name = "comboBoxInstrumentType";
            // 
            // btnFolderToWatch
            // 
            resources.ApplyResources(this.btnFolderToWatch, "btnFolderToWatch");
            this.btnFolderToWatch.Name = "btnFolderToWatch";
            this.btnFolderToWatch.UseVisualStyleBackColor = true;
            this.btnFolderToWatch.Click += new System.EventHandler(this.btnFolderToWatch_Click);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textFolderToWatchPath
            // 
            resources.ApplyResources(this.textFolderToWatchPath, "textFolderToWatchPath");
            this.textFolderToWatchPath.Name = "textFolderToWatchPath";
            this.toolTip1.SetToolTip(this.textFolderToWatchPath, resources.GetString("textFolderToWatchPath.ToolTip"));
            // 
            // btnSkylineFilePath
            // 
            resources.ApplyResources(this.btnSkylineFilePath, "btnSkylineFilePath");
            this.btnSkylineFilePath.Name = "btnSkylineFilePath";
            this.btnSkylineFilePath.UseVisualStyleBackColor = true;
            this.btnSkylineFilePath.Click += new System.EventHandler(this.btnSkylineFilePath_Click);
            // 
            // textSkylinePath
            // 
            resources.ApplyResources(this.textSkylinePath, "textSkylinePath");
            this.textSkylinePath.Name = "textSkylinePath";
            this.toolTip1.SetToolTip(this.textSkylinePath, resources.GetString("textSkylinePath.ToolTip"));
            // 
            // tabPanoramaSettings
            // 
            this.tabPanoramaSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabPanoramaSettings.Controls.Add(this.cbPublishToPanorama);
            this.tabPanoramaSettings.Controls.Add(this.groupBoxPanorama);
            resources.ApplyResources(this.tabPanoramaSettings, "tabPanoramaSettings");
            this.tabPanoramaSettings.Name = "tabPanoramaSettings";
            this.tabPanoramaSettings.Enter += new System.EventHandler(this.TabEnter);
            // 
            // cbPublishToPanorama
            // 
            resources.ApplyResources(this.cbPublishToPanorama, "cbPublishToPanorama");
            this.cbPublishToPanorama.Name = "cbPublishToPanorama";
            this.cbPublishToPanorama.UseVisualStyleBackColor = true;
            this.cbPublishToPanorama.CheckedChanged += new System.EventHandler(this.cbPublishToPanorama_CheckedChanged);
            // 
            // groupBoxPanorama
            // 
            resources.ApplyResources(this.groupBoxPanorama, "groupBoxPanorama");
            this.groupBoxPanorama.Controls.Add(this.labelPanoramaFolder);
            this.groupBoxPanorama.Controls.Add(this.textPanoramaFolder);
            this.groupBoxPanorama.Controls.Add(this.lblPanoramaUrl);
            this.groupBoxPanorama.Controls.Add(this.textPanoramaUrl);
            this.groupBoxPanorama.Controls.Add(this.textPanoramaPasswd);
            this.groupBoxPanorama.Controls.Add(this.lblPanoramaPasswd);
            this.groupBoxPanorama.Controls.Add(this.lblPanoramaEmail);
            this.groupBoxPanorama.Controls.Add(this.textPanoramaEmail);
            this.groupBoxPanorama.Name = "groupBoxPanorama";
            this.groupBoxPanorama.TabStop = false;
            // 
            // labelPanoramaFolder
            // 
            resources.ApplyResources(this.labelPanoramaFolder, "labelPanoramaFolder");
            this.labelPanoramaFolder.Name = "labelPanoramaFolder";
            // 
            // textPanoramaFolder
            // 
            resources.ApplyResources(this.textPanoramaFolder, "textPanoramaFolder");
            this.textPanoramaFolder.Name = "textPanoramaFolder";
            // 
            // lblPanoramaUrl
            // 
            resources.ApplyResources(this.lblPanoramaUrl, "lblPanoramaUrl");
            this.lblPanoramaUrl.Name = "lblPanoramaUrl";
            // 
            // textPanoramaUrl
            // 
            resources.ApplyResources(this.textPanoramaUrl, "textPanoramaUrl");
            this.textPanoramaUrl.Name = "textPanoramaUrl";
            // 
            // textPanoramaPasswd
            // 
            resources.ApplyResources(this.textPanoramaPasswd, "textPanoramaPasswd");
            this.textPanoramaPasswd.Name = "textPanoramaPasswd";
            this.textPanoramaPasswd.UseSystemPasswordChar = true;
            // 
            // lblPanoramaPasswd
            // 
            resources.ApplyResources(this.lblPanoramaPasswd, "lblPanoramaPasswd");
            this.lblPanoramaPasswd.Name = "lblPanoramaPasswd";
            // 
            // lblPanoramaEmail
            // 
            resources.ApplyResources(this.lblPanoramaEmail, "lblPanoramaEmail");
            this.lblPanoramaEmail.Name = "lblPanoramaEmail";
            // 
            // textPanoramaEmail
            // 
            resources.ApplyResources(this.textPanoramaEmail, "textPanoramaEmail");
            this.textPanoramaEmail.Name = "textPanoramaEmail";
            // 
            // tabSkylineSettings
            // 
            this.tabSkylineSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabSkylineSettings.Controls.Add(this.panelSkylineSettings);
            resources.ApplyResources(this.tabSkylineSettings, "tabSkylineSettings");
            this.tabSkylineSettings.Name = "tabSkylineSettings";
            this.tabSkylineSettings.Enter += new System.EventHandler(this.TabEnter);
            // 
            // panelSkylineSettings
            // 
            resources.ApplyResources(this.panelSkylineSettings, "panelSkylineSettings");
            this.panelSkylineSettings.Name = "panelSkylineSettings";
            // 
            // groupBox1
            // 
            this.groupBox1.BackColor = System.Drawing.SystemColors.ControlLight;
            this.groupBox1.Controls.Add(this.lblConfigRunning);
            this.groupBox1.Controls.Add(this.btnCancelConfig);
            this.groupBox1.Controls.Add(this.btnSaveConfig);
            this.groupBox1.Controls.Add(this.btnOkConfig);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // lblConfigRunning
            // 
            resources.ApplyResources(this.lblConfigRunning, "lblConfigRunning");
            this.lblConfigRunning.ForeColor = System.Drawing.Color.DarkRed;
            this.lblConfigRunning.Name = "lblConfigRunning";
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
            // btnOkConfig
            // 
            resources.ApplyResources(this.btnOkConfig, "btnOkConfig");
            this.btnOkConfig.Name = "btnOkConfig";
            this.btnOkConfig.UseVisualStyleBackColor = true;
            this.btnOkConfig.Click += new System.EventHandler(this.btnOkConfig_Click);
            // 
            // AutoQcConfigForm
            // 
            this.AcceptButton = this.btnSaveConfig;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancelConfig;
            this.Controls.Add(this.splitContainer1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AutoQcConfigForm";
            this.ShowInTaskbar = false;
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.tabSettings.ResumeLayout(false);
            this.groupBoxMain.ResumeLayout(false);
            this.groupBoxMain.PerformLayout();
            this.tabPanoramaSettings.ResumeLayout(false);
            this.tabPanoramaSettings.PerformLayout();
            this.groupBoxPanorama.ResumeLayout(false);
            this.groupBoxPanorama.PerformLayout();
            this.tabSkylineSettings.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btnSaveConfig;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.GroupBox groupBoxMain;
        private System.Windows.Forms.TextBox textQCFilePattern;
        private System.Windows.Forms.Label labelMinutes;
        private System.Windows.Forms.Label labelAquisitionTime;
        private System.Windows.Forms.Label labelDays;
        private System.Windows.Forms.TextBox textResultsTimeWindow;
        private System.Windows.Forms.Label labelAccumulationTimeWindow;
        private System.Windows.Forms.Label labelInstrumentType;
        private System.Windows.Forms.ComboBox comboBoxInstrumentType;
        private System.Windows.Forms.Button btnFolderToWatch;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textFolderToWatchPath;
        private System.Windows.Forms.Button btnSkylineFilePath;
        private System.Windows.Forms.TextBox textSkylinePath;
        private System.Windows.Forms.TabPage tabPanoramaSettings;
        private System.Windows.Forms.CheckBox cbPublishToPanorama;
        private System.Windows.Forms.GroupBox groupBoxPanorama;
        private System.Windows.Forms.Label labelPanoramaFolder;
        private System.Windows.Forms.TextBox textPanoramaFolder;
        private System.Windows.Forms.Label lblPanoramaUrl;
        private System.Windows.Forms.TextBox textPanoramaUrl;
        private System.Windows.Forms.TextBox textPanoramaPasswd;
        private System.Windows.Forms.Label lblPanoramaPasswd;
        private System.Windows.Forms.Label lblPanoramaEmail;
        private System.Windows.Forms.TextBox textPanoramaEmail;
        private System.Windows.Forms.TextBox textConfigName;
        private System.Windows.Forms.Label labelConfigName;
        private System.Windows.Forms.Button btnCancelConfig;
        private System.Windows.Forms.Label lblConfigRunning;
        private System.Windows.Forms.CheckBox includeSubfoldersCb;
        private System.Windows.Forms.ComboBox comboBoxFileFilter;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label labelQcFilePattern;
        private System.Windows.Forms.Button btnOkConfig;
        private System.Windows.Forms.CheckBox checkBoxRemoveResults;
        private System.Windows.Forms.TextBox textAquisitionTime;
        private System.Windows.Forms.TabPage tabSkylineSettings;
        private System.Windows.Forms.Panel panelSkylineSettings;
        private System.Windows.Forms.Button btnAnnotationsFile;
        private System.Windows.Forms.Label labelAnnotationsFile;
        private System.Windows.Forms.TextBox textAnnotationsFilePath;
    }
}