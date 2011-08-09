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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding.Controls
{
    public partial class CustomizeViewForm : Form
    {
        private ViewSpec _viewSpec = new ViewSpec();
        private Font _strikeThroughFont;
        private bool _inChangeView;
        public CustomizeViewForm(IViewContext viewContext, ViewSpec viewSpec)
        {
            InitializeComponent();
            ViewContext = viewContext;
            ParentColumn = viewContext.ParentColumn;
            _strikeThroughFont = new Font(listViewColumns.Font, FontStyle.Strikeout);
            availableFieldsTreeColumns.RootColumn = ParentColumn;
            ViewSpec = OriginalViewSpec = viewSpec;
            tbxViewName.Text = viewSpec.Name;
            ExistingCustomViewSpec =
                viewContext.CustomViewSpecs.FirstOrDefault(customViewSpec => viewSpec.Name == customViewSpec.Name);
        }

        public ColumnDescriptor ParentColumn { get; private set; }
        public IViewContext ViewContext { get; private set; }
        public ViewSpec OriginalViewSpec { get; private set; }
        public ViewSpec ExistingCustomViewSpec { get; private set; }
        public ViewSpec ViewSpec { 
            get
            {
                return _viewSpec;
            }
            
            private set
            {
                if (_inChangeView)
                {
                    return;
                }
                try
                {
                    _inChangeView = true;
                    if (Equals(_viewSpec, value))
                    {
                        return;
                    }
                    var columnListHelper = GetColumnListHelper();
                    _viewSpec = value;
                    availableFieldsTreeColumns.CheckedColumns = ViewSpec.Columns.Select(c => c.IdentifierPath);
                    columnListHelper.Items = ViewSpec.Columns;
                    UpdatePropertySheet();
                }
                finally
                {
                    _inChangeView = false;
                }
            }
        }

        private void availableFieldsTreeColumns_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            var treeNode = e.Node;
            var columnDescriptor = availableFieldsTreeColumns.GetColumnDescriptor(treeNode);
            if (columnDescriptor == null)
            {
                return;
            }
            if (treeNode.Checked)
            {
                AddColumns(new[]{columnDescriptor});
            }
            else
            {
                RemoveColumns(new[]{columnDescriptor.IdPath});
            }
        }
        private void AddColumns(IEnumerable<ColumnDescriptor> columnDescriptors)
        {
            List<ColumnSpec> columnSpecs = new List<ColumnSpec>(ViewSpec.Columns);
            var addedIds = new List<IdentifierPath>();
            IdentifierPath lastId = null;
            int focusedIndex = listViewColumns.FocusedItem == null ? -1 : listViewColumns.FocusedItem.Index;
            
            foreach (var columnDescriptor in columnDescriptors)
            {
                var idPath = columnDescriptor.IdPath;
                var existingColumnIndex = columnSpecs
                    .FindIndex(c => Equals(idPath, c.IdentifierPath));
                lastId = idPath;
                if (existingColumnIndex >= 0)
                {
                    continue;
                }
                var columnSpec = columnDescriptor.GetColumnSpec();
                columnSpec = columnSpec.SetVisible(true);
                columnSpecs.Insert(focusedIndex + 1, columnSpec);
                focusedIndex++;
                addedIds.Add(idPath);
            }
            if (addedIds.Count > 0)
            {
                ViewSpec = ViewSpec.SetColumns(columnSpecs);
            }
            var helper = GetColumnListHelper();
            if (addedIds.Count > 0)
            {
                helper.SelectKeys(addedIds);
            }
            else
            {
                helper.SelectKey(lastId);
            }
        }
        private void RemoveColumns(IEnumerable<IdentifierPath> identifierPaths)
        {
            var hashSet = new HashSet<IdentifierPath>(identifierPaths);
            var newItems = ViewSpec.Columns.Where(columnSpec => !hashSet.Contains(columnSpec.IdentifierPath)).ToArray();
            if (newItems.Count() == ViewSpec.Columns.Count)
            {
                return;
            }
            ViewSpec = ViewSpec.SetColumns(newItems);
        }
        private ListViewItem MakeListViewItem(ColumnSpec columnSpec)
        {
            var listViewItem = new ListViewItem(columnSpec.Caption ?? columnSpec.IdentifierPath.Name);
            var columnDescriptor = ParentColumn.ResolveDescendant(columnSpec.IdentifierPath);
            if (columnDescriptor == null)
            {
                listViewItem.Font = _strikeThroughFont;
            }
            if (!columnSpec.Visible)
            {
                listViewItem.ForeColor = Color.LightGray;
            }
            return listViewItem;
        }

        public ColumnSpec[] GetSelectedColumns()
        {
            return listViewColumns.SelectedIndices.Cast<int>()
                .Select(index => ViewSpec.Columns[index]).ToArray();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel || DialogResult == DialogResult.Cancel)
            {
                return;
            }
            ValidateViewName(e);
        }

        protected void ValidateViewName(FormClosingEventArgs formClosingEventArgs)
        {
            if (formClosingEventArgs.Cancel)
            {
                return;
            }
            ViewSpec = ViewSpec.SetName(tbxViewName.Text);
            var name = ViewSpec.Name;
            string errorMessage = null;
            if (string.IsNullOrEmpty(name))
            {
                errorMessage = "View name cannot be blank.";
            }
            else if (ViewContext.BuiltInViewSpecs.FirstOrDefault(viewSpec=>name==viewSpec.Name) != null)
            {
                errorMessage = string.Format("There is already a built in view named '{0}'.", name);
            }
            else
            {
                var currentExistingView = ViewContext.CustomViewSpecs.FirstOrDefault(viewSpec => name == viewSpec.Name);
                if (currentExistingView != null && name != OriginalViewSpec.Name)
                {
                    if (ViewContext.ShowMessageBox(this, string.Format("Do you want to overwrite the existing view named '{0}'", name), MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    {
                        formClosingEventArgs.Cancel = true;
                    }
                }
            }
            if (errorMessage != null)
            {
                ViewContext.ShowMessageBox(this, errorMessage, MessageBoxButtons.OK);
                formClosingEventArgs.Cancel = true;
            }
            if (formClosingEventArgs.Cancel)
            {
                tbxViewName.Focus();
            }
        }

        private void UpdatePropertySheet()
        {
            var helper = GetColumnListHelper();
            var selectedColumns = helper.GetSelectedItems();
            if (selectedColumns.Length == 1)
            {
                var selectedColumn = selectedColumns[0];
                lblCaption.Visible = tbxCaption.Visible = true;
                tbxCaption.Text = selectedColumn.Caption;
                availableFieldsTreeColumns.SelectColumn(selectedColumn.IdentifierPath);
            }
            else
            {
                lblCaption.Visible = tbxCaption.Visible = false;
            }
            if (selectedColumns.Length == 0)
            {
                cbxVisible.Visible = false;
            }
            else
            {
                cbxVisible.Visible = true;
                cbxVisible.CheckState = selectedColumns.All(columnSpec => columnSpec.Visible) ? CheckState.Checked
                    : selectedColumns.Any(columnSpec => columnSpec.Visible) ? CheckState.Indeterminate 
                    : CheckState.Unchecked;
            }
            btnRemove.Enabled = selectedColumns.Length > 0;
            btnUp.Enabled = helper.IsMoveUpEnabled();
            btnDown.Enabled = helper.IsMoveDownEnabled();
        }

        private ListViewHelper<IdentifierPath, ColumnSpec> GetColumnListHelper()
        {
            return new ListViewHelper<IdentifierPath, ColumnSpec>(
                listViewColumns, ViewSpec.Columns, 
                columnSpec => columnSpec.IdentifierPath, MakeListViewItem);
        }

        private void btnAddColumn_Click(object sender, EventArgs e)
        {
            AddColumns(availableFieldsTreeColumns.CheckedColumns
                .Select(id=>ParentColumn.ResolveDescendant(id)));
        }

        private void cbxVisible_CheckedChanged(object sender, EventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            var newVisible = cbxVisible.Checked;
            var newColumns = ViewSpec.Columns.Select((columnSpec, index)
                =>listViewColumns.Items[index].Selected 
                ? columnSpec.SetVisible(newVisible)
                : columnSpec);
            ViewSpec = ViewSpec.SetColumns(newColumns);
        }

        private void tbxCaption_Leave(object sender, EventArgs e)
        {
            if (_inChangeView)
            {
                return;
            }
            var newCaption = tbxCaption.Text;
            if (string.IsNullOrEmpty(newCaption))
            {
                newCaption = null;
            }
            var newColumns = ViewSpec.Columns.Select((columnSpec, index)
                => listViewColumns.Items[index].Selected
                ? columnSpec.SetCaption(newCaption)
                : columnSpec);
            ViewSpec = ViewSpec.SetColumns(newColumns);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            ViewSpec = ViewSpec.SetColumns(
                ViewSpec.Columns.Where((columnSpec, index) => !listViewColumns.Items[index].Selected));
        }

        private void listViewColumns_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePropertySheet();
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            var helper = GetColumnListHelper();
            ViewSpec = ViewSpec.SetColumns(helper.MoveItemsUp());
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            var helper = GetColumnListHelper();
            ViewSpec = ViewSpec.SetColumns(helper.MoveItemsDown());
        }

        private void listViewColumns_SizeChanged(object sender, EventArgs e)
        {
            listViewColumns.Columns[0].Width = listViewColumns.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
        }
    }
}
