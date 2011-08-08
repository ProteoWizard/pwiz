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
            PropertyDescriptor = propertyDescriptor;
            if (PropertyDescriptor != null)
            {
                PropertyType = PropertyDescriptor.PropertyType;
            }
        }


        public ColumnDescriptor(ColumnDescriptor parent, CollectionInfo collectionInfo)
        {
            DataSchema = parent.DataSchema;
            CollectionInfo = collectionInfo;
            PropertyType = collectionInfo.IsDictionary ? collectionInfo.ValueType : collectionInfo.ElementType;
        }
        public ColumnDescriptor(ColumnDescriptor columnDescriptor)
        {
            DataSchema = columnDescriptor.DataSchema;
            Parent = columnDescriptor.Parent;
            IdPath = columnDescriptor.IdPath;
            PropertyType = columnDescriptor.PropertyType;
            CollectionInfo = columnDescriptor.CollectionInfo;
            BoundKey = columnDescriptor.BoundKey;
            PropertyDescriptor = columnDescriptor.PropertyDescriptor;
            Visible = columnDescriptor.Visible;
            Caption = columnDescriptor.Caption;
            Crosstab = columnDescriptor.Crosstab;
        }
        public ColumnDescriptor RemoveAncestor(int depth)
        {
            int myDepth = IdPath.Length;
            if (depth > myDepth)
            {
                return null;
            }
            if (depth == myDepth)
            {
                return new ColumnDescriptor(DataSchema, PropertyType);
            }
            return new ColumnDescriptor(Parent.RemoveAncestor(depth), Name, PropertyDescriptor);
        }

        public DataSchema DataSchema { get; private set; }
        public ColumnDescriptor Parent { get; private set; }
        public ColumnDescriptor SetParent(ColumnDescriptor newParent)
        {
            if (newParent == Parent)
            {
                return this;
            }
            return new ColumnDescriptor(this) {Parent = newParent};
        }
        public String Name { get { return IdPath == null ? null : IdPath.Name;} }
        public CollectionInfo CollectionInfo { get; private set; }
        protected PropertyDescriptor PropertyDescriptor { get; private set; }
        public Type PropertyType { get; private set; }
        public object BoundKey { get; private set; }
        public ColumnDescriptor BindKey(object key)
        {
            return new ColumnDescriptor(this) {BoundKey = key};
        }
        public ColumnDescriptor Bind(RowKey rowKey)
        {
            var result = this;
            if (Parent != null)
            {
                result = result.SetParent(Parent.Bind(rowKey));
            }
            object key;
            if (rowKey.TryGetValue(IdPath, out key))
            {
                return result.BindKey(key);
            }
            return result;
        }
        public object GetPropertyValue(object component)
        {
            if (Parent == null)
            {
                var rowValue = component as RowValue;
                if (rowValue != null)
                {
                    return rowValue.RowData;
                }
                return component;
            }
            if (IsUnbound())
            {
                var rowValue = component as RowValue;
                if (rowValue == null)
                {
                    return null;
                }
                rowValue.RowKey.TryGetValue(IdPath, out component);
                return component;
            }
            return GetPropertyValueFromParent(Parent.GetPropertyValue(component));
        }

        public object GetPropertyValueFromParent(object parentComponent)
        {
            if (parentComponent == null)
            {
                return null;
            }
            if (PropertyDescriptor == null)
            {
                return parentComponent;
            }
            return PropertyDescriptor.GetValue(parentComponent);
        }

        public bool Crosstab { get; private set; }
        public ColumnDescriptor SetCrosstab(bool value)
        {
            if (value == Crosstab)
            {
                return this;
            }
            return new ColumnDescriptor(this) {Crosstab = value};
        }
        public bool Visible { get; private set; }

        public string Caption { get; private set; } 
        public ColumnDescriptor SetCaption(string value)
        {
            if (value == Caption)
            {
                return this;
            }
            return new ColumnDescriptor(this){Caption = value};
        }
        public string DisplayName {
            get
            {
                return Caption ?? DataSchema.CaptionFromName(Name);
            }
        }

        public ColumnSpec GetColumnSpec()
        {
            return new ColumnSpec()
                .SetIdentifierPath(IdPath)
                .SetVisible(Visible)
                .SetCrosstab(Crosstab)
                .SetCaption(Caption);
        }
        public ColumnDescriptor SetColumnSpec(ColumnSpec columnSpec)
        {
            return SetVisible(columnSpec.Visible)
                .SetCrosstab(columnSpec.Crosstab)
                .SetCaption(columnSpec.Caption);
        }
        public ColumnDescriptor SetVisible(bool value)
        {
            if (value == Visible)
            {
                return this;
            }
            return new ColumnDescriptor(this) {Visible = value};
        }

        public bool IsUnbound()
        {
            return PropertyDescriptor == null && BoundKey == null;
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
            var propertyDescriptor = DataSchema.GetPropertyDescriptor(PropertyType, name);
            if (propertyDescriptor == null)
            {
                return null;
            }
            return new ColumnDescriptor(this, propertyDescriptor);
        }
        public ColumnDescriptor ResolveDescendant(IdentifierPath identifierPath)
        {
            if (identifierPath == null)
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
    }
}
