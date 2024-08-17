/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Results.Spectra;

namespace pwiz.Skyline.EditUI
{
    public partial class EditSpectrumFilterDlg : CommonFormEx
    {
        private List<Row> _rowList;
        private BindingList<Row> _rowBindingList;
        private FilterPages _originalFilterPages;
        private List<RadioButton> _pageRadioButtons = new List<RadioButton>();
        private ColumnDescriptor _rootColumn;
        private Dictionary<string, ColumnDescriptor> _propertyColumns = new Dictionary<string, ColumnDescriptor>();

        public EditSpectrumFilterDlg(ColumnDescriptor rootColumn, FilterPages filterPages)
        {
            InitializeComponent();
            _rootColumn = rootColumn;
            _originalFilterPages = FilterPages = filterPages;
            for (int iPage = 0; iPage < FilterPages.Pages.Count; iPage++)
            {
                _pageRadioButtons.Add(MakePageButton(iPage, FilterPages.Pages[iPage]));
            }
            panelPages.Controls.AddRange(_pageRadioButtons.ToArray());

            if (FilterPages.Pages.Count == 1)
            {
                panelPages.Visible = false;
            }
            _rowList = new List<Row>();
            _rowBindingList = new BindingList<Row>(_rowList);
            dataGridViewEx1.DataSource = _rowBindingList;
            operationColumn.Items.AddRange(FilterOperations.ListOperations().Select(op=>(object) op.DisplayName).ToArray());
            // Select the first non-empty page
            for (int i = 0; i < FilterPages.Clauses.Count; i++)
            {
                if (!FilterPages.Clauses[i].IsEmpty)
                {
                    CurrentPageIndex = i;
                    break;
                }
            }
            DisplayCurrentPage();
        }
        public FilterPages FilterPages { get; private set; }
        public int CurrentPageIndex { get; private set; }

        public FilterPage CurrentPage { get { return FilterPages.Pages[CurrentPageIndex]; } }

        public IFilterAutoComplete AutoComplete { get; set; }

        public string Description
        {
            get
            {
                return lblDescription.Text;
            }
            set
            {
                lblDescription.Text = value ?? string.Empty;
                lblDescription.Visible = !string.IsNullOrEmpty(lblDescription.Text);
            }
        }

        public class Row
        {
            public string Property { get; set; }
            public string Operation { get; set; }
            public string Value { get; set; }

            public void SetOperation(IFilterOperation filterOperation)
            {
                Operation = (filterOperation ?? FilterOperations.OP_HAS_ANY_VALUE).DisplayName;
            }

            public void SetValue(object value)
            {
                Value = value?.ToString() ?? string.Empty;
            }

