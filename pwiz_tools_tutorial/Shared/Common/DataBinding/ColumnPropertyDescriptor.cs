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

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// PropertyDescriptor which uses a <see cref="ColumnDescriptor"/> to obtain the property value.
    /// </summary>
    public class ColumnPropertyDescriptor : PropertyDescriptor
    {
        public ColumnPropertyDescriptor(DisplayColumn displayColumn, string name) : this(displayColumn, name, displayColumn.IdentifierPath, null)
        {
        }
        public ColumnPropertyDescriptor(DisplayColumn displayColumn, string name, IdentifierPath identifierPath, PivotKey pivotKey)
            : base(name, new Attribute[0])
        {
//            if (columnDescriptor.CollectionInfo != null && columnDescriptor.CollectionInfo.IsDictionary)
//            {
                // Special case dictionary items.
                // No one wants to see a KeyValuePair displayed in a GridColumn,
                // so we display the Value instead.
//                ColumnDescriptor = columnDescriptor.ResolveChild("Value");
//            }
//            if (ColumnDescriptor == null)
//            {
//                ColumnDescriptor = columnDescriptor;
//            }
//            else
//            {
//                ColumnDescriptor = ColumnDescriptor.SetCaption(columnDescriptor.Caption);
//            }
            DisplayColumn = displayColumn;
            IdentifierPath = identifierPath;
            PivotKey = pivotKey;
        }
        public IdentifierPath IdentifierPath
        {
            get; private set;
        }
        public PivotKey PivotKey { get; private set; }

        public DisplayColumn DisplayColumn { get; private set; }
        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override object GetValue(object component)
        {
            return DisplayColumn.GetValue(component as RowItem, PivotKey);
        }

        public override void ResetValue(object component)
        {
            throw new NotImplementedException();
        }

        public override void SetValue(object component, object value)
        {
            DisplayColumn.SetValue(component as RowItem, PivotKey, value);
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
            get { return DisplayColumn.IsReadOnly; }
        }

        public bool IsReadOnlyForRow(object rowValue)
        {
            return DisplayColumn.IsReadOnlyForRow(rowValue as RowItem, PivotKey);
        }

        public override Type PropertyType
        {
            get { return DisplayColumn.PropertyType; }
        }

        public override string DisplayName
        {
            get
            {
                return DisplayColumn.GetColumnCaption(PivotKey);
            }
        }
        public delegate void HookPropertyChange(object component, PropertyDescriptor propertyDescriptor);
    }
}
