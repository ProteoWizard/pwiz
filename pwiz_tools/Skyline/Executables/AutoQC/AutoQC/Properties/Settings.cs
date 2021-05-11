
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

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string InstalledVersion
        {
            get
            {
                return (string)this["InstalledVersion"];
            }
            set
            {
                this["InstalledVersion"] = value;
            }
        }

        public override void Upgrade()
        {
            ProgramLog.Info("In Upgrade method");
            ProgramLog.Info("SettingsKey: " + SettingsKey);
            var props = Properties;
            foreach (SettingsProperty settingsProp in props)
            {
                ProgramLog.Info("Property: " + settingsProp.Name);
            }

            try
            {
                var configList = GetPreviousVersion("ConfigList");
                if (configList != null)
                {
                    ProgramLog.Info("Found ConfigList in previous version");
                    ProgramLog.Info("Current version " + Program.Version());
                    ProgramLog.Info("Previous version " + Default.InstalledVersion);
                    if (configList is ConfigList)
                    {
                        ProgramLog.Info("It is a ConfigList!");
                        var cList = configList as ConfigList;
                        ProgramLog.Info("Number of entries: " + cList.Count);
                    }
                }
                base.Upgrade();
            }
            catch (SettingsPropertyNotFoundException e)
            {
                ProgramLog.Error("ConfigList property not found.", e);
            }
        }
    }
}
