/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;
using ZedGraph;
using Label = System.Windows.Forms.Label;

namespace SkylineTester
{
    public partial class SkylineTesterWindow : Form
    {
        #region Fields

        public const string QualityLogsDirectory = "Quality logs";
        public const string SummaryLog = "Summary.log";
        public const string SkylineTesterZip = "SkylineTester.zip";
        public const string SkylineTesterFiles = "SkylineTester Files";

        public string Subversion { get; private set; }
        public string Devenv { get; private set; }
        public string RootDir { get; private set; }
        public string ExeDir { get; private set; }
        public string DefaultLogFile { get; private set; }
        public Summary Summary { get; private set; }
        public Summary.Run NewQualityRun { get; set; }
        public int TestsRun { get; set; }
        public string LastTestResult { get; set; }
        public string LastRunName { get; set; }

        private readonly Dictionary<string, string> _languageNames = new Dictionary<string, string>
        {
            {"en", "English"},
            {"fr", "French"},
            {"ja", "Japanese"},
            {"zh", "Chinese"}
        };

        private static readonly string[] TEST_DLLS = { "Test.dll", "TestA.dll", "TestFunctional.dll", "TestTutorial.dll" };
        private static readonly string[] FORMS_DLLS = { "TestFunctional.dll", "TestTutorial.dll" };
        private static readonly string[] TUTORIAL_DLLS = { "TestTutorial.dll" };

        private List<string> _formsTestList;
        private readonly string _resultsDir;
        private readonly string _openFile;

        private Button[] _runButtons;
        private TabBase _runningTab;
        private DateTime _runStartTime;
        private Timer _runTimer;

        private TabForms _tabForms;
        private TabTutorials _tabTutorials;
        private TabTests _tabTests;
        private TabBuild _tabBuild;
        private TabQuality _tabQuality;
        private TabOutput _tabOutput;
        private TabErrors _tabErrors;
        private TabBase[] _tabs;

        private ZedGraphControl graphMemory;
        private ZedGraphControl graphMemoryHistory;
        private ZedGraphControl graphFailures;
        private ZedGraphControl graphDuration;
        private ZedGraphControl graphTestsRun;

        #endregion Fields

        #region Create and load window

        public SkylineTesterWindow()
        {
            InitializeComponent();
        }

        public SkylineTesterWindow(string[] args)
        {
            InitializeComponent();

            string exeFile = Assembly.GetExecutingAssembly().Location;
            ExeDir = Path.GetDirectoryName(exeFile);
            RootDir = ExeDir;
            while (RootDir != null)
            {
                if (Path.GetFileName(RootDir).StartsWith("Skyline"))
                    break;
                RootDir = Path.GetDirectoryName(RootDir);
            }
            if (RootDir == null)
                throw new ApplicationException("Can't find Skyline or SkylineTester directory");

            _resultsDir = Path.Combine(RootDir, "SkylineTester Results");
            DefaultLogFile = Path.Combine(RootDir, "SkylineTester.log");
            if (File.Exists(DefaultLogFile))
                Try.Multi<Exception>(() => File.Delete(DefaultLogFile));

            if (args.Length > 0)
                _openFile = args[0];
        }

        private void SkylineTesterWindow_Load(object sender, EventArgs e)
        {
            if (!Program.IsRunning)
                return; // design mode

            // Register file/exe/icon associations.
            var checkRegistry = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Classes\SkylineTester\shell\open\command", null, null);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\SkylineTester\shell\open\command", null,
                Assembly.GetExecutingAssembly().Location.Quote() + @" ""%1""");
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\.skyt", null, "SkylineTester");
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\.skytr", null, "SkylineTester");
            
            // Refresh shell if association changed.
            if (checkRegistry == null)
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            _runButtons = new[]
            {
                runForms, runTutorials, runTests, runBuild, runQuality
            };

            // Try to find where subversion is available.
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            Subversion = ChooseMostRecentFile(
                Path.Combine(programFiles, @"Subversion\bin\svn.exe"),
                Path.Combine(programFiles, @"VisualSVN\bin\svn.exe"));

            // Find Visual Studio, if available.
            Devenv = Path.Combine(programFiles, @"Microsoft Visual Studio 10.0\Common7\IDE\devenv.exe");
            if (!File.Exists(Devenv))
                Devenv = null;

            commandShell.StopButton = buttonStop;
            errorsShell.StopButton = buttonErrorsStop;
            commandShell.DoLiveUpdate = true;
            commandShell.AddColorPattern("# ", Color.DarkGreen);
            commandShell.AddColorPattern("> ", Color.FromArgb(120, 120, 120));
            commandShell.AddColorPattern("...skipped ", Color.Orange);
            commandShell.AddColorPattern("...failed ", Color.Red);
            commandShell.AddColorPattern("!!!", Color.Red);
            errorsShell.AddColorPattern("!!!", Color.FromArgb(150, 0, 0));

