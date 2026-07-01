/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util.Nomenclature
{
    /// <summary>
    /// Derives a molecular formula by reading the formula layer of a standard InChI string (the
    /// text between "InChI=1S/" and the next "/"). Offline and deterministic. Handles multi-component
    /// formulas (dot-separated, with optional integer multipliers, for example "2C6H12O6.H2O") and
    /// the mobile-proton "/p" layer. The "/q" net-charge layer is preserved in the retained InChI
    /// but does not alter the neutral composition reported here; charge is expressed separately
    /// through Skyline's adduct handling.
    /// </summary>
    public class InChiFormulaInterpreter : INomenclatureInterpreter
    {
        public const string MODE_KEY = "inchi";

        private const string PREFIX_STANDARD = "InChI=1S/";
        private const string PREFIX_NONSTANDARD = "InChI=1/";

        // A component of the formula layer: an optional integer multiplier followed by the formula,
        // for example "2C6H12O6" or "H2O".
        private static readonly Regex REGEX_COMPONENT = new Regex(@"^(\d*)([A-Za-z].*)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Signed integers within a "/p" proton layer, for example "-2" in "p-2" or the two values
        // in the per-component form "p-2;+1".
        private static readonly Regex REGEX_SIGNED_INT = new Regex(@"[+-]?\d+",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public string ModeKey { get { return MODE_KEY; } }

        public bool CanHandle(string text)
        {
            return PrefixLength(text) > 0;
        }

        public ParsedMolecule Derive(string text, out MoleculeAccessionNumbers idsFound)
        {
            idsFound = MoleculeAccessionNumbers.EMPTY;
            var trimmed = (text ?? string.Empty).Trim();
            var prefixLen = PrefixLength(trimmed);
            if (prefixLen == 0)
            {
                throw new InvalidDataException(text); // Not an InChI; caller composes the localized message
            }

            var layers = trimmed.Substring(prefixLen).Split('/');
            var formulaLayer = layers.Length > 0 ? layers[0] : string.Empty;
            if (string.IsNullOrEmpty(formulaLayer))
            {
                throw new InvalidDataException(text);
            }

            // Sum element counts across dot-separated components, honoring integer multipliers.
            var elementCounts = new Dictionary<string, int>();
            foreach (var component in formulaLayer.Split('.'))
            {
                var match = REGEX_COMPONENT.Match(component);
                if (!match.Success)
                {
                    throw new InvalidDataException(text);
                }
                var multiplier = match.Groups[1].Value.Length == 0
                    ? 1
                    : int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                foreach (var pair in Molecule.ParseToDictionary(match.Groups[2].Value))
                {
                    AddCount(elementCounts, pair.Key, multiplier * pair.Value);
                }
            }

            // Apply the mobile-proton "/p" layer (protons added to, or removed from, the formula).
            var protonDelta = ProtonDelta(layers);
            if (protonDelta != 0)
            {
                AddCount(elementCounts, BioMassCalc.H, protonDelta);
            }

            var formula = ToHillFormula(elementCounts);
            if (string.IsNullOrEmpty(formula))
            {
                throw new InvalidDataException(text);
            }

            idsFound = new MoleculeAccessionNumbers(new Dictionary<string, string>
            {
                { MoleculeAccessionNumbers.TagInChI, trimmed }
            });

            return ParsedMolecule.Create(formula);
        }

        private static int PrefixLength(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            var trimmed = text.Trim();
            if (trimmed.StartsWith(PREFIX_STANDARD, StringComparison.Ordinal))
            {
                return PREFIX_STANDARD.Length;
            }
            if (trimmed.StartsWith(PREFIX_NONSTANDARD, StringComparison.Ordinal))
            {
                return PREFIX_NONSTANDARD.Length;
            }
            return 0;
        }

        private static void AddCount(IDictionary<string, int> counts, string element, int delta)
        {
            counts.TryGetValue(element, out var existing);
            var updated = existing + delta;
            if (updated == 0)
            {
                counts.Remove(element);
            }
            else
            {
                counts[element] = updated;
            }
        }

        private static int ProtonDelta(IEnumerable<string> layers)
        {
            var delta = 0;
            foreach (var layer in layers)
            {
                // The proton layer begins with 'p' followed by a sign or digit, for example "p-2".
                // Do not confuse it with the 'h' hydrogen layer or stereo layers.
                if (layer.Length >= 2 && layer[0] == 'p' &&
                    (layer[1] == '+' || layer[1] == '-' || char.IsDigit(layer[1])))
                {
                    foreach (Match m in REGEX_SIGNED_INT.Matches(layer.Substring(1)))
                    {
                        delta += int.Parse(m.Value, CultureInfo.InvariantCulture);
                    }
                }
            }
            return delta;
        }

        private static string ToHillFormula(IDictionary<string, int> counts)
        {
            // Hill order: carbon first, hydrogen second, then remaining elements alphabetically.
            var sb = new StringBuilder();
            AppendElement(sb, counts, BioMassCalc.C);
            AppendElement(sb, counts, BioMassCalc.H);
            foreach (var element in counts.Keys
                         .Where(e => !Equals(e, BioMassCalc.C) && !Equals(e, BioMassCalc.H))
                         .OrderBy(e => e, StringComparer.Ordinal))
            {
                AppendElement(sb, counts, element);
            }
            return sb.ToString();
        }

        private static void AppendElement(StringBuilder sb, IDictionary<string, int> counts, string element)
        {
            if (!counts.TryGetValue(element, out var count) || count == 0)
            {
                return;
            }
            sb.Append(element);
            if (count != 1)
            {
                sb.Append(count.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}
