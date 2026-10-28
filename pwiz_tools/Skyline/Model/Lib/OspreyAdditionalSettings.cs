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
using System.Linq;
using System.Text;
using System.Xml;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Util.Extensions;
using UserSettings = pwiz.Skyline.Properties.Settings;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// The settings of an Osprey run beyond the ones the wizard's pages bind: every property the
    /// installed Osprey declares (<see cref="OspreyProperties"/>) except the form-bound ones
    /// (<see cref="OspreyHelpers.FORM_BOUND_ARGUMENTS"/>), each as a
    /// <see cref="AbstractDdaSearchEngine.Setting"/> named by its argument, which the Additional
    /// settings grid edits under its label, the command line carries when it is not the default
    /// and applies to the run (<see cref="AppendArguments"/>), and the user's last values persist
    /// through (<see cref="ToXml"/>, <see cref="ApplyXml"/>) in the shape the DDA search presets
    /// keep theirs.
    /// </summary>
    public sealed class OspreyAdditionalSettings
    {
        public static readonly OspreyPropertyScope[] ALL_SCOPES =
            { OspreyPropertyScope.search, OspreyPropertyScope.library, OspreyPropertyScope.digest, OspreyPropertyScope.tuning };

        /// <summary>
        /// The scopes a run configured as <paramref name="config"/> is: a search of data files or of
        /// tuning runs; a predicted library (from a FASTA or a precursor table); a FASTA digest; a
        /// fine-tune on tuning runs. Osprey refuses an option outside its scope, so the grid shows
        /// and the command line carries only these.
        /// </summary>
        public static ICollection<OspreyPropertyScope> ScopesFor(OspreyConfig config)
        {
            var scopes = new List<OspreyPropertyScope>();
            if (config.DataFiles.Count > 0 || config.TrainingInputs.Count > 0)
                scopes.Add(OspreyPropertyScope.search);
            if (string.IsNullOrEmpty(config.LibraryPath))
            {
                scopes.Add(OspreyPropertyScope.library);
                if (!string.IsNullOrEmpty(config.FastaPath))
                    scopes.Add(OspreyPropertyScope.digest);
                if (config.TrainingInputs.Count > 0)
                    scopes.Add(OspreyPropertyScope.tuning);
            }
            return scopes;
        }

        private readonly Dictionary<string, OspreyProperty> _propertiesBySettingName = new Dictionary<string, OspreyProperty>();

        public OspreyAdditionalSettings(OspreyProperties properties)
        {
            Properties = properties;
            Settings = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            var formBound = new HashSet<string>(OspreyHelpers.FORM_BOUND_ARGUMENTS);
            foreach (var property in properties.Properties.Where(p => !formBound.Contains(p.Name)))
            {
                var setting = property.CreateSetting();
                Settings[setting.Name] = setting;
                _propertiesBySettingName[setting.Name] = property;
            }
        }

        public OspreyProperties Properties { get; }

        /// <summary>The settings by argument name: what the command line and the persisted values use.</summary>
        public IDictionary<string, AbstractDdaSearchEngine.Setting> Settings { get; }

        /// <summary>The settings that are not at their defaults, in the properties' order.</summary>
        public IEnumerable<AbstractDdaSearchEngine.Setting> ChangedSettings
        {
            get { return Properties.Properties.Select(p => Settings.TryGetValue(p.Name, out var s) ? s : null).Where(s => s != null && !s.IsDefault); }
        }

        /// <summary>The property of a setting from <see cref="Settings"/>.</summary>
        public OspreyProperty GetProperty(AbstractDdaSearchEngine.Setting setting)
        {
            return _propertiesBySettingName[setting.Name];
        }

        /// <summary>
        /// The settings of <paramref name="scopes"/> under their labels, for the grid: the same
        /// objects as <see cref="Settings"/>, so what the grid sets is what the command line reads.
        /// A label two properties share (not expected) is told apart by its argument. The dictionary
        /// enumerates in the properties' (help) order, which the grid relies on to show the settings
        /// under their group headers: a Dictionary keeps its insertion order while nothing is removed.
        /// </summary>
        public IDictionary<string, AbstractDdaSearchEngine.Setting> GetGridValues(ICollection<OspreyPropertyScope> scopes)
        {
            var gridValues = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            foreach (var property in Properties.Properties.Where(p => scopes.Contains(p.Scope)))
            {
                if (!Settings.TryGetValue(property.Name, out var setting))
                    continue;
                string label = gridValues.ContainsKey(property.Label) ? property.Label + @" (" + property.ArgumentText + @")" : property.Label;
                gridValues[label] = setting;
            }
            return gridValues;
        }

        /// <summary>The grid's check of text typed for <paramref name="setting"/>: the property's rules, then the setting's own.</summary>
        public void Validate(string text, AbstractDdaSearchEngine.Setting setting)
        {
            GetProperty(setting).Validate(text);
            setting.Validate(text);
        }

        /// <summary>
        /// Sets <paramref name="setting"/> from the text the grid holds: a number typed in the UI
        /// culture (0,05 under fr-FR) is kept in the invariant culture, so it persists and reaches
        /// the command line the same for any culture; blank, auto and every other type as typed.
        /// </summary>
        public void SetValue(AbstractDdaSearchEngine.Setting setting, string text)
        {
            var property = GetProperty(setting);
            if (property.Type == OspreyPropertyType.integer || property.Type == OspreyPropertyType.number)
            {
                string argumentValue = property.ToArgumentValue(text);
                setting.Value = string.IsNullOrEmpty(argumentValue) ? text : argumentValue;
            }
            else
            {
                setting.Value = text;
            }
        }

        public string GetTooltip(AbstractDdaSearchEngine.Setting setting)
        {
            return GetProperty(setting).Tooltip;
        }

        /// <summary>The title of the help section of <paramref name="setting"/>: the grid's group header.</summary>
        public string GetGroup(AbstractDdaSearchEngine.Setting setting)
        {
            return GetProperty(setting).Group;
        }

        /// <summary>
        /// Appends to <paramref name="sb"/> the arguments of the settings that are not at their
        /// defaults and apply in <paramref name="scopes"/>, each followed by a space: a flag as a
        /// bare switch, a value in the form the command line takes, quoted when it needs to be.
        /// </summary>
        public void AppendArguments(StringBuilder sb, ICollection<OspreyPropertyScope> scopes)
        {
            foreach (var property in Properties.Properties.Where(p => scopes.Contains(p.Scope)))
            {
                if (!Settings.TryGetValue(property.Name, out var setting) || setting.IsDefault)
                    continue;
                if (property.Type == OspreyPropertyType.flag)
                {
                    if (Equals(setting.Value, true))
                        sb.Append(property.ArgumentText).Append(' ');
                    continue;
                }
                string value = property.ToArgumentValue(setting.Value?.ToString());
                if (value == null)
                    continue;
                sb.Append(property.ArgumentText);
                if (value.Length > 0)
                    sb.Append(' ').Append(QuoteIfNeeded(property, value));
                sb.Append(' ');
            }
        }

        /// <summary>The settings not at their defaults, as the DDA search presets keep theirs; null when all are.</summary>
        public string ToXml()
        {
            return ChangedSettings.Any() ? SearchSettingsPreset.SerializeAdditionalSettings(Settings) : null;
        }

        /// <summary>Returns every setting to Osprey's default, before a preset's values replace the ones it names.</summary>
        public void ResetToDefaults()
        {
            foreach (var setting in Settings.Values)
                setting.Value = setting.DefaultValue;
        }

        /// <summary>Sets the values the user last ran with (<see cref="SaveLastUsed"/>), returning the names refused (<see cref="ApplyXml"/>).</summary>
        public IList<string> ApplyLastUsed()
        {
            return ApplyXml(UserSettings.Default.OspreyAdditionalSettings);
        }

        /// <summary>Keeps the values that are not defaults as the ones the user last ran with, for the next wizard.</summary>
        public void SaveLastUsed()
        {
            UserSettings.Default.OspreyAdditionalSettings = ToXml() ?? string.Empty;
        }

        /// <summary>
        /// Sets the settings from <paramref name="xml"/> (<see cref="ToXml"/>), one named there that
        /// this Osprey does not declare ignored, one the wizard's pages bind refused: those are
        /// returned, for a message naming them, and the rest are applied. A value the setting
        /// refuses is reported the same way.
        /// </summary>
        public IList<string> ApplyXml(string xml)
        {
            var refused = new List<string>();
            if (string.IsNullOrEmpty(xml))
                return refused;
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var nodes = doc.SelectNodes(@"//Setting");
            if (nodes == null)
                return refused;
            var formBound = new HashSet<string>(OspreyHelpers.FORM_BOUND_ARGUMENTS);
            foreach (XmlNode node in nodes)
            {
                string name = node.Attributes?[@"name"]?.Value;
                string value = node.Attributes?[@"value"]?.Value;
                if (name == null || value == null)
                    continue;
                if (formBound.Contains(name))
                {
                    refused.Add(name);
                    continue;
                }
                if (!Settings.TryGetValue(name, out var setting))
                    continue;
                try
                {
                    GetProperty(setting).Validate(value);
                    setting.Value = value;
                }
                catch (Exception)
                {
                    refused.Add(name);
                }
            }
            return refused;
        }

        private static string QuoteIfNeeded(OspreyProperty property, string value)
        {
            if (property.Type == OspreyPropertyType.path || value.Any(char.IsWhiteSpace))
                return value.Quote();
            return value;
        }
    }
}
