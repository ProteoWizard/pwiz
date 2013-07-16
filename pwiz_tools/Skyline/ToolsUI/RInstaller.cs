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
using System.IO.Pipes;
using System.Linq;
using System.Net;
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
    // TODO: (trevor) long-term allow for ranges of installations
    // TODO: (trevor) long-term investigate the possibility of checking for currently installed packages?
    public partial class RInstaller : FormEx
    {

        private string Version { get; set; }
        private bool Installed { get; set; }
        private ICollection<string> PackageUris { get; set; }
        private TextWriter Writer { get; set; }

        public RInstaller(ProgramPathContainer rPathContainer, ICollection<string> packageUris, TextWriter writer)
            : this(rPathContainer, packageUris, CheckInstalled(rPathContainer.ProgramVersion), writer)
        {
        }

        public RInstaller(ProgramPathContainer rPathContainer, ICollection<string> packageUris, bool installed, TextWriter writer)
        {
            Version = rPathContainer.ProgramVersion;
            PackageUris = packageUris;
            Installed = installed;
            Writer = writer;
            InitializeComponent();
        }

        public bool IsLoaded { get; set; }

        private void RInstaller_Load(object sender, EventArgs e)
        {
            if (!Installed && PackageUris.Count != 0)
            {
                PopulatePackageCheckListBox();
                labelMessage.Text = string.Format(
                    Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0__and_the_following_packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_, Version);
            }
            else if (!Installed)
            {
                labelMessage.Text = string.Format(
                    Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0___Click_Install_to_begin_the_installation_process_, Version);
                int shift = btnCancel.Top - checkedListBoxPackages.Top;
                checkedListBoxPackages.Visible = checkedListBoxPackages.Enabled = false;
                Height -= shift;
            }
            else if (PackageUris.Count != 0)
            {
                PopulatePackageCheckListBox();
                labelMessage.Text = Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_the_following_R_Packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process;
            }

            IsLoaded = true;
        }

        private void PopulatePackageCheckListBox()
        {
            // add package names
            ICollection<string> packageNames = new Collection<string>();
            const string pattern = @"([^/]*)\.(zip|tar\.gz)$"; // Not L10N
            foreach (var package in PackageUris)
            {
                Match name = Regex.Match(package, pattern);
                packageNames.Add(name.Groups[1].ToString());
            }
            checkedListBoxPackages.DataSource = packageNames;

            // initially set them as checked
            for (int i = 0; i < checkedListBoxPackages.Items.Count; i++)
            {
                checkedListBoxPackages.SetItemChecked(i, true);
            }
        }

        private void btnInstall_Click(object sender, EventArgs e)
        {
            Hide();

            // if R is not installed, install it!
            if (!Installed)
                Installed = InstallR();

            // once R is installed, install Packages, if necessary
            bool packagesInstalled = false;
            if (Installed)
                packagesInstalled = GetPackages();

            // if both R and the associated packages are installed successfully, return YES, otherwise NO
            DialogResult = (Installed && packagesInstalled) ? DialogResult.Yes : DialogResult.No;
        }

        /// <summary>
        /// Returns true if R is installed successfully
        /// </summary>
        private bool InstallR()
        {
            // First, download the executable installer
            try
            {
                using (var dlg = new LongWaitDlg {Message = Resources.RInstaller_InstallR_Downloading_R, ProgressValue = 0})
                {
                    dlg.PerformWork(this, 500, DownloadR);
                }
            }
            catch (MessageException ex)
            {
                MessageDlg.Show(this, ex.Message);
                return false;
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException.GetType() == typeof(MessageException))
                {
                    MessageDlg.Show(this, ex.Message);
                    return false;
                }
                throw;
            }
            MessageDlg.Show(this, Resources.RInstaller_InstallR_Download_succeeded);                        

            // Then run the installer
            if (!RunInstaller())
            {
                MessageDlg.Show(this, Resources.RInstaller_InstallR_R_installation_was_not_completed__Cancelling_tool_installation_);
                return false;
            }
            return true;
        }

        private string DownloadPath { get; set; }

        private void DownloadR(ILongWaitBroker longWaitBroker)
        {
            // the repository containing the downloadable R exes
            const string baseUri = "http://cran.r-project.org/bin/windows/base/"; // Not L10N

            // format the file name, e.g. R-2.15.2-win.exe
            string exe = "R-" + Version + "-win.exe"; // Not L10N

            // create the download path for the file
            DownloadPath = Path.GetTempPath() + exe;

            // create the webclient
            using (var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(longWaitBroker, 2))
            {

                // first try downloading it as if it is the most recent release of R. The most
                // recent version is stored in a different location of the CRAN repo than older versions
                Uri versionUri = new Uri(baseUri + exe);
                bool successfulDownload = webClient.DownloadFileAsyncWithBroker(versionUri, DownloadPath);
                if (successfulDownload)
                    return;

                // otherwise, check and see if it is an older release
                versionUri = new Uri(baseUri + "old/" + Version + "/" + exe); // Not L10N
                successfulDownload = webClient.DownloadFileAsyncWithBroker(versionUri, DownloadPath);

                if (!successfulDownload)
                    throw new MessageException(
                        TextUtil.LineSeparate(
                            Resources.RInstaller_DownloadR_Download_failed, 
                            Resources.RInstaller_DownloadPackages_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_));
            }
        }

        private class MultiFileAsynchronousDownloadClient : IAsynchronousDownloadClient
        {
            private readonly ILongWaitBroker _longWaitBroker;
            private readonly WebClient _webClient;
            private bool DownloadComplete { get; set; }
            private bool SuccessfulDownload { get; set; }
            private int DownloadsCompleted { get; set; }
            
            /// <summary>
            /// The asynchronous download client links a webclient to a longwaitbroker; it supports
            /// multiple asynchronous downloads during the same instance of a longwaitdlg's perform work
            /// </summary>
            /// <param name="longWaitBroker">The associated longwaitbroker</param>
            /// <param name="filesToDownload">The number of files to download; this is used to update
            /// the associated longWaitDlg's progress bar</param>
            public MultiFileAsynchronousDownloadClient(ILongWaitBroker longWaitBroker, int filesToDownload)
            {
                _longWaitBroker = longWaitBroker;
                _webClient = new WebClient();
                DownloadsCompleted = 0;
                _webClient.DownloadProgressChanged += (sender, args) =>
                    {
                        longWaitBroker.ProgressValue = (int) (((((double) DownloadsCompleted)/filesToDownload)*100)
                                                       + ((1.0/filesToDownload)*args.ProgressPercentage));
                    };
                _webClient.DownloadFileCompleted += (sender, args) =>
                    {
                        DownloadsCompleted++;
                        SuccessfulDownload = (args.Error == null);
                        DownloadComplete = true;
                    };
            }

            /// <summary>
            /// Downloads a file asynchronously, updating the instances' wait dialog's progress bar
            /// </summary>
            /// <exception cref="MessageException">If the user cancels the download through the associated long wait dialog</exception>
            /// <returns>True if the download was successful, otherwise false</returns>
            public bool DownloadFileAsyncWithBroker(Uri address, string fileName)
            {
                // reset download status
                DownloadComplete = false;
                SuccessfulDownload = false;

                Match file = Regex.Match(address.AbsolutePath, @"[^/]*$"); // Not L10N
                _longWaitBroker.Message = string.Format(Resources.AsynchronousDownloadClient_DownloadFileAsyncWithBroker_Downloading__0_, file);
                _webClient.DownloadFileAsync(address, fileName);
                
                // while downloading, check to see if the user has canceled the operation
                while (!DownloadComplete)
                {
                    if (_longWaitBroker.IsCanceled)
                    {
                        _webClient.CancelAsync();
                        throw new MessageException(Resources.AsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled);
                    }
                }
                return SuccessfulDownload;
            }

            #region IDisposable Members

            public void Dispose()
            {
                _webClient.Dispose();
            }

            #endregion
        }

        /// <summary>
        /// Returns true if the installation was successful.
        /// </summary>
        private bool RunInstaller()
        {
            var processRunner = TestProcessRunner ?? new InstallProcessRunner();
            // an exit code of 0 indicates a successful installation
            return processRunner.RunProcess(new ProcessStartInfo {FileName = DownloadPath}) == 0;
        }

        private class InstallProcessRunner : IProcessRunner
        {
            public int RunProcess(ProcessStartInfo startInfo)
            {
                Process install = new Process { StartInfo = startInfo };
                install.Start();
                install.WaitForExit();
                int exitCode = install.ExitCode;
                install.Close();
                return exitCode;
            }
        }

        /// <summary>
        /// Returns true if packages were downloaded AND installed successfully
        /// </summary>
        /// <returns></returns>
        private bool GetPackages()
        {
            // only download the checked packages
            ICollection<Uri> packagesToDownload = new Collection<Uri>();
            int index = 0;
            foreach (var package in PackageUris)
            {
                if (checkedListBoxPackages.GetItemCheckState(index) == CheckState.Checked)
                    packagesToDownload.Add(new Uri(package));
                index++;
            }

            if (packagesToDownload.Count == 0)
                return true;

            try
            {
                string packagePaths = null;
                using (var dlg = new LongWaitDlg {Message = Resources.RInstaller_GetPackages_Downloading_packages})
                {
                    dlg.PerformWork(this, 1000,
                                    longWaitBroker =>
                                    packagePaths = DownloadPackages(packagesToDownload, longWaitBroker));
                }
                MessageDlg.Show(this, Resources.RInstaller_InstallR_Download_succeeded);
                InstallPackages(packagePaths);
            }
            catch (TargetInvocationException x)
            {
                if (x.InnerException is MessageException)
                {
                    MessageDlg.Show(this, x.InnerException.Message);
                    return false;
                }
            }
            catch (MessageException x)
            {
                MessageDlg.Show(this, x.Message);
                return false;
            }
            return true;
        }

        private string DownloadPackages(ICollection<Uri> packagesToDownload, ILongWaitBroker longWaitBroker)
        {
            // create the webclient
            using (var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(longWaitBroker, packagesToDownload.Count))
            {
                // store filepaths
                var packagePaths = new Collection<string>();

                // collect failed package installs
                var failedDownloads = new List<string>();

                // download each package
                foreach (var package in packagesToDownload)
                {
                    Match file = Regex.Match(package.AbsolutePath, @"([^/]*\.)(zip|tar\.gz)"); // Not L10N
                    string downloadPath = Path.GetTempPath() + file.Groups[1] + file.Groups[2];
                    if (webClient.DownloadFileAsyncWithBroker(package, downloadPath))
                    {
                        packagePaths.Add(downloadPath);
                    }
                    else
                        failedDownloads.Add(package.ToString());
                }

                if (failedDownloads.Count > 0)
                {
                    throw new MessageException(
                        TextUtil.LineSeparate(
                            Resources.RInstaller_DownloadPackages_Failed_to_download_the_following_packages_,
                            string.Empty,
                            TextUtil.LineSeparate(failedDownloads),
                            string.Empty,
                            Resources
                                .RInstaller_DownloadPackages_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_));
                }
                string result = CommandLine.ParseCommandLineArray(packagePaths.ToArray());
                return result;
            }
        }

        public bool PackageInstallError { get; set; }

        /// <summary>
        /// Returns true if packages were installed successfully
        /// </summary>
        private void InstallPackages(string packagePaths)
        {
            // ensure that there are Packages to install
            if (string.IsNullOrEmpty(packagePaths))
                return;

            // then get the program path
            string programPath = TestProgramPath ?? FindRProgramPath(Version);
            if (programPath == null)
                throw new MessageException(Resources.RInstaller_InstallPackages_Unknown_error_installing_packages);

            // create GUID
            string guidSuffix = string.Format("-{0}", Guid.NewGuid());

            var startInfo = new ProcessStartInfo
                {
                    FileName = "RPackageInstaller.exe", // Not L10N
                    Arguments = guidSuffix + " /C \"" + programPath + "\" CMD INSTALL " + packagePaths, // Not L10N
                    Verb = "runas" // Not L10N
                };

            string pipeName = "SkylineRPackageInstallPipe" + guidSuffix; // Not L10N

            using (var pipeStream = new NamedPipeServerStream(pipeName))
            {
                var installer = TestAsyncProcessRunner ?? new AsynchronousProcessRunner(startInfo);
                bool installationFinished = false;
                installer.Exited += (sender, args) => installationFinished = true;
                installer.Start();

                var namedPipeServerConnector = new NamedPipeServerConnector();
                // if(the connection fails)
                if (!(TestConnectionSuccess ?? namedPipeServerConnector.WaitForConnection(pipeStream, pipeName)))
                {
                    throw new MessageException(Resources.RInstaller_InstallPackages_Unknown_error_installing_packages);
                }
                else
                {
                    using (var reader = TestPipeStreamReader ?? new PipeStreamReaderWrapper(pipeStream))
                    {
                        reader.PropogateStream(Writer);
                    }

                    while (!installationFinished)
                    {
                        // wait for package installation process to finish
                    }

                    if (installer.GetExitCode() != 0)
                    {
                        PackageInstallError = true;
                        throw new MessageException(Resources.RInstaller_InstallPackages_Package_installation_failed__Error_log_in_immediate_window_);
                    }
                }
            }
        }

        #region Functional testing support

        // test classes for functional testing
        public IProcessRunner TestProcessRunner { get; set; }
        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        public IPipeStreamReaderWrapper TestPipeStreamReader { get; set; }
        public IAsynchronousProcessRunner TestAsyncProcessRunner { get; set; }
        public string TestProgramPath { get; set; }
        public bool? TestConnectionSuccess { get; set; }

        public string Message
        {
            get { return labelMessage.Text; }
        }

        public int PackagesListCount
        {
            get { return checkedListBoxPackages.Items.Count; }
        }

        public int PackagesListCheckedCount
        {
            get { return checkedListBoxPackages.CheckedItems.Count; }
        }

        public void UncheckAllPackages()
        {
            foreach (int packageIndex in checkedListBoxPackages.CheckedIndices)
            {
                checkedListBoxPackages.SetItemCheckState(packageIndex, CheckState.Unchecked);
            }
        }

        #endregion

        /// <summary>
        /// Wrapper class for an asynchronous process that supports user added exit event handling
        /// and exit codes
        /// </summary>
        private class AsynchronousProcessRunner : IAsynchronousProcessRunner
        {
            private readonly Process _process;
            public event EventHandler Exited
            {
                add { _process.Exited += value; }
                remove { _process.Exited += value; }
            }

            private int ExitCode { get; set; }

            public AsynchronousProcessRunner(ProcessStartInfo startInfo)
            {
                _process = new Process {StartInfo = startInfo, EnableRaisingEvents = true};
                _process.Exited += (sender, args) => ExitCode = _process.ExitCode;
            }

            public void Start()
            {
                _process.Start();
            }

            public int GetExitCode()
            {
                return ExitCode;
            }
        }

        /// <summary>
        /// Wrapper class for a streamreader that reads from a named pipe and writes
        /// it to Skyline's immediate window
        /// </summary>
        private class PipeStreamReaderWrapper : IPipeStreamReaderWrapper
        {
            private readonly StreamReader _streamReader;

            public PipeStreamReaderWrapper(Stream namedPipe)
            {
                _streamReader = new StreamReader(namedPipe);
            }

            public void PropogateStream(TextWriter writer)
            {
                var immediateWindow = writer as TextBoxStreamWriterHelper; 
                string line;
                while ((line = _streamReader.ReadLine()) != null)
                {
                    if (immediateWindow != null) immediateWindow.WriteLine(line);
                }
            }

            #region IDisposable Members

            public void Dispose()
            {
                _streamReader.Dispose();
            }

            #endregion
        }

        private const string REGISTRY_LOCATION = @"SOFTWARE\R-core\R\"; // Not L10N

        // Checks the registry to see if the specified version of R is installed on
        // the local machine, e.g. "2.15.2" or "3.0.0"
        /// <summary>
        /// Checks the registry to see if the specified version of R is installed on the local
        /// machine, e.g. "2.15.2" or "3.00"
        /// </summary>
        /// <param name="rVersion">The version to check</param>
        public static bool CheckInstalled(string rVersion)
        {
            RegistryKey softwareKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE"); // Not L10N
            if (softwareKey == null)
                return false;

            if (softwareKey.GetSubKeyNames().Contains("R-core")) // Not L10N
            {
                RegistryKey rKey = Registry.LocalMachine.OpenSubKey(REGISTRY_LOCATION);
                if (rKey == null)
                    return false;

                foreach (var localVersion in rKey.GetSubKeyNames())
                {
                    if (localVersion.Equals(rVersion))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Finds the program path for the specified version of R.
        /// </summary>
        /// <param name="rVersion">The version of R, e.g. "2.15.2"</param>
        public static string FindRProgramPath(string rVersion)
        {
            RegistryKey rKey = Registry.LocalMachine.OpenSubKey(REGISTRY_LOCATION + rVersion);
            if (rKey == null)
                return null;

            string installPath = rKey.GetValue("InstallPath") as string; // Not L10N
            if (installPath == null)
                return null;

            return installPath + "\\bin\\R.exe"; // Not L10N
        }

    }

    // interfaces to support wrapper classes for downloading, running processes, using named pipes and writing over
    // streams. These support automated functional testing of the class

    public interface IAsynchronousDownloadClient : IDisposable
    {
        /// <returns>True if the given file was downloaded successfully</returns>
        bool DownloadFileAsyncWithBroker(Uri address, string fileName);
    }

    public interface IProcessRunner
    {
        /// <returns>The exit code of the process</returns>
        int RunProcess(ProcessStartInfo startInfo);
    }

    public interface IAsynchronousProcessRunner
    {
        event EventHandler Exited;
        void Start();
        int GetExitCode();
    }

    public interface IPipeStreamReaderWrapper : IDisposable
    {
        void PropogateStream(TextWriter writer);
    }

}

