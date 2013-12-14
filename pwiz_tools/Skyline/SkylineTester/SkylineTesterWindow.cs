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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
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

        private Process _process;
        private List<string> _formsTestList;
        private readonly string _resultsDir;
        private string _buildDir;
        private readonly string _logFile;
        private bool _appendToLog;
        private TabPage _runningTab;
        private string _subversion;
        private readonly string _exeDir;
        private readonly string _rootDir;
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
            _resultsDir = Path.Combine(_rootDir, "SkylineTester Results");
            _logFile = Path.Combine(_rootDir, "SkylineTester.log");
            if (args.Length > 0)
                _openFile = args[0];
        }

        private void SkylineTesterWindow_Load(object sender, EventArgs e)
        {
            if (!Program.IsRunning)
                return; // design mode

            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\SkylineTester\shell\open\command", null, 
                Assembly.GetExecutingAssembly().Location + @" ""%1""");
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\.skyt", null, "SkylineTester");
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\.skytr", null, "SkylineTester");
            
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

            linkLogFile.Text = _logFile;

            var loader = new BackgroundWorker();
            loader.DoWork += BackgroundLoad;
            loader.RunWorkerAsync();

            textBoxLog.HScroll += TextBoxLogOnScroll;
            textBoxLog.VScroll += TextBoxLogOnScroll;
        }

        private bool _logWasScrolled;
        private void TextBoxLogOnScroll(object sender, EventArgs eventArgs)
        {
            _logWasScrolled = true;
        }

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
                        if (Tabs.SelectedTab == tabForms)
                            RunForms(null, null);
                        else if (Tabs.SelectedTab == tabTutorials)
                            RunTutorials(null, null);
                        else if (Tabs.SelectedTab == tabTest)
                            RunTests(null, null);
                        else if (Tabs.SelectedTab == tabBuild)
                            RunBuild(null, null);
                        else if (Tabs.SelectedTab == tabQuality)
                            RunQuality(null, null);
                    }
                }));
            }
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
            KillTestProcess();
            base.OnClosed(e);
        }

        private void KillTestProcess()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    ProcessUtilities.KillProcessTree(_process); 
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }

            _process = null;
        }

        private bool ToggleRunButtons(TabPage tab)
        {
            // Stop running task.
            if (_runningTab != null || tab == null)
            {
                _runningTab = null;

                foreach (var runButton in _runButtons)
                    runButton.Text = "Run";
                buttonStopLog.Enabled = false;

                if (tab != null)
                {
                    KillTestProcess();
                    LogComment("# Stopped.");
                }

                FinishLog();
                return false;
            }

            // Prepare to start task.
            _runningTab = tab;
            foreach (var runButton in _runButtons)
                runButton.Text = "Stop";
            buttonStopLog.Enabled = true;

            // Clear log.
            textBoxLog.Text = null;
            if (File.Exists(_logFile))
                File.Delete(_logFile);

            return true;
        }

        private void RunTestRunner(string args)
        {
            Tabs.SelectTab(tabOutput);
            MemoryChartWindow.Start("TestRunnerMemory.log");
            _appendToLog = false;

            var testRunnerArgs = new StringBuilder("random=off results=\"");
            testRunnerArgs.Append(_resultsDir);
            testRunnerArgs.Append("\" ");
            testRunnerArgs.Append("log=\"");
            testRunnerArgs.Append(_logFile);
            testRunnerArgs.Append("\" ");
            if (RunWithDebugger.Checked)
                testRunnerArgs.Append("Debug ");
            testRunnerArgs.Append(args);

            StartProcess(
                "TestRunner.exe",
                testRunnerArgs.ToString(),
                _exeDir);
        }

        private readonly StringBuilder _logBuffer = new StringBuilder();

        private void ProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (_runningTab != null)
                AddLine(e.Data);
        }

        private void AddLine(string line)
        {
            lock (_logBuffer)
            {
                _logBuffer.Append(line);
                _logBuffer.Append(Environment.NewLine);
            }
        }

        private void UpdateLog(object sender, EventArgs eventArgs)
        {
            if (_logWasScrolled)
            {
                _logWasScrolled = false;
                return;
            }

            string logLines;
            lock (_logBuffer)
            {
                if (_logBuffer.Length == 0)
                    return;
                logLines = _logBuffer.ToString();
                _logBuffer.Clear();
            }

            Log(logLines);
        }

        private void Log(string text)
        {
            if (_appendToLog)
                File.AppendAllText(_logFile, text);

            // Scroll if text box is already scrolled to bottom.
            int previousLength = textBoxLog.TextLength;
            var point = textBoxLog.GetPositionFromCharIndex(previousLength-1);
            bool autoScroll = (textBoxLog.ClientRectangle.Bottom >= point.Y);

            buttonStopLog.Focus(); // text box scrolls if it has focus!
            textBoxLog.AppendText(text);

            if (autoScroll)
            {
                textBoxLog.Select(textBoxLog.Text.Length - 1, 0);
                textBoxLog.ScrollToCaret();
            }

            textBoxLog.DoPaint = false;
            ColorText(previousLength, "\n#", Color.DarkGreen);
            if (_runningTab == tabBuild)
            {
                ColorText(previousLength, "\n...skipped", Color.Orange);
                ColorText(previousLength, "\n...failed", Color.Red);
            }
            textBoxLog.DoPaint = true;
        }

        private void ColorText(int previousLength, string text, Color color)
        {
            while (true)
            {
                int startIndex = textBoxLog.Text.IndexOf(text, previousLength, StringComparison.InvariantCulture);
                if (startIndex < 0)
                    return;
                int endIndex = textBoxLog.Text.IndexOf('\n', startIndex + 1);
                if (endIndex < 0)
                    return;
                textBoxLog.Select(startIndex, endIndex - startIndex);
                textBoxLog.SelectionColor = color;
                previousLength = endIndex;
            }
        }

        private void LogComment(string text)
        {
            AddLine(Environment.NewLine + text);
        }

        private Process CreateProcess(string fileName, string workingDirectory = null)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true
            };

            if (!RunWithDebugger.Checked)
            {
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
            }

            if (workingDirectory != null)
                process.StartInfo.WorkingDirectory = workingDirectory;

            return process;
        }

        private void StartProcess(
            string exe, 
            string arguments, 
            string workingDirectory,
            EventHandler processExitHandler = null,
            bool appendToLog = false)
        {
            _process = CreateProcess(exe, workingDirectory);
            _process.StartInfo.Arguments = arguments;
            if (!RunWithDebugger.Checked)
            {
                _process.OutputDataReceived += ProcessOutputDataReceived;
                _process.ErrorDataReceived += ProcessOutputDataReceived;
            }
            _process.Exited += processExitHandler ?? ProcessExit;
            _process.Start();
            if (!RunWithDebugger.Checked)
            {
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }

            _outputTimer = new Timer {Interval = 500};
            _outputTimer.Tick += UpdateLog;
            _outputTimer.Start();
        }

        private Timer _outputTimer;

        void ProcessExit(object sender, EventArgs e)
        {
            if (_runningTab == null)
                return;

            try
            {
                Invoke(new Action(() =>
                {
                    if (_runningTab == tabForms)
                        ExitForms();
                    ToggleRunButtons(null);
                    FinishLog();
                    _process = null;
                }));
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }

        private void FinishLog()
        {
            if (_outputTimer != null)
            {
                _outputTimer.Stop();
                _outputTimer = null;
            }

            // Show final log output.
            _logWasScrolled = false;
            UpdateLog(null, null);
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
    }
}
