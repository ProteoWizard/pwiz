/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ionic.Zip;
using Microsoft.Win32;
using OneOf;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Koina;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using static pwiz.Skyline.CommandArgs;

namespace pwiz.Skyline.ToolsUI
{
    public partial class PythonInstaller : FormEx
    {
        private const string PYTHON_FTP_SERVER_URL = @"https://www.python.org/ftp/python/";
        private const string BOOTSTRAP_PYPA_URL = @"https://bootstrap.pypa.io/";
        private const string PYTHON = @"Python";
        private const string PYTHON_LOWER_CASE = @"python";
        private const string HYPHEN = @"-";
        private const string EMBED_LOWER_CASE = @"embed";
        private const string DOT_ZIP = @".zip";
        private const string FORWARD_SLASH = @"/";
        private const string BACK_SLASH = @"\";
        private const string GET_PIP_SCRIPT_FILE_NAME = @"get-pip.py";
        private const string PYTHON_EXECUTABLE = @"python.exe";
        private const string VIRTUALENV = @"virtualenv";
        private const string PIP = @"pip";
        private const string INSTALL = @"install";
        private const string PYTHON_MODULE_OPTION = @"-m";
        private const string CD = @"cd";
        private const string ECHO = @"echo";
        private const string CMD_PROCEEDING_SYMBOL = @"&";
        private const string CONDITIONAL_CMD_PROCEEDING_SYMBOL = @"&&";
        private const string CMD_ESCAPE_SYMBOL = @"^";
        private const string SPACE = @" ";
        private const string SCRIPTS = @"Scripts";
        private const string EQUALS = @"==";

        private readonly bool _installed;
        private readonly TextWriter _writer;

        public int NumTotalTasks { get; private set; }
        public int NumCompletedTasks { get; private set; }
        /// <summary>
        /// For testing purpose only. Setting this property will bypass the TaskValidator
        /// </summary>
        public List<TaskName> TestPythonVirtualEnvironmentTaskNames { get; set; }
        public string PythonVersion { get; }
        public IEnumerable<PythonPackage> PythonPackages { get; }
        public string PythonEmbeddablePackageFileName => PythonEmbeddablePackageFileBaseName + DOT_ZIP;
        public Uri PythonEmbeddablePackageUri => new Uri(PYTHON_FTP_SERVER_URL + PythonVersion + FORWARD_SLASH + PythonEmbeddablePackageFileName);
        public string PythonEmbeddablePackageDownloadPath => PythonVersionDir + BACK_SLASH + PythonEmbeddablePackageFileName;
        public string PythonEmbeddablePackageExtractDir => PythonVersionDir + BACK_SLASH + PythonEmbeddablePackageFileBaseName;
        public Uri GetPipScriptDownloadUri => new Uri(BOOTSTRAP_PYPA_URL + GET_PIP_SCRIPT_FILE_NAME);
        public string GetPipScriptDownloadPath => PythonRootDir + BACK_SLASH + GET_PIP_SCRIPT_FILE_NAME;
        public string BasePythonExecutablePath => PythonEmbeddablePackageExtractDir + BACK_SLASH + PYTHON_EXECUTABLE;
        public string VirtualEnvironmentName { get; }
        public string VirtualEnvironmentDir => PythonVersionDir + BACK_SLASH + VirtualEnvironmentName;
        public string VirtualEnvironmentPythonExecutablePath => VirtualEnvironmentDir + BACK_SLASH + SCRIPTS + BACK_SLASH + PYTHON_EXECUTABLE;

        private List<Task> PendingTasks { get; set; }
        private string PythonVersionDir => PythonRootDir + BACK_SLASH + PythonVersion;
        private string PythonEmbeddablePackageFileBaseName {
            get
            {
                var architecture = PythonUtil.GetPythonPackageArchitectureSubstring(
                    System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
                var fileBaseName = string.Join(HYPHEN, new[] { PYTHON_LOWER_CASE, PythonVersion, EMBED_LOWER_CASE, architecture });
                return fileBaseName;
            }
        }
        private IList<string> PackageUris { get; set; }
        private IList<string> LocalPackages { get; set; } 
        private enum Tab {standard, virtual_environment}
        private IPythonInstallerTaskValidator TaskValidator { get; }
        private string PythonRootDir { get; } = PythonUtil.PythonRootDir;

        public PythonInstaller(ProgramPathContainer pythonPathContainer, IEnumerable<string> packages, TextWriter writer)
            : this(pythonPathContainer, packages, PythonUtil.CheckInstalled(pythonPathContainer.ProgramVersion), writer) { }

        public PythonInstaller(ProgramPathContainer pythonPathContainer, IEnumerable<string> packages, bool installed,
            TextWriter writer)
            : this(pythonPathContainer, packages.Select(package => new PythonPackage { Name = package, Version = null }),
                installed, writer, null) { }

        public PythonInstaller(ProgramPathContainer pythonPathContainer, IEnumerable<PythonPackage> packages,
            TextWriter writer, IPythonInstallerTaskValidator taskValidator, string virtualEnvironmentName = null)
            : this(pythonPathContainer, packages, PythonUtil.CheckInstalled(pythonPathContainer.ProgramVersion), writer,
                taskValidator, virtualEnvironmentName) { }

        public PythonInstaller(ProgramPathContainer pythonPathContainer, IEnumerable<PythonPackage> packages, bool installed, 
            TextWriter writer, IPythonInstallerTaskValidator taskValidator, string virtualEnvironmentName = null)
        {
            PythonVersion = pythonPathContainer.ProgramVersion;
            _installed = installed;
            _writer = writer;
            TaskValidator = taskValidator;
            VirtualEnvironmentName = virtualEnvironmentName;
            PythonPackages = packages.ToList();
            PendingTasks = new List<Task>();
            AssignPackages(PythonPackages.Select(package => package.Name).ToList());
            InitializeComponent();
            CreatePythonDirectories();
            PopulateDlgText();
            if (virtualEnvironmentName != null)
            {
                tabs.SelectedIndex = (int)Tab.virtual_environment;
            }

        }

        private void AssignPackages(IEnumerable<string> packages)
        {
            PackageUris = new List<string>();
            LocalPackages = new List<string>();
            foreach (var package in packages)
            {
                if (package.StartsWith(@"http"))
                    PackageUris.Add(package);
                else
                    LocalPackages.Add(package);
            }
        }

        private void CreatePythonDirectories()
        {
            CreateDirIfNotExist(PythonRootDir);
            CreateDirIfNotExist(PythonVersionDir);
        }

        private void PopulateDlgText()
        {
            this.labelVirtualEnvironment.Text = $@"This tool requires Python {PythonVersion} and the following packages. A dedicated python virtual environment {VirtualEnvironmentName} will be created for the tool. Click the ""Install"" button to start installation.";
            var packagesStringBuilder = new StringBuilder();
            foreach (var package in PythonPackages)
            {
                packagesStringBuilder.Append(HYPHEN + SPACE + package.Name);
                if (package.Version != null)
                {
                    packagesStringBuilder.Append(SPACE + package.Version);
                }
                packagesStringBuilder.Append(Environment.NewLine);
            }
            this.textBoxVirtualEnvironment.Text = packagesStringBuilder.ToString();
        }

        public bool IsLoaded { get; private set; }

        private void PythonInstaller_Load(object sender, EventArgs e)
        {
            if (!_installed && (PackageUris.Count + LocalPackages.Count) != 0)
            {
                labelMessage.Text = string.Format(Resources.PythonInstaller_PythonInstaller_Load_This_tool_requires_Python__0__and_the_following_packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_, PythonVersion);
                PopulatePackageCheckListBox();
            } else if (!_installed)
            {
                labelMessage.Text = string.Format(Resources.PythonInstaller_PythonInstaller_Load_This_tool_requires_Python__0___Click_install_to_begin_the_installation_process_, PythonVersion);
                int shift = btnCancel.Top - clboxPackages.Top;
                clboxPackages.Visible = clboxPackages.Enabled = false;
                Height -= shift;
            }
            else if ((PackageUris.Count + LocalPackages.Count) != 0)
            {
                labelMessage.Text = Resources.PythonInstaller_PythonInstaller_Load_This_tool_requires_the_following_Python_packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_;
                PopulatePackageCheckListBox();
            }
            IsLoaded = true;
        }

        private void PopulatePackageCheckListBox()
        {
            clboxPackages.DataSource = IsolatePackageNames(PackageUris).Concat(IsolatePackageNames(LocalPackages)).ToList();

            // initially set them as checked
            for (int i = 0; i < clboxPackages.Items.Count; i++)
            {
                clboxPackages.SetItemChecked(i, true);
            }
        }

        private static IEnumerable<string> IsolatePackageNames(IEnumerable<string> packages)
        {
            ICollection<string> packageNames = new Collection<string>();
            const string pattern = @"([^/\\]*)\.(zip|tar\.gz|exe)$";
            foreach (var package in packages)
            {
                Match name = Regex.Match(package, pattern);
                packageNames.Add(name.Groups[1].ToString());
            }
            return packageNames;
        } 

        private void btnInstall_Click(object sender, EventArgs e)
        {
            if (VirtualEnvironmentName != null)
            {
                OkDialogVirtualEnvironment();
            }
            else
            {
                OkDialog();
            }
        }

        public void OkDialog()
        {
            Enabled = false;

            if ((_installed || GetPython()) && (clboxPackages.CheckedIndices.Count == 0 || GetPackages()))
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }
        }

