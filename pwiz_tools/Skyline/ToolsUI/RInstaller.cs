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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
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
    public partial class RInstaller : FormEx
    {
        private readonly string _version;
        private readonly bool _installed;
        private readonly TextWriter _writer;
        private string PathToInstallScript { get; set; }
        private ICollection<ToolPackage> PackagesToInstall { get; set; }

        public RInstaller(ProgramPathContainer rPathContainer, ICollection<ToolPackage> packages, TextWriter writer, string pathToInstallScript)
            : this(rPathContainer, packages, RUtil.CheckInstalled(rPathContainer.ProgramVersion), writer, pathToInstallScript)
        {
        }

        public RInstaller(ProgramPathContainer rPathContainer, ICollection<ToolPackage> packages, bool installed, TextWriter writer, string pathToInstallScript)
        {
            PackagesToInstall = packages;
            _version = rPathContainer.ProgramVersion;
            _installed = installed;
            _writer = writer;
            PathToInstallScript = pathToInstallScript;
            InitializeComponent();
        }

        public bool IsLoaded { get; private set; }

        private void RInstaller_Load(object sender, EventArgs e)
        {
            if (!_installed && (PackagesToInstall.Count() != 0))
            {
                PopulatePackageListBox();
                labelMessage.Text = string.Format(Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0__and_the_following_packages_,
                                                  _version);
            }
            else if (!_installed)
            {
                labelMessage.Text = string.Format(
                    Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0___Click_Install_to_begin_the_installation_process_,
                    _version);
                int shift = btnCancel.Top - listBoxPackages.Top;
                listBoxPackages.Visible = listBoxPackages.Enabled = false;
                Height -= shift;
            }
            else if (PackagesToInstall.Count() != 0)
            {
                PopulatePackageListBox();
                labelMessage.Text = Resources.RInstaller_RInstaller_Load_This_Tool_requires_the_use_of_the_following_R_Packages_;
            }
            IsLoaded = true;
        }

        private IList<string> MakePackageList()
        {
            var packageList = new List<string>();
            foreach (var packageContainer in PackagesToInstall)
            {
                if (packageContainer.Version == null)
                {
                    packageList.Add(packageContainer.Name);
                }
                else
                {
                    packageList.Add(string.Concat(packageContainer.Name, " (", packageContainer.Version, ")")); // Not L10N
                }
            }
            return packageList;
        } 

        private void PopulatePackageListBox()
        {
            listBoxPackages.DataSource = MakePackageList();
        }

        private void btnInstall_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            Enabled = false;

            if ((_installed || GetR()) && (PackagesToInstall.Count == 0 || GetPackages()))
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
                using (var dlg = new LongWaitDlg {Message = Resources.RInstaller_InstallR_Downloading_R, ProgressValue = 0})
                {
                    // Short wait, because this can't possible happen fast enough to avoid
                    // showing progress, except in testing
                    dlg.PerformWork(this, 50, DownloadR);
                }
                using (var dlg = new LongWaitDlg(null, false) {Message = Resources.RInstaller_GetR_Installing_R})
                {
                    dlg.PerformWork(this, 50, InstallR);
                }
                MessageDlg.Show(this, Resources.RInstaller_GetR_R_installation_complete_);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException.GetType() == typeof (ToolExecutionException))
                {
                    MessageDlg.ShowException(this, ex);
                    return false;
                }
                throw;
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
                var olderUri = new Uri(baseUri + "old/" + _version + "/" + exe); // Not L10N

                if (!webClient.DownloadFileAsync(recentUri, DownloadPath) && !webClient.DownloadFileAsync(olderUri, DownloadPath))
                    throw new ToolExecutionException(
                        TextUtil.LineSeparate(
                            Resources.RInstaller_DownloadR_Download_failed_,
                            Resources
                                .RInstaller_DownloadPackages_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_));
            }
        }

        private void InstallR()
        {
            var processRunner = TestRunProcess ?? new SynchronousRunProcess();
            // an exit code of 0 indicates a successful installation
            if (processRunner.RunProcess(new Process {StartInfo = new ProcessStartInfo {FileName = DownloadPath}}) != 0)
                throw new ToolExecutionException(
                    Resources.RInstaller_InstallR_R_installation_was_not_completed__Cancelling_tool_installation_);
        }

        private bool GetPackages()
        {
            try
            {
                using (var dlg = new LongWaitDlg(null, false) {Message = Resources.RInstaller_GetPAckages_Installing_Packages})
                {
                    dlg.PerformWork(this, 1000, InstallPackages);
                }
            }
            catch(TargetInvocationException ex)
            {
                //Win32Exception is thrown when the user does not ok Administrative Privileges
                if (ex.InnerException is ToolExecutionException || ex.InnerException is System.ComponentModel.Win32Exception) 
                {
                    MessageDlg.ShowException(this, ex);
                    return false;
                }
                else
                    throw;
            }
            return true;
        }

        // Exit code when the user exits the package install command script before it finishes.
        public const int EXIT_EARLY_CODE = -1073741510;

        private void InstallPackages()
        {
            if (PackagesToInstall.Count == 0)
                return;

            if (!PackageInstallHelpers.CheckForInternetConnection())
            {
                throw new ToolExecutionException(
                    TextUtil.LineSeparate(Resources.RInstaller_InstallPackages_Error__No_internet_connection_,string.Empty, Resources.RInstaller_InstallPackages_Installing_R_packages_requires_an_internet_connection__Please_check_your_connection_and_try_again));
            }

            string programPath = PackageInstallHelpers.FindRProgramPath(_version);
            var argumentBuilder = new StringBuilder();
            argumentBuilder.Append("\"" + programPath + "\"").Append(" -f \"").Append(PathToInstallScript).Append("\" --slave"); // Not L10N
            try
            {
                ISkylineProcessRunnerWrapper processRunner = TestSkylineProcessRunnerWrapper ?? new SkylineProcessRunnerWrapper();
                int exitCode = processRunner.RunProcess(argumentBuilder.ToString(), true, _writer);
                if (exitCode == EXIT_EARLY_CODE) // When the user exits the command script before it finishes.
                {
                    throw new ToolExecutionException(string.Format(Resources.RInstaller_InstallPackages_Error__Package_installation_did_not_complete__Output_logged_to_the_Immediate_Window_));
                }
                if (exitCode != 0)
                {
                    throw new ToolExecutionException(Resources.RInstaller_InstallPackages_Unknown_Error_installing_packages__Output_logged_to_the_Immediate_Window_);
                }

                //Check for packages again. 
                var failedPackages = PackageInstallHelpers.WhichPackagesToInstall(PackagesToInstall, programPath);
                if (failedPackages.Count != 0)
                {
                    if (failedPackages.Count == 1)
                    {
                        throw new ToolExecutionException(
                            string.Format(TextUtil.LineSeparate(Resources.RInstaller_InstallPackages_The_package__0__failed_to_install_,
                                                                string.Empty,
                                                                Resources.RInstaller_InstallPackages_Output_logged_to_the_Immediate_Window_), failedPackages.First().Name));
                    }
                    IEnumerable<string> failedPackagesTitles = failedPackages.Select(p => p.Name);
                    throw new ToolExecutionException(
                        TextUtil.LineSeparate(Resources.RInstaller_InstallPackages_The_following_packages_failed_to_install_,
                                                            string.Empty,                                    
                                                            TextUtil.LineSeparate(failedPackagesTitles),
                                                            string.Empty,
                                                            Resources.RInstaller_InstallPackages_Output_logged_to_the_Immediate_Window_));
                }
            }
            catch (IOException ex)
            {
                throw new ToolExecutionException(TextUtil.LineSeparate(Resources.RInstaller_InstallPackages_Unknown_error_installing_packages__Tool_Installation_Failed_,
                                                                  string.Empty,
                                                                  ex.Message));
            }
        }
        #region Functional testing support

        public interface IPackageInstallHelpers
        {
            ICollection<ToolPackage> WhichPackagesToInstall(ICollection<ToolPackage> packages, string pathToR);
            string FindRProgramPath(string rVersion);
            bool CheckForInternetConnection();
        } 

        public IPackageInstallHelpers PackageInstallHelpers
        {
            get { return _packageInstallHelpers ?? (_packageInstallHelpers = new PackageInstallHelpers()); }
            set { _packageInstallHelpers = value; }
        }
        private IPackageInstallHelpers _packageInstallHelpers { get; set; }

        public IRunProcess TestRunProcess { get; set; }
        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        public ISkylineProcessRunnerWrapper TestSkylineProcessRunnerWrapper { get; set; }   

        public string Message
        {
            get { return labelMessage.Text; }
        }

        public int PackagesListCount
        {
            get { return listBoxPackages.Items.Count; }
        }
        #endregion
    }

    internal class PackageInstallHelpers : RInstaller.IPackageInstallHelpers
    {
        public ICollection<ToolPackage> WhichPackagesToInstall(ICollection<ToolPackage> packages, string pathToR)
        {
            return RUtil.WhichPackagesToInstall(packages, pathToR);
        }

        public string FindRProgramPath(string rVersion)
        {
            return RUtil.FindRProgramPath(rVersion);
        }

        public bool CheckForInternetConnection()
        {
            return RUtil.CheckForInternetConnection();
        }
    }

    public static class RUtil
    {
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
            return (Registry.LocalMachine.OpenSubKey(REGISTRY_LOCATION + rVersion) ?? Registry.CurrentUser.OpenSubKey(REGISTRY_LOCATION + rVersion)) != null;
        }

        /// <summary>
        /// Finds the program path for the specified version of R.
        /// </summary>
        /// <param name="rVersion">The version of R, e.g. "2.15.2"</param>
        public static string FindRProgramPath(string rVersion)
        {
            RegistryKey rKey = Registry.LocalMachine.OpenSubKey(REGISTRY_LOCATION + rVersion) ?? Registry.CurrentUser.OpenSubKey(REGISTRY_LOCATION + rVersion);
            if (rKey == null)
                return null;

            string installPath = rKey.GetValue("InstallPath") as string; // Not L10N
            return (installPath != null) ? installPath + "\\bin\\R.exe" : null; // Not L10N
        }

        /// <summary>
        ///  Writes a file with an R script that will check for each package.
        /// </summary>
        /// <param name="packages"> list of all packages to check.</param>
        /// <returns> Path to R script</returns>
        public static string WriteCheckForPackagesFile(IEnumerable<ToolPackage> packages)
        {
            var filePath = Path.GetTempFileName();
            using (StreamWriter file = new StreamWriter(filePath))
            {
                file.WriteLine("a<-installed.packages()"); // Not L10N
                file.WriteLine("packages<-a[,1]"); // Not L10N

                // Function that looks up all the version of a specified package.
                
                foreach (ToolPackage package in packages)
                {
                    string name = package.Name;
                    string version = package.Version;
                    file.WriteLine("# Check For Package {0}", name); // Not L10N
                    file.WriteLine("cat(\"{0} - \")", name); // Not L10N
                    if (string.IsNullOrEmpty(version))
                    {
                        // Just check if it exists if there is no version provided
                        file.WriteLine("cat (is.element(\"{0}\",packages))", name); // Not L10N
                    }
                    else
                    {
                        file.WriteLine("cat (is.element(\"{0}\",packages) && (packageVersion(\"{0}\") >= \"{1}\"))", name, version); // Not L10N
                    }
                    file.WriteLine("cat(\"\\n\")"); // Not L10N
                }
                file.Flush();
            }
            return filePath;
        }

        /// <summary>
        ///  Given a list of packages it determines which need to be installed and which are already installed.
        /// </summary>
        /// <param name="packages">Collection of package names to check for</param>
        /// <param name="pathToR">Path to R</param>
        /// <returns>Collection of packages that need to be installed</returns>
        public static ICollection<ToolPackage> WhichPackagesToInstall(ICollection<ToolPackage> packages, string pathToR)
        {
            List<ToolPackage> packagesToInstall = new List<ToolPackage>();
            string pathToScript = WriteCheckForPackagesFile(packages);
            string response = RunRscript(pathToR, pathToScript);
            string[] lines = response.Split('\n');
            foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l)))
            {
                string[] split = line.Split('-');
                if (split.Length > 1 && split[1].Contains("FALSE")) // Not L10N
                {
                    string packageName = split[0].Trim();
                    var toInstall = packages.First(packageContainer => Equals(packageContainer.Name, packageName));
                    packagesToInstall.Add(toInstall);
                }
            }
            FileEx.SafeDelete(pathToScript);

            return packagesToInstall;
        }

        /// <summary>
        /// Runs a given R script
        /// </summary>
        private static string RunRscript(string pathToR, string pathToScriptFile)
        {
            string args = String.Format("-f {0} --slave", pathToScriptFile); // Not L10N
            ProcessStartInfo startInfo = new ProcessStartInfo(pathToR, args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
            Process p = new Process { StartInfo = startInfo };
            p.Start();
            return p.StandardOutput.ReadToEnd();
        }

        /// <summary>
        /// Returns true if internet connection is avalible.
        /// </summary>
        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                using (client.OpenRead("http://www.google.com")) // Not L10N
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

