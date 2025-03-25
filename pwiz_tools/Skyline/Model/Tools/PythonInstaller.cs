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
using OneOf;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Process = System.Diagnostics.Process;

[assembly: InternalsVisibleTo("TestFunctional")]
[assembly: InternalsVisibleTo("TestUtil")]


namespace pwiz.Skyline.Model.Tools
{
    public class PythonInstaller
    {
        private const string BOOTSTRAP_PYPA_URL = @"https://bootstrap.pypa.io/";
        private const string CD = @"cd";
        private const string CMD_ESCAPE_SYMBOL = TextUtil.CARET;
        internal const string CMD_PROCEEDING_SYMBOL = TextUtil.AMPERSAND;
        private const string CONDITIONAL_CMD_PROCEEDING_SYMBOL = TextUtil.AMPERSAND + TextUtil.AMPERSAND;
        private const string DOT_ZIP = @".zip";
        private const string DOT_EXE = @".exe";
        internal const string ECHO = @"echo";
        private const string EMBED_LOWER_CASE = @"embed";
        private const string EQUALS = TextUtil.EQUAL + TextUtil.EQUAL;
        private const string FORWARD_SLASH = TextUtil.FORWARD_SLASH;
        private const string GET_PIP_SCRIPT_FILE_NAME = @"get-pip.py";
        private const string HYPHEN = TextUtil.HYPHEN;
        private const string INSTALL = @"install";
        private const string PIP = @"pip";
        private const string PYTHON_EXECUTABLE = @"python.exe";
        private const string PYTHON_FTP_SERVER_URL = @"https://www.python.org/ftp/python/";
        private const string PYTHON_LOWER_CASE = @"python";
        private const string PYTHON_MODULE_OPTION = @"-m";
        private const string SCRIPTS = @"Scripts";
        private const string PIP_EXE = @"pip.exe";
        private const string SPACE = TextUtil.SPACE;
        private const string VIRTUALENV = @"virtualenv";
        private const string GIT = @"git";
        private const string HTTP = @"http";
        internal const string REG_ADD_COMMAND = @"reg add";
        internal const string REG_FILESYSTEM_KEY = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem";
        internal const string REG_LONGPATHS_ENABLED = @"LongPathsEnabled";
        internal const string REG_LONGPATH_TYPE = @"/t REG_DWORD";
        internal const string REG_LONGPATH_VALUE = @"/d 0x00000001";
        internal const string REG_LONGPATH_ZERO = @"/d 0x00000000";
        internal const string REG_LONGPATH_FORCE = @"/f";
        internal string REG_LONGPATH_NAME = $@"/v {REG_LONGPATHS_ENABLED}";

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

        private bool? _NvidiaGpuAvailable;
        public bool? NvidiaGpuAvailable
        {
            get
            {
                if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT) return true;
                return _NvidiaGpuAvailable;
            }

            internal set
            {
                _NvidiaGpuAvailable = value;
            }
        }

        public int NumTotalTasks { get; set; }
        public int NumCompletedTasks { get; set; }
        public string PythonVersion { get; }
        public List<PythonPackage> PythonPackages { get; }
        public string PythonEmbeddablePackageFileName => PythonEmbeddablePackageFileBaseName + DOT_ZIP;
        public Uri PythonEmbeddablePackageUri => new Uri(PYTHON_FTP_SERVER_URL + PythonVersion + FORWARD_SLASH + PythonEmbeddablePackageFileName);
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string PythonEmbeddablePackageDownloadPath => Path.Combine(PythonVersionDir, PythonEmbeddablePackageFileName);
        public string PythonEmbeddablePackageExtractDir => Path.Combine(PythonVersionDir, PythonEmbeddablePackageFileBaseName);
        public Uri GetPipScriptDownloadUri => new Uri(BOOTSTRAP_PYPA_URL + GET_PIP_SCRIPT_FILE_NAME);
        public string GetPipScriptDownloadPath => Path.Combine(PythonVersionDir, GET_PIP_SCRIPT_FILE_NAME);
        public string BasePythonExecutablePath => Path.Combine(PythonEmbeddablePackageExtractDir, PYTHON_EXECUTABLE);
        public string VirtualEnvironmentName { get; }
        public string VirtualEnvironmentDir => Path.Combine(PythonVersionDir, VirtualEnvironmentName);
        public string VirtualEnvironmentPythonExecutablePath => Path.Combine(VirtualEnvironmentDir, SCRIPTS, PYTHON_EXECUTABLE);
        public List<PythonTask> PendingTasks { get; set; }

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
        public ISkylineProcessRunnerWrapper TestPipeSkylineProcessRunner { get; set; }
        /// <summary>
        /// For testing purpose only. Setting this property will bypass the TaskValidator
        /// </summary>
        public List<PythonTaskName> TestPythonVirtualEnvironmentTaskNames { get; set; }
        #endregion
        public string PythonVersionDir => Path.Combine(PythonRootDir, PythonVersion);
  
        private string PythonEmbeddablePackageFileBaseName
        {
            get
            {
                var architecture = PythonInstallerUtil.GetPythonPackageArchitectureSubstring(
                    RuntimeInformation.ProcessArchitecture);
                var fileBaseName = string.Join(HYPHEN, new[] { PYTHON_LOWER_CASE, PythonVersion, EMBED_LOWER_CASE, architecture });
                return fileBaseName;
            }
        }
        private string PythonRootDir { get; } = PythonInstallerUtil.PythonRootDir;
        internal TextWriter Writer { get; }
        private IPythonInstallerTaskValidator TaskValidator { get; }

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

        public static bool? CuDNNLibraryInstalled(bool longValidate = false)
        {
            if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIAHARD ||
                SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT ||
                SimulatedInstallationState == eSimulatedInstallationState.NAIVE)
                return false;

            string targetDirectory = CuDNNInstallDir + @"\bin";

            if (!Directory.Exists(targetDirectory)) return false;

            string newPath = Environment.GetEnvironmentVariable(@"PATH");

            if (newPath != null && !newPath.Contains(targetDirectory))
            {
                newPath += TextUtil.SEMICOLON + targetDirectory;
                Environment.SetEnvironmentVariable(@"PATH", newPath, EnvironmentVariableTarget.User);
            }


