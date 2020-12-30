﻿using System;
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
            Type = type;

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
                    CmdPath = Path.Combine(folderPath, Installations.SkylineCmdExe);
                    break;
            }
        }

        public void Validate()
        {
            if (!File.Exists(CmdPath))
            {
                switch (Type)
                {
                    case SkylineType.Skyline:
                        throw new ArgumentException(Resources.SkylineSettings_Unable_to_find_Skyline_installation_);
                    case SkylineType.SkylineDaily:
                        throw new ArgumentException(Resources.SkylineSettings_Unable_to_find_Skyline_Daily_installation_);
                    case SkylineType.Local:
                        throw new ArgumentException(Resources.SkylineSettings_Unable_to_find_local_Skyline_installation_);
                    case SkylineType.Custom:
                        throw new ArgumentException(string.Format(Resources.SkylineSettings_Unable_to_find_Skyline_installation_at__0__, CmdPath));
                }
            }
        }

        public readonly SkylineType Type;

        public readonly string CmdPath;


        private enum Attr
        {
            Type,
            CmdPath,
        }

        

        public static SkylineSettings ReadXml(XmlReader reader)
        {
            var type = Enum.Parse(typeof(SkylineType), reader.GetAttribute(Attr.Type), false);
            var cmdPath = Path.GetDirectoryName(reader.GetAttribute(Attr.CmdPath));
            return new SkylineSettings((SkylineType)type, cmdPath);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("config_skyline_settings");
            writer.WriteAttributeIfString(Attr.Type, Type.ToString());
            writer.WriteAttributeIfString(Attr.CmdPath, CmdPath);
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