        public void OkDialogVirtualEnvironment()
        {
            var tasks = PendingTasks.IsNullOrEmpty() ? ValidatePythonVirtualEnvironment() : PendingTasks;
            NumTotalTasks = tasks.Count;
            NumCompletedTasks = 0;
            foreach (var task in tasks) {
                try
                {
                    if (task.IsActionWithNoArg)
                    {
                        using var waitDlg = new LongWaitDlg();
                        waitDlg.Message = task.InProgressMessage;
                        waitDlg.PerformWork(this, 50, task.AsActionWithNoArg);
                    }
                    else if (task.IsActionWithProgressMonitor)
                    {
                        using var waitDlg = new LongWaitDlg();
                        waitDlg.ProgressValue = 0;
                        waitDlg.PerformWork(this, 50, task.AsActionWithProgressMonitor);
                    }
                    else
                    {
                        throw new PythonInstallerUnsupportedTaskException(task);
                    }
                    NumCompletedTasks++;
                }
                catch (Exception ex)
                {
                    MessageDlg.Show(this, task.FailureMessage);
                    MessageDlg.ShowWithException(this, (ex.InnerException ?? ex).Message, ex);
                    break;
                }
            }
            Debug.WriteLine($@"total: {NumTotalTasks}, completed: {NumCompletedTasks}");
            if (NumCompletedTasks == NumTotalTasks)
            {
                PendingTasks.Clear();
                MessageDlg.Show(this, ToolsUIResources.PythonInstaller_OkDialogVirtualEnvironment_Successfully_set_up_Python_virtual_environment);
                DialogResult = DialogResult.OK;
            }
            else
            {
                MessageDlg.Show(this, ToolsUIResources.PythonInstaller_OkDialogVirtualEnvironment_Failed_to_set_up_Python_virtual_environment);
                DialogResult = DialogResult.Cancel;
            }
        }

        public bool IsPythonVirtualEnvironmentReady()
        {
            var tasks = PendingTasks.IsNullOrEmpty()? ValidatePythonVirtualEnvironment() : PendingTasks;
            return tasks.Count == 0;
        }

        private List<Task> ValidatePythonVirtualEnvironment()
        {
            var tasks = new List<Task>();
            if (!TestPythonVirtualEnvironmentTaskNames.IsNullOrEmpty())
            {
                foreach (var taskName in TestPythonVirtualEnvironmentTaskNames)
                {
                    tasks.Add(GetPythonVirtualEnvironmentTask(taskName));
                    return tasks;
                }
            }

            var taskNodes = PythonUtil.GetPythonVirtualEnvironmentTaskNodes();
            var hasSeenFailure = false;
            foreach (var taskNode in taskNodes)
            {
                var isTaskValid = TaskValidator.Validate(taskNode.TaskName, this);
                if (hasSeenFailure)
                {
                    if (isTaskValid && taskNode.ParentNodes.Equals(null)) { continue; }
                }
                else
                {
                    if (isTaskValid) { continue; }
                    hasSeenFailure = true;
                }
                tasks.Add(GetPythonVirtualEnvironmentTask(taskNode.TaskName));
            }
            PendingTasks = tasks;
            return tasks;
        }

