/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding.Controls
{
    public partial class PivotEditor : CommonFormEx
    {
        private BindingListSource _bindingListSource;
        private PivotSpec _pivotSpec = PivotSpec.EMPTY;
        private ImmutableList<DataPropertyDescriptor> _allProperties = ImmutableList<DataPropertyDescriptor>.EMPTY;
        private ImmutableList<DataPropertyDescriptor> _availableProperties = ImmutableList<DataPropertyDescriptor>.EMPTY;
        public const int MAX_COLUMN_COUNT = 2000;

        private bool _inUpdateUi;

        public PivotEditor(IViewContext viewContext)
        {
            InitializeComponent();
            ViewContext = viewContext;
            comboAggregateOp.Items.AddRange(AggregateOperation.ALL.ToArray());
            comboAggregateOp.SelectedIndex = 0;
            rowHeadersList.ColumnsMoved += RowHeadersOnColumnsMoved;
            columnHeadersList.ColumnsMoved += ColumnHeadersListOnColumnsMoved;
            valuesList.ColumnsMoved += ValuesListOnColumnsMoved;
        }

        public IViewContext ViewContext { get; private set; }

        private void ValuesListOnColumnsMoved(IList<int> ints)
        {
            PivotSpec = PivotSpec.ChangeValues(ints.Select(i => PivotSpec.Values[i]));
        }

        private void ColumnHeadersListOnColumnsMoved(IList<int> ints)
        {
            PivotSpec = PivotSpec.ChangeColumnHeaders(ints.Select(i => PivotSpec.ColumnHeaders[i]));
        }

        public DataSchema DataSchema { get { return ViewContext.DataSchema; } }
        public DataSchemaLocalizer DataSchemaLocalizer { get { return ViewContext.DataSchema.DataSchemaLocalizer; } }

        private void RowHeadersOnColumnsMoved(IList<int> ints)
        {
            PivotSpec = PivotSpec.ChangeRowHeaders(ints.Select(i => PivotSpec.RowHeaders[i]));
        }

        public PivotSpec PivotSpec
        {
            get { return _pivotSpec; }
            set
            {
                _pivotSpec = value;
                UpdateUi();
            }
        }

        public IList<DataPropertyDescriptor> AllProperties
        {
            get { return _allProperties; }

            set
            {
                _allProperties = ImmutableList.ValueOfOrEmpty(value);
                UpdateUi();
            }
        }

        public IList<DataPropertyDescriptor> AvailableProperties
        {
            get { return _availableProperties; }
        }

        public ListView AvailableColumnList { get { return availableColumnList; } }

        public void UpdateUi()
        {
            bool wasInUpdateUi = _inUpdateUi;
            try
            {
                _inUpdateUi = true;
                rowHeadersList.ReplaceItems(PivotSpec.RowHeaders.Select(col => MakeListItem(col, caption => caption)));
                columnHeadersList.ReplaceItems(PivotSpec.ColumnHeaders.Select(col => MakeListItem(col, caption=>caption)));
                valuesList.ReplaceItems(
                    PivotSpec.Values.Select(col => MakeListItem(col, col.AggregateOperation.QualifyColumnCaption)));
                _availableProperties = ImmutableList.ValueOf(_allProperties.Where(pd => PivotSpec.RowHeaders.Concat(PivotSpec.ColumnHeaders)
                    .All(col => !Equals(col.SourceColumn, ColumnId.GetColumnId(pd)))));
                availableColumnList.ReplaceItems(_availableProperties.Select(prop => new ListViewItem(prop.DisplayName)));
                UpdateButtons();
            }
            finally
            {
                _inUpdateUi = wasInUpdateUi;
            }
        }

        private ListViewItem MakeListItem(PivotSpec.Column column, Func<IColumnCaption, IColumnCaption> captionMaker)
        {
            var pd = FindPropertyDescriptor(column.SourceColumn);
            var listViewItem = new ListViewItem();
            if (column.Caption != null)
            {
                listViewItem.Text = column.Caption;
                listViewItem.Font = new Font(listViewItem.Font, FontStyle.Bold | listViewItem.Font.Style);
            }
            else if (pd != null)
            {
                listViewItem.Text = captionMaker(ColumnCaption.GetColumnCaption(pd)).GetCaption(DataSchemaLocalizer);
            }
            else
            {
                listViewItem.Text = captionMaker(ColumnCaption.UnlocalizableCaption(column.SourceColumn.ToString())).GetCaption(DataSchemaLocalizer);
            }
            if (pd == null)
            {
                listViewItem.Font = new Font(listViewItem.Font, FontStyle.Strikeout);
                listViewItem.ToolTipText = string.Format(Resources.PivotEditor_MakeListItem_Unable_to_find_column___0___,
                    column.SourceColumn);
            }
            return listViewItem;
        }

        private PropertyDescriptor FindPropertyDescriptor(ColumnId columnId)
        {
            return AllProperties.FirstOrDefault(pd => Equals(columnId, ColumnId.GetColumnId(pd)));
        }

        public void UpdateButtons()
        {
            if (availableColumnList.SelectedIndices.Count == 0)
            {
                btnAddColumnHeader.Enabled = btnAddRowHeader.Enabled = btnAddValue.Enabled = false;
            }
            else
            {
                btnAddColumnHeader.Enabled = btnAddRowHeader.Enabled = btnAddValue.Enabled = true;
                var currentAggregateOp = comboAggregateOp.SelectedItem as AggregateOperation;
                var selectedFieldTypes = availableColumnList.SelectedIndices.Cast<int>()
                    .Select(i => _availableProperties[i].PropertyType).Distinct().ToArray();
                var newOps = AggregateOperation.ALL.Where(op =>
                    selectedFieldTypes.All(type => op.IsValidForType(DataSchema, type))).ToArray();
                if (!newOps.SequenceEqual(comboAggregateOp.Items.OfType<AggregateOperation>()))
                {
                    comboAggregateOp.Items.Clear();
                    comboAggregateOp.Items.AddRange(newOps);
                    if (comboAggregateOp.Items.Count > 0)
                    {
                        int selectedIndex = Array.IndexOf(newOps, currentAggregateOp);
                        if (selectedIndex < 0)
                        {
                            selectedIndex = 0;
                        }
                        comboAggregateOp.SelectedIndex = selectedIndex;
                    }

                }
            }
        }

        public BindingListSource BindingListSource
        {
            get { return _bindingListSource; }
            set
            {
                if (ReferenceEquals(BindingListSource, value))
                {
                    return;
                }
                if (BindingListSource != null)
                {
                    BindingListSource.ListChanged -= BindingListSourceOnListChanged;
                }
                _bindingListSource = value;
                if (BindingListSource != null)
                {
                    BindingListSource.ListChanged += BindingListSourceOnListChanged;
                }
                UpdateBindingSource();
            }
        }

        private void BindingListSourceOnListChanged(object sender, ListChangedEventArgs listChangedEventArgs)
        {
            UpdateBindingSource();
        }

        private void UpdateBindingSource()
        {
            if (BindingListSource == null)
            {
                _allProperties = ImmutableList<DataPropertyDescriptor>.EMPTY;
            }
            else
            {
                _allProperties = BindingListSource.ItemProperties.AsImmutableList();
            }
            UpdateUi();
        }

        private void btnAddRowHeader_Click(object sender, EventArgs e)
        {
            AddRowHeader();
        }

        public void AddRowHeader()
        {
            var newColumns = GetSelectedProperties().Select(pd => new PivotSpec.Column(ColumnId.GetColumnId(pd)));
            PivotSpec = PivotSpec.ChangeRowHeaders(PivotSpec.RowHeaders.Concat(newColumns));
        }



        private IEnumerable<DataPropertyDescriptor> GetSelectedProperties()
        {
            return availableColumnList.SelectedIndices.OfType<int>().Select(index => _availableProperties[index]);
        }

        private void btnAddColumnHeader_Click(object sender, EventArgs e)
        {
            AddColumnHeader();
        }

        public void AddColumnHeader()
        {
            var newColumns = GetSelectedProperties()
                .Select(pd => new PivotSpec.Column(ColumnId.GetColumnId(pd)).ChangeVisible(false));
            PivotSpec = PivotSpec.ChangeColumnHeaders(PivotSpec.ColumnHeaders.Concat(newColumns));
        }

        private void btnAddValue_Click(object sender, EventArgs e)
        {
            AddValue();
        }

        public void AddValue()
        {
            var aggregateOperation = comboAggregateOp.SelectedItem as AggregateOperation;
            if (aggregateOperation == null)
            {
                return;
            }
            var newColumns = GetSelectedProperties()
                .Select(pd => new PivotSpec.AggregateColumn(ColumnId.GetColumnId(pd), aggregateOperation));
            PivotSpec = PivotSpec.ChangeValues(PivotSpec.Values.Concat(newColumns));
        }

        public void SelectAggregateOperation(AggregateOperation aggregateOperation)
        {
            comboAggregateOp.SelectedItem = aggregateOperation;
        }

        private void panelValueButtonsOuter_Resize(object sender, EventArgs e)
        {
            valueButtons.Top = (valueButtonPanel.Height - valueButtons.Height) / 2;
        }

        private void rowHeaderButtonPanel_Resize(object sender, EventArgs e)
        {
            btnAddRowHeader.Top = (rowHeaderButtonPanel.Height - btnAddRowHeader.Height) / 2;
        }

        private void columnHeaderButtonPanel_Resize(object sender, EventArgs e)
        {
            btnAddColumnHeader.Top = (columnHeaderButtonPanel.Height - btnAddColumnHeader.Height) / 2;
        }

        private void availableColumnList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inUpdateUi)
            {
                return;
            }
            UpdateButtons();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        public static void ShowPivotEditor(Control owner, BindingListSource bindingListSource, bool alwaysAddNewLevel)
        {
            var viewContext = bindingListSource.ViewContext;
            using (var groupingTotalForm = new PivotEditor(viewContext))
            {
                var transformResults = bindingListSource.BindingListView.QueryResults.TransformResults;
                var currentPivotSpec = alwaysAddNewLevel ? null : transformResults.RowTransform as PivotSpec;
                IList<DataPropertyDescriptor> allProperties;
                if (currentPivotSpec == null)
                {
                    allProperties = transformResults.PivotedRows.ItemProperties;
                    groupingTotalForm.PivotSpec = PivotSpec.EMPTY;
                }
                else
                {
                    groupingTotalForm.PivotSpec = currentPivotSpec;
                    allProperties = transformResults.Parent.PivotedRows.ItemProperties;
                }
                if (allProperties.Count > MAX_COLUMN_COUNT)
                {
                    string message = string.Format(Resources.PivotingForm_ShowPivotingForm_The_Pivot_Editor_cannot_be_shown_because_there_are_more_than__0__columns_,
                        MAX_COLUMN_COUNT);
                    viewContext.ShowMessageBox(owner, message, MessageBoxButtons.OK);
                    return;
                }
                groupingTotalForm.AllProperties = allProperties;
                if (groupingTotalForm.ShowDialog(owner) == DialogResult.OK)
                {
                    if (currentPivotSpec == null)
                    {
                        bindingListSource.BindingListView.TransformStack =
                            bindingListSource.BindingListView.TransformStack.PushTransform(groupingTotalForm.PivotSpec);
                    }
                    else
                    {
                        bindingListSource.BindingListView.TransformStack =
                            bindingListSource.BindingListView.TransformStack.Predecessor.PushTransform(groupingTotalForm.PivotSpec);
                    }
                }
            }
        }
    }
}

