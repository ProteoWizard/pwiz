/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/input/CParameter.java
 *   and CModification.java (getPTMs, addVarMods)
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
using System.Linq;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Carafe's modification options, as comma-separated <see cref="CarafeModification"/> ids,
    /// with CParameter's defaults: Carbamidomethyl C fixed, Oxidation M variable, at most one
    /// variable modification per peptide and one modification per site.
    /// </summary>
    public sealed class ModificationSettings
    {
        public const string DEFAULT_FIXED_MODIFICATIONS = @"1";
        public const string DEFAULT_VARIABLE_MODIFICATIONS = @"2";

        /// <summary>Carafe's <c>-fixMod</c>: ids, or "0" for none.</summary>
        public string FixedModifications { get; set; } = DEFAULT_FIXED_MODIFICATIONS;

        /// <summary>Carafe's <c>-varMod</c>: ids, or "0" or "no" for none.</summary>
        public string VariableModifications { get; set; } = DEFAULT_VARIABLE_MODIFICATIONS;

        /// <summary>Carafe's <c>-maxVar</c>.</summary>
        public int MaxVariableModifications { get; set; } = 1;

        /// <summary>CParameter's <c>maxModsPerAA</c>, which has no command-line option.</summary>
        public int MaxModificationsPerSite { get; set; } = 1;

        /// <summary>
        /// The fixed modifications. As in Carafe, only "0" means none; "no" is not a number and
        /// fails, unlike for the variable modifications.
        /// </summary>
        public IReadOnlyList<CarafeModification> GetFixedModifications()
        {
            return string.Equals(FixedModifications, @"0", StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<CarafeModification>()
                : ParseIds(FixedModifications);
        }

        public IReadOnlyList<CarafeModification> GetVariableModifications()
        {
            return string.Equals(VariableModifications, @"0", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(VariableModifications, @"no", StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<CarafeModification>()
                : ParseIds(VariableModifications);
        }

        private static IReadOnlyList<CarafeModification> ParseIds(string ids)
        {
            return JavaText.Split(ids, ',')
                .Select(id => CarafeModification.GetById(int.Parse(id, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)))
                .ToArray();
        }
    }
}
