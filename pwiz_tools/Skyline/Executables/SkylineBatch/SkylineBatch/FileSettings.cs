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
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    [XmlRoot("file_settings")]
    public class FileSettings
    {

        // IMMUTABLE - all fields are readonly literals
        // Describes file modifications user would like to do on the .sky file in the analysis folder

        public static FileSettings FromUi(string msOneResolvingPowerString, string msMsResolvingPowerString, string retentionTimeString, bool addDecoys, bool shuffleDecoys, bool trainMProphet)
        {
            var msOneResolvingPower = TextUtil.GetNullableIntFromUiString(msOneResolvingPowerString,
                Resources.FileSettings_Validate_MS1_filtering_res_accuracy);
            var msMsResolvingPower = TextUtil.GetNullableIntFromUiString(msMsResolvingPowerString,
                Resources.FileSettings_Validate_Ms_Ms_filtering_res_accuracy);
            var retentionTime = TextUtil.GetNullableIntFromUiString(retentionTimeString,
                Resources.FileSettings_Validate_retention_time_filtering);
            return new FileSettings(msOneResolvingPower, msMsResolvingPower, retentionTime, addDecoys, shuffleDecoys, trainMProphet);
        }


        public FileSettings(int? msOneResolvingPower, int? msMsResolvingPower, int? retentionTime, bool addDecoys, bool shuffleDecoys, bool trainMProphet)
        {
            MsOneResolvingPower = msOneResolvingPower;
            MsMsResolvingPower = msMsResolvingPower;
            RetentionTime = retentionTime;
            AddDecoys = addDecoys;
            ShuffleDecoys = shuffleDecoys;
            TrainMProphet = trainMProphet;
        }

        public readonly int? MsOneResolvingPower;
        public readonly int? MsMsResolvingPower;
        public readonly int? RetentionTime;
        public readonly bool AddDecoys;
        public readonly bool ShuffleDecoys;
        public readonly bool TrainMProphet;

        private void ValidateNonNegative(int? optionalInteger, string fieldName)
        {
            if (optionalInteger == null) return;
            if (optionalInteger < 0)
                throw new ArgumentException(string.Format(Resources.FileSettings_ValidateNonNegative_The__0__cannot_be_less_than_zero_, fieldName) + Environment.NewLine +
                                            Resources.FileSettings_ValidateNonNegative_Please_enter_a_positive_integer_);
        
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("MS1 filtering res/accuracy: ").AppendLine(MsOneResolvingPower.ToString());
            sb.Append("Ms/Ms filtering res/accuracy: ").AppendLine(MsMsResolvingPower.ToString());
            sb.Append("Retention time: ").AppendLine(RetentionTime.ToString());
            return sb.ToString();
        }

        public void Validate()
        {
            ValidateNonNegative(MsOneResolvingPower, Resources.FileSettings_Validate_MS1_filtering_res_accuracy);
            ValidateNonNegative(MsMsResolvingPower, Resources.FileSettings_Validate_Ms_Ms_filtering_res_accuracy);
            ValidateNonNegative(RetentionTime, Resources.FileSettings_Validate_retention_time_filtering);
            // CONSIDER: adding more validation that checks if numbers are within a certain range
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
            var msOneResolvingPower = TextUtil.GetNullableIntFromInvariantString(reader.GetAttribute(Attr.MsOneResolvingPower));
            var msMsResolvingPower = TextUtil.GetNullableIntFromInvariantString(reader.GetAttribute(Attr.MsMsResolvingPower));
            var retentionTime = TextUtil.GetNullableIntFromInvariantString(reader.GetAttribute(Attr.RetentionTime));
            var addDecoys = reader.GetBoolAttribute(Attr.AddDecoys);
            var shuffleDecoys = reader.GetBoolAttribute(Attr.ShuffleDecoys);
            var trainMProphet = reader.GetBoolAttribute(Attr.TrainMProphet);
            return new FileSettings(msOneResolvingPower, msMsResolvingPower, retentionTime, addDecoys, shuffleDecoys, trainMProphet);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("file_settings");
            writer.WriteAttributeIfString(Attr.MsOneResolvingPower,
                TextUtil.ToInvariantCultureString(MsOneResolvingPower));
            writer.WriteAttributeIfString(Attr.MsMsResolvingPower, 
                TextUtil.ToInvariantCultureString(MsMsResolvingPower));
            writer.WriteAttributeIfString(Attr.RetentionTime, 
                TextUtil.ToInvariantCultureString(RetentionTime));
            writer.WriteAttribute(Attr.AddDecoys, AddDecoys);
            writer.WriteAttribute(Attr.ShuffleDecoys, ShuffleDecoys);
            writer.WriteAttribute(Attr.TrainMProphet, TrainMProphet);
            writer.WriteEndElement();
        }
        #endregion

        #region Batch Commands

        public const string MS_ONE_RESOLVING_POWER_COMMAND = "--full-scan-precursor-res={0}";
        public const string MSMS_RESOLVING_POWER_COMMAND = "--full-scan-product-res={0}";
        public const string RETENTION_TIME_COMMAND = "--full-scan-rt-filter-tolerance={0}";
        public const string ADD_DECOYS_COMMAND = "--decoys-add={0}";
        public const string TRAIN_MPROPHET_COMMAND =
            "--reintegrate-model-name=\"{0}\" --reintegrate-create-model --reintegrate-overwrite-peaks";

        public void WriteMsOneCommand(CommandWriter commandWriter)
        {
            if (MsOneResolvingPower != null)
                commandWriter.Write(MS_ONE_RESOLVING_POWER_COMMAND, TextUtil.ToUiString((int)MsOneResolvingPower));
        }

        public void WriteMsMsCommand(CommandWriter commandWriter)
        {
            if (MsMsResolvingPower != null)
                commandWriter.Write(MSMS_RESOLVING_POWER_COMMAND, TextUtil.ToUiString((int)MsMsResolvingPower));
        }

        public void WriteRetentionTimeCommand(CommandWriter commandWriter)
        {
            if (RetentionTime != null)
                commandWriter.Write(RETENTION_TIME_COMMAND, TextUtil.ToUiString((int)RetentionTime));
        }

        public void WriteAddDecoysCommand(CommandWriter commandWriter)
        {
            if (AddDecoys)
                commandWriter.Write(ADD_DECOYS_COMMAND, ShuffleDecoys ? "shuffle" : "reverse");
        }

        public void WriteTrainMProphetCommand(CommandWriter commandWriter, string modelName)
        {
            if (TrainMProphet)
                commandWriter.Write(TRAIN_MPROPHET_COMMAND, modelName);
        }

        #endregion

        protected bool Equals(FileSettings other)
        {
            // shuffle decoys only matters if add decoys is true
            if (other.AddDecoys && AddDecoys && other.ShuffleDecoys != ShuffleDecoys)
                return false;
            return other.MsOneResolvingPower.Equals(MsOneResolvingPower) &&
                   other.MsMsResolvingPower.Equals(MsMsResolvingPower) &&
                   other.RetentionTime.Equals(RetentionTime) &&
                   other.AddDecoys == AddDecoys;
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