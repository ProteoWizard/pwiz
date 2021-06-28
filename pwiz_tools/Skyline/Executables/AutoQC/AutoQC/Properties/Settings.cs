using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml;
using SharedBatch;
using SharedBatch.Properties;

namespace AutoQC.Properties
{
    internal sealed partial class Settings
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
            ConfigList.Version = currentVersion;
            ConfigList.Importer = AutoQcConfig.ReadXml;
            // return if settings are already correct version
            if (Equals(Default.InstalledVersion, currentVersion))
                return;
            // get file path of user.config file with old settings
            var xmlVersion = !string.IsNullOrEmpty(Default.InstalledVersion) ? new Version(Default.InstalledVersion) : null;
            var possibleOldSettingsVersion = xmlVersion != null && xmlVersion.Major > 1
                ? Default.InstalledVersion
                : "1.0.0.0";
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
                .FilePath;
            var possibleOldConfigFile = configFile.Replace(currentVersion, possibleOldSettingsVersion);
            // check if old user.config file exists (if it does not, this is a first installation of autoQC)
            if (File.Exists(possibleOldConfigFile))
            {
                // update ConfigList using old user.config
                if (xmlVersion == null || xmlVersion.Major < 21)
                    SharedBatch.Properties.Settings.Default.Update(possibleOldConfigFile, currentVersion, Program.AppName, XmlUpdater.GetUpdatedXml);
                // update skyline settings of all configuration if settings were very old and still in AutoQC.Properties.Settings
                if (migrate)
                    GetSettingsFromPreviousVersion(possibleOldConfigFile);
            }
            Default.InstalledVersion = currentVersion;
            Save();
        }

        private void GetSettingsFromPreviousVersion(string fileName)
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

                var skySettings = new SkylineSettings(type, skylineInstallDir);

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
