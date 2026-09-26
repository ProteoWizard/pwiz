/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on alphabase (https://github.com/MannLabs/alphabase), Apache-2.0
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
using System.Globalization;
using System.IO;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// alphabase 1.2.1's modification table (<c>alphabase/constants/const_files/modification.tsv</c>,
    /// Apache-2.0, embedded verbatim), loaded the way alphabase's <c>load_mod_df</c> loads it:
    /// every name containing a space is also registered with the spaces replaced by
    /// underscores, and the first row wins for a duplicated name.
    /// </summary>
    public static class ModificationTable
    {
        private const string RESOURCE_NAME = @"pwiz.CarafeSharp.Core.Resources.alphabase_modification.tsv";

        private static readonly Lazy<IReadOnlyList<ModificationDefinition>> DEFINITIONS =
            new Lazy<IReadOnlyList<ModificationDefinition>>(LoadDefinitions);

        private static readonly Lazy<Dictionary<string, ModificationDefinition>> BY_NAME =
            new Lazy<Dictionary<string, ModificationDefinition>>(IndexByName);

        /// <summary>All definitions in alphabase's MOD_DF order, underscore aliases last.</summary>
        public static IReadOnlyList<ModificationDefinition> All
        {
            get { return DEFINITIONS.Value; }
        }

        public static bool TryGet(string name, out ModificationDefinition definition)
        {
            return BY_NAME.Value.TryGetValue(name, out definition);
        }

        public static ModificationDefinition Get(string name)
        {
            if (!TryGet(name, out var definition))
                throw new ArgumentException(string.Format(@"Unknown alphabase modification '{0}'.", name), nameof(name));
            return definition;
        }

        private static IReadOnlyList<ModificationDefinition> LoadDefinitions()
        {
            var rows = new List<ModificationDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var spaced = new List<ModificationDefinition>();
            using (var stream = typeof(ModificationTable).Assembly.GetManifestResourceStream(RESOURCE_NAME))
            {
                if (stream == null)
                    throw new InvalidOperationException(string.Format(@"Missing embedded resource {0}.", RESOURCE_NAME));
                using (var reader = new StreamReader(stream))
                {
                    string[] header = reader.ReadLine()?.Split('\t') ?? Array.Empty<string>();
                    int iName = Array.IndexOf(header, @"mod_name");
                    int iComposition = Array.IndexOf(header, @"composition");
                    int iLossComposition = Array.IndexOf(header, @"modloss_composition");
                    int iUnimod = Array.IndexOf(header, @"unimod_id");
                    int iImportance = Array.IndexOf(header, @"modloss_importance");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0)
                            continue;
                        string[] fields = line.Split('\t');
                        var definition = new ModificationDefinition(fields[iName],
                            ChemicalFormula.Parse(fields[iComposition]),
                            ChemicalFormula.Parse(fields[iLossComposition]),
                            ParseInt(fields[iUnimod]),
                            ParseDouble(fields[iImportance]));
                        if (definition.Name.IndexOf(' ') >= 0)
                            spaced.Add(definition);
                        if (seen.Add(definition.Name))
                            rows.Add(definition);
                    }
                }
            }
            // pandas concat of the table and its underscore copies, then drop_duplicates keeping
            // the first occurrence of each name.
            foreach (var definition in spaced)
            {
                string alias = definition.Name.Replace(' ', '_');
                if (seen.Add(alias))
                {
                    rows.Add(new ModificationDefinition(alias, definition.Composition,
                        definition.ModLossComposition, definition.UnimodId, definition.ModLossImportance));
                }
            }
            return rows;
        }

        private static Dictionary<string, ModificationDefinition> IndexByName()
        {
            var index = new Dictionary<string, ModificationDefinition>(StringComparer.Ordinal);
            foreach (var definition in DEFINITIONS.Value)
                index[definition.Name] = definition;
            return index;
        }

        private static int ParseInt(string text)
        {
            return string.IsNullOrEmpty(text) ? 0 : (int)double.Parse(text, CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string text)
        {
            return string.IsNullOrEmpty(text) ? 0 : double.Parse(text, CultureInfo.InvariantCulture);
        }
    }
}
