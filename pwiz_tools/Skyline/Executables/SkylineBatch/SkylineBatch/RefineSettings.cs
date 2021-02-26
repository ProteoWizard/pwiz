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
using System.IO;
using System.Text;
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
            string numDetectedReplicates)
        {
            CvCutoff = cvCutoff ?? string.Empty;
            NormalizeMethod = normalizeMethod ?? string.Empty;
            QValueCutoff = qValueCutoff ?? string.Empty;
            NumDetectedReplicates = numDetectedReplicates ?? string.Empty;
        }

        public readonly string CvCutoff;

        public readonly string NormalizeMethod;

        public readonly string QValueCutoff;

        public readonly string NumDetectedReplicates;

        public void Validate()
        {
            ValidateNumberInput(CvCutoff, "CV cutoff", 0, 100);
            ValidateNumberInput(QValueCutoff, "Q value cutoff", 0, 1);
            ValidateNumberInput(NumDetectedReplicates, "minimum detections allowed", 0, 100);
        }

        private void ValidateNumberInput(string numberAsString, string variableName, int min, int max)
        {
            if (string.IsNullOrWhiteSpace(numberAsString)) return; // optional input
            var isNumber = Double.TryParse(numberAsString, out Double number);
            if (!isNumber)
                throw new ArgumentException(string.Format("Invalid value for {0}: {1}", variableName, numberAsString) + Environment.NewLine +
                                            "Please enter a number.");
            if (number > max || number < min)
                throw new ArgumentException(string.Format("{0} is out of range for the {1}.", number, variableName) + Environment.NewLine +
                                            string.Format("Please enter a number between {0} and {1}", min, max));
        }

        
        #region Read/Write XML

        private enum Attr
        {
            CvCutoff,
            NormalizeMethod,
            QValueCutoff,
            NumDetectedReplicates
        };

        public static RefineSettings ReadXml(XmlReader reader)
        {
            var cvCutoff = reader.GetAttribute(Attr.CvCutoff);
            var normalizeMethod = reader.GetAttribute(Attr.NormalizeMethod);
            var qValueCutoff = reader.GetAttribute(Attr.QValueCutoff);
            var numDetectedReplicates = reader.GetAttribute(Attr.NumDetectedReplicates);
            return new RefineSettings(cvCutoff, normalizeMethod, qValueCutoff, numDetectedReplicates);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("refine_settings");
            writer.WriteAttributeIfString(Attr.CvCutoff, CvCutoff);
            writer.WriteAttributeIfString(Attr.NormalizeMethod, NormalizeMethod);
            writer.WriteAttributeIfString(Attr.QValueCutoff, QValueCutoff);
            writer.WriteAttributeIfString(Attr.NumDetectedReplicates, NumDetectedReplicates);
            writer.WriteEndElement();
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
                    EmptyOrEqual(other.NumDetectedReplicates, NumDetectedReplicates));
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
                   NumDetectedReplicates.GetHashCode();
        }
    }
}