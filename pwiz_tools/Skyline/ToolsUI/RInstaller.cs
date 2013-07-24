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
    // TODO: (trevor) long-term allow for ranges of installations
    // TODO: (trevor) long-term investigate the possibility of checking for currently installed packages?
    public partial class RInstaller : FormEx
    {

        private readonly string _version;
        private readonly bool _installed;
        private readonly ICollection<string> _packageUris;
        private readonly TextWriter _writer;

        public RInstaller(ProgramPathContainer rPathContainer, ICollection<string> packageUris, TextWriter writer)
            : this(rPathContainer, packageUris, RUtil.CheckInstalled(rPathContainer.ProgramVersion), writer)
        {
        }

        public RInstaller(ProgramPathContainer rPathContainer, ICollection<string> packageUris, bool installed, TextWriter writer)
        {
            _version = rPathContainer.ProgramVersion;
            _packageUris = packageUris;
            _installed = installed;
            _writer = writer;
            InitializeComponent();
        }

        public bool IsLoaded { get; set; }

        private void RInstaller_Load(object sender, EventArgs e)
        {
            if (!_installed && _packageUris.Count != 0)
            {
                PopulatePackageCheckListBox();
                labelMessage.Text = string.Format(
                    Resources
                        .RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0__and_the_following_packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_,
                    _version);
            }
            else if (!_installed)
            {
                labelMessage.Text = string.Format(
                    Resources
                        .RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0___Click_Install_to_begin_the_installation_process_,
                    _version);
                int shift = btnCancel.Top - checkedListBoxPackages.Top;
                checkedListBoxPackages.Visible = checkedListBoxPackages.Enabled = false;
                Height -= shift;
            }
            else if (_packageUris.Count != 0)
            {
                PopulatePackageCheckListBox();
                labelMessage.Text =
                    Resources
                        .RInstaller_RInstaller_Load_This_tool_requires_the_use_of_the_following_R_Packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process;
            }

            IsLoaded = true;
        }

        private void PopulatePackageCheckListBox()
        {
            // add package names
            ICollection<string> packageNames = new Collection<string>();
            const string pattern = @"([^/]*)\.(zip|tar\.gz)$"; // Not L10N
            foreach (var package in _packageUris)
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

            if ((_installed || GetR()) && (checkedListBoxPackages.CheckedItems.Count == 0 || GetPackages()))
            {
                DialogResult = DialogResult.Yes;
            }
            else
            {
                DialogResult = DialogResult.No;
            }
        }

        private bool GetR()
        {
            try
            {
                using (
                    var dlg = new LongWaitDlg {Message = Resources.RInstaller_InstallR_Downloading_R, ProgressValue = 0}
                    )
                {
                    dlg.PerformWork(this, 500, DownloadR);
                }
                InstallR();
                MessageDlg.Show(this, Resources.RInstaller_GetR_R_installation_complete);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException.GetType() == typeof (MessageException))
                {
                    MessageDlg.Show(this, ex.Message);
                    return false;
                }
                throw;
            }
            catch (MessageException ex)
            {
                MessageDlg.Show(this, ex.Message);
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
            string exe = "R-" + _version + "-win.exe"; // Not L10N

            // create the download path for the file
            DownloadPath = Path.GetTempPath() + exe;

            // create the webclient
            using (var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(longWaitBroker, 2))
            {

                // First try downloading it as if it is the most recent release of R. The most
                // recent version is stored in a different location of the CRAN repo than older versions.
                // Otherwise, check and see if it is an older release

                var recentUri = new Uri(baseUri + exe);
                var olderUri = new Uri(baseUri + "old/" + _version + "/" + exe);

                if (!webClient.DownloadFileAsync(recentUri, DownloadPath) && !webClient.DownloadFileAsync(olderUri, DownloadPath))
                    throw new MessageException(
                        TextUtil.LineSeparate(
                            Resources.RInstaller_DownloadR_Download_failed,
                            Resources
                                .RInstaller_DownloadPackages_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_));
            }
        }

        private void InstallR()
        {
            var processRunner = TestProcessRunner ?? new SynchronousProcessRunner();
            // an exit code of 0 indicates a successful installation
            if (processRunner.RunProcess(new Process {StartInfo = new ProcessStartInfo {FileName = DownloadPath}}) != 0)
                throw new MessageException(
                    Resources.RInstaller_InstallR_R_installation_was_not_completed__Cancelling_tool_installation_);
        }

        private bool GetPackages()
        {
            try
            {
                string packagePaths = null;
                using (var dlg = new LongWaitDlg {Message = Resources.RInstaller_GetPackages_Downloading_packages, ProgressValue = 0})
                {
                    dlg.PerformWork(this, 1000,
                                    longWaitBroker =>
                                    packagePaths = DownloadPackages(longWaitBroker));
                }
                InstallPackages(packagePaths);
                MessageDlg.Show(this, Resources.RInstaller_GetPackages_Package_installation_complete);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is MessageException)
                {
                    MessageDlg.Show(this, ex.InnerException.Message);
                    return false;
                }
            }
            catch (MessageException ex)
            {
                MessageDlg.Show(this, ex.Message);
                return false;
            }
            return true;
        }

        /// <returns>A space separated string of package source file locations on the local file system</returns>
        private string DownloadPackages(ILongWaitBroker longWaitBroker)
        {
            // only download the checked packages
            ICollection<Uri> packagesToDownload = new Collection<Uri>();
            int index = 0;
            foreach (var package in _packageUris)
            {
                if (checkedListBoxPackages.GetItemCheckState(index) == CheckState.Checked)
                    packagesToDownload.Add(new Uri(package));
                index++;
            }

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
                    if (webClient.DownloadFileAsync(package, downloadPath))
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

                return CommandLine.ParseCommandLineArray(packagePaths.ToArray());
            }
        }

        private void InstallPackages(string packagePaths)
        {
            // ensure that there are Packages to install
            if (string.IsNullOrEmpty(packagePaths))
                return;

            // then get the program path
            string programPath = TestProgramPath ?? RUtil.FindRProgramPath(_version);
            if (programPath == null)
                throw new MessageException(Resources.RInstaller_InstallPackages_Unknown_error_installing_packages);

            // create argument string
            var argumentBuilder = new StringBuilder();
            argumentBuilder.Append("/C \"").Append(programPath).Append("\" CMD INSTALL ").Append(packagePaths);

            var processRunner = TestNamedPipeProcessRunner ?? new NamedPipeProcessRunnerWrapper();
            try
            {
                if (processRunner.RunProcess(argumentBuilder.ToString(), true, _writer) != 0)
                {
                    throw new MessageException(Resources.RInstaller_InstallPackages_Package_installation_failed__Error_log_output_in_immediate_window_);
                }
            }
            catch (IOException)
            {
                throw new MessageException(Resources.RInstaller_InstallPackages_Unknown_error_installing_packages);
            }
        }

        #region Functional testing support

        // test classes for functional testing
        public IProcessRunner TestProcessRunner { get; set; }
        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        public INamedPipeProcessRunnerWrapper TestNamedPipeProcessRunner { get; set; }
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

    }

    public static class RUtil {

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
}

