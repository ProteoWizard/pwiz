/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Internal;

// This code is associated with the DocumentGrid.

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class DataboundGridControl: UserControl
    {
        private PropertyDescriptor _columnFilterPropertyDescriptor;

        public DataboundGridControl()
        {
            InitializeComponent();
        }

        public BindingListSource BindingListSource
        {
            get { return bindingListSource; }
            set
            {
                bindingListSource = value;
                NavBar.BindingListSource = bindingListSource;
                boundDataGridView.DataSource = bindingListSource;
            }
        }

        #region Methods exposed for testing
        public BoundDataGridViewEx DataGridView { get { return boundDataGridView; } }
        public NavBar NavBar { get { return navBar; } }

        public DataGridViewColumn FindColumn(PropertyPath propertyPath)
        {
            var propertyDescriptor =
                BindingListSource.GetItemProperties(null)
                    .OfType<ColumnPropertyDescriptor>()
                    .FirstOrDefault(colPd => Equals(propertyPath, colPd.PropertyPath));
            if (null == propertyDescriptor)
            {
                return null;
            }
            return DataGridView.Columns.Cast<DataGridViewColumn>().FirstOrDefault(col => col.DataPropertyName == propertyDescriptor.Name);
        }
        public bool IsComplete
        {
            get
            {
                return BindingListSource.IsComplete;
            }
        }

        public void ChooseView(string viewName)
        {
            var viewSpecs = BindingListSource.ViewContext.BuiltInViews.Concat(BindingListSource.ViewContext.CustomViews);
            var viewSpec = viewSpecs.First(view => view.Name == viewName);
            BindingListSource.SetViewSpec(viewSpec);
        }

        public int RowCount
        {
            get { return DataGridView.RowCount; }
        }

        public int ColumnCount
        {
            get { return DataGridView.ColumnCount; }
        }

        public string[] ColumnHeaderNames
        {
            get
            {
                return DataGridView.Columns.Cast<DataGridViewColumn>().Select(col => col.HeaderText).ToArray();
            }
        }

        public void ManageViews()
        {
            BindingListSource.ViewContext.ManageViews(NavBar);
        }

        public void QuickFilter(DataGridViewColumn column)
        {
            _columnFilterPropertyDescriptor = BindingListSource.GetItemProperties(null)[column.DataPropertyName];
            filterToolStripMenuItem_Click(filterToolStripMenuItem, new EventArgs());
        }

        #endregion

        protected virtual void boundDataGridView_CellContextMenuStripNeeded(object sender, DataGridViewCellContextMenuStripNeededEventArgs e)
        {
            PropertyDescriptor propertyDescriptor = null;
            if (e.ColumnIndex >= 0)
            {
                var column = boundDataGridView.Columns[e.ColumnIndex];
                propertyDescriptor = GetPropertyDescriptor(column);
            }
            e.ContextMenuStrip = contextMenuStrip;
            clearAllFiltersToolStripMenuItem.Enabled = !BindingListSource.RowFilter.IsEmpty;
            _columnFilterPropertyDescriptor = propertyDescriptor;
            if (null != _columnFilterPropertyDescriptor)
            {

                clearFilterToolStripMenuItem.Enabled =
                    BindingListSource.RowFilter.ColumnFilters.Any(
                        filter => Equals(_columnFilterPropertyDescriptor.DisplayName, filter.ColumnCaption));
                filterToolStripMenuItem.Enabled = true;
                ListSortDirection? sortDirection = null;
                if (null != BindingListSource.SortDescriptions && BindingListSource.SortDescriptions.Count > 0)
                {
                    var sortDescription = BindingListSource.SortDescriptions.OfType<ListSortDescription>().First();
                    if (sortDescription.PropertyDescriptor.Name == _columnFilterPropertyDescriptor.Name)
                    {
                        sortDirection = sortDescription.SortDirection;
                    }
                    clearSortToolStripMenuItem.Enabled = true;
                }
                else
                {
                    clearSortToolStripMenuItem.Enabled = false;
                }
                sortAscendingToolStripMenuItem.Enabled = true;
                sortDescendingToolStripMenuItem.Enabled = true;
                sortAscendingToolStripMenuItem.Checked = ListSortDirection.Ascending == sortDirection;
                sortDescendingToolStripMenuItem.Checked = ListSortDirection.Descending == sortDirection;
            }
            else
            {
                clearFilterToolStripMenuItem.Enabled = false;
                filterToolStripMenuItem.Enabled = false;
                sortAscendingToolStripMenuItem.Enabled = false;
                sortDescendingToolStripMenuItem.Enabled = false;
                sortAscendingToolStripMenuItem.Checked = false;
                sortDescendingToolStripMenuItem.Checked = false;
            }
        }

        private void clearAllFiltersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BindingListSource.RowFilter = RowFilter.Empty;
        }

        private void clearFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null != _columnFilterPropertyDescriptor)
            {
                var rowFilter = BindingListSource.RowFilter;
                rowFilter = rowFilter.SetColumnFilters(
                    rowFilter.ColumnFilters.Where(spec => !Equals(spec.ColumnCaption, _columnFilterPropertyDescriptor.DisplayName)));
                BindingListSource.RowFilter = rowFilter;
            }
        }

        private void filterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var quickFilterForm = new QuickFilterForm())
            {
                quickFilterForm.SetFilter(BindingListSource.ViewInfo.DataSchema, _columnFilterPropertyDescriptor, BindingListSource.RowFilter);
                if (quickFilterForm.ShowDialog(this) == DialogResult.OK)
                {
                    BindingListSource.RowFilter = quickFilterForm.RowFilter;
                }
            }
        }

        private void sortAscendingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSortDirection(_columnFilterPropertyDescriptor, ListSortDirection.Ascending);
        }

        private void sortDescendingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSortDirection(_columnFilterPropertyDescriptor, ListSortDirection.Descending);
        }

        public void SetSortDirection(PropertyDescriptor propertyDescriptor, ListSortDirection direction)
        {
            if (null == propertyDescriptor)
            {
                return;
            }
            List<ListSortDescription> sortDescriptions = new List<ListSortDescription>();
            sortDescriptions.Add(new ListSortDescription(propertyDescriptor, direction));
            if (null != BindingListSource.SortDescriptions)
            {
                sortDescriptions.AddRange(
                    BindingListSource.SortDescriptions.OfType<ListSortDescription>()
                        .Where(sortDescription => sortDescription.PropertyDescriptor.Name != propertyDescriptor.Name));
            }
            BindingListSource.ApplySort(new ListSortDescriptionCollection(sortDescriptions.ToArray()));
        }

        private void clearSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BindingListSource.ApplySort(new ListSortDescriptionCollection());
        }

        /// <summary>
        /// Displays the context menu if the user left-clicks on a column header.
        /// </summary>
        private void boundDataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0)
            {
                return;
            }
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            var args = new DataGridViewCellContextMenuStripNeededEventArgs(e.ColumnIndex, e.RowIndex);
            boundDataGridView_CellContextMenuStripNeeded(sender, args);
            if (null != args.ContextMenuStrip)
            {
                var rcCell = boundDataGridView.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                args.ContextMenuStrip.Show(boundDataGridView, new Point(rcCell.X + e.X, rcCell.Y + e.Y));
            }
        }

        public PropertyDescriptor GetPropertyDescriptor(DataGridViewColumn column)
        {
            return bindingListSource.GetItemProperties(null)[column.DataPropertyName];
        }
    }
}
