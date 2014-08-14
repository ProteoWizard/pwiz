/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Threading.Tasks;
using pwiz.Common.DataBinding.RowSources;

namespace pwiz.Common.DataBinding.Internal
{
    internal class CloneableRowSourceWrapper<TKey, TItem> : AbstractRowSourceWrapper
    {
        public CloneableRowSourceWrapper(ICloneableList<TKey, TItem> cloneableList) : base(cloneableList)
        {
            WrappedList = cloneableList;
        }

        public ICloneableList<TKey, TItem> WrappedList { get; private set; }

        public override void StartQuery(IQueryRequest queryRequest)
        {
            new BackgroundQuery(this, TaskScheduler.Default, queryRequest).Start();
        }

        public bool IsCloneable { get { return true; } }
        public Type KeyType { get { return typeof (TKey); } }
        public Type ItemType { get { return typeof (TItem); } }
        public int Count { get { return WrappedList.Count; } }
        public IEnumerable DeepClone()
        {
            return WrappedList.DeepClone();
        }

        public int IndexOfKey(object key)
        {
            return WrappedList.IndexOfKey((TKey)key);
        }

        public object GetKey(object item)
        {
            return WrappedList.GetKey((TItem) item);
        }

        public object this[int index]
        {
            get { return WrappedList[index]; }
        }

        public override IEnumerable<RowItem> ListRowItems()
        {
            return WrappedList.DeepClone().Select(item => new RowItem(item));
        }

        public override QueryResults MakeLive(QueryResults queryResults)
        {
            var liveResults = queryResults.SetSourceRows(MakeLive(queryResults.SourceRows));
            if (ReferenceEquals(queryResults.SourceRows, queryResults.PivotedRows))
            {
                liveResults = liveResults.SetPivotedRows(new PivotedRows(liveResults.SourceRows, queryResults.ItemProperties));
            }
            else
            {
                liveResults = liveResults.SetPivotedRows(new PivotedRows(MakeLive(queryResults.PivotedRows), queryResults.ItemProperties));
            }
            if (ReferenceEquals(queryResults.PivotedRows, queryResults.FilteredRows))
            {
                liveResults = liveResults.SetFilteredRows(liveResults.PivotedRows);
            }
            else
            {
                liveResults = liveResults.SetFilteredRows(MakeLive(queryResults.FilteredRows));
            }
            if (ReferenceEquals(queryResults.FilteredRows, queryResults.SortedRows))
            {
                liveResults = liveResults.SetSortedRows(liveResults.FilteredRows);
            }
            else
            {
                liveResults = liveResults.SetSortedRows(MakeLive(queryResults.SortedRows));
            }
            return liveResults;
        }
        private IEnumerable<RowItem> MakeLive(IEnumerable<RowItem> rowItems)
        {
            if (null == rowItems)
            {
                return null;
            }
            return rowItems.Select(MakeLive);
        }
        private RowItem MakeLive(RowItem rowItem)
        {
            if (rowItem.Value is GroupedRow)
            {
                return rowItem;
            }
            int index = WrappedList.IndexOfKey(WrappedList.GetKey((TItem) rowItem.Value));
            TItem item = index >= 0 ? WrappedList[index] : default(TItem);
            return rowItem.SetValue(item);
        }
    }
}