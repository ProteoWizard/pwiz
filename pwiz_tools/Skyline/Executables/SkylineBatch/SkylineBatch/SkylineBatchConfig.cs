/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2020 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using SkylineBatch.Properties;
using SharedAutoQcBatch;

namespace SkylineBatch
{
    [XmlRoot("skylinebatch_config")]
    public class SkylineBatchConfig : IConfig
    {
        // IMMUTABLE - all fields are readonly, all variables are immutable
        // A configuration is a set of information about a skyline file, data, reports and scripts.
        // To be a valid configuration, it must contain enough of this information to run a batch 
        // script that will copy the skyline file, import data, export reports, and run r scripts.

        
        public SkylineBatchConfig(string name, bool enabled, DateTime modified, MainSettings mainSettings, 
            FileSettings fileSettings, ReportSettings reportSettings, SkylineSettings skylineSettings)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(string.Format(Resources.SkylineBatchConfig_SkylineBatchConfig___0___is_not_a_valid_name_for_the_configuration_, name) + Environment.NewLine +
                                            Resources.SkylineBatchConfig_SkylineBatchConfig_Please_enter_a_name_);
            }
            Name = name;
            Enabled = enabled;
            Modified = modified;
            MainSettings = mainSettings;
            FileSettings = fileSettings;
            ReportSettings = reportSettings;
            SkylineSettings = skylineSettings;
        }

        public readonly string Name;

        public readonly DateTime Modified;

        public readonly MainSettings MainSettings;

        public readonly FileSettings FileSettings;

        public readonly ReportSettings ReportSettings;

        public readonly SkylineSettings SkylineSettings;

        public bool Enabled;

        public bool UsesSkyline => SkylineSettings.Type == SkylineType.Skyline;

        public bool UsesSkylineDaily => SkylineSettings.Type == SkylineType.SkylineDaily;

        public bool UsesCustomSkylinePath => SkylineSettings.Type == SkylineType.Custom;

        public string GetName() { return Name; }

        public DateTime GetModified()  { return Modified; }

        public CreateRunner GetRunnerCreator() { return ConfigRunner.StaticConstructor; }

        public ListViewItem AsListViewItem(IConfigRunner runner)
        {
            var lvi = new ListViewItem(Name);
            lvi.Checked = Enabled;
            lvi.SubItems.Add(Modified.ToShortDateString());
            lvi.SubItems.Add(((ConfigRunner)runner).GetDisplayStatus());//_configRunners[config.Name].GetDisplayStatus());
            return lvi;
        }

        public IConfigRunner CreateRunner(Logger logger, IMainUiControl uiControl)
        {
            return new ConfigRunner(this, logger, uiControl);
        }

        private enum Attr
        {
            Name,
            Enabled,
            Modified
        }
        
        #region XML

        public XmlSchema GetSchema()
        {
            return null;
        }

        public static SkylineBatchConfig ReadXml(XmlReader reader)
        {
            var name = reader.GetAttribute(Attr.Name);
            var enabled = reader.GetBoolAttribute(Attr.Enabled);
            DateTime modified;
            DateTime.TryParse(reader.GetAttribute(Attr.Modified), out modified);

            do
            {
                reader.Read();
            } while (reader.NodeType != XmlNodeType.Element);

            MainSettings mainSettings = null;
            FileSettings fileSettings = null;
            ReportSettings reportSettings = null;
            SkylineSettings skylineSettings = null;
            string exceptionMessage = null;
            try
            {
                mainSettings = MainSettings.ReadXml(reader);
                do
                {
                    reader.Read();
                } while (reader.NodeType != XmlNodeType.Element);

                fileSettings = FileSettings.ReadXml(reader);
                do
                {
                    reader.Read();
                } while (reader.NodeType != XmlNodeType.Element);

                reportSettings = ReportSettings.ReadXml(reader);
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
            
            do
            {
                reader.Read();
            } while (!(reader.Name == "skylinebatch_config" && reader.NodeType == XmlNodeType.EndElement));

            if (exceptionMessage != null)
                throw new ArgumentException(exceptionMessage);

            return new SkylineBatchConfig(name, enabled, modified, mainSettings, fileSettings, reportSettings, skylineSettings);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("skylinebatch_config");
            writer.WriteAttribute(Attr.Name, Name);
            writer.WriteAttribute(Attr.Enabled, Enabled);
            writer.WriteAttributeIfString(Attr.Modified, Modified.ToShortDateString() + " " + Modified.ToShortTimeString());
            MainSettings.WriteXml(writer);
            FileSettings.WriteXml(writer);
            ReportSettings.WriteXml(writer);
            SkylineSettings.WriteXml(writer);
            writer.WriteEndElement();
        }
        
        #endregion
        
        public void Validate()
        {
            MainSettings.Validate();
            FileSettings.Validate();
            ReportSettings.Validate();
            SkylineSettings.Validate();
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out IConfig replacedPathConfig)
        {
            var mainSettingsReplaced = MainSettings.TryPathReplace(oldRoot, newRoot, out MainSettings pathReplacedMainSettings);
            var reportSettingsReplaced =
                ReportSettings.TryPathReplace(oldRoot, newRoot, out ReportSettings pathReplacedReportSettings);
            replacedPathConfig = new SkylineBatchConfig(Name, Enabled, DateTime.Now, pathReplacedMainSettings, FileSettings, pathReplacedReportSettings, SkylineSettings);
            return mainSettingsReplaced || reportSettingsReplaced;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Name: ").AppendLine(Name);
            sb.Append("Enabled: ").Append(Enabled);
            sb.Append("Modified: ").Append(Modified.ToShortDateString()).AppendLine(Modified.ToShortTimeString());
            sb.AppendLine(string.Empty).AppendLine("Main Settings");
            sb.Append(MainSettings);
            return sb.ToString();
        }

        #region Equality members

        protected bool Equals(SkylineBatchConfig other)
        {
            return string.Equals(Name, other.Name)
                   && Equals(MainSettings, other.MainSettings)
                  && Equals(ReportSettings, other.ReportSettings);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SkylineBatchConfig) obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() + Modified.GetHashCode() +
                   MainSettings.GetHashCode() + ReportSettings.GetHashCode();
        }

        #endregion
    }
}