        private Task GetPythonVirtualEnvironmentTask(TaskName taskName)
        {
            switch (taskName)
            {
                case TaskName.download_python_embeddable_package:
                    var task1 = new Task(DownloadPythonEmbeddablePackage);
                    task1.InProgressMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Downloading_Python_embeddable_package;
                    task1.FailureMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Failed_to_download_Python_embeddable_package;
                    return task1;
                case TaskName.unzip_python_embeddable_package:
                    var task2 = new Task(UnzipPythonEmbeddablePackage);
                    task2.InProgressMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Unzipping_Python_embeddable_package;
                    task2.FailureMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Failed_to_unzip_Python_embeddable_package;
                    return task2;
                case TaskName.enable_search_path_in_python_embeddable_package:
                    var task3 = new Task(EnableSearchPathInPythonEmbeddablePackage);
                    task3.InProgressMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Enabling_search_path_in_Python_embeddable_package;
                    task3.FailureMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Failed_to_enable_search_path_in_Python_embeddable_package;
                    return task3;
                case TaskName.download_getpip_script:
                    var task4 = new Task(DownloadGetPipScript);
                    task4.InProgressMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Downloading_the_get_pip_py_script;
                    task4.FailureMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Failed_to_download_the_get_pip_py_script;
                    return task4;
                case TaskName.run_getpip_script:
                    var task5 = new Task(RunGetPipScript);
                    task5.InProgressMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Running_the_get_pip_py_script;
                    task5.FailureMessage = ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Failed_to_run_the_get_pip_py_script;
                    return task5;
                case TaskName.pip_install_virtualenv:
                    var virtualEnvPackage = new PythonPackage { Name = VIRTUALENV, Version = null };
                    var task6 = new Task(() => PipInstall(BasePythonExecutablePath, new [] {virtualEnvPackage}));
                    task6.InProgressMessage = string.Format(ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Running_pip_install__0_, VIRTUALENV);
                    task6.FailureMessage = string.Format(ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Failed_to_run_pip_install__0_, VIRTUALENV);
                    return task6;
                case TaskName.create_virtual_environment:
                    var task7 = new Task(() => RunPythonModule(
                        BasePythonExecutablePath, PythonVersionDir, VIRTUALENV, new [] {VirtualEnvironmentName}));
                    task7.InProgressMessage = string.Format(ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Creating_virtual_environment__0_, VirtualEnvironmentName);
                    task7.FailureMessage = string.Format(ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Failed_to_create_virtual_environment__0_, VirtualEnvironmentName);
                    return task7;
                case TaskName.pip_install_packages:
                    var task8 = new Task(() => PipInstall(VirtualEnvironmentPythonExecutablePath, PythonPackages));
                    task8.InProgressMessage = string.Format(ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Installing_Python_packages_in_virtual_environment__0_, VirtualEnvironmentName);
                    task8.FailureMessage = string.Format(ToolsUIResources.PythonInstaller_ValidatePythonVirtualEnvironment_Failed_to_install_Python_packages_in_virtual_environment__0_, VirtualEnvironmentName);
                    return task8;
                default:
                    throw new PythonInstallerUnsupportedTaskNameException(taskName);
            }
        }

        private bool GetPython()
        {
            try
            {
                using (var waitDlg = new LongWaitDlg())
                {
                    waitDlg.ProgressValue = 0;
                    // Short wait, because this can't possible happen fast enough to avoid
                    // showing progress, except in testing
                    waitDlg.PerformWork(this, 50, DownloadPython);
                }
                using (var waitDlg = new LongWaitDlg(null, false))
                {
                    waitDlg.Message = ToolsUIResources.PythonInstaller_GetPython_Installing_Python;
                    waitDlg.PerformWork(this, 50, InstallPython);
                }
                MessageDlg.Show(this, Resources.PythonInstaller_GetPython_Python_installation_completed_);
                return true;
            }
            catch (Exception ex)
            {
                MessageDlg.ShowWithException(this, (ex.InnerException ?? ex).Message, ex);
            }
            return false;
        }

        private string DownloadPath { get; set; }

        private void DownloadPython(IProgressMonitor waitBroker)
        {
            // the base Url for python releases
            const string baseUri = "http://python.org/ftp/python/";

            // the installer file name, e.g. python-2.7.msi
            string fileName = @"python-" + PythonVersion + @".msi";

            // the fully formed Uri, e.g. http://python.org/ftp/python/2.7/python-2.7.msi
            var downloadUri = new Uri(baseUri + PythonVersion + @"/" + fileName);

            using (var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(waitBroker, 1))
            {
                if (!webClient.DownloadFileAsync(downloadUri, DownloadPath = Path.GetTempPath() + fileName, out var downloadException))
                    throw new ToolExecutionException(TextUtil.LineSeparate(
                        Resources.PythonInstaller_DownloadPython_Download_failed_, 
                        Resources.PythonInstaller_DownloadPython_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_), downloadException);
            }
        }

        private void InstallPython()
        {
            var processRunner = TestRunProcess ?? new SynchronousRunProcess();
            var startInfo = new ProcessStartInfo
                {
                    FileName = @"msiexec",
                    // ReSharper disable LocalizableElement
                    Arguments = "/i \"" + DownloadPath + "\"",
                    // ReSharper restore LocalizableElement
                };
            if (processRunner.RunProcess(new Process {StartInfo = startInfo}) != 0)
                throw new ToolExecutionException(Resources.PythonInstaller_InstallPython_Python_installation_failed__Canceling_tool_installation_);
        }

        private void DownloadPythonEmbeddablePackage(IProgressMonitor progressMonitor)
        {
            using var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);
            if (!webClient.DownloadFileAsync(PythonEmbeddablePackageUri, PythonEmbeddablePackageDownloadPath, out var downloadException))
                throw new ToolExecutionException(TextUtil.LineSeparate(
                    Resources.PythonInstaller_DownloadPython_Download_failed_,
                    Resources.PythonInstaller_DownloadPython_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_), downloadException);
        }

        private void UnzipPythonEmbeddablePackage(IProgressMonitor progressMonitor)
        {
            using var zipFile = ZipFile.Read(PythonEmbeddablePackageDownloadPath);
            zipFile.ExtractAll(PythonEmbeddablePackageExtractDir);
        }