            commandShell.FilterFunc = line =>
            {
                if (line != null)
                {
                    if (line.StartsWith("#@ "))
                    {
                        // Update status.
                        RunUI(() => statusLabel.Text = line.Substring(3));
                        return false;
                    }

                    if (line.StartsWith("...skipped ") ||
                        line.StartsWith("...failed ") ||
                        line.StartsWith("!!! "))
                    {
                        errorsShell.Log(line);
                        RunUI(() => errorsShell.UpdateLog());
                    }

                    if (NewQualityRun != null)
                    {
                        if (line.StartsWith("!!! "))
                        {
                            var parts = line.Split(' ');
                            if (parts[2] == "LEAKED" || parts[2] == "CRT-LEAKED")
                                NewQualityRun.Leaks++;
                        }
                        else if (line.Length > 6 && line[0] == '[' && line[6] == ']' && line.Contains(" failures, "))
                        {
                            lock (NewQualityRun)
                            {
                                LastTestResult = line;
                                TestsRun++;
                            }
                        }
                    }
                }
                return true;
            };

            var loader = new BackgroundWorker();
            loader.DoWork += BackgroundLoad;
            loader.RunWorkerAsync();

            TabBase.MainWindow = this;
            _tabForms = new TabForms();
            _tabTutorials = new TabTutorials();
            _tabTests = new TabTests();
            _tabBuild = new TabBuild();
            _tabQuality = new TabQuality();
            _tabOutput = new TabOutput();
            _tabErrors = new TabErrors();

            _tabs = new TabBase[]
            {
                _tabForms,
                _tabTutorials,
                _tabTests,
                _tabBuild,
                _tabQuality,
                _tabOutput,
                _tabErrors
            };

