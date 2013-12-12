using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using TestRunnerLib;

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
 
        public SkylineTesterWindow()
        {
            InitializeComponent();
        }

        public SkylineTesterWindow(string[] args)
        {
            InitializeComponent();

            _resultsDir = (args.Length > 0)
                ? args[0]
                : Path.Combine(Environment.CurrentDirectory, "SkylineTester Results");
            _logFile = Path.GetDirectoryName(Environment.CurrentDirectory) ?? "";
            _logFile = Path.Combine(_logFile, "SkylineTester.log");
        }

        private void SkylineTesterWindow_Load(object sender, EventArgs e)
        {
            if (!Program.IsRunning)
                return; // design mode

            _runButtons = new[]
            {
                runForms, runTutorials, runTests, runBuild, runQuality
            };

            linkLogFile.Text = _logFile;

            var loader = new BackgroundWorker();
            loader.DoWork += BackgroundLoad;
            loader.RunWorkerAsync();
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
        }

        public static IEnumerable<string> GetTestInfos(string testDll)
        {
            var assembly = Assembly.LoadFrom(testDll);
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

        protected override void OnClosed(EventArgs e)
        {
            KillTestProcess();
            base.OnClosed(e);
        }

        private void KillTestProcess()
        {
            if (_process != null)
            {
                ProcessUtilities.KillProcessTree(_process); 
                _process = null;

                var stopTimer = new Timer {Interval = 500};
                stopTimer.Tick += (sender, args) =>
                {
                    stopTimer.Stop();
                    textBoxLog.AppendText("#  Stopped.\r\n");
                };
                stopTimer.Start();
            }
        }

        private void CreateFormsTree()
        {
            FormsTree.Nodes.Clear();

            var forms = new List<TreeNode>();
            var skylinePath = File.Exists("Skyline.exe") ? "Skyline.exe" : "Skyline-daily.exe";
            var assembly = Assembly.LoadFrom(skylinePath);
            var types = assembly.GetTypes();
            var formLookup = new FormLookup();

            foreach (var type in types)
            {
                if (type.IsSubclassOf(typeof (Form)) && !type.IsAbstract)
                {
                    var node = new TreeNode(type.Name);
                    if (!formLookup.HasTest(type.Name))
                        node.ForeColor = Color.Gray;
                    forms.Add(node);
                }
            }

            forms = forms.OrderBy(node => node.Text).ToList();
            FormsTree.Nodes.Add(new TreeNode("Skyline", forms.ToArray()));
            FormsTree.ExpandAll();
        }

        private void runClick(object sender, EventArgs e)
        {
            foreach (var runButton in _runButtons)
                runButton.Text = (_process == null) ? "Stop" : "Run";

            if (_process != null)
            {
                KillTestProcess();

                if (RegenerateCache.Checked)
                    CreateFormsTree();

                RegenerateCache.Checked = false;
                buttonStopLog.Enabled = false;
                return;
            }

            buttonStopLog.Enabled = true;
            if (File.Exists(_logFile))
                File.Delete(_logFile);
            _appendToLog = false;

            _process = CreateProcess("TestRunner.exe");
            _buildDir = null;

            var args = new StringBuilder("SkylineTester random=off results=\"");
            args.Append(_resultsDir);
            args.Append("\" ");
            args.Append("log=\"");
            args.Append(_logFile);
            args.Append("\" ");
            if (RunWithDebugger.Checked)
                args.Append("Debug ");

            switch (Tabs.SelectedIndex)
            {
                case 0:
                    GetArgsForForms(args);
                    break;

                case 1:
                    GetArgsForTutorials(args);
                    break;

                case 2:
                    GetArgsForTests(args);
                    break;

                case 3:
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    _process.StartInfo.FileName = Path.Combine(programFiles, @"Subversion\bin\svn.exe");
                    if (!File.Exists(_process.StartInfo.FileName))
                    {
                        _process.StartInfo.FileName = Path.Combine(programFiles, @"VisualSVN\bin\svn.exe");
                        if (!File.Exists(_process.StartInfo.FileName))
                        {
                            // TODO: Offer to install for the user.
                            MessageBox.Show("Must install Subversion");
                            _process = null;
                            foreach (var runButton in _runButtons)
                                runButton.Text = "Run";
                            buttonStopLog.Enabled = false;
                            return;
                        }
                    }
                    args.Clear();
                    args.Append(@"checkout https://svn.code.sf.net/p/proteowizard/code/trunk/pwiz ");
                    _buildDir = Environment.CurrentDirectory;
                    _buildDir = Path.GetDirectoryName(_buildDir) ?? "";
                    _buildDir = Path.Combine(_buildDir, "Build");
                    args.Append(_buildDir);
                    _appendToLog = true;
                    break;

                case 4:
                    break;
            }

            MemoryChartWindow.Start("TestRunnerMemory.log");

            textBoxLog.Text = null;
            StartProcess(args.ToString());

            Tabs.SelectTab(tabOutput);
        }

        private const int SB_VERT = 0x1;
        private const int WM_VSCROLL = 0x115;
        private const int SB_THUMBPOSITION = 0x4;
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetScrollPos(IntPtr hWnd, int nBar);
        [DllImport("user32.dll")]
        private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);
        [DllImport("user32.dll")]
        private static extern bool PostMessageA(IntPtr hWnd, int nBar, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool GetScrollRange(IntPtr hWnd, int nBar, out int lpMinPos, out int lpMaxPos);
        private void AppendTextToTextBox(MyTextBox textbox, string text, bool autoscroll)
        {
            textbox.Suspend();
            int savedVpos = GetScrollPos(textbox.Handle, SB_VERT);
            textbox.AppendText(text);
            if (autoscroll)
            {
                int VSmin, VSmax;
                GetScrollRange(textbox.Handle, SB_VERT, out VSmin, out VSmax);
                int sbOffset = (textbox.ClientSize.Height - SystemInformation.HorizontalScrollBarHeight) / (textbox.Font.Height);
                savedVpos = VSmax - sbOffset;
            }
            SetScrollPos(textbox.Handle, SB_VERT, savedVpos, true);
            PostMessageA(textbox.Handle, WM_VSCROLL, SB_THUMBPOSITION + 0x10000 * savedVpos, 0);
            textbox.Resume();
        }

        void ProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                var line = e.Data + Environment.NewLine;
                if (_appendToLog)
                    File.AppendAllText(_logFile, line);
                Invoke(new Action(() =>
                {
                    textBoxLog.Focus();
                    AppendTextToTextBox(textBoxLog, line, false);
                }));
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
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

        private void StartProcess(string arguments)
        {
            _process.StartInfo.Arguments = arguments;
            if (!RunWithDebugger.Checked)
            {
                _process.OutputDataReceived += ProcessOutputDataReceived;
                _process.ErrorDataReceived += ProcessOutputDataReceived;
            }
            _process.Exited += ProcessExit;
            _process.Start();
            if (!RunWithDebugger.Checked)
            {
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
        }

        void ProcessExit(object sender, EventArgs e)
        {
            if (_buildDir != null)
            {
                _process = CreateProcess(
                    Path.Combine(_buildDir, @"pwiz_tools\build-apps.bat"),
                    _buildDir);
                _buildDir = null;
                StartProcess(@"32 --i-agree-to-the-vendor-licenses toolset=msvc-10.0 nolog");
                return;
            }

            try
            {
                Invoke(new Action(() =>
                {
                    foreach (var runButton in _runButtons)
                        runButton.Text = "Run";
                    buttonStopLog.Enabled = false;
                    _process = null;
                }));
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }

        private void GetArgsForForms(StringBuilder args)
        {
            if (RegenerateCache.Checked)
            {
                args.Append("loop=1 offscreen=off culture=en-US form=__REGEN__");
                return;
            }

            // Create list of forms the user wants to see.
            var formList = new List<string>();
            var skylineNode = FormsTree.Nodes[0];
            foreach (TreeNode node in skylineNode.Nodes)
            {
                if (node.Checked)
                    formList.Add(node.Text);
            }
            args.Append("loop=1 offscreen=off culture=en-US form=");
            args.Append(string.Join(",", formList));
            int pauseSeconds = -1;
            if (PauseFormDelay.Checked && !int.TryParse(PauseFormSeconds.Text, out pauseSeconds))
                pauseSeconds = 0;
            args.Append(" pause=");
            args.Append(pauseSeconds);
        }

        private void GetArgsForTutorials(StringBuilder args)
        {
            var testList = new List<string>();
            GetCheckedTests(TutorialsTree.TopNode, testList);

            args.Append("offscreen=off loop=1 culture=en-US");
            if (TutorialsDemoMode.Checked)
                args.Append(" demo=on");
            else
            {
                int pauseSeconds = -1;
                if (!PauseTutorialsScreenShots.Checked && !int.TryParse(PauseTutorialsSeconds.Text, out pauseSeconds))
                    pauseSeconds = 0;
                args.Append(" pause=");
                args.Append(pauseSeconds);
            }
            args.Append(" test=");
            args.Append(string.Join(",", testList));
        }

        private void GetArgsForTests(StringBuilder args)
        {
            var testList = new List<string>();

            foreach (TreeNode node in TestsTree.Nodes)
                GetCheckedTests(node, testList, SkipCheckedTests.Checked);

            args.Append("offscreen=");
            args.Append(Offscreen.Checked);

            if (!RunIndefinitely.Checked)
            {
                int loop;
                if (!int.TryParse(RunLoopsCount.Text, out loop))
                    loop = 1;
                args.Append(" loop=");
                args.Append(loop);
            }

            var cultures = new List<CultureInfo>();
            if (CultureEnglish.Checked || !CultureFrench.Checked)
                cultures.Add(new CultureInfo("en-US"));
            if (CultureFrench.Checked)
                cultures.Add(new CultureInfo("fr-FR"));

            args.Append(" culture=");
            args.Append(string.Join(",", cultures));
            if (PauseTestsScreenShots.Checked)
                args.Append(" pause=-1");
            args.Append(" test=");
            args.Append(string.Join(",", testList));
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

        private void checkAll_Click(object sender, EventArgs e)
        {
            foreach (var node in TestsTree.Nodes)
            {
                ((TreeNode) node).Checked = true;
                CheckAllChildNodes((TreeNode) node, true);
            }
        }

        private void uncheckAll_Click(object sender, EventArgs e)
        {
            foreach (var node in TestsTree.Nodes)
            {
                ((TreeNode)node).Checked = false;
                CheckAllChildNodes((TreeNode)node, false);
            }
        }

        private void ViewMemoryUse(object sender, EventArgs e)
        {
            MemoryChartWindow.ShowMemoryChart();
        }

        private void pauseTestsForScreenShots_CheckedChanged(object sender, EventArgs e)
        {
            if (PauseTestsScreenShots.Checked)
                Offscreen.Checked = false;
        }

        private void offscreen_CheckedChanged(object sender, EventArgs e)
        {
            if (Offscreen.Checked)
                PauseTestsScreenShots.Checked = false;
        }

        private void open_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Skyline Tester (*.skyt)|*.skyt" };
            if (openFileDialog.ShowDialog() == DialogResult.Cancel)
                return;

            var doc = XDocument.Load(openFileDialog.FileName);
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
            var saveFileDialog = new SaveFileDialog {Filter = "Skyline Tester (*.skyt)|*.skyt"};
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
                SkipCheckedTests);

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

        private void FormsTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            e.Node.Checked = !e.Node.Checked;
            FormsTree.SelectedNode = null;
        }

        private void linkLogFile_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (!File.Exists(_logFile))
                return;

            var editLogFile = new Process
            {
                StartInfo =
                {
                    FileName = _logFile
                }
            };
            editLogFile.Start();
        }
    }
}
