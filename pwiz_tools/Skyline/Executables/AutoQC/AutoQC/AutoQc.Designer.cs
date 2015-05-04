namespace AutoQC
{
    partial class AutoQc
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AutoQc));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabRunSetup = new System.Windows.Forms.TabPage();
            this.comboBoxType = new System.Windows.Forms.ComboBox();
            this.labelType = new System.Windows.Forms.Label();
            this.labelNumberOfRows = new System.Windows.Forms.Label();
            this.textBoxNewRows = new System.Windows.Forms.TextBox();
            this.btnAddRows = new System.Windows.Forms.Button();
            this.runGridView = new System.Windows.Forms.DataGridView();
            this.Type = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.buttonClear = new System.Windows.Forms.Button();
            this.statusImg = new System.Windows.Forms.PictureBox();
            this.labelStatus = new System.Windows.Forms.Label();
            this.textBoxLog = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.btnRunSprocopAuto = new System.Windows.Forms.Button();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.groupBoxPanorama = new System.Windows.Forms.GroupBox();
            this.labelPanoramaFolder = new System.Windows.Forms.Label();
            this.textPanoramaFolder = new System.Windows.Forms.TextBox();
            this.lblPanoramaUrl = new System.Windows.Forms.Label();
            this.textPanoramaUrl = new System.Windows.Forms.TextBox();
            this.textPanoramaPasswd = new System.Windows.Forms.TextBox();
            this.lblPanoramaPasswd = new System.Windows.Forms.Label();
            this.lblPanoramaEmail = new System.Windows.Forms.Label();
            this.textPanoramaEmail = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnRScriptPath = new System.Windows.Forms.Button();
            this.label10 = new System.Windows.Forms.Label();
            this.textBoxRScriptPath = new System.Windows.Forms.TextBox();
            this.skylineRunnerPathLabel = new System.Windows.Forms.Label();
            this.btnFolderToWatch = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.folderToWatchPath = new System.Windows.Forms.TextBox();
            this.skylineRunnerPathInput = new System.Windows.Forms.TextBox();
            this.btnSkylineFilePath = new System.Windows.Forms.Button();
            this.skylineFilePath = new System.Windows.Forms.TextBox();
            this.btnSkylingRunnerPath = new System.Windows.Forms.Button();
            this.tabSprocopSettings = new System.Windows.Forms.TabPage();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.checkBoxIsHighRes = new System.Windows.Forms.CheckBox();
            this.numericUpDownMMA = new System.Windows.Forms.NumericUpDown();
            this.labelMMA = new System.Windows.Forms.Label();
            this.labelThreshold = new System.Windows.Forms.Label();
            this.numericUpDownThreshold = new System.Windows.Forms.NumericUpDown();
            this.tabInstructions = new System.Windows.Forms.TabPage();
            this.label5 = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabRunSetup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.runGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.statusImg)).BeginInit();
            this.tabSettings.SuspendLayout();
            this.groupBoxPanorama.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.tabSprocopSettings.SuspendLayout();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMMA)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreshold)).BeginInit();
            this.tabInstructions.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabRunSetup);
            this.tabControl1.Controls.Add(this.tabSettings);
            this.tabControl1.Controls.Add(this.tabSprocopSettings);
            this.tabControl1.Controls.Add(this.tabInstructions);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(645, 572);
            this.tabControl1.TabIndex = 0;
            // 
            // tabRunSetup
            // 
            this.tabRunSetup.Controls.Add(this.comboBoxType);
            this.tabRunSetup.Controls.Add(this.labelType);
            this.tabRunSetup.Controls.Add(this.labelNumberOfRows);
            this.tabRunSetup.Controls.Add(this.textBoxNewRows);
            this.tabRunSetup.Controls.Add(this.btnAddRows);
            this.tabRunSetup.Controls.Add(this.runGridView);
            this.tabRunSetup.Controls.Add(this.buttonClear);
            this.tabRunSetup.Controls.Add(this.statusImg);
            this.tabRunSetup.Controls.Add(this.labelStatus);
            this.tabRunSetup.Controls.Add(this.textBoxLog);
            this.tabRunSetup.Controls.Add(this.label7);
            this.tabRunSetup.Controls.Add(this.btnRunSprocopAuto);
            this.tabRunSetup.Location = new System.Drawing.Point(4, 22);
            this.tabRunSetup.Name = "tabRunSetup";
            this.tabRunSetup.Padding = new System.Windows.Forms.Padding(3);
            this.tabRunSetup.Size = new System.Drawing.Size(637, 546);
            this.tabRunSetup.TabIndex = 0;
            this.tabRunSetup.Text = "Run  Setup";
            this.tabRunSetup.UseVisualStyleBackColor = true;
            // 
            // comboBoxType
            // 
            this.comboBoxType.FormattingEnabled = true;
            this.comboBoxType.Items.AddRange(new object[] {
            "None",
            "Threshold",
            "QC"});
            this.comboBoxType.Location = new System.Drawing.Point(94, 100);
            this.comboBoxType.Name = "comboBoxType";
            this.comboBoxType.Size = new System.Drawing.Size(92, 21);
            this.comboBoxType.TabIndex = 67;
            // 
            // labelType
            // 
            this.labelType.AutoSize = true;
            this.labelType.ForeColor = System.Drawing.Color.Black;
            this.labelType.Location = new System.Drawing.Point(91, 84);
            this.labelType.Name = "labelType";
            this.labelType.Size = new System.Drawing.Size(55, 13);
            this.labelType.TabIndex = 66;
            this.labelType.Text = "Row type:";
            // 
            // labelNumberOfRows
            // 
            this.labelNumberOfRows.AutoSize = true;
            this.labelNumberOfRows.ForeColor = System.Drawing.Color.Black;
            this.labelNumberOfRows.Location = new System.Drawing.Point(8, 84);
            this.labelNumberOfRows.Name = "labelNumberOfRows";
            this.labelNumberOfRows.Size = new System.Drawing.Size(77, 13);
            this.labelNumberOfRows.TabIndex = 65;
            this.labelNumberOfRows.Text = "# of new rows:";
            // 
            // textBoxNewRows
            // 
            this.textBoxNewRows.Location = new System.Drawing.Point(10, 100);
            this.textBoxNewRows.Name = "textBoxNewRows";
            this.textBoxNewRows.Size = new System.Drawing.Size(72, 20);
            this.textBoxNewRows.TabIndex = 64;
            // 
            // btnAddRows
            // 
            this.btnAddRows.Location = new System.Drawing.Point(192, 98);
            this.btnAddRows.Name = "btnAddRows";
            this.btnAddRows.Size = new System.Drawing.Size(50, 23);
            this.btnAddRows.TabIndex = 63;
            this.btnAddRows.Text = "Add";
            this.btnAddRows.UseVisualStyleBackColor = true;
            this.btnAddRows.Click += new System.EventHandler(this.buttonAddRuns_Click);
            // 
            // runGridView
            // 
            this.runGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.runGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Type,
            this.ColumnFile});
            this.runGridView.Location = new System.Drawing.Point(8, 125);
            this.runGridView.Name = "runGridView";
            this.runGridView.Size = new System.Drawing.Size(621, 200);
            this.runGridView.TabIndex = 58;
            this.runGridView.UserAddedRow += new System.Windows.Forms.DataGridViewRowEventHandler(this.runGridView_UserAddedRow);
            this.runGridView.UserDeletedRow += new System.Windows.Forms.DataGridViewRowEventHandler(this.runGridView_UserDeletedRow);
            // 
            // Type
            // 
            this.Type.HeaderText = "Type";
            this.Type.Items.AddRange(new object[] {
            "None",
            "Threshold",
            "QC"});
            this.Type.Name = "Type";
            this.Type.Width = 210;
            // 
            // ColumnFile
            // 
            this.ColumnFile.HeaderText = "File";
            this.ColumnFile.Name = "ColumnFile";
            this.ColumnFile.Width = 300;
            // 
            // buttonClear
            // 
            this.buttonClear.Enabled = false;
            this.buttonClear.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonClear.Location = new System.Drawing.Point(8, 54);
            this.buttonClear.Name = "buttonClear";
            this.buttonClear.Size = new System.Drawing.Size(148, 27);
            this.buttonClear.TabIndex = 47;
            this.buttonClear.Text = "Clear Stored Run Data";
            this.buttonClear.UseVisualStyleBackColor = true;
            this.buttonClear.Click += new System.EventHandler(this.buttonClear_Click);
            // 
            // statusImg
            // 
            this.statusImg.Image = global::AutoQC.Properties.Resources.redstatus;
            this.statusImg.Location = new System.Drawing.Point(599, 22);
            this.statusImg.Name = "statusImg";
            this.statusImg.Size = new System.Drawing.Size(30, 30);
            this.statusImg.TabIndex = 39;
            this.statusImg.TabStop = false;
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(222, 36);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(21, 13);
            this.labelStatus.TabIndex = 40;
            this.labelStatus.Text = "Off";
            // 
            // textBoxLog
            // 
            this.textBoxLog.AcceptsReturn = true;
            this.textBoxLog.Location = new System.Drawing.Point(6, 331);
            this.textBoxLog.Multiline = true;
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxLog.Size = new System.Drawing.Size(621, 207);
            this.textBoxLog.TabIndex = 41;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(176, 36);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(40, 13);
            this.label7.TabIndex = 38;
            this.label7.Text = "Status:";
            // 
            // btnRunSprocopAuto
            // 
            this.btnRunSprocopAuto.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRunSprocopAuto.Location = new System.Drawing.Point(8, 22);
            this.btnRunSprocopAuto.Name = "btnRunSprocopAuto";
            this.btnRunSprocopAuto.Size = new System.Drawing.Size(148, 27);
            this.btnRunSprocopAuto.TabIndex = 37;
            this.btnRunSprocopAuto.Text = "Run AutoQC";
            this.btnRunSprocopAuto.UseVisualStyleBackColor = true;
            this.btnRunSprocopAuto.Click += new System.EventHandler(this.btnRunSprocopAuto_Click);
            // 
            // tabSettings
            // 
            this.tabSettings.Controls.Add(this.groupBoxPanorama);
            this.tabSettings.Controls.Add(this.btnSave);
            this.tabSettings.Controls.Add(this.groupBox2);
            this.tabSettings.Location = new System.Drawing.Point(4, 22);
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabSettings.Size = new System.Drawing.Size(637, 546);
            this.tabSettings.TabIndex = 1;
            this.tabSettings.Text = "Settings";
            this.tabSettings.UseVisualStyleBackColor = true;
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
            this.groupBoxPanorama.Location = new System.Drawing.Point(8, 305);
            this.groupBoxPanorama.Name = "groupBoxPanorama";
            this.groupBoxPanorama.Size = new System.Drawing.Size(621, 193);
            this.groupBoxPanorama.TabIndex = 55;
            this.groupBoxPanorama.TabStop = false;
            this.groupBoxPanorama.Text = "Panorama";
            // 
            // labelPanoramaFolder
            // 
            this.labelPanoramaFolder.AutoSize = true;
            this.labelPanoramaFolder.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.labelPanoramaFolder.Location = new System.Drawing.Point(6, 135);
            this.labelPanoramaFolder.Name = "labelPanoramaFolder";
            this.labelPanoramaFolder.Size = new System.Drawing.Size(201, 13);
            this.labelPanoramaFolder.TabIndex = 13;
            this.labelPanoramaFolder.Text = "Folder on Panorama (e.g. /MacCoss/QC)";
            // 
            // textPanoramaFolder
            // 
            this.textPanoramaFolder.Location = new System.Drawing.Point(9, 151);
            this.textPanoramaFolder.Name = "textPanoramaFolder";
            this.textPanoramaFolder.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textPanoramaFolder.Size = new System.Drawing.Size(512, 20);
            this.textPanoramaFolder.TabIndex = 14;
            // 
            // lblPanoramaUrl
            // 
            this.lblPanoramaUrl.AutoSize = true;
            this.lblPanoramaUrl.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblPanoramaUrl.Location = new System.Drawing.Point(6, 24);
            this.lblPanoramaUrl.Name = "lblPanoramaUrl";
            this.lblPanoramaUrl.Size = new System.Drawing.Size(188, 13);
            this.lblPanoramaUrl.TabIndex = 7;
            this.lblPanoramaUrl.Text = "URL (e.g. https://panoramaweb.org/):";
            // 
            // textPanoramaUrl
            // 
            this.textPanoramaUrl.Location = new System.Drawing.Point(9, 40);
            this.textPanoramaUrl.Name = "textPanoramaUrl";
            this.textPanoramaUrl.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textPanoramaUrl.Size = new System.Drawing.Size(512, 20);
            this.textPanoramaUrl.TabIndex = 8;
            // 
            // textPanoramaPasswd
            // 
            this.textPanoramaPasswd.Location = new System.Drawing.Point(244, 93);
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
            this.lblPanoramaPasswd.Location = new System.Drawing.Point(241, 73);
            this.lblPanoramaPasswd.Name = "lblPanoramaPasswd";
            this.lblPanoramaPasswd.Size = new System.Drawing.Size(56, 13);
            this.lblPanoramaPasswd.TabIndex = 11;
            this.lblPanoramaPasswd.Text = "Password:";
            // 
            // lblPanoramaEmail
            // 
            this.lblPanoramaEmail.AutoSize = true;
            this.lblPanoramaEmail.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblPanoramaEmail.Location = new System.Drawing.Point(6, 75);
            this.lblPanoramaEmail.Name = "lblPanoramaEmail";
            this.lblPanoramaEmail.Size = new System.Drawing.Size(35, 13);
            this.lblPanoramaEmail.TabIndex = 9;
            this.lblPanoramaEmail.Text = "Email:";
            // 
            // textPanoramaEmail
            // 
            this.textPanoramaEmail.Location = new System.Drawing.Point(9, 93);
            this.textPanoramaEmail.Name = "textPanoramaEmail";
            this.textPanoramaEmail.Size = new System.Drawing.Size(207, 20);
            this.textPanoramaEmail.TabIndex = 10;
            // 
            // btnSave
            // 
            this.btnSave.BackColor = System.Drawing.Color.Tomato;
            this.btnSave.Location = new System.Drawing.Point(243, 504);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(126, 23);
            this.btnSave.TabIndex = 54;
            this.btnSave.Text = "Save Settings";
            this.btnSave.UseVisualStyleBackColor = false;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.btnRScriptPath);
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.textBoxRScriptPath);
            this.groupBox2.Controls.Add(this.skylineRunnerPathLabel);
            this.groupBox2.Controls.Add(this.btnFolderToWatch);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.folderToWatchPath);
            this.groupBox2.Controls.Add(this.skylineRunnerPathInput);
            this.groupBox2.Controls.Add(this.btnSkylineFilePath);
            this.groupBox2.Controls.Add(this.skylineFilePath);
            this.groupBox2.Controls.Add(this.btnSkylingRunnerPath);
            this.groupBox2.Location = new System.Drawing.Point(8, 16);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(621, 256);
            this.groupBox2.TabIndex = 53;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "System Paths";
            // 
            // btnRScriptPath
            // 
            this.btnRScriptPath.Location = new System.Drawing.Point(541, 206);
            this.btnRScriptPath.Name = "btnRScriptPath";
            this.btnRScriptPath.Size = new System.Drawing.Size(29, 23);
            this.btnRScriptPath.TabIndex = 53;
            this.btnRScriptPath.Text = "...";
            this.btnRScriptPath.UseVisualStyleBackColor = true;
            this.btnRScriptPath.Click += new System.EventHandler(this.btnRScriptPath_Click);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.Location = new System.Drawing.Point(6, 188);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(144, 15);
            this.label10.TabIndex = 51;
            this.label10.Text = "R(3.0.2) RScript.exe path";
            // 
            // textBoxRScriptPath
            // 
            this.textBoxRScriptPath.Location = new System.Drawing.Point(7, 209);
            this.textBoxRScriptPath.Name = "textBoxRScriptPath";
            this.textBoxRScriptPath.Size = new System.Drawing.Size(514, 20);
            this.textBoxRScriptPath.TabIndex = 52;
            // 
            // skylineRunnerPathLabel
            // 
            this.skylineRunnerPathLabel.AutoSize = true;
            this.skylineRunnerPathLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.skylineRunnerPathLabel.ForeColor = System.Drawing.SystemColors.Desktop;
            this.skylineRunnerPathLabel.Location = new System.Drawing.Point(6, 20);
            this.skylineRunnerPathLabel.Name = "skylineRunnerPathLabel";
            this.skylineRunnerPathLabel.Size = new System.Drawing.Size(121, 15);
            this.skylineRunnerPathLabel.TabIndex = 27;
            this.skylineRunnerPathLabel.Text = "Skyline Runner Path:";
            // 
            // btnFolderToWatch
            // 
            this.btnFolderToWatch.Location = new System.Drawing.Point(541, 150);
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
            this.label2.Location = new System.Drawing.Point(6, 77);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(97, 15);
            this.label2.TabIndex = 28;
            this.label2.Text = "Skyline File Path";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(6, 132);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(92, 15);
            this.label3.TabIndex = 29;
            this.label3.Text = "Folder to Watch";
            // 
            // folderToWatchPath
            // 
            this.folderToWatchPath.Location = new System.Drawing.Point(7, 153);
            this.folderToWatchPath.Name = "folderToWatchPath";
            this.folderToWatchPath.Size = new System.Drawing.Size(514, 20);
            this.folderToWatchPath.TabIndex = 30;
            // 
            // skylineRunnerPathInput
            // 
            this.skylineRunnerPathInput.Location = new System.Drawing.Point(7, 38);
            this.skylineRunnerPathInput.Name = "skylineRunnerPathInput";
            this.skylineRunnerPathInput.Size = new System.Drawing.Size(514, 20);
            this.skylineRunnerPathInput.TabIndex = 31;
            // 
            // btnSkylineFilePath
            // 
            this.btnSkylineFilePath.Location = new System.Drawing.Point(541, 94);
            this.btnSkylineFilePath.Name = "btnSkylineFilePath";
            this.btnSkylineFilePath.Size = new System.Drawing.Size(29, 23);
            this.btnSkylineFilePath.TabIndex = 46;
            this.btnSkylineFilePath.Text = "...";
            this.btnSkylineFilePath.UseVisualStyleBackColor = true;
            this.btnSkylineFilePath.Click += new System.EventHandler(this.btnSkylineFilePath_Click);
            // 
            // skylineFilePath
            // 
            this.skylineFilePath.Location = new System.Drawing.Point(7, 95);
            this.skylineFilePath.Name = "skylineFilePath";
            this.skylineFilePath.Size = new System.Drawing.Size(514, 20);
            this.skylineFilePath.TabIndex = 32;
            // 
            // btnSkylingRunnerPath
            // 
            this.btnSkylingRunnerPath.Location = new System.Drawing.Point(541, 37);
            this.btnSkylingRunnerPath.Name = "btnSkylingRunnerPath";
            this.btnSkylingRunnerPath.Size = new System.Drawing.Size(29, 23);
            this.btnSkylingRunnerPath.TabIndex = 45;
            this.btnSkylingRunnerPath.Text = "...";
            this.btnSkylingRunnerPath.UseVisualStyleBackColor = true;
            this.btnSkylingRunnerPath.Click += new System.EventHandler(this.btnSkylingRunnerPath_Click);
            // 
            // tabSprocopSettings
            // 
            this.tabSprocopSettings.Controls.Add(this.groupBox1);
            this.tabSprocopSettings.Location = new System.Drawing.Point(4, 22);
            this.tabSprocopSettings.Name = "tabSprocopSettings";
            this.tabSprocopSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabSprocopSettings.Size = new System.Drawing.Size(637, 546);
            this.tabSprocopSettings.TabIndex = 3;
            this.tabSprocopSettings.Text = "SProCoP Settings";
            this.tabSprocopSettings.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.checkBoxIsHighRes);
            this.groupBox1.Controls.Add(this.numericUpDownMMA);
            this.groupBox1.Controls.Add(this.labelMMA);
            this.groupBox1.Controls.Add(this.labelThreshold);
            this.groupBox1.Controls.Add(this.numericUpDownThreshold);
            this.groupBox1.Location = new System.Drawing.Point(19, 21);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(283, 147);
            this.groupBox1.TabIndex = 53;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "SProCoP";
            // 
            // checkBoxIsHighRes
            // 
            this.checkBoxIsHighRes.AutoSize = true;
            this.checkBoxIsHighRes.ForeColor = System.Drawing.Color.Black;
            this.checkBoxIsHighRes.Location = new System.Drawing.Point(12, 67);
            this.checkBoxIsHighRes.Name = "checkBoxIsHighRes";
            this.checkBoxIsHighRes.Size = new System.Drawing.Size(124, 17);
            this.checkBoxIsHighRes.TabIndex = 56;
            this.checkBoxIsHighRes.Text = "Is high resolution MS";
            this.checkBoxIsHighRes.UseVisualStyleBackColor = true;
            // 
            // numericUpDownMMA
            // 
            this.numericUpDownMMA.Location = new System.Drawing.Point(12, 110);
            this.numericUpDownMMA.Name = "numericUpDownMMA";
            this.numericUpDownMMA.Size = new System.Drawing.Size(36, 20);
            this.numericUpDownMMA.TabIndex = 55;
            // 
            // labelMMA
            // 
            this.labelMMA.AutoSize = true;
            this.labelMMA.ForeColor = System.Drawing.Color.Black;
            this.labelMMA.Location = new System.Drawing.Point(9, 94);
            this.labelMMA.Name = "labelMMA";
            this.labelMMA.Size = new System.Drawing.Size(65, 13);
            this.labelMMA.TabIndex = 54;
            this.labelMMA.Text = "MMA Value:";
            // 
            // labelThreshold
            // 
            this.labelThreshold.AutoSize = true;
            this.labelThreshold.ForeColor = System.Drawing.Color.Black;
            this.labelThreshold.Location = new System.Drawing.Point(9, 18);
            this.labelThreshold.Name = "labelThreshold";
            this.labelThreshold.Size = new System.Drawing.Size(57, 13);
            this.labelThreshold.TabIndex = 53;
            this.labelThreshold.Text = "Threshold:";
            // 
            // numericUpDownThreshold
            // 
            this.numericUpDownThreshold.Location = new System.Drawing.Point(12, 34);
            this.numericUpDownThreshold.Name = "numericUpDownThreshold";
            this.numericUpDownThreshold.Size = new System.Drawing.Size(36, 20);
            this.numericUpDownThreshold.TabIndex = 52;
            // 
            // tabInstructions
            // 
            this.tabInstructions.Controls.Add(this.label5);
            this.tabInstructions.Location = new System.Drawing.Point(4, 22);
            this.tabInstructions.Name = "tabInstructions";
            this.tabInstructions.Padding = new System.Windows.Forms.Padding(3);
            this.tabInstructions.Size = new System.Drawing.Size(637, 546);
            this.tabInstructions.TabIndex = 2;
            this.tabInstructions.Text = "Instructions";
            this.tabInstructions.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(3, 3);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(588, 368);
            this.label5.TabIndex = 44;
            this.label5.Text = resources.GetString("label5.Text");
            // 
            // AutoQc
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(645, 572);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "AutoQc";
            this.Text = " AutoQC";
            this.tabControl1.ResumeLayout(false);
            this.tabRunSetup.ResumeLayout(false);
            this.tabRunSetup.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.runGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.statusImg)).EndInit();
            this.tabSettings.ResumeLayout(false);
            this.groupBoxPanorama.ResumeLayout(false);
            this.groupBoxPanorama.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.tabSprocopSettings.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMMA)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreshold)).EndInit();
            this.tabInstructions.ResumeLayout(false);
            this.tabInstructions.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabRunSetup;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.Button btnRunSprocopAuto;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.PictureBox statusImg;
        private System.Windows.Forms.TextBox textBoxLog;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnRScriptPath;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox textBoxRScriptPath;
        private System.Windows.Forms.Label skylineRunnerPathLabel;
        private System.Windows.Forms.Button btnFolderToWatch;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox folderToWatchPath;
        private System.Windows.Forms.TextBox skylineRunnerPathInput;
        private System.Windows.Forms.Button btnSkylineFilePath;
        private System.Windows.Forms.TextBox skylineFilePath;
        private System.Windows.Forms.Button btnSkylingRunnerPath;
        private System.Windows.Forms.TabPage tabInstructions;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button buttonClear;
        private System.Windows.Forms.DataGridView runGridView;
        private System.Windows.Forms.DataGridViewComboBoxColumn Type;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnFile;
        private System.Windows.Forms.ComboBox comboBoxType;
        private System.Windows.Forms.Label labelType;
        private System.Windows.Forms.Label labelNumberOfRows;
        private System.Windows.Forms.TextBox textBoxNewRows;
        private System.Windows.Forms.Button btnAddRows;
        private System.Windows.Forms.TabPage tabSprocopSettings;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox checkBoxIsHighRes;
        private System.Windows.Forms.NumericUpDown numericUpDownMMA;
        private System.Windows.Forms.Label labelMMA;
        private System.Windows.Forms.Label labelThreshold;
        private System.Windows.Forms.NumericUpDown numericUpDownThreshold;
        private System.Windows.Forms.GroupBox groupBoxPanorama;
        private System.Windows.Forms.Label lblPanoramaUrl;
        private System.Windows.Forms.TextBox textPanoramaUrl;
        internal System.Windows.Forms.TextBox textPanoramaPasswd;
        private System.Windows.Forms.Label lblPanoramaPasswd;
        private System.Windows.Forms.Label lblPanoramaEmail;
        internal System.Windows.Forms.TextBox textPanoramaEmail;
        private System.Windows.Forms.Label labelPanoramaFolder;
        private System.Windows.Forms.TextBox textPanoramaFolder;
    }
}