            InitQuality();
            _tabForms.Open();
            statusLabel.Text = "";
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private void BackgroundLoad(object sender, DoWorkEventArgs e)
        {
            _formsTestList = new List<string>();

            try
            {
                // Load all tests from each dll.
                foreach (var testDll in TEST_DLLS)
                {
                    var tests = GetTestInfos(testDll).OrderBy(test => test).ToArray();
                    if (FORMS_DLLS.Contains(testDll))
                        _formsTestList.AddRange(tests);

                    // Add tests to test tree view.
                    var dllName = testDll.Replace(".dll", "");
                    RunUI(() =>
                    {
                        var childNodes = new TreeNode[tests.Length];
                        for (int i = 0; i < childNodes.Length; i++)
                            childNodes[i] = new TreeNode(tests[i]);
                        testsTree.Nodes.Add(new TreeNode(dllName, childNodes));
                    });
                }

                var tutorialTests = new List<string>();
                foreach (var tutorialDll in TUTORIAL_DLLS)
                    tutorialTests.AddRange(GetTestInfos(tutorialDll));
                var tutorialNodes = new TreeNode[tutorialTests.Count];
                tutorialTests = tutorialTests.OrderBy(test => test).ToList();
                RunUI(() =>
                {
                    for (int i = 0; i < tutorialNodes.Length; i++)
                    {
                        tutorialNodes[i] = new TreeNode(tutorialTests[i]);
                    }
                    tutorialsTree.Nodes.Add(new TreeNode("Tutorial tests", tutorialNodes));
                    tutorialsTree.ExpandAll();
                    tutorialsTree.Nodes[0].Checked = true;
                    TabTests.CheckAllChildNodes(tutorialsTree.Nodes[0], true);

                    // Add forms to forms tree view.
                    TabForms.CreateFormsTree();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            if (_openFile != null)
            {
                RunUI(() =>
                {
                    OpenFile(_openFile);
                    if (Path.GetExtension(_openFile) == ".skytr")
                    {
                        Run(null, null);
                    }
                });
            }
        }

        public IEnumerable<string> GetTestInfos(string testDll)
        {
            var dllPath = Path.Combine(ExeDir, testDll);
            var assembly = Assembly.LoadFrom(dllPath);
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                if (type.IsClass && HasAttribute(type, "TestClassAttribute"))
                {
                    var methods = type.GetMethods();
                    foreach (var method in methods)
                    {
                        if (HasAttribute(method, "TestMethodAttribute"))
                            yield return method.Name;
                    }
                }
            }
        }

        // Determine if the given class or method from an assembly has the given attribute.
        private static bool HasAttribute(MemberInfo info, string attributeName)
        {
            var attributes = info.GetCustomAttributes(false);
            return attributes.Any(attribute => attribute.ToString().EndsWith(attributeName));
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _runningTab = null;
            commandShell.Stop();
            base.OnClosed(e);
        }

        private void TabChanged(object sender, EventArgs e)
        {
            _tabs[tabs.SelectedIndex].Open();
        }

        public void ShowOutput()
        {
            tabs.SelectTab(tabOutput);
        }

        public void SetStatus(string status = null)
        {
            statusLabel.Text = status;
        }

        #region Menu

        private void open_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog {Filter = "Skyline Tester (*.skyt;*.skytr)|*.skyt;*.skytr"};
            if (openFileDialog.ShowDialog() != DialogResult.Cancel)
                OpenFile(openFileDialog.FileName);
        }

        private void save_Click(object sender, EventArgs e)
        {
            var saveFileDialog = new SaveFileDialog { Filter = "Skyline Tester (*.skyt;*.skytr)|*.skyt;*.skytr" };
            if (saveFileDialog.ShowDialog() == DialogResult.Cancel)
                return;

            var root = CreateElement(
                "SkylineTester",
                tabs,

                // Forms
                pauseFormDelay,
                pauseFormSeconds,
                pauseFormButton,
                formsLanguage,
                formsTree,

                // Tutorials
                pauseTutorialsDelay,
                pauseTutorialsSeconds,
                pauseTutorialsScreenShots,
                tutorialsDemoMode,
                tutorialsLanguage,
                tutorialsTree,

                // Tests
                pauseTestsScreenShots,
                offscreen,
                runLoops,
                runLoopsCount,
                runIndefinitely,
                testsEnglish,
                testsChinese,
                testsFrench,
                testsJapanese,
                testsTree,
                runCheckedTests,
                skipCheckedTests,
                
                // Build
                build32,
                build64,
                buildTrunk,
                buildBranch,
                branchUrl,
                nukeBuild,
                updateBuild,
                incrementalBuild,
                startSln,

                // Quality
                qualityRunNow,
                passCount,
                qualityRunSchedule,
                qualityStartTime,
                qualityEndTime,
                pass0,
                pass1,
                qualityBuildType,
                qualityAllTests,
                qualityChooseTests);

            XDocument doc = new XDocument(root);
            File.WriteAllText(saveFileDialog.FileName, doc.ToString());
        }

        private void exit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void about_Click(object sender, EventArgs e)
        {
            using (var aboutWindow = new AboutWindow())
            {
                aboutWindow.ShowDialog();
            }
        }

        private void CreateInstallerZipFile(object sender, EventArgs e)
        {
            var skylineDirectory = GetSkylineDirectory(ExeDir);
            if (skylineDirectory == null)
            {
                MessageBox.Show(this,
                    "The zip file can only be created if you're running SkylineTester from the bin directory of the Skyline project directory.");
                return;
            }

            string zipDirectory;
            using (var dlg = new CreateZipInstallerWindow())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                zipDirectory = dlg.ZipDirectory;
            }
            var zipFile = Path.Combine(zipDirectory, "SkylineTester.zip");

            TabBase.StartLog("Zip", null, true);
            commandShell.Add("{0} {1}", Assembly.GetExecutingAssembly().Location.Quote(), zipFile.Quote());
            RunCommands();
        }

        private void OpenFile(string file)
        {
            var doc = XDocument.Load(file);
            foreach (var element in doc.Descendants())
            {
                var control = Controls.Find(element.Name.ToString(), true).FirstOrDefault();
                if (control == null)
                    continue;

                var tab = control as TabControl;
                if (tab != null)
                    tab.SelectTab(element.Value);

                var button = control as RadioButton;
                if (button != null && element.Value == "true")
                    button.Checked = true;

                var checkBox = control as CheckBox;
                if (checkBox != null)
                    checkBox.Checked = (element.Value == "true");

                var textBox = control as TextBox;
                if (textBox != null)
                    textBox.Text = element.Value;

                var comboBox = control as ComboBox;
                if (comboBox != null)
                    comboBox.SelectedItem = element.Value;

                var treeView = control as TreeView;
                if (treeView != null)
                    CheckNodes(treeView, element.Value.Split(','));
            }
        }

