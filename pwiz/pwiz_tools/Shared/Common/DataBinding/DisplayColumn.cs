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
using System.Text;
using pwiz.Common.Properties;

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
                : columnDescriptor.CollectionAncestor() ?? viewInfo.ParentColumn;
        }

        public ViewInfo ViewInfo { get; private set; }
        public DataSchema DataSchema { get { return ViewInfo.DataSchema; } }
        public ColumnSpec ColumnSpec { get; private set; }
        public PropertyPath PropertyPath { get { return ColumnSpec.PropertyPath; } }
        public ColumnDescriptor ColumnDescriptor { get; private set; }
        public ColumnDescriptor CollectionColumn { get; private set; }
        public string GetColumnCaption(PivotKey pivotKey, ColumnCaptionType columnCaptionType)
        {
            string columnCaption;
            if (null != ColumnSpec.Caption)
            {
                columnCaption = ColumnSpec.Caption;
            }
            else if (null == ColumnDescriptor)
            {
                columnCaption = PropertyPath.ToString();
            }
            else
            {
                columnCaption = DataSchema.GetColumnCaption(DataSchema.GetColumnCaption(ColumnDescriptor), columnCaptionType);
            }
            return QualifyColumnCaption(pivotKey, columnCaption);
        }

        public static string QualifyColumnCaption(PivotKey pivotKey, string columnCaption)
        {
            if (null == pivotKey)
            {
                return columnCaption;
            }
            StringBuilder prefix = new StringBuilder();
            foreach (var kvp in pivotKey.KeyPairs)
            {
                if (prefix.Length > 0)
                {
                    prefix.Append(" "); // Not L10N
                }
                prefix.Append(kvp.Value ?? string.Empty);
            }
            if (prefix.Length == 0)
            {
                return columnCaption;
            }
            return prefix + " " + columnCaption; // Not L10N
        }

        public object GetValue(RowItem rowItem, PivotKey pivotKey)
        {
            if (rowItem == null)
            {
                return null;
            }
            if (ColumnDescriptor == null)
            {
                return "#COLUMN " + PropertyPath + " NOT FOUND#"; // Not L10N
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

        public void SetValue(RowItem rowItem, PivotKey pivotKey, object value)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException(Resources.DisplayColumn_SetValue_Column_is_read_only);
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

        public IEnumerable<Attribute> GetAttributes(PivotKey pivotKey)
        {
            if (null == ColumnDescriptor)
            {
                return new Attribute[0];
            }
            var overrideAttributes = new Attribute[] {new DisplayNameAttribute(GetColumnCaption(pivotKey, ColumnCaptionType.localized))};
            var mergedAttributes = AttributeCollection.FromExisting(new AttributeCollection(ColumnDescriptor.GetAttributes().ToArray()), overrideAttributes);
            return mergedAttributes.Cast<Attribute>();
        }
    }
}
