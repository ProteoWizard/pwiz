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
using System.Collections.Generic;
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
        private static readonly ILog LOG = LogManager.GetLogger("AutoQC");
        private static string _version;

        public const bool IsDaily = false;
        public const string AutoQcStarter = IsDaily ? "AutoQCDailyStarter" : "AutoQCStarter";
        public static readonly string AutoQcStarterExe = $"{AutoQcStarter}.exe";

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            // Handle exceptions on the UI thread.
            Application.ThreadException += ((sender, e) => LOG.Error(e.Exception));
            // Handle exceptions on the non-UI thread.
            AppDomain.CurrentDomain.UnhandledException += ((sender, e) =>
            {
                try
                {
                    LOG.Error("AutoQC Loader encountered an unexpected error. ", (Exception)e.ExceptionObject);
                    MessageBox.Show("AutoQC Loader encountered an unexpected error. " +
                                    "Error details may be found in the AutoQCProgram.log file in this directory : "
                                    + Path.GetDirectoryName(Application.ExecutablePath)
                    );
                }
                finally
                {
                    Application.Exit();
                }
            });

            using (var mutex = new Mutex(false, $"University of Washington {AppName()}"))
            {
                if (!mutex.WaitOne(TimeSpan.Zero))
                {
                    MessageBox.Show($"Another instance of {AppName()} is already running.", $"{AppName()} Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                InitializeSecurityProtocol();

                // Initialize log4net -- global application logging
                XmlConfigurator.Configure();
                if (!InitSkylineSettings())
                {
                    mutex.ReleaseMutex();
                    return;
                }

                var form = new MainForm();

                // CurrentDeployment is null if it isn't network deployed.
                _version = ApplicationDeployment.IsNetworkDeployed
                    ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                    : "";
                form.Text = Version();

                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                Console.WriteLine("Local user config path: {0}", config.FilePath);

                var worker = new BackgroundWorker {WorkerSupportsCancellation = false, WorkerReportsProgress = false};
                worker.DoWork += UpdateAutoQcStarter;
                worker.RunWorkerCompleted += (o, eventArgs) =>
                {
                    if (eventArgs.Error != null)
                    {
                        LogError($"Unable to update {AutoQcStarter} shortcut.", eventArgs.Error);
                        form.DisplayError($"{AutoQcStarter} Update Error", $"Unable to update {AutoQcStarter} shortcut.  Error was: {eventArgs.Error.ToString()}");
                    }
                };

                worker.RunWorkerAsync();

                Application.Run(form);

                mutex.ReleaseMutex();
            }
        }

        private static bool InitSkylineSettings()
        {
            if (!string.IsNullOrWhiteSpace(Settings.Default.SkylineType))
                return true;
            var listOfDirectories = "";
            // C:\Program Files\Skyline  
            var adminSkylineDir = Path.Combine("C:\\Program Files", "Skyline");
            if (Directory.Exists(adminSkylineDir))
            {
                var cmdFile = Path.Combine(adminSkylineDir, MainForm.SKYLINE_CMD);
                const string skylineExe = MainForm.SKYLINE + ".exe";
                const string skylineDailyExe = MainForm.SKYLINE_DAILY + ".exe";
                if (File.Exists(cmdFile)
                    && (File.Exists(Path.Combine(adminSkylineDir, skylineExe))))
                {
                    Settings.Default.SkylineCmdLineExePath = cmdFile;
                    Settings.Default.SkylineType = MainForm.SKYLINE;
                    return true;
                }
                else if (File.Exists(cmdFile) && File.Exists(Path.Combine(adminSkylineDir, skylineDailyExe)))
                {
                    Settings.Default.SkylineCmdLineExePath = cmdFile;
                    Settings.Default.SkylineType = MainForm.SKYLINE_DAILY;
                    return true;
                }
            }
            listOfDirectories += "\n" + adminSkylineDir;

            // C:\Program Files\Skyline-daily
            if (Directory.Exists(adminSkylineDir = Path.Combine("C:\\Program Files", "Skyline-daily")))
            {
                const string skylineDailyExe = MainForm.SKYLINE_DAILY + ".exe";
                var cmdFile = Path.Combine(adminSkylineDir, MainForm.SKYLINE_CMD);
                if (File.Exists(cmdFile)
                    && File.Exists(Path.Combine(adminSkylineDir, skylineDailyExe)))
                {
                    Settings.Default.SkylineType = MainForm.SKYLINE_DAILY;
                    Settings.Default.SkylineCmdLineExePath = cmdFile;
                    return true;
                }
            }
            listOfDirectories += "\n" + adminSkylineDir;

            // ClickOnce Skyline installation   
            if (MainForm.ListPossibleSkylineShortcutPaths(MainForm.SKYLINE).FirstOrDefault(File.Exists) != null)
            {
                Settings.Default.SkylineType = MainForm.SKYLINE;
                Settings.Default.SkylineCmdLineExePath = Path.Combine(MainForm.AutoQCInstallDir, MainForm.SKYLINE_RUNNER);
                return true;
            }
            listOfDirectories += "\n" + MainForm.AutoQCInstallDir;

            // ClickOnce Skyline-daily installation  
            if (MainForm.ListPossibleSkylineShortcutPaths(MainForm.SKYLINE_DAILY).FirstOrDefault(File.Exists) != null)
            {
                Settings.Default.SkylineType = MainForm.SKYLINE_DAILY;
                Settings.Default.SkylineCmdLineExePath = Path.Combine(MainForm.AutoQCInstallDir, MainForm.SKYLINE_DAILY_RUNNER);
                return true;
            }
            listOfDirectories += "\n" + MainForm.AutoQCInstallDir;

            MessageBox.Show("AutoQC Loader requires Skyline or Skyline - daily to be installed on the computer. " +
                            $"Unable to find Skyline at any of the following locations: {listOfDirectories}. " +
                            "Please install Skyline or Skyline - daily to use AutoQC Loader", "Unable To Find Skyline",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;

        }

        private static void UpdateAutoQcStarter(object sender, DoWorkEventArgs e)
        {
            if (!Properties.Settings.Default.KeepAutoQcRunning) return;

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
                    LogInfo($"{AutoQcStarter} is not running. It should be running since Keep AutoQC Loader running is checked. Starting it up...");
                    StartupManager.UpdateAutoQcStarterInStartup();
                }
            }
        }

        private static bool IsFirstRun()
        {
            if (!ApplicationDeployment.IsNetworkDeployed)
                return false;

            var currentVersion = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            var installedVersion = Properties.Settings.Default.InstalledVersion ?? string.Empty;
            if (!currentVersion.Equals(installedVersion))
            {
                LogInfo(string.Empty.Equals(installedVersion)
                    ? $"This is a first install and run of version: {currentVersion}."
                    : $"Current version: {currentVersion} is newer than the last installed version: {installedVersion}.");

                Properties.Settings.Default.InstalledVersion = currentVersion;
                Properties.Settings.Default.Save();
                return true;
            }
            LogInfo($"Current version: {currentVersion} same as last installed version: {installedVersion}.");
            return false;
        }

        public static void LogError(string message)
        {
            LOG.Error(message);
        }

        public static void LogError(string configName, string message)
        {
            LOG.Error(string.Format("{0}: {1}", configName, message));
        }

        public static void LogError(string message, Exception e)
        {
            LOG.Error(message, e);
        }

        public static void LogError(string configName, string message, Exception e)
        {
            LogError(string.Format("{0}: {1}", configName, message), e);
        }

        public static void LogInfo(string message)
        {
            LOG.Info(message);
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
            return $"{AppName()} {_version}";
        }

        private static string AppName()
        {
            return IsDaily ? "AutoQC Loader-daily" : "AutoQC Loader";
        }

        private static void InitializeSecurityProtocol()
        {
            // Make sure we can negotiate with HTTPS servers that demand TLS 1.2 (default in dotNet 4.6, but has to be turned on in 4.5)
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);  
        }
    }
}
