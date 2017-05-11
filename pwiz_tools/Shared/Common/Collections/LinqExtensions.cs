// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2017
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.Collections
{
    public static class SystemLinqExtensionMethods
    {
        public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, SortOrder sortOrder)
        {
            switch (sortOrder)
            {
                case SortOrder.Ascending: return source.OrderBy(keySelector);
                case SortOrder.Descending: return source.OrderByDescending(keySelector);
                case SortOrder.None:
                default: throw new ArgumentException();
            }
        }

        public static int SequenceCompareTo<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second) where TSource : IComparable<TSource>
        {
            int result = 0;
            var itr1 = first.GetEnumerator();
            var itr2 = second.GetEnumerator();
            bool itr1Valid = true, itr2Valid = true;
            while (true)
            {
                itr1Valid = itr1.MoveNext();
                itr2Valid = itr2.MoveNext();

                if (!itr1Valid && !itr2Valid) break;
                if (itr1Valid && !itr2Valid) return 1;
                if (!itr1Valid && itr2Valid) return -1;

                result = itr1.Current.CompareTo(itr2.Current);
                if (result != 0)
                    break;
            }
            return result;
        }

        public static bool IsNullOrEmpty<TSource>(this IEnumerable<TSource> list)
        {
            if (list == null)
                return true;

            var genericCollection = list as ICollection<TSource>;
            if (genericCollection != null)
                return genericCollection.Count == 0;

            var nonGenericCollection = list as System.Collections.ICollection;
            if (nonGenericCollection != null)
                return nonGenericCollection.Count == 0;

            var string_ = list as string;
            if (string_ != null)
                return String.IsNullOrEmpty(string_);

            return !list.Any();
        }

        public static ReadOnlyCollection<TSource> AsReadOnly<TSource>(this IList<TSource> list)
        {
            return list == null ? null : new ReadOnlyCollection<TSource>(list);
        }

        public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            if (list == null) throw new ArgumentNullException("list");
            if (items == null) throw new ArgumentNullException("items");

            if (list is List<T>)
            {
                ((List<T>)list).AddRange(items);
            }
            else
            {
                foreach (var item in items)
                {
                    list.Add(item);
                }
            }
        }

        public static void InsertRange<T>(this IList<T> list, int index, IEnumerable<T> items)
        {
            if (list == null) throw new ArgumentNullException("list");
            if (items == null) throw new ArgumentNullException("items");

            if (list is List<T>)
            {
                ((List<T>)list).InsertRange(index, items);
            }
            else
            {
                foreach (var item in items)
                {
                    list.Insert(index, item);
                    ++index;
                }
            }
        }

        public static void RemoveRange<T>(this IList<T> list, int index, int count)
        {
            if (list == null) throw new ArgumentNullException("list");

            if (list is List<T>)
            {
                ((List<T>)list).RemoveRange(index, count);
            }
            else
            {
                // iterate backwards to preserve index order
                for (int last = index + count; last >= index; --last)
                {
                    list.RemoveAt(last);
                }
            }
        }

        public class FuncEqualityComparer<T> : IEqualityComparer<T>
        {
            private readonly Func<T, T, bool> comparer;

            public FuncEqualityComparer(Func<T, T, bool> comparer)
            {
                if (comparer == null)
                    throw new ArgumentNullException("comparer cannot be null");

                this.comparer = comparer;
            }

            public bool Equals(T x, T y)
            {
                return comparer(x, y);
            }

            public int GetHashCode(T obj)
            {
                return obj.ToString().ToLower().GetHashCode();
            }
        }

        public static bool Contains<T>(this IEnumerable<T> list, T item, Func<T, T, bool> compareFunc)
        {
            return list.Contains(item, new FuncEqualityComparer<T>(compareFunc));
        }

        private static int GetMedian(int low, int hi)
        {
            return low + ((hi - low) >> 1);
        }

        /// <summary>
        /// Microsoft implementation of Array.Sort for IList
        /// </summary>
        private struct SorterObjectArray<TKey, TValue>
        {
            private IList<TKey> keys;
            private IList<TValue> items;
            private IComparer comparer;

            internal SorterObjectArray(IList<TKey> keys, IList<TValue> items, IComparer comparer)
            {
                if (comparer == null) comparer = Comparer.Default;
                this.keys = keys;
                this.items = items;
                this.comparer = comparer;
            }

            internal void SwapIfGreaterWithItems(int a, int b)
            {
                if (a != b)
                {
                    if (comparer.Compare(keys[a], keys[b]) > 0)
                    {
                        TKey temp = keys[a];
                        keys[a] = keys[b];
                        keys[b] = temp;
                        if (items != null)
                        {
                            TValue item = items[a];
                            items[a] = items[b];
                            items[b] = item;
                        }
                    }
                }
            }

            internal void QuickSort(int left, int right)
            {
                // Can use the much faster jit helpers for array access.
                do
                {
                    int i = left;
                    int j = right;

                    // pre-sort the low, middle (pivot), and high values in place.
                    // this improves performance in the face of already sorted data, or 
                    // data that is made up of multiple sorted runs appended together.
                    int middle = GetMedian(i, j);
                    SwapIfGreaterWithItems(i, middle); // swap the low with the mid point
                    SwapIfGreaterWithItems(i, j); // swap the low with the high 
                    SwapIfGreaterWithItems(middle, j); // swap the middle with the high

                    TKey x = keys[middle];
                    do
                    {
                        while (comparer.Compare(keys[i], x) < 0) i++;
                        while (comparer.Compare(x, keys[j]) < 0) j--; 

                        if (i > j) break;
                        if (i < j)
                        {
                            TKey key = keys[i];
                            keys[i] = keys[j];
                            keys[j] = key;
                            if (items != null)
                            {
                                TValue item = items[i];
                                items[i] = items[j];
                                items[j] = item;
                            }
                        }
                        i++;
                        j--;
                    } while (i <= j);
                    if (j - left <= right - i)
                    {
                        if (left < j) QuickSort(left, j);
                        left = i;
                    }
                    else
                    {
                        if (i < right) QuickSort(i, right);
                        right = j;
                    }
                } while (left < right);
            }
        }

        /// <summary>
        /// Co-sorts two lists of values using default comparator. Similar to Array.Sort()
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="keys"></param>
        /// <param name="values"></param>
        public static void Sort<TKey, TValue>(this IList<TKey> keys, IList<TValue> values, IComparer comparer = null)
        {
            if (comparer == null) comparer = Comparer.Default;
            var sorter = new SorterObjectArray<TKey, TValue>(keys, values, comparer);
            sorter.QuickSort(0, keys.Count - 1);
        }

        /// <summary>
        /// Binary searches the list for value (assumes the list is sorted by comparer), and returns the index of the value if it exists.
        /// If it doesn't, returns a negative number which is the complement of the index of the first value after the searched value.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <typeparam name="TSearch">The type of the searched item.</typeparam>
        /// <param name="list">The list to be searched.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
        /// <remarks>from http://stackoverflow.com/a/2948872/638445 </remarks>
        public static int BinarySearch<TItem, TSearch>(this IList<TItem> list, TSearch value, Func<TSearch, TItem, int> comparer)
        {
            if (list == null) throw new ArgumentNullException("list");
            if (comparer == null) throw new ArgumentNullException("comparer");

            int lower = 0;
            int upper = list.Count - 1;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                int comparisonResult = comparer(value, list[middle]);
                if (comparisonResult < 0)
                    upper = middle - 1;
                else if (comparisonResult > 0)
                    lower = middle + 1;
                else
                    return middle;
            }

            return ~lower;
        }

        /// <summary>
        /// Binary searches the list for value (assumes the list is sorted by the default comparer), and returns the index of the value if it exists.
        /// If it doesn't, returns a negative number which is the complement of the index of the first value after the searched value.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="list">The list to be searched.</param>
        /// <param name="value">The value to search for.</param>
        /// <remarks>from http://stackoverflow.com/a/2948872/638445 </remarks>
        public static int BinarySearch<TItem>(this IList<TItem> list, TItem value)
        {
            return BinarySearch(list, value, Comparer<TItem>.Default);
        }

        /// <summary>
        /// Binary searches the list for value (assumes the list is sorted by comparer), and returns the index of the value if it exists.
        /// If it doesn't, returns a negative number which is the complement of the index of the first value after the searched value.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="list">The list to be searched.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
        /// <remarks>from http://stackoverflow.com/a/2948872/638445 </remarks>
        public static int BinarySearch<TItem>(this IList<TItem> list, TItem value, IComparer<TItem> comparer)
        {
            return list.BinarySearch(value, comparer.Compare);
        }

        /// <summary>
        /// Binary searches the list for the first element greater than or equal to value (assumes the list is sorted by comparer), and returns its index (or Count if value is greater than the last element).
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="list">The list to be searched.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
        public static int LowerBound<TItem>(this IList<TItem> list, TItem value, IComparer<TItem> comparer)
        {
            int index = list.BinarySearch(value, comparer.Compare);
            if (index < 0)
                return ~index;
            return index;
        }

        /// <summary>
        /// Binary searches the list for the first element greater than or equal to value (assumes the list is sorted by the default comparer), and returns its index (or Count if value is greater than the last element).
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="list">The list to be searched.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
        public static int LowerBound<TItem>(this IList<TItem> list, TItem value)
        {
            return list.LowerBound(value, Comparer<TItem>.Default);
        }
    }
}
