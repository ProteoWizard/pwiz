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
using System.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// An instantiation of a <see cref="ViewSpec" />.
    /// Whereas a ViewSpec identifies columns simply by a string, a ViewInfo
    /// has retrieved the relevant PropertyDescriptors from the DataSchema.
    /// </summary>
    public class ViewInfo : Immutable
    {
        private readonly IDictionary<PropertyPath, ColumnDescriptor> _columnDescriptors = new Dictionary<PropertyPath, ColumnDescriptor>();
        
        public ViewInfo(DataSchema dataSchema, Type rootType, ViewSpec viewSpec) : this(ColumnDescriptor.RootColumn(dataSchema, rootType), viewSpec)
        {
        }

        public ViewInfo(ColumnDescriptor parentColumn, ViewSpec viewSpec)
        {
            ParentColumn = parentColumn;
            DataSchema = parentColumn.DataSchema;
            Name = viewSpec.Name;
            RowSourceName = viewSpec.RowSource;

            _columnDescriptors.Add(parentColumn.PropertyPath, parentColumn);
            var displayColumns = new List<DisplayColumn>();
            foreach (var column in viewSpec.Columns)
            {
                var columnDescriptor = GetColumnDescriptor(column.PropertyPath);
                displayColumns.Add(new DisplayColumn(this, column, columnDescriptor));
            }
            DisplayColumns = Array.AsReadOnly(displayColumns.ToArray());
            SublistId = viewSpec.SublistId;
            var filters = new List<FilterInfo>();
            foreach (var filterSpec in viewSpec.Filters)
            {
                var columnDescriptor = GetColumnDescriptor(filterSpec.ColumnId);
                ColumnDescriptor collectionColumn = null;
                if (columnDescriptor != null)
                {
                    collectionColumn = columnDescriptor.CollectionAncestor() ?? ParentColumn;
                }
                filters.Add(new FilterInfo(filterSpec, columnDescriptor, collectionColumn));
            }
            
            Filters = Array.AsReadOnly(filters.ToArray());
            var sorts = new List<KeyValuePair<int, DisplayColumn>>();
            for (int i = 0; i < DisplayColumns.Count; i++)
            {
                if (DisplayColumns[i].ColumnSpec.SortDirection != null)
                {
                    sorts.Add(new KeyValuePair<int, DisplayColumn>(i, DisplayColumns[i]));
                }
            }
            if (sorts.Count > 0)
            {
                sorts.Sort(CompareSortEntries);
                SortColumns = Array.AsReadOnly(sorts.Select(kvp => kvp.Value).ToArray());
            }
            else
            {
                SortColumns = new DisplayColumn[0];
            }
        }
        public ViewSpec GetViewSpec()
        {
            return new ViewSpec()
                .SetName(Name)
                .SetRowSource(RowSourceName)
                .SetSublistId(SublistId)
                .SetColumns(DisplayColumns.Select(dc=>dc.ColumnSpec))
                .SetFilters(Filters.Select(filterInfo=>filterInfo.FilterSpec));
        }

        public ViewSpec ViewSpec
        {
            get
            {
                return GetViewSpec();
            }
        }

        public ViewGroup ViewGroup { get; private set; }

        public ViewInfo ChangeViewGroup(ViewGroup viewGroup)
        {
            return ChangeProp(ImClone(this), im => im.ViewGroup = viewGroup);
        }

        public bool HasTotals
        {
            get
            {
                return DisplayColumns.Any(col => TotalOperation.PivotValue == col.ColumnSpec.Total);
            }
        }

        public DataSchema DataSchema { get; private set; }
        public ColumnDescriptor ParentColumn { get; private set; }
        public string Name { get; private set; }
        public string RowSourceName { get; private set; }
        public PropertyPath SublistId { get; private set; }
        public IList<DisplayColumn> DisplayColumns { get; private set; }
        public IList<DisplayColumn> SortColumns { get; private set; }
        public IList<FilterInfo> Filters { get; private set; }
        public IEnumerable<ColumnDescriptor> AllColumnDescriptors { get { return _columnDescriptors.Values.ToArray(); } }
        public ICollection<ColumnDescriptor> GetCollectionColumns()
        {
            var unboundColumnSet = new HashSet<ColumnDescriptor> {ParentColumn};
            var allColumnDescriptors = DisplayColumns.Select(displayColumn => displayColumn.ColumnDescriptor)
                .Concat(Filters.Select(filter => filter.ColumnDescriptor))
                .Where(columnDescriptor=>null != columnDescriptor);
            foreach (var columnDescriptor in allColumnDescriptors)
            {
                for (var unboundParent = columnDescriptor.CollectionAncestor(); unboundParent != null; unboundParent = unboundParent.Parent.CollectionAncestor())
                {
                    unboundColumnSet.Add(unboundParent);
                }
            }
            return unboundColumnSet;
        }
        private ColumnDescriptor GetColumnDescriptor(PropertyPath idPath)
        {
            ColumnDescriptor columnDescriptor;
            if (_columnDescriptors.TryGetValue(idPath, out columnDescriptor))
            {
                return columnDescriptor;
            }
            var parent = GetColumnDescriptor(idPath.Parent);
            if (parent == null)
            {
                return null;
            }
            if (idPath.Name != null)
            {
                columnDescriptor = parent.ResolveChild(idPath.Name);
            }
            else
            {
                columnDescriptor = parent.GetCollectionColumn();
                if (columnDescriptor == null)
                {
                    return null;
                }
            }
            _columnDescriptors.Add(idPath, columnDescriptor);
            return columnDescriptor;
        }

        private int CompareSortEntries(KeyValuePair<int, DisplayColumn> kvp1, KeyValuePair<int, DisplayColumn> kvp2)
        {
            var sortIndex1 = kvp1.Value.ColumnSpec.SortIndex;
            var sortIndex2 = kvp2.Value.ColumnSpec.SortIndex;
            if (sortIndex1.HasValue && sortIndex2.HasValue)
            {
                return sortIndex1.Value.CompareTo(sortIndex2.Value);
            }
            return kvp1.Key.CompareTo(kvp2.Key);
        }
    }
}
