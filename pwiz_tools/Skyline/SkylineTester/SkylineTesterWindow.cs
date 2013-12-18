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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SkylineTester
{
    public partial class SkylineTesterWindow : Form
    {
        private static readonly string[] TEST_DLLS = { "Test.dll", "TestA.dll", "TestFunctional.dll", "TestTutorial.dll" };
        private static readonly string[] FORMS_DLLS = { "TestFunctional.dll", "TestTutorial.dll" };
        private static readonly string[] TUTORIAL_DLLS = { "TestTutorial.dll" };
        private Button[] _runButtons;

        private List<string> _formsTestList;
        private readonly string _rootDir;
        private readonly string _resultsDir;
        private readonly string _buildDir;
        private string _logFile;
        private TabPage _runningTab;
        private string _subversion;
        private string _devenv;
        private readonly string _exeDir;
        private readonly string _openFile;

        #region Create and load window

        public SkylineTesterWindow()
        {
            InitializeComponent();
        }

        public SkylineTesterWindow(string[] args)
        {
            InitializeComponent();

            string exeFile = Assembly.GetExecutingAssembly().Location;
            _exeDir = Path.GetDirectoryName(exeFile);
            _rootDir = Path.GetDirectoryName(_exeDir) ?? "";
            _buildDir = Path.Combine(_rootDir, "Build");
            _resultsDir = Path.Combine(_rootDir, "SkylineTester Results");
            if (args.Length > 0)
                _openFile = args[0];
        }

        private void SkylineTesterWindow_Load(object sender, EventArgs e)
        {
            if (!Program.IsRunning)
                return; // design mode

            // Register file/exe/icon associations.
            var checkRegistry = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Classes\SkylineTesterx\shell\open\command", null, null);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\SkylineTester\shell\open\command", null,
                Assembly.GetExecutingAssembly().Location + @" ""%1""");
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
            _subversion = Path.Combine(programFiles, @"Subversion\bin\svn.exe");
            if (!File.Exists(_subversion))
            {
                _subversion = Path.Combine(programFiles, @"VisualSVN\bin\svn.exe");
                if (!File.Exists(_subversion))
                    _subversion = null;
            }

            // Find Visual Studio, if available.
            _devenv = Path.Combine(programFiles, @"Microsoft Visual Studio 10.0\Common7\IDE\devenv.exe");
            if (!File.Exists(_devenv))
            {
                _devenv = null;
                StartSln.Enabled = false;
                QualityBuildFirst.Enabled = false;
                QualityCurrentBuild.Checked = true;
            }

            commandShell.StopButton = buttonStop;

            var loader = new BackgroundWorker();
            loader.DoWork += BackgroundLoad;
            loader.RunWorkerAsync();

            InitQuality();
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
                    Invoke(new Action(() =>
                    {
                        var childNodes = new TreeNode[tests.Length];
                        for (int i = 0; i < childNodes.Length; i++)
                            childNodes[i] = new TreeNode(tests[i]);
                        TestsTree.Nodes.Add(new TreeNode(dllName, childNodes));
                    }));
                }

                var tutorialTests = new List<string>();
                foreach (var tutorialDll in TUTORIAL_DLLS)
                    tutorialTests.AddRange(GetTestInfos(tutorialDll));
                var tutorialNodes = new TreeNode[tutorialTests.Count];
                tutorialTests = tutorialTests.OrderBy(test => test).ToList();
                Invoke(new Action(() =>
                {
                    for (int i = 0; i < tutorialNodes.Length; i++)
                    {
                        tutorialNodes[i] = new TreeNode(tutorialTests[i]);
                    }
                    TutorialsTree.Nodes.Add(new TreeNode("Tutorial tests", tutorialNodes));
                    TutorialsTree.ExpandAll();
                    TutorialsTree.Nodes[0].Checked = true;
                    CheckAllChildNodes(TutorialsTree.Nodes[0], true);

                    // Add forms to forms tree view.
                    CreateFormsTree();
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            if (_openFile != null)
            {
                Invoke(new Action(() =>
                {
                    OpenFile(_openFile);
                    if (Path.GetExtension(_openFile) == ".skytr")
                    {
                        Run(Tabs.SelectedTab);
                    }
                }));
            }
        }

        private void Run(TabPage tab)
        {
            if (tab == tabForms)
                RunForms(null, null);
            else if (tab == tabTutorials)
                RunTutorials(null, null);
            else if (tab == tabTest)
                RunTests(null, null);
            else if (tab == tabBuild)
                RunBuild(null, null);
            else if (tab == tabQuality)
                RunQuality(null, null);
        }

        public IEnumerable<string> GetTestInfos(string testDll)
        {
            var dllPath = Path.Combine(_exeDir, testDll);
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

        private bool ToggleRunButtons(TabPage tab)
        {
            // Stop running task.
            if (_runningTab != null || tab == null)
            {
                _runningTab = null;

                foreach (var runButton in _runButtons)
                    runButton.Text = "Run";
                buttonStop.Enabled = false;

                return false;
            }

            // Prepare to start task.
            _runningTab = tab;
            foreach (var runButton in _runButtons)
                runButton.Text = "Stop";
            buttonStop.Enabled = true;

            return true;
        }

        private void StartTestRunner(string args, Action<bool> doneAction = null)
        {
            Tabs.SelectTab(tabOutput);
            MemoryChartWindow.Start("TestRunnerMemory.log");

            var testRunner = Path.Combine(
                _buildDir, 
                string.Format(@"pwiz_tools\Skyline\bin\{0}\Release\TestRunner.exe", Build32.Checked ? "x86" : "x64"));
            if (!File.Exists(testRunner))
                testRunner = Path.Combine(_exeDir, "TestRunner.exe");
            _stopTestRunner = Path.Combine(Path.GetDirectoryName(testRunner) ?? "", "StopTestRunner.txt");
            if (File.Exists(_stopTestRunner))
                File.Delete(_stopTestRunner);
            commandShell.Add("{0} random=off results={1} {2} {3}",
                Quote(testRunner),
                Quote(_resultsDir),
                RunWithDebugger.Checked ? "Debug" : "",
                args);

            commandShell.Run(doneAction ?? TestRunnerDone);
        }

        private string _stopTestRunner;
        private WaitWindow _waitWindow;

        private void StopTestRunner()
        {
            File.WriteAllText(_stopTestRunner, "");
            _waitWindow = new WaitWindow();
            _waitWindow.Show();
        }

        private void TestRunnerDone(bool success)
        {
            if (_waitWindow != null)
            {
                _waitWindow.Close();
                _waitWindow.Dispose();
                _waitWindow = null;
            }

            ToggleRunButtons(null);
        }


        private void GetCheckedTests(TreeNode node, List<string> testList, bool skipTests = false)
        {
            foreach (TreeNode childNode in node.Nodes)
            {
                if (childNode.Checked == !skipTests)
                    testList.Add(childNode.Text);
            }
        }

        // Updates all child tree nodes recursively.
        private void CheckAllChildNodes(TreeNode treeNode, bool nodeChecked)
        {
            foreach (TreeNode node in treeNode.Nodes)
            {
                node.Checked = nodeChecked;
                if (node.Nodes.Count > 0)
                {
                    // If the current node has child nodes, call the CheckAllChildsNodes method recursively.
                    CheckAllChildNodes(node, nodeChecked);
                }
            }
        }

        // NOTE   This code can be added to the BeforeCheck event handler instead of the AfterCheck event.
        // After a tree node's Checked property is changed, all its child nodes are updated to the same value.
        private void node_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // The code only executes if the user caused the checked state to change.
            if (e.Action != TreeViewAction.Unknown)
            {
                if (e.Node.Nodes.Count > 0)
                {
                    /* Calls the CheckAllChildNodes method, passing in the current 
                    Checked value of the TreeNode whose checked state changed. */
                    CheckAllChildNodes(e.Node, e.Node.Checked);
                }
            }
        }

        private void ViewMemoryUse(object sender, EventArgs e)
        {
            MemoryChartWindow.ShowMemoryChart();
        }

        private void open_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog {Filter = "Skyline Tester (*.skyt;*.skytr)|*.skyt;*.skytr"};
            if (openFileDialog.ShowDialog() != DialogResult.Cancel)
                OpenFile(openFileDialog.FileName);
        }

        private void OpenFile(string file)
        {
            var doc = XDocument.Load(file);
            foreach (var element in doc.Descendants())
            {
                var control = Controls.Find(element.Name.ToString(), true).FirstOrDefault();
                if (control == null)
                    continue;

                var tabs = control as TabControl;
                if (tabs != null)
                    tabs.SelectTab(element.Value);

                var button = control as RadioButton;
                if (button != null && element.Value == "true")
                    button.Checked = true;

                var checkBox = control as CheckBox;
                if (checkBox != null)
                    checkBox.Checked = (element.Value == "true");

                var textBox = control as TextBox;
                if (textBox != null)
                    textBox.Text = element.Value;

                var treeView = control as TreeView;
                if (treeView != null)
                    CheckNodes(treeView, element.Value.Split(','));
            }

            if (_devenv == null)
            {
                QualityCurrentBuild.Checked = true;
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

        private static void UncheckNodes(TreeNode node)
        {
            node.Checked = false;
            foreach (TreeNode childNode in node.Nodes)
                UncheckNodes(childNode);
        }

        private static void CheckNodes(TreeNode node, ICollection<string> checkedNames)
        {
            if (checkedNames.Contains(node.Text))
                node.Checked = true;

            foreach (TreeNode childNode in node.Nodes)
                CheckNodes(childNode, checkedNames);
        }

        private void save_Click(object sender, EventArgs e)
        {
            var saveFileDialog = new SaveFileDialog { Filter = "Skyline Tester (*.skyt;*.skytr)|*.skyt;*.skytr" };
            if (saveFileDialog.ShowDialog() == DialogResult.Cancel)
                return;

            var root = CreateElement(
                "SkylineTester",
                Tabs,

                // Forms
                PauseFormDelay,
                PauseFormSeconds,
                PauseFormButton,
                FormsTree,

                // Tutorials
                PauseTutorialsDelay,
                PauseTutorialsSeconds,
                PauseTutorialsScreenShots,
                TutorialsDemoMode,
                TutorialsTree,

                // Tests
                PauseTestsScreenShots,
                Offscreen,
                RunLoops,
                RunLoopsCount,
                RunIndefinitely,
                CultureEnglish,
                CultureFrench,
                TestsTree,
                RunCheckedTests,
                SkipCheckedTests,
                
                // Build
                Build32,
                Build64,
                BuildTrunk,
                BuildBranch,
                BranchUrl,
                BuildClean,
                StartSln,

                // Quality
                QualityStartNow,
                QualityStartLater,
                QualityStartTime,
                QualityEndTime,
                QualityBuildFirst,
                QualityCurrentBuild,
                QualityAllTests,
                QualityChooseTests);

            XDocument doc = new XDocument(root);
            File.WriteAllText(saveFileDialog.FileName, doc.ToString());
        }

        private XElement CreateElement(string name, params object[] childElements)
        {
            var element = new XElement(name);
            foreach (var child in childElements)
            {
                var tabs = child as TabControl;
                if (tabs != null)
                    element.Add(new XElement(tabs.Name, tabs.SelectedTab.Name));

                var button = child as RadioButton;
                if (button != null)
                    element.Add(new XElement(button.Name, button.Checked));

                var checkBox = child as CheckBox;
                if (checkBox != null)
                    element.Add(new XElement(checkBox.Name, checkBox.Checked));

                var textBox = child as TextBox;
                if (textBox != null)
                    element.Add(new XElement(textBox.Name, textBox.Text));

                var treeView = child as TreeView;
                if (treeView != null)
                    element.Add(new XElement(treeView.Name, GetCheckedNodes(treeView)));
            }

            return element;
        }

        private string GetCheckedNodes(TreeView treeView)
        {
            var names = new StringBuilder();
            foreach (TreeNode childNode in treeView.Nodes)
                GetCheckedNodes(childNode, names);
            return names.Length > 0 ? names.ToString(1, names.Length - 1) : string.Empty;
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

        private void exit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void TabChanged(object sender, EventArgs e)
        {
            if (Tabs.SelectedTab == tabQuality)
                OpenQuality();
            else if (Tabs.SelectedTab == tabOutput)
                OpenOutput();
        }

        private void FormsTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            e.Node.Checked = !e.Node.Checked;
            FormsTree.SelectedNode = null;
        }
    }
}
