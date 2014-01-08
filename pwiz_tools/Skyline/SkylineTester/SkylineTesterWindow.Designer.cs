using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SkylineTesterWindow));
            this.mainPanel = new System.Windows.Forms.Panel();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusRunTime = new System.Windows.Forms.ToolStripStatusLabel();
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabForms = new System.Windows.Forms.TabPage();
            this.label15 = new System.Windows.Forms.Label();
            this.groupBox13 = new System.Windows.Forms.GroupBox();
            this.formsLanguage = new System.Windows.Forms.ComboBox();
            this.regenerateCache = new System.Windows.Forms.CheckBox();
            this.runForms = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.formsTree = new SkylineTester.MyTreeView();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.pauseFormSeconds = new System.Windows.Forms.TextBox();
            this.pauseFormDelay = new System.Windows.Forms.RadioButton();
            this.pauseFormButton = new System.Windows.Forms.RadioButton();
            this.tabTutorials = new System.Windows.Forms.TabPage();
            this.label16 = new System.Windows.Forms.Label();
            this.groupBox14 = new System.Windows.Forms.GroupBox();
            this.tutorialsLanguage = new System.Windows.Forms.ComboBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.tutorialsTree = new SkylineTester.MyTreeView();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.tutorialsDemoMode = new System.Windows.Forms.RadioButton();
            this.label5 = new System.Windows.Forms.Label();
            this.pauseTutorialsSeconds = new System.Windows.Forms.TextBox();
            this.pauseTutorialsDelay = new System.Windows.Forms.RadioButton();
            this.pauseTutorialsScreenShots = new System.Windows.Forms.RadioButton();
            this.runTutorials = new System.Windows.Forms.Button();
            this.tabTests = new System.Windows.Forms.TabPage();
            this.label17 = new System.Windows.Forms.Label();
            this.groupBox15 = new System.Windows.Forms.GroupBox();
            this.testsFrench = new System.Windows.Forms.CheckBox();
            this.testsJapanese = new System.Windows.Forms.CheckBox();
            this.testsChinese = new System.Windows.Forms.CheckBox();
            this.testsEnglish = new System.Windows.Forms.CheckBox();
            this.runTests = new System.Windows.Forms.Button();
            this.pauseGroup = new System.Windows.Forms.GroupBox();
            this.pauseTestsScreenShots = new System.Windows.Forms.CheckBox();
            this.windowsGroup = new System.Windows.Forms.GroupBox();
            this.offscreen = new System.Windows.Forms.CheckBox();
            this.iterationsGroup = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.runLoopsCount = new System.Windows.Forms.TextBox();
            this.runLoops = new System.Windows.Forms.RadioButton();
            this.runIndefinitely = new System.Windows.Forms.RadioButton();
            this.testsGroup = new System.Windows.Forms.GroupBox();
            this.testsTree = new SkylineTester.MyTreeView();
            this.skipCheckedTests = new System.Windows.Forms.RadioButton();
            this.runCheckedTests = new System.Windows.Forms.RadioButton();
            this.button3 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.tabBuild = new System.Windows.Forms.TabPage();
            this.label14 = new System.Windows.Forms.Label();
            this.groupBox10 = new System.Windows.Forms.GroupBox();
            this.labelSpecifyPath = new System.Windows.Forms.Label();
            this.buttonDeleteBuild = new System.Windows.Forms.Button();
            this.buildRoot = new System.Windows.Forms.TextBox();
            this.buttonBrowseBuild = new System.Windows.Forms.Button();
            this.groupBox16 = new System.Windows.Forms.GroupBox();
            this.startSln = new System.Windows.Forms.CheckBox();
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox12 = new System.Windows.Forms.GroupBox();
            this.historyTable = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.groupBox11 = new System.Windows.Forms.GroupBox();
            this.panelMemoryGraph = new System.Windows.Forms.Panel();
            this.buttonDeleteRun = new System.Windows.Forms.Button();
            this.buttonOpenLog = new System.Windows.Forms.Button();
            this.labelLeaks = new System.Windows.Forms.Label();
            this.labelFailures = new System.Windows.Forms.Label();
            this.labelTestsRun = new System.Windows.Forms.Label();
            this.labelDuration = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.comboRunDate = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.groupBox9 = new System.Windows.Forms.GroupBox();
            this.qualityAllTests = new System.Windows.Forms.RadioButton();
            this.qualityChooseTests = new System.Windows.Forms.RadioButton();
            this.groupBox8 = new System.Windows.Forms.GroupBox();
            this.pass1 = new System.Windows.Forms.CheckBox();
            this.pass0 = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.passCount = new System.Windows.Forms.TextBox();
            this.qualityEndTime = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.qualityStartTime = new System.Windows.Forms.TextBox();
            this.qualityRunNow = new System.Windows.Forms.RadioButton();
            this.qualityRunSchedule = new System.Windows.Forms.RadioButton();
            this.groupBox7 = new System.Windows.Forms.GroupBox();
            this.qualityBuildType = new System.Windows.Forms.ComboBox();
            this.label18 = new System.Windows.Forms.Label();
            this.runQuality = new System.Windows.Forms.Button();
            this.tabOutput = new System.Windows.Forms.TabPage();
            this.buttonOpenOutput = new System.Windows.Forms.Button();
            this.comboBoxOutput = new System.Windows.Forms.ComboBox();
            this.label19 = new System.Windows.Forms.Label();
            this.buttonStop = new System.Windows.Forms.Button();
            this.commandShell = new SkylineTester.CommandShell();
            this.tabErrors = new System.Windows.Forms.TabPage();
            this.buttonOpenErrors = new System.Windows.Forms.Button();
            this.comboBoxErrors = new System.Windows.Forms.ComboBox();
            this.label20 = new System.Windows.Forms.Label();
            this.buttonErrorsStop = new System.Windows.Forms.Button();
            this.errorsShell = new SkylineTester.CommandShell();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.createInstallerZipFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.memoryUseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runWithDebugger = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.radioButton3 = new System.Windows.Forms.RadioButton();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.radioButton5 = new System.Windows.Forms.RadioButton();
            this.myTreeView1 = new SkylineTester.MyTreeView();
            this.qualityThumbnail = new SkylineTester.WindowThumbnail();
            this.mainPanel.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.tabs.SuspendLayout();
            this.tabForms.SuspendLayout();
            this.groupBox13.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.tabTutorials.SuspendLayout();
            this.groupBox14.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.tabTests.SuspendLayout();
            this.groupBox15.SuspendLayout();
            this.pauseGroup.SuspendLayout();
            this.windowsGroup.SuspendLayout();
            this.iterationsGroup.SuspendLayout();
            this.testsGroup.SuspendLayout();
            this.tabBuild.SuspendLayout();
            this.groupBox10.SuspendLayout();
            this.groupBox16.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.tabQuality.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBox12.SuspendLayout();
            this.panel1.SuspendLayout();
            this.groupBox11.SuspendLayout();
            this.groupBox9.SuspendLayout();
            this.groupBox8.SuspendLayout();
            this.groupBox7.SuspendLayout();
            this.tabOutput.SuspendLayout();
            this.tabErrors.SuspendLayout();
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
            this.mainPanel.Size = new System.Drawing.Size(744, 697);
            this.mainPanel.TabIndex = 0;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel,
            this.statusRunTime});
            this.statusStrip1.Location = new System.Drawing.Point(0, 675);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 13, 0);
            this.statusStrip1.Size = new System.Drawing.Size(744, 22);
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
            // statusRunTime
            // 
            this.statusRunTime.BackColor = System.Drawing.Color.Transparent;
            this.statusRunTime.ForeColor = System.Drawing.SystemColors.GrayText;
            this.statusRunTime.Margin = new System.Windows.Forms.Padding(0, 3, 6, 2);
            this.statusRunTime.Name = "statusRunTime";
            this.statusRunTime.Size = new System.Drawing.Size(686, 17);
            this.statusRunTime.Spring = true;
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
            this.tabs.Controls.Add(this.tabOutput);
            this.tabs.Controls.Add(this.tabErrors);
            this.tabs.Location = new System.Drawing.Point(-4, 33);
            this.tabs.Margin = new System.Windows.Forms.Padding(4);
            this.tabs.Name = "tabs";
            this.tabs.Padding = new System.Drawing.Point(20, 6);
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(752, 651);
            this.tabs.TabIndex = 4;
            this.tabs.SelectedIndexChanged += new System.EventHandler(this.TabChanged);
            // 
            // tabForms
            // 
            this.tabForms.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(205)))));
            this.tabForms.Controls.Add(this.label15);
            this.tabForms.Controls.Add(this.groupBox13);
            this.tabForms.Controls.Add(this.regenerateCache);
            this.tabForms.Controls.Add(this.runForms);
            this.tabForms.Controls.Add(this.groupBox1);
            this.tabForms.Controls.Add(this.groupBox2);
            this.tabForms.Location = new System.Drawing.Point(4, 28);
            this.tabForms.Margin = new System.Windows.Forms.Padding(4);
            this.tabForms.Name = "tabForms";
            this.tabForms.Padding = new System.Windows.Forms.Padding(4);
            this.tabForms.Size = new System.Drawing.Size(744, 619);
            this.tabForms.TabIndex = 1;
            this.tabForms.Text = "Forms";
            // 
            // label15
            // 
            this.label15.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label15.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label15.Location = new System.Drawing.Point(7, 4);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(730, 44);
            this.label15.TabIndex = 31;
            this.label15.Text = "View Skyline forms";
            this.label15.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox13
            // 
            this.groupBox13.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(205)))));
            this.groupBox13.Controls.Add(this.formsLanguage);
            this.groupBox13.Location = new System.Drawing.Point(11, 148);
            this.groupBox13.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox13.Name = "groupBox13";
            this.groupBox13.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox13.Size = new System.Drawing.Size(220, 69);
            this.groupBox13.TabIndex = 21;
            this.groupBox13.TabStop = false;
            this.groupBox13.Text = "Language";
            // 
            // formsLanguage
            // 
            this.formsLanguage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.formsLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.formsLanguage.FormattingEnabled = true;
            this.formsLanguage.Location = new System.Drawing.Point(9, 25);
            this.formsLanguage.Margin = new System.Windows.Forms.Padding(4);
            this.formsLanguage.Name = "formsLanguage";
            this.formsLanguage.Size = new System.Drawing.Size(203, 21);
            this.formsLanguage.TabIndex = 0;
            // 
            // regenerateCache
            // 
            this.regenerateCache.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.regenerateCache.AutoSize = true;
            this.regenerateCache.Location = new System.Drawing.Point(348, 672);
            this.regenerateCache.Margin = new System.Windows.Forms.Padding(4);
            this.regenerateCache.Name = "regenerateCache";
            this.regenerateCache.Size = new System.Drawing.Size(137, 17);
            this.regenerateCache.TabIndex = 20;
            this.regenerateCache.Text = "Regenerate list of forms";
            this.regenerateCache.UseVisualStyleBackColor = true;
            // 
            // runForms
            // 
            this.runForms.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runForms.Location = new System.Drawing.Point(631, 582);
            this.runForms.Margin = new System.Windows.Forms.Padding(4);
            this.runForms.Name = "runForms";
            this.runForms.Size = new System.Drawing.Size(100, 28);
            this.runForms.TabIndex = 19;
            this.runForms.Text = "Run";
            this.runForms.UseVisualStyleBackColor = true;
            this.runForms.Click += new System.EventHandler(this.Run);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.formsTree);
            this.groupBox1.Location = new System.Drawing.Point(239, 50);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox1.Size = new System.Drawing.Size(492, 524);
            this.groupBox1.TabIndex = 18;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Forms";
            // 
            // formsTree
            // 
            this.formsTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.formsTree.CheckBoxes = true;
            this.formsTree.Location = new System.Drawing.Point(8, 23);
            this.formsTree.Margin = new System.Windows.Forms.Padding(4);
            this.formsTree.Name = "formsTree";
            this.formsTree.Size = new System.Drawing.Size(476, 493);
            this.formsTree.TabIndex = 15;
            this.formsTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.node_AfterCheck);
            this.formsTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FormsTree_AfterSelect);
            // 
            // groupBox2
            // 
            this.groupBox2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(205)))));
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.pauseFormSeconds);
            this.groupBox2.Controls.Add(this.pauseFormDelay);
            this.groupBox2.Controls.Add(this.pauseFormButton);
            this.groupBox2.Location = new System.Drawing.Point(11, 50);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox2.Size = new System.Drawing.Size(220, 90);
            this.groupBox2.TabIndex = 17;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Pause";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(147, 26);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "seconds";
            // 
            // pauseFormSeconds
            // 
            this.pauseFormSeconds.Location = new System.Drawing.Point(101, 23);
            this.pauseFormSeconds.Margin = new System.Windows.Forms.Padding(4);
            this.pauseFormSeconds.Name = "pauseFormSeconds";
            this.pauseFormSeconds.Size = new System.Drawing.Size(41, 20);
            this.pauseFormSeconds.TabIndex = 4;
            this.pauseFormSeconds.Text = "0";
            // 
            // pauseFormDelay
            // 
            this.pauseFormDelay.AutoSize = true;
            this.pauseFormDelay.Checked = true;
            this.pauseFormDelay.Location = new System.Drawing.Point(8, 23);
            this.pauseFormDelay.Margin = new System.Windows.Forms.Padding(4);
            this.pauseFormDelay.Name = "pauseFormDelay";
            this.pauseFormDelay.Size = new System.Drawing.Size(70, 17);
            this.pauseFormDelay.TabIndex = 1;
            this.pauseFormDelay.TabStop = true;
            this.pauseFormDelay.Text = "Pause for";
            this.pauseFormDelay.UseVisualStyleBackColor = true;
            // 
            // pauseFormButton
            // 
            this.pauseFormButton.AutoSize = true;
            this.pauseFormButton.Location = new System.Drawing.Point(8, 52);
            this.pauseFormButton.Margin = new System.Windows.Forms.Padding(4);
            this.pauseFormButton.Name = "pauseFormButton";
            this.pauseFormButton.Size = new System.Drawing.Size(103, 17);
            this.pauseFormButton.TabIndex = 0;
            this.pauseFormButton.Text = "Pause for button";
            this.pauseFormButton.UseVisualStyleBackColor = true;
            // 
            // tabTutorials
            // 
            this.tabTutorials.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(225)))));
            this.tabTutorials.Controls.Add(this.label16);
            this.tabTutorials.Controls.Add(this.groupBox14);
            this.tabTutorials.Controls.Add(this.groupBox3);
            this.tabTutorials.Controls.Add(this.groupBox4);
            this.tabTutorials.Controls.Add(this.runTutorials);
            this.tabTutorials.Location = new System.Drawing.Point(4, 28);
            this.tabTutorials.Margin = new System.Windows.Forms.Padding(4);
            this.tabTutorials.Name = "tabTutorials";
            this.tabTutorials.Padding = new System.Windows.Forms.Padding(4);
            this.tabTutorials.Size = new System.Drawing.Size(744, 619);
            this.tabTutorials.TabIndex = 2;
            this.tabTutorials.Text = "Tutorials";
            // 
            // label16
            // 
            this.label16.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label16.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(225)))));
            this.label16.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label16.Location = new System.Drawing.Point(7, 4);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(730, 44);
            this.label16.TabIndex = 31;
            this.label16.Text = "Run Skyline tutorials";
            this.label16.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox14
            // 
            this.groupBox14.BackColor = System.Drawing.Color.Transparent;
            this.groupBox14.Controls.Add(this.tutorialsLanguage);
            this.groupBox14.Location = new System.Drawing.Point(11, 174);
            this.groupBox14.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox14.Name = "groupBox14";
            this.groupBox14.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox14.Size = new System.Drawing.Size(220, 69);
            this.groupBox14.TabIndex = 25;
            this.groupBox14.TabStop = false;
            this.groupBox14.Text = "Language";
            // 
            // tutorialsLanguage
            // 
            this.tutorialsLanguage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tutorialsLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.tutorialsLanguage.FormattingEnabled = true;
            this.tutorialsLanguage.Location = new System.Drawing.Point(9, 25);
            this.tutorialsLanguage.Margin = new System.Windows.Forms.Padding(4);
            this.tutorialsLanguage.Name = "tutorialsLanguage";
            this.tutorialsLanguage.Size = new System.Drawing.Size(203, 21);
            this.tutorialsLanguage.TabIndex = 0;
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.Controls.Add(this.tutorialsTree);
            this.groupBox3.Location = new System.Drawing.Point(239, 50);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox3.Size = new System.Drawing.Size(492, 524);
            this.groupBox3.TabIndex = 24;
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
            this.tutorialsTree.Size = new System.Drawing.Size(474, 492);
            this.tutorialsTree.TabIndex = 15;
            this.tutorialsTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.node_AfterCheck);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.tutorialsDemoMode);
            this.groupBox4.Controls.Add(this.label5);
            this.groupBox4.Controls.Add(this.pauseTutorialsSeconds);
            this.groupBox4.Controls.Add(this.pauseTutorialsDelay);
            this.groupBox4.Controls.Add(this.pauseTutorialsScreenShots);
            this.groupBox4.Location = new System.Drawing.Point(11, 50);
            this.groupBox4.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox4.Size = new System.Drawing.Size(220, 116);
            this.groupBox4.TabIndex = 23;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Pause";
            // 
            // tutorialsDemoMode
            // 
            this.tutorialsDemoMode.AutoSize = true;
            this.tutorialsDemoMode.Location = new System.Drawing.Point(8, 80);
            this.tutorialsDemoMode.Margin = new System.Windows.Forms.Padding(4);
            this.tutorialsDemoMode.Name = "tutorialsDemoMode";
            this.tutorialsDemoMode.Size = new System.Drawing.Size(82, 17);
            this.tutorialsDemoMode.TabIndex = 6;
            this.tutorialsDemoMode.Text = "Demo mode";
            this.tutorialsDemoMode.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(147, 26);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(47, 13);
            this.label5.TabIndex = 5;
            this.label5.Text = "seconds";
            // 
            // pauseTutorialsSeconds
            // 
            this.pauseTutorialsSeconds.Location = new System.Drawing.Point(101, 23);
            this.pauseTutorialsSeconds.Margin = new System.Windows.Forms.Padding(4);
            this.pauseTutorialsSeconds.Name = "pauseTutorialsSeconds";
            this.pauseTutorialsSeconds.Size = new System.Drawing.Size(41, 20);
            this.pauseTutorialsSeconds.TabIndex = 4;
            this.pauseTutorialsSeconds.Text = "0";
            // 
            // pauseTutorialsDelay
            // 
            this.pauseTutorialsDelay.AutoSize = true;
            this.pauseTutorialsDelay.Checked = true;
            this.pauseTutorialsDelay.Location = new System.Drawing.Point(8, 23);
            this.pauseTutorialsDelay.Margin = new System.Windows.Forms.Padding(4);
            this.pauseTutorialsDelay.Name = "pauseTutorialsDelay";
            this.pauseTutorialsDelay.Size = new System.Drawing.Size(70, 17);
            this.pauseTutorialsDelay.TabIndex = 1;
            this.pauseTutorialsDelay.TabStop = true;
            this.pauseTutorialsDelay.Text = "Pause for";
            this.pauseTutorialsDelay.UseVisualStyleBackColor = true;
            // 
            // pauseTutorialsScreenShots
            // 
            this.pauseTutorialsScreenShots.AutoSize = true;
            this.pauseTutorialsScreenShots.Location = new System.Drawing.Point(8, 52);
            this.pauseTutorialsScreenShots.Margin = new System.Windows.Forms.Padding(4);
            this.pauseTutorialsScreenShots.Name = "pauseTutorialsScreenShots";
            this.pauseTutorialsScreenShots.Size = new System.Drawing.Size(133, 17);
            this.pauseTutorialsScreenShots.TabIndex = 0;
            this.pauseTutorialsScreenShots.Text = "Pause for screen shots";
            this.pauseTutorialsScreenShots.UseVisualStyleBackColor = true;
            // 
            // runTutorials
            // 
            this.runTutorials.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runTutorials.Location = new System.Drawing.Point(631, 582);
            this.runTutorials.Margin = new System.Windows.Forms.Padding(4);
            this.runTutorials.Name = "runTutorials";
            this.runTutorials.Size = new System.Drawing.Size(100, 28);
            this.runTutorials.TabIndex = 22;
            this.runTutorials.Text = "Run";
            this.runTutorials.UseVisualStyleBackColor = true;
            this.runTutorials.Click += new System.EventHandler(this.Run);
            // 
            // tabTests
            // 
            this.tabTests.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(220)))), ((int)(((byte)(220)))));
            this.tabTests.Controls.Add(this.label17);
            this.tabTests.Controls.Add(this.groupBox15);
            this.tabTests.Controls.Add(this.runTests);
            this.tabTests.Controls.Add(this.pauseGroup);
            this.tabTests.Controls.Add(this.windowsGroup);
            this.tabTests.Controls.Add(this.iterationsGroup);
            this.tabTests.Controls.Add(this.testsGroup);
            this.tabTests.Location = new System.Drawing.Point(4, 28);
            this.tabTests.Margin = new System.Windows.Forms.Padding(4);
            this.tabTests.Name = "tabTests";
            this.tabTests.Padding = new System.Windows.Forms.Padding(4);
            this.tabTests.Size = new System.Drawing.Size(744, 619);
            this.tabTests.TabIndex = 0;
            this.tabTests.Text = "Tests";
            // 
            // label17
            // 
            this.label17.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label17.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label17.Location = new System.Drawing.Point(7, 4);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(730, 44);
            this.label17.TabIndex = 31;
            this.label17.Text = "Run Skyline tests";
            this.label17.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox15
            // 
            this.groupBox15.BackColor = System.Drawing.Color.Transparent;
            this.groupBox15.Controls.Add(this.testsFrench);
            this.groupBox15.Controls.Add(this.testsJapanese);
            this.groupBox15.Controls.Add(this.testsChinese);
            this.groupBox15.Controls.Add(this.testsEnglish);
            this.groupBox15.Location = new System.Drawing.Point(11, 277);
            this.groupBox15.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox15.Name = "groupBox15";
            this.groupBox15.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox15.Size = new System.Drawing.Size(220, 146);
            this.groupBox15.TabIndex = 26;
            this.groupBox15.TabStop = false;
            this.groupBox15.Text = "Language";
            // 
            // testsFrench
            // 
            this.testsFrench.AutoSize = true;
            this.testsFrench.Location = new System.Drawing.Point(8, 81);
            this.testsFrench.Margin = new System.Windows.Forms.Padding(4);
            this.testsFrench.Name = "testsFrench";
            this.testsFrench.Size = new System.Drawing.Size(59, 17);
            this.testsFrench.TabIndex = 1;
            this.testsFrench.Text = "French";
            this.testsFrench.UseVisualStyleBackColor = true;
            // 
            // testsJapanese
            // 
            this.testsJapanese.AutoSize = true;
            this.testsJapanese.Location = new System.Drawing.Point(8, 110);
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
            this.testsChinese.Location = new System.Drawing.Point(9, 52);
            this.testsChinese.Margin = new System.Windows.Forms.Padding(4);
            this.testsChinese.Name = "testsChinese";
            this.testsChinese.Size = new System.Drawing.Size(64, 17);
            this.testsChinese.TabIndex = 2;
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
            this.testsEnglish.TabIndex = 1;
            this.testsEnglish.Text = "English";
            this.testsEnglish.UseVisualStyleBackColor = true;
            // 
            // runTests
            // 
            this.runTests.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runTests.Location = new System.Drawing.Point(631, 582);
            this.runTests.Margin = new System.Windows.Forms.Padding(4);
            this.runTests.Name = "runTests";
            this.runTests.Size = new System.Drawing.Size(100, 28);
            this.runTests.TabIndex = 14;
            this.runTests.Text = "Run";
            this.runTests.UseVisualStyleBackColor = true;
            this.runTests.Click += new System.EventHandler(this.Run);
            // 
            // pauseGroup
            // 
            this.pauseGroup.Controls.Add(this.pauseTestsScreenShots);
            this.pauseGroup.Location = new System.Drawing.Point(11, 214);
            this.pauseGroup.Margin = new System.Windows.Forms.Padding(4);
            this.pauseGroup.Name = "pauseGroup";
            this.pauseGroup.Padding = new System.Windows.Forms.Padding(4);
            this.pauseGroup.Size = new System.Drawing.Size(220, 55);
            this.pauseGroup.TabIndex = 20;
            this.pauseGroup.TabStop = false;
            this.pauseGroup.Text = "Pause";
            // 
            // pauseTestsScreenShots
            // 
            this.pauseTestsScreenShots.AutoSize = true;
            this.pauseTestsScreenShots.Location = new System.Drawing.Point(8, 23);
            this.pauseTestsScreenShots.Margin = new System.Windows.Forms.Padding(4);
            this.pauseTestsScreenShots.Name = "pauseTestsScreenShots";
            this.pauseTestsScreenShots.Size = new System.Drawing.Size(201, 17);
            this.pauseTestsScreenShots.TabIndex = 2;
            this.pauseTestsScreenShots.Text = "Pause for screen shots (tutorials only)";
            this.pauseTestsScreenShots.UseVisualStyleBackColor = true;
            this.pauseTestsScreenShots.CheckedChanged += new System.EventHandler(this.pauseTestsForScreenShots_CheckedChanged);
            // 
            // windowsGroup
            // 
            this.windowsGroup.Controls.Add(this.offscreen);
            this.windowsGroup.Location = new System.Drawing.Point(11, 148);
            this.windowsGroup.Margin = new System.Windows.Forms.Padding(4);
            this.windowsGroup.Name = "windowsGroup";
            this.windowsGroup.Padding = new System.Windows.Forms.Padding(4);
            this.windowsGroup.Size = new System.Drawing.Size(220, 58);
            this.windowsGroup.TabIndex = 18;
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
            this.offscreen.TabIndex = 1;
            this.offscreen.Text = "Off screen";
            this.offscreen.UseVisualStyleBackColor = true;
            this.offscreen.CheckedChanged += new System.EventHandler(this.offscreen_CheckedChanged);
            // 
            // iterationsGroup
            // 
            this.iterationsGroup.Controls.Add(this.label2);
            this.iterationsGroup.Controls.Add(this.runLoopsCount);
            this.iterationsGroup.Controls.Add(this.runLoops);
            this.iterationsGroup.Controls.Add(this.runIndefinitely);
            this.iterationsGroup.Location = new System.Drawing.Point(11, 50);
            this.iterationsGroup.Margin = new System.Windows.Forms.Padding(4);
            this.iterationsGroup.Name = "iterationsGroup";
            this.iterationsGroup.Padding = new System.Windows.Forms.Padding(4);
            this.iterationsGroup.Size = new System.Drawing.Size(220, 90);
            this.iterationsGroup.TabIndex = 17;
            this.iterationsGroup.TabStop = false;
            this.iterationsGroup.Text = "Run options";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(111, 26);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(40, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "passes";
            // 
            // runLoopsCount
            // 
            this.runLoopsCount.Location = new System.Drawing.Point(65, 22);
            this.runLoopsCount.Margin = new System.Windows.Forms.Padding(4);
            this.runLoopsCount.Name = "runLoopsCount";
            this.runLoopsCount.Size = new System.Drawing.Size(41, 20);
            this.runLoopsCount.TabIndex = 2;
            this.runLoopsCount.Text = "1";
            // 
            // runLoops
            // 
            this.runLoops.AutoSize = true;
            this.runLoops.Checked = true;
            this.runLoops.Location = new System.Drawing.Point(8, 23);
            this.runLoops.Margin = new System.Windows.Forms.Padding(4);
            this.runLoops.Name = "runLoops";
            this.runLoops.Size = new System.Drawing.Size(45, 17);
            this.runLoops.TabIndex = 1;
            this.runLoops.TabStop = true;
            this.runLoops.Text = "Run";
            this.runLoops.UseVisualStyleBackColor = true;
            // 
            // runIndefinitely
            // 
            this.runIndefinitely.AutoSize = true;
            this.runIndefinitely.Location = new System.Drawing.Point(8, 54);
            this.runIndefinitely.Margin = new System.Windows.Forms.Padding(4);
            this.runIndefinitely.Name = "runIndefinitely";
            this.runIndefinitely.Size = new System.Drawing.Size(97, 17);
            this.runIndefinitely.TabIndex = 0;
            this.runIndefinitely.Text = "Run indefinitely";
            this.runIndefinitely.UseVisualStyleBackColor = true;
            // 
            // testsGroup
            // 
            this.testsGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.testsGroup.Controls.Add(this.testsTree);
            this.testsGroup.Controls.Add(this.skipCheckedTests);
            this.testsGroup.Controls.Add(this.runCheckedTests);
            this.testsGroup.Controls.Add(this.button3);
            this.testsGroup.Controls.Add(this.button2);
            this.testsGroup.Location = new System.Drawing.Point(239, 50);
            this.testsGroup.Margin = new System.Windows.Forms.Padding(4);
            this.testsGroup.Name = "testsGroup";
            this.testsGroup.Padding = new System.Windows.Forms.Padding(4);
            this.testsGroup.Size = new System.Drawing.Size(492, 524);
            this.testsGroup.TabIndex = 16;
            this.testsGroup.TabStop = false;
            this.testsGroup.Text = "Tests";
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
            this.testsTree.Size = new System.Drawing.Size(474, 394);
            this.testsTree.TabIndex = 15;
            this.testsTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.node_AfterCheck);
            // 
            // skipCheckedTests
            // 
            this.skipCheckedTests.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.skipCheckedTests.AutoSize = true;
            this.skipCheckedTests.Location = new System.Drawing.Point(8, 493);
            this.skipCheckedTests.Margin = new System.Windows.Forms.Padding(4);
            this.skipCheckedTests.Name = "skipCheckedTests";
            this.skipCheckedTests.Size = new System.Drawing.Size(116, 17);
            this.skipCheckedTests.TabIndex = 14;
            this.skipCheckedTests.Text = "Skip checked tests";
            this.skipCheckedTests.UseVisualStyleBackColor = true;
            // 
            // runCheckedTests
            // 
            this.runCheckedTests.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.runCheckedTests.AutoSize = true;
            this.runCheckedTests.Checked = true;
            this.runCheckedTests.Location = new System.Drawing.Point(8, 467);
            this.runCheckedTests.Margin = new System.Windows.Forms.Padding(4);
            this.runCheckedTests.Name = "runCheckedTests";
            this.runCheckedTests.Size = new System.Drawing.Size(115, 17);
            this.runCheckedTests.TabIndex = 13;
            this.runCheckedTests.TabStop = true;
            this.runCheckedTests.Text = "Run checked tests";
            this.runCheckedTests.UseVisualStyleBackColor = true;
            // 
            // button3
            // 
            this.button3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button3.Location = new System.Drawing.Point(116, 425);
            this.button3.Margin = new System.Windows.Forms.Padding(4);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(100, 28);
            this.button3.TabIndex = 12;
            this.button3.Text = "Uncheck all";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.uncheckAll_Click);
            // 
            // button2
            // 
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button2.Location = new System.Drawing.Point(8, 425);
            this.button2.Margin = new System.Windows.Forms.Padding(4);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(100, 28);
            this.button2.TabIndex = 11;
            this.button2.Text = "Check all";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.checkAll_Click);
            // 
            // tabBuild
            // 
            this.tabBuild.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(225)))), ((int)(((byte)(200)))));
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
            this.tabBuild.Size = new System.Drawing.Size(744, 619);
            this.tabBuild.TabIndex = 3;
            this.tabBuild.Text = "Build";
            // 
            // label14
            // 
            this.label14.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label14.Location = new System.Drawing.Point(7, 4);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(731, 44);
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
            this.groupBox10.Size = new System.Drawing.Size(720, 93);
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
            this.labelSpecifyPath.Size = new System.Drawing.Size(594, 28);
            this.labelSpecifyPath.TabIndex = 28;
            this.labelSpecifyPath.Text = "(Specify absolute path or path relative to Skyline folder)";
            this.labelSpecifyPath.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // buttonDeleteBuild
            // 
            this.buttonDeleteBuild.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDeleteBuild.Enabled = false;
            this.buttonDeleteBuild.Location = new System.Drawing.Point(610, 54);
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
            this.buildRoot.Size = new System.Drawing.Size(594, 20);
            this.buildRoot.TabIndex = 3;
            this.buildRoot.Text = "Build";
            // 
            // buttonBrowseBuild
            // 
            this.buttonBrowseBuild.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonBrowseBuild.Location = new System.Drawing.Point(610, 18);
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
            this.groupBox16.Controls.Add(this.startSln);
            this.groupBox16.Controls.Add(this.incrementalBuild);
            this.groupBox16.Controls.Add(this.updateBuild);
            this.groupBox16.Controls.Add(this.nukeBuild);
            this.groupBox16.Location = new System.Drawing.Point(11, 327);
            this.groupBox16.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox16.Name = "groupBox16";
            this.groupBox16.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox16.Size = new System.Drawing.Size(720, 149);
            this.groupBox16.TabIndex = 28;
            this.groupBox16.TabStop = false;
            this.groupBox16.Text = "Build type";
            // 
            // startSln
            // 
            this.startSln.AutoSize = true;
            this.startSln.Location = new System.Drawing.Point(7, 122);
            this.startSln.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.startSln.Name = "startSln";
            this.startSln.Size = new System.Drawing.Size(213, 17);
            this.startSln.TabIndex = 27;
            this.startSln.Text = "Open Skyline in Visual Studio after build";
            this.startSln.UseVisualStyleBackColor = true;
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
            this.runBuild.Location = new System.Drawing.Point(631, 582);
            this.runBuild.Margin = new System.Windows.Forms.Padding(4);
            this.runBuild.Name = "runBuild";
            this.runBuild.Size = new System.Drawing.Size(100, 28);
            this.runBuild.TabIndex = 22;
            this.runBuild.Text = "Run";
            this.runBuild.UseVisualStyleBackColor = true;
            this.runBuild.Click += new System.EventHandler(this.Run);
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
            this.groupBox6.Size = new System.Drawing.Size(720, 86);
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
            this.branchUrl.Size = new System.Drawing.Size(630, 20);
            this.branchUrl.TabIndex = 2;
            this.branchUrl.Text = "https://svn.code.sf.net/p/proteowizard/code/branches/work/BRANCHNAME";
            // 
            // groupBox5
            // 
            this.groupBox5.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox5.Controls.Add(this.build64);
            this.groupBox5.Controls.Add(this.build32);
            this.groupBox5.Location = new System.Drawing.Point(11, 245);
            this.groupBox5.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox5.Size = new System.Drawing.Size(720, 74);
            this.groupBox5.TabIndex = 20;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Architecture";
            // 
            // build64
            // 
            this.build64.AutoSize = true;
            this.build64.Checked = true;
            this.build64.CheckState = System.Windows.Forms.CheckState.Checked;
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
            this.tabQuality.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(228)))), ((int)(((byte)(222)))), ((int)(((byte)(190)))));
            this.tabQuality.Controls.Add(this.tableLayoutPanel1);
            this.tabQuality.Controls.Add(this.label18);
            this.tabQuality.Controls.Add(this.runQuality);
            this.tabQuality.Location = new System.Drawing.Point(4, 28);
            this.tabQuality.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabQuality.Name = "tabQuality";
            this.tabQuality.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabQuality.Size = new System.Drawing.Size(744, 619);
            this.tabQuality.TabIndex = 4;
            this.tabQuality.Text = "Quality";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.groupBox12, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.panel1, 0, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(9, 51);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 70F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(722, 527);
            this.tableLayoutPanel1.TabIndex = 32;
            // 
            // groupBox12
            // 
            this.groupBox12.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox12.Controls.Add(this.historyTable);
            this.groupBox12.Location = new System.Drawing.Point(0, 368);
            this.groupBox12.Margin = new System.Windows.Forms.Padding(0);
            this.groupBox12.Name = "groupBox12";
            this.groupBox12.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox12.Size = new System.Drawing.Size(722, 159);
            this.groupBox12.TabIndex = 30;
            this.groupBox12.TabStop = false;
            this.groupBox12.Text = "Trends";
            // 
            // historyTable
            // 
            this.historyTable.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.historyTable.ColumnCount = 4;
            this.historyTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.historyTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.historyTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.historyTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.historyTable.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.FixedSize;
            this.historyTable.Location = new System.Drawing.Point(4, 17);
            this.historyTable.Margin = new System.Windows.Forms.Padding(0);
            this.historyTable.Name = "historyTable";
            this.historyTable.RowCount = 1;
            this.historyTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.historyTable.Size = new System.Drawing.Size(714, 138);
            this.historyTable.TabIndex = 4;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.groupBox11);
            this.panel1.Controls.Add(this.groupBox9);
            this.panel1.Controls.Add(this.groupBox8);
            this.panel1.Controls.Add(this.groupBox7);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(722, 368);
            this.panel1.TabIndex = 0;
            // 
            // groupBox11
            // 
            this.groupBox11.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox11.Controls.Add(this.qualityThumbnail);
            this.groupBox11.Controls.Add(this.panelMemoryGraph);
            this.groupBox11.Controls.Add(this.buttonDeleteRun);
            this.groupBox11.Controls.Add(this.buttonOpenLog);
            this.groupBox11.Controls.Add(this.labelLeaks);
            this.groupBox11.Controls.Add(this.labelFailures);
            this.groupBox11.Controls.Add(this.labelTestsRun);
            this.groupBox11.Controls.Add(this.labelDuration);
            this.groupBox11.Controls.Add(this.label12);
            this.groupBox11.Controls.Add(this.label13);
            this.groupBox11.Controls.Add(this.label10);
            this.groupBox11.Controls.Add(this.label9);
            this.groupBox11.Controls.Add(this.comboRunDate);
            this.groupBox11.Controls.Add(this.label8);
            this.groupBox11.Location = new System.Drawing.Point(192, 0);
            this.groupBox11.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox11.Name = "groupBox11";
            this.groupBox11.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox11.Size = new System.Drawing.Size(530, 364);
            this.groupBox11.TabIndex = 32;
            this.groupBox11.TabStop = false;
            this.groupBox11.Text = "Run results";
            // 
            // panelMemoryGraph
            // 
            this.panelMemoryGraph.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelMemoryGraph.Location = new System.Drawing.Point(11, 108);
            this.panelMemoryGraph.Name = "panelMemoryGraph";
            this.panelMemoryGraph.Size = new System.Drawing.Size(512, 249);
            this.panelMemoryGraph.TabIndex = 32;
            // 
            // buttonDeleteRun
            // 
            this.buttonDeleteRun.Location = new System.Drawing.Point(260, 76);
            this.buttonDeleteRun.Margin = new System.Windows.Forms.Padding(4);
            this.buttonDeleteRun.Name = "buttonDeleteRun";
            this.buttonDeleteRun.Size = new System.Drawing.Size(96, 24);
            this.buttonDeleteRun.TabIndex = 31;
            this.buttonDeleteRun.Text = "Delete run";
            this.buttonDeleteRun.UseVisualStyleBackColor = true;
            this.buttonDeleteRun.Click += new System.EventHandler(this.buttonDeleteRun_Click);
            // 
            // buttonOpenLog
            // 
            this.buttonOpenLog.Location = new System.Drawing.Point(260, 48);
            this.buttonOpenLog.Margin = new System.Windows.Forms.Padding(4);
            this.buttonOpenLog.Name = "buttonOpenLog";
            this.buttonOpenLog.Size = new System.Drawing.Size(96, 24);
            this.buttonOpenLog.TabIndex = 30;
            this.buttonOpenLog.Text = "Open log";
            this.buttonOpenLog.UseVisualStyleBackColor = true;
            this.buttonOpenLog.Click += new System.EventHandler(this.buttonOpenLog_Click);
            // 
            // labelLeaks
            // 
            this.labelLeaks.AutoSize = true;
            this.labelLeaks.Location = new System.Drawing.Point(213, 82);
            this.labelLeaks.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelLeaks.Name = "labelLeaks";
            this.labelLeaks.Size = new System.Drawing.Size(13, 13);
            this.labelLeaks.TabIndex = 12;
            this.labelLeaks.Text = "0";
            // 
            // labelFailures
            // 
            this.labelFailures.AutoSize = true;
            this.labelFailures.Location = new System.Drawing.Point(213, 54);
            this.labelFailures.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelFailures.Name = "labelFailures";
            this.labelFailures.Size = new System.Drawing.Size(13, 13);
            this.labelFailures.TabIndex = 11;
            this.labelFailures.Text = "0";
            // 
            // labelTestsRun
            // 
            this.labelTestsRun.AutoSize = true;
            this.labelTestsRun.Location = new System.Drawing.Point(87, 82);
            this.labelTestsRun.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelTestsRun.Name = "labelTestsRun";
            this.labelTestsRun.Size = new System.Drawing.Size(13, 13);
            this.labelTestsRun.TabIndex = 9;
            this.labelTestsRun.Text = "0";
            // 
            // labelDuration
            // 
            this.labelDuration.AutoSize = true;
            this.labelDuration.Location = new System.Drawing.Point(87, 54);
            this.labelDuration.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDuration.Name = "labelDuration";
            this.labelDuration.Size = new System.Drawing.Size(28, 13);
            this.labelDuration.TabIndex = 8;
            this.labelDuration.Text = "0:00";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(155, 82);
            this.label12.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(39, 13);
            this.label12.TabIndex = 6;
            this.label12.Text = "Leaks:";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(155, 54);
            this.label13.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(46, 13);
            this.label13.TabIndex = 5;
            this.label13.Text = "Failures:";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(19, 82);
            this.label10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(54, 13);
            this.label10.TabIndex = 3;
            this.label10.Text = "Tests run:";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(19, 54);
            this.label9.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(50, 13);
            this.label9.TabIndex = 2;
            this.label9.Text = "Duration:";
            // 
            // comboRunDate
            // 
            this.comboRunDate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRunDate.FormattingEnabled = true;
            this.comboRunDate.Location = new System.Drawing.Point(85, 21);
            this.comboRunDate.Margin = new System.Windows.Forms.Padding(4);
            this.comboRunDate.Name = "comboRunDate";
            this.comboRunDate.Size = new System.Drawing.Size(271, 21);
            this.comboRunDate.TabIndex = 1;
            this.comboRunDate.SelectedIndexChanged += new System.EventHandler(this.comboRunDate_SelectedIndexChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(8, 26);
            this.label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(54, 13);
            this.label8.TabIndex = 0;
            this.label8.Text = "Run date:";
            // 
            // groupBox9
            // 
            this.groupBox9.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBox9.Controls.Add(this.qualityAllTests);
            this.groupBox9.Controls.Add(this.qualityChooseTests);
            this.groupBox9.Location = new System.Drawing.Point(1, 233);
            this.groupBox9.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox9.Name = "groupBox9";
            this.groupBox9.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox9.Size = new System.Drawing.Size(184, 131);
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
            this.groupBox8.Controls.Add(this.pass1);
            this.groupBox8.Controls.Add(this.pass0);
            this.groupBox8.Controls.Add(this.label7);
            this.groupBox8.Controls.Add(this.passCount);
            this.groupBox8.Controls.Add(this.qualityEndTime);
            this.groupBox8.Controls.Add(this.label6);
            this.groupBox8.Controls.Add(this.label1);
            this.groupBox8.Controls.Add(this.qualityStartTime);
            this.groupBox8.Controls.Add(this.qualityRunNow);
            this.groupBox8.Controls.Add(this.qualityRunSchedule);
            this.groupBox8.Location = new System.Drawing.Point(1, 1);
            this.groupBox8.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox8.Name = "groupBox8";
            this.groupBox8.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox8.Size = new System.Drawing.Size(180, 161);
            this.groupBox8.TabIndex = 30;
            this.groupBox8.TabStop = false;
            this.groupBox8.Text = "Run options";
            // 
            // pass1
            // 
            this.pass1.AutoSize = true;
            this.pass1.Checked = true;
            this.pass1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.pass1.Location = new System.Drawing.Point(8, 138);
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
            this.pass0.Location = new System.Drawing.Point(8, 115);
            this.pass0.Name = "pass0";
            this.pass0.Size = new System.Drawing.Size(161, 17);
            this.pass0.TabIndex = 9;
            this.pass0.Text = "Pass 0: French / no vendors";
            this.pass0.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(98, 25);
            this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(40, 13);
            this.label7.TabIndex = 8;
            this.label7.Text = "passes";
            // 
            // passCount
            // 
            this.passCount.Location = new System.Drawing.Point(60, 23);
            this.passCount.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.passCount.Name = "passCount";
            this.passCount.Size = new System.Drawing.Size(36, 20);
            this.passCount.TabIndex = 7;
            this.passCount.Text = "1";
            // 
            // qualityEndTime
            // 
            this.qualityEndTime.Location = new System.Drawing.Point(100, 91);
            this.qualityEndTime.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.qualityEndTime.Name = "qualityEndTime";
            this.qualityEndTime.Size = new System.Drawing.Size(69, 20);
            this.qualityEndTime.TabIndex = 5;
            this.qualityEndTime.Text = "8:00 AM";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(25, 94);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(48, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "End time";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(25, 70);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(51, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Start time";
            // 
            // qualityStartTime
            // 
            this.qualityStartTime.Location = new System.Drawing.Point(100, 67);
            this.qualityStartTime.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.qualityStartTime.Name = "qualityStartTime";
            this.qualityStartTime.Size = new System.Drawing.Size(69, 20);
            this.qualityStartTime.TabIndex = 2;
            this.qualityStartTime.Text = "6:00 PM";
            // 
            // qualityRunNow
            // 
            this.qualityRunNow.AutoSize = true;
            this.qualityRunNow.Checked = true;
            this.qualityRunNow.Location = new System.Drawing.Point(8, 23);
            this.qualityRunNow.Margin = new System.Windows.Forms.Padding(4);
            this.qualityRunNow.Name = "qualityRunNow";
            this.qualityRunNow.Size = new System.Drawing.Size(45, 17);
            this.qualityRunNow.TabIndex = 1;
            this.qualityRunNow.TabStop = true;
            this.qualityRunNow.Text = "Run";
            this.qualityRunNow.UseVisualStyleBackColor = true;
            // 
            // qualityRunSchedule
            // 
            this.qualityRunSchedule.AutoSize = true;
            this.qualityRunSchedule.Location = new System.Drawing.Point(8, 47);
            this.qualityRunSchedule.Margin = new System.Windows.Forms.Padding(4);
            this.qualityRunSchedule.Name = "qualityRunSchedule";
            this.qualityRunSchedule.Size = new System.Drawing.Size(57, 17);
            this.qualityRunSchedule.TabIndex = 0;
            this.qualityRunSchedule.Text = "Run at";
            this.qualityRunSchedule.UseVisualStyleBackColor = true;
            // 
            // groupBox7
            // 
            this.groupBox7.Controls.Add(this.qualityBuildType);
            this.groupBox7.Location = new System.Drawing.Point(0, 170);
            this.groupBox7.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox7.Name = "groupBox7";
            this.groupBox7.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox7.Size = new System.Drawing.Size(184, 55);
            this.groupBox7.TabIndex = 29;
            this.groupBox7.TabStop = false;
            this.groupBox7.Text = "Build/test type";
            // 
            // qualityBuildType
            // 
            this.qualityBuildType.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.qualityBuildType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.qualityBuildType.FormattingEnabled = true;
            this.qualityBuildType.Items.AddRange(new object[] {
            "Test default",
            "Test 32-bit",
            "Test 64-bit",
            "Build and test 32-bit",
            "Build and test 64-bit"});
            this.qualityBuildType.Location = new System.Drawing.Point(8, 21);
            this.qualityBuildType.Margin = new System.Windows.Forms.Padding(4);
            this.qualityBuildType.Name = "qualityBuildType";
            this.qualityBuildType.Size = new System.Drawing.Size(166, 21);
            this.qualityBuildType.TabIndex = 2;
            // 
            // label18
            // 
            this.label18.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label18.Location = new System.Drawing.Point(7, 4);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(731, 44);
            this.label18.TabIndex = 31;
            this.label18.Text = "Skyline quality checks";
            this.label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // runQuality
            // 
            this.runQuality.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runQuality.Location = new System.Drawing.Point(631, 582);
            this.runQuality.Margin = new System.Windows.Forms.Padding(4);
            this.runQuality.Name = "runQuality";
            this.runQuality.Size = new System.Drawing.Size(100, 28);
            this.runQuality.TabIndex = 26;
            this.runQuality.Text = "Run";
            this.runQuality.UseVisualStyleBackColor = true;
            this.runQuality.Click += new System.EventHandler(this.Run);
            // 
            // tabOutput
            // 
            this.tabOutput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(190)))), ((int)(((byte)(210)))));
            this.tabOutput.Controls.Add(this.buttonOpenOutput);
            this.tabOutput.Controls.Add(this.comboBoxOutput);
            this.tabOutput.Controls.Add(this.label19);
            this.tabOutput.Controls.Add(this.buttonStop);
            this.tabOutput.Controls.Add(this.commandShell);
            this.tabOutput.Location = new System.Drawing.Point(4, 28);
            this.tabOutput.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabOutput.Name = "tabOutput";
            this.tabOutput.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabOutput.Size = new System.Drawing.Size(744, 619);
            this.tabOutput.TabIndex = 5;
            this.tabOutput.Text = "Output";
            // 
            // buttonOpenOutput
            // 
            this.buttonOpenOutput.Location = new System.Drawing.Point(300, 51);
            this.buttonOpenOutput.Margin = new System.Windows.Forms.Padding(0);
            this.buttonOpenOutput.Name = "buttonOpenOutput";
            this.buttonOpenOutput.Size = new System.Drawing.Size(89, 23);
            this.buttonOpenOutput.TabIndex = 33;
            this.buttonOpenOutput.Text = "Open log";
            this.buttonOpenOutput.UseVisualStyleBackColor = true;
            this.buttonOpenOutput.Click += new System.EventHandler(this.buttonOpenOutput_Click);
            // 
            // comboBoxOutput
            // 
            this.comboBoxOutput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxOutput.FormattingEnabled = true;
            this.comboBoxOutput.Location = new System.Drawing.Point(16, 52);
            this.comboBoxOutput.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxOutput.Name = "comboBoxOutput";
            this.comboBoxOutput.Size = new System.Drawing.Size(271, 21);
            this.comboBoxOutput.TabIndex = 32;
            this.comboBoxOutput.SelectedIndexChanged += new System.EventHandler(this.comboBoxOutput_SelectedIndexChanged);
            // 
            // label19
            // 
            this.label19.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label19.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label19.Location = new System.Drawing.Point(7, 4);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(731, 44);
            this.label19.TabIndex = 31;
            this.label19.Text = "Output console";
            this.label19.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // buttonStop
            // 
            this.buttonStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonStop.Enabled = false;
            this.buttonStop.Location = new System.Drawing.Point(631, 582);
            this.buttonStop.Margin = new System.Windows.Forms.Padding(4);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(100, 28);
            this.buttonStop.TabIndex = 27;
            this.buttonStop.Text = "Stop";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.Stop);
            // 
            // commandShell
            // 
            this.commandShell.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.commandShell.DefaultDirectory = null;
            this.commandShell.DoLiveUpdate = false;
            this.commandShell.FilterFunc = null;
            this.commandShell.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.commandShell.Location = new System.Drawing.Point(16, 82);
            this.commandShell.LogFile = null;
            this.commandShell.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.commandShell.Name = "commandShell";
            this.commandShell.NextCommand = 0;
            this.commandShell.Size = new System.Drawing.Size(715, 494);
            this.commandShell.StopButton = null;
            this.commandShell.TabIndex = 2;
            this.commandShell.Text = "";
            this.commandShell.WordWrap = false;
            // 
            // tabErrors
            // 
            this.tabErrors.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(195)))), ((int)(((byte)(190)))));
            this.tabErrors.Controls.Add(this.buttonOpenErrors);
            this.tabErrors.Controls.Add(this.comboBoxErrors);
            this.tabErrors.Controls.Add(this.label20);
            this.tabErrors.Controls.Add(this.buttonErrorsStop);
            this.tabErrors.Controls.Add(this.errorsShell);
            this.tabErrors.Location = new System.Drawing.Point(4, 28);
            this.tabErrors.Name = "tabErrors";
            this.tabErrors.Padding = new System.Windows.Forms.Padding(3);
            this.tabErrors.Size = new System.Drawing.Size(744, 619);
            this.tabErrors.TabIndex = 6;
            this.tabErrors.Text = "Errors";
            // 
            // buttonOpenErrors
            // 
            this.buttonOpenErrors.Location = new System.Drawing.Point(300, 51);
            this.buttonOpenErrors.Margin = new System.Windows.Forms.Padding(0);
            this.buttonOpenErrors.Name = "buttonOpenErrors";
            this.buttonOpenErrors.Size = new System.Drawing.Size(89, 23);
            this.buttonOpenErrors.TabIndex = 35;
            this.buttonOpenErrors.Text = "Open log";
            this.buttonOpenErrors.UseVisualStyleBackColor = true;
            this.buttonOpenErrors.Click += new System.EventHandler(this.buttonOpenErrors_Click);
            // 
            // comboBoxErrors
            // 
            this.comboBoxErrors.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxErrors.FormattingEnabled = true;
            this.comboBoxErrors.Location = new System.Drawing.Point(16, 52);
            this.comboBoxErrors.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxErrors.Name = "comboBoxErrors";
            this.comboBoxErrors.Size = new System.Drawing.Size(271, 21);
            this.comboBoxErrors.TabIndex = 34;
            this.comboBoxErrors.SelectedIndexChanged += new System.EventHandler(this.comboBoxErrors_SelectedIndexChanged);
            // 
            // label20
            // 
            this.label20.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label20.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label20.Location = new System.Drawing.Point(7, 4);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(731, 44);
            this.label20.TabIndex = 32;
            this.label20.Text = "Error summary";
            this.label20.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // buttonErrorsStop
            // 
            this.buttonErrorsStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonErrorsStop.BackColor = System.Drawing.Color.Transparent;
            this.buttonErrorsStop.Enabled = false;
            this.buttonErrorsStop.Location = new System.Drawing.Point(631, 582);
            this.buttonErrorsStop.Margin = new System.Windows.Forms.Padding(4);
            this.buttonErrorsStop.Name = "buttonErrorsStop";
            this.buttonErrorsStop.Size = new System.Drawing.Size(100, 28);
            this.buttonErrorsStop.TabIndex = 31;
            this.buttonErrorsStop.Text = "Stop";
            this.buttonErrorsStop.UseVisualStyleBackColor = false;
            this.buttonErrorsStop.Click += new System.EventHandler(this.Stop);
            // 
            // errorsShell
            // 
            this.errorsShell.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.errorsShell.DefaultDirectory = null;
            this.errorsShell.DoLiveUpdate = false;
            this.errorsShell.FilterFunc = null;
            this.errorsShell.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.errorsShell.Location = new System.Drawing.Point(16, 82);
            this.errorsShell.LogFile = null;
            this.errorsShell.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.errorsShell.Name = "errorsShell";
            this.errorsShell.NextCommand = 0;
            this.errorsShell.Size = new System.Drawing.Size(715, 494);
            this.errorsShell.StopButton = null;
            this.errorsShell.TabIndex = 30;
            this.errorsShell.Text = "";
            this.errorsShell.WordWrap = false;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(8, 2, 0, 2);
            this.menuStrip1.Size = new System.Drawing.Size(744, 24);
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
            this.openToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.openToolStripMenuItem.Text = "Open...";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.open_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.saveToolStripMenuItem.Text = "Save...";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.save_Click);
            // 
            // exitToolStripMenuItem1
            // 
            this.exitToolStripMenuItem1.Name = "exitToolStripMenuItem1";
            this.exitToolStripMenuItem1.Size = new System.Drawing.Size(186, 6);
            // 
            // createInstallerZipFileToolStripMenuItem
            // 
            this.createInstallerZipFileToolStripMenuItem.Name = "createInstallerZipFileToolStripMenuItem";
            this.createInstallerZipFileToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.createInstallerZipFileToolStripMenuItem.Text = "Create installer zip file";
            this.createInstallerZipFileToolStripMenuItem.Click += new System.EventHandler(this.CreateInstallerZipFile);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(186, 6);
            // 
            // exitToolStripMenuItem2
            // 
            this.exitToolStripMenuItem2.Name = "exitToolStripMenuItem2";
            this.exitToolStripMenuItem2.Size = new System.Drawing.Size(189, 22);
            this.exitToolStripMenuItem2.Text = "Exit";
            this.exitToolStripMenuItem2.Click += new System.EventHandler(this.exit_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.memoryUseToolStripMenuItem,
            this.runWithDebugger});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.viewToolStripMenuItem.Text = "Options";
            // 
            // memoryUseToolStripMenuItem
            // 
            this.memoryUseToolStripMenuItem.Name = "memoryUseToolStripMenuItem";
            this.memoryUseToolStripMenuItem.Size = new System.Drawing.Size(185, 22);
            this.memoryUseToolStripMenuItem.Text = "Show memory graph";
            this.memoryUseToolStripMenuItem.Click += new System.EventHandler(this.ViewMemoryUse);
            // 
            // runWithDebugger
            // 
            this.runWithDebugger.CheckOnClick = true;
            this.runWithDebugger.Name = "runWithDebugger";
            this.runWithDebugger.Size = new System.Drawing.Size(185, 22);
            this.runWithDebugger.Text = "Run with debugger";
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(116, 22);
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
            // qualityThumbnail
            // 
            this.qualityThumbnail.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.qualityThumbnail.Location = new System.Drawing.Point(363, 21);
            this.qualityThumbnail.Name = "qualityThumbnail";
            this.qualityThumbnail.ProcessId = 0;
            this.qualityThumbnail.Size = new System.Drawing.Size(160, 79);
            this.qualityThumbnail.TabIndex = 34;
            // 
            // SkylineTesterWindow
            // 
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(744, 697);
            this.Controls.Add(this.mainPanel);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(700, 640);
            this.Name = "SkylineTesterWindow";
            this.Text = "Skyline Tester";
            this.Load += new System.EventHandler(this.SkylineTesterWindow_Load);
            this.mainPanel.ResumeLayout(false);
            this.mainPanel.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.tabs.ResumeLayout(false);
            this.tabForms.ResumeLayout(false);
            this.tabForms.PerformLayout();
            this.groupBox13.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.tabTutorials.ResumeLayout(false);
            this.groupBox14.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.tabTests.ResumeLayout(false);
            this.groupBox15.ResumeLayout(false);
            this.groupBox15.PerformLayout();
            this.pauseGroup.ResumeLayout(false);
            this.pauseGroup.PerformLayout();
            this.windowsGroup.ResumeLayout(false);
            this.windowsGroup.PerformLayout();
            this.iterationsGroup.ResumeLayout(false);
            this.iterationsGroup.PerformLayout();
            this.testsGroup.ResumeLayout(false);
            this.testsGroup.PerformLayout();
            this.tabBuild.ResumeLayout(false);
            this.groupBox10.ResumeLayout(false);
            this.groupBox10.PerformLayout();
            this.groupBox16.ResumeLayout(false);
            this.groupBox16.PerformLayout();
            this.groupBox6.ResumeLayout(false);
            this.groupBox6.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.tabQuality.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.groupBox12.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.groupBox11.ResumeLayout(false);
            this.groupBox11.PerformLayout();
            this.groupBox9.ResumeLayout(false);
            this.groupBox9.PerformLayout();
            this.groupBox8.ResumeLayout(false);
            this.groupBox8.PerformLayout();
            this.groupBox7.ResumeLayout(false);
            this.tabOutput.ResumeLayout(false);
            this.tabErrors.ResumeLayout(false);
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
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem memoryUseToolStripMenuItem;
        private Button runTests;
        private TabControl tabs;
        private TabPage tabTests;
        private GroupBox testsGroup;
        private MyTreeView testsTree;
        private RadioButton skipCheckedTests;
        private RadioButton runCheckedTests;
        private Button button3;
        private Button button2;
        private TabPage tabForms;
        private GroupBox groupBox1;
        private MyTreeView formsTree;
        private GroupBox groupBox2;
        private Label label3;
        private TextBox pauseFormSeconds;
        private RadioButton pauseFormDelay;
        private RadioButton pauseFormButton;
        private Button runForms;
        private TabPage tabTutorials;
        private Button runTutorials;
        private GroupBox pauseGroup;
        private CheckBox testsFrench;
        private GroupBox windowsGroup;
        private CheckBox offscreen;
        private GroupBox iterationsGroup;
        private Label label2;
        private TextBox runLoopsCount;
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
        private TextBox pauseTutorialsSeconds;
        private RadioButton pauseTutorialsDelay;
        private RadioButton pauseTutorialsScreenShots;
        private RadioButton tutorialsDemoMode;
        private CheckBox pauseTestsScreenShots;
        private ToolStripMenuItem runWithDebugger;
        private CheckBox regenerateCache;
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
        private TabPage tabErrors;
        private Button buttonErrorsStop;
        private CommandShell errorsShell;
        private GroupBox groupBox10;
        private TextBox buildRoot;
        private Label labelSpecifyPath;
        private Button buttonDeleteBuild;
        private Label label15;
        private Label label16;
        private Label label14;
        private Label label17;
        private Label label19;
        private Label label20;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ComboBox comboBoxOutput;
        private Button buttonOpenOutput;
        private Button buttonOpenErrors;
        private ComboBox comboBoxErrors;
        private TableLayoutPanel tableLayoutPanel1;
        private GroupBox groupBox12;
        private TableLayoutPanel historyTable;
        private Panel panel1;
        private GroupBox groupBox11;
        private Panel panelMemoryGraph;
        private Button buttonDeleteRun;
        private Button buttonOpenLog;
        private Label labelLeaks;
        private Label labelFailures;
        private Label labelTestsRun;
        private Label labelDuration;
        private Label label12;
        private Label label13;
        private Label label10;
        private Label label9;
        private ComboBox comboRunDate;
        private Label label8;
        private GroupBox groupBox9;
        private RadioButton qualityAllTests;
        private RadioButton qualityChooseTests;
        private GroupBox groupBox8;
        private CheckBox pass1;
        private CheckBox pass0;
        private Label label7;
        private TextBox passCount;
        private TextBox qualityEndTime;
        private Label label6;
        private Label label1;
        private TextBox qualityStartTime;
        private RadioButton qualityRunNow;
        private RadioButton qualityRunSchedule;
        private GroupBox groupBox7;
        private ComboBox qualityBuildType;
        private Label label18;
        private WindowThumbnail qualityThumbnail;
    }
}

