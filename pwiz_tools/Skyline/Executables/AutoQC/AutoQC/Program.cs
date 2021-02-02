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
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using AutoQC.Properties;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository.Hierarchy;

namespace AutoQC
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger("AutoQC");
        private static string _version;

        public const string AUTO_QC_STARTER = "AutoQCStarter";
        public static readonly string AutoQcStarterExe = $"{AUTO_QC_STARTER}.exe";

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            // Handle exceptions on the UI thread.
            Application.ThreadException += ((sender, e) => Log.Error(e.Exception));
            // Handle exceptions on the non-UI thread.
            AppDomain.CurrentDomain.UnhandledException += ((sender, e) =>
            {
                try
                {
                    Log.Error("AutoQC Loader encountered an unexpected error. ", (Exception)e.ExceptionObject);

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
                        string.Format(Resources.Program_Main__0__Error, AppName), MessageBoxButtons.OK,
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
                    LogInfo(string.Format("user.config path: {0}", config.FilePath));
                }
                catch (Exception)
                {
                    // ignored
                }

                // Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja");
                var form = new MainForm();

                // CurrentDeployment is null if it isn't network deployed.
                _version = ApplicationDeployment.IsNetworkDeployed
                    ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                    : "";
                form.Text = Version();

                var worker = new BackgroundWorker {WorkerSupportsCancellation = false, WorkerReportsProgress = false};
                worker.DoWork += UpdateAutoQcStarter;
                worker.RunWorkerCompleted += (o, eventArgs) =>
                {
                    if (eventArgs.Error != null)
                    {
                        LogError($"Unable to update {AUTO_QC_STARTER} shortcut.", eventArgs.Error);
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
            if (Installations.FindSkyline())
                return true;

            var skylineForm = new FindSkylineForm();
            Application.Run(skylineForm);

            if (skylineForm.DialogResult != DialogResult.OK)
            {
                MessageBox.Show($@"{AppName} requires Skyline to run.", $@"{AppName} Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            
            return true;
        }

        private static void UpdateAutoQcStarter(object sender, DoWorkEventArgs e)
        {
            if (!Settings.Default.KeepAutoQcRunning) return;

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                if (IsFirstRun())
                {
                    // First time running a newer version of the application
                    LogInfo($"Updating {AutoQcStarterExe} shortcut.");
                    StartupManager.UpdateAutoQcStarterInStartup();
                }
                else if (!StartupManager.IsAutoQcStarterRunning())
                {
                    // AutoQCStarter should be running but it is not
                    LogInfo($"{AUTO_QC_STARTER} is not running. It should be running since Keep AutoQC Loader running is checked. Starting it up...");
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
                LogInfo(string.Empty.Equals(installedVersion)
                    ? $"This is a first install and run of version: {currentVersion}."
                    : $"Current version: {currentVersion} is newer than the last installed version: {installedVersion}.");

                Settings.Default.InstalledVersion = currentVersion;
                Settings.Default.Save();
                return true;
            }
            LogInfo($"Current version: {currentVersion} same as last installed version: {installedVersion}.");
            return false;
        }

        public static void LogError(string message)
        {
            Log.Error(message);
        }

        public static void LogError(string configName, string message)
        {
            Log.Error(string.Format("{0}: {1}", configName, message));
        }

        public static void LogError(string message, Exception e)
        {
            Log.Error(message, e);
        }

        public static void LogError(string configName, string message, Exception e)
        {
            LogError(string.Format("{0}: {1}", configName, message), e);
        }

        public static void LogInfo(string message)
        {
            Log.Info(message);
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

        private static void InitializeSecurityProtocol()
        {
            // Make sure we can negotiate with HTTPS servers that demand TLS 1.2 (default in dotNet 4.6, but has to be turned on in 4.5)
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);  
        }
    }
}
