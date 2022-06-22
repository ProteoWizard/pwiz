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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using SkylineTester.Properties;
using TestRunnerLib;
using ZedGraph;
using Label = System.Windows.Forms.Label;
using Timer = System.Windows.Forms.Timer;

namespace SkylineTester
{
    public partial class SkylineTesterWindow : Form
    {
        #region Fields

        public const string SummaryLog = "Summary.log";
        public const string SkylineTesterFiles = "SkylineTester Files";

        public const string DocumentationLink =
            "https://skyline.gs.washington.edu/labkey/wiki/home/development/page.view?name=SkylineTesterDoc";

        public string Git { get; private set; }
        public string Devenv { get; private set; }
        public string RootDir { get; private set; }
        public string Exe { get; private set; }
        public string ExeDir { get; private set; }
        public string DefaultLogFile { get; private set; }
        public string NightlyLogFile { get; set; }
        public Summary Summary { get; set; }
        public Summary.Run NewNightlyRun { get; set; }
        public int TestsRun { get; set; }
        public string LastTestResult { get; set; }
        public string LastRunName { get; set; }
        public string RunningTestName { get; private set; }
        public int LastTabIndex { get; private set; }
        public int NightlyTabIndex { get; private set; }
        public BuildDirs SelectedBuild { get; private set; }
        public bool ShiftKeyPressed { get; private set; }

        private Button _defaultButton;
        private bool _restart;

        public Button DefaultButton
        {
            get { return _defaultButton; }
            set
            {
                _defaultButton = value;
                if (_runningTab == null)
                    AcceptButton = _defaultButton;
            }
        }

        private readonly Dictionary<string, string> _languageNames = new Dictionary<string, string>
        {
            {"en", "English"},
            {"fr", "French"},
            {"tr", "Turkish"},
            {"ja", "Japanese"},
            {"zh-CHS", "Chinese"}
        };

        private static readonly string[] TEST_DLLS =
        {
            "Test.dll", "TestData.dll", "TestFunctional.dll", "TestTutorial.dll",
            "CommonTest.dll", "TestConnected.dll", "TestPerf.dll"
        };

        private static readonly string[] FORMS_DLLS = {"TestFunctional.dll", "TestTutorial.dll"};
        private static readonly string[] TUTORIAL_DLLS = {"TestTutorial.dll", "TestPerf.dll"};

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
        private TabNightly _tabNightly;
        private TabOutput _tabOutput;
        private TabRunStats _tabRunStats;
        private TabBase[] _tabs;

        private int _findPosition;
        private string _findText;

        private ZedGraphControl graphMemory;

        private ZedGraphControl nightlyGraphMemory;
        private ZedGraphControl graphMemoryHistory;
        private ZedGraphControl graphFailures;
        private ZedGraphControl graphDuration;
        private ZedGraphControl graphTestsRun;

        #endregion Fields

        #region Create and load window

        public SkylineTesterWindow()
        {
            InitializeComponent();

            // Get placement values before changing anything.
            Point location = Settings.Default.WindowLocation;
            Size size = Settings.Default.WindowSize;
            bool maximize = Settings.Default.WindowMaximized;

            // Restore window placement.
            if (!location.IsEmpty)
            {
                StartPosition = FormStartPosition.Manual;
                Location = location;
            }
            if (!size.IsEmpty)
                Size = size;
            if (maximize)
                WindowState = FormWindowState.Maximized;
        }

        public SkylineTesterWindow(string[] args)
            : this()
        {
            // Grab some critical config values to avoid some timing issues in the initialization process
            string settings = args.Length > 0 ? File.ReadAllText(args[0]) : Settings.Default.SavedSettings;
            if (!string.IsNullOrEmpty(settings))
            {
                var xml = new XmlDocument();
                xml.LoadXml(settings);
                var elementNightlyRoot = xml.SelectSingleNode("/SkylineTester/nightlyRoot");
                if (elementNightlyRoot != null)
                    nightlyRoot.Text = elementNightlyRoot.InnerText;
                var elementBuildRoot = xml.SelectSingleNode("/SkylineTester/buildRoot");
                if (elementBuildRoot != null)
                    buildRoot.Text = elementBuildRoot.InnerText;
            }

            Exe = Assembly.GetExecutingAssembly().Location;
            ExeDir = Path.GetDirectoryName(Exe);
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

            runMode.SelectedIndex = 0;

            InitLanguages(formsLanguage);
            InitLanguages(tutorialsLanguage);
            EnableButtonSelectFailedTests(false); // No tests run yet

            if (args.Length > 0)
                _openFile = args[0];
            else if (!string.IsNullOrEmpty(Settings.Default.SavedSettings))
                LoadSettingsFromString(Settings.Default.SavedSettings);
        }

        private void SkylineTesterWindow_Load(object sender, EventArgs e)
        {
            if (!Program.IsRunning)
                return; // design mode

            // Register file/exe/icon associations.
            var checkRegistry = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Classes\SkylineTester\shell\open\command", null, null);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\SkylineTester\shell\open\command", null,
                Assembly.GetExecutingAssembly().Location.Quote() + @" ""%1""");
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\.skyt", null, "SkylineTester");
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\.skytr", null, "SkylineTester");

            // Refresh shell if association changed.
            if (checkRegistry == null)
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            _runButtons = new[]
            {
                runForms, runTutorials, runTests, runBuild, runQuality, runNightly
            };

            GetBuildPrerequisites();
            FindBuilds();

            commandShell.StopButton = buttonStop;
            commandShell.AddColorPattern("# ", Color.DarkGreen);
            commandShell.AddColorPattern("> ", Color.FromArgb(120, 120, 120));
            commandShell.AddColorPattern("...skipped ", Color.Orange);
            commandShell.AddColorPattern("...failed ", Color.Red);
            commandShell.AddColorPattern("!!!", Color.Red);
            commandShell.AddColorPatternEx("   at ", ":line ", Color.Blue);

            commandShell.ColorLine = line =>
            {
                if (line.StartsWith("...skipped ") ||
                    line.StartsWith("...failed ") ||
                    line.StartsWith("!!! "))
                {
                    _tabOutput.ProcessError(line);
                }
            };

            commandShell.FilterFunc = line =>
            {
                if (line == null)
                    return false;

                if (line.StartsWith("[MLRAW:"))
                    return false;

                // Filter out false error from Waters DLL (it's looking .ind, ultimately finds and uses .idx)
                if (line.StartsWith("Error opening index file") && line.EndsWith(".ind"))
                    return false;

                if (line.StartsWith("#@ "))
                {
                    // Update status.
                    RunUI(() =>
                    {
                        RunningTestName = line.Remove(0, "#@ Running ".Length).TrimEnd('.');
                        statusLabel.Text = line.Substring(3);
                    });
                    return false;
                }

                if (line.StartsWith("...skipped ") ||
                    line.StartsWith("...failed ") ||
                    line.StartsWith("!!! "))
                {
                    RunUI(() => _tabOutput.ProcessError(line));
                }

                if (NewNightlyRun != null)
                {
                    if (line.StartsWith("!!! "))
                    {
                        var parts = line.Split(' ');
                        if (parts[2] == "LEAKED" || parts[2] == "CRT-LEAKED" || parts[2] == "HANDLE-LEAKED")
                            NewNightlyRun.Leaks++;
                    }
                    else if (line.Length > 6 && line[0] == '[' && line[6] == ']' && line.Contains(" failures, "))
                    {
                        lock (NewNightlyRun)
                        {
                            LastTestResult = line;
                            TestsRun++;
                        }
                    }
                }

                return true;
            };

