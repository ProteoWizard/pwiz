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
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    public abstract class AbstractFormula<T,TValue> : IDictionary<string, TValue>, IComparable<T>
        where T : AbstractFormula<T, TValue>, new()
        where TValue : IComparable<TValue>
    {
// ReSharper disable InconsistentNaming
        public static readonly T Empty = new T { Dictionary = ImmutableSortedList<string, TValue>.EMPTY };
// ReSharper restore InconsistentNaming
        public abstract override string ToString();
        private int _hashCode;
        private ImmutableSortedList<string, TValue> _dict;
        public virtual String ToDisplayString()
        {
            return ToString();
        }

        public T SetElementCount(String element, TValue count)
        {
            if (count.Equals(default(TValue)))
            {
                return new T {Dictionary = Dictionary.RemoveKey(element)};
            }
            return new T {Dictionary = Dictionary.Replace(element, count)};
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
            using (var thisEnumerator = GetEnumerator())
            using (var thatEnumerator = that.GetEnumerator())
            {
                while (thisEnumerator.MoveNext())
                {
                    if (!thatEnumerator.MoveNext())
                    {
                        return 1;
                    }
                    int keyCompare = string.CompareOrdinal(thisEnumerator.Current.Key, thatEnumerator.Current.Key);
                    if (keyCompare != 0)
                    {
                        return keyCompare;
                    }
                    int valueCompare = thisEnumerator.Current.Value.CompareTo(thatEnumerator.Current.Value);
                    if (valueCompare != 0)
                    {
                        return valueCompare;
                    }
                }
                return thatEnumerator.MoveNext() ? -1 : 0;
            }
        }
        public ImmutableSortedList<string, TValue> Dictionary
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

        public static bool IsNullOrEmpty(Formula<T> value)
        {
            return value == null || value.IsEmpty();
        }

        public static T Parse(string formula)
        {
            return new T { Dictionary = ImmutableSortedList.FromValues(ParseToDictionary(formula)) };
        }

        public static T Parse(string formula, out string regularizedFormula)
        {
            var formulaCleanup = new StringBuilder(string.IsNullOrEmpty(formula) ? 0 : formula.Length);
            var keyValuePairs = ParseToDictionary(formula, formulaCleanup);
            regularizedFormula = formulaCleanup.ToString();
            return new T { Dictionary = ImmutableSortedList.FromValues(keyValuePairs) };
        }

        /// <summary>
        /// Parse a string like C12H5 into a dictionary.
        /// Handles simple math like "C12H5-C3H2"
        /// Returns a tidied-up version of the input that removes zero-count elements but preserves idiosyncratic things like "HOOON1"
        /// </summary>
        /// <param name="formula">original string describing the formula</param>
        /// <param name="regularizedFormula">optional StringBuilder for cleaned up formula string</param>
        /// <returns></returns>
        public static Dictionary<string, int> ParseToDictionary(string formula, StringBuilder regularizedFormula = null)
        {
            // Watch for trivial case
            var result = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(formula))
            {
                return result;
            }

            string currentElement = null;
            int? currentQuantity = null;
            var currentPolarity = 1;
            var polarityNext = 1;

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

                    if (regularizedFormula != null)
                    {
                        if ((currentQuantity ?? 1) > 0) // Omit any zero counts e.g. N0, but save any explicit counts e.g. "N1"
                        {
                            regularizedFormula.Append(currentElement);
                            if (currentQuantity.HasValue) // Preserve the "1" in "N1" if that's how the user presented it
                            {
                                regularizedFormula.Append(currentQuantity.Value.ToString(CultureInfo.InvariantCulture));
                            }
                        }
                    }
                }
                else if ((currentQuantity ?? 0) != 0) // Input was something like "123" or "5C12H"
                {
                    throw new ArgumentException($@"""{formula}""");
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
                        regularizedFormula?.Append(@"-");
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

            return result;
        }



        // Subtract other's atom counts from ours
        public T Difference(T other)
        {
            if (other == null || other.Count == 0)
            {
                return (T)this;
            }

            var resultDict = new Dictionary<string, int>(this);
            foreach (var kvpOther in other)
            {
                if (TryGetValue(kvpOther.Key, out var countCurrent))
                {
                    var newCount = countCurrent - kvpOther.Value;
                    if (newCount == 0)
                    {
                        resultDict.Remove(kvpOther.Key);
                    }
                    else
                    {
                        resultDict[kvpOther.Key] = newCount;
                    }
                }
                else
                {
                    resultDict.Add(kvpOther.Key, -kvpOther.Value);
                }
            }

            return FromDict(resultDict);
        }

        public T Plus(T other)
        {
            if (other == null || other.Count == 0)
            {
                return (T)this;
            }

            var resultDict = new Dictionary<string, int>(this);
            foreach (var kvpOther in other)
            {
                if (TryGetValue(kvpOther.Key, out var count))
                {
                    var newCount = count + kvpOther.Value;
                    if (newCount == 0)
                    {
                        resultDict.Remove(kvpOther.Key);
                    }
                    else
                    {
                        resultDict[kvpOther.Key] = newCount;
                    }
                }
                else
                {
                    resultDict.Add(kvpOther.Key, kvpOther.Value);
                }
            }

            return FromDict(resultDict);
        }

        public bool IsEmpty()
        {
            return Dictionary == null || Dictionary.Count == 0;
        }

        public static T FromDict(IDictionary<string, int> dict)
        {
            return new T {Dictionary = ImmutableSortedList.FromValues(dict.Where(entry=>entry.Value != 0))};
        }

        public T AdjustElementCount(string element, int delta, bool allowNegative = false)
        {
            TryGetValue(element, out var existing);
            var newCount = existing + delta;
            if (allowNegative || newCount >= 0) // There are some to take away, or we're planning to add some
            {
                var dict = new Dictionary<string, int>(this);
                if (newCount == 0)
                {
                    if (existing != 0)
                    {
                        dict.Remove(element);
                        return FromDict(dict);
                    }
                    return this as T;
                }
                if (existing == 0)
                {
                    dict.Add(element, newCount);
                }
                else
                {
                    dict[element] = newCount;
                }
                return FromDict(dict);
            }
            return this as T;
        }

        public static string AdjustElementCount(string formula, string element, int delta)
        {
            var dict = Parse(formula);
            return dict.AdjustElementCount(element, delta).ToString();
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
            return left.Equals(right);
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            bool anyNegative = false;
            foreach (var entry in this)
            {
                if (entry.Value >= 0)
                {
                    result.Append(entry.Key);
                    if (entry.Value != 1)
                    {
                        result.Append(entry.Value);
                    }
                }
                else
                {
                    anyNegative = true;
                }
            }

            if (!anyNegative)
            {
                return result.ToString();
            }

            result.Append("-");
            foreach (var entry in this)
            {
                if (entry.Value < 0)
                {
                    result.Append(entry.Key);
                    if (entry.Value != -1)
                    {
                        result.Append(-entry.Value);
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