            if (longValidate)
            {
                var computeHash = PythonInstallerUtil.IsRunningElevated()
                    ? PythonInstallerUtil.GetDirectoryHash(targetDirectory)
                    : null;
                bool? isValid = PythonInstallerUtil.IsSignatureValid(targetDirectory, computeHash);
                if (isValid == true || isValid == null)
                {
                    targetDirectory = CuDNNInstallDir + @"\include";
                    if (!Directory.Exists(targetDirectory)) return false;

                    computeHash = PythonInstallerUtil.IsRunningElevated()
                        ? PythonInstallerUtil.GetDirectoryHash(targetDirectory)
                        : null;
                    isValid = PythonInstallerUtil.IsSignatureValid(targetDirectory, computeHash);
                    if (isValid == true || isValid == null)
                    {
                        targetDirectory = CuDNNInstallDir + @"\lib";
                        if (!Directory.Exists(targetDirectory)) return false;

                        computeHash = PythonInstallerUtil.IsRunningElevated()
                            ? PythonInstallerUtil.GetDirectoryHash(targetDirectory)
                            : null;
                        isValid = PythonInstallerUtil.IsSignatureValid(targetDirectory, computeHash);
                    }

                }
                return isValid;
            }
            else
            {
                bool isValid = Directory.Exists(targetDirectory); //PythonInstallerUtil.IsSignedFileOrDirectory(targetDirectory);
                if (isValid)
                {
                    targetDirectory = CuDNNInstallDir + @"\include";
                    if (!Directory.Exists(targetDirectory)) return false;

                    //isValid = PythonInstallerUtil.IsSignedFileOrDirectory(targetDirectory);
                    //if (isValid)
                    //{
                        targetDirectory = CuDNNInstallDir + @"\lib";
                        if (!Directory.Exists(targetDirectory)) return false;

                        //isValid = PythonInstallerUtil.IsSignedFileOrDirectory(targetDirectory);
                    //}

                }
                return isValid;

            }

        }

        public void WriteInstallNvidiaBatScript()
        {
            FileEx.SafeDelete(InstallNvidiaLibrariesBat);
            string resourceString = ModelResources.NvidiaInstaller_Batch_script;
            resourceString = resourceString.Replace(@"{{0}}", CUDA_VERSION);
            resourceString = resourceString.Replace(@"{{1}}", CUDNN_VERSION);
            File.WriteAllText(InstallNvidiaLibrariesBat, resourceString);
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
        /// False means NONVIDIA hardware, true means NVIDIA hardware, null means don't know
        /// </summary>
        /// <returns></returns>
        public static bool? TestForNvidiaGPU()
        {
            if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIAHARD || SimulatedInstallationState == eSimulatedInstallationState.NAIVE) 
                return false;
            if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT)  //Also assume we have NVIDIA card when we assume we don't have NVIDIA software
                return true;

            bool? nvidiaGpu = null;
            try
            {
                // Query for video controllers using WMI
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_VideoController");
                
                foreach (ManagementObject obj in searcher.Get())
                {
                    //  GPU information
                    nvidiaGpu = obj[@"Name"].ToString().StartsWith(@"NVIDIA");
                    if (nvidiaGpu != false) break;
                }
            }
            catch (ManagementException e)
            {
                Console.WriteLine(@"An error occurred while querying for WMI data: " + e.Message);
            }
            return nvidiaGpu;
        }
        
        public PythonInstaller(ProgramPathContainer pythonPathContainer, IEnumerable<PythonPackage> packages,
            TextWriter writer, IPythonInstallerTaskValidator taskValidator, string virtualEnvironmentName)
        {
           // SimulatedInstallationState = eSimulatedInstallationState.NONE;
            PythonVersion = pythonPathContainer.ProgramVersion;
            Writer = writer;
            TaskValidator = taskValidator;
            VirtualEnvironmentName = virtualEnvironmentName;
            PendingTasks = new List<PythonTask>();
            Directory.CreateDirectory(PythonRootDir);
            Directory.CreateDirectory(PythonVersionDir);
           
            NvidiaGpuAvailable = TestForNvidiaGPU();
            PythonPackages = packages.ToList();

            if (NvidiaGpuAvailable == true)
            {
                PythonPackages.Add(new PythonPackage { Name = @"wheel", Version = null });
                PythonPackages.Add(new PythonPackage { Name = @"nvidia-cudnn-cu12", Version = null });
                PythonPackages.Add(new PythonPackage { Name = @"torch --extra-index-url https://download.pytorch.org/whl/cu118 --upgrade", Version = null });
            }
        }


        public bool IsNvidiaEnvironmentReady(List<PythonTask> abortedTasks = null)
        {
            if (SimulatedInstallationState == eSimulatedInstallationState.NONVIDIAHARD)
                return true;

            if (abortedTasks == null && SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT)
                return false;
            
            var tasks = PendingTasks.IsNullOrEmpty() ? ValidatePythonVirtualEnvironment() : PendingTasks;
            //NumTotalTasks = tasks.Count;

            

            if (abortedTasks != null && abortedTasks.Count > 0)
                return !abortedTasks.Any(task =>
                    (task.Name == PythonTaskName.setup_nvidia_libraries));
            //|| task.Name == PythonTaskName.download_cuda_library
            //|| task.Name == PythonTaskName.install_cuda_library
            //|| task.Name == PythonTaskName.download_cudnn_library
            //|| task.Name == PythonTaskName.install_cudnn_library));

            return true;
        }

        public bool IsPythonVirtualEnvironmentReady(List<PythonTask> abortedTasks = null)
        {
            if (SimulatedInstallationState == eSimulatedInstallationState.NAIVE)
                return false;
            if (abortedTasks == null && SimulatedInstallationState == eSimulatedInstallationState.NONVIDIASOFT)
                return true;

            var tasks = PendingTasks.IsNullOrEmpty() ? ValidatePythonVirtualEnvironment() : PendingTasks;
            //NumTotalTasks = tasks.Count;
            if (abortedTasks != null && abortedTasks.Count > 0) tasks = abortedTasks;

            if (NumTotalTasks == NumCompletedTasks && NumCompletedTasks > 0)
                return true;

            return !tasks.Any(task =>
                (task.Name != PythonTaskName.setup_nvidia_libraries
                 && task.Name != PythonTaskName.download_cuda_library
                 && task.Name != PythonTaskName.install_cuda_library
                 && task.Name != PythonTaskName.download_cudnn_library
                 && task.Name != PythonTaskName.install_cudnn_library));
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

            if (tasks.Any(task =>
                    (task.Name != PythonTaskName.setup_nvidia_libraries
                     && task.Name != PythonTaskName.download_cuda_library
                     && task.Name != PythonTaskName.install_cuda_library
                     && task.Name != PythonTaskName.download_cudnn_library
                     && task.Name != PythonTaskName.install_cudnn_library)))
            {
                HavePythonTasks = true;
            }

            if (tasks.Any(task =>
                    (task.Name == PythonTaskName.setup_nvidia_libraries
                     || task.Name == PythonTaskName.download_cuda_library
                     || task.Name == PythonTaskName.install_cuda_library
                     || task.Name == PythonTaskName.download_cudnn_library
                     || task.Name == PythonTaskName.install_cudnn_library)))
            {
                HaveNvidiaTasks = true;
            }
        }


        public List<PythonTask> ValidatePythonVirtualEnvironment()
        {
            var tasks = new List<PythonTask>();
            if (!TestPythonVirtualEnvironmentTaskNames.IsNullOrEmpty())
            {
                foreach (var taskName in TestPythonVirtualEnvironmentTaskNames)
                {
                    tasks.Add(GetPythonTask(taskName));
                    return tasks;
                }
            }

            var taskNodes = PythonInstallerUtil.GetPythonTaskNodes(this.NvidiaGpuAvailable);
            var hasSeenFailure = false;
            foreach (var taskNode in taskNodes)
            {
                bool? isTaskValid = TaskValidator.Validate(taskNode.PythonTaskName, this);
                if (hasSeenFailure)
                {
                    if ( (isTaskValid == true || isTaskValid == null) && null == taskNode.ParentNodes) { continue; }
                   
                    bool havePrerequisite = false;

                    if (taskNode.ParentNodes == null)
                    {
                        tasks.Add(GetPythonTask(taskNode.PythonTaskName));
                        continue;
                    }
                    else if (tasks.Count > 0)
                    {
                        foreach (var parentTask in taskNode.ParentNodes)
                        {
                            if (tasks.Count > 0 && tasks.Where(p => p.Name == parentTask.PythonTaskName).ToArray().Length > 0)
                            {
                                havePrerequisite = true;
                                break;
                            }
                        }

                    }

                    if (!havePrerequisite) { 
                        continue; 
                    }
                }
                else
                {
                    if (isTaskValid == true || isTaskValid == null) { continue; }
                    hasSeenFailure = true;
                }

                PythonTask nextTask = GetPythonTask(taskNode.PythonTaskName);
                if (nextTask != null) tasks.Add(nextTask);
            }
            PendingTasks = tasks;
            NumTotalTasks = PendingTasks.Count;
            return tasks;
        }

        private PythonTask GetPythonTask(PythonTaskName pythonTaskName) 
        {
            switch (pythonTaskName)
            {
                case PythonTaskName.download_python_embeddable_package:
                    var task1 = new PythonTask(DownloadPythonEmbeddablePackage);
                    task1.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Downloading_Python_embeddable_package;
                    task1.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_Python_embeddable_package;
                    task1.Name = pythonTaskName;
                    return task1;
                case PythonTaskName.unzip_python_embeddable_package:
                    var task2 = new PythonTask(UnzipPythonEmbeddablePackage);
                    task2.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Unzipping_Python_embeddable_package;
                    task2.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_unzip_Python_embeddable_package;
                    task2.Name = pythonTaskName;
                    return task2;
                case PythonTaskName.enable_search_path_in_python_embeddable_package:
                    var task3 = new PythonTask(EnableSearchPathInPythonEmbeddablePackage);
                    task3.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Enabling_search_path_in_Python_embeddable_package;
                    task3.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_enable_search_path_in_Python_embeddable_package;
                    task3.Name = pythonTaskName;
                    return task3;
                case PythonTaskName.enable_longpaths:
                    var task4 = new PythonTask((broker) => EnableWindowsLongPaths(broker));
                    task4.InProgressMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Enable_Long_Paths_For_Python_packages_in_virtual_environment__0_, VirtualEnvironmentName);
                    task4.FailureMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_enable_long_paths_Python_packages_in_virtual_environment__0_, VirtualEnvironmentName);
                    task4.Name = pythonTaskName;
                    return task4;
                case PythonTaskName.download_getpip_script:
                    var task5 = new PythonTask(DownloadGetPipScript);
                    task5.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Downloading_the_get_pip_py_script;
                    task5.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_the_get_pip_py_script;
                    task5.Name = pythonTaskName;
                    return task5;
                case PythonTaskName.run_getpip_script:
                    var task6 = new PythonTask((broker) => RunGetPipScript(broker));
                    task6.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Running_the_get_pip_py_script;
                    task6.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_run_the_get_pip_py_script;
                    task6.Name = pythonTaskName;
                    return task6;
                case PythonTaskName.pip_install_virtualenv:
                    var virtualEnvPackage = new PythonPackage { Name = VIRTUALENV, Version = null };
                    var task7 = new PythonTask((broker) => PipInstall(BasePythonExecutablePath, new[] { virtualEnvPackage }, broker));
                    task7.InProgressMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Running_pip_install__0_, VIRTUALENV);
                    task7.FailureMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_run_pip_install__0_, VIRTUALENV);
                    task7.Name = pythonTaskName;
                    return task7;
                case PythonTaskName.create_virtual_environment:
                    var task8 = new PythonTask((longWaitBroker) => RunPythonModule(
                        BasePythonExecutablePath, PythonVersionDir, VIRTUALENV, new[] { VirtualEnvironmentName }, longWaitBroker));
                    task8.InProgressMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Creating_virtual_environment__0_, VirtualEnvironmentName);
                    task8.FailureMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_create_virtual_environment__0_, VirtualEnvironmentName);
                    task8.Name = pythonTaskName;
                    return task8;
                case PythonTaskName.pip_install_packages:
                    var task9= new PythonTask((longWaitBroker) => PipInstall(VirtualEnvironmentPythonExecutablePath, PythonPackages, longWaitBroker));
                    task9.InProgressMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Installing_Python_packages_in_virtual_environment__0_, VirtualEnvironmentName);
                    task9.FailureMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_install_Python_packages_in_virtual_environment__0_, VirtualEnvironmentName);
                    task9.Name = pythonTaskName;
                    return task9;
                case PythonTaskName.setup_nvidia_libraries:
                    if (NvidiaGpuAvailable == true)
                    {
                        Directory.CreateDirectory(CudaVersionDir);
                        var task10 = new PythonTask((longWaitBroker) => SetupNvidiaLibraries(longWaitBroker));
                        task10.InProgressMessage = ToolsResources.NvidiaInstaller_Setup_Nvidia_Libraries;
                        task10.FailureMessage = ToolsResources.NvidiaInstaller_Failed_Setup_Nvidia_Libraries;
                        task10.Name = pythonTaskName;
                        return task10;
                    }
                    else
                    {
                        return null;
                    }

                /* ************************************************************************************************************************************************************
                case PythonTaskName.download_cuda_library:
                    if (NvidiaGpuAvailable == true)
                    {
                        Directory.CreateDirectory(CudaVersionDir);
                        var task10 = new PythonTask(DownloadCudaLibrary);
                        task10.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Downloading_Cuda_Installer;
                        task10.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_Cuda_Installer;
                        task10.Name = pythonTaskName;
                        return task10;
                    }
                    else
                    {
                        return null;
                    }
                case PythonTaskName.install_cuda_library:
                    if (NvidiaGpuAvailable == true)
                    {
                        Directory.CreateDirectory(CudaVersionDir);
                        var task11 = new PythonTask(InstallCudaLibrary);
                        task11.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Installing_Cuda;
                        task11.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_install_Cuda;
                        task11.Name = pythonTaskName;
                        return task11;
                    }
                    else
                    {
                        return null;
                    }
                case PythonTaskName.download_cudnn_library:
                    if (NvidiaGpuAvailable == true)
                    {
                        Directory.CreateDirectory(CuDNNVersionDir);
                        var task12 = new PythonTask(DownloadCuDNNLibrary);
                        task12.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Downloading_CuDNN_Installer;
                        task12.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_CuDNN_Installer;
                        task12.Name = pythonTaskName;
                        return task12;
                    }
                    else
                    {
                        return null;
                    }
                case PythonTaskName.install_cudnn_library:
                    if (NvidiaGpuAvailable == true)
                    {
                        Directory.CreateDirectory(CuDNNVersionDir);
                        var task13 = new PythonTask(InstallCuDNNLibrary);
                        task13.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Installing_CuDNN;
                        task13.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_install_CuDNN;
                        task13.Name = pythonTaskName;
                        return task13;
                    }
                    else
                    {
                        return null;
                    }
                **************************************************************************************************************************************************** */

                default:
                    throw new PythonInstallerUnsupportedTaskNameException(pythonTaskName);
            }
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

            if (processRunner.RunProcess(cmd, true, Writer, true, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));

        }
        public void DisableWindowsLongPaths(ILongWaitBroker broker = null)
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
        private void SetupNvidiaLibraries(ILongWaitBroker broker = null)
        {
            CancellationToken cancelToken = CancellationToken.None;
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(InstallNvidiaLibrariesBat);
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, cmdBuilder, CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            if (broker != null) cancelToken = broker.CancellationToken;
            if (pipedProcessRunner.RunProcess(cmd, true, Writer, false, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));
        }
        private void DownloadCudaLibrary(IProgressMonitor progressMonitor)
        {
            using var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);
            if (!webClient.DownloadFileAsync(CudaDownloadUri, CudaDownloadPath, out var downloadException))
                throw new ToolExecutionException(
                    ToolsResources.PythonInstaller_Cuda_Download_failed__Check_your_network_connection_or_contact_Skyline_team_for_help_, downloadException);
        }
        private void InstallCudaLibrary(IProgressMonitor progressMonitor)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(CudaDownloadPath);
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, cmdBuilder, CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            if (pipedProcessRunner.RunProcess(cmd, false, Writer, true) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));
        }

        private void DownloadCuDNNLibrary(IProgressMonitor progressMonitor)
        {
            using var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);
            if (!webClient.DownloadFileAsync(CuDNNDownloadUri, CuDNNDownloadPath, out var downloadException))
                throw new ToolExecutionException(
                    ToolsResources.PythonInstaller_CuDNN_Download_failed__Check_your_network_connection_or_contact_Skyline_team_for_help_, downloadException);
        }
        private void InstallCuDNNLibrary(IProgressMonitor progressMonitor)
        {
            using var zipFile = ZipFile.Read(CuDNNDownloadPath);
            if (!PythonInstallerUtil.IsSignedFileOrDirectory(CuDNNVersionDir))
                zipFile.ExtractAll(CuDNNVersionDir);
            
            var cmdBuilder = new StringBuilder();
            string binDirectory = CuDNNInstallDir + @"\bin";
            string includeDirectory = CuDNNInstallDir + @"\include";
            string libDirectory = CuDNNInstallDir + @"\lib";

            string [] directories = { binDirectory, includeDirectory, libDirectory };

            PythonInstallerUtil.CreateDirectoriesElevated(directories, TestPipeSkylineProcessRunner, Writer);

            try
            {
                string sourceDirectory = Path.Combine(CuDNNVersionDir, CuDNNArchive) + @"\bin";
                string searchPattern = @"cudnn*.dll";

                PythonInstallerUtil.CopyFilesElevated(sourceDirectory, binDirectory, searchPattern, TestPipeSkylineProcessRunner, Writer);
                PythonInstallerUtil.SignDirectory(binDirectory);

                sourceDirectory = Path.Combine(CuDNNVersionDir, CuDNNArchive) + @"\include";
                searchPattern = @"cudnn*.h";

                PythonInstallerUtil.CopyFilesElevated(sourceDirectory, includeDirectory, searchPattern, TestPipeSkylineProcessRunner, Writer);
                PythonInstallerUtil.SignDirectory(includeDirectory);

                sourceDirectory = Path.Combine(CuDNNVersionDir, CuDNNArchive) + @"\lib\x64"; 
                searchPattern = @"cudnn*.lib";

                PythonInstallerUtil.CopyFilesElevated(sourceDirectory, libDirectory, searchPattern, TestPipeSkylineProcessRunner, Writer);
                PythonInstallerUtil.SignDirectory(libDirectory);


            }
            catch (Exception)
            {
                //exception 
            }


        }
        private void DownloadPythonEmbeddablePackage(IProgressMonitor progressMonitor)
        {
            using var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);
            if (!webClient.DownloadFileAsync(PythonEmbeddablePackageUri, PythonEmbeddablePackageDownloadPath, out var downloadException))
                throw new ToolExecutionException(
                    ToolsResources.PythonInstaller_Download_failed__Check_your_network_connection_or_contact_Skyline_team_for_help_, downloadException);
        }

        private void UnzipPythonEmbeddablePackage(IProgressMonitor progressMonitor)
        {
            //PythonInstallerUtil.UnblockFile(PythonEmbeddablePackageDownloadPath);
            using var zipFile = ZipFile.Read(PythonEmbeddablePackageDownloadPath);
            DirectoryEx.SafeDeleteLongPath(PythonEmbeddablePackageExtractDir);
            zipFile.ExtractAll(PythonEmbeddablePackageExtractDir);
        }

        private void EnableSearchPathInPythonEmbeddablePackage()
        {
            var files = Directory.GetFiles(PythonEmbeddablePackageExtractDir, @"python*._pth");
            Assume.IsTrue(files.Length == 1, ToolsResources.PythonInstaller_EnableSearchPathInPythonEmbeddablePackage_Found_0_or_more_than_one_files_with__pth_extension__this_is_unexpected);
            var oldFilePath = files[0];
            var newFilePath = Path.ChangeExtension(oldFilePath, @".pth");
            File.Move(oldFilePath, newFilePath);
        }
        private void DownloadGetPipScript(IProgressMonitor progressMonitor)
        {
            using var webClient = TestPipDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);
            if (!webClient.DownloadFileAsync(GetPipScriptDownloadUri, GetPipScriptDownloadPath, out var downloadException))
            {
                throw new ToolExecutionException(
                    ToolsResources.PythonInstaller_Download_failed__Check_your_network_connection_or_contact_Skyline_team_for_help_, downloadException);
            }
            PythonInstallerUtil.SignFile(GetPipScriptDownloadPath);
        }

        private void RunGetPipScript(ILongWaitBroker broker = null)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(BasePythonExecutablePath)
                .Append(SPACE)
                .Append(GetPipScriptDownloadPath);
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, cmdBuilder, CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            CancellationToken cancelToken = CancellationToken.None;
            if (broker != null) cancelToken = broker.CancellationToken;
            if (pipedProcessRunner.RunProcess(cmd, false, Writer, true, cancelToken) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));

            var filePath = Path.Combine(PythonEmbeddablePackageExtractDir, SCRIPTS, PIP_EXE);
            PythonInstallerUtil.SignFile(filePath);

        }

        private void PipInstall(string pythonExecutablePath, IEnumerable<PythonPackage> packages, ILongWaitBroker broker = null)
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
                    else if (package.Version.StartsWith(HTTP))
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
            catch (Exception ex)
            {
                string error = $@"Unexpected error: {ex.Message}";
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__,
                    error));
            }

            var filePath = Path.Combine(PythonEmbeddablePackageExtractDir, SCRIPTS, VIRTUALENV);
            PythonInstallerUtil.SignFile(filePath+DOT_EXE);
            PythonInstallerUtil.SignDirectory(VirtualEnvironmentDir);

        }

        private void RunPythonModule(string pythonExecutablePath, string changeDir, string moduleName, string[] arguments, ILongWaitBroker broker = null)
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

        /// <summary>
        /// Returns a list of Python installation tasks in topological order, with each task as a PythonTaskNode.
        /// </summary>
        internal static List<PythonTaskNode> GetPythonTaskNodes(bool? haveNvidiaGpu = false)
        {
            var node1 = new PythonTaskNode { PythonTaskName = PythonTaskName.download_python_embeddable_package, ParentNodes = null };
            var node2 = new PythonTaskNode { PythonTaskName = PythonTaskName.unzip_python_embeddable_package, ParentNodes = new List<PythonTaskNode> { node1 } };
            var node3 = new PythonTaskNode { PythonTaskName = PythonTaskName.enable_search_path_in_python_embeddable_package, ParentNodes = new List<PythonTaskNode> { node2 } };
            var node4 = new PythonTaskNode { PythonTaskName = PythonTaskName.enable_longpaths, ParentNodes = null };
            var node5 = new PythonTaskNode { PythonTaskName = PythonTaskName.download_getpip_script, ParentNodes = new List<PythonTaskNode> { node3 } };
            var node6 = new PythonTaskNode { PythonTaskName = PythonTaskName.run_getpip_script, ParentNodes = new List<PythonTaskNode> { node3, node5 } };
            var node7 = new PythonTaskNode { PythonTaskName = PythonTaskName.pip_install_virtualenv, ParentNodes = new List<PythonTaskNode> { node6 } };
            var node8 = new PythonTaskNode { PythonTaskName = PythonTaskName.create_virtual_environment, ParentNodes = new List<PythonTaskNode> { node7 } };
            var node9 = new PythonTaskNode { PythonTaskName = PythonTaskName.pip_install_packages, ParentNodes = new List<PythonTaskNode> { node8 } };
            var node10 = new PythonTaskNode { PythonTaskName = PythonTaskName.setup_nvidia_libraries, ParentNodes = null };

//            var node10 = new PythonTaskNode { PythonTaskName = PythonTaskName.download_cuda_library, ParentNodes = null };
//            var node11 = new PythonTaskNode { PythonTaskName = PythonTaskName.install_cuda_library, ParentNodes = new List<PythonTaskNode> { node10 } };
//            var node12 = new PythonTaskNode { PythonTaskName = PythonTaskName.download_cudnn_library, ParentNodes = new List<PythonTaskNode> { node11 } };
//            var node13 = new PythonTaskNode { PythonTaskName = PythonTaskName.install_cudnn_library, ParentNodes = new List<PythonTaskNode> { node12 } };

            if (haveNvidiaGpu == true)
                return new List<PythonTaskNode>
                    { node1, node2, node3, node4, node5, node6, node7, node8, node9, node10 }; // node11, node12, node13 };
            
            return new List<PythonTaskNode> { node1, node2, node3, node4, node5, node6, node7, node8, node9 };


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

                    using (var stream = File.OpenRead($@"\\?\{fullPath}"))
                    {
                        hash = sha256.ComputeHash(stream);

                    }
                });

                if (!hash.IsNullOrEmpty())
                    return BitConverter.ToString(hash).Replace(@"-", "").ToLowerInvariant();

                return @"F00F00F00";
            }
        }

        public static void RetryAction([InstantHandle] Action act, int maxRetries = 100)
        {
            int retry = 0;

            while (retry++ < maxRetries)
            {
                try
                {
                    act();
                    break;
                }
                catch (IOException)
                {

                }
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
        /// Return false if skysign file doesn't exist or it exists and is invalid, return true is skysign file matches provide signature,
        /// return null if signature provided is null or when signature is not null but user not running as administrator
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
                return signature == File.ReadAllText(filePath);
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
                        RetryAction(() => {
                            using (var fileStream = new FileStream($@"\\?\{filesArray[fileCount]}", FileMode.Open))
                            {
                                // Copy file contents to the combined stream
                                fileStream.CopyTo(combinedStream);
                                // Add a separator or file name to differentiate between files

                                var separator = Encoding.UTF8.GetBytes(Path.GetFileName(filesArray[fileCount]) ?? string.Empty);
                                combinedStream.Write(separator, 0, separator.Length);
                            }
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
        bool? Validate(PythonTaskName pythonTaskName, PythonInstaller pythonInstaller);
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

    public class TestPythonInstallerTaskValidator : IPythonInstallerTaskValidator
    {
        private List<PythonTaskNode> TaskNodes => PythonInstallerUtil.GetPythonTaskNodes();
        private Dictionary<PythonTaskName, bool> TaskValidationResult { get; } = new Dictionary<PythonTaskName, bool>
        {
            { PythonTaskName.download_python_embeddable_package, false },
            { PythonTaskName.unzip_python_embeddable_package, false },
            { PythonTaskName.enable_search_path_in_python_embeddable_package, false },
            { PythonTaskName.enable_longpaths, false },
            { PythonTaskName.download_getpip_script, false },
            { PythonTaskName.run_getpip_script, false },
            { PythonTaskName.pip_install_virtualenv, false },
            { PythonTaskName.create_virtual_environment, false },
            { PythonTaskName.pip_install_packages, false },
            { PythonTaskName.download_cuda_library, false },
            { PythonTaskName.install_cuda_library, false },
            { PythonTaskName.download_cudnn_library, false },
            { PythonTaskName.install_cudnn_library, false },
            { PythonTaskName.setup_nvidia_libraries, false }

        };

        public bool? Validate(PythonTaskName pythonTaskName, PythonInstaller pythonInstaller)
        {
            switch (pythonTaskName)
            {
                case PythonTaskName.download_python_embeddable_package:
                    return ValidateDownloadPythonEmbeddablePackage();
                case PythonTaskName.unzip_python_embeddable_package:
                    return ValidateUnzipPythonEmbeddablePackage();
                case PythonTaskName.enable_search_path_in_python_embeddable_package:
                    return ValidateEnableSearchPathInPythonEmbeddablePackage();
                case PythonTaskName.enable_longpaths:
                    return ValidateEnableLongpaths();
                case PythonTaskName.download_getpip_script:
                    return ValidateDownloadGetPipScript();
                case PythonTaskName.run_getpip_script:
                    return ValidateRunGetPipScript();
                case PythonTaskName.pip_install_virtualenv:
                    return ValidatePipInstallVirtualenv();
                case PythonTaskName.create_virtual_environment:
                    return ValidateCreateVirtualEnvironment();
                case PythonTaskName.pip_install_packages:
                    return ValidatePipInstallPackages();
                case PythonTaskName.download_cuda_library:
                    return ValidateDownloadCudaLibrary();
                case PythonTaskName.install_cuda_library:
                    return ValidateInstallCudaLibrary();
                case PythonTaskName.download_cudnn_library:
                    return ValidateDownloadCuDNNLibrary();
                case PythonTaskName.install_cudnn_library:
                    return ValidateInstallCuDNNLibrary();
                case PythonTaskName.setup_nvidia_libraries:
                    return ValidateSetupNvidiaLibraries();
                default:
                    throw new PythonInstallerUnsupportedTaskNameException(pythonTaskName);
            }
        }

        public void SetSuccessUntil(PythonTaskName pythonTaskName)
        {
            var seenTask = false;
            foreach (var taskNode in TaskNodes)
            {
                if (!seenTask)
                {
                    if (taskNode.PythonTaskName.Equals(pythonTaskName))
                    {
                        seenTask = true;
                    }
                    TaskValidationResult[taskNode.PythonTaskName] = true;
                }
                else
                {
                    TaskValidationResult[taskNode.PythonTaskName] = false;
                }
            }
        }
        private bool ValidateSetupNvidiaLibraries()
        {
            return GetTaskValidationResult(PythonTaskName.setup_nvidia_libraries);
        }
        private bool ValidateDownloadCudaLibrary()
        {
            return GetTaskValidationResult(PythonTaskName.download_cuda_library);
        }
        private bool ValidateInstallCudaLibrary()
        {
            return GetTaskValidationResult(PythonTaskName.install_cuda_library);
        }
        private bool ValidateDownloadCuDNNLibrary()
        {
            return GetTaskValidationResult(PythonTaskName.download_cuda_library);
        }
        private bool ValidateInstallCuDNNLibrary()
        {
            return GetTaskValidationResult(PythonTaskName.install_cuda_library);
        }
        private bool ValidateDownloadPythonEmbeddablePackage()
        {
            return GetTaskValidationResult(PythonTaskName.download_python_embeddable_package);
        }

        private bool ValidateUnzipPythonEmbeddablePackage()
        {
            return GetTaskValidationResult(PythonTaskName.unzip_python_embeddable_package);
        }

        private bool ValidateEnableSearchPathInPythonEmbeddablePackage()
        {
            return GetTaskValidationResult(PythonTaskName.enable_search_path_in_python_embeddable_package);
        }

        private bool ValidateDownloadGetPipScript()
        {
            return GetTaskValidationResult(PythonTaskName.download_getpip_script);
        }

        private bool ValidateRunGetPipScript()
        {
            return GetTaskValidationResult(PythonTaskName.run_getpip_script);
        }

        private bool ValidatePipInstallVirtualenv()
        {
            return GetTaskValidationResult(PythonTaskName.pip_install_virtualenv);
        }

        private bool ValidateCreateVirtualEnvironment()
        {
            return GetTaskValidationResult(PythonTaskName.create_virtual_environment);
        }
        internal bool ValidateEnableLongpaths()
        {
            return GetTaskValidationResult(PythonTaskName.enable_longpaths);
        }
        private bool ValidatePipInstallPackages()
        {
            return GetTaskValidationResult(PythonTaskName.pip_install_packages);
        }

        private bool GetTaskValidationResult(PythonTaskName pythonTaskName)
        {
            return TaskValidationResult[pythonTaskName];
        }
    }

    public class PythonInstallerTaskValidator : IPythonInstallerTaskValidator
    {
        private const string SCRIPTS = @"Scripts";
        private const string PIP_DOT_EXE = @"pip.exe";
        private const string VIRTUALENV_DOT_EXE = @"virtualenv.exe";
        private const string SPACE = TextUtil.SPACE;
        private const string PYTHON_MODULE_OPTION = @"-m";
        private const string PIP = @"pip";
        private const string FREEZE = @"freeze";
        private const string EQUALS = TextUtil.EQUAL + TextUtil.EQUAL;
        private const string AT_SPLITTER = @" @ ";
        private const string GIT = @"git";

        private static PythonInstaller _pythonInstaller { get; set; }

        public static void DisableWindowsLongPaths()
        {
            _pythonInstaller.DisableWindowsLongPaths();
        }
        public bool? Validate(PythonTaskName pythonTaskName, PythonInstaller pythonInstaller)
        {
            _pythonInstaller = pythonInstaller;
            switch (pythonTaskName)
            {
                case PythonTaskName.download_python_embeddable_package:
                    return ValidateDownloadPythonEmbeddablePackage();
                case PythonTaskName.unzip_python_embeddable_package:
                    return ValidateUnzipPythonEmbeddablePackage();
                case PythonTaskName.enable_search_path_in_python_embeddable_package:
                    return ValidateEnableSearchPathInPythonEmbeddablePackage();
                case PythonTaskName.enable_longpaths:
                    return ValidateEnableLongpaths();
                case PythonTaskName.download_getpip_script:
                    return ValidateDownloadGetPipScript();
                case PythonTaskName.run_getpip_script:
                    return ValidateRunGetPipScript();
                case PythonTaskName.pip_install_virtualenv:
                    return ValidatePipInstallVirtualenv();
                case PythonTaskName.create_virtual_environment:
                    return ValidateCreateVirtualEnvironment();
                case PythonTaskName.pip_install_packages:
                    return ValidatePipInstallPackages();
                case PythonTaskName.download_cuda_library:
                    return ValidateDownloadCudaLibrary();
                case PythonTaskName.install_cuda_library:
                    return ValidateInstallCudaLibrary();
                case PythonTaskName.download_cudnn_library:
                    return ValidateDownloadCuDNNLibrary();
                case PythonTaskName.install_cudnn_library:
                    return ValidateInstallCuDNNLibrary();
                case PythonTaskName.setup_nvidia_libraries:
                    return ValidateSetupNvidiaLibraries();
                default:
                    throw new PythonInstallerUnsupportedTaskNameException(pythonTaskName);
            }
        }
        public class PythonTaskAndHash
        {
            public PythonTaskAndHash(PythonTaskName task, string hash)
            {
                Task = task;
                Hash = hash;
            }
            public PythonTaskName Task { get; private set; }
            public string Hash { get; private set; }
            public override string ToString() { return TextUtil.ColonSeparate(Task.ToString(),Hash); }
        }
        private IList<PythonTaskAndHash> TargetsAndHashes =>
            new[]
            {
                new PythonTaskAndHash(PythonTaskName.download_python_embeddable_package, @"938a1f3b80d580320836260612084d74ce094a261e36f9ff3ac7b9463df5f5e4"),
                new PythonTaskAndHash(PythonTaskName.unzip_python_embeddable_package, @"1ac00589117bc386ded40a44b99cb357c15f3f35443d3599370e4f750cdba678"),
                new PythonTaskAndHash(PythonTaskName.enable_search_path_in_python_embeddable_package, @"95f29168dc5cf35585a501bf35ec865383300bfac0e2222c7ec7c02ca7bde475"),
                new PythonTaskAndHash(PythonTaskName.download_getpip_script, @"96e58b5962f307566141ea9b393e136cbdf811db9f02968dc5bc88f43989345c"),
                new PythonTaskAndHash(PythonTaskName.download_cuda_library, @"05cd1a726216d83d124b2139464e998520384585d4e9f45bd7ffd902635aab07"),
                new PythonTaskAndHash(PythonTaskName.install_cuda_library, @"ABCD123"),
                new PythonTaskAndHash(PythonTaskName.download_cudnn_library, @"65ca0f2d77a46de1def35e289780b8d8729ef2fa39cf8dd0c8448e381dd2978c"),
                new PythonTaskAndHash(PythonTaskName.install_cudnn_library, @"ABCD123")

            };
        
        private bool ValidateDownloadCudaLibrary()
        {
            if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD)
                return true;
            if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT || 
                PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NAIVE)
                return false;
            if (PythonInstaller.CudaLibraryInstalled())
                return true;
            if (!File.Exists(_pythonInstaller.CudaInstallerDownloadPath))
                return false;

            var computeHash =
                PythonInstallerUtil.GetFileHash(_pythonInstaller.CudaInstallerDownloadPath);
            var storedHash = TargetsAndHashes.Where(m => m.Task == PythonTaskName.download_cuda_library).ToArray()[0].Hash;
            return computeHash == storedHash;
        }

        private bool ValidateSetupNvidiaLibraries()
        {
            return PythonInstaller.NvidiaLibrariesInstalled();
        }

        private bool ValidateInstallCudaLibrary()
        {
            if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD)
                return true;
            if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT ||
                PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NAIVE)
                return false;

            return PythonInstaller.CudaLibraryInstalled();
        }

        private bool ValidateDownloadCuDNNLibrary()
        {
            if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD)
                return true;
            if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT ||
                PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NAIVE)
                return false;
            if (PythonInstaller.CuDNNLibraryInstalled() != false)
                return true;
            if (!File.Exists(_pythonInstaller.CuDNNInstallerDownloadPath))
                return false;
           
            var computeHash =
                PythonInstallerUtil.GetFileHash(_pythonInstaller.CuDNNInstallerDownloadPath);
            var storedHash = TargetsAndHashes.Where(m => m.Task == PythonTaskName.download_cudnn_library).ToArray()[0].Hash;
            return computeHash == storedHash;
        }
       
        private bool? ValidateInstallCuDNNLibrary()
        {
            if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD)
                return true;
            if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT ||
                PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NAIVE)
                return false;

            return PythonInstaller.CuDNNLibraryInstalled();
        }

        private bool ValidateDownloadPythonEmbeddablePackage()
        {
            var pythonFilePath = _pythonInstaller.PythonEmbeddablePackageDownloadPath;
            if (!File.Exists(pythonFilePath))
                return false;
            var computeHash = PythonInstallerUtil.GetFileHash(pythonFilePath);
            var storedHash = TargetsAndHashes.Where(m => m.Task == PythonTaskName.download_python_embeddable_package).ToArray()[0].Hash;
            return computeHash == storedHash;
        }

        private bool? ValidateUnzipPythonEmbeddablePackage(bool longValidate = false)
        {
            if (!Directory.Exists(_pythonInstaller.PythonEmbeddablePackageExtractDir))
                return false;

            if (longValidate)
                return
                    PythonInstallerUtil.IsSignatureValid(_pythonInstaller.PythonEmbeddablePackageExtractDir, 
                    PythonInstallerUtil.GetDirectoryHash(_pythonInstaller.PythonEmbeddablePackageExtractDir));

            return PythonInstallerUtil.IsSignedFileOrDirectory(_pythonInstaller.PythonEmbeddablePackageExtractDir);
        }

        private bool ValidateEnableSearchPathInPythonEmbeddablePackage()
        {
            if (!Directory.Exists(_pythonInstaller.PythonEmbeddablePackageExtractDir))
                return false;
            
            var disabledPathFiles = Directory.GetFiles(_pythonInstaller.PythonEmbeddablePackageExtractDir, @"python*._pth");
            var enabledPathFiles = Directory.GetFiles(_pythonInstaller.PythonEmbeddablePackageExtractDir, @"python*.pth");
            var computeHash =
                PythonInstallerUtil.GetFilesArrayHash(enabledPathFiles);
            var storedHash = TargetsAndHashes.Where(m => m.Task == PythonTaskName.enable_search_path_in_python_embeddable_package).ToArray()[0].Hash;
            return computeHash == storedHash;
        }

        private bool? ValidateDownloadGetPipScript()
        {
            var filePath = _pythonInstaller.GetPipScriptDownloadPath;

            if (!File.Exists(filePath))
                return false;

            return
                PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));
        }

        private bool? ValidateRunGetPipScript()
        {
            var filePath = Path.Combine(_pythonInstaller.PythonEmbeddablePackageExtractDir, SCRIPTS, PIP_DOT_EXE); 

            if (!File.Exists(filePath))
                return false;

            return
                PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));
        }

        private bool? ValidatePipInstallVirtualenv()
        {
            var filePath = Path.Combine(_pythonInstaller.PythonEmbeddablePackageExtractDir, SCRIPTS, VIRTUALENV_DOT_EXE);
            
            if (!File.Exists(filePath))
                return false;

            return
                PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));
        }

        private bool? ValidateCreateVirtualEnvironment(bool longValidate = false)
        {
            if (!Directory.Exists(_pythonInstaller.VirtualEnvironmentDir))
                return false;
            if (longValidate)
                return
                    PythonInstallerUtil.IsSignatureValid(_pythonInstaller.VirtualEnvironmentDir, PythonInstallerUtil.GetDirectoryHash(_pythonInstaller.VirtualEnvironmentDir));
            return true;
//            return PythonInstallerUtil.IsSignedFileOrDirectory(_pythonInstaller.VirtualEnvironmentDir);
        }
        internal static bool ValidateEnableLongpaths()
        {
            return PythonInstaller.SimulatedInstallationState != PythonInstaller.eSimulatedInstallationState.NAIVE && 
                   (int)Registry.GetValue(PythonInstaller.REG_FILESYSTEM_KEY, PythonInstaller.REG_LONGPATHS_ENABLED,0) == 1;
        }
        private bool? ValidatePipInstallPackages(bool longValidate = false)
        {
            if (!File.Exists(_pythonInstaller.VirtualEnvironmentPythonExecutablePath))
            {
                return false;
            }

            if (!Directory.Exists(_pythonInstaller.VirtualEnvironmentDir))
                return false;

            bool? signatureValid;
           
            if (!longValidate)
                signatureValid = PythonInstallerUtil.IsSignedFileOrDirectory(_pythonInstaller.VirtualEnvironmentDir);
            else
                signatureValid = PythonInstallerUtil.IsSignatureValid(_pythonInstaller.VirtualEnvironmentDir, 
                    PythonInstallerUtil.GetDirectoryHash(_pythonInstaller.VirtualEnvironmentDir));

            if (signatureValid == true || signatureValid == null)
                return true;

            var argumentsBuilder = new StringBuilder();
            argumentsBuilder.Append(PYTHON_MODULE_OPTION)
                .Append(SPACE)
                .Append(PIP)
                .Append(SPACE)
                .Append(FREEZE);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _pythonInstaller.VirtualEnvironmentPythonExecutablePath,
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
                var words = line.Split(new [] {EQUALS}, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length != 2)
                {
                    words = line.Split(new [] {AT_SPLITTER}, StringSplitOptions.RemoveEmptyEntries);
                }
                Assume.IsTrue(words.Length.Equals(2), string.Format(ToolsResources.PythonInstallerTaskValidator_ValidatePipInstallPackages_Failed_to_parse_package_name_and_version_from_entry___0__, line));
                packageToVersionMap.Add(words[0], words[1]);
            }

            foreach (var package in _pythonInstaller.PythonPackages)
            {
                if (!packageToVersionMap.ContainsKey(package.Name))
                {
                    return false;
                }
                if (!package.Version.IsNullOrEmpty())
                {
                    if (package.Version.StartsWith(GIT))
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
    }

    public class PythonTask
    {
        private OneOf<Action, Action<IProgressMonitor>, Action<ILongWaitBroker>> _action;
        public bool IsAction => IsActionWithNoArg || IsActionWithProgressMonitor || IsActionWithLongWaitBroker;
        public bool IsActionWithNoArg => _action.IsT0;
        public bool IsActionWithProgressMonitor => _action.IsT1;
        public bool IsActionWithLongWaitBroker => _action.IsT2;
        public Action AsActionWithNoArg => _action.AsT0;
        public Action<IProgressMonitor> AsActionWithProgressMonitor => _action.AsT1;
        public Action<ILongWaitBroker> AsActionWithLongWaitBroker => _action.AsT2;

        public Type ActionType
        {
            get
            {
                return _action.Match(
                    t0 => t0.GetType(),
                    t1 => t1.GetType(),
                    t2 => t2.GetType());
            }
        }

        public PythonTaskName Name { get; set; }

        public string InProgressMessage { get; set; }
        public string SuccessMessage { get; set; }
        public string FailureMessage { get; set; }

        public PythonTask(Action action)
        {
            _action = action;
        }

        public PythonTask(Action<IProgressMonitor> action)
        {
            _action = action;
        }

        public PythonTask(Action<ILongWaitBroker> action)
        {
            _action = action;
        }
    }

    internal class PythonTaskNode
    {
        public PythonTaskName PythonTaskName { get; set; }
        public List<PythonTaskNode> ParentNodes { get; set; }
    }

    internal class PythonInstallerException : Exception
    {
        public PythonInstallerException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    internal class PythonInstallerUnsupportedTaskException : PythonInstallerException
    {
        public PythonInstallerUnsupportedTaskException(PythonTask pythonTask)
            : base(string.Format(ToolsResources.PythonInstallerUnsupportedTaskException_Task_with_action_type__0__is_not_supported_by_PythonInstaller_yet, pythonTask.ActionType)) { }
    }

    internal class PythonInstallerUnsupportedTaskNameException : PythonInstallerException
    {
        public PythonInstallerUnsupportedTaskNameException(PythonTaskName pythonTaskName)
            : base(string.Format(ToolsResources.PythonInstallerUnsupportedTaskNameException_Task_with_task_name__0__is_not_supported_by_PythonInstaller_yet, pythonTaskName)) { }
    }
}

