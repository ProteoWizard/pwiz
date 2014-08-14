/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// A ViewContext with extra logic for remembering the width of columns that the user may have adjusted.
    /// </summary>
    public class ResultsGridViewContext : SkylineViewContext
    {
        public ResultsGridViewContext(SkylineDataSchema dataSchema, IEnumerable<RowSourceInfo> rowSources) : base(dataSchema, rowSources)
        {
        }

        /// <summary>
        /// Called just before the selection has changed in Skyline, and the LiveResultsGrid is going
        /// to be displaying a different view, this method saves the current widths of the columns.
        /// </summary>
        public void RememberColumnWidths(BoundDataGridView boundDataGridView)
        {
            var bindingListSource = boundDataGridView.DataSource as BindingListSource;
            if (null == bindingListSource || !bindingListSource.IsComplete)
            {
                return;
            }
            string gridColumnsKey = GetGridColumnsKey(bindingListSource.ViewInfo);
            if (null == gridColumnsKey)
            {
                return;
            }
            var itemProperties = bindingListSource.GetItemProperties(null);
            var gridColumnList = new List<GridColumn>();
            var propertyPaths = new HashSet<PropertyPath>();
            foreach (DataGridViewColumn column in boundDataGridView.Columns)
            {
                var columnPropertyDescriptor =
                    itemProperties.Find(column.DataPropertyName, false) as ColumnPropertyDescriptor;
                if (null == columnPropertyDescriptor)
                {
                    continue;
                }
                if (!propertyPaths.Add(columnPropertyDescriptor.PropertyPath))
                {
                    continue;
                }
                gridColumnList.Add(new GridColumn(GetPersistedColumnName(columnPropertyDescriptor.PropertyPath), true, column.Width));
            }
            var allGridColumnLists = Properties.Settings.Default.GridColumnsList;
            allGridColumnLists.Add(new GridColumns(gridColumnsKey, gridColumnList));
            Properties.Settings.Default.GridColumnsList = allGridColumnLists;
        }

        /// <summary>
        /// Overridden method which sets the width of the DataGridViewColumn if the width has been saved in user Settings.
        /// </summary>
        protected override TColumn InitializeColumn<TColumn>(TColumn dataGridViewColumn, PropertyDescriptor propertyDescriptor)
        {
            dataGridViewColumn = base.InitializeColumn(dataGridViewColumn, propertyDescriptor);
            var columnPropertyDescriptor = propertyDescriptor as ColumnPropertyDescriptor;
            if (null == columnPropertyDescriptor)
            {
                return dataGridViewColumn;
            }
            string key = GetGridColumnsKey(columnPropertyDescriptor.DisplayColumn.ViewInfo);
            if (null == key)
            {
                return dataGridViewColumn;
            }
            GridColumns gridColumns;
            if (!Properties.Settings.Default.GridColumnsList.TryGetValue(key, out gridColumns))
            {
                return dataGridViewColumn;
            }
            var columnName = GetPersistedColumnName(columnPropertyDescriptor.PropertyPath);
            var gridColumn = gridColumns.Columns.FirstOrDefault(column => Equals(columnName, column.Name));
            if (null != gridColumn)
            {
                dataGridViewColumn.Width = gridColumn.Width;
            }
            return dataGridViewColumn;
        }

        /// <summary>
        /// Returns the name to use in a <see cref="Properties.GridColumnsList" />.
        /// The name is constructed so as not to conflict with any name that the old Results Grid uses.
        /// </summary>
        private string GetGridColumnsKey(ViewInfo viewInfo)
        {
            if (null == viewInfo)
            {
                return null;
            }
            return "LiveReports" + viewInfo.ParentColumn.PropertyType.Name + "/" + viewInfo.Name; // Not L10N
        }

        /// <summary>
        /// Returns the string which will be used as the name of a <see cref="GridColumn"/>.
        /// </summary>
        private string GetPersistedColumnName(PropertyPath propertyPath)
        {
            return "Property" + propertyPath; // Not L10N
        }
    }
}
