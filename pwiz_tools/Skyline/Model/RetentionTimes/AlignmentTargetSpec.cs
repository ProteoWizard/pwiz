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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.RetentionTimes
{
    [XmlRoot("alignment")]
    public class AlignmentTargetSpec : Immutable, IXmlSerializable
    {
        public static readonly AlignmentTargetSpec None = new AlignmentTargetSpec(@"none");
        public static readonly AlignmentTargetSpec Default = new AlignmentTargetSpec(@"default");
        public static readonly AlignmentTargetSpec Calculator = new AlignmentTargetSpec(@"calculator");
        public static readonly AlignmentTargetSpec Library = new AlignmentTargetSpec(@"library");
        public static readonly AlignmentTargetSpec ChromatogramPeaks = new AlignmentTargetSpec(@"chromatogram_peaks");

        private AlignmentTargetSpec(string type)
        {
            Type = type;
        }

        [Track]
        public string Type { get; private set; }

        public AlignmentTargetSpec ChangeType(string value)
        {
            return ChangeProp(ImClone(this), im => im.Type = value);
        }

        [Track(defaultValues:typeof(DefaultValuesNull))]
        public string Name { get; private set; }

        public AlignmentTargetSpec ChangeName(string value)
        {
            return ChangeProp(ImClone(this), im => im.Name = value);
        }

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public string RegressionMethod { get; private set; }

        public AlignmentTargetSpec ChangeRegressionMethod(string value)
        {
            return ChangeProp(ImClone(this), im => im.RegressionMethod = value);
        }

        protected bool Equals(AlignmentTargetSpec other)
        {
            return Type == other.Type && Name == other.Name && RegressionMethod == other.RegressionMethod;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AlignmentTargetSpec)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RegressionMethod != null ? RegressionMethod.GetHashCode() : 0);
                return hashCode;
            }
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        private AlignmentTargetSpec()
        {
        }

        private enum ATTR
        {
            type,
            name,
            regression_method
        }
        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (Type != null)
            {
                throw new InvalidOperationException();
            }

            Type = reader.GetAttribute(ATTR.type) ?? Default.Type;
            Name = reader.GetAttribute(ATTR.name);
            RegressionMethod = reader.GetAttribute(ATTR.regression_method);
            reader.Skip();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.type, Type);
            writer.WriteAttributeIfString(ATTR.name, Name);
            writer.WriteAttributeIfString(ATTR.regression_method, RegressionMethod);
        }

        public static IList<AlignmentTargetSpec> GetOptions(PeptideSettings peptideSettings)
        {
            var list = new List<AlignmentTargetSpec>
            {
                None,
                Default
            };
            if (peptideSettings.Prediction?.RetentionTime?.Calculator != null)
            {
                list.Add(Calculator);
            }

            for (int iLibrary = 0; iLibrary < peptideSettings.Libraries.Libraries.Count; iLibrary++)
            {
                var library = peptideSettings.Libraries.Libraries[iLibrary];
                var libraryName = library?.Name ?? peptideSettings.Libraries.LibrarySpecs[iLibrary]?.Name;
                if (libraryName == null)
                {
                    continue;
                }

                if (true == library?.IsLoaded && !library.ListRetentionTimeSources().Any())
                {
                    continue;
                }
                list.Add(Library.ChangeName(libraryName));
            }
            list.Add(ChromatogramPeaks);
            return list;
        }

        public RtCalculatorOption ToRtCalculatorOption(PeptideSettings peptideSettings)
        {
            if (Type == Default.Type)
            {
                RtCalculatorOption.TryGetDefault(peptideSettings, out var defaultOption);
                return defaultOption;
            }

            if (Type == Library.Type)
            {
                return new RtCalculatorOption.Library(Name);
            }

            if (Type == Calculator.Type)
            {
                return new RtCalculatorOption.Irt(Name ?? peptideSettings.Prediction?.RetentionTime?.Calculator?.Name);
            }

            if (Type == ChromatogramPeaks.Type)
            {
                return RtCalculatorOption.MedianDocRetentionTimes;
            }

            return null;
        }
        public static AlignmentTargetSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AlignmentTargetSpec());
        }

        public string GetLabel(PeptideSettings peptideSettings)
        {
            if (Type == None.Type)
            {
                return Resources.SettingsList_ELEMENT_NONE_None;
            }

            if (Type == Default.Type)
            {
                if (!RtCalculatorOption.TryGetDefault(peptideSettings, out var defaultOption))
                {
                    return RetentionTimesResources.AlignmentTargetSpec_GetLabel_Default;
                }

                var defaultSpec = defaultOption?.ToAlignmentTargetSpec() ?? None;
                return TextUtil.SpaceSeparate(defaultSpec.GetLabel(peptideSettings), RetentionTimesResources.AlignmentTargetSpec_GetLabel__default_);
            }

            if (Type == Library.Type)
            {
                return new RtCalculatorOption.Library(Name).DisplayName;
            }

            if (Type == Calculator.Type)
            {
                var name = Name ?? peptideSettings.Prediction.RetentionTime?.Calculator?.Name;
                if (string.IsNullOrEmpty(name))
                {
                    return RetentionTimesResources.AlignmentTargetSpec_GetLabel_Retention_time_calculator;
                }

                return string.Format(RetentionTimesResources.AlignmentTargetSpec_GetLabel_RT_calculator__0_, name);
            }

            if (Type == ChromatogramPeaks.Type)
            {
                return RtCalculatorOption.MedianDocRetentionTimes.DisplayName;
            }

            return RetentionTimesResources.AlignmentTargetSpec_GetLabel___Invalid__;
        }

        public string GetTooltip(PeptideSettings peptideSettings, SrmDocument.DOCUMENT_TYPE documentType)
        {
            if (Type == None.Type)
            {
                return RetentionTimesResources.AlignmentTargetSpec_GetTooltip_Retention_time_values_will_be_unchanged_when_mapping_between_runs;
            }

            if (Type == Default.Type)
            {
                if (!RtCalculatorOption.TryGetDefault(peptideSettings, out var defaultOption))
                {
                    return RetentionTimesResources.AlignmentTargetSpec_GetTooltip_Use_the_retention_time_calculator_if_there_is_one_or_the_first_library;
                }

                var defaultSpec = defaultOption?.ToAlignmentTargetSpec() ?? None;
                return defaultSpec.GetTooltip(peptideSettings, documentType);
            }

            if (Type == Calculator.Type)
            {
                var calculator = peptideSettings.Prediction.RetentionTime?.Calculator;
                if (calculator == null)
                {
                    return RetentionTimesResources.AlignmentTargetSpec_GetTooltip_Invalid;
                }

                if (calculator is RCalcIrt rCalcIrt && rCalcIrt.IsUsable)
                {
                    int standardCount = rCalcIrt.GetStandardPeptides().Count();
                    if (standardCount != 0)
                    {
                        if (documentType == SrmDocument.DOCUMENT_TYPE.proteomic)
                        {
                            return string.Format(RetentionTimesResources.AlignmentTargetSpec_GetTooltip__0__regression_against__1__standard_peptides_in_calculator__2_,
                                rCalcIrt.RegressionType, standardCount, rCalcIrt.Name);
                        }
                        else
                        {
                            return string.Format(RetentionTimesResources.AlignmentTargetSpec_GetTooltip__0__regression_against__1__standard_molecules_in_calculator__2_,
                                rCalcIrt.RegressionType, standardCount, rCalcIrt.Name);
                        }
                    }

                    if (documentType == SrmDocument.DOCUMENT_TYPE.proteomic)
                    {
                        return string.Format(RetentionTimesResources.AlignmentTargetSpec_GetTooltip__0__regression_against_all_peptides_in_calculator__1_,
                            rCalcIrt.RegressionType, rCalcIrt.Name);
                    }

                    return string.Format(RetentionTimesResources.AlignmentTargetSpec_GetTooltip__0__regression_against_all_molecules_in_calculator__1_,
                        rCalcIrt.RegressionType, rCalcIrt.Name);
                }
            }

            if (Type == Library.Type)
            {
                return string.Format(RetentionTimesResources.AlignmentTargetSpec_GetTooltip__0__regression_against_median_retention_times_from_library__1_,
                    IrtRegressionType.LOWESS, Name);
            }

            if (Type == ChromatogramPeaks.Type)
            {
                return string.Format(
                    RetentionTimesResources.AlignmentTargetSpec_GetTooltip__0__regression_against_median_chromatogram_peak_apex_times_across_replicate_in_this_document,
                    IrtRegressionType.LOWESS);
            }

            return null;
        }

        public bool TryGetAlignmentTarget(SrmSettings settings, out AlignmentTarget alignmentTarget)
        {
            if (Type == None.Type)
            {
                alignmentTarget = null;
                return true;
            }

            var calculatorOption = ToRtCalculatorOption(settings.PeptideSettings);
            if (calculatorOption == null)
            {
                alignmentTarget = null;
                return false;
            }
            alignmentTarget = calculatorOption.GetAlignmentTarget(settings);
            return alignmentTarget != null;
        }

        public bool TryGetAlignmentTarget(SrmDocument document, out AlignmentTarget alignmentTarget)
        {
            if (Type == None.Type)
            {
                alignmentTarget = null;
                return true;
            }
            if (Type == ChromatogramPeaks.Type)
            {
                alignmentTarget = document.Settings.DocumentRetentionTimes?.MedianDocumentRetentionTimes ??
                                  new AlignmentTarget.MedianDocumentRetentionTimes(document);
                return true;
            }

            RtCalculatorOption option;
            if (Type == Default.Type)
            {
                if (!RtCalculatorOption.TryGetDefault(document.Settings.PeptideSettings, out option))
                {
                    alignmentTarget = null;
                    return false;
                }
            }
            else
            {
                option = ToRtCalculatorOption(document.Settings.PeptideSettings);
                if (option == null)
                {
                    alignmentTarget = null;
                    return false;
                }
            }

            if (option == null)
            {
                alignmentTarget = null;
                return true;
            }

            alignmentTarget = option.GetAlignmentTarget(document.Settings);
            return alignmentTarget != null;
        }

        public bool IsChromatogramPeaks => Type == ChromatogramPeaks.Type;

        public bool IsSameAsDefault(PeptideSettings peptideSettings)
        {
            if (!RtCalculatorOption.TryGetDefault(peptideSettings, out var defaultOption))
            {
                return false;
            }

            if (Type == None.Type)
            {
                return defaultOption == null;
            }

            if (Type == Calculator.Type)
            {
                return defaultOption is RtCalculatorOption.Irt;
            }

            if (Type == Library.Type)
            {
                return defaultOption is RtCalculatorOption.Library libraryOption && libraryOption.LibraryName == Name;
            }

            return false;
        }
    }
}