            if (_openFile != null)
                LoadSettingsFromFile(_openFile);

            TabBase.MainWindow = this;
            _tabForms = new TabForms();
            _tabTutorials = new TabTutorials();
            _tabTests = new TabTests();
            _tabBuild = new TabBuild();
            _tabQuality = new TabQuality();
            _tabNightly = new TabNightly();
            _tabOutput = new TabOutput();
            _tabRunStats = new TabRunStats();

            _tabs = new TabBase[]
            {
                _tabForms,
                _tabTutorials,
                _tabTests,
                _tabBuild,
                _tabQuality,
                _tabNightly,
                _tabOutput,
                _tabRunStats
            };
            NightlyTabIndex = Array.IndexOf(_tabs, _tabNightly);
            // Make sure NightlyExit is checked for IsNightlyRun() which makes testings
            // easier by just making this function return true. The nightly tests usually
            // set this in the .skytr file.
            NightlyExit.Checked = NightlyExit.Checked || IsNightlyRun();

            InitQuality();
            _previousTab = tabs.SelectedIndex;
            _tabs[_previousTab].Enter();
            statusLabel.Text = "";

            var loader = new BackgroundWorker();
            loader.DoWork += BackgroundLoad;
            loader.RunWorkerAsync();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private void BackgroundLoad(object sender, DoWorkEventArgs e)
        {
            try
            {
                var skylineNode = new TreeNode("Skyline tests");

                // Load all tests from each dll.
                var arrayDllNames = showTutorialsOnly.Checked ? TUTORIAL_DLLS : TEST_DLLS;
                foreach (var testDll in arrayDllNames)
                {
                    var tests = GetTestInfos(testDll).OrderBy(test => test).ToArray();

                    // Add tests to test tree view.
                    var dllName = testDll.Replace(".dll", "");
                    var childNodes = new List<TreeNode>(tests.Length);
                    foreach (var test in tests)
                    {
                        if (!showTutorialsOnly.Checked || test.EndsWith("Tutorial"))
                            childNodes.Add(new TreeNode(test));
                }
                    skylineNode.Nodes.Add(new TreeNode(dllName, childNodes.ToArray()));
                }

                bool tutorialsLoaded = false;

                RunUI(() =>
                {
                    testsTree.Nodes.Clear();
                    testsTree.Nodes.Add(skylineNode);
                    skylineNode.Expand();

                    tutorialsLoaded = tutorialsTree.Nodes.Count > 0;

//                    var focusNode = new TreeNode("Focus tests");
//                    focusNode.Nodes.Add(new TreeNode("Mzml speed", new []{new TreeNode("x")}));
//                    focusNode.Nodes.Add(new TreeNode("Gene name", new []{new TreeNode("y")}));
//                    testsTree.Nodes.Add(focusNode);
//                    focusNode.Expand();
                });

                if (tutorialsLoaded)
                    return;

                var tutorialTests = new List<string>();
                foreach (var tutorialDll in TUTORIAL_DLLS)
                    tutorialTests.AddRange(GetTestInfos(tutorialDll, "NoLocalizationAttribute", "Tutorial"));
                foreach (var test in tutorialTests.ToArray())
                {
                    // Remove any tutorial tests we've hacked for extra testing (extending test name not to end with Tutorial) - not of interest to localizers
                    if (!test.EndsWith("Tutorial"))
                        tutorialTests.Remove(test);
                }
                var tutorialNodes = new TreeNode[tutorialTests.Count];
                tutorialTests = tutorialTests.OrderBy(test => test).ToList();
                RunUI(() =>
                {
                    for (int i = 0; i < tutorialNodes.Length; i++)
                    {
                        tutorialNodes[i] = new TreeNode(tutorialTests[i]);
                    }
                    tutorialsTree.Nodes.Clear();
                    tutorialsTree.Nodes.Add(new TreeNode("Tutorial tests", tutorialNodes));
                    tutorialsTree.ExpandAll();
                    tutorialsTree.Nodes[0].Checked = true;
                    TabTests.CheckAllChildNodes(tutorialsTree.Nodes[0], true);

                    // Add forms to forms tree view.
                    _tabForms.CreateFormsGrid();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            if (_openFile != null && Path.GetExtension(_openFile) == ".skytr")
            {
                RunUI(Run);
            }
        }

        public static bool Implements(Type type, string interfaceName)
        {
            return type.GetInterfaces().Any(t => t.Name == interfaceName);
        }

        public IEnumerable<string> GetTestInfos(string testDll, string filterAttribute = null, string filterName = null)
        {
            var dllPath = Path.Combine(ExeDir, testDll);
            var assembly = LoadFromAssembly.Try(dllPath);
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                if (type.IsClass && HasAttribute(type, "TestClassAttribute"))
                {
                    var methods = type.GetMethods();
                    foreach (var method in methods)
                    {
                        if (HasAttribute(method, "TestMethodAttribute") && 
                            (filterAttribute == null || !HasAttribute(method, filterAttribute)) &&
                            (filterName == null || method.Name.Contains(filterName)))
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

        private static bool IsNightlyRun()
        {
            // Uncomment for testing
//            return true;

            bool isNightly;
            try
            {
                // From http://stackoverflow.com/questions/2531837/how-can-i-get-the-pid-of-the-parent-process-of-my-application
                var myId = Process.GetCurrentProcess().Id;
                var query = string.Format("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {0}", myId);
                var search = new ManagementObjectSearcher("root\\CIMV2", query);
                var results = search.Get().GetEnumerator();
                results.MoveNext();
                var queryObj = results.Current;
                var parentId = (uint) queryObj["ParentProcessId"];
                var parent = Process.GetProcessById((int) parentId);
                // Only go interactive if our parent process is not named "SkylineNightly"
                isNightly = CultureInfo.InvariantCulture.CompareInfo.IndexOf(parent.ProcessName, "SkylineNightly", CompareOptions.IgnoreCase) >= 0;
            }
            catch
            {
                isNightly = false;
            }
            return isNightly;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // If child process is attached to debugger, don't shut down without asking
            if (commandShell.IsDebuggerAttached)
            {
                var message = string.Format("The currently running test is attached to a debugger.  Are you sure you want to close {0}?", Text);
                if (MessageBox.Show(message, Text, MessageBoxButtons.OKCancel) != DialogResult.OK)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // If there are tests running, check with user before actually shutting down.
            if (_runningTab != null && !ShiftKeyPressed)
            {
                // Skip that check if we closed programatically.
                var isNightly = IsNightlyRun();

                var message = isNightly
                    ? string.Format("The currently running tests are part of a SkylineNightly run. Are you sure you want to end all tests and close {0}?  No report will be sent to the server if you do.", Text)
                    : string.Format("Tests are running. Are you sure you want to end all tests and close {0}?", Text);
                if (MessageBox.Show(message, Text, MessageBoxButtons.OKCancel) != DialogResult.OK)
                {
                    e.Cancel = true;
                    return;
                }
                if (isNightly)
                    Program.UserKilledTestRun = true;
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _runningTab = null;
            var preserveHungProcesses = IsNightlyRun();
            commandShell.Stop(preserveHungProcesses);
            Settings.Default.SavedSettings = SaveSettings();
            Settings.Default.Save();
            base.OnClosed(e);
        }

        private int _previousTab;

        private void TabChanged(object sender, EventArgs e)
        {
            if (_tabs == null)
                return;

            _tabs[_previousTab].Leave();
            LastTabIndex = _previousTab;
            _previousTab = tabs.SelectedIndex;
            _findPosition = 0;

            RunUI(() => _tabs[_previousTab].Enter(), 500);
        }

        public void ShowOutput()
        {
            tabs.SelectTab(tabOutput);
        }

        public void SetStatus(string status = null)
        {
            statusLabel.Text = status;
        }

        private bool _buildDebug;

        public enum BuildDirs
        {
            bin32,
            bin64,
            build32,
            build64,
            nightly32,
            nightly64,
            zip32,
            zip64
        }

        private string[] GetPossibleBuildDirs()
        {
            var dirs = new[]
            {
                Path.GetFullPath(Path.Combine(ExeDir, @"..\..\x86\Release")),
                Path.GetFullPath(Path.Combine(ExeDir, @"..\..\x64\Release")),
                Path.Combine(GetBuildRoot(), @"pwiz\pwiz_tools\Skyline\bin\x86\Release"),
                Path.Combine(GetBuildRoot(), @"pwiz\pwiz_tools\Skyline\bin\x64\Release"),
                Path.Combine(GetNightlyBuildRoot(), @"pwiz\pwiz_tools\Skyline\bin\x86\Release"),
                Path.Combine(GetNightlyBuildRoot(), @"pwiz\pwiz_tools\Skyline\bin\x64\Release"),
                GetZipPath(32),
                GetZipPath(64),
            };
            if (_buildDebug)
                dirs = dirs.Select(dir => dir.Replace(@"\Release", @"\Debug")).ToArray();
            return dirs;
        }

        public void FindBuilds()
        {
            var buildDirs = GetPossibleBuildDirs();

            // Determine which builds exist.
            CheckBuildDirExistence(buildDirs);
            if (buildDirs.All(dir => dir == null))
            {
                _buildDebug = true;
                buildDirs = GetPossibleBuildDirs();
                CheckBuildDirExistence(buildDirs);
                _buildDebug = buildDirs.Any(dir => dir != null);
            }

            // Hide builds that don't exist.
            int defaultIndex = int.MaxValue;
            for (int i = 0; i < buildDirs.Length; i++)
            {
                var item = (ToolStripMenuItem) selectBuildMenuItem.DropDownItems[i];
                if (buildDirs[i] == null)
                    item.Visible = false;
                else
                {
                    item.Visible = true;
                    defaultIndex = Math.Min(defaultIndex, i);
                }
            }

            // Select first available build if previously selected build doesn't exist.
            SelectBuild(buildDirs[(int) SelectedBuild] != null ? SelectedBuild : (BuildDirs) defaultIndex);
        }

        private static void CheckBuildDirExistence(string[] buildDirs)
        {
            for (int i = 0; i < buildDirs.Length; i++)
            {
                if (!File.Exists(Path.Combine(buildDirs[i], "Skyline.exe")) &&
                    !File.Exists(Path.Combine(buildDirs[i], "Skyline-daily.exe")))  // Keep -daily
                {
                    buildDirs[i] = null;
                }
            }
        }

        public void SelectBuild(BuildDirs select)
        {
            SelectedBuild = select;

            // Clear all checks.
            foreach (var buildDirType in (BuildDirs[]) Enum.GetValues(typeof (BuildDirs)))
            {
                if ((int)buildDirType < selectBuildMenuItem.DropDownItems.Count)
                {
                    var item = (ToolStripMenuItem) selectBuildMenuItem.DropDownItems[(int) buildDirType];
                    item.Checked = false;
                }
            }

            // Check the selected build.
            if ((int)select >= 0 && (int)select < selectBuildMenuItem.DropDownItems.Count)
            {
                var selectedItem = (ToolStripMenuItem) selectBuildMenuItem.DropDownItems[(int) select];
                selectedItem.Visible = true;
                selectedItem.Checked = true;
                selectedBuild.Text = selectedItem.Text;
                // Reset languages to match the selected build
                InitLanguages(formsLanguage);
                InitLanguages(tutorialsLanguage);
            }
        }

        public string GetSelectedBuildDir()
        {
            var buildDirs = GetPossibleBuildDirs();
            return buildDirs[(int) SelectedBuild];
        }

        private string GetZipPath(int architecture)
        {
            return
                (File.Exists(Path.Combine(RootDir, "fileio.dll")) && architecture == 32) ||
                (File.Exists(Path.Combine(RootDir, "ThermoFisher.CommonCore.RawFileReader.dll")) && architecture == 64)
                    ? RootDir
                    : "\\";
        }

        public enum MemoryGraphLocation { quality, nightly }

        public interface IMemoryGraphContainer
        {
            MemoryGraphLocation Location { get; }
            Summary.Run CurrentRun { get; }
            List<string> Labels { get; }
            List<string> FindTest { get; }
            bool UseRunningLogFile { get; }

            BackgroundWorker UpdateWorker { get; set; }
        }

        public void UpdateMemoryGraph(IMemoryGraphContainer graphContainer)
        {
            if (graphContainer.Location == MemoryGraphLocation.quality)
            {
                UpdateMemoryGraph(graphContainer,
                    GraphMemory,
                    LabelDuration,
                    LabelTestsRun,
                    LabelFailures,
                    LabelLeaks,
                    QualityMemoryGraphType);
            }
            else
            {
                UpdateMemoryGraph(graphContainer,
                    NightlyGraphMemory,
                    NightlyLabelDuration,
                    NightlyLabelTestsRun,
                    NightlyLabelFailures,
                    NightlyLabelLeaks,
                    NightlyMemoryGraphType);
            }
        }

        private const string LABEL_TITLE_MEMORY = "Memory Used";
        private const string LABEL_TITLE_HANDLES = "Handles Held";
        private const string LABEL_UNITS_MEMORY = "MB";
        private const string LABEL_UNITS_HANDLE = "Handles";
        private const string LABEL_CURVE_MEMORY_TOTAL = "Total";
        private const string LABEL_CURVE_MEMORY_HEAPS = "Heaps";
        private const string LABEL_CURVE_MEMORY_MANAGED = "Managed";
        private const string LABEL_CURVE_HANDLES_TOTAL = "Total";
        private const string LABEL_CURVE_HANDLES_USER_GDI = "User + GDI";
        private const string LABEL_MENU_MEMORY_TOTAL = "Total Memory";
        private const string LABEL_MENU_HANDLES_TOTAL = "Total Handles";

        private void UpdateMemoryGraph(IMemoryGraphContainer graphContainer,
            ZedGraphControl graphControl,
            Label duration, Label testsRun, Label failures, Label leaks,
            bool memoryGraphType)
        {
            var pane = graphControl.GraphPane;
            pane.Title.FontSpec.Size = 13;
            pane.Title.Text = memoryGraphType ? LABEL_TITLE_MEMORY : LABEL_TITLE_HANDLES;
            pane.YAxis.Title.Text = memoryGraphType ? LABEL_UNITS_MEMORY : LABEL_UNITS_HANDLE;

            bool showTotalCurve = memoryGraphType ? Settings.Default.ShowTotalMemory : Settings.Default.ShowTotalHandles;
            bool showMiddleCurve = memoryGraphType && Settings.Default.ShowHeapMemory;
            bool showLowCurve = memoryGraphType ? Settings.Default.ShowManagedMemory : Settings.Default.ShowUserGdiHandles;

            var run = graphContainer.CurrentRun;
            if (run == null)
            {
                pane.CurveList.Clear();
                pane.XAxis.Scale.TextLabels = new string[0];
                duration.Text = testsRun.Text = failures.Text = leaks.Text = string.Empty;
                graphControl.Refresh();
                return;
            }

            duration.Text = (run.RunMinutes / 60) + ":" + (run.RunMinutes % 60).ToString("D2");
            testsRun.Text = run.TestsRun.ToString(CultureInfo.InvariantCulture);
            failures.Text = run.Failures.ToString(CultureInfo.InvariantCulture);
            leaks.Text = run.Leaks.ToString(CultureInfo.InvariantCulture);

            if (graphContainer.UpdateWorker != null)
                return;

            var worker = new BackgroundWorker();
            graphContainer.UpdateWorker = worker;
            worker.DoWork += (sender, args) =>
            {
                var minorMemoryPoints = showLowCurve ? new PointPairList() : null;
                var middleMemoryPoints = showMiddleCurve ? new PointPairList() : null;
                var majorMemoryPoints = showTotalCurve ? new PointPairList() : null;

                var logFile = graphContainer.UseRunningLogFile ? DefaultLogFile : Summary.GetLogFile(run);
                var labels = new List<string>();
                var findTest = new List<string>();
                if (File.Exists(logFile))
                {
                    string[] logLines;
                    try
                    {
                        lock (CommandShell.LogLock)
                        {
                            logLines = File.ReadAllLines(logFile);
                        }
                    }
                    catch (Exception)
                    {
                        logLines = new string[] { };// Log file is busy
                    }
                    foreach (var line in logLines)
                    {
                        ParseMemoryLine(line, memoryGraphType,
                            minorMemoryPoints, middleMemoryPoints, majorMemoryPoints,
                            labels, findTest);
                    }
                }

                RunUI(() =>
                {
                    pane.CurveList.Clear();

                    try
                    {
                        if (pane.XAxis.Scale.Min < 1)
                            pane.XAxis.Scale.Min = 1;
                        if (pane.XAxis.Scale.Max > labels.Count || pane.XAxis.Scale.Max == graphContainer.Labels.Count)
                            pane.XAxis.Scale.Max = labels.Count;
                        pane.XAxis.Scale.MinGrace = 0;
                        pane.XAxis.Scale.MaxGrace = 0;
                        pane.YAxis.Scale.MinGrace = 0.05;
                        pane.YAxis.Scale.MaxGrace = 0.05;
                        pane.XAxis.Scale.TextLabels = labels.ToArray();
                        pane.Legend.FontSpec.Size = 11;
                        pane.XAxis.Title.FontSpec.Size = 11;
                        pane.XAxis.Scale.FontSpec.Size = 11;

                        graphContainer.Labels.Clear();
                        graphContainer.Labels.AddRange(labels);
                        graphContainer.FindTest.Clear();
                        graphContainer.FindTest.AddRange(findTest);

                        var fillGreen = new Fill(Color.FromArgb(70, 150, 70), Color.FromArgb(150, 230, 150), -90);
//                        var fillYellow = new Fill(Color.FromArgb(237, 125, 49), Color.FromArgb(255, 192, 0), -90);
                        var fillPurple = new Fill(Color.FromArgb(160, 120, 160), Color.FromArgb(220, 180, 220), -90);
                        var fillBlue = new Fill(Color.FromArgb(91, 155, 213), Color.LightBlue, -90);
                        if (minorMemoryPoints != null && minorMemoryPoints.Count > 0)
                        {
                            var managedMemoryCurve = pane.AddCurve(memoryGraphType ? LABEL_CURVE_MEMORY_MANAGED : LABEL_CURVE_HANDLES_USER_GDI,
                                minorMemoryPoints, Color.Black, SymbolType.None);
                            managedMemoryCurve.Line.Fill = fillGreen;
                        }
                        if (middleMemoryPoints != null && middleMemoryPoints.Count > 0)
                        {
                            var middleMemoryCurve = pane.AddCurve(LABEL_CURVE_MEMORY_HEAPS,
                                middleMemoryPoints, Color.Black, SymbolType.None);
                            middleMemoryCurve.Line.Fill = fillBlue;
                        }
                        if (majorMemoryPoints != null && majorMemoryPoints.Count > 0)
                        {
                            var totalMemoryCurve = pane.AddCurve(memoryGraphType ? LABEL_CURVE_MEMORY_TOTAL : LABEL_CURVE_HANDLES_TOTAL,
                                majorMemoryPoints, Color.Black, SymbolType.None);
                            totalMemoryCurve.Line.Fill = fillPurple;
                        }

                        pane.AxisChange();
                        graphControl.Refresh();
                    }
                    // ReSharper disable once EmptyGeneralCatchClause
                    catch (Exception)
                    {
                        // Weird: I got an exception assigning to TextLabels once.  No need
                        // to kill a whole nightly run for that.
                    }
                });

                graphContainer.UpdateWorker = null;
            };

            worker.RunWorkerAsync();
        }

        public static void ParseMemoryLine(string line, bool memoryGraphType,
            PointPairList minorMemoryPoints,
            PointPairList middleMemoryPoints,
            PointPairList majorMemoryPoints,
            List<string> labels,
            List<string> findTest)
        {
            // If this line doesn't look like it should have memory information, just skip it entirely
            if (line.Length < 7 || line[0] != '[' || line[3] != ':' || line[6] != ']' ||
                line.IndexOf("failures, ", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            var testNumber = line.Substring(8, 7).Trim();
            var testName = line.Substring(16, 46).TrimEnd();
            var parts = Regex.Split(line, "\\s+");
            var partsIndex = memoryGraphType ? 6 : 8;
            var unitsIndex = partsIndex + 1;
            var units = memoryGraphType ? LABEL_UNITS_MEMORY : LABEL_UNITS_HANDLE;
            double minorMemory = 0, majorMemory = 0;
            double? middleMemory = null;
            if (unitsIndex < parts.Length && parts[unitsIndex].Equals(units + ",", StringComparison.InvariantCultureIgnoreCase))
            {
                try
                {
                    var memoryParts = parts[partsIndex].Split('/');
                    minorMemory = double.Parse(memoryParts[0]);
                    if (memoryParts.Length > 2)
                    {
                        middleMemory = double.Parse(memoryParts[1]);
                        // To be sure the two lower values, which are somewhat independent,
                        // show up on the graph, the first is added to the second.
                        middleMemory += minorMemory;
                    }
                    majorMemory = double.Parse(memoryParts[memoryParts.Length - 1]);
                }
                catch (Exception)
                {
                    minorMemory = majorMemory = 0;
                    middleMemory = null;
                }
            }
            var minorTag = GetPointTag(minorMemory, units, testNumber, testName);
            var middleTag = GetPointTag(middleMemory, units, testNumber, testName);
            var majorTag = GetPointTag(majorMemory, units, testNumber, testName);

            if (labels.LastOrDefault() == testNumber)
            {
                if (minorMemoryPoints != null)
                {
                    minorMemoryPoints[minorMemoryPoints.Count - 1].Y = minorMemory;
                    minorMemoryPoints[minorMemoryPoints.Count - 1].Tag = minorTag;
                }
                if (majorMemoryPoints != null)
                {
                    majorMemoryPoints[majorMemoryPoints.Count - 1].Y = majorMemory;
                    majorMemoryPoints[majorMemoryPoints.Count - 1].Tag = majorTag;
                }
                if (middleMemoryPoints != null && middleMemory.HasValue)
                {
                    middleMemoryPoints[middleMemoryPoints.Count - 1].Y = middleMemory.Value;
                    middleMemoryPoints[middleMemoryPoints.Count - 1].Tag = middleTag;
                }
            }
            else
            {
                labels.Add(testNumber);
                findTest.Add(line.Substring(8, 54).TrimEnd() + " ");
                if (minorMemoryPoints != null)
                    minorMemoryPoints.Add(minorMemoryPoints.Count, minorMemory, minorTag);
                if (majorMemoryPoints != null)
                    majorMemoryPoints.Add(majorMemoryPoints.Count, majorMemory, majorTag);
                if (middleMemoryPoints != null && middleMemory.HasValue)
                    middleMemoryPoints.Add(middleMemoryPoints.Count, middleMemory.Value, middleTag);
            }
        }

        private static string GetPointTag(double? memory, string units, string testNumber, string testName)
        {
            return memory.HasValue
                ? "{0} {1}\n{2:F2} {3:F1}".With(memory, units, testNumber, testName)
                : null;
        }

        private void GraphControlOnContextMenuBuilder(IMemoryGraphContainer graphContainer, ZedGraphControl graph, ContextMenuStrip menuStrip)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == @"unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, new ToolStripSeparator());

            int iInsert = 0;
            AddGraphPointSetMenuItem(graphContainer, menuStrip, iInsert++, LABEL_MENU_MEMORY_TOTAL,
                () => Settings.Default.ShowTotalMemory, b => Settings.Default.ShowTotalMemory = b);
            AddGraphPointSetMenuItem(graphContainer, menuStrip, iInsert++, LABEL_CURVE_MEMORY_HEAPS,
                () => Settings.Default.ShowHeapMemory, b => Settings.Default.ShowHeapMemory = b);
            AddGraphPointSetMenuItem(graphContainer, menuStrip, iInsert++, LABEL_CURVE_MEMORY_MANAGED,
                () => Settings.Default.ShowManagedMemory, b => Settings.Default.ShowManagedMemory = b);
            AddGraphPointSetMenuItem(graphContainer, menuStrip, iInsert++, LABEL_MENU_HANDLES_TOTAL,
                () => Settings.Default.ShowTotalHandles, b => Settings.Default.ShowTotalHandles = b);
            AddGraphPointSetMenuItem(graphContainer, menuStrip, iInsert++, LABEL_CURVE_HANDLES_USER_GDI,
                () => Settings.Default.ShowUserGdiHandles, b => Settings.Default.ShowUserGdiHandles = b);

            menuStrip.Items.Insert(iInsert++, new ToolStripSeparator());

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.Remove(item);
            }
        }

        private void AddGraphPointSetMenuItem(IMemoryGraphContainer graphContainer, ContextMenuStrip menuStrip, int i,
            string label, Func<bool> get, Action<bool> set)
        {
            var menuItem = new ToolStripMenuItem(label, null, (s, e) =>
            {
                set(!get());
                UpdateMemoryGraph(graphContainer);
            });
            menuItem.Checked = get();
            menuStrip.Items.Insert(i, menuItem);
        }

        #region Menu

        private void open_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog {Filter = "Skyline Tester (*.skyt;*.skytr)|*.skyt;*.skytr"};
            if (openFileDialog.ShowDialog() != DialogResult.Cancel)
                LoadSettingsFromFile(openFileDialog.FileName);
        }

        private void save_Click(object sender, EventArgs e)
        {
            var saveFileDialog = new SaveFileDialog {Filter = "Skyline Tester (*.skyt;*.skytr)|*.skyt;*.skytr"};
            if (saveFileDialog.ShowDialog() != DialogResult.Cancel)
                Save(saveFileDialog.FileName);
        }

        public void Save(string skytFile)
        {
            File.WriteAllText(skytFile, SaveSettings());
        }

        public string SaveSettings()
        {
            var root = CreateElement(
                "SkylineTester",
                tabs,
                accessInternet,

                // Forms
                formsLanguage,
                showFormNames,

                // Tutorials
                pauseTutorialsScreenShots,
                modeTutorialsCoverShots,
                pauseTutorialsDelay,
                pauseTutorialsSeconds,
                tutorialsDemoMode,
                tutorialsLanguage,
                showFormNamesTutorial,
                showMatchingPagesTutorial,
                tutorialsTree,

                // Tests
                runLoops,
                runLoopsCount,
                runIndefinitely,
                repeat,
                randomize,
                recordAuditLogs,
                offscreen,
                testsEnglish,
                testsChinese,
                testsFrench,
                testsJapanese,
                testsTurkish,
                testsTree,
                runCheckedTests,
                skipCheckedTests,
                showTutorialsOnly,
                runMode,

                // Build
                buildTrunk,
                buildBranch,
                branchUrl,
                buildRoot,
                build32,
                build64,
                runBuildVerificationTests,
                startSln,
                nukeBuild,
                updateBuild,
                incrementalBuild,

                // Quality
                qualityPassDefinite,
                qualityPassCount,
                qualityPassIndefinite,
                pass0,
                pass1,
                qualityAllTests,
                qualityChooseTests,

                // Nightly
                nightlyStartTime,
                nightlyDuration,
                nightlyBuildType,
                nightlyBuildTrunk,
                nightlyRunPerfTests,
                nightlyRandomize,
                nightlyRepeat,
                nightlyBranch,
                nightlyBranchUrl,
                nightlyRoot,
                nightlyRunIndefinitely);

            XDocument doc = new XDocument(root);
            return doc.ToString();
        }


        private void exit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void documentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(DocumentationLink);
            }
            catch (Exception)
            {
                MessageBox.Show("Could not open web browser to show link: " + DocumentationLink);
            }
        }

        private void about_Click(object sender, EventArgs e)
        {
            using (var aboutWindow = new AboutWindow())
            {
                aboutWindow.ShowDialog();
            }
        }

        private void SaveZipFileInstaller(object sender, EventArgs e)
        {
            var skylineDirectory = GetSkylineDirectory(ExeDir);
            if (skylineDirectory == null)
            {
                MessageBox.Show(this,
                    "To create the zip file, you must run SkylineTester from the bin directory of the Skyline project.");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                FileName = "SkylineTester.zip",
                Title = "Save zip file installer",
                Filter = "Zip file (*.zip)|*.zip"
            };
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
                return;

            commandShell.RunStartTime = DateTime.UtcNow;
            TabBase.StartLog("Zip", null, true);
            commandShell.Add("{0} {1}", Assembly.GetExecutingAssembly().Location.Quote(),
                saveFileDialog.FileName.Quote());
            RunCommands();
        }

        private void LoadSettingsFromString(string settings)
        {
            using (var stream = new StringReader(settings))
            {
                LoadSettings(XDocument.Load(stream));
            }
        }

        private void LoadSettingsFromFile(string file)
        {
            LoadSettings(XDocument.Load(file));
        }

        private void LoadSettings(XDocument doc)
        {
            foreach (var element in doc.Descendants())
            {
                var name = element.Name.ToString();
                var control = Controls.Find(name, true).FirstOrDefault();
                if (control == null)
                {
                    var menuItems = menuStrip1.Items.Find(name, true);
                    if (menuItems.Length > 0)
                        ((ToolStripMenuItem)menuItems[0]).Checked = (element.Value == "true");
                    continue;
                }

                var tab = control as TabControl;
                if (tab != null)
                {
                    tab.SelectTab(element.Value);
                    continue;
                }

                var button = control as RadioButton;
                if (button != null)
                {
                    if (element.Value == "true")
                        button.Checked = true;
                    continue;
                }

                var checkBox = control as CheckBox;
                if (checkBox != null)
                {
                    checkBox.Checked = (element.Value == "true");
                    continue;
                }

                var textBox = control as TextBox;
                if (textBox != null)
                {
                    textBox.Text = element.Value;
                    continue;
                }

                var comboBox = control as ComboBox;
                if (comboBox != null)
                {
                    comboBox.SelectedItem = element.Value;
                    continue;
                }

                var treeView = control as TreeView;
                if (treeView != null)
                {
                    CheckNodes(treeView, element.Value.Split(','));
                    continue;
                }

                var upDown = control as NumericUpDown;
                if (upDown != null)
                {
                    upDown.Value = int.Parse(element.Value);
                    continue;
                }

                var domainUpDown = control as DomainUpDown;
                if (domainUpDown != null)
                {
                    domainUpDown.SelectedIndex = int.Parse(element.Value);
                    continue;
                }

                var dateTimePicker = control as DateTimePicker;
                if (dateTimePicker != null)
                {
                    dateTimePicker.Value = DateTime.Parse(element.Value);
                    continue;
                }

                var label = control as Label;
                if (label != null)
                {
                    label.Text = element.Value;
                    continue;
                }

                throw new ApplicationException("Attempted to load unknown control type.");
            }
        }

        private XElement CreateElement(string name, params object[] childElements)
        {
            var element = new XElement(name);
            foreach (var child in childElements)
            {
                var tab = child as TabControl;
                if (tab != null)
                {
                    element.Add(new XElement(tab.Name, tab.SelectedTab.Name));
                    continue;
                }

                var button = child as RadioButton;
                if (button != null)
                {
                    element.Add(new XElement(button.Name, button.Checked));
                    continue;
                }

                var checkBox = child as CheckBox;
                if (checkBox != null)
                {
                    element.Add(new XElement(checkBox.Name, checkBox.Checked));
                    continue;
                }

                var textBox = child as TextBox;
                if (textBox != null)
                {
                    element.Add(new XElement(textBox.Name, textBox.Text));
                    continue;
                }

                var comboBox = child as ComboBox;
                if (comboBox != null)
                {
                    element.Add(new XElement(comboBox.Name, comboBox.SelectedItem));
                    continue;
                }

                var treeView = child as TreeView;
                if (treeView != null)
                {
                    element.Add(new XElement(treeView.Name, GetCheckedNodes(treeView)));
                    continue;
                }

                var upDown = child as NumericUpDown;
                if (upDown != null)
                {
                    element.Add(new XElement(upDown.Name, upDown.Value));
                    continue;
                }

                var domainUpDown = child as DomainUpDown;
                if (domainUpDown != null)
                {
                    element.Add(new XElement(domainUpDown.Name, domainUpDown.SelectedIndex));
                    continue;
                }

                var dateTimePicker = child as DateTimePicker;
                if (dateTimePicker != null)
                {
                    element.Add(new XElement(dateTimePicker.Name, dateTimePicker.Value.ToShortTimeString()));
                    continue;
                }

                var menuItem = child as ToolStripMenuItem;
                if (menuItem != null)
                {
                    element.Add(new XElement(menuItem.Name, menuItem.Checked));
                    continue;
                }

                var label = child as Label;
                if (label != null)
                {
                    element.Add(new XElement(label.Name, label.Text));
                    continue;
                }

                throw new ApplicationException("Attempted to save unknown control type");
            }

            return element;
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

        public void EnableButtonSelectFailedTests(bool hasFailures)
        {
            buttonSelectFailedTestsTab.Enabled = buttonSelectFailedTestsTab.Visible = hasFailures;
            buttonSelectFailedOutputTab.Enabled = buttonSelectFailedOutputTab.Visible = hasFailures;
        }

        #endregion Tree support

        #region Accessors

        public ToolStripMenuItem AccessInternet             { get { return accessInternet; } }
        public TextBox          BranchUrl                   { get { return branchUrl; } }
        public CheckBox         Build32                     { get { return build32; } }
        public CheckBox         Build64                     { get { return build64; } }
        public TextBox          BuildRoot                   { get { return buildRoot; } }
        public RadioButton      BuildTrunk                  { get { return buildTrunk; } }
        public Button           ButtonDeleteBuild           { get { return buttonDeleteBuild; } }
        public Button           ButtonOpenLog               { get { return buttonOpenLog; } }
        public Button           ButtonViewLog               { get { return buttonViewLog; } }
        public ComboBox         ComboOutput                 { get { return comboBoxOutput; } }
        public ComboBox         ComboRunStats               { get { return comboBoxRunStats; } }
        public ComboBox         ComboRunStatsCompare        { get { return comboBoxRunStatsCompare; } }
        public CommandShell     CommandShell                { get { return commandShell; } }
        public DataGridView     DataGridRunStats            { get { return dataGridRunStats; } }
        public Button           DeleteNightlyTask           { get { return buttonDeleteNightlyTask; } }
        public RichTextBox      ErrorConsole                { get { return errorConsole; } }
        public ComboBox         FormsLanguage               { get { return formsLanguage; } }
        public DataGridView     FormsGrid                   { get { return formsGrid; } }
        public ToolStripLabel   FormsSeenPercent            { get { return labelFormsSeenPercent; } }
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
        public TextBox          NightlyBranchUrl            { get { return nightlyBranchUrl; } }
        public RadioButton      NightlyBuildTrunk           { get { return nightlyBuildTrunk; } }
        public DomainUpDown     NightlyBuildType            { get { return nightlyBuildType; } }
        public Button           NightlyDeleteRun            { get { return nightlyDeleteRun; } }
        public NumericUpDown    NightlyDuration             { get { return nightlyDuration; } }
        public CheckBox         NightlyExit                 { get { return nightlyExit; } }
        public Label            NightlyLabelDuration        { get { return nightlyLabelDuration; } }
        public Label            NightlyLabelFailures        { get { return nightlyLabelFailures; } }
        public Label            NightlyLabelLeaks           { get { return nightlyLabelLeaks; } }
        public Label            NightlyLabelTestsRun        { get { return nightlyLabelTestsRun; } }
        public ZedGraphControl  NightlyGraphMemory          { get { return nightlyGraphMemory; } }
        public CheckBox         NightlyRandomize            { get { return nightlyRandomize; } }
        public CheckBox         NightlyRunIndefinitely      { get { return nightlyRunIndefinitely; } }
        public Label            NightlyRoot                 { get { return nightlyRoot; } }
        public ComboBox         NightlyRunDate              { get { return nightlyRunDate; } }
        public ComboBox         NightlyRepeat               { get { return nightlyRepeat; } }
        public CheckBox         NightlyRunPerfTests         { get { return nightlyRunPerfTests; } }
        public DateTimePicker   NightlyStartTime            { get { return nightlyStartTime; } }
        public Label            NightlyTestName             { get { return nightlyTestName; } }
        public WindowThumbnail  NightlyThumbnail            { get { return nightlyThumbnail; } }
        public Button           NightlyViewLog              { get { return nightlyViewLog; } }
        public bool             NightlyMemoryGraphType      { get { return radioNightlyMemory.Checked; } }
        public RadioButton      NukeBuild                   { get { return nukeBuild; } }
        public CheckBox         Offscreen                   { get { return offscreen; } }
        public ComboBox         OutputJumpTo                { get { return outputJumpTo; } }
        public SplitContainer   OutputSplitContainer        { get { return outputSplitContainer; } }
        public CheckBox         Pass0                       { get { return pass0; } }
        public CheckBox         Pass1                       { get { return pass1; } }
        public RadioButton      ModeTutorialsCoverShots     { get { return modeTutorialsCoverShots; } }
        public TextBox          PauseStartingPage           { get { return pauseStartingPage; } }
        public RadioButton      PauseTutorialsScreenShots   { get { return pauseTutorialsScreenShots; } }
        public NumericUpDown    PauseTutorialsSeconds       { get { return pauseTutorialsSeconds; } }
        public RadioButton      QualityChooseTests          { get { return qualityChooseTests; } }
        public TabPage          QualityPage                 { get { return tabQuality; } }
        public NumericUpDown    QualityPassCount            { get { return qualityPassCount; } }
        public RadioButton      QualityPassDefinite         { get { return qualityPassDefinite; } }
        public Label            QualityTestName             { get { return qualityTestName; } }
        public CheckBox         QualityRunSmallMoleculeVersions { get { return qualityRunSmallMoleculeVersions; } }
        public WindowThumbnail  QualityThumbnail            { get { return qualityThumbnail; } }
        public bool             QualityMemoryGraphType      { get { return radioQualityMemory.Checked; } }
        public Button           RunBuild                    { get { return runBuild; } }
        public CheckBox         RunBuildVerificationTests   { get { return runBuildVerificationTests; } }
        public Button           RunForms                    { get { return runForms; } }
        public ComboBox         RunTestMode                 { get { return runMode; } }
        public RadioButton      RunIndefinitely             { get { return runIndefinitely; } }
        public NumericUpDown    RunLoopsCount               { get { return runLoopsCount; } }
        public Button           RunNightly                  { get { return runNightly; } }
        public Button           RunQuality                  { get { return runQuality; } }
        public Button           RunTests                    { get { return runTests; } }
        public Button           RunTutorials                { get { return runTutorials; } }
        public CheckBox         ShowFormNames               { get { return showFormNames; } }
        public CheckBox         ShowMatchingPagesTutorial   { get { return showMatchingPagesTutorial; } }
        public CheckBox         ShowFormNamesTutorial       { get { return showFormNamesTutorial; } }
        public CheckBox         ShowTutorialsOnly           { get { return showTutorialsOnly; } }
        public RadioButton      SkipCheckedTests            { get { return skipCheckedTests; } }
        public CheckBox         StartSln                    { get { return startSln; } }
        public TabControl       Tabs                        { get { return tabs; } }
        public CheckBox         TestsRunSmallMoleculeVersions { get {  return testsRunSmallMoleculeVersions;} }
        public CheckBox         TestsRandomize              { get { return randomize; } }
        public CheckBox         TestsRecordAuditLogs        { get { return recordAuditLogs; } }
        public ComboBox         TestsRepeatCount            { get { return repeat; } }
        public MyTreeView       TestsTree                   { get { return testsTree; } }
        public CheckBox         TestsChinese                { get { return testsChinese; } }
        public CheckBox         TestsEnglish                { get { return testsEnglish; } }
        public CheckBox         TestsFrench                 { get { return testsFrench; } }
        public CheckBox         TestsJapanese               { get { return testsJapanese; } }
        public CheckBox         TestsTurkish                { get { return testsTurkish; } }
        public RadioButton      TutorialsDemoMode           { get { return tutorialsDemoMode; } }
        public ComboBox         TutorialsLanguage           { get { return tutorialsLanguage; } }
        public MyTreeView       TutorialsTree               { get { return tutorialsTree; } }
        public RadioButton      UpdateBuild                 { get { return updateBuild; } }

        #endregion Accessors

        #region Control events

        private void comboBoxOutput_SelectedIndexChanged(object sender, EventArgs e)
        {
            _tabOutput.ClearErrors();
            commandShell.Load(GetSelectedLog(comboBoxOutput), comboBoxOutput.SelectedIndex == 0,() => _tabOutput.LoadDone());
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

        private void nightlyBrowseBuild_Click(object sender, EventArgs e)
        {
            _tabNightly.BrowseBuild();
        }

        private void comboRunDate_SelectedIndexChanged(object sender, EventArgs e)
        {
            _tabNightly.RunDateChanged();
        }

        private void buttonOpenLog_Click(object sender, EventArgs e)
        {
            ShowOutput();
        }

        private void buttonDeleteRun_Click(object sender, EventArgs e)
        {
            _tabNightly.DeleteRun();
        }

        private void selectBuild_Click(object sender, EventArgs e)
        {
            SelectBuild((BuildDirs) selectBuildMenuItem.DropDownItems.IndexOf((ToolStripMenuItem)sender));
        }

        private void selectBuildMenuOpening(object sender, EventArgs e)
        {
            FindBuilds();
        }

        private void commandShell_MouseClick(object sender, MouseEventArgs e)
        {
            _tabOutput.CommandShellMouseClick();
        }

        private void errorConsole_SelectionChanged(object sender, EventArgs e)
        {
            if (_tabOutput != null)
                _tabOutput.ErrorSelectionChanged();
        }

        private void SkylineTesterWindow_Move(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                Settings.Default.WindowLocation = Location;
            Settings.Default.WindowMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private void SkylineTesterWindow_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                Settings.Default.WindowSize = Size;
            Settings.Default.WindowMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private void findTestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var findWindow = new FindWindow())
            {
                if (findWindow.ShowDialog() != DialogResult.OK)
                    return;
                _findText = findWindow.FindText;
            }

            _findPosition = 0;
            findNextToolStripMenuItem_Click(null, null);
        }

        private void findNextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_findText == null)
            {
                findTestToolStripMenuItem_Click(null, null);
                return;
            }

            if (_findPosition >= 0)
                _findPosition = _tabs[_previousTab].Find(_findText, _findPosition);

            if (_findPosition == -1)
                MessageBox.Show(this, "Couldn't find \"{0}\"".With(_findText));
        }

        public int FindOutput(string text, int position)
        {
            _tabOutput.AfterLoad = () =>
            {
                _findPosition = _tabOutput.Find(text, position);
            };
            ShowOutput();
            return 0;
        }

        private void buttonDeleteNightlyTask_Click(object sender, EventArgs e)
        {
            using (TaskService ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(TabNightly.NIGHTLY_TASK_NAME, false);
            }
            buttonDeleteNightlyTask.Enabled = false;
        }

        private void buttonNow_Click(object sender, EventArgs e)
        {
            nightlyStartTime.Value = DateTime.Now;
        }

        private void outputJumpTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            _tabOutput.JumpTo(outputJumpTo.SelectedIndex);
        }

        private void outputJumpTo_Click(object sender, EventArgs e)
        {
            _tabOutput.PrepareJumpTo();
        }

        private void formsGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex > 1)
                return;

            // If there is an active run, stop it and then restart.
            bool restart = _runningTab != null;
            if (restart && !ReferenceEquals(_runningTab, _tabForms))
            {
                MessageBox.Show(this,
                    "Tests are running in a different tab. Click Stop before showing forms.");
                return;
            }

            _restart = restart;

            if (e.ColumnIndex == 1)
            {
                var testLink = formsGrid.Rows[e.RowIndex].Cells[1].Value;
                if (testLink != null)
                {
                    var testName = testLink.ToString();
                    for (int i = 0; i < formsGrid.RowCount; i++)
                    {
                        var thisTest = formsGrid.Rows[i].Cells[1].Value;
                        if (thisTest != null)
                            formsGrid.Rows[i].Selected = (thisTest.ToString() == testName);
                    }
                }
            }

            // Start new run.
            RunOrStopByUser();
        }

        private void formsGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 2)
                return;

