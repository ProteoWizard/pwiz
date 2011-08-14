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
using System.ComponentModel;
using System.Text;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// PropertyDescriptor which uses a <see cref="ColumnDescriptor"/> to obtain the property value.
    /// </summary>
    public class ColumnPropertyDescriptor : PropertyDescriptor
    {
        private const string ColumnPropertyDescriptorBaseName = "Column_";
        public ColumnPropertyDescriptor(ColumnDescriptor columnDescriptor, RowKey rowKey) : this(columnDescriptor, RowKey.QualifyIdentifierPath(rowKey, columnDescriptor.IdPath))
        {
            RowKey = rowKey;
        }
        private ColumnPropertyDescriptor(ColumnDescriptor columnDescriptor, IdentifierPath identifierPath) : base(ColumnPropertyDescriptorBaseName + identifierPath, new Attribute[0])
        {
            if (columnDescriptor.CollectionInfo != null && columnDescriptor.CollectionInfo.IsDictionary)
            {
                // Special case dictionary items.
                // No one wants to see a KeyValuePair displayed in a GridColumn,
                // so we display the Value instead.
                ColumnDescriptor = columnDescriptor.ResolveChild("Value");
            }
            if (ColumnDescriptor == null)
            {
                ColumnDescriptor = columnDescriptor;
            }
            else
            {
                ColumnDescriptor = ColumnDescriptor.SetCaption(columnDescriptor.Caption);
            }
            IdentifierPath = identifierPath;
        }
        public IdentifierPath IdentifierPath
        {
            get; private set;
        }
        public RowKey RowKey { get; private set; }

        public ColumnDescriptor ColumnDescriptor { get; private set; }
        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override object GetValue(object component)
        {
            return ColumnDescriptor.GetPropertyValue(component as RowItem, RowKey);
        }

        public override void ResetValue(object component)
        {
            throw new NotImplementedException();
        }

        public override void SetValue(object component, object value)
        {
            throw new NotImplementedException();
        }

        public override bool ShouldSerializeValue(object component)
        {
            throw new NotImplementedException();
        }

        public override Type ComponentType
        {
            get { return typeof(RowItem); }
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override Type PropertyType
        {
            get { return ColumnDescriptor.PropertyType; }
        }

        public override string DisplayName
        {
            get
            {
                StringBuilder prefix = new StringBuilder();
                var rowKey = RowKey;
                while (rowKey != null)
                {
                    if (prefix.Length > 0)
                    {
                        prefix.Insert(0, " ");
                    }
                    prefix.Insert(0, (rowKey.Value ?? "").ToString());
                    rowKey = rowKey.Parent;
                }
                if (prefix.Length == 0)
                {
                    return ColumnDescriptor.DisplayName;
                }
                return prefix + " " + ColumnDescriptor.DisplayName;
            }
        }
    }
}
