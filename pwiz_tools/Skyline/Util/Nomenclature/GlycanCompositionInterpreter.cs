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
using System.Text.RegularExpressions;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util.Nomenclature
{
    /// <summary>
    /// Derives a molecular formula from a glycan composition string such as "HexNAc2Hex3" or the
    /// equivalent building-block(count) form "Hex(3)HexNAc(2)". Offline, deterministic, and
    /// topology-independent (branching does not change the formula). Each monosaccharide contributes
    /// its residue (monomer minus water) formula; the free reducing end adds one water.
    /// Residue formulas are validated against the ionsource carbohydrate table, ExPASy GlycoMod,
    /// and PSI ProForma 2.0.
    /// </summary>
    public class GlycanCompositionInterpreter : INomenclatureInterpreter
    {
        public const string MODE_KEY = "glycan";

        // Residue = monosaccharide monomer minus H2O. Total glycan = sum(residues) + one H2O.
        private static readonly IDictionary<string, string> RESIDUE_FORMULAS =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { @"HexNAc", @"C8H13NO5" }, { @"GlcNAc", @"C8H13NO5" }, { @"GalNAc", @"C8H13NO5" },
                { @"Neu5Ac", @"C11H17NO8" }, { @"NeuAc", @"C11H17NO8" }, { @"NeuNAc", @"C11H17NO8" },
                { @"Neu5Gc", @"C11H17NO9" }, { @"NeuGc", @"C11H17NO9" },
                { @"HexA", @"C6H8O6" }, { @"GlcA", @"C6H8O6" },
                { @"dHex", @"C6H10O4" }, { @"Fuc", @"C6H10O4" },
                { @"Hex", @"C6H10O5" }, { @"Glc", @"C6H10O5" }, { @"Gal", @"C6H10O5" }, { @"Man", @"C6H10O5" },
                { @"Pent", @"C5H8O4" }, { @"Xyl", @"C5H8O4" }, { @"Ara", @"C5H8O4" },
                { @"Sulfo", @"O3S" }, { @"Phospho", @"HO3P" }
            };

        // One "<symbol>[<count>]" unit anchored at the current position. Symbols are listed
        // longest-first so, for example, "HexNAc" wins over "Hex" and "HexA" over "Hex". The count
        // is either bare digits ("Hex3") or parenthesized ("Hex(3)"); absent means one.
        private static readonly Regex REGEX_UNIT = new Regex(
            @"\G(?<sym>Neu5Ac|Neu5Gc|HexNAc|GlcNAc|GalNAc|NeuNAc|Phospho|Sulfo|NeuAc|NeuGc|HexA|GlcA|dHex|Hex|Glc|Gal|Man|Fuc|Pent|Xyl|Ara)(?:\((?<cnt>\d+)\)|(?<cnt>\d+))?",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex REGEX_WHITESPACE = new Regex(@"\s+",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public string ModeKey { get { return MODE_KEY; } }

        public bool CanHandle(string text)
        {
            return TryTokenize(text, out _);
        }

        public ParsedMolecule Derive(string text, out MoleculeAccessionNumbers idsFound)
        {
            idsFound = MoleculeAccessionNumbers.EMPTY;
            if (!TryTokenize(text, out var units))
            {
                throw new InvalidDataException(text); // Caller composes the localized message
            }

            var elements = new ElementCounts();
            foreach (var unit in units)
            {
                elements.AddFormula(RESIDUE_FORMULAS[unit.Symbol], unit.Count);
            }
            elements.AddFormula(@"H2O"); // Free reducing end

            return ParsedMolecule.Create(elements.ToHillFormula());
        }

        private static bool TryTokenize(string text, out List<GlycanUnit> units)
        {
            units = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var stripped = REGEX_WHITESPACE.Replace(text.Trim(), string.Empty);
            var result = new List<GlycanUnit>();
            var pos = 0;
            while (pos < stripped.Length)
            {
                var match = REGEX_UNIT.Match(stripped, pos);
                if (!match.Success || match.Index != pos)
                {
                    return false; // Unrecognized token; not a glycan composition
                }
                var count = match.Groups[@"cnt"].Success
                    ? int.Parse(match.Groups[@"cnt"].Value, CultureInfo.InvariantCulture)
                    : 1;
                result.Add(new GlycanUnit(match.Groups[@"sym"].Value, count));
                pos += match.Length;
            }

            if (result.Count == 0)
            {
                return false;
            }
            units = result;
            return true;
        }

        private struct GlycanUnit
        {
            public GlycanUnit(string symbol, int count)
            {
                Symbol = symbol;
                Count = count;
            }
            public string Symbol { get; }
            public int Count { get; }
        }
    }
}
