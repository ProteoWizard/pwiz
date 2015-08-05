namespace AutoQC
{
    partial class AutoQCForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AutoQCForm));
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.textFolderToWatchPath = new System.Windows.Forms.TextBox();
            this.textSkylinePath = new System.Windows.Forms.TextBox();
            this.textAquisitionTime = new System.Windows.Forms.TextBox();
            this.textResultsTimeWindow = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.groupBoxMain = new System.Windows.Forms.GroupBox();
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
            this.tabSprocopSettings = new System.Windows.Forms.TabPage();
            this.cbRunsprocop = new System.Windows.Forms.CheckBox();
            this.groupBoxSprocop = new System.Windows.Forms.GroupBox();
            this.btnRScriptPath = new System.Windows.Forms.Button();
            this.label10 = new System.Windows.Forms.Label();
            this.textRScriptPath = new System.Windows.Forms.TextBox();
            this.checkBoxIsHighRes = new System.Windows.Forms.CheckBox();
            this.numericUpDownMMA = new System.Windows.Forms.NumericUpDown();
            this.labelMMA = new System.Windows.Forms.Label();
            this.labelThreshold = new System.Windows.Forms.Label();
            this.numericUpDownThreshold = new System.Windows.Forms.NumericUpDown();
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
            this.tabOutput = new System.Windows.Forms.TabPage();
            this.textOutput = new System.Windows.Forms.RichTextBox();
            this.tabInstructions = new System.Windows.Forms.TabPage();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.labelStatusRunning = new System.Windows.Forms.Label();
            this.btnStartStop = new System.Windows.Forms.Button();
            this.statusImg = new System.Windows.Forms.PictureBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.groupBoxMain.SuspendLayout();
            this.tabSprocopSettings.SuspendLayout();
            this.groupBoxSprocop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMMA)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreshold)).BeginInit();
            this.tabPanoramaSettings.SuspendLayout();
            this.groupBoxPanorama.SuspendLayout();
            this.tabOutput.SuspendLayout();
            this.tabInstructions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.statusImg)).BeginInit();
            this.SuspendLayout();
            // 
            // textFolderToWatchPath
            // 
            this.textFolderToWatchPath.Location = new System.Drawing.Point(21, 105);
            this.textFolderToWatchPath.Name = "textFolderToWatchPath";
            this.textFolderToWatchPath.Size = new System.Drawing.Size(514, 20);
            this.textFolderToWatchPath.TabIndex = 30;
            this.toolTip1.SetToolTip(this.textFolderToWatchPath, "Path to the folder where the instrument will write QC runs");
            // 
            // textSkylinePath
            // 
            this.textSkylinePath.Location = new System.Drawing.Point(21, 49);
            this.textSkylinePath.Name = "textSkylinePath";
            this.textSkylinePath.Size = new System.Drawing.Size(514, 20);
            this.textSkylinePath.TabIndex = 32;
            this.toolTip1.SetToolTip(this.textSkylinePath, "Path to a Skyline docuement where results will be imported");
            // 
            // textAquisitionTime
            // 
            this.textAquisitionTime.Location = new System.Drawing.Point(302, 243);
            this.textAquisitionTime.Name = "textAquisitionTime";
            this.textAquisitionTime.Size = new System.Drawing.Size(100, 20);
            this.textAquisitionTime.TabIndex = 57;
            this.toolTip1.SetToolTip(this.textAquisitionTime, "Expected duration in minutes to completely acquire a run.  The file will be impor" +
        "ted into the Skyline document after the specified number of minutes have elapsed" +
        " since it was created.");
            // 
            // textResultsTimeWindow
            // 
            this.textResultsTimeWindow.Location = new System.Drawing.Point(21, 173);
            this.textResultsTimeWindow.Name = "textResultsTimeWindow";
            this.textResultsTimeWindow.Size = new System.Drawing.Size(100, 20);
            this.textResultsTimeWindow.TabIndex = 55;
            this.toolTip1.SetToolTip(this.textResultsTimeWindow, resources.GetString("textResultsTimeWindow.ToolTip"));
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
            this.splitContainer1.Panel2.Controls.Add(this.labelStatusRunning);
            this.splitContainer1.Panel2.Controls.Add(this.btnStartStop);
            this.splitContainer1.Panel2.Controls.Add(this.statusImg);
            this.splitContainer1.Panel2.Controls.Add(this.groupBox1);
            this.splitContainer1.Size = new System.Drawing.Size(645, 572);
            this.splitContainer1.SplitterDistance = 480;
            this.splitContainer1.TabIndex = 0;
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabSettings);
            this.tabControl.Controls.Add(this.tabSprocopSettings);
            this.tabControl.Controls.Add(this.tabPanoramaSettings);
            this.tabControl.Controls.Add(this.tabOutput);
            this.tabControl.Controls.Add(this.tabInstructions);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.Padding = new System.Drawing.Point(20, 6);
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(645, 480);
            this.tabControl.TabIndex = 1;
            // 
            // tabSettings
            // 
            this.tabSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabSettings.Controls.Add(this.groupBoxMain);
            this.tabSettings.Location = new System.Drawing.Point(4, 28);
            this.tabSettings.Margin = new System.Windows.Forms.Padding(4);
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.Padding = new System.Windows.Forms.Padding(4);
            this.tabSettings.Size = new System.Drawing.Size(637, 448);
            this.tabSettings.TabIndex = 1;
            this.tabSettings.Text = "Settings";
            // 
            // groupBoxMain
            // 
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
            this.groupBoxMain.Location = new System.Drawing.Point(8, 15);
            this.groupBoxMain.Name = "groupBoxMain";
            this.groupBoxMain.Size = new System.Drawing.Size(621, 314);
            this.groupBoxMain.TabIndex = 53;
            this.groupBoxMain.TabStop = false;
            // 
            // labelMinutes
            // 
            this.labelMinutes.AutoSize = true;
            this.labelMinutes.Location = new System.Drawing.Point(408, 248);
            this.labelMinutes.Name = "labelMinutes";
            this.labelMinutes.Size = new System.Drawing.Size(43, 13);
            this.labelMinutes.TabIndex = 59;
            this.labelMinutes.Text = "minutes";
            // 
            // labelAquisitionTime
            // 
            this.labelAquisitionTime.AutoSize = true;
            this.labelAquisitionTime.Location = new System.Drawing.Point(302, 225);
            this.labelAquisitionTime.Name = "labelAquisitionTime";
            this.labelAquisitionTime.Size = new System.Drawing.Size(149, 13);
            this.labelAquisitionTime.TabIndex = 58;
            this.labelAquisitionTime.Text = "Expected acquisition duration:";
            // 
            // labelDays
            // 
            this.labelDays.AutoSize = true;
            this.labelDays.Location = new System.Drawing.Point(128, 176);
            this.labelDays.Name = "labelDays";
            this.labelDays.Size = new System.Drawing.Size(29, 13);
            this.labelDays.TabIndex = 56;
            this.labelDays.Text = "days";
            // 
            // labelAccumulationTimeWindow
            // 
            this.labelAccumulationTimeWindow.AutoSize = true;
            this.labelAccumulationTimeWindow.Location = new System.Drawing.Point(20, 157);
            this.labelAccumulationTimeWindow.Name = "labelAccumulationTimeWindow";
            this.labelAccumulationTimeWindow.Size = new System.Drawing.Size(106, 13);
            this.labelAccumulationTimeWindow.TabIndex = 54;
            this.labelAccumulationTimeWindow.Text = "Results time window:";
            // 
            // labelInstrumentType
            // 
            this.labelInstrumentType.AutoSize = true;
            this.labelInstrumentType.Location = new System.Drawing.Point(20, 225);
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
            "Agilent"});
            this.comboBoxInstrumentType.Location = new System.Drawing.Point(21, 241);
            this.comboBoxInstrumentType.Name = "comboBoxInstrumentType";
            this.comboBoxInstrumentType.Size = new System.Drawing.Size(163, 21);
            this.comboBoxInstrumentType.TabIndex = 51;
            // 
            // btnFolderToWatch
            // 
            this.btnFolderToWatch.Location = new System.Drawing.Point(555, 102);
            this.btnFolderToWatch.Name = "btnFolderToWatch";
            this.btnFolderToWatch.Size = new System.Drawing.Size(29, 23);
            this.btnFolderToWatch.TabIndex = 50;
            this.btnFolderToWatch.Text = "...";
            this.btnFolderToWatch.UseVisualStyleBackColor = true;
            this.btnFolderToWatch.Click += new System.EventHandler(this.btnFolderToWatch_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(20, 31);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(92, 15);
            this.label2.TabIndex = 28;
            this.label2.Text = "Skyline file path";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(20, 87);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(90, 15);
            this.label3.TabIndex = 29;
            this.label3.Text = "Folder to watch";
            // 
            // btnSkylineFilePath
            // 
            this.btnSkylineFilePath.Location = new System.Drawing.Point(555, 48);
            this.btnSkylineFilePath.Name = "btnSkylineFilePath";
            this.btnSkylineFilePath.Size = new System.Drawing.Size(29, 23);
            this.btnSkylineFilePath.TabIndex = 46;
            this.btnSkylineFilePath.Text = "...";
            this.btnSkylineFilePath.UseVisualStyleBackColor = true;
            this.btnSkylineFilePath.Click += new System.EventHandler(this.btnSkylineFilePath_Click);
            // 
            // tabSprocopSettings
            // 
            this.tabSprocopSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabSprocopSettings.Controls.Add(this.cbRunsprocop);
            this.tabSprocopSettings.Controls.Add(this.groupBoxSprocop);
            this.tabSprocopSettings.Location = new System.Drawing.Point(4, 28);
            this.tabSprocopSettings.Name = "tabSprocopSettings";
            this.tabSprocopSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabSprocopSettings.Size = new System.Drawing.Size(637, 448);
            this.tabSprocopSettings.TabIndex = 3;
            this.tabSprocopSettings.Text = "SProCoP Settings";
            // 
            // cbRunsprocop
            // 
            this.cbRunsprocop.AutoSize = true;
            this.cbRunsprocop.Location = new System.Drawing.Point(19, 36);
            this.cbRunsprocop.Name = "cbRunsprocop";
            this.cbRunsprocop.Size = new System.Drawing.Size(92, 17);
            this.cbRunsprocop.TabIndex = 54;
            this.cbRunsprocop.Text = "Run SProCoP";
            this.cbRunsprocop.UseVisualStyleBackColor = true;
            this.cbRunsprocop.CheckedChanged += new System.EventHandler(this.cbRunsprocop_CheckedChanged);
            // 
            // groupBoxSprocop
            // 
            this.groupBoxSprocop.Controls.Add(this.btnRScriptPath);
            this.groupBoxSprocop.Controls.Add(this.label10);
            this.groupBoxSprocop.Controls.Add(this.textRScriptPath);
            this.groupBoxSprocop.Controls.Add(this.checkBoxIsHighRes);
            this.groupBoxSprocop.Controls.Add(this.numericUpDownMMA);
            this.groupBoxSprocop.Controls.Add(this.labelMMA);
            this.groupBoxSprocop.Controls.Add(this.labelThreshold);
            this.groupBoxSprocop.Controls.Add(this.numericUpDownThreshold);
            this.groupBoxSprocop.Location = new System.Drawing.Point(19, 74);
            this.groupBoxSprocop.Name = "groupBoxSprocop";
            this.groupBoxSprocop.Size = new System.Drawing.Size(564, 239);
            this.groupBoxSprocop.TabIndex = 53;
            this.groupBoxSprocop.TabStop = false;
            this.groupBoxSprocop.Text = "SProCoP";
            // 
            // btnRScriptPath
            // 
            this.btnRScriptPath.Location = new System.Drawing.Point(513, 181);
            this.btnRScriptPath.Name = "btnRScriptPath";
            this.btnRScriptPath.Size = new System.Drawing.Size(29, 23);
            this.btnRScriptPath.TabIndex = 59;
            this.btnRScriptPath.Text = "...";
            this.btnRScriptPath.UseVisualStyleBackColor = true;
            this.btnRScriptPath.Click += new System.EventHandler(this.btnRScriptPath_Click);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.Location = new System.Drawing.Point(14, 164);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(144, 15);
            this.label10.TabIndex = 57;
            this.label10.Text = "R(3.0.2) RScript.exe path";
            // 
            // textRScriptPath
            // 
            this.textRScriptPath.Location = new System.Drawing.Point(15, 183);
            this.textRScriptPath.Name = "textRScriptPath";
            this.textRScriptPath.Size = new System.Drawing.Size(477, 20);
            this.textRScriptPath.TabIndex = 58;
            // 
            // checkBoxIsHighRes
            // 
            this.checkBoxIsHighRes.AutoSize = true;
            this.checkBoxIsHighRes.ForeColor = System.Drawing.Color.Black;
            this.checkBoxIsHighRes.Location = new System.Drawing.Point(18, 81);
            this.checkBoxIsHighRes.Name = "checkBoxIsHighRes";
            this.checkBoxIsHighRes.Size = new System.Drawing.Size(115, 17);
            this.checkBoxIsHighRes.TabIndex = 56;
            this.checkBoxIsHighRes.Text = "High resolution MS";
            this.checkBoxIsHighRes.UseVisualStyleBackColor = true;
            // 
            // numericUpDownMMA
            // 
            this.numericUpDownMMA.Location = new System.Drawing.Point(18, 124);
            this.numericUpDownMMA.Name = "numericUpDownMMA";
            this.numericUpDownMMA.Size = new System.Drawing.Size(36, 20);
            this.numericUpDownMMA.TabIndex = 55;
            // 
            // labelMMA
            // 
            this.labelMMA.AutoSize = true;
            this.labelMMA.ForeColor = System.Drawing.Color.Black;
            this.labelMMA.Location = new System.Drawing.Point(14, 108);
            this.labelMMA.Name = "labelMMA";
            this.labelMMA.Size = new System.Drawing.Size(64, 13);
            this.labelMMA.TabIndex = 54;
            this.labelMMA.Text = "MMA value:";
            // 
            // labelThreshold
            // 
            this.labelThreshold.AutoSize = true;
            this.labelThreshold.ForeColor = System.Drawing.Color.Black;
            this.labelThreshold.Location = new System.Drawing.Point(14, 32);
            this.labelThreshold.Name = "labelThreshold";
            this.labelThreshold.Size = new System.Drawing.Size(57, 13);
            this.labelThreshold.TabIndex = 53;
            this.labelThreshold.Text = "Threshold:";
            // 
            // numericUpDownThreshold
            // 
            this.numericUpDownThreshold.Location = new System.Drawing.Point(18, 48);
            this.numericUpDownThreshold.Name = "numericUpDownThreshold";
            this.numericUpDownThreshold.Size = new System.Drawing.Size(36, 20);
            this.numericUpDownThreshold.TabIndex = 52;
            // 
            // tabPanoramaSettings
            // 
            this.tabPanoramaSettings.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabPanoramaSettings.Controls.Add(this.cbPublishToPanorama);
            this.tabPanoramaSettings.Controls.Add(this.groupBoxPanorama);
            this.tabPanoramaSettings.Location = new System.Drawing.Point(4, 28);
            this.tabPanoramaSettings.Name = "tabPanoramaSettings";
            this.tabPanoramaSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabPanoramaSettings.Size = new System.Drawing.Size(637, 448);
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
            this.groupBoxPanorama.Size = new System.Drawing.Size(599, 218);
            this.groupBoxPanorama.TabIndex = 56;
            this.groupBoxPanorama.TabStop = false;
            this.groupBoxPanorama.Text = "Panorama";
            // 
            // labelPanoramaFolder
            // 
            this.labelPanoramaFolder.AutoSize = true;
            this.labelPanoramaFolder.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelPanoramaFolder.Location = new System.Drawing.Point(14, 147);
            this.labelPanoramaFolder.Name = "labelPanoramaFolder";
            this.labelPanoramaFolder.Size = new System.Drawing.Size(201, 13);
            this.labelPanoramaFolder.TabIndex = 13;
            this.labelPanoramaFolder.Text = "Folder on Panorama (e.g. /MacCoss/QC)";
            // 
            // textPanoramaFolder
            // 
            this.textPanoramaFolder.Location = new System.Drawing.Point(16, 165);
            this.textPanoramaFolder.Name = "textPanoramaFolder";
            this.textPanoramaFolder.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textPanoramaFolder.Size = new System.Drawing.Size(512, 20);
            this.textPanoramaFolder.TabIndex = 14;
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
            this.textPanoramaUrl.Size = new System.Drawing.Size(512, 20);
            this.textPanoramaUrl.TabIndex = 8;
            // 
            // textPanoramaPasswd
            // 
            this.textPanoramaPasswd.Location = new System.Drawing.Point(251, 103);
            this.textPanoramaPasswd.Name = "textPanoramaPasswd";
            this.textPanoramaPasswd.PasswordChar = '*';
            this.textPanoramaPasswd.Size = new System.Drawing.Size(207, 20);
            this.textPanoramaPasswd.TabIndex = 12;
            this.textPanoramaPasswd.UseSystemPasswordChar = true;
            // 
            // lblPanoramaPasswd
            // 
            this.lblPanoramaPasswd.AutoSize = true;
            this.lblPanoramaPasswd.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblPanoramaPasswd.Location = new System.Drawing.Point(248, 86);
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
            this.textPanoramaEmail.TabIndex = 10;
            // 
            // tabOutput
            // 
            this.tabOutput.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabOutput.Controls.Add(this.textOutput);
            this.tabOutput.Location = new System.Drawing.Point(4, 28);
            this.tabOutput.Name = "tabOutput";
            this.tabOutput.Padding = new System.Windows.Forms.Padding(3);
            this.tabOutput.Size = new System.Drawing.Size(637, 448);
            this.tabOutput.TabIndex = 5;
            this.tabOutput.Text = "Output";
            // 
            // textOutput
            // 
            this.textOutput.Location = new System.Drawing.Point(25, 27);
            this.textOutput.Name = "textOutput";
            this.textOutput.ReadOnly = true;
            this.textOutput.Size = new System.Drawing.Size(581, 397);
            this.textOutput.TabIndex = 0;
            this.textOutput.Text = "";
            // 
            // tabInstructions
            // 
            this.tabInstructions.Controls.Add(this.richTextBox1);
            this.tabInstructions.Location = new System.Drawing.Point(4, 28);
            this.tabInstructions.Name = "tabInstructions";
            this.tabInstructions.Padding = new System.Windows.Forms.Padding(3);
            this.tabInstructions.Size = new System.Drawing.Size(637, 448);
            this.tabInstructions.TabIndex = 2;
            this.tabInstructions.Text = "Instructions";
            this.tabInstructions.UseVisualStyleBackColor = true;
            // 
            // richTextBox1
            // 
            this.richTextBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBox1.Enabled = false;
            this.richTextBox1.Location = new System.Drawing.Point(24, 17);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.richTextBox1.Size = new System.Drawing.Size(587, 498);
            this.richTextBox1.TabIndex = 0;
            this.richTextBox1.Text = resources.GetString("richTextBox1.Text");
            // 
            // labelStatusRunning
            // 
            this.labelStatusRunning.AutoSize = true;
            this.labelStatusRunning.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.labelStatusRunning.Location = new System.Drawing.Point(65, 38);
            this.labelStatusRunning.Name = "labelStatusRunning";
            this.labelStatusRunning.Size = new System.Drawing.Size(95, 13);
            this.labelStatusRunning.TabIndex = 64;
            this.labelStatusRunning.Text = "AutoQC is stopped";
            // 
            // btnStartStop
            // 
            this.btnStartStop.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnStartStop.Location = new System.Drawing.Point(462, 28);
            this.btnStartStop.Name = "btnStartStop";
            this.btnStartStop.Size = new System.Drawing.Size(148, 27);
            this.btnStartStop.TabIndex = 56;
            this.btnStartStop.Text = "Run AutoQC";
            this.btnStartStop.UseVisualStyleBackColor = true;
            this.btnStartStop.Click += new System.EventHandler(this.btnStartStopAutoQC_Click);
            // 
            // statusImg
            // 
            this.statusImg.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.statusImg.Image = global::AutoQC.Properties.Resources.redstatus;
            this.statusImg.Location = new System.Drawing.Point(29, 22);
            this.statusImg.Name = "statusImg";
            this.statusImg.Size = new System.Drawing.Size(30, 30);
            this.statusImg.TabIndex = 61;
            this.statusImg.TabStop = false;
            // 
            // groupBox1
            // 
            this.groupBox1.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.groupBox1.Location = new System.Drawing.Point(4, -3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(637, 85);
            this.groupBox1.TabIndex = 65;
            this.groupBox1.TabStop = false;
            // 
            // AutoQCForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(645, 572);
            this.Controls.Add(this.splitContainer1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "AutoQCForm";
            this.Text = " AutoQC";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.tabSettings.ResumeLayout(false);
            this.groupBoxMain.ResumeLayout(false);
            this.groupBoxMain.PerformLayout();
            this.tabSprocopSettings.ResumeLayout(false);
            this.tabSprocopSettings.PerformLayout();
            this.groupBoxSprocop.ResumeLayout(false);
            this.groupBoxSprocop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMMA)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreshold)).EndInit();
            this.tabPanoramaSettings.ResumeLayout(false);
            this.tabPanoramaSettings.PerformLayout();
            this.groupBoxPanorama.ResumeLayout(false);
            this.groupBoxPanorama.PerformLayout();
            this.tabOutput.ResumeLayout(false);
            this.tabInstructions.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.statusImg)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.GroupBox groupBoxMain;
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
        private System.Windows.Forms.TabPage tabSprocopSettings;
        private System.Windows.Forms.CheckBox cbRunsprocop;
        private System.Windows.Forms.GroupBox groupBoxSprocop;
        private System.Windows.Forms.Button btnRScriptPath;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox textRScriptPath;
        private System.Windows.Forms.CheckBox checkBoxIsHighRes;
        private System.Windows.Forms.NumericUpDown numericUpDownMMA;
        private System.Windows.Forms.Label labelMMA;
        private System.Windows.Forms.Label labelThreshold;
        private System.Windows.Forms.NumericUpDown numericUpDownThreshold;
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
        private System.Windows.Forms.TabPage tabOutput;
        private System.Windows.Forms.RichTextBox textOutput;
        private System.Windows.Forms.TabPage tabInstructions;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Label labelStatusRunning;
        private System.Windows.Forms.Button btnStartStop;
        private System.Windows.Forms.PictureBox statusImg;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label labelAquisitionTime;
        private System.Windows.Forms.TextBox textAquisitionTime;
        private System.Windows.Forms.Label labelMinutes;
    }
}