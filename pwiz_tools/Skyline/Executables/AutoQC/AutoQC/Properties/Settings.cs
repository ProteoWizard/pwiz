using System;
using System.Configuration;

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

        public void UpdateIfNecessary(string oldVersion)
        {
            SharedBatch.Properties.ConfigList.Version = Default.InstalledVersion;
            SharedBatch.Properties.ConfigList.Importer = AutoQcConfig.ReadXml;
            if (Equals(oldVersion, Default.InstalledVersion))
                return;
            var xmlVersion = oldVersion != null ? new Version(oldVersion) : null;
            if (xmlVersion == null || xmlVersion.Major < 21)
                SharedBatch.Properties.Settings.Default.Update(Default.InstalledVersion, Program.AppName, XmlUpdater.GetUpdatedXml);
            Save();
        }
    }
}
