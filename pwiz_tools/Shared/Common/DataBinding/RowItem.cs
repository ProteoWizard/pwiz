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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public class RowItem : Immutable
    {
        private HashSet<PivotKey> _pivotKeys;
        public RowItem(object value)
        {
            Value = value;
            RowKey = PivotKey.EMPTY;
        }
        public PivotKey RowKey { get; private set; }

        public RowItem SetRowKey(PivotKey rowKey)
        {
            return ChangeProp(ImClone(this), im => im.RowKey = rowKey);
        }

        public object Value { get; private set; }

        public IEnumerable<PivotKey> PivotKeys
        {
            get
            {
                return _pivotKeys?.AsEnumerable() ?? ImmutableList.Empty<PivotKey>();
            }
        }

        public int PivotKeyCount
        {
            get { return _pivotKeys?.Count ?? 0; }
        }

        public bool ContainsPivotKey(PivotKey pivotKey)
        {
            if (_pivotKeys == null)
            {
                return false;
            }

            return _pivotKeys.Contains(pivotKey);
        }

        public RowItem SetPivotKeys(IEnumerable<PivotKey> pivotKeys)
        {
            var newPivotKeys = pivotKeys.ToHashSet();
            if (newPivotKeys.Count == 0 && _pivotKeys == null)
            {
                return this;
            }

            return ChangeProp(ImClone(this), im => im._pivotKeys = newPivotKeys.Count == 0 ? null : newPivotKeys);
        }
    }
}
