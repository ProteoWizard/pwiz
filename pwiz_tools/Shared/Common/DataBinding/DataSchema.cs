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
                return CaptionFromName(type.Name);
            }
            return CaptionFromName(pdChain.PropertyType.Name);
        }
        public virtual bool IsRootTypeSelectable(Type type)
        {
            return typeof (ILinkValue).IsAssignableFrom(type);
        }

        public virtual event DataRowsChangedEventHandler DataRowsChanged;
        protected virtual void OnDataRowsChanged(DataRowsChangedEventArgs args)
        {
            var dataRowsChanged = DataRowsChanged;
            if (dataRowsChanged != null)
            {
                dataRowsChanged.Invoke(this, args);
            }
        }
        public virtual bool UpdateGridColumns(BindingListView bindingListView, DataGridViewColumn[] columnArray)
        {
            bool changed = false;
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
                if (dataGridViewColumn.SortMode == DataGridViewColumnSortMode.NotSortable)
                {
                    dataGridViewColumn.SortMode = DataGridViewColumnSortMode.Automatic;
                    changed = true;
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
                changed = true;
            }
            return changed;
        }
    }
}
