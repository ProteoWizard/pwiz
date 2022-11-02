using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml;
using SharedBatch;
using SharedBatch.Properties;

namespace AutoQC.Properties
{
    public sealed partial class Settings
    {

        [UserScopedSetting]
        [DefaultSettingValue("false")]
        public bool MinimizeToSystemTray {
            get
            {
                return (bool) this["MinimizeToSystemTray"];
            }
            set
            {
                this["MinimizeToSystemTray"] = value;
            }
        }

        [UserScopedSetting]
        [DefaultSettingValue("false")]
        public bool KeepAutoQcRunning
        {
            get
            {
                return (bool)this["KeepAutoQcRunning"];
            }
            set
            {
                this["KeepAutoQcRunning"] = value;
            }
        }

        public new void Upgrade()
        {
            base.Upgrade();
            SharedBatch.Properties.Settings.Default.Upgrade();
        }

        public new void Save()
        {
            base.Save();
            SharedBatch.Properties.Settings.Default.Save();
        }

        public void UpdateIfNecessary(string currentVersion, bool migrate)
        {
            // set ConfigList methods
            ConfigList.XmlVersion = Default.XmlVersion;
            ConfigList.Importer = AutoQcConfig.ReadXml;
            // return if settings are already correct version
            if (Equals(Default.InstalledVersion, currentVersion))
            {
                ProgramLog.Info(
                    $"No config settings update is required. Installed version: {Default.InstalledVersion}; Current version: {currentVersion}");
                return;
            }

            // get file path of user.config file with old settings
            var savedVersion = !string.IsNullOrEmpty(Default.InstalledVersion) ? new Version(Default.InstalledVersion) : null;
            var possibleOldSettingsVersion = savedVersion != null && savedVersion.Major > 1
                ? Default.InstalledVersion
                : "1.0.0.0";
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
                .FilePath;
            var possibleOldConfigFile = GetOldConfigFile(configFile, currentVersion, possibleOldSettingsVersion);
            // check if old user.config file exists (if it does not, this is a first installation of autoQC)
            if (File.Exists(possibleOldConfigFile))
            {
                ProgramLog.Info($"Reading old settings from: {possibleOldConfigFile}");
                // update ConfigList using old user.config
                if (savedVersion == null || savedVersion.Major < 21)
                {
                    SharedBatch.Properties.Settings.Default.Update(possibleOldConfigFile, Default.XmlVersion,
                        Program.AppName);
                }

                if (migrate)
                {
                    // Update skyline settings of all configuration if settings are old (v 1.1.0.20237 and older)
                    // and still in AutoQC.Properties.Settings
                    ProgramLog.Info($"Migrating old Skyline settings from: {possibleOldConfigFile}");
                    GetSkylineSettingsFromPreviousVersion(possibleOldConfigFile);
                }
            }
            Default.InstalledVersion = currentVersion;
            Save();
        }

        private static string GetOldConfigFile(string defaultConfigFile, string currentVersion, string oldSettingsVersion)
        {
            if (defaultConfigFile.Contains(currentVersion))
            {
                return defaultConfigFile.Replace(currentVersion, oldSettingsVersion);
            }

            // Handle the case where there is a mismatch between the version in AssemblyInfo and the published version.
            // The current user.config is saved in a folder named for the version in AssemblyInfo.  The default is 1.0.0.0 but we
            // are now updating it to be the current published version (e.g. 21.1.0.158).  If there is a mismatch between the 
            // published version (ApplicationDeployment.CurrentDeployment.CurrentVersion) and the version in AssemblyInfo
            // then the replacement above will not work.  Here we will get the path to the directory that contains the current
            // user.config(e.g. 21.1.0.158) and build the path to the old user.config.
            var configDir = FileUtil.GetDirectorySafe(FileUtil.GetDirectorySafe(defaultConfigFile));
            return Path.Combine(configDir, oldSettingsVersion, Path.GetFileName(defaultConfigFile));
        }

        private void GetSkylineSettingsFromPreviousVersion(string fileName)
        {
            string skylineType = null;
            string skylineInstallDir = string.Empty;
            
            using (var stream = new FileStream(fileName, FileMode.Open))
            {
                using (var reader = XmlReader.Create(stream))
                {
                    var inAutoQcProps = false;
                    while (reader.Read())
                    {
                        if (reader.IsElement("AutoQC.Properties.Settings"))
                        {
                            inAutoQcProps = true;
                            continue;
                        }

                        if (reader.IsEndElement("AutoQC.Properties.Settings"))
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
                    }
                }
            }

            SkylineType type;
            if (!string.IsNullOrEmpty(skylineInstallDir))
            {
                type = SkylineType.Custom;
            }
            else
            {
                type = "Skyline-daily".Equals(skylineType) ? SkylineType.SkylineDaily : SkylineType.Skyline;
            }

            var updatedConfigs = new ConfigList();
            foreach (var iconfig in SharedBatch.Properties.Settings.Default.ConfigList.ToList())
            {
                var config = (AutoQcConfig)iconfig;

                var skySettings = new SkylineSettings(type, null, skylineInstallDir);

                AutoQcConfig updatedConfig = new AutoQcConfig(config.Name, false,
                    config.Created, config.Modified,
                    config.MainSettings, config.PanoramaSettings, skySettings);
                updatedConfigs.Add(updatedConfig);
            }

            SharedBatch.Properties.Settings.Default.ConfigList = updatedConfigs;
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
    }
}
