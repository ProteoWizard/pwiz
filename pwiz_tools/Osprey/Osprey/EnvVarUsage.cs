/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Common.CommandLine;
using pwiz.Osprey.Core;

// The environment variables are developer switches, deliberately off the command line, and their
// catalog is developer text: not translated, as diagnostics reached through an OSPREY_* setting
// are not (see "Localization" in docs/20-command-line.md).
// ReSharper disable LocalizableElement

namespace pwiz.Osprey
{
    /// <summary>
    /// The catalog of every <see cref="EnvVar"/> this build declares, and the text that presents it:
    /// <c>--help-env</c>, rendered in the tables <c>--help</c> uses, one per
    /// <see cref="EnvVarGroup"/>, and the startup line naming every switch that is set.
    /// </summary>
    internal static class EnvVarUsage
    {
        private static readonly Lazy<IReadOnlyList<EnvVar>> CATALOG =
            new Lazy<IReadOnlyList<EnvVar>>(() => EnvVarCatalog.Collect(typeof(EnvVarUsage).Assembly));

        /// <summary>Every declaration in Osprey and the assemblies it references, by name.</summary>
        public static IReadOnlyList<EnvVar> Catalog
        {
            get { return CATALOG.Value; }
        }

        /// <summary>
        /// The catalog as <c>--help-env</c> prints it: an introduction, then a bordered table per
        /// group. <paramref name="formatType"/> is <c>ascii</c> for lower-128 borders, otherwise
        /// unicode, as for <c>--help</c>.
        /// </summary>
        public static string BuildUsage(string formatType, int width)
        {
            var sb = new StringBuilder();
            sb.Append(ConsoleTable.ParaToString(width,
                @"Environment variables Osprey reads. These are developer switches, kept off the command " +
                @"line on purpose: debugging dumps, A/B arms and experiments. Any other OSPREY_* variable " +
                @"stops Osprey at startup, as an unknown argument does, and so does a value a switch with " +
                @"listed values does not list.", true));
            bool ascii = string.Equals(formatType, ArgUsage.FORMAT_ASCII, StringComparison.OrdinalIgnoreCase);
            foreach (EnvVarGroup group in Enum.GetValues(typeof(EnvVarGroup)))
            {
                var entries = Catalog.Where(e => e.Group == group).ToList();
                if (entries.Count == 0)
                    continue;
                var table = new ConsoleTable
                {
                    Title = GroupTitle(group),
                    Borders = true,
                    Ascii = ascii,
                    Width = width,
                };
                table.SetHeaders(@"Variable", @"Description");
                foreach (var entry in entries)
                    table.AddRow(entry.Usage, Describe(entry));
                int left = Math.Min(width - 30, entries.Max(e => e.Usage.Length));
                table.Widths = new[] { left, width - left - 3 };   // 3 borders
                sb.Append(table);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The startup line naming every catalogued switch that is set, with its value, so a log
        /// answers "which arm was this run?" without inference. Null when none is set, so the log
        /// of a run that sets nothing is unchanged.
        /// </summary>
        public static string DescribeSetVariables()
        {
            var set = EnvVarCatalog.SetVariables(Catalog);
            if (set.Count == 0)
                return null;
            return @"Environment switches: " + string.Join(@", ", set.Select(e => e.Name + @"=" + e.Value));
        }

        /// <summary>
        /// The refusal of every set OSPREY_* name the catalog does not hold, naming the closest
        /// catalogued name when one is near enough to be a misspelling of it; null when there is none.
        /// </summary>
        public static string DescribeUnrecognizedVariables(string helpEnvArgument)
        {
            var unrecognized = EnvVarCatalog.UnrecognizedVariables(Catalog);
            if (unrecognized.Count == 0)
                return null;
            var names = unrecognized.Select(name =>
            {
                string suggestion = EnvVarCatalog.SuggestName(name, Catalog);
                return suggestion == null ? name : string.Format(@"{0} (did you mean {1}?)", name, suggestion);
            });
            return string.Format(
                @"Osprey does not recognize the environment variable(s) {0}. Unset them. A variable that " +
                @"was removed no longer has any effect, and one that only Rust osprey reads is never " +
                @"honored here. Run {1} for the full list.",
                string.Join(@", ", names), helpEnvArgument);
        }

        private static string Describe(EnvVar entry)
        {
            return entry.DefaultValue == null
                ? entry.Description
                : string.Format(@"{0} Default: {1}.", entry.Description, entry.DefaultValue);
        }

        private static string GroupTitle(EnvVarGroup group)
        {
            switch (group)
            {
                case EnvVarGroup.behavior:
                    return @"Behavior - changes the reported results";
                case EnvVarGroup.allowance:
                    return @"Allowances - relax a check that would otherwise stop the run";
                case EnvVarGroup.performance:
                    return @"Performance - changes how the work is done, not what it produces";
                case EnvVarGroup.harness:
                    return @"Harness - stops early or limits the work; the output is incomplete";
                default:
                    return @"Diagnostics - extra files or log lines; the output is unchanged";
            }
        }
    }
}
