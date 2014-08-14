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

using System.Collections.Generic;
using System.ComponentModel;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Internal
{
    internal class QueryResults
    {
        public static readonly QueryResults Empty = new QueryResults();
        private QueryResults()
        {
            Parameters = QueryParameters.Empty;
            PivotedRows = FilteredRows = SortedRows = new RowItem[0];
            ItemProperties = null;
        }
        public QueryResults(QueryResults copy)
        {
            Parameters = copy.Parameters;

            SourceRows = copy.SourceRows;

            PivotedRows = copy.PivotedRows;
            ItemProperties = copy.ItemProperties;

            FilteredRows = copy.FilteredRows;
            SortedRows = copy.SortedRows;
        }

        public QueryParameters Parameters { get; private set; }
        public QueryResults SetParameters(QueryParameters newParameters)
        {
            var result = new QueryResults
                             {
                                 Parameters = newParameters
                             };
            if (Parameters.PivotValid(result.Parameters))
            {
                result.PivotedRows = PivotedRows;
                result.ItemProperties = ItemProperties;
                if (Parameters.FilterValid(newParameters))
                {
                    result.FilteredRows = FilteredRows;
                    if (Parameters.SortValid(newParameters))
                    {
                        result.SortedRows = SortedRows;
                    }
                    else
                    {
                        result.SortedRows = null;
                    }
                }
                else
                {
                    result.FilteredRows = result.SortedRows = null;
                }
            }
            else
            {
                if (result.Parameters.ViewInfo == null)
                {
                    result.SortedRows = result.FilteredRows = result.PivotedRows = Empty.ResultRows;
                }
                else
                {
                    result.SortedRows = result.FilteredRows = result.PivotedRows = null;
                }
                result.ItemProperties = null;
            }
            return result;
        }
        public bool IsComplete
        {
            get { return SortedRows != null;}
        }
        public PropertyDescriptorCollection ItemProperties { get; private set; }

        public IList<RowItem> SourceRows { get; private set; }
        public QueryResults SetSourceRows(IEnumerable<RowItem> sourceRows)
        {
            return new QueryResults(this) {
                SourceRows = ImmutableList.ValueOf(sourceRows)
            };
        }
        public IList<RowItem> PivotedRows { get; private set; }
        public QueryResults SetPivotedRows(PivotedRows pivotedRows)
        {
            return new QueryResults(this)
                       {
                           PivotedRows = pivotedRows.RowItems,
                           ItemProperties = pivotedRows.ItemProperties,
                       };
        }
        public IList<RowItem> FilteredRows { get; private set; }
        public QueryResults SetFilteredRows(IEnumerable<RowItem> value)
        {
            return new QueryResults(this) {FilteredRows = ImmutableList.ValueOf(value)};
        }
        public IList<RowItem> SortedRows { get; private set; }
        public QueryResults SetSortedRows(IEnumerable<RowItem> value)
        {
            return new QueryResults(this) {SortedRows = ImmutableList.ValueOf(value)};
        }
        public IList<RowItem> ResultRows { get { return SortedRows;} }
    }

    internal class QueryParameters
    {
        public static readonly QueryParameters Empty = new QueryParameters();
        private QueryParameters()
        {
            ViewInfo = null;
            RowFilter = RowFilter.Empty;
        }
        public QueryParameters(ViewInfo viewInfo, RowFilter rowFilter, ListSortDescriptionCollection sortDescriptions)
        {
            ViewInfo = viewInfo;
            RowFilter = rowFilter;
            SortDescriptions = sortDescriptions;
        }
        public QueryParameters(QueryParameters that)
        {
            ViewInfo = that.ViewInfo;
            RowFilter = that.RowFilter;
            SortDescriptions = that.SortDescriptions;
        }
        public ViewInfo ViewInfo { get; private set;}
        public QueryParameters SetViewInfo(ViewInfo value)
        {
            return new QueryParameters(this) {ViewInfo = value};
        }
        public RowFilter RowFilter { get; private set; }
        public QueryParameters SetRowFilter(RowFilter value)
        {
            return new QueryParameters(this) {RowFilter = value};
        }
        public ListSortDescriptionCollection SortDescriptions { get; private set; }
        public QueryParameters SetSortDescriptions(ListSortDescriptionCollection value)
        {
            return new QueryParameters(this) {SortDescriptions = value};
        }
        public bool PivotValid(QueryParameters that)
        {
            return ReferenceEquals(ViewInfo, that.ViewInfo);
        }
        public bool FilterValid(QueryParameters that)
        {
            return PivotValid(that) && Equals(RowFilter, that.RowFilter);
        }
        public bool SortValid(QueryParameters that)
        {
            return FilterValid(that) && Equals(SortDescriptions, that.SortDescriptions);
        }
    }
}