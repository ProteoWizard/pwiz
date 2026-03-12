/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.5) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DdaSearch
{
    [XmlRoot("search_workflow")]
    public sealed class SearchSettingsPreset : Immutable, IKeyContainer<string>, IXmlSerializable
    {
        public string Name { get; private set; }
        public SearchSettingsControl.SearchEngine SearchEngine { get; private set; }
        public double PrecursorToleranceValue { get; private set; }
        public MzTolerance.Units PrecursorToleranceUnit { get; private set; }
        public double FragmentToleranceValue { get; private set; }
        public MzTolerance.Units FragmentToleranceUnit { get; private set; }
        public int MaxVariableMods { get; private set; }
        public string FragmentIons { get; private set; }
        public string Ms2Analyzer { get; private set; }
        public double CutoffScore { get; private set; }
        public string AdditionalSettingsXml { get; private set; }

        public SearchSettingsPreset(string name,
            SearchSettingsControl.SearchEngine searchEngine,
            MzTolerance precursorTolerance,
            MzTolerance fragmentTolerance,
            int maxVariableMods,
            string fragmentIons,
            string ms2Analyzer,
            double cutoffScore,
            string additionalSettingsXml)
        {
            Name = name;
            SearchEngine = searchEngine;
            PrecursorToleranceValue = precursorTolerance.Value;
            PrecursorToleranceUnit = precursorTolerance.Unit;
            FragmentToleranceValue = fragmentTolerance.Value;
            FragmentToleranceUnit = fragmentTolerance.Unit;
            MaxVariableMods = maxVariableMods;
            FragmentIons = fragmentIons;
            Ms2Analyzer = ms2Analyzer;
            CutoffScore = cutoffScore;
            AdditionalSettingsXml = additionalSettingsXml;
        }

        public MzTolerance PrecursorTolerance => new MzTolerance(PrecursorToleranceValue, PrecursorToleranceUnit);
        public MzTolerance FragmentTolerance => new MzTolerance(FragmentToleranceValue, FragmentToleranceUnit);

        public SearchSettingsPreset ChangeName(string name)
        {
            return new SearchSettingsPreset(name, SearchEngine, PrecursorTolerance, FragmentTolerance,
                MaxVariableMods, FragmentIons, Ms2Analyzer, CutoffScore, AdditionalSettingsXml);
        }

        public string GetKey()
        {
            return Name;
        }

        /// <summary>
        /// Apply the additional settings from this workflow to a search engine instance.
        /// </summary>
        public void ApplyAdditionalSettings(AbstractDdaSearchEngine engine)
        {
            if (string.IsNullOrEmpty(AdditionalSettingsXml) || engine?.AdditionalSettings == null)
                return;

            var doc = new XmlDocument();
            doc.LoadXml(AdditionalSettingsXml);
            foreach (XmlNode node in doc.SelectNodes(@"//Setting"))
            {
                var settingName = node.Attributes?[@"name"]?.Value;
                var settingValue = node.Attributes?[@"value"]?.Value;
                if (settingName != null && engine.AdditionalSettings.TryGetValue(settingName, out var setting))
                    setting.Value = settingValue;
            }
        }

        /// <summary>
        /// Serialize additional settings from a search engine to XML format.
        /// Only non-default values are stored.
        /// </summary>
        public static string SerializeAdditionalSettings(IDictionary<string, AbstractDdaSearchEngine.Setting> settings)
        {
            if (settings == null || settings.Count == 0)
                return null;

            using (var sw = new System.IO.StringWriter())
            using (var writer = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                writer.WriteStartElement(@"AdditionalSettings");
                foreach (var kvp in settings)
                {
                    if (kvp.Value.IsDefault)
                        continue;
                    writer.WriteStartElement(@"Setting");
                    writer.WriteAttributeString(@"name", kvp.Key);
                    writer.WriteAttributeString(@"value", kvp.Value.Value?.ToString() ?? string.Empty);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.Flush();
                return sw.ToString();
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private SearchSettingsPreset()
        {
        }

        private enum ATTR
        {
            name,
            search_engine,
            precursor_tolerance_value,
            precursor_tolerance_unit,
            fragment_tolerance_value,
            fragment_tolerance_unit,
            max_variable_mods,
            fragment_ions,
            ms2_analyzer,
            cutoff_score
        }

        private enum EL
        {
            additional_settings
        }

        public static SearchSettingsPreset Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new SearchSettingsPreset());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            Name = reader.GetAttribute(ATTR.name);
            SearchEngine = reader.GetEnumAttribute(ATTR.search_engine, SearchSettingsControl.SearchEngine.MSFragger);
            PrecursorToleranceValue = reader.GetDoubleAttribute(ATTR.precursor_tolerance_value, 10.0);
            PrecursorToleranceUnit = reader.GetEnumAttribute(ATTR.precursor_tolerance_unit, MzTolerance.Units.ppm);
            FragmentToleranceValue = reader.GetDoubleAttribute(ATTR.fragment_tolerance_value, 0.5);
            FragmentToleranceUnit = reader.GetEnumAttribute(ATTR.fragment_tolerance_unit, MzTolerance.Units.mz);
            MaxVariableMods = reader.GetIntAttribute(ATTR.max_variable_mods, 3);
            FragmentIons = reader.GetAttribute(ATTR.fragment_ions);
            Ms2Analyzer = reader.GetAttribute(ATTR.ms2_analyzer);
            CutoffScore = reader.GetDoubleAttribute(ATTR.cutoff_score, 0.01);

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
                    {
                        AdditionalSettingsXml = reader.ReadOuterXml();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                reader.ReadEndElement();
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.name, Name);
            writer.WriteAttribute(ATTR.search_engine, SearchEngine);
            writer.WriteAttribute(ATTR.precursor_tolerance_value, PrecursorToleranceValue);
            writer.WriteAttribute(ATTR.precursor_tolerance_unit, PrecursorToleranceUnit);
            writer.WriteAttribute(ATTR.fragment_tolerance_value, FragmentToleranceValue);
            writer.WriteAttribute(ATTR.fragment_tolerance_unit, FragmentToleranceUnit);
            writer.WriteAttribute(ATTR.max_variable_mods, MaxVariableMods);
            writer.WriteAttributeIfString(ATTR.fragment_ions, FragmentIons);
            writer.WriteAttributeIfString(ATTR.ms2_analyzer, Ms2Analyzer);
            writer.WriteAttribute(ATTR.cutoff_score, CutoffScore);

            if (!string.IsNullOrEmpty(AdditionalSettingsXml))
            {
                writer.WriteStartElement(EL.additional_settings);
                writer.WriteRaw(AdditionalSettingsXml);
                writer.WriteEndElement();
            }
        }

        #endregion

        #region object overrides

        private bool Equals(SearchSettingsPreset other)
        {
            return Name == other.Name &&
                   SearchEngine == other.SearchEngine &&
                   PrecursorToleranceValue.Equals(other.PrecursorToleranceValue) &&
                   PrecursorToleranceUnit == other.PrecursorToleranceUnit &&
                   FragmentToleranceValue.Equals(other.FragmentToleranceValue) &&
                   FragmentToleranceUnit == other.FragmentToleranceUnit &&
                   MaxVariableMods == other.MaxVariableMods &&
                   FragmentIons == other.FragmentIons &&
                   Ms2Analyzer == other.Ms2Analyzer &&
                   CutoffScore.Equals(other.CutoffScore) &&
                   AdditionalSettingsXml == other.AdditionalSettingsXml;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is SearchSettingsPreset workflow && Equals(workflow);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Name?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (int)SearchEngine;
                hashCode = (hashCode * 397) ^ PrecursorToleranceValue.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)PrecursorToleranceUnit;
                hashCode = (hashCode * 397) ^ FragmentToleranceValue.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)FragmentToleranceUnit;
                hashCode = (hashCode * 397) ^ MaxVariableMods;
                hashCode = (hashCode * 397) ^ (FragmentIons?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Ms2Analyzer?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ CutoffScore.GetHashCode();
                hashCode = (hashCode * 397) ^ (AdditionalSettingsXml?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        #endregion
    }
}