            int value;
            int.TryParse(formsGrid.Rows[e.RowIndex].Cells[2].Value.ToString(), out value);
            formsGrid.Rows[e.RowIndex].Cells[2].Value = value;
        }

        private void clearSeenButton_Click(object sender, EventArgs e)
        {
            FormSeen.Clear();
            _tabForms.UpdateForms();
        }

        private void formsGrid_SelectionChanged(object sender, EventArgs e)
        {
            labelSelectedFormsCount.Text = formsGrid.SelectedRows.Count + " selected";
        }

        private void pauseTutorialsScreenShots_CheckedChanged(object sender, EventArgs e)
        {
            bool pauseChecked = pauseTutorialsScreenShots.Checked;
            showMatchingPagesTutorial.Enabled = pauseChecked;
            if (!pauseChecked)
                showMatchingPagesTutorial.Checked = false;
        }

        private void comboBoxRunStats_SelectedIndexChanged(object sender, EventArgs e)
        {
            _tabRunStats.Process(GetSelectedLog(comboBoxRunStats), GetSelectedLog(comboBoxRunStatsCompare));
        }

        private void radioQualityMemory_CheckedChanged(object sender, EventArgs e)
        {
            _tabQuality.UpdateGraph();
        }

        private void radioQualityHandles_CheckedChanged(object sender, EventArgs e)
        {
            _tabQuality.UpdateGraph();
        }

