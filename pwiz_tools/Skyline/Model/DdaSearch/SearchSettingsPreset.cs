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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DdaSearch
{
    public enum SearchEngine
    {
        MSAmanda,
        MSGFPlus,
        MSFragger,
        Comet,
        Tide,
        Hardklor
    }


    [XmlRoot("search_workflow")]
    public sealed class SearchSettingsPreset : Immutable, IKeyContainer<string>, IXmlSerializable
    {
        // Search engine settings
        public string Name { get; private set; }
        public SearchEngine SearchEngine { get; private set; }
        public double PrecursorToleranceValue { get; private set; }
        public MzTolerance.Units PrecursorToleranceUnit { get; private set; }
        public double FragmentToleranceValue { get; private set; }
        public MzTolerance.Units FragmentToleranceUnit { get; private set; }
        public int MaxVariableMods { get; private set; }
        public string FragmentIons { get; private set; }
        public string Ms2Analyzer { get; private set; }
        public double CutoffScore { get; private set; }
        public string AdditionalSettingsXml { get; private set; }

        // FASTA settings
        public string FastaFilePath { get; private set; }
        public string EnzymeName { get; private set; }
        public int MaxMissedCleavages { get; private set; }
        public string DecoyGenerationMethod { get; private set; }
        public double? NumDecoys { get; private set; }
        public bool AutoTrain { get; private set; }

        // Modification settings (null means "use document defaults", empty means "no mods checked")
        public ImmutableList<StaticMod> StructuralModifications { get; private set; }
        public ImmutableList<StaticMod> HeavyModifications { get; private set; }
        public bool HasExplicitModifications { get; private set; }

        // Workflow settings
        public string WorkflowType { get; private set; }
        public string IrtStandardName { get; private set; }

        public SearchSettingsPreset(string name,
            SearchEngine searchEngine,
            MzTolerance precursorTolerance,
            MzTolerance fragmentTolerance,
            int maxVariableMods,
            string fragmentIons,
            string ms2Analyzer,
            double cutoffScore,
            IDictionary<string, string> additionalSettings,
            string fastaFilePath = null,
            string enzymeName = null,
            int maxMissedCleavages = 0,
            string decoyGenerationMethod = null,
            double? numDecoys = null,
            bool autoTrain = false,
            IEnumerable<StaticMod> structuralModifications = null,
            IEnumerable<StaticMod> heavyModifications = null,
            string workflowType = null,
            string irtStandardName = null,
            bool hasExplicitModifications = false)
            : this(name, searchEngine, precursorTolerance, fragmentTolerance, maxVariableMods,
                fragmentIons, ms2Analyzer, cutoffScore, SerializeSettingsDictionary(additionalSettings),
                fastaFilePath, enzymeName, maxMissedCleavages, decoyGenerationMethod, numDecoys, autoTrain,
                structuralModifications, heavyModifications, workflowType, irtStandardName, hasExplicitModifications)
        {
        }

        public SearchSettingsPreset(string name,
            SearchEngine searchEngine,
            MzTolerance precursorTolerance,
            MzTolerance fragmentTolerance,
            int maxVariableMods,
            string fragmentIons,
            string ms2Analyzer,
            double cutoffScore,
            string additionalSettingsXml,
            string fastaFilePath = null,
            string enzymeName = null,
            int maxMissedCleavages = 0,
            string decoyGenerationMethod = null,
            double? numDecoys = null,
            bool autoTrain = false,
            IEnumerable<StaticMod> structuralModifications = null,
            IEnumerable<StaticMod> heavyModifications = null,
            string workflowType = null,
            string irtStandardName = null,
            bool hasExplicitModifications = false)
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
            FastaFilePath = fastaFilePath;
            EnzymeName = enzymeName;
            MaxMissedCleavages = maxMissedCleavages;
            DecoyGenerationMethod = decoyGenerationMethod;
            NumDecoys = numDecoys;
            AutoTrain = autoTrain;
            StructuralModifications = ImmutableList.ValueOfOrEmpty(structuralModifications);
            HeavyModifications = ImmutableList.ValueOfOrEmpty(heavyModifications);
            HasExplicitModifications = hasExplicitModifications;
            WorkflowType = workflowType;
            IrtStandardName = irtStandardName;
        }

        public MzTolerance PrecursorTolerance => new MzTolerance(PrecursorToleranceValue, PrecursorToleranceUnit);
        public MzTolerance FragmentTolerance => new MzTolerance(FragmentToleranceValue, FragmentToleranceUnit);

        public SearchSettingsPreset ChangeName(string name)
        {
            return new SearchSettingsPreset(name, SearchEngine, PrecursorTolerance, FragmentTolerance,
                MaxVariableMods, FragmentIons, Ms2Analyzer, CutoffScore, AdditionalSettingsXml,
                FastaFilePath, EnzymeName, MaxMissedCleavages, DecoyGenerationMethod, NumDecoys, AutoTrain,
                StructuralModifications, HeavyModifications, WorkflowType, IrtStandardName, HasExplicitModifications);
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

        private static string SerializeSettingsDictionary(IDictionary<string, string> settings)
        {
            if (settings == null || settings.Count == 0)
                return null;

            using (var sw = new System.IO.StringWriter())
            using (var writer = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                writer.WriteStartElement(@"AdditionalSettings");
                foreach (var kvp in settings)
                {
                    writer.WriteStartElement(@"Setting");
                    writer.WriteAttributeString(@"name", kvp.Key);
                    writer.WriteAttributeString(@"value", kvp.Value);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.Flush();
                return sw.ToString();
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
            cutoff_score,
            fasta_file_path,
            enzyme_name,
            max_missed_cleavages,
            decoy_generation_method,
            num_decoys,
            auto_train,
            workflow_type,
            irt_standard_name,
            has_explicit_modifications
        }

        private enum EL
        {
            additional_settings,
            structural_modifications,
            heavy_modifications
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
            SearchEngine = reader.GetEnumAttribute(ATTR.search_engine, SearchEngine.MSFragger);
            PrecursorToleranceValue = reader.GetDoubleAttribute(ATTR.precursor_tolerance_value, 10.0);
            PrecursorToleranceUnit = reader.GetEnumAttribute(ATTR.precursor_tolerance_unit, MzTolerance.Units.ppm);
            FragmentToleranceValue = reader.GetDoubleAttribute(ATTR.fragment_tolerance_value, 0.5);
            FragmentToleranceUnit = reader.GetEnumAttribute(ATTR.fragment_tolerance_unit, MzTolerance.Units.mz);
            MaxVariableMods = reader.GetIntAttribute(ATTR.max_variable_mods, 3);
            FragmentIons = reader.GetAttribute(ATTR.fragment_ions);
            Ms2Analyzer = reader.GetAttribute(ATTR.ms2_analyzer);
            CutoffScore = reader.GetDoubleAttribute(ATTR.cutoff_score, 0.01);
            FastaFilePath = reader.GetAttribute(ATTR.fasta_file_path);
            EnzymeName = reader.GetAttribute(ATTR.enzyme_name);
            MaxMissedCleavages = reader.GetIntAttribute(ATTR.max_missed_cleavages, 0);
            DecoyGenerationMethod = reader.GetAttribute(ATTR.decoy_generation_method);
            NumDecoys = reader.GetNullableDoubleAttribute(ATTR.num_decoys);
            AutoTrain = reader.GetBoolAttribute(ATTR.auto_train, false);
            WorkflowType = reader.GetAttribute(ATTR.workflow_type);
            IrtStandardName = reader.GetAttribute(ATTR.irt_standard_name);
            HasExplicitModifications = reader.GetBoolAttribute(ATTR.has_explicit_modifications, false);

            var structuralMods = new List<StaticMod>();
            var heavyMods = new List<StaticMod>();

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
                    else if (reader.IsStartElement(EL.structural_modifications))
                    {
                        reader.ReadStartElement();
                        while (reader.IsStartElement(@"static_modification"))
                            structuralMods.Add(StaticMod.Deserialize(reader));
                        reader.ReadEndElement();
                    }
                    else if (reader.IsStartElement(EL.heavy_modifications))
                    {
                        reader.ReadStartElement();
                        while (reader.IsStartElement(@"static_modification"))
                            heavyMods.Add(StaticMod.Deserialize(reader));
                        reader.ReadEndElement();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                reader.ReadEndElement();
            }

            StructuralModifications = ImmutableList.ValueOfOrEmpty(structuralMods);
            HeavyModifications = ImmutableList.ValueOfOrEmpty(heavyMods);
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
            writer.WriteAttributeIfString(ATTR.fasta_file_path, FastaFilePath);
            writer.WriteAttributeIfString(ATTR.enzyme_name, EnzymeName);
            writer.WriteAttribute(ATTR.max_missed_cleavages, MaxMissedCleavages);
            writer.WriteAttributeIfString(ATTR.decoy_generation_method, DecoyGenerationMethod);
            if (NumDecoys.HasValue)
                writer.WriteAttribute(ATTR.num_decoys, NumDecoys.Value);
            writer.WriteAttribute(ATTR.auto_train, AutoTrain);
            writer.WriteAttributeIfString(ATTR.workflow_type, WorkflowType);
            writer.WriteAttributeIfString(ATTR.irt_standard_name, IrtStandardName);
            writer.WriteAttribute(ATTR.has_explicit_modifications, HasExplicitModifications);

            if (!string.IsNullOrEmpty(AdditionalSettingsXml))
            {
                writer.WriteStartElement(EL.additional_settings);
                writer.WriteRaw(AdditionalSettingsXml);
                writer.WriteEndElement();
            }

            if (StructuralModifications.Count > 0)
            {
                writer.WriteStartElement(EL.structural_modifications);
                foreach (var mod in StructuralModifications)
                    writer.WriteElement(mod);
                writer.WriteEndElement();
            }

            if (HeavyModifications.Count > 0)
            {
                writer.WriteStartElement(EL.heavy_modifications);
                foreach (var mod in HeavyModifications)
                    writer.WriteElement(mod);
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
                   AdditionalSettingsXml == other.AdditionalSettingsXml &&
                   FastaFilePath == other.FastaFilePath &&
                   EnzymeName == other.EnzymeName &&
                   MaxMissedCleavages == other.MaxMissedCleavages &&
                   DecoyGenerationMethod == other.DecoyGenerationMethod &&
                   Equals(NumDecoys, other.NumDecoys) &&
                   AutoTrain == other.AutoTrain &&
                   StructuralModifications.SequenceEqual(other.StructuralModifications) &&
                   HeavyModifications.SequenceEqual(other.HeavyModifications) &&
                   HasExplicitModifications == other.HasExplicitModifications &&
                   WorkflowType == other.WorkflowType &&
                   IrtStandardName == other.IrtStandardName;
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
                hashCode = (hashCode * 397) ^ (FastaFilePath?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (EnzymeName?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ MaxMissedCleavages;
                hashCode = (hashCode * 397) ^ (DecoyGenerationMethod?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ NumDecoys.GetHashCode();
                hashCode = (hashCode * 397) ^ AutoTrain.GetHashCode();
                hashCode = (hashCode * 397) ^ StructuralModifications.GetHashCode();
                hashCode = (hashCode * 397) ^ HeavyModifications.GetHashCode();
                hashCode = (hashCode * 397) ^ HasExplicitModifications.GetHashCode();
                hashCode = (hashCode * 397) ^ WorkflowType.GetHashCode();
                hashCode = (hashCode * 397) ^ (IrtStandardName?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        #endregion
    }
}
