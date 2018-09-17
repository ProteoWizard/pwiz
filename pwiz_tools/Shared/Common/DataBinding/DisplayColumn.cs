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
using pwiz.Common.DataBinding.Attributes;
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
        public IColumnCaption GetColumnCaption(PivotKey pivotKey)
        {
            IColumnCaption columnCaption;
            if (null != ColumnSpec.Caption)
            {
                columnCaption = ColumnCaption.UnlocalizableCaption(ColumnSpec.Caption);
            }
            else if (null == ColumnDescriptor)
            {
                columnCaption = ColumnCaption.UnlocalizableCaption(PropertyPath.ToString());
            }
            else
            {
                columnCaption = DataSchema.GetColumnCaption(ColumnDescriptor);
            }
            return QualifyColumnCaption(pivotKey, columnCaption);
        }

        public string GetColumnCaption(PivotKey pivotKey, ColumnCaptionType captionType)
        {
            return GetColumnCaption(pivotKey).GetCaption(captionType == ColumnCaptionType.invariant
                ? DataSchemaLocalizer.INVARIANT
                : DataSchema.DataSchemaLocalizer);
        }

        public string GetColumnDescription(PivotKey pivotKey)
        {
            if (ColumnDescriptor == null)
            {
                return null;
            }
            return DataSchema.GetColumnDescription(ColumnDescriptor);
        }

        public static IColumnCaption QualifyColumnCaption(PivotKey pivotKey, IColumnCaption columnCaption)
        {
            if (null == pivotKey)
            {
                return columnCaption;
            }
            return QualifyColumnCaption(pivotKey.KeyPairs.Select(pair => pair.Value), columnCaption);
        }

        public static IColumnCaption QualifyColumnCaption(IEnumerable<object> values, IColumnCaption columnCaption)
        {
            return CaptionComponentList.SpaceSeparate(values.Concat(new[] {columnCaption}));
        }

        public object GetValue(RowItem rowItem, PivotKey pivotKey)
        {
            if (rowItem == null)
            {
                return null;
            }
            if (ColumnDescriptor == null)
            {
                return @"#COLUMN " + PropertyPath + @" NOT FOUND#";
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
            var columnCaption = GetColumnCaption(pivotKey);
            var overrideAttributes = new List<Attribute>{new DisplayNameAttribute(columnCaption.GetCaption(DataSchema.DataSchemaLocalizer)),
                new ColumnCaptionAttribute(columnCaption)};
            if (ColumnDescriptor.IsExpensive)
            {
                overrideAttributes.Add(new ExpensiveAttribute());
            }
            var mergedAttributes = AttributeCollection.FromExisting(new AttributeCollection(ColumnDescriptor.GetAttributes().ToArray()), overrideAttributes.ToArray());
            return mergedAttributes.Cast<Attribute>();
        }
    }
}
