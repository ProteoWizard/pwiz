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
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    [XmlRoot("file_settings")]
    public class FileSettings
    {

        // IMMUTABLE - all fields are readonly strings
        // Holds file locations and naming pattern to use when running the configuration


        public FileSettings(string resolvingPower, string retentionTime)
        {
            ResolvingPower = resolvingPower ?? "";
            RetentionTime = retentionTime ?? "";
        }

        public readonly string ResolvingPower;

        public readonly string RetentionTime;
        

        private int ValidateIntTextField(string textToParse, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(textToParse)) return 0;
            int parsedInt;
            if (!Int32.TryParse(textToParse, out parsedInt))
            {
                throw new ArgumentException(string.Format(
                    Resources.FileSettings_ValidateIntTextField_Invalid_value_for__0___1__, fieldName,
                    textToParse));
            }
            return parsedInt;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Resolving power: ").AppendLine(ResolvingPower);
            sb.Append("Retention time: ").AppendLine(RetentionTime);
            return sb.ToString();
        }

        public void Validate()
        {
            /*var resolvingPowerInt = */ValidateIntTextField(ResolvingPower, Resources.FileSettings_Resolving_Power);
            /*var retentionTimeInt = */ValidateIntTextField(RetentionTime, Resources.FileSettings_Retention_Time);
            
            // TODO: Add validation
        }






        #region Read/Write XML

        private enum Attr
        {
            ResolvingPower,
            RetentionTime
        };

        public static FileSettings ReadXml(XmlReader reader)
        {
            var resolvingPower = reader.GetAttribute(Attr.ResolvingPower);
            var retentionTime = reader.GetAttribute(Attr.RetentionTime);
            return new FileSettings(resolvingPower, retentionTime);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("file_settings");
            writer.WriteAttributeIfString(Attr.ResolvingPower, ResolvingPower);
            writer.WriteAttributeIfString(Attr.RetentionTime, RetentionTime);
            writer.WriteEndElement();
        }
        #endregion

        protected bool Equals(FileSettings other)
        {

            return other.ResolvingPower == ResolvingPower && other.RetentionTime == RetentionTime;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FileSettings)obj);
        }

        public override int GetHashCode()
        {
            return RetentionTime.GetHashCode() +
                   ResolvingPower.GetHashCode();
        }
    }
}