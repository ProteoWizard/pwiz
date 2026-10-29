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

using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using pwiz.Common.CommandLine;
using pwiz.Osprey.Core;

namespace pwiz.Osprey
{
    /// <summary>
    /// The arguments that are settings of a run, as a host presents them on a settings page:
    /// <c>Osprey --help properties</c> prints them as JSON, generated from the declarations in
    /// <see cref="OspreyCommandArgs"/> (every argument with a <see cref="ArgumentBase.PropertyName"/>,
    /// Osprey's own and the CarafeSharp ones it hosts) in the language of <c>--culture</c>, so an
    /// argument added here appears on the host's page without work there. Skyline runs it once
    /// per installed Osprey and language, and builds its Additional settings grid from it.
    /// <para>
    /// Each property: <c>name</c> (the argument's, which the host spells as <c>--name</c>),
    /// <c>label</c> and <c>description</c> (the usage text, for a tooltip) in the current language,
    /// <c>group</c> (the help section's title), <c>scope</c> (<see cref="OspreyPropertyScope"/>:
    /// when it applies), <c>type</c> (<see cref="ArgumentPropertyType"/>), <c>values</c> for a
    /// list, <c>default</c> when the tool has one to name, <c>min</c> and <c>max</c> for a number,
    /// and <c>optional</c> when the argument may be given without a value.
    /// </para>
    /// </summary>
    internal static class OspreyProperties
    {
        /// <summary>The <c>--help</c> value that prints the properties.</summary>
        public const string HELP_FORMAT = @"properties";

        public const string FIELD_TOOL = @"tool";
        public const string FIELD_VERSION = @"version";
        public const string FIELD_CULTURE = @"culture";
        public const string FIELD_PROPERTIES = @"properties";
        public const string FIELD_NAME = @"name";
        public const string FIELD_LABEL = @"label";
        public const string FIELD_DESCRIPTION = @"description";
        public const string FIELD_GROUP = @"group";
        public const string FIELD_SCOPE = @"scope";
        public const string FIELD_TYPE = @"type";
        public const string FIELD_VALUES = @"values";
        public const string FIELD_DEFAULT = @"default";
        public const string FIELD_MIN = @"min";
        public const string FIELD_MAX = @"max";
        public const string FIELD_OPTIONAL = @"optional";

        /// <summary>The properties as JSON, in the current UI culture.</summary>
        public static string Generate()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString(FIELD_TOOL, @"Osprey");
                writer.WriteString(FIELD_VERSION, OspreyVersion.DisplayVersion);
                writer.WriteString(FIELD_CULTURE, CultureInfo.CurrentUICulture.Name);
                writer.WriteStartArray(FIELD_PROPERTIES);
                foreach (var group in OspreyCommandArgs.UsageBlocks.OfType<ArgumentGroup<OspreyCommandArgs>>())
                {
                    foreach (var arg in group.Args.Cast<OspreyArgument>().Where(a => a.PropertyName != null))
                        WriteProperty(writer, arg, group.Title);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteProperty(Utf8JsonWriter writer, OspreyArgument arg, string groupTitle)
        {
            writer.WriteStartObject();
            writer.WriteString(FIELD_NAME, arg.Name);
            writer.WriteString(FIELD_LABEL, arg.PropertyName());
            writer.WriteString(FIELD_DESCRIPTION, arg.Description);
            writer.WriteString(FIELD_GROUP, groupTitle);
            writer.WriteString(FIELD_SCOPE, arg.Scope.ToString());
            var type = arg.EffectivePropertyType;
            writer.WriteString(FIELD_TYPE, type.ToString());
            if (type == ArgumentPropertyType.list)
            {
                writer.WriteStartArray(FIELD_VALUES);
                foreach (string value in arg.Values)
                    writer.WriteStringValue(value);
                writer.WriteEndArray();
            }
            string defaultValue = arg.DefaultValue?.Invoke();
            if (defaultValue != null)
                writer.WriteString(FIELD_DEFAULT, defaultValue);
            if (arg.MinValue.HasValue)
                writer.WriteNumber(FIELD_MIN, arg.MinValue.Value);
            if (arg.MaxValue.HasValue)
                writer.WriteNumber(FIELD_MAX, arg.MaxValue.Value);
            if (arg.OptionalValue)
                writer.WriteBoolean(FIELD_OPTIONAL, true);
            writer.WriteEndObject();
        }
    }
}