        private XElement CreateElement(string name, params object[] childElements)
        {
            var element = new XElement(name);
            foreach (var child in childElements)
            {
                var tab = child as TabControl;
                if (tab != null)
                    element.Add(new XElement(tab.Name, tab.SelectedTab.Name));

                var button = child as RadioButton;
                if (button != null)
                    element.Add(new XElement(button.Name, button.Checked));

                var checkBox = child as CheckBox;
                if (checkBox != null)
                    element.Add(new XElement(checkBox.Name, checkBox.Checked));

                var textBox = child as TextBox;
                if (textBox != null)
                    element.Add(new XElement(textBox.Name, textBox.Text));

                var comboBox = child as ComboBox;
                if (comboBox != null)
                    element.Add(new XElement(comboBox.Name, comboBox.SelectedItem));

                var treeView = child as TreeView;
                if (treeView != null)
                    element.Add(new XElement(treeView.Name, GetCheckedNodes(treeView)));
            }

            return element;
        }

        private void ViewMemoryUse(object sender, EventArgs e)
        {
            MemoryChartWindow.ShowMemoryChart();
        }

        #endregion Menu

        #region Tree support

        // After a tree node's Checked property is changed, all its child nodes are updated to the same value.
        private void node_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // The code only executes if the user caused the checked state to change.
            if (e.Action != TreeViewAction.Unknown)
            {
                if (e.Node.Nodes.Count > 0)
                    TabTests.CheckAllChildNodes(e.Node, e.Node.Checked);
            }
        }

        private static void CheckNodes(TreeView treeView, ICollection<string> checkedNames)
        {
            foreach (TreeNode childNode in treeView.Nodes)
            {
                UncheckNodes(childNode);
                CheckNodes(childNode, checkedNames);
            }
        }

        private static void CheckNodes(TreeNode node, ICollection<string> checkedNames)
        {
            if (checkedNames.Contains(node.Text))
                node.Checked = true;

            foreach (TreeNode childNode in node.Nodes)
                CheckNodes(childNode, checkedNames);
        }

        private static void UncheckNodes(TreeNode node)
        {
            node.Checked = false;
            foreach (TreeNode childNode in node.Nodes)
                UncheckNodes(childNode);
        }

        private string GetCheckedNodes(TreeView treeView)
        {
            var names = new StringBuilder();
            foreach (TreeNode childNode in treeView.Nodes)
                GetCheckedNodes(childNode, names);
            return names.Length > 0 ? names.ToString(1, names.Length - 1) : String.Empty;
        }

        private void GetCheckedNodes(TreeNode node, StringBuilder names)
        {
            if (node.Checked)
            {
                names.Append(",");
                names.Append(node.Text);
            }

            foreach (TreeNode childNode in node.Nodes)
                GetCheckedNodes(childNode, names);
        }