            public void SetProperty(SpectrumClassColumn spectrumClassColumn)
            {
                Property = spectrumClassColumn.GetLocalizedColumnName(CultureInfo.CurrentCulture);
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private IEnumerable<Row> GetRows(FilterClause clause)
        {
            var rows = new List<Row>();
            var dataSchema = _rootColumn.DataSchema;
            foreach (var filterSpec in clause.FilterSpecs)
            {
                var propertyPath = filterSpec.ColumnId;
                var entry = _propertyColumns.FirstOrDefault(kvp => Equals(kvp.Value.PropertyPath, propertyPath));
                if (entry.Value == null)
                {
                    continue;
                }
                rows.Add(new Row
                {
                    Property = entry.Key,
                    Operation = filterSpec.Operation.DisplayName,
                    Value = filterSpec.Predicate.GetOperandDisplayText(dataSchema, entry.Value.PropertyType)
                });
            }

            return rows;
        }

        public void OkDialog()
        {
            if (!RememberFilterForCurrentPage())
            {
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private void dataGridViewEx1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            MessageDlg.ShowWithException(this, e.Exception.Message, e.Exception);
        }

        private void btnDeleteFilter_Click(object sender, EventArgs e)
        {
            var rowIndex = dataGridViewEx1.CurrentRow?.Index ?? -1;
            if (rowIndex >= 0 && rowIndex < _rowBindingList.Count)
            {
                _rowBindingList.RemoveAt(rowIndex);
            }
        }

        public bool CreateCopy
        {
            get { return cbCreateCopy.Checked; }
            set { cbCreateCopy.Checked = value; }
        }

        public bool CreateCopyEnabled
        {
            get
            {
                return cbCreateCopy.Enabled;
            }
            set
            {
                cbCreateCopy.Enabled = value;
            }
        }

        public bool CreateCopyVisible
        {
            get
            {
                return cbCreateCopy.Visible;
            }
            set
            {
                cbCreateCopy.Visible = value;
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            Reset();
        }

        public void Reset()
        {
            FilterPages = _originalFilterPages;
            DisplayCurrentPage();

        }

        public BindingList<Row> RowBindingList
        {
            get { return _rowBindingList; }
        }

        private void dataGridViewEx1_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            int columnIndex = dataGridViewEx1.CurrentCell.ColumnIndex;
            int rowIndex = dataGridViewEx1.CurrentCell.RowIndex;

            AutoCompleteStringCollection autoCompleteStringCollection = null;
            if (AutoComplete != null && columnIndex == valueColumn.Index && rowIndex >= 0 && rowIndex < _rowBindingList.Count)
            {
                var row = _rowBindingList[rowIndex];
                if (row.Property != null)
                {
                    _propertyColumns.TryGetValue(row.Property, out var propertyColumnDescriptor);
                    if (propertyColumnDescriptor != null)
                    {
                        autoCompleteStringCollection = AutoComplete.GetAutoCompleteValues(propertyColumnDescriptor.PropertyPath);
                    }
                }
            }
            TextBox textBox = e.Control as TextBox;
            if (textBox != null)
            {
                if (autoCompleteStringCollection == null)
                {
                    textBox.AutoCompleteMode = AutoCompleteMode.None;
                    textBox.AutoCompleteCustomSource = null;
                    textBox.AutoCompleteSource = AutoCompleteSource.None;
                }
                else
                {
                    textBox.AutoCompleteMode = AutoCompleteMode.Suggest;
                    textBox.AutoCompleteCustomSource = autoCompleteStringCollection;
                    textBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
                }
            }
        }

        private RadioButton MakePageButton(int index, FilterPage page)
        {
            var radioButton = new RadioButton
            {
                Text = page.Caption ?? string.Format(EditUIResources.EditSpectrumFilterDlg_MakePageButton_Case__0_, index + 1),
                AutoCheck = false
            };
            radioButton.Click += (sender, args)=>SelectPage(index);
            if (page.Description != null)
            {
                toolTip1.SetToolTip(radioButton, page.Description);
            }
            return radioButton;
        }

        public bool SelectPage(int pageIndex)
        {
            if (!RememberFilterForCurrentPage())
            {
                return false;
            }

            CurrentPageIndex = pageIndex;
            DisplayCurrentPage();
            return true;
        }

        private void DisplayCurrentPage()
        {
            var currentPage = FilterPages.Pages[CurrentPageIndex];
            propertyColumn.Items.Clear();
            _propertyColumns.Clear();
            foreach (var column in currentPage.AvailableColumns)
            {
                var columnDescriptor = GetColumnDescriptor(column);
                string caption = columnDescriptor.GetColumnCaption(ColumnCaptionType.localized);
                if (!_propertyColumns.ContainsKey(caption))
                {
                    _propertyColumns.Add(caption, columnDescriptor);
                    propertyColumn.Items.Add(caption);
                }
            }

            for (int i = 0; i < _pageRadioButtons.Count; i++)
            {
                _pageRadioButtons[i].Checked = i == CurrentPageIndex;
            }
            _rowList.Clear();
            _rowList.AddRange(GetRows(FilterPages.Clauses[CurrentPageIndex]));
            _rowBindingList.ResetBindings();
        }

        private bool RememberFilterForCurrentPage()
        {
            var currentFilter = GetFilterForCurrentPage();
            if (currentFilter == null)
            {
                return false;
            }

            FilterPages = FilterPages.ReplaceClause(CurrentPageIndex, currentFilter);
            return true;
        }

        public FilterClause GetFilterForCurrentPage()
        {
            var filterSpecs = new List<FilterSpec>();
            for (int iRow = 0; iRow < _rowList.Count; iRow++)
            {
                var row = _rowList[iRow];
                var filterOperation = FilterOperations.ListOperations()
                    .FirstOrDefault(op => op.DisplayName == row.Operation);
                if (filterOperation == null || filterOperation == FilterOperations.OP_HAS_ANY_VALUE)
                {
                    continue;
                }

                if (!_propertyColumns.TryGetValue(row.Property, out var propertyColumnDescriptor))
                {
                    continue;
                }
                FilterPredicate filterPredicate;
                try
                {
                    filterPredicate =
                        FilterPredicate.CreateFilterPredicate(_rootColumn.DataSchema, propertyColumnDescriptor.PropertyType, filterOperation,
                            row.Value);
                }
                catch (Exception ex)
                {
                    MessageDlg.ShowWithException(this, ex.Message, ex);
                    dataGridViewEx1.CurrentCell = dataGridViewEx1.Rows[iRow].Cells[valueColumn.Index];
                    return null;
                }

                var filterSpec = new FilterSpec(propertyColumnDescriptor.PropertyPath, filterPredicate);
                filterSpecs.Add(filterSpec);
            }
            return new FilterClause(filterSpecs);
        }

        private ColumnDescriptor GetColumnDescriptor(PropertyPath propertyPath)
        {
            if (propertyPath.IsRoot)
            {
                return _rootColumn;
            }

            var parent = GetColumnDescriptor(propertyPath.Parent);
            if (parent == null)
            {
                return null;
            }

            if (propertyPath.IsProperty)
            {
                return parent.ResolveChild(propertyPath.Name);
            }

            throw new ArgumentException(@"Invalid property path " + propertyPath);
        }
    }
}
