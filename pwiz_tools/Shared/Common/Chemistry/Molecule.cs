/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

using pwiz.Common.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System;
using System.Collections;

namespace pwiz.Common.Chemistry
{
    /// <summary>
    /// Represents a parsed molecular formula.  Formulas consist of capital letters, lowercase letters
    /// and numbers.  Element names start with a capital letter.
    /// </summary>
    public class Molecule : IDictionary<string, int>
    {
        internal int _hashCode;
        internal int? _originalHashCode; // If this matches _hashCode, then _orderHintString should match the contents and can be used for ToString()
        private ReadOnlyDictionary<string, int> _dict;
        internal string _orderHintString; // For use in ToString - preserves element order of original formula string, if any. Not considered in comparisons.
        private TypedMass _totalMassMonoisotopic;
        private TypedMass _totalMassAverage;

        public Molecule SetElementCount(string element, int count)
        {
            if (Equals(GetElementCount(element), count))
            {
                return this;
            }

            var dict = new Dictionary<string, int>(_dict);
            if (count.Equals(default(int))) // Moleculehat is to say, zero
            {
                dict.Remove(element);
            }
            else
            {
                dict[element] = count;
            }
            return new Molecule
            {
                Dictionary = new ReadOnlyDictionary<string, int>(dict),
                _orderHintString = this._orderHintString // Still useful for ordering ToString()
            };
        }

        public int GetElementCount(string element)
        {
            int atomCount;
            if (!TryGetValue(element, out atomCount))
                atomCount = default(int);
            return atomCount;
        }

        public override int GetHashCode()
        {
            // ReSharper disable NonReadonlyFieldInGetHashCode
            return _hashCode;
            // ReSharper restore NonReadonlyFieldInGetHashCode
        }

        public override bool Equals(Object o)
        {
            if (ReferenceEquals(o, this))
            {
                return true;
            }
            var that = o as Molecule;
            if (that == null)
            {
                return false;
            }
            return CollectionUtil.EqualsDeep(_dict, that._dict);
        }

        public int CompareTo(Molecule that)
        {
            var compareCount = this.Count.CompareTo(that.Count);
            if (compareCount != 0)
            {
                return compareCount;
            }
            foreach (var kvp in Dictionary)
            {
                if (!that.TryGetValue(kvp.Key, out var thatValue))
                {
                    return -1; // Different key sets
                }
                var compareValue = kvp.Value.CompareTo(thatValue);
                if (compareValue != 0)
                {
                    return compareValue;
                }
            }
            return 0;
        }

        public ReadOnlyDictionary<string, int> Dictionary
        {
            get { return _dict; }
            protected set
            {
                _dict = value ?? new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
                // We want a hashcode that is invariant to the order of the storage.
                _hashCode = 0;
                foreach (var key in _dict.Keys.OrderBy(k => k))
                {
                    _hashCode = (_hashCode * 397) ^ key.GetHashCode();
                    _hashCode = (_hashCode * 397) ^ _dict[key].GetHashCode();
                }

                if (!_originalHashCode.HasValue)
                {
                    _originalHashCode = _hashCode; // This is the first time we've set the dictionary
                }

                if (_dict.Values.Contains(default(int)))
                {
                    // Zeroes should have been filtered out before getting here.
                    throw new ArgumentException($@"The formula {this.ToString()} cannot contain zero");
                }

                // Cache the total mass
                _totalMassMonoisotopic = BioMassCalc.MONOISOTOPIC.GetChemicalMass(_dict);
                _totalMassAverage = BioMassCalc.AVERAGE.GetChemicalMass(_dict);
            }
        }



        public static Molecule EMPTY = new Molecule() { Dictionary = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>()), _orderHintString = null };

        public static Molecule Parse(string formula)
        {
            if (string.IsNullOrEmpty(formula?.Trim()))
            {
                return EMPTY;
            }

            return new Molecule
            {
                Dictionary = new ReadOnlyDictionary<string, int>(ParseToDictionary(formula, out var tidiedFormulaText)),
                _orderHintString = tidiedFormulaText
            };
        }

        public static bool TryParse(string formula, out Molecule result)
        {
            try
            {
                result = Parse(formula);
                return true;
            }
            catch (ArgumentException)
            {
                result = EMPTY;
                return false;
            }
        }

        public static bool IsNullOrEmpty(Molecule f) => f == null || f.Count == 0;


        /// <summary>
        /// Parse a string like C12H5 into a dictionary.
        /// Handles simple math like "C12H5-C3H2"
        /// Returns a tidied-up version of the input that removes zero-count elements but preserves idiosyncratic things like "HOOON1"
        /// </summary>
        /// <param name="formula">original string describing the formula</param>
        /// <param name="regularizedFormula">cleaned up formula string</param>
        /// <returns></returns>
        public static Dictionary<string, int> ParseToDictionary(string formula, out string regularizedFormula)
        {
            // Watch for trivial case
            var result = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(formula))
            {
                regularizedFormula = string.Empty;
                return result;
            }

            string currentElement = null;
            int? currentQuantity = null;
            var currentPolarity = 1;
            var polarityNext = 1;
            var tidied = new StringBuilder(formula.Length);

