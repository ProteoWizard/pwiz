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
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    // TODO: (trevor) allow for ranges of installations
    // TODO: (trevor) investigate the possibility of checking for currently installed packages?
    public partial class RInstaller : FormEx
    {

        private string Version { get; set; }
        private bool Installed { get; set; }
        private ICollection<string> Packages { get; set; }

        public RInstaller(ProgramPathContainer rPathContainer, ICollection<string> packageUris)
        {
            Version = rPathContainer.ProgramVersion;
            Packages = packageUris;
            Installed = CheckInstalled(Version);
            InitializeComponent();
        }

        private void RInstaller_Load(object sender, EventArgs e)
        {
            if (!Installed && Packages.Count != 0)
            {
                PopulatePackageCheckListBox();
                labelMessage.Text = string.Format(
                    Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0__and_the_following_packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_, Version);
            }
            else if (!Installed)
            {
                labelMessage.Text = string.Format(
                    Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0___Click_Install_to_begin_the_installation_process_, Version);
                int shift = checkedListBoxPackages.Height;
                checkedListBoxPackages.Visible = checkedListBoxPackages.Enabled = false;
                Height -= shift;
            }
            else if (Packages.Count != 0)
            {
                PopulatePackageCheckListBox();
                labelMessage.Text = Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_the_following_R_Packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process;
            }
        }

        private void PopulatePackageCheckListBox()
        {
            // add package names
            ICollection<string> packageNames = new Collection<string>();
            const string pattern = @"([^/]*)\.(zip|tar\.gz)$";
            foreach (var package in Packages)
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
            if (Installed && checkedListBoxPackages.SelectedIndices.Count != 0)
                GetPackages();
            
            // if R is successfully installed, return OK for a successful installation, regardless
            // of how the package installation turned out
            DialogResult = Installed ? DialogResult.OK : DialogResult.Cancel;
        }

        // returns true if R is installed successfully
        private bool InstallR()
        {
            // First, download the executable installer
            try
            {
                using (var dlg = new LongWaitDlg {Text = "Downloading R"})
                {
                    dlg.PerformWork(this, 1000, DownloadR);
                }
            }
            catch (Exception ex)
            {
                MessageDlg.Show(this, ex.Message);
                return false;
            }
            MessageDlg.Show(this, "Download succeeded.");

            // Then run the installer
            bool successfulInstall = RunInstaller();
            if (!successfulInstall)
            {
                MessageDlg.Show(this, "Installation was not completed. Cancelling tool installation."); // TODO: something other than this?
                return false;
            }
            return true;
        }

        private string DownloadPath { get; set; }

        private void DownloadR(ILongWaitBroker longWaitBroker)
        {
            // the repository containing the downloadable R exes
            const string baseUri = "http://cran.r-project.org/bin/windows/base/";

            // format the file name, e.g. R-2.15.2-win.exe
            string exe = "R-" + Version + "-win.exe";

            // create the download path for the file
            DownloadPath = Path.GetTempPath() + exe;

            // create the webclient
            AsynchronousDownloadClient webClient = new AsynchronousDownloadClient(longWaitBroker);   

            // first try downloading it as if it is the most recent release of R. The most
            // recent version is stored in a different location of the CRAN repo than
            // older versions
            Uri versionUri = new Uri(baseUri + exe);
            bool successfulDownload = webClient.DownloadFileAsyncWithBroker(versionUri, DownloadPath);
            if (successfulDownload)
                return;

            // otherwise, check and see if it is an older release
            versionUri = new Uri(baseUri + "old/" + Version + "/" + exe);
            successfulDownload = webClient.DownloadFileAsyncWithBroker(versionUri, DownloadPath);
            
            if (!successfulDownload)
                throw new Exception("Download Failed");
        }

        // The asynchronous download client links a webclient to a longwaitbroker; it supports
        // multiple downloads during the same instance of a longwaitdlg's perform work
        private class AsynchronousDownloadClient: WebClient
        {

            public AsynchronousDownloadClient(ILongWaitBroker longWaitBroker)
            {
                _longWaitBroker = longWaitBroker;
                DownloadProgressChanged += (sender, args) =>
                {
                    longWaitBroker.ProgressValue = args.ProgressPercentage;
                };
                DownloadFileCompleted += (sender, args) =>
                {
                    SuccessfulDownload = (args.Error == null);
                    DownloadComplete = true;
                };
            }

            private readonly ILongWaitBroker _longWaitBroker;
            private bool DownloadComplete { get; set; }
            private bool SuccessfulDownload { get; set; }
            
            // returns true if the file was successfully downloaded
            public bool DownloadFileAsyncWithBroker(Uri address, string fileName)
            {
                DownloadComplete = false;
                SuccessfulDownload = false;

                Match file = Regex.Match(address.AbsolutePath, @"[^/]*$");
                _longWaitBroker.Message = "Downloading: " + file;
                DownloadFileAsync(address, fileName);
                while (!DownloadComplete)
                {
                    if (_longWaitBroker.IsCanceled)
                    {
                        CancelAsync();
                        throw new Exception("Download Canceled");
                    }
                }
                return SuccessfulDownload;
            }
        }

        // returns true if installation was successful
        private bool RunInstaller()
        {
            Process rInstaller = new Process { StartInfo = { FileName = DownloadPath } };
            rInstaller.Start();
            rInstaller.WaitForExit();
            int statusCode = rInstaller.ExitCode;
            rInstaller.Close();
            // a status code of 0 indicates a successful installation
            return statusCode == 0;
        }

        private void GetPackages()
        {
            // only download the checked packages
            ICollection<Uri> packagesToDownload = new Collection<Uri>();
            int index = 0;
            foreach (var package in Packages)
            {
                if (checkedListBoxPackages.GetItemCheckState(index) == CheckState.Checked)
                    packagesToDownload.Add(new Uri(package));
                index++;
            }

            if (packagesToDownload.Count != 0)
            {
                string packagePaths = null;
                using (var dlg = new LongWaitDlg {Text = "Downloading Packages" })
                {
                    dlg.PerformWork(this, 1000, longWaitBroker => packagePaths = DownloadPackages(packagesToDownload, longWaitBroker));
                }
                // then install them
                InstallPackages(packagePaths);
            }
        }

        private static string DownloadPackages(IEnumerable<Uri> packagesToDownload, ILongWaitBroker longWaitBroker)
        {
            // create the webclient
            AsynchronousDownloadClient webClient = new AsynchronousDownloadClient(longWaitBroker);
            
            // store filepaths
            StringBuilder packagePaths = new StringBuilder();

            // collect failed package installs
            ICollection<Uri> failedDownloads = new Collection<Uri>();
            
            // download each package
            foreach (var package in packagesToDownload)
            {
                Match file = Regex.Match(package.AbsolutePath, @"([^/]*\.)(zip|tar\.gz)");
                string downloadPath = Path.GetTempPath() + file.Groups[1] + file.Groups[2];
                try
                {
                    if(webClient.DownloadFileAsyncWithBroker(package, downloadPath))
                        packagePaths.Append(" " + downloadPath);
                }
                catch
                {
                    failedDownloads.Add(package); // TODO: do we want to do something with this? 
                }
            }
           return packagePaths.ToString();
        }

        private void InstallPackages(string packagePaths)
        {
            // ensure that there are Packages to install
            if (string.IsNullOrEmpty(packagePaths))
                return;

            // then get the program path
            string programPath = FindRProgramPath(Version);
            if (programPath == null)
            {
                MessageDlg.Show(this, "Unknown error installing Packages");
                return;
            }

            // run installer
            ProcessStartInfo startInfo = new ProcessStartInfo("CMD.exe")
            {
                Verb = "runas",
                Arguments = "/K \"" + programPath + "\" CMD INSTALL " + packagePaths
            };

            // TODO: (trevor) if tool installation fails, should we allow the tools to be installed?

            try
            {
                Process packageInstall = new Process {StartInfo = startInfo};
                packageInstall.Start();
                packageInstall.WaitForExit();
                MessageDlg.Show(this, "Tool installation completed");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error installing Packages: " + ex.Message);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private const string REGISTRY_LOCATION = @"SOFTWARE\R-core\R\";

        // Checks the registry to see if the specified version of R is installed on
        // the local machine, e.g. "2.15.2" or "3.0.0"
        /// <summary>
        /// Checks the registry to see if the specified version of R is installed on the local
        /// machine, e.g. "2.15.2" or "3.00"
        /// </summary>
        /// <param name="rVersion">The version to check</param>
        public static bool CheckInstalled(string rVersion)
        {
            RegistryKey softwareKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE");
            if (softwareKey == null)
                return false;

            if (softwareKey.GetSubKeyNames().Contains("R-core"))
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

            string installPath = rKey.GetValue("InstallPath") as string;
            if (installPath == null)
                return null;

            return installPath + "\\bin\\R.exe";
        }

    }

}
