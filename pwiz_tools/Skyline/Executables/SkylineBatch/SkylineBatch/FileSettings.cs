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
using System.Xml.Serialization;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    [XmlRoot("file_settings")]
    public class FileSettings
    {

        // IMMUTABLE - all fields are readonly strings
        // Holds file locations and naming pattern to use when running the configuration
        
        public FileSettings(string msOneResolvingPower, string msMsResolvingPower, string retentionTime, bool addDecoys, bool shuffleDecoys, bool trainMProphet)
        {
            MsOneResolvingPower = msOneResolvingPower ?? string.Empty;
            MsMsResolvingPower = msMsResolvingPower ?? string.Empty;
            RetentionTime = retentionTime ?? string.Empty;
            AddDecoys = addDecoys;
            ShuffleDecoys = shuffleDecoys;
            TrainMProphet = trainMProphet;
        }

        public readonly string MsOneResolvingPower;
        public readonly string MsMsResolvingPower;
        public readonly string RetentionTime;
        public readonly bool AddDecoys;
        public readonly bool ShuffleDecoys;
        public readonly bool TrainMProphet;

        private int ValidateIntTextField(string textToParse, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(textToParse)) return 0;
            int parsedInt;
            if (!Int32.TryParse(textToParse, out parsedInt))
            {
                throw new ArgumentException(string.Format(Resources.FileSettings_ValidateIntTextField__0__is_not_a_valid_value_for__1__, fieldName,
                    textToParse) + Environment.NewLine +
                                            Resources.FileSettings_ValidateIntTextField_Please_enter_a_number_);
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
            ValidateIntTextField(MsOneResolvingPower, Resources.FileSettings_Validate_MS1_filtering_res_accuracy);
            ValidateIntTextField(MsMsResolvingPower, Resources.FileSettings_Validate_Ms_Ms_filtering_res_accuracy);
            ValidateIntTextField(RetentionTime, Resources.FileSettings_Validate_retention_time_filtering);
            // CONSIDER: adding validation that checks if numbers are within a certain range
        }
        
        #region Read/Write XML

        private enum Attr
        {
            MsOneResolvingPower,
            MsMsResolvingPower,
            RetentionTime,
            AddDecoys,
            ShuffleDecoys,
            TrainMProphet
        };

        public static FileSettings ReadXml(XmlReader reader)
        {
            var msOneResolvingPower = reader.GetAttribute(Attr.MsOneResolvingPower);
            var msMsResolvingPower = reader.GetAttribute(Attr.MsMsResolvingPower);
            var retentionTime = reader.GetAttribute(Attr.RetentionTime);
            var addDecoys = reader.GetBoolAttribute(Attr.AddDecoys);
            var shuffleDecoys = reader.GetBoolAttribute(Attr.ShuffleDecoys);
            var trainMProphet = reader.GetBoolAttribute(Attr.TrainMProphet);
            return new FileSettings(msOneResolvingPower, msMsResolvingPower, retentionTime, addDecoys, shuffleDecoys, trainMProphet);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("file_settings");
            writer.WriteAttributeIfString(Attr.MsOneResolvingPower, MsOneResolvingPower);
            writer.WriteAttributeIfString(Attr.MsMsResolvingPower, MsMsResolvingPower);
            writer.WriteAttributeIfString(Attr.RetentionTime, RetentionTime);
            writer.WriteAttribute(Attr.AddDecoys, AddDecoys);
            writer.WriteAttribute(Attr.ShuffleDecoys, ShuffleDecoys);
            writer.WriteAttribute(Attr.TrainMProphet, TrainMProphet);
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