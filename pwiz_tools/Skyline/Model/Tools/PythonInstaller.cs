/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Ionic.Zip;
using JetBrains.Annotations;
using Microsoft.Win32;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Settings = pwiz.Skyline.Properties.Settings;

[assembly: InternalsVisibleTo("TestFunctional")]
[assembly: InternalsVisibleTo("TestUtil")]
[assembly: InternalsVisibleTo("TestPerf")]


namespace pwiz.Skyline.Model.Tools
{
    public class PythonInstaller
    {
        private const string PYTHON = @"Python";
        private const string BOOTSTRAP_PYPA_URL = @"https://bootstrap.pypa.io/";
        private const string CD = @"cd";
        private const string CMD_ESCAPE_SYMBOL = TextUtil.CARET;
        internal const string CMD_PROCEEDING_SYMBOL = TextUtil.AMPERSAND;
        private const string CONDITIONAL_CMD_PROCEEDING_SYMBOL = TextUtil.AMPERSAND + TextUtil.AMPERSAND;
        private const string DOT_ZIP = @".zip";
        private const string DOT_EXE = @".exe";
        internal const string ECHO = @"echo";
        private const string EMBED_LOWER_CASE = @"embed";
        internal const string EQUALS = TextUtil.EQUAL + TextUtil.EQUAL;
        private const string FORWARD_SLASH = TextUtil.FORWARD_SLASH;
        private const string GET_PIP_SCRIPT_FILE_NAME = @"get-pip.py";
        private const string HYPHEN = TextUtil.HYPHEN;
        private const string INSTALL = @"install";
        internal const string PIP = @"pip";
        internal const string FREEZE = @"freeze";
        internal const string AT_SPLITTER = @" @ ";
        private const string PYTHON_EXECUTABLE = @"python.exe";
        private const string PYTHON_FTP_SERVER_URL = @"https://www.python.org/ftp/python/";
        private const string PYTHON_LOWER_CASE = @"python";
        internal const string PYTHON_MODULE_OPTION = @"-m";
        internal const string SCRIPTS = @"Scripts";
        internal const string PIP_EXE = @"pip.exe";
        internal const string VIRTUALENV_EXE = @"virtualenv.exe";
        private const string SPACE = TextUtil.SPACE;
        internal const string VIRTUALENV = @"virtualenv";
        internal const string GIT = @"git";
        internal const string REG_ADD_COMMAND = @"reg add";
        internal const string REG_FILESYSTEM_KEY = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem";
        internal const string REG_LONGPATHS_ENABLED = @"LongPathsEnabled";
        internal const string REG_LONGPATH_TYPE = @"/t REG_DWORD";
        internal const string REG_LONGPATH_VALUE = @"/d 0x00000001";
        internal const string REG_LONGPATH_ZERO = @"/d 0x00000000";
        internal const string REG_LONGPATH_FORCE = @"/f";
        internal static string REG_LONGPATH_NAME = $@"/v {REG_LONGPATHS_ENABLED}";

        private static string CUDA_VERSION = @"12.6.3";
        private static string CUDNN_VERSION = @"9.6.0.74_cuda12";
        //private static readonly string CUDA_INSTALLER_URL = $@"https://developer.download.nvidia.com/compute/cuda/{CUDA_VERSION}/local_installers/";
        private static readonly string CUDA_INSTALLER_URL = $@"https://developer.download.nvidia.com/compute/cuda/{CUDA_VERSION}/network_installers/";

        private static readonly string CUDNN_INSTALLER_URL =
            @"https://developer.download.nvidia.com/compute/cudnn/redist/cudnn/windows-x86_64/";

        private string CUDNN_INSTALLER = $@"cudnn-windows-x86_64-{CUDNN_VERSION}-archive.zip";
        private string CudaVersionDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"cuda", CUDA_VERSION);
        public string CudaInstallerDownloadPath => Path.Combine(CudaVersionDir, CUDA_INSTALLER);

