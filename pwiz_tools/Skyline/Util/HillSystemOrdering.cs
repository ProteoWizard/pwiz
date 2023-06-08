/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;
using pwiz.Common.Chemistry;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Implements ToOrderedString(Molecule molecule, string orderOverride) which performs
    /// ToString on a molecule, but enforcing Hill System order (C, H, then alphabetically)
    /// or a custom order based on an order override string like "Cl3N2D" (so Cl, N, H, D, then alphabetically -
    /// note that it understands the isotope notation "D" and includes H in the sort order)
    /// </summary>
    public class HillSystemOrdering : IComparer<string>
    {
        internal static HillSystemOrdering DEFAULT_ORDER = new HillSystemOrdering(null)
        {
            _elementOrder = new Dictionary<string, int>()
                {
                    { @"C", 0 }, { @"C'", 1 }, { @"C\", 2 },
                    { @"H", 3 }, { @"H'", 4 }, { @"D", 5 }, { @"H\", 6 }, { @"T", 7 }
                },
            _checkIsotopes = false
        };

        private HillSystemOrdering(string orderOverride)
        {
            if (!string.IsNullOrEmpty(orderOverride))
            {
                var elements = Regex.Matches(orderOverride, @"([A-Z][a-z]?)");
                _elementOrder = new Dictionary<string, int>();
                foreach (Match m in elements)
                {
                    if (!_elementOrder.ContainsKey(m.Value))
                    {
                        _elementOrder[m.Value] = _elementOrder.Count;
                    }
                    _hasIsotopesInOrderHint |= BioMassCalc.DICT_HEAVYSYMBOL_TO_MONOSYMBOL.ContainsKey(m.Value);
                }
                _checkIsotopes = true;
            }
        }

        private Dictionary<string, int> _elementOrder;
        private bool _checkIsotopes;
        private bool _hasIsotopesInOrderHint;

        // Try to find element's place in the original order, treating elements and their isotopes as interchangeable for position purposes
        private int IndexOfElementOrRelatedIsotope(string element)
        {
            if (_elementOrder.TryGetValue(element, out var order))
            {
                return order;
            }

            if (!_checkIsotopes)
            {
                return -1;
            }

            // Maybe it was an isotope that got changed to light e.g. original string was C'2 but now dictionary holds C2
            if (BioMassCalc.DICT_HEAVYSYMBOL_TO_MONOSYMBOL.TryGetValue(element, out var light))
            {
                if (_elementOrder.TryGetValue(light, out order))
                {
                    return order;
                }
            }

            if (_hasIsotopesInOrderHint)
            {
                // Maybe it was a light that got changed to an isotope  e.g. original string was C2 but now dictionary holds C"2
                foreach (var kvp in
                         BioMassCalc.DICT_HEAVYSYMBOL_TO_MONOSYMBOL.Where(kvp => Equals(kvp.Value, element)))
                {
                    if (_elementOrder.TryGetValue(kvp.Key, out order))
                    {
                        return order;
                    }
                }
            }

            return -1;
        }

        int IComparer<string>.Compare(string xElement, string yElement)
        {
            var xOrder = IndexOfElementOrRelatedIsotope(xElement);
            var yOrder = IndexOfElementOrRelatedIsotope(yElement);

            if (xOrder == yOrder)
            {
                // Either they're isotopes of each other (e.g. H and H'), or two elements that weren't in the ordered list at all
                if (BioMassCalc.ElementIsIsotopeOf(xElement, yElement))
                {
                    return 1; // X is an isotope of Y, so X should appear after Y in output string
                }
                if (BioMassCalc.ElementIsIsotopeOf(yElement, xElement))
                {
                    return -1; // Y is an isotope of X, so Y should appear after X in output string
                }
                return string.Compare(xElement, yElement, StringComparison.Ordinal);
            }

            return xOrder == -1 ?
                1 : // Only yElement is in the known order, xElement should appear after yElement in output string
                yOrder == -1 ? -1 : // Only xElement is in the known order, yElement should appear after xElement in output string
                    xOrder.CompareTo(yOrder); // Both in known order, sort on that basis
        }

        /// <summary>
        /// Perform ToString on a molecule, but enforcing Hill System order (C, H, then alphabetically)
        /// or a custom order based on a string like "Cl3N2D"
        /// </summary>
        public static string ToOrderedString(Molecule mol, string orderOverride)
        {
            if (Molecule.IsNullOrEmpty(mol))
            {
                return string.Empty;
            }

            var result = new StringBuilder();
            var anyNegative = false;

            var elementOrderComparer = string.IsNullOrEmpty(orderOverride) ?
                HillSystemOrdering.DEFAULT_ORDER :
                new HillSystemOrdering(orderOverride);

            // Write out the formula in the desired order
            var orderedKeys = mol.Keys.OrderBy(k => k, elementOrderComparer).ToArray();
            foreach (var key in orderedKeys)
            {
                var count = mol[key];
                if (count > 0)
                {
                    result.Append(key);
                    if (count != 1) // "H" is same as "H1"
                    {
                        result.Append(count);
                    }
                }
                else if (count < 0)
                {
                    anyNegative = true;
                }
            }

            if (anyNegative)
            {
                result.Append(@"-");
                foreach (var key in orderedKeys)
                {
                    var count = mol[key];
                    if (count < 0)
                    {
                        {
                            result.Append(key);
                            if (count != -1)
                            {
                                result.Append(-count);
                            }
                        }
                    }
                }
            }

            return result.ToString();
        }
    }



}
