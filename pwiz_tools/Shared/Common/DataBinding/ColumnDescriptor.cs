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

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Provides a description of a property on a Type, or a descendent of a Type.
    /// </summary>
    /// <seealso cref="DataBinding.PropertyPath"/>
    public abstract class ColumnDescriptor
    {
        public static ColumnDescriptor RootColumn(DataSchema dataSchema, Type propertyType, string uiMode)
        {
            return new Root(dataSchema, propertyType, dataSchema.NormalizeUiMode(uiMode));
        }

        public static ColumnDescriptor RootColumn(DataSchema dataSchema, Type propertyType)
        {
            return RootColumn(dataSchema, propertyType, dataSchema.DefaultUiMode);
        }

        protected ColumnDescriptor(DataSchema dataSchema)
        {
            DataSchema = dataSchema;
        }

        protected ColumnDescriptor(ColumnDescriptor parent, PropertyPath propertyPath) : this(parent.DataSchema)
        {
            Parent = parent;
            PropertyPath = propertyPath;
        }

        public DataSchema DataSchema { get; private set; }
        public ColumnDescriptor Parent { get; private set; }
        public String Name { get { return PropertyPath.Name;} }
        public virtual ICollectionInfo CollectionInfo { get { return null; } }
        public virtual PropertyDescriptor ReflectedPropertyDescriptor { get { return null; } }
        public String Description { get { return DataSchema.GetColumnDescription(this); } }

        public virtual string UiMode
        {
            get { return Parent?.UiMode ?? string.Empty; }
        }

        public abstract Type PropertyType { get;  }
        public Type WrappedPropertyType
        {
            get
            {
                return PropertyType == null ? null : DataSchema.GetWrappedValueType(PropertyType);
            }
        }

        public abstract object GetPropertyValue(RowItem rowItem, PivotKey pivotKey);

        public virtual bool IsReadOnly
        {
            get
            {
                return true;
            }
        }
        public ColumnDescriptor GetOneToManyColumn()
        {
            if (Parent == null)
            {
                return null;
            }
            if (Parent.CollectionInfo != null)
            {
                return Parent.Parent;
            }
            if (Parent.Parent != null && Parent.Parent.PropertyType.IsGenericType 
                && Parent.Parent.PropertyType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                return Parent.GetOneToManyColumn();
            }
            return null;
        }
        public bool IsAdvanced
        {
            get
            {
                return DataSchema.IsHidden(this);
            }
        }
        public virtual void SetValue(RowItem rowItem, PivotKey pivotKey, object value)
        {
        }

        public string GetColumnCaption(ColumnCaptionType columnCaptionType)
        {
            return DataSchema.GetColumnCaption(this).GetCaption(DataSchema.GetDataSchemaLocalizer(columnCaptionType));
        }

        public ColumnDescriptor CollectionAncestor()
        {
            if (Parent == null)
            {
                return null;
            }
            if (CollectionInfo != null)
            {
                return this;
            }
            return Parent.CollectionAncestor();
        }

        public PropertyPath PropertyPath
        {
            get; protected set;
        }
        public ColumnDescriptor ResolveChild(string name)
        {
            var propertyDescriptor = DataSchema.GetPropertyDescriptor(PropertyType, name);
            if (propertyDescriptor == null)
            {
                return null;
            }
            return new Reflected(this, propertyDescriptor);
        }

        public ColumnDescriptor GetChild(PropertyDescriptor propertyDescriptor)
        {
            return new Reflected(this, propertyDescriptor);
        }

        public ColumnDescriptor GetCollectionColumn()
        {
            var collectionInfo = DataSchema.GetCollectionInfo(PropertyType);
            if (null == collectionInfo)
            {
                return null;
            }
            return new Collection(this, collectionInfo);
        }
        public IEnumerable<ColumnDescriptor> GetChildColumns()
        {
            return DataSchema.GetPropertyDescriptors(PropertyType).Select(pd => new Reflected(this, pd));
        }

        public virtual IEnumerable<Attribute> GetAttributes()
        {
            return new Attribute[0];
        }

        public virtual bool IsExpensive
        {
            get { return Parent != null && Parent.IsExpensive; }
        }

        #region Equality Members
        protected bool Equals(ColumnDescriptor other)
        {
            return Equals(DataSchema, other.DataSchema)
                   && Equals(Parent, other.Parent)
                   && Equals(PropertyPath, other.PropertyPath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ColumnDescriptor) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (DataSchema != null ? DataSchema.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Parent != null ? Parent.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PropertyPath != null ? PropertyPath.GetHashCode() : 0);
                return hashCode;
            }
        }
        #endregion

        private class Root : ColumnDescriptor
        {
            private readonly Type _propertyType;
            private string _uiMode;
            public Root(DataSchema dataSchema, Type propertyType, string uiMode) : base(dataSchema)
            {
                PropertyPath = PropertyPath.Root;
                _propertyType = propertyType;
                _uiMode = uiMode;
            }

            public override Type PropertyType
            {
                get { return _propertyType; }
            }

            public override object GetPropertyValue(RowItem rowItem, PivotKey pivotKey)
            {
                if (null != pivotKey)
                {
                    if (!rowItem.PivotKeys.Contains(pivotKey))
                    {
                        return null;
                    }
                }
                return rowItem.Value;
            }

            public override string UiMode
            {
                get { return _uiMode; }
            }

// ReSharper disable once MemberCanBePrivate.Local
            protected bool Equals(Root other)
            {
                return base.Equals(other) && _propertyType == other._propertyType && _uiMode == other._uiMode;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Root) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int result = base.GetHashCode();
                    result = (result * 397) ^ _propertyType.GetHashCode();
                    result = (result * 397) ^ (_uiMode == null ? 0 : _uiMode.GetHashCode());
                    return result;
                }
            }
        }

        private class Reflected : ColumnDescriptor
        {
            private readonly PropertyDescriptor _propertyDescriptor;
            public Reflected(ColumnDescriptor parent, PropertyDescriptor propertyDescriptor) : base(parent, parent.PropertyPath.Property(propertyDescriptor.Name))
            {
                _propertyDescriptor = propertyDescriptor;
            }

            public override Type PropertyType
            {
                get { return _propertyDescriptor.PropertyType; }
            }

            public override object GetPropertyValue(RowItem rowItem, PivotKey pivotKey)
            {
                object parentValue = Parent.GetPropertyValue(rowItem, pivotKey);
                if (null == parentValue)
                {
                    return null;
                }
                try
                {
                    return _propertyDescriptor.GetValue(parentValue);
                }
                catch
                {
                    return null;
                }
            }

            public override bool IsReadOnly
            {
                get { return _propertyDescriptor.IsReadOnly; }
            }

            public override void SetValue(RowItem rowItem, PivotKey pivotKey, object value)
            {
                var parentComponent = Parent.GetPropertyValue(rowItem, pivotKey);
                if (parentComponent == null)
                {
                    return;
                }
                _propertyDescriptor.SetValue(parentComponent, value);
            }

            public override IEnumerable<Attribute> GetAttributes()
            {
                return DataSchema.FilterAttributes(UiMode, _propertyDescriptor.Attributes.Cast<Attribute>()).Concat(base.GetAttributes());
            }

// ReSharper disable once MemberCanBePrivate.Local
            protected bool Equals(Reflected other)
            {
                return base.Equals(other) && _propertyDescriptor.Equals(other._propertyDescriptor);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Reflected) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (base.GetHashCode()*397) ^ _propertyDescriptor.GetHashCode();
                }
            }

            public override PropertyDescriptor ReflectedPropertyDescriptor
            {
                get { return _propertyDescriptor; }
            }

            public override bool IsExpensive
            {
                get { return base.IsExpensive || GetAttributes().OfType<ExpensiveAttribute>().Any(); }
            }
        }

        private class Collection : ColumnDescriptor
        {
            private readonly ICollectionInfo _collectionInfo;
            public Collection(ColumnDescriptor parent, ICollectionInfo collectionInfo) : base(parent, parent.PropertyPath.LookupAllItems())
            {
                _collectionInfo = collectionInfo;
            }

            public override Type PropertyType
            {
                get { return _collectionInfo.ElementType; }
            }

            public override object GetPropertyValue(RowItem rowItem, PivotKey pivotKey)
            {
                var collection = Parent.GetPropertyValue(rowItem, pivotKey);
                if (null == collection)
                {
                    return null;
                }
                object key = rowItem.RowKey.FindValue(PropertyPath);
                if (null == key && null != pivotKey)
                {
                    key = pivotKey.FindValue(PropertyPath);
                }
                if (null == key)
                {
                    return null;
                }
                return _collectionInfo.GetItemFromKey(collection, key);
            }

            public override ICollectionInfo CollectionInfo
            {
                get { return _collectionInfo; }
            }

// ReSharper disable once MemberCanBePrivate.Local
            protected bool Equals(Collection other)
            {
                return base.Equals(other) && _collectionInfo.Equals(other._collectionInfo);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Collection) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (base.GetHashCode()*397) ^ _collectionInfo.GetHashCode();
                }
            }
        }
    }
}
