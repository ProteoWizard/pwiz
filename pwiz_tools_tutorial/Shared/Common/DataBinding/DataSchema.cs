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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Handles property inspection on types.
    /// Applications can override this class in order to add properties to types to
    /// include user-defined properties.
    /// </summary>
    public class DataSchema
    {
        public virtual IEnumerable<PropertyDescriptor> GetPropertyDescriptors(Type type)
        {
            var chainParent = GetChainedPropertyDescriptorParent(type);
            if (chainParent != null)
            {
                return GetPropertyDescriptors(chainParent.PropertyType)
                    .Select(pd => (PropertyDescriptor) new ChainedPropertyDescriptor(pd.Name, chainParent, pd));
            }
            if (IsScalar(type))
            {
                return new PropertyDescriptor[0];
            }
            return TypeDescriptor.GetProperties(type).Cast<PropertyDescriptor>().Where(IsBrowsable);
        }
        public PropertyDescriptor GetPropertyDescriptor(Type type, string name)
        {
            return GetPropertyDescriptors(type).FirstOrDefault(pd => pd.Name == name);
        }
        public CollectionInfo GetCollectionInfo(Type type)
        {
            return CollectionInfo.ForType(type);
        }
        public virtual bool IsBrowsable(PropertyDescriptor propertyDescriptor)
        {
            if (!propertyDescriptor.IsBrowsable)
            {
                return false;
            }
            if (propertyDescriptor.PropertyType.IsGenericType && typeof(ICollection<>) == propertyDescriptor.PropertyType.GetGenericTypeDefinition())
            {
                return false;
            }
            return true;
        }
        protected bool IsScalar(Type type)
        {
            return type.IsPrimitive 
                || type.IsEnum
                || type == typeof (string);
        }
        public virtual bool IsReadOnly(PropertyDescriptor propertyDescriptor)
        {
            return true;
        }
        protected PropertyDescriptor GetChainedPropertyDescriptorParent(Type type)
        {
            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Nullable<>)
                    || genericTypeDefinition == typeof(LinkValue<>))
                {
                    return TypeDescriptor.GetProperties(type).Find("Value", false);
                }

            }
            return null;
        }
        public Type GetWrappedValueType(Type type)
        {
            var propertyDescriptor = GetChainedPropertyDescriptorParent(type);
            if (propertyDescriptor == null)
            {
                return type;
            }
            return propertyDescriptor.PropertyType;
        }
        public object UnwrapValue(object value)
        {
            if (value == null)
            {
                return null;
            }
            var propertyDescriptor = GetChainedPropertyDescriptorParent(value.GetType());
            if (propertyDescriptor != null)
            {
                return propertyDescriptor.GetValue(value);
            }
            return value;
        }

        public virtual int Compare(object o1, object o2)
        {
            if (o1 == o2)
            {
                return 0;
            }
            while (o1 is ILinkValue)
            {
                o1 = ((ILinkValue) o1).Value;
            }
            while (o2 is ILinkValue)
            {
                o2 = ((ILinkValue) o2).Value;
            }
            if (o1 is IComparable || o2 is IComparable)
            {
                return Comparer.Default.Compare(o1, o2);
            }
            if (o1 == null)
            {
                return -1;
            }
            if (o2 == null)
            {
                return 1;
            }
            return Comparer.Default.Compare(o1.ToString(), o2.ToString());
        }
        public virtual string CaptionFromName(string name)
        {
            StringBuilder result = new StringBuilder();
            char? lastCh = null;
            foreach (var ch in name)
            {
                if (char.IsUpper(ch) && lastCh.HasValue && char.IsLower(lastCh.Value))
                {
                    result.Append(" ");
                }
                result.Append(ch);
                lastCh = ch;
            }
            return result.ToString();
        }
        public virtual string CaptionFromType(Type type)
        {
            var pdChain = GetChainedPropertyDescriptorParent(type);
            if (pdChain == null)
            {
                var displayNameAttribute =
                    type.GetCustomAttributes(typeof (DisplayNameAttribute), true)
                        .Cast<DisplayNameAttribute>().FirstOrDefault();
                if (displayNameAttribute != null && !displayNameAttribute.IsDefaultAttribute())
                {
                    return displayNameAttribute.DisplayName;
                }
                return CaptionFromName(type.Name);
            }
            return CaptionFromType(pdChain.PropertyType);
        }
        public virtual bool IsRootTypeSelectable(Type type)
        {
            return typeof (ILinkValue).IsAssignableFrom(type);
        }
        public virtual string GetDisplayName(ColumnDescriptor columnDescriptor)
        {
            var oneToManyColumn = columnDescriptor.GetOneToManyColumn();
            if (oneToManyColumn != null && oneToManyColumn.ReflectedPropertyDescriptor != null)
            {
                var oneToManyAttribute = oneToManyColumn.ReflectedPropertyDescriptor.Attributes[typeof(OneToManyAttribute)] as OneToManyAttribute;
                if (oneToManyAttribute != null)
                {
                    if ("Key" == columnDescriptor.Name && oneToManyAttribute.IndexDisplayName != null)
                    {
                        return oneToManyAttribute.IndexDisplayName;
                    }
                    if ("Value" == columnDescriptor.Name && oneToManyAttribute.ItemDisplayName != null)
                    {
                        return oneToManyAttribute.ItemDisplayName;
                    }
                }
            }
            if (columnDescriptor.ReflectedPropertyDescriptor != null)
            {
                var displayNameAttr = columnDescriptor.ReflectedPropertyDescriptor.Attributes[typeof(DisplayNameAttribute)] as DisplayNameAttribute;
                if (displayNameAttr != null && !displayNameAttr.IsDefaultAttribute())
                {
                    return displayNameAttr.DisplayName;
                }
            }
            if (columnDescriptor.Name == null && columnDescriptor.Parent != null)
            {
                return columnDescriptor.Parent.DefaultCaption;
            }
            if (columnDescriptor.Name == null && columnDescriptor.PropertyType != null)
            {
                return CaptionFromType(columnDescriptor.PropertyType);
            }
            return CaptionFromName(columnDescriptor.Name);
        }

        public virtual string GetBaseDisplayName(DisplayColumn displayColumn)
        {
            var columnDescriptor = displayColumn.ColumnDescriptor;
            var oneToManyColumn = columnDescriptor.GetOneToManyColumn();
            if (oneToManyColumn != null && oneToManyColumn.ReflectedPropertyDescriptor != null)
            {
                var oneToManyAttribute = oneToManyColumn.ReflectedPropertyDescriptor.Attributes[typeof(OneToManyAttribute)] as OneToManyAttribute;
                if (oneToManyAttribute != null)
                {
                    if ("Key" == columnDescriptor.Name && oneToManyAttribute.IndexDisplayName != null)
                    {
                        return oneToManyAttribute.IndexDisplayName;
                    }
                    if ("Value" == columnDescriptor.Name && oneToManyAttribute.ItemDisplayName != null)
                    {
                        return oneToManyAttribute.ItemDisplayName;
                    }
                }
            }
            if (columnDescriptor.Name == null && columnDescriptor.PropertyType != null)
            {
                return CaptionFromType(columnDescriptor.PropertyType);
            }
            return GetDisplayName(columnDescriptor);
        }
        public virtual string GetDefaultDisplayName(DisplayColumn displayColumn)
        {
            return GetBaseDisplayName(displayColumn);
        }
        public virtual bool IsAdvanced(ColumnDescriptor columnDescriptor)
        {
            ColumnDescriptor oneToManyColumn = columnDescriptor.GetOneToManyColumn();
            if (oneToManyColumn != null)
            {
                if (oneToManyColumn.ReflectedPropertyDescriptor != null)
                {
                    var oneToManyAttribute = oneToManyColumn.ReflectedPropertyDescriptor
                        .Attributes[typeof(OneToManyAttribute)] as OneToManyAttribute;
                    if (oneToManyAttribute != null && oneToManyAttribute.ForeignKey == columnDescriptor.Name)
                    {
                        return true;
                    }
                }
            }
            if (columnDescriptor.ReflectedPropertyDescriptor != null)
            {
                var dataColumnAttribute = columnDescriptor.ReflectedPropertyDescriptor
                    .Attributes[typeof (DataColumnAttribute)] as DataColumnAttribute;
                if (dataColumnAttribute != null)
                {
                    return dataColumnAttribute.Advanced;
                }
            }
            return false;
        }

        public virtual void UpdateGridColumns(BindingListView bindingListView, DataGridViewColumn[] columnArray)
        {
            var properties = bindingListView.GetItemProperties(new PropertyDescriptor[0])
                .Cast<PropertyDescriptor>()
                .ToDictionary(pd => pd.Name, pd => pd);
            for (int iCol = 0; iCol < columnArray.Count(); iCol++)
            {
                var dataGridViewColumn = columnArray[iCol];
                PropertyDescriptor pd;
                if (!properties.TryGetValue(dataGridViewColumn.DataPropertyName, out pd))
                {
                    continue;
                }
                if (pd is ColumnPropertyDescriptor)
                {
                    var columnDescriptor = ((ColumnPropertyDescriptor) pd).DisplayColumn.ColumnDescriptor;
                    if (columnDescriptor == null)
                    {
                        continue;
                    }
                    if (columnDescriptor.ReflectedPropertyDescriptor != null)
                    {
                        var dataColumnAttribute =
                            columnDescriptor.ReflectedPropertyDescriptor.Attributes[typeof (DataColumnAttribute)] as
                            DataColumnAttribute;
                        if (dataColumnAttribute != null)
                        {
                            if (dataColumnAttribute.Format != null)
                            {
                                dataGridViewColumn.DefaultCellStyle.Format = dataColumnAttribute.Format;
                            }
                        }
                    }
                }
                if (dataGridViewColumn.SortMode == DataGridViewColumnSortMode.NotSortable)
                {
                    dataGridViewColumn.SortMode = DataGridViewColumnSortMode.Automatic;
                }
                if (!typeof(ILinkValue).IsAssignableFrom(pd.PropertyType))
                {
                    continue;
                }
                var textBoxColumn = dataGridViewColumn as DataGridViewTextBoxColumn;
                if (textBoxColumn == null)
                {
                    continue;
                }
                var linkColumn = new DataGridViewLinkColumn
                {
                    Name = textBoxColumn.Name,
                    DataPropertyName = textBoxColumn.DataPropertyName,
                    DisplayIndex = textBoxColumn.DisplayIndex,
                    HeaderText = textBoxColumn.HeaderText,
                    SortMode = textBoxColumn.SortMode,
                };
                columnArray[iCol] = linkColumn;
            }
        }
    }
}
