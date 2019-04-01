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
using System.Data;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lists
{
    [XmlRoot("list_data")]
    public class ListData : Immutable, IXmlSerializable, IKeyContainer<string>, IAuditLogObject
    {
        public static readonly ListData EMPTY = new ListData(ListDef.EMPTY, ImmutableList.Empty<ColumnData>());
        private ImmutableList<ListItemId> _ids;
        private int _icolPk;
        private int _icolDisplay;
        private Dictionary<object, int> _pkIndex;
        public ListData(ListDef listDef, IEnumerable<ColumnData> columns)
        {
            ListDef = listDef;
            Columns = ImmutableList.ValueOf(columns);
            Init();
            RebuildIndex();
        }

        public ListData(ListDef listDef) : this(listDef, listDef.Properties.Select(ColumnData.MakeColumnData))
        {
        }

        public string ListName { get { return ListDef.Name; } }
        [TrackChildren(ignoreName: true)]
        public ListDef ListDef { get; private set; }
        public ImmutableList<ColumnData> Columns { get; private set; }

        [TrackChildren]
        public IList<Annotations> Rows
        {
            get { return ReadOnlyList.Create(RowCount, GetRowAnnotations); }
        }

        string IKeyContainer<string>.GetKey()
        {
            return ListName;
        }

        public ColumnData PkColumn
        {
            get { return GetColumn(_icolPk); }
        }

        public ColumnData DisplayColumn
        {
            get { return GetColumn(_icolDisplay); }
        }

        public ImmutableList<ListItemId> ListItemIds
        {
            get { return _ids; }
        }

        private void Init()
        {
            _ids = ImmutableList.ValueOf(Enumerable.Range(1, RowCount).Select(i => new ListItemId(i)));
            _icolPk = EnsureFindColumnIndex(ListDef.IdProperty);
            _icolDisplay = EnsureFindColumnIndex(ListDef.DisplayProperty);
            if (_icolDisplay == _icolPk)
            {
                _icolDisplay = -1;
            }
        }

        private int EnsureFindColumnIndex(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
            {
                return -1;
            }
            int icol = FindColumnIndex(columnName);
            if (icol < 0)
            {
                throw CommonException.Create(ListExceptionDetail.ColumnNotFound(ListName, columnName));
            }
            return icol;
        }

        private void RebuildIndex()
        {
            var pkColumn = GetColumn(_icolPk);
            if (pkColumn == null)
            {
                _pkIndex = null;
                return;
            }

            var dictionary = new Dictionary<object, int>(pkColumn.RowCount);
            for (int i = 0; i < pkColumn.RowCount; i++)
            {
                var value = pkColumn.GetValue(i);
                if (null == value)
                {
                    throw CommonException.Create(ListExceptionDetail.NullValue(ListName, ListDef.IdProperty));
                }
                try
                {
                    dictionary.Add(pkColumn.GetValue(i), i);
                }
                catch (Exception e)
                {
                    throw CommonException.Create(ListExceptionDetail.DuplicateValue(ListName, ListDef.IdProperty, value), e);
                }
            }
            _pkIndex = dictionary;
        }

        private void CopyIndexFrom(ListData listData)
        {
            if (ReferenceEquals(PkColumn, listData.PkColumn))
            {
                _icolPk = listData._icolPk;
            }
            else
            {
                RebuildIndex();
            }
        }

        public ListData ChangeColumn(int i, ColumnData columnData)
        {
            if (RowCount != columnData.RowCount)
            {
                throw new ArgumentException();
            }
            return ChangeProp(ImClone(this), im =>
            {
                im.Columns = im.Columns.ReplaceAt(i, columnData);
                im.CopyIndexFrom(this);
            });
        }

        public ListData AddRow(IDictionary<string, object> values, out ListItemId newItemId)
        {
            if (_ids.Count == 0)
            {
                newItemId = new ListItemId(1);
            }
            else
            {
                newItemId = new ListItemId(_ids[_ids.Count - 1].IntValue + 1);
            }
            var newIds = ImmutableList.ValueOf(_ids.Concat(new[] {newItemId}));
            return ChangeProp(ImClone(this), im =>
            {
                var newColumns = new List<ColumnData>();
                for (int iCol = 0; iCol < ListDef.Properties.Count; iCol++)
                {
                    object value;
                    values.TryGetValue(ListDef.Properties[iCol].Name, out value);
                    newColumns.Add(Columns[iCol].AddRow(value));
                }
                im.Columns = ImmutableList.ValueOf(newColumns);
                im._ids = newIds;
                im.RebuildIndex();
            });
        }

        public int RowCount
        {
            get
            {
                if (Columns.Count == 0)
                {
                    return 0;
                }
                return Columns[0].RowCount;
            }
        }

        protected bool Equals(ListData other)
        {
            return ListDef.Equals(other.ListDef) && Columns.Equals(other.Columns);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ListData) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ListDef.GetHashCode() * 397) ^ Columns.GetHashCode();
            }
        }

        private ListData()
        {
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        private enum El
        {
            list_def,
            column
        }

        public void ReadXml(XmlReader reader)
        {
            if (null != Columns)
            {
                throw new ReadOnlyException();
            }
            List<ColumnData> columns = new List<ColumnData>();
            if (reader.IsEmptyElement)
            {
                reader.ReadElementString(@"list_data");
                return;
            }
            reader.Read();
            while (true)
            {
                if (reader.IsStartElement(El.list_def))
                {
                    ListDef = ListDef.Deserialize(reader);
                }
                else if (reader.IsStartElement(El.column))
                {
                    string columnText = reader.ReadElementContentAsString();
                    if (columns.Count < ListDef.Properties.Count)
                    {
                        var annotationDef = ListDef.Properties[columns.Count];
                        columns.Add(ColumnData.MakeColumnData(annotationDef).SetPersistedString(columnText));
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    reader.ReadEndElement();
                    break;
                }
                else
                {
                    reader.Skip();
                }
            }
            Columns = ImmutableList.ValueOf(columns);
            Init();
            RebuildIndex();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(El.list_def);
            ListDef.WriteXml(writer);
            writer.WriteEndElement();
            foreach (var column in Columns)
            {
                writer.WriteElementString(El.column, column.ToPersistedString());
            }
        }

        public static ListData Deserialize(XmlReader xmlReader)
        {
            return xmlReader.Deserialize(new ListData());
        }

        public int FindColumnIndex(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
            {
                return -1;
            }
            return ListDef.Properties.IndexOf(annotationDef => annotationDef.Name == columnName);
        }

        public ColumnData FindColumn(string columnName)
        {
            return GetColumn(FindColumnIndex(columnName));
        }

        public ColumnData GetColumn(int index)
        {
            if (index < 0)
            {
                return null;
            }
            return Columns[index];
        }

        public int RowIndexOf(ListItemId listItemId)
        {
            if (_ids == null)
            {
                if (listItemId.IntValue < 0 || listItemId.IntValue >= RowCount)
                {
                    return -1;
                }
            }
            int index = CollectionUtil.BinarySearch(_ids, listItemId);
            if (index < 0)
            {
                return -1;
            }
            return index;
        }

        public int RowIndexOfPrimaryKey(object pk)
        {
            int index;
            if (_pkIndex != null && _pkIndex.TryGetValue(pk, out index))
            {
                return index;
            }
            return -1;
        }

        public ListData DeleteItems(IEnumerable<ListItemId> listItemIds)
        {
            var deletedRowIndexes = new HashSet<int>(listItemIds.Select(RowIndexOf));
            var newRowIndexes = Enumerable.Range(0, RowCount)
                .Where(i => !deletedRowIndexes.Contains(i)).ToArray();
            if (newRowIndexes.Length == RowCount)
            {
                return this;
            }
            return ChangeProp(ImClone(this), im =>
            {
                im.Columns = ImmutableList.ValueOf(im.Columns.Select(col => 
                    col.ReplaceValues(newRowIndexes.Select(col.GetValue))));
                im._ids = ImmutableList.ValueOf(newRowIndexes.Select(iRow=>_ids[iRow]));
                im.RebuildIndex();
            });
        }

        public ListData DeleteAllRows()
        {
            if (RowCount == 0)
            {
                return this;
            }
            return new ListData(ListDef, Columns.Select(column => column.SetRowCount(0)));
        }

        public ListData ChangeListDef(ListDef newListDef, IDictionary<string, string> originalColumnNames)
        {
            if (RowCount == 0 || originalColumnNames != null && originalColumnNames.Count == 0)
            {
                return new ListData(newListDef);
            }
            var newColumns = new List<ColumnData>();
            foreach (var annotationDef in newListDef.Properties)
            {
                string originalName;
                if (originalColumnNames == null)
                {
                    originalName = annotationDef.Name;
                }
                else
                {
                    if (!originalColumnNames.TryGetValue(annotationDef.Name, out originalName))
                    {
                        newColumns.Add(ColumnData.MakeColumnData(annotationDef).SetRowCount(RowCount));
                        continue;
                    }
                }
                int icolOrig = FindColumnIndex(originalName);
                var origColumn = Columns[icolOrig];
                var origAnnotationDef = ListDef.Properties[icolOrig];
                if (annotationDef.ValueType == origAnnotationDef.ValueType)
                {
                    newColumns.Add(origColumn);
                    continue;
                }
                var newColumn = ColumnData.MakeColumnData(annotationDef).SetRowCount(RowCount);
                for (int i = 0; i < RowCount; i++)
                {
                    var origValue = origColumn.GetValue(i);
                    object newValue;
                    if (!TryChangeType(origValue, annotationDef.ValueType, out newValue))
                    {
                        throw CommonException.Create(
                            ListExceptionDetail.InvalidValue(newListDef.Name, origAnnotationDef.Name, origValue));
                    }
                    if (newValue != null)
                    {
                        newColumn.SetValue(i, newValue);
                    }
                }
                newColumns.Add(newColumn);
            }
            return new ListData(newListDef, newColumns);
        }

        private static bool TryChangeType(object oldValue, Type targetType, out object newValue)
        {
            if (oldValue == null)
            {
                newValue = null;
                return true;
            }
            try
            {
                newValue = Convert.ChangeType(oldValue, targetType, CultureInfo.CurrentCulture);
                return true;
            }
            catch (Exception)
            {
                try
                {
                    newValue = Convert.ChangeType(oldValue, targetType, CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception)
                {
                    newValue = null;
                    if (Equals(oldValue, false) || Equals(oldValue, string.Empty) || Equals(oldValue, 0.0))
                    {
                        return true;
                    }
                    return false;
                }
            }
        }

        public string AuditLogText
        {
            get { return ListName; }
        }
        public bool IsName
        {
            get { return true; }
        }

        public Annotations GetRowAnnotations(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= RowCount)
            {
                throw new IndexOutOfRangeException();
            }

            var result = Annotations.EMPTY;
            for (int i = 0; i < Columns.Count; i++)
            {
                result = result.ChangeAnnotation(ListDef.Properties[i], Columns[i].GetValue(rowIndex));
            }

            return result;
        }
    }
}
