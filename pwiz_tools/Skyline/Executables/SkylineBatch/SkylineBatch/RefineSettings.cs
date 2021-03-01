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
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    [XmlRoot("refine_settings")]
    public class RefineSettings
    {

        // IMMUTABLE - all fields are readonly strings
        // Holds information for refining the skyline file after data import

        public RefineSettings(string cvCutoff, string normalizeMethod, string qValueCutoff,
            string minDetectedReplicates)
        {
            CvCutoff = cvCutoff ?? string.Empty;
            NormalizeMethod = normalizeMethod ?? string.Empty;
            QValueCutoff = qValueCutoff ?? string.Empty;
            MinDetectedReplicates = minDetectedReplicates ?? string.Empty;
        }

        public readonly string CvCutoff;

        public readonly string NormalizeMethod;

        public readonly string QValueCutoff;

        public readonly string MinDetectedReplicates;

        public void Validate()
        {
            ValidateNumberInput(CvCutoff, Resources.RefineSettings_Validate_CV_cutoff, 0, 100);
            ValidateNumberInput(QValueCutoff, Resources.RefineSettings_Validate_Q_value_cutoff, 0, 1);
            ValidateNumberInput(MinDetectedReplicates, Resources.RefineSettings_Validate_minimum_detections_allowed, 0, 100);
        }

        private void ValidateNumberInput(string numberAsString, string variableName, int min, int max)
        {
            if (string.IsNullOrWhiteSpace(numberAsString)) return; // optional input
            var isNumber = Double.TryParse(numberAsString, out Double number);
            if (!isNumber)
                throw new ArgumentException(string.Format(Resources.RefineSettings_ValidateNumberInput_Invalid_value_for_the__0____1_, variableName, numberAsString) + Environment.NewLine +
                                            Resources.RefineSettings_ValidateNumberInput_Please_enter_a_number_);
            if (number > max || number < min)
                throw new ArgumentException(string.Format(Resources.RefineSettings_ValidateNumberInput__0__is_out_of_range_for_the__1__, number, variableName) + Environment.NewLine +
                                            string.Format(Resources.RefineSettings_ValidateNumberInput_Please_enter_a_number_between__0__and__1_, min, max));
        }

        
        #region Read/Write XML

        private enum Attr
        {
            CvCutoff,
            NormalizeMethod,
            QValueCutoff,
            MinDetectedReplicates
        };

        public static RefineSettings ReadXml(XmlReader reader)
        {
            var cvCutoff = reader.GetAttribute(Attr.CvCutoff);
            var normalizeMethod = reader.GetAttribute(Attr.NormalizeMethod);
            var qValueCutoff = reader.GetAttribute(Attr.QValueCutoff);
            var minDetectedReplicates = reader.GetAttribute(Attr.MinDetectedReplicates);
            return new RefineSettings(cvCutoff, normalizeMethod, qValueCutoff, minDetectedReplicates);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("refine_settings");
            writer.WriteAttributeIfString(Attr.CvCutoff, CvCutoff);
            writer.WriteAttributeIfString(Attr.NormalizeMethod, NormalizeMethod);
            writer.WriteAttributeIfString(Attr.QValueCutoff, QValueCutoff);
            writer.WriteAttributeIfString(Attr.MinDetectedReplicates, MinDetectedReplicates);
            writer.WriteEndElement();
        }
        #endregion

        #region Batch Commands

        public const string CV_CUTOFF_COMMAND = "--refine-cv-remove-above-cutoff={0}";
        public const string CV_GLOBAL_NORMALIZE_COMMAND = "--refine-cv-global-normalize={0}";

        // TODO (Ali): ask if this is feasible to include. How to get reference types? User input?
        public const string CV_REFERENCE_NORMALIZE_COMMAND = "--refine-cv-reference-normalize={0}";
        public const string Q_VALUE_CUTOFF_COMMAND = "--refine-qvalue-cutoff={0}";
        public const string MINIMUM_DETECTIONS_COMMAND = "--refine-minimum-detections={0}";
        public const string REMOVE_DECOYS_COMMAND = "--decoys-discard";
        public const string REMOVE_RESULTS_COMMAND = "--remove-all";

        public void WriteCvCutoffCommand(CommandWriter commandWriter)
        {
            if (!string.IsNullOrEmpty(CvCutoff))
                commandWriter.Write(CV_CUTOFF_COMMAND, CvCutoff);
        }

        public void WriteNormalizeCommand(CommandWriter commandWriter)
        {   
            // TODO (Ali): update this if no reference normalize
            if (string.IsNullOrEmpty(NormalizeMethod) || NormalizeMethod.Equals("None"))
                return;
            var formattedInput = NormalizeMethod.ToLower().Replace(' ', '_');
            if (new List<string> {"equalize_medians", "global_standards"}.Contains(formattedInput))
                commandWriter.Write(CV_GLOBAL_NORMALIZE_COMMAND, formattedInput);
            else
                commandWriter.Write(CV_REFERENCE_NORMALIZE_COMMAND, formattedInput);
        }

        public void WriteQValueCutoffCommand(CommandWriter commandWriter)
        {
            if (!string.IsNullOrEmpty(QValueCutoff))
                commandWriter.Write(Q_VALUE_CUTOFF_COMMAND, QValueCutoff);
        }

        public void WriteMinimumDetectionsCommand(CommandWriter commandWriter)
        {
            if (!string.IsNullOrEmpty(MinDetectedReplicates))
                commandWriter.Write(MINIMUM_DETECTIONS_COMMAND, MinDetectedReplicates);
        }

        #endregion

        private bool EmptyOrEqual(string one, string two)
        {
            if (string.IsNullOrWhiteSpace(one) && string.IsNullOrWhiteSpace(two))
                return true;
            if (one == null) return false;
            return one.Equals(two);
        }

        protected bool Equals(RefineSettings other)
        {
            return (EmptyOrEqual(other.CvCutoff, CvCutoff) &&
                    other.NormalizeMethod.Equals(NormalizeMethod) &&
                    EmptyOrEqual(other.QValueCutoff, QValueCutoff) &&
                    EmptyOrEqual(other.MinDetectedReplicates, MinDetectedReplicates));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RefineSettings)obj);
        }

        public override int GetHashCode()
        {
            return CvCutoff.GetHashCode() +
                   NormalizeMethod.GetHashCode() +
                   QValueCutoff.GetHashCode() +
                   MinDetectedReplicates.GetHashCode();
        }
    }
}