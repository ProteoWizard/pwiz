using ZedGraph;

namespace SkylineTester
{
    partial class SkylineTesterWindow
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SkylineTesterWindow));
            this.mainPanel = new System.Windows.Forms.Panel();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.Tabs = new System.Windows.Forms.TabControl();
            this.tabForms = new System.Windows.Forms.TabPage();
            this.groupBox13 = new System.Windows.Forms.GroupBox();
            this.comboBoxFormsLanguage = new System.Windows.Forms.ComboBox();
            this.RegenerateCache = new System.Windows.Forms.CheckBox();
            this.runForms = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.PauseFormSeconds = new System.Windows.Forms.TextBox();
            this.PauseFormDelay = new System.Windows.Forms.RadioButton();
            this.PauseFormButton = new System.Windows.Forms.RadioButton();
            this.tabTutorials = new System.Windows.Forms.TabPage();
            this.groupBox14 = new System.Windows.Forms.GroupBox();
            this.comboBoxTutorialsLanguage = new System.Windows.Forms.ComboBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.TutorialsDemoMode = new System.Windows.Forms.RadioButton();
            this.label5 = new System.Windows.Forms.Label();
            this.PauseTutorialsSeconds = new System.Windows.Forms.TextBox();
            this.PauseTutorialsDelay = new System.Windows.Forms.RadioButton();
            this.PauseTutorialsScreenShots = new System.Windows.Forms.RadioButton();
            this.runTutorials = new System.Windows.Forms.Button();
            this.tabTests = new System.Windows.Forms.TabPage();
            this.groupBox15 = new System.Windows.Forms.GroupBox();
            this.checkBoxTestsJapanese = new System.Windows.Forms.CheckBox();
            this.checkBoxTestsChinese = new System.Windows.Forms.CheckBox();
            this.checkBoxTestsEnglish = new System.Windows.Forms.CheckBox();
            this.runTests = new System.Windows.Forms.Button();
            this.pauseGroup = new System.Windows.Forms.GroupBox();
            this.PauseTestsScreenShots = new System.Windows.Forms.CheckBox();
            this.cultureGroup = new System.Windows.Forms.GroupBox();
            this.CultureFrench = new System.Windows.Forms.CheckBox();
            this.CultureEnglish = new System.Windows.Forms.CheckBox();
            this.windowsGroup = new System.Windows.Forms.GroupBox();
            this.Offscreen = new System.Windows.Forms.CheckBox();
            this.iterationsGroup = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.RunLoopsCount = new System.Windows.Forms.TextBox();
            this.RunLoops = new System.Windows.Forms.RadioButton();
            this.RunIndefinitely = new System.Windows.Forms.RadioButton();
            this.testsGroup = new System.Windows.Forms.GroupBox();
            this.SkipCheckedTests = new System.Windows.Forms.RadioButton();
            this.RunCheckedTests = new System.Windows.Forms.RadioButton();
            this.button3 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.tabBuild = new System.Windows.Forms.TabPage();
            this.groupBox10 = new System.Windows.Forms.GroupBox();
            this.BuildClean = new System.Windows.Forms.CheckBox();
            this.StartSln = new System.Windows.Forms.CheckBox();
            this.runBuild = new System.Windows.Forms.Button();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.BuildBranch = new System.Windows.Forms.RadioButton();
            this.BuildTrunk = new System.Windows.Forms.RadioButton();
            this.BranchUrl = new System.Windows.Forms.TextBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.Build64 = new System.Windows.Forms.RadioButton();
            this.Build32 = new System.Windows.Forms.RadioButton();
            this.tabQuality = new System.Windows.Forms.TabPage();
            this.groupBox12 = new System.Windows.Forms.GroupBox();
            this.graphMemoryHistory = new ZedGraph.ZedGraphControl();
            this.graphFailures = new ZedGraph.ZedGraphControl();
            this.graphDuration = new ZedGraph.ZedGraphControl();
            this.graphTestsRun = new ZedGraph.ZedGraphControl();
            this.groupBox11 = new System.Windows.Forms.GroupBox();
            this.buttonDeleteRun = new System.Windows.Forms.Button();
            this.buttonOpenLog = new System.Windows.Forms.Button();
            this.labelLeaks = new System.Windows.Forms.Label();
            this.labelFailures = new System.Windows.Forms.Label();
            this.labelTestsRun = new System.Windows.Forms.Label();
            this.labelDuration = new System.Windows.Forms.Label();
            this.graphMemory = new ZedGraph.ZedGraphControl();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.comboRunDate = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.groupBox9 = new System.Windows.Forms.GroupBox();
            this.QualityAllTests = new System.Windows.Forms.RadioButton();
            this.QualityChooseTests = new System.Windows.Forms.RadioButton();
            this.runQuality = new System.Windows.Forms.Button();
            this.groupBox8 = new System.Windows.Forms.GroupBox();
            this.QualityEndTime = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.QualityStartTime = new System.Windows.Forms.TextBox();
            this.QualityStartNow = new System.Windows.Forms.RadioButton();
            this.QualityStartLater = new System.Windows.Forms.RadioButton();
            this.groupBox7 = new System.Windows.Forms.GroupBox();
            this.QualityBuildFirst = new System.Windows.Forms.RadioButton();
            this.QualityCurrentBuild = new System.Windows.Forms.RadioButton();
            this.tabOutput = new System.Windows.Forms.TabPage();
            this.buttonStop = new System.Windows.Forms.Button();
            this.linkLogFile = new System.Windows.Forms.LinkLabel();
            this.label7 = new System.Windows.Forms.Label();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.memoryUseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.RunWithDebugger = new System.Windows.Forms.ToolStripMenuItem();
            this.radioButton3 = new System.Windows.Forms.RadioButton();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.radioButton5 = new System.Windows.Forms.RadioButton();
            this.createInstallerZipFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.FormsTree = new SkylineTester.MyTreeView();
            this.TutorialsTree = new SkylineTester.MyTreeView();
            this.TestsTree = new SkylineTester.MyTreeView();
            this.commandShell = new SkylineTester.CommandShell();
            this.myTreeView1 = new SkylineTester.MyTreeView();
            this.mainPanel.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.Tabs.SuspendLayout();
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
            this.cultureGroup.SuspendLayout();
            this.windowsGroup.SuspendLayout();
            this.iterationsGroup.SuspendLayout();
            this.testsGroup.SuspendLayout();
            this.tabBuild.SuspendLayout();
            this.groupBox10.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.tabQuality.SuspendLayout();
            this.groupBox12.SuspendLayout();
            this.groupBox11.SuspendLayout();
            this.groupBox9.SuspendLayout();
            this.groupBox8.SuspendLayout();
            this.groupBox7.SuspendLayout();
            this.tabOutput.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainPanel
            // 
            this.mainPanel.BackColor = System.Drawing.Color.Silver;
            this.mainPanel.Controls.Add(this.statusStrip1);
            this.mainPanel.Controls.Add(this.Tabs);
            this.mainPanel.Controls.Add(this.menuStrip1);
            this.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainPanel.Location = new System.Drawing.Point(0, 0);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.Size = new System.Drawing.Size(737, 680);
            this.mainPanel.TabIndex = 0;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip1.Location = new System.Drawing.Point(0, 658);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 10, 0);
            this.statusStrip1.Size = new System.Drawing.Size(737, 22);
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
            // Tabs
            // 
            this.Tabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.Tabs.Controls.Add(this.tabForms);
            this.Tabs.Controls.Add(this.tabTutorials);
            this.Tabs.Controls.Add(this.tabTests);
            this.Tabs.Controls.Add(this.tabBuild);
            this.Tabs.Controls.Add(this.tabQuality);
            this.Tabs.Controls.Add(this.tabOutput);
            this.Tabs.Location = new System.Drawing.Point(-3, 27);
            this.Tabs.Name = "Tabs";
            this.Tabs.Padding = new System.Drawing.Point(20, 6);
            this.Tabs.SelectedIndex = 0;
            this.Tabs.Size = new System.Drawing.Size(743, 650);
            this.Tabs.TabIndex = 4;
            this.Tabs.SelectedIndexChanged += new System.EventHandler(this.TabChanged);
            // 
            // tabForms
            // 
            this.tabForms.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(205)))));
            this.tabForms.Controls.Add(this.groupBox13);
            this.tabForms.Controls.Add(this.RegenerateCache);
            this.tabForms.Controls.Add(this.runForms);
            this.tabForms.Controls.Add(this.groupBox1);
            this.tabForms.Controls.Add(this.groupBox2);
            this.tabForms.Location = new System.Drawing.Point(4, 28);
            this.tabForms.Name = "tabForms";
            this.tabForms.Padding = new System.Windows.Forms.Padding(3);
            this.tabForms.Size = new System.Drawing.Size(735, 618);
            this.tabForms.TabIndex = 1;
            this.tabForms.Text = "Forms";
            // 
            // groupBox13
            // 
            this.groupBox13.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(205)))));
            this.groupBox13.Controls.Add(this.comboBoxFormsLanguage);
            this.groupBox13.Location = new System.Drawing.Point(8, 85);
            this.groupBox13.Name = "groupBox13";
            this.groupBox13.Size = new System.Drawing.Size(228, 56);
            this.groupBox13.TabIndex = 21;
            this.groupBox13.TabStop = false;
            this.groupBox13.Text = "Language";
            // 
            // comboBoxFormsLanguage
            // 
            this.comboBoxFormsLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFormsLanguage.FormattingEnabled = true;
            this.comboBoxFormsLanguage.Location = new System.Drawing.Point(7, 20);
            this.comboBoxFormsLanguage.Name = "comboBoxFormsLanguage";
            this.comboBoxFormsLanguage.Size = new System.Drawing.Size(150, 21);
            this.comboBoxFormsLanguage.TabIndex = 0;
            // 
            // RegenerateCache
            // 
            this.RegenerateCache.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.RegenerateCache.AutoSize = true;
            this.RegenerateCache.Location = new System.Drawing.Point(261, 577);
            this.RegenerateCache.Name = "RegenerateCache";
            this.RegenerateCache.Size = new System.Drawing.Size(137, 17);
            this.RegenerateCache.TabIndex = 20;
            this.RegenerateCache.Text = "Regenerate list of forms";
            this.RegenerateCache.UseVisualStyleBackColor = true;
            // 
            // runForms
            // 
            this.runForms.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runForms.Location = new System.Drawing.Point(650, 573);
            this.runForms.Name = "runForms";
            this.runForms.Size = new System.Drawing.Size(75, 23);
            this.runForms.TabIndex = 19;
            this.runForms.Text = "Run";
            this.runForms.UseVisualStyleBackColor = true;
            this.runForms.Click += new System.EventHandler(this.RunForms);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.FormsTree);
            this.groupBox1.Location = new System.Drawing.Point(255, 6);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(470, 561);
            this.groupBox1.TabIndex = 18;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Forms";
            // 
            // groupBox2
            // 
            this.groupBox2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(205)))));
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.PauseFormSeconds);
            this.groupBox2.Controls.Add(this.PauseFormDelay);
            this.groupBox2.Controls.Add(this.PauseFormButton);
            this.groupBox2.Location = new System.Drawing.Point(8, 6);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(228, 73);
            this.groupBox2.TabIndex = 17;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Pause";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(110, 21);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "seconds";
            // 
            // PauseFormSeconds
            // 
            this.PauseFormSeconds.Location = new System.Drawing.Point(76, 19);
            this.PauseFormSeconds.Name = "PauseFormSeconds";
            this.PauseFormSeconds.Size = new System.Drawing.Size(32, 20);
            this.PauseFormSeconds.TabIndex = 4;
            this.PauseFormSeconds.Text = "0";
            // 
            // PauseFormDelay
            // 
            this.PauseFormDelay.AutoSize = true;
            this.PauseFormDelay.Checked = true;
            this.PauseFormDelay.Location = new System.Drawing.Point(6, 19);
            this.PauseFormDelay.Name = "PauseFormDelay";
            this.PauseFormDelay.Size = new System.Drawing.Size(70, 17);
            this.PauseFormDelay.TabIndex = 1;
            this.PauseFormDelay.TabStop = true;
            this.PauseFormDelay.Text = "Pause for";
            this.PauseFormDelay.UseVisualStyleBackColor = true;
            // 
            // PauseFormButton
            // 
            this.PauseFormButton.AutoSize = true;
            this.PauseFormButton.Location = new System.Drawing.Point(6, 42);
            this.PauseFormButton.Name = "PauseFormButton";
            this.PauseFormButton.Size = new System.Drawing.Size(103, 17);
            this.PauseFormButton.TabIndex = 0;
            this.PauseFormButton.Text = "Pause for button";
            this.PauseFormButton.UseVisualStyleBackColor = true;
            // 
            // tabTutorials
            // 
            this.tabTutorials.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(220)))), ((int)(((byte)(225)))));
            this.tabTutorials.Controls.Add(this.groupBox14);
            this.tabTutorials.Controls.Add(this.groupBox3);
            this.tabTutorials.Controls.Add(this.groupBox4);
            this.tabTutorials.Controls.Add(this.runTutorials);
            this.tabTutorials.Location = new System.Drawing.Point(4, 28);
            this.tabTutorials.Name = "tabTutorials";
            this.tabTutorials.Padding = new System.Windows.Forms.Padding(3);
            this.tabTutorials.Size = new System.Drawing.Size(735, 618);
            this.tabTutorials.TabIndex = 2;
            this.tabTutorials.Text = "Tutorials";
            // 
            // groupBox14
            // 
            this.groupBox14.BackColor = System.Drawing.Color.Transparent;
            this.groupBox14.Controls.Add(this.comboBoxTutorialsLanguage);
            this.groupBox14.Location = new System.Drawing.Point(8, 106);
            this.groupBox14.Name = "groupBox14";
            this.groupBox14.Size = new System.Drawing.Size(228, 56);
            this.groupBox14.TabIndex = 25;
            this.groupBox14.TabStop = false;
            this.groupBox14.Text = "Language";
            // 
            // comboBoxTutorialsLanguage
            // 
            this.comboBoxTutorialsLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTutorialsLanguage.FormattingEnabled = true;
            this.comboBoxTutorialsLanguage.Location = new System.Drawing.Point(7, 20);
            this.comboBoxTutorialsLanguage.Name = "comboBoxTutorialsLanguage";
            this.comboBoxTutorialsLanguage.Size = new System.Drawing.Size(150, 21);
            this.comboBoxTutorialsLanguage.TabIndex = 0;
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.Controls.Add(this.TutorialsTree);
            this.groupBox3.Location = new System.Drawing.Point(255, 6);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(470, 561);
            this.groupBox3.TabIndex = 24;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Tutorials";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.TutorialsDemoMode);
            this.groupBox4.Controls.Add(this.label5);
            this.groupBox4.Controls.Add(this.PauseTutorialsSeconds);
            this.groupBox4.Controls.Add(this.PauseTutorialsDelay);
            this.groupBox4.Controls.Add(this.PauseTutorialsScreenShots);
            this.groupBox4.Location = new System.Drawing.Point(8, 6);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(228, 94);
            this.groupBox4.TabIndex = 23;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Pause";
            // 
            // TutorialsDemoMode
            // 
            this.TutorialsDemoMode.AutoSize = true;
            this.TutorialsDemoMode.Location = new System.Drawing.Point(6, 65);
            this.TutorialsDemoMode.Name = "TutorialsDemoMode";
            this.TutorialsDemoMode.Size = new System.Drawing.Size(82, 17);
            this.TutorialsDemoMode.TabIndex = 6;
            this.TutorialsDemoMode.Text = "Demo mode";
            this.TutorialsDemoMode.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(110, 21);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(47, 13);
            this.label5.TabIndex = 5;
            this.label5.Text = "seconds";
            // 
            // PauseTutorialsSeconds
            // 
            this.PauseTutorialsSeconds.Location = new System.Drawing.Point(76, 19);
            this.PauseTutorialsSeconds.Name = "PauseTutorialsSeconds";
            this.PauseTutorialsSeconds.Size = new System.Drawing.Size(32, 20);
            this.PauseTutorialsSeconds.TabIndex = 4;
            this.PauseTutorialsSeconds.Text = "0";
            // 
            // PauseTutorialsDelay
            // 
            this.PauseTutorialsDelay.AutoSize = true;
            this.PauseTutorialsDelay.Checked = true;
            this.PauseTutorialsDelay.Location = new System.Drawing.Point(6, 19);
            this.PauseTutorialsDelay.Name = "PauseTutorialsDelay";
            this.PauseTutorialsDelay.Size = new System.Drawing.Size(70, 17);
            this.PauseTutorialsDelay.TabIndex = 1;
            this.PauseTutorialsDelay.TabStop = true;
            this.PauseTutorialsDelay.Text = "Pause for";
            this.PauseTutorialsDelay.UseVisualStyleBackColor = true;
            // 
            // PauseTutorialsScreenShots
            // 
            this.PauseTutorialsScreenShots.AutoSize = true;
            this.PauseTutorialsScreenShots.Location = new System.Drawing.Point(6, 42);
            this.PauseTutorialsScreenShots.Name = "PauseTutorialsScreenShots";
            this.PauseTutorialsScreenShots.Size = new System.Drawing.Size(133, 17);
            this.PauseTutorialsScreenShots.TabIndex = 0;
            this.PauseTutorialsScreenShots.Text = "Pause for screen shots";
            this.PauseTutorialsScreenShots.UseVisualStyleBackColor = true;
            // 
            // runTutorials
            // 
            this.runTutorials.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runTutorials.Location = new System.Drawing.Point(650, 573);
            this.runTutorials.Name = "runTutorials";
            this.runTutorials.Size = new System.Drawing.Size(75, 23);
            this.runTutorials.TabIndex = 22;
            this.runTutorials.Text = "Run";
            this.runTutorials.UseVisualStyleBackColor = true;
            this.runTutorials.Click += new System.EventHandler(this.RunTutorials);
            // 
            // tabTests
            // 
            this.tabTests.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(220)))), ((int)(((byte)(220)))));
            this.tabTests.Controls.Add(this.groupBox15);
            this.tabTests.Controls.Add(this.runTests);
            this.tabTests.Controls.Add(this.pauseGroup);
            this.tabTests.Controls.Add(this.cultureGroup);
            this.tabTests.Controls.Add(this.windowsGroup);
            this.tabTests.Controls.Add(this.iterationsGroup);
            this.tabTests.Controls.Add(this.testsGroup);
            this.tabTests.Location = new System.Drawing.Point(4, 28);
            this.tabTests.Name = "tabTests";
            this.tabTests.Padding = new System.Windows.Forms.Padding(3);
            this.tabTests.Size = new System.Drawing.Size(735, 618);
            this.tabTests.TabIndex = 0;
            this.tabTests.Text = "Tests";
            // 
            // groupBox15
            // 
            this.groupBox15.BackColor = System.Drawing.Color.Transparent;
            this.groupBox15.Controls.Add(this.checkBoxTestsJapanese);
            this.groupBox15.Controls.Add(this.checkBoxTestsChinese);
            this.groupBox15.Controls.Add(this.checkBoxTestsEnglish);
            this.groupBox15.Location = new System.Drawing.Point(8, 265);
            this.groupBox15.Name = "groupBox15";
            this.groupBox15.Size = new System.Drawing.Size(228, 92);
            this.groupBox15.TabIndex = 26;
            this.groupBox15.TabStop = false;
            this.groupBox15.Text = "Language";
            // 
            // checkBoxTestsJapanese
            // 
            this.checkBoxTestsJapanese.AutoSize = true;
            this.checkBoxTestsJapanese.Location = new System.Drawing.Point(7, 65);
            this.checkBoxTestsJapanese.Name = "checkBoxTestsJapanese";
            this.checkBoxTestsJapanese.Size = new System.Drawing.Size(72, 17);
            this.checkBoxTestsJapanese.TabIndex = 3;
            this.checkBoxTestsJapanese.Text = "Japanese";
            this.checkBoxTestsJapanese.UseVisualStyleBackColor = true;
            // 
            // checkBoxTestsChinese
            // 
            this.checkBoxTestsChinese.AutoSize = true;
            this.checkBoxTestsChinese.Location = new System.Drawing.Point(7, 42);
            this.checkBoxTestsChinese.Name = "checkBoxTestsChinese";
            this.checkBoxTestsChinese.Size = new System.Drawing.Size(64, 17);
            this.checkBoxTestsChinese.TabIndex = 2;
            this.checkBoxTestsChinese.Text = "Chinese";
            this.checkBoxTestsChinese.UseVisualStyleBackColor = true;
            // 
            // checkBoxTestsEnglish
            // 
            this.checkBoxTestsEnglish.AutoSize = true;
            this.checkBoxTestsEnglish.Checked = true;
            this.checkBoxTestsEnglish.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxTestsEnglish.Location = new System.Drawing.Point(7, 19);
            this.checkBoxTestsEnglish.Name = "checkBoxTestsEnglish";
            this.checkBoxTestsEnglish.Size = new System.Drawing.Size(60, 17);
            this.checkBoxTestsEnglish.TabIndex = 1;
            this.checkBoxTestsEnglish.Text = "English";
            this.checkBoxTestsEnglish.UseVisualStyleBackColor = true;
            // 
            // runTests
            // 
            this.runTests.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runTests.Location = new System.Drawing.Point(650, 573);
            this.runTests.Name = "runTests";
            this.runTests.Size = new System.Drawing.Size(75, 23);
            this.runTests.TabIndex = 14;
            this.runTests.Text = "Run";
            this.runTests.UseVisualStyleBackColor = true;
            this.runTests.Click += new System.EventHandler(this.RunTests);
            // 
            // pauseGroup
            // 
            this.pauseGroup.Controls.Add(this.PauseTestsScreenShots);
            this.pauseGroup.Location = new System.Drawing.Point(8, 6);
            this.pauseGroup.Name = "pauseGroup";
            this.pauseGroup.Size = new System.Drawing.Size(228, 45);
            this.pauseGroup.TabIndex = 20;
            this.pauseGroup.TabStop = false;
            this.pauseGroup.Text = "Pause";
            // 
            // PauseTestsScreenShots
            // 
            this.PauseTestsScreenShots.AutoSize = true;
            this.PauseTestsScreenShots.Location = new System.Drawing.Point(6, 19);
            this.PauseTestsScreenShots.Name = "PauseTestsScreenShots";
            this.PauseTestsScreenShots.Size = new System.Drawing.Size(134, 17);
            this.PauseTestsScreenShots.TabIndex = 2;
            this.PauseTestsScreenShots.Text = "Pause for screen shots";
            this.PauseTestsScreenShots.UseVisualStyleBackColor = true;
            this.PauseTestsScreenShots.CheckedChanged += new System.EventHandler(this.pauseTestsForScreenShots_CheckedChanged);
            // 
            // cultureGroup
            // 
            this.cultureGroup.Controls.Add(this.CultureFrench);
            this.cultureGroup.Controls.Add(this.CultureEnglish);
            this.cultureGroup.Location = new System.Drawing.Point(8, 189);
            this.cultureGroup.Name = "cultureGroup";
            this.cultureGroup.Size = new System.Drawing.Size(228, 70);
            this.cultureGroup.TabIndex = 19;
            this.cultureGroup.TabStop = false;
            this.cultureGroup.Text = "Number format";
            // 
            // CultureFrench
            // 
            this.CultureFrench.AutoSize = true;
            this.CultureFrench.Checked = true;
            this.CultureFrench.CheckState = System.Windows.Forms.CheckState.Checked;
            this.CultureFrench.Location = new System.Drawing.Point(7, 44);
            this.CultureFrench.Name = "CultureFrench";
            this.CultureFrench.Size = new System.Drawing.Size(59, 17);
            this.CultureFrench.TabIndex = 1;
            this.CultureFrench.Text = "French";
            this.CultureFrench.UseVisualStyleBackColor = true;
            // 
            // CultureEnglish
            // 
            this.CultureEnglish.AutoSize = true;
            this.CultureEnglish.Checked = true;
            this.CultureEnglish.CheckState = System.Windows.Forms.CheckState.Checked;
            this.CultureEnglish.Location = new System.Drawing.Point(7, 20);
            this.CultureEnglish.Name = "CultureEnglish";
            this.CultureEnglish.Size = new System.Drawing.Size(60, 17);
            this.CultureEnglish.TabIndex = 0;
            this.CultureEnglish.Text = "English";
            this.CultureEnglish.UseVisualStyleBackColor = true;
            // 
            // windowsGroup
            // 
            this.windowsGroup.Controls.Add(this.Offscreen);
            this.windowsGroup.Location = new System.Drawing.Point(8, 57);
            this.windowsGroup.Name = "windowsGroup";
            this.windowsGroup.Size = new System.Drawing.Size(228, 47);
            this.windowsGroup.TabIndex = 18;
            this.windowsGroup.TabStop = false;
            this.windowsGroup.Text = "Windows";
            // 
            // Offscreen
            // 
            this.Offscreen.AutoSize = true;
            this.Offscreen.Location = new System.Drawing.Point(6, 19);
            this.Offscreen.Name = "Offscreen";
            this.Offscreen.Size = new System.Drawing.Size(75, 17);
            this.Offscreen.TabIndex = 1;
            this.Offscreen.Text = "Off screen";
            this.Offscreen.UseVisualStyleBackColor = true;
            this.Offscreen.CheckedChanged += new System.EventHandler(this.offscreen_CheckedChanged);
            // 
            // iterationsGroup
            // 
            this.iterationsGroup.Controls.Add(this.label2);
            this.iterationsGroup.Controls.Add(this.RunLoopsCount);
            this.iterationsGroup.Controls.Add(this.RunLoops);
            this.iterationsGroup.Controls.Add(this.RunIndefinitely);
            this.iterationsGroup.Location = new System.Drawing.Point(8, 110);
            this.iterationsGroup.Name = "iterationsGroup";
            this.iterationsGroup.Size = new System.Drawing.Size(228, 73);
            this.iterationsGroup.TabIndex = 17;
            this.iterationsGroup.TabStop = false;
            this.iterationsGroup.Text = "Loop";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(83, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(40, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "passes";
            // 
            // RunLoopsCount
            // 
            this.RunLoopsCount.Location = new System.Drawing.Point(49, 18);
            this.RunLoopsCount.Name = "RunLoopsCount";
            this.RunLoopsCount.Size = new System.Drawing.Size(32, 20);
            this.RunLoopsCount.TabIndex = 2;
            this.RunLoopsCount.Text = "1";
            // 
            // RunLoops
            // 
            this.RunLoops.AutoSize = true;
            this.RunLoops.Checked = true;
            this.RunLoops.Location = new System.Drawing.Point(6, 19);
            this.RunLoops.Name = "RunLoops";
            this.RunLoops.Size = new System.Drawing.Size(45, 17);
            this.RunLoops.TabIndex = 1;
            this.RunLoops.TabStop = true;
            this.RunLoops.Text = "Run";
            this.RunLoops.UseVisualStyleBackColor = true;
            // 
            // RunIndefinitely
            // 
            this.RunIndefinitely.AutoSize = true;
            this.RunIndefinitely.Location = new System.Drawing.Point(6, 44);
            this.RunIndefinitely.Name = "RunIndefinitely";
            this.RunIndefinitely.Size = new System.Drawing.Size(97, 17);
            this.RunIndefinitely.TabIndex = 0;
            this.RunIndefinitely.Text = "Run indefinitely";
            this.RunIndefinitely.UseVisualStyleBackColor = true;
            // 
            // testsGroup
            // 
            this.testsGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.testsGroup.Controls.Add(this.TestsTree);
            this.testsGroup.Controls.Add(this.SkipCheckedTests);
            this.testsGroup.Controls.Add(this.RunCheckedTests);
            this.testsGroup.Controls.Add(this.button3);
            this.testsGroup.Controls.Add(this.button2);
            this.testsGroup.Location = new System.Drawing.Point(255, 6);
            this.testsGroup.Name = "testsGroup";
            this.testsGroup.Size = new System.Drawing.Size(470, 561);
            this.testsGroup.TabIndex = 16;
            this.testsGroup.TabStop = false;
            this.testsGroup.Text = "Tests";
            // 
            // SkipCheckedTests
            // 
            this.SkipCheckedTests.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SkipCheckedTests.AutoSize = true;
            this.SkipCheckedTests.Location = new System.Drawing.Point(6, 532);
            this.SkipCheckedTests.Name = "SkipCheckedTests";
            this.SkipCheckedTests.Size = new System.Drawing.Size(116, 17);
            this.SkipCheckedTests.TabIndex = 14;
            this.SkipCheckedTests.Text = "Skip checked tests";
            this.SkipCheckedTests.UseVisualStyleBackColor = true;
            // 
            // RunCheckedTests
            // 
            this.RunCheckedTests.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.RunCheckedTests.AutoSize = true;
            this.RunCheckedTests.Checked = true;
            this.RunCheckedTests.Location = new System.Drawing.Point(6, 509);
            this.RunCheckedTests.Name = "RunCheckedTests";
            this.RunCheckedTests.Size = new System.Drawing.Size(115, 17);
            this.RunCheckedTests.TabIndex = 13;
            this.RunCheckedTests.TabStop = true;
            this.RunCheckedTests.Text = "Run checked tests";
            this.RunCheckedTests.UseVisualStyleBackColor = true;
            // 
            // button3
            // 
            this.button3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button3.Location = new System.Drawing.Point(85, 482);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 23);
            this.button3.TabIndex = 12;
            this.button3.Text = "Uncheck all";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.uncheckAll_Click);
            // 
            // button2
            // 
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button2.Location = new System.Drawing.Point(4, 482);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 11;
            this.button2.Text = "Check all";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.checkAll_Click);
            // 
            // tabBuild
            // 
            this.tabBuild.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(225)))), ((int)(((byte)(200)))));
            this.tabBuild.Controls.Add(this.groupBox10);
            this.tabBuild.Controls.Add(this.runBuild);
            this.tabBuild.Controls.Add(this.groupBox6);
            this.tabBuild.Controls.Add(this.groupBox5);
            this.tabBuild.Location = new System.Drawing.Point(4, 28);
            this.tabBuild.Margin = new System.Windows.Forms.Padding(2);
            this.tabBuild.Name = "tabBuild";
            this.tabBuild.Padding = new System.Windows.Forms.Padding(2);
            this.tabBuild.Size = new System.Drawing.Size(735, 618);
            this.tabBuild.TabIndex = 3;
            this.tabBuild.Text = "Build";
            // 
            // groupBox10
            // 
            this.groupBox10.Controls.Add(this.BuildClean);
            this.groupBox10.Controls.Add(this.StartSln);
            this.groupBox10.Location = new System.Drawing.Point(7, 170);
            this.groupBox10.Name = "groupBox10";
            this.groupBox10.Size = new System.Drawing.Size(442, 64);
            this.groupBox10.TabIndex = 24;
            this.groupBox10.TabStop = false;
            this.groupBox10.Text = "Options";
            // 
            // BuildClean
            // 
            this.BuildClean.AutoSize = true;
            this.BuildClean.Checked = true;
            this.BuildClean.CheckState = System.Windows.Forms.CheckState.Checked;
            this.BuildClean.Location = new System.Drawing.Point(7, 18);
            this.BuildClean.Margin = new System.Windows.Forms.Padding(2);
            this.BuildClean.Name = "BuildClean";
            this.BuildClean.Size = new System.Drawing.Size(78, 17);
            this.BuildClean.TabIndex = 25;
            this.BuildClean.Text = "Clean build";
            this.BuildClean.UseVisualStyleBackColor = true;
            // 
            // StartSln
            // 
            this.StartSln.AutoSize = true;
            this.StartSln.Checked = true;
            this.StartSln.CheckState = System.Windows.Forms.CheckState.Checked;
            this.StartSln.Location = new System.Drawing.Point(7, 40);
            this.StartSln.Margin = new System.Windows.Forms.Padding(2);
            this.StartSln.Name = "StartSln";
            this.StartSln.Size = new System.Drawing.Size(213, 17);
            this.StartSln.TabIndex = 24;
            this.StartSln.Text = "Open Skyline in Visual Studio after build";
            this.StartSln.UseVisualStyleBackColor = true;
            // 
            // runBuild
            // 
            this.runBuild.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runBuild.Location = new System.Drawing.Point(650, 573);
            this.runBuild.Name = "runBuild";
            this.runBuild.Size = new System.Drawing.Size(75, 23);
            this.runBuild.TabIndex = 22;
            this.runBuild.Text = "Run";
            this.runBuild.UseVisualStyleBackColor = true;
            this.runBuild.Click += new System.EventHandler(this.RunBuild);
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.BuildBranch);
            this.groupBox6.Controls.Add(this.BuildTrunk);
            this.groupBox6.Controls.Add(this.BranchUrl);
            this.groupBox6.Location = new System.Drawing.Point(7, 74);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(442, 90);
            this.groupBox6.TabIndex = 21;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Source";
            // 
            // BuildBranch
            // 
            this.BuildBranch.AutoSize = true;
            this.BuildBranch.Location = new System.Drawing.Point(7, 41);
            this.BuildBranch.Margin = new System.Windows.Forms.Padding(2);
            this.BuildBranch.Name = "BuildBranch";
            this.BuildBranch.Size = new System.Drawing.Size(59, 17);
            this.BuildBranch.TabIndex = 4;
            this.BuildBranch.Text = "Branch";
            this.BuildBranch.UseVisualStyleBackColor = true;
            // 
            // BuildTrunk
            // 
            this.BuildTrunk.AutoSize = true;
            this.BuildTrunk.Checked = true;
            this.BuildTrunk.Location = new System.Drawing.Point(7, 19);
            this.BuildTrunk.Margin = new System.Windows.Forms.Padding(2);
            this.BuildTrunk.Name = "BuildTrunk";
            this.BuildTrunk.Size = new System.Drawing.Size(53, 17);
            this.BuildTrunk.TabIndex = 3;
            this.BuildTrunk.TabStop = true;
            this.BuildTrunk.Text = "Trunk";
            this.BuildTrunk.UseVisualStyleBackColor = true;
            // 
            // BranchUrl
            // 
            this.BranchUrl.Location = new System.Drawing.Point(25, 59);
            this.BranchUrl.Margin = new System.Windows.Forms.Padding(2);
            this.BranchUrl.Name = "BranchUrl";
            this.BranchUrl.Size = new System.Drawing.Size(410, 20);
            this.BranchUrl.TabIndex = 2;
            this.BranchUrl.Text = "https://svn.code.sf.net/p/proteowizard/code/branches/work/BRANCHNAME";
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.Build64);
            this.groupBox5.Controls.Add(this.Build32);
            this.groupBox5.Location = new System.Drawing.Point(7, 6);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(442, 62);
            this.groupBox5.TabIndex = 20;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Architecture";
            // 
            // Build64
            // 
            this.Build64.AutoSize = true;
            this.Build64.Checked = true;
            this.Build64.Location = new System.Drawing.Point(7, 37);
            this.Build64.Margin = new System.Windows.Forms.Padding(2);
            this.Build64.Name = "Build64";
            this.Build64.Size = new System.Drawing.Size(51, 17);
            this.Build64.TabIndex = 5;
            this.Build64.TabStop = true;
            this.Build64.Text = "64 bit";
            this.Build64.UseVisualStyleBackColor = true;
            // 
            // Build32
            // 
            this.Build32.AutoSize = true;
            this.Build32.Location = new System.Drawing.Point(7, 17);
            this.Build32.Margin = new System.Windows.Forms.Padding(2);
            this.Build32.Name = "Build32";
            this.Build32.Size = new System.Drawing.Size(51, 17);
            this.Build32.TabIndex = 4;
            this.Build32.Text = "32 bit";
            this.Build32.UseVisualStyleBackColor = true;
            // 
            // tabQuality
            // 
            this.tabQuality.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(228)))), ((int)(((byte)(222)))), ((int)(((byte)(190)))));
            this.tabQuality.Controls.Add(this.groupBox12);
            this.tabQuality.Controls.Add(this.groupBox11);
            this.tabQuality.Controls.Add(this.groupBox9);
            this.tabQuality.Controls.Add(this.runQuality);
            this.tabQuality.Controls.Add(this.groupBox8);
            this.tabQuality.Controls.Add(this.groupBox7);
            this.tabQuality.Location = new System.Drawing.Point(4, 28);
            this.tabQuality.Margin = new System.Windows.Forms.Padding(2);
            this.tabQuality.Name = "tabQuality";
            this.tabQuality.Padding = new System.Windows.Forms.Padding(2);
            this.tabQuality.Size = new System.Drawing.Size(735, 618);
            this.tabQuality.TabIndex = 4;
            this.tabQuality.Text = "Quality";
            // 
            // groupBox12
            // 
            this.groupBox12.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox12.Controls.Add(this.graphMemoryHistory);
            this.groupBox12.Controls.Add(this.graphFailures);
            this.groupBox12.Controls.Add(this.graphDuration);
            this.groupBox12.Controls.Add(this.graphTestsRun);
            this.groupBox12.Location = new System.Drawing.Point(6, 297);
            this.groupBox12.Name = "groupBox12";
            this.groupBox12.Size = new System.Drawing.Size(719, 270);
            this.groupBox12.TabIndex = 29;
            this.groupBox12.TabStop = false;
            this.groupBox12.Text = "History";
            // 
            // graphMemoryHistory
            // 
            this.graphMemoryHistory.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.graphMemoryHistory.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphMemoryHistory.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphMemoryHistory.IsEnableVPan = false;
            this.graphMemoryHistory.IsEnableVZoom = false;
            this.graphMemoryHistory.Location = new System.Drawing.Point(540, 19);
            this.graphMemoryHistory.Margin = new System.Windows.Forms.Padding(4);
            this.graphMemoryHistory.Name = "graphMemoryHistory";
            this.graphMemoryHistory.ScrollGrace = 0D;
            this.graphMemoryHistory.ScrollMaxX = 0D;
            this.graphMemoryHistory.ScrollMaxY = 0D;
            this.graphMemoryHistory.ScrollMaxY2 = 0D;
            this.graphMemoryHistory.ScrollMinX = 0D;
            this.graphMemoryHistory.ScrollMinY = 0D;
            this.graphMemoryHistory.ScrollMinY2 = 0D;
            this.graphMemoryHistory.Size = new System.Drawing.Size(172, 244);
            this.graphMemoryHistory.TabIndex = 3;
            // 
            // graphFailures
            // 
            this.graphFailures.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.graphFailures.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphFailures.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphFailures.IsEnableVPan = false;
            this.graphFailures.IsEnableVZoom = false;
            this.graphFailures.Location = new System.Drawing.Point(362, 19);
            this.graphFailures.Margin = new System.Windows.Forms.Padding(4);
            this.graphFailures.Name = "graphFailures";
            this.graphFailures.ScrollGrace = 0D;
            this.graphFailures.ScrollMaxX = 0D;
            this.graphFailures.ScrollMaxY = 0D;
            this.graphFailures.ScrollMaxY2 = 0D;
            this.graphFailures.ScrollMinX = 0D;
            this.graphFailures.ScrollMinY = 0D;
            this.graphFailures.ScrollMinY2 = 0D;
            this.graphFailures.Size = new System.Drawing.Size(172, 244);
            this.graphFailures.TabIndex = 2;
            // 
            // graphDuration
            // 
            this.graphDuration.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.graphDuration.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphDuration.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphDuration.IsEnableVPan = false;
            this.graphDuration.IsEnableVZoom = false;
            this.graphDuration.Location = new System.Drawing.Point(184, 19);
            this.graphDuration.Margin = new System.Windows.Forms.Padding(4);
            this.graphDuration.Name = "graphDuration";
            this.graphDuration.ScrollGrace = 0D;
            this.graphDuration.ScrollMaxX = 0D;
            this.graphDuration.ScrollMaxY = 0D;
            this.graphDuration.ScrollMaxY2 = 0D;
            this.graphDuration.ScrollMinX = 0D;
            this.graphDuration.ScrollMinY = 0D;
            this.graphDuration.ScrollMinY2 = 0D;
            this.graphDuration.Size = new System.Drawing.Size(172, 244);
            this.graphDuration.TabIndex = 1;
            // 
            // graphTestsRun
            // 
            this.graphTestsRun.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.graphTestsRun.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphTestsRun.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphTestsRun.IsEnableVPan = false;
            this.graphTestsRun.IsEnableVZoom = false;
            this.graphTestsRun.Location = new System.Drawing.Point(6, 19);
            this.graphTestsRun.Margin = new System.Windows.Forms.Padding(4);
            this.graphTestsRun.Name = "graphTestsRun";
            this.graphTestsRun.ScrollGrace = 0D;
            this.graphTestsRun.ScrollMaxX = 0D;
            this.graphTestsRun.ScrollMaxY = 0D;
            this.graphTestsRun.ScrollMaxY2 = 0D;
            this.graphTestsRun.ScrollMinX = 0D;
            this.graphTestsRun.ScrollMinY = 0D;
            this.graphTestsRun.ScrollMinY2 = 0D;
            this.graphTestsRun.Size = new System.Drawing.Size(172, 244);
            this.graphTestsRun.TabIndex = 0;
            // 
            // groupBox11
            // 
            this.groupBox11.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox11.Controls.Add(this.buttonDeleteRun);
            this.groupBox11.Controls.Add(this.buttonOpenLog);
            this.groupBox11.Controls.Add(this.labelLeaks);
            this.groupBox11.Controls.Add(this.labelFailures);
            this.groupBox11.Controls.Add(this.labelTestsRun);
            this.groupBox11.Controls.Add(this.labelDuration);
            this.groupBox11.Controls.Add(this.graphMemory);
            this.groupBox11.Controls.Add(this.label12);
            this.groupBox11.Controls.Add(this.label13);
            this.groupBox11.Controls.Add(this.label10);
            this.groupBox11.Controls.Add(this.label9);
            this.groupBox11.Controls.Add(this.comboRunDate);
            this.groupBox11.Controls.Add(this.label8);
            this.groupBox11.Location = new System.Drawing.Point(190, 6);
            this.groupBox11.Name = "groupBox11";
            this.groupBox11.Size = new System.Drawing.Size(535, 284);
            this.groupBox11.TabIndex = 28;
            this.groupBox11.TabStop = false;
            this.groupBox11.Text = "Run results";
            // 
            // buttonDeleteRun
            // 
            this.buttonDeleteRun.Location = new System.Drawing.Point(413, 49);
            this.buttonDeleteRun.Name = "buttonDeleteRun";
            this.buttonDeleteRun.Size = new System.Drawing.Size(87, 23);
            this.buttonDeleteRun.TabIndex = 31;
            this.buttonDeleteRun.Text = "Delete run";
            this.buttonDeleteRun.UseVisualStyleBackColor = true;
            this.buttonDeleteRun.Click += new System.EventHandler(this.buttonDeleteRun_Click);
            // 
            // buttonOpenLog
            // 
            this.buttonOpenLog.Location = new System.Drawing.Point(294, 49);
            this.buttonOpenLog.Name = "buttonOpenLog";
            this.buttonOpenLog.Size = new System.Drawing.Size(87, 23);
            this.buttonOpenLog.TabIndex = 30;
            this.buttonOpenLog.Text = "Open log";
            this.buttonOpenLog.UseVisualStyleBackColor = true;
            this.buttonOpenLog.Click += new System.EventHandler(this.buttonOpenLog_Click);
            // 
            // labelLeaks
            // 
            this.labelLeaks.AutoSize = true;
            this.labelLeaks.Location = new System.Drawing.Point(233, 67);
            this.labelLeaks.Name = "labelLeaks";
            this.labelLeaks.Size = new System.Drawing.Size(13, 13);
            this.labelLeaks.TabIndex = 12;
            this.labelLeaks.Text = "0";
            // 
            // labelFailures
            // 
            this.labelFailures.AutoSize = true;
            this.labelFailures.Location = new System.Drawing.Point(233, 44);
            this.labelFailures.Name = "labelFailures";
            this.labelFailures.Size = new System.Drawing.Size(13, 13);
            this.labelFailures.TabIndex = 11;
            this.labelFailures.Text = "0";
            // 
            // labelTestsRun
            // 
            this.labelTestsRun.AutoSize = true;
            this.labelTestsRun.Location = new System.Drawing.Point(119, 67);
            this.labelTestsRun.Name = "labelTestsRun";
            this.labelTestsRun.Size = new System.Drawing.Size(13, 13);
            this.labelTestsRun.TabIndex = 9;
            this.labelTestsRun.Text = "0";
            // 
            // labelDuration
            // 
            this.labelDuration.AutoSize = true;
            this.labelDuration.Location = new System.Drawing.Point(119, 44);
            this.labelDuration.Name = "labelDuration";
            this.labelDuration.Size = new System.Drawing.Size(28, 13);
            this.labelDuration.TabIndex = 8;
            this.labelDuration.Text = "0:00";
            // 
            // graphMemory
            // 
            this.graphMemory.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.graphMemory.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphMemory.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphMemory.IsEnableVPan = false;
            this.graphMemory.IsEnableVZoom = false;
            this.graphMemory.Location = new System.Drawing.Point(9, 88);
            this.graphMemory.Margin = new System.Windows.Forms.Padding(4);
            this.graphMemory.Name = "graphMemory";
            this.graphMemory.ScrollGrace = 0D;
            this.graphMemory.ScrollMaxX = 0D;
            this.graphMemory.ScrollMaxY = 0D;
            this.graphMemory.ScrollMaxY2 = 0D;
            this.graphMemory.ScrollMinX = 0D;
            this.graphMemory.ScrollMinY = 0D;
            this.graphMemory.ScrollMinY2 = 0D;
            this.graphMemory.Size = new System.Drawing.Size(519, 190);
            this.graphMemory.TabIndex = 7;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(183, 67);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(39, 13);
            this.label12.TabIndex = 6;
            this.label12.Text = "Leaks:";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(183, 44);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(46, 13);
            this.label13.TabIndex = 5;
            this.label13.Text = "Failures:";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(63, 67);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(54, 13);
            this.label10.TabIndex = 3;
            this.label10.Text = "Tests run:";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(63, 44);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(50, 13);
            this.label9.TabIndex = 2;
            this.label9.Text = "Duration:";
            // 
            // comboRunDate
            // 
            this.comboRunDate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRunDate.FormattingEnabled = true;
            this.comboRunDate.Location = new System.Drawing.Point(64, 17);
            this.comboRunDate.Name = "comboRunDate";
            this.comboRunDate.Size = new System.Drawing.Size(204, 21);
            this.comboRunDate.TabIndex = 1;
            this.comboRunDate.SelectedIndexChanged += new System.EventHandler(this.comboRunDate_SelectedIndexChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(6, 21);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(54, 13);
            this.label8.TabIndex = 0;
            this.label8.Text = "Run date:";
            // 
            // groupBox9
            // 
            this.groupBox9.Controls.Add(this.QualityAllTests);
            this.groupBox9.Controls.Add(this.QualityChooseTests);
            this.groupBox9.Location = new System.Drawing.Point(7, 214);
            this.groupBox9.Name = "groupBox9";
            this.groupBox9.Size = new System.Drawing.Size(177, 76);
            this.groupBox9.TabIndex = 27;
            this.groupBox9.TabStop = false;
            this.groupBox9.Text = "Test selection";
            // 
            // QualityAllTests
            // 
            this.QualityAllTests.AutoSize = true;
            this.QualityAllTests.Checked = true;
            this.QualityAllTests.Location = new System.Drawing.Point(6, 19);
            this.QualityAllTests.Name = "QualityAllTests";
            this.QualityAllTests.Size = new System.Drawing.Size(61, 17);
            this.QualityAllTests.TabIndex = 1;
            this.QualityAllTests.TabStop = true;
            this.QualityAllTests.Text = "All tests";
            this.QualityAllTests.UseVisualStyleBackColor = true;
            // 
            // QualityChooseTests
            // 
            this.QualityChooseTests.AutoSize = true;
            this.QualityChooseTests.Location = new System.Drawing.Point(6, 42);
            this.QualityChooseTests.Name = "QualityChooseTests";
            this.QualityChooseTests.Size = new System.Drawing.Size(159, 17);
            this.QualityChooseTests.TabIndex = 0;
            this.QualityChooseTests.Text = "Choose tests (see Tests tab)";
            this.QualityChooseTests.UseVisualStyleBackColor = true;
            // 
            // runQuality
            // 
            this.runQuality.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.runQuality.Location = new System.Drawing.Point(650, 573);
            this.runQuality.Name = "runQuality";
            this.runQuality.Size = new System.Drawing.Size(75, 23);
            this.runQuality.TabIndex = 26;
            this.runQuality.Text = "Run";
            this.runQuality.UseVisualStyleBackColor = true;
            this.runQuality.Click += new System.EventHandler(this.RunQuality);
            // 
            // groupBox8
            // 
            this.groupBox8.Controls.Add(this.QualityEndTime);
            this.groupBox8.Controls.Add(this.label6);
            this.groupBox8.Controls.Add(this.label1);
            this.groupBox8.Controls.Add(this.QualityStartTime);
            this.groupBox8.Controls.Add(this.QualityStartNow);
            this.groupBox8.Controls.Add(this.QualityStartLater);
            this.groupBox8.Location = new System.Drawing.Point(7, 6);
            this.groupBox8.Name = "groupBox8";
            this.groupBox8.Size = new System.Drawing.Size(177, 119);
            this.groupBox8.TabIndex = 25;
            this.groupBox8.TabStop = false;
            this.groupBox8.Text = "Schedule";
            // 
            // QualityEndTime
            // 
            this.QualityEndTime.Location = new System.Drawing.Point(76, 88);
            this.QualityEndTime.Margin = new System.Windows.Forms.Padding(2);
            this.QualityEndTime.Name = "QualityEndTime";
            this.QualityEndTime.Size = new System.Drawing.Size(53, 20);
            this.QualityEndTime.TabIndex = 5;
            this.QualityEndTime.Text = "8:00 AM";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(21, 90);
            this.label6.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(48, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "End time";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(21, 67);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(51, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Start time";
            // 
            // QualityStartTime
            // 
            this.QualityStartTime.Location = new System.Drawing.Point(76, 65);
            this.QualityStartTime.Margin = new System.Windows.Forms.Padding(2);
            this.QualityStartTime.Name = "QualityStartTime";
            this.QualityStartTime.Size = new System.Drawing.Size(53, 20);
            this.QualityStartTime.TabIndex = 2;
            this.QualityStartTime.Text = "6:00 PM";
            // 
            // QualityStartNow
            // 
            this.QualityStartNow.AutoSize = true;
            this.QualityStartNow.Checked = true;
            this.QualityStartNow.Location = new System.Drawing.Point(6, 19);
            this.QualityStartNow.Name = "QualityStartNow";
            this.QualityStartNow.Size = new System.Drawing.Size(70, 17);
            this.QualityStartNow.TabIndex = 1;
            this.QualityStartNow.TabStop = true;
            this.QualityStartNow.Text = "Start now";
            this.QualityStartNow.UseVisualStyleBackColor = true;
            // 
            // QualityStartLater
            // 
            this.QualityStartLater.AutoSize = true;
            this.QualityStartLater.Location = new System.Drawing.Point(6, 42);
            this.QualityStartLater.Name = "QualityStartLater";
            this.QualityStartLater.Size = new System.Drawing.Size(87, 17);
            this.QualityStartLater.TabIndex = 0;
            this.QualityStartLater.Text = "Delayed start";
            this.QualityStartLater.UseVisualStyleBackColor = true;
            // 
            // groupBox7
            // 
            this.groupBox7.Controls.Add(this.QualityBuildFirst);
            this.groupBox7.Controls.Add(this.QualityCurrentBuild);
            this.groupBox7.Location = new System.Drawing.Point(7, 131);
            this.groupBox7.Name = "groupBox7";
            this.groupBox7.Size = new System.Drawing.Size(177, 76);
            this.groupBox7.TabIndex = 24;
            this.groupBox7.TabStop = false;
            this.groupBox7.Text = "Build options";
            // 
            // QualityBuildFirst
            // 
            this.QualityBuildFirst.AutoSize = true;
            this.QualityBuildFirst.Location = new System.Drawing.Point(6, 42);
            this.QualityBuildFirst.Name = "QualityBuildFirst";
            this.QualityBuildFirst.Size = new System.Drawing.Size(137, 17);
            this.QualityBuildFirst.TabIndex = 1;
            this.QualityBuildFirst.Text = "Build first (see Build tab)";
            this.QualityBuildFirst.UseVisualStyleBackColor = true;
            // 
            // QualityCurrentBuild
            // 
            this.QualityCurrentBuild.AutoSize = true;
            this.QualityCurrentBuild.Checked = true;
            this.QualityCurrentBuild.Location = new System.Drawing.Point(6, 19);
            this.QualityCurrentBuild.Name = "QualityCurrentBuild";
            this.QualityCurrentBuild.Size = new System.Drawing.Size(105, 17);
            this.QualityCurrentBuild.TabIndex = 0;
            this.QualityCurrentBuild.TabStop = true;
            this.QualityCurrentBuild.Text = "Use current build";
            this.QualityCurrentBuild.UseVisualStyleBackColor = true;
            // 
            // tabOutput
            // 
            this.tabOutput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(190)))), ((int)(((byte)(210)))));
            this.tabOutput.Controls.Add(this.buttonStop);
            this.tabOutput.Controls.Add(this.linkLogFile);
            this.tabOutput.Controls.Add(this.label7);
            this.tabOutput.Controls.Add(this.commandShell);
            this.tabOutput.Location = new System.Drawing.Point(4, 28);
            this.tabOutput.Margin = new System.Windows.Forms.Padding(2);
            this.tabOutput.Name = "tabOutput";
            this.tabOutput.Padding = new System.Windows.Forms.Padding(2);
            this.tabOutput.Size = new System.Drawing.Size(735, 618);
            this.tabOutput.TabIndex = 5;
            this.tabOutput.Text = "Output";
            // 
            // buttonStop
            // 
            this.buttonStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonStop.Enabled = false;
            this.buttonStop.Location = new System.Drawing.Point(650, 573);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(75, 23);
            this.buttonStop.TabIndex = 27;
            this.buttonStop.Text = "Stop";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.Stop);
            // 
            // linkLogFile
            // 
            this.linkLogFile.AutoSize = true;
            this.linkLogFile.Location = new System.Drawing.Point(68, 6);
            this.linkLogFile.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.linkLogFile.Name = "linkLogFile";
            this.linkLogFile.Size = new System.Drawing.Size(37, 13);
            this.linkLogFile.TabIndex = 1;
            this.linkLogFile.TabStop = true;
            this.linkLogFile.Text = "log file";
            this.linkLogFile.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLogFile_LinkClicked);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(10, 6);
            this.label7.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(59, 13);
            this.label7.TabIndex = 0;
            this.label7.Text = "Output log:";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.viewToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(737, 24);
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
            this.RunWithDebugger});
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
            // RunWithDebugger
            // 
            this.RunWithDebugger.CheckOnClick = true;
            this.RunWithDebugger.Name = "RunWithDebugger";
            this.RunWithDebugger.Size = new System.Drawing.Size(185, 22);
            this.RunWithDebugger.Text = "Run with debugger";
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
            // FormsTree
            // 
            this.FormsTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.FormsTree.CheckBoxes = true;
            this.FormsTree.Location = new System.Drawing.Point(6, 19);
            this.FormsTree.Name = "FormsTree";
            this.FormsTree.Size = new System.Drawing.Size(458, 536);
            this.FormsTree.TabIndex = 15;
            this.FormsTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.node_AfterCheck);
            this.FormsTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FormsTree_AfterSelect);
            // 
            // TutorialsTree
            // 
            this.TutorialsTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TutorialsTree.CheckBoxes = true;
            this.TutorialsTree.Location = new System.Drawing.Point(6, 19);
            this.TutorialsTree.Name = "TutorialsTree";
            this.TutorialsTree.Size = new System.Drawing.Size(458, 535);
            this.TutorialsTree.TabIndex = 15;
            this.TutorialsTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.node_AfterCheck);
            // 
            // TestsTree
            // 
            this.TestsTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TestsTree.CheckBoxes = true;
            this.TestsTree.Location = new System.Drawing.Point(6, 19);
            this.TestsTree.Name = "TestsTree";
            this.TestsTree.Size = new System.Drawing.Size(458, 456);
            this.TestsTree.TabIndex = 15;
            this.TestsTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.node_AfterCheck);
            // 
            // commandShell
            // 
            this.commandShell.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.commandShell.DefaultDirectory = null;
            this.commandShell.FilterFunc = null;
            this.commandShell.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.commandShell.Location = new System.Drawing.Point(13, 28);
            this.commandShell.LogFile = null;
            this.commandShell.Margin = new System.Windows.Forms.Padding(2);
            this.commandShell.Name = "commandShell";
            this.commandShell.Size = new System.Drawing.Size(712, 540);
            this.commandShell.StopButton = null;
            this.commandShell.TabIndex = 2;
            this.commandShell.Text = "";
            this.commandShell.WordWrap = false;
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
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(737, 680);
            this.Controls.Add(this.mainPanel);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.MinimumSize = new System.Drawing.Size(500, 510);
            this.Name = "SkylineTesterWindow";
            this.Text = "Skyline Tester";
            this.Load += new System.EventHandler(this.SkylineTesterWindow_Load);
            this.mainPanel.ResumeLayout(false);
            this.mainPanel.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.Tabs.ResumeLayout(false);
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
            this.cultureGroup.ResumeLayout(false);
            this.cultureGroup.PerformLayout();
            this.windowsGroup.ResumeLayout(false);
            this.windowsGroup.PerformLayout();
            this.iterationsGroup.ResumeLayout(false);
            this.iterationsGroup.PerformLayout();
            this.testsGroup.ResumeLayout(false);
            this.testsGroup.PerformLayout();
            this.tabBuild.ResumeLayout(false);
            this.groupBox10.ResumeLayout(false);
            this.groupBox10.PerformLayout();
            this.groupBox6.ResumeLayout(false);
            this.groupBox6.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.tabQuality.ResumeLayout(false);
            this.groupBox12.ResumeLayout(false);
            this.groupBox11.ResumeLayout(false);
            this.groupBox11.PerformLayout();
            this.groupBox9.ResumeLayout(false);
            this.groupBox9.PerformLayout();
            this.groupBox8.ResumeLayout(false);
            this.groupBox8.PerformLayout();
            this.groupBox7.ResumeLayout(false);
            this.groupBox7.PerformLayout();
            this.tabOutput.ResumeLayout(false);
            this.tabOutput.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel mainPanel;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator exitToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem memoryUseToolStripMenuItem;
        private System.Windows.Forms.Button runTests;
        private System.Windows.Forms.TabControl Tabs;
        private System.Windows.Forms.TabPage tabTests;
        private System.Windows.Forms.GroupBox testsGroup;
        private MyTreeView TestsTree;
        private System.Windows.Forms.RadioButton SkipCheckedTests;
        private System.Windows.Forms.RadioButton RunCheckedTests;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.TabPage tabForms;
        private System.Windows.Forms.GroupBox groupBox1;
        private MyTreeView FormsTree;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox PauseFormSeconds;
        private System.Windows.Forms.RadioButton PauseFormDelay;
        private System.Windows.Forms.RadioButton PauseFormButton;
        private System.Windows.Forms.Button runForms;
        private System.Windows.Forms.TabPage tabTutorials;
        private System.Windows.Forms.Button runTutorials;
        private System.Windows.Forms.GroupBox pauseGroup;
        private System.Windows.Forms.GroupBox cultureGroup;
        private System.Windows.Forms.CheckBox CultureFrench;
        private System.Windows.Forms.CheckBox CultureEnglish;
        private System.Windows.Forms.GroupBox windowsGroup;
        private System.Windows.Forms.CheckBox Offscreen;
        private System.Windows.Forms.GroupBox iterationsGroup;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox RunLoopsCount;
        private System.Windows.Forms.RadioButton RunLoops;
        private System.Windows.Forms.RadioButton RunIndefinitely;
        private System.Windows.Forms.RadioButton radioButton3;
        private System.Windows.Forms.RadioButton radioButton2;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label4;
        private MyTreeView myTreeView1;
        private System.Windows.Forms.RadioButton radioButton5;
        private System.Windows.Forms.GroupBox groupBox3;
        private MyTreeView TutorialsTree;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox PauseTutorialsSeconds;
        private System.Windows.Forms.RadioButton PauseTutorialsDelay;
        private System.Windows.Forms.RadioButton PauseTutorialsScreenShots;
        private System.Windows.Forms.RadioButton TutorialsDemoMode;
        private System.Windows.Forms.CheckBox PauseTestsScreenShots;
        private System.Windows.Forms.ToolStripMenuItem RunWithDebugger;
        private System.Windows.Forms.CheckBox RegenerateCache;
        private System.Windows.Forms.TabPage tabBuild;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.TextBox BranchUrl;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button runBuild;
        private System.Windows.Forms.RadioButton BuildBranch;
        private System.Windows.Forms.RadioButton BuildTrunk;
        private System.Windows.Forms.TabPage tabQuality;
        private System.Windows.Forms.Button runQuality;
        private System.Windows.Forms.GroupBox groupBox8;
        private System.Windows.Forms.TextBox QualityEndTime;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox QualityStartTime;
        private System.Windows.Forms.RadioButton QualityStartNow;
        private System.Windows.Forms.RadioButton QualityStartLater;
        private System.Windows.Forms.GroupBox groupBox7;
        private System.Windows.Forms.RadioButton QualityBuildFirst;
        private System.Windows.Forms.RadioButton QualityCurrentBuild;
        private System.Windows.Forms.TabPage tabOutput;
        private System.Windows.Forms.Button buttonStop;
        private CommandShell commandShell;
        private System.Windows.Forms.LinkLabel linkLogFile;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.RadioButton Build64;
        private System.Windows.Forms.RadioButton Build32;
        private System.Windows.Forms.GroupBox groupBox9;
        private System.Windows.Forms.RadioButton QualityAllTests;
        private System.Windows.Forms.RadioButton QualityChooseTests;
        private System.Windows.Forms.GroupBox groupBox10;
        private System.Windows.Forms.CheckBox BuildClean;
        private System.Windows.Forms.CheckBox StartSln;
        private System.Windows.Forms.GroupBox groupBox12;
        private ZedGraphControl graphMemoryHistory;
        private ZedGraphControl graphFailures;
        private ZedGraphControl graphDuration;
        private ZedGraphControl graphTestsRun;
        private ZedGraphControl graphMemory;
        private System.Windows.Forms.GroupBox groupBox11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox comboRunDate;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label labelLeaks;
        private System.Windows.Forms.Label labelFailures;
        private System.Windows.Forms.Label labelTestsRun;
        private System.Windows.Forms.Label labelDuration;
        private System.Windows.Forms.GroupBox groupBox13;
        private System.Windows.Forms.ComboBox comboBoxFormsLanguage;
        private System.Windows.Forms.GroupBox groupBox14;
        private System.Windows.Forms.ComboBox comboBoxTutorialsLanguage;
        private System.Windows.Forms.GroupBox groupBox15;
        private System.Windows.Forms.CheckBox checkBoxTestsJapanese;
        private System.Windows.Forms.CheckBox checkBoxTestsChinese;
        private System.Windows.Forms.CheckBox checkBoxTestsEnglish;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.Button buttonDeleteRun;
        private System.Windows.Forms.Button buttonOpenLog;
        private System.Windows.Forms.ToolStripMenuItem createInstallerZipFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;

    }
}

