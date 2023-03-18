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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Chemistry
{
    public abstract class AbstractFormula<T,TValue> : Immutable, IDictionary<string, TValue>, IComparable<T>
        where T : AbstractFormula<T, TValue>, new()
        where TValue : IComparable<TValue>
    {
// ReSharper disable InconsistentNaming
        public static readonly T Empty = new T { Dictionary = new ImmutableDictionary<string, TValue>(new Dictionary<string, TValue>()) };
        // ReSharper restore InconsistentNaming
        public abstract override string ToString();
        private int _hashCode;
        private ImmutableDictionary<string, TValue> _dict;
        protected List<string> _elementOrder; // For use in ToString - preserves element order of original formula string, if any. Not considered in comparisons.
        public virtual String ToDisplayString()
        {
            return ToString();
        }

        public T SetElementCount(String element, TValue count)
        {
            if (count.Equals(default(TValue))) // That is to say, zero
            {
                return new T
                {
                    Dictionary = Dictionary.RemoveKey(element),
                    _elementOrder = this._elementOrder
                };
            }
            return new T
            {
                Dictionary = Dictionary.Replace(element, count),
                _elementOrder = this._elementOrder
            };
        }

        public TValue GetElementCount(String element)
        {
            TValue atomCount;
            if (!TryGetValue(element, out atomCount))
                atomCount = default(TValue);
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
            var that = o as T;
            if (that == null)
            {
                return false;
            }
            return Dictionary.Equals(that.Dictionary);
        }

        public int CompareTo(T that)
        {
            var compareCount = this.Count.CompareTo(that.Count);
            if (compareCount != 0)
            {
                return compareCount;
            }
            foreach (var kvp in this)
            {
                if (!that.TryGetValue(kvp.Key, out var thatValue))
                {
                    return -1; // Different keys
                }
                if ((kvp.Value == null) != (thatValue == null))
                {
                    return -1; // Different values
                }
                if (kvp.Value != null)
                {
                    var compareValue = kvp.Value.CompareTo(thatValue);
                    if (compareValue != 0)
                    {
                        return compareValue;
                    }
                }
            }
            return 0;
        }

        public ImmutableDictionary<string, TValue> Dictionary
        {
            get { return _dict; }
            protected set 
            { 
                _dict = value;
                if (_dict.Values.Contains(default(TValue)))
                {
                    // Zeroes should have been filtered out before getting here.
                    throw new ArgumentException(string .Format(@"The formula {0} cannot contain zero", this));
                }
                _hashCode = value.GetHashCode();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
        {
            return Dictionary.GetEnumerator();
        }
        public int Count { get { return Dictionary.Count; } }
        public bool TryGetValue(string key, out TValue value)
        {
            return Dictionary.TryGetValue(key, out value);
        }
        public ICollection<string> Keys { get { return Dictionary.Keys; } }
        public ICollection<TValue> Values { get { return Dictionary.Values; } }
        public TValue this[string key]
        {
            get 
            { 
                TValue value;
                TryGetValue(key, out value);
                return value;
            }
            set
            {
                throw new InvalidOperationException();
            }
        }

        public void Add(KeyValuePair<string, TValue> item)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains(KeyValuePair<string, TValue> item)
        {
            return Dictionary.Contains(item);
        }

        public bool Remove(KeyValuePair<string, TValue> item)
        {
            throw new InvalidOperationException();
        }

        public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
        {
            Dictionary.CopyTo(array, arrayIndex);
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool ContainsKey(string key)
        {
            return Dictionary.ContainsKey(key);
        }

        public void Add(string key, TValue value)
        {
            throw new InvalidOperationException();
        }

        public bool Remove(string key)
        {
            throw new InvalidOperationException();
        }

    }

    public class Formula<T> : AbstractFormula<T, int>
        where T : Formula<T>, new()
    {
        public static T EMPTY = new T() { Dictionary = new ImmutableDictionary<string, int>(new Dictionary<string, int>()), _elementOrder = null };

        public static T Parse(string formula)
        {
            if (string.IsNullOrEmpty(formula))
            {
                return EMPTY;
            }
            var elementOrder = new List<string>();
            return new T
            {
                Dictionary = new ImmutableDictionary<string, int>(ParseToDictionary(formula, elementOrder)),
                _elementOrder = elementOrder
            };
        }

        public static bool IsNullOrEmpty(T f) => f == null || f.Count == 0;

        public static Dictionary<string, int> ParseToDictionary(string formula, List<string> elementOrder = null)
        {
            var result = new Dictionary<string, int>();
            string currentElement = null;
            int? currentQuantity = null;
            foreach (char ch in formula)
            {
                if (Char.IsDigit(ch))
                {
                    currentQuantity = (currentQuantity??0) * 10 + (ch - '0');
                }
                else if (Char.IsUpper(ch))
                {
                    // Close out current element, if any
                    if (currentElement != null)
                    {
                        int previousAtomCount;
                        var currentAtomCount = currentQuantity ?? 1; // No count declared implies 1
                        if (result.TryGetValue(currentElement, out previousAtomCount))
                        {
                            result[currentElement] = previousAtomCount + currentAtomCount;
                        }
                        else if (currentAtomCount != 0) // Beware explicitly declared 0 count
                        {
                            result.Add(currentElement, previousAtomCount + currentAtomCount);
                            elementOrder?.Add(currentElement);
                        }
                    }
                    currentQuantity = null;
                    currentElement = string.Empty + ch;
                }
                // Allow apostrophe for heavy isotopes (e.g. C' for 13C)
                else if (!Char.IsWhiteSpace(ch))
                {
                    currentElement = currentElement + ch;
                }
            }
            if (currentElement != null)
            {
                int previousAtomCount;
                var currentAtomCount = currentQuantity ?? 1; // No count declared implies 1
                if (result.TryGetValue(currentElement, out previousAtomCount))
                {
                    result[currentElement] = previousAtomCount + currentAtomCount;
                }
                else if (currentAtomCount != 0) // Beware explicitly declared 0 count
                {
                    result.Add(currentElement, currentAtomCount);
                    elementOrder?.Add(currentElement);
                }
            }
            return result;
        }

        // Handle formulae which may contain subtractions, as is deprotonation description ie C12H8O2-H (=C12H7O2) or even C12H8O2-H2O (=C12H6O)
        public static T ParseExpression(String formula)
        {
            var elementOrder = new List<string>();
            return FromDict(ParseExpressionToDictionary(formula, elementOrder), elementOrder);
        }

        public static Dictionary<String, int> ParseExpressionToDictionary(string expression, List<string> elementOrder = null)
        {
            var parts = expression.Split('-');
            if (parts.Length > 2)
            {
                throw new ArgumentException(@"Molecular formula subtraction expressions are limited a single operation");
            }
            var result = ParseToDictionary(parts[0], elementOrder);
            if (parts.Length > 1)
            {
                var subtractive = ParseToDictionary(parts[1]);
                foreach (var element in subtractive)
                {
                    int previous;
                    if (result.TryGetValue(element.Key, out previous))
                    {
                        int newCount = previous - element.Value;
                        if (newCount == 0)
                        {
                            result.Remove(element.Key);
                        }
                        else
                        {
                            result[element.Key] = newCount;
                        }
                    }
                    else
                    {
                        result.Add(element.Key, -element.Value);  // Seems weird, but possibly describing a proton lost from something other that H?
                    }
                }
            }
            return result;
        }

        // Subtract other's atom counts from ours
        public T Difference(T other)
        {
            var resultDict = new Dictionary<string, int>(this);
            foreach (var kvp in other)
            {
                TryGetValue(kvp.Key, out int count);
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

            return FromDict(resultDict, _elementOrder);
        }

        public T ChangeFormula(IEnumerable<KeyValuePair<string, int>> formula)
        {
            return FromDict(formula, _elementOrder);
        }

        public static T FromDict(IEnumerable<KeyValuePair<string, int>> dict, List<string> elementOrder = null)
        {
            return new T
            {
                Dictionary = new ImmutableDictionary<string, int>(dict.Where(kvp=> kvp.Value != 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)),
                _elementOrder = elementOrder
            };
        }

        public static bool AreEquivalentFormulas(string formulaLeft, string formulaRight)
        {
            if (string.Equals(formulaLeft, formulaRight))
            {
                return true;
            }
            // Consider C2C'4H5 to be same as H5C'4C2, or "C10H30Si5O5H-CH4" same as "C9H26O5Si5", etc
            var left = ParseExpression(formulaLeft);
            var right = ParseExpression(formulaRight);
            return left.Equals(right);
        }

        public bool HasElement(string element) => Dictionary.ContainsKey(element);


        // For use in ToString(), try to emit the formula string in a sensible order
        private class ElementOrderComparer : IComparer<string>
        {
            public ElementOrderComparer(List<string> elementOrder)
            {
                if (elementOrder != null && elementOrder.Count > 0)
                {
                    _elementOrder = elementOrder.ToArray();
                }
            }

            // Default to Hill System order (C then H then alphabetical) if no other order specified
            private readonly string[] _elementOrder = { @"C", @"H" };

            // Try to find element's place in the original order, treating elements and their isotopes as interchangeable for position purposes
            private int IndexOfElementOrRelatedIsotope(string element)
            {
                var order = Array.IndexOf(_elementOrder, element);
                if (order != -1)
                {
                    return order;
                }

                // Maybe it was an isotope that got changed to light e.g. original string was C'2 but now dictionary holds C2
                if (BioMassCalcBase.DICT_HEAVYSYMBOL_TO_MONOSYMBOL.TryGetValue(element, out var light))
                {
                    order = Array.IndexOf(_elementOrder, light);
                    if (order != -1)
                    {
                        return order;
                    }
                }

                // Maybe it was a light that got changed to an isotope  e.g. original string was C2 but now dictionary holds C"2
                if (element.IndexOfAny(BioMassCalcBase.HEAVYSYMBOL_HINTS) != -1) // Looks light it might be an isotope
                {
                    foreach (var kvp in 
                             BioMassCalcBase.DICT_HEAVYSYMBOL_TO_MONOSYMBOL.Where(kvp => Equals(kvp.Value, element)))
                    {
                        order = Array.IndexOf(_elementOrder, kvp.Key);
                        if (order != -1)
                        {
                            return order;
                        }
                    }
                }

                return order;
            }

            int IComparer<string>.Compare(string xElement, string yElement)
            {
                var xOrder = IndexOfElementOrRelatedIsotope(xElement);
                var yOrder = IndexOfElementOrRelatedIsotope(yElement);

                if (xOrder == yOrder)
                {
                    // Either isotopes of each other (e.g. H and H'), or two elements that weren't in the ordered list at all
                    return string.Compare(xElement, yElement, StringComparison.Ordinal);
                }

                return xOrder == -1 ?
                    1 : // Only yElement is in the known order, xElement should appear after yElement in output string
                    yOrder == -1 ? -1 : // Only xElement is in the known order, yElement should appear after xElement in output string
                        xOrder.CompareTo(yOrder); // Both in known order, sort on that basis
            }
        }


        public override string ToString()
        {
            var result = new StringBuilder();
            var anyNegative = false;

            var elementOrderComparer = new ElementOrderComparer(_elementOrder);

            foreach (var key in Keys.OrderBy(k => k, elementOrderComparer))
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
                foreach (var key in Keys.OrderBy(k => k, elementOrderComparer)) // Write in standard Hill system order
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

        public static T Sum(IEnumerable<T> items)
        {
            var dictionary = new Dictionary<string, int>();
            foreach (var item in items)
            {
                foreach (var entry in item)
                {
                    int count;
                    if (dictionary.TryGetValue(entry.Key, out count))
                    {
                        dictionary[entry.Key] = count + entry.Value;
                    }
                    else
                    {
                        dictionary.Add(entry.Key, entry.Value);
                    }
                }
            }

            return FromDict(dictionary);
        }
    }
}
