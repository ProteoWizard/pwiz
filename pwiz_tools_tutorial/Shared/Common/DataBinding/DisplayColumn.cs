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
using System.Text;

namespace pwiz.Common.DataBinding
{
    public class DisplayColumn
    {
        public DisplayColumn(ViewInfo viewInfo, ColumnSpec columnSpec, ColumnDescriptor columnDescriptor)
        {
            ViewInfo = viewInfo;
            ColumnDescriptor = columnDescriptor;
            ColumnSpec = columnSpec;
            CollectionColumn = columnDescriptor == null ? null 
                : columnDescriptor.FirstUnboundParent() ?? viewInfo.ParentColumn;
        }

        public ViewInfo ViewInfo { get; private set; }
        public DataSchema DataSchema { get { return ViewInfo.DataSchema; } }
        public ColumnSpec ColumnSpec { get; private set; }
        public IdentifierPath IdentifierPath { get { return ColumnSpec.IdentifierPath; } }
        public ColumnDescriptor ColumnDescriptor { get; private set; }
        public ColumnDescriptor CollectionColumn { get; private set; }
        public string DefaultDisplayName 
        {
            get
            {
                return DataSchema.GetDefaultDisplayName(this);
            }
        }
        public string ColumnCaption
        {
            get
            {
                return ColumnSpec.Caption ?? DefaultDisplayName;
            }
        }
        public string GetColumnCaption(PivotKey pivotKey)
        {
            StringBuilder prefix = new StringBuilder();
            while (pivotKey != null)
            {
                if (prefix.Length > 0)
                {
                    prefix.Insert(0, " ");
                }
                foreach (var kvp in pivotKey.ValuePairs)
                {
                    prefix.Insert(0, kvp.Value ?? "");
                }
                pivotKey = pivotKey.Parent;
            }
            if (prefix.Length == 0)
            {
                return ColumnCaption;
            }
            return prefix + " " + ColumnCaption;

        }
        public object GetValue(RowItem rowItem, PivotKey pivotKey)
        {
            if (rowItem == null)
            {
                return null;
            }
            if (ColumnDescriptor == null)
            {
                return "#COLUMN " + IdentifierPath + " NOT FOUND#";
            }
            return ColumnDescriptor.GetPropertyValue(rowItem, pivotKey);
        }
        public bool IsReadOnly
        {
            get
            {
                return ColumnDescriptor == null || ColumnDescriptor.IsReadOnly;
            }
        }
        public bool IsReadOnlyForRow(RowItem rowItem, PivotKey pivotKey)
        {
            if (IsReadOnly)
            {
                return true;
            }
            return ColumnDescriptor.IsReadOnlyForRow(rowItem, pivotKey);
        }
        public void SetValue(RowItem rowItem, PivotKey pivotKey, object value)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Column is read only");
            }
            ColumnDescriptor.SetValue(rowItem, pivotKey, value);
        }
        public Type PropertyType
        {
            get
            {
                if (ColumnDescriptor == null)
                {
                    return typeof (object);
                }
                return ColumnDescriptor.PropertyType ?? typeof (object);
            }
        }
    }
}
