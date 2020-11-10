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
    public class SkylineBatchConfig : IXmlSerializable
    {
        public string Name { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }

        public MainSettings MainSettings { get; set; }

        public ReportSettings ReportSettings { get; set; }


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


        public void ReadXml(XmlReader reader)
        {
            Name = reader.GetAttribute(Attr.Name);
            DateTime dateTime;
            DateTime.TryParse(reader.GetAttribute(Attr.Created), out dateTime);
            Created = dateTime;
            DateTime.TryParse(reader.GetAttribute(Attr.Modified), out dateTime);
            Modified = dateTime;

            do
            {
                reader.Read();
            } while (reader.NodeType != XmlNodeType.Element);

            var mainSettings = new MainSettings();
            mainSettings.ReadXml(reader);
            MainSettings = mainSettings;
            do
            {
                reader.Read();
            } while (reader.NodeType != XmlNodeType.Element);
            var reportSettings = new ReportSettings();
            reportSettings.ReadXml(reader);
            ReportSettings = reportSettings;

            do
            {
                reader.Read();
            } while (reader.NodeType != XmlNodeType.EndElement);
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

        public static SkylineBatchConfig Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new SkylineBatchConfig());
        }



        #endregion


        #region Producers

        public static SkylineBatchConfig GetDefault()
        {
            var config = new SkylineBatchConfig
            {
                MainSettings = MainSettings.GetDefault(),
                ReportSettings = new ReportSettings()
            };
            return config;
        }

        public SkylineBatchConfig MakeChild()
        {
            var childConfig = this.Copy();
            childConfig.Name = "";
            childConfig.Created = DateTime.Now;
            childConfig.Modified = DateTime.Now;
            childConfig.MainSettings = MainSettings.MakeChild();
            childConfig.ReportSettings = new ReportSettings();
            return childConfig;
        }

        public SkylineBatchConfig Copy()
        {
            return new SkylineBatchConfig
            {
                Name = Name,
                Created = Created,
                Modified = Modified,
                MainSettings = MainSettings.Clone(),
                ReportSettings = ReportSettings.Copy()
            };
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

            MainSettings.ValidateSettings();
            ReportSettings.ValidateSettings();
           
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
            // TODO: make config variables readonly so they can be used for a more meaningful hash function without warnings
            return "Configuration".GetHashCode();
        }

        #endregion
    }
}
