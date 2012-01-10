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

namespace pwiz.Common.DataBinding
{
    public class RowItem
    {
        public RowItem(object key, object value) : this(null, IdentifierPath.Root, key, value)
        {
        }
        public RowItem(RowItem parent, IdentifierPath sublistId, object key, object value)
        {
            Parent = parent;
            SublistId = sublistId;
            Key = key;
            Value = value;
            PivotKeys = new PivotKey[0];
        }
        protected RowItem(RowItem copy)
        {
            Parent = copy.Parent;
            SublistId = copy.SublistId;
            Key = copy.Key;
            Value = copy.Value;
            PivotKeys = copy.PivotKeys;
        }

        public PivotKey GetGroupKey()
        {
            var result = Key as PivotKey;
            if (result != null)
            {
                return result;
            }
            if (Parent == null)
            {
                return new PivotKey(SublistId, Key);
            }
            return PivotKey.OfValues(Parent.GetGroupKey(), Parent.SublistId, new[] {new KeyValuePair<IdentifierPath, object>(SublistId, Key)});
        }

        public RowItem SetPivotKeys(HashSet<PivotKey> pivotKeys)
        {
            if (PivotKeys.Count == 0 && pivotKeys.Count == 0)
            {
                return this;
            }
            return new RowItem(this){PivotKeys = Array.AsReadOnly(pivotKeys.ToArray())};
        }

        public RowItem SetParent(RowItem newParent)
        {
            return new RowItem(this){Parent=newParent};
        }

        public RowItem Parent { get; private set; }
        public IdentifierPath SublistId { get; private set; }
        public object Key { get; private set; }
        public object Value { get; private set; }
        public ICollection<PivotKey> PivotKeys { get; private set; }
        public virtual void HookPropertyChange(object component, PropertyDescriptor propertyDescriptor)
        {
        }
    }
}
