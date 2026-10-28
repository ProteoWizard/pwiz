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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>What a property's value is, as Osprey declares it: the control the grid shows and how the value is checked.</summary>
    public enum OspreyPropertyType
    {
        flag,
        text,
        path,
        integer,
        number,
        list,
    }

    /// <summary>
    /// When a property applies, as Osprey declares it: to every run, to a run that predicts its
    /// library, to one that digests a FASTA, or to one that fine-tunes the models on DIA runs.
    /// The grid shows, and the command line carries, only the properties of the run's scopes.
    /// </summary>
    public enum OspreyPropertyScope
    {
        search,
        library,
        digest,
        tuning,
    }

    /// <summary>
    /// One argument of Osprey's command line that is a setting of a run, as
    /// <c>Osprey --help properties</c> describes it: its name, its label and description in the
    /// UI language, when it applies, what it takes, its default and its range.
    /// </summary>
    public sealed class OspreyProperty
    {
        /// <summary>The value of an optional-value integer (<c>--parallel-files</c>) that gives the argument without a value.</summary>
        public const string AUTO_VALUE = @"auto";

        public OspreyProperty(string name, string label, string description, string group, OspreyPropertyScope scope,
            OspreyPropertyType type, IList<string> values, string defaultValue, double? min, double? max, bool optional)
        {
            Name = name;
            Label = label;
            Description = description;
            Group = group;
            Scope = scope;
            Type = type;
            Values = values;
            Default = defaultValue;
            Min = min;
            Max = max;
            Optional = optional;
        }

        /// <summary>The argument's name, without its prefix: <c>run-fdr</c>.</summary>
        public string Name { get; }
        /// <summary>The name shown in the grid, in the UI language.</summary>
        public string Label { get; }
        /// <summary>The usage text, in the UI language: the grid row's tooltip.</summary>
        public string Description { get; }
        /// <summary>The help section's title, in the UI language.</summary>
        public string Group { get; }
        public OspreyPropertyScope Scope { get; }
        public OspreyPropertyType Type { get; }
        /// <summary>The values a <see cref="OspreyPropertyType.list"/> takes.</summary>
        public IList<string> Values { get; }
        /// <summary>The value Osprey uses when the argument is absent, as the command line spells it, or null when Osprey names none.</summary>
        public string Default { get; }
        public double? Min { get; }
        public double? Max { get; }
        /// <summary>True when the argument may be given without a value (<see cref="AUTO_VALUE"/> in the grid).</summary>
        public bool Optional { get; }

        /// <summary>The argument as the command line spells it: <c>--run-fdr</c>.</summary>
        public string ArgumentText => OspreyHelpers.ARG_PREFIX + Name;

        public bool HasDefault => Default != null;

        /// <summary>
        /// The setting the grid edits and the command line reads: a flag is a checkbox; a list a
        /// combo of its values, with a blank choice first when Osprey names no default; anything
        /// else text, blank when Osprey names no default, which the command line leaves out.
        /// The setting is named by the argument, which the audit log and the persisted settings
        /// record; the grid shows <see cref="Label"/>.
        /// </summary>
        public AbstractDdaSearchEngine.Setting CreateSetting()
        {
            switch (Type)
            {
                case OspreyPropertyType.flag:
                    return new AbstractDdaSearchEngine.Setting(Name, false);
                case OspreyPropertyType.list:
                    // A default the list does not spell (an export from another Osprey) is offered first,
                    // so the combo always has a selection
                    var values = !HasDefault ? new[] { string.Empty }.Concat(Values).ToArray()
                        : Values.Contains(Default) ? Values : new[] { Default }.Concat(Values).ToArray();
                    return new AbstractDdaSearchEngine.Setting(Name, Default ?? string.Empty, values);
                default:
                    return new AbstractDdaSearchEngine.Setting(Name, Default ?? string.Empty);
            }
        }

        /// <summary>
        /// The tooltip of the grid row: the argument and its usage text, and for an optional-value
        /// argument how to give it without a value.
        /// </summary>
        public string Tooltip
        {
            get
            {
                var sb = new StringBuilder(ArgumentText).AppendLine().Append(Description);
                if (Optional)
                    sb.AppendLine().Append(string.Format(ModelResources.OspreyProperty_Tooltip_Enter__0__to_give_the_argument_without_a_value_, AUTO_VALUE));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Checks text typed for this property, throwing an <see cref="ArgumentException"/> that
        /// names the property when it is not a value the argument takes: a number within its
        /// range, a listed value, or blank, which leaves the argument out so Osprey uses its own
        /// default.
        /// </summary>
        public void Validate(string text)
        {
            if (IsAbsent(text))
                return;
            switch (Type)
            {
                case OspreyPropertyType.integer:
                    if (!TryParseInt(text, out int i))
                    {
                        throw new ArgumentException(Optional
                            ? string.Format(ModelResources.OspreyProperty_Validate_The_value___0___is_not_valid_for__1___which_must_be_a_whole_number__blank__or___2__, text, Label, AUTO_VALUE)
                            : string.Format(Resources.ValueInvalidIntException_ValueInvalidIntException_The_value___0___is_not_valid_for_the_argument__1__which_requires_an_integer_, text, Label));
                    }
                    CheckRange(i, text);
                    break;
                case OspreyPropertyType.number:
                    if (!TryParseDouble(text, out double d))
                    {
                        throw new ArgumentException(string.Format(
                            Resources.ValueInvalidDoubleException_ValueInvalidDoubleException_The_value___0___is_not_valid_for_the_argument__1__which_requires_a_decimal_number_, text, Label));
                    }
                    CheckRange(d, text);
                    break;
                case OspreyPropertyType.list:
                    if (!Values.Contains(text))
                    {
                        throw new ArgumentException(string.Format(
                            Resources.CommandArgs_ParseArgsInternal_Error____0___is_not_a_valid_value_for__1___It_must_be_one_of_the_following___2_,
                            text, Label, string.Join(@", ", Values)));
                    }
                    break;
            }
        }

        /// <summary>
        /// The value as the command line takes it: a number in the invariant culture, whatever
        /// culture it was typed in; other text as it is. Null when the text is blank, which leaves
        /// the argument out, and empty for an optional-value argument given without a value.
        /// </summary>
        public string ToArgumentValue(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            if (Optional && IsAuto(text))
                return string.Empty;
            switch (Type)
            {
                case OspreyPropertyType.integer:
                    return TryParseInt(text, out int i) ? i.ToString(CultureInfo.InvariantCulture) : text;
                case OspreyPropertyType.number:
                    return TryParseDouble(text, out double d) ? d.ToString(CultureInfo.InvariantCulture) : text;
                default:
                    return text;
            }
        }

        /// <summary>True when <paramref name="text"/> leaves the argument out or, for an optional-value one, gives it without a value.</summary>
        private bool IsAbsent(string text)
        {
            return string.IsNullOrEmpty(text) || (Optional && IsAuto(text));
        }

        private static bool IsAuto(string text)
        {
            return string.Equals(text, AUTO_VALUE, StringComparison.OrdinalIgnoreCase);
        }

        private void CheckRange(double value, string text)
        {
            if ((Min.HasValue && value < Min.Value) || (Max.HasValue && value > Max.Value))
            {
                throw new ArgumentOutOfRangeException(null, string.Format(
                    Resources.ValueOutOfRangeDoubleException_ValueOutOfRangeException_The_value___0___for_the_argument__1__must_be_between__2__and__3__,
                    text, Label, Min.HasValue ? Min.Value.ToString(CultureInfo.CurrentCulture) : string.Empty,
                    Max.HasValue ? Max.Value.ToString(CultureInfo.CurrentCulture) : string.Empty));
            }
        }

        /// <summary>A number as typed in the UI culture, or as a command line spells it.</summary>
        private static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) ||
                   int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                   double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <summary>
    /// The settings of an Osprey run as the installed Osprey declares them, read once per
    /// executable and UI language from <c>Osprey --help properties</c> (see Osprey's
    /// OspreyProperties), so an argument added to Osprey appears in Skyline's Additional
    /// settings grid without a change here. The JSON names the tool and its version, the culture
    /// it was written in, and the properties in help order.
    /// </summary>
    public sealed class OspreyProperties
    {
        public const string HELP_FORMAT = @"properties";

        private static readonly Dictionary<string, OspreyProperties> CACHE = new Dictionary<string, OspreyProperties>();

        private OspreyProperties(string version, string culture, IList<OspreyProperty> properties)
        {
            Version = version;
            Culture = culture;
            Properties = properties;
        }

        public string Version { get; }
        public string Culture { get; }
        /// <summary>The properties in help order, which the grid keeps within each group.</summary>
        public IList<OspreyProperty> Properties { get; }

        public OspreyProperty Find(string name)
        {
            return Properties.FirstOrDefault(p => p.Name == name);
        }

        /// <summary>
        /// The properties of the Osprey at <paramref name="ospreyExe"/> in <paramref name="culture"/>,
        /// asked of it once and kept for the session. Throws <see cref="IOException"/> when it cannot
        /// be run or does not answer with its properties (an Osprey older than the grid).
        /// </summary>
        public static OspreyProperties Load(string ospreyExe, CultureInfo culture)
        {
            // Keyed on the file as well as its path, so an Osprey upgraded in place during the session is read again
            string key = ospreyExe + @"|" + culture.Name + @"|" + (File.Exists(ospreyExe) ? File.GetLastWriteTimeUtc(ospreyExe).Ticks : 0);
            lock (CACHE)
            {
                if (CACHE.TryGetValue(key, out var cached))
                    return cached;
            }
            var properties = Parse(Run(ospreyExe, culture), ospreyExe);
            lock (CACHE)
            {
                CACHE[key] = properties;
            }
            return properties;
        }

        /// <summary>Forgets what was read, for a test that changes the Osprey a path names.</summary>
        public static void ClearCache()
        {
            lock (CACHE)
            {
                CACHE.Clear();
            }
        }

        /// <summary>The JSON <c>Osprey --help properties</c> writes, read as the properties; <paramref name="source"/> names the Osprey in an error.</summary>
        public static OspreyProperties Parse(string json, string source = null)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception x)
            {
                throw new IOException(string.Format(ModelResources.OspreyProperties_Parse_Osprey_at__0__did_not_report_its_settings___1_,
                    source, FirstLine(json)), x);
            }
            var properties = new List<OspreyProperty>();
            foreach (var element in root[@"properties"] ?? Enumerable.Empty<JToken>())
            {
                var scope = ParseEnum((string)element[@"scope"], OspreyPropertyScope.search);
                var type = ParseEnum((string)element[@"type"], OspreyPropertyType.text);
                var values = element[@"values"]?.Select(v => (string)v).ToArray() ?? Array.Empty<string>();
                properties.Add(new OspreyProperty((string)element[@"name"], (string)element[@"label"], (string)element[@"description"],
                    (string)element[@"group"], scope, type, values, (string)element[@"default"],
                    (double?)element[@"min"], (double?)element[@"max"], (bool?)element[@"optional"] ?? false));
            }
            return new OspreyProperties((string)root[@"version"], (string)root[@"culture"], properties);
        }

        private static TEnum ParseEnum<TEnum>(string text, TEnum defaultValue) where TEnum : struct
        {
            return Enum.TryParse(text, out TEnum value) ? value : defaultValue;
        }

        /// <summary>
        /// Runs <c>Osprey --help properties --culture &lt;culture&gt;</c> and returns what it wrote,
        /// or throws when it could not be run or answered anything but its properties (an Osprey from
        /// before the grid answers with a help section error).
        /// </summary>
        private static string Run(string ospreyExe, CultureInfo culture)
        {
            string args = string.Join(@" ", OspreyHelpers.ARG_HELP, HELP_FORMAT, OspreyHelpers.ARG_CULTURE, culture.Name);
            string output;
            int exitCode;
            try
            {
                string exe = PathEx.GetNonUnicodePath(ospreyExe) ?? ospreyExe;
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    // Only stdout is read: reading stderr too, one after the other, deadlocks when
                    // the child fills the other pipe first, and nothing here needs its text
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        throw new IOException(string.Format(ModelResources.OspreyProperties_Run_Osprey_at__0__could_not_be_run_, ospreyExe));
                    output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception x) when (!(x is IOException))
            {
                throw new IOException(string.Format(ModelResources.OspreyProperties_Run_Osprey_at__0__could_not_be_run_, ospreyExe), x);
            }
            if (exitCode != 0 || !output.TrimStart().StartsWith(@"{"))
            {
                throw new IOException(string.Format(ModelResources.OspreyProperties_Parse_Osprey_at__0__did_not_report_its_settings___1_,
                    ospreyExe, FirstLine(output)));
            }
            return output;
        }

        private static string FirstLine(string text)
        {
            string trimmed = (text ?? string.Empty).Trim();
            int end = trimmed.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? trimmed : trimmed.Substring(0, end);
        }
    }
}
