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

namespace pwiz.Common.DataBinding
{
    public class RowItem
    {
        public RowItem(object value)
        {
            Value = value;
        }
        public virtual PivotKey RowKey { get {return PivotKey.EMPTY;}}

        public virtual RowItem SetRowKey(PivotKey rowKey)
        {
            return new WithRowKey(Value, rowKey);
        }

        public object Value { get; }

        public virtual IEnumerable<PivotKey> PivotKeys
        {
            get
            {
                return ImmutableList.Empty<PivotKey>();
            }
        }

        public virtual int PivotKeyCount
        {
            get { return 0; }
        }

        public virtual bool ContainsPivotKey(PivotKey pivotKey)
        {
            return false;
        }

        public RowItem SetPivotKeys(IEnumerable<PivotKey> pivotKeys)
        {
            var newPivotKeys = pivotKeys.ToHashSet();
            if (newPivotKeys.Count == 0 && PivotKeyCount == 0)
            {
                return this;
            }

            return new WithPivotKeys(Value, RowKey, newPivotKeys);
        }

        private class WithRowKey : RowItem
        {
            private PivotKey _rowKey;

            public WithRowKey(object value, PivotKey rowKey) : base(value)
            {
                _rowKey = rowKey;
            }

            public override PivotKey RowKey
            {
                get { return _rowKey; }
            }
        }

        private class WithPivotKeys : WithRowKey
        {
            private readonly HashSet<PivotKey> _pivotKeys;

            public WithPivotKeys(object value, PivotKey rowKey, HashSet<PivotKey> pivotKeys) : base(value, rowKey)
            {
                _pivotKeys = pivotKeys;
            }

            public override bool ContainsPivotKey(PivotKey pivotKey)
            {
                return _pivotKeys.Contains(pivotKey);
            }

            public override int PivotKeyCount
            {
                get { return _pivotKeys.Count; }
            }
            public override IEnumerable<PivotKey> PivotKeys
            {
                get { return _pivotKeys.AsEnumerable(); }
            }
            public override RowItem SetRowKey(PivotKey rowKey)
            {
                return new WithPivotKeys(Value, RowKey, _pivotKeys);
            }
        }
    }
}
