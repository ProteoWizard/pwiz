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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The instruments AlphaPeptDeep's pretrained MS2 model predicts for: peptdeep's instrument
    /// grouping (<c>settings['model_mgr']['instrument_group']</c>), which maps an instrument's name
    /// to one of the five groups the model was trained with, plus CarafeSharp's names for a
    /// Stellar and for TribridOT (a Tribrid's Orbitrap). The names are what a command line or a
    /// settings page offers (<see cref="NAMES"/>); any other name predicts as a Lumos.
    /// </summary>
    public static class PeptdeepInstruments
    {
        public const string GROUP_QE = @"QE";
        public const string GROUP_LUMOS = @"Lumos";
        public const string GROUP_TIMSTOF = @"timsTOF";
        public const string GROUP_SCIEXTOF = @"SciexTOF";
        public const string GROUP_THERMOTOF = @"ThermoTOF";

        /// <summary>The groups the pretrained model was trained with, in its embedding order.</summary>
        public static readonly IReadOnlyList<string> GROUPS = new[] { GROUP_QE, GROUP_LUMOS, GROUP_TIMSTOF, GROUP_SCIEXTOF, GROUP_THERMOTOF };

        public const string DEFAULT_GROUP = GROUP_LUMOS;

        // The names and their groups, in the order the names are offered: by group, the group's
        // own name first.
        private static readonly KeyValuePair<string, string>[] NAME_GROUPS =
        {
            new KeyValuePair<string, string>(@"QE", GROUP_QE),
            new KeyValuePair<string, string>(@"QE+", GROUP_QE),
            new KeyValuePair<string, string>(@"QEHF", GROUP_QE),
            new KeyValuePair<string, string>(@"QEHFX", GROUP_QE),
            new KeyValuePair<string, string>(@"Exploris", GROUP_QE),
            new KeyValuePair<string, string>(@"Exploris480", GROUP_QE),
            new KeyValuePair<string, string>(@"Lumos", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"Fusion", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"Eclipse", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"Astral", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"Stellar", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"Velos", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"Elite", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"OrbitrapTribrid", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"ThermoTribrid", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"TribridOT", GROUP_LUMOS),
            new KeyValuePair<string, string>(@"timsTOF", GROUP_TIMSTOF),
            new KeyValuePair<string, string>(@"SciexTOF", GROUP_SCIEXTOF),
            new KeyValuePair<string, string>(@"ThermoTOF", GROUP_THERMOTOF),
        };

        private static readonly Dictionary<string, string> GROUP_BY_UPPER_NAME =
            NAME_GROUPS.ToDictionary(kvp => kvp.Key.ToUpperInvariant(), kvp => kvp.Value, StringComparer.Ordinal);

        /// <summary>The instrument names, as a command line or a settings page offers them.</summary>
        public static readonly IReadOnlyList<string> NAMES = NAME_GROUPS.Select(kvp => kvp.Key).ToArray();

        /// <summary>
        /// The group peptdeep's ModelManager assigns to an instrument name, matched without
        /// regard to case (Eclipse and Astral are Lumos, Exploris is QE); <see cref="DEFAULT_GROUP"/>
        /// for a name it does not know, or none.
        /// </summary>
        public static string GetGroup(string instrumentName)
        {
            if (string.IsNullOrEmpty(instrumentName))
                return DEFAULT_GROUP;
            return GROUP_BY_UPPER_NAME.TryGetValue(instrumentName.ToUpperInvariant(), out string group) ? group : DEFAULT_GROUP;
        }
    }
}
