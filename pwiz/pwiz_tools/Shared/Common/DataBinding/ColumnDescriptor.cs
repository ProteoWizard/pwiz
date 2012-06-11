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
    /// <summary>
    /// Provides a description of a property on a Type, or a descendent of a Type.
    /// </summary>
    /// <seealso cref="IdentifierPath"/>
    public class ColumnDescriptor
    {
        public ColumnDescriptor(DataSchema dataSchema, Type propertyType)
        {
            DataSchema = dataSchema;
            PropertyType = propertyType;
            IdPath = IdentifierPath.Root;
        }
        public ColumnDescriptor(ColumnDescriptor parent, String name) : this(parent, name, parent.DataSchema.GetPropertyDescriptor(parent.PropertyType, name))
        {
        }

        public ColumnDescriptor(ColumnDescriptor parent, PropertyDescriptor propertyDescriptor) : this(parent, propertyDescriptor.Name, propertyDescriptor)
        {
        }
        public ColumnDescriptor(ColumnDescriptor parent, string name, PropertyDescriptor propertyDescriptor)
        {
            DataSchema = parent.DataSchema;
            Parent = parent;
            IdPath = new IdentifierPath(parent.IdPath, name);
            ReflectedPropertyDescriptor = propertyDescriptor;
            if (ReflectedPropertyDescriptor != null)
            {
                PropertyType = ReflectedPropertyDescriptor.PropertyType;
            }
        }


        public ColumnDescriptor(ColumnDescriptor parent, CollectionInfo collectionInfo) : this(parent, null, null)
        {
            CollectionInfo = collectionInfo;
            PropertyType = collectionInfo.ElementType;
        }
        public ColumnDescriptor(ColumnDescriptor columnDescriptor)
        {
            DataSchema = columnDescriptor.DataSchema;
            Parent = columnDescriptor.Parent;
            IdPath = columnDescriptor.IdPath;
            PropertyType = columnDescriptor.PropertyType;
            CollectionInfo = columnDescriptor.CollectionInfo;
            ReflectedPropertyDescriptor = columnDescriptor.ReflectedPropertyDescriptor;
        }
        public DataSchema DataSchema { get; private set; }
        public ColumnDescriptor Parent { get; private set; }
        public ColumnDescriptor SetParent(ColumnDescriptor newParent)
        {
            if (newParent == Parent)
            {
                return this;
            }
            var newIdPath = newParent == null ? IdentifierPath.Root : new IdentifierPath(newParent.IdPath, Name);
            return new ColumnDescriptor(this) {Parent = newParent, IdPath = newIdPath};
        }
        public String Name { get { return IdPath.Name;} }
        public CollectionInfo CollectionInfo { get; private set; }
        public PropertyDescriptor ReflectedPropertyDescriptor { get; private set; }
        public Type PropertyType { get; private set; }
        public Type WrappedPropertyType
        {
            get
            {
                return PropertyType == null ? null : DataSchema.GetWrappedValueType(PropertyType);
            }
        }
        public object GetPropertyValue(RowItem rowItem, PivotKey pivotKey)
        {
            while (!IdPath.StartsWith(rowItem.SublistId))
            {
                rowItem = rowItem.Parent;
            }
            if (IdPath.Equals(rowItem.SublistId))
            {
                return rowItem.Value;
            }
            var parentValue = Parent.GetPropertyValue(rowItem, pivotKey);
            if (parentValue == null)
            {
                return null;
            }
            rowItem.HookPropertyChange(parentValue, ReflectedPropertyDescriptor);
            return GetPropertyValueFromParent(parentValue, pivotKey);
        }
        public object GetUnwrappedPropertyValue(RowItem rowItem, PivotKey pivotKey)
        {
            return DataSchema.UnwrapValue(GetPropertyValue(rowItem, pivotKey));
        }
        public object GetPropertyValue(RowNode rowNode)
        {
            if (IdPath.Length == rowNode.IdentifierPath.Length)
            {
                return rowNode.RowItem.Value;
            }
            var parentValue = Parent.GetPropertyValue(rowNode);
            if (parentValue == null)
            {
                return null;
            }
            
            return GetPropertyValueFromParent(parentValue, null);
        }

        public object GetPropertyValueFromParent(object parentComponent, PivotKey pivotKey)
        {
            if (parentComponent == null)
            {
                return null;
            }
            if (ReflectedPropertyDescriptor == null)
            {
                if (pivotKey == null)
                {
                    return null;
                }
                var collectionInfo = CollectionInfo;
                if (collectionInfo == null)
                {
                    return null;
                }
                var key = pivotKey.FindValue(IdPath);
                if (key == null)
                {
                    return null;
                }
                return collectionInfo.GetItemFromKey(parentComponent, key);
            }
            try
            {
                return ReflectedPropertyDescriptor.GetValue(parentComponent);
            }
            catch (Exception)
            {
                return null;
            }
        }
        public bool IsReadOnly
        {
            get
            {
                if (Parent == null || ReflectedPropertyDescriptor == null)
                {
                    return true;
                }
                return ReflectedPropertyDescriptor.IsReadOnly;
            }
        }
        public ColumnDescriptor GetOneToManyColumn()
        {
            if (Parent == null)
            {
                return null;
            }
            if (Parent.ReflectedPropertyDescriptor != null)
            {
                if (Parent.ReflectedPropertyDescriptor.PropertyType.IsGenericType && Parent.PropertyType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    return Parent.GetOneToManyColumn();
                }
            }
            if (Parent.CollectionInfo != null)
            {
                return Parent.Parent;
            }
            return null;
        }
        public bool IsAdvanced
        {
            get
            {
                return DataSchema.IsAdvanced(this);
            }
        }
        public void SetValue(RowItem rowItem, PivotKey pivotKey, object value)
        {
            if (Parent == null || ReflectedPropertyDescriptor == null)
            {
                return;
            }
            var parentComponent = Parent.GetPropertyValue(rowItem, pivotKey);
            if (parentComponent == null)
            {
                return;
            }
            ReflectedPropertyDescriptor.SetValue(parentComponent, value);
        }

        public bool IsReadOnlyForRow(RowItem rowItem, PivotKey pivotKey)
        {
            if (IsReadOnly)
            {
                return true;
            }
            return Parent.GetPropertyValue(rowItem, pivotKey) == null;
        }

        public string Caption { get; private set; } 
        public ColumnDescriptor SetCaption(string value)
        {
            if (value == Caption)
            {
                return this;
            }
            return new ColumnDescriptor(this){Caption = value};
        }
        public string DefaultCaption
        {
            get
            {
                return DataSchema.GetDisplayName(this);
            }
        }

        public string DisplayName {
            get
            {
                return Caption ?? DefaultCaption;
            }
        }

        public bool Hidden { get; private set; }
        public ColumnDescriptor SetHidden(bool value)
        {
            return Hidden == value ? this : new ColumnDescriptor(this){Hidden = value};
        }
        public ColumnDescriptor SetColumnSpec(ColumnSpec columnSpec)
        {
            return SetCaption(columnSpec.Caption)
                .SetHidden(columnSpec.Hidden);
        }
        public bool IsUnbound()
        {
            if (Parent == null)
            {
                return false;
            }
            if (Parent.IsUnbound())
            {
                return true;
            }
            return ReflectedPropertyDescriptor == null;
        }

        public IList<ColumnDescriptor> ListUnboundColumns()
        {            
            if (Parent == null)
            {
                return new ColumnDescriptor[0];
            }
            var parentUnboundColumns = Parent.ListUnboundColumns();
            if (ReflectedPropertyDescriptor != null)
            {
                return parentUnboundColumns;
            }
            var result = new ColumnDescriptor[parentUnboundColumns.Count + 1];
            parentUnboundColumns.CopyTo(result, 0);
            result[result.Length - 1] = this;
            return result;
        }

        public ColumnDescriptor FirstUnboundParent()
        {
            if (Parent == null)
            {
                return null;
            }
            if (ReflectedPropertyDescriptor == null)
            {
                return this;
            }
            return Parent.FirstUnboundParent();
        }

        public IdentifierPath IdPath
        {
            get; private set;
        }
        public ColumnDescriptor ResolveChild(string name)
        {
            if (PropertyType == null)
            {
                return null;
            }
            if (name == null)
            {
                var collectionInfo = DataSchema.GetCollectionInfo(PropertyType);
                if (collectionInfo != null)
                {
                    return new ColumnDescriptor(this, collectionInfo);
                }
            }
            var propertyDescriptor = DataSchema.GetPropertyDescriptor(PropertyType, name);
            if (propertyDescriptor == null)
            {
                return null;
            }
            return new ColumnDescriptor(this, propertyDescriptor);
        }
        public IEnumerable<ColumnDescriptor> GetChildColumns()
        {
            return DataSchema.GetPropertyDescriptors(PropertyType).Select(pd => new ColumnDescriptor(this, pd));
        }
        public ColumnDescriptor ResolveDescendant(IdentifierPath identifierPath)
        {
            if (identifierPath.IsRoot)
            {
                return this;
            }
            ColumnDescriptor parent = ResolveDescendant(identifierPath.Parent);
            if (parent == null)
            {
                return null;
            }
            return parent.ResolveChild(identifierPath.Name);
        }
        public bool IsSelectable
        {
            get
            {
                if (Parent != null)
                {
                    return true;
                }
                return DataSchema.IsRootTypeSelectable(PropertyType);
            }
        }
        public IAggregateFunction AggregateFunction { get; private set; }
        public ColumnDescriptor SetAggregateFunction(IAggregateFunction value)
        {
            return new ColumnDescriptor(this){AggregateFunction = value};
        }
    }
}