        private void FormsTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            e.Node.Checked = !e.Node.Checked;
            formsTree.SelectedNode = null;
        }

        #endregion Tree support

        #region Accessors

        public TextBox          BranchUrl                   { get { return branchUrl; } }
        public CheckBox         Build32                     { get { return build32; } }
        public CheckBox         Build64                     { get { return build64; } }
        public TextBox          BuildRoot                   { get { return buildRoot; } }
        public RadioButton      BuildTrunk                  { get { return buildTrunk; } }
        public Button           ButtonDeleteBuild           { get { return buttonDeleteBuild; } }
        public Button           ButtonOpenErrors            { get { return buttonOpenErrors; } }
        public Button           ButtonOpenLog               { get { return buttonOpenLog; } }
        public Button           ButtonOpenOutput            { get { return buttonOpenOutput; } }
        public ComboBox         ComboErrors                 { get { return comboBoxErrors; } }
        public ComboBox         ComboOutput                 { get { return comboBoxOutput; } }
        public ComboBox         ComboRunDate                { get { return comboRunDate; } }
        public CommandShell     CommandShell                { get { return commandShell; } }
        public Button           ErrorsOpenLog               { get { return buttonOpenErrors; } }
        public CommandShell     ErrorsShell                 { get { return errorsShell; } }
        public ComboBox         FormsLanguage               { get { return formsLanguage; } }
        public MyTreeView       FormsTree                   { get { return formsTree; } }
        public ZedGraphControl  GraphDuration               { get { return graphDuration; } }
        public ZedGraphControl  GraphFailures               { get { return graphFailures; } }
        public ZedGraphControl  GraphMemory                 { get { return graphMemory; } }
        public ZedGraphControl  GraphMemoryHistory          { get { return graphMemoryHistory; } }
        public ZedGraphControl  GraphTestsRun               { get { return graphTestsRun; } }
        public Label            LabelDuration               { get { return labelDuration; } }
        public Label            LabelFailures               { get { return labelFailures; } }
        public Label            LabelLeaks                  { get { return labelLeaks; } }
        public Label            LabelSpecifyPath            { get { return labelSpecifyPath; } }
        public Label            LabelTestsRun               { get { return labelTestsRun; } }
        public RadioButton      NukeBuild                   { get { return nukeBuild; } }
        public CheckBox         Offscreen                   { get { return offscreen; } }
        public Button           OutputOpenLog               { get { return buttonOpenOutput; } }
        public CheckBox         Pass0                       { get { return pass0; } }
        public CheckBox         Pass1                       { get { return pass1; } }
        public TextBox          PassCount                   { get { return passCount; } }
        public RadioButton      PauseFormDelay              { get { return pauseFormDelay; } }
        public TextBox          PauseFormSeconds            { get { return pauseFormSeconds; } }
        public CheckBox         PauseTestsScreenShots       { get { return pauseTestsScreenShots; } }
        public RadioButton      PauseTutorialsScreenShots   { get { return pauseTutorialsScreenShots; } }
        public TextBox          PauseTutorialsSeconds       { get { return pauseTutorialsSeconds; } }
        public ComboBox         QualityBuildType            { get { return qualityBuildType; } }
        public RadioButton      QualityChooseTests          { get { return qualityChooseTests; } }
        public TextBox          QualityEndTime              { get { return qualityEndTime; } }
        public RadioButton      QualityRunSchedule          { get { return qualityRunSchedule; } }
        public TextBox          QualityStartTime            { get { return qualityStartTime; } }
        public CheckBox         RegenerateCache             { get { return regenerateCache; } }
        public RadioButton      RunIndefinitely             { get { return runIndefinitely; } }
        public TextBox          RunLoopsCount               { get { return runLoopsCount; } }
        public CheckBox         StartSln                    { get { return startSln; } }
        public RadioButton      SkipCheckedTests            { get { return skipCheckedTests; } }
        public PictureBox       SkylineThumbnail            { get { return skylineThumbnail; } }
        public TabPage          QualityPage                 { get { return tabQuality; } }
        public TabControl       Tabs                        { get { return tabs; } }
        public MyTreeView       TestsTree                   { get { return testsTree; } }
        public CheckBox         TestsChinese                { get { return testsChinese; } }
        public CheckBox         TestsEnglish                { get { return testsEnglish; } }
        public CheckBox         TestsFrench                 { get { return testsFrench; } }
        public CheckBox         TestsJapanese               { get { return testsJapanese; } }
        public RadioButton      TutorialsDemoMode           { get { return tutorialsDemoMode; } }
        public ComboBox         TutorialsLanguage           { get { return tutorialsLanguage; } }
        public MyTreeView       TutorialsTree               { get { return tutorialsTree; } }
        public RadioButton      UpdateBuild                 { get { return updateBuild; } }

        #endregion Accessors

        #region Control events

        private void comboBoxOutput_SelectedIndexChanged(object sender, EventArgs e)
        {
            commandShell.Load(GetSelectedLog(comboBoxOutput));
            commandShell.DoLiveUpdate = comboBoxOutput.SelectedIndex == 0;
        }

        private void buttonOpenOutput_Click(object sender, EventArgs e)
        {
            OpenSelectedLog(comboBoxOutput);
        }

        private void buttonDeleteBuild_Click(object sender, EventArgs e)
        {
            _tabBuild.DeleteBuild();
        }

        private void buttonBrowseBuild_Click(object sender, EventArgs e)
        {
            _tabBuild.BrowseBuild();
        }

        private void buttonOpenErrors_Click(object sender, EventArgs e)
        {
            _tabErrors.OpenLog();
        }

        private void comboBoxErrors_SelectedIndexChanged(object sender, EventArgs e)
        {
            _tabErrors.SelectLog();
        }

        private void checkAll_Click(object sender, EventArgs e)
        {
            _tabTests.CheckAll();
        }

        private void uncheckAll_Click(object sender, EventArgs e)
        {
            _tabTests.UncheckAll();
        }

        private void pauseTestsForScreenShots_CheckedChanged(object sender, EventArgs e)
        {
            _tabTests.PauseTestsForScreenShotsChanged();
        }

        private void offscreen_CheckedChanged(object sender, EventArgs e)
        {
            _tabTests.OffscreenChanged();
        }

        private void comboRunDate_SelectedIndexChanged(object sender, EventArgs e)
        {
            _tabQuality.RunDateChanged();
        }

        private void buttonOpenLog_Click(object sender, EventArgs e)
        {
            _tabQuality.OpenLog();
        }

        private void buttonDeleteRun_Click(object sender, EventArgs e)
        {
            _tabQuality.DeleteRun();
        }

        #endregion Control events
    }
}
