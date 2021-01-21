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


        public FileSettings(string msOneResolvingPower, string msMsResolvingPower, string retentionTime, bool addDecoys, bool shuffleDecoys)
        {
            MsOneResolvingPower = msOneResolvingPower ?? "";
            MsMsResolvingPower = msMsResolvingPower ?? "";
            RetentionTime = retentionTime ?? "";
            AddDecoys = addDecoys;
            ShuffleDecoys = shuffleDecoys;
        }

        public readonly string MsOneResolvingPower;
        public readonly string MsMsResolvingPower;
        public readonly string RetentionTime;
        public readonly bool AddDecoys;
        public readonly bool ShuffleDecoys;

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
            sb.Append("MS1 filtering res/accuracy: ").AppendLine(MsOneResolvingPower);
            sb.Append("Ms/Ms filtering res/accuracy: ").AppendLine(MsMsResolvingPower);
            sb.Append("Retention time: ").AppendLine(RetentionTime);
            return sb.ToString();
        }

        public void Validate()
        {
            ValidateIntTextField(MsOneResolvingPower, Resources.FileSettings_Resolving_Power);
            ValidateIntTextField(MsMsResolvingPower, Resources.FileSettings_Resolving_Power);
            ValidateIntTextField(RetentionTime, Resources.FileSettings_Retention_Time);
            
            // CONSIDER: adding validation that checks if numbers are within a certain range
        }






        #region Read/Write XML

        private enum Attr
        {
            MsOneResolvingPower,
            MsMsResolvingPower,
            RetentionTime,
            AddDecoys,
            ShuffleDecoys
        };

        public static FileSettings ReadXml(XmlReader reader)
        {
            var msOneResolvingPower = reader.GetAttribute(Attr.MsOneResolvingPower);
            var msMsResolvingPower = reader.GetAttribute(Attr.MsMsResolvingPower);
            var retentionTime = reader.GetAttribute(Attr.RetentionTime);
            var addDecoys = reader.GetBoolAttribute(Attr.AddDecoys);
            var shuffleDecoys = reader.GetBoolAttribute(Attr.ShuffleDecoys);
            return new FileSettings(msOneResolvingPower, msMsResolvingPower, retentionTime, addDecoys, shuffleDecoys);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("file_settings");
            writer.WriteAttributeIfString(Attr.MsOneResolvingPower, MsOneResolvingPower);
            writer.WriteAttributeIfString(Attr.MsMsResolvingPower, MsMsResolvingPower);
            writer.WriteAttributeIfString(Attr.RetentionTime, RetentionTime);
            writer.WriteAttribute(Attr.AddDecoys, AddDecoys);
            writer.WriteAttribute(Attr.ShuffleDecoys, ShuffleDecoys);
            writer.WriteEndElement();
        }
        #endregion

        protected bool Equals(FileSettings other)
        {

            return other.MsOneResolvingPower == MsOneResolvingPower &&
                   other.MsMsResolvingPower == MsMsResolvingPower &&
                   other.RetentionTime == RetentionTime &&
                   other.AddDecoys == AddDecoys &&
                   other.ShuffleDecoys == ShuffleDecoys;
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
                   MsOneResolvingPower.GetHashCode() +
                   MsMsResolvingPower.GetHashCode();
        }
    }
}