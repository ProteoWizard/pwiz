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

        public void UpdateIfNecessary(string oldVersion, string currentVersion, bool migrate)
        {
            ConfigList.Version = currentVersion;
            ConfigList.Importer = AutoQcConfig.ReadXml;
            if (Equals(oldVersion, Default.InstalledVersion))
                return;
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
                .FilePath;
            var lastVersion = !string.IsNullOrEmpty(oldVersion)
                ? Default.InstalledVersion
                : "1.0.0.0";
            var oldConfigFile = configFile.Replace(currentVersion, lastVersion);
            if (!File.Exists(oldConfigFile))
            {
                throw new Exception("Old settings did not exist");
            }
            var xmlVersion = !string.IsNullOrEmpty(oldVersion) ? new Version(oldVersion) : null;
            if (xmlVersion == null || xmlVersion.Major < 21)
                SharedBatch.Properties.Settings.Default.Update(oldConfigFile, Default.InstalledVersion, Program.AppName, XmlUpdater.GetUpdatedXml);
            if (migrate)
            {
                GetSettingsFromPreviousVersion(oldConfigFile);
            }

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
