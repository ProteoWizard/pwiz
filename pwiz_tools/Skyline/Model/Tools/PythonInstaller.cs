using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Ionic.Zip;
using Microsoft.Win32;
using OneOf;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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
        internal const string REG_ADD_COMMAND = @"reg add";
        internal const string REG_FILESYSTEM_KEY = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem";
        internal const string REG_LONGPATHS_ENABLED = @"LongPathsEnabled";
        internal const string REG_LONGPATH_TYPE = @"/t REG_DWORD";
        internal const string REG_LONGPATH_VALUE = @"/d 0x00000001";
        internal const string REG_LONGPATH_ZERO = @"/d 0x00000000";
        internal const string REG_LONGPATH_FORCE = @"/f";
        internal string REG_LONGPATH_NAME = $@"/v {REG_LONGPATHS_ENABLED}";

        private static string CUDA_VERSION = @"12.6.3";

        //private static readonly string CUDA_INSTALLER_URL = $@"https://developer.download.nvidia.com/compute/cuda/{CUDA_VERSION}/local_installers/";
        private static readonly string CUDA_INSTALLER_URL = $@"https://developer.download.nvidia.com/compute/cuda/{CUDA_VERSION}/network_installers/";

        //private string CUDA_INSTALLER = $@"cuda_{CUDA_VERSION_FULL}_windows.exe";
        private string CUDA_INSTALLER = $@"cuda_{CUDA_VERSION}_windows_network.exe";
        public Uri CudaDownloadUri => new Uri(CUDA_INSTALLER_URL+CUDA_INSTALLER);
        public string CudaDownloadPath => Path.Combine(CudaVersionDir, CudaInstallerDownloadPath);
        public bool? NvidiaGpuAvailable { get; internal set; }
        public int NumTotalTasks { get; set; }
        public int NumCompletedTasks { get; set; }
        public string PythonVersion { get; }
        public List<PythonPackage> PythonPackages { get; }
        public string PythonEmbeddablePackageFileName => PythonEmbeddablePackageFileBaseName + DOT_ZIP;
        public Uri PythonEmbeddablePackageUri => new Uri(PYTHON_FTP_SERVER_URL + PythonVersion + FORWARD_SLASH + PythonEmbeddablePackageFileName);
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
        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        public IAsynchronousDownloadClient TestPipDownloadClient { get; set; }
        public ISkylineProcessRunnerWrapper TestPipeSkylineProcessRunner { get; set; }
        /// <summary>
        /// For testing purpose only. Setting this property will bypass the TaskValidator
        /// </summary>
        public List<PythonTaskName> TestPythonVirtualEnvironmentTaskNames { get; set; }
        #endregion
        public string PythonVersionDir => Path.Combine(PythonRootDir, PythonVersion);
        private string CudaVersionDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"cuda", CUDA_VERSION);
        public string CudaInstallerDownloadPath => Path.Combine(CudaVersionDir, CUDA_INSTALLER);

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
        private bool? TestForNvidiaGPU()
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
            PythonVersion = pythonPathContainer.ProgramVersion;
            Writer = writer;
            TaskValidator = taskValidator;
            VirtualEnvironmentName = virtualEnvironmentName;
            PythonPackages = packages.ToList();
            PendingTasks = new List<PythonTask>();
            Directory.CreateDirectory(PythonRootDir);
            Directory.CreateDirectory(PythonVersionDir);
            NvidiaGpuAvailable = TestForNvidiaGPU();
        }

        public bool IsPythonVirtualEnvironmentReady()
        {
            // TODO(xgwang): make sure terminal window does not show when validating
            var tasks = PendingTasks.IsNullOrEmpty() ? ValidatePythonVirtualEnvironment() : PendingTasks;
            return tasks.Count == 0;
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

            var taskNodes = PythonInstallerUtil.GetPythonTaskNodes();
            var hasSeenFailure = false;
            foreach (var taskNode in taskNodes)
            {
                var isTaskValid = TaskValidator.Validate(taskNode.PythonTaskName, this);
                if (hasSeenFailure)
                {
                    if (isTaskValid && null == taskNode.ParentNodes) { continue; }
                   
                    bool havePrerequisite = false;

                    if (taskNode.ParentNodes == null)
                    {
                        tasks.Add(GetPythonTask(taskNode.PythonTaskName));
                        continue;
                    }
                    else
                    {
                        foreach (var parentTask in taskNode.ParentNodes)
                        {
                            if (tasks.Where(p => p.Name == parentTask.PythonTaskName).ToArray().Length > 0)
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
                    if (isTaskValid) { continue; }
                    hasSeenFailure = true;
                }

                PythonTask nextTask = GetPythonTask(taskNode.PythonTaskName);
                if (nextTask != null) tasks.Add(nextTask);
            }
            PendingTasks = tasks;
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
                    var task4 = new PythonTask(() => EnableWindowsLongPaths());
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
                    var task6 = new PythonTask(RunGetPipScript);
                    task6.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Running_the_get_pip_py_script;
                    task6.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_run_the_get_pip_py_script;
                    task6.Name = pythonTaskName;
                    return task6;
                case PythonTaskName.pip_install_virtualenv:
                    var virtualEnvPackage = new PythonPackage { Name = VIRTUALENV, Version = null };
                    var task7 = new PythonTask(() => PipInstall(BasePythonExecutablePath, new[] { virtualEnvPackage }));
                    task7.InProgressMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Running_pip_install__0_, VIRTUALENV);
                    task7.FailureMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_run_pip_install__0_, VIRTUALENV);
                    task7.Name = pythonTaskName;
                    return task7;
                case PythonTaskName.create_virtual_environment:
                    var task8 = new PythonTask(() => RunPythonModule(
                        BasePythonExecutablePath, PythonVersionDir, VIRTUALENV, new[] { VirtualEnvironmentName }));
                    task8.InProgressMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Creating_virtual_environment__0_, VirtualEnvironmentName);
                    task8.FailureMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_create_virtual_environment__0_, VirtualEnvironmentName);
                    task8.Name = pythonTaskName;
                    return task8;
                case PythonTaskName.pip_install_packages:
                    var task9= new PythonTask(() => PipInstall(VirtualEnvironmentPythonExecutablePath, PythonPackages));
                    task9.InProgressMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Installing_Python_packages_in_virtual_environment__0_, VirtualEnvironmentName);
                    task9.FailureMessage = string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_install_Python_packages_in_virtual_environment__0_, VirtualEnvironmentName);
                    task9.Name = pythonTaskName;
                    return task9;
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
                        var task10 = new PythonTask(InstallCudaLibrary);
                        task10.InProgressMessage = ToolsResources.PythonInstaller_GetPythonTask_Installing_Cuda;
                        task10.FailureMessage = ToolsResources.PythonInstaller_GetPythonTask_Failed_to_install_Cuda;
                        task10.Name = pythonTaskName;
                        return task10;
                    }
                    else
                    {
                        return null;
                    }
                default:
                    throw new PythonInstallerUnsupportedTaskNameException(pythonTaskName);
            }
        }

        public void EnableWindowsLongPaths()
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

            if (processRunner.RunProcess(cmd, true, Writer) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));

        }
        public void DisableWindowsLongPaths()
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

            if (processRunner.RunProcess(cmd, true, Writer) != 0)
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
            if (pipedProcessRunner.RunProcess(cmd, false, Writer) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));
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
            zipFile.ExtractAll(PythonEmbeddablePackageExtractDir);
        }

        private void EnableSearchPathInPythonEmbeddablePackage()
        {
            var files = Directory.GetFiles(PythonEmbeddablePackageExtractDir, "python*._pth");
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
        }

        private void RunGetPipScript(IProgressMonitor progressMonitor)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append(BasePythonExecutablePath)
                .Append(SPACE)
                .Append(GetPipScriptDownloadPath);
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, cmdBuilder, CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            if (pipedProcessRunner.RunProcess(cmd, false, Writer) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));

            var filePath = Path.Combine(PythonEmbeddablePackageExtractDir, SCRIPTS, PIP_EXE);
            PythonInstallerUtil.SignFile(filePath);

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
                string arg;
                if (package.Version.IsNullOrEmpty())
                {
                    arg = package.Name;
                } 
                else if (package.Version.StartsWith(GIT))
                {
                    arg = package.Version;
                }
                else
                {
                    arg = package.Name + EQUALS + package.Version;
                }
                arg = TextUtil.Quote(arg);
                cmdBuilder.Append(arg).Append(SPACE);
            }
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, cmdBuilder, CMD_PROCEEDING_SYMBOL);
            cmd += string.Format(ToolsResources.PythonInstaller_PipInstall__0__This_sometimes_could_take_3_5_minutes__Please_be_patient___1__, ECHO, CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            if (pipedProcessRunner.RunProcess(cmd, false, Writer) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));
         
            var filePath = Path.Combine(PythonEmbeddablePackageExtractDir, SCRIPTS, VIRTUALENV);
            PythonInstallerUtil.SignFile(filePath+DOT_EXE);
            PythonInstallerUtil.SignDirectory(VirtualEnvironmentDir);

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
            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO, GetEscapedCmdString(cmdBuilder.ToString()), CMD_PROCEEDING_SYMBOL);
            cmd += cmdBuilder;
            var pipedProcessRunner = TestPipeSkylineProcessRunner ?? new SkylineProcessRunnerWrapper();
            if (pipedProcessRunner.RunProcess(cmd, false, Writer) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmdBuilder));
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
            DeleteDirectory(PythonVersionDir);
            DeleteDirectory(Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), name));
        }
      
        static void DeleteDirectory(string targetDir)
        {
            string longPath = $@"\\?\{targetDir}";
            if (!Directory.Exists(targetDir))
                return;

            // Get all files in the directory
            string[] files = Directory.GetFiles(targetDir);
            foreach (string file in files)
            {
                // Delete files
                File.SetAttributes($@"\\?\{file}", FileAttributes.Normal);
                File.Delete($@"\\?\{file}");
            }

            // Get all subdirectories
            string[] directories = Directory.GetDirectories(targetDir);
            foreach (string dir in directories)
            {
                // Recursively delete subdirectories
                DeleteDirectory(dir);
            }

            // Finally, delete the directory itself
            Directory.Delete(longPath, false);
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
        internal static List<PythonTaskNode> GetPythonTaskNodes()
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
            var node10 = new PythonTaskNode { PythonTaskName = PythonTaskName.download_cuda_library, ParentNodes = null };
            var node11 = new PythonTaskNode { PythonTaskName = PythonTaskName.install_cuda_library, ParentNodes = null };
            return new List<PythonTaskNode> { node1, node2, node3, node4, node5, node6, node7, node8, node9 }; //TODO: , node10, node11 };
        }

        public static string GetFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead($@"\\?\{filePath}"))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace(@"-", "").ToLowerInvariant();
                }
            }
        }

        public static bool IsSignatureValid(string path, string signature)
        {
            if (!IsSignedFileOrDirectory(path))
                return false;
            return signature == File.ReadAllText(Path.GetFullPath(path) + SIGNATURE_EXTENSION);
        }

        public static bool IsSignedFileOrDirectory(string path)
        {
            return File.Exists(Path.GetFullPath(path) + SIGNATURE_EXTENSION);
        }

        public static void SignFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            string signatureFile = Path.GetFullPath(filePath) + SIGNATURE_EXTENSION;
            File.WriteAllText(signatureFile, GetFileHash(filePath));
        }

        public static void SignDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;
            string signatureFile = Path.GetFullPath(dirPath) + SIGNATURE_EXTENSION;
            File.WriteAllText(signatureFile, GetDirectoryHash(dirPath));
        }
        public static string GetDirectoryHash(string directoryPath)
        {
            string[] filesArray = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
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
                        using (var fileStream = new FileStream( $@"\\?\{filesArray[fileCount]}", FileMode.Open))
                        {
                            // Copy file contents to the combined stream
                            fileStream.CopyTo(combinedStream);
                            // Add a separator or file name to differentiate between files
                            var separator = Encoding.UTF8.GetBytes(Path.GetFileName(filesArray[fileCount]));
                            combinedStream.Write(separator, 0, separator.Length);
                        }
                    }

                    combinedStream.Seek(0, SeekOrigin.Begin); // Reset stream position
                    // Compute hash of combined stream
                    var hashBytes = sha.ComputeHash(combinedStream);
                    return BitConverter.ToString(hashBytes).Replace(@"-", @"").ToLower();
                }
            }
        }
        public static void UnblockFile(string filePath)
        {
            // Construct the PowerShell command
            TextWriter writer = new TextBoxStreamWriterHelper();
            var command = TextUtil.Quote($@"Unblock-File -Path '{filePath}'");

            var cmd = $@"powershell.exe -Command {command}";

            // Prepare the ProcessStartInfo
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var processRunner = new SkylineProcessRunnerWrapper();

            if (processRunner.RunProcess(cmd, true, writer) != 0)
                throw new ToolExecutionException(string.Format(ToolsResources.PythonInstaller_Failed_to_execute_command____0__, cmd));

        }
    }

    public interface IPythonInstallerTaskValidator
    {
        bool Validate(PythonTaskName pythonTaskName, PythonInstaller pythonInstaller);
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
        install_cuda_library
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
            { PythonTaskName.download_cuda_library, false }
        };

        public bool Validate(PythonTaskName pythonTaskName, PythonInstaller pythonInstaller)
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
        private bool ValidateDownloadCudaLibrary()
        {
            return GetTaskValidationResult(PythonTaskName.download_cuda_library);
        }
        private bool ValidateInstallCudaLibrary()
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
        public bool Validate(PythonTaskName pythonTaskName, PythonInstaller pythonInstaller)
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
                    //TODO: implement me return ValidateInstallCudaLibrary();
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
                new PythonTaskAndHash(PythonTaskName.download_python_embeddable_package, @"90f12b2475290459e1800d5170bdb5ef444f3803fdf2994edd7cfcd2c92a88ab"),
                new PythonTaskAndHash(PythonTaskName.unzip_python_embeddable_package, @"1ac00589117bc386ded40a44b99cb357c15f3f35443d3599370e4f750cdba678"),
                new PythonTaskAndHash(PythonTaskName.enable_search_path_in_python_embeddable_package, @"95f29168dc5cf35585a501bf35ec865383300bfac0e2222c7ec7c02ca7bde475"),
                new PythonTaskAndHash(PythonTaskName.download_getpip_script, @"96e58b5962f307566141ea9b393e136cbdf811db9f02968dc5bc88f43989345c"),
                new PythonTaskAndHash(PythonTaskName.download_cuda_library, @"ABCD123"),
                new PythonTaskAndHash(PythonTaskName.install_cuda_library, @"ABCD123")

            };
        private bool ValidateDownloadCudaLibrary()
        {
            if (!File.Exists(_pythonInstaller.CudaInstallerDownloadPath))
                return false;
            var computeHash =
                PythonInstallerUtil.GetFileHash(_pythonInstaller.CudaInstallerDownloadPath);
            var storedHash = TargetsAndHashes.Where(m => m.Task == PythonTaskName.download_cuda_library).ToArray()[0].Hash;
            return computeHash == storedHash;
        }
        private bool ValidateDownloadPythonEmbeddablePackage()
        {
            if (!File.Exists(_pythonInstaller.PythonEmbeddablePackageDownloadPath))
                return false;
            var computeHash = PythonInstallerUtil.GetFileHash(_pythonInstaller.PythonEmbeddablePackageDownloadPath);
            var storedHash = TargetsAndHashes.Where(m => m.Task == PythonTaskName.download_python_embeddable_package).ToArray()[0].Hash;
            return computeHash == storedHash;
        }

        private bool ValidateUnzipPythonEmbeddablePackage()
        {
            if (!Directory.Exists(_pythonInstaller.PythonEmbeddablePackageExtractDir))
                return false;
            
            return
                PythonInstallerUtil.IsSignatureValid(_pythonInstaller.PythonEmbeddablePackageExtractDir,
                PythonInstallerUtil.GetDirectoryHash(_pythonInstaller.PythonEmbeddablePackageExtractDir));
        }

        private bool ValidateEnableSearchPathInPythonEmbeddablePackage()
        {
            if (!Directory.Exists(_pythonInstaller.PythonEmbeddablePackageExtractDir))
                return false;
            
            var disabledPathFiles = Directory.GetFiles(_pythonInstaller.PythonEmbeddablePackageExtractDir, "python*._pth");
            var enabledPathFiles = Directory.GetFiles(_pythonInstaller.PythonEmbeddablePackageExtractDir, "python*.pth");
            var computeHash =
                PythonInstallerUtil.GetFilesArrayHash(enabledPathFiles);
            var storedHash = TargetsAndHashes.Where(m => m.Task == PythonTaskName.enable_search_path_in_python_embeddable_package).ToArray()[0].Hash;
            return computeHash == storedHash;
        }

        private bool ValidateDownloadGetPipScript()
        {
            if (!File.Exists(_pythonInstaller.GetPipScriptDownloadPath))
                return false;
            var computeHash = PythonInstallerUtil.GetFileHash(_pythonInstaller.GetPipScriptDownloadPath);
            var storedHash = TargetsAndHashes.Where(m => m.Task == PythonTaskName.download_getpip_script).ToArray()[0].Hash;
            return computeHash == storedHash;
        }

        private bool ValidateRunGetPipScript()
        {
            var filePath = Path.Combine(_pythonInstaller.PythonEmbeddablePackageExtractDir, SCRIPTS, PIP_DOT_EXE); 

            if (!File.Exists(filePath))
                return false;

            return
                PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));
        }

        private bool ValidatePipInstallVirtualenv()
        {
            var filePath = Path.Combine(_pythonInstaller.PythonEmbeddablePackageExtractDir, SCRIPTS, VIRTUALENV_DOT_EXE);
            
            if (!File.Exists(filePath))
                return false;

            return
                PythonInstallerUtil.IsSignatureValid(filePath, PythonInstallerUtil.GetFileHash(filePath));
        }

        private bool ValidateCreateVirtualEnvironment()
        {
            if (!Directory.Exists(_pythonInstaller.VirtualEnvironmentDir))
                return false;
            
            return
                PythonInstallerUtil.IsSignatureValid(_pythonInstaller.VirtualEnvironmentDir, PythonInstallerUtil.GetDirectoryHash(_pythonInstaller.VirtualEnvironmentDir));
        }
        internal static bool ValidateEnableLongpaths()
        {
            return (int) Registry.GetValue(PythonInstaller.REG_FILESYSTEM_KEY, PythonInstaller.REG_LONGPATHS_ENABLED, 0) == 1;
        }
        private bool ValidatePipInstallPackages()
        {
            if (!File.Exists(_pythonInstaller.VirtualEnvironmentPythonExecutablePath))
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
                FileName = _pythonInstaller.VirtualEnvironmentPythonExecutablePath,
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

            if (!Directory.Exists(_pythonInstaller.VirtualEnvironmentDir))
                return false;

            return
                PythonInstallerUtil.IsSignatureValid(_pythonInstaller.VirtualEnvironmentDir, PythonInstallerUtil.GetDirectoryHash(_pythonInstaller.VirtualEnvironmentDir));
        
        }
    }

    public class PythonTask
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

