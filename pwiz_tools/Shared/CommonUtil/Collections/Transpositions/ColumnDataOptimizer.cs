/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace pwiz.Common.Collections.Transpositions
{
    /// <summary>
    /// Reduces the amount of memory that is used by collections of <see cref="ColumnData"/>.
    /// </summary>
    public class ColumnDataOptimizer<T>
    {
        public ColumnDataOptimizer(ValueCache valueCache, ColumnOptimizeOptions options)
        {
            Options = options;
            ValueCache = options.UseValueCache ? valueCache : null;
            ItemSize = ComputeItemSize();
        }

        
        /// <summary>
        /// The amount of memory that each item in the list is assumed to take.
        /// This does not need to be exact. It is used to decide whether a
        /// list can be made smaller by converting it into a <see cref="FactorList{T}"/> that is
        /// indexed with a <see cref="ByteList"/>.
        /// </summary>
        public int ItemSize { get; private set; }

        public ColumnOptimizeOptions Options { get; private set; }

        public static int ComputeItemSize()
        {
            if (typeof(T).IsClass)
            {
                return IntPtr.Size;
            }

            return Marshal.ReadInt32(typeof(T).TypeHandle.Value, 4);
        }
        
        public ValueCache ValueCache { get; private set; }
        
        /// <summary>
        /// Returns a new list of ColumnData objects with a smaller memory footprint.
        /// The optimizations are made by <ul>
        /// <li>Finding ColumnData's where all of the values are the same and replacing them with <see cref="ColumnData.ForConstant{T}"/></li>
        /// <li>Finding ColumnData's with identical values in them and using the same <see cref="ImmutableList"/> for each of them</li>
        /// <li>For lists with fewer than 255 unique values, replace them with a <see cref="FactorList{T}"/> indexed with a <see cref="ByteList"/></li>
        /// </ul>
        /// </summary>
        public IEnumerable<ColumnData<T>> OptimizeMemoryUsage(IEnumerable<ColumnData<T>> columnDataList)
        {
            var columnInfos = GetColumnDataInfos(columnDataList);

            var columnDataValues = columnInfos.Where(col => col.ColumnValues != null).Select(col => col.ColumnValues)
                .Distinct().ToList();
            
            var optimizedColumnValues = OptimizeColumnLists(columnDataValues);
            
            foreach (var columnInfo in columnInfos)
            {
                ColumnData<T> optimizedColumn;
                if (columnInfo.ColumnValues == null || !optimizedColumnValues.TryGetValue(columnInfo.ColumnValues, out var storedList))
                {
                    optimizedColumn = columnInfo.OriginalColumnData;
                }
                else
                {
                    if (ValueCache != null && true == storedList?.IsImmutableList)
                    {
                        var columnValues = storedList.ToImmutableList();
                        if (columnValues == null)
                        {
                            throw new InvalidOperationException();
                        }
                        optimizedColumn = ColumnData<T>.ForValues(ValueCache.CacheValue(columnValues));
                    }
                    else
                    {
                        optimizedColumn = storedList;
                    }
                }
#if DEBUG
                if (!Equals(columnInfo.OriginalColumnData, optimizedColumn))
                {
                    throw new InvalidOperationException();
                }

#endif
                yield return optimizedColumn;
            }
        }

        private List<ColumnDataInfo> GetColumnDataInfos(IEnumerable<ColumnData<T>> columnDataList)
        {
            var localValueCache = new ValueCache();
            var columnInfos = new List<ColumnDataInfo>();
            foreach (var columnData in columnDataList)
            {
                var columnValues = (columnData as ColumnData<T>.List)?.ToImmutableList();
                if (columnValues == null)
                {
                    // ColumnData is either empty or a constant: can't be optimized any more
                    columnInfos.Add(new ColumnDataInfo(columnData, null));
                    continue;
                }
                if (ValueCache != null && ValueCache.TryGetCachedValue(ref columnValues))
                {
                    // If we find the values in the ValueCache, use that
                    columnInfos.Add(new ColumnDataInfo(ColumnData.ForList(columnValues), HashedObject.ValueOf(columnValues)));
                }
                else
                {
                    columnInfos.Add(new ColumnDataInfo(columnData, localValueCache.CacheValue(HashedObject.ValueOf(columnValues))));
                }
            }

            return columnInfos;
        }

        public Dictionary<HashedObject<ImmutableList<T>>, ColumnData<T>.List> OptimizeColumnLists(
            IList<HashedObject<ImmutableList<T>>> columnDataValues)
        {
            IList<ListInfo> remainingLists = new List<ListInfo>();
            var storedLists = new Dictionary<HashedObject<ImmutableList<T>>, ColumnData<T>.List>();
            foreach (var list in columnDataValues)
            {
                if (list == null)
                {
                    continue;
                }
                if (Options.UseFactorLists)
                {
                    remainingLists.Add(new ListInfo(list));
                }
            }

            if (remainingLists.Count == 0)
            {
                return storedLists;
            }

            // ReSharper disable once RedundantAssignment
            remainingLists = MakeFactorLists(storedLists, remainingLists);
            return storedLists;
        }

        protected IList<ListInfo> MakeFactorLists(
            Dictionary<HashedObject<ImmutableList<T>>, ColumnData<T>.List> storedLists, IList<ListInfo> remainingLists)
        {
            if (ItemSize <= 1)
            {
                return remainingLists;
            }

            var mostUniqueItems = remainingLists.Max(list => list.UniqueValues.Count);
            if (mostUniqueItems >= byte.MaxValue)
            {
                return remainingLists;
            }

            var allUniqueItems = remainingLists.SelectMany(listInfo => listInfo.UniqueValues)
                .Where(v => !Equals(v, default(T))).Distinct().ToList();
            if (allUniqueItems.Count >= byte.MaxValue)
            {
                return remainingLists;
            }

            int totalItemCount = remainingLists.Sum(list => list.ColumnValues.Value.Count);
            var potentialSavings = (totalItemCount - allUniqueItems.Count) * (ItemSize - 1) - IntPtr.Size * remainingLists.Count;
            if (potentialSavings <= 0)
            {
                return remainingLists;
            }

            var factorListBuilder = new FactorList<T>.Builder(remainingLists.SelectMany(listInfo => listInfo.UniqueValues));
            foreach (var listInfo in remainingLists)
            {
                var storedData = ColumnData.ForList(factorListBuilder.MakeFactorList(listInfo.ColumnValues.Value));
                if (storedData is ColumnData<T>.List storedList)
                {
                    storedLists.Add(listInfo.ColumnValues, storedList);
                }
                else
                {
                    string message = string.Format(@"ColumnData.ForList returned {0}", storedData);
                    throw new InvalidOperationException(message);
                }
            }

            return ImmutableList.Empty<ListInfo>();
        }

        protected class ColumnDataInfo
        {
            public ColumnDataInfo(ColumnData<T> columnData, HashedObject<ImmutableList<T>> columnValues)
            {
                OriginalColumnData = columnData;
                ColumnValues = columnValues;
            }

            public ColumnData<T> OriginalColumnData { get; }
            public HashedObject<ImmutableList<T>> ColumnValues { get; }
        }

        protected class ListInfo
        {
            public ListInfo(HashedObject<ImmutableList<T>> list)
            {
                ColumnValues = list;
                UniqueValues = list.Value.Distinct().ToList();
            }

            public HashedObject<ImmutableList<T>> ColumnValues { get; }
            public List<T> UniqueValues { get; private set; }
        }
    }

    public class ColumnOptimizeOptions : Immutable
    {
        public static readonly ColumnOptimizeOptions Default = new ColumnOptimizeOptions();

        private ColumnOptimizeOptions()
        {
        }
        public bool UseValueCache { get; private set; }

        public ColumnOptimizeOptions WithValueCache()
        {
            return ChangeProp(ImClone(this), im => im.UseValueCache = true);
        }
        public bool UseFactorLists { get; private set; }

        public ColumnOptimizeOptions WithFactorLists()
        {
            return ChangeProp(ImClone(this), im => im.UseFactorLists = true);
        }
    }
}
