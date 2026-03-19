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
using System.Diagnostics;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Enumerates RowItem's and updates progress.
    /// </summary>
    public abstract class RowItemEnumerator : IDisposable
    {
        protected RowItemEnumerator(long? rowCount)
        {
            Length = rowCount;
            if (Length.HasValue)
            {
                ProgressMessageTemplate = string.Format(Resources.RowItemEnumerator_RowItemEnumerator_Writing_row__0___1_, @"{0:#,0}", Length.Value.ToString(@"#,0"));
            }
            else
            {
                ProgressMessageTemplate = Resources.RowItemEnumerator_RowItemEnumerator_Writing_row__0___0_;
            }

        }
        public abstract RowItem Current { get; }
        public ItemProperties ItemProperties { get; set; } = ItemProperties.EMPTY;
        public ColumnFormats ColumnFormats { get; set; } = new ColumnFormats();

        /// <summary>
        /// The number of RowItem's that have been returned so far.
        /// </summary>
        public long Position { get; protected set; }
        /// <summary>
        /// The total number of RowItem's that will be returned, if known.
        /// </summary>
        public long? Length { get; }

        public virtual double? PercentComplete
        {
            get
            {
                if (Length.HasValue)
                {
                    return Position * 100.0 / Length.Value;
                }
                return null;
            }
        }

        /// <summary>
        /// Move to the next RowItem and update progress
        /// </summary>

        public bool MoveNext()
        {
            if (IsCanceled)
            {
                return false;
            }

            bool result = TryMoveNext();
            if (result)
            {
                UpdateProgress();
            }

            return result;
        }

        public bool IsCanceled
        {
            get
            {
                return true == ProgressMonitor?.IsCanceled;
            }
        }

        protected abstract bool TryMoveNext();
       

        public virtual void Dispose()
        {
        }

        public IProgressStatus Status { get; set; }
        public IProgressMonitor ProgressMonitor { get; private set; }

        public void SetProgressMonitor(IProgressMonitor progressMonitor, IProgressStatus status)
        {
            ProgressMonitor = progressMonitor;
            Status = status;
            ProgressUpdateStopwatch = Stopwatch.StartNew();
        }
        public TimeSpan ProgressUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);
        public Stopwatch ProgressUpdateStopwatch { get; set; }

        public int StartProgressPercent { get; set; }
        public string ProgressMessageTemplate { get; set; }

        public void UpdateProgress()
        {
            if (Status == null)
            {
                return;
            }

            int? newPercentComplete = (int?) (StartProgressPercent + this.PercentComplete * (100 - StartProgressPercent) / 100);
            if (newPercentComplete > Status.PercentComplete ||
                ProgressUpdateStopwatch?.Elapsed > ProgressUpdateInterval)
            {
                Status = Status.ChangeMessage(string.Format(ProgressMessageTemplate, Position + 1));
                if (newPercentComplete.HasValue)
                {
                    Status = Status.ChangePercentComplete(Math.Max(0, Math.Min(99, newPercentComplete.Value)));
                }

                ProgressMonitor?.UpdateProgress(Status);
            }
        }
    }


    /// <summary>
    /// Single-use enumerator through a list of RowItem objects.
    /// Nulls out items in its array after they have been returned so that they can be garbage collected.
    /// </summary>
    public class RowItemList : RowItemEnumerator
    {
        private readonly ImmutableList<RowItem[]> _rowItems;
        private int _listIndex;
        private int _indexInList;
        private RowItem _current;

        private RowItemList(ImmutableList<RowItem[]> rowItems) : base(rowItems.Sum(list=>list.Length))
        {
            _rowItems = rowItems;
        }

        public RowItemList(BigList<RowItem> bigList)
            : this(ImmutableList.ValueOf(bigList.InnerLists.Select(list=>list.ToArray())))
        {
        }

        protected override bool TryMoveNext()
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

            Position++;
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

    /// <summary>
    /// Enumerates RowItems when it is not known how many there will actually be.
    /// The RowItems come from performing an operation which maps each source RowItem to zero or more RowItems.
    /// </summary>
    public class StreamingRowItemEnumerator : RowItemEnumerator
    {

        private IEnumerator<RowItem> _enumerator;
        private long _sourceIndex;
        private long? _sourceCount;
        private Func<RowItem, IEnumerable<RowItem>> _expander;

        /// <summary>
        /// Constructor for a StreamingRowItemEnumerator which iterates over RowItems that are obtained by performing an operation on source row items.
        /// </summary>
        /// <param name="source">The RowItem objects that will be given to the <paramref name="expander"/> function</param>
        /// <param name="sourceCount">The number of source RowItem objects or null if that count is not known</param>
        /// <param name="expander">Mapping operation that returns zero or more RowItem objects for each input.</param>
        /// <param name="expandedItemCount">The total number of RowItems that will be returned by invoking expander on each source RowItem, or null if not known</param>
        public StreamingRowItemEnumerator(IEnumerable<RowItem> source, long? sourceCount, Func<RowItem, IEnumerable<RowItem>> expander,
            long? expandedItemCount) : base(expandedItemCount)
        {
            _expander = expander;
            _sourceCount = sourceCount;
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
                    Position++;
                }
                _sourceIndex++;
            }
        }

        protected override bool TryMoveNext()
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
