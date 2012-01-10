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
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    internal class QueryWorker : MustDispose
    {
        private BindingListEventHandler _bindingListEventHandler;
        private QueryResults _queryResults;
        public QueryWorker(BindingListEventHandler bindingListEventHandler)
        {
            _bindingListEventHandler = bindingListEventHandler;
        }

            private void ThrowIfCancelledPivot()
            {
                CheckDisposed();
                if (!_queryResults.Parameters.PivotValid(_bindingListEventHandler.QueryParameters)) 
                {
                    throw new ObjectDisposedException("Cancelled");
                }
            }

            public QueryResults DoWork()
            {
                _queryResults = _bindingListEventHandler.QueryResults;
                if (_queryResults.PivotedRows == null)
                {
                    var pivoter = new Pivoter(_queryResults.Parameters.ViewInfo);
                    var rowItems = Array.AsReadOnly(pivoter.ExpandAndPivot(ThrowIfCancelledPivot, _queryResults.Parameters.Rows).ToArray());
                    _queryResults = _queryResults.SetPivotedRows(pivoter, rowItems);
                }
                if (_queryResults.FilteredRows == null)
                {
                    _queryResults = _queryResults.SetFilteredRows(DoFilter());
                }
                if (_queryResults.SortedRows == null)
                {
                    _queryResults = _queryResults.SetSortedRows(DoSort());
                }
                return _queryResults;
            }

            private IList<RowItem> DoFilter()
            {
                var unfilteredRows = _queryResults.PivotedRows;
                var filter = _queryResults.Parameters.RowFilter;
                if (filter.IsEmpty)
                {
                    return unfilteredRows;
                }
                var properties = _queryResults.ItemProperties;
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

                for (int iRow = 0; iRow < unfilteredRows.Count; iRow++)
                {
                    var row = unfilteredRows[iRow];
                    for (int iProperty = 0; iProperty < properties.Count; iProperty++)
                    {
                        var property = properties[iProperty];
                        var value = property.GetValue(row);
                        if (value == null)
                        {
                            continue;
                        }
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
                        if (filter.Matches(strValue))
                        {
                            filteredRows.Add(row);
                            break;
                        }
                    }
                }
                if (filteredRows.Count == unfilteredRows.Count)
                {
                    return unfilteredRows;
                }
                return Array.AsReadOnly(filteredRows.ToArray());
            }
            private IList<RowItem> DoSort()
            {
                var sortDescriptions = _queryResults.Parameters.SortDescriptions;
                var unsortedRows = _queryResults.FilteredRows;
                if (sortDescriptions == null || sortDescriptions.Count == 0)
                {
                    return unsortedRows;
                }
                var sortRows = new SortRow[unsortedRows.Count];
                for (int iRow = 0; iRow < sortRows.Count(); iRow++)
                {
                    sortRows[iRow] = new SortRow(this, unsortedRows[iRow], iRow);
                }
                Array.Sort(sortRows);
                return Array.AsReadOnly(sortRows.Select(sr => sr.RowItem).ToArray());
            }
            class SortRow : IComparable<SortRow>
            {
                private object[] _keys;
                public SortRow(QueryWorker worker, RowItem rowItem, int rowIndex)
                {
                    Worker = worker;
                    RowItem = rowItem;
                    OriginalRowIndex = rowIndex;
                    _keys = new object[Sorts.Count];
                    for (int i = 0; i < Sorts.Count; i++)
                    {
                        _keys[i] = Sorts[i].PropertyDescriptor.GetValue(RowItem);
                    }
                }
                public QueryWorker Worker { get; private set; }
                public RowItem RowItem { get; private set; }
                public int OriginalRowIndex { get; private set; }
                public ListSortDescriptionCollection Sorts
                {
                    get { return Worker._queryResults.Parameters.SortDescriptions; }
                }
                public int CompareTo(SortRow other)
                {
                    Worker.ThrowIfCancelledPivot();
                    for (int i = 0; i < Sorts.Count; i++)
                    {
                        var sort = Sorts[i];
                        int result = Worker._queryResults.Parameters.ViewInfo.DataSchema.Compare(_keys[i], other._keys[i]);
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