        private void EnableSearchPathInPythonEmbeddablePackage()
        {
            var files = Directory.GetFiles(PythonEmbeddablePackageExtractDir, "python*._pth");
            Assume.IsTrue(files.Length == 1, ToolsUIResources.PythonInstaller_EnableSearchPathInPythonEmbeddablePackage_Found_0_or_more_than_one_files_with___pth_extension__this_is_unexpected_);
            var oldFilePath = files[0];
            var newFilePath = Path.ChangeExtension(oldFilePath, @".pth");
            File.Move(oldFilePath, newFilePath);
        }
        private void DownloadGetPipScript(IProgressMonitor progressMonitor)
        {
            using var webClient = TestPipDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 2);
            if (!webClient.DownloadFileAsync(GetPipScriptDownloadUri, GetPipScriptDownloadPath, out var downloadException))
            {
                throw new ToolExecutionException(
                    Resources.PythonInstaller_DownloadPip_Download_failed__Check_your_network_connection_or_contact_Skyline_developers_, downloadException);
            }
        }

        private void RunGetPipScript(IProgressMonitor progressMonitor)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(BasePythonExecutablePath)
                .Append(SPACE)
                .Append(GetPipScriptDownloadPath);
            var cmd = $@"{ECHO} Running command: [{cmdBuilder}] {CMD_PROCEEDING_SYMBOL} " + cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            if (pipedProcessRunner.RunProcess(cmdBuilder.ToString(), false, _writer) != 0)
                throw new ToolExecutionException($@"Failed to execute command: [{cmdBuilder}]");
        }

        private void PipInstall(string pythonExecutablePath, IEnumerable<PythonPackage> packages)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(pythonExecutablePath)
                .Append(SPACE)
                .Append(PYTHON_MODULE_OPTION)
                .Append(SPACE)
                .Append(PIP)
                .Append(SPACE)
                .Append(INSTALL)
                .Append(SPACE);
            foreach (var package in packages)
            {
                var arg = package.Version.IsNullOrEmpty() ? package.Name : package.Name + EQUALS + package.Version;
                cmdBuilder.Append(arg)
                    .Append(TextUtil.SEPARATOR_SPACE);
            }
            var cmd = $@"{ECHO} Running command: [{cmdBuilder}] {CMD_PROCEEDING_SYMBOL} ";
            cmd += $@"{ECHO} This sometimes could take 3-5 minutes. Please be patient. {CMD_PROCEEDING_SYMBOL} ";
            cmd += cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            if (pipedProcessRunner.RunProcess(cmd, false, _writer) != 0)
                throw new ToolExecutionException($@"Failed to execute command: [{cmdBuilder}]");
        }

        private void RunPythonModule(string pythonExecutablePath, string changeDir, string moduleName, string[] arguments)
        {
            var cmdBuilder = new StringBuilder();
            if (changeDir != null)
            {
                cmdBuilder.Append(CD)
                    .Append(SPACE)
                    .Append(changeDir)
                    .Append(SPACE)
                    .Append(CONDITIONAL_CMD_PROCEEDING_SYMBOL)
                    .Append(SPACE);
            }
            cmdBuilder.Append(pythonExecutablePath)
                .Append(SPACE)
                .Append(PYTHON_MODULE_OPTION)
                .Append(SPACE)
                .Append(moduleName)
                .Append(SPACE);
            foreach (var argument in arguments)
            {
                cmdBuilder.Append(argument)
                    .Append(SPACE);
            }
            var cmd = $@"{ECHO} Running command: [{GetEscapedCmdString(cmdBuilder.ToString())}] {CMD_PROCEEDING_SYMBOL} " + cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            if (pipedProcessRunner.RunProcess(cmd, false, _writer) != 0)
                throw new ToolExecutionException($@"Failed to execute command: [{cmdBuilder}]");
        }

        private string GetEscapedCmdString(string cmdString)
        {
            var specialChars = new[] { CMD_PROCEEDING_SYMBOL };
            foreach(var specialChar in specialChars)
            {
                cmdString = cmdString.Replace(specialChar, CMD_ESCAPE_SYMBOL + specialChar);
            }
            return cmdString;
        }


        private bool GetPackages()
        {
            ICollection<string> downloadablePackages = new Collection<string>();
            ICollection<string> localPackages = new Collection<string>();
            AssignPackagesToInstall(ref downloadablePackages, ref localPackages);
            
            IEnumerable<string> packagePaths = null;
            try
            {
                // download packages
                using (var waitDlg = new LongWaitDlg())
                {
                    waitDlg.ProgressValue = 0;
                    waitDlg.PerformWork(this, 500, longWaitBroker => packagePaths = DownloadPackages(longWaitBroker, downloadablePackages));
                }

                // separate packages
                ICollection<string> exePaths = new Collection<string>();
                ICollection<string> sourcePaths = new Collection<string>();
                foreach (var package in (packagePaths == null) ? localPackages : packagePaths.Concat(localPackages))
                {
                    if (package.EndsWith(@".exe"))
                        exePaths.Add(package);
                    else
                        sourcePaths.Add(package);
                }

                // first install executable packages, if any
                if (exePaths.Count != 0)
                {
                    using (var waitDlg = new LongWaitDlg(null, false))
                    {
                        waitDlg.Message = ToolsUIResources.PythonInstaller_GetPackages_Installing_Packages;
                        waitDlg.PerformWork(this, 500, () => InstallExecutablePackages(exePaths));
                    }   
                }

                // then install source paths, if any
                if (sourcePaths.Count != 0)
                {
                    // try and find the path to the pip package manager .exe
                    string pipPath = PythonUtil.GetPipPath(PythonVersion);

                    // if it can't be found, install it
                    if (pipPath == null || TestingPip)
                    {
                        DialogResult result = MultiButtonMsgDlg.Show(
                            this,
                            Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_,
                            ToolsUIResources.PythonInstaller_InstallPackages_Install);
                        if (result == DialogResult.OK && GetPip())
                        {
                            pipPath = PythonUtil.GetPipPath(PythonVersion);
                            MessageDlg.Show(this, Resources.PythonInstaller_InstallPackages_Pip_installation_complete_);
                        }
                        else
                        {
                            MessageDlg.Show(this, Resources.PythonInstaller_InstallPackages_Python_package_installation_cannot_continue__Canceling_tool_installation_);
                            return false;
                        }
                    }

                    using (var waitDlg = new LongWaitDlg(null, false))
                    {
                        waitDlg.Message = ToolsUIResources.PythonInstaller_GetPackages_Installing_Packages;
                        waitDlg.PerformWork(this, 500, () => InstallSourcePackages(sourcePaths, pipPath));
                    }   
                }
                MessageDlg.Show(this, Resources.PythonInstaller_GetPackages_Package_installation_completed_); 
                return true;
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is ToolExecutionException)
                {
                    MessageDlg.ShowException(this, ex);
                    return false;
                }
                throw;
            }
        }

        private void AssignPackagesToInstall(ref ICollection<string> downloadableFiles, ref ICollection<string> localFiles)
        {
            foreach (int index in clboxPackages.CheckedIndices)
            {
                if (index < PackageUris.Count)
                {
                    downloadableFiles.Add(PackageUris[index]);
                }
                else
                {
                    localFiles.Add(LocalPackages[index - PackageUris.Count]);
                }
            }
        }

        private IEnumerable<string> DownloadPackages(IProgressMonitor waitBroker, IEnumerable<string> packagesToDownload)
        {
            ICollection<string> downloadPaths = new Collection<string>();
            ICollection<string> failedDownloads = new Collection<string>();
            List<Exception> downloadExceptions = new List<Exception>();

            using (var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(waitBroker, PackageUris.Count))
            {
                foreach (var package in packagesToDownload)
                {
                    Match file = Regex.Match(package, @"[^/]*$");
                    string downloadPath = Path.GetTempPath() + file;
                    if (webClient.DownloadFileAsync(new Uri(package), downloadPath, out var downloadException))
                    {
                        downloadPaths.Add(downloadPath);
                    }
                    else
                    {
                        failedDownloads.Add(package);
                        if (downloadException != null)
                        {
                            downloadExceptions.Add(downloadException);
                        }
                    }
                }        
            }

            if (failedDownloads.Count != 0)
            {
                Exception cause;
                switch (downloadExceptions.Count)
                {
                    case 0:
                        cause = null;
                        break;
                    case 1:
                        cause = downloadExceptions[0];
                        break;
                    default:
                        cause = new AggregateException(downloadExceptions);
                        break;
                }
                throw new ToolExecutionException(
                        TextUtil.LineSeparate(
                            Resources.PythonInstaller_DownloadPackages_Failed_to_download_the_following_packages_,
                            string.Empty,
                            TextUtil.LineSeparate(failedDownloads),
                            string.Empty,
                            Resources.PythonInstaller_DownloadPython_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_), cause);
            }
            return downloadPaths;
        }

        public void InstallExecutablePackages(IEnumerable<string> packages)
        {
            foreach (var package in packages)
            {
                var processRunner = TestRunProcess ?? new SynchronousRunProcess();
                if (processRunner.RunProcess(new Process { StartInfo = new ProcessStartInfo(package) }) != 0)
                {
                    throw new ToolExecutionException(Resources.PythonInstaller_InstallPackages_Package_Installation_was_not_completed__Canceling_tool_installation_);
                }
            }
        }

        private void InstallSourcePackages(ICollection<string> packages, string pipPath)
        {
            // then install packages from source
            if (packages.Count != 0)
            {
                if (!File.Exists(pipPath) && !TestingPip)
                    throw new ToolExecutionException(Resources.PythonInstaller_InstallPackages_Unknown_error_installing_packages_);

                var argumentBuilder = new StringBuilder(@"echo installing packages");
                foreach (var package in packages)
                {
                    // ReSharper disable LocalizableElement
                    argumentBuilder.Append(" & ")
                                 .Append(pipPath)
                                 .Append(" install ")
                                 .Append("\"")
                                 .Append(package)
                                 .Append("\""); 
                    // ReSharper restore LocalizableElement
                }

                var pipedProcessRunner = TestSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
                try
                {
                    if (pipedProcessRunner.RunProcess(argumentBuilder.ToString(), false, _writer) != 0)
                        throw new ToolExecutionException(Resources.PythonInstaller_InstallPackages_Package_installation_failed__Error_log_output_in_immediate_window_);
                }
                catch (IOException)
                {
                    throw new ToolExecutionException(Resources.PythonInstaller_InstallPackages_Unknown_error_installing_packages_);
                }
            }
        }

        private bool GetPip()
        {   
            try
            {
                using (var dlg = new LongWaitDlg())
                {
                    dlg.ProgressValue = 0;
                    // Short wait, because this can't possible happen fast enough to avoid
                    // showing progress, except in testing
                    dlg.PerformWork(this, 50, DownloadPip);
                }
                using (var dlg = new LongWaitDlg(null, false))
                {
                    dlg.Message = ToolsUIResources.PythonInstaller_GetPip_Installing_Pip;
                    dlg.PerformWork(this, 50, InstallPip);
                }
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is ToolExecutionException)
                {
                    MessageDlg.ShowException(this, ex);
                    return false;
                }
                throw;
            }
            return true;
        }

        private string SetupToolsPath { get; set; }
        private string PipPath { get; set; }

        // Consider: the location of the following python links is assumed to be relatively stable, but could change. We
        // might want to package these scripts with Skyline itself to assure that they are available

        private void DownloadPip(IProgressMonitor longWaitBroker)
        {
            // location of the setuptools install script
            const string setupToolsScript = "https://bitbucket.org/pypa/setuptools/downloads/ez_setup.py";
            SetupToolsPath = Path.GetTempPath() + @"ez_setup.py";

            // location of the pip install script
            const string pipScript = "https://raw.github.com/pypa/pip/master/contrib/get-pip.py";
            PipPath = Path.GetTempPath() + @"get-pip.py";

            using (var webClient = TestPipDownloadClient ?? new MultiFileAsynchronousDownloadClient(longWaitBroker, 2))
            {
                Exception error;
                if (!webClient.DownloadFileAsync(new Uri(setupToolsScript), SetupToolsPath, out error) ||
                    !webClient.DownloadFileAsync(new Uri(pipScript), PipPath, out error))
                {
                    throw new ToolExecutionException(Resources.PythonInstaller_DownloadPip_Download_failed__Check_your_network_connection_or_contact_Skyline_developers_, error);
                }
            }
        }

        private void InstallPip()
        {
            var argumentBuilder = new StringBuilder();
            string pythonPath = PythonUtil.GetProgramPath(PythonVersion);
            argumentBuilder.Append(pythonPath)
                           .Append(TextUtil.SEPARATOR_SPACE)
                           .Append(SetupToolsPath)
                           .Append(@" & ")
                           .Append(pythonPath)
                           .Append(TextUtil.SEPARATOR_SPACE)
                           .Append(PipPath);

            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            try
            {
                if (pipedProcessRunner.RunProcess(argumentBuilder.ToString(), false, _writer) != 0)
                    throw new ToolExecutionException(Resources.PythonInstaller_InstallPip_Pip_installation_failed__Error_log_output_in_immediate_window__);
            }
            catch (IOException)
            {
                throw new ToolExecutionException(Resources.PythonInstaller_InstallPip_Unknown_error_installing_pip_);
            }
        }

        private static string CreateDirIfNotExist(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        #region Functional testing support

        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        public IRunProcess TestRunProcess { get; set; }
        public ISkylineProcessRunnerWrapper TestSkylineProcessRunner { get; set; }
        public IAsynchronousDownloadClient TestPipDownloadClient { get; set; }
        public ISkylineProcessRunnerWrapper TestPipeSkylineProcessRunner { get; set; }
        public IRunProcess TestPipRunProcess { get; set; }
        public bool TestingPip { get; set; }
        
        public string Message => labelMessage.Text;

        public int PackagesListCount => clboxPackages.Items.Count;

        public int CheckedPackagesCount => clboxPackages.CheckedItems.Count;

        public void UncheckAllPackages()
        {
            foreach (int packageIndex in clboxPackages.CheckedIndices)
            {
                clboxPackages.SetItemCheckState(packageIndex, CheckState.Unchecked);
            }
        }

        #endregion
    }

    public static class PythonUtil
    {
        /// <summary>
        /// This property is only used when setting up python with virtual environments.
        /// </summary>
        public static string PythonRootDir {
            get
            {
                var toolsDir = ToolDescriptionHelpers.GetToolsDirectory();
                var dir = toolsDir + @"\" + $@"Python";
                return dir;
            }
        }

        /// <summary>
        /// A utility function to check and see if the specified version of Python is installed
        /// on the user's machine
        /// </summary>
        /// <param name="version">The version to check</param>
        /// <returns>True if the specified version is installed, otherwise false</returns>
        public static bool CheckInstalled(string version)
        {
            return GetPythonKey(version) != null;
        }

        /// <summary>
        /// A utility function to find the program path of the .exe for specified version of Python
        /// </summary>
        /// <param name="version">The version to find</param>
        /// <returns>The install path. If the version is not installed, or the install path cannot be found, 
        /// returns null. </returns>
        public static string GetProgramPath(string version)
        {
            RegistryKey pythonKey = GetPythonKey(version);
            return (pythonKey != null) ? pythonKey.GetValue(null) + (@"python.exe") : null;
        }

        /// <summary>
        /// A utility function to find the pip (Python package installer) executable
        /// </summary>
        /// <param name="version">The version of Python pip is associated with</param>
        /// <returns>The program path of pip.exe if it exists, otherwise null</returns>
        public static string GetPipPath(string version)
        {
            RegistryKey pythonKey = GetPythonKey(version);
            if (pythonKey != null)
            {
                // ReSharper disable LocalizableElement
                string path = pythonKey.GetValue(null) + "scripts\\pip.exe";
                // ReSharper restore LocalizableElement
                return File.Exists(path) ? path : null;
            }
            return null;
        }

        /// <summary>
        /// When downloading a python binary or embedded package from the python ftp, the file name contains
        /// a substring that represents the CPU architecture, e.g. amd64, arm64, win32, etc. This function
        /// figures out the current CPU architecture based on the C# runtime, then maps it to that in
        /// the python package names.
        /// </summary>
        /// <returns></returns>
        public static string GetPythonPackageArchitectureSubstring(Architecture architecture)
        {
            var map = new Dictionary<Architecture, string>
            {
                { Architecture.Arm, null },
                { Architecture.Arm64, @"arm64" },
                { Architecture.X64, @"amd64" },
                { Architecture.X86, @"win32" }
            };
            return map[architecture];
        }

        internal static List<TaskNode> GetPythonVirtualEnvironmentTaskNodes()
        {
            var node1 = new TaskNode { TaskName = TaskName.download_python_embeddable_package, ParentNodes = null };
            var node2 = new TaskNode { TaskName = TaskName.unzip_python_embeddable_package, ParentNodes = new List<TaskNode> { node1 } };
            var node3 = new TaskNode { TaskName = TaskName.enable_search_path_in_python_embeddable_package, ParentNodes = new List<TaskNode> { node2 } };
            var node4 = new TaskNode { TaskName = TaskName.download_getpip_script, ParentNodes = null };
            var node5 = new TaskNode { TaskName = TaskName.run_getpip_script, ParentNodes = new List<TaskNode> { node3, node4 } };
            var node6 = new TaskNode { TaskName = TaskName.pip_install_virtualenv, ParentNodes = new List<TaskNode> { node5 } };
            var node7 = new TaskNode { TaskName = TaskName.create_virtual_environment, ParentNodes = new List<TaskNode> { node6 } };
            var node8 = new TaskNode { TaskName = TaskName.pip_install_packages, ParentNodes = new List<TaskNode> { node7 } };
            return new List<TaskNode> { node1, node2, node3, node4, node5, node6, node7, node8 };
        }

        private const string PYTHON_X64_LOCATION = @"SOFTWARE\Wow6432Node\Python\PythonCore\";
        private const string PYTHON_X86_LOCATION = @"SOFTWARE\Python\PythonCore\";
        private const string INSTALL_DIR = "\\InstallPath";

        // Returns the registry key where the specified version of Python is installed. It does so by opening the
        // registry folder \Python\PythonCore\<version>\InstallPath 
        //
        // When Python is uninstalled, it leaves a registry folder. So if we uninstalled Python 2.7, we would
        // still find the folder \Python\PythonCore\2.7\ . Thus we have to see if the install directory folder
        // remains in order to check installation; documented here: http://bugs.python.org/issue3778
        private static RegistryKey GetPythonKey(string version)
        {
            RegistryKey pythonKey =
                (Registry.LocalMachine.OpenSubKey(PYTHON_X64_LOCATION + FormatVersion(version) + INSTALL_DIR))
                ??
                (Registry.LocalMachine.OpenSubKey(PYTHON_X86_LOCATION + FormatVersion(version) + INSTALL_DIR))
                ??
                (Registry.CurrentUser.OpenSubKey(PYTHON_X86_LOCATION + FormatVersion(version) + INSTALL_DIR));

            return pythonKey;
        }

        // Python stores version info in the registry using only the first two numbers of the version. For example, Python 2.7.5
        // is stored as Python\PythonCore\2.7\ So for checking installation, and the program path, we need to format the version string
        // to look for the base version
        private static string FormatVersion(string version)
        {
            Match versionBase = Regex.Match(version, @"(^[0-9]+\.[0-9]+).*");
            return versionBase.Groups[1].ToString();
        }
    }

    public class PythonPackage
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }

    public enum TaskName
    {
        download_python_embeddable_package,
        unzip_python_embeddable_package,
        enable_search_path_in_python_embeddable_package,
        download_getpip_script,
        run_getpip_script,
        pip_install_virtualenv,
        create_virtual_environment,
        pip_install_packages
    }

    public interface IPythonInstallerTaskValidator
    {
        bool Validate(TaskName taskName, PythonInstaller pythonInstaller);
    }

    public abstract class PythonInstallerTaskValidatorBase : IPythonInstallerTaskValidator
    {   
        public bool Validate(TaskName taskName, PythonInstaller pythonInstaller)
        {
            switch (taskName)
            {
                case TaskName.download_python_embeddable_package:
                    return ValidateDownloadPythonEmbeddablePackage(pythonInstaller);
                case TaskName.unzip_python_embeddable_package:
                    return ValidateUnzipPythonEmbeddablePackage(pythonInstaller);
                case TaskName.enable_search_path_in_python_embeddable_package:
                    return ValidateEnableSearchPathInPythonEmbeddablePackage(pythonInstaller);
                case TaskName.download_getpip_script:
                    return ValidateDownloadGetPipScript(pythonInstaller);
                case TaskName.run_getpip_script:
                    return ValidateRunGetPipScript(pythonInstaller);
                case TaskName.pip_install_virtualenv:
                    return ValidatePipInstallVirtualenv(pythonInstaller);
                case TaskName.create_virtual_environment:
                    return ValidateCreateVirtualEnvironment(pythonInstaller);
                case TaskName.pip_install_packages:
                    return ValidatePipInstallPackages(pythonInstaller);
                default:
                    throw new PythonInstallerUnsupportedTaskNameException(taskName);
            }
        }

        protected abstract bool ValidateDownloadPythonEmbeddablePackage(PythonInstaller pythonInstaller);
        protected abstract bool ValidateUnzipPythonEmbeddablePackage(PythonInstaller pythonInstaller);
        protected abstract bool ValidateEnableSearchPathInPythonEmbeddablePackage(PythonInstaller pythonInstaller);
        protected abstract bool ValidateDownloadGetPipScript(PythonInstaller pythonInstaller);
        protected abstract bool ValidateRunGetPipScript(PythonInstaller pythonInstaller);
        protected abstract bool ValidatePipInstallVirtualenv(PythonInstaller pythonInstaller);
        protected abstract bool ValidateCreateVirtualEnvironment(PythonInstaller pythonInstaller);
        protected abstract bool ValidatePipInstallPackages(PythonInstaller pythonInstaller);
    }

    public class TestPythonInstallerTaskValidator : IPythonInstallerTaskValidator
    {
        private List<TaskNode> TaskNodes => PythonUtil.GetPythonVirtualEnvironmentTaskNodes();
        private Dictionary<TaskName, bool> TaskValidationResult { get; } = new Dictionary<TaskName, bool>
        {
            { TaskName.download_python_embeddable_package, false },
            { TaskName.unzip_python_embeddable_package, false },
            { TaskName.enable_search_path_in_python_embeddable_package, false },
            { TaskName.download_getpip_script, false },
            { TaskName.run_getpip_script, false },
            { TaskName.pip_install_virtualenv, false },
            { TaskName.create_virtual_environment, false },
            { TaskName.pip_install_packages, false },
        };

        public bool Validate(TaskName taskName, PythonInstaller pythonInstaller)
        {
            switch (taskName)
            {
                case TaskName.download_python_embeddable_package:
                    return ValidateDownloadPythonEmbeddablePackage();
                case TaskName.unzip_python_embeddable_package:
                    return ValidateUnzipPythonEmbeddablePackage();
                case TaskName.enable_search_path_in_python_embeddable_package:
                    return ValidateEnableSearchPathInPythonEmbeddablePackage();
                case TaskName.download_getpip_script:
                    return ValidateDownloadGetPipScript();
                case TaskName.run_getpip_script:
                    return ValidateRunGetPipScript();
                case TaskName.pip_install_virtualenv:
                    return ValidatePipInstallVirtualenv();
                case TaskName.create_virtual_environment:
                    return ValidateCreateVirtualEnvironment();
                case TaskName.pip_install_packages:
                    return ValidatePipInstallPackages();
                default:
                    throw new PythonInstallerUnsupportedTaskNameException(taskName);
            }
        }

        public void SetSuccessUntil(TaskName taskName)
        {
            var seenTask = false;
            foreach (var taskNode in TaskNodes)
            {
                if (!seenTask)
                {
                    if (taskNode.TaskName.Equals(taskName))
                    {
                        seenTask = true;
                    }
                    TaskValidationResult[taskNode.TaskName] = true;
                }
                else
                {
                    TaskValidationResult[taskNode.TaskName] = false;
                }
            }
        }

        public void ShowValidationResults()
        {
            Debug.WriteLine($@"IsValidDownloadPythonEmbeddablePackage: {TaskValidationResult[TaskName.download_python_embeddable_package]}");
            Debug.WriteLine($@"IsValidUnzipPythonEmbeddablePackage: {TaskValidationResult[TaskName.unzip_python_embeddable_package]}");
            Debug.WriteLine($@"IsValidEnableSearchPathInPythonEmbeddablePackage: {TaskValidationResult[TaskName.enable_search_path_in_python_embeddable_package]}");
            Debug.WriteLine($@"IsValidDownloadGetPipScript: {TaskValidationResult[TaskName.download_getpip_script]}");
            Debug.WriteLine($@"IsValidRunGetPipScript: {TaskValidationResult[TaskName.run_getpip_script]}");
            Debug.WriteLine($@"IsValidPipInstallVirtualenv: {TaskValidationResult[TaskName.pip_install_virtualenv]}");
            Debug.WriteLine($@"IsValidCreateVirtualEnvironment: {TaskValidationResult[TaskName.create_virtual_environment]}");
            Debug.WriteLine($@"IsValidPipInstallPackages: {TaskValidationResult[TaskName.pip_install_packages]}");
        }

        private bool ValidateDownloadPythonEmbeddablePackage()
        {
            return GetTaskValidationResult(TaskName.download_python_embeddable_package);
        }

        private bool ValidateUnzipPythonEmbeddablePackage()
        {
            return GetTaskValidationResult(TaskName.unzip_python_embeddable_package);
        }

        private bool ValidateEnableSearchPathInPythonEmbeddablePackage()
        {
            return GetTaskValidationResult(TaskName.enable_search_path_in_python_embeddable_package);
        }

        private bool ValidateDownloadGetPipScript()
        {
            return GetTaskValidationResult(TaskName.download_getpip_script);
        }

        private bool ValidateRunGetPipScript()
        {
            return GetTaskValidationResult(TaskName.run_getpip_script);
        }

        private bool ValidatePipInstallVirtualenv()
        {
            return GetTaskValidationResult(TaskName.pip_install_virtualenv);
        }

        private bool ValidateCreateVirtualEnvironment()
        {
            return GetTaskValidationResult(TaskName.create_virtual_environment);
        }

        private bool ValidatePipInstallPackages()
        {
            return GetTaskValidationResult(TaskName.pip_install_packages);
        }

        private bool GetTaskValidationResult(TaskName taskName)
        {
            return TaskValidationResult[taskName];
        }
    }

    public class PythonInstallerTaskValidator : IPythonInstallerTaskValidator
    {
        private const string SCRIPTS = @"Scripts";
        private const string PIP_DOT_EXE = @"pip.exe";
        private const string VIRTUALENV_DOT_EXE = @"virtualenv.exe";
        private const string BACK_SLASH = @"\";
        private const string SPACE = @" ";
        private const string PYTHON_MODULE_OPTION = @"-m";
        private const string PIP = @"pip";
        private const string FREEZE = @"freeze";
        private const string EQUALS = @"==";

        private PythonInstaller PythonInstaller { get; set; }

        public bool Validate(TaskName taskName, PythonInstaller pythonInstaller)
        {
            PythonInstaller = pythonInstaller;
            switch (taskName)
            {
                case TaskName.download_python_embeddable_package:
                    return ValidateDownloadPythonEmbeddablePackage();
                case TaskName.unzip_python_embeddable_package:
                    return ValidateUnzipPythonEmbeddablePackage();
                case TaskName.enable_search_path_in_python_embeddable_package:
                    return ValidateEnableSearchPathInPythonEmbeddablePackage();
                case TaskName.download_getpip_script:
                    return ValidateDownloadGetPipScript();
                case TaskName.run_getpip_script:
                    return ValidateRunGetPipScript();
                case TaskName.pip_install_virtualenv:
                    return ValidatePipInstallVirtualenv();
                case TaskName.create_virtual_environment:
                    return ValidateCreateVirtualEnvironment();
                case TaskName.pip_install_packages:
                    return ValidatePipInstallPackages();
                default:
                    throw new PythonInstallerUnsupportedTaskNameException(taskName);
            }
        }

        private bool ValidateDownloadPythonEmbeddablePackage()
        {
            return File.Exists(PythonInstaller.PythonEmbeddablePackageDownloadPath);
        }

        private bool ValidateUnzipPythonEmbeddablePackage()
        {
            return Directory.Exists(PythonInstaller.PythonEmbeddablePackageExtractDir);
        }

        private bool ValidateEnableSearchPathInPythonEmbeddablePackage()
        {
            if (!Directory.Exists(PythonInstaller.PythonEmbeddablePackageExtractDir))
            {
                return false;
            }
            var disabledPathFiles = Directory.GetFiles(PythonInstaller.PythonEmbeddablePackageExtractDir, "python*._pth");
            var enabledPathFiles = Directory.GetFiles(PythonInstaller.PythonEmbeddablePackageExtractDir, "python*.pth");
            return disabledPathFiles.Length.Equals(0) && enabledPathFiles.Length.Equals(1);
        }

        private bool ValidateDownloadGetPipScript()
        {
            return File.Exists(PythonInstaller.GetPipScriptDownloadPath);
        }

        private bool ValidateRunGetPipScript()
        {
            var filePath = PythonInstaller.PythonEmbeddablePackageExtractDir + BACK_SLASH + SCRIPTS + BACK_SLASH + PIP_DOT_EXE;
            return File.Exists(filePath);
        }

        private bool ValidatePipInstallVirtualenv()
        {
            var filePath = PythonInstaller.PythonEmbeddablePackageExtractDir + BACK_SLASH + SCRIPTS + BACK_SLASH + VIRTUALENV_DOT_EXE;
            return File.Exists(filePath);
        }

        private bool ValidateCreateVirtualEnvironment()
        {
            return Directory.Exists(PythonInstaller.VirtualEnvironmentDir);
        }

        private bool ValidatePipInstallPackages()
        {
            if (!File.Exists(PythonInstaller.VirtualEnvironmentPythonExecutablePath))
            {
                return false;
            }

            var argumentsBuilder = new StringBuilder();
            argumentsBuilder.Append(PYTHON_MODULE_OPTION)
                .Append(SPACE)
                .Append(PIP)
                .Append(SPACE)
                .Append(FREEZE);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = PythonInstaller.VirtualEnvironmentPythonExecutablePath,
                Arguments = argumentsBuilder.ToString(),
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return false;
            }
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            var lines = output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var packageToVersionMap = new Dictionary<string, string>();

            foreach (var line in lines)
            {
                var words = line.Split(EQUALS.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                Assume.IsTrue(words.Length.Equals(2), $@"Failed to parse package name and version from entry [{line}]");
                packageToVersionMap.Add(words[0], words[1]);
            }

            foreach (var package in PythonInstaller.PythonPackages)
            {
                if (!packageToVersionMap.ContainsKey(package.Name))
                {
                    return false;
                }
                if (!package.Version.IsNullOrEmpty() && !packageToVersionMap[package.Name].Equals(package.Version))
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal class TaskNode
    {
        public TaskName TaskName { get; set; } 
        public List<TaskNode> ParentNodes { get; set; }
    }

    internal class Task
    {
        private OneOf<Action, Action<IProgressMonitor>> _action;

        public bool IsActionWithNoArg => _action.IsT0;
        public bool IsActionWithProgressMonitor => _action.IsT1;
        public Action AsActionWithNoArg => _action.AsT0;
        public Action<IProgressMonitor> AsActionWithProgressMonitor => _action.AsT1;

        public Type ActionType
        {
            get
            {
                return _action.Match(
                    t0 => t0.GetType(),
                    t1 => t1.GetType());
            }
        }

        public string InProgressMessage { get; set; }
        public string SuccessMessage { get; set; }
        public string FailureMessage { get; set; }

        public Task(Action action)
        {
            _action = action;
        }

        public Task(Action<IProgressMonitor> action)
        {
            _action = action;
        }
    }

    internal class PythonInstallerException : Exception
    {
        public PythonInstallerException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    internal class PythonInstallerUnsupportedTaskException : PythonInstallerException
    {
        public PythonInstallerUnsupportedTaskException(Task task)
            : base($@"Task with action type {task.ActionType} is not supported by PythonInstaller yet") { }
    }

    internal class PythonInstallerUnsupportedTaskNameException : PythonInstallerException
    {
        public PythonInstallerUnsupportedTaskNameException(TaskName taskName)
            : base($@"Task with task name {taskName} is not supported by PythonInstaller yet") { }
    }
}
