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
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Controls;

namespace pwiz.Common.DataBinding
{
    public abstract class RowItemEnumerator : IDisposable
    {
        protected RowItemEnumerator(ItemProperties itemProperties, ColumnFormats columnFormats)
        {
            ItemProperties = itemProperties;
            ColumnFormats = columnFormats;
        }
        public abstract RowItem Current { get; }
        public ItemProperties ItemProperties { get; }
        public ColumnFormats ColumnFormats { get; }

        public long Index { get; protected set; }
        public long? Count { get; protected set; }

        public virtual double? PercentComplete
        {
            get
            {
                if (Count.HasValue)
                {
                    return Index * 100.0 / Count.Value;
                }
                return null;
            }
        }

        public abstract bool MoveNext();

        public virtual void Dispose()
        {
        }
    }


    /// <summary>
    /// Single-use enumerator throw a list of RowItem objects.
    /// Nulls out items in its array after they have been returned so that they can be garbage collected.
    /// </summary>
    public class RowItemList : RowItemEnumerator
    {
        private ImmutableList<RowItem[]> _rowItems;
        private int _listIndex;
        private int _indexInList;
        private RowItem _current;

        public RowItemList(IEnumerable<IEnumerable<RowItem>> rowItemLists, ItemProperties itemProperties,
            ColumnFormats columnFormats) : base(itemProperties, columnFormats)
        {
            _rowItems = ImmutableList.ValueOf(rowItemLists.Select(list => list.ToArray()));
            Count = _rowItems.Sum(list => (long) list.Length);
        }

        public RowItemList(BigList<RowItem> bigList, ItemProperties itemProperties, ColumnFormats columnFormats) : this(bigList.InnerLists, itemProperties, columnFormats)
        {

        }

        public static RowItemList FromBindingListSource(BindingListSource source)
        {
            return new RowItemList(source.ReportResults.RowItems, source.ItemProperties, source.ColumnFormats);
        }


        public override bool MoveNext()
        {
            if (_listIndex >= _rowItems.Count)
            {
                return false;
            }

            _current = _rowItems[_listIndex][_indexInList];
            _rowItems[_listIndex][_indexInList] = null;
            _indexInList++;
            if (_indexInList >= _rowItems[_listIndex].Length)
            {
                _indexInList = 0;
                _listIndex++;
            }

            Index++;
            return true;
        }

        public override RowItem Current
        {
            get { return _current; }
        }


        public BigList<RowItem> GetRowItems()
        {
            return _rowItems.SelectMany(list => list).ToBigList();
        }
    }

    public class StreamingRowItemEnumerator : RowItemEnumerator
    {

        private IEnumerator<RowItem> _enumerator;
        private long _sourceIndex;
        private long? _sourceCount;
        private Func<RowItem, IEnumerable<RowItem>> _expander;

        public StreamingRowItemEnumerator(IEnumerable<RowItem> source, long? sourceCount, Func<RowItem, IEnumerable<RowItem>> expander,
            long? expandedItemCount,
            ItemProperties itemProperties,
            ColumnFormats columnFormats) : base(itemProperties, columnFormats)
        {
            _expander = expander;
            _sourceCount = sourceCount;
            Count = expandedItemCount;
            _enumerator = EnumerateRowItems(source);
        }

        private IEnumerator<RowItem> EnumerateRowItems(IEnumerable<RowItem> source)
        {
            _sourceIndex = 0;
            foreach (var o in source)
            {

                foreach (var rowItem in _expander(o))
                {
                    yield return rowItem;
                    Index++;
                }
                _sourceIndex++;
            }
        }

        public override bool MoveNext()
        {
            return _enumerator.MoveNext();
        }

        public override RowItem Current
        {
            get { return _enumerator.Current; }
        }
        public override void Dispose()
        {
            _enumerator.Dispose();
            base.Dispose();
        }

        public override double? PercentComplete
        {
            get
            {
                var value = base.PercentComplete;
                if (_sourceCount.HasValue)
                {
                    value ??= _sourceIndex * 100.0 / _sourceCount.Value;
                }
                return value;
            }
        }
    }
}
