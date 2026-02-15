/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lists;

namespace pwiz.Skyline.Model.Databinding
{
    public class ListLookupPropertyDescriptor : PropertyDescriptor
    {
        private Type _listItemType;
        private PropertyDescriptor _innerPropertyDescriptor;
        private CachedValue<ListData> _listData;

        public ListLookupPropertyDescriptor(SkylineDataSchema dataSchema, string listName, PropertyDescriptor innerPropertyDescriptor) 
            : base(innerPropertyDescriptor.Name, GetAttributes(innerPropertyDescriptor))
        {
            SkylineDataSchema = dataSchema;
            ListName = listName;
            _listItemType = ListItemTypes.INSTANCE.GetListItemType(listName);
            _innerPropertyDescriptor = innerPropertyDescriptor;
            _listData = CachedValue.Create(dataSchema, () =>
            {
                return SkylineDataSchema.Document.Settings.DataSettings.Lists.FirstOrDefault(list =>
                    list.ListDef.Name == ListName);
            });
        }

        public SkylineDataSchema SkylineDataSchema { get; private set; }
        public string ListName { get; private set; }
        public ListData ListData { get { return _listData.Value; } }

        public override object GetValue(object component)
        {
            return MakeListItemFromPk(_innerPropertyDescriptor.GetValue(component));
        }

        public ListData GetListData()
        {
            return SkylineDataSchema.Document.Settings.DataSettings.Lists.FirstOrDefault(list =>
                list.ListDef.Name == ListName);
        }

        public ListItem MakeListItemFromPk(object annotationValue)
        {
            if (annotationValue == null)
            {
                return null;
            }

            return GetListData()?.GetListItem(annotationValue);
        }

        public IEnumerable<ListItem> GetListItems(ListData listData)
        {
            if (listData == null)
            {
                return new ListItem[0];
            }
            return Enumerable.Range(0, listData.RowCount).Select(rowIndex =>
                ListItem.ExistingRecord(_listItemType, listData, rowIndex));
        }

        public override Type PropertyType
        {
            get { return _listItemType; }
        }

        public override void SetValue(object component, object value)
        {
            var listItem = (ListItem) value;
            object annotationValue;
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            // ReSharper disable HeuristicUnreachableCode
            if (listItem == null)
            {
                annotationValue = null;
            }
            else
            {
                annotationValue = listItem.GetRecord().PrimaryKeyValue;
            }
            // ReSharper restore HeuristicUnreachableCode
            // ReSharper restore ConditionIsAlwaysTrueOrFalse
            _innerPropertyDescriptor.SetValue(component, annotationValue);
        }

        public override bool CanResetValue(object component)
        {
            return _innerPropertyDescriptor.CanResetValue(component);
        }

        public override Type ComponentType
        {
            get { return _innerPropertyDescriptor.ComponentType; }
        }

        public override bool IsReadOnly
        {
            get
            {
                return _innerPropertyDescriptor.IsReadOnly;
            }
        }

        public override void ResetValue(object component)
        {
            _innerPropertyDescriptor.ResetValue(component);
        }

        public override bool ShouldSerializeValue(object component)
        {
            return _innerPropertyDescriptor.ShouldSerializeValue(component);
        }

        private static Attribute[] GetAttributes(PropertyDescriptor innerPropertyDescriptor)
        {
            var attributes = new List<Attribute>();
            var annotationDef = (innerPropertyDescriptor as AnnotationPropertyDescriptor)?.AnnotationDef;
            if (!string.IsNullOrEmpty(annotationDef?.Lookup))
            {
                var listItemType = ListItemTypes.INSTANCE.GetListItemType(annotationDef.Lookup);
                if (annotationDef.Type == AnnotationDef.AnnotationType.number)
                {
                    attributes.Add(new ImportableAttribute
                        { Formatter = typeof(NumericKeyPropertyFormatter<>).MakeGenericType(listItemType) });
                }
                else
                {
                    attributes.Add(new ImportableAttribute
                        { Formatter = typeof(ListItemPropertyFormatter<>).MakeGenericType(listItemType) });
                }
            }

            attributes.AddRange(innerPropertyDescriptor.Attributes.Cast<Attribute>()
                .Where(a => a is DisplayNameAttribute || a is ColumnCaptionAttribute));
            return attributes.ToArray();
        }

        private class ListItemPropertyFormatter<T> : IPropertyFormatter
        {
            public string FormatValue(CultureInfo cultureInfo, object value)
            {
                var key = (value as ListItem)?.GetRecord().PrimaryKeyValue;
                if (key is double doubleValue)
                {
                    return doubleValue.ToString(Formats.RoundTrip, CultureInfo.InvariantCulture);
                }
                return value?.ToString() ?? string.Empty;
            }

            public virtual object ParseValue(CultureInfo cultureInfo, string text)
            {
                return ListItem.ConstructListItem(typeof(T), new ListItem.OrphanRecordData(text, false));
            }
        }

        private class NumericKeyPropertyFormatter<T> : ListItemPropertyFormatter<T>
        {
            public override object ParseValue(CultureInfo cultureInfo, string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }

                var doubleValue = double.Parse(text, cultureInfo);
                return ListItem.ConstructListItem(typeof(ListItem), new ListItem.OrphanRecordData(doubleValue, false));
            }
        }
    }
}
