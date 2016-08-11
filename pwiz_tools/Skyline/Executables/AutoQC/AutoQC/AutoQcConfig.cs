using System;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace AutoQC
{
    [XmlRoot("autoqc_config")]
    public class AutoQcConfig: IXmlSerializable
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        public string User { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }

        public MainSettings MainSettings { get; set; }
        public PanoramaSettings PanoramaSettings { get; set; }


        private enum ATTR
        {
            name,
            is_enabled,
            user,
            created,
            modified
        };


        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            Name = reader.GetAttribute(ATTR.name);
            IsEnabled = reader.GetBoolAttribute(ATTR.is_enabled);
            User = reader.GetAttribute(ATTR.user);
            DateTime dateTime;
            DateTime.TryParse(reader.GetAttribute(ATTR.created), out dateTime);
            Created = dateTime;
            DateTime.TryParse(reader.GetAttribute(ATTR.modified), out dateTime);
            Modified = dateTime;

            reader.Read();

            var mainSettings = new MainSettings();
            mainSettings.ReadXml(reader);
            MainSettings = mainSettings;
            reader.Read();

            var panoramaSettings = new PanoramaSettings();
            panoramaSettings.ReadXml(reader);
            PanoramaSettings = panoramaSettings;
            reader.Read();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("autoqc_config");
            writer.WriteAttribute(ATTR.name, Name);
            writer.WriteAttribute(ATTR.is_enabled, IsEnabled);
            writer.WriteAttributeIfString(ATTR.user, User);
            writer.WriteAttributeIfString(ATTR.created, Created.ToShortDateString() + " " + Created.ToShortTimeString());
            writer.WriteAttributeIfString(ATTR.modified, Modified.ToShortDateString() + " " + Modified.ToShortTimeString());
            MainSettings.WriteXml(writer);
            PanoramaSettings.WriteXml(writer);
            writer.WriteEndElement();
        }

        public static AutoQcConfig Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AutoQcConfig());
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new ArgumentException("Please enter a name for the configuration.");
            }

            MainSettings.ValidateSettings();
            PanoramaSettings.ValidateSettings();
           
        }

        public static AutoQcConfig GetDefault()
        {
            var config = new AutoQcConfig
            {
                MainSettings = MainSettings.GetDefault(),
                PanoramaSettings = PanoramaSettings.GetDefault()
            };
            return config;
        }

        public AutoQcConfig Copy()
        {
            return new AutoQcConfig
            {
                Name = Name,
                IsEnabled = false, // Disable by default
                User = PanoramaSettings.PanoramaUserEmail,
                MainSettings = MainSettings.Clone(),
                PanoramaSettings = PanoramaSettings.Clone()
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Name: ").AppendLine(Name);
            sb.Append("Enabled: ").AppendLine(IsEnabled.ToString());
            sb.Append("Panorama User: ").AppendLine(User);
            sb.Append("Created: ").Append(Created.ToShortDateString()).AppendLine(Created.ToShortTimeString());
            sb.Append("Modified: ").Append(Modified.ToShortDateString()).AppendLine(Modified.ToShortTimeString());
            sb.AppendLine("").AppendLine("Main Settings");
            sb.Append(MainSettings);
            sb.AppendLine("").AppendLine("Panorama Settings");
            sb.Append(PanoramaSettings);
            return sb.ToString();
        }

        #region Equality members

        protected bool Equals(AutoQcConfig other)
        {
            return string.Equals(Name, other.Name)
                   && IsEnabled == other.IsEnabled
                   && Equals(MainSettings, other.MainSettings)
                   && Equals(PanoramaSettings, other.PanoramaSettings);
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
                hashCode = (hashCode*397) ^ IsEnabled.GetHashCode();
                hashCode = (hashCode*397) ^ (MainSettings != null ? MainSettings.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PanoramaSettings != null ? PanoramaSettings.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }
}
