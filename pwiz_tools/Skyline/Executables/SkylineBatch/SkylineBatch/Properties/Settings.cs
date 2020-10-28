using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SkylineBatch.Properties
{
    internal sealed partial class Settings
    {

        [UserScopedSetting]
        public ConfigList ConfigList
        {
            get
            {
                var list = (ConfigList) this["ConfigList"]; // Not L10N
                if (list == null)
                {
                    list = new ConfigList();
                    ConfigList = list;
                }
                return list;
            }
            set {
                this["ConfigList"] = value; // Not L10N
            }
        }

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

    public class ConfigList : Collection<SkylineBatchConfig>, IXmlSerializable
    {
        #region IXmlSerializable Members

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            var isEmpty = reader.IsEmptyElement;

            // Read past the property element
            reader.Read();

            // For an empty list in Settings.Default
            if (isEmpty)
            {
                return;
            }

            // Read list items
            var list = new List<SkylineBatchConfig>();
            while (reader.IsStartElement())
            {
                list.Add(reader.Deserialize(new SkylineBatchConfig()));
                reader.Read();
            }

            Clear();
            foreach (var config in list)
            {
                Add(config);
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            try
            {
                foreach (var config in this)
                {
                    config.WriteXml(writer);
                }
            }
            catch (Exception)
            {
                // System.Xml will unfortunately swallow this exception when we rethrow it,
                // in the context of saving a settings list to user.config.
                // So we have to save it and throw it again after Settings.Default.Save is
                // complete - see SkylineWindow.OnClosing.
                //                Settings.Default.SaveException = ex;
                throw;
            }
        }

        #endregion // IXmlSerializable Members  
    }
}