            void CloseOutCurrentElement()
            {
                if (currentElement != null)
                {
                    var currentAtomCount = currentPolarity * (currentQuantity ?? 1); // No count declared implies 1
                    var hasAtom = result.TryGetValue(currentElement, out var previousAtomCount);
                    var newAtomCount = previousAtomCount + currentAtomCount;
                    if (hasAtom)
                    {
                        if (newAtomCount == 0)
                        {
                            result.Remove(currentElement);
                        }
                        else
                        {
                            result[currentElement] = newAtomCount;
                        }
                    }
                    else if (newAtomCount != 0) // Beware explicitly declared 0 count e.g. "H0"
                    {
                        result.Add(currentElement, newAtomCount);
                    }

                    if ((currentQuantity ?? 1) > 0) // Omit any zero counts e.g. N0, but save any explicit counts e.g. "N1"
                    {
                        tidied.Append(currentElement);
                        if (currentQuantity.HasValue) // Preserve the "1" in "N1" if that's how the user presented it
                        {
                            tidied.Append(currentQuantity.Value.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }
                else if ((currentQuantity??0) != 0) // Input was something like "123" or "5C12H"
                {
                    throw new ArgumentException(BioMassCalc.FormatArgumentExceptionMessage(formula));
                }
            }

            foreach (char ch in formula)
            {
                if (Char.IsDigit(ch))
                {
                    currentQuantity = (currentQuantity ?? 0) * 10 + (ch - '0');
                }
                else if (Char.IsUpper(ch))
                {
                    // Close out current element, if any
                    CloseOutCurrentElement();
                    currentQuantity = null;
                    currentElement = string.Empty + ch;
                    if (currentPolarity != polarityNext)
                    {
                        tidied.Append(@"-");
                        currentPolarity = polarityNext;
                    }
                }
                else if (Equals(ch, '-'))
                {
                    if (currentPolarity < 0)
                    {
                        throw new ArgumentException($@"Failed parsing ""{formula}"": molecular formula subtraction expressions are limited to a single operation");
                    }
                    polarityNext = -currentPolarity; // Flip +/- after we process the previous element
                }
                // Allow apostrophe for heavy isotopes (e.g. C' for 13C)
                else if (!Char.IsWhiteSpace(ch))
                {
                    currentElement = currentElement + ch;
                }
            }
            CloseOutCurrentElement(); // Finish up the last element

            regularizedFormula = tidied.ToString();
            return result;
        }

        // Subtract other's atom counts from ours
        public virtual Molecule Difference(Molecule other)
        {
            if (other == null || other.Count == 0)
            {
                return this;
            }
            var resultDict = new Dictionary<string, int>(this.Dictionary);
            foreach (var kvp in other.Dictionary)
            {
                TryGetValue(kvp.Key, out var count);
                count -= kvp.Value;
                if (count == 0)
                {
                    resultDict.Remove(kvp.Key);
                }
                else
                {
                    resultDict[kvp.Key] = count;
                }
            }

            return FromDictionary(resultDict,
                (_orderHintString ?? string.Empty) + (string.IsNullOrEmpty(other._orderHintString) ? string.Empty : ("-" + other._orderHintString)));
        }

        // Add other's contents to ours
        public virtual Molecule Plus(Molecule other)
        {
            if (other == null || other.Count == 0)
            {
                return this;
            }

            if (Count == 0)
            {
                return other;
            }

            var resultDict = new Dictionary<string, int>(Dictionary);
            foreach (var kvp in other.Dictionary)
            {
                TryGetValue(kvp.Key, out var count);
                count += kvp.Value;
                if (count == 0)
                {
                    resultDict.Remove(kvp.Key);
                }
                else
                {
                    resultDict[kvp.Key] = count;
                }
            }

            return FromDictionary(resultDict, (_orderHintString ?? string.Empty) + (other._orderHintString ?? string.Empty));
        }

        public virtual Molecule ChangeFormula(IDictionary<string, int> formula)
        {
            return (formula == null ? Dictionary.Count == 0 : CollectionUtil.EqualsDeep(formula, Dictionary)) ?
                this :
                FromDictionary(formula, _orderHintString, _originalHashCode);
        }

        public virtual Molecule ChangeFormula(IEnumerable<KeyValuePair<string, int>> formula)
        {
            var dict = formula.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return FromDictionary(dict, _orderHintString, _originalHashCode); // Give the old hashcode for use in quick ToString()
        }

        public static Molecule FromDictionary(IDictionary<string, int> dict, string orderHintString = null, int? oldHashCode = null)
        {
            if (dict == null)
            {
                return EMPTY;
            }

            if (!dict.Any())
            {
                return EMPTY;
            }

            return new Molecule
            {
                _originalHashCode = oldHashCode, // Important to set this before setting Dictionary
                Dictionary = dict is ReadOnlyDictionary<string, int> readOnlyDictionary ? readOnlyDictionary :  new ReadOnlyDictionary<string, int>(dict),
                _orderHintString = orderHintString,
            };
        }

        public virtual TypedMass GetTotalMass(MassType massType)
        {
            return massType.IsMonoisotopic() ?
                _totalMassMonoisotopic :
                _totalMassAverage;
        }

        public static bool AreEquivalentFormulas(string formulaLeft, string formulaRight)
        {
            if (string.Equals(formulaLeft, formulaRight))
            {
                return true;
            }
            // Consider C2C'4H5 to be same as H5C'4C2, or "C10H30Si5O5H-CH4" same as "C9H26O5Si5", etc
            var left = Parse(formulaLeft);
            var right = Parse(formulaRight);
            return left.Equals(right); // Compare dictionaries, ignoring any difference in string representation
        }

        public bool HasElement(string element) => Dictionary.ContainsKey(element);

        public bool HasIsotopes() => Dictionary.Keys.Any(k => BioMassCalc.DICT_HEAVYSYMBOL_TO_MONOSYMBOL.ContainsKey(k));

        // For use in ToString(), try to emit the formula string in a sensible order
        private class ElementOrderComparer : IComparer<string>
        {
            internal static ElementOrderComparer HILL_SYSTEM_ORDER = new ElementOrderComparer(null)
            {
                _elementOrder = new Dictionary<string, int>()
                {
                    { @"C", 0 }, { @"C'", 1 }, { @"C\", 2 },
                    { @"H", 3 }, { @"H'", 4 }, { @"D", 5 }, { @"H\", 6 }, { @"T", 7 }
                },
                _checkIsotopes = false
            };
                
            public ElementOrderComparer(string orderHints)
            {
                if (!string.IsNullOrEmpty(orderHints))
                {
                    var elements = Regex.Matches(orderHints, @"([A-Z][a-z]?)");
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
        }

        public virtual string ToDisplayString()
        {
            return ToString();
        }

        /// <summary>
        /// Return a string representation as close as possible to the one that gave rise to this object
        /// </summary>
        public override string ToString()
        {
            // It's likely that _orderHintString is the very string that this was built from, if so just return that
            if (!string.IsNullOrEmpty(_orderHintString) && _hashCode == _originalHashCode)
            {
                return _orderHintString;
            }

            var result = new StringBuilder();
            var anyNegative = false;

            var elementOrderComparer = string.IsNullOrEmpty(_orderHintString) ?
                ElementOrderComparer.HILL_SYSTEM_ORDER :
                new ElementOrderComparer(_orderHintString);

            var orderedKeys = Keys.OrderBy(k => k, elementOrderComparer).ToArray();
            foreach (var key in orderedKeys)
            {
                var count = this[key];
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
                result.Append("-");
                foreach (var key in orderedKeys)
                {
                    var count = this[key];
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

        public virtual Molecule AdjustElementCount(string element, int delta)
        {
            if (delta == 0)
            {
                return this;
            }
            TryGetValue(element, out var count);
            count += delta;
            var newDict = new Dictionary<string, int>(Dictionary);
            if (count == 0)
            {
                newDict.Remove(element);
            }
            else
            {
                newDict[element] = count;
            }
            return ChangeFormula(newDict);
        }

        public virtual Molecule StripIsotopicLabels()
        {
            var stripped = BioMassCalc.StripLabelsFromFormula(Dictionary);

            return ChangeFormula(stripped);
        }

        public static Molecule Sum(IEnumerable<Molecule> items)
        {
            var dictionary = new Dictionary<string, int>();
            foreach (var item in items)
            {
                foreach (var entry in item.Dictionary)
                {
                    if (dictionary.TryGetValue(entry.Key, out var count))
                    {
                        count += entry.Value;
                        if (count == 0)
                        {
                            dictionary.Remove(entry.Key);
                        }
                        else
                        {
                            dictionary[entry.Key] = count;
                        }
                    }
                    else if (entry.Value != 0)
                    {
                        dictionary.Add(entry.Key, entry.Value);
                    }
                }
            }

            return FromDictionary(dictionary);
        }


        #region implement IEnumerable<KeyValuePair<string, int>>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            return Dictionary.GetEnumerator();
        }
        #endregion

        #region implement IDictionary<string, int>

        public bool Remove(KeyValuePair<string, int> item)
        {
            throw new InvalidOperationException();
        }

        public int Count { get { return Dictionary.Count; } }
        public bool IsReadOnly => true;

        public bool TryGetValue(string key, out int value)
        {
            return Dictionary.TryGetValue(key, out value);
        }
        public ICollection<string> Keys { get { return Dictionary.Keys; } }
        public ICollection<int> Values { get { return Dictionary.Values; } }
        public int this[string key]
        {
            get
            {
                int value;
                TryGetValue(key, out value);
                return value;
            }
            set
            {
                throw new InvalidOperationException();
            }
        }

        public void Add(KeyValuePair<string, int> item)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains(KeyValuePair<string, int> item)
        {
            return Dictionary.Contains(item);
        }

        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
        {
            throw new InvalidOperationException();
        }

        public bool ContainsKey(string key)
        {
            return Dictionary.ContainsKey(key);
        }

        public void Add(string key, int value)
        {
            throw new InvalidOperationException();
        }

        public bool Remove(string key)
        {
            throw new InvalidOperationException();
        }
        #endregion
    }
}
