/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Configuration;
using System.Deployment.Application;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using AutoQC.Properties;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository.Hierarchy;
using SharedBatch;

namespace AutoQC
{
    class Program
    {
        private static string _version;

        public const string AUTO_QC_STARTER = "AutoQCStarter";
        public static readonly string AutoQcStarterExe = $"{AUTO_QC_STARTER}.exe";

        [STAThread]
        public static void Main(string[] args)
        {
            ProgramLog.Init("AutoQC");
            Application.EnableVisualStyles();

            AddFileTypesToRegistry();

            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            // Handle exceptions on the UI thread.
            Application.ThreadException += ((sender, e) => ProgramLog.Error(e.Exception.Message, e.Exception));
            // Handle exceptions on the non-UI thread.
            AppDomain.CurrentDomain.UnhandledException += ((sender, e) =>
            {
                try
                {
                    ProgramLog.Error("AutoQC Loader encountered an unexpected error. ", (Exception)e.ExceptionObject);

                    const string logFile = "AutoQCProgram.log";
                    MessageBox.Show(
                        string.Format(
                            Resources
                                .Program_Main_AutoQC_Loader_encountered_an_unexpected_error__Error_details_may_be_found_in_the__0__file_in_this_directory___,
                            logFile)
                        + Path.GetDirectoryName(Application.ExecutablePath)
                    );
                }
                finally
                {
                    Application.Exit(); 
                }
            });

            using (var mutex = new Mutex(false, $"University of Washington {AppName}"))
            {
                if (!mutex.WaitOne(TimeSpan.Zero))
                {
                    MessageBox.Show(
                        string.Format(Resources.Program_Main_Another_instance_of__0__is_already_running_, AppName),
                        AppName, MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                InitializeSecurityProtocol();

                // Initialize log4net -- global application logging
                XmlConfigurator.Configure();

                if (!InitSkylineSettings()) return;

                try
                {
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    ProgramLog.Info(string.Format("user.config path: {0}", config.FilePath));
                }
                catch (Exception)
                {
                    // ignored
                }

                // Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja");
                var openFile = GetFirstArg(args);
                var form = new MainForm(openFile);

                // CurrentDeployment is null if it isn't network deployed.
                _version = ApplicationDeployment.IsNetworkDeployed
                    ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                    : string.Empty;
                form.Text = Version();

                var worker = new BackgroundWorker {WorkerSupportsCancellation = false, WorkerReportsProgress = false};
                worker.DoWork += UpdateAutoQcStarter;
                worker.RunWorkerCompleted += (o, eventArgs) =>
                {
                    if (eventArgs.Error != null)
                    {
                        ProgramLog.Error($"Unable to update {AUTO_QC_STARTER} shortcut.", eventArgs.Error);
                        form.DisplayError(string.Format(Resources.Program_Main_Unable_to_update__0__shortcut___Error_was___1_,
                                AUTO_QC_STARTER, eventArgs.Error));
                    }
                };

                worker.RunWorkerAsync();

                Application.Run(form);

                mutex.ReleaseMutex();
            }
        }

        private static bool InitSkylineSettings()
        {
            if (SkylineInstallations.FindSkyline())
                return true;
            var skylineForm = new FindSkylineForm(AppName, Icon());
            Application.Run(skylineForm);

            if (skylineForm.DialogResult != DialogResult.OK)
            {
                MessageBox.Show(string.Format(Resources.Program_InitSkylineSettings__0__requires_Skyline_to_run_, AppName) + Environment.NewLine +
                    string.Format(Resources.Program_InitSkylineSettings_Please_install_Skyline_to_run__0__, AppName), AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            
            return true;
        }

        private static string GetFirstArg(string[] args)
        {
            string arg;
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                _version = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
                var activationData = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                arg = activationData != null && activationData.Length > 0
                    ? activationData[0]
                    : string.Empty;
            }
            else
            {
                _version = string.Empty;
                arg = args.Length > 0 ? args[0] : string.Empty;
            }

            return arg;
        }

        private static void AddFileTypesToRegistry()
        {
            var appReference = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Microsoft\\Windows\\Start Menu\\Programs\\MacCoss Lab, UW\\" + AppName.Substring(0, AppName.IndexOf(" ")) + TextUtil.EXT_APPREF;
            var appExe = Application.ExecutablePath;

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var configFileIconPath = Path.Combine(baseDirectory, "AutoQC_configs.ico");

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                FileUtil.AddFileTypeClickOnce(TextUtil.EXT_QCFG, "AutoQC.Configuration.0",
                    Resources.Program_AddFileTypesToRegistry_AutoQC_Configuration_File,
                    appReference, configFileIconPath);
            }
            else
            {
                FileUtil.AddFileTypeAdminInstall(TextUtil.EXT_QCFG, "AutoQC.Configuration.0",
                    Resources.Program_AddFileTypesToRegistry_AutoQC_Configuration_File,
                    appExe, configFileIconPath);
            }
        }

        private static void UpdateAutoQcStarter(object sender, DoWorkEventArgs e)
        {
            if (!Settings.Default.KeepAutoQcRunning) return;

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                if (IsFirstRun())
                {
                    // First time running a newer version of the application
                    ProgramLog.Info($"Updating {AutoQcStarterExe} shortcut.");
                    StartupManager.UpdateAutoQcStarterInStartup();
                }
                else if (!StartupManager.IsAutoQcStarterRunning())
                {
                    // AutoQCStarter should be running but it is not
                    ProgramLog.Info($"{AUTO_QC_STARTER} is not running. It should be running since Keep AutoQC Loader running is checked. Starting it up...");
                    StartupManager.UpdateAutoQcStarterInStartup();
                }
            }
        }

        private static bool IsFirstRun()
        {
            if (!ApplicationDeployment.IsNetworkDeployed)
                return false;

            var currentVersion = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            var installedVersion = Settings.Default.InstalledVersion ?? string.Empty;
            if (!currentVersion.Equals(installedVersion))
            {
                ProgramLog.Info(string.Empty.Equals(installedVersion)
                    ? $"This is a first install and run of version: {currentVersion}."
                    : $"Current version: {currentVersion} is newer than the last installed version: {installedVersion}.");

                Settings.Default.InstalledVersion = currentVersion;
                Settings.Default.Save();
                return true;
            }
            ProgramLog.Info($"Current version: {currentVersion} same as last installed version: {installedVersion}.");
            return false;
        }
        
        public static string GetProgramLogFilePath()
        {
            var repository = ((Hierarchy) LogManager.GetRepository());
            FileAppender rootAppender = null;
            if (repository != null)
            {
                rootAppender = repository.Root.Appenders.OfType<FileAppender>()
                    .FirstOrDefault();
            }

            return rootAppender != null ? rootAppender.File : string.Empty;
        }

        public static string Version()
        {
            return $"{AppName} {_version}";
        }

        public static string AppName => "AutoQC Loader";

        public static Icon Icon()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var iconPath = Path.Combine(baseDirectory, "AutoQC_release.ico");
            return  System.Drawing.Icon.ExtractAssociatedIcon(iconPath);
        }

        private static void InitializeSecurityProtocol()
        {
            // Make sure we can negotiate with HTTPS servers that demand TLS 1.2 (default in dotNet 4.6, but has to be turned on in 4.5)
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);  
        }
    }
}
