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
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public partial class PythonInstaller : FormEx
    {
        private readonly string _version;
        private readonly bool _installed;
        private readonly TextWriter _writer;

        private IList<string> PackageUris { get; set; }
        private IList<string> LocalPackages { get; set; } 

        public PythonInstaller(ProgramPathContainer pythonPathContainer, IEnumerable<string> packages, TextWriter writer)
            : this(pythonPathContainer, packages, PythonUtil.CheckInstalled(pythonPathContainer.ProgramVersion), writer)
        {
        }

        public PythonInstaller(ProgramPathContainer pythonPathContainer, IEnumerable<string> packages, bool installed, TextWriter writer)
        {
            _version = pythonPathContainer.ProgramVersion;
            _installed = installed;
            _writer = writer;
            AssignPackages(packages);
            InitializeComponent();
        }

        private void AssignPackages(IEnumerable<string> packages)
        {
            PackageUris = new List<string>();
            LocalPackages = new List<string>();
            foreach (var package in packages)
            {
                if (package.StartsWith("http")) // Not L10N
                    PackageUris.Add(package);
                else
                    LocalPackages.Add(package);
            }
        }

        public bool IsLoaded { get; private set; }

        private void PythonInstaller_Load(object sender, EventArgs e)
        {
            if (!_installed && (PackageUris.Count + LocalPackages.Count) != 0)
            {
                labelMessage.Text = string.Format(Resources.PythonInstaller_PythonInstaller_Load_This_tool_requires_Python__0__and_the_following_packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_, _version);
                PopulatePackageCheckListBox();
            } else if (!_installed)
            {
                labelMessage.Text = string.Format(Resources.PythonInstaller_PythonInstaller_Load_This_tool_requires_Python__0___Click_install_to_begin_the_installation_process_, _version);
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
            const string pattern = @"([^/\\]*)\.(zip|tar\.gz|exe)$"; // Not L10N
            foreach (var package in packages)
            {
                Match name = Regex.Match(package, pattern);
                packageNames.Add(name.Groups[1].ToString());
            }
            return packageNames;
        } 

        private void btnInstall_Click(object sender, EventArgs e)
        {
            OkDialog();
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

        private bool GetPython()
        {
            try
            {
                using (var waitDlg = new LongWaitDlg{ProgressValue = 0})
                {
                    // Short wait, because this can't possible happen fast enough to avoid
                    // showing progress, except in testing
                    waitDlg.PerformWork(this, 50, DownloadPython);
                }
                using (var waitDlg = new LongWaitDlg(null, false) {Message = Resources.PythonInstaller_GetPython_Installing_Python})
                {
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

        private void DownloadPython(ILongWaitBroker waitBroker)
        {
            // the base Url for python releases
            const string baseUri = "http://python.org/ftp/python/"; // Not L10N

            // the installer file name, e.g. python-2.7.msi
            string fileName = "python-" + _version + ".msi"; // Not L10N

            // the fully formed Uri, e.g. http://python.org/ftp/python/2.7/python-2.7.msi
            var downloadUri = new Uri(baseUri + _version + "/" + fileName); // Not L10N

            using (var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(waitBroker, 1))
            {
                if (!webClient.DownloadFileAsync(downloadUri, DownloadPath = Path.GetTempPath() + fileName))
                    throw new ToolExecutionException(TextUtil.LineSeparate(
                        Resources.PythonInstaller_DownloadPython_Download_failed_, 
                        Resources.PythonInstaller_DownloadPython_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_));
            }
        }

        private void InstallPython()
        {
            var processRunner = TestRunProcess ?? new SynchronousRunProcess();
            var startInfo = new ProcessStartInfo
                {
                    FileName = "msiexec", // Not L10N
                    Arguments = "/i \"" + DownloadPath + "\"", // Not L10N
                };
            if (processRunner.RunProcess(new Process {StartInfo = startInfo}) != 0)
                throw new ToolExecutionException(Resources.PythonInstaller_InstallPython_Python_installation_failed__Canceling_tool_installation_);
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
                using (var waitDlg = new LongWaitDlg{ProgressValue = 0})
                {
                    waitDlg.PerformWork(this, 500, longWaitBroker => packagePaths = DownloadPackages(longWaitBroker, downloadablePackages));
                }

                // separate packages
                ICollection<string> exePaths = new Collection<string>();
                ICollection<string> sourcePaths = new Collection<string>();
                foreach (var package in (packagePaths == null) ? localPackages : packagePaths.Concat(localPackages))
                {
                    if (package.EndsWith(".exe")) // Not L10N
                        exePaths.Add(package);
                    else
                        sourcePaths.Add(package);
                }

                // first install executable packages, if any
                if (exePaths.Count != 0)
                {
                    using (var waitDlg = new LongWaitDlg(null, false) { Message = Resources.PythonInstaller_GetPackages_Installing_Packages })
                    {
                        waitDlg.PerformWork(this, 500, () => InstallExecutablePackages(exePaths));
                    }   
                }

                // then install source paths, if any
                if (sourcePaths.Count != 0)
                {
                    // try and find the path to the pip package manager .exe
                    string pipPath = PythonUtil.GetPipPath(_version);

                    // if it can't be found, install it
                    if (pipPath == null || TestingPip)
                    {
                        DialogResult result = MultiButtonMsgDlg.Show(
                            this,
                            Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_,
                            Resources.PythonInstaller_InstallPackages_Install);
                        if (result == DialogResult.OK && GetPip())
                        {
                            pipPath = PythonUtil.GetPipPath(_version);
                            MessageDlg.Show(this, Resources.PythonInstaller_InstallPackages_Pip_installation_complete_);
                        }
                        else
                        {
                            MessageDlg.Show(this, Resources.PythonInstaller_InstallPackages_Python_package_installation_cannot_continue__Canceling_tool_installation_);
                            return false;
                        }
                    }

                    using (var waitDlg = new LongWaitDlg(null, false) { Message = Resources.PythonInstaller_GetPackages_Installing_Packages })
                    {
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

        private IEnumerable<string> DownloadPackages(ILongWaitBroker waitBroker, IEnumerable<string> packagesToDownload)
        {
            ICollection<string> downloadPaths = new Collection<string>();
            ICollection<string> failedDownloads = new Collection<string>();

            using (var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(waitBroker, PackageUris.Count))
            {
                foreach (var package in packagesToDownload)
                {
                    Match file = Regex.Match(package, @"[^/]*$"); // Not L10N
                    string downloadPath = Path.GetTempPath() + file;
                    if (webClient.DownloadFileAsync(new Uri(package), downloadPath))
                    {
                        downloadPaths.Add(downloadPath);
                    }
                    else
                    {
                        failedDownloads.Add(package);
                    }
                }        
            }

            if (failedDownloads.Count != 0)
            {
                throw new ToolExecutionException(
                        TextUtil.LineSeparate(
                            Resources.PythonInstaller_DownloadPackages_Failed_to_download_the_following_packages_,
                            string.Empty,
                            TextUtil.LineSeparate(failedDownloads),
                            string.Empty,
                            Resources.PythonInstaller_DownloadPython_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_));
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

                var argumentBuilder = new StringBuilder("echo installing packages"); // Not L10N
                foreach (var package in packages)
                {
                    // ReSharper disable NonLocalizedString
                    argumentBuilder.Append(" & ")
                                 .Append(pipPath)
                                 .Append(" install ")
                                 .Append("\"")
                                 .Append(package)
                                 .Append("\""); 
                    // ReSharper restore NonLocalizedString
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
                using (var dlg = new LongWaitDlg{ProgressValue = 0})
                {
                    // Short wait, because this can't possible happen fast enough to avoid
                    // showing progress, except in testing
                    dlg.PerformWork(this, 50, DownloadPip);
                }
                using (var dlg = new LongWaitDlg(null, false) {Message = Resources.PythonInstaller_GetPip_Installing_Pip})
                {
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

        private void DownloadPip(ILongWaitBroker longWaitBroker)
        {
            // location of the setuptools install script
            const string setupToolsScript = "https://bitbucket.org/pypa/setuptools/downloads/ez_setup.py";  // Not L10N
            SetupToolsPath = Path.GetTempPath() + "ez_setup.py";    // Not L10N

            // location of the pip install script
            const string pipScript = "https://raw.github.com/pypa/pip/master/contrib/get-pip.py";   // Not L10N
            PipPath = Path.GetTempPath() + "get-pip.py";    // Not L10N

            using (var webClient = TestPipDownloadClient ?? new MultiFileAsynchronousDownloadClient(longWaitBroker, 2))
            {
                if (!webClient.DownloadFileAsync(new Uri(setupToolsScript), SetupToolsPath) ||
                    !webClient.DownloadFileAsync(new Uri(pipScript), PipPath))
                {
                    throw new ToolExecutionException(Resources.PythonInstaller_DownloadPip_Download_failed__Check_your_network_connection_or_contact_Skyline_developers_);
                }
            }
        }

        private void InstallPip()
        {
            var argumentBuilder = new StringBuilder();
            string pythonPath = PythonUtil.GetProgramPath(_version);
            argumentBuilder.Append(pythonPath)
                           .Append(TextUtil.SEPARATOR_SPACE)
                           .Append(SetupToolsPath)
                           .Append(" & ") // Not L10N
                           .Append(pythonPath)
                           .Append(TextUtil.SEPARATOR_SPACE)
                           .Append(PipPath); // Not L10N

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

        #region Functional testing support

        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        public IRunProcess TestRunProcess { get; set; }
        public ISkylineProcessRunnerWrapper TestSkylineProcessRunner { get; set; }
        public IAsynchronousDownloadClient TestPipDownloadClient { get; set; }
        public ISkylineProcessRunnerWrapper TestPipeSkylineProcessRunner { get; set; }
        public IRunProcess TestPipRunProcess { get; set; }
        public bool TestingPip { get; set; }
        
        public string Message
        {
            get { return labelMessage.Text; }
        }

        public int PackagesListCount
        {
            get { return clboxPackages.Items.Count; }
        }

        public int CheckedPackagesCount
        {
            get { return clboxPackages.CheckedItems.Count; }
        }

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
            return (pythonKey != null) ? pythonKey.GetValue(null) + ("python.exe") : null; // Not L10N
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
                string path = pythonKey.GetValue(null) + "scripts\\pip.exe";  // Not L10N
                return File.Exists(path) ? path : null;
            }
            return null;
        }

        private const string PYTHON_X64_LOCATION = @"SOFTWARE\Wow6432Node\Python\PythonCore\"; // Not L10N
        private const string PYTHON_X86_LOCATION = @"SOFTWARE\Python\PythonCore\";             // Not L10N
        private const string INSTALL_DIR = "\\InstallPath";                                    // Not L10N

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
            Match versionBase = Regex.Match(version, @"(^[0-9]+\.[0-9]+).*"); // Not L10N
            return versionBase.Groups[1].ToString();
        }
    }
}
