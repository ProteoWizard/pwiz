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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository.Hierarchy;
using pwiz.Common;
using SharedBatch;
using Resources = AutoQC.Properties.Resources;
using Settings = AutoQC.Properties.Settings;

namespace AutoQC
{
    public class Program
    {
        private static Version _version;
        private static string _lastInstalledVersion;

        public const string AUTO_QC_STARTER = "AutoQCStarter";
        public static readonly string AutoQcStarterExe = $"{AUTO_QC_STARTER}.exe";

        // For functional tests
        public static MainForm MainWindow { get; private set; } 
        public static List<Exception> TestExceptions { get; set; }
        public static bool FunctionalTest { get; set; } // Set to true by AbstractFunctionalTest
        public static readonly string TEST_VERSION = "1000.0.0.0";


        [STAThread]
        public static void Main(string[] args)
        {
            ProgramLog.Init("AutoQC");
            CommonApplicationSettings.ProgramName = "AutoQC Loader";
            CommonApplicationSettings.ProgramNameAndVersion = Version();
            Application.EnableVisualStyles();

            AddFileTypesToRegistry();

            if (!FunctionalTest)
            {
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                // Handle exceptions on the UI thread.
                Application.ThreadException += ((sender, e) => ProgramLog.Error(e.Exception.Message, e.Exception));
                // Handle exceptions on the non-UI thread.
                AppDomain.CurrentDomain.UnhandledException += ((sender, e) =>
                {
                    try
                    {
                        ProgramLog.Error("AutoQC Loader encountered an unexpected error. ",
                            (Exception)e.ExceptionObject);

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
            }

            var doRestart = false;
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

                ProgramLog.Info($"Starting {AppName}");

                string configFile = null;
                try
                {
                    var config =
                        ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    configFile = config.FilePath;
                    ProgramLog.Info(string.Format("user.config path: {0}", configFile));
                    if (!InitSkylineSettings()) return;
                    UpgradeSettingsIfRequired();
                }
                catch (Exception e)
                {
                    ProgramLog.Error(e.Message, e);

                    if (configFile == null && e is ConfigurationException ce)
                    {
                        configFile = ce.Filename; // Get the user.config file path from the exception
                    }
                    if (configFile == null)
                    {
                        MessageBox.Show(
                            $"There was an error loading the saved configurations and settings. The error was {e.Message}");
                        return;

                    }

                    var msg1 = TextUtil.LineSeparate(
                        $"There was an error loading the saved configurations and settings in {configFile}.",
                        $"The error was {e.Message}");
                    var msg2 = "Please provide this file to the developers to help debug the problem.";
                    // Try to move the offending file to where the program exe is located
                    var folderToCopy = Path.GetDirectoryName(ProgramLog.GetProgramLogFilePath()) ?? string.Empty;
                    var newFileName = Path.Combine(folderToCopy, "error-user.config");
                    ProgramLog.Error($"Moving '{configFile}' to '{newFileName}'.");
                    try
                    {
                        File.Copy(configFile, newFileName, true);
                        File.Delete(configFile);
                    }
                    catch (Exception exception)
                    {
                        var err = $"Attempting to move the configuration file failed with the error: {exception.Message}.";
                        ProgramLog.Error(err, exception);
                        MessageBox.Show(TextUtil.LineSeparate(msg1, Environment.NewLine, err, msg2));
                        return;
                    }
                    
                    var message = TextUtil.LineSeparate(msg1,
                        Environment.NewLine,
                        $"The settings file has been moved to {newFileName}.",
                        msg2,
                        Environment.NewLine,
                        $"{AppName} will now start with default settings.");

                    MessageBox.Show(message);
                    ProgramLog.Error("Starting with default settings.");
                    // Restart the application with a default user.settings after releasing the mutex.
                    // If we restart here we will get an error message about an already running instance of AutoQC Loader.
                    doRestart = true;  
                }

                if (!doRestart)
                {
                    var openFile = GetFirstArg(args);
                    if (!string.IsNullOrEmpty(openFile))
                    {
                        ProgramLog.Info($"Reading configurations from file {openFile}");
                    }

                    MainWindow = new MainForm(openFile) {Text = Version()};

                    var worker = new BackgroundWorker
                        {WorkerSupportsCancellation = false, WorkerReportsProgress = false};
                    worker.DoWork += UpdateAutoQcStarter;
                    worker.RunWorkerCompleted += (o, eventArgs) =>
                    {
                        if (eventArgs.Error != null)
                        {
                            ProgramLog.Error($"Unable to update {AUTO_QC_STARTER} shortcut.", eventArgs.Error);
                            MainWindow.DisplayError(string.Format(
                                Resources.Program_Main_Unable_to_update__0__shortcut___Error_was___1_,
                                AUTO_QC_STARTER, eventArgs.Error));
                        }
                    };

                    worker.RunWorkerAsync();

                    Application.Run(MainWindow);
                }

                mutex.ReleaseMutex();
            }

            if (doRestart)
            {
                // user.config somehow got corrupted. We will restart with a fresh user.config.
                Application.Restart();
            }
        }

        private static void UpgradeSettingsIfRequired()
        {
            if (Settings.Default.SettingsUpgradeRequired)
            {
                Settings.Default.Upgrade(); // This should copy all the settings from the previous version
                Settings.Default.SettingsUpgradeRequired = false;
                Settings.Default.Save();
                Settings.Default.Reload();
            }
            GetCurrentAndLastInstalledVersions();
            Settings.Default.UpdateIfNecessary(_version.ToString(), ConfigMigrationRequired());
        }

        private static void GetCurrentAndLastInstalledVersions()
        {
            if (ApplicationDeployment.IsNetworkDeployed) // clickOnce installation
            {
                _version = ApplicationDeployment.CurrentDeployment.CurrentVersion;
            }
            else // developer build
            {
                // copied from Skyline Install.cs GetVersion()
                try
                {
                    string productVersion = null;

                    Assembly entryAssembly = Assembly.GetEntryAssembly();
                    if (entryAssembly != null)
                    {
                        // custom attribute
                        object[] attrs = entryAssembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
                        // Play it safe with a null check no matter what ReSharper thinks
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (attrs != null && attrs.Length > 0)
                        {
                            productVersion = ((AssemblyInformationalVersionAttribute)attrs[0]).InformationalVersion;
                        }
                        else
                        {
                            // win32 version info
                            productVersion = FileVersionInfo.GetVersionInfo(entryAssembly.Location).ProductVersion?.Trim();
                        }
                    }

                    _version = productVersion != null ? new Version(productVersion) : null;
                }
                catch (Exception)
                {
                    _version = null;
                }
            }

            _lastInstalledVersion = Settings.Default.InstalledVersion ?? string.Empty;

            if (_version != null && !_version.ToString().Equals(_lastInstalledVersion))
            {
                ProgramLog.Info(string.Empty.Equals(_lastInstalledVersion)
                    ? $"This is a first install and run of version: {_version}."
                    : $"Current version: {_version} is newer than the last installed version: {_lastInstalledVersion}.");

                return;
            }

            var currentVerStr = _version != null ? _version.ToString() : string.Empty;
            if (FunctionalTest)
            {
                _version = new Version(TEST_VERSION);
                _lastInstalledVersion = TEST_VERSION;
            }

            ProgramLog.Info($"Current version: '{currentVerStr}' is the same as last installed version: '{_lastInstalledVersion}'.");
            
        }

        private static bool ConfigMigrationRequired()
        {
            if (_version != null)
            {
                if (string.IsNullOrEmpty(_lastInstalledVersion))
                {
                    ProgramLog.Info("Last installed version not found. Config migration is required.");
                    return true;
                }

                if (System.Version.TryParse(_lastInstalledVersion, out var lastVersion))
                {
                    // ProgramLog.Info($"Current Version: {_version.Major}-{_version.MajorRevision}-{_version.Minor}-{_version.MinorRevision}");
                    // ProgramLog.Info($"Previous Version: {lastVersion.Major}-{lastVersion.MajorRevision}-{lastVersion.Minor}-{lastVersion.MinorRevision}");
                    if (lastVersion.Major == 1 && lastVersion.Minor == 1 && lastVersion.MinorRevision <= 20237)
                    {
                        ProgramLog.Info($"Last installed version was {_lastInstalledVersion}. Config migration is required.");
                        return true;
                    }
                }
                else
                {
                    ProgramLog.Info($"Could not parse last installed version {_lastInstalledVersion}. Skipping configuration migration.");
                    return false;
                }
            }

            ProgramLog.Info("Configuration migration is not required.");
            return false;
        }

        private static bool InitSkylineSettings()
        {
            ProgramLog.Info("Initializing Skyline settings.");
            if (SkylineInstallations.FindSkyline())
            {
                if (SkylineInstallations.HasSkyline)
                {
                    ProgramLog.Info(string.Format("Found SkylineRunner at: {0}.", SharedBatch.Properties.Settings.Default.SkylineRunnerPath));
                }
                if (SkylineInstallations.HasSkylineDaily)
                {
                    ProgramLog.Info(string.Format("Found SkylineDailyRunner at: {0}.", SharedBatch.Properties.Settings.Default.SkylineDailyRunnerPath));
                }
                // Save the Skyline settings otherwise, in a new installation of AutoQC Loader, "Skyline" and "Skyline Daily" options
                // are disabled in the "Skyline" tab.
                SharedBatch.Properties.Settings.Default.Save();
                return true;
            }

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
                var activationData = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                arg = activationData != null && activationData.Length > 0
                    ? activationData[0]
                    : string.Empty;
            }
            else
            {
                arg = args.Length > 0 ? args[0] : string.Empty;
            }

            return arg;
        }

        private static void AddFileTypesToRegistry()
        {
            var allProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var appName = "AutoQC";
            var apprefName = appName + TextUtil.EXT_APPREF;
            var publisherName = "University of Washington";
            var paths = new[]
            {
                //e.g. %APPDATA%\Microsoft\Windows\Start Menu\Programs\University of Washington\AutoQC.appref-ms
                Path.Combine(Path.Combine(allProgramsPath, publisherName), apprefName),
                //e.g. %APPDATA%\Microsoft\Windows\Start Menu\Programs\AutoQC\AutoQC.appref-ms
                Path.Combine(Path.Combine(allProgramsPath, appName), apprefName)
            };
            var appRefPath = paths.FirstOrDefault(File.Exists);

            if (appRefPath != null)
            {
                var appExe = Application.ExecutablePath;

                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var configFileIconPath = Path.Combine(baseDirectory, "AutoQC_configs.ico");

                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    FileUtil.AddFileTypeClickOnce(TextUtil.EXT_QCFG, "AutoQC.Configuration.0",
                        Resources.Program_AddFileTypesToRegistry_AutoQC_Configuration_File,
                        appRefPath, configFileIconPath);
                }
                else
                {
                    FileUtil.AddFileTypeAdminInstall(TextUtil.EXT_QCFG, "AutoQC.Configuration.0",
                        Resources.Program_AddFileTypesToRegistry_AutoQC_Configuration_File,
                        appExe, configFileIconPath);
                }
            }
        }

        private static void UpdateAutoQcStarter(object sender, DoWorkEventArgs e)
        {
            if (!Settings.Default.KeepAutoQcRunning) return;

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                if (_version != null && !_version.ToString().Equals(_lastInstalledVersion))
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

        public static string AppName => CommonApplicationSettings.ProgramName;

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

        public static void AddTestException(Exception exception)
        {
            lock (TestExceptions)
            {
                TestExceptions.Add(exception);
            }
        }
    }
}
