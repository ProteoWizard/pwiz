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

namespace pwiz.Common.DataBinding
{
    public class RowItem
    {
        public RowItem(object value) : this(value, PivotKey.EMPTY, ImmutableList.Empty<PivotKey>())
        {
        }
        public RowItem(object value, PivotKey rowKey, IEnumerable<PivotKey> pivotKeys)
        {
            Value = value;
            RowKey = rowKey;
            PivotKeys = ImmutableList.ValueOf(pivotKeys);
        }
        protected RowItem(RowItem copy)
        {
            Value = copy.Value;
            RowKey = copy.RowKey;
            PivotKeys = copy.PivotKeys;
        }

        public PivotKey RowKey { get; private set; }

        public RowItem SetRowKey(PivotKey rowKey)
        {
            return new RowItem(this){RowKey = rowKey};
        }

        public object Value { get; private set; }

        public RowItem SetValue(object value)
        {
            return new RowItem(this) {Value = value};
        }
        public ICollection<PivotKey> PivotKeys { get; private set; }
        public RowItem SetPivotKeys(IEnumerable<PivotKey> pivotKeys)
        {
            var newPivotKeys = ImmutableList.ValueOf(pivotKeys);
            if (newPivotKeys.Count == 0 && PivotKeys.Count == 0)
            {
                return this;
            }
            return new RowItem(this) { PivotKeys = newPivotKeys };
        }

        public virtual void HookPropertyChange(object component, PropertyDescriptor propertyDescriptor)
        {
        }
    }
}
