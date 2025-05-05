/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
    /// Exposes a set of generic Array extension utility functions.
    /// </summary>
    public static class ArraySortUtil
    {
        private static void SwapItems<TItem>(ref TItem val1, ref TItem val2)
        {
            TItem tmp = val1;
            val1 = val2;
            val2 = tmp;
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
        /// Use when you have more than just one ArraySegment to sort.
        /// </summary>
        public static void Sort<TItem>(ArraySegment<TItem> arraySegment, params ArraySegment<TItem>?[] secondaryArrays) where TItem : IComparable<TItem>
        {
            // Check for presorted
            var presorted = true;
            var end = arraySegment.Offset + arraySegment.Count;
            for (var i = arraySegment.Offset+1; i < end; i++)
            {
                // ReSharper disable once PossibleNullReferenceException
                if (arraySegment.Array[i-1].CompareTo(arraySegment.Array[i]) > 0)
                {
                    presorted = false;
                    break;
                }
            }
            if (presorted)
            {
                return;
            }
                
            var sortIndexes = new int[arraySegment.Array!.Length];
            for (var i = 0; i < arraySegment.Count; i++)
            {
                sortIndexes[arraySegment.Offset+i] = i;
            }
            Array.Sort(arraySegment.Array, sortIndexes, arraySegment.Offset, arraySegment.Count);
            var len = arraySegment.Count;
            var buffer = new TItem[len];
            foreach (var secondaryArray in secondaryArrays.Where(a => a?.Array != null))
            {
                var asList = secondaryArray.Value as IList<TItem>;
                for (var i = 0; i <len; i++)
                {
                    buffer[i] = asList[sortIndexes[arraySegment.Offset + i]];
                }
                // ReSharper disable once AssignNullToNotNullAttribute
                Array.Copy(buffer, 0, secondaryArray.Value.Array, secondaryArray.Value.Offset, len);
            }
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
    }
}
