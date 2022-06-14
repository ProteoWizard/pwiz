using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZedGraph;
using Label = System.Windows.Forms.Label;

namespace SkylineTester
{
    partial class SkylineTesterWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

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

        private void dataGridRunStats_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column.Index == 0)
            {
                e.SortResult = System.String.Compare(e.CellValue1.ToString(), e.CellValue2.ToString());
            }
            else if (e.CellValue1 != null && e.CellValue2 != null)
            {
                string v1 = e.CellValue1.ToString().Split('/')[0];
                string v2 = e.CellValue2.ToString().Split('/')[0];
                double d1, d2;
                if (double.TryParse(v1, out d1) && double.TryParse(v2, out d2))
                {
                    e.SortResult = d1.CompareTo(d2);
                }
                else
                {
                    e.SortResult = String.Compare(e.CellValue1.ToString(), e.CellValue2.ToString());
                }
            }

            // If the cells are equal, sort based on the test name.
            if (e.SortResult == 0)
            {
                e.SortResult = System.String.Compare(
                    dataGridRunStats.Rows[e.RowIndex1].Cells[0].Value.ToString(),
                    dataGridRunStats.Rows[e.RowIndex2].Cells[0].Value.ToString());
            }
            e.Handled = true;
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SkylineTesterWindow));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.mainPanel = new System.Windows.Forms.Panel();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.selectedBuild = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusRunTime = new System.Windows.Forms.ToolStripStatusLabel();
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabForms = new System.Windows.Forms.TabPage();
            this.showChangedFiles = new System.Windows.Forms.CheckBox();
            this.groupBox12 = new System.Windows.Forms.GroupBox();
            this.showFormNames = new System.Windows.Forms.CheckBox();
            this.label15 = new System.Windows.Forms.Label();
            this.groupBox13 = new System.Windows.Forms.GroupBox();
            this.diffButton = new System.Windows.Forms.Button();
            this.formsLanguageDiff = new System.Windows.Forms.ComboBox();
            this.label20 = new System.Windows.Forms.Label();
            this.formsLanguage = new System.Windows.Forms.ComboBox();
            this.runForms = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.labelSelectedFormsCount = new System.Windows.Forms.ToolStripLabel();
            this.clearSeenButton = new System.Windows.Forms.ToolStripButton();
            this.labelFormsSeenPercent = new System.Windows.Forms.ToolStripLabel();
            this.formsGrid = new SkylineTester.SafeDataGridView();
            this.FormColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.TestColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.SeenColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabTutorials = new System.Windows.Forms.TabPage();
            this.groupBox21 = new System.Windows.Forms.GroupBox();
            this.showMatchingPagesTutorial = new System.Windows.Forms.CheckBox();
            this.showFormNamesTutorial = new System.Windows.Forms.CheckBox();
            this.label16 = new System.Windows.Forms.Label();
            this.groupBox14 = new System.Windows.Forms.GroupBox();
            this.tutorialsLanguage = new System.Windows.Forms.ComboBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.tutorialsTree = new SkylineTester.MyTreeView();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.pauseStartingPage = new System.Windows.Forms.TextBox();
            this.labelPauseStartingPage = new System.Windows.Forms.Label();
            this.modeTutorialsCoverShots = new System.Windows.Forms.RadioButton();
            this.pauseTutorialsSeconds = new System.Windows.Forms.NumericUpDown();
            this.tutorialsDemoMode = new System.Windows.Forms.RadioButton();
            this.label5 = new System.Windows.Forms.Label();
            this.pauseTutorialsDelay = new System.Windows.Forms.RadioButton();
            this.pauseTutorialsScreenShots = new System.Windows.Forms.RadioButton();
            this.runTutorials = new System.Windows.Forms.Button();
            this.tabTests = new System.Windows.Forms.TabPage();
            this.runTests = new System.Windows.Forms.Button();
            this.buttonSelectFailedTestsTab = new System.Windows.Forms.Button();
            this.label17 = new System.Windows.Forms.Label();
            this.groupBox15 = new System.Windows.Forms.GroupBox();
            this.testsTurkish = new System.Windows.Forms.CheckBox();
            this.testsFrench = new System.Windows.Forms.CheckBox();
            this.testsJapanese = new System.Windows.Forms.CheckBox();
            this.testsChinese = new System.Windows.Forms.CheckBox();
            this.testsEnglish = new System.Windows.Forms.CheckBox();
            this.windowsGroup = new System.Windows.Forms.GroupBox();
            this.offscreen = new System.Windows.Forms.CheckBox();
            this.iterationsGroup = new System.Windows.Forms.GroupBox();
            this.recordAuditLogs = new System.Windows.Forms.CheckBox();
            this.testsRunSmallMoleculeVersions = new System.Windows.Forms.CheckBox();
            this.randomize = new System.Windows.Forms.CheckBox();
            this.repeat = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.runLoopsCount = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.runLoops = new System.Windows.Forms.RadioButton();
            this.runIndefinitely = new System.Windows.Forms.RadioButton();
            this.testsGroup = new System.Windows.Forms.GroupBox();
            this.runMode = new System.Windows.Forms.ComboBox();
            this.label21 = new System.Windows.Forms.Label();
            this.showTutorialsOnly = new System.Windows.Forms.CheckBox();
            this.testsTree = new SkylineTester.MyTreeView();
            this.skipCheckedTests = new System.Windows.Forms.RadioButton();
            this.runCheckedTests = new System.Windows.Forms.RadioButton();
            this.tabBuild = new System.Windows.Forms.TabPage();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.startSln = new System.Windows.Forms.CheckBox();
            this.runBuildVerificationTests = new System.Windows.Forms.CheckBox();
            this.label14 = new System.Windows.Forms.Label();
            this.groupBox10 = new System.Windows.Forms.GroupBox();
            this.labelSpecifyPath = new System.Windows.Forms.Label();
            this.buttonDeleteBuild = new System.Windows.Forms.Button();
            this.buildRoot = new System.Windows.Forms.TextBox();
            this.buttonBrowseBuild = new System.Windows.Forms.Button();
            this.groupBox16 = new System.Windows.Forms.GroupBox();
            this.incrementalBuild = new System.Windows.Forms.RadioButton();
            this.updateBuild = new System.Windows.Forms.RadioButton();
            this.nukeBuild = new System.Windows.Forms.RadioButton();
            this.runBuild = new System.Windows.Forms.Button();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.buildBranch = new System.Windows.Forms.RadioButton();
            this.buildTrunk = new System.Windows.Forms.RadioButton();
            this.branchUrl = new System.Windows.Forms.TextBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.build64 = new System.Windows.Forms.CheckBox();
            this.build32 = new System.Windows.Forms.CheckBox();
            this.tabQuality = new System.Windows.Forms.TabPage();
            this.panel2 = new System.Windows.Forms.Panel();
            this.radioQualityHandles = new System.Windows.Forms.RadioButton();
            this.radioQualityMemory = new System.Windows.Forms.RadioButton();
            this.qualityTableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.groupBox7 = new System.Windows.Forms.GroupBox();
            this.qualityTestName = new System.Windows.Forms.Label();
            this.qualityThumbnail = new SkylineTester.WindowThumbnail();
            this.groupBox11 = new System.Windows.Forms.GroupBox();
            this.buttonViewLog = new System.Windows.Forms.Button();
            this.labelLeaks = new System.Windows.Forms.Label();
            this.labelFailures = new System.Windows.Forms.Label();
            this.labelTestsRun = new System.Windows.Forms.Label();
            this.labelDuration = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.groupBox9 = new System.Windows.Forms.GroupBox();
            this.qualityAllTests = new System.Windows.Forms.RadioButton();
            this.qualityChooseTests = new System.Windows.Forms.RadioButton();
            this.groupBox8 = new System.Windows.Forms.GroupBox();
            this.qualityRunSmallMoleculeVersions = new System.Windows.Forms.CheckBox();
            this.qualityPassIndefinite = new System.Windows.Forms.RadioButton();
            this.qualityPassCount = new System.Windows.Forms.NumericUpDown();
            this.pass1 = new System.Windows.Forms.CheckBox();
            this.pass0 = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.qualityPassDefinite = new System.Windows.Forms.RadioButton();
            this.panelMemoryGraph = new System.Windows.Forms.Panel();
            this.label18 = new System.Windows.Forms.Label();
            this.runQuality = new System.Windows.Forms.Button();
            this.tabNightly = new System.Windows.Forms.TabPage();
            this.nightlyExit = new System.Windows.Forms.CheckBox();
            this.buttonDeleteNightlyTask = new System.Windows.Forms.Button();
            this.nightlyTableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox17 = new System.Windows.Forms.GroupBox();
            this.nightlyTrendsTable = new System.Windows.Forms.TableLayoutPanel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.groupBox19 = new System.Windows.Forms.GroupBox();
            this.label34 = new System.Windows.Forms.Label();
            this.nightlyRoot = new System.Windows.Forms.Label();
            this.nightlyBrowseBuild = new System.Windows.Forms.Button();
            this.groupBox22 = new System.Windows.Forms.GroupBox();
            this.nightlyBranch = new System.Windows.Forms.RadioButton();
            this.nightlyBuildTrunk = new System.Windows.Forms.RadioButton();
            this.nightlyBranchUrl = new System.Windows.Forms.TextBox();
            this.groupBox18 = new System.Windows.Forms.GroupBox();
            this.panel4 = new System.Windows.Forms.Panel();
            this.radioNightlyHandles = new System.Windows.Forms.RadioButton();
            this.radioNightlyMemory = new System.Windows.Forms.RadioButton();
            this.nightlyTestName = new System.Windows.Forms.Label();
            this.nightlyThumbnail = new SkylineTester.WindowThumbnail();
            this.nightlyGraphPanel = new System.Windows.Forms.Panel();
            this.nightlyDeleteRun = new System.Windows.Forms.Button();
            this.nightlyViewLog = new System.Windows.Forms.Button();
            this.nightlyLabelLeaks = new System.Windows.Forms.Label();
            this.nightlyLabelFailures = new System.Windows.Forms.Label();
            this.nightlyLabelTestsRun = new System.Windows.Forms.Label();
            this.nightlyLabelDuration = new System.Windows.Forms.Label();
            this.label25 = new System.Windows.Forms.Label();
            this.nightlyLabel3 = new System.Windows.Forms.Label();
            this.nightlyLabel2 = new System.Windows.Forms.Label();
            this.nightlyLabel1 = new System.Windows.Forms.Label();
            this.nightlyRunDate = new System.Windows.Forms.ComboBox();
            this.label29 = new System.Windows.Forms.Label();
            this.groupBox20 = new System.Windows.Forms.GroupBox();
            this.nightlyRunIndefinitely = new System.Windows.Forms.CheckBox();
            this.nightlyRandomize = new System.Windows.Forms.CheckBox();
            this.nightlyRepeat = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.nightlyRunPerfTests = new System.Windows.Forms.CheckBox();
            this.buttonNow = new System.Windows.Forms.Button();
            this.nightlyStartTime = new System.Windows.Forms.DateTimePicker();
            this.nightlyBuildType = new System.Windows.Forms.DomainUpDown();
            this.label31 = new System.Windows.Forms.Label();
            this.label35 = new System.Windows.Forms.Label();
            this.nightlyDuration = new System.Windows.Forms.NumericUpDown();
            this.label30 = new System.Windows.Forms.Label();
            this.label32 = new System.Windows.Forms.Label();
            this.label33 = new System.Windows.Forms.Label();
            this.runNightly = new System.Windows.Forms.Button();
            this.tabOutput = new System.Windows.Forms.TabPage();
            this.buttonSelectFailedOutputTab = new System.Windows.Forms.Button();
            this.outputJumpTo = new System.Windows.Forms.ComboBox();
            this.outputSplitContainer = new System.Windows.Forms.SplitContainer();
            this.commandShell = new SkylineTester.CommandShell();
            this.errorConsole = new System.Windows.Forms.RichTextBox();
            this.buttonOpenLog = new System.Windows.Forms.Button();
            this.comboBoxOutput = new System.Windows.Forms.ComboBox();
            this.label19 = new System.Windows.Forms.Label();
            this.buttonStop = new System.Windows.Forms.Button();
            this.tabRunStats = new System.Windows.Forms.TabPage();
            this.labelCompareTo = new System.Windows.Forms.Label();
            this.comboBoxRunStatsCompare = new System.Windows.Forms.ComboBox();
            this.comboBoxRunStats = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.dataGridRunStats = new SkylineTester.SafeDataGridView();
            this.TestName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Iterations = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Duration = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AverageDuration = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.RelDuration = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DeltaTotalDuration = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.createInstallerZipFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.findToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.findTestToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.findNextToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectBuildMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bin32Bit = new System.Windows.Forms.ToolStripMenuItem();
            this.bin64Bit = new System.Windows.Forms.ToolStripMenuItem();
            this.build32Bit = new System.Windows.Forms.ToolStripMenuItem();
            this.build64Bit = new System.Windows.Forms.ToolStripMenuItem();
            this.nightly32Bit = new System.Windows.Forms.ToolStripMenuItem();
            this.nightly64Bit = new System.Windows.Forms.ToolStripMenuItem();
            this.zip32Bit = new System.Windows.Forms.ToolStripMenuItem();
            this.zip64Bit = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.accessInternet = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.radioButton3 = new System.Windows.Forms.RadioButton();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.radioButton5 = new System.Windows.Forms.RadioButton();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.myTreeView1 = new SkylineTester.MyTreeView();
            this.mainPanel.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.tabs.SuspendLayout();
            this.tabForms.SuspendLayout();
            this.groupBox12.SuspendLayout();
            this.groupBox13.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.formsGrid)).BeginInit();
            this.tabTutorials.SuspendLayout();
            this.groupBox21.SuspendLayout();
            this.groupBox14.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pauseTutorialsSeconds)).BeginInit();
            this.tabTests.SuspendLayout();
            this.groupBox15.SuspendLayout();
            this.windowsGroup.SuspendLayout();
            this.iterationsGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.runLoopsCount)).BeginInit();
            this.testsGroup.SuspendLayout();
            this.tabBuild.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox10.SuspendLayout();
            this.groupBox16.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.tabQuality.SuspendLayout();
            this.panel2.SuspendLayout();
            this.qualityTableLayout.SuspendLayout();
            this.panel1.SuspendLayout();
            this.groupBox7.SuspendLayout();
            this.groupBox11.SuspendLayout();
            this.groupBox9.SuspendLayout();
            this.groupBox8.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.qualityPassCount)).BeginInit();
            this.tabNightly.SuspendLayout();
            this.nightlyTableLayout.SuspendLayout();
            this.groupBox17.SuspendLayout();
            this.panel3.SuspendLayout();
            this.groupBox19.SuspendLayout();
            this.groupBox22.SuspendLayout();
            this.groupBox18.SuspendLayout();
            this.panel4.SuspendLayout();
            this.groupBox20.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nightlyDuration)).BeginInit();
            this.tabOutput.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.outputSplitContainer)).BeginInit();
            this.outputSplitContainer.Panel1.SuspendLayout();
            this.outputSplitContainer.Panel2.SuspendLayout();
            this.outputSplitContainer.SuspendLayout();
            this.tabRunStats.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridRunStats)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainPanel
            // 
            this.mainPanel.BackColor = System.Drawing.Color.Silver;
            this.mainPanel.Controls.Add(this.statusStrip1);
            this.mainPanel.Controls.Add(this.tabs);
            this.mainPanel.Controls.Add(this.menuStrip1);
            this.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainPanel.Location = new System.Drawing.Point(0, 0);
            this.mainPanel.Margin = new System.Windows.Forms.Padding(4);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.Size = new System.Drawing.Size(709, 767);
            this.mainPanel.TabIndex = 0;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel,
            this.selectedBuild,
            this.statusRunTime});
            this.statusStrip1.Location = new System.Drawing.Point(0, 745);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 13, 0);
            this.statusStrip1.Size = new System.Drawing.Size(709, 22);
            this.statusStrip1.TabIndex = 23;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            this.statusLabel.BackColor = System.Drawing.Color.Transparent;
            this.statusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(38, 17);
            this.statusLabel.Text = "status";
            // 
            // selectedBuild
            // 
            this.selectedBuild.BackColor = System.Drawing.Color.Transparent;
            this.selectedBuild.ForeColor = System.Drawing.SystemColors.GrayText;
            this.selectedBuild.Name = "selectedBuild";
            this.selectedBuild.Size = new System.Drawing.Size(608, 17);
            this.selectedBuild.Spring = true;
            this.selectedBuild.Text = "selected build";
            // 
            // statusRunTime
            // 
            this.statusRunTime.BackColor = System.Drawing.Color.Transparent;
            this.statusRunTime.ForeColor = System.Drawing.SystemColors.GrayText;
            this.statusRunTime.Margin = new System.Windows.Forms.Padding(0, 3, 6, 2);
            this.statusRunTime.Name = "statusRunTime";
            this.statusRunTime.Size = new System.Drawing.Size(43, 17);
            this.statusRunTime.Text = "0:00:00";
            this.statusRunTime.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // tabs
            // 
            this.tabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabs.Controls.Add(this.tabForms);
            this.tabs.Controls.Add(this.tabTutorials);
            this.tabs.Controls.Add(this.tabTests);
            this.tabs.Controls.Add(this.tabBuild);
            this.tabs.Controls.Add(this.tabQuality);
            this.tabs.Controls.Add(this.tabNightly);
            this.tabs.Controls.Add(this.tabOutput);
            this.tabs.Controls.Add(this.tabRunStats);
            this.tabs.Location = new System.Drawing.Point(-4, 33);
            this.tabs.Margin = new System.Windows.Forms.Padding(4);
            this.tabs.Name = "tabs";
            this.tabs.Padding = new System.Drawing.Point(20, 6);
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(717, 721);
            this.tabs.TabIndex = 0;
            this.tabs.SelectedIndexChanged += new System.EventHandler(this.TabChanged);
            // 
            // tabForms
            // 
            this.tabForms.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(205)))));
            this.tabForms.Controls.Add(this.showChangedFiles);
            this.tabForms.Controls.Add(this.groupBox12);
            this.tabForms.Controls.Add(this.label15);
            this.tabForms.Controls.Add(this.groupBox13);
            this.tabForms.Controls.Add(this.runForms);
            this.tabForms.Controls.Add(this.groupBox1);
            this.tabForms.Location = new System.Drawing.Point(4, 28);
            this.tabForms.Margin = new System.Windows.Forms.Padding(4);
            this.tabForms.Name = "tabForms";
            this.tabForms.Padding = new System.Windows.Forms.Padding(4);
            this.tabForms.Size = new System.Drawing.Size(709, 689);
            this.tabForms.TabIndex = 1;
            this.tabForms.Text = "Forms";
            // 
            // showChangedFiles
            // 
            this.showChangedFiles.AutoSize = true;
            this.showChangedFiles.Location = new System.Drawing.Point(16, 235);
            this.showChangedFiles.Name = "showChangedFiles";
            this.showChangedFiles.Size = new System.Drawing.Size(126, 17);
            this.showChangedFiles.TabIndex = 5;
            this.showChangedFiles.Text = "Show changed forms";
            this.showChangedFiles.UseVisualStyleBackColor = true;
            this.showChangedFiles.CheckedChanged += new System.EventHandler(this.showChangedFiles_CheckedChanged);
            // 
            // groupBox12
            // 
            this.groupBox12.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(205)))));
            this.groupBox12.Controls.Add(this.showFormNames);
            this.groupBox12.Location = new System.Drawing.Point(8, 190);
            this.groupBox12.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox12.Name = "groupBox12";
            this.groupBox12.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox12.Size = new System.Drawing.Size(280, 62);
            this.groupBox12.TabIndex = 2;
            this.groupBox12.TabStop = false;
            this.groupBox12.Text = "Options";
            // 
            // showFormNames
            // 
            this.showFormNames.AutoSize = true;
            this.showFormNames.Checked = true;
            this.showFormNames.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showFormNames.Location = new System.Drawing.Point(8, 21);
            this.showFormNames.Margin = new System.Windows.Forms.Padding(4);
            this.showFormNames.Name = "showFormNames";
            this.showFormNames.Size = new System.Drawing.Size(110, 17);
            this.showFormNames.TabIndex = 0;
            this.showFormNames.Text = "Show form names";
            this.showFormNames.UseVisualStyleBackColor = true;
            // 
            // label15
            // 
            this.label15.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label15.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label15.Location = new System.Drawing.Point(7, 4);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(695, 44);
            this.label15.TabIndex = 0;
            this.label15.Text = "View Skyline forms";
            this.label15.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox13
            // 
            this.groupBox13.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(205)))));
            this.groupBox13.Controls.Add(this.diffButton);
            this.groupBox13.Controls.Add(this.formsLanguageDiff);
            this.groupBox13.Controls.Add(this.label20);
            this.groupBox13.Controls.Add(this.formsLanguage);
            this.groupBox13.Location = new System.Drawing.Point(8, 50);
            this.groupBox13.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox13.Name = "groupBox13";
            this.groupBox13.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox13.Size = new System.Drawing.Size(280, 97);
            this.groupBox13.TabIndex = 1;
            this.groupBox13.TabStop = false;
            this.groupBox13.Text = "Language";
            // 
            // diffButton
            // 
            this.diffButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.diffButton.Location = new System.Drawing.Point(201, 66);
            this.diffButton.Margin = new System.Windows.Forms.Padding(4);
            this.diffButton.Name = "diffButton";
            this.diffButton.Size = new System.Drawing.Size(40, 21);
            this.diffButton.TabIndex = 9;
            this.diffButton.Text = "Diff";
            this.diffButton.UseVisualStyleBackColor = true;
            this.diffButton.Click += new System.EventHandler(this.diffButton_Click);
            // 
            // formsLanguageDiff
            // 
            this.formsLanguageDiff.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.formsLanguageDiff.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.formsLanguageDiff.FormattingEnabled = true;
            this.formsLanguageDiff.Location = new System.Drawing.Point(8, 66);
            this.formsLanguageDiff.Margin = new System.Windows.Forms.Padding(4);
            this.formsLanguageDiff.Name = "formsLanguageDiff";
            this.formsLanguageDiff.Size = new System.Drawing.Size(185, 21);
            this.formsLanguageDiff.TabIndex = 1;
            this.formsLanguageDiff.SelectedIndexChanged += new System.EventHandler(this.formsLanguageDiff_SelectedIndexChanged);
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(5, 49);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(46, 13);
            this.label20.TabIndex = 1;
            this.label20.Text = "Diff from";
            // 
            // formsLanguage
            // 
            this.formsLanguage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.formsLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.formsLanguage.FormattingEnabled = true;
            this.formsLanguage.Location = new System.Drawing.Point(8, 21);
            this.formsLanguage.Margin = new System.Windows.Forms.Padding(4);
            this.formsLanguage.Name = "formsLanguage";
            this.formsLanguage.Size = new System.Drawing.Size(185, 21);
            this.formsLanguage.TabIndex = 0;
            this.formsLanguage.SelectedIndexChanged += new System.EventHandler(this.formsLanguage_SelectedIndexChanged);
            // 
            // runForms
            // 
            this.runForms.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runForms.Location = new System.Drawing.Point(596, 649);
            this.runForms.Margin = new System.Windows.Forms.Padding(4);
            this.runForms.Name = "runForms";
            this.runForms.Size = new System.Drawing.Size(100, 28);
            this.runForms.TabIndex = 4;
            this.runForms.Text = "Run";
            this.runForms.UseVisualStyleBackColor = true;
            this.runForms.Click += new System.EventHandler(this.RunOrStop_Clicked);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.toolStrip1);
            this.groupBox1.Controls.Add(this.formsGrid);
            this.groupBox1.Location = new System.Drawing.Point(299, 50);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox1.Size = new System.Drawing.Size(402, 591);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Forms";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.toolStrip1.AutoSize = false;
            this.toolStrip1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(200)))), ((int)(((byte)(190)))));
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.labelSelectedFormsCount,
            this.clearSeenButton,
            this.labelFormsSeenPercent});
            this.toolStrip1.Location = new System.Drawing.Point(8, 21);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(387, 25);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // labelSelectedFormsCount
            // 
            this.labelSelectedFormsCount.Margin = new System.Windows.Forms.Padding(8, 1, 0, 2);
            this.labelSelectedFormsCount.Name = "labelSelectedFormsCount";
            this.labelSelectedFormsCount.Size = new System.Drawing.Size(59, 22);
            this.labelSelectedFormsCount.Text = "0 selected";
            // 
            // clearSeenButton
            // 
            this.clearSeenButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.clearSeenButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearSeenButton.Image = ((System.Drawing.Image)(resources.GetObject("clearSeenButton.Image")));
            this.clearSeenButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.clearSeenButton.Name = "clearSeenButton";
            this.clearSeenButton.Padding = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.clearSeenButton.Size = new System.Drawing.Size(71, 22);
            this.clearSeenButton.Text = "Clear seen";
            this.clearSeenButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.clearSeenButton.Click += new System.EventHandler(this.clearSeenButton_Click);
            // 
            // labelFormsSeenPercent
            // 
            this.labelFormsSeenPercent.Margin = new System.Windows.Forms.Padding(80, 1, 0, 2);
            this.labelFormsSeenPercent.Name = "labelFormsSeenPercent";
            this.labelFormsSeenPercent.Size = new System.Drawing.Size(107, 22);
            this.labelFormsSeenPercent.Text = "0% of 0 forms seen";
            this.labelFormsSeenPercent.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // formsGrid
            // 
            this.formsGrid.AllowUserToAddRows = false;
            this.formsGrid.AllowUserToDeleteRows = false;
            this.formsGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.formsGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.formsGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.FormColumn,
            this.TestColumn,
            this.SeenColumn});
            this.formsGrid.Location = new System.Drawing.Point(8, 49);
            this.formsGrid.Name = "formsGrid";
            this.formsGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.formsGrid.Size = new System.Drawing.Size(387, 535);
            this.formsGrid.TabIndex = 1;
            this.formsGrid.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.formsGrid_CellContentClick);
            this.formsGrid.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.formsGrid_CellEndEdit);
            this.formsGrid.SelectionChanged += new System.EventHandler(this.formsGrid_SelectionChanged);
            // 
            // FormColumn
            // 
            this.FormColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.FormColumn.HeaderText = "Form";
            this.FormColumn.Name = "FormColumn";
            this.FormColumn.ReadOnly = true;
            this.FormColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // TestColumn
            // 
            this.TestColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.TestColumn.HeaderText = "Test";
            this.TestColumn.Name = "TestColumn";
            this.TestColumn.ReadOnly = true;
            this.TestColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.TestColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // SeenColumn
            // 
            this.SeenColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle1.Format = "N0";
            dataGridViewCellStyle1.NullValue = null;
            dataGridViewCellStyle1.Padding = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.SeenColumn.DefaultCellStyle = dataGridViewCellStyle1;
            this.SeenColumn.HeaderText = "Seen";
            this.SeenColumn.MinimumWidth = 40;
            this.SeenColumn.Name = "SeenColumn";
            this.SeenColumn.Width = 57;
            // 
            // tabTutorials
            // 
            this.tabTutorials.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(222)))), ((int)(((byte)(228)))));
            this.tabTutorials.Controls.Add(this.groupBox21);
            this.tabTutorials.Controls.Add(this.label16);
            this.tabTutorials.Controls.Add(this.groupBox14);
            this.tabTutorials.Controls.Add(this.groupBox3);
            this.tabTutorials.Controls.Add(this.groupBox4);
            this.tabTutorials.Controls.Add(this.runTutorials);
            this.tabTutorials.Location = new System.Drawing.Point(4, 28);
            this.tabTutorials.Margin = new System.Windows.Forms.Padding(4);
            this.tabTutorials.Name = "tabTutorials";
            this.tabTutorials.Padding = new System.Windows.Forms.Padding(4);
            this.tabTutorials.Size = new System.Drawing.Size(709, 689);
            this.tabTutorials.TabIndex = 2;
            this.tabTutorials.Text = "Tutorials";
            // 
            // groupBox21
            // 
            this.groupBox21.BackColor = System.Drawing.Color.Transparent;
            this.groupBox21.Controls.Add(this.showMatchingPagesTutorial);
            this.groupBox21.Controls.Add(this.showFormNamesTutorial);
            this.groupBox21.Location = new System.Drawing.Point(13, 295);
            this.groupBox21.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox21.Name = "groupBox21";
            this.groupBox21.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox21.Size = new System.Drawing.Size(280, 76);
            this.groupBox21.TabIndex = 4;
            this.groupBox21.TabStop = false;
            this.groupBox21.Text = "Options";
            // 
            // showMatchingPagesTutorial
            // 
            this.showMatchingPagesTutorial.AutoSize = true;
            this.showMatchingPagesTutorial.Location = new System.Drawing.Point(8, 45);
            this.showMatchingPagesTutorial.Name = "showMatchingPagesTutorial";
            this.showMatchingPagesTutorial.Size = new System.Drawing.Size(160, 17);
            this.showMatchingPagesTutorial.TabIndex = 6;
            this.showMatchingPagesTutorial.Text = "Show matching tutorial page";
            this.showMatchingPagesTutorial.UseVisualStyleBackColor = true;
            // 
            // showFormNamesTutorial
            // 
            this.showFormNamesTutorial.AutoSize = true;
            this.showFormNamesTutorial.Location = new System.Drawing.Point(8, 21);
            this.showFormNamesTutorial.Margin = new System.Windows.Forms.Padding(4);
            this.showFormNamesTutorial.Name = "showFormNamesTutorial";
            this.showFormNamesTutorial.Size = new System.Drawing.Size(110, 17);
            this.showFormNamesTutorial.TabIndex = 0;
            this.showFormNamesTutorial.Text = "Show form names";
            this.showFormNamesTutorial.UseVisualStyleBackColor = true;
            // 
            // label16
            // 
            this.label16.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label16.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(225)))));
            this.label16.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label16.Location = new System.Drawing.Point(7, 4);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(694, 44);
            this.label16.TabIndex = 0;
            this.label16.Text = "Run Skyline tutorials";
            this.label16.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox14
            // 
            this.groupBox14.BackColor = System.Drawing.Color.Transparent;
            this.groupBox14.Controls.Add(this.tutorialsLanguage);
            this.groupBox14.Location = new System.Drawing.Point(11, 229);
            this.groupBox14.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox14.Name = "groupBox14";
            this.groupBox14.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox14.Size = new System.Drawing.Size(280, 58);
            this.groupBox14.TabIndex = 3;
            this.groupBox14.TabStop = false;
            this.groupBox14.Text = "Language";
            // 
            // tutorialsLanguage
            // 
            this.tutorialsLanguage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tutorialsLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.tutorialsLanguage.FormattingEnabled = true;
            this.tutorialsLanguage.Location = new System.Drawing.Point(8, 21);
            this.tutorialsLanguage.Margin = new System.Windows.Forms.Padding(4);
            this.tutorialsLanguage.Name = "tutorialsLanguage";
            this.tutorialsLanguage.Size = new System.Drawing.Size(185, 21);
            this.tutorialsLanguage.TabIndex = 0;
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.Controls.Add(this.tutorialsTree);
            this.groupBox3.Location = new System.Drawing.Point(299, 50);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox3.Size = new System.Drawing.Size(402, 591);
            this.groupBox3.TabIndex = 0;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Tutorials";
            // 
            // tutorialsTree
            // 
            this.tutorialsTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tutorialsTree.CheckBoxes = true;
            this.tutorialsTree.Location = new System.Drawing.Point(8, 23);
            this.tutorialsTree.Margin = new System.Windows.Forms.Padding(4);
            this.tutorialsTree.Name = "tutorialsTree";
            this.tutorialsTree.Size = new System.Drawing.Size(384, 559);
            this.tutorialsTree.TabIndex = 0;
            this.tutorialsTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.node_AfterCheck);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.pauseStartingPage);
            this.groupBox4.Controls.Add(this.labelPauseStartingPage);
            this.groupBox4.Controls.Add(this.modeTutorialsCoverShots);
            this.groupBox4.Controls.Add(this.pauseTutorialsSeconds);
            this.groupBox4.Controls.Add(this.tutorialsDemoMode);
            this.groupBox4.Controls.Add(this.label5);
            this.groupBox4.Controls.Add(this.pauseTutorialsDelay);
            this.groupBox4.Controls.Add(this.pauseTutorialsScreenShots);
            this.groupBox4.Location = new System.Drawing.Point(11, 50);
            this.groupBox4.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox4.Size = new System.Drawing.Size(280, 159);
            this.groupBox4.TabIndex = 0;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Pause";
            // 
            // pauseStartingPage
            // 
            this.pauseStartingPage.Location = new System.Drawing.Point(99, 45);
            this.pauseStartingPage.Name = "pauseStartingPage";
            this.pauseStartingPage.Size = new System.Drawing.Size(41, 20);
            this.pauseStartingPage.TabIndex = 2;
            this.pauseStartingPage.Text = "1";
            // 
            // labelPauseStartingPage
            // 
            this.labelPauseStartingPage.AutoSize = true;
            this.labelPauseStartingPage.Location = new System.Drawing.Point(25, 48);
            this.labelPauseStartingPage.Name = "labelPauseStartingPage";
            this.labelPauseStartingPage.Size = new System.Drawing.Size(73, 13);
            this.labelPauseStartingPage.TabIndex = 1;
            this.labelPauseStartingPage.Text = "Starting page:";
            // 
            // modeTutorialsCoverShots
            // 
            this.modeTutorialsCoverShots.AutoSize = true;
            this.modeTutorialsCoverShots.Location = new System.Drawing.Point(7, 131);
            this.modeTutorialsCoverShots.Margin = new System.Windows.Forms.Padding(4);
            this.modeTutorialsCoverShots.Name = "modeTutorialsCoverShots";
            this.modeTutorialsCoverShots.Size = new System.Drawing.Size(110, 17);
            this.modeTutorialsCoverShots.TabIndex = 7;
            this.modeTutorialsCoverShots.Text = "Cover shots mode";
            this.toolTip1.SetToolTip(this.modeTutorialsCoverShots, "Runs the tutorial until cover shot is reached, saves the image, then exits");
            this.modeTutorialsCoverShots.UseVisualStyleBackColor = true;
            // 
            // pauseTutorialsSeconds
            // 
            this.pauseTutorialsSeconds.Location = new System.Drawing.Point(99, 82);
            this.pauseTutorialsSeconds.Name = "pauseTutorialsSeconds";
            this.pauseTutorialsSeconds.Size = new System.Drawing.Size(41, 20);
            this.pauseTutorialsSeconds.TabIndex = 4;
            // 
            // tutorialsDemoMode
            // 
            this.tutorialsDemoMode.AutoSize = true;
            this.tutorialsDemoMode.Location = new System.Drawing.Point(7, 106);
            this.tutorialsDemoMode.Margin = new System.Windows.Forms.Padding(4);
            this.tutorialsDemoMode.Name = "tutorialsDemoMode";
            this.tutorialsDemoMode.Size = new System.Drawing.Size(82, 17);
            this.tutorialsDemoMode.TabIndex = 6;
            this.tutorialsDemoMode.Text = "Demo mode (deprecated)";
            this.toolTip1.SetToolTip(this.tutorialsDemoMode, "Use the Tests tab's 'Tutorials only', 'Mode', and 'Run indefinitely' settings for more fine-grained control of demos");
            this.tutorialsDemoMode.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(147, 84);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(47, 13);
            this.label5.TabIndex = 5;
            this.label5.Text = "seconds";
            // 
            // pauseTutorialsDelay
            // 
            this.pauseTutorialsDelay.AutoSize = true;
            this.pauseTutorialsDelay.Location = new System.Drawing.Point(7, 82);
            this.pauseTutorialsDelay.Margin = new System.Windows.Forms.Padding(4);
            this.pauseTutorialsDelay.Name = "pauseTutorialsDelay";
            this.pauseTutorialsDelay.Size = new System.Drawing.Size(70, 17);
            this.pauseTutorialsDelay.TabIndex = 3;
            this.pauseTutorialsDelay.Text = "Pause for";
            this.pauseTutorialsDelay.UseVisualStyleBackColor = true;
            // 
            // pauseTutorialsScreenShots
            // 
            this.pauseTutorialsScreenShots.AutoSize = true;
            this.pauseTutorialsScreenShots.Checked = true;
            this.pauseTutorialsScreenShots.Location = new System.Drawing.Point(7, 23);
            this.pauseTutorialsScreenShots.Margin = new System.Windows.Forms.Padding(4);
            this.pauseTutorialsScreenShots.Name = "pauseTutorialsScreenShots";
            this.pauseTutorialsScreenShots.Size = new System.Drawing.Size(133, 17);
            this.pauseTutorialsScreenShots.TabIndex = 0;
            this.pauseTutorialsScreenShots.TabStop = true;
            this.pauseTutorialsScreenShots.Text = "Pause for screen shots";
            this.toolTip1.SetToolTip(this.pauseTutorialsScreenShots, "Interactively pauses the tutorial test at calls to PauseForScreenShot()");
            this.pauseTutorialsScreenShots.UseVisualStyleBackColor = true;
            this.pauseTutorialsScreenShots.CheckedChanged += new System.EventHandler(this.pauseTutorialsScreenShots_CheckedChanged);
            // 
            // runTutorials
            // 
            this.runTutorials.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runTutorials.Location = new System.Drawing.Point(596, 649);
            this.runTutorials.Margin = new System.Windows.Forms.Padding(4);
            this.runTutorials.Name = "runTutorials";
            this.runTutorials.Size = new System.Drawing.Size(100, 28);
            this.runTutorials.TabIndex = 1;
            this.runTutorials.Text = "Run";
            this.runTutorials.UseVisualStyleBackColor = true;
            this.runTutorials.Click += new System.EventHandler(this.RunOrStop_Clicked);
            // 
            // tabTests
            // 
            this.tabTests.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(195)))), ((int)(((byte)(220)))), ((int)(((byte)(210)))));
            this.tabTests.Controls.Add(this.runTests);
            this.tabTests.Controls.Add(this.buttonSelectFailedTestsTab);
            this.tabTests.Controls.Add(this.label17);
            this.tabTests.Controls.Add(this.groupBox15);
            this.tabTests.Controls.Add(this.windowsGroup);
            this.tabTests.Controls.Add(this.iterationsGroup);
            this.tabTests.Controls.Add(this.testsGroup);
            this.tabTests.Location = new System.Drawing.Point(4, 28);
            this.tabTests.Margin = new System.Windows.Forms.Padding(4);
            this.tabTests.Name = "tabTests";
            this.tabTests.Padding = new System.Windows.Forms.Padding(4);
            this.tabTests.Size = new System.Drawing.Size(709, 689);
            this.tabTests.TabIndex = 0;
            this.tabTests.Text = "Tests";
            // 
            // runTests
            // 
            this.runTests.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runTests.Location = new System.Drawing.Point(596, 649);
            this.runTests.Margin = new System.Windows.Forms.Padding(4);
            this.runTests.Name = "runTests";
            this.runTests.Size = new System.Drawing.Size(100, 28);
            this.runTests.TabIndex = 5;
            this.runTests.Text = "Run";
            this.toolTip1.SetToolTip(this.runTests, "run the selected tests, immediately");
            this.runTests.UseVisualStyleBackColor = true;
            this.runTests.Click += new System.EventHandler(this.RunOrStop_Clicked);
            // 
            // buttonSelectFailedTestsTab
            // 
            this.buttonSelectFailedTestsTab.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonSelectFailedTestsTab.Location = new System.Drawing.Point(16, 646);
            this.buttonSelectFailedTestsTab.Margin = new System.Windows.Forms.Padding(0);
            this.buttonSelectFailedTestsTab.Name = "buttonSelectFailedTestsTab";
            this.buttonSelectFailedTestsTab.Size = new System.Drawing.Size(152, 28);
            this.buttonSelectFailedTestsTab.TabIndex = 36;
            this.buttonSelectFailedTestsTab.Text = "Select failed tests";
            this.toolTip1.SetToolTip(this.buttonSelectFailedTestsTab, "Select failed tests and deselect all others");
            this.buttonSelectFailedTestsTab.UseVisualStyleBackColor = true;
            this.buttonSelectFailedTestsTab.Click += new System.EventHandler(this.SelectFailedTests);
            // 
            // label17
            // 
            this.label17.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label17.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label17.Location = new System.Drawing.Point(7, 4);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(687, 44);
            this.label17.TabIndex = 0;
            this.label17.Text = "Run Skyline tests";
            this.label17.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox15
            // 
            this.groupBox15.BackColor = System.Drawing.Color.Transparent;
            this.groupBox15.Controls.Add(this.testsTurkish);
            this.groupBox15.Controls.Add(this.testsFrench);
            this.groupBox15.Controls.Add(this.testsJapanese);
            this.groupBox15.Controls.Add(this.testsChinese);
            this.groupBox15.Controls.Add(this.testsEnglish);
            this.groupBox15.Location = new System.Drawing.Point(11, 308);
            this.groupBox15.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox15.Name = "groupBox15";
            this.groupBox15.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox15.Size = new System.Drawing.Size(280, 163);
            this.groupBox15.TabIndex = 3;
            this.groupBox15.TabStop = false;
            this.groupBox15.Text = "Language";
            // 
            // testsTurkish
            // 
            this.testsTurkish.AutoSize = true;
            this.testsTurkish.Location = new System.Drawing.Point(8, 135);
            this.testsTurkish.Margin = new System.Windows.Forms.Padding(4);
            this.testsTurkish.Name = "testsTurkish";
            this.testsTurkish.Size = new System.Drawing.Size(61, 17);
            this.testsTurkish.TabIndex = 4;
            this.testsTurkish.Text = "Turkish";
            this.testsTurkish.UseVisualStyleBackColor = true;
            // 
            // testsFrench
            // 
            this.testsFrench.AutoSize = true;
            this.testsFrench.Location = new System.Drawing.Point(8, 79);
            this.testsFrench.Margin = new System.Windows.Forms.Padding(4);
            this.testsFrench.Name = "testsFrench";
            this.testsFrench.Size = new System.Drawing.Size(59, 17);
            this.testsFrench.TabIndex = 2;
            this.testsFrench.Text = "French";
            this.testsFrench.UseVisualStyleBackColor = true;
            // 
            // testsJapanese
            // 
            this.testsJapanese.AutoSize = true;
            this.testsJapanese.Location = new System.Drawing.Point(8, 107);
            this.testsJapanese.Margin = new System.Windows.Forms.Padding(4);
            this.testsJapanese.Name = "testsJapanese";
            this.testsJapanese.Size = new System.Drawing.Size(72, 17);
            this.testsJapanese.TabIndex = 3;
            this.testsJapanese.Text = "Japanese";
            this.testsJapanese.UseVisualStyleBackColor = true;
            // 
            // testsChinese
            // 
            this.testsChinese.AutoSize = true;
            this.testsChinese.Location = new System.Drawing.Point(9, 51);
            this.testsChinese.Margin = new System.Windows.Forms.Padding(4);
            this.testsChinese.Name = "testsChinese";
            this.testsChinese.Size = new System.Drawing.Size(64, 17);
            this.testsChinese.TabIndex = 1;
            this.testsChinese.Text = "Chinese";
            this.testsChinese.UseVisualStyleBackColor = true;
            // 
            // testsEnglish
            // 
            this.testsEnglish.AutoSize = true;
            this.testsEnglish.Checked = true;
            this.testsEnglish.CheckState = System.Windows.Forms.CheckState.Checked;
            this.testsEnglish.Location = new System.Drawing.Point(9, 23);
            this.testsEnglish.Margin = new System.Windows.Forms.Padding(4);
            this.testsEnglish.Name = "testsEnglish";
            this.testsEnglish.Size = new System.Drawing.Size(60, 17);
            this.testsEnglish.TabIndex = 0;
            this.testsEnglish.Text = "English";
            this.testsEnglish.UseVisualStyleBackColor = true;
            // 
            // windowsGroup
            // 
            this.windowsGroup.Controls.Add(this.offscreen);
            this.windowsGroup.Location = new System.Drawing.Point(11, 242);
            this.windowsGroup.Margin = new System.Windows.Forms.Padding(4);
            this.windowsGroup.Name = "windowsGroup";
            this.windowsGroup.Padding = new System.Windows.Forms.Padding(4);
            this.windowsGroup.Size = new System.Drawing.Size(280, 58);
            this.windowsGroup.TabIndex = 2;
            this.windowsGroup.TabStop = false;
            this.windowsGroup.Text = "Windows";
            // 
            // offscreen
            // 
            this.offscreen.AutoSize = true;
            this.offscreen.Location = new System.Drawing.Point(8, 23);
            this.offscreen.Margin = new System.Windows.Forms.Padding(4);
            this.offscreen.Name = "offscreen";
            this.offscreen.Size = new System.Drawing.Size(75, 17);
            this.offscreen.TabIndex = 0;
            this.offscreen.Text = "Off screen";
            this.offscreen.UseVisualStyleBackColor = true;
            // 
            // iterationsGroup
            // 
            this.iterationsGroup.Controls.Add(this.recordAuditLogs);
            this.iterationsGroup.Controls.Add(this.testsRunSmallMoleculeVersions);
            this.iterationsGroup.Controls.Add(this.randomize);
            this.iterationsGroup.Controls.Add(this.repeat);
            this.iterationsGroup.Controls.Add(this.label6);
            this.iterationsGroup.Controls.Add(this.label3);
            this.iterationsGroup.Controls.Add(this.runLoopsCount);
            this.iterationsGroup.Controls.Add(this.label2);
            this.iterationsGroup.Controls.Add(this.runLoops);
            this.iterationsGroup.Controls.Add(this.runIndefinitely);
            this.iterationsGroup.Location = new System.Drawing.Point(11, 50);
            this.iterationsGroup.Margin = new System.Windows.Forms.Padding(4);
            this.iterationsGroup.Name = "iterationsGroup";
            this.iterationsGroup.Padding = new System.Windows.Forms.Padding(4);
            this.iterationsGroup.Size = new System.Drawing.Size(280, 184);
            this.iterationsGroup.TabIndex = 1;
            this.iterationsGroup.TabStop = false;
            this.iterationsGroup.Text = "Run options";
            // 
            // recordAuditLogs
            // 
            this.recordAuditLogs.AutoSize = true;
            this.recordAuditLogs.Location = new System.Drawing.Point(5, 127);
            this.recordAuditLogs.Name = "recordAuditLogs";
            this.recordAuditLogs.Size = new System.Drawing.Size(166, 17);
            this.recordAuditLogs.TabIndex = 10;
            this.recordAuditLogs.Text = "Record new tutorial audit logs";
            this.toolTip1.SetToolTip(this.recordAuditLogs, "Create new or updated audit logs for tutorial tests");
            this.recordAuditLogs.UseVisualStyleBackColor = true;
            // 
            // testsRunSmallMoleculeVersions
            // 
            this.testsRunSmallMoleculeVersions.AutoSize = true;
            this.testsRunSmallMoleculeVersions.Checked = true;
            this.testsRunSmallMoleculeVersions.CheckState = System.Windows.Forms.CheckState.Checked;
            this.testsRunSmallMoleculeVersions.Location = new System.Drawing.Point(5, 150);
            this.testsRunSmallMoleculeVersions.Name = "testsRunSmallMoleculeVersions";
            this.testsRunSmallMoleculeVersions.Size = new System.Drawing.Size(179, 17);
            this.testsRunSmallMoleculeVersions.TabIndex = 9;
            this.testsRunSmallMoleculeVersions.Text = "Run small molecule test versions";
            this.toolTip1.SetToolTip(this.testsRunSmallMoleculeVersions, "Include small molecule versions of  test when available");
            this.testsRunSmallMoleculeVersions.UseVisualStyleBackColor = true;
            // 
            // randomize
            // 
            this.randomize.AutoSize = true;
            this.randomize.Location = new System.Drawing.Point(5, 106);
            this.randomize.Name = "randomize";
            this.randomize.Size = new System.Drawing.Size(126, 17);
            this.randomize.TabIndex = 7;
            this.randomize.Text = "Randomize test order";
            this.randomize.UseVisualStyleBackColor = true;
            // 
            // repeat
            // 
            this.repeat.FormattingEnabled = true;
            this.repeat.Items.AddRange(new object[] {
            "1",
            "2",
            "5",
            "10",
            "20",
            "50",
            "100"});
            this.repeat.Location = new System.Drawing.Point(83, 75);
            this.repeat.Name = "repeat";
            this.repeat.Size = new System.Drawing.Size(52, 21);
            this.repeat.TabIndex = 5;
            this.toolTip1.SetToolTip(this.repeat, "Stress each test by running it multiple times before proceeding to next test.  Pe" +
        "rf tests only run once.");
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(141, 78);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(120, 13);
            this.label6.TabIndex = 6;
            this.label6.Text = "time(s) in a row per pass";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 78);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(74, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "Run each test";
            // 
            // runLoopsCount
            // 
            this.runLoopsCount.Location = new System.Drawing.Point(64, 23);
            this.runLoopsCount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.runLoopsCount.Name = "runLoopsCount";
            this.runLoopsCount.Size = new System.Drawing.Size(41, 20);
            this.runLoopsCount.TabIndex = 1;
            this.runLoopsCount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(111, 26);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(40, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "passes";
            // 
            // runLoops
            // 
            this.runLoops.AutoSize = true;
            this.runLoops.Checked = true;
            this.runLoops.Location = new System.Drawing.Point(8, 23);
            this.runLoops.Margin = new System.Windows.Forms.Padding(4);
            this.runLoops.Name = "runLoops";
            this.runLoops.Size = new System.Drawing.Size(45, 17);
            this.runLoops.TabIndex = 0;
            this.runLoops.TabStop = true;
            this.runLoops.Text = "Run";
            this.runLoops.UseVisualStyleBackColor = true;
            // 
            // runIndefinitely
            // 
            this.runIndefinitely.AutoSize = true;
            this.runIndefinitely.Location = new System.Drawing.Point(8, 48);
            this.runIndefinitely.Margin = new System.Windows.Forms.Padding(4);
            this.runIndefinitely.Name = "runIndefinitely";
            this.runIndefinitely.Size = new System.Drawing.Size(97, 17);
            this.runIndefinitely.TabIndex = 3;
            this.runIndefinitely.Text = "Run indefinitely";
            this.runIndefinitely.UseVisualStyleBackColor = true;
            // 
            // testsGroup
            // 
            this.testsGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.testsGroup.Controls.Add(this.runMode);
            this.testsGroup.Controls.Add(this.label21);
            this.testsGroup.Controls.Add(this.showTutorialsOnly);
            this.testsGroup.Controls.Add(this.testsTree);
            this.testsGroup.Controls.Add(this.skipCheckedTests);
            this.testsGroup.Controls.Add(this.runCheckedTests);
            this.testsGroup.Location = new System.Drawing.Point(299, 50);
            this.testsGroup.Margin = new System.Windows.Forms.Padding(4);
            this.testsGroup.Name = "testsGroup";
            this.testsGroup.Padding = new System.Windows.Forms.Padding(4);
            this.testsGroup.Size = new System.Drawing.Size(402, 591);
            this.testsGroup.TabIndex = 4;
            this.testsGroup.TabStop = false;
            this.testsGroup.Text = "Tests";
            // 
            // runMode
            // 
            this.runMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.runMode.Items.AddRange(new object[] {
            "Test",
            "Quality",
            "Demo",
            "Covershot"});
            this.runMode.Location = new System.Drawing.Point(276, 553);
            this.runMode.Name = "runMode";
            this.runMode.Size = new System.Drawing.Size(121, 21);
            this.runMode.TabIndex = 0;
            // 
            // label21
            // 
            this.label21.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(273, 536);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(37, 13);
            this.label21.TabIndex = 35;
            this.label21.Text = "Mode:";
            // 
            // showTutorialsOnly
            // 
            this.showTutorialsOnly.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.showTutorialsOnly.AutoSize = true;
            this.showTutorialsOnly.Location = new System.Drawing.Point(307, 1);
            this.showTutorialsOnly.Name = "showTutorialsOnly";
            this.showTutorialsOnly.Size = new System.Drawing.Size(88, 17);
            this.showTutorialsOnly.TabIndex = 34;
            this.showTutorialsOnly.Text = "Tutorials only";
            this.showTutorialsOnly.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.showTutorialsOnly.UseVisualStyleBackColor = true;
            this.showTutorialsOnly.CheckedChanged += new System.EventHandler(this.showTutorialsOnly_CheckedChanged);
            // 
            // testsTree
            // 
            this.testsTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.testsTree.CheckBoxes = true;
            this.testsTree.Location = new System.Drawing.Point(8, 23);
            this.testsTree.Margin = new System.Windows.Forms.Padding(4);
            this.testsTree.Name = "testsTree";
            this.testsTree.Size = new System.Drawing.Size(387, 503);
            this.testsTree.TabIndex = 15;
            this.testsTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.node_AfterCheck);
            // 
            // skipCheckedTests
            // 
            this.skipCheckedTests.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.skipCheckedTests.AutoSize = true;
            this.skipCheckedTests.Location = new System.Drawing.Point(8, 560);
            this.skipCheckedTests.Margin = new System.Windows.Forms.Padding(4);
            this.skipCheckedTests.Name = "skipCheckedTests";
            this.skipCheckedTests.Size = new System.Drawing.Size(116, 17);
            this.skipCheckedTests.TabIndex = 2;
            this.skipCheckedTests.Text = "Skip checked tests";
            this.skipCheckedTests.UseVisualStyleBackColor = true;
            // 
            // runCheckedTests
            // 
            this.runCheckedTests.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.runCheckedTests.AutoSize = true;
            this.runCheckedTests.Checked = true;
            this.runCheckedTests.Location = new System.Drawing.Point(8, 534);
            this.runCheckedTests.Margin = new System.Windows.Forms.Padding(4);
            this.runCheckedTests.Name = "runCheckedTests";
            this.runCheckedTests.Size = new System.Drawing.Size(115, 17);
            this.runCheckedTests.TabIndex = 1;
            this.runCheckedTests.TabStop = true;
            this.runCheckedTests.Text = "Run checked tests";
            this.runCheckedTests.UseVisualStyleBackColor = true;
            // 
            // tabBuild
            // 
            this.tabBuild.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(195)))), ((int)(((byte)(210)))), ((int)(((byte)(195)))));
            this.tabBuild.Controls.Add(this.groupBox2);
            this.tabBuild.Controls.Add(this.label14);
            this.tabBuild.Controls.Add(this.groupBox10);
            this.tabBuild.Controls.Add(this.groupBox16);
            this.tabBuild.Controls.Add(this.runBuild);
            this.tabBuild.Controls.Add(this.groupBox6);
            this.tabBuild.Controls.Add(this.groupBox5);
            this.tabBuild.Location = new System.Drawing.Point(4, 28);
            this.tabBuild.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabBuild.Name = "tabBuild";
            this.tabBuild.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabBuild.Size = new System.Drawing.Size(709, 689);
            this.tabBuild.TabIndex = 3;
            this.tabBuild.Text = "Build";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.startSln);
            this.groupBox2.Controls.Add(this.runBuildVerificationTests);
            this.groupBox2.Location = new System.Drawing.Point(153, 245);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox2.Size = new System.Drawing.Size(543, 74);
            this.groupBox2.TabIndex = 31;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Options";
            // 
            // startSln
            // 
            this.startSln.AutoSize = true;
            this.startSln.Location = new System.Drawing.Point(7, 42);
            this.startSln.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.startSln.Name = "startSln";
            this.startSln.Size = new System.Drawing.Size(213, 17);
            this.startSln.TabIndex = 27;
            this.startSln.Text = "Open Skyline in Visual Studio after build";
            this.startSln.UseVisualStyleBackColor = true;
            // 
            // runBuildVerificationTests
            // 
            this.runBuildVerificationTests.AutoSize = true;
            this.runBuildVerificationTests.Checked = true;
            this.runBuildVerificationTests.CheckState = System.Windows.Forms.CheckState.Checked;
            this.runBuildVerificationTests.Location = new System.Drawing.Point(7, 21);
            this.runBuildVerificationTests.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.runBuildVerificationTests.Name = "runBuildVerificationTests";
            this.runBuildVerificationTests.Size = new System.Drawing.Size(150, 17);
            this.runBuildVerificationTests.TabIndex = 26;
            this.runBuildVerificationTests.Text = "Run build verification tests";
            this.runBuildVerificationTests.UseVisualStyleBackColor = true;
            // 
            // label14
            // 
            this.label14.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label14.Location = new System.Drawing.Point(7, 4);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(689, 44);
            this.label14.TabIndex = 30;
            this.label14.Text = "Build Skyline";
            this.label14.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox10
            // 
            this.groupBox10.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox10.Controls.Add(this.labelSpecifyPath);
            this.groupBox10.Controls.Add(this.buttonDeleteBuild);
            this.groupBox10.Controls.Add(this.buildRoot);
            this.groupBox10.Controls.Add(this.buttonBrowseBuild);
            this.groupBox10.Location = new System.Drawing.Point(11, 144);
            this.groupBox10.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox10.Name = "groupBox10";
            this.groupBox10.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox10.Size = new System.Drawing.Size(685, 93);
            this.groupBox10.TabIndex = 29;
            this.groupBox10.TabStop = false;
            this.groupBox10.Text = "Build root folder";
            // 
            // labelSpecifyPath
            // 
            this.labelSpecifyPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelSpecifyPath.Location = new System.Drawing.Point(9, 45);
            this.labelSpecifyPath.Name = "labelSpecifyPath";
            this.labelSpecifyPath.Size = new System.Drawing.Size(484, 28);
            this.labelSpecifyPath.TabIndex = 28;
            this.labelSpecifyPath.Text = "(Specify absolute path or path relative to User folder)";
            this.labelSpecifyPath.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // buttonDeleteBuild
            // 
            this.buttonDeleteBuild.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDeleteBuild.Enabled = false;
            this.buttonDeleteBuild.Location = new System.Drawing.Point(500, 54);
            this.buttonDeleteBuild.Margin = new System.Windows.Forms.Padding(4);
            this.buttonDeleteBuild.Name = "buttonDeleteBuild";
            this.buttonDeleteBuild.Size = new System.Drawing.Size(101, 28);
            this.buttonDeleteBuild.TabIndex = 27;
            this.buttonDeleteBuild.Text = "Delete root";
            this.buttonDeleteBuild.UseVisualStyleBackColor = true;
            this.buttonDeleteBuild.Click += new System.EventHandler(this.buttonDeleteBuild_Click);
            // 
            // buildRoot
            // 
            this.buildRoot.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buildRoot.Location = new System.Drawing.Point(9, 21);
            this.buildRoot.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buildRoot.Name = "buildRoot";
            this.buildRoot.Size = new System.Drawing.Size(484, 20);
            this.buildRoot.TabIndex = 3;
            this.buildRoot.Text = "Documents\\SkylineBuild";
            // 
            // buttonBrowseBuild
            // 
            this.buttonBrowseBuild.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonBrowseBuild.Location = new System.Drawing.Point(500, 18);
            this.buttonBrowseBuild.Margin = new System.Windows.Forms.Padding(4);
            this.buttonBrowseBuild.Name = "buttonBrowseBuild";
            this.buttonBrowseBuild.Size = new System.Drawing.Size(101, 28);
            this.buttonBrowseBuild.TabIndex = 26;
            this.buttonBrowseBuild.Text = "Browse...";
            this.buttonBrowseBuild.UseVisualStyleBackColor = true;
            this.buttonBrowseBuild.Click += new System.EventHandler(this.buttonBrowseBuild_Click);
            // 
            // groupBox16
            // 
            this.groupBox16.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox16.Controls.Add(this.incrementalBuild);
            this.groupBox16.Controls.Add(this.updateBuild);
            this.groupBox16.Controls.Add(this.nukeBuild);
            this.groupBox16.Location = new System.Drawing.Point(11, 327);
            this.groupBox16.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox16.Name = "groupBox16";
            this.groupBox16.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox16.Size = new System.Drawing.Size(685, 100);
            this.groupBox16.TabIndex = 28;
            this.groupBox16.TabStop = false;
            this.groupBox16.Text = "Code synchronizaton";
            // 
            // incrementalBuild
            // 
            this.incrementalBuild.AutoSize = true;
            this.incrementalBuild.Location = new System.Drawing.Point(7, 71);
            this.incrementalBuild.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.incrementalBuild.Name = "incrementalBuild";
            this.incrementalBuild.Size = new System.Drawing.Size(117, 17);
            this.incrementalBuild.TabIndex = 6;
            this.incrementalBuild.Text = "Incremental re-build";
            this.incrementalBuild.UseVisualStyleBackColor = true;
            // 
            // updateBuild
            // 
            this.updateBuild.AutoSize = true;
            this.updateBuild.Location = new System.Drawing.Point(7, 46);
            this.updateBuild.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.updateBuild.Name = "updateBuild";
            this.updateBuild.Size = new System.Drawing.Size(165, 17);
            this.updateBuild.TabIndex = 5;
            this.updateBuild.Text = "Update (Sync before building)";
            this.updateBuild.UseVisualStyleBackColor = true;
            // 
            // nukeBuild
            // 
            this.nukeBuild.AutoSize = true;
            this.nukeBuild.Checked = true;
            this.nukeBuild.Location = new System.Drawing.Point(7, 21);
            this.nukeBuild.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nukeBuild.Name = "nukeBuild";
            this.nukeBuild.Size = new System.Drawing.Size(178, 17);
            this.nukeBuild.TabIndex = 4;
            this.nukeBuild.TabStop = true;
            this.nukeBuild.Text = "Nuke (Checkout before building)";
            this.nukeBuild.UseVisualStyleBackColor = true;
            // 
            // runBuild
            // 
            this.runBuild.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runBuild.Location = new System.Drawing.Point(596, 649);
            this.runBuild.Margin = new System.Windows.Forms.Padding(4);
            this.runBuild.Name = "runBuild";
            this.runBuild.Size = new System.Drawing.Size(100, 28);
            this.runBuild.TabIndex = 22;
            this.runBuild.Text = "Run";
            this.runBuild.UseVisualStyleBackColor = true;
            this.runBuild.Click += new System.EventHandler(this.RunOrStop_Clicked);
            // 
            // groupBox6
            // 
            this.groupBox6.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox6.Controls.Add(this.buildBranch);
            this.groupBox6.Controls.Add(this.buildTrunk);
            this.groupBox6.Controls.Add(this.branchUrl);
            this.groupBox6.Location = new System.Drawing.Point(11, 50);
            this.groupBox6.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox6.Size = new System.Drawing.Size(685, 86);
            this.groupBox6.TabIndex = 21;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Source";
            // 
            // buildBranch
            // 
            this.buildBranch.AutoSize = true;
            this.buildBranch.Location = new System.Drawing.Point(9, 50);
            this.buildBranch.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buildBranch.Name = "buildBranch";
            this.buildBranch.Size = new System.Drawing.Size(59, 17);
            this.buildBranch.TabIndex = 4;
            this.buildBranch.Text = "Branch";
            this.buildBranch.UseVisualStyleBackColor = true;
            // 
            // buildTrunk
            // 
            this.buildTrunk.AutoSize = true;
            this.buildTrunk.Checked = true;
            this.buildTrunk.Location = new System.Drawing.Point(9, 23);
            this.buildTrunk.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buildTrunk.Name = "buildTrunk";
            this.buildTrunk.Size = new System.Drawing.Size(53, 17);
            this.buildTrunk.TabIndex = 3;
            this.buildTrunk.TabStop = true;
            this.buildTrunk.Text = "Trunk";
            this.buildTrunk.UseVisualStyleBackColor = true;
            // 
            // branchUrl
            // 
            this.branchUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.branchUrl.Location = new System.Drawing.Point(81, 49);
            this.branchUrl.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.branchUrl.Name = "branchUrl";
            this.branchUrl.Size = new System.Drawing.Size(595, 20);
            this.branchUrl.TabIndex = 2;
            this.branchUrl.Text = "https://github.com/ProteoWizard/pwiz/tree/BRANCHNAME";
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.build64);
            this.groupBox5.Controls.Add(this.build32);
            this.groupBox5.Location = new System.Drawing.Point(11, 245);
            this.groupBox5.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox5.Size = new System.Drawing.Size(134, 74);
            this.groupBox5.TabIndex = 20;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Architecture";
            // 
            // build64
            // 
            this.build64.AutoSize = true;
            this.build64.Location = new System.Drawing.Point(9, 46);
            this.build64.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.build64.Name = "build64";
            this.build64.Size = new System.Drawing.Size(52, 17);
            this.build64.TabIndex = 27;
            this.build64.Text = "64 bit";
            this.build64.UseVisualStyleBackColor = true;
            // 
            // build32
            // 
            this.build32.AutoSize = true;
            this.build32.Checked = true;
            this.build32.CheckState = System.Windows.Forms.CheckState.Checked;
            this.build32.Location = new System.Drawing.Point(9, 21);
            this.build32.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.build32.Name = "build32";
            this.build32.Size = new System.Drawing.Size(52, 17);
            this.build32.TabIndex = 26;
            this.build32.Text = "32 bit";
            this.build32.UseVisualStyleBackColor = true;
            // 
            // tabQuality
            // 
            this.tabQuality.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(212)))), ((int)(((byte)(190)))));
            this.tabQuality.Controls.Add(this.panel2);
            this.tabQuality.Controls.Add(this.qualityTableLayout);
            this.tabQuality.Controls.Add(this.label18);
            this.tabQuality.Controls.Add(this.runQuality);
            this.tabQuality.Location = new System.Drawing.Point(4, 28);
            this.tabQuality.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabQuality.Name = "tabQuality";
            this.tabQuality.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabQuality.Size = new System.Drawing.Size(709, 689);
            this.tabQuality.TabIndex = 4;
            this.tabQuality.Text = "Quality";
            // 
            // panel2
            // 
            this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.panel2.Controls.Add(this.radioQualityHandles);
            this.panel2.Controls.Add(this.radioQualityMemory);
            this.panel2.Location = new System.Drawing.Point(9, 648);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(137, 20);
            this.panel2.TabIndex = 38;
            // 
            // radioQualityHandles
            // 
            this.radioQualityHandles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioQualityHandles.AutoSize = true;
            this.radioQualityHandles.Location = new System.Drawing.Point(70, 0);
            this.radioQualityHandles.Name = "radioQualityHandles";
            this.radioQualityHandles.Size = new System.Drawing.Size(64, 17);
            this.radioQualityHandles.TabIndex = 37;
            this.radioQualityHandles.Text = "Handles";
            this.radioQualityHandles.UseVisualStyleBackColor = true;
            this.radioQualityHandles.CheckedChanged += new System.EventHandler(this.radioQualityHandles_CheckedChanged);
            // 
            // radioQualityMemory
            // 
            this.radioQualityMemory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioQualityMemory.AutoSize = true;
            this.radioQualityMemory.Checked = true;
            this.radioQualityMemory.Location = new System.Drawing.Point(2, 0);
            this.radioQualityMemory.Name = "radioQualityMemory";
            this.radioQualityMemory.Size = new System.Drawing.Size(62, 17);
            this.radioQualityMemory.TabIndex = 36;
            this.radioQualityMemory.TabStop = true;
            this.radioQualityMemory.Text = "Memory";
            this.radioQualityMemory.UseVisualStyleBackColor = true;
            this.radioQualityMemory.CheckedChanged += new System.EventHandler(this.radioQualityMemory_CheckedChanged);
            // 
            // qualityTableLayout
            // 
            this.qualityTableLayout.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.qualityTableLayout.ColumnCount = 1;
            this.qualityTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.qualityTableLayout.Controls.Add(this.panel1, 0, 0);
            this.qualityTableLayout.Controls.Add(this.panelMemoryGraph, 0, 1);
            this.qualityTableLayout.Location = new System.Drawing.Point(9, 51);
            this.qualityTableLayout.Margin = new System.Windows.Forms.Padding(0);
            this.qualityTableLayout.Name = "qualityTableLayout";
            this.qualityTableLayout.RowCount = 2;
            this.qualityTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 46.80412F));
            this.qualityTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 53.19588F));
            this.qualityTableLayout.Size = new System.Drawing.Size(691, 594);
            this.qualityTableLayout.TabIndex = 32;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.groupBox7);
            this.panel1.Controls.Add(this.groupBox11);
            this.panel1.Controls.Add(this.groupBox9);
            this.panel1.Controls.Add(this.groupBox8);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(691, 278);
            this.panel1.TabIndex = 0;
            // 
            // groupBox7
            // 
            this.groupBox7.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox7.Controls.Add(this.qualityTestName);
            this.groupBox7.Controls.Add(this.qualityThumbnail);
            this.groupBox7.Location = new System.Drawing.Point(376, 0);
            this.groupBox7.Name = "groupBox7";
            this.groupBox7.Size = new System.Drawing.Size(315, 274);
            this.groupBox7.TabIndex = 35;
            this.groupBox7.TabStop = false;
            this.groupBox7.Text = "Skyline windows";
            // 
            // qualityTestName
            // 
            this.qualityTestName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.qualityTestName.AutoEllipsis = true;
            this.qualityTestName.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.qualityTestName.Location = new System.Drawing.Point(6, 251);
            this.qualityTestName.Name = "qualityTestName";
            this.qualityTestName.Size = new System.Drawing.Size(303, 20);
            this.qualityTestName.TabIndex = 35;
            this.qualityTestName.Text = "test name";
            // 
            // qualityThumbnail
            // 
            this.qualityThumbnail.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.qualityThumbnail.Location = new System.Drawing.Point(8, 19);
            this.qualityThumbnail.Name = "qualityThumbnail";
            this.qualityThumbnail.ProcessId = 0;
            this.qualityThumbnail.Size = new System.Drawing.Size(301, 229);
            this.qualityThumbnail.TabIndex = 34;
            // 
            // groupBox11
            // 
            this.groupBox11.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBox11.Controls.Add(this.buttonViewLog);
            this.groupBox11.Controls.Add(this.labelLeaks);
            this.groupBox11.Controls.Add(this.labelFailures);
            this.groupBox11.Controls.Add(this.labelTestsRun);
            this.groupBox11.Controls.Add(this.labelDuration);
            this.groupBox11.Controls.Add(this.label12);
            this.groupBox11.Controls.Add(this.label13);
            this.groupBox11.Controls.Add(this.label10);
            this.groupBox11.Controls.Add(this.label9);
            this.groupBox11.Location = new System.Drawing.Point(232, 0);
            this.groupBox11.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox11.Name = "groupBox11";
            this.groupBox11.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox11.Size = new System.Drawing.Size(137, 274);
            this.groupBox11.TabIndex = 32;
            this.groupBox11.TabStop = false;
            this.groupBox11.Text = "Run results";
            // 
            // buttonViewLog
            // 
            this.buttonViewLog.Location = new System.Drawing.Point(20, 118);
            this.buttonViewLog.Margin = new System.Windows.Forms.Padding(4);
            this.buttonViewLog.Name = "buttonViewLog";
            this.buttonViewLog.Size = new System.Drawing.Size(96, 26);
            this.buttonViewLog.TabIndex = 30;
            this.buttonViewLog.Text = "View log";
            this.buttonViewLog.UseVisualStyleBackColor = true;
            this.buttonViewLog.Click += new System.EventHandler(this.buttonOpenLog_Click);
            // 
            // labelLeaks
            // 
            this.labelLeaks.AutoSize = true;
            this.labelLeaks.Location = new System.Drawing.Point(76, 74);
            this.labelLeaks.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelLeaks.Name = "labelLeaks";
            this.labelLeaks.Size = new System.Drawing.Size(13, 13);
            this.labelLeaks.TabIndex = 12;
            this.labelLeaks.Text = "0";
            // 
            // labelFailures
            // 
            this.labelFailures.AutoSize = true;
            this.labelFailures.Location = new System.Drawing.Point(76, 58);
            this.labelFailures.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelFailures.Name = "labelFailures";
            this.labelFailures.Size = new System.Drawing.Size(13, 13);
            this.labelFailures.TabIndex = 11;
            this.labelFailures.Text = "0";
            // 
            // labelTestsRun
            // 
            this.labelTestsRun.AutoSize = true;
            this.labelTestsRun.Location = new System.Drawing.Point(76, 42);
            this.labelTestsRun.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelTestsRun.Name = "labelTestsRun";
            this.labelTestsRun.Size = new System.Drawing.Size(13, 13);
            this.labelTestsRun.TabIndex = 9;
            this.labelTestsRun.Text = "0";
            // 
            // labelDuration
            // 
            this.labelDuration.AutoSize = true;
            this.labelDuration.Location = new System.Drawing.Point(76, 25);
            this.labelDuration.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDuration.Name = "labelDuration";
            this.labelDuration.Size = new System.Drawing.Size(28, 13);
            this.labelDuration.TabIndex = 8;
            this.labelDuration.Text = "0:00";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(8, 74);
            this.label12.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(39, 13);
            this.label12.TabIndex = 6;
            this.label12.Text = "Leaks:";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(8, 58);
            this.label13.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(46, 13);
            this.label13.TabIndex = 5;
            this.label13.Text = "Failures:";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(8, 42);
            this.label10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(54, 13);
            this.label10.TabIndex = 3;
            this.label10.Text = "Tests run:";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(8, 25);
            this.label9.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(50, 13);
            this.label9.TabIndex = 2;
            this.label9.Text = "Duration:";
            // 
            // groupBox9
            // 
            this.groupBox9.Controls.Add(this.qualityAllTests);
            this.groupBox9.Controls.Add(this.qualityChooseTests);
            this.groupBox9.Location = new System.Drawing.Point(0, 162);
            this.groupBox9.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox9.Name = "groupBox9";
            this.groupBox9.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox9.Size = new System.Drawing.Size(224, 76);
            this.groupBox9.TabIndex = 31;
            this.groupBox9.TabStop = false;
            this.groupBox9.Text = "Test selection";
            // 
            // qualityAllTests
            // 
            this.qualityAllTests.AutoSize = true;
            this.qualityAllTests.Checked = true;
            this.qualityAllTests.Location = new System.Drawing.Point(7, 19);
            this.qualityAllTests.Margin = new System.Windows.Forms.Padding(4);
            this.qualityAllTests.Name = "qualityAllTests";
            this.qualityAllTests.Size = new System.Drawing.Size(61, 17);
            this.qualityAllTests.TabIndex = 1;
            this.qualityAllTests.TabStop = true;
            this.qualityAllTests.Text = "All tests";
            this.qualityAllTests.UseVisualStyleBackColor = true;
            // 
            // qualityChooseTests
            // 
            this.qualityChooseTests.AutoSize = true;
            this.qualityChooseTests.Location = new System.Drawing.Point(7, 38);
            this.qualityChooseTests.Margin = new System.Windows.Forms.Padding(4);
            this.qualityChooseTests.Name = "qualityChooseTests";
            this.qualityChooseTests.Size = new System.Drawing.Size(159, 17);
            this.qualityChooseTests.TabIndex = 0;
            this.qualityChooseTests.Text = "Choose tests (see Tests tab)";
            this.qualityChooseTests.UseVisualStyleBackColor = true;
            // 
            // groupBox8
            // 
            this.groupBox8.Controls.Add(this.qualityRunSmallMoleculeVersions);
            this.groupBox8.Controls.Add(this.qualityPassIndefinite);
            this.groupBox8.Controls.Add(this.qualityPassCount);
            this.groupBox8.Controls.Add(this.pass1);
            this.groupBox8.Controls.Add(this.pass0);
            this.groupBox8.Controls.Add(this.label7);
            this.groupBox8.Controls.Add(this.qualityPassDefinite);
            this.groupBox8.Location = new System.Drawing.Point(0, 0);
            this.groupBox8.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox8.Name = "groupBox8";
            this.groupBox8.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox8.Size = new System.Drawing.Size(224, 144);
            this.groupBox8.TabIndex = 30;
            this.groupBox8.TabStop = false;
            this.groupBox8.Text = "Run options";
            // 
            // qualityRunSmallMoleculeVersions
            // 
            this.qualityRunSmallMoleculeVersions.AutoSize = true;
            this.qualityRunSmallMoleculeVersions.Checked = true;
            this.qualityRunSmallMoleculeVersions.CheckState = System.Windows.Forms.CheckState.Checked;
            this.qualityRunSmallMoleculeVersions.Location = new System.Drawing.Point(7, 118);
            this.qualityRunSmallMoleculeVersions.Name = "qualityRunSmallMoleculeVersions";
            this.qualityRunSmallMoleculeVersions.Size = new System.Drawing.Size(179, 17);
            this.qualityRunSmallMoleculeVersions.TabIndex = 14;
            this.qualityRunSmallMoleculeVersions.Text = "Run small molecule test versions";
            this.toolTip1.SetToolTip(this.qualityRunSmallMoleculeVersions, "Include small molecule versions of tests when available");
            this.qualityRunSmallMoleculeVersions.UseVisualStyleBackColor = true;
            // 
            // qualityPassIndefinite
            // 
            this.qualityPassIndefinite.AutoSize = true;
            this.qualityPassIndefinite.Location = new System.Drawing.Point(8, 48);
            this.qualityPassIndefinite.Margin = new System.Windows.Forms.Padding(4);
            this.qualityPassIndefinite.Name = "qualityPassIndefinite";
            this.qualityPassIndefinite.Size = new System.Drawing.Size(97, 17);
            this.qualityPassIndefinite.TabIndex = 12;
            this.qualityPassIndefinite.Text = "Run indefinitely";
            this.qualityPassIndefinite.UseVisualStyleBackColor = true;
            // 
            // qualityPassCount
            // 
            this.qualityPassCount.Location = new System.Drawing.Point(60, 23);
            this.qualityPassCount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.qualityPassCount.Name = "qualityPassCount";
            this.qualityPassCount.Size = new System.Drawing.Size(41, 20);
            this.qualityPassCount.TabIndex = 11;
            this.qualityPassCount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // pass1
            // 
            this.pass1.AutoSize = true;
            this.pass1.Checked = true;
            this.pass1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.pass1.Location = new System.Drawing.Point(8, 95);
            this.pass1.Name = "pass1";
            this.pass1.Size = new System.Drawing.Size(163, 17);
            this.pass1.TabIndex = 10;
            this.pass1.Text = "Pass 1: Detect memory leaks";
            this.pass1.UseVisualStyleBackColor = true;
            // 
            // pass0
            // 
            this.pass0.AutoSize = true;
            this.pass0.Checked = true;
            this.pass0.CheckState = System.Windows.Forms.CheckState.Checked;
            this.pass0.Location = new System.Drawing.Point(8, 72);
            this.pass0.Name = "pass0";
            this.pass0.Size = new System.Drawing.Size(161, 17);
            this.pass0.TabIndex = 9;
            this.pass0.Text = "Pass 0: French / no vendors";
            this.pass0.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(108, 25);
            this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(40, 13);
            this.label7.TabIndex = 8;
            this.label7.Text = "passes";
            // 
            // qualityPassDefinite
            // 
            this.qualityPassDefinite.AutoSize = true;
            this.qualityPassDefinite.Checked = true;
            this.qualityPassDefinite.Location = new System.Drawing.Point(8, 23);
            this.qualityPassDefinite.Margin = new System.Windows.Forms.Padding(4);
            this.qualityPassDefinite.Name = "qualityPassDefinite";
            this.qualityPassDefinite.Size = new System.Drawing.Size(45, 17);
            this.qualityPassDefinite.TabIndex = 1;
            this.qualityPassDefinite.TabStop = true;
            this.qualityPassDefinite.Text = "Run";
            this.qualityPassDefinite.UseVisualStyleBackColor = true;
            // 
            // panelMemoryGraph
            // 
            this.panelMemoryGraph.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelMemoryGraph.Location = new System.Drawing.Point(0, 284);
            this.panelMemoryGraph.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.panelMemoryGraph.Name = "panelMemoryGraph";
            this.panelMemoryGraph.Size = new System.Drawing.Size(691, 310);
            this.panelMemoryGraph.TabIndex = 32;
            // 
            // label18
            // 
            this.label18.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label18.Location = new System.Drawing.Point(7, 4);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(693, 44);
            this.label18.TabIndex = 31;
            this.label18.Text = "Skyline quality checks";
            this.label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // runQuality
            // 
            this.runQuality.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runQuality.Location = new System.Drawing.Point(596, 649);
            this.runQuality.Margin = new System.Windows.Forms.Padding(4);
            this.runQuality.Name = "runQuality";
            this.runQuality.Size = new System.Drawing.Size(100, 28);
            this.runQuality.TabIndex = 26;
            this.runQuality.Text = "Run";
            this.runQuality.UseVisualStyleBackColor = true;
            this.runQuality.Click += new System.EventHandler(this.RunOrStop_Clicked);
            // 
            // tabNightly
            // 
            this.tabNightly.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(170)))), ((int)(((byte)(190)))), ((int)(((byte)(200)))));
            this.tabNightly.Controls.Add(this.nightlyExit);
            this.tabNightly.Controls.Add(this.buttonDeleteNightlyTask);
            this.tabNightly.Controls.Add(this.nightlyTableLayout);
            this.tabNightly.Controls.Add(this.label33);
            this.tabNightly.Controls.Add(this.runNightly);
            this.tabNightly.Location = new System.Drawing.Point(4, 28);
            this.tabNightly.Name = "tabNightly";
            this.tabNightly.Padding = new System.Windows.Forms.Padding(3);
            this.tabNightly.Size = new System.Drawing.Size(709, 689);
            this.tabNightly.TabIndex = 7;
            this.tabNightly.Text = "Nightly";
            // 
            // nightlyExit
            // 
            this.nightlyExit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.nightlyExit.AutoSize = true;
            this.nightlyExit.Location = new System.Drawing.Point(200, 656);
            this.nightlyExit.Name = "nightlyExit";
            this.nightlyExit.Size = new System.Drawing.Size(73, 17);
            this.nightlyExit.TabIndex = 37;
            this.nightlyExit.Text = "nightlyExit";
            this.nightlyExit.UseVisualStyleBackColor = true;
            this.nightlyExit.Visible = false;
            // 
            // buttonDeleteNightlyTask
            // 
            this.buttonDeleteNightlyTask.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonDeleteNightlyTask.Enabled = false;
            this.buttonDeleteNightlyTask.Location = new System.Drawing.Point(11, 649);
            this.buttonDeleteNightlyTask.Margin = new System.Windows.Forms.Padding(4);
            this.buttonDeleteNightlyTask.Name = "buttonDeleteNightlyTask";
            this.buttonDeleteNightlyTask.Size = new System.Drawing.Size(180, 28);
            this.buttonDeleteNightlyTask.TabIndex = 36;
            this.buttonDeleteNightlyTask.Text = "Delete nightly task";
            this.buttonDeleteNightlyTask.UseVisualStyleBackColor = true;
            this.buttonDeleteNightlyTask.Click += new System.EventHandler(this.buttonDeleteNightlyTask_Click);
            // 
            // nightlyTableLayout
            // 
            this.nightlyTableLayout.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.nightlyTableLayout.ColumnCount = 1;
            this.nightlyTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.nightlyTableLayout.Controls.Add(this.groupBox17, 0, 1);
            this.nightlyTableLayout.Controls.Add(this.panel3, 0, 0);
            this.nightlyTableLayout.Location = new System.Drawing.Point(9, 51);
            this.nightlyTableLayout.Margin = new System.Windows.Forms.Padding(0);
            this.nightlyTableLayout.Name = "nightlyTableLayout";
            this.nightlyTableLayout.RowCount = 2;
            this.nightlyTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 76.53277F));
            this.nightlyTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 23.46723F));
            this.nightlyTableLayout.Size = new System.Drawing.Size(687, 594);
            this.nightlyTableLayout.TabIndex = 35;
            // 
            // groupBox17
            // 
            this.groupBox17.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox17.Controls.Add(this.nightlyTrendsTable);
            this.groupBox17.Location = new System.Drawing.Point(0, 454);
            this.groupBox17.Margin = new System.Windows.Forms.Padding(0);
            this.groupBox17.Name = "groupBox17";
            this.groupBox17.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox17.Size = new System.Drawing.Size(687, 140);
            this.groupBox17.TabIndex = 31;
            this.groupBox17.TabStop = false;
            this.groupBox17.Text = "Trends";
            // 
            // nightlyTrendsTable
            // 
            this.nightlyTrendsTable.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.nightlyTrendsTable.ColumnCount = 4;
            this.nightlyTrendsTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.nightlyTrendsTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.nightlyTrendsTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.nightlyTrendsTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.nightlyTrendsTable.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.FixedSize;
            this.nightlyTrendsTable.Location = new System.Drawing.Point(4, 17);
            this.nightlyTrendsTable.Margin = new System.Windows.Forms.Padding(0);
            this.nightlyTrendsTable.Name = "nightlyTrendsTable";
            this.nightlyTrendsTable.RowCount = 1;
            this.nightlyTrendsTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.nightlyTrendsTable.Size = new System.Drawing.Size(679, 119);
            this.nightlyTrendsTable.TabIndex = 4;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.groupBox19);
            this.panel3.Controls.Add(this.groupBox22);
            this.panel3.Controls.Add(this.groupBox18);
            this.panel3.Controls.Add(this.groupBox20);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Margin = new System.Windows.Forms.Padding(0);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(687, 454);
            this.panel3.TabIndex = 0;
            // 
            // groupBox19
            // 
            this.groupBox19.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBox19.Controls.Add(this.label34);
            this.groupBox19.Controls.Add(this.nightlyRoot);
            this.groupBox19.Controls.Add(this.nightlyBrowseBuild);
            this.groupBox19.Location = new System.Drawing.Point(0, 329);
            this.groupBox19.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox19.Name = "groupBox19";
            this.groupBox19.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox19.Size = new System.Drawing.Size(240, 121);
            this.groupBox19.TabIndex = 36;
            this.groupBox19.TabStop = false;
            this.groupBox19.Text = "Nightly directory";
            // 
            // label34
            // 
            this.label34.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label34.Location = new System.Drawing.Point(10, 62);
            this.label34.Name = "label34";
            this.label34.Size = new System.Drawing.Size(223, 21);
            this.label34.TabIndex = 28;
            this.label34.Text = "(Absolute path or path relative to User folder)";
            this.label34.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // nightlyRoot
            // 
            this.nightlyRoot.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.nightlyRoot.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.nightlyRoot.Location = new System.Drawing.Point(10, 24);
            this.nightlyRoot.Name = "nightlyRoot";
            this.nightlyRoot.Size = new System.Drawing.Size(222, 36);
            this.nightlyRoot.TabIndex = 29;
            // 
            // nightlyBrowseBuild
            // 
            this.nightlyBrowseBuild.Location = new System.Drawing.Point(10, 83);
            this.nightlyBrowseBuild.Margin = new System.Windows.Forms.Padding(4);
            this.nightlyBrowseBuild.Name = "nightlyBrowseBuild";
            this.nightlyBrowseBuild.Size = new System.Drawing.Size(89, 28);
            this.nightlyBrowseBuild.TabIndex = 26;
            this.nightlyBrowseBuild.Text = "Change...";
            this.nightlyBrowseBuild.UseVisualStyleBackColor = true;
            this.nightlyBrowseBuild.Click += new System.EventHandler(this.nightlyBrowseBuild_Click);
            // 
            // groupBox22
            // 
            this.groupBox22.Controls.Add(this.nightlyBranch);
            this.groupBox22.Controls.Add(this.nightlyBuildTrunk);
            this.groupBox22.Controls.Add(this.nightlyBranchUrl);
            this.groupBox22.Location = new System.Drawing.Point(0, 235);
            this.groupBox22.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox22.Name = "groupBox22";
            this.groupBox22.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox22.Size = new System.Drawing.Size(240, 86);
            this.groupBox22.TabIndex = 34;
            this.groupBox22.TabStop = false;
            this.groupBox22.Text = "Source";
            // 
            // nightlyBranch
            // 
            this.nightlyBranch.AutoSize = true;
            this.nightlyBranch.Location = new System.Drawing.Point(9, 50);
            this.nightlyBranch.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nightlyBranch.Name = "nightlyBranch";
            this.nightlyBranch.Size = new System.Drawing.Size(59, 17);
            this.nightlyBranch.TabIndex = 4;
            this.nightlyBranch.Text = "Branch";
            this.nightlyBranch.UseVisualStyleBackColor = true;
            // 
            // nightlyBuildTrunk
            // 
            this.nightlyBuildTrunk.AutoSize = true;
            this.nightlyBuildTrunk.Checked = true;
            this.nightlyBuildTrunk.Location = new System.Drawing.Point(9, 23);
            this.nightlyBuildTrunk.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nightlyBuildTrunk.Name = "nightlyBuildTrunk";
            this.nightlyBuildTrunk.Size = new System.Drawing.Size(53, 17);
            this.nightlyBuildTrunk.TabIndex = 3;
            this.nightlyBuildTrunk.TabStop = true;
            this.nightlyBuildTrunk.Text = "Trunk";
            this.nightlyBuildTrunk.UseVisualStyleBackColor = true;
            // 
            // nightlyBranchUrl
            // 
            this.nightlyBranchUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.nightlyBranchUrl.Location = new System.Drawing.Point(74, 49);
            this.nightlyBranchUrl.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nightlyBranchUrl.Name = "nightlyBranchUrl";
            this.nightlyBranchUrl.Size = new System.Drawing.Size(159, 20);
            this.nightlyBranchUrl.TabIndex = 2;
            this.nightlyBranchUrl.Text = "https://github.com/ProteoWizard/pwiz/tree/BRANCHNAME";
            // 
            // groupBox18
            // 
            this.groupBox18.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox18.Controls.Add(this.panel4);
            this.groupBox18.Controls.Add(this.nightlyTestName);
            this.groupBox18.Controls.Add(this.nightlyThumbnail);
            this.groupBox18.Controls.Add(this.nightlyGraphPanel);
            this.groupBox18.Controls.Add(this.nightlyDeleteRun);
            this.groupBox18.Controls.Add(this.nightlyViewLog);
            this.groupBox18.Controls.Add(this.nightlyLabelLeaks);
            this.groupBox18.Controls.Add(this.nightlyLabelFailures);
            this.groupBox18.Controls.Add(this.nightlyLabelTestsRun);
            this.groupBox18.Controls.Add(this.nightlyLabelDuration);
            this.groupBox18.Controls.Add(this.label25);
            this.groupBox18.Controls.Add(this.nightlyLabel3);
            this.groupBox18.Controls.Add(this.nightlyLabel2);
            this.groupBox18.Controls.Add(this.nightlyLabel1);
            this.groupBox18.Controls.Add(this.nightlyRunDate);
            this.groupBox18.Controls.Add(this.label29);
            this.groupBox18.Location = new System.Drawing.Point(248, 0);
            this.groupBox18.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox18.Name = "groupBox18";
            this.groupBox18.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox18.Size = new System.Drawing.Size(439, 450);
            this.groupBox18.TabIndex = 32;
            this.groupBox18.TabStop = false;
            this.groupBox18.Text = "Run results";
            // 
            // panel4
            // 
            this.panel4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.panel4.Controls.Add(this.radioNightlyHandles);
            this.panel4.Controls.Add(this.radioNightlyMemory);
            this.panel4.Location = new System.Drawing.Point(8, 427);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(136, 21);
            this.panel4.TabIndex = 38;
            // 
            // radioNightlyHandles
            // 
            this.radioNightlyHandles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioNightlyHandles.AutoSize = true;
            this.radioNightlyHandles.Location = new System.Drawing.Point(68, 0);
            this.radioNightlyHandles.Name = "radioNightlyHandles";
            this.radioNightlyHandles.Size = new System.Drawing.Size(64, 17);
            this.radioNightlyHandles.TabIndex = 37;
            this.radioNightlyHandles.Text = "Handles";
            this.radioNightlyHandles.UseVisualStyleBackColor = true;
            this.radioNightlyHandles.CheckedChanged += new System.EventHandler(this.radioNightlyHandles_CheckedChanged);
            // 
            // radioNightlyMemory
            // 
            this.radioNightlyMemory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioNightlyMemory.AutoSize = true;
            this.radioNightlyMemory.Checked = true;
            this.radioNightlyMemory.Location = new System.Drawing.Point(0, 0);
            this.radioNightlyMemory.Name = "radioNightlyMemory";
            this.radioNightlyMemory.Size = new System.Drawing.Size(62, 17);
            this.radioNightlyMemory.TabIndex = 36;
            this.radioNightlyMemory.TabStop = true;
            this.radioNightlyMemory.Text = "Memory";
            this.radioNightlyMemory.UseVisualStyleBackColor = true;
            this.radioNightlyMemory.CheckedChanged += new System.EventHandler(this.radioNightlyMemory_CheckedChanged);
            // 
            // nightlyTestName
            // 
            this.nightlyTestName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.nightlyTestName.AutoEllipsis = true;
            this.nightlyTestName.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.nightlyTestName.Location = new System.Drawing.Point(292, 104);
            this.nightlyTestName.Name = "nightlyTestName";
            this.nightlyTestName.Size = new System.Drawing.Size(115, 20);
            this.nightlyTestName.TabIndex = 35;
            this.nightlyTestName.Text = "test name";
            // 
            // nightlyThumbnail
            // 
            this.nightlyThumbnail.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.nightlyThumbnail.Location = new System.Drawing.Point(294, 21);
            this.nightlyThumbnail.Name = "nightlyThumbnail";
            this.nightlyThumbnail.ProcessId = 0;
            this.nightlyThumbnail.Size = new System.Drawing.Size(138, 79);
            this.nightlyThumbnail.TabIndex = 34;
            // 
            // nightlyGraphPanel
            // 
            this.nightlyGraphPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.nightlyGraphPanel.Location = new System.Drawing.Point(11, 126);
            this.nightlyGraphPanel.Name = "nightlyGraphPanel";
            this.nightlyGraphPanel.Size = new System.Drawing.Size(421, 298);
            this.nightlyGraphPanel.TabIndex = 32;
            // 
            // nightlyDeleteRun
            // 
            this.nightlyDeleteRun.Location = new System.Drawing.Point(189, 92);
            this.nightlyDeleteRun.Margin = new System.Windows.Forms.Padding(4);
            this.nightlyDeleteRun.Name = "nightlyDeleteRun";
            this.nightlyDeleteRun.Size = new System.Drawing.Size(88, 26);
            this.nightlyDeleteRun.TabIndex = 31;
            this.nightlyDeleteRun.Text = "Delete run";
            this.nightlyDeleteRun.UseVisualStyleBackColor = true;
            this.nightlyDeleteRun.Click += new System.EventHandler(this.buttonDeleteRun_Click);
            // 
            // nightlyViewLog
            // 
            this.nightlyViewLog.Location = new System.Drawing.Point(189, 58);
            this.nightlyViewLog.Margin = new System.Windows.Forms.Padding(4);
            this.nightlyViewLog.Name = "nightlyViewLog";
            this.nightlyViewLog.Size = new System.Drawing.Size(88, 26);
            this.nightlyViewLog.TabIndex = 30;
            this.nightlyViewLog.Text = "View log";
            this.nightlyViewLog.UseVisualStyleBackColor = true;
            this.nightlyViewLog.Click += new System.EventHandler(this.buttonOpenLog_Click);
            // 
            // nightlyLabelLeaks
            // 
            this.nightlyLabelLeaks.AutoSize = true;
            this.nightlyLabelLeaks.Location = new System.Drawing.Point(87, 103);
            this.nightlyLabelLeaks.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.nightlyLabelLeaks.Name = "nightlyLabelLeaks";
            this.nightlyLabelLeaks.Size = new System.Drawing.Size(13, 13);
            this.nightlyLabelLeaks.TabIndex = 12;
            this.nightlyLabelLeaks.Text = "0";
            // 
            // nightlyLabelFailures
            // 
            this.nightlyLabelFailures.AutoSize = true;
            this.nightlyLabelFailures.Location = new System.Drawing.Point(87, 87);
            this.nightlyLabelFailures.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.nightlyLabelFailures.Name = "nightlyLabelFailures";
            this.nightlyLabelFailures.Size = new System.Drawing.Size(13, 13);
            this.nightlyLabelFailures.TabIndex = 11;
            this.nightlyLabelFailures.Text = "0";
            // 
            // nightlyLabelTestsRun
            // 
            this.nightlyLabelTestsRun.AutoSize = true;
            this.nightlyLabelTestsRun.Location = new System.Drawing.Point(87, 71);
            this.nightlyLabelTestsRun.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.nightlyLabelTestsRun.Name = "nightlyLabelTestsRun";
            this.nightlyLabelTestsRun.Size = new System.Drawing.Size(13, 13);
            this.nightlyLabelTestsRun.TabIndex = 9;
            this.nightlyLabelTestsRun.Text = "0";
            // 
            // nightlyLabelDuration
            // 
            this.nightlyLabelDuration.AutoSize = true;
            this.nightlyLabelDuration.Location = new System.Drawing.Point(87, 54);
            this.nightlyLabelDuration.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.nightlyLabelDuration.Name = "nightlyLabelDuration";
            this.nightlyLabelDuration.Size = new System.Drawing.Size(28, 13);
            this.nightlyLabelDuration.TabIndex = 8;
            this.nightlyLabelDuration.Text = "0:00";
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(19, 103);
            this.label25.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(39, 13);
            this.label25.TabIndex = 6;
            this.label25.Text = "Leaks:";
            // 
            // nightlyLabel3
            // 
            this.nightlyLabel3.AutoSize = true;
            this.nightlyLabel3.Location = new System.Drawing.Point(19, 87);
            this.nightlyLabel3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.nightlyLabel3.Name = "nightlyLabel3";
            this.nightlyLabel3.Size = new System.Drawing.Size(46, 13);
            this.nightlyLabel3.TabIndex = 5;
            this.nightlyLabel3.Text = "Failures:";
            // 
            // nightlyLabel2
            // 
            this.nightlyLabel2.AutoSize = true;
            this.nightlyLabel2.Location = new System.Drawing.Point(19, 71);
            this.nightlyLabel2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.nightlyLabel2.Name = "nightlyLabel2";
            this.nightlyLabel2.Size = new System.Drawing.Size(54, 13);
            this.nightlyLabel2.TabIndex = 3;
            this.nightlyLabel2.Text = "Tests run:";
            // 
            // nightlyLabel1
            // 
            this.nightlyLabel1.AutoSize = true;
            this.nightlyLabel1.Location = new System.Drawing.Point(19, 54);
            this.nightlyLabel1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.nightlyLabel1.Name = "nightlyLabel1";
            this.nightlyLabel1.Size = new System.Drawing.Size(50, 13);
            this.nightlyLabel1.TabIndex = 2;
            this.nightlyLabel1.Text = "Duration:";
            // 
            // nightlyRunDate
            // 
            this.nightlyRunDate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.nightlyRunDate.FormattingEnabled = true;
            this.nightlyRunDate.Location = new System.Drawing.Point(85, 21);
            this.nightlyRunDate.Margin = new System.Windows.Forms.Padding(4);
            this.nightlyRunDate.Name = "nightlyRunDate";
            this.nightlyRunDate.Size = new System.Drawing.Size(192, 21);
            this.nightlyRunDate.TabIndex = 1;
            this.nightlyRunDate.SelectedIndexChanged += new System.EventHandler(this.comboRunDate_SelectedIndexChanged);
            // 
            // label29
            // 
            this.label29.AutoSize = true;
            this.label29.Location = new System.Drawing.Point(8, 26);
            this.label29.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(54, 13);
            this.label29.TabIndex = 0;
            this.label29.Text = "Run date:";
            // 
            // groupBox20
            // 
            this.groupBox20.Controls.Add(this.nightlyRunIndefinitely);
            this.groupBox20.Controls.Add(this.nightlyRandomize);
            this.groupBox20.Controls.Add(this.nightlyRepeat);
            this.groupBox20.Controls.Add(this.label8);
            this.groupBox20.Controls.Add(this.label11);
            this.groupBox20.Controls.Add(this.nightlyRunPerfTests);
            this.groupBox20.Controls.Add(this.buttonNow);
            this.groupBox20.Controls.Add(this.nightlyStartTime);
            this.groupBox20.Controls.Add(this.nightlyBuildType);
            this.groupBox20.Controls.Add(this.label31);
            this.groupBox20.Controls.Add(this.label35);
            this.groupBox20.Controls.Add(this.nightlyDuration);
            this.groupBox20.Controls.Add(this.label30);
            this.groupBox20.Controls.Add(this.label32);
            this.groupBox20.Location = new System.Drawing.Point(0, 0);
            this.groupBox20.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox20.Name = "groupBox20";
            this.groupBox20.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox20.Size = new System.Drawing.Size(240, 227);
            this.groupBox20.TabIndex = 30;
            this.groupBox20.TabStop = false;
            this.groupBox20.Text = "Run";
            // 
            // nightlyRunIndefinitely
            // 
            this.nightlyRunIndefinitely.AutoSize = true;
            this.nightlyRunIndefinitely.Location = new System.Drawing.Point(10, 199);
            this.nightlyRunIndefinitely.Name = "nightlyRunIndefinitely";
            this.nightlyRunIndefinitely.Size = new System.Drawing.Size(98, 17);
            this.nightlyRunIndefinitely.TabIndex = 39;
            this.nightlyRunIndefinitely.Text = "Run indefinitely";
            this.nightlyRunIndefinitely.UseVisualStyleBackColor = true;
            // 
            // nightlyRandomize
            // 
            this.nightlyRandomize.AutoSize = true;
            this.nightlyRandomize.Location = new System.Drawing.Point(10, 176);
            this.nightlyRandomize.Name = "nightlyRandomize";
            this.nightlyRandomize.Size = new System.Drawing.Size(126, 17);
            this.nightlyRandomize.TabIndex = 38;
            this.nightlyRandomize.Text = "Randomize test order";
            this.nightlyRandomize.UseVisualStyleBackColor = true;
            // 
            // nightlyRepeat
            // 
            this.nightlyRepeat.FormattingEnabled = true;
            this.nightlyRepeat.Items.AddRange(new object[] {
            "1",
            "2",
            "5",
            "10",
            "20",
            "50",
            "100"});
            this.nightlyRepeat.Location = new System.Drawing.Point(87, 145);
            this.nightlyRepeat.Name = "nightlyRepeat";
            this.nightlyRepeat.Size = new System.Drawing.Size(52, 21);
            this.nightlyRepeat.TabIndex = 36;
            this.toolTip1.SetToolTip(this.nightlyRepeat, "Stress each test by running it multiple times before proceeding to next test.  Pe" +
        "rf tests only run once.");
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(145, 148);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(77, 13);
            this.label8.TabIndex = 37;
            this.label8.Text = "time(s) in a row";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(7, 148);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(74, 13);
            this.label11.TabIndex = 35;
            this.label11.Text = "Run each test";
            // 
            // nightlyRunPerfTests
            // 
            this.nightlyRunPerfTests.AutoSize = true;
            this.nightlyRunPerfTests.Location = new System.Drawing.Point(9, 95);
            this.nightlyRunPerfTests.Name = "nightlyRunPerfTests";
            this.nightlyRunPerfTests.Size = new System.Drawing.Size(169, 17);
            this.nightlyRunPerfTests.TabIndex = 33;
            this.nightlyRunPerfTests.Text = "Include perf tests in nightly run";
            this.toolTip1.SetToolTip(this.nightlyRunPerfTests, "Perf tests run only once per language, and only in pass 2 (no leak detection or i" +
        "nitial novendor check)");
            this.nightlyRunPerfTests.UseVisualStyleBackColor = true;
            // 
            // buttonNow
            // 
            this.buttonNow.Location = new System.Drawing.Point(173, 17);
            this.buttonNow.Name = "buttonNow";
            this.buttonNow.Size = new System.Drawing.Size(59, 23);
            this.buttonNow.TabIndex = 32;
            this.buttonNow.Text = "now";
            this.buttonNow.UseVisualStyleBackColor = true;
            this.buttonNow.Click += new System.EventHandler(this.buttonNow_Click);
            // 
            // nightlyStartTime
            // 
            this.nightlyStartTime.CustomFormat = "h:mm tt";
            this.nightlyStartTime.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.nightlyStartTime.Location = new System.Drawing.Point(85, 19);
            this.nightlyStartTime.Name = "nightlyStartTime";
            this.nightlyStartTime.ShowUpDown = true;
            this.nightlyStartTime.Size = new System.Drawing.Size(82, 20);
            this.nightlyStartTime.TabIndex = 31;
            this.nightlyStartTime.Value = new System.DateTime(2014, 1, 14, 18, 0, 0, 0);
            // 
            // nightlyBuildType
            // 
            this.nightlyBuildType.Items.Add("32 bit");
            this.nightlyBuildType.Items.Add("64 bit");
            this.nightlyBuildType.Location = new System.Drawing.Point(85, 69);
            this.nightlyBuildType.Name = "nightlyBuildType";
            this.nightlyBuildType.ReadOnly = true;
            this.nightlyBuildType.Size = new System.Drawing.Size(82, 20);
            this.nightlyBuildType.TabIndex = 30;
            this.nightlyBuildType.Text = "32 bit";
            this.nightlyBuildType.Wrap = true;
            // 
            // label31
            // 
            this.label31.AutoSize = true;
            this.label31.Location = new System.Drawing.Point(170, 47);
            this.label31.Name = "label31";
            this.label31.Size = new System.Drawing.Size(33, 13);
            this.label31.TabIndex = 6;
            this.label31.Text = "hours";
            // 
            // label35
            // 
            this.label35.AutoSize = true;
            this.label35.Location = new System.Drawing.Point(6, 71);
            this.label35.Name = "label35";
            this.label35.Size = new System.Drawing.Size(31, 13);
            this.label35.TabIndex = 29;
            this.label35.Text = "Type";
            // 
            // nightlyDuration
            // 
            this.nightlyDuration.Location = new System.Drawing.Point(85, 45);
            this.nightlyDuration.Maximum = new decimal(new int[] {
            168,
            0,
            0,
            0});
            this.nightlyDuration.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nightlyDuration.Name = "nightlyDuration";
            this.nightlyDuration.Size = new System.Drawing.Size(82, 20);
            this.nightlyDuration.TabIndex = 5;
            this.nightlyDuration.Value = new decimal(new int[] {
            12,
            0,
            0,
            0});
            // 
            // label30
            // 
            this.label30.AutoSize = true;
            this.label30.Location = new System.Drawing.Point(7, 47);
            this.label30.Name = "label30";
            this.label30.Size = new System.Drawing.Size(47, 13);
            this.label30.TabIndex = 4;
            this.label30.Text = "Duration";
            // 
            // label32
            // 
            this.label32.AutoSize = true;
            this.label32.Location = new System.Drawing.Point(7, 22);
            this.label32.Name = "label32";
            this.label32.Size = new System.Drawing.Size(51, 13);
            this.label32.TabIndex = 3;
            this.label32.Text = "Start time";
            // 
            // label33
            // 
            this.label33.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label33.BackColor = System.Drawing.Color.Transparent;
            this.label33.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label33.Location = new System.Drawing.Point(7, 4);
            this.label33.Name = "label33";
            this.label33.Size = new System.Drawing.Size(696, 44);
            this.label33.TabIndex = 34;
            this.label33.Text = "Skyline nightly build/test (normally you configure this using the SkylineNightly " +
    "app)";
            this.label33.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // runNightly
            // 
            this.runNightly.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runNightly.Location = new System.Drawing.Point(596, 649);
            this.runNightly.Margin = new System.Windows.Forms.Padding(4);
            this.runNightly.Name = "runNightly";
            this.runNightly.Size = new System.Drawing.Size(100, 28);
            this.runNightly.TabIndex = 33;
            this.runNightly.Text = "Run";
            this.runNightly.UseVisualStyleBackColor = true;
            this.runNightly.Click += new System.EventHandler(this.RunOrStop_Clicked);
            // 
            // tabOutput
            // 
            this.tabOutput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(140)))), ((int)(((byte)(160)))));
            this.tabOutput.Controls.Add(this.buttonSelectFailedOutputTab);
            this.tabOutput.Controls.Add(this.outputJumpTo);
            this.tabOutput.Controls.Add(this.outputSplitContainer);
            this.tabOutput.Controls.Add(this.buttonOpenLog);
            this.tabOutput.Controls.Add(this.comboBoxOutput);
            this.tabOutput.Controls.Add(this.label19);
            this.tabOutput.Controls.Add(this.buttonStop);
            this.tabOutput.Location = new System.Drawing.Point(4, 28);
            this.tabOutput.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabOutput.Name = "tabOutput";
            this.tabOutput.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabOutput.Size = new System.Drawing.Size(709, 689);
            this.tabOutput.TabIndex = 5;
            this.tabOutput.Text = "Output";
            // 
            // buttonSelectFailedOutputTab
            // 
            this.buttonSelectFailedOutputTab.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonSelectFailedOutputTab.Location = new System.Drawing.Point(16, 649);
            this.buttonSelectFailedOutputTab.Margin = new System.Windows.Forms.Padding(0);
            this.buttonSelectFailedOutputTab.Name = "buttonSelectFailedOutputTab";
            this.buttonSelectFailedOutputTab.Size = new System.Drawing.Size(152, 28);
            this.buttonSelectFailedOutputTab.TabIndex = 37;
            this.buttonSelectFailedOutputTab.Text = "Select failed tests";
            this.toolTip1.SetToolTip(this.buttonSelectFailedOutputTab, "Select failed tests and deselect all others");
            this.buttonSelectFailedOutputTab.UseVisualStyleBackColor = true;
            this.buttonSelectFailedOutputTab.Click += new System.EventHandler(this.SelectFailedTests);
            // 
            // outputJumpTo
            // 
            this.outputJumpTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.outputJumpTo.FormattingEnabled = true;
            this.outputJumpTo.Location = new System.Drawing.Point(238, 52);
            this.outputJumpTo.Margin = new System.Windows.Forms.Padding(4);
            this.outputJumpTo.Name = "outputJumpTo";
            this.outputJumpTo.Size = new System.Drawing.Size(254, 21);
            this.outputJumpTo.TabIndex = 36;
            this.outputJumpTo.SelectedIndexChanged += new System.EventHandler(this.outputJumpTo_SelectedIndexChanged);
            this.outputJumpTo.Click += new System.EventHandler(this.outputJumpTo_Click);
            // 
            // outputSplitContainer
            // 
            this.outputSplitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.outputSplitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.outputSplitContainer.Location = new System.Drawing.Point(16, 80);
            this.outputSplitContainer.Margin = new System.Windows.Forms.Padding(0);
            this.outputSplitContainer.Name = "outputSplitContainer";
            this.outputSplitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // outputSplitContainer.Panel1
            // 
            this.outputSplitContainer.Panel1.Controls.Add(this.commandShell);
            // 
            // outputSplitContainer.Panel2
            // 
            this.outputSplitContainer.Panel2.Controls.Add(this.errorConsole);
            this.outputSplitContainer.Size = new System.Drawing.Size(671, 562);
            this.outputSplitContainer.SplitterDistance = 362;
            this.outputSplitContainer.SplitterWidth = 10;
            this.outputSplitContainer.TabIndex = 35;
            // 
            // commandShell
            // 
            this.commandShell.ColorLine = null;
            this.commandShell.DefaultDirectory = null;
            this.commandShell.Dock = System.Windows.Forms.DockStyle.Fill;
            this.commandShell.FilterFunc = null;
            this.commandShell.FinishedOneCommand = null;
            this.commandShell.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.commandShell.IgnorePaint = 0;
            this.commandShell.IsUnattended = false;
            this.commandShell.IsWaiting = false;
            this.commandShell.Location = new System.Drawing.Point(0, 0);
            this.commandShell.LogFile = null;
            this.commandShell.Margin = new System.Windows.Forms.Padding(0, 0, 0, 14);
            this.commandShell.Name = "commandShell";
            this.commandShell.NextCommand = 0;
            this.commandShell.RestartCount = 0;
            this.commandShell.RunStartTime = new System.DateTime(((long)(0)));
            this.commandShell.Size = new System.Drawing.Size(671, 350);
            this.commandShell.StopButton = null;
            this.commandShell.TabIndex = 2;
            this.commandShell.Text = "";
            this.commandShell.VisibleLogFile = null;
            this.commandShell.WordWrap = false;
            this.commandShell.MouseClick += new System.Windows.Forms.MouseEventHandler(this.commandShell_MouseClick);
            // 
            // errorConsole
            // 
            this.errorConsole.DetectUrls = false;
            this.errorConsole.Dock = System.Windows.Forms.DockStyle.Fill;
            this.errorConsole.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.errorConsole.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.errorConsole.Location = new System.Drawing.Point(0, 0);
            this.errorConsole.Margin = new System.Windows.Forms.Padding(0);
            this.errorConsole.Name = "errorConsole";
            this.errorConsole.ReadOnly = true;
            this.errorConsole.Size = new System.Drawing.Size(671, 202);
            this.errorConsole.TabIndex = 3;
            this.errorConsole.Text = "";
            this.errorConsole.SelectionChanged += new System.EventHandler(this.errorConsole_SelectionChanged);
            // 
            // buttonOpenLog
            // 
            this.buttonOpenLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOpenLog.Location = new System.Drawing.Point(596, 50);
            this.buttonOpenLog.Margin = new System.Windows.Forms.Padding(0);
            this.buttonOpenLog.Name = "buttonOpenLog";
            this.buttonOpenLog.Size = new System.Drawing.Size(89, 23);
            this.buttonOpenLog.TabIndex = 33;
            this.buttonOpenLog.Text = "Open log";
            this.buttonOpenLog.UseVisualStyleBackColor = true;
            this.buttonOpenLog.Click += new System.EventHandler(this.buttonOpenOutput_Click);
            // 
            // comboBoxOutput
            // 
            this.comboBoxOutput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxOutput.FormattingEnabled = true;
            this.comboBoxOutput.Location = new System.Drawing.Point(16, 52);
            this.comboBoxOutput.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxOutput.Name = "comboBoxOutput";
            this.comboBoxOutput.Size = new System.Drawing.Size(214, 21);
            this.comboBoxOutput.TabIndex = 32;
            this.comboBoxOutput.SelectedIndexChanged += new System.EventHandler(this.comboBoxOutput_SelectedIndexChanged);
            // 
            // label19
            // 
            this.label19.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label19.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label19.ForeColor = System.Drawing.Color.White;
            this.label19.Location = new System.Drawing.Point(7, 4);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(680, 44);
            this.label19.TabIndex = 31;
            this.label19.Text = "Output console";
            this.label19.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // buttonStop
            // 
            this.buttonStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonStop.Enabled = false;
            this.buttonStop.Location = new System.Drawing.Point(596, 649);
            this.buttonStop.Margin = new System.Windows.Forms.Padding(4);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(100, 28);
            this.buttonStop.TabIndex = 27;
            this.buttonStop.Text = "Stop";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.Stop_Clicked);
            // 
            // tabRunStats
            // 
            this.tabRunStats.BackColor = System.Drawing.Color.BurlyWood;
            this.tabRunStats.Controls.Add(this.labelCompareTo);
            this.tabRunStats.Controls.Add(this.comboBoxRunStatsCompare);
            this.tabRunStats.Controls.Add(this.comboBoxRunStats);
            this.tabRunStats.Controls.Add(this.label1);
            this.tabRunStats.Controls.Add(this.dataGridRunStats);
            this.tabRunStats.Location = new System.Drawing.Point(4, 28);
            this.tabRunStats.Name = "tabRunStats";
            this.tabRunStats.Size = new System.Drawing.Size(709, 689);
            this.tabRunStats.TabIndex = 8;
            this.tabRunStats.Text = "Run Stats";
            // 
            // labelCompareTo
            // 
            this.labelCompareTo.AutoSize = true;
            this.labelCompareTo.Location = new System.Drawing.Point(249, 51);
            this.labelCompareTo.Name = "labelCompareTo";
            this.labelCompareTo.Size = new System.Drawing.Size(60, 13);
            this.labelCompareTo.TabIndex = 35;
            this.labelCompareTo.Text = "compare to";
            // 
            // comboBoxRunStatsCompare
            // 
            this.comboBoxRunStatsCompare.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRunStatsCompare.FormattingEnabled = true;
            this.comboBoxRunStatsCompare.Location = new System.Drawing.Point(326, 48);
            this.comboBoxRunStatsCompare.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxRunStatsCompare.Name = "comboBoxRunStatsCompare";
            this.comboBoxRunStatsCompare.Size = new System.Drawing.Size(214, 21);
            this.comboBoxRunStatsCompare.TabIndex = 34;
            this.comboBoxRunStatsCompare.SelectedIndexChanged += new System.EventHandler(this.comboBoxRunStats_SelectedIndexChanged);
            // 
            // comboBoxRunStats
            // 
            this.comboBoxRunStats.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRunStats.FormattingEnabled = true;
            this.comboBoxRunStats.Location = new System.Drawing.Point(13, 48);
            this.comboBoxRunStats.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxRunStats.Name = "comboBoxRunStats";
            this.comboBoxRunStats.Size = new System.Drawing.Size(214, 21);
            this.comboBoxRunStats.TabIndex = 33;
            this.comboBoxRunStats.SelectedIndexChanged += new System.EventHandler(this.comboBoxRunStats_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.Black;
            this.label1.Location = new System.Drawing.Point(8, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(696, 44);
            this.label1.TabIndex = 32;
            this.label1.Text = "Skyline run stats";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // dataGridRunStats
            // 
            this.dataGridRunStats.AllowUserToAddRows = false;
            this.dataGridRunStats.AllowUserToDeleteRows = false;
            this.dataGridRunStats.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridRunStats.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridRunStats.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridRunStats.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.TestName,
            this.Iterations,
            this.Duration,
            this.AverageDuration,
            this.RelDuration,
            this.DeltaTotalDuration});
            this.dataGridRunStats.Location = new System.Drawing.Point(12, 76);
            this.dataGridRunStats.Name = "dataGridRunStats";
            this.dataGridRunStats.ReadOnly = true;
            this.dataGridRunStats.RowHeadersVisible = false;
            this.dataGridRunStats.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridRunStats.Size = new System.Drawing.Size(685, 595);
            this.dataGridRunStats.TabIndex = 0;
            this.dataGridRunStats.SortCompare += new System.Windows.Forms.DataGridViewSortCompareEventHandler(this.dataGridRunStats_SortCompare);
            // 
            // TestName
            // 
            this.TestName.HeaderText = "Test";
            this.TestName.Name = "TestName";
            this.TestName.ReadOnly = true;
            // 
            // Iterations
            // 
            this.Iterations.HeaderText = "Iterations";
            this.Iterations.Name = "Iterations";
            this.Iterations.ReadOnly = true;
            // 
            // Duration
            // 
            this.Duration.HeaderText = "Total duration";
            this.Duration.Name = "Duration";
            this.Duration.ReadOnly = true;
            // 
            // AverageDuration
            // 
            this.AverageDuration.HeaderText = "Average duration";
            this.AverageDuration.Name = "AverageDuration";
            this.AverageDuration.ReadOnly = true;
            // 
            // RelDuration
            // 
            this.RelDuration.HeaderText = "Relative duration";
            this.RelDuration.Name = "RelDuration";
            this.RelDuration.ReadOnly = true;
            // 
            // DeltaTotalDuration
            // 
            this.DeltaTotalDuration.HeaderText = "Delta total duration";
            this.DeltaTotalDuration.Name = "DeltaTotalDuration";
            this.DeltaTotalDuration.ReadOnly = true;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.findToolStripMenuItem,
            this.selectBuildMenuItem,
            this.optionsToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(8, 2, 0, 2);
            this.menuStrip1.Size = new System.Drawing.Size(709, 24);
            this.menuStrip1.TabIndex = 8;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.exitToolStripMenuItem1,
            this.createInstallerZipFileToolStripMenuItem,
            this.toolStripSeparator1,
            this.exitToolStripMenuItem2});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
            this.openToolStripMenuItem.Text = "Open...";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.open_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
            this.saveToolStripMenuItem.Text = "Save as...";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.save_Click);
            // 
            // exitToolStripMenuItem1
            // 
            this.exitToolStripMenuItem1.Name = "exitToolStripMenuItem1";
            this.exitToolStripMenuItem1.Size = new System.Drawing.Size(185, 6);
            // 
            // createInstallerZipFileToolStripMenuItem
            // 
            this.createInstallerZipFileToolStripMenuItem.Name = "createInstallerZipFileToolStripMenuItem";
            this.createInstallerZipFileToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
            this.createInstallerZipFileToolStripMenuItem.Text = "Save zip file installer...";
            this.createInstallerZipFileToolStripMenuItem.Click += new System.EventHandler(this.SaveZipFileInstaller);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(185, 6);
            // 
            // exitToolStripMenuItem2
            // 
            this.exitToolStripMenuItem2.Name = "exitToolStripMenuItem2";
            this.exitToolStripMenuItem2.Size = new System.Drawing.Size(188, 22);
            this.exitToolStripMenuItem2.Text = "Exit";
            this.exitToolStripMenuItem2.Click += new System.EventHandler(this.exit_Click);
            // 
            // findToolStripMenuItem
            // 
            this.findToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.findTestToolStripMenuItem,
            this.findNextToolStripMenuItem});
            this.findToolStripMenuItem.Name = "findToolStripMenuItem";
            this.findToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.findToolStripMenuItem.Size = new System.Drawing.Size(42, 20);
            this.findToolStripMenuItem.Text = "Find";
            // 
            // findTestToolStripMenuItem
            // 
            this.findTestToolStripMenuItem.Name = "findTestToolStripMenuItem";
            this.findTestToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F)));
            this.findTestToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.findTestToolStripMenuItem.Text = "Find...";
            this.findTestToolStripMenuItem.Click += new System.EventHandler(this.findTestToolStripMenuItem_Click);
            // 
            // findNextToolStripMenuItem
            // 
            this.findNextToolStripMenuItem.Name = "findNextToolStripMenuItem";
            this.findNextToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.findNextToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.findNextToolStripMenuItem.Text = "Find next";
            this.findNextToolStripMenuItem.Click += new System.EventHandler(this.findNextToolStripMenuItem_Click);
            // 
            // selectBuildMenuItem
            // 
            this.selectBuildMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bin32Bit,
            this.bin64Bit,
            this.build32Bit,
            this.build64Bit,
            this.nightly32Bit,
            this.nightly64Bit,
            this.zip32Bit,
            this.zip64Bit});
            this.selectBuildMenuItem.Name = "selectBuildMenuItem";
            this.selectBuildMenuItem.Size = new System.Drawing.Size(80, 20);
            this.selectBuildMenuItem.Text = "Select build";
            this.selectBuildMenuItem.DropDownOpening += new System.EventHandler(this.selectBuildMenuOpening);
            // 
            // bin32Bit
            // 
            this.bin32Bit.CheckOnClick = true;
            this.bin32Bit.Name = "bin32Bit";
            this.bin32Bit.Size = new System.Drawing.Size(153, 22);
            this.bin32Bit.Text = "bin (32 bit)";
            this.bin32Bit.Click += new System.EventHandler(this.selectBuild_Click);
            // 
            // bin64Bit
            // 
            this.bin64Bit.CheckOnClick = true;
            this.bin64Bit.Name = "bin64Bit";
            this.bin64Bit.Size = new System.Drawing.Size(153, 22);
            this.bin64Bit.Text = "bin (64 bit)";
            this.bin64Bit.Click += new System.EventHandler(this.selectBuild_Click);
            // 
            // build32Bit
            // 
            this.build32Bit.CheckOnClick = true;
            this.build32Bit.Name = "build32Bit";
            this.build32Bit.Size = new System.Drawing.Size(153, 22);
            this.build32Bit.Text = "Build (32 bit)";
            this.build32Bit.Click += new System.EventHandler(this.selectBuild_Click);
            // 
            // build64Bit
            // 
            this.build64Bit.CheckOnClick = true;
            this.build64Bit.Name = "build64Bit";
            this.build64Bit.Size = new System.Drawing.Size(153, 22);
            this.build64Bit.Text = "Build (64 bit)";
            this.build64Bit.Click += new System.EventHandler(this.selectBuild_Click);
            // 
            // nightly32Bit
            // 
            this.nightly32Bit.CheckOnClick = true;
            this.nightly32Bit.Name = "nightly32Bit";
            this.nightly32Bit.Size = new System.Drawing.Size(153, 22);
            this.nightly32Bit.Text = "Nightly (32 bit)";
            this.nightly32Bit.Click += new System.EventHandler(this.selectBuild_Click);
            // 
            // nightly64Bit
            // 
            this.nightly64Bit.CheckOnClick = true;
            this.nightly64Bit.Name = "nightly64Bit";
            this.nightly64Bit.Size = new System.Drawing.Size(153, 22);
            this.nightly64Bit.Text = "Nightly (64 bit)";
            this.nightly64Bit.Click += new System.EventHandler(this.selectBuild_Click);
            // 
            // zip32Bit
            // 
            this.zip32Bit.CheckOnClick = true;
            this.zip32Bit.Name = "zip32Bit";
            this.zip32Bit.Size = new System.Drawing.Size(153, 22);
            this.zip32Bit.Text = "zip (32 bit)";
            this.zip32Bit.Click += new System.EventHandler(this.selectBuild_Click);
            // 
            // zip64Bit
            // 
            this.zip64Bit.CheckOnClick = true;
            this.zip64Bit.Name = "zip64Bit";
            this.zip64Bit.Size = new System.Drawing.Size(153, 22);
            this.zip64Bit.Text = "zip (64 bit)";
            this.zip64Bit.Click += new System.EventHandler(this.selectBuild_Click);
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.accessInternet});
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.optionsToolStripMenuItem.Text = "Options";
            // 
            // accessInternet
            // 
            this.accessInternet.CheckOnClick = true;
            this.accessInternet.Name = "accessInternet";
            this.accessInternet.Size = new System.Drawing.Size(154, 22);
            this.accessInternet.Text = "Access internet";
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.documentationToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // documentationToolStripMenuItem
            // 
            this.documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            this.documentationToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.documentationToolStripMenuItem.Text = "Documentation...";
            this.documentationToolStripMenuItem.Click += new System.EventHandler(this.documentationToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.aboutToolStripMenuItem.Text = "About...";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.about_Click);
            // 
            // radioButton3
            // 
            this.radioButton3.AutoSize = true;
            this.radioButton3.Location = new System.Drawing.Point(6, 42);
            this.radioButton3.Name = "radioButton3";
            this.radioButton3.Size = new System.Drawing.Size(125, 17);
            this.radioButton3.TabIndex = 0;
            this.radioButton3.Text = "Pause for screenshot";
            this.radioButton3.UseVisualStyleBackColor = true;
            // 
            // radioButton2
            // 
            this.radioButton2.AutoSize = true;
            this.radioButton2.Checked = true;
            this.radioButton2.Location = new System.Drawing.Point(6, 19);
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new System.Drawing.Size(70, 17);
            this.radioButton2.TabIndex = 1;
            this.radioButton2.TabStop = true;
            this.radioButton2.Text = "Pause for";
            this.radioButton2.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(76, 19);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(32, 20);
            this.textBox1.TabIndex = 4;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(110, 21);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(0, 13);
            this.label4.TabIndex = 5;
            // 
            // radioButton5
            // 
            this.radioButton5.AutoSize = true;
            this.radioButton5.Location = new System.Drawing.Point(6, 65);
            this.radioButton5.Name = "radioButton5";
            this.radioButton5.Size = new System.Drawing.Size(125, 17);
            this.radioButton5.TabIndex = 6;
            this.radioButton5.Text = "Pause for screenshot";
            this.radioButton5.UseVisualStyleBackColor = true;
            // 
            // myTreeView1
            // 
            this.myTreeView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.myTreeView1.CheckBoxes = true;
            this.myTreeView1.LineColor = System.Drawing.Color.Empty;
            this.myTreeView1.Location = new System.Drawing.Point(6, 19);
            this.myTreeView1.Name = "myTreeView1";
            this.myTreeView1.Size = new System.Drawing.Size(309, 350);
            this.myTreeView1.TabIndex = 15;
            // 
            // SkylineTesterWindow
            // 
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(709, 767);
            this.Controls.Add(this.mainPanel);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(700, 700);
            this.Name = "SkylineTesterWindow";
            this.Text = "Skyline Tester";
            this.Load += new System.EventHandler(this.SkylineTesterWindow_Load);
            this.Move += new System.EventHandler(this.SkylineTesterWindow_Move);
            this.Resize += new System.EventHandler(this.SkylineTesterWindow_Resize);
            this.mainPanel.ResumeLayout(false);
            this.mainPanel.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.tabs.ResumeLayout(false);
            this.tabForms.ResumeLayout(false);
            this.tabForms.PerformLayout();
            this.groupBox12.ResumeLayout(false);
            this.groupBox12.PerformLayout();
            this.groupBox13.ResumeLayout(false);
            this.groupBox13.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.formsGrid)).EndInit();
            this.tabTutorials.ResumeLayout(false);
            this.groupBox21.ResumeLayout(false);
            this.groupBox21.PerformLayout();
            this.groupBox14.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pauseTutorialsSeconds)).EndInit();
            this.tabTests.ResumeLayout(false);
            this.groupBox15.ResumeLayout(false);
            this.groupBox15.PerformLayout();
            this.windowsGroup.ResumeLayout(false);
            this.windowsGroup.PerformLayout();
            this.iterationsGroup.ResumeLayout(false);
            this.iterationsGroup.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.runLoopsCount)).EndInit();
            this.testsGroup.ResumeLayout(false);
            this.testsGroup.PerformLayout();
            this.tabBuild.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox10.ResumeLayout(false);
            this.groupBox10.PerformLayout();
            this.groupBox16.ResumeLayout(false);
            this.groupBox16.PerformLayout();
            this.groupBox6.ResumeLayout(false);
            this.groupBox6.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.tabQuality.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.qualityTableLayout.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.groupBox7.ResumeLayout(false);
            this.groupBox11.ResumeLayout(false);
            this.groupBox11.PerformLayout();
            this.groupBox9.ResumeLayout(false);
            this.groupBox9.PerformLayout();
            this.groupBox8.ResumeLayout(false);
            this.groupBox8.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.qualityPassCount)).EndInit();
            this.tabNightly.ResumeLayout(false);
            this.tabNightly.PerformLayout();
            this.nightlyTableLayout.ResumeLayout(false);
            this.groupBox17.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.groupBox19.ResumeLayout(false);
            this.groupBox22.ResumeLayout(false);
            this.groupBox22.PerformLayout();
            this.groupBox18.ResumeLayout(false);
            this.groupBox18.PerformLayout();
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            this.groupBox20.ResumeLayout(false);
            this.groupBox20.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nightlyDuration)).EndInit();
            this.tabOutput.ResumeLayout(false);
            this.outputSplitContainer.Panel1.ResumeLayout(false);
            this.outputSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.outputSplitContainer)).EndInit();
            this.outputSplitContainer.ResumeLayout(false);
            this.tabRunStats.ResumeLayout(false);
            this.tabRunStats.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridRunStats)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private Panel mainPanel;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem saveToolStripMenuItem;
        private ToolStripSeparator exitToolStripMenuItem1;
        private ToolStripMenuItem exitToolStripMenuItem2;
        private Button runTests;
        private TabControl tabs;
        private TabPage tabTests;
        private GroupBox testsGroup;
        private MyTreeView testsTree;
        private RadioButton skipCheckedTests;
        private RadioButton runCheckedTests;
        private TabPage tabForms;
        private GroupBox groupBox1;
        private Button runForms;
        private TabPage tabTutorials;
        private Button runTutorials;
        private CheckBox testsFrench;
        private GroupBox windowsGroup;
        private CheckBox offscreen;
        private GroupBox iterationsGroup;
        private Label label2;
        private RadioButton runLoops;
        private RadioButton runIndefinitely;
        private RadioButton radioButton3;
        private RadioButton radioButton2;
        private TextBox textBox1;
        private Label label4;
        private MyTreeView myTreeView1;
        private RadioButton radioButton5;
        private GroupBox groupBox3;
        private MyTreeView tutorialsTree;
        private GroupBox groupBox4;
        private Label label5;
        private RadioButton pauseTutorialsDelay;
        private RadioButton pauseTutorialsScreenShots;
        private RadioButton tutorialsDemoMode;
        private TabPage tabBuild;
        private GroupBox groupBox6;
        private TextBox branchUrl;
        private GroupBox groupBox5;
        private Button runBuild;
        private RadioButton buildBranch;
        private RadioButton buildTrunk;
        private TabPage tabQuality;
        private Button runQuality;
        private TabPage tabOutput;
        private Button buttonStop;
        private CommandShell commandShell;
        private GroupBox groupBox13;
        private ComboBox formsLanguage;
        private GroupBox groupBox14;
        private ComboBox tutorialsLanguage;
        private GroupBox groupBox15;
        private CheckBox testsJapanese;
        private CheckBox testsChinese;
        private CheckBox testsEnglish;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
        private ToolStripMenuItem createInstallerZipFileToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private Button buttonBrowseBuild;
        private GroupBox groupBox16;
        private CheckBox startSln;
        private RadioButton incrementalBuild;
        private RadioButton updateBuild;
        private RadioButton nukeBuild;
        private CheckBox build64;
        private CheckBox build32;
        private ToolStripStatusLabel statusRunTime;
        private GroupBox groupBox10;
        private TextBox buildRoot;
        private Label labelSpecifyPath;
        private Button buttonDeleteBuild;
        private Label label15;
        private Label label16;
        private Label label14;
        private Label label17;
        private Label label19;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ComboBox comboBoxOutput;
        private Button buttonOpenLog;
        private TableLayoutPanel qualityTableLayout;
        private Panel panel1;
        private GroupBox groupBox11;
        private Panel panelMemoryGraph;
        private Button buttonViewLog;
        private Label labelLeaks;
        private Label labelFailures;
        private Label labelTestsRun;
        private Label labelDuration;
        private Label label12;
        private Label label13;
        private Label label10;
        private Label label9;
        private GroupBox groupBox9;
        private RadioButton qualityAllTests;
        private RadioButton qualityChooseTests;
        private GroupBox groupBox8;
        private CheckBox pass1;
        private CheckBox pass0;
        private Label label7;
        private RadioButton qualityPassDefinite;
        private Label label18;
        private WindowThumbnail qualityThumbnail;
        private Label qualityTestName;
        private TabPage tabNightly;
        private TableLayoutPanel nightlyTableLayout;
        private Panel panel3;
        private CheckBox nightlyRunPerfTests;
        private GroupBox groupBox22;
        private RadioButton nightlyBranch;
        private RadioButton nightlyBuildTrunk;
        private TextBox nightlyBranchUrl;
        private GroupBox groupBox18;
        private Label nightlyTestName;
        private WindowThumbnail nightlyThumbnail;
        private Panel nightlyGraphPanel;
        private Button nightlyDeleteRun;
        private Button nightlyViewLog;
        private Label nightlyLabelLeaks;
        private Label nightlyLabelFailures;
        private Label nightlyLabelTestsRun;
        private Label nightlyLabelDuration;
        private Label label25;
        private Label nightlyLabel3;
        private Label nightlyLabel2;
        private Label nightlyLabel1;
        private ComboBox nightlyRunDate;
        private Label label29;
        private GroupBox groupBox20;
        private Label label31;
        private NumericUpDown nightlyDuration;
        private Label label30;
        private Label label32;
        private Label label33;
        private Button runNightly;
        private DomainUpDown nightlyBuildType;
        private Label label35;
        private ToolStripStatusLabel selectedBuild;
        private ToolStripMenuItem selectBuildMenuItem;
        private ToolStripMenuItem bin32Bit;
        private ToolStripMenuItem bin64Bit;
        private ToolStripMenuItem build32Bit;
        private ToolStripMenuItem build64Bit;
        private ToolStripMenuItem nightly32Bit;
        private ToolStripMenuItem nightly64Bit;
        private ToolStripMenuItem zip32Bit;
        private ToolStripMenuItem zip64Bit;
        private GroupBox groupBox7;
        private RichTextBox errorConsole;
        private SplitContainer outputSplitContainer;
        private ToolStripMenuItem findToolStripMenuItem;
        private ToolStripMenuItem findTestToolStripMenuItem;
        private ToolStripMenuItem findNextToolStripMenuItem;
        private Button buttonDeleteNightlyTask;
        private DateTimePicker nightlyStartTime;
        private NumericUpDown pauseTutorialsSeconds;
        private NumericUpDown runLoopsCount;
        private NumericUpDown qualityPassCount;
        private ToolStripMenuItem documentationToolStripMenuItem;
        private Button buttonNow;
        private RadioButton qualityPassIndefinite;
        private ComboBox outputJumpTo;
        private GroupBox groupBox12;
        private CheckBox showFormNames;
        private GroupBox groupBox21;
        private CheckBox showFormNamesTutorial;
        private CheckBox testsTurkish;
        private SafeDataGridView formsGrid;
        private ToolStrip toolStrip1;
        private ToolStripLabel labelSelectedFormsCount;
        private ToolStripButton clearSeenButton;
        private DataGridViewLinkColumn FormColumn;
        private DataGridViewLinkColumn TestColumn;
        private DataGridViewTextBoxColumn SeenColumn;
        private ToolStripLabel labelFormsSeenPercent;
        private GroupBox groupBox2;
        private CheckBox runBuildVerificationTests;
        private CheckBox showMatchingPagesTutorial;
        private ToolStripMenuItem optionsToolStripMenuItem;
        private ToolStripMenuItem accessInternet;
        private ToolTip toolTip1;
        private CheckBox nightlyExit;
        private GroupBox groupBox19;
        private Label label34;
        private Label nightlyRoot;
        private Button nightlyBrowseBuild;
        private GroupBox groupBox17;
        private TableLayoutPanel nightlyTrendsTable;
        private TabPage tabRunStats;
        private SafeDataGridView dataGridRunStats;
        private Label label1;
        private ComboBox comboBoxRunStats;
        private DataGridViewTextBoxColumn TestName;
        private DataGridViewTextBoxColumn Iterations;
        private DataGridViewTextBoxColumn Duration;
        private DataGridViewTextBoxColumn AverageDuration;
        private DataGridViewTextBoxColumn RelDuration;
        private DataGridViewTextBoxColumn DeltaTotalDuration;
        private Label label6;
        private Label label3;
        private CheckBox randomize;
        private ComboBox repeat;
        private CheckBox nightlyRandomize;
        private ComboBox nightlyRepeat;
        private Label label8;
        private Label label11;
        private CheckBox testsRunSmallMoleculeVersions;
        private CheckBox qualityRunSmallMoleculeVersions;
        private ComboBox comboBoxRunStatsCompare;
        private Label labelCompareTo;
        private Button buttonSelectFailedTestsTab;
        private Button buttonSelectFailedOutputTab;
        private RadioButton radioQualityHandles;
        private RadioButton radioQualityMemory;
        private RadioButton radioNightlyHandles;
        private RadioButton radioNightlyMemory;
        private Panel panel2;
        private Panel panel4;
        private CheckBox nightlyRunIndefinitely;
        private CheckBox recordAuditLogs;
        private RadioButton modeTutorialsCoverShots;
        private TextBox pauseStartingPage;
        private Label labelPauseStartingPage;
        private Button diffButton;
        private ComboBox formsLanguageDiff;
        private Label label20;
        private CheckBox showChangedFiles;
        private ComboBox runMode;
        private Label label21;
        private CheckBox showTutorialsOnly;
    }
}
