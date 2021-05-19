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
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository.Hierarchy;
using SharedBatch;
using SharedBatch.Properties;
using Resources = AutoQC.Properties.Resources;
using Settings = AutoQC.Properties.Settings;

namespace AutoQC
{
    class Program
    {
        private static Version _version;
        private static string _lastInstalledVersion;

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

                ProgramLog.Info($"Starting {AppName}");

                string configFile = null;
                try
                {
                    if (!InitSkylineSettings()) return;
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    configFile = config.FilePath;
                    ProgramLog.Info(string.Format("user.config path: {0}", configFile));
                }
                catch (Exception e)
                {
                    ProgramLog.Error(e.Message, e);
                    var folderToCopy = Path.GetDirectoryName(ProgramLog.GetProgramLogFilePath()) ?? string.Empty;
                    var newFileName = Path.Combine(folderToCopy, "error-user.config");
                    var message = "There was an error loading the saved configurations.";
                    if (configFile != null)
                    {
                        File.Copy(configFile, newFileName, true);
                        File.Delete(configFile);
                        message += Environment.NewLine + Environment.NewLine +
                                   string.Format(
                                       "To help improve {0} in future versions, please post the configuration file to the support board:",
                                       AppName) +
                                   Environment.NewLine +
                                   newFileName;
                    }

                    MessageBox.Show(message);
                    return;
                }

                UpgradeSettingsIfRequired(configFile);

                var openFile = GetFirstArg(args);
                if (!string.IsNullOrEmpty(openFile))
                {
                    ProgramLog.Info($"Reading configurations from file {openFile}");
                }

                var form = new MainForm(openFile) {Text = Version()};

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

        private static void UpgradeSettingsIfRequired(string configFile)
        {
            if (Settings.Default.SettingsUpgradeRequired)
            {
                Settings.Default.Upgrade(); // This should copy all the settings from the previous version
                Settings.Default.SettingsUpgradeRequired = false;
                Settings.Default.Save();
            }
            Settings.Default.Reload();

            GetCurrentAndLastInstalledVersions();

            if (ConfigMigrationRequired())
            {
                if (SharedBatch.Properties.Settings.Default.ConfigList.Count > 0)
                {
                    // This should not happen
                    ProgramLog.Error("Configuration migration is required but config list in SharedBatch properties is not empty. Skipping migration.");
                    return;
                }

                ProgramLog.Info("Migrating configurations from previous version.");
                
                var list = ReadOldConfigList(configFile);
                if (list.Count > 0)
                {
                    ProgramLog.Info($"Found {list.Count} configurations to migrate.");
                    SharedBatch.Properties.Settings.Default.ConfigList = list;
                    SharedBatch.Properties.Settings.Default.Save();
                }
                else
                {
                    ProgramLog.Info("No configurations were found.");
                }
            }
        }

        private static void GetCurrentAndLastInstalledVersions()
        {
            // CurrentDeployment is null if it isn't network deployed.
            _version = ApplicationDeployment.IsNetworkDeployed
                ? ApplicationDeployment.CurrentDeployment.CurrentVersion
                : null;

            _lastInstalledVersion = Settings.Default.InstalledVersion ?? string.Empty;

            if (_version != null && !_version.ToString().Equals(_lastInstalledVersion))
            {
                ProgramLog.Info(string.Empty.Equals(_lastInstalledVersion)
                    ? $"This is a first install and run of version: {_version}."
                    : $"Current version: {_version} is newer than the last installed version: {_lastInstalledVersion}.");

                Settings.Default.InstalledVersion = _version.ToString();
                Settings.Default.Save();
                return;
            }

            var _currentVerStr = _version != null ? _version.ToString() : string.Empty;
            ProgramLog.Info($"Current version: '{_currentVerStr}' is the same as last installed version: '{_lastInstalledVersion}'.");
        }

