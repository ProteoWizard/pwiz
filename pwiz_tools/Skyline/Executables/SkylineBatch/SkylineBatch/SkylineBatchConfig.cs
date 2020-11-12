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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SkylineBatch
{
    [XmlRoot("skylinebatch_config")]
    public class SkylineBatchConfig
    {
        // IMMUTABLE - all fields are readonly, all variables are immutable
        // A configuration is a set of information about a skyline file, data, reports and scripts.
        // To be a valid configuration, it must contain enough of this information to run a batch 
        // script that will copy the skyline file, import data, export reports, and run r scripts.

        
        public SkylineBatchConfig(string name, DateTime created, DateTime modified, MainSettings mainSettings, ReportSettings reportSettings)
        {
            Name = name;
            Created = created;
            Modified = modified;
            MainSettings = mainSettings;
            ReportSettings = reportSettings;
            Validate();
        }

        public readonly string Name;

        public readonly DateTime Created;

        public readonly DateTime Modified;

        public readonly MainSettings MainSettings;

        public readonly ReportSettings ReportSettings;

        private enum Attr
        {
            Name,
            Created,
            Modified
        };


        #region XML

        public XmlSchema GetSchema()
        {
            return null;
        }

        public static SkylineBatchConfig ReadXml(XmlReader reader)
        {
            var name = reader.GetAttribute(Attr.Name);
            DateTime created;
            DateTime modified;
            DateTime.TryParse(reader.GetAttribute(Attr.Created), out created);
            DateTime.TryParse(reader.GetAttribute(Attr.Modified), out modified);

            do
            {
                reader.Read();
            } while (reader.NodeType != XmlNodeType.Element);


            var mainSettings = MainSettings.ReadXml(reader);
            do
            {
                reader.Read();
            } while (reader.NodeType != XmlNodeType.Element);
            var reportSettings = ReportSettings.ReadXml(reader);

            do
            {
                reader.Read();
            } while (reader.NodeType != XmlNodeType.EndElement);

            return new SkylineBatchConfig(name, created, modified, mainSettings, reportSettings);
        }

        public void WriteXml(XmlWriter writer)
        {
            Validate();
            writer.WriteStartElement("skylinebatch_config");
            writer.WriteAttribute(Attr.Name, Name);
            writer.WriteAttributeIfString(Attr.Created, Created.ToShortDateString() + " " + Created.ToShortTimeString());
            writer.WriteAttributeIfString(Attr.Modified, Modified.ToShortDateString() + " " + Modified.ToShortTimeString());
            MainSettings.WriteXml(writer);
            ReportSettings.WriteXml(writer);
            writer.WriteEndElement();
        }



        #endregion

        


        public void Validate()
        {
            if (MainSettings == null || ReportSettings == null)
            {
                throw new Exception("Configuration settings not initialized.");
            }

            if (string.IsNullOrEmpty(Name))
            {
                throw new ArgumentException("Please enter a name for the configuration.");
            }

            MainSettings.Validate();
            ReportSettings.Validate();
           
        }

        public override string ToString()
        {
            Validate();
            var sb = new StringBuilder();
            sb.Append("Name: ").AppendLine(Name);
            sb.Append("Created: ").Append(Created.ToShortDateString()).AppendLine(Created.ToShortTimeString());
            sb.Append("Modified: ").Append(Modified.ToShortDateString()).AppendLine(Modified.ToShortTimeString());
            sb.AppendLine("").AppendLine("Main Settings");
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
            return Name.GetHashCode() + Created.GetHashCode() + Modified.GetHashCode() +
                   MainSettings.GetHashCode() + ReportSettings.GetHashCode();
        }

        #endregion
    }
}
