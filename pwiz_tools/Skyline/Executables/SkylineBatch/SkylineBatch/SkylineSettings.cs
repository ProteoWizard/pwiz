using System;
using System.IO;
using System.Xml;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public enum SkylineType
    {
        Skyline,
        SkylineDaily,
        Local,
        Custom
    }


    public class SkylineSettings
    {
        public SkylineSettings(SkylineType type, string folderPath = "")
        {
            this.Type = type;

            bool skylineAdminInstallation = !string.IsNullOrEmpty(Settings.Default.SkylineAdminCmdPath);
            bool skylineWebInstallation = !string.IsNullOrEmpty(Settings.Default.SkylineRunnerPath);
            bool skylineDailyAdminInstallation = !string.IsNullOrEmpty(Settings.Default.SkylineDailyAdminCmdPath);
            bool skylineDailyWebInstallation = !string.IsNullOrEmpty(Settings.Default.SkylineDailyRunnerPath);

            switch (type)
            {
                case SkylineType.Skyline:
                    if (skylineWebInstallation)
                        CmdPath = Settings.Default.SkylineRunnerPath;
                    if (skylineAdminInstallation)
                        CmdPath = Settings.Default.SkylineAdminCmdPath;
                    break;
                case SkylineType.SkylineDaily:
                    if (skylineDailyWebInstallation)
                        CmdPath = Settings.Default.SkylineDailyRunnerPath;
                    if (skylineDailyAdminInstallation)
                        CmdPath = Settings.Default.SkylineDailyAdminCmdPath;
                    break;
                case SkylineType.Local:
                    CmdPath = Settings.Default.SkylineLocalCommandPath;
                    break;
                case SkylineType.Custom:
                    CmdPath = string.IsNullOrEmpty(folderPath) ? Settings.Default.SkylineCustomCmdPath : Path.Combine(folderPath, Installations.SkylineCmdExe);
                    CmdPath = File.Exists(CmdPath) ? CmdPath : "";
                    break;
            }
            
            Validate();
        }

        public readonly SkylineType Type;

        public readonly string CmdPath;

        public void Validate()
        {
            if (string.IsNullOrEmpty(CmdPath))
            {
                var typeString = Type.ToString().Contains("Skyline") ? Type + " installation": Type + " Skyline installation";
                throw new ArgumentException($"Skyline Settings: Unable to find {typeString}.");
            }
        }

        private enum Attr
        {
            Type
        }

        

        public static SkylineSettings ReadXml(XmlReader reader)
        {
            var type = Enum.Parse(typeof(SkylineType), reader.GetAttribute(Attr.Type), false);
            return new SkylineSettings((SkylineType)type);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("config_skyline_settings");
            writer.WriteAttributeIfString(Attr.Type, Type.ToString());
            writer.WriteEndElement();
        }

        protected bool Equals(SkylineSettings other)
        {
            return Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SkylineSettings)obj);
        }

        public override int GetHashCode()
        {

            return Type.GetHashCode();
        }
    }
}
