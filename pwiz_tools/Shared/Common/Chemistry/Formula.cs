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
            TryGetValue(element, out atomCount);
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
        public static T Parse(String formula)
        {
            return new T {Dictionary = ImmutableSortedList.FromValues(ParseToDictionary(formula))};
        }

        public static Dictionary<string, int> ParseToDictionary(string formula)
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
                }
            }
            return result;
        }

        // Handle formulae which may contain subtractions, as is deprotonation description ie C12H8O2-H (=C12H7O2) or even C12H8O2-H2O (=C12H6O)
        public static T ParseExpression(String formula)
        {
            return new T { Dictionary = ImmutableSortedList.FromValues(ParseExpressionToDictionary(formula)) };
        }

        public static Dictionary<String, int> ParseExpressionToDictionary(string expression)
        {
            var parts = expression.Split('-');
            if (parts.Length > 2)
            {
                throw new ArgumentException(@"Molecular formula subtraction expressions are limited a single operation");
            }
            var result = ParseToDictionary(parts[0]);
            if (parts.Length > 1)
            {
                var subtractive = ParseToDictionary(parts[1]);
                foreach (var element in subtractive)
                {
                    int previous;
                    if (result.TryGetValue(element.Key, out previous))
                    {
                        result[element.Key] = previous - element.Value;
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
                int count;
                if (TryGetValue(kvp.Key, out count))
                {
                    resultDict[kvp.Key] = count - kvp.Value;
                }
                else
                {
                    resultDict.Add(kvp.Key, -kvp.Value);
                }
            }
            return new T { Dictionary = new ImmutableSortedList<string, int>(resultDict) };
        }

        public static T FromDict(ImmutableSortedList<string, int> dict)
        {
            return new T {Dictionary = dict};
        }

        public static T FromDict(IDictionary<string, int> dict)
        {
            return new T {Dictionary = ImmutableSortedList.FromValues(dict)};
        }

        public static string AdjustElementCount(string formula, string element, int delta)
        {
            var dict = Molecule.ParseToDictionary(formula);
            int count;
            if (!dict.TryGetValue(element, out count))
                count = 0;
            if ((count > 0) || (delta > 0)) // There are some to take away, or we're planning to add some
            {
                count += delta;
                if (count >= 0)
                {
                    dict[element] = count;
                    return Molecule.FromDict(dict).ToString();
                }
            }
            return formula;
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
            return left.Difference(right).All(atom => atom.Value == 0);
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            foreach (var entry in this)
            {
                if (entry.Value > 0)
                {
                    result.Append(entry.Key);
                    if (entry.Value != 1)
                    {
                        result.Append(entry.Value);
                    }
                }
            }
            return result.ToString();
        }
    }
}