        public string CuDNNVersionDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"cudnn", CUDNN_VERSION);

        public string CuDNNArchive = $@"cudnn-windows-x86_64-{CUDNN_VERSION}-archive";
        public static string CuDNNInstallDir => @"C:\Program Files\NVIDIA\CUDNN\v9.x";
        public string CuDNNInstallerDownloadPath => Path.Combine(CuDNNVersionDir, CUDNN_INSTALLER);

        //private string CUDA_INSTALLER = $@"cuda_{CUDA_VERSION_FULL}_windows.exe";
        private string CUDA_INSTALLER = $@"cuda_{CUDA_VERSION}_windows_network.exe";
        public Uri CudaDownloadUri => new Uri(CUDA_INSTALLER_URL+CUDA_INSTALLER);
        public string CudaDownloadPath => Path.Combine(CudaVersionDir, CudaInstallerDownloadPath);
        public Uri CuDNNDownloadUri => new Uri(CUDNN_INSTALLER_URL + CUDNN_INSTALLER);
        public string CuDNNDownloadPath => Path.Combine(CuDNNVersionDir, CuDNNInstallerDownloadPath);

        public static string InstallNvidiaLibrariesBat =>
            Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), "InstallNvidiaLibraries.bat");
        public static string GetInstallNvidiaLibrariesBat()
        {
            return InstallNvidiaLibrariesBat;
        }
        public bool? NvidiaGpuAvailable
        {
            get
            {
                if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIAHARD || SimulatedInstallationState == eSimulatedInstallationState.NAIVE)
                    return false;
                if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT)  //Also assume we have NVIDIA card when we assume we don't have NVIDIA software
                    return true;

                return TestForNvidiaGPU();
            }

        }

        /// <summary>
        /// Returns a list of Python installation tasks in topological order, with each task as a PythonTaskBase.
        /// </summary>
        internal List<PythonTaskBase> GetPythonTasks(bool? haveNvidiaGpu = false)
        {
            var tasks = new List<PythonTaskBase>
            {
                new DownloadPythonEmbeddablePackageTask(this),
                new UnzipPythonEmbeddablePackageTask(this),
                new EnableSearchPathInPythonEmbeddablePackageTask(this),
                new EnableLongPathsTask(this),
                new DownloadGetPipScriptTask(this),
                new RunGetPipScriptTask(this),
                new PipInstallVirtualEnvTask(this),
                new CreateVirtualEnvironmentTask(this),
                new PipInstallPackagesTask(this),
            };
            if (haveNvidiaGpu == true)
            {
                tasks.Add(new SetupNvidiaLibrariesTask(this));
            }

            return tasks;
        }
 
        public int NumTotalTasks { get; set; }
        public int NumCompletedTasks { get; set; }

        public static ProgramPathContainer PythonPathContainer = new ProgramPathContainer(PYTHON, Settings.Default.PythonEmbeddableVersion);
        public static string PythonVersion
        {
            get => PythonPathContainer.ProgramVersion;
        }

        public List<PythonPackage> PythonPackages { get; }
        public static string PythonEmbeddablePackageFileName => PythonEmbeddablePackageFileBaseName + DOT_ZIP;
        public Uri PythonEmbeddablePackageUri => new Uri(PYTHON_FTP_SERVER_URL + PythonVersion + FORWARD_SLASH + PythonEmbeddablePackageFileName);
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string PythonEmbeddablePackageDownloadPath => Path.Combine(PythonVersionDir, PythonEmbeddablePackageFileName);
        public static string PythonEmbeddablePackageExtractDir => Path.Combine(PythonVersionDir, PythonEmbeddablePackageFileBaseName);
        public Uri GetPipScriptDownloadUri => new Uri(BOOTSTRAP_PYPA_URL + GET_PIP_SCRIPT_FILE_NAME);
        public string GetPipScriptDownloadPath => Path.Combine(PythonVersionDir, GET_PIP_SCRIPT_FILE_NAME);
        public string BasePythonExecutablePath => Path.Combine(PythonEmbeddablePackageExtractDir, PYTHON_EXECUTABLE);
        public string VirtualEnvironmentName { get; }
        public string VirtualEnvironmentDir => Path.Combine(PythonVersionDir, VirtualEnvironmentName);
        public string VirtualEnvironmentPythonExecutablePath => Path.Combine(VirtualEnvironmentDir, SCRIPTS, PYTHON_EXECUTABLE);
        public List<PythonTaskBase> PendingTasks { get; set; }

        public List<PythonTaskBase> PendingPythonTasks { get; set; }

        #region Functional testing support
        public enum eSimulatedInstallationState
        {
            NONE,       // Normal tests systems will have registry set suitably
            NAIVE,      // Simulate system where Python is not installed and Nvidia Hardware not present
            NONVIDIAHARD,    // Simulate Nvidia Hardware not present 
            NONVIDIASOFT     // Simulate Python is installed, Nvidia Hardware present, Nvidia Software not present
        }
        public static eSimulatedInstallationState SimulatedInstallationState { get; set; }
        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        public IAsynchronousDownloadClient TestPipDownloadClient { get; set; }
        public static ISkylineProcessRunnerWrapper TestPipeSkylineProcessRunner { get; set; }
        #endregion
        public static string PythonVersionDir => Path.Combine(PythonRootDir, PythonVersion);
  
        private static string PythonEmbeddablePackageFileBaseName
        {
            get
            {
                var architecture = PythonInstallerUtil.GetPythonPackageArchitectureSubstring(
                    RuntimeInformation.ProcessArchitecture);
                var fileBaseName = string.Join(HYPHEN, new[] { PYTHON_LOWER_CASE, PythonVersion, EMBED_LOWER_CASE, architecture });
                return fileBaseName;
            }
        }
        private static string PythonRootDir { get; } = PythonInstallerUtil.PythonRootDir;
        internal static TextWriter Writer { get; set; }
        public bool HavePythonTasks { get; private set;}
        public bool HaveNvidiaTasks { get; private set; }


        public static bool CudaLibraryInstalled()
        {
            if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIAHARD || 
                SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT ||
                SimulatedInstallationState == eSimulatedInstallationState.NAIVE)
                return false;
            string cudaPath = Environment.GetEnvironmentVariable(@"CUDA_PATH");
            return !string.IsNullOrEmpty(cudaPath);
        }

        public static bool NvidiaLibrariesInstalled()
        {
            if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIAHARD ||
                SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT ||
                SimulatedInstallationState == eSimulatedInstallationState.NAIVE)
                return false;

            bool cudaYes = CudaLibraryInstalled();
            bool? cudnnYes = CuDNNLibraryInstalled();

            return cudaYes && cudnnYes != false;
        }

        public static bool? CuDNNLibraryInstalled()
        {
            if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIAHARD ||
                SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT ||
                SimulatedInstallationState == eSimulatedInstallationState.NAIVE)
                return false;

            string targetDirectory = CuDNNInstallDir + @"\bin";

            if (!Directory.Exists(targetDirectory)) 
                return false;

            string newPath = Environment.GetEnvironmentVariable(@"PATH");

            if (newPath != null && !newPath.Contains(targetDirectory))
            {
                newPath += TextUtil.SEMICOLON + targetDirectory;
                Environment.SetEnvironmentVariable(@"PATH", newPath, EnvironmentVariableTarget.User);
            }

            bool isValid = Directory.Exists(targetDirectory); 
            if (isValid)
            {
                targetDirectory = CuDNNInstallDir + @"\include";
                if (!Directory.Exists(targetDirectory)) 
                    return false;
                
                targetDirectory = CuDNNInstallDir + @"\lib";
                if (!Directory.Exists(targetDirectory)) 
                    return false;

            }
            return isValid;
        }

        public void WriteInstallNvidiaBatScript()
        {
            FileEx.SafeDelete(InstallNvidiaLibrariesBat);
            var type = typeof(PythonInstaller);
            using var stream = type.Assembly.GetManifestResourceStream(type, "InstallNvidiaLibraries-bat.txt");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                string resourceString = reader.ReadToEnd();
                resourceString = resourceString.Replace(@"{{0}}", CUDA_VERSION);
                resourceString = resourceString.Replace(@"{{1}}", CUDNN_VERSION);
                File.WriteAllText(InstallNvidiaLibrariesBat, resourceString);
            }
            else
            {
                string msg = string.Format(ModelResources.NvidiaInstaller_Missing_resource_0_,
                    @"InstallNvidiaLibraries-bat.txt");
                Console.WriteLine(msg);
            }
        }

        public static bool IsRunningElevated()
        {
            // Get current user's Windows identity
            WindowsIdentity identity = WindowsIdentity.GetCurrent();

            // Convert identity to WindowsPrincipal to check for roles
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            // Check if the current user is in the Administrators role
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static bool ValidateEnableLongpaths()
        {
            var task = new EnableLongPathsTask(new PythonInstaller()); 
            bool? valid = task.IsTaskComplete();
            if (valid != null)
            {
                return (bool)valid;
            }
            return false;
        }

        /// <summary>
        /// False means NONVIDIA hardware, true means NVIDIA hardware, null means don't know
        /// </summary>
        /// <returns></returns>
        public static bool? TestForNvidiaGPU()
        {


            bool? nvidiaGpu = null;
            try
            {
                // Query for video controllers using WMI
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_VideoController");
                
                foreach (ManagementObject obj in searcher.Get())
                {
                    //  GPU information
                    nvidiaGpu = obj[@"Name"].ToString().StartsWith(@"NVIDIA");
                    if (nvidiaGpu != false) 
                        break;
                }
            }
            catch (ManagementException e)
            {
                Console.WriteLine(@"An error occurred while querying for WMI data: " + e.Message);
            }
            return nvidiaGpu;
        }

        public PythonInstaller(IEnumerable<PythonPackage> packages,
            TextWriter writer, string virtualEnvironmentName)
        {
            Writer = writer;
            VirtualEnvironmentName = virtualEnvironmentName;
            PendingTasks = new List<PythonTaskBase>();
            Directory.CreateDirectory(PythonRootDir);
            Directory.CreateDirectory(PythonVersionDir);
            PythonPackages = packages.ToList();

            if (NvidiaGpuAvailable == true)
            {
                PythonPackages.Add(new PythonPackage { Name = @"wheel", Version = null });
                PythonPackages.Add(new PythonPackage { Name = @"nvidia-cudnn-cu12", Version = null });
                PythonPackages.Add(new PythonPackage { Name = @"torch --extra-index-url https://download.pytorch.org/whl/cu118 --upgrade", Version = null });
            }

        }

        private PythonInstaller()
        {
        }

        public bool IsNvidiaEnvironmentReady(List<PythonTaskBase> abortedTasks = null)
        {
            if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIAHARD)
                return true;

            if (abortedTasks == null && SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT)
                return false;

            if (PendingTasks.IsNullOrEmpty())
                ValidatePythonVirtualEnvironment();

            if (abortedTasks != null && abortedTasks.Count > 0)
                return !abortedTasks.Any(task => task.IsNvidiaTask);
            return true;
        }

        public bool IsPythonVirtualEnvironmentReady(List<PythonTaskBase> abortedTasks = null)
        {
            if (SimulatedInstallationState == eSimulatedInstallationState.NAIVE)
                return false;
            if (abortedTasks == null && SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT)
                return true;

            var tasks = PendingTasks.IsNullOrEmpty() ? ValidatePythonVirtualEnvironment() : PendingTasks;

            if (abortedTasks != null && abortedTasks.Count > 0) 
                tasks = abortedTasks;

            if (NumTotalTasks == NumCompletedTasks && NumCompletedTasks > 0)
                return true;

            return !tasks.Any(task => task.IsRequiredForPythonEnvironment);
        }

        public void ClearPendingTasks() 
        {
            PendingTasks.Clear();
        }

        public void CheckPendingTasks()
        {
            var tasks = PendingTasks;
            HavePythonTasks = false;
            HaveNvidiaTasks = false;

            if (tasks.Any(task => task.IsRequiredForPythonEnvironment))
            {
                HavePythonTasks = true;
            }

            if (tasks.Any(task => task.IsNvidiaTask))
            {
                HaveNvidiaTasks = true;
            }
        }


        public List<PythonTaskBase> ValidatePythonVirtualEnvironment()
        {
            var pendingTasks = new List<PythonTaskBase>();
            var allTasks = GetPythonTasks(this.NvidiaGpuAvailable);

            var hasSeenFailure = false;
            foreach (var task in allTasks)
            {
                bool? isTaskValid = task.IsTaskComplete();

                if (hasSeenFailure)
                {
                    if ( (isTaskValid == true || isTaskValid == null) && null == task.ParentTask)  
                        continue; 
                   
                    bool havePrerequisite = false;

                    if (task.ParentTask == null)
                    {
                        pendingTasks.Add(task);
                        continue;
                    }
                    else
                    {
                        var prereqTask = task.ParentTask;
                        while (prereqTask != null)
                        {
                            if (pendingTasks.Count > 0 && pendingTasks.Where(p => p.TaskName == prereqTask.TaskName).ToArray().Length > 0)
                            {
                                havePrerequisite = true;
                                break;
                            }
                            prereqTask = prereqTask.ParentTask;
                        }

                    }

                    if (!havePrerequisite) 
                        continue; 
                    
                }
                else
                {
                    if (isTaskValid == true || isTaskValid == null) 
                        continue; 
                    hasSeenFailure = true;
                }
                pendingTasks.Add(task);
            }
            PendingTasks = pendingTasks;
            return pendingTasks;
        }

        public void EnableWindowsLongPaths(ILongWaitBroker broker = null)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(REG_ADD_COMMAND)
                .Append(SPACE)
                .Append(TextUtil.Quote(REG_FILESYSTEM_KEY))
                .Append(SPACE)
                .Append(REG_LONGPATH_NAME)
                .Append(SPACE)
                .Append(REG_LONGPATH_TYPE)
                .Append(SPACE)
                .Append(REG_LONGPATH_VALUE)
                .Append(SPACE)
                .Append(REG_LONGPATH_FORCE);

            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, cmdBuilder, CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;

            var processRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();

            CancellationToken cancelToken = CancellationToken.None;
            if (broker != null) cancelToken = broker.CancellationToken;

            if (processRunner.RunProcess(cmd, true, Writer, false, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));

        }
        public static void DisableWindowsLongPaths(ILongWaitBroker broker = null)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(REG_ADD_COMMAND)
                .Append(SPACE)
                .Append(TextUtil.Quote(REG_FILESYSTEM_KEY))
                .Append(SPACE)
                .Append(REG_LONGPATH_NAME)
                .Append(SPACE)
                .Append(REG_LONGPATH_TYPE)
                .Append(SPACE)
                .Append(REG_LONGPATH_ZERO)
                .Append(SPACE)
                .Append(REG_LONGPATH_FORCE);

            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, cmdBuilder, CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;

            var processRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();

            CancellationToken cancelToken = CancellationToken.None;
            if (broker != null) cancelToken = broker.CancellationToken;


            if (processRunner.RunProcess(cmd, true, Writer, true, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));

        }

        internal void PipInstall(string pythonExecutablePath, IEnumerable<PythonPackage> packages, ILongWaitBroker broker = null)
        {
            var cmdBuilder = new StringBuilder();
            CancellationToken cancelToken = CancellationToken.None;

            try
            {
                foreach (var package in packages)
                {
                    string arg;
                    cmdBuilder.Clear();
                    cmdBuilder.Append(pythonExecutablePath)
                        .Append(SPACE)
                        .Append(PYTHON_MODULE_OPTION)
                        .Append(SPACE)
                        .Append(PIP)
                        .Append(SPACE)
                        .Append(INSTALL)
                        .Append(SPACE);
                    if (package.Version.IsNullOrEmpty())
                    {
                        arg = package.Name;
                    }
                    else if (package.Version.StartsWith(GIT))
                    {
                        arg = package.Version;
                        arg = TextUtil.Quote(arg);
                    }
                    else
                    {
                        arg = package.Name + EQUALS + package.Version;
                        arg = TextUtil.Quote(arg);
                    }


                    cmdBuilder.Append(arg).Append(SPACE);

                    var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO,
                        cmdBuilder, CMD_PROCEEDING_SYMBOL);
                    cmd += string.Format(
                        ToolsResources
                            .PythonInstaller_PipInstall__0__This_sometimes_could_take_3_5_minutes__Please_be_patient___1__,
                        ECHO, CMD_PROCEEDING_SYMBOL);
                    cmd += cmdBuilder;
                    var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();

                    if (broker != null) cancelToken = broker.CancellationToken;
                    if (pipedProcessRunner.RunProcess(cmd, false, Writer, true, cancelToken) != 0)
                    {
                        throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__,
                            cmdBuilder));
    
                    }

                    if (cancelToken.IsCancellationRequested)
                        break;
                }
            }
            catch 
            {
                return;
            }

            var filePath = Path.Combine(PythonEmbeddablePackageExtractDir, SCRIPTS, VIRTUALENV);
            PythonInstallerUtil.SignFile(filePath+DOT_EXE);
            PythonInstallerUtil.SignDirectory(VirtualEnvironmentDir);

        }

        internal void RunPythonModule(string pythonExecutablePath, string changeDir, string moduleName, string[] arguments, ILongWaitBroker broker = null)
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
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, GetEscapedCmdString(cmdBuilder.ToString()), CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            CancellationToken cancelToken = CancellationToken.None;
            if (broker != null) cancelToken = broker.CancellationToken;
            if (pipedProcessRunner.RunProcess(cmd, false, Writer, true, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));
            
            PythonInstallerUtil.SignDirectory(PythonEmbeddablePackageExtractDir);

        }

        private string GetEscapedCmdString(string cmdString)
        {
            var specialChars = new[] { CMD_PROCEEDING_SYMBOL };
            foreach (var specialChar in specialChars)
            {
                cmdString = cmdString.Replace(specialChar, CMD_ESCAPE_SYMBOL + specialChar);
            }
            return cmdString;
        }

        public void CleanUpPythonEnvironment(string name)
        {
            if (!PythonInstallerUtil.IsSignedFileOrDirectory(CudaVersionDir))
                DirectoryEx.SafeDeleteLongPath(CudaVersionDir);
            if (!PythonInstallerUtil.IsSignedFileOrDirectory(CuDNNVersionDir))
                DirectoryEx.SafeDeleteLongPath(CuDNNVersionDir);
            if (!PythonInstallerUtil.IsSignedFileOrDirectory(Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), name))) 
                DirectoryEx.SafeDeleteLongPath(Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), name));
        }

        public static bool DeleteToolsPythonDirectory()
        {
            DirectoryEx.SafeDeleteLongPath(Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"Python"));
            if (Directory.Exists(Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"Python"))) return false;
            return true;
        }

        public void SignPythonEnvironment(string name)
        {
            PythonInstallerUtil.SignDirectory(CudaVersionDir);
            PythonInstallerUtil.SignDirectory(CuDNNVersionDir);
            PythonInstallerUtil.SignDirectory(Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), name));
        }
    }

    public static class PythonInstallerUtil
    {
        private const string PYTHON = @"Python";
        private const string SCRIPTS = @"Scripts";
        private const string PYTHON_EXECUTABLE = @"python.exe";
        private const string ACTIVATE_SCRIPT_FILE_NAME = @"activate.bat";
        private const string SIGNATURE_EXTENSION = ".skysign";
        /// <summary>
        /// This directory is used to store python executables and virtual environments.
        /// </summary>
        public static string PythonRootDir
        {
            get
            {
                var toolsDir = ToolDescriptionHelpers.GetToolsDirectory();
                var dir = toolsDir + @"\" + PYTHON;
                return dir;
            }
        }

        /// <summary>
        /// When downloading a python binary or embedded package from the python ftp, the file name contains
        /// a substring that represents the CPU architecture, e.g. amd64, arm64, win32, etc. This function
        /// figures out the current CPU architecture based on the C# runtime, then maps it to that in
        /// the python package names.
        /// </summary>
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


        /// <summary>
        /// Get python virtual environment directory.
        /// </summary>
        /// <param name="pythonVersion"></param>
        /// <param name="virtualEnvironmentName"></param>
        /// <returns></returns>
        public static string GetPythonVirtualEnvironmentDir(string pythonVersion, string virtualEnvironmentName)
        {
            return Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), PYTHON, pythonVersion,
                virtualEnvironmentName);
        }

        public static string GetPythonVirtualEnvironmentScriptsDir(string pythonVersion, string virtualEnvironmentName)
        {
            return Path.Combine(GetPythonVirtualEnvironmentDir(pythonVersion, virtualEnvironmentName), SCRIPTS);
        }

        /// <summary>
        /// Get Python executable path.
        /// </summary>
        /// <param name="pythonVersion"></param>
        /// <param name="virtualEnvironmentName"></param>
        /// <returns></returns>
        public static string GetPythonExecutablePath(string pythonVersion, string virtualEnvironmentName)
        {
            return Path.Combine(GetPythonVirtualEnvironmentScriptsDir(pythonVersion, virtualEnvironmentName),
                PYTHON_EXECUTABLE);
        }

        /// <summary>
        /// Get Python virtual environment activate.bat script path.
        /// </summary>
        /// <param name="pythonVersion"></param>
        /// <param name="virtualEnvironmentName"></param>
        /// <returns></returns>
        public static string GetPythonVirtualEnvironmentActivationScriptPath(string pythonVersion, string virtualEnvironmentName)
        {
            return Path.Combine(GetPythonVirtualEnvironmentScriptsDir(pythonVersion, virtualEnvironmentName),
                ACTIVATE_SCRIPT_FILE_NAME);
        }

        public static void CopyFiles(string fromDirectory, string toDirectory, string pattern = @"*")
        {
            // Ensure the target directory exists
            Directory.CreateDirectory(toDirectory);

            // Get all files matching the pattern
            string[] files = Directory.GetFiles(fromDirectory, pattern);

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(toDirectory, fileName);
                File.Copy(file, destFile, true); // true for overwrite existing file
            }
        }
        public static void CopyFilesElevated(string fromDirectory, string toDirectory, string pattern, ISkylineProcessRunnerWrapper TestPipeSkylineProcessRunner, TextWriter Writer, ILongWaitBroker broker = null)
        {
            if (pattern.IsNullOrEmpty()) pattern = @"*";

            if (!Directory.Exists(toDirectory))
            {
                string[] destination = { toDirectory };
                CreateDirectoriesElevated(destination, TestPipeSkylineProcessRunner, Writer);
            }

            string[] files = Directory.GetFiles(fromDirectory, pattern);
            CancellationToken cancelToken = CancellationToken.None;
            if (broker != null) cancelToken = broker.CancellationToken;

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var command = $@"copy {TextUtil.Quote(file)} {TextUtil.Quote(toDirectory)} /y ";
                var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, @"echo", command, TextUtil.AMPERSAND);
                var processRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
                cmd += command;
                if (processRunner.RunProcess(cmd, true, Writer, true, cancelToken) != 0)
                    throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, command));
                if (cancelToken.IsCancellationRequested) break;
            }
        }
        public static void CreateDirectoriesElevated(string [] directories, ISkylineProcessRunnerWrapper TestPipeSkylineProcessRunner, TextWriter Writer, ILongWaitBroker broker = null)
        {
            CancellationToken cancelToken = CancellationToken.None;
            if (broker != null) cancelToken = broker.CancellationToken;
            for (int i = 0; i < directories.Length; i++) 
            {
               var directory = directories[i];
               if (Directory.Exists(directory)) 
                   continue;
               var command = $@"mkdir {TextUtil.Quote(directory)}";
               var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, @"echo", command, TextUtil.AMPERSAND);
               var processRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
               cmd += command;
               if (processRunner.RunProcess(cmd, true, Writer, true, cancelToken) != 0)
                   throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, command));
               if (cancelToken.IsCancellationRequested) break;
            }
        }
        public static string GetFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = { };
                string fullPath = Path.GetFullPath(filePath);

                RetryAction(() =>
                {
                    using var stream = File.OpenRead($@"\\?\{fullPath}");
                    hash = sha256.ComputeHash(stream);
                });

                if (!hash.IsNullOrEmpty())
                    return BitConverter.ToString(hash).Replace(@"-", "").ToLowerInvariant();

                return @"F00F00F00";
            }
        }
        public static string GetMD5FileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = { };
                string fullPath = Path.GetFullPath(filePath);

                RetryAction(() =>
                {
                    using var stream = File.OpenRead($@"\\?\{fullPath}");
                    hash = md5.ComputeHash(stream);
                });

                if (!hash.IsNullOrEmpty())
                    return BitConverter.ToString(hash).Replace(@"-", "").ToLowerInvariant();

                return @"F00F00F00";
            }
        }

        public static void RetryAction([InstantHandle] Action act, int maxRetries = 100)
        {
            try
            {
                Helpers.TryTwice(act, maxRetries);
            }
            catch (IOException)
            {

            }
        }

        public static bool IsRunningElevated()
        {
            // Get current user's Windows identity
            WindowsIdentity identity = WindowsIdentity.GetCurrent();

            // Convert identity to WindowsPrincipal to check for roles
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            // Check if the current user is in the Administrators role
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Return false if skysign file doesn't exist or it exists and is invalid, return true is skysign file matches provided signature,
        /// return null if signature provided is null or when signature is not null, but user not running as administrator
        /// </summary>
        /// <param name="path">path to file or directory</param>
        /// <param name="signature">key</param>
        /// <returns></returns>
        public static bool? IsSignatureValid(string path, string signature)
        {
            string filePath = Path.GetFullPath(path) + SIGNATURE_EXTENSION;
            if (!IsSignedFileOrDirectory(path))
                return false;
            if (signature.IsNullOrEmpty())
                return null;
            if (IsRunningElevated())
                return signature == File.ReadAllText(filePath);
            else
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    var stream = fileInfo.OpenRead();
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
                if (signature != File.ReadAllText(filePath))
                {
                    File.Delete(filePath);
                    return false;
                }
                return true;
            }

        }
        public static bool IsSignedFileOrDirectory(string path)
        {
            return File.Exists(Path.GetFullPath(path) + SIGNATURE_EXTENSION);
        }
        public static void SignFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            string signatureFile = Path.GetFullPath(filePath) + SIGNATURE_EXTENSION;
            using (FileSaver file = new FileSaver(signatureFile))
            {
                File.WriteAllText(file.SafeName, GetFileHash(filePath));
                RetryAction(() => { file.Commit(); });
            }
        }
        public static void SignDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;

            string signatureFile = Path.GetFullPath(dirPath) + SIGNATURE_EXTENSION;
            using (FileSaver file = new FileSaver(signatureFile))
            {
                File.WriteAllText(file.SafeName, GetDirectoryHash(dirPath));
                RetryAction(() => { file.Commit(); });
            }
        }
        public static string GetDirectoryHash(string directoryPath)
        {
            string[] filesArray = Directory.GetFiles(directoryPath, @"*", SearchOption.AllDirectories);
            return GetFilesArrayHash(filesArray);
        }
        public static string GetFilesArrayHash(string[] filesArray, int maxFilesToCheck = 1000)
        {
            // Use SHA256 for hashing
            using (var sha = SHA256.Create())
            {
                // Create a new hash for all files combined
                using (var combinedStream = new MemoryStream())
                {
                    Array.Sort(filesArray); // Ensure consistent order
                    int fileCount = 0;
                    for (fileCount = 0; fileCount < Math.Min(filesArray.Length, maxFilesToCheck); fileCount++)
                    {
                        //Sometimes the file is locked by another process so we retry up to 100 times
                        RetryAction(() =>
                        {
                            using var fileStream = new FileStream($@"\\?\{filesArray[fileCount]}", FileMode.Open);
                            // Copy file contents to the combined stream
                            fileStream.CopyTo(combinedStream);

                            // Add a separator or file name to differentiate between files
                            var separator = Encoding.UTF8.GetBytes(Path.GetFileName(filesArray[fileCount]) ?? string.Empty);
                            combinedStream.Write(separator, 0, separator.Length);
                        });
                    }

                    combinedStream.Seek(0, SeekOrigin.Begin); // Reset stream position
                    // Compute hash of combined stream
                    var hashBytes = sha.ComputeHash(combinedStream);
                    return BitConverter.ToString(hashBytes).Replace(@"-", @"").ToLower();
                }
            }
        }
    }

    public interface IPythonInstallerTaskValidator
    {
        bool? Validate(PythonTaskBase python, PythonInstaller pythonInstaller);
    }

    public enum PythonTaskName
    {
        download_python_embeddable_package,
        unzip_python_embeddable_package,
        enable_search_path_in_python_embeddable_package,
        enable_longpaths,
        download_getpip_script,
        run_getpip_script,
        pip_install_virtualenv,
        create_virtual_environment,
        pip_install_packages,
        download_cuda_library,
        install_cuda_library,
        download_cudnn_library,
        install_cudnn_library,
        setup_nvidia_libraries          //Write batch script and execute it with elevation
    }

    public class PythonPackage
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }
    internal class PythonInstallerException : Exception
    {
        public PythonInstallerException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    internal class PythonInstallerUnsupportedTaskException : PythonInstallerException
    {
        public PythonInstallerUnsupportedTaskException(PythonTaskBase pythonTask)
            : base(string.Format(ToolsResources.PythonInstallerUnsupportedTaskException_Task_with_action_type__0__is_not_supported_by_PythonInstaller_yet, pythonTask.TaskName)) { }
    }

    internal class PythonInstallerUnsupportedTaskNameException : PythonInstallerException
    {
        public PythonInstallerUnsupportedTaskNameException(PythonTaskName pythonTaskName)
            : base(string.Format(ToolsResources.PythonInstallerUnsupportedTaskNameException_Task_with_task_name__0__is_not_supported_by_PythonInstaller_yet, pythonTaskName)) { }
    }

    public abstract class PythonTaskBase
    {
        public PythonInstaller PythonInstaller { get; }
        public PythonTaskName TaskName { get; set; }
        public abstract string InProgressMessage();
        public abstract string SuccessMessage();
        public abstract string FailureMessage();

        public PythonTaskBase ParentTask { get; set; }

        public PythonTaskBase(PythonInstaller pythonInstaller, PythonTaskName name, PythonTaskBase parentTask = null)
        {
            PythonInstaller = pythonInstaller;
            TaskName = name;
            ParentTask = parentTask;
        }
        /// <summary>
        /// Checks for presence of hashes or expected files and/or directories signifying completion of tasks
        /// </summary>

        public abstract bool? IsTaskComplete();
        public abstract void DoAction(ILongWaitBroker broker);
        public virtual bool IsRequiredForPythonEnvironment
        {
            get { return true; }
        }

        public virtual bool IsNvidiaTask
        {
            get { return false;  }
        }

        public virtual bool IsEnableLongPathsTask
        {
            get { return false; }
        }
    }

    public class DownloadPythonEmbeddablePackageTask : PythonTaskBase
    {
        private string _storedHash = Settings.Default.PythonEmbeddableHash;
        public DownloadPythonEmbeddablePackageTask(PythonInstaller installer) : base(installer, PythonTaskName.download_python_embeddable_package)
        {
        }

        public override string InProgressMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Downloading_Python_embeddable_package;
        }

        public override string FailureMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_Python_embeddable_package;
        }
        public override string SuccessMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Successfully_downloaded_Python_embeddable_package;
        }

        public override bool? IsTaskComplete()
        {
            var pythonFilePath = PythonInstaller.PythonEmbeddablePackageDownloadPath;
            if (!File.Exists(pythonFilePath))
                return false;

            return _storedHash == PythonInstallerUtil.GetMD5FileHash(pythonFilePath);
        }
        public override void DoAction(ILongWaitBroker broker)
        {
            var progressWaitBroker = new ProgressWaitBroker(DoActionWithProgressMonitor);
            progressWaitBroker.PerformWork(broker);
        }

        private void DoActionWithProgressMonitor(IProgressMonitor progressMonitor)
        {
            using var webClient = PythonInstaller.TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);
            using var fileSaver = new FileSaver(PythonInstaller.PythonEmbeddablePackageDownloadPath);
            
             if (!webClient.DownloadFileAsync(PythonInstaller.PythonEmbeddablePackageUri, fileSaver.SafeName,
                    out var downloadException))
                throw new ToolExecutionException(
                    ToolsResources
                        .PythonInstaller_Download_failed__Check_your_network_connection_or_contact_Skyline_team_for_help_,
                    downloadException);

            fileSaver.Commit();
        }
    }

    public class UnzipPythonEmbeddablePackageTask : PythonTaskBase
    {
        public UnzipPythonEmbeddablePackageTask(PythonInstaller installer) : base(installer, PythonTaskName.unzip_python_embeddable_package, new DownloadPythonEmbeddablePackageTask(installer))
        {
        }

        public override string InProgressMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Unzipping_Python_embeddable_package;
        }

        public override string FailureMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Failed_to_unzip_Python_embeddable_package;
        }

        public override string SuccessMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Successfully_unzipped_Python_embeddable_package;
        }
        public override bool? IsTaskComplete()
        {
            if (!Directory.Exists(PythonInstaller.PythonEmbeddablePackageExtractDir))
                return false;

           
            return PythonInstallerUtil.IsSignedFileOrDirectory(PythonInstaller.PythonEmbeddablePackageExtractDir);
        }
        public override void DoAction(ILongWaitBroker broker)
        {
            var progressWaitBroker = new ProgressWaitBroker(DoActionWithProgressMonitor);
            progressWaitBroker.PerformWork(broker);
        }

        private void DoActionWithProgressMonitor(IProgressMonitor progressMonitor)
        {
            using var zipFile = ZipFile.Read(PythonInstaller.PythonEmbeddablePackageDownloadPath);
            DirectoryEx.SafeDeleteLongPath(PythonInstaller.PythonEmbeddablePackageExtractDir);
            zipFile.ExtractAll(PythonInstaller.PythonEmbeddablePackageExtractDir);
        }
    }

    public class EnableSearchPathInPythonEmbeddablePackageTask : PythonTaskBase
    {
        private string _storedHash = Settings.Default.SearchPathInPythonEmbeddableHash;
        public EnableSearchPathInPythonEmbeddablePackageTask(PythonInstaller installer) : base(installer, PythonTaskName.unzip_python_embeddable_package, new UnzipPythonEmbeddablePackageTask(installer))
        {
        }

        public override string InProgressMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Enabling_search_path_in_Python_embeddable_package;
        }

        public override string FailureMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Failed_to_enable_search_path_in_Python_embeddable_package;
        }

        public override string SuccessMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Successfully_enabled_search_path_in_Python_embeddable_package;
        }

        public override bool? IsTaskComplete()
        {
            if (!Directory.Exists(PythonInstaller.PythonEmbeddablePackageExtractDir))
                return false;

            var computeHash = PythonInstallerUtil.GetFilesArrayHash(Directory.GetFiles(PythonInstaller.PythonEmbeddablePackageExtractDir, @"python*.pth"));
            return computeHash == _storedHash;
        }
        public override void DoAction(ILongWaitBroker broker)
        {
            var files = Directory.GetFiles(PythonInstaller.PythonEmbeddablePackageExtractDir, @"python*._pth");
            Assume.IsTrue(files.Length == 1,
                ToolsResources
                    .PythonInstaller_EnableSearchPathInPythonEmbeddablePackage_Found_0_or_more_than_one_files_with__pth_extension__this_is_unexpected);
            var oldFilePath = files[0];
            var newFilePath = Path.ChangeExtension(oldFilePath, @".pth");
            File.Move(oldFilePath, newFilePath);
        }

    }

    public class EnableLongPathsTask : PythonTaskBase
    {
        public EnableLongPathsTask(PythonInstaller installer) : base(installer, PythonTaskName.enable_longpaths)
        {
        }

        public override string InProgressMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Enable_Long_Paths_For_Python_packages_in_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);
        }

        public override string FailureMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_enable_long_paths_Python_packages_in_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);
        }

        public override string SuccessMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Successfully_enabled_long_paths_Python_packages_in_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);
        }

        public override bool? IsTaskComplete()
        {
            return PythonInstaller.SimulatedInstallationState != PythonInstaller.eSimulatedInstallationState.NAIVE &&
                   (int)Registry.GetValue(PythonInstaller.REG_FILESYSTEM_KEY, PythonInstaller.REG_LONGPATHS_ENABLED, 0) == 1;
        }
        public override void DoAction(ILongWaitBroker broker)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(PythonInstaller.REG_ADD_COMMAND)
                .Append(TextUtil.SPACE)
                .Append(TextUtil.Quote(PythonInstaller.REG_FILESYSTEM_KEY))
                .Append(TextUtil.SPACE)
                .Append(PythonInstaller.REG_LONGPATH_NAME)
                .Append(TextUtil.SPACE)
                .Append(PythonInstaller.REG_LONGPATH_TYPE)
                .Append(TextUtil.SPACE)
                .Append(PythonInstaller.REG_LONGPATH_VALUE)
                .Append(TextUtil.SPACE)
                .Append(PythonInstaller.REG_LONGPATH_FORCE);

            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, PythonInstaller.ECHO, cmdBuilder, PythonInstaller.CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;

            var processRunner = PythonInstaller.TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();

            CancellationToken cancelToken = CancellationToken.None;
            if (broker != null) 
                cancelToken = broker.CancellationToken;

            if (processRunner.RunProcess(cmd, true, PythonInstaller.Writer, false, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));
        }

        private void DoActionWithProgressMonitor(IProgressMonitor progressMonitor)
        {
            throw new NotImplementedException();
        }

        public override bool IsEnableLongPathsTask
        {
            get { return true; }
        }
    }

    public class DownloadGetPipScriptTask : PythonTaskBase
    {
        public DownloadGetPipScriptTask(PythonInstaller installer) : base(installer, PythonTaskName.download_getpip_script, new EnableSearchPathInPythonEmbeddablePackageTask(installer))
        {
        }

        public override string InProgressMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Downloading_the_get_pip_py_script;
        }

        public override string FailureMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_the_get_pip_py_script;
        }
        public override string SuccessMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Successfully_downloaded_the_get_pip_py_script;
        }

        public override bool? IsTaskComplete()
        {
            var filePath = PythonInstaller.GetPipScriptDownloadPath;

            if (!File.Exists(filePath))
                return false;

            return
                PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));

        }
        public override void DoAction(ILongWaitBroker broker)
        {
            var progressWaitBroker = new ProgressWaitBroker(DoActionWithProgressMonitor);
            progressWaitBroker.PerformWork(broker);
        }

        private void DoActionWithProgressMonitor(IProgressMonitor progressMonitor)
        {
            using var webClient = PythonInstaller.TestPipDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);
            if (!webClient.DownloadFileAsync(PythonInstaller.GetPipScriptDownloadUri, PythonInstaller.GetPipScriptDownloadPath, out var downloadException))
            {
                throw new ToolExecutionException(
                    ToolsResources.PythonInstaller_Download_failed__Check_your_network_connection_or_contact_Skyline_team_for_help_, downloadException);
            }
            PythonInstallerUtil.SignFile(PythonInstaller.GetPipScriptDownloadPath);
        }
    }

    public class RunGetPipScriptTask : PythonTaskBase
    {
        public RunGetPipScriptTask(PythonInstaller installer) : base(installer, PythonTaskName.download_python_embeddable_package, new DownloadGetPipScriptTask(installer))
        {
        }

        public override string InProgressMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Running_the_get_pip_py_script;
        }

        public override string FailureMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Failed_to_run_the_get_pip_py_script;
        }

        public override string SuccessMessage()
        {
            return ToolsResources.PythonInstaller_GetPythonTask_Successfully_ran_the_get_pip_py_script; 
        }

        public override bool? IsTaskComplete()
        {
            var filePath = Path.Combine(PythonInstaller.PythonEmbeddablePackageExtractDir, PythonInstaller.SCRIPTS, PythonInstaller.PIP_EXE);

            if (!File.Exists(filePath))
                return false;

            return
                PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));

        }
        public override void DoAction(ILongWaitBroker broker)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(PythonInstaller.BasePythonExecutablePath)
                .Append(TextUtil.SPACE)
                .Append(PythonInstaller.GetPipScriptDownloadPath);
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, PythonInstaller.ECHO, cmdBuilder, PythonInstaller.CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;
            var pipedProcessRunner = PythonInstaller.TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            CancellationToken cancelToken = CancellationToken.None;
            if (broker != null) 
                cancelToken = broker.CancellationToken;

            if (pipedProcessRunner.RunProcess(cmd, false, PythonInstaller.Writer, true, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));

            var filePath = Path.Combine(PythonInstaller.PythonEmbeddablePackageExtractDir, PythonInstaller.SCRIPTS, PythonInstaller.PIP_EXE);
            PythonInstallerUtil.SignFile(filePath);

        }

        private void DoActionWithProgressMonitor(IProgressMonitor progressMonitor)
        {
            throw new NotImplementedException();
        }
    }
    public class PipInstallVirtualEnvTask : PythonTaskBase
    {

        public PipInstallVirtualEnvTask(PythonInstaller installer) : base(installer, PythonTaskName.download_python_embeddable_package, new RunGetPipScriptTask(installer))
        {
        }

        public override string InProgressMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Running_pip_install__0_, PythonInstaller.VIRTUALENV);
        }

        public override string FailureMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_run_pip_install__0_, PythonInstaller.VIRTUALENV);
        }

        public override string SuccessMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Successfully_ran_pip_install__0_, PythonInstaller.VIRTUALENV);
        }

        public override bool? IsTaskComplete()
        {
            var filePath = Path.Combine(PythonInstaller.PythonEmbeddablePackageExtractDir, PythonInstaller.SCRIPTS, PythonInstaller.VIRTUALENV_EXE);

            if (!File.Exists(filePath))
                return false;

            return
                PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));

        }
        public override void DoAction(ILongWaitBroker broker)
        {
            var virtualEnvPackage = new PythonPackage { Name = PythonInstaller.VIRTUALENV, Version = null };
            string pythonExecutablePath = PythonInstaller.BasePythonExecutablePath;
            PythonInstaller.PipInstall(PythonInstaller.BasePythonExecutablePath, new [] {virtualEnvPackage}, broker);
        }

        private void DoActionWithProgressMonitor(IProgressMonitor progressMonitor)
        {
            throw new NotImplementedException();
        }
    }

    public class CreateVirtualEnvironmentTask : PythonTaskBase
    {

        public CreateVirtualEnvironmentTask(PythonInstaller installer) : base(installer, PythonTaskName.download_python_embeddable_package, new PipInstallVirtualEnvTask(installer))
        {
        }

        public override string InProgressMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Creating_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);
        }

        public override string FailureMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_create_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);
        }

        public override string SuccessMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Successfully_created_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);
        }

        public override bool? IsTaskComplete()
        {
            return Directory.Exists(PythonInstaller.VirtualEnvironmentDir);
        }
        public override void DoAction(ILongWaitBroker broker)
        {
            string pythonExecutablePath = PythonInstaller.BasePythonExecutablePath;
            PythonInstaller.RunPythonModule(
                PythonInstaller.BasePythonExecutablePath, PythonInstaller.PythonVersionDir, PythonInstaller.VIRTUALENV, new[] { PythonInstaller.VirtualEnvironmentName }, broker);
        }

        private void DoActionWithProgressMonitor(IProgressMonitor progressMonitor)
        {
            throw new NotImplementedException();
        }
    }

    public class PipInstallPackagesTask : PythonTaskBase
    {

        public PipInstallPackagesTask(PythonInstaller installer) : base(installer, PythonTaskName.download_python_embeddable_package, new CreateVirtualEnvironmentTask(installer))
        {
        }

        public override string InProgressMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Installing_Python_packages_in_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);
        }

        public override string FailureMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_install_Python_packages_in_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);
        }

        public override string SuccessMessage()
        {
            return string.Format(ToolsResources.PythonInstaller_GetPythonTask_Successfully_installed_Python_packages_in_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);
        }

        public override bool? IsTaskComplete()
        {
            if (!File.Exists(PythonInstaller.VirtualEnvironmentPythonExecutablePath))
            {
                return false;
            }

            if (!Directory.Exists(PythonInstaller.VirtualEnvironmentDir))
                return false;

            if (PythonInstallerUtil.IsSignedFileOrDirectory(PythonInstaller.VirtualEnvironmentDir))
                return true;

            var argumentsBuilder = new StringBuilder();
            argumentsBuilder.Append(PythonInstaller.PYTHON_MODULE_OPTION)
                .Append(TextUtil.SPACE)
                .Append(PythonInstaller.PIP)
                .Append(TextUtil.SPACE)
                .Append(PythonInstaller.FREEZE);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = PythonInstaller.VirtualEnvironmentPythonExecutablePath,
                Arguments = argumentsBuilder.ToString(),
                RedirectStandardOutput = true,
                CreateNoWindow = true,
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
                var words = line.Split(new[] { PythonInstaller.EQUALS }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length != 2)
                {
                    words = line.Split(new[] { PythonInstaller.AT_SPLITTER }, StringSplitOptions.RemoveEmptyEntries);
                }
                Assume.IsTrue(words.Length.Equals(2), string.Format(ToolsResources.PythonInstallerTaskValidator_ValidatePipInstallPackages_Failed_to_parse_package_name_and_version_from_entry___0__, line));
                packageToVersionMap.Add(words[0], words[1]);
            }

            foreach (var package in PythonInstaller.PythonPackages)
            {
                if (!packageToVersionMap.ContainsKey(package.Name))
                {
                    return false;
                }
                if (!package.Version.IsNullOrEmpty())
                {
                    if (package.Version.StartsWith(PythonInstaller.GIT))
                    {
                        if (!packageToVersionMap[package.Name].StartsWith(package.Version))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!packageToVersionMap[package.Name].Equals(package.Version))
                        {
                            return false;
                        }

                    }
                }
            }
            return true;
        }
        public override void DoAction(ILongWaitBroker broker)
        {
            string pythonExecutablePath = PythonInstaller.BasePythonExecutablePath;
            PythonInstaller.PipInstall(PythonInstaller.VirtualEnvironmentPythonExecutablePath, PythonInstaller.PythonPackages, broker);
        }
    }
    public class SetupNvidiaLibrariesTask : PythonTaskBase
    {

        public SetupNvidiaLibrariesTask(PythonInstaller installer) : base(installer, PythonTaskName.download_python_embeddable_package)
        {
        }

        public override string InProgressMessage()
        {
            return ToolsResources.NvidiaInstaller_Setup_Nvidia_Libraries;
        }

        public override string FailureMessage()
        {
            return ToolsResources.NvidiaInstaller_Failed_Setup_Nvidia_Libraries;
        }

        public override string SuccessMessage()
        {
            return ToolsResources.NvidiaInstaller_Successfully_Setup_Nvidia_Libraries;
        }

        public override bool? IsTaskComplete()
        {
            return PythonInstaller.NvidiaLibrariesInstalled();
        }
        public override void DoAction(ILongWaitBroker broker)
        {
            CancellationToken cancelToken = CancellationToken.None;
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(PythonInstaller.InstallNvidiaLibrariesBat);
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, PythonInstaller.ECHO, cmdBuilder, PythonInstaller.CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;
            var pipedProcessRunner = PythonInstaller.TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            if (broker != null) 
                cancelToken = broker.CancellationToken;
            if (pipedProcessRunner.RunProcess(cmd, true, PythonInstaller.Writer, false, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));
        }

        private void DoActionWithProgressMonitor(IProgressMonitor progressMonitor)
        {
            throw new NotImplementedException();
        }

        public override bool IsRequiredForPythonEnvironment
        {
            get { return false; }
        }
        public override bool IsNvidiaTask
        {
            get { return true; }
        }
    }
}

