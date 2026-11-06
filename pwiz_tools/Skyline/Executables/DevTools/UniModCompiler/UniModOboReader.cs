/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace UniModCompiler
{
    /// <summary>
    /// Reads pwiz/data/common/unimod.obo, the UniMod snapshot the rest of ProteoWizard uses,
    /// into the same <see cref="mod_t"/> objects the compiler once deserialized from the retired
    /// unimod.xml (replaced by unimod.obo in #4990/#4991).
    /// The obo carries everything the compiler needs except two things unimod.xml spelled out:
    /// the atoms of the composition "bricks" (Hex, HexNAc, ...) and the element order UniMod
    /// uses when it expands a composition. Both are fixed UniMod definitions, held below.
    /// </summary>
    public static class UniModOboReader
    {
        /// <summary>
        /// UniMod's element table order (the umod:elements list of the retired unimod.xml). An
        /// expanded composition lists its atoms in this order, which is the order the generated
        /// formulas keep.
        /// </summary>
        private static readonly string[] ELEMENT_ORDER =
        {
            "H", "2H", "Li", "C", "13C", "N", "15N", "O", "18O", "F", "Na", "P", "S", "Cl", "K", "Ca",
            "Fe", "Ni", "Zn", "Se", "Br", "Ag", "Hg", "Au", "I", "Mo", "Cu", "e", "B", "As", "Cd",
            "Cr", "Co", "Mn", "Mg", "Pd", "Al", "Pt", "Ru", "Si"
        };

        /// <summary>
        /// UniMod's composition bricks that are not single elements (the umod:mod_bricks list of
        /// the retired unimod.xml).
        /// </summary>
        private static readonly Dictionary<string, string> BRICKS = new Dictionary<string, string>
        {
            { "Hex", "H(10) C(6) O(5)" },
            { "HexNAc", "H(13) C(8) N O(5)" },
            { "Ac", "H(2) C(2) O" },
            { "dHex", "H(10) C(6) O(4)" },
            { "HexA", "H(8) C(6) O(6)" },
            { "Kdn", "H(14) C(9) O(8)" },
            { "Kdo", "H(12) C(8) O(7)" },
            { "Me", "H(2) C" },
            { "NeuAc", "H(17) C(11) N O(8)" },
            { "NeuGc", "H(17) C(11) N O(9)" },
            { "Water", "H(2) O" },
            { "Phos", "H P O(3)" },
            { "Sulf", "S O(3)" },
            { "Pent", "H(8) C(5) O(4)" },
            { "Hep", "H(12) C(7) O(6)" },
            { "HexN", "H(11) C(6) O(4) N" },
        };

        private static readonly Regex XREF = new Regex(@"^xref: (\S+) ""(.*)""$");
        private static readonly Regex SPEC_KEY = new Regex(@"^spec_(\d+)_(.+)$");
        private static readonly Regex LOSS_KEY = new Regex(@"^neutral_loss_(\d+)_(.+)$");
        private static readonly Regex COMPOSITION_TOKEN = new Regex(@"^([A-Za-z0-9]+?)(?:\((-?\d+)\))?$");

        public static mod_t[] Read(string path)
        {
            var mods = new List<mod_t>();
            mod_t mod = null;
            List<specificity_t> specs = null;
            List<NeutralLoss_t> losses = null;
            foreach (var line in File.ReadLines(path))
            {
                if (line == "[Term]" || line == "[Typedef]")
                {
                    Complete(mod, specs, losses, mods);
                    mod = null;
                    if (line == "[Term]")
                    {
                        mod = new mod_t();
                        specs = new List<specificity_t>();
                        losses = null;
                    }
                    continue;
                }
                if (mod == null)
                    continue;
                if (line.StartsWith("name: "))
                {
                    mod.title = line.Substring("name: ".Length);
                    continue;
                }
                var match = XREF.Match(line);
                if (!match.Success)
                    continue;
                string key = match.Groups[1].Value, value = match.Groups[2].Value;
                switch (key)
                {
                    case "record_id":
                        mod.record_id = long.Parse(value, CultureInfo.InvariantCulture);
                        mod.record_idSpecified = true;
                        break;
                    case "delta_mono_mass":
                        mod.delta ??= new composition_t();
                        mod.delta.mono_mass = ParseDouble(value);
                        break;
                    case "delta_avge_mass":
                        mod.delta ??= new composition_t();
                        mod.delta.avge_mass = ParseDouble(value);
                        break;
                    case "delta_composition":
                        mod.delta ??= new composition_t();
                        mod.delta.composition = value;
                        mod.delta.element = ParseComposition(value);
                        break;
                    case "approved":
                        mod.approved = value == "1";
                        break;
                    default:
                        var specMatch = SPEC_KEY.Match(key);
                        if (specMatch.Success)
                            losses = AddSpecField(specs, losses, specMatch.Groups[1].Value, specMatch.Groups[2].Value, value);
                        break;
                }
            }
            Complete(mod, specs, losses, mods);
            return mods.ToArray();
        }

        /// <summary>
        /// Applies one spec_N_* field. A specificity starts at its spec_N_group field and a
        /// neutral loss at its neutral_loss_M_mono_mass field: the obo repeats spec_N_* keys
        /// for every site in a site group (Phospho's S and T are both spec_1), so the key number
        /// alone cannot tell one specificity from the next.
        /// </summary>
        private static List<NeutralLoss_t> AddSpecField(List<specificity_t> specs, List<NeutralLoss_t> losses,
            string group, string field, string value)
        {
            if (field == "group")
            {
                CompleteLosses(specs, losses);
                specs.Add(new specificity_t { spec_group = group });
                return new List<NeutralLoss_t>();
            }
            var spec = specs.Last();
            var lossMatch = LOSS_KEY.Match(field);
            if (lossMatch.Success)
            {
                string lossField = lossMatch.Groups[2].Value;
                if (lossField == "mono_mass")
                    losses.Add(new NeutralLoss_t { mono_mass = ParseDouble(value), mono_massSpecified = true });
                var loss = losses.Last();
                switch (lossField)
                {
                    case "avge_mass":
                        loss.avge_mass = ParseDouble(value);
                        loss.avge_massSpecified = true;
                        break;
                    case "flag":
                        loss.flag = value == "true";
                        break;
                    case "composition":
                        loss.composition = value;
                        loss.element = value == "0" ? new elem_ref_t[0] : ParseComposition(value);
                        break;
                }
                return losses;
            }
            switch (field)
            {
                case "hidden":
                    spec.hidden = value == "1";
                    break;
                case "site":
                    spec.site = value;
                    break;
                case "position":
                    spec.position = ParseXmlEnum<position_t>(value);
                    break;
                case "classification":
                    spec.classification = ParseXmlEnum<classification_t>(value);
                    break;
                case "misc_notes":
                    spec.misc_notes = value;
                    break;
            }
            return losses;
        }

        private static void CompleteLosses(List<specificity_t> specs, List<NeutralLoss_t> losses)
        {
            if (specs.Count > 0 && losses != null && losses.Count > 0)
                specs.Last().NeutralLoss = losses.ToArray();
        }

        private static void Complete(mod_t mod, List<specificity_t> specs, List<NeutralLoss_t> losses, List<mod_t> mods)
        {
            // The root node (UNIMOD:0) has no record_id and is not a modification
            if (mod == null || !mod.record_idSpecified)
                return;
            CompleteLosses(specs, losses);
            mod.specificity = specs.ToArray();
            mods.Add(mod);
        }

        /// <summary>
        /// Expands a UniMod composition ("H(-1) C(2) Hex(2)") into its atoms, summed per element
        /// and listed in UniMod's element order, as the retired unimod.xml listed them.
        /// </summary>
        public static elem_ref_t[] ParseComposition(string composition)
        {
            var counts = new Dictionary<string, int>();
            AddComposition(composition, 1, counts);
            foreach (var symbol in counts.Keys.Where(s => !ELEMENT_ORDER.Contains(s)))
                throw new InvalidDataException(string.Format("Unknown element {0} in composition {1}", symbol, composition));
            return ELEMENT_ORDER.Where(s => counts.TryGetValue(s, out int count) && count != 0)
                .Select(s => new elem_ref_t { symbol = s, number = counts[s].ToString(CultureInfo.InvariantCulture) })
                .ToArray();
        }

        private static void AddComposition(string composition, int multiplier, Dictionary<string, int> counts)
        {
            foreach (var token in composition.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = COMPOSITION_TOKEN.Match(token);
                if (!match.Success)
                    throw new InvalidDataException(string.Format("Unexpected token {0} in composition {1}", token, composition));
                string symbol = match.Groups[1].Value;
                int count = multiplier * (match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1);
                if (BRICKS.TryGetValue(symbol, out var brick))
                    AddComposition(brick, count, counts);
                else
                {
                    counts.TryGetValue(symbol, out int current);
                    counts[symbol] = current + count;
                }
            }
        }

        private static double ParseDouble(string value)
        {
            return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static T ParseXmlEnum<T>(string value) where T : struct, Enum
        {
            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = field.GetCustomAttribute<XmlEnumAttribute>();
                if (Equals(attr?.Name ?? field.Name, value))
                    return (T)field.GetValue(null);
            }
            throw new InvalidDataException(string.Format("Unexpected {0} value {1}", typeof(T).Name, value));
        }
    }
}