        private void radioNightlyMemory_CheckedChanged(object sender, EventArgs e)
        {
            _tabNightly.UpdateGraph();
        }

        private void radioNightlyHandles_CheckedChanged(object sender, EventArgs e)
        {
            _tabNightly.UpdateGraph();
        }

        private void ShowDiff(string path, string path2)
        {
            var tGitDiffFiles = "/command:diff /path:" + path + " /path2:" + path2;
            Process.Start("TortoiseGitProc.exe", tGitDiffFiles);
        }

        private string GetPathForLanguage(string formName, string languageName)
        {
            string skylineDir = Path.GetFullPath(Path.Combine(ExeDir, @"..\..\.."));
            string fileName = GetFileForLanguage(formName, languageName);
            var files =  Directory.GetFiles(skylineDir, fileName, SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                string commonDir = Path.GetFullPath(Path.Combine(skylineDir, @"..\Shared\Common"));
                files = Directory.GetFiles(commonDir, fileName, SearchOption.AllDirectories);
                if (files.Length == 0)
                    return String.Empty;
            }

            return files[0];
        }

        private string GetFileForLanguage(string formName, string languageName)
        {
            if (Equals(languageName, "English"))
                return formName + ".resx";
            if (Equals(languageName, "Chinese"))
                return formName + ".zh-CHS.resx";
            if (Equals(languageName, "Japanese"))
                return formName + ".ja.resx";
            return null;
        }

