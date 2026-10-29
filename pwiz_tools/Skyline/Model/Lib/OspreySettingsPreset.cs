/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// A named set of the values of the Osprey Search Settings page, kept in
    /// <see cref="Settings.OspreySettingsPresets"/> so a lab's settings survive a session and
    /// travel between installations (the list imports and exports): the page's own controls and the
    /// additional settings the grid holds, as <see cref="OspreyAdditionalSettings.ToXml"/> writes
    /// them. The thread count is left out, since it is the machine's.
    /// </summary>
    [XmlRoot("osprey_settings_preset")]
    public sealed class OspreySettingsPreset : XmlNamedElement
    {
        public OspreySettingsPreset(string name, bool isHighResolution, double fragmentTolerance, bool isFragmentTolerancePpm,
            double runFdr, double experimentFdr, double proteinFdr, string additionalSettingsXml)
            : base(name)
        {
            IsHighResolution = isHighResolution;
            FragmentTolerance = fragmentTolerance;
            IsFragmentTolerancePpm = isFragmentTolerancePpm;
            RunFdr = runFdr;
            ExperimentFdr = experimentFdr;
            ProteinFdr = proteinFdr;
            AdditionalSettingsXml = string.IsNullOrEmpty(additionalSettingsXml) ? null : additionalSettingsXml;
        }

        public bool IsHighResolution { get; private set; }
        public double FragmentTolerance { get; private set; }
        public bool IsFragmentTolerancePpm { get; private set; }
        public double RunFdr { get; private set; }
        public double ExperimentFdr { get; private set; }
        public double ProteinFdr { get; private set; }

        /// <summary>
        /// The additional settings that were not Osprey's defaults when the preset was saved, in the
        /// XML <see cref="OspreyAdditionalSettings.ToXml"/> writes and <see cref="OspreyAdditionalSettings.ApplyXml"/>
        /// reads; null when all were.
        /// </summary>
        public string AdditionalSettingsXml { get; private set; }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private OspreySettingsPreset()
        {
        }

        private enum ATTR
        {
            high_resolution,
            fragment_tolerance,
            fragment_tolerance_ppm,
            run_fdr,
            experiment_fdr,
            protein_fdr
        }

        private enum EL
        {
            additional_settings
        }

        public static OspreySettingsPreset Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new OspreySettingsPreset());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            IsHighResolution = reader.GetBoolAttribute(ATTR.high_resolution, true);
            FragmentTolerance = reader.GetDoubleAttribute(ATTR.fragment_tolerance);
            IsFragmentTolerancePpm = reader.GetBoolAttribute(ATTR.fragment_tolerance_ppm, true);
            RunFdr = reader.GetDoubleAttribute(ATTR.run_fdr);
            ExperimentFdr = reader.GetDoubleAttribute(ATTR.experiment_fdr);
            ProteinFdr = reader.GetDoubleAttribute(ATTR.protein_fdr);

            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.ReadStartElement();
                while (reader.IsStartElement())
                {
                    if (reader.IsStartElement(EL.additional_settings))
                        AdditionalSettingsXml = reader.ReadInnerXml();
                    else
                        reader.Skip();
                }
                reader.ReadEndElement();
            }
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            // The bool helpers omit the attribute at its default, so each default here is the one ReadXml reads
            writer.WriteAttribute(ATTR.high_resolution, IsHighResolution, true);
            writer.WriteAttribute(ATTR.fragment_tolerance, FragmentTolerance);
            writer.WriteAttribute(ATTR.fragment_tolerance_ppm, IsFragmentTolerancePpm, true);
            writer.WriteAttribute(ATTR.run_fdr, RunFdr);
            writer.WriteAttribute(ATTR.experiment_fdr, ExperimentFdr);
            writer.WriteAttribute(ATTR.protein_fdr, ProteinFdr);

            if (!string.IsNullOrEmpty(AdditionalSettingsXml))
            {
                writer.WriteStartElement(EL.additional_settings);
                writer.WriteRaw(AdditionalSettingsXml);
                writer.WriteEndElement();
            }
        }

        #endregion

        #region object overrides

        private bool Equals(OspreySettingsPreset other)
        {
            return base.Equals(other) &&
                   IsHighResolution == other.IsHighResolution &&
                   FragmentTolerance.Equals(other.FragmentTolerance) &&
                   IsFragmentTolerancePpm == other.IsFragmentTolerancePpm &&
                   RunFdr.Equals(other.RunFdr) &&
                   ExperimentFdr.Equals(other.ExperimentFdr) &&
                   ProteinFdr.Equals(other.ProteinFdr) &&
                   AdditionalSettingsXml == other.AdditionalSettingsXml;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is OspreySettingsPreset preset && Equals(preset);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ IsHighResolution.GetHashCode();
                hashCode = (hashCode * 397) ^ FragmentTolerance.GetHashCode();
                hashCode = (hashCode * 397) ^ IsFragmentTolerancePpm.GetHashCode();
                hashCode = (hashCode * 397) ^ RunFdr.GetHashCode();
                hashCode = (hashCode * 397) ^ ExperimentFdr.GetHashCode();
                hashCode = (hashCode * 397) ^ ProteinFdr.GetHashCode();
                hashCode = (hashCode * 397) ^ (AdditionalSettingsXml?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        #endregion
    }

    /// <summary>
    /// The user's Osprey settings presets (<see cref="Settings.OspreySettingsPresets"/>): none by
    /// default, each saved from the Search Settings page, edited, imported and exported through
    /// the list editor as the DDA and DIA-NN search presets are.
    /// </summary>
    public sealed class OspreySettingsPresetList : SerializableSettingsList<OspreySettingsPreset>
    {
        public override IEnumerable<OspreySettingsPreset> GetDefaults(int revisionIndex)
        {
            return Array.Empty<OspreySettingsPreset>();
        }

        public override string Title => PropertiesResources.SearchSettingsPresetList_Title_Edit_Settings_Presets;
        public override string Label => PropertiesResources.SearchSettingsPresetList_Label_Settings_Presets;

        public override Type SerialType => typeof(OspreySettingsPresetList);
        public override ICollection<OspreySettingsPreset> CreateEmptyList() => new OspreySettingsPresetList();
        public override string FileExtension => @".skyosp";
    }
}
