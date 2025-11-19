/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("imputation")]
    public class ImputationSettings : Immutable, IXmlSerializable
    {
        public static readonly ImputationSettings DEFAULT = new ImputationSettings();

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public double? MaxRtShift { get; private set; }

        public ImputationSettings ChangeMaxRtShift(double? value)
        {
            return ChangeProp(ImClone(this), im => im.MaxRtShift = value);
        }

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public double? MaxPeakWidthVariation { get; private set; }

        public ImputationSettings ChangeMaxPeakWidthVariation(double? value)
        {
            return ChangeProp(ImClone(this), im => im.MaxPeakWidthVariation = value);
        }

        [Track(defaultValues:typeof(DefaultValuesFalse))]
        public bool ImputeMissingPeaks { get; private set; }

        public ImputationSettings ChangeImputeMissing(bool value)
        {
            return ChangeProp(ImClone(this), im => im.ImputeMissingPeaks = value);
        }

        [Track(defaultValues:typeof(DefaultValuesNull))]
        public AlignmentTargetSpec AlignmentTarget { get; private set; }

        public ImputationSettings ChangeAlignmentTarget(AlignmentTargetSpec value)
        {
            return ChangeProp(ImClone(this),
                im => im.AlignmentTarget = Equals(value, AlignmentTargetSpec.Default) ? null : value);
        }

        public bool HasImputation
        {
            get
            {
                return ImputeMissingPeaks || MaxPeakWidthVariation.HasValue || MaxRtShift.HasValue;
            }
        }

        protected bool Equals(ImputationSettings other)
        {
            return Nullable.Equals(MaxRtShift, other.MaxRtShift) &&
                   Nullable.Equals(MaxPeakWidthVariation, other.MaxPeakWidthVariation) &&
                   Equals(ImputeMissingPeaks, other.ImputeMissingPeaks) &&
                   Equals(AlignmentTarget, other.AlignmentTarget);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ImputationSettings)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MaxRtShift.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxPeakWidthVariation.GetHashCode();
                hashCode = (hashCode * 397) ^ ImputeMissingPeaks.GetHashCode();
                hashCode = (hashCode * 397) ^ (AlignmentTarget?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        #region XML Serialization
        private enum Attr
        {
            max_rt_shift,
            max_peak_width_var,
            impute_missing,
        }
        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        private ImputationSettings()
        {
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            MaxRtShift = reader.GetNullableDoubleAttribute(Attr.max_rt_shift);
            MaxPeakWidthVariation = reader.GetNullableDoubleAttribute(Attr.max_peak_width_var);
            ImputeMissingPeaks = reader.GetBoolAttribute(Attr.impute_missing, false);
            bool empty = reader.IsEmptyElement;
            reader.Read();
            if (!empty)
            {
                AlignmentTarget = reader.DeserializeElement<AlignmentTargetSpec>();
                reader.ReadEndElement();
            }
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeNullable(Attr.max_rt_shift, MaxRtShift);
            writer.WriteAttributeNullable(Attr.max_peak_width_var, MaxPeakWidthVariation);
            writer.WriteAttribute(Attr.impute_missing, ImputeMissingPeaks, false);
            if (AlignmentTarget != null)
            {
                writer.WriteElement(AlignmentTarget);
            }
        }

        public static ImputationSettings Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ImputationSettings());
        }
        #endregion
    }
}
