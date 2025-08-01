﻿/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{

    /// <summary>
    /// Allows access to a default state for a collection that allows
    /// editing.
    /// </summary>
    /// <typeparam name="TItem">The type of the items in the collection</typeparam>
    public interface IListDefaults<TItem>
    {
        /// <summary>
        /// Gets the current revision index for this list
        /// </summary>
        int RevisionIndexCurrent { get; }

        /// <summary>
        /// Gets the default collection as an enumerable list.
        /// </summary>
        /// <returns>The default collection</returns>
        IEnumerable<TItem> GetDefaults(int revisionIndex);

        /// <summary>
        /// Gets the localized display name for an item in this list
        /// usually replacing names for the default items with localized text.
        /// </summary>
        /// <param name="item">The item for which to get the display text</param>
        /// <returns>Localized display text for default items or user supplied text for other items</returns>
        string GetDisplayName(TItem item);
    }

    /// <summary>
    /// Exposes properties necessary for using <see cref="EditListDlg{T,TItem}"/>
    /// to edit a list.
    /// </summary>
    public interface IListEditorSupport
    {
        /// <summary>
        /// The title to display on <see cref="EditListDlg{T,TItem}"/>
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Label string for the listbox that shows this list being
        /// edited.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// True if the list can be reset to its default contents.
        /// </summary>
        bool AllowReset { get; }

        /// <summary>
        /// The number of default items that should be exclude when editing
        /// the list.  Useful when some default items cannot be edited
        /// or removed from the list.
        /// </summary>
        int ExcludeDefaults { get; }
    }

    /// <summary>
    /// Implement this interfact to support the <see cref="EditListDlg{T,TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">Type of items in the list to be edited</typeparam>
    public interface IListEditor<TItem>
    {
        /// <summary>
        /// Exposes ability to edit a list of items.
        /// </summary>
        /// <returns>The new list after editing, or null if the user cancelled</returns>
        IEnumerable<TItem> EditList(Control owner, object tag);

        /// <summary>
        /// Returns true, if a new list is accepted to replace the current list
        /// </summary>
        bool AcceptList(Control owner, IList<TItem> listNew);
    }

    /// <summary>
    /// Implement this interfact to support the <see cref="ShareListDlg{T,TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">Type of items in the list to be edited</typeparam>
    public interface IListSerializer<TItem>
    {
        Type SerialType { get; }

        Type DeserialType { get; }

        ICollection<TItem> CreateEmptyList();

        bool ContainsKey(string key);
    }

    /// <summary>
    /// Implement this interface to support the "Add" and "Edit"
    /// buttons in the <see cref="EditListDlg{T,TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">Type of items in the list to be edited</typeparam>
    public interface IItemEditor<TItem>
    {
        /// <summary>
        /// Exposes the ability to create a new item for this list.
        /// </summary>
        /// <param name="owner">Window requesting the edit</param>
        /// <param name="existing">A list of existing items of this type</param>
        /// <param name="tag">Object passed to the list editor for use in item editors</param>
        /// <returns>The new item, or null if the user cancelled</returns>
        TItem NewItem(Control owner, IEnumerable<TItem> existing, object tag);

        /// <summary>
        /// Exposes the ability to edit an individual item, return
        /// a new modified item.  Items are considered immutable,
        /// so successful return value will always be a new item.
        /// </summary>
        /// <param name="owner">Window requesting the edit</param>
        /// <param name="item">The item to edit</param>
        /// <param name="existing">A list of existing items of this type</param>
        /// <param name="tag">Object passed to the list editor for use in item editors</param>
        /// <returns>The new item, or null if the user cancelled</returns>
        TItem EditItem(Control owner, TItem item, IEnumerable<TItem> existing, object tag);

        /// <summary>
        /// Copies an item for this list, with the copied item's name reset
        /// to the empty string.
        /// </summary>
        /// <param name="item">The item to copy</param>
        /// <returns>The copied item with empty name</returns>
        TItem CopyItem(TItem item);
    }

    /// <summary>
    /// A singleton list that allows its one value to be changed
    /// </summary>
    public class SingletonList<T> : IList<T>
    {
        private T _item;

        public SingletonList(T item)
        {
            _item = item;
        }

        public IEnumerator<T> GetEnumerator()
        {
            yield return _item;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            if (item == null)
                return _item == null;

            return item.Equals(_item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            array[arrayIndex] = _item;
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        public int Count
        {
            get { return 1; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public int IndexOf(T item)
        {
            return Contains(item) ? 0 : -1;
        }

        public void Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        public T this[int index]
        {
            get
            {
                if (index != 0)
                    throw new IndexOutOfRangeException();
                return _item;
            }
            set
            {
                if (index != 0)
                    throw new IndexOutOfRangeException();
                _item = value;
            }
        }
    }


    /// <summary>
    /// Exposes a set of generic Array extension utility functions.
    /// </summary>
    public static class ArrayUtil
    {
        /// <summary>
        /// Returns the length of an array or zero if it is null.
        /// </summary>
        public static int SafeLength<TItem>(this IList<TItem> values)
        {
            return values != null ? values.Count : 0;
        }

        /// <summary>
        /// Parses an array of items from a string, which are separated by
        /// a specific character (e.g. "1, 2, 3" or "3.5; 4.5; 5.5").  Whitespace
        /// is trimmed.
        /// </summary>
        /// <typeparam name="TItem">Type of items in the array returned</typeparam>
        /// <param name="values">The string to parse</param>
        /// <param name="conv">An instance of a string to T converter</param>
        /// <param name="separatorChar">The separator character</param>
        /// <param name="defaults">A default array to return, if the string is null or empty</param>
        /// <returns></returns>
        public static TItem[] Parse<TItem>(string values, Converter<string, TItem> conv,
            char separatorChar, params TItem[] defaults)
        {
            if (!string.IsNullOrEmpty(values))
            {
                try
                {
                    List<TItem> list = new List<TItem>();
                    string[] parts = values.Split(separatorChar);
                    foreach (string part in parts)
                        list.Add(conv(part.Trim()));
                    return list.ToArray();
                }
// ReSharper disable EmptyGeneralCatchClause
                catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
                {
                }
            }
            return defaults;
        }

        /// <summary>
        /// Joins the ToString() value for an array of objects, with a specified
        /// separator character between each item.
        /// </summary>
        /// <typeparam name="TItem">The type of the items in the array</typeparam>
        /// <param name="values">The array of items to join</param>
        /// <param name="separator">The separator character to place between strings</param>
        /// <returns>A joined string of items with intervening separators</returns>
        public static string ToString<TItem>(this IList<TItem> values, string separator)
        {
            StringBuilder sb = new StringBuilder();
            foreach (TItem value in values)
            {
                if (sb.Length > 0)
                    sb.Append(separator);
                sb.Append(value);
            }
            return sb.ToString();
        }

        public static TItem[] ToArrayStd<TItem>(this IList<TItem> list)
        {
            var a = list as TItem[];
            if (a == null)
            {
                a = new TItem[list.Count];
                for (int i = 0; i < a.Length; i++)
                    a[i] = list[i];
            }
            return a;
        }

        /// <summary>
        /// Gets a <see cref="IEnumerable{T}"/> for enumerating over an Array.
        /// </summary>
        /// <typeparam name="TItem">Type of items in the array</typeparam>
        /// <param name="values">Array instance</param>
        /// <param name="forward">True if the enumerator should be forward, False if reversed</param>
        /// <returns>The enumeration of the Array</returns>
        public static IEnumerable<TItem> GetEnumerator<TItem>(this IList<TItem> values, bool forward)
        {
            if (forward)
            {
                foreach (TItem value in values)
                    yield return value;
            }
            else
            {
                for (int i = values.Count - 1; i >= 0; i--)
                    yield return values[i];
            }
        }

        public const int RANDOM_SEED = 7 * 7 * 7 * 7 * 7; // 7^5 recommended by Brian S.

        /// <summary>
        /// Creates a random order of indexes into an array for a random linear walk
        /// through an array.
        /// </summary>
        public static IEnumerable<TItem> RandomOrder<TItem>(this IList<TItem> list, int? seed = null)
        {
            int count = list.Count;
            var indexOrder = new int[count];
            for (int i = 0; i < count; i++)
                indexOrder[i] = i;
            Random r = seed.HasValue ? new Random(seed.Value) : new Random();
            for (int i = 0; i < count; i++)
                Helpers.Swap(ref indexOrder[0], ref indexOrder[r.Next(count)]);
            foreach (int i in indexOrder)
            {
                yield return list[i];
            }
        }

        /// <summary>
        /// Searches an Array for an item that is reference equal with
        /// a specified item to find.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="find">The item to find</param>
        /// <returns>The index in the Array of the specified reference, or -1 if not found</returns>
        public static int IndexOfReference<TItem>(this IList<TItem> values, TItem find)
        {
            return values.IndexOf(value => ReferenceEquals(value, find));
        }

        /// <summary>
        /// Searches an Array for an item that matches criteria specified
        /// through a delegate function.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <returns>The index in the Array of the match, or -1 if not found</returns>
        public static int IndexOf<TItem>(this IList<TItem> values, Predicate<TItem> found)
        {
            return IndexOf(values, found, 0);
        }

        /// <summary>
        /// Searches an Array for an item that matches criteria specified
        /// through a delegate function. Search starts at the given index.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <param name="startIndex">Starting index of the search.</param>
        /// <returns>The index in the Array of the match, or -1 if not found</returns>
        public static int IndexOf<TItem>(this IList<TItem> values, Predicate<TItem> found, int startIndex)
        {
            for (int i = startIndex; i < values.Count; i++)
            {
                if (found(values[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Searches backward in an Array for an item that matches criteria specified
        /// through a delegate function.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <returns>The index in the Array of the last match, or -1 if not found</returns>
        public static int LastIndexOf<TItem>(this IList<TItem> values, Predicate<TItem> found)
        {
            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (found(values[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Searches an Array for an item that matches criteria specified
        /// through a delegate function.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <returns>True if the accepting function returns true for an element</returns>
        public static bool Contains<TItem>(this IEnumerable<TItem> values, Predicate<TItem> found)
        {
            foreach (TItem value in values)
            {
                if (found(value))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks for equality of all items in an IEnumerable without regard for order.
        /// </summary>
        /// <typeparam name="TItem">Type of items in the IEnumerable</typeparam>
        /// <param name="values1">First IEnumerable in the comparison</param>
        /// <param name="values2">Second IEnumerable in the comparison</param>
        /// <returns>True if all items in one IEnumerable are found in the other, and IEnumerables are same length</returns>
        public static bool ContainsAll<TItem>(this IEnumerable<TItem> values1, IEnumerable<TItem> values2)
        {
            var set1 = values1.ToHashSet();
            var set2 = values2.ToHashSet();
            return set1.Count == set2.Count && set1.IsSubsetOf(set2);
        }

        /// <summary>
        /// Checks for deep equality, or equality of all items in an Array.
        /// </summary>
        /// <typeparam name="TItem">Type of items in the array</typeparam>
        /// <param name="values1">First array in the comparison</param>
        /// <param name="values2">Second array in the comparison</param>
        /// <returns>True if all items in both arrays in identical positions are Equal</returns>
        public static bool EqualsDeep<TItem>(IList<TItem> values1, IList<TItem> values2)
        {
            if (values1 == null && values2 == null)
                return true;
            if (values1 == null || values2 == null)
                return false;
            int count = values1.Count;
            if (count != values2.Count)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!Equals(values1[i], values2[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks for deep equality, or equality of all items in a Dictionary.
        /// </summary>
        /// <typeparam name="TItemKey">Type of items in the dictionary keys</typeparam>
        /// <typeparam name="TItemValue">Type of items in the dictionary values</typeparam>
        /// <param name="values1">First array in the comparison</param>
        /// <param name="values2">Second array in the comparison</param>
        /// <returns>True if all items in both arrays in identical positions are Equal</returns>
        public static bool EqualsDeep<TItemKey, TItemValue>(IDictionary<TItemKey, TItemValue> values1,
            IDictionary<TItemKey, TItemValue> values2)
        {
            if (values1 == null && values2 == null)
                return true;
            if (values1 == null || values2 == null)
                return false;
            if (values1.Count != values2.Count)
                return false;
            foreach (var keyValuePair1 in values1)
            {
                TItemValue value2;
                if (!values2.TryGetValue(keyValuePair1.Key, out value2))
                    return false;
                if (!Equals(keyValuePair1.Value, value2))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Constructs a hash-code for an Array from all of the items in
        /// the array.
        /// </summary>
        /// <typeparam name="TItem">Type of the items in the array</typeparam>
        /// <param name="values">The Array instance</param>
        /// <returns>A hash-code value constructed from all items in the array</returns>
        public static int GetHashCodeDeep<TItem>(this IList<TItem> values)
        {
            return values.GetHashCodeDeep(v => v.GetHashCode());
        }

        public static int GetHashCodeDeep<TItem>(this IList<TItem> values, Func<TItem, int> getHashCode)
        {
            unchecked
            {
                int result = 0;
                foreach (TItem value in values)
                    result = (result * 397) ^ (!Equals(value, default(TItem)) ? getHashCode(value) : 0);
                return result;
            }
        }

        /// <summary>
        /// Checks if all elements in one list are <see cref="object.ReferenceEquals"/>
        /// with the elements in another list.
        /// </summary>
        /// <typeparam name="TItem">Type of the list elements</typeparam>
        /// <param name="values1">The first list in the comparison</param>
        /// <param name="values2">The second list in the comparison</param>
        /// <returns>True if all references in the lists are equal to each other</returns>
        public static bool ReferencesEqual<TItem>(IList<TItem> values1, IList<TItem> values2)
        {
            if (values1 == null && values2 == null)
                return true;
            if (values1 == null || values2 == null)
                return false;
            int count = values1.Count;
            if (count != values2.Count)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!ReferenceEquals(values1[i], values2[i]))
                    return false;
            }
            return true;
        }

        public static bool InnerReferencesEqual<TItem, TItemList>(IList<TItemList> values1, IList<TItemList> values2)
            where TItemList : IList<TItem>
        {
            if (values1 == null && values2 == null)
                return true;
            if (values1 == null || values2 == null)
                return false;
            if (values1.Count != values2.Count)
                return false;
            for (int i = 0; i < values1.Count; i++)
            {
                if (!ReferencesEqual(values1[i], values2[i]))
                    return false;
            }
            return true;
            
        }

        /// <summary>
        /// Enumerates two lists assigning references from the second list to
        /// entries in the first list, where they are equal.  Useful for maintaining
        /// reference equality when recalculating values. Similar to <see cref="Helpers.AssignIfEquals{T}"/>.
        /// </summary>
        /// <typeparam name="TItem">Type of the list elements</typeparam>
        /// <param name="values1">The first list in the comparison</param>
        /// <param name="values2">The second list in the comparison</param>
        public static void AssignIfEqualsDeep<TItem>(IList<TItem> values1, IList<TItem> values2)
        {
            if (values1 == null || values2 == null)
                return;
            for (int i = 0, len = Math.Min(values1.Count, values2.Count); i < len; i++)
            {
                if (Equals(values1[i], values2[i]))
                    values1[i] = values2[i];
            }
        }

        /// <summary>
        /// Sort an array and produce an output array that shows how the indexes of the
        /// elements have been reordered.  The indexing array can then be applied to a
        /// different array to follow the ordering of the initial array.
        /// </summary>
        /// <typeparam name="TItem">Type of array elements</typeparam>
        /// <param name="array">Array to sort</param>
        /// <param name="sortIndexes">Records how indexes were changed as a result of sorting</param>
        public static void Sort<TItem>(TItem[] array, out int[] sortIndexes)
        {
            sortIndexes = new int[array.Length];
            for (int i = 0; i < array.Length; i++)
                sortIndexes[i] = i;
            Array.Sort(array, sortIndexes);
        }

        /// <summary>
        /// Use when you have more than just one other array to sort. Otherwise, consider using Linq
        /// </summary>
        public static void Sort<TItem>(TItem[] array, params TItem[][] secondaryArrays)
        {
            int[] sortIndexes;
            Sort(array, out sortIndexes);
            int len = array.Length;
            TItem[] buffer = new TItem[len];
            foreach (var secondaryArray in secondaryArrays.Where(a => a != null))
                ApplyOrder(sortIndexes, secondaryArray, buffer);
        }

        /// <summary>
        /// Apply the ordering gotten from the sorting of an array (see Sort method above)
        /// to a new array.
        /// </summary>
        /// <typeparam name="TItem">Type of array elements</typeparam>
        /// <param name="sortIndexes">Array of indexes that recorded sort operations</param>
        /// <param name="array">Array to be reordered using the index array</param>
        /// <param name="buffer">An optional buffer to use to avoid allocating a new array and force in-place sorting</param>
        /// <returns>A sorted version of the original array</returns>
        public static TItem[] ApplyOrder<TItem>(int[] sortIndexes, TItem[] array, TItem[] buffer = null)
        {
            TItem[] ordered;
            int len = array.Length;
            if (buffer == null)
                ordered = new TItem[len];
            else
            {
                Array.Copy(array, buffer, len);
                ordered = array;
                array = buffer;
            }
            for (int i = 0; i < array.Length; i++)
                ordered[i] = array[sortIndexes[i]];
            return ordered;
        }

        /// <summary>
        /// Returns true if the given array is not in sort order.
        /// </summary>
        /// <param name="array"></param>
        /// <returns>True if array needs to be sorted</returns>
        public static bool NeedsSort(float[] array)
        {
            for (int i = 0; i < array.Length - 1; i++)
                if (array[i] > array[i + 1])
                    return true;
            return false;
        }

        /// <summary>
        /// Linear time function for finding the n-th item in a list and placing it at that location in
        /// the list while putting all values greater than it higher in the list and all values less
        /// than it lower in the list. That is the values in the list get partitioned by the Nth value
        /// but do not get fully sorted.
        /// </summary>
        public static TItem QNthItem<TItem>(this IList<TItem> list, int elementIndex) where TItem : IComparable<TItem>
        {
            if (elementIndex < 0 || list.Count <= elementIndex)
                throw new IndexOutOfRangeException($@"The element index {elementIndex} is outside the range of the {list.Count} element array.");
            
            int left = 0;
            int right = list.Count - 1;
            while (left < right)
            {
                TItem value = list[elementIndex];
                int splitLeft = left, splitRight = right;
                Split(list, value, ref splitLeft, ref splitRight);
                if (splitRight < elementIndex)
                    left = splitLeft;
                if (elementIndex < splitLeft)
                    right = splitRight;
            }
            return list[elementIndex];
        }

        private static void Split<TItem>(IList<TItem> list, TItem value, ref int left, ref int right) where TItem : IComparable<TItem>
        {
            // Left and right scan until the pointers cross
            do
            {
                while (list[left].CompareTo(value) < 0)
                    left++;
                while (value.CompareTo(list[right]) < 0)
                    right--;

                if (left <= right)
                {
                    // Swap
                    (list[left], list[right]) = (list[right], list[left]);

                    left++;
                    right--;
                }
            } while (left <= right);
        }
    }

    /// <summary>
    /// Read a potentially large array into a list of arrays in order to avoid very large memory allocations.
    /// We are trying to avoid not only memory fragmentation issues, but also the size limit of 2 gigabytes.
    /// </summary>
    public class BlockedArray<TItem> : IReadOnlyList<TItem>
    {
        public static readonly BlockedArray<TItem> EMPTY = new BlockedArray<TItem>();
        private readonly List<TItem[]> _blocks;
        private readonly int _itemCount;

        /// <summary>
        /// Empty array.
        /// </summary>
        public BlockedArray()
        {
        }

        /// <summary>
        /// Read an array into blocks.
        /// </summary>
        /// <param name="readItems">Function to read a number of items and return them in an array.</param>
        /// <param name="itemCount">Total number of items to read.</param>
        /// <param name="itemSize">Size of each item in bytes.</param>
        /// <param name="bytesPerBlock">Maximum size of a block in bytes.</param>
        /// <param name="progressMonitor">Optional progress monitor for reporting progress over long periods</param>
        /// <param name="status">Optional progress status object for reporting progress</param>
        public BlockedArray(Func<int, TItem[]> readItems, int itemCount, int itemSize, int bytesPerBlock,
            IProgressMonitor progressMonitor = null, IProgressStatus status = null)
        {
            Assume.IsTrue(itemSize < bytesPerBlock);    // Make sure these values aren't flipped

            _itemCount = itemCount;
            _blocks = new List<TItem[]>();

            var itemsPerBlock = bytesPerBlock/itemSize;
            int startPercent = status != null ? status.PercentComplete : 0;
            while (itemCount > 0)
            {
                _blocks.Add(readItems(Math.Min(itemCount, itemsPerBlock)));
                itemCount -= itemsPerBlock;

                if (progressMonitor != null && status != null)
                {
                    int currentPercent = (int)(100 - ((100.0 - startPercent) * itemCount) / _itemCount);
                    if (currentPercent != status.PercentComplete)
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(currentPercent));
                }
            }
        }

        public static BlockedArray<TItem> FromEnumerable(IEnumerable<TItem> enumerable, int itemCount, int itemSize,
            int bytesPerBlock, IProgressMonitor progressMonitor, IProgressStatus status)
        {
            using (var enumerator = enumerable.GetEnumerator())
            {
                return new BlockedArray<TItem>(count =>
                {
                    var array = new TItem[count];
                    for (int i = 0; i < count; i++)
                    {
                        Assume.IsTrue(enumerator.MoveNext());
                        array[i] = enumerator.Current;
                    }

                    return array;
                }, itemCount, itemSize, bytesPerBlock, progressMonitor, status);
            }
        }

        /// <summary>
        /// Copy a list into blocks.
        /// </summary>
        /// <param name="items">Items to copy.</param>
        /// <param name="itemSize">Size of each item in bytes.</param>
        /// <param name="bytesPerBlock">Maximum size of a block in bytes.</param>
        public BlockedArray(IList<TItem> items, int itemSize, int bytesPerBlock)
        {
            _itemCount = items.Count;
            _blocks = new List<TItem[]>();

            var itemsPerBlock = bytesPerBlock/itemSize;
            TItem[] block = null;
            for (int index = 0; index < _itemCount; index++)
            {
                var inBlockIndex = index%itemsPerBlock;
                if (inBlockIndex == 0)
                {
                    block = new TItem[Math.Min(_itemCount - index, itemsPerBlock)];
                    _blocks.Add(block);
                }
// ReSharper disable PossibleNullReferenceException
                block[inBlockIndex] = items[index];
// ReSharper restore PossibleNullReferenceException
            }
        }

        public BlockedArray(BlockedArrayList<TItem> items)
        {
            _itemCount = items.Count;
            _blocks = items.GetBlocks().ToList();
        }

        public static BlockedArray<TItem> Convert<TItemSrc>(BlockedArrayList<TItemSrc> blockedArrayList,
            Func<TItemSrc, TItem> converter)
        {
            return new BlockedArray<TItem>(blockedArrayList.GetBlocks()
                .Select(block => block.Select(converter).ToArray())
                .ToList(),
                blockedArrayList.Count);
        }

        private BlockedArray(List<TItem[]> blocks, int itemCount)
        {
            _itemCount = itemCount;
            _blocks = blocks;
        }

        /// <summary>
        /// Number of items in this array.
        /// </summary>
        public int Length { get { return _itemCount; } }

        public int Count { get { return Length; } }

        public IEnumerable<TItem[]> Blocks { get { return _blocks; } }

        /// <summary>
        /// Return the item corresponding to the given index.
        /// </summary>
        /// <param name="index">Array index.</param>
        public TItem this[int index]
        {
            get
            {
                if (index >= _itemCount)
                    throw new IndexOutOfRangeException();
                var blockLength = _blocks[0].Length;
                var blockIndex = index/blockLength;
                var itemIndex = index%blockLength;
                return _blocks[blockIndex][itemIndex];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            return _blocks.SelectMany(block => block).Take(_itemCount).GetEnumerator();
        }

        /// <summary>
        /// Write the array.
        /// </summary>
        /// <param name="writeAction">Action to write one array block.</param>
        public void WriteArray(Action<TItem[]> writeAction)
        {
            if (_blocks != null)
            {
                foreach (var block in _blocks)
                    writeAction(block);
            }
        }

        /// <summary>
        /// Write the array.
        /// </summary>
        /// <param name="writeAction">Action to write one array block.</param>
        /// <param name="startIndex">First index to write.</param>
        /// <param name="count">How many items to write.</param>
        public void WriteArray(Action<TItem[], int, int> writeAction, int startIndex, int count)
        {
            if (startIndex + count > _itemCount)
                throw new IndexOutOfRangeException();
            var blockLength = _blocks[0].Length;
            while (count > 0)
            {
                var blockIndex = startIndex/blockLength;
                var itemIndex = startIndex%blockLength;
                var writeCount = Math.Min(count, blockLength - itemIndex);
                writeAction(_blocks[blockIndex], itemIndex, writeCount);
                startIndex += writeCount;
                count -= writeCount;
            }
        }

        public BlockedArray<TItem> ChangeAll(Func<TItem, TItem> changeElement)
        {
            var newBlocks = new List<TItem[]>();
            foreach (var block in _blocks)
            {
                var newBlock = new TItem[block.Length];
                newBlocks.Add(newBlock);

                for (int i = 0; i < block.Length; i++)
                    newBlock[i] = changeElement(block[i]);
            }
            return new BlockedArray<TItem>(newBlocks, _itemCount);
        }
    }

    public class BlockedArrayList<TItem> : IList<TItem>
    {
        private List<List<TItem>> _blocks = new List<List<TItem>>{new List<TItem>()};
        private int _itemCount;
        private readonly int _itemsPerBlock;

        public BlockedArrayList(int itemSize, int bytesPerBlock)
        {
            _itemsPerBlock = bytesPerBlock/itemSize;
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            return _blocks.SelectMany(block => block).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(TItem item)
        {
            var block = _blocks.Last();
            if (block.Count >= _itemsPerBlock)
            {
                block = new List<TItem>();
                _blocks.Add(block);
            }
            block.Add(item);
            _itemCount++;
        }

        public void AddRange(IList<TItem> chromTransitions)
        {
            // CONSIDER: Make this faster than adding one at a time?
            foreach (var t in chromTransitions)
            {
                Add(t);
            }
        }

        public void AddRange(BlockedArray<TItem> chromTransitions)
        {
            int transferCount = chromTransitions.Count;
            int blockIndex = 0;
            int itemIndex = 0;
            var chromTransitionBlocks = chromTransitions.Blocks.ToArray();

            while (transferCount > 0)
            {
                var blockSrc = chromTransitionBlocks[blockIndex];
                int copyCount = blockSrc.Length - itemIndex;

                var blockDest = _blocks.Last();
                if (blockDest.Count >= _itemsPerBlock)
                {
                    // Pre-allocate a new list to the smaller of the number of items
                    // to copy or the total items per block
                    blockDest = new List<TItem>(Math.Min(copyCount, _itemsPerBlock));
                    _blocks.Add(blockDest);
                }
                // Copy everything remaining in current source block or the maximum left in the destination block
                int remainder = _itemsPerBlock - blockDest.Count;
                if (copyCount <= remainder)
                {
                    blockDest.AddRange(blockSrc.Skip(itemIndex).Take(copyCount));
                    blockIndex++;
                    itemIndex = 0;
                }
                else
                {
                    copyCount = remainder;
                    blockDest.AddRange(blockSrc.Skip(itemIndex).Take(copyCount));
                    itemIndex += copyCount;
                }
                transferCount -= copyCount;
                _itemCount += copyCount;
            }
        }

        public void Clear()
        {
            _blocks = new List<List<TItem>> { new List<TItem>() };
            _itemCount = 0;
        }

        public bool Contains(TItem item)
        {
            return _blocks.Contains(l => l.Contains(item));
        }

        public void CopyTo(TItem[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        public bool Remove(TItem item)
        {
            throw new NotSupportedException();
        }

        public int Count { get { return _itemCount; } }
        public bool IsReadOnly { get { return false; } }

        public IEnumerable<TItem[]> GetBlocks()
        {
            return _blocks.Select(b => b.ToArray());
        }

        public int IndexOf(TItem item)
        {
            int index = 0;
            foreach (var block in _blocks)
            {
                foreach (var itemTest in block)
                {
                    if (Equals(item, itemTest))
                        return index;
                    index++;
                }
            }
            return -1;
        }

        public TItem this[int index]
        {
            get
            {
                if (index >= _itemCount)
                    throw new IndexOutOfRangeException();
                var blockIndex = index / _itemsPerBlock;
                var itemIndex = index % _itemsPerBlock;
                return _blocks[blockIndex][itemIndex];
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public void Insert(int index, TItem item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        public BlockedArray<TItem> ToBlockedArray()
        {
            return new BlockedArray<TItem>(this);
        }

        public void Reorder(IEnumerable<int> newOrder)
        {
            var blockNext = new List<TItem>(_blocks[0].Count);
            var blocksNew = new List<List<TItem>>(_blocks.Count) { blockNext };
            foreach (var i in newOrder)
            {
                if (blockNext.Count == _itemsPerBlock)
                {
                    blockNext = new List<TItem>(_blocks[blocksNew.Count].Count);
                    blocksNew.Add(blockNext);
                }
                blockNext.Add(this[i]);
            }
            _blocks = blocksNew;
        }

        public void Sort()
        {
            Sort(Comparer<TItem>.Default.Compare);
        }

        public void Sort(Comparison<TItem> compare)
        {
            foreach (var block in _blocks)
                block.Sort(compare);
            if (_blocks.Count < 2)
                return;

            try
            {
                // Merge sort the blocks into new list
                var nextIndexes = new int[_blocks.Count];
                var blockNext = new List<TItem>(_blocks[0].Count);
                var blocksNew = new List<List<TItem>>(_blocks.Count) { blockNext };
                for (int i = 0; i < _itemCount; i++)
                {
                    if (blockNext.Count == _itemsPerBlock)
                    {
                        blockNext = new List<TItem>(_blocks[blocksNew.Count].Count);
                        blocksNew.Add(blockNext);
                    }
                    int iBlockMin = 0;
                    for (int iBlock = 1; iBlock < _blocks.Count; iBlock++)
                    {
                        int iNext = nextIndexes[iBlock];
                        int iMin = nextIndexes[iBlockMin];
                        if (iNext >= _blocks[iBlock].Count)
                            continue;
                        if (iMin >= _blocks[iBlockMin].Count || compare(_blocks[iBlock][iNext], _blocks[iBlockMin][iMin]) < 1)
                            iBlockMin = iBlock;
                    }
                    blockNext.Add(_blocks[iBlockMin][nextIndexes[iBlockMin]]);
                    nextIndexes[iBlockMin]++;
                }
                _blocks = blocksNew;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
                throw;
            }
        }
    }

    /// <summary>
    /// A set of generic, static helper functions.
    /// </summary>
    public static partial class Helpers
    {
        /// <summary>
        /// Swaps two reference values in memory, making each contain
        /// the reference the other started with.
        /// </summary>
        /// <typeparam name="TItem">Type of the two values</typeparam>
        /// <param name="val1">Left value</param>
        /// <param name="val2">Right value</param>
        public static void Swap<TItem>(ref TItem val1, ref TItem val2)
        {
            TItem tmp = val1;
            val1 = val2;
            val2 = tmp;
        }

        /// <summary>
        /// Assigns the a source reference to the intended destination,
        /// only if they are <see cref="object.Equals(object,object)"/>.
        /// 
        /// This can be useful in combination with immutable objects,
        /// allowing the caller choose an existing object already referenced
        /// in a data structure over a newly created instance, if the two
        /// are identical in value.
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        public static void AssignIfEquals<TItem>(ref TItem dest, TItem src)
        {
            if (Equals(dest, src))
                dest = src;
        }

        /// <summary>
        /// Compare two IEnumerable instances for equality.
        /// </summary>
        /// <typeparam name="TItem">The type of element being enumerated</typeparam>
        /// <param name="e1">The first IEnumerable</param>
        /// <param name="e2">The second IEnumberable</param>
        /// <returns>True if the two IEnumerables enumerate over equal objects</returns>
        public static bool Equals<TItem>(IEnumerable<TItem> e1, IEnumerable<TItem> e2)
        {
            using IEnumerator<TItem> enum1 = e1.GetEnumerator();
            using IEnumerator<TItem> enum2 = e2.GetEnumerator();
            bool b1, b2;
            while (MoveNext(enum1, out b1, enum2, out b2))
            {
                if (!Equals(enum1.Current, enum2.Current))
                    break;
            }

            // If both enums have advanced to completion without finding
            // a difference, then they are equal.
            return (!b1 && !b2);
        }

        /// <summary>
        /// Call MoveNext on two IEnumerator instances in one operation,
        /// but avoid short-circuiting of (e1.MoveNext() && e2.MoveNext),
        /// and pass the return values of both as out parameters.
        /// </summary>
        /// <param name="e1">First Enumerator to advance</param>
        /// <param name="b1">Return value of e1.MoveNext()</param>
        /// <param name="e2">Second Enumerator to advance</param>
        /// <param name="b2">Return value of e2.MoveNext()</param>
        /// <returns>True if both calls to MoveNext() succeed</returns>
        private static bool MoveNext(IEnumerator e1, out bool b1,
            IEnumerator e2, out bool b2)
        {
            b1 = e1.MoveNext();
            b2 = e2.MoveNext();
            return b1 && b2;
        }

        /// <summary>
        /// Parses an enum value from a string, returning a default value,
        /// if the string fails to parse.
        /// </summary>
        /// <typeparam name="TEnum">The enum type</typeparam>
        /// <param name="value">The string to parse</param>
        /// <param name="defaultValue">The value to return, if parsing fails</param>
        /// <returns>An enum value of type <see cref="TEnum"/></returns>
        public static TEnum ParseEnum<TEnum>(string value, TEnum defaultValue) where TEnum : struct
        {
            if (Enum.TryParse(value, true, out TEnum result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Given a localized string and an array of localized strings with the
        /// index of each localized string matching the desired enum value for
        /// an enum type, returns the enum value corresponding to the localized string.
        /// </summary>
        /// <typeparam name="TEnum">The enum type</typeparam>
        /// <param name="value">The localized string for which the enum value is desired</param>
        /// <param name="localizedStrings">Array of all localized strings</param>
        /// <returns>An enum value of type <see cref="TEnum"/></returns>
        public static TEnum EnumFromLocalizedString<TEnum>(string value, string[] localizedStrings)
        {
            int i = localizedStrings.IndexOf(v => Equals(v, value));
            if (i == -1)
                throw new ArgumentException(string.Format(@"The string '{0}' does not match an enum value ({1})", value, string.Join(@", ", localizedStrings)));
            return (TEnum) (object) i;            
        }

        public static TEnum EnumFromLocalizedString<TEnum>(string value, string[] localizedStrings, TEnum defaultValue)
        {
            int i = localizedStrings.IndexOf(v => Equals(v, value??string.Empty));
            return (i == -1 ? defaultValue : (TEnum) (object) i);
        }

        /// <summary>
        /// Enumerate all possible values of the given enum type.
        /// </summary>
        public static IEnumerable<TEnum> GetEnumValues<TEnum>()
        {
            return Enum.GetValues(typeof (TEnum)).Cast<TEnum>();
        }

        public static int CountEnumValues<TEnum>()
        {
            return Enum.GetValues(typeof (TEnum)).Length;
        }

        public static string MakeId(IEnumerable<char> name)
        {
            return MakeId(name, false);
        }

        public static string MakeId(IEnumerable<char> name, bool capitalize)
        {
            StringBuilder sb = new StringBuilder();
            char lastC = '\0'; 
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (lastC == ' ')
                        sb.Append('_');
                    lastC = c;
                    if (capitalize && sb.Length == 0)
                        sb.Append(c.ToString(CultureInfo.InvariantCulture).ToUpperInvariant());
                    else
                        sb.Append(c);
                }
                // Must start with a letter or digit
                else if (lastC != '\0')
                {
                    // After the start _ okay (dashes turned out to be problematic)
                    if (c == '_' /* || c == '-'*/)
                        sb.Append(lastC = c);
                    // All other characters are replaced with _, but once the next
                    // letter or number is seen.
                    else if (char.IsLetterOrDigit(lastC))
                        lastC = ' ';
                }
            }
            return sb.ToString();
        }

        // ReSharper disable LocalizableElement
        private static readonly Regex REGEX_XML_ID = new Regex("/^[:_A-Za-z][-.:_A-Za-z0-9]*$/");
        private const string XML_ID_FIRST_CHARS = ":_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private const string XML_ID_FOLLOW_CHARS = "-.:_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private const string XML_NON_ID_SEPARATOR_CHARS = ";[]{}()!|\\/\"'<>";
        private const string XML_NON_ID_PUNCTUATION_CHARS = ",?";
        // ReSharper restore LocalizableElement

        public static string MakeXmlId(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException(
                    UtilResources.Helpers_MakeXmlId_Failure_creating_XML_ID_Input_string_may_not_be_empty);
            if (REGEX_XML_ID.IsMatch(name))
                return name;

            var sb = new StringBuilder();
            int i = 0;
            if (XML_ID_FIRST_CHARS.Contains(name[i]))
                sb.Append(name[i++]);
            else
            {
                sb.Append('_');
                // If the first character is not allowable, advance past it.
                // Otherwise, keep it in the ID.
                if (!XML_ID_FOLLOW_CHARS.Contains(name[i]))
                    i++;
            }
            for (; i < name.Length; i++)
            {
                char c = name[i];
                if (XML_ID_FOLLOW_CHARS.Contains(c))
                    sb.Append(c);
                else if (char.IsWhiteSpace(c))
                    sb.Append('_');
                else if (XML_NON_ID_SEPARATOR_CHARS.Contains(c))
                    sb.Append(':');
                else if (XML_NON_ID_PUNCTUATION_CHARS.Contains(c))
                    sb.Append('.');
                else
                    sb.Append('-');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Given a proposed name and a set of existing names, returns a unique name by adding
        /// or incrementing an integer suffix.
        /// </summary>
        /// <param name="name">A proposed name to add</param>
        /// <param name="set">A set of existing names</param>
        /// <returns>A new unique name that can be safely added to the existing set without name conflict</returns>
        public static string GetUniqueName(string name, ICollection<string> set)
        {
            return GetUniqueName(name, s => !set.Contains(s));
        }

        public static string GetUniqueName(string name, Func<string, bool> isUnique)
        {
            if (isUnique(name))
                return name;

            int num = 1;
            // If the name has an integer suffix, start searching with the base name
            // and the integer suffix incremented by 1.
            int i = GetIntSuffixStart(name);
            if (i < name.Length)
            {
                num = int.Parse(name.Substring(i)) + 1;
                name = name.Substring(0, i);
            }
            // Loop until a unique base name and integer suffix combination is found.
            while (!isUnique(name + num))
                num++;
            return name + num;
        }

        /// <summary>
        /// Given a name returns the start index of an integer suffix, if the name has one,
        /// or the length of the string, if no integer suffix is present.
        /// </summary>
        /// <param name="name">A name to analyze</param>
        /// <returns>The starting position of an integer suffix or the length of the string, if the name does not have one</returns>
        private static int GetIntSuffixStart(string name)
        {
            for (int i = name.Length; i > 0; i--)
            {
                if (!int.TryParse(name.Substring(i - 1), out _))
                    return i;
            }
            return 0;
        }

        public static List<string> EnsureUniqueNames(List<string> names, HashSet<string> reservedNames = null)
        {
            var setUsedNames = reservedNames ?? new HashSet<string>();
            var result = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string baseName = names[i];
                // Make sure the next name added is unique
                string name = (baseName.Length != 0 ? baseName : @"1");
                for (int suffix = 2; setUsedNames.Contains(name); suffix++)
                    name = baseName + suffix;
                result.Add(name);
                // Add this name to the used set
                setUsedNames.Add(name);
            }
            return result;
        }

        /// <summary>
        /// Count the number of lines in the file specified.
        /// </summary>
        /// <param name="f">The filename to count lines in.</param>
        /// <returns>The number of lines in the file.</returns>
        public static long CountLinesInFile(string f)
        {
            long count = 0;
            using (StreamReader r = new StreamReader(f))
            {
                while (r.ReadLine() != null)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Count the number of lines in the string specified.
        /// </summary>
        /// <param name="s">The string to count lines in.</param>
        /// <returns>The number of lines in the string.</returns>
        public static long CountLinesInString(string s)
        {
            long count = 1;
            int start = 0;
            while ((start = s.IndexOf('\n', start)) != -1)
            {
                count++;
                start++;
            }
            return count;
        }

        private const char LABEL_SEP_CHAR = '_';
        private const string ELIPSIS = "...";
        private static readonly char[] SPACE_CHARS = { '_', '-', ' ', '.', ',' };

        /// <summary>
        /// Finds repetitive text in labels and removes the text to save space.
        /// </summary>
        /// <param name="labels">The labels we are removing redundant text from.</param>
        /// <param name="startLabelIndex">Index we want to start looking at, in case the Expected/Library
        /// label is showing.</param>
        /// <returns>Return </returns>
        public static bool RemoveRepeatedLabelText(string[] labels, int startLabelIndex)
        {
            // Check to see if there are any labels. 
            if (labels.Length == startLabelIndex)
                return false;

            // Creat a normalized set of labels to test for repeated text
            string[] labelsRemove = new string[labels.Length];

            Array.Copy(labels, labelsRemove, labels.Length);

            if (startLabelIndex != 0)
            {
                labelsRemove = new string[labelsRemove.Length - startLabelIndex];
                Array.Copy(labels, startLabelIndex, labelsRemove, 0, labelsRemove.Length);
            }

            for (int i = 0; i < labelsRemove.Length; i++)
                labelsRemove[i] = NormalizeSeparators(labelsRemove[i]);

            var labelParts = labelsRemove[0].Split(LABEL_SEP_CHAR);

            // If all labels start with the first part
            string replaceString = labelParts[0];
            string partFirst = replaceString + LABEL_SEP_CHAR;
            if (!labelsRemove.Contains(label => !label.StartsWith(partFirst)))
            {
                RemoveString(labels, startLabelIndex, replaceString, ReplaceLocation.start);
                return true;
            }

            // If all labels end with the last part
            replaceString = labelParts[labelParts.Length - 1];
            string partLast = LABEL_SEP_CHAR + replaceString;
            if (!labelsRemove.Contains(label => !label.EndsWith(partLast)))
            {
                RemoveString(labels, startLabelIndex, replaceString, ReplaceLocation.end);
                return true;
            }

            for (int i = 1 ; i < labelParts.Length - 1; i++)
            {
                replaceString = labelParts[i];
                if (string.IsNullOrEmpty(replaceString))
                    continue;
                string partMiddle = LABEL_SEP_CHAR + replaceString + LABEL_SEP_CHAR;
                // If all labels contain the middle part
                if (!labelsRemove.Contains(label => !label.Contains(partMiddle)))
                {
                    RemoveString(labels, startLabelIndex, replaceString, ReplaceLocation.middle);
                    return true;
                }
            }

            return false;
        }

        private static bool IsSpaceChar(char c)
        {
            return SPACE_CHARS.Contains(c);
        }

        private static string NormalizeSeparators(string startLabelText)
        {
            startLabelText = startLabelText.Replace(ELIPSIS, LABEL_SEP_CHAR.ToString(CultureInfo.InvariantCulture));
            foreach (var spaceChar in SPACE_CHARS)
            {
                startLabelText = startLabelText.Replace(spaceChar, LABEL_SEP_CHAR);
            }

            return startLabelText;
        }

        /// <summary>
        /// Truncates labels.
        /// </summary>
        /// <param name="labels">Labels text will be removed from.</param>
        /// <param name="startLabelIndex">Index we want to start looking at, in case the Expected/Library
        /// label is showing.</param>
        /// <param name="replaceString">Text being removed from labels.</param>
        /// <param name="location">Expected location of the replacement text</param>
        public static void RemoveString(string[] labels, int startLabelIndex, string replaceString, ReplaceLocation location)
        {
            for (int i = startLabelIndex; i < labels.Length; i++)
                labels[i] = RemoveString(labels[i], replaceString, location);
        }

        public enum ReplaceLocation {start, middle, end}

        private static string RemoveString(string label, string replaceString, ReplaceLocation location)
        {
            int startIndex = -1;
            while ((startIndex = label.IndexOf(replaceString, startIndex + 1, StringComparison.Ordinal)) != -1)
            {
                int endIndex = startIndex + replaceString.Length;
                // Not start string and does not end with space
                if ((startIndex != 0 && !IsSpaceChar(label[startIndex - 1])) || 
                    (startIndex == 0 && location != ReplaceLocation.start))
                    continue;
                
                // Not end string and does not start with space
                if ((endIndex != label.Length && !IsSpaceChar(label[endIndex])) ||
                    (endIndex == label.Length && location != ReplaceLocation.end))
                    continue;
                
                bool elipsisSeen = false;
                bool middle = true;
                // Check left of the string for the start of the label or a space char
                if (startIndex == 0)
                    middle = false;
                else if (startIndex >= ELIPSIS.Length && label.LastIndexOf(ELIPSIS, startIndex, StringComparison.Ordinal) == startIndex - ELIPSIS.Length)
                    elipsisSeen = true;
                else
                    startIndex--;
                
                // Check right of the string for the end of the label or a space char
                if (endIndex == label.Length)
                    middle = false;
                else if (label.IndexOf(ELIPSIS, endIndex, StringComparison.Ordinal) == endIndex)
                    elipsisSeen = true;
                else
                    endIndex++;
                label = label.Remove(startIndex, endIndex - startIndex);
                // Insert an elipsis, if this is in the middle and no elipsis has been seen
                if (middle && !elipsisSeen && location == ReplaceLocation.middle)
                    label = label.Insert(startIndex, ELIPSIS);
                return label;
            }
            return label;
        }

        public static string TruncateString(string s, int length)
        {
            return s.Length <= length ? s : s.Substring(0, length - ELIPSIS.Length) + ELIPSIS;
        }

        // Detect the use of ReSharper code coverage, memory profiling etc, which may affect timing
        //
        // Per https://youtrack.jetbrains.com/issue/PROF-1093
        // "Set JETBRAINS_DPA_AGENT_ENABLE=0 environment variable for user apps started from dotTrace, and JETBRAINS_DPA_AGENT_ENABLE=1
        // in case of dotCover and dotMemory."

        public static void WrapAndThrowException(Exception x)
        {
            // The thrown exception needs to be preserved to preserve
            // the original stack trace from which it was thrown.  In some cases,
            // its type must also be preserved, because existing code handles certain
            // exception types.  If this case threw only TargetInvocationException,
            // then more frequently the code would just have to have a blanket catch
            // of the base exception type, which could hide coding errors.
            if (x is InvalidDataException)
                throw new InvalidDataException(x.Message, x);
            if (x is IOException)
                throw new IOException(x.Message, x);
            if (x is OperationCanceledException)
                throw new OperationCanceledException(x.Message, x);
            if (x is UnauthorizedAccessException)
                throw new UnauthorizedAccessException(x.Message, x);
            throw new TargetInvocationException(x.Message, x);            
        }

        public static double? ParseNullableDouble(string s)
        {
            double d;
            return double.TryParse(s, out d) ? d : (double?)null;
        }

        public static string NullableDoubleToString(double? d)
        {
            return d.HasValue ? d.Value.ToString(LocalizationHelper.CurrentCulture) : String.Empty;
        }
    }

    public static class MathEx
    {
        public static double RoundAboveZero(float value, int startDigits, int mostDigits)
        {
            for (int i = startDigits; i <= mostDigits; i++)
            {
                double rounded = Math.Round(value, i);
                if (rounded > 0)
                    return rounded;
            }
            return 0;
        }
    }

    public static class ExceptionUtil
    {
        public static string GetMessage(Exception ex)
        {
            // Drill down to see if the innermost exception was an out-of-memory exception.
            var innerException = ex;
            while (innerException.InnerException != null)
                innerException = innerException.InnerException;
            if (innerException is OutOfMemoryException)
            {
                string memoryMessage = String.Format(Resources.SkylineWindow_CompleteProgressUI_Ran_Out_Of_Memory, Program.Name);
                if (!Install.Is64Bit && Environment.Is64BitOperatingSystem)
                {
                    memoryMessage += String.Format(Resources.SkylineWindow_CompleteProgressUI_version_issue, Program.Name);
                }
                return TextUtil.LineSeparate(ex.Message, memoryMessage);
            }
            return ex.Message;
        }

        /// <summary>
        /// Returns text to be used when reporting an unhandled exception to the Skyline.ms.
        /// </summary>
        public static string GetExceptionText(Exception exception, StackTrace stackTraceExceptionCaughtAt)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(exception.ToString());
            if (stackTraceExceptionCaughtAt != null)
            {
                stringBuilder.AppendLine(@"Exception caught at: ");
                stringBuilder.AppendLine(stackTraceExceptionCaughtAt.ToString());
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Returns true if the exception is not something which could happen while trying to read
        /// from disk.
        /// Exception such as these should be displayed to the user with <see cref="Alerts.ReportErrorDlg"/>
        /// so that they can report them as bugs.
        /// </summary>
        public static bool IsProgrammingDefect(Exception exception)
        {
            if (exception is InvalidDataException 
                || exception is IOException 
                || exception is UnauthorizedAccessException)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calls either <see cref="MessageDlg.ShowWithException"/> or <see cref="Program.ReportException"/> depending
        /// on what <see cref="IsProgrammingDefect"/> returns.
        /// <param name="parent">Parent window for message dialog</param>
        /// <param name="exception">The exception that was caught</param>
        /// <param name="message">Optional message which summarizes what Skyline was trying to do when the exception happened,
        /// to be inserted on a separate line before the exception's message.</param>
        /// </summary>
        public static void DisplayOrReportException(IWin32Window parent, Exception exception, string message = null)
        {
            if (IsProgrammingDefect(exception))
            {
                Program.ReportException(exception);
            }
            else
            {
                string fullMessage = exception.Message;
                if (string.IsNullOrEmpty(message))
                {
                    fullMessage = TextUtil.LineSeparate(message, fullMessage);
                }
                MessageDlg.ShowWithException(parent, fullMessage, exception);
            }
        }
    }

    public class Alarms
    {
        private readonly Dictionary<object, AlarmInfo> _timers =
            new Dictionary<object, AlarmInfo>();

        private class AlarmInfo
        {
            public Timer Timer;
            public long Ticks;
        }

        public void Run(Control control, int milliseconds, object id, Action action)
        {
            try
            {
                control.Invoke(new Action(() =>
                {
                    lock (_timers)
                    {
                        var alarmTicks = DateTime.Now.Ticks + milliseconds*TimeSpan.TicksPerMillisecond;
                        if (_timers.ContainsKey(id))
                        {
                            if (_timers[id].Ticks <= alarmTicks)
                                return;
                            _timers[id].Timer.Dispose();
                        }
                        var timer = new Timer {Interval = milliseconds};
                        timer.Tick += (sender, args) => TimerTick(id, action);
                        _timers[id] = new AlarmInfo {Timer = timer, Ticks = alarmTicks};
                        timer.Start();
                    }
                }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void TimerTick(object id, Action action)
        {
            lock (_timers)
            {
                var alarmInfo = _timers[id];
                _timers.Remove(id);
                alarmInfo.Timer.Dispose();
            }
            action();
        }
    }

    public static class SecurityProtocolInitializer
    {
        // Make sure we can negotiate with HTTPS servers that demand modern TLS levels
        // The current recommendation from MSFT for future-proofing this https://docs.microsoft.com/en-us/dotnet/framework/network-programming/tls
        // is don't specify TLS levels at all, let the OS decide. But we worry that this will mess up Win7 and Win8 installs, so we continue to specify explicitly.
        public static void Initialize()
        {
            try
            {
                var Tls13 = (SecurityProtocolType)12288; // From decompiled SecurityProtocolType - compiler has no definition for some reason
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | Tls13;
            }
            catch (NotSupportedException)
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12; // Probably an older Windows Server
            }
        }
    }

    /// <summary>
    /// Creates a string representing a UTC time and offset to local time zone, per ISO 8601 standard
    /// </summary>
    public class TimeStampISO8601
    {
        public TimeStampISO8601(DateTime timeStampUTC)
        {
            Assume.IsTrue(timeStampUTC.Kind == DateTimeKind.Utc); // We only deal in UTC
            TimeStampUTC = timeStampUTC;
            TimeZoneOffset = TimeZoneInfo.Local.GetUtcOffset(TimeStampUTC); // UTC offset e.g. -8 for Seattle whn not on DST
        }

        public TimeStampISO8601() : this(DateTime.UtcNow)
        {
        }

        public DateTime TimeStampUTC { get; } // UTC time of creation
        public TimeSpan TimeZoneOffset { get; } // UTC offset at time of creation e.g. -8 for Seattle when not on DST, -7 when DST  

        public override string ToString()
        {
            var localTime = TimeStampUTC + TimeZoneOffset;
            var tzShift = TimeZoneOffset.TotalHours; // Decimal hours eg 8.5 or -0.5 etc
            return localTime.ToString(@"s", DateTimeFormatInfo.InvariantInfo) +
                   (tzShift == 0
                       ? @"Z"
                       : (tzShift < 0 ? @"-" : @"+") + TimeZoneOffset.ToString(@"hh\:mm"));
        }
    }

}
