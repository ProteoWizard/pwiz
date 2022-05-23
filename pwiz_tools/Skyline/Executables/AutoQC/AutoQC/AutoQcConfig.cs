using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    [XmlRoot("autoqc_config")]
    public class AutoQcConfig : IConfig
    {
        public const string AUTOQC_CONFIG = "autoqc_config";
        
        public AutoQcConfig(string name, bool isEnabled, DateTime created, DateTime modified,
            MainSettings mainSettings, PanoramaSettings panoramaSettings, SkylineSettings skylineSettings)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(Resources.AutoQcConfig_AutoQcConfig_Configuration_name_cannot_be_blank__Please_enter_a_name_for_the_configuration_);
            }
            Name = name;
            IsEnabled = isEnabled;
            Created = created;
            Modified = modified;
            MainSettings = mainSettings;
            PanoramaSettings = panoramaSettings;
            SkylineSettings = skylineSettings;
        }

        public readonly string Name;

        public readonly bool IsEnabled;

        public readonly DateTime Created;

        public readonly DateTime Modified;

        public readonly MainSettings MainSettings;

        public readonly PanoramaSettings PanoramaSettings;

        public readonly SkylineSettings SkylineSettings;

        public string GetName() { return Name; }

        public DateTime GetModified() { return Modified; }

        public bool TryPathReplace(string oldRoot, string newRoot, out IConfig replacedConfig)
        {
            replacedConfig = this;
            return false;
        }

        public IConfig ForcePathReplace(string oldRoot, string newRoot)
        {
            // method used in SkylineBatch to create valid configurations with no files on disk (all downloaded later)
            throw new NotImplementedException();
        }

        public IConfig ReplaceSkylineVersion(SkylineSettings newSettings)
        {
            return new AutoQcConfig(Name, IsEnabled, Created, Modified, MainSettings, PanoramaSettings, newSettings);
        }

        public ListViewItem AsListViewItem(IConfigRunner runner, Graphics graphics)
        {
            var lvi = new ListViewItem(Name);
            lvi.Checked = IsEnabled;
            lvi.UseItemStyleForSubItems = false; // So that we can change the color for sub-items.
            lvi.SubItems.Add(User);
            lvi.SubItems.Add(Created.ToShortDateString());
            lvi.SubItems.Add(runner.GetDisplayStatus());

            var runnerStatusIndex = lvi.SubItems.Count - 1;
            lvi.SubItems[runnerStatusIndex].ForeColor = runner.GetDisplayColor();
            return lvi;
        }

        public string User => PanoramaSettings.PublishToPanorama ? PanoramaSettings.PanoramaUserEmail : string.Empty;
        
        public bool UsesSkyline => SkylineSettings.Type == SkylineType.Skyline;

        public bool UsesSkylineDaily => SkylineSettings.Type == SkylineType.SkylineDaily;

        public bool UsesCustomSkylinePath => SkylineSettings.Type == SkylineType.Custom;

        public string GetConfigDir()
        {
            var skylineFileDir = Path.GetDirectoryName(MainSettings.SkylineFilePath);
            return Path.Combine(skylineFileDir ?? string.Empty, FileUtil.GetSafeNameForDir(Name));
        }

        public string getConfigFilePath(string file)
        {
            return Path.Combine(GetConfigDir(), file);
        }

        private enum Attr
        {
            name,
            is_enabled,
            created,
            modified
        }


        public XmlSchema GetSchema()
        {
            return null;
        }

        #region XML

        public static AutoQcConfig ReadXml(XmlReader reader, decimal version)
        {
            switch (version)
            {
                case 21.1M:
                    return ReadXml(reader, MainSettings.ReadXml, PanoramaSettings.ReadXml, SkylineSettings.ReadXml);
                case 20.2M:
                    return ReadXml(reader, MainSettings.ReadXml, PanoramaSettings.ReadXml, SkylineSettings.ReadXmlVersion_20_2);
                default:
                    throw new ArgumentException(string.Format(
                        Resources.AutoQcConfig_ReadXml_The_version_of_the_imported_file__0__was_not_recognized__No_configurations_will_be_imported_,
                        version));
            }
        }

        private static AutoQcConfig ReadXml(XmlReader reader, Func<XmlReader, MainSettings> mainSettingsReader,
            Func<XmlReader, PanoramaSettings> panoramaSettingsReader,
            Func<XmlReader, SkylineSettings> skylineSettingsReader)
        {
            var name = reader.GetAttribute(Attr.name);
            
            var isEnabled = reader.GetBoolAttribute(Attr.is_enabled);
            DateTime dateTime;
            DateTime.TryParse(reader.GetAttribute(Attr.created), out dateTime);
            var created = dateTime;
            DateTime.TryParse(reader.GetAttribute(Attr.modified), out dateTime);
            var modified = dateTime;


            MainSettings mainSettings = null;
            PanoramaSettings panoramaSettings = null;
            SkylineSettings skylineSettings = null;
            string exceptionMessage = null;

            while(reader.Read())
            {
                if (reader.IsEndElement(AUTOQC_CONFIG))
                {
                   break; // We are done reading the config
                }

                try
                {
                    if (reader.IsElement(MainSettings.XML_EL))
                    {
                        mainSettings = mainSettingsReader(reader);
                    }
                    else if (reader.IsElement(PanoramaSettings.XML_EL))
                    {
                        panoramaSettings = panoramaSettingsReader(reader);
                    }
                    else if (reader.IsElement(SkylineSettings.XML_EL))
                    {
                        skylineSettings = skylineSettingsReader(reader);
                    }
                }
                catch (ArgumentException e)
                {
                    exceptionMessage = string.Format("\"{0}\" ({1})", name, e.Message);
                }
            }
            
            // Old configurations did not have Skyline settings. Create default SkylineSettings.
            skylineSettings = skylineSettings ?? new SkylineSettings(SkylineType.Skyline, null);

            // finish reading config before exception is thrown so following configs aren't messed up
            if (exceptionMessage != null)
                throw new ArgumentException(exceptionMessage);

            return new AutoQcConfig(name, isEnabled, created, modified, mainSettings, panoramaSettings, skylineSettings);
        }

        // Method to read SkylineSettings from v.21.1.0.158 of AutoQC Loader. Not putting this in SkylineSettings.cs since that code
        // is shared with SkylineBatch.
        private static SkylineSettings ReadSkylineSettings_v21_1_0_158(XmlReader reader)
        {
            var type = (SkylineType)Enum.Parse(typeof(SkylineType), reader.GetAttribute(Old_Attr.Type), false);
            var cmdPath = Path.GetDirectoryName(reader.GetAttribute(Old_Attr.CmdPath));
            return new SkylineSettings(type, null, cmdPath);
        }
        // Attributes that were used in version 21.1.0.158 of AutoQC Loader
        private enum Old_Attr
        {
            Type,
            CmdPath
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(AUTOQC_CONFIG);
            writer.WriteAttribute(Attr.name, Name);
            writer.WriteAttribute(Attr.is_enabled, IsEnabled);
            writer.WriteAttributeIfString(Attr.created, Created.ToShortDateString() + " " + Created.ToShortTimeString());
            writer.WriteAttributeIfString(Attr.modified, Modified.ToShortDateString() + " " + Modified.ToShortTimeString());
            MainSettings.WriteXml(writer);
            PanoramaSettings.WriteXml(writer);
            SkylineSettings.WriteXml(writer);
            writer.WriteEndElement();
        }

        #endregion

        public void Validate()
        {
            Validate(false);
        }

        public void Validate(bool doServerCheck)
        {
            if (string.IsNullOrEmpty(Name))
                throw new ArgumentException(Resources.AutoQcConfig_Validate_Please_enter_a_name_for_the_configuration_);

            MainSettings.ValidateSettings();
            SkylineSettings.Validate();
            PanoramaSettings.ValidateSettings(doServerCheck);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Name: ").AppendLine(Name);
            sb.Append("Enabled: ").AppendLine(IsEnabled.ToString());
            sb.Append("Created: ").Append(Created.ToShortDateString()).Append(" ").AppendLine(Created.ToShortTimeString());
            sb.Append("Modified: ").Append(Modified.ToShortDateString()).Append(" ").AppendLine(Modified.ToShortTimeString());
            sb.AppendLine().AppendLine("Main Settings");
            sb.Append(MainSettings);
            sb.AppendLine().AppendLine("Panorama Settings");
            sb.Append(PanoramaSettings);
            sb.AppendLine().AppendLine("Skyline Settings");
            sb.Append(SkylineSettings);
            return sb.ToString();
        }

        #region Equality members

        protected bool Equals(AutoQcConfig other)
        {
            return string.Equals(Name, other.Name)
                   && IsEnabled == other.IsEnabled
                   && Equals(MainSettings, other.MainSettings)
                   && Equals(PanoramaSettings, other.PanoramaSettings)
                   && Equals(SkylineSettings, other.SkylineSettings);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AutoQcConfig) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (MainSettings != null ? MainSettings.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PanoramaSettings != null ? PanoramaSettings.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (SkylineSettings != null ? SkylineSettings.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }
}
