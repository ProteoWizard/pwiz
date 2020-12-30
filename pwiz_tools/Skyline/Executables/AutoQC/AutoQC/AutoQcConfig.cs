using System;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using AutoQC.Properties;

namespace AutoQC
{
    [XmlRoot("autoqc_config")]
    public class AutoQcConfig
    {

        public AutoQcConfig(string name, bool isEnabled, DateTime created, DateTime modified,
            MainSettings mainSettings, PanoramaSettings panoramaSettings, SkylineSettings skylineSettings)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(Resources.AutoQcConfig_Please_enter_a_name_for_the_configuration_);
            }
            Name = name;
            IsEnabled = isEnabled;
            User = panoramaSettings.PanoramaUserEmail;
            Created = created;
            Modified = modified;
            MainSettings = mainSettings;
            PanoramaSettings = panoramaSettings;
            SkylineSettings = skylineSettings;
        }

        public readonly string Name;

        public bool IsEnabled;

        public readonly string User;

        public readonly DateTime Created;

        public readonly DateTime Modified;

        public readonly MainSettings MainSettings;

        public readonly PanoramaSettings PanoramaSettings;

        public readonly SkylineSettings SkylineSettings;

        public bool UsesSkyline => SkylineSettings.Type == SkylineType.Skyline;

        public bool UsesSkylineDaily => SkylineSettings.Type == SkylineType.SkylineDaily;

        public bool UsesCustomSkylinePath => SkylineSettings.Type == SkylineType.Custom;

        private enum Attr
        {
            Name,
            IsEnabled,
            User,
            Created,
            Modified
        }


        public XmlSchema GetSchema()
        {
            return null;
        }

        #region XML

        public static AutoQcConfig ReadXml(XmlReader reader)
        {
            var name = reader.GetAttribute(Attr.Name);
            
            var isEnabled = reader.GetBoolAttribute(Attr.IsEnabled);
            DateTime dateTime;
            DateTime.TryParse(reader.GetAttribute(Attr.Created), out dateTime);
            var created = dateTime;
            DateTime.TryParse(reader.GetAttribute(Attr.Modified), out dateTime);
            var modified = dateTime;

            do
            {
                reader.Read();
            } while (reader.NodeType != XmlNodeType.Element);

            MainSettings mainSettings = null;
            PanoramaSettings panoramaSettings = null;
            SkylineSettings skylineSettings = null;
            string exceptionMessage = null;
            try
            {
                mainSettings = MainSettings.ReadXml(reader);
                do
                {
                    reader.Read();
                } while (reader.NodeType != XmlNodeType.Element);
                panoramaSettings = PanoramaSettings.ReadXml(reader);
                do
                {
                    reader.Read();
                } while (reader.NodeType != XmlNodeType.Element);
                skylineSettings = SkylineSettings.ReadXml(reader);
            }
            catch (ArgumentException e)
            {
                exceptionMessage = string.Format("\"{0}\" ({1})", name, e.Message);
            }

            // finish reading config before exception is thrown so following configs aren't messed up
            do
            {
                reader.Read();
            } while (!(reader.Name == "autoqc_config" && reader.NodeType == XmlNodeType.EndElement));

            if (exceptionMessage != null)
                throw new ArgumentException(exceptionMessage);

            return new AutoQcConfig(name, isEnabled, created, modified, mainSettings, panoramaSettings, skylineSettings);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("autoqc_config");
            writer.WriteAttribute(Attr.Name, Name);
            writer.WriteAttribute(Attr.IsEnabled, IsEnabled);
            writer.WriteAttributeIfString(Attr.User, User);
            writer.WriteAttributeIfString(Attr.Created, Created.ToShortDateString() + " " + Created.ToShortTimeString());
            writer.WriteAttributeIfString(Attr.Modified, Modified.ToShortDateString() + " " + Modified.ToShortTimeString());
            MainSettings.WriteXml(writer);
            PanoramaSettings.WriteXml(writer);
            SkylineSettings.WriteXml(writer);
            writer.WriteEndElement();
        }

        #endregion

        public void Validate()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new ArgumentException("Please enter a name for the configuration.");
            }

            MainSettings.ValidateSettings();
            PanoramaSettings.ValidateSettings();
            SkylineSettings.Validate();
           
        }

        public virtual ProcessInfo RunBefore(ImportContext importContext)
        {
            string archiveArgs = null;
            if (!importContext.ImportExisting)
            {
                // If we are NOT importing existing results, create an archive (if required) of the 
                // Skyline document BEFORE importing a results file.
                archiveArgs = MainSettings.GetArchiveArgs(MainSettings.GetLastArchivalDate(), DateTime.Today);
            }
            if (string.IsNullOrEmpty(archiveArgs))
            {
                return null;
            }
            var args = string.Format("--in=\"{0}\" {1}", MainSettings.SkylineFilePath, archiveArgs);
            return new ProcessInfo(SkylineSettings.CmdPath, args, args);
        }

        public virtual ProcessInfo RunAfter(ImportContext importContext)
        {
            string archiveArgs = null;
            var currentDate = DateTime.Today;
            if (importContext.ImportExisting && importContext.ImportingLast())
            {
                // If we are importing existing files in the folder, create an archive (if required) of the 
                // Skyline document AFTER importing the last results file.
                var oldestFileDate = importContext.GetOldestImportedFileDate(MainSettings.LastAcquiredFileDate);
                var today = DateTime.Today;
                if (oldestFileDate.Year < today.Year || oldestFileDate.Month < today.Month)
                {
                    archiveArgs = MainSettings.GetArchiveArgs(currentDate.AddMonths(-1), currentDate);
                }
            }
            if (string.IsNullOrEmpty(archiveArgs))
            {
                return null;
            }
            var args = string.Format("--in=\"{0}\" {1}", MainSettings.SkylineFilePath, archiveArgs);
            return new ProcessInfo(SkylineSettings.CmdPath, args, args);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Name: ").AppendLine(Name);
            sb.Append("Enabled: ").AppendLine(IsEnabled.ToString());
            sb.Append("Panorama User: ").AppendLine(User);
            sb.Append("Created: ").Append(Created.ToShortDateString()).Append(" ").AppendLine(Created.ToShortTimeString());
            sb.Append("Modified: ").Append(Modified.ToShortDateString()).Append(" ").AppendLine(Modified.ToShortTimeString());
            sb.AppendLine("").AppendLine("Main Settings");
            sb.Append(MainSettings);
            sb.AppendLine("").AppendLine("Panorama Settings");
            sb.Append(PanoramaSettings);
            sb.AppendLine("").AppendLine("Skyline Settings");
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
