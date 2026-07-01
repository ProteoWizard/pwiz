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
using System.Linq;
using System.Text;
using pwiz.Common.Chemistry;

namespace pwiz.Skyline.Util.Nomenclature
{
    /// <summary>
    /// A small mutable accumulator of element counts, shared by the residue-sum and structure-layer
    /// nomenclature interpreters. Elements are added (with an optional multiplier), zero totals are
    /// dropped, and the result is rendered as a Hill-order formula string that Skyline's existing
    /// formula parser (<see cref="ParsedMolecule"/> / <see cref="Molecule"/>) understands.
    /// </summary>
    internal class ElementCounts
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public bool IsEmpty { get { return _counts.Count == 0; } }

        /// <summary>
        /// Adds (or, for a negative delta, subtracts) a count of a single element; an element whose
        /// running total reaches zero is removed.
        /// </summary>
        public void Add(string element, int delta)
        {
            _counts.TryGetValue(element, out var existing);
            var updated = existing + delta;
            if (updated == 0)
            {
                _counts.Remove(element);
            }
            else
            {
                _counts[element] = updated;
            }
        }

        /// <summary>
        /// Parses a plain chemical formula token (for example "C6H10O5") and adds each element,
        /// multiplied by <paramref name="multiplier"/>.
        /// </summary>
        public void AddFormula(string formula, int multiplier = 1)
        {
            foreach (var pair in Molecule.ParseToDictionary(formula))
            {
                Add(pair.Key, multiplier * pair.Value);
            }
        }

        /// <summary>
        /// Renders the accumulated counts as a Hill-order formula string: carbon first, hydrogen
        /// second, then remaining elements alphabetically. Counts of one are written without a digit.
        /// </summary>
        public string ToHillFormula()
        {
            var sb = new StringBuilder();
            AppendElement(sb, BioMassCalc.C);
            AppendElement(sb, BioMassCalc.H);
            foreach (var element in _counts.Keys
                         .Where(e => !Equals(e, BioMassCalc.C) && !Equals(e, BioMassCalc.H))
                         .OrderBy(e => e, StringComparer.Ordinal))
            {
                AppendElement(sb, element);
            }
            return sb.ToString();
        }

        private void AppendElement(StringBuilder sb, string element)
        {
            if (!_counts.TryGetValue(element, out var count) || count == 0)
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
