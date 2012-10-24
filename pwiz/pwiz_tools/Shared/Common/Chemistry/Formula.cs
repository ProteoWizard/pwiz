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
using System.Collections.Generic;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    public abstract class AbstractFormula<T,TKey> : ImmutableDictionary<String, TKey>, IComparable<T>
        where T : AbstractFormula<T, TKey>, new()
        where TKey : IComparable<TKey>
    {
// ReSharper disable InconsistentNaming
        public static readonly T Empty = new T { Dictionary = new SortedDictionary<string, TKey>() };
// ReSharper restore InconsistentNaming
        public override abstract string ToString();
        public virtual String ToDisplayString()
        {
            return ToString();
        }

        public T SetElementCount(String element, TKey count)
        {
            var dict = new SortedDictionary<String, TKey>(this);
            if (count.Equals(default(TKey)))
            {
                dict.Remove(element);
            }
            else
            {
                dict[element] = count;
            }
            return new T { Dictionary = dict };
        }

        public TKey GetElementCount(String element)
        {
            TKey atomCount;
            TryGetValue(element, out atomCount);
            return atomCount;
        }

        public override int GetHashCode()
        {
            int result = 0;
            foreach (var entry in this)
            {
                result += entry.Key.GetHashCode() * entry.Value.GetHashCode();
            }
            return result;
        }

        public override bool Equals(Object o)
        {
            if (o == this)
            {
                return true;
            }
            var that = o as T;
            if (that == null)
            {
                return false;
            }
            if (Count != that.Count)
            {
                return false;
            }
            foreach (var entry in this)
            {
                TKey thatValue;
                if (!that.TryGetValue(entry.Key, out thatValue))
                {
                    return false;
                }
                if (!Equals(entry.Value, thatValue))
                {
                    return false;
                }
            }
            return true;
        }

        public int CompareTo(T that)
        {
            var thisEnumerator = GetEnumerator();
            var thatEnumerator = that.GetEnumerator();
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

    public class Formula<T> : AbstractFormula<T, int>
        where T : Formula<T>, new()
    {
        public static T Parse(String formula)
        {
            var result = new SortedDictionary<string, int>();
            string currentElement = null;
            int currentQuantity = 0;
            foreach (char ch in formula)
            {
                if (Char.IsDigit(ch))
                {
                    currentQuantity = currentQuantity * 10 + (ch - '0');
                }
                else if (Char.IsUpper(ch))
                {
                    if (currentElement != null)
                    {
                        if (currentQuantity == 0)
                        {
                            currentQuantity = 1;
                        }
                        if (result.ContainsKey(currentElement))
                        {
                            result[currentElement] = result[currentElement] + currentQuantity;
                        }
                        else
                        {
                            result[currentElement] = currentQuantity;
                        }
                    }
                    currentQuantity = 0;
                    currentElement = "" + ch;
                }
                // Allow apostrophe for heavy isotopes (e.g. C' for 13C)
                else if (!Char.IsWhiteSpace(ch))
                {
                    currentElement = currentElement + ch;
                }
            }
            if (currentElement != null)
            {
                if (currentQuantity == 0)
                {
                    currentQuantity = 1;
                }
                if (result.ContainsKey(currentElement))
                {
                    result[currentElement] = result[currentElement] + currentQuantity;
                }
                else
                {
                    result[currentElement] = currentQuantity;
                }
            }
            return new T {Dictionary = result};
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            foreach (var entry in this)
            {
                result.Append(entry.Key);
                if (entry.Value != 1)
                {
                    result.Append(entry.Value);
                }
            }
            return result.ToString();
        }
    }
}
