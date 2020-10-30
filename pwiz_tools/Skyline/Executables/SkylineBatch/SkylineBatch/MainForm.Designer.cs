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
            this.cb_keepRunning = new System.Windows.Forms.CheckBox();
            this.btnExportConfigs = new System.Windows.Forms.Button();
            this.btnImportConfigs = new System.Windows.Forms.Button();
            this.btnRunBatch = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.systray_icon = new System.Windows.Forms.NotifyIcon(this.components);
            this.cb_minimizeToSysTray = new System.Windows.Forms.CheckBox();
            this.textBoxSkylinePath = new System.Windows.Forms.TextBox();
            this.radioButtonSpecifySkylinePath = new System.Windows.Forms.RadioButton();
            this.buttonApplySkylineSettings = new System.Windows.Forms.Button();
            this.radioButtonWebBasedSkyline = new System.Windows.Forms.RadioButton();
            this.buttonFileDialogSkylineInstall = new System.Windows.Forms.Button();
            this.panelSkylineType = new System.Windows.Forms.Panel();
            this.radioButtonUseSkyline = new System.Windows.Forms.RadioButton();
            this.radioButtonUseSkylineDaily = new System.Windows.Forms.RadioButton();
            this.tabLog = new System.Windows.Forms.TabPage();
            this.textBoxLog = new System.Windows.Forms.RichTextBox();
            this.tabFront = new System.Windows.Forms.TabPage();
            this.listViewConfigs = new System.Windows.Forms.ListView();
            this.listViewConfigName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewCreated = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.labelSavedConfigurations = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnRunOptions = new System.Windows.Forms.Button();
            this.lblNoConfigs = new System.Windows.Forms.Label();
            this.btnNewConfig = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnViewLog1 = new System.Windows.Forms.Button();
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnCopy = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.tabMain = new System.Windows.Forms.TabControl();
            this.batchRunDropDown = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.startFromStepOne = new System.Windows.Forms.ToolStripMenuItem();
            this.startFromStepTwo = new System.Windows.Forms.ToolStripMenuItem();
            this.startFromStepThree = new System.Windows.Forms.ToolStripMenuItem();
            this.startFromStepFour = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnUpArrow = new System.Windows.Forms.ToolStripButton();
            this.btnDownArrow = new System.Windows.Forms.ToolStripButton();
            this.tabLog.SuspendLayout();
            this.tabFront.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
            this.tabMain.SuspendLayout();
            this.batchRunDropDown.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // cb_keepRunning
            // 
            this.cb_keepRunning.AutoSize = true;
            this.cb_keepRunning.Location = new System.Drawing.Point(14, 22);
            this.cb_keepRunning.Name = "cb_keepRunning";
            this.cb_keepRunning.Size = new System.Drawing.Size(165, 17);
            this.cb_keepRunning.TabIndex = 0;
            this.cb_keepRunning.Text = "Keep AutoQC Loader running\r\n";
            this.toolTip_MainForm.SetToolTip(this.cb_keepRunning, resources.GetString("cb_keepRunning.ToolTip"));
            this.cb_keepRunning.UseVisualStyleBackColor = true;
            // 
            // btnExportConfigs
            // 
            this.btnExportConfigs.Location = new System.Drawing.Point(46, 255);
            this.btnExportConfigs.Name = "btnExportConfigs";
            this.btnExportConfigs.Size = new System.Drawing.Size(75, 23);
            this.btnExportConfigs.TabIndex = 10;
            this.btnExportConfigs.Text = "E&xport...";
            this.toolTip_MainForm.SetToolTip(this.btnExportConfigs, "Export saved configurations...");
            this.btnExportConfigs.UseVisualStyleBackColor = true;
            this.btnExportConfigs.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // btnImportConfigs
            // 
            this.btnImportConfigs.Location = new System.Drawing.Point(46, 295);
            this.btnImportConfigs.Name = "btnImportConfigs";
            this.btnImportConfigs.Size = new System.Drawing.Size(75, 23);
            this.btnImportConfigs.TabIndex = 11;
            this.btnImportConfigs.Text = "&Import...";
            this.toolTip_MainForm.SetToolTip(this.btnImportConfigs, "Import saved configurations...");
            this.btnImportConfigs.UseVisualStyleBackColor = true;
            this.btnImportConfigs.Click += new System.EventHandler(this.btnImport_Click);
            // 
            // btnRunBatch
            // 
            this.btnRunBatch.Location = new System.Drawing.Point(160, 70);
            this.btnRunBatch.Name = "btnRunBatch";
            this.btnRunBatch.Size = new System.Drawing.Size(202, 23);
            this.btnRunBatch.TabIndex = 12;
            this.btnRunBatch.Text = "&Run all steps";
            this.toolTip_MainForm.SetToolTip(this.btnRunBatch, "Run selected configurations");
            this.btnRunBatch.UseVisualStyleBackColor = true;
            this.btnRunBatch.Click += new System.EventHandler(this.btnRunBatch_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(46, 442);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 12;
            this.btnCancel.Text = "S&top";
            this.toolTip_MainForm.SetToolTip(this.btnCancel, "Import saved configurations...");
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // systray_icon
            // 
            this.systray_icon.Icon = ((System.Drawing.Icon)(resources.GetObject("systray_icon.Icon")));
            this.systray_icon.Text = "SkylineBatch";
            this.systray_icon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.systray_icon_MouseDoubleClick);
            // 
            // cb_minimizeToSysTray
            // 
            this.cb_minimizeToSysTray.AutoSize = true;
            this.cb_minimizeToSysTray.Location = new System.Drawing.Point(14, 51);
            this.cb_minimizeToSysTray.Name = "cb_minimizeToSysTray";
            this.cb_minimizeToSysTray.Size = new System.Drawing.Size(227, 17);
            this.cb_minimizeToSysTray.TabIndex = 2;
            this.cb_minimizeToSysTray.Text = "Minimize program to Windows System Tray";
            this.cb_minimizeToSysTray.UseVisualStyleBackColor = true;
            // 
            // textBoxSkylinePath
            // 
            this.textBoxSkylinePath.Enabled = false;
            this.textBoxSkylinePath.Location = new System.Drawing.Point(10, 122);
            this.textBoxSkylinePath.Name = "textBoxSkylinePath";
            this.textBoxSkylinePath.Size = new System.Drawing.Size(546, 20);
            this.textBoxSkylinePath.TabIndex = 4;
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
            this.radioButtonSpecifySkylinePath.CheckedChanged += new System.EventHandler(this.SpecifyInstall_Click);
            // 
            // buttonApplySkylineSettings
            // 
            this.buttonApplySkylineSettings.Location = new System.Drawing.Point(267, 159);
            this.buttonApplySkylineSettings.Name = "buttonApplySkylineSettings";
            this.buttonApplySkylineSettings.Size = new System.Drawing.Size(75, 26);
            this.buttonApplySkylineSettings.TabIndex = 6;
            this.buttonApplySkylineSettings.Text = "Apply";
            this.buttonApplySkylineSettings.UseVisualStyleBackColor = true;
            // 
            // radioButtonWebBasedSkyline
            // 
            this.radioButtonWebBasedSkyline.AutoSize = true;
            this.radioButtonWebBasedSkyline.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
            this.radioButtonWebBasedSkyline.Location = new System.Drawing.Point(10, 76);
            this.radioButtonWebBasedSkyline.Name = "radioButtonWebBasedSkyline";
            this.radioButtonWebBasedSkyline.Size = new System.Drawing.Size(188, 17);
            this.radioButtonWebBasedSkyline.TabIndex = 9;
            this.radioButtonWebBasedSkyline.TabStop = true;
            this.radioButtonWebBasedSkyline.Text = "Use web-based Skyline installation";
            this.radioButtonWebBasedSkyline.UseVisualStyleBackColor = true;
            this.radioButtonWebBasedSkyline.CheckedChanged += new System.EventHandler(this.WebBasedInstall_Click);
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
            // panelSkylineType
            // 
            this.panelSkylineType.Location = new System.Drawing.Point(6, 19);
            this.panelSkylineType.Name = "panelSkylineType";
            this.panelSkylineType.Size = new System.Drawing.Size(611, 39);
            this.panelSkylineType.TabIndex = 10;
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
            // 
            // tabLog
            // 
            this.tabLog.BackColor = System.Drawing.Color.Transparent;
            this.tabLog.Controls.Add(this.textBoxLog);
            this.tabLog.Location = new System.Drawing.Point(4, 22);
            this.tabLog.Name = "tabLog";
            this.tabLog.Padding = new System.Windows.Forms.Padding(3);
            this.tabLog.Size = new System.Drawing.Size(731, 525);
            this.tabLog.TabIndex = 1;
            this.tabLog.Text = "Log";
            this.tabLog.Enter += new System.EventHandler(this.tabLog_Enter);
            // 
            // textBoxLog
            // 
            this.textBoxLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxLog.Location = new System.Drawing.Point(24, 22);
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.ReadOnly = true;
            this.textBoxLog.Size = new System.Drawing.Size(684, 483);
            this.textBoxLog.TabIndex = 2;
            this.textBoxLog.Text = "";
            // 
            // tabFront
            // 
            this.tabFront.BackColor = System.Drawing.Color.Transparent;
            this.tabFront.Controls.Add(this.listViewConfigs);
            this.tabFront.Controls.Add(this.labelSavedConfigurations);
            this.tabFront.Controls.Add(this.panel2);
            this.tabFront.Controls.Add(this.panel1);
            this.tabFront.Location = new System.Drawing.Point(4, 22);
            this.tabFront.Name = "tabFront";
            this.tabFront.Padding = new System.Windows.Forms.Padding(3);
            this.tabFront.Size = new System.Drawing.Size(731, 525);
            this.tabFront.TabIndex = 0;
            this.tabFront.Text = "Configurations";
            // 
            // listViewConfigs
            // 
            this.listViewConfigs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listViewConfigs.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.listViewConfigName,
            this.listViewCreated,
            this.listViewStatus});
            this.listViewConfigs.FullRowSelect = true;
            this.listViewConfigs.HideSelection = false;
            this.listViewConfigs.Location = new System.Drawing.Point(48, 54);
            this.listViewConfigs.MultiSelect = false;
            this.listViewConfigs.Name = "listViewConfigs";
            this.listViewConfigs.Size = new System.Drawing.Size(538, 347);
            this.listViewConfigs.TabIndex = 7;
            this.listViewConfigs.UseCompatibleStateImageBehavior = false;
            this.listViewConfigs.View = System.Windows.Forms.View.Details;
            this.listViewConfigs.SelectedIndexChanged += new System.EventHandler(this.listViewConfigs_SelectedIndexChanged);
            // 
            // listViewConfigName
            // 
            this.listViewConfigName.Text = "Configuration";
            this.listViewConfigName.Width = 369;
            // 
            // listViewCreated
            // 
            this.listViewCreated.Text = "Created";
            this.listViewCreated.Width = 78;
            // 
            // listViewStatus
            // 
            this.listViewStatus.Text = "Status";
            this.listViewStatus.Width = 86;
            // 
            // labelSavedConfigurations
            // 
            this.labelSavedConfigurations.AutoSize = true;
            this.labelSavedConfigurations.Location = new System.Drawing.Point(47, 35);
            this.labelSavedConfigurations.Name = "labelSavedConfigurations";
            this.labelSavedConfigurations.Size = new System.Drawing.Size(110, 13);
            this.labelSavedConfigurations.TabIndex = 8;
            this.labelSavedConfigurations.Text = "Saved configurations:";
            // 
            // panel2
            // 
            this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.panel2.Controls.Add(this.btnRunOptions);
            this.panel2.Controls.Add(this.lblNoConfigs);
            this.panel2.Controls.Add(this.btnRunBatch);
            this.panel2.Controls.Add(this.btnNewConfig);
            this.panel2.Location = new System.Drawing.Point(64, 407);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(523, 112);
            this.panel2.TabIndex = 13;
            // 
            // btnRunOptions
            // 
            this.btnRunOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRunOptions.Location = new System.Drawing.Point(343, 70);
            this.btnRunOptions.Name = "btnRunOptions";
            this.btnRunOptions.Size = new System.Drawing.Size(19, 23);
            this.btnRunOptions.TabIndex = 16;
            this.btnRunOptions.UseVisualStyleBackColor = true;
            this.btnRunOptions.Click += new System.EventHandler(this.btnRunOptions_Click);
            // 
            // lblNoConfigs
            // 
            this.lblNoConfigs.AutoSize = true;
            this.lblNoConfigs.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblNoConfigs.ForeColor = System.Drawing.Color.Blue;
            this.lblNoConfigs.Location = new System.Drawing.Point(1, 2);
            this.lblNoConfigs.Name = "lblNoConfigs";
            this.lblNoConfigs.Size = new System.Drawing.Size(521, 16);
            this.lblNoConfigs.TabIndex = 5;
            this.lblNoConfigs.Text = "There are no saved configurations. Click the button below to create a new configu" +
    "ration.";
            // 
            // btnNewConfig
            // 
            this.btnNewConfig.Location = new System.Drawing.Point(160, 30);
            this.btnNewConfig.Name = "btnNewConfig";
            this.btnNewConfig.Size = new System.Drawing.Size(202, 23);
            this.btnNewConfig.TabIndex = 6;
            this.btnNewConfig.Text = "Create a &new configuration";
            this.btnNewConfig.UseVisualStyleBackColor = true;
            this.btnNewConfig.Click += new System.EventHandler(this.btnNewConfig_Click);
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.toolStrip1);
            this.panel1.Controls.Add(this.btnCancel);
            this.panel1.Controls.Add(this.btnImportConfigs);
            this.panel1.Controls.Add(this.btnExportConfigs);
            this.panel1.Controls.Add(this.btnViewLog1);
            this.panel1.Controls.Add(this.btnEdit);
            this.panel1.Controls.Add(this.btnCopy);
            this.panel1.Controls.Add(this.btnDelete);
            this.panel1.Location = new System.Drawing.Point(585, 35);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(140, 481);
            this.panel1.TabIndex = 12;
            // 
            // btnViewLog1
            // 
            this.btnViewLog1.Location = new System.Drawing.Point(46, 190);
            this.btnViewLog1.Name = "btnViewLog1";
            this.btnViewLog1.Size = new System.Drawing.Size(75, 23);
            this.btnViewLog1.TabIndex = 9;
            this.btnViewLog1.Text = "&View log";
            this.btnViewLog1.UseVisualStyleBackColor = true;
            this.btnViewLog1.Click += new System.EventHandler(this.btnViewLog1_Click);
            // 
            // btnEdit
            // 
            this.btnEdit.Location = new System.Drawing.Point(46, 42);
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Size = new System.Drawing.Size(75, 23);
            this.btnEdit.TabIndex = 2;
            this.btnEdit.Text = "&Edit";
            this.btnEdit.UseVisualStyleBackColor = true;
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            // 
            // btnCopy
            // 
            this.btnCopy.Location = new System.Drawing.Point(46, 83);
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Size = new System.Drawing.Size(75, 23);
            this.btnCopy.TabIndex = 4;
            this.btnCopy.Text = "&Copy";
            this.btnCopy.UseVisualStyleBackColor = true;
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(46, 124);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(75, 23);
            this.btnDelete.TabIndex = 3;
            this.btnDelete.Text = "&Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // tabMain
            // 
            this.tabMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabMain.Controls.Add(this.tabFront);
            this.tabMain.Controls.Add(this.tabLog);
            this.tabMain.Location = new System.Drawing.Point(12, 12);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(739, 551);
            this.tabMain.TabIndex = 10;
            // 
            // batchRunDropDown
            // 
            this.batchRunDropDown.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startFromStepOne,
            this.startFromStepTwo,
            this.startFromStepThree,
            this.startFromStepFour});
            this.batchRunDropDown.Name = "batchRunDropDown";
            this.batchRunDropDown.Size = new System.Drawing.Size(253, 92);
            this.batchRunDropDown.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.batchRunDropDown_ItemClicked);
            // 
            // startFromStepOne
            // 
            this.startFromStepOne.Checked = true;
            this.startFromStepOne.CheckState = System.Windows.Forms.CheckState.Checked;
            this.startFromStepOne.Name = "startFromStepOne";
            this.startFromStepOne.Size = new System.Drawing.Size(252, 22);
            this.startFromStepOne.Text = "Run all steps";
            // 
            // startFromStepTwo
            // 
            this.startFromStepTwo.Name = "startFromStepTwo";
            this.startFromStepTwo.Size = new System.Drawing.Size(252, 22);
            this.startFromStepTwo.Text = "Run from step 2: data import";
            // 
            // startFromStepThree
            // 
            this.startFromStepThree.Name = "startFromStepThree";
            this.startFromStepThree.Size = new System.Drawing.Size(252, 22);
            this.startFromStepThree.Text = "Run from step 3: report extraction";
            // 
            // startFromStepFour
            // 
            this.startFromStepFour.Name = "startFromStepFour";
            this.startFromStepFour.Size = new System.Drawing.Size(252, 22);
            this.startFromStepFour.Text = "Run from step 4: run R scripts";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnUpArrow,
            this.btnDownArrow});
            this.toolStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStrip1.Location = new System.Drawing.Point(4, 19);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(32, 67);
            this.toolStrip1.TabIndex = 14;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnUpArrow
            // 
            this.btnUpArrow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUpArrow.Enabled = false;
            this.btnUpArrow.Image = global::SkylineBatch.Properties.Resources.up_pro32;
            this.btnUpArrow.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnUpArrow.Name = "btnUpArrow";
            this.btnUpArrow.Size = new System.Drawing.Size(30, 20);
            this.btnUpArrow.Text = "toolStripButton1";
            this.btnUpArrow.ToolTipText = "Up";
            this.btnUpArrow.Click += new System.EventHandler(this.btnUpArrow_Click);
            // 
            // btnDownArrow
            // 
            this.btnDownArrow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDownArrow.Enabled = false;
            this.btnDownArrow.Image = ((System.Drawing.Image)(resources.GetObject("btnDownArrow.Image")));
            this.btnDownArrow.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDownArrow.Name = "btnDownArrow";
            this.btnDownArrow.Size = new System.Drawing.Size(30, 20);
            this.btnDownArrow.Text = "toolStripButton2";
            this.btnDownArrow.ToolTipText = "Down";
            this.btnDownArrow.Click += new System.EventHandler(this.btnDownArrow_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(763, 573);
            this.Controls.Add(this.tabMain);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "SkylineBatch";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.tabLog.ResumeLayout(false);
            this.tabFront.ResumeLayout(false);
            this.tabFront.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.tabMain.ResumeLayout(false);
            this.batchRunDropDown.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ToolTip toolTip_MainForm;
        private System.Windows.Forms.NotifyIcon systray_icon;
        private System.Windows.Forms.CheckBox cb_keepRunning;
        private System.Windows.Forms.CheckBox cb_minimizeToSysTray;
        private System.Windows.Forms.TextBox textBoxSkylinePath;
        private System.Windows.Forms.RadioButton radioButtonSpecifySkylinePath;
        private System.Windows.Forms.Button buttonApplySkylineSettings;
        private System.Windows.Forms.RadioButton radioButtonWebBasedSkyline;
        private System.Windows.Forms.Button buttonFileDialogSkylineInstall;
        private System.Windows.Forms.Panel panelSkylineType;
        private System.Windows.Forms.RadioButton radioButtonUseSkyline;
        private System.Windows.Forms.RadioButton radioButtonUseSkylineDaily;
        private System.Windows.Forms.TabPage tabLog;
        private System.Windows.Forms.RichTextBox textBoxLog;
        private System.Windows.Forms.TabPage tabFront;
        private System.Windows.Forms.Label labelSavedConfigurations;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnImportConfigs;
        private System.Windows.Forms.Button btnExportConfigs;
        private System.Windows.Forms.Button btnViewLog1;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnCopy;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label lblNoConfigs;
        private System.Windows.Forms.Button btnNewConfig;
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.Button btnRunBatch;
        private System.Windows.Forms.ListView listViewConfigs;
        private System.Windows.Forms.ColumnHeader listViewConfigName;
        private System.Windows.Forms.ColumnHeader listViewCreated;
        private System.Windows.Forms.ColumnHeader listViewStatus;
        private System.Windows.Forms.ContextMenuStrip batchRunDropDown;
        private System.Windows.Forms.ToolStripMenuItem startFromStepOne;
        private System.Windows.Forms.ToolStripMenuItem startFromStepTwo;
        private System.Windows.Forms.ToolStripMenuItem startFromStepThree;
        private System.Windows.Forms.Button btnRunOptions;
        private System.Windows.Forms.ToolStripMenuItem startFromStepFour;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnUpArrow;
        private System.Windows.Forms.ToolStripButton btnDownArrow;
    }
}