        private void diffButton_Click(object sender, EventArgs e)
        {
            var formName = formsGrid.CurrentRow?.Cells[0].Value.ToString();

            if (formName != null && formName.Contains("."))
                formName = formName.Substring(0, formName.IndexOf(".", StringComparison.Ordinal));

            if (formName != null)
            {
                ShowDiff(GetPathForLanguage(formName, formsLanguage.SelectedItem.ToString()),
                    GetPathForLanguage(formName, formsLanguageDiff.SelectedItem.ToString()));
            }
        }

        private void PopulateFormsLanguageDiff(string language)
        {
            var listLanguages = new List<string> { "English", "Chinese", "Japanese" };
            listLanguages.Remove(language);
            if (Equals(language, "English"))
                listLanguages.Insert(0, string.Empty);
            if (!formsLanguageDiff.Enabled)
                formsLanguageDiff.Enabled = true;
            formsLanguageDiff.Items.Clear();
            formsLanguageDiff.Items.AddRange(listLanguages.ToArray());
            if (Equals(language, "Chinese") || Equals(language, "Japanese"))
            {
                formsLanguageDiff.SelectedItem = "English";
                diffButton.Enabled = true;
            }
            else
            {
                diffButton.Enabled = false;
            }
        }

        private void formsLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Equals(formsLanguage.SelectedItem.ToString(), "French") || Equals(formsLanguage.SelectedItem.ToString(), "Turkish"))
            {
                formsLanguageDiff.Items.Clear();
                formsLanguageDiff.Enabled = false;
                diffButton.Enabled = false;
            }
            else
            {
                PopulateFormsLanguageDiff(formsLanguage.SelectedItem.ToString());
            }
        }

        private void formsLanguageDiff_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (formsLanguageDiff.SelectedItem.ToString() == "")
                diffButton.Enabled = false;
            else
                diffButton.Enabled = true;
        }

        private string[] GetChangedFileList()
        {
            var skylineDir = Path.GetFullPath(Path.Combine(ExeDir, @"..\..\.."));
            var procStartInfo = new ProcessStartInfo("cmd", "/c " + skylineDir + " & git diff --name-only");

            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            var cmd = new Process();
            cmd.StartInfo = procStartInfo;
            cmd.Start();

            var changedFiles = cmd.StandardOutput.ReadToEnd();
            return changedFiles.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);
        }

        private static string GetFileName(string file)
        {
            var fileName = Path.GetFileName(file);
            return fileName.Substring(0, fileName.IndexOf(".", StringComparison.Ordinal));
        }

        private void showChangedFiles_CheckedChanged(object sender, EventArgs e)
        {
            var changedFilesArray = GetChangedFileList();

            if (showChangedFiles.Checked)
            {
                foreach (DataGridViewRow row in formsGrid.Rows)
                {
                    var formNoExt = row.Cells[0].Value.ToString().Split('.')[0];

                    foreach (var file in changedFilesArray)
                    {
                        if (!Equals(file, ""))
                        {
                            var filename = GetFileName(file);
                            if (Equals(formNoExt, filename))
                            {
                                row.Visible = true;
                                break;
                            }
                            else
                            {
                                row.Visible = false;
                            }
                        }
                    }
                }
                formsGrid.Update();
                formsGrid.Refresh();
            }
            else
            {
                foreach (DataGridViewRow row in formsGrid.Rows)
                {
                    row.Visible = true;
                }
                formsGrid.Update();
                formsGrid.Refresh();
            }
        }

        private void showTutorialsOnly_CheckedChanged(object sender, EventArgs e)
        {
            var loader = new BackgroundWorker();
            loader.DoWork += BackgroundLoad;
            loader.RunWorkerAsync();
        }

        #endregion Control events
    }
}
