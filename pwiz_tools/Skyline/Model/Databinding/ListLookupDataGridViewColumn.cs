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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Lists;

namespace pwiz.Skyline.Model.Databinding
{
    public class ListLookupDataGridViewColumn : BoundComboBoxColumn
    {
        public ListLookupDataGridViewColumn() : base(new ListItemCell())
        {
            DisplayMember = @"Key";
            ValueMember = @"Value";
        }

        protected override object[] GetDropdownItems()
        {
            if (ListLookupPropertyDescriptor == null)
            {
                return null;
            }

            var items = new List<KeyValuePair<string, ListItem>>();
            items.Add(new KeyValuePair<string, ListItem>(string.Empty, null));
            var listData = ListLookupPropertyDescriptor.GetListData();
            var listItems = ListLookupPropertyDescriptor.GetListItems(listData).ToArray();
            var displayTexts = MakeUnambiguousNames(listData, listItems);
            for (int i = 0; i < listItems.Length; i++)
            {
                items.Add(new KeyValuePair<string, ListItem>(displayTexts[i], listItems[i]));
            }
            return items.Cast<object>().ToArray();
        }

        public ListLookupPropertyDescriptor ListLookupPropertyDescriptor
        {
            get {
                if (ColumnPropertyDescriptor == null)
                {
                    return null;
                } 
                return ColumnPropertyDescriptor.DisplayColumn.ColumnDescriptor.ReflectedPropertyDescriptor as ListLookupPropertyDescriptor;
            }
        }

        public class ListItemCell : BoundComboBoxCell
        {
            protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
                TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
            {
                var listItem = value as ListItem;
                if (listItem != null && listItem.GetRecord() is ListItem.OrphanRecordData)
                {
                    return listItem.ToString(Style.Format, Style.FormatProvider);
                }
                object result = base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter,
                    formattedValueTypeConverter, context);
                return result;
            }        
        }

        /// <summary>
        /// The combo box relies on each of the items in the list having a unique text form.
        /// If any of the list item texts are the same, then they get the bracketed primary
        /// key value appeneded to them.
        /// </summary>
        private IList<string> MakeUnambiguousNames(ListData listData, IList<ListItem> listItems)
        {
            string format = DefaultCellStyle.Format;
            IFormatProvider formatProvider = DefaultCellStyle.FormatProvider;
            var displayTexts = new string[listItems.Count];
            for (int i = 0; i < listItems.Count; i++)
            {
                displayTexts[i] = listItems[i].ToString(format, formatProvider);
            }
            var duplicates = new HashSet<string>(displayTexts.ToLookup(x => x)
                .Where(grouping => grouping.Count() > 1)
                .Select(grouping => grouping.Key));
            if (duplicates.Any())
            {
                for (int i = 0; i < listItems.Count; i++)
                {
                    if (duplicates.Contains(displayTexts[i]))
                    {
                        displayTexts[i] = listItems[i].GetRecord().GetFullyQualifiedLabel(format, formatProvider);
                    }
                }
            }
            return displayTexts;
        }
    }
}
