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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataBinding.Controls;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Single-use enumerator throw a list of RowItem objects.
    /// Nulls out items in its array after they have been returned so that they can be garbage collected.
    /// </summary>
    public class RowItemEnumerator : IEnumerator<RowItem>
    {
        private RowItem[] _rowItems;
        private RowItem _current;
        private int _index;
        public RowItemEnumerator(IEnumerable<RowItem> rowItems, ItemProperties itemProperties,
            ColumnFormats columnFormats)
        {
            _rowItems = rowItems.ToArray();
            ItemProperties = itemProperties;
            ColumnFormats = columnFormats;
        }

        public static RowItemEnumerator FromBindingListSource(BindingListSource source)
        {
            return new RowItemEnumerator(source.Cast<RowItem>(), source.ItemProperties, source.ColumnFormats);
        }

        public int Count
        {
            get { return _rowItems.Length; }
        }

        public ItemProperties ItemProperties { get; }
        public ColumnFormats ColumnFormats { get; }

        void IDisposable.Dispose()
        {
        }

        void IEnumerator.Reset()
        {
            throw new InvalidOperationException();
        }

        public bool MoveNext()
        {
            if (_index >= _rowItems.Length)
            {
                return false;
            }

            _current = _rowItems[_index];
            _rowItems[_index] = null;
            _index++;
            return true;
        }

        object IEnumerator.Current => Current;

        public RowItem Current
        {
            get
            {
                return _current;
            }
        }
    }
}
