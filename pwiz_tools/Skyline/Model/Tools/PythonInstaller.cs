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
        internal const string VIRTUALENV = @"virtualenv";
        internal const string GIT = @"git";
        internal const string REG_ADD_COMMAND = @"reg add";

        public const string REG_FILESYSTEM_KEY = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem";
        public const string REG_LONGPATHS_ENABLED = @"LongPathsEnabled";
        public const string REG_LONGPATH_TYPE = @"/t REG_DWORD";
        public const string REG_LONGPATH_VALUE = @"/d 0x00000001";
        public const string REG_LONGPATH_ZERO = @"/d 0x00000000";
        public const string REG_LONGPATH_FORCE = @"/f";
        public const string REG_LONGPATH_NAME = @"/v " + REG_LONGPATHS_ENABLED;

        private const string CUDA_VERSION = @"12.6.3";
        private const string CUDNN_VERSION = @"9.6.0.74_cuda12";
        // private static readonly string CUDA_INSTALLER_URL = $@"https://developer.download.nvidia.com/compute/cuda/{CUDA_VERSION}/local_installers/";
        // private static readonly string CUDA_INSTALLER_URL = $@"https://developer.download.nvidia.com/compute/cuda/{CUDA_VERSION}/network_installers/";
        // private static readonly string CUDNN_INSTALLER_URL = @"https://developer.download.nvidia.com/compute/cudnn/redist/cudnn/windows-x86_64/";
        // private static readonly string CUDNN_INSTALLER = $@"cudnn-windows-x86_64-{CUDNN_VERSION}-archive.zip";
        private static string CudaVersionDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"cuda", CUDA_VERSION);
        // public static string CudaInstallerDownloadPath => Path.Combine(CudaVersionDir, CUDA_INSTALLER);
        public static string CuDNNVersionDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"cudnn", CUDNN_VERSION);
        // public static string CuDNNArchive = $@"cudnn-windows-x86_64-{CUDNN_VERSION}-archive";
        public static string CuDNNInstallDir => @"C:\Program Files\NVIDIA\CUDNN\v9.x";
        // public static string CuDNNInstallerDownloadPath => Path.Combine(CuDNNVersionDir, CUDNN_INSTALLER);

        // private static readonly string CUDA_INSTALLER = $@"cuda_{CUDA_VERSION_FULL}_windows.exe";
        // private static readonly string CUDA_INSTALLER = $@"cuda_{CUDA_VERSION}_windows_network.exe";
        // public static Uri CudaDownloadUri => new Uri(CUDA_INSTALLER_URL + CUDA_INSTALLER);
        // public static string CudaDownloadPath => Path.Combine(CudaVersionDir, CudaInstallerDownloadPath);
        // public static Uri CuDNNDownloadUri => new Uri(CUDNN_INSTALLER_URL + CUDNN_INSTALLER);
        // public static string CuDNNDownloadPath => Path.Combine(CuDNNVersionDir, CuDNNInstallerDownloadPath);

        public static string InstallNvidiaLibrariesBat =>
            Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"InstallNvidiaLibraries.bat");
        public static string UninstallNvidiaLibrariesBat =>
            Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"UninstallNvidiaLibraries.bat");

        public bool? NvidiaGpuAvailable
        {
            get
            {
                if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIAHARD ||
                    SimulatedInstallationState == eSimulatedInstallationState.NAIVE)
                {
                    return false;
                }
                // Also assume we have NVIDIA card when we assume we don't have NVIDIA software
                if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT)
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
 
        public static ProgramPathContainer PythonPathContainer = new ProgramPathContainer(PYTHON, Settings.Default.PythonEmbeddableVersion);
        public static string PythonVersion => PythonPathContainer.ProgramVersion;
        public static string PythonEmbeddablePackageFileName => PythonEmbeddablePackageFileBaseName + DOT_ZIP;
        public static Uri PythonEmbeddablePackageUri => new Uri(PYTHON_FTP_SERVER_URL + PythonVersion + FORWARD_SLASH + PythonEmbeddablePackageFileName);
        public static string PythonEmbeddablePackageDownloadPath => Path.Combine(PythonVersionDir, PythonEmbeddablePackageFileName);
        public static string PythonEmbeddablePackageExtractDir => Path.Combine(PythonVersionDir, PythonEmbeddablePackageFileBaseName);
        public static Uri GetPipScriptDownloadUri => new Uri(BOOTSTRAP_PYPA_URL + GET_PIP_SCRIPT_FILE_NAME);
        public static string GetPipScriptDownloadPath => Path.Combine(PythonVersionDir, GET_PIP_SCRIPT_FILE_NAME);
        public static string BasePythonExecutablePath => Path.Combine(PythonEmbeddablePackageExtractDir, PYTHON_EXECUTABLE);

        public int NumTotalTasks { get; set; }
        public int NumCompletedTasks { get; set; }
        public List<PythonPackage> PythonPackages { get; }
        public List<PythonTaskBase> PendingTasks { get; set; }

        public string VirtualEnvironmentName { get; }
        public string VirtualEnvironmentDir => Path.Combine(PythonVersionDir, VirtualEnvironmentName);
        public string VirtualEnvironmentPythonExecutablePath => Path.Combine(VirtualEnvironmentDir, SCRIPTS, PYTHON_EXECUTABLE);

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

        private static string PythonRootDir => PythonInstallerUtil.PythonRootDir;
        internal static TextWriter Writer { get; set; }
        public bool HavePythonTasks => PendingTasks.Any(task => task.IsRequiredForPythonEnvironment);
        public bool HaveNvidiaTasks => PendingTasks.Any(task => task.IsNvidiaTask);

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

        public static void WriteInstallNvidiaBatScript()
        {
            FileEx.SafeDelete(InstallNvidiaLibrariesBat);
            var type = typeof(PythonInstaller);
            using var stream = type.Assembly.GetManifestResourceStream(type, @"InstallNvidiaLibraries-bat.txt");
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
                // Programmer error. This file must be included in the project where it is expected.
                Assume.Fail(string.Format($@"Missing resource {type}.InstallNvidiaLibraries-bat.txt"));
            }
        }

        public static void WriteUninstallNvidiaBatScript()
        {
            FileEx.SafeDelete(UninstallNvidiaLibrariesBat);
            var type = typeof(PythonInstaller);
            using var stream = type.Assembly.GetManifestResourceStream(type, @"UninstallNvidiaLibraries-bat.txt");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                string resourceString = reader.ReadToEnd();
                resourceString = resourceString.Replace(@"{{0}}", CUDA_VERSION);
                resourceString = resourceString.Replace(@"{{1}}", CUDNN_VERSION);
                File.WriteAllText(UninstallNvidiaLibrariesBat, resourceString);
            }
            else
            {
                // Programmer error. This file must be included in the project where it is expected.
                Assume.Fail(string.Format($@"Missing resource {type}.UninstallNvidiaLibraries-bat.txt"));
            }
        }

        public static bool IsRunningElevated()
        {
            // Get current user's Windows identity
            var identity = WindowsIdentity.GetCurrent();

            // Convert identity to WindowsPrincipal to check for roles
            var principal = new WindowsPrincipal(identity);

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
        /// Returns true for NVIDIA hardware, false for non-NIDIA, and null if unknown
        /// </summary>
        public static bool? TestForNvidiaGPU()
        {
            try
            {
                // Query for video controllers using WMI
                var searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_VideoController");

                foreach (var obj in searcher.Get())
                {
                    //  GPU information
                    if (obj[@"Name"].ToString().StartsWith(@"NVIDIA"))
                        return true;
                }

                return false;
            }
            catch (ManagementException)
            {
                // Console.WriteLine(@"An error occurred while querying for WMI data: " + e.Message);
                return null;
            }
        }

        public PythonInstaller(IEnumerable<PythonPackage> packages,
            TextWriter writer, string virtualEnvironmentName)
        {
            // SimulatedInstallationState = eSimulatedInstallationState.NONE;
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

        public List<PythonTaskBase> ValidatePythonVirtualEnvironment()
        {
            var pendingTasks = new List<PythonTaskBase>();
            var allTasks = GetPythonTasks(this.NvidiaGpuAvailable);

            var hasSeenFailure = false;
            foreach (var task in allTasks)
            {
                bool? isTaskComplete = task.IsTaskComplete();

                if (hasSeenFailure)
                {
                    if ((isTaskComplete == true || isTaskComplete == null) && null == task.ParentTask)
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
                    if (isTaskComplete == true || isTaskComplete == null)
                        continue; 
                    hasSeenFailure = true;
                }
                pendingTasks.Add(task);
            }
            PendingTasks = pendingTasks;
            return pendingTasks;
        }

        public static void EnableWindowsLongPaths(bool enable, ILongWaitBroker broker = null)
        {
            var cmdLine = TextUtil.SpaceSeparate(REG_ADD_COMMAND,
                REG_FILESYSTEM_KEY.Quote(),
                REG_LONGPATH_NAME,
                REG_LONGPATH_TYPE,
                enable ? REG_LONGPATH_VALUE : REG_LONGPATH_ZERO,
                REG_LONGPATH_FORCE);

            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, cmdLine, CMD_PROCEEDING_SYMBOL);
            cmd += cmdLine;

            var processRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();

            var cancelToken = broker.GetSafeCancellationToken();

            if (processRunner.RunProcess(cmd, true, Writer, false, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdLine));
        }

        internal void PipInstall(string pythonExecutablePath, IEnumerable<PythonPackage> packages, ILongWaitBroker broker = null)
        {
            try
            {
                foreach (var package in packages)
                {
                    string arg;
                    if (package.Version.IsNullOrEmpty())
                    {
                        arg = package.Name;
                    }
                    else if (package.Version.StartsWith(GIT))
                    {
                        arg = package.Version;
                        arg = arg.Quote();
                    }
                    else
                    {
                        arg = package.Name + EQUALS + package.Version;
                        arg = arg.Quote();
                    }

                    var cmdLine = TextUtil.SpaceSeparate(pythonExecutablePath,
                        PYTHON_MODULE_OPTION, PIP, INSTALL, arg, string.Empty);

                    var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO,
                        cmdLine, CMD_PROCEEDING_SYMBOL);
                    cmd += string.Format(
                        ToolsResources.PythonInstaller_PipInstall__0__This_sometimes_could_take_3_5_minutes__Please_be_patient___1__,
                        ECHO, CMD_PROCEEDING_SYMBOL);
                    cmd += cmdLine;

                    var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();

                    var cancelToken = broker.GetSafeCancellationToken();

                    if (pipedProcessRunner.RunProcess(cmd, false, Writer, true, cancelToken) != 0)
                    {
                        throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__,
                            cmdLine));
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
            var cmdLine = TextUtil.SpaceSeparate(pythonExecutablePath, PYTHON_MODULE_OPTION, moduleName,
                TextUtil.SpaceSeparate(arguments), string.Empty);
            if (changeDir != null)
            {
                cmdLine = TextUtil.SpaceSeparate(CD, changeDir.Quote(), CONDITIONAL_CMD_PROCEEDING_SYMBOL, cmdLine);
            }
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, GetEscapedCmdString(cmdLine.ToString()), CMD_PROCEEDING_SYMBOL);
            cmd += cmdLine;

            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();

            var cancelToken = broker.GetSafeCancellationToken();

            if (pipedProcessRunner.RunProcess(cmd, false, Writer, true, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdLine));
            
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
            if (Directory.Exists(Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"Python")))
                return false;
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
        public static string GetPythonExecutablePath(string pythonVersion, string virtualEnvironmentName)
        {
            return Path.Combine(GetPythonVirtualEnvironmentScriptsDir(pythonVersion, virtualEnvironmentName),
                PYTHON_EXECUTABLE);
        }

        /// <summary>
        /// Get Python virtual environment activate.bat script path.
        /// </summary>
        public static string GetPythonVirtualEnvironmentActivationScriptPath(string pythonVersion, string virtualEnvironmentName)
        {
            return Path.Combine(GetPythonVirtualEnvironmentScriptsDir(pythonVersion, virtualEnvironmentName),
                ACTIVATE_SCRIPT_FILE_NAME);
        }

        public static CancellationToken GetSafeCancellationToken(this ILongWaitBroker broker)
        {
            return broker?.CancellationToken ?? CancellationToken.None;
        }

        public static string GetFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = { };

                RetryAction(() =>
                {
                    using var stream = File.OpenRead(filePath.ToLongPath());
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

                RetryAction(() =>
                {
                    using var stream = File.OpenRead(filePath.ToLongPath());
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
                TryHelper.TryTwice(act, maxRetries);
            }
            catch (IOException)
            {
                // Ignore and hope for the best
            }
        }

        public static bool IsRunningElevated()
        {
            // Get current user's Windows identity
            var identity = WindowsIdentity.GetCurrent();

            // Convert identity to WindowsPrincipal to check for roles
            var principal = new WindowsPrincipal(identity);

            // Check if the current user is in the Administrators role
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Return false if skysign file doesn't exist or it exists and is invalid, return true is skysign file matches provided signature,
        /// return null if signature provided is null or when signature is not null, but user not running as administrator
        /// </summary>
        /// <param name="path">path to file or directory</param>
        /// <param name="signature">key</param>
        public static bool? IsSignatureValid(string path, string signature)
        {
            string filePath = Path.GetFullPath(path) + SIGNATURE_EXTENSION;
            if (!IsSignedFileOrDirectory(path))
                return false;
            if (signature.IsNullOrEmpty())
                return null;
            if (IsRunningElevated())
                return Equals(signature, File.ReadAllText(filePath));
            else
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    using (fileInfo.OpenRead())
                    {
                        // Do nothing. Just testing the OpenRead() does not throw.
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
                if (!Equals(signature, File.ReadAllText(filePath)))
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
            if (!File.Exists(filePath))
                return;

            string signatureFile = Path.GetFullPath(filePath) + SIGNATURE_EXTENSION;
            using (var file = new FileSaver(signatureFile))
            {
                File.WriteAllText(file.SafeName, GetFileHash(filePath));
                RetryAction(() => { file.Commit(); });
            }
        }

        public static void SignDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath))
                return;

            string signatureFile = Path.GetFullPath(dirPath) + SIGNATURE_EXTENSION;
            using (var file = new FileSaver(signatureFile))
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
                            using var fileStream = new FileStream(filesArray[fileCount].ToLongPath(), FileMode.Open);
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
        setup_nvidia_libraries          // All NVIDIA tasks in a single batch script and executed with elevation
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

    public abstract class PythonTaskBase
    {
        public PythonInstaller PythonInstaller { get; }
        public PythonTaskName TaskName { get; }
        public PythonTaskBase ParentTask { get; }

        public abstract string InProgressMessage { get; }

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
        public virtual bool IsRequiredForPythonEnvironment => true;
        public virtual bool IsNvidiaTask => false;
        public virtual bool IsEnableLongPathsTask => false;
    }

    public class DownloadPythonEmbeddablePackageTask : PythonTaskBase
    {
        private string _storedHash = Settings.Default.PythonEmbeddableHash;
        public DownloadPythonEmbeddablePackageTask(PythonInstaller installer)
            : base(installer, PythonTaskName.download_python_embeddable_package)
        {
        }

        public override string InProgressMessage => ToolsResources.PythonInstaller_GetPythonTask_Downloading_Python_embeddable_package;

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

            webClient.DownloadFileAsyncOrThrow(PythonInstaller.PythonEmbeddablePackageUri, fileSaver.SafeName);
            fileSaver.Commit();
        }
    }

    public class UnzipPythonEmbeddablePackageTask : PythonTaskBase
    {
        public UnzipPythonEmbeddablePackageTask(PythonInstaller installer)
            : base(installer, PythonTaskName.unzip_python_embeddable_package, new DownloadPythonEmbeddablePackageTask(installer))
        {
        }

        public override string InProgressMessage => ToolsResources.PythonInstaller_GetPythonTask_Unzipping_Python_embeddable_package;

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
        public EnableSearchPathInPythonEmbeddablePackageTask(PythonInstaller installer)
            : base(installer, PythonTaskName.enable_search_path_in_python_embeddable_package, new UnzipPythonEmbeddablePackageTask(installer))
        {
        }

        public override string InProgressMessage => ToolsResources.PythonInstaller_GetPythonTask_Enabling_search_path_in_Python_embeddable_package;

        public override bool? IsTaskComplete()
        {
            if (!Directory.Exists(PythonInstaller.PythonEmbeddablePackageExtractDir))
                return false;

            return PythonInstallerUtil.IsSignatureValid(PythonInstaller.PythonEmbeddablePackageExtractDir,
                PythonInstallerUtil.GetDirectoryHash(PythonInstaller.PythonEmbeddablePackageExtractDir));
        }

        public override void DoAction(ILongWaitBroker broker)
        {
            var files = Directory.GetFiles(PythonInstaller.PythonEmbeddablePackageExtractDir, @"python*._pth");
            Assume.IsTrue(files.Length == 1,
                ToolsResources.PythonInstaller_EnableSearchPathInPythonEmbeddablePackage_Found_0_or_more_than_one_files_with__pth_extension__this_is_unexpected);
            var oldFilePath = files[0];
            var newFilePath = Path.ChangeExtension(oldFilePath, @".pth");
            File.Move(oldFilePath, newFilePath);
        }
    }

    public class EnableLongPathsTask : PythonTaskBase
    {
        public EnableLongPathsTask(PythonInstaller installer)
            : base(installer, PythonTaskName.enable_longpaths)
        {
        }

        public override string InProgressMessage => string.Format(ToolsResources.PythonInstaller_GetPythonTask_Enable_Long_Paths_For_Python_packages_in_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);

        public override bool? IsTaskComplete()
        {
            if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NAIVE ||
                Registry.GetValue(PythonInstaller.REG_FILESYSTEM_KEY, PythonInstaller.REG_LONGPATHS_ENABLED, 0) == null)
            {
                return false;
            }

            return (int)Registry.GetValue(PythonInstaller.REG_FILESYSTEM_KEY, PythonInstaller.REG_LONGPATHS_ENABLED, 0) == 1;
        }
        
        public override void DoAction(ILongWaitBroker broker)
        {
            PythonInstaller.EnableWindowsLongPaths(true);
        }

        public override bool IsEnableLongPathsTask => true;
    }

    public class DownloadGetPipScriptTask : PythonTaskBase
    {
        public DownloadGetPipScriptTask(PythonInstaller installer)
            : base(installer, PythonTaskName.download_getpip_script, new EnableSearchPathInPythonEmbeddablePackageTask(installer))
        {
        }

        public override string InProgressMessage => ToolsResources.PythonInstaller_GetPythonTask_Downloading_the_get_pip_py_script;

        public override bool? IsTaskComplete()
        {
            var filePath = PythonInstaller.GetPipScriptDownloadPath;

            if (!File.Exists(filePath))
                return false;

            return PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));
        }

        public override void DoAction(ILongWaitBroker broker)
        {
            var progressWaitBroker = new ProgressWaitBroker(DoActionWithProgressMonitor);
            progressWaitBroker.PerformWork(broker);
        }

        private void DoActionWithProgressMonitor(IProgressMonitor progressMonitor)
        {
            using var webClient = PythonInstaller.TestPipDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);
            webClient.DownloadFileAsyncOrThrow(PythonInstaller.GetPipScriptDownloadUri, PythonInstaller.GetPipScriptDownloadPath);
            PythonInstallerUtil.SignFile(PythonInstaller.GetPipScriptDownloadPath);
        }
    }

    public class RunGetPipScriptTask : PythonTaskBase
    {
        public RunGetPipScriptTask(PythonInstaller installer)
            : base(installer, PythonTaskName.run_getpip_script, new DownloadGetPipScriptTask(installer))
        {
        }

        public override string InProgressMessage => ToolsResources.PythonInstaller_GetPythonTask_Running_the_get_pip_py_script;

        public override bool? IsTaskComplete()
        {
            var filePath = Path.Combine(PythonInstaller.PythonEmbeddablePackageExtractDir, PythonInstaller.SCRIPTS, PythonInstaller.PIP_EXE);

            if (!File.Exists(filePath))
                return false;

            return PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));
        }

        public override void DoAction(ILongWaitBroker broker)
        {
            var cmdLine = TextUtil.SpaceSeparate(PythonInstaller.BasePythonExecutablePath.Quote(), PythonInstaller.GetPipScriptDownloadPath.Quote());
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, PythonInstaller.ECHO, cmdLine, PythonInstaller.CMD_PROCEEDING_SYMBOL);
            cmd += cmdLine;
            
            var pipedProcessRunner = PythonInstaller.TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            
            var cancelToken = broker.GetSafeCancellationToken();

            if (pipedProcessRunner.RunProcess(cmd, false, PythonInstaller.Writer, true, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdLine));

            var filePath = Path.Combine(PythonInstaller.PythonEmbeddablePackageExtractDir, PythonInstaller.SCRIPTS, PythonInstaller.PIP_EXE);
            PythonInstallerUtil.SignFile(filePath);
        }
    }
    public class PipInstallVirtualEnvTask : PythonTaskBase
    {
        public PipInstallVirtualEnvTask(PythonInstaller installer)
            : base(installer, PythonTaskName.pip_install_virtualenv, new RunGetPipScriptTask(installer))
        {
        }

        public override string InProgressMessage => string.Format(ToolsResources.PythonInstaller_GetPythonTask_Running_pip_install__0_, PythonInstaller.VIRTUALENV);

        public override bool? IsTaskComplete()
        {
            var filePath = Path.Combine(PythonInstaller.PythonEmbeddablePackageExtractDir, PythonInstaller.SCRIPTS, PythonInstaller.VIRTUALENV_EXE);

            if (!File.Exists(filePath))
                return false;

            return PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));
        }
        
        public override void DoAction(ILongWaitBroker broker)
        {
            var virtualEnvPackage = new PythonPackage { Name = PythonInstaller.VIRTUALENV, Version = null };
            PythonInstaller.PipInstall(PythonInstaller.BasePythonExecutablePath.Quote(), new [] {virtualEnvPackage}, broker);
        }
    }

    public class CreateVirtualEnvironmentTask : PythonTaskBase
    {
        public CreateVirtualEnvironmentTask(PythonInstaller installer) 
            : base(installer, PythonTaskName.create_virtual_environment, new PipInstallVirtualEnvTask(installer))
        {
        }

        public override string InProgressMessage => string.Format(ToolsResources.PythonInstaller_GetPythonTask_Creating_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);

        public override bool? IsTaskComplete()
        {
            return Directory.Exists(PythonInstaller.VirtualEnvironmentDir);
        }
        
        public override void DoAction(ILongWaitBroker broker)
        {
            PythonInstaller.RunPythonModule(PythonInstaller.BasePythonExecutablePath.Quote(), PythonInstaller.PythonVersionDir,
                PythonInstaller.VIRTUALENV, new[] { PythonInstaller.VirtualEnvironmentName }, broker);
        }
    }

    public class PipInstallPackagesTask : PythonTaskBase
    {
        public PipInstallPackagesTask(PythonInstaller installer)
            : base(installer, PythonTaskName.pip_install_packages, new CreateVirtualEnvironmentTask(installer))
        {
        }

        public override string InProgressMessage => string.Format(ToolsResources.PythonInstaller_GetPythonTask_Installing_Python_packages_in_virtual_environment__0_, PythonInstaller.VirtualEnvironmentName);

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

            var arguments = TextUtil.SpaceSeparate(PythonInstaller.PYTHON_MODULE_OPTION,
                PythonInstaller.PIP,
                PythonInstaller.FREEZE);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = PythonInstaller.VirtualEnvironmentPythonExecutablePath,
                Arguments = arguments,
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
            PythonInstaller.PipInstall(PythonInstaller.VirtualEnvironmentPythonExecutablePath.Quote(), PythonInstaller.PythonPackages, broker);
        }
    }
    
    public class SetupNvidiaLibrariesTask : PythonTaskBase
    {
        public SetupNvidiaLibrariesTask(PythonInstaller installer)
            : base(installer, PythonTaskName.setup_nvidia_libraries)
        {
        }

        public override string InProgressMessage => ToolsResources.NvidiaInstaller_Setup_Nvidia_Libraries;

        public override bool? IsTaskComplete()
        {
            return PythonInstaller.NvidiaLibrariesInstalled();
        }

        public override void DoAction(ILongWaitBroker broker)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(PythonInstaller.InstallNvidiaLibrariesBat);
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, PythonInstaller.ECHO, cmdBuilder, PythonInstaller.CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;

            var pipedProcessRunner = PythonInstaller.TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();

            var cancelToken = broker.GetSafeCancellationToken();

            if (pipedProcessRunner.RunProcess(cmd, true, PythonInstaller.Writer, false, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));
        }

        public override bool IsRequiredForPythonEnvironment => false;
        public override bool IsNvidiaTask => true;
    }
}

