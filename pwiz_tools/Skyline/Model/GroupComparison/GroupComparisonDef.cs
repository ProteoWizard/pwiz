/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.GroupComparison
{
    [XmlRoot("group_comparison")]
    public sealed class GroupComparisonDef : XmlNamedElement
    {
        public static readonly GroupComparisonDef EMPTY = new GroupComparisonDef
        {
            NormalizationMethod = NormalizationMethod.NONE,
            SummarizationMethod = SummarizationMethod.AVERAGING,
            ConfidenceLevelTimes100 = 95,
        };

        public GroupComparisonDef(string name) : base(name)
        {
            NormalizationMethod = NormalizationMethod.NONE;
        }

        public string ControlAnnotation { get; private set; }

        public GroupComparisonDef ChangeControlAnnotation(string value)
        {
            return ChangeProp(ImClone(this), im => im.ControlAnnotation = value);
        }

        public string ControlValue { get; private set; }

        public GroupComparisonDef ChangeControlValue(string value)
        {
            return ChangeProp(ImClone(this), im => im.ControlValue = value);
        }

        public string CaseValue { get; private set; }

        public GroupComparisonDef ChangeCaseValue(string value)
        {
            return ChangeProp(ImClone(this), im => im.CaseValue = value);
        }

        public string IdentityAnnotation { get; private set; }

        public GroupComparisonDef ChangeIdentityAnnotation(string value)
        {
            return ChangeProp(ImClone(this), im => im.IdentityAnnotation = value);
        }

        public bool AverageTechnicalReplicates { get; private set; }

        public GroupComparisonDef ChangeAverageTechnicalReplicates(bool value)
        {
            return ChangeProp(ImClone(this), im => im.AverageTechnicalReplicates = value);
        }

        public NormalizationMethod NormalizationMethod { get; private set; }

        public GroupComparisonDef ChangeNormalizationMethod(NormalizationMethod value)
        {
            return ChangeProp(ImClone(this), im => im.NormalizationMethod = value);
        }

        public bool IncludeInteractionTransitions { get; private set; }

        public GroupComparisonDef ChangeIncludeInteractionTransitions(bool value)
        {
            return ChangeProp(ImClone(this), im => im.IncludeInteractionTransitions = value);
        }

        public new GroupComparisonDef ChangeName(string name)
        {
            return (GroupComparisonDef) base.ChangeName(name);
        }

        public SummarizationMethod SummarizationMethod { get; private set; }

        public GroupComparisonDef ChangeSummarizationMethod(SummarizationMethod summarizationMethod)
        {
            return ChangeProp(ImClone(this), im => im.SummarizationMethod = summarizationMethod);
        }

        public double ConfidenceLevel { get { return ConfidenceLevelTimes100/100; } }

        public double ConfidenceLevelTimes100 { get; private set; }

        public GroupComparisonDef ChangeConfidenceLevelTimes100(double value)
        {
            return ChangeProp(ImClone(this), im => im.ConfidenceLevelTimes100 = value);
        }

        public bool PerProtein { get; private set; }

        public GroupComparisonDef ChangePerProtein(bool value)
        {
            return ChangeProp(ImClone(this), im => im.PerProtein = value);
        }

        public bool UseZeroForMissingPeaks { get; private set; }

        public GroupComparisonDef ChangeUseZeroForMissingPeaks(bool value)
        {
            return ChangeProp(ImClone(this), im => im.UseZeroForMissingPeaks = value);
        }

        public double? QValueCutoff { get; private set; }

        public GroupComparisonDef ChangeQValueCutoff(double? qValueCutoff)
        {
            return ChangeProp(ImClone(this), im => im.QValueCutoff = qValueCutoff);
        }

        public GroupIdentifier GetGroupIdentifier(SrmSettings settings, ChromatogramSet chromatogramSet)
        {
            AnnotationDef annotationDef =
                settings.DataSettings.AnnotationDefs.FirstOrDefault(a => a.Name == ControlAnnotation);
            if (annotationDef == null)
            {
                return default(GroupIdentifier);
            }
            return GroupIdentifier.MakeGroupIdentifier(chromatogramSet.Annotations.GetAnnotation(annotationDef));
        }

        public GroupIdentifier GetControlGroupIdentifier(SrmSettings settings)
        {
            if (string.IsNullOrEmpty(ControlAnnotation))
            {
                return default(GroupIdentifier);
            }
            AnnotationDef annotationDef =
                settings.DataSettings.AnnotationDefs.FirstOrDefault(a => a.Name == ControlAnnotation);
            if (annotationDef == null)
            {
                return default(GroupIdentifier);
            }
            return GroupIdentifier.MakeGroupIdentifier(annotationDef.ParsePersistedString(ControlValue));
        }

        #region XML serialization

        private enum ATTR
        {
            control_annotation,
            control_value,
            case_value,
            avg_tech_replicates,
            identity_annotation,
            normalization_method,
            include_interaction_transitions,
            summarization_method,
            confidence_level,
            per_protein,
            use_zero_for_missing_peaks,
            q_value_cutoff,
        }
        private GroupComparisonDef()
        {
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            ControlAnnotation = reader.GetAttribute(ATTR.control_annotation);
            ControlValue = reader.GetAttribute(ATTR.control_value);
            CaseValue = reader.GetAttribute(ATTR.case_value);
            IdentityAnnotation = reader.GetAttribute(ATTR.identity_annotation);
            AverageTechnicalReplicates = reader.GetBoolAttribute(ATTR.avg_tech_replicates, true);
            NormalizationMethod = NormalizationMethod.FromName(reader.GetAttribute(ATTR.normalization_method));
            IncludeInteractionTransitions = reader.GetBoolAttribute(ATTR.include_interaction_transitions, false);
            SummarizationMethod = SummarizationMethod.FromName(reader.GetAttribute(ATTR.summarization_method));
            ConfidenceLevelTimes100 = reader.GetDoubleAttribute(ATTR.confidence_level, 95);
            PerProtein = reader.GetBoolAttribute(ATTR.per_protein, false);
            UseZeroForMissingPeaks = reader.GetBoolAttribute(ATTR.use_zero_for_missing_peaks, false);
            QValueCutoff = reader.GetNullableDoubleAttribute(ATTR.q_value_cutoff);
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttributeIfString(ATTR.control_annotation, ControlAnnotation);
            writer.WriteAttributeIfString(ATTR.control_value, ControlValue);
            writer.WriteAttributeIfString(ATTR.case_value, CaseValue);
            writer.WriteAttributeIfString(ATTR.identity_annotation, IdentityAnnotation);
            writer.WriteAttribute(ATTR.avg_tech_replicates, AverageTechnicalReplicates, true);
            writer.WriteAttributeIfString(ATTR.normalization_method, NormalizationMethod.Name);
            writer.WriteAttribute(ATTR.include_interaction_transitions, IncludeInteractionTransitions, false);
            writer.WriteAttribute(ATTR.summarization_method, SummarizationMethod.Name);
            writer.WriteAttribute(ATTR.confidence_level, ConfidenceLevelTimes100);
            writer.WriteAttribute(ATTR.per_protein, PerProtein, false);
            writer.WriteAttribute(ATTR.use_zero_for_missing_peaks, UseZeroForMissingPeaks, false);
            writer.WriteAttributeNullable(ATTR.q_value_cutoff, QValueCutoff);
        }

        public static GroupComparisonDef Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new GroupComparisonDef());
        }

        #endregion

        private bool Equals(GroupComparisonDef other)
        {
            return base.Equals(other) &&
                   string.Equals(ControlAnnotation, other.ControlAnnotation) &&
                   string.Equals(ControlValue, other.ControlValue) && 
                   string.Equals(CaseValue, other.CaseValue) &&
                   string.Equals(IdentityAnnotation, other.IdentityAnnotation) &&
                   AverageTechnicalReplicates.Equals(other.AverageTechnicalReplicates) &&
                   Equals(NormalizationMethod, other.NormalizationMethod) &&
                   IncludeInteractionTransitions.Equals(other.IncludeInteractionTransitions) &&
                   Equals(SummarizationMethod, other.SummarizationMethod) &&
                   Equals(ConfidenceLevel, other.ConfidenceLevel) && 
                   Equals(PerProtein, other.PerProtein) &&
                   Equals(UseZeroForMissingPeaks, other.UseZeroForMissingPeaks) &&
                   Equals(QValueCutoff, other.QValueCutoff);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is GroupComparisonDef && Equals((GroupComparisonDef) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ (ControlAnnotation != null ? ControlAnnotation.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (ControlValue != null ? ControlValue.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (CaseValue != null ? CaseValue.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (IdentityAnnotation != null ? IdentityAnnotation.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ AverageTechnicalReplicates.GetHashCode();
                hashCode = (hashCode*397) ^ NormalizationMethod.GetHashCode();
                hashCode = (hashCode*397) ^ IncludeInteractionTransitions.GetHashCode();
                hashCode = (hashCode*397) ^ SummarizationMethod.GetHashCode();
                hashCode = (hashCode*397) ^ ConfidenceLevel.GetHashCode();
                hashCode = (hashCode*397) ^ PerProtein.GetHashCode();
                hashCode = (hashCode*397) ^ UseZeroForMissingPeaks.GetHashCode();
                hashCode = (hashCode*397) ^ QValueCutoff.GetHashCode();
                return hashCode;
            }
        }
    }
}
