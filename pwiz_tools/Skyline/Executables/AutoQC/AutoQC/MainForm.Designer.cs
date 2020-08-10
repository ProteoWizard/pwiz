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
            this.btnImportConfigs = new System.Windows.Forms.Button();
            this.btnExportConfigs = new System.Windows.Forms.Button();
            this.tabLog = new System.Windows.Forms.TabPage();
            this.btnOpenFolder = new System.Windows.Forms.Button();
            this.btnViewLog2 = new System.Windows.Forms.Button();
            this.lblConfigSelect = new System.Windows.Forms.Label();
            this.textBoxLog = new System.Windows.Forms.RichTextBox();
            this.comboConfigs = new System.Windows.Forms.ComboBox();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.radioButtonUseSkylineDaily = new System.Windows.Forms.RadioButton();
            this.radioButtonUseSkyline = new System.Windows.Forms.RadioButton();
            this.buttonFileDialogSkylineInstall = new System.Windows.Forms.Button();
            this.radioButtonWebBasedSkylinePath = new System.Windows.Forms.RadioButton();
            this.buttonSkylinePathApply = new System.Windows.Forms.Button();
            this.radioButtonSpecifySkylinePath = new System.Windows.Forms.RadioButton();
            this.textBoxSkylineCmdPath = new System.Windows.Forms.TextBox();
            this.label_Skylinecmd = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cb_minimizeToSysTray = new System.Windows.Forms.CheckBox();
            this.cb_keepRunning = new System.Windows.Forms.CheckBox();
            this.toolTip_MainForm = new System.Windows.Forms.ToolTip(this.components);
            this.systray_icon = new System.Windows.Forms.NotifyIcon(this.components);
            this.tabMain.SuspendLayout();
            this.tabFront.SuspendLayout();
            this.tabLog.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.panel1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnEdit
            // 
            this.btnEdit.Location = new System.Drawing.Point(603, 111);
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Size = new System.Drawing.Size(75, 23);
            this.btnEdit.TabIndex = 2;
            this.btnEdit.Text = "Edit";
            this.btnEdit.UseVisualStyleBackColor = true;
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(603, 193);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(75, 23);
            this.btnDelete.TabIndex = 3;
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnCopy
            // 
            this.btnCopy.Location = new System.Drawing.Point(603, 153);
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Size = new System.Drawing.Size(75, 23);
            this.btnCopy.TabIndex = 4;
            this.btnCopy.Text = "Copy";
            this.btnCopy.UseVisualStyleBackColor = true;
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            // 
            // lblNoConfigs
            // 
            this.lblNoConfigs.AutoSize = true;
            this.lblNoConfigs.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblNoConfigs.ForeColor = System.Drawing.Color.Blue;
            this.lblNoConfigs.Location = new System.Drawing.Point(81, 440);
            this.lblNoConfigs.Name = "lblNoConfigs";
            this.lblNoConfigs.Size = new System.Drawing.Size(521, 16);
            this.lblNoConfigs.TabIndex = 5;
            this.lblNoConfigs.Text = "There are no saved configurations. Click the button below to create a new configu" +
    "ration.";
            // 
            // btnNewConfig
            // 
            this.btnNewConfig.Location = new System.Drawing.Point(260, 468);
            this.btnNewConfig.Name = "btnNewConfig";
            this.btnNewConfig.Size = new System.Drawing.Size(202, 23);
            this.btnNewConfig.TabIndex = 6;
            this.btnNewConfig.Text = "Create a new configuration";
            this.btnNewConfig.UseVisualStyleBackColor = true;
            this.btnNewConfig.Click += new System.EventHandler(this.btnNewConfig_Click);
            // 
            // listViewConfigs
            // 
            this.listViewConfigs.CheckBoxes = true;
            this.listViewConfigs.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.listViewConfigName,
            this.listViewUser,
            this.listViewCreated,
            this.listViewStatus});
            this.listViewConfigs.FullRowSelect = true;
            this.listViewConfigs.HideSelection = false;
            this.listViewConfigs.Location = new System.Drawing.Point(59, 54);
            this.listViewConfigs.MultiSelect = false;
            this.listViewConfigs.Name = "listViewConfigs";
            this.listViewConfigs.Size = new System.Drawing.Size(527, 373);
            this.listViewConfigs.TabIndex = 7;
            this.listViewConfigs.UseCompatibleStateImageBehavior = false;
            this.listViewConfigs.View = System.Windows.Forms.View.Details;
            this.listViewConfigs.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listViewConfigs_ColumnClick);
            this.listViewConfigs.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listViewConfigs_ItemCheck);
            this.listViewConfigs.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.listViewConfigs_ItemChecked);
            this.listViewConfigs.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listViewConfigs_ItemSelectionChanged);
            // 
            // listViewConfigName
            // 
            this.listViewConfigName.Text = "Configuration";
            this.listViewConfigName.Width = 257;
            // 
            // listViewUser
            // 
            this.listViewUser.Text = "User";
            this.listViewUser.Width = 117;
            // 
            // listViewCreated
            // 
            this.listViewCreated.Text = "Created";
            this.listViewCreated.Width = 80;
            // 
            // listViewStatus
            // 
            this.listViewStatus.Text = "Status";
            this.listViewStatus.Width = 69;
            // 
            // labelSavedConfigurations
            // 
            this.labelSavedConfigurations.AutoSize = true;
            this.labelSavedConfigurations.Location = new System.Drawing.Point(56, 35);
            this.labelSavedConfigurations.Name = "labelSavedConfigurations";
            this.labelSavedConfigurations.Size = new System.Drawing.Size(110, 13);
            this.labelSavedConfigurations.TabIndex = 8;
            this.labelSavedConfigurations.Text = "Saved configurations:";
            // 
            // btnViewLog1
            // 
            this.btnViewLog1.Location = new System.Drawing.Point(603, 286);
            this.btnViewLog1.Name = "btnViewLog1";
            this.btnViewLog1.Size = new System.Drawing.Size(75, 23);
            this.btnViewLog1.TabIndex = 9;
            this.btnViewLog1.Text = "View log";
            this.btnViewLog1.UseVisualStyleBackColor = true;
            this.btnViewLog1.Click += new System.EventHandler(this.btnViewLog1_Click);
            // 
            // tabMain
            // 
            this.tabMain.Controls.Add(this.tabFront);
            this.tabMain.Controls.Add(this.tabLog);
            this.tabMain.Controls.Add(this.tabSettings);
            this.tabMain.Location = new System.Drawing.Point(12, 12);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(739, 551);
            this.tabMain.TabIndex = 10;
            this.tabMain.SelectedIndexChanged += new System.EventHandler(this.Tab_Click);
            // 
            // tabFront
            // 
            this.tabFront.BackColor = System.Drawing.Color.Transparent;
            this.tabFront.Controls.Add(this.btnImportConfigs);
            this.tabFront.Controls.Add(this.btnExportConfigs);
            this.tabFront.Controls.Add(this.listViewConfigs);
            this.tabFront.Controls.Add(this.labelSavedConfigurations);
            this.tabFront.Controls.Add(this.btnViewLog1);
            this.tabFront.Controls.Add(this.lblNoConfigs);
            this.tabFront.Controls.Add(this.btnNewConfig);
            this.tabFront.Controls.Add(this.btnEdit);
            this.tabFront.Controls.Add(this.btnCopy);
            this.tabFront.Controls.Add(this.btnDelete);
            this.tabFront.Location = new System.Drawing.Point(4, 22);
            this.tabFront.Name = "tabFront";
            this.tabFront.Padding = new System.Windows.Forms.Padding(3);
            this.tabFront.Size = new System.Drawing.Size(731, 525);
            this.tabFront.TabIndex = 0;
            this.tabFront.Text = "Configurations";
            // 
            // btnImportConfigs
            // 
            this.btnImportConfigs.Location = new System.Drawing.Point(603, 380);
            this.btnImportConfigs.Name = "btnImportConfigs";
            this.btnImportConfigs.Size = new System.Drawing.Size(75, 23);
            this.btnImportConfigs.TabIndex = 11;
            this.btnImportConfigs.Text = "Import...";
            this.toolTip_MainForm.SetToolTip(this.btnImportConfigs, "Import saved configurations...");
            this.btnImportConfigs.UseVisualStyleBackColor = true;
            this.btnImportConfigs.Click += new System.EventHandler(this.btnImport_Click);
            // 
            // btnExportConfigs
            // 
            this.btnExportConfigs.Location = new System.Drawing.Point(603, 340);
            this.btnExportConfigs.Name = "btnExportConfigs";
            this.btnExportConfigs.Size = new System.Drawing.Size(75, 23);
            this.btnExportConfigs.TabIndex = 10;
            this.btnExportConfigs.Text = "Export...";
            this.toolTip_MainForm.SetToolTip(this.btnExportConfigs, "Export saved configurations...");
            this.btnExportConfigs.UseVisualStyleBackColor = true;
            this.btnExportConfigs.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // tabLog
            // 
            this.tabLog.BackColor = System.Drawing.Color.Transparent;
            this.tabLog.Controls.Add(this.btnOpenFolder);
            this.tabLog.Controls.Add(this.btnViewLog2);
            this.tabLog.Controls.Add(this.lblConfigSelect);
            this.tabLog.Controls.Add(this.textBoxLog);
            this.tabLog.Controls.Add(this.comboConfigs);
            this.tabLog.Location = new System.Drawing.Point(4, 22);
            this.tabLog.Name = "tabLog";
            this.tabLog.Padding = new System.Windows.Forms.Padding(3);
            this.tabLog.Size = new System.Drawing.Size(731, 525);
            this.tabLog.TabIndex = 1;
            this.tabLog.Text = "Log";
            // 
            // btnOpenFolder
            // 
            this.btnOpenFolder.Location = new System.Drawing.Point(627, 19);
            this.btnOpenFolder.Name = "btnOpenFolder";
            this.btnOpenFolder.Size = new System.Drawing.Size(81, 23);
            this.btnOpenFolder.TabIndex = 5;
            this.btnOpenFolder.Text = "Open folder";
            this.btnOpenFolder.UseVisualStyleBackColor = true;
            this.btnOpenFolder.Click += new System.EventHandler(this.btnOpenFolder_Click);
            // 
            // btnViewLog2
            // 
            this.btnViewLog2.Location = new System.Drawing.Point(546, 19);
            this.btnViewLog2.Name = "btnViewLog2";
            this.btnViewLog2.Size = new System.Drawing.Size(75, 23);
            this.btnViewLog2.TabIndex = 4;
            this.btnViewLog2.Text = "View log";
            this.btnViewLog2.UseVisualStyleBackColor = true;
            this.btnViewLog2.Click += new System.EventHandler(this.btnViewLog2_Click);
            // 
            // lblConfigSelect
            // 
            this.lblConfigSelect.AutoSize = true;
            this.lblConfigSelect.Location = new System.Drawing.Point(21, 19);
            this.lblConfigSelect.Name = "lblConfigSelect";
            this.lblConfigSelect.Size = new System.Drawing.Size(72, 13);
            this.lblConfigSelect.TabIndex = 2;
            this.lblConfigSelect.Text = "Configuration:";
            // 
            // textBoxLog
            // 
            this.textBoxLog.Location = new System.Drawing.Point(24, 59);
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.ReadOnly = true;
            this.textBoxLog.Size = new System.Drawing.Size(684, 446);
            this.textBoxLog.TabIndex = 1;
            this.textBoxLog.Text = "";
            // 
            // comboConfigs
            // 
            this.comboConfigs.FormattingEnabled = true;
            this.comboConfigs.Location = new System.Drawing.Point(114, 19);
            this.comboConfigs.Name = "comboConfigs";
            this.comboConfigs.Size = new System.Drawing.Size(414, 21);
            this.comboConfigs.TabIndex = 0;
            // 
            // tabSettings
            // 
            this.tabSettings.BackColor = System.Drawing.SystemColors.Control;
            this.tabSettings.Controls.Add(this.groupBox3);
            this.tabSettings.Controls.Add(this.label_Skylinecmd);
            this.tabSettings.Controls.Add(this.groupBox1);
            this.tabSettings.Location = new System.Drawing.Point(4, 22);
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabSettings.Size = new System.Drawing.Size(731, 525);
            this.tabSettings.TabIndex = 2;
            this.tabSettings.Text = "Settings";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.panel1);
            this.groupBox3.Controls.Add(this.buttonFileDialogSkylineInstall);
            this.groupBox3.Controls.Add(this.radioButtonWebBasedSkylinePath);
            this.groupBox3.Controls.Add(this.buttonSkylinePathApply);
            this.groupBox3.Controls.Add(this.radioButtonSpecifySkylinePath);
            this.groupBox3.Controls.Add(this.textBoxSkylineCmdPath);
            this.groupBox3.Location = new System.Drawing.Point(53, 27);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(629, 196);
            this.groupBox3.TabIndex = 9;
            this.groupBox3.TabStop = false;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.radioButtonUseSkylineDaily);
            this.panel1.Controls.Add(this.radioButtonUseSkyline);
            this.panel1.Location = new System.Drawing.Point(6, 19);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(611, 39);
            this.panel1.TabIndex = 10;
            // 
            // radioButtonUseSkylineDaily
            // 
            this.radioButtonUseSkylineDaily.AutoSize = true;
            this.radioButtonUseSkylineDaily.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.radioButtonUseSkylineDaily.Location = new System.Drawing.Point(122, 14);
            this.radioButtonUseSkylineDaily.Name = "radioButtonUseSkylineDaily";
            this.radioButtonUseSkylineDaily.Size = new System.Drawing.Size(105, 17);
            this.radioButtonUseSkylineDaily.TabIndex = 8;
            this.radioButtonUseSkylineDaily.TabStop = true;
            this.radioButtonUseSkylineDaily.Text = "Use Skyline-daily";
            this.radioButtonUseSkylineDaily.UseVisualStyleBackColor = true;
            this.radioButtonUseSkylineDaily.CheckedChanged += new System.EventHandler(this.TypeSkylineDaily_Click);
            // 
            // radioButtonUseSkyline
            // 
            this.radioButtonUseSkyline.AutoSize = true;
            this.radioButtonUseSkyline.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.radioButtonUseSkyline.Location = new System.Drawing.Point(4, 14);
            this.radioButtonUseSkyline.Name = "radioButtonUseSkyline";
            this.radioButtonUseSkyline.Size = new System.Drawing.Size(81, 17);
            this.radioButtonUseSkyline.TabIndex = 7;
            this.radioButtonUseSkyline.TabStop = true;
            this.radioButtonUseSkyline.Text = "Use Skyline";
            this.radioButtonUseSkyline.UseVisualStyleBackColor = true;
            this.radioButtonUseSkyline.CheckedChanged += new System.EventHandler(this.TypeSkyline_Click);
            // 
            // buttonFileDialogSkylineInstall
            // 
            this.buttonFileDialogSkylineInstall.Enabled = false;
            this.buttonFileDialogSkylineInstall.Location = new System.Drawing.Point(562, 122);
            this.buttonFileDialogSkylineInstall.Name = "buttonFileDialogSkylineInstall";
            this.buttonFileDialogSkylineInstall.Size = new System.Drawing.Size(26, 20);
            this.buttonFileDialogSkylineInstall.TabIndex = 11;
            this.buttonFileDialogSkylineInstall.Text = "...";
            this.buttonFileDialogSkylineInstall.UseVisualStyleBackColor = true;
            this.buttonFileDialogSkylineInstall.Click += new System.EventHandler(this.buttonFileDialogSkylineInstall_click);
            // 
            // radioButtonWebBasedSkylinePath
            // 
            this.radioButtonWebBasedSkylinePath.AutoSize = true;
            this.radioButtonWebBasedSkylinePath.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
            this.radioButtonWebBasedSkylinePath.Location = new System.Drawing.Point(10, 76);
            this.radioButtonWebBasedSkylinePath.Name = "radioButtonWebBasedSkylinePath";
            this.radioButtonWebBasedSkylinePath.Size = new System.Drawing.Size(188, 17);
            this.radioButtonWebBasedSkylinePath.TabIndex = 9;
            this.radioButtonWebBasedSkylinePath.TabStop = true;
            this.radioButtonWebBasedSkylinePath.Text = "Use web-based Skyline installation";
            this.radioButtonWebBasedSkylinePath.UseVisualStyleBackColor = true;
            this.radioButtonWebBasedSkylinePath.CheckedChanged += new System.EventHandler(this.WebBasedInstall_Click);
            // 
            // buttonSkylinePathApply
            // 
            this.buttonSkylinePathApply.Location = new System.Drawing.Point(267, 159);
            this.buttonSkylinePathApply.Name = "buttonSkylinePathApply";
            this.buttonSkylinePathApply.Size = new System.Drawing.Size(75, 26);
            this.buttonSkylinePathApply.TabIndex = 6;
            this.buttonSkylinePathApply.Text = "Apply";
            this.buttonSkylinePathApply.UseVisualStyleBackColor = true;
            this.buttonSkylinePathApply.Click += new System.EventHandler(this.ApplySkylinePath_Click);
            // 
            // radioButtonSpecifySkylinePath
            // 
            this.radioButtonSpecifySkylinePath.AutoSize = true;
            this.radioButtonSpecifySkylinePath.CheckAlign = System.Drawing.ContentAlignment.BottomLeft;
            this.radioButtonSpecifySkylinePath.Location = new System.Drawing.Point(10, 99);
            this.radioButtonSpecifySkylinePath.Name = "radioButtonSpecifySkylinePath";
            this.radioButtonSpecifySkylinePath.Size = new System.Drawing.Size(192, 17);
            this.radioButtonSpecifySkylinePath.TabIndex = 10;
            this.radioButtonSpecifySkylinePath.TabStop = true;
            this.radioButtonSpecifySkylinePath.Text = "Specify Skyline installation directory";
            this.radioButtonSpecifySkylinePath.UseVisualStyleBackColor = true;
            this.radioButtonSpecifySkylinePath.CheckedChanged += new System.EventHandler(this.SpecifiyInstall_Click);
            // 
            // textBoxSkylineCmdPath
            // 
            this.textBoxSkylineCmdPath.Enabled = false;
            this.textBoxSkylineCmdPath.Location = new System.Drawing.Point(10, 122);
            this.textBoxSkylineCmdPath.Name = "textBoxSkylineCmdPath";
            this.textBoxSkylineCmdPath.Size = new System.Drawing.Size(546, 20);
            this.textBoxSkylineCmdPath.TabIndex = 4;
            // 
            // label_Skylinecmd
            // 
            this.label_Skylinecmd.AutoSize = true;
            this.label_Skylinecmd.Location = new System.Drawing.Point(53, 147);
            this.label_Skylinecmd.Name = "label_Skylinecmd";
            this.label_Skylinecmd.Size = new System.Drawing.Size(0, 13);
            this.label_Skylinecmd.TabIndex = 5;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cb_minimizeToSysTray);
            this.groupBox1.Controls.Add(this.cb_keepRunning);
            this.groupBox1.Location = new System.Drawing.Point(53, 257);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(629, 81);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            // 
            // cb_minimizeToSysTray
            // 
            this.cb_minimizeToSysTray.AutoSize = true;
            this.cb_minimizeToSysTray.Location = new System.Drawing.Point(14, 48);
            this.cb_minimizeToSysTray.Name = "cb_minimizeToSysTray";
            this.cb_minimizeToSysTray.Size = new System.Drawing.Size(227, 17);
            this.cb_minimizeToSysTray.TabIndex = 2;
            this.cb_minimizeToSysTray.Text = "Minimize program to Windows System Tray";
            this.cb_minimizeToSysTray.UseVisualStyleBackColor = true;
            // 
            // cb_keepRunning
            // 
            this.cb_keepRunning.AutoSize = true;
            this.cb_keepRunning.Location = new System.Drawing.Point(14, 19);
            this.cb_keepRunning.Name = "cb_keepRunning";
            this.cb_keepRunning.Size = new System.Drawing.Size(165, 17);
            this.cb_keepRunning.TabIndex = 0;
            this.cb_keepRunning.Text = "Keep AutoQC Loader running\r\n";
            this.toolTip_MainForm.SetToolTip(this.cb_keepRunning, resources.GetString("cb_keepRunning.ToolTip"));
            this.cb_keepRunning.UseVisualStyleBackColor = true;
            // 
            // systray_icon
            // 
            this.systray_icon.Icon = ((System.Drawing.Icon)(resources.GetObject("systray_icon.Icon")));
            this.systray_icon.Text = "AutoQC Loader";
            this.systray_icon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.systray_icon_MouseDoubleClick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(763, 573);
            this.Controls.Add(this.tabMain);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = " AutoQC";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.tabMain.ResumeLayout(false);
            this.tabFront.ResumeLayout(false);
            this.tabFront.PerformLayout();
            this.tabLog.ResumeLayout(false);
            this.tabLog.PerformLayout();
            this.tabSettings.ResumeLayout(false);
            this.tabSettings.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
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
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox cb_keepRunning;
        private System.Windows.Forms.NotifyIcon systray_icon;
        private System.Windows.Forms.Button buttonSkylinePathApply;
        private System.Windows.Forms.Label label_Skylinecmd;
        private System.Windows.Forms.TextBox textBoxSkylineCmdPath;
        private System.Windows.Forms.Button buttonFileDialogSkylineInstall;
        private System.Windows.Forms.RadioButton radioButtonSpecifySkylinePath;
        private System.Windows.Forms.RadioButton radioButtonWebBasedSkylinePath;
        private System.Windows.Forms.RadioButton radioButtonUseSkylineDaily;
        private System.Windows.Forms.RadioButton radioButtonUseSkyline;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Panel panel1;
    }
}