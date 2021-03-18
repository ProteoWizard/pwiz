﻿using System;
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

        public AutoQcConfig(string name, bool isEnabled, DateTime created, DateTime modified,
            MainSettings mainSettings, PanoramaSettings panoramaSettings, SkylineSettings skylineSettings)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(string.Format(Resources.AutoQcConfig_AutoQcConfig___0___is_not_a_valid_name_for_the_configuration_, name) + Environment.NewLine +
                                            Resources.AutoQcConfig_AutoQcConfig_Please_enter_a_name_);
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

        public bool IsEnabled;

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

        public ListViewItem AsListViewItem(IConfigRunner runner)
        {
            var lvi = new ListViewItem(Name);
            var runnerStatusIndex = 2;
            lvi.UseItemStyleForSubItems = false; // So that we can change the color for sub-items.
            lvi.SubItems.Add(User);
            lvi.SubItems.Add(Created.ToShortDateString());
            lvi.SubItems.Add(runner.GetDisplayStatus());
            lvi.SubItems[runnerStatusIndex].ForeColor = runner.GetDisplayColor();
            return lvi;
        }

        public string User => PanoramaSettings.PublishToPanorama ? PanoramaSettings.PanoramaUserEmail : string.Empty;
        
        public bool UsesSkyline => SkylineSettings.Type == SkylineType.Skyline;

        public bool UsesSkylineDaily => SkylineSettings.Type == SkylineType.SkylineDaily;

        public bool UsesCustomSkylinePath => SkylineSettings.Type == SkylineType.Custom;

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

        public static AutoQcConfig ReadXml(XmlReader reader)
        {
            var name = reader.GetAttribute(Attr.name);
            
            var isEnabled = reader.GetBoolAttribute(Attr.is_enabled);
            DateTime dateTime;
            DateTime.TryParse(reader.GetAttribute(Attr.created), out dateTime);
            var created = dateTime;
            DateTime.TryParse(reader.GetAttribute(Attr.modified), out dateTime);
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

                    if (reader.Name.Equals("autoqc_config")) // handles old configurations without skyline settings
                    {
                        skylineSettings = new SkylineSettings(SkylineType.Skyline);
                        break;
                    }
                } while (reader.NodeType != XmlNodeType.Element);
                skylineSettings = skylineSettings ?? SkylineSettings.ReadXml(reader);
            }
            catch (ArgumentException e)
            {
                exceptionMessage = string.Format("\"{0}\" ({1})", name, e.Message);
            }

            // finish reading config before exception is thrown so following configs aren't messed up
            while (!(reader.Name == "autoqc_config" && reader.NodeType == XmlNodeType.EndElement))
            {
                reader.Read();
            } 

            if (exceptionMessage != null)
                throw new ArgumentException(exceptionMessage);

            return new AutoQcConfig(name, isEnabled, created, modified, mainSettings, panoramaSettings, skylineSettings);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("autoqc_config");
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
            if (string.IsNullOrEmpty(Name))
            {
                throw new ArgumentException("Please enter a name for the configuration.");
            }

            MainSettings.ValidateSettings();
            SkylineSettings.Validate();
            PanoramaSettings.ValidateSettings();

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
