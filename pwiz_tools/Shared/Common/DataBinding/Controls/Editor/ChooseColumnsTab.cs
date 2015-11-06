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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    public partial class ChooseColumnsTab : ViewEditorWidget
    {
        private bool _inLabelEdit;
        public ChooseColumnsTab()
        {
            InitializeComponent();
        }

        protected override void OnViewChange()
        {
            base.OnViewChange();
            _inLabelEdit = false;
            availableFieldsTreeColumns.RootColumn = ViewInfo.ParentColumn;
            availableFieldsTreeColumns.ShowAdvancedFields = ViewEditor.ShowHiddenFields;
            availableFieldsTreeColumns.SublistId = ViewInfo.SublistId;
            availableFieldsTreeColumns.CheckedColumns = ListColumnsInView();
            ListViewHelper.ReplaceItems(listViewColumns,
                VisibleColumns.Select(MakeListViewColumnItem).ToArray());
            if (null != SelectedPaths)
            {
                var selectedIndexes = VisibleColumns
                    .Select((col, index) => new KeyValuePair<DisplayColumn, int>(col, index))
                    .Where(kvp => SelectedPaths.Contains(kvp.Key.PropertyPath))
                    .Select(kvp => kvp.Value);
                ListViewHelper.SelectIndexes(listViewColumns, selectedIndexes);
            }
            UpdateButtons();
        }

        protected override bool UseTransformedView
        {
            get { return true; }
        }

        public int AdvancedPanelWidth { get; private set; }

        private IList<DisplayColumn> VisibleColumns
        {
            get
            {
                return ImmutableList.ValueOf(ViewInfo.DisplayColumns.Where(col => !col.ColumnSpec.Hidden));
            }
        }

        private IList<ColumnSpec> ColumnSpecs
        {
            get
            {
                return ImmutableList.ValueOf(VisibleColumns.Select(col => col.ColumnSpec));
            }
            set
            {
                if (ColumnSpecs.SequenceEqual(value))
                {
                    return;
                }
                ViewSpec = ViewSpec.SetColumns(value.Concat(ViewSpec.Columns.Where(col => col.Hidden)));
            }
        }

        protected override IEnumerable<PropertyPath> GetSelectedPaths()
        {
            var columns = VisibleColumns;
            return listViewColumns.SelectedIndices.Cast<int>().Select(index => columns[index].PropertyPath);
        }

        /// <summary>
        /// Returns the set of columns that should be checked in the Available Fields Tree.
        /// </summary>
        private IEnumerable<PropertyPath> ListColumnsInView()
        {
            return VisibleColumns.Select(dc => dc.PropertyPath);
        }
        private ListViewItem MakeListViewColumnItem(DisplayColumn displayColumn)
        {
            string listItemText = displayColumn.GetColumnCaption(null, ColumnCaptionType.localized);
            
            var listViewItem = new ListViewItem {Text = listItemText };
            Debug.Assert(!displayColumn.ColumnSpec.Hidden);
            if (!string.IsNullOrEmpty(displayColumn.ColumnSpec.Caption))
            {
                listViewItem.Font = new Font(listViewItem.Font, FontStyle.Bold | listViewItem.Font.Style);
                DataSchema dataSchema = displayColumn.DataSchema;
                ColumnCaption columnCaption = dataSchema.GetColumnCaption(displayColumn.ColumnDescriptor);
                listViewItem.ToolTipText = dataSchema.GetLocalizedColumnCaption(columnCaption);
            }
            return listViewItem;
        }
        private void AfterResizeListViewColumns()
        {
            listViewColumns.Columns[0].Width = listViewColumns.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 1;
        }
        private void UpdateButtons()
        {
            var selectedColumns = listViewColumns.SelectedIndices.Cast<int>().Select(i => ViewInfo.DisplayColumns[i]).ToArray();
            btnRemove.Enabled = selectedColumns.Length > 0;
            btnUp.Enabled = ListViewHelper.IsMoveUpEnabled(listViewColumns);
            btnDown.Enabled = ListViewHelper.IsMoveDownEnabled(listViewColumns);
            AfterResizeListViewColumns();
        }
        
        private void ListViewColumnsOnItemActivate(object sender, EventArgs e)
        {
            ActivatePropertyPath(VisibleColumns[listViewColumns.FocusedItem.Index].PropertyPath);
        }

        private void ListViewColumnsOnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (InChangeView)
            {
                return;
            }
            UpdateButtons();
        }

        private void MoveColumns(bool upwards)
        {
            var selectedIndexes = listViewColumns.SelectedIndices.Cast<int>().ToArray();
            var newIndexes = ListViewHelper.MoveItems(
                Enumerable.Range(0, listViewColumns.Items.Count), selectedIndexes, upwards);
            var newSelection = ListViewHelper.MoveSelectedIndexes(listViewColumns.Items.Count, selectedIndexes, upwards);
            var newColumns = newIndexes.Select(i => VisibleColumns[i].ColumnSpec).ToArray();
            ColumnSpecs = newColumns;
            ListViewHelper.SelectIndexes(listViewColumns, newSelection);
        }

        private void BtnUpOnClick(object sender, EventArgs e)
        {
            MoveColumns(true);
        }

        private void BtnDownOnClick(object sender, EventArgs e)
        {
            MoveColumns(false);
        }

        private void ListViewColumnsOnSizeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(AfterResizeListViewColumns));
            }
        }
        /// <summary>
        /// When the user double clicks in the tree, selects the column in the ListView.
        /// </summary>
        private void AvailableFieldsTreeColumnsOnNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var columnDescriptor = availableFieldsTreeColumns.GetValueColumn(e.Node);
            if (columnDescriptor == null)
            {
                return;
            }
            foreach (ListViewItem item in listViewColumns.Items)
            {
                var displayColumn = ViewInfo.DisplayColumns[item.Index];
                if (Equals(columnDescriptor.PropertyPath, displayColumn.PropertyPath) && Editor.ViewEditor.IsCanonical(displayColumn))
                {
                    item.Focused = true;
                    if (item.Selected && listViewColumns.SelectedIndices.Count == 1)
                    {
                        return;
                    }
                    listViewColumns.SelectedIndices.Clear();
                    item.Selected = true;
                    return;
                }
            }
        }

        public AvailableFieldsTree AvailableFieldsTree { get { return availableFieldsTreeColumns; } }

        private void AvailableFieldsTreeColumnsOnAfterCheck(object sender, TreeViewEventArgs e)
        {
            if (InChangeView)
            {
                return;
            }
            var treeNode = e.Node;
            var columnDescriptor = availableFieldsTreeColumns.GetValueColumn(treeNode);
            if (columnDescriptor == null)
            {
                return;
            }
            if (treeNode.Checked)
            {
                AddColumn(columnDescriptor.PropertyPath);
            }
            else
            {
                RemoveColumn(columnDescriptor.PropertyPath);
            }
        }
        public void AddColumn(PropertyPath propertyPath)
        {
            List<ColumnSpec> columnSpecs = new List<ColumnSpec>(VisibleColumns.Select(col=>col.ColumnSpec));
            var newColumn = new ColumnSpec(propertyPath);
            if (listViewColumns.SelectedIndices.Count == 0)
            {
                columnSpecs.Add(newColumn);
            }
            else
            {
                columnSpecs.Insert(listViewColumns.SelectedIndices.Cast<int>().Min(), newColumn);
            }
            ColumnSpecs = columnSpecs;
        }
        public void RemoveColumn(PropertyPath propertyPath)
        {
            int index = IndexOfCanconical(propertyPath);
            if (index < 0)
            {
                return;
            }
            var newColumns = ColumnSpecs.ToList();
            newColumns.RemoveAt(index);
            ColumnSpecs = newColumns;
        }
        public ColumnSpec[] GetSelectedColumns()
        {
            return listViewColumns.SelectedIndices.Cast<int>()
                .Select(index => ColumnSpecs[index]).ToArray();
        }
        private int IndexOfCanconical(PropertyPath propertyPath)
        {
            for (int i = 0; i < ViewInfo.DisplayColumns.Count; i++)
            {
                var displayColumn = ViewInfo.DisplayColumns[i];
                if (Equals(propertyPath, displayColumn.PropertyPath) && Editor.ViewEditor.IsCanonical(displayColumn))
                {
                    return i;
                }
            }
            return -1;
        }

        private void BtnRemoveOnClick(object sender, EventArgs e)
        {
            var newColumns = ColumnSpecs.Where((columnSpec, index) => !listViewColumns.Items[index].Selected).ToArray();
            if (newColumns.Length == ColumnSpecs.Count)
            {
                return;
            }
            int firstSelection = listViewColumns.SelectedIndices.Cast<int>().Min();
            ColumnSpecs = newColumns;
            listViewColumns.SelectedIndices.Clear();
            if (newColumns.Length > 0)
            {
                listViewColumns.SelectedIndices.Add(Math.Min(firstSelection, newColumns.Length - 1));
            }
        }

        private void listViewColumns_BeforeLabelEdit(object sender, LabelEditEventArgs e)
        {
            _inLabelEdit = true;
        }

        private void listViewColumns_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (!_inLabelEdit)
            {
                return;
            }
            if (null == e.Label)
            {
                _inLabelEdit = false;
                return;
            }
            var columns = ColumnSpecs.ToArray();
            if (string.IsNullOrEmpty(e.Label))
            {
                columns[e.Item] = columns[e.Item].SetCaption(null);
            }
            else
            {
                columns[e.Item] = columns[e.Item].SetCaption(e.Label);
            }
            ColumnSpecs = columns;
        }

        protected override void OnActivatePropertyPath(PropertyPath propertyPath)
        {
            availableFieldsTreeColumns.SelectColumn(propertyPath);
        }

        #region For Testing
        public bool TrySelect(PropertyPath propertyPath)
        {
            availableFieldsTreeColumns.SelectColumn(propertyPath);
            if (null == availableFieldsTreeColumns.SelectedNode)
            {
                return false;
            }
            var columnDescriptor = availableFieldsTreeColumns.GetTreeColumn(availableFieldsTreeColumns.SelectedNode);
            return null != columnDescriptor && Equals(propertyPath, columnDescriptor.PropertyPath);
        }

        public void ExpandPropertyPath(PropertyPath propertyPath, bool expand)
        {
            TreeNode treeNode = availableFieldsTreeColumns.FindTreeNode(propertyPath, true);
            if (expand)
            {
                treeNode.Expand();
            }
            else
            {
                treeNode.Collapse();
            }
        }

        public void AddSelectedColumn()
        {
            // Surprisingly, setting Checked = true, even when already checked
            // caused an automated test to add duplicate columns
            if (!availableFieldsTreeColumns.SelectedNode.Checked)
                availableFieldsTreeColumns.SelectedNode.Checked = true;
        }

        public int ColumnCount
        {
            get { return listViewColumns.Items.Count; }
        }

        public string[] ColumnNames
        {
            get
            {
                return listViewColumns.Items.Cast<ListViewItem>().Select(item => item.Text).ToArray();
            }
        }

        public void RemoveColumns(int startIndex, int endIndex)
        {
            for (int i = Math.Min(endIndex, ColumnCount) - 1; i >= Math.Max(0, startIndex); i--)
                RemoveColumn(VisibleColumns[i].PropertyPath);
        }

        public void ActivateColumn(int index)
        {
            ListViewHelper.SelectIndex(listViewColumns, index);
            ActivatePropertyPath(VisibleColumns[index].PropertyPath);
        }
        #endregion
    }
}
