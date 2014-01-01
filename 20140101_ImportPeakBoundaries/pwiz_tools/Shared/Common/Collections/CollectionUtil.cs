/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Helper methods for dealing with Collections.
    /// </summary>
    public static class CollectionUtil
    {
        public static bool EqualsDeep<TKey,TValue>(IDictionary <TKey,TValue> dict1, IDictionary<TKey,TValue> dict2)
        {
            if (dict1.Count != dict2.Count)
            {
                return false;
            }
            foreach (var entry in dict1)
            {
                TValue value2;
                if (!dict2.TryGetValue(entry.Key, out value2))
                {
                    return false;
                }
                if (!Equals(entry.Value, value2))
                {
                    return false;
                }
            }
            return true;
        }
        public static int GetHashCodeDeep<TKey, TValue>(IDictionary<TKey, TValue> dict)
        {
            return dict.Aggregate(0, 
                (seed, keyValuePair) => seed ^ keyValuePair.GetHashCode()
            );
        }

        public static int GetHashCodeDeep<T>(IList<T> list)
        {
            return list.Aggregate(0, (seed, item) => seed*397 + SafeGetHashCode(item));
        }
        public static int SafeGetHashCode<T>(T item)
        {
            return Equals(null, item) ? 0 : item.GetHashCode();
        }
        private class ListContentsEqualityComparer<T> : IEqualityComparer<IList<T>>
        {
            public bool Equals(IList<T> x, IList<T> y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(IList<T> obj)
            {
                return GetHashCodeDeep(obj);
            }
        }
        public static IEqualityComparer<IList<T>> GetListContentsEqualityComparer<T>()
        {
            return new ListContentsEqualityComparer<T>();
        }
        public static IDictionary<TKey,TValue> SingletonDictionary<TKey, TValue>(TKey key, TValue value)
        {
            // TODO: if performance becomes an issue, change this
            return new ImmutableDictionary<TKey, TValue>(new Dictionary<TKey, TValue> {{key, value}});
        }
        public static IDictionary<TKey,TValue> EmptyDictionary<TKey,TValue>()
        {
            return new ImmutableDictionary<TKey, TValue>(new Dictionary<TKey, TValue>());
        }
        /// <summary>
        /// Performs a binary search in a list of items.  The list is assumed to be sorted with respect to 
        /// <paramref name="compareFunc" /> such that those items for which compareFunc returns a negative
        /// number appear earlier in the list than those items for which compareFunc returns 0, which appear
        /// earlier than the items for which compareFunc returns a positive number.
        /// The return value is the index of the first or last (depending on <paramref name="firstIndex"/>) item 
        /// for which compareFunc returns 0.  If no item was found, then the return value is the one's complement
        /// of the index of the first item in the list for which compareFunc returns a positive number.
        /// </summary>
        public static int BinarySearch<TItem>(IList<TItem> items, Func<TItem, int> compareFunc, bool firstIndex)
        {
            var range = BinarySearch(items, compareFunc);
            if (range.Length == 0)
            {
                return ~range.Start;
            }
            return firstIndex ? range.Start : range.End - 1;
        }
        public static Range BinarySearch<TItem>(IList<TItem> items, Func<TItem, int> compareFunc)
        {
            Range result = new Range(0, items.Count);
            foreach (bool firstIndex in new[] { true, false })
            {
                int lo = result.Start;
                int hi = result.End;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;

                    int c = compareFunc(items[mid]);
                    if (c == 0)
                    {
                        if (firstIndex)
                        {
                            hi = mid;
                        }
                        else
                        {
                            lo = mid + 1;
                        }
                    }
                    else if (c < 0)
                    {
                        lo = mid + 1;
                        result = new Range(lo, result.End);
                    }
                    else
                    {
                        hi = mid;
                        result = new Range(result.Start, hi);
                    }
                }
            }
            return result;
        }
        public static int BinarySearch<TItem>(IList<TItem> items, TItem key) where TItem : IComparable
        {
            Range range = BinarySearch(items, item => item.CompareTo(key));
            if (range.Length == 0)
            {
                return ~range.Start;
            }
            return range.Start;
        }
    }
}