        private static bool ConfigMigrationRequired()
        {
            if (_version != null)
            {
                if (string.IsNullOrEmpty(_lastInstalledVersion))
                {
                    return true;
                }

                if (System.Version.TryParse(_lastInstalledVersion, out var lastVersion))
                {
                    // ProgramLog.Info($"Current Version: {_version.Major}-{_version.MajorRevision}-{_version.Minor}-{_version.MinorRevision}");
                    // ProgramLog.Info($"Previous Version: {lastVersion.Major}-{lastVersion.MajorRevision}-{lastVersion.Minor}-{lastVersion.MinorRevision}");
                    if (lastVersion.Major == 1 && lastVersion.Minor == 1 && lastVersion.MinorRevision <= 20237)
                    {
                        return true;
                    }
                }
                else
                {
                    ProgramLog.Info($"Could not parse last installed version {_lastInstalledVersion}. Skipping configuration migration.");
                    return false;
                }
            }

            ProgramLog.Info("Configuration migration not required.");
            return false;
        }

        private static ConfigList ReadOldConfigList(string configFile)
        {
            string skylineType = null;
            string skylineInstallDir = string.Empty;
            ConfigList configList = null;
            try
            {
                using (var stream = new FileStream(configFile, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        var inAutoQcProps = false;
                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.Element && reader.Name.Equals("AutoQC.Properties.Settings"))
                            {
                                inAutoQcProps = true;
                                continue;
                            }

                            if (reader.NodeType == XmlNodeType.EndElement && reader.Name.Equals("AutoQC.Properties.Settings"))
                            {
                                break;
                            }

                            if (!inAutoQcProps)
                            {
                                continue;
                            }

                            if (reader.NodeType == XmlNodeType.Element && reader.Name.Equals("setting"))
                            {
                                var propName = reader.GetAttribute("name");
                                if ("SkylineInstallDir".Equals(propName))
                                {
                                    skylineInstallDir = ReadValue(reader);
                                }
                                else if ("SkylineType".Equals(propName))
                                {
                                    skylineType = ReadValue(reader);
                                }
                            }
                            else if (reader.Name.Equals("ConfigList"))
                            {
                                configList = new ConfigList();
                                ConfigList.Importer = AutoQcConfig.ReadXml;
                                configList.ReadXml(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // possible xml format error
                ProgramLog.Error(string.Format(Resources.ConfigManager_Import_An_error_occurred_while_importing_configurations_from__0__, configFile) + Environment.NewLine +
                             e.Message);
                return new ConfigList();
            }

            var updatedConfigs = new ConfigList();

            if (configList != null && configList.Count > 0)
            {
                SkylineType type;
                if (!string.IsNullOrEmpty(skylineInstallDir))
                {
                    type = SkylineType.Custom;
                }
                else
                {
                    type = "Skyline-daily".Equals(skylineType) ? SkylineType.SkylineDaily : SkylineType.Skyline;
                }

                foreach (var iconfig in configList.ToList())
                {
                    var config = (AutoQcConfig)iconfig;

                    var skySettings = new SkylineSettings(type, skylineInstallDir);

                    AutoQcConfig updatedConfig = new AutoQcConfig(config.Name, false,
                        config.Created, config.Modified,
                        config.MainSettings, config.PanoramaSettings, skySettings);
                    updatedConfigs.Add(updatedConfig);
                }
            }

            return updatedConfigs;
        }

        private static string ReadValue(XmlReader reader)
        {
            if (reader.ReadToDescendant("value"))
            {
                if (reader.IsEmptyElement)
                {
                    return string.Empty;
                }
                reader.Read();
                return reader.Value;
            }

            return string.Empty;
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
            var appReference = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                               "\\Microsoft\\Windows\\Start Menu\\Programs\\MacCoss Lab, UW\\" +
                               AppName.Substring(0, AppName.IndexOf(" ")) + TextUtil.EXT_APPREF;
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
