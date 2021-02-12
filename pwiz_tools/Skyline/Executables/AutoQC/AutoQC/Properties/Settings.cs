using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

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
    }
}
