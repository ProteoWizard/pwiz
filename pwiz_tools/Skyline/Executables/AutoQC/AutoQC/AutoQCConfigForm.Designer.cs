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
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.textSkylinePath = new System.Windows.Forms.TextBox();
            this.textFolderToWatchPath = new System.Windows.Forms.TextBox();
            this.textResultsTimeWindow = new System.Windows.Forms.TextBox();
            this.textAquisitionTime = new System.Windows.Forms.TextBox();
            this.textQCFilePattern = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.textConfigName = new System.Windows.Forms.TextBox();
            this.labelConfigName = new System.Windows.Forms.Label();
            this.groupBoxMain = new System.Windows.Forms.GroupBox();
            this.comboBoxFileFilter = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.includeSubfoldersCb = new System.Windows.Forms.CheckBox();
            this.labelMinutes = new System.Windows.Forms.Label();
            this.labelAquisitionTime = new System.Windows.Forms.Label();
            this.labelDays = new System.Windows.Forms.Label();
            this.labelAccumulationTimeWindow = new System.Windows.Forms.Label();
            this.labelInstrumentType = new System.Windows.Forms.Label();
            this.comboBoxInstrumentType = new System.Windows.Forms.ComboBox();
            this.btnFolderToWatch = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.btnSkylineFilePath = new System.Windows.Forms.Button();
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.lblConfigRunning = new System.Windows.Forms.Label();
            this.btnCancelConfig = new System.Windows.Forms.Button();
            this.btnSaveConfig = new System.Windows.Forms.Button();
            this.labelQcFilePattern = new System.Windows.Forms.Label();
            this.btnOkConfig = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.groupBoxMain.SuspendLayout();
            this.tabPanoramaSettings.SuspendLayout();
            this.groupBoxPanorama.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // textSkylinePath
            // 
            this.textSkylinePath.Location = new System.Drawing.Point(21, 39);
            this.textSkylinePath.Name = "textSkylinePath";
            this.textSkylinePath.Size = new System.Drawing.Size(430, 20);
            this.textSkylinePath.TabIndex = 0;
            this.toolTip1.SetToolTip(this.textSkylinePath, "Path to a Skyline docuement where results will be imported");
            // 
            // textFolderToWatchPath
            // 
            this.textFolderToWatchPath.Location = new System.Drawing.Point(21, 89);
            this.textFolderToWatchPath.Name = "textFolderToWatchPath";
            this.textFolderToWatchPath.Size = new System.Drawing.Size(430, 20);
            this.textFolderToWatchPath.TabIndex = 2;
            this.toolTip1.SetToolTip(this.textFolderToWatchPath, "Path to the folder where the instrument will write QC runs");
            // 
            // textResultsTimeWindow
            // 
            this.textResultsTimeWindow.Location = new System.Drawing.Point(21, 259);
            this.textResultsTimeWindow.Name = "textResultsTimeWindow";
            this.textResultsTimeWindow.Size = new System.Drawing.Size(100, 20);
            this.textResultsTimeWindow.TabIndex = 5;
            this.toolTip1.SetToolTip(this.textResultsTimeWindow, resources.GetString("textResultsTimeWindow.ToolTip"));
            // 
            // textAquisitionTime
            // 
            this.textAquisitionTime.Location = new System.Drawing.Point(302, 310);
            this.textAquisitionTime.Name = "textAquisitionTime";
            this.textAquisitionTime.Size = new System.Drawing.Size(100, 20);
            this.textAquisitionTime.TabIndex = 7;
            this.toolTip1.SetToolTip(this.textAquisitionTime, "Expected duration in minutes to completely acquire a run.  The file will be impor" +
        "ted into the Skyline document after the specified number of minutes have elapsed" +
        " since it was created.");
            // 
            // textQCFilePattern
            // 
            this.textQCFilePattern.Location = new System.Drawing.Point(211, 172);
            this.textQCFilePattern.Name = "textQCFilePattern";
            this.textQCFilePattern.Size = new System.Drawing.Size(240, 20);
            this.textQCFilePattern.TabIndex = 4;
            this.toolTip1.SetToolTip(this.textQCFilePattern, "Results files matching the regular expression will be imported to the given Skyli" +
        "ne document.  If blank, all results files added to the folder will be imported t" +
        "o the Skyline document.");
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
            this.tabControl.Controls.Add(this.tabPanoramaSettings);
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
            this.textConfigName.TabIndex = 0;
            // 
            // labelConfigName
            // 
            this.labelConfigName.AutoSize = true;
            this.labelConfigName.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelConfigName.Location = new System.Drawing.Point(29, 15);
            this.labelConfigName.Name = "labelConfigName";
            this.labelConfigName.Size = new System.Drawing.Size(116, 13);
            this.labelConfigName.TabIndex = 54;
            this.labelConfigName.Text = "Configuration name";
            // 
            // groupBoxMain
            // 
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
            this.groupBoxMain.Location = new System.Drawing.Point(9, 57);
            this.groupBoxMain.Name = "groupBoxMain";
            this.groupBoxMain.Size = new System.Drawing.Size(519, 369);
            this.groupBoxMain.TabIndex = 53;
            this.groupBoxMain.TabStop = false;
            // 
            // comboBoxFileFilter
            // 
            this.comboBoxFileFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFileFilter.FormattingEnabled = true;
            this.comboBoxFileFilter.Location = new System.Drawing.Point(23, 172);
            this.comboBoxFileFilter.Name = "comboBoxFileFilter";
            this.comboBoxFileFilter.Size = new System.Drawing.Size(161, 21);
            this.comboBoxFileFilter.TabIndex = 66;
            this.comboBoxFileFilter.SelectedIndexChanged += new System.EventHandler(this.comboBoxFileFilter_SelectedIndexChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(20, 153);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(48, 13);
            this.label5.TabIndex = 65;
            this.label5.Text = "File filter:";
            // 
            // includeSubfoldersCb
            // 
            this.includeSubfoldersCb.AutoSize = true;
            this.includeSubfoldersCb.Location = new System.Drawing.Point(23, 115);
            this.includeSubfoldersCb.Name = "includeSubfoldersCb";
            this.includeSubfoldersCb.Size = new System.Drawing.Size(112, 17);
            this.includeSubfoldersCb.TabIndex = 63;
            this.includeSubfoldersCb.Text = "Include subfolders";
            this.includeSubfoldersCb.UseVisualStyleBackColor = true;
            // 
            // labelMinutes
            // 
            this.labelMinutes.AutoSize = true;
            this.labelMinutes.Location = new System.Drawing.Point(408, 315);
            this.labelMinutes.Name = "labelMinutes";
            this.labelMinutes.Size = new System.Drawing.Size(43, 13);
            this.labelMinutes.TabIndex = 59;
            this.labelMinutes.Text = "minutes";
            // 
            // labelAquisitionTime
            // 
            this.labelAquisitionTime.AutoSize = true;
            this.labelAquisitionTime.Location = new System.Drawing.Point(302, 292);
            this.labelAquisitionTime.Name = "labelAquisitionTime";
            this.labelAquisitionTime.Size = new System.Drawing.Size(149, 13);
            this.labelAquisitionTime.TabIndex = 58;
            this.labelAquisitionTime.Text = "Expected acquisition duration:";
            // 
            // labelDays
            // 
            this.labelDays.AutoSize = true;
            this.labelDays.Location = new System.Drawing.Point(128, 262);
            this.labelDays.Name = "labelDays";
            this.labelDays.Size = new System.Drawing.Size(29, 13);
            this.labelDays.TabIndex = 56;
            this.labelDays.Text = "days";
            // 
            // labelAccumulationTimeWindow
            // 
            this.labelAccumulationTimeWindow.AutoSize = true;
            this.labelAccumulationTimeWindow.Location = new System.Drawing.Point(20, 243);
            this.labelAccumulationTimeWindow.Name = "labelAccumulationTimeWindow";
            this.labelAccumulationTimeWindow.Size = new System.Drawing.Size(106, 13);
            this.labelAccumulationTimeWindow.TabIndex = 54;
            this.labelAccumulationTimeWindow.Text = "Results time window:";
            // 
            // labelInstrumentType
            // 
            this.labelInstrumentType.AutoSize = true;
            this.labelInstrumentType.Location = new System.Drawing.Point(20, 292);
            this.labelInstrumentType.Name = "labelInstrumentType";
            this.labelInstrumentType.Size = new System.Drawing.Size(82, 13);
            this.labelInstrumentType.TabIndex = 52;
            this.labelInstrumentType.Text = "Instrument type:";
            // 
            // comboBoxInstrumentType
            // 
            this.comboBoxInstrumentType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxInstrumentType.FormattingEnabled = true;
            this.comboBoxInstrumentType.Items.AddRange(new object[] {
            "Thermo",
            "Waters",
            "SCIEX",
            "Agilent",
            "Bruker",
            "Shimadzu"});
            this.comboBoxInstrumentType.Location = new System.Drawing.Point(21, 309);
            this.comboBoxInstrumentType.Name = "comboBoxInstrumentType";
            this.comboBoxInstrumentType.Size = new System.Drawing.Size(163, 21);
            this.comboBoxInstrumentType.TabIndex = 6;
            // 
            // btnFolderToWatch
            // 
            this.btnFolderToWatch.Location = new System.Drawing.Point(470, 85);
            this.btnFolderToWatch.Name = "btnFolderToWatch";
            this.btnFolderToWatch.Size = new System.Drawing.Size(29, 23);
            this.btnFolderToWatch.TabIndex = 3;
            this.btnFolderToWatch.Text = "...";
            this.btnFolderToWatch.UseVisualStyleBackColor = true;
            this.btnFolderToWatch.Click += new System.EventHandler(this.btnFolderToWatch_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(20, 20);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(95, 15);
            this.label2.TabIndex = 28;
            this.label2.Text = "Skyline file path:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(20, 70);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(93, 15);
            this.label3.TabIndex = 29;
            this.label3.Text = "Folder to watch:";
            // 
            // btnSkylineFilePath
            // 
            this.btnSkylineFilePath.Location = new System.Drawing.Point(470, 38);
            this.btnSkylineFilePath.Name = "btnSkylineFilePath";
            this.btnSkylineFilePath.Size = new System.Drawing.Size(29, 23);
            this.btnSkylineFilePath.TabIndex = 1;
            this.btnSkylineFilePath.Text = "...";
            this.btnSkylineFilePath.UseVisualStyleBackColor = true;
            this.btnSkylineFilePath.Click += new System.EventHandler(this.btnSkylineFilePath_Click);
            // 
            // tabPanoramaSettings
            // 
            this.tabPanoramaSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabPanoramaSettings.Controls.Add(this.cbPublishToPanorama);
            this.tabPanoramaSettings.Controls.Add(this.groupBoxPanorama);
            this.tabPanoramaSettings.Location = new System.Drawing.Point(4, 28);
            this.tabPanoramaSettings.Name = "tabPanoramaSettings";
            this.tabPanoramaSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabPanoramaSettings.Size = new System.Drawing.Size(536, 433);
            this.tabPanoramaSettings.TabIndex = 4;
            this.tabPanoramaSettings.Text = "Panorama Settings";
            // 
            // cbPublishToPanorama
            // 
            this.cbPublishToPanorama.AutoSize = true;
            this.cbPublishToPanorama.Location = new System.Drawing.Point(19, 36);
            this.cbPublishToPanorama.Name = "cbPublishToPanorama";
            this.cbPublishToPanorama.Size = new System.Drawing.Size(123, 17);
            this.cbPublishToPanorama.TabIndex = 57;
            this.cbPublishToPanorama.Text = "Publish to Panorama";
            this.cbPublishToPanorama.UseVisualStyleBackColor = true;
            this.cbPublishToPanorama.CheckedChanged += new System.EventHandler(this.cbPublishToPanorama_CheckedChanged);
            // 
            // groupBoxPanorama
            // 
            this.groupBoxPanorama.Controls.Add(this.labelPanoramaFolder);
            this.groupBoxPanorama.Controls.Add(this.textPanoramaFolder);
            this.groupBoxPanorama.Controls.Add(this.lblPanoramaUrl);
            this.groupBoxPanorama.Controls.Add(this.textPanoramaUrl);
            this.groupBoxPanorama.Controls.Add(this.textPanoramaPasswd);
            this.groupBoxPanorama.Controls.Add(this.lblPanoramaPasswd);
            this.groupBoxPanorama.Controls.Add(this.lblPanoramaEmail);
            this.groupBoxPanorama.Controls.Add(this.textPanoramaEmail);
            this.groupBoxPanorama.Location = new System.Drawing.Point(19, 74);
            this.groupBoxPanorama.Name = "groupBoxPanorama";
            this.groupBoxPanorama.Size = new System.Drawing.Size(482, 295);
            this.groupBoxPanorama.TabIndex = 56;
            this.groupBoxPanorama.TabStop = false;
            this.groupBoxPanorama.Text = "Panorama";
            // 
            // labelPanoramaFolder
            // 
            this.labelPanoramaFolder.AutoSize = true;
            this.labelPanoramaFolder.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelPanoramaFolder.Location = new System.Drawing.Point(14, 195);
            this.labelPanoramaFolder.Name = "labelPanoramaFolder";
            this.labelPanoramaFolder.Size = new System.Drawing.Size(204, 13);
            this.labelPanoramaFolder.TabIndex = 13;
            this.labelPanoramaFolder.Text = "Folder on Panorama (e.g. /MacCoss/QC):";
            // 
            // textPanoramaFolder
            // 
            this.textPanoramaFolder.Location = new System.Drawing.Point(16, 213);
            this.textPanoramaFolder.Name = "textPanoramaFolder";
            this.textPanoramaFolder.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textPanoramaFolder.Size = new System.Drawing.Size(378, 20);
            this.textPanoramaFolder.TabIndex = 3;
            // 
            // lblPanoramaUrl
            // 
            this.lblPanoramaUrl.AutoSize = true;
            this.lblPanoramaUrl.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblPanoramaUrl.Location = new System.Drawing.Point(14, 32);
            this.lblPanoramaUrl.Name = "lblPanoramaUrl";
            this.lblPanoramaUrl.Size = new System.Drawing.Size(188, 13);
            this.lblPanoramaUrl.TabIndex = 7;
            this.lblPanoramaUrl.Text = "URL (e.g. https://panoramaweb.org/):";
            // 
            // textPanoramaUrl
            // 
            this.textPanoramaUrl.Location = new System.Drawing.Point(16, 49);
            this.textPanoramaUrl.Name = "textPanoramaUrl";
            this.textPanoramaUrl.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textPanoramaUrl.Size = new System.Drawing.Size(378, 20);
            this.textPanoramaUrl.TabIndex = 0;
            // 
            // textPanoramaPasswd
            // 
            this.textPanoramaPasswd.Location = new System.Drawing.Point(17, 156);
            this.textPanoramaPasswd.Name = "textPanoramaPasswd";
            this.textPanoramaPasswd.PasswordChar = '*';
            this.textPanoramaPasswd.Size = new System.Drawing.Size(207, 20);
            this.textPanoramaPasswd.TabIndex = 2;
            this.textPanoramaPasswd.UseSystemPasswordChar = true;
            // 
            // lblPanoramaPasswd
            // 
            this.lblPanoramaPasswd.AutoSize = true;
            this.lblPanoramaPasswd.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblPanoramaPasswd.Location = new System.Drawing.Point(14, 139);
            this.lblPanoramaPasswd.Name = "lblPanoramaPasswd";
            this.lblPanoramaPasswd.Size = new System.Drawing.Size(56, 13);
            this.lblPanoramaPasswd.TabIndex = 11;
            this.lblPanoramaPasswd.Text = "Password:";
            // 
            // lblPanoramaEmail
            // 
            this.lblPanoramaEmail.AutoSize = true;
            this.lblPanoramaEmail.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblPanoramaEmail.Location = new System.Drawing.Point(14, 86);
            this.lblPanoramaEmail.Name = "lblPanoramaEmail";
            this.lblPanoramaEmail.Size = new System.Drawing.Size(35, 13);
            this.lblPanoramaEmail.TabIndex = 9;
            this.lblPanoramaEmail.Text = "Email:";
            // 
            // textPanoramaEmail
            // 
            this.textPanoramaEmail.Location = new System.Drawing.Point(16, 103);
            this.textPanoramaEmail.Name = "textPanoramaEmail";
            this.textPanoramaEmail.Size = new System.Drawing.Size(207, 20);
            this.textPanoramaEmail.TabIndex = 1;
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
            this.btnCancelConfig.TabIndex = 57;
            this.btnCancelConfig.Text = "Cancel";
            this.btnCancelConfig.UseVisualStyleBackColor = true;
            // 
            // btnSaveConfig
            // 
            this.btnSaveConfig.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSaveConfig.Location = new System.Drawing.Point(181, 26);
            this.btnSaveConfig.Name = "btnSaveConfig";
            this.btnSaveConfig.Size = new System.Drawing.Size(79, 26);
            this.btnSaveConfig.TabIndex = 0;
            this.btnSaveConfig.Text = "Save";
            this.btnSaveConfig.UseVisualStyleBackColor = true;
            this.btnSaveConfig.Click += new System.EventHandler(this.btnSaveConfig_Click);
            // 
            // labelQcFilePattern
            // 
            this.labelQcFilePattern.AutoSize = true;
            this.labelQcFilePattern.Location = new System.Drawing.Point(211, 153);
            this.labelQcFilePattern.Name = "labelQcFilePattern";
            this.labelQcFilePattern.Size = new System.Drawing.Size(44, 13);
            this.labelQcFilePattern.TabIndex = 67;
            this.labelQcFilePattern.Text = "Pattern:";
            // 
            // btnOkConfig
            // 
            this.btnOkConfig.Location = new System.Drawing.Point(223, 28);
            this.btnOkConfig.Name = "btnOkConfig";
            this.btnOkConfig.Size = new System.Drawing.Size(75, 23);
            this.btnOkConfig.TabIndex = 59;
            this.btnOkConfig.Text = "OK";
            this.btnOkConfig.UseVisualStyleBackColor = true;
            this.btnOkConfig.Click += new System.EventHandler(this.btnOkConfig_Click);
            // 
            // AutoQcConfigForm
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
            this.Name = "AutoQcConfigForm";
            this.Text = "AutoQC Configuration";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.tabSettings.ResumeLayout(false);
            this.tabSettings.PerformLayout();
            this.groupBoxMain.ResumeLayout(false);
            this.groupBoxMain.PerformLayout();
            this.tabPanoramaSettings.ResumeLayout(false);
            this.tabPanoramaSettings.PerformLayout();
            this.groupBoxPanorama.ResumeLayout(false);
            this.groupBoxPanorama.PerformLayout();
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
        private System.Windows.Forms.TextBox textAquisitionTime;
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
    }
}