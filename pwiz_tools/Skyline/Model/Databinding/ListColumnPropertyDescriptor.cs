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
using System.ComponentModel;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding
{
    public class ListColumnPropertyDescriptor : PropertyDescriptor
    {
        private CachedValue<ListInfo> _listInfo;
        private Type _listItemType;
        public ListColumnPropertyDescriptor(SkylineDataSchema dataSchema, string listName, AnnotationDef annotationDef)
            : base(AnnotationDef.ANNOTATION_PREFIX + annotationDef.Name, AnnotationPropertyDescriptor.GetAttributes(annotationDef))
        {
            SkylineDataSchema = dataSchema;
            ListName = listName;
            AnnotationDef = annotationDef;
            _listInfo = CachedValue.Create(dataSchema, ()=>GetListInfo(SkylineDataSchema.Document));
            _listItemType = ListItemTypes.INSTANCE.GetListItemType(listName);
        }

        public SkylineDataSchema SkylineDataSchema { get; private set; }
        public string ListName { get; private set; }
        public AnnotationDef AnnotationDef { get; private set; }

        public override bool CanResetValue(object component)
        {
            return null != GetValue(component);
        }

        public override Type ComponentType
        {
            get { return _listItemType; }
        }

        public override object GetValue(object component)
        {
            var listItem = component as ListItem;
            if (listItem == null)
            {
                return null;
            }
            var newRecord = listItem.GetRecord() as ListItem.NewRecordData;
            if (newRecord != null)
            {
                return newRecord.GetColumnValue(AnnotationDef.Name);
            }
            var existingRecord = listItem.GetRecord() as ListItem.ExistingRecordData;
            if (existingRecord != null)
            {
                var listInfo = _listInfo.Value;
                if (listInfo == null)
                {
                    return null;
                }
                var rowIndex = listInfo.ListData.RowIndexOf(existingRecord.ListItemId);
                if (rowIndex < 0)
                {
                    return null;
                }
                return listInfo.ListData.Columns[listInfo.ColumnIndex].GetValue(rowIndex);
            }
            return null;
        }

        public override bool IsReadOnly
        {
            get { return false; }
        }

        public override Type PropertyType
        {
            get { return AnnotationDef.ValueType; }
        }

        public override void ResetValue(object component)
        {
            // ReSharper disable AssignNullToNotNullAttribute
            SetValue(component, null);
            // ReSharper restore AssignNullToNotNullAttribute
        }

        public override void SetValue(object component, object value)
        {
            var listItem = (ListItem) component;
            var newRecord = listItem.GetRecord() as ListItem.NewRecordData;
            if (newRecord != null)
            {
                newRecord.UncommittedValues[AnnotationDef.Name] = value;
                return;
            }
            var existingRecord = listItem.GetRecord() as ListItem.ExistingRecordData;
            if (existingRecord == null)
            {
                throw new InvalidOperationException(@"Invalid row " + listItem.GetRecord()); // Cannot happen
            }
            var editDescription = EditDescription.SetAnnotation(AnnotationDef, value)
                .ChangeElementRef(((ListRef) ListRef.PROTOTYPE.ChangeName(ListName)).GetListItemRef(listItem));
            SkylineDataSchema.ModifyDocument(editDescription, doc =>
            {
                var listInfo = GetListInfo(doc);
                if (listInfo == null)
                {
                    throw new InvalidOperationException(Resources.ListColumnPropertyDescriptor_SetValue_List_has_been_deleted);
                }
                if (listInfo.ColumnIndex < 0)
                {
                    throw CommonException.Create(ListExceptionDetail.ColumnNotFound(ListName, AnnotationDef.Name));
                }
                int rowIndex = listInfo.ListData.RowIndexOf(existingRecord.ListItemId);
                if (rowIndex < 0)
                {
                    throw new ArgumentException(Resources.ListColumnPropertyDescriptor_SetValue_List_item_has_been_deleted_);
                }
                var listData = listInfo.ListData;
                listData = listData.ChangeColumn(listInfo.ColumnIndex, listData.Columns[listInfo.ColumnIndex].SetValue(rowIndex, value));
                return doc.ChangeSettings(
                    doc.Settings.ChangeDataSettings(
                    
                    ChangeListData(doc.Settings.DataSettings, listData)));
            }, AuditLogEntry.SettingsLogFunction);

        }

        private DataSettings ChangeListData(DataSettings dataSettings, ListData listData)
        {
            for (int i = 0; i < dataSettings.Lists.Count; i++)
            {
                if (listData.ListDef.Name == dataSettings.Lists[i].ListDef.Name)
                {
                    return dataSettings.ChangeListDefs(dataSettings.Lists.ReplaceAt(i, listData));
                }
            }
            throw new ArgumentException(string.Format(Resources.ListColumnPropertyDescriptor_ChangeListData_No_such_list__0_, listData.ListDef.Name));
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }

        private ListInfo GetListInfo(SrmDocument document)
        {
            var listData =
                SkylineDataSchema.Document.Settings.DataSettings.Lists.FirstOrDefault(list =>
                    list.ListDef.Name == ListName);
            if (listData == null)
            {
                return null;
            }
            return new ListInfo(listData, listData.FindColumnIndex(AnnotationDef.Name));
        }

        private class ListInfo
        {
            public ListInfo(ListData listData, int columnIndex)
            {
                ListData = listData;
                ColumnIndex = columnIndex;
            }
            public ListData ListData { get; private set; }
            public int ColumnIndex { get; private set; }
        }
    }
}
