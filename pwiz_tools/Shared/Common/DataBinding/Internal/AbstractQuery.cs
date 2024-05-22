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
using System.ComponentModel;
using System.Linq;
using System.Threading;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Internal
{
    internal class AbstractQuery
    {
        protected QueryResults RunAll(CancellationToken cancellationToken, QueryResults results)
        {
            var pivotedRows = Pivot(cancellationToken, results);
            var dataSchema = results.Parameters.ViewInfo.DataSchema;
            var transformedRows = Transform(cancellationToken, dataSchema, new TransformResults(null, null, pivotedRows), results.Parameters.TransformStack);
            if (null != results.Parameters.ClusteringSpec)
            {
                var clusteredResults = Clusterer.PerformClustering(new ProgressHandler(cancellationToken), results.Parameters.ClusteringSpec, transformedRows.PivotedRows);
                if (clusteredResults != null)
                {
                    transformedRows = new TransformResults(transformedRows.Parent, transformedRows.RowTransform, clusteredResults);
                }
            }
            return results.ChangeTransformResults(transformedRows);
        }

        protected ReportResults Pivot(CancellationToken cancellationToken, QueryResults results)
        {
            var viewInfo = results.Parameters.ViewInfo;
            // Construct the ViewInfo again so that it picks up the latest property definitions from
            // the DataSchema.
            viewInfo = new ViewInfo(viewInfo.DataSchema, viewInfo.ParentColumn.PropertyType, viewInfo.ViewSpec);
            var pivoter = new Pivoter(viewInfo);
            return pivoter.ExpandAndPivot(cancellationToken, results.SourceRows);
        }

        protected TransformResults Transform(CancellationToken cancellationToken, DataSchema dataSchema, TransformResults input,
            TransformStack transformStack)
        {
            if (transformStack == null || transformStack.StackIndex >= transformStack.RowTransforms.Count)
            {
                return input;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (transformStack.Predecessor != null)
            {
                input = Transform(cancellationToken, dataSchema, input, transformStack.Predecessor);
            }
            var filter = transformStack.CurrentTransform as RowFilter;
            if (filter != null)
            {
                var filteredRows = new ReportResults(Filter(cancellationToken, dataSchema, filter, input.PivotedRows),
                    input.PivotedRows.ItemProperties);
                filteredRows = Sort(cancellationToken, dataSchema, filter, filteredRows);
                return new TransformResults(input, filter, filteredRows);
            }
            var pivotSpec = transformStack.CurrentTransform as PivotSpec;
            if (pivotSpec != null)
            {
                var pivotedRows = GroupAndTotaler.GroupAndTotal(cancellationToken, dataSchema, pivotSpec, input.PivotedRows);
                return new TransformResults(input, pivotSpec, pivotedRows);
            }
            return input;
        }

        protected IEnumerable<RowItem> Filter(CancellationToken cancellationToken, DataSchema dataSchema, RowFilter filter, ReportResults pivotedRows)
        {
            if (filter.IsEmptyFilter)
            {
                return pivotedRows.RowItems;
            }
            var properties = pivotedRows.ItemProperties;
            Predicate<object>[] columnPredicates = new Predicate<object>[properties.Count];
            
            for (int i = 0; i < properties.Count; i++)
            {
                columnPredicates[i] = filter.GetPredicate(dataSchema, properties[i]);
            }
            var filteredRows = new List<RowItem>();
            // toString on an enum is incredibly slow, so we cache the results in 
            // in a dictionary.
            var toStringCaches = new Dictionary<object, string>[properties.Count];
            for (int i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                if (property.PropertyType.IsEnum)
                {
                    toStringCaches[i] = new Dictionary<object, string>();
                }
            }
            
            foreach (var row in pivotedRows.RowItems)
            {
                bool matchesText = string.IsNullOrEmpty(filter.Text);
                bool matchesFilter = true;
                for (int iProperty = 0; iProperty < properties.Count; iProperty++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var predicate = columnPredicates[iProperty];
                    if (matchesText && null == predicate)
                    {
                        continue;
                    }
                    var property = properties[iProperty];
                    var value = property.GetValue(row);
                    if (!matchesText && value != null)
                    {
                        var cache = toStringCaches[iProperty];
                        string strValue;
                        if (cache == null)
                        {
                            strValue = value.ToString();
                        }
                        else
                        {
                            if (!cache.TryGetValue(value, out strValue))
                            {
                                strValue = value.ToString();
                                cache.Add(value, strValue);
                            }
                        }
                        if (filter.MatchesText(strValue))
                        {
                            matchesText = true;
                        }
                    }
                    matchesFilter = null == predicate || predicate(value);
                    if (!matchesFilter)
                    {
                        break;
                    }
                }
                if (matchesText && matchesFilter)
                {
                    filteredRows.Add(row);
                }
            }

            if (filteredRows.Count == pivotedRows.RowItems.Count)
            {
                return pivotedRows.RowItems;
            }
            return filteredRows;
        }

        protected ReportResults Sort(CancellationToken cancellationToken, DataSchema dataSchema, RowFilter rowFilter,
            ReportResults pivotedRows)
        {
            if (rowFilter.ColumnSorts.Count == 0)
            {
                return pivotedRows;
            }
            var sortDescriptions = rowFilter.GetListSortDescriptionCollection(pivotedRows.ItemProperties);
            if (sortDescriptions.Count == 0)
            {
                return pivotedRows;
            }
            return pivotedRows.ChangeRowItems(Sort(cancellationToken, dataSchema, sortDescriptions, pivotedRows));
        }

        protected IEnumerable<RowItem> Sort(CancellationToken cancellationToken, DataSchema dataSchema, ListSortDescriptionCollection sortDescriptions, ReportResults pivotedRows)
        {
            var unsortedRows = pivotedRows.RowItems;
            if (sortDescriptions == null || sortDescriptions.Count == 0)
            {
                return unsortedRows;
            }
            var sortRows = new SortRow[unsortedRows.Count];
            for (int iRow = 0; iRow < sortRows.Length; iRow++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sortRows[iRow] = new SortRow(cancellationToken, dataSchema, sortDescriptions, unsortedRows[iRow], iRow);
            }
            Array.Sort(sortRows);
            return Array.AsReadOnly(sortRows.Select(sr => sr.RowItem).ToArray());
        }
        class SortRow : IComparable<SortRow>
        {
            private readonly object[] _keys;
            public SortRow(CancellationToken cancellationToken, DataSchema dataSchema, ListSortDescriptionCollection sorts, RowItem rowItem, int rowIndex)
            {
                CancellationToken = cancellationToken;
                DataSchema = dataSchema;
                Sorts = sorts;
                RowItem = rowItem;
                OriginalRowIndex = rowIndex;
                _keys = new object[Sorts.Count];
                for (int i = 0; i < Sorts.Count; i++)
                {
                    _keys[i] = Sorts[i].PropertyDescriptor.GetValue(RowItem);
                }
            }
// ReSharper disable MemberCanBePrivate.Local
            public CancellationToken CancellationToken { get; private set; }
            public DataSchema DataSchema { get; private set; }
            public RowItem RowItem { get; private set; }
            public int OriginalRowIndex { get; private set; }
            public ListSortDescriptionCollection Sorts { get; private set; }
// ReSharper restore MemberCanBePrivate.Local
            public int CompareTo(SortRow other)
            {
                CancellationToken.ThrowIfCancellationRequested();
                for (int i = 0; i < Sorts.Count; i++)
                {
                    var sort = Sorts[i];
                    int result = DataSchema.Compare(_keys[i], other._keys[i]);
                    if (sort.SortDirection == ListSortDirection.Descending)
                    {
                        result = -result;
                    }
                    if (result != 0)
                    {
                        return result;
                    }
                }
                return OriginalRowIndex.CompareTo(other.OriginalRowIndex);
            }
        }
    }
}
