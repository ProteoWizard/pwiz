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
using System.Drawing;
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
        private FilterPages _filterPages;
        private FilterPages _originalFilterPages;
        private ColumnDescriptor _rootColumn;
        private Dictionary<string, FilterColumn> _propertyColumns = new Dictionary<string, FilterColumn>();
        /// <summary>
        /// All that can be asked of a column whose term carries no value: whether the spectrum has it.
        ///
        /// Has Any Value is deliberately absent, though it is offered everywhere else. It means "no
        /// criterion" - it matches every row and is dropped when the clause is built - but beside these
        /// two it reads as the presence test they actually perform, and would silently filter nothing.
        /// The name is doubly wrong here: a flag that is present carries an empty value, so "has any
        /// value" is untrue of every spectrum, present or absent. Leaving the operation blank still means
        /// no criterion, and the row delete button still removes one.
        /// </summary>
        private static readonly IList<IFilterOperation> VALUELESS_COLUMN_OPERATIONS = new[]
        {
            FilterOperations.OP_IS_BLANK, FilterOperations.OP_IS_NOT_BLANK
        };

        private IList<SpectrumClassColumn> _extraColumns;
        private SpectrumColumnScanner.Availability _columnAvailability = SpectrumColumnScanner.Availability.UNKNOWN;
        private Font _unanswerableFont;
        private bool _updating;

        public EditSpectrumFilterDlg(ColumnDescriptor rootColumn, FilterPages filterPages)
            : this(rootColumn, filterPages, null)
        {
        }

        /// <summary>
        /// <paramref name="extraColumns"/> are dynamic columns (the discovered mzML CV/user parameters)
        /// that are not properties of the databound row type, so they cannot be resolved from
        /// <paramref name="rootColumn"/>; the dialog offers them alongside the resolvable columns.
        /// </summary>
        public EditSpectrumFilterDlg(ColumnDescriptor rootColumn, FilterPages filterPages,
            IEnumerable<SpectrumClassColumn> extraColumns)
        {
            InitializeComponent();
            // The property column holds friendly CV term names (e.g. "base peak intensity (MS:1000505)"),
            // which are longer than the operand values, so give it the larger share of the width.
            propertyColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            propertyColumn.FillWeight = 2;
            valueColumn.FillWeight = 1;
            _extraColumns = extraColumns?.ToArray() ?? Array.Empty<SpectrumClassColumn>();
            _rootColumn = rootColumn;
            _rowList = new List<Row>();
            _rowBindingList = new BindingList<Row>(_rowList);
            dataGridViewEx1.DataSource = _rowBindingList;
            operationColumn.Items.AddRange(FilterOperations.ListOperations().Select(op => (object)op.DisplayName).ToArray());
            FilterPages = _originalFilterPages = filterPages;
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
        public FilterPages FilterPages
        {
            get
            {
                return _filterPages;
            }
            set
            {
                _updating = true;
                try
                {
                    _filterPages = value;
                    var newTabNames = GetFilterTabNames(FilterPages);
                    for (int i = 0; i < newTabNames.Count; i++)
                    {
                        if (tabClauses.TabCount <= i)
                        {
                            tabClauses.TabPages.Add(new TabPage());
                        }
                        var tabPage = tabClauses.TabPages[i];
                        tabPage.Text = newTabNames[i];
                    }

                    while (tabClauses.TabCount > newTabNames.Count)
                    {
                        tabClauses.TabPages.RemoveAt(tabClauses.TabCount - 1);
                    }
                }
                finally
                {
                    _updating = false;
                }
            }
        }

        private IList<string> GetFilterTabNames(FilterPages filterPages)
        {
            if (filterPages.Pages.Count == 0)
            {
                return new[] { EditUIResources.EditSpectrumFilterDlg_GetFilterTabNames_Criteria };
            }

            bool lastPageEmpty = filterPages.Pages[filterPages.Pages.Count - 1].Discriminant.IsEmpty && filterPages
                .Clauses[filterPages.Clauses.Count - 1].FilterSpecs
                .All(spec => spec.Operation == FilterOperations.OP_HAS_ANY_VALUE);

            List<string> tabNames = new List<string>();
            int unnamedTabCount = 0;
            for (int i = 0; i < filterPages.Pages.Count; i++)
            {
                var caption = filterPages.Pages[i].Caption;
                if (string.IsNullOrEmpty(caption))
                {
                    unnamedTabCount++;
                    if (filterPages.Pages.Count == 1)
                    {
                        caption = EditUIResources.EditSpectrumFilterDlg_GetFilterTabNames_Criteria;
                    }
                    else
                    {
                        caption = string.Format(EditUIResources.EditSpectrumFilterDlg_GetFilterTabNames_Criteria__0_, unnamedTabCount);
                    }
                }

                tabNames.Add(caption);
            }

            if (!lastPageEmpty)
            {
                tabNames.Add(EditUIResources.EditSpectrumFilterDlg_GetFilterTabNames___Add_Alternative);
            }

            return tabNames;
        }

        public int CurrentPageIndex { get; private set; }

        // CurrentPageIndex can be Pages.Count (the "+ Add Alternative" tab) or Pages can be empty,
        // so guard the index (mirrors DisplayCurrentPage) rather than throwing ArgumentOutOfRangeException.
        public FilterPage CurrentPage
        {
            get { return FilterPages.Pages.ElementAtOrDefault(CurrentPageIndex) ?? SpectrumClassFilter.GenericFilterPage; }
        }

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

        /// <summary>
        /// A filterable column the dialog offers, reduced to what the dialog needs (caption, path, type).
        /// Backed either by a <see cref="ColumnDescriptor"/> (a property of the databound row type) or by
        /// a dynamic <see cref="SpectrumClassColumn"/> (a discovered mzML CV/user parameter, which has no
        /// such property).
        /// </summary>
        private class FilterColumn
        {
            public FilterColumn(PropertyPath propertyPath, Type propertyType, string caption,
                SpectrumClassColumn spectrumColumn = null)
            {
                PropertyPath = propertyPath;
                PropertyType = propertyType;
                Caption = caption;
                SpectrumColumn = spectrumColumn;
            }

            public PropertyPath PropertyPath { get; }
            public Type PropertyType { get; }
            public string Caption { get; }

            // The dynamic CV/user-parameter column this represents, or null for an ordinary databound
            // property. A CV column's operand type depends on the operator rather than the column's
            // discovered ValueType (see GetOperandType).
            public SpectrumClassColumn SpectrumColumn { get; }

            public static FilterColumn FromColumnDescriptor(ColumnDescriptor columnDescriptor)
            {
                return new FilterColumn(columnDescriptor.PropertyPath, columnDescriptor.PropertyType,
                    columnDescriptor.GetColumnCaption(ColumnCaptionType.localized));
            }

            public static FilterColumn FromSpectrumClassColumn(SpectrumClassColumn column)
            {
                return new FilterColumn(column.PropertyPath, column.ValueType,
                    column.GetLocalizedColumnName(CultureInfo.CurrentCulture), column);
            }
        }

        /// <summary>
        /// Explains the property list's styling. The three states are conveyed by appearance alone,
        /// with nowhere on the form to say what they mean, so this is where the rule is written down.
        /// </summary>
        private void btnHelp_Click(object sender, EventArgs e)
        {
            ShowStylingHelp();
        }

        /// <summary>
        /// Public so a test can open the help the way the button does, rather than reaching for the button.
        /// </summary>
        public void ShowStylingHelp()
        {
            using var dlg = new SpectrumFilterStylingHelpDlg();
            dlg.ShowDialog(this);
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
                var filterColumn = ResolveFilterColumn(filterSpec.ColumnId);
                if (filterColumn == null)
                {
                    continue;
                }
                rows.Add(new Row
                {
                    Property = filterColumn.Caption,
                    Operation = filterSpec.Operation.DisplayName,
                    Value = filterSpec.Predicate.GetOperandDisplayText(dataSchema,
                        GetOperandType(filterColumn, filterSpec.Operation))
                });
            }

            return rows;
        }

        /// <summary>
        /// Finds the offered column for a saved filter spec's path. If the path is a CV/user-parameter
        /// column the editor does not currently offer (a userParam absent from the loaded data, or a term
        /// excluded from the catalog), it is reconstructed from its encoded path and registered, so the
        /// criterion is shown and preserved rather than silently dropped when the dialog is confirmed.
        /// </summary>
        private FilterColumn ResolveFilterColumn(PropertyPath propertyPath)
        {
            var filterColumn = _propertyColumns.Values.FirstOrDefault(fc => Equals(fc.PropertyPath, propertyPath));
            if (filterColumn != null)
            {
                return filterColumn;
            }
            var spectrumColumn = SpectrumClassColumn.FindColumn(propertyPath);
            if (spectrumColumn == null || !SpectrumClassColumn.IsCvParamColumn(spectrumColumn))
            {
                return null;
            }
            AddFilterColumn(FilterColumn.FromSpectrumClassColumn(spectrumColumn));
            return _propertyColumns.Values.FirstOrDefault(fc => Equals(fc.PropertyPath, propertyPath));
        }

        /// <summary>
        /// The type used to parse and display a column's operand. For a CV/user-parameter column the operand
        /// type depends on the operator (matching how chromatogram extraction evaluates it), not on the
        /// column's discovered ValueType - so a "contains" operand is text even on a term whose values are
        /// numeric, and an ordered comparison is numeric.
        /// </summary>
        private static Type GetOperandType(FilterColumn filterColumn, IFilterOperation operation)
        {
            if (filterColumn.SpectrumColumn != null && SpectrumClassColumn.IsCvParamColumn(filterColumn.SpectrumColumn))
            {
                return SpectrumClassFilter.GetCvOperandType(operation);
            }
            return filterColumn.PropertyType;
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

        /// <summary>
        /// What this document's data was found to answer, which the property dropdown marks (see
        /// <see cref="propertyComboBox_DrawItem"/>). May be set after construction; nothing is precomputed
        /// from it, so late assignment still takes effect. Defaults to
        /// <see cref="SpectrumColumnScanner.Availability.UNKNOWN"/>, so a dialog opened by a caller that
        /// never scanned marks nothing rather than marking everything.
        /// </summary>
        public SpectrumColumnScanner.Availability ColumnAvailability
        {
            get { return _columnAvailability; }
            set { _columnAvailability = value ?? SpectrumColumnScanner.Availability.UNKNOWN; }
        }

        /// <summary>
        /// Where the column behind a dropdown caption stands for this document's data.
        /// </summary>
        private SpectrumColumnScanner.Standing GetCaptionStanding(string caption)
        {
            if (caption == null || !_propertyColumns.TryGetValue(caption, out var filterColumn))
            {
                return SpectrumColumnScanner.Standing.undetermined;
            }
            return _columnAvailability.GetStanding(filterColumn.PropertyPath,
                SpectrumClassColumn.IsCvParamColumn(filterColumn.SpectrumColumn));
        }

        /// <summary>
        /// Shows where each column stands for this document's data, in three states rather than two:
        ///
        ///   accent color  - this data has it, so a filter on it will match something
        ///   plain         - nothing is known either way, which is a document with no results yet
        ///   italic        - this data was examined and has nothing to match on
        ///
        /// Two states would have to conflate one pair, and every pairing misleads: merging "has it" with
        /// "unknown" hides the only positive signal, and merging "unknown" with "does not have it" claims
        /// an absence never observed.
        ///
        /// The accent also acts as the legend. There is nowhere to explain what the styling means, so a
        /// familiar column that any data answers - MS level, say - is left marked rather than suppressed
        /// as redundant: it lets the reader confirm the rule against data they already understand before
        /// trusting the styling on a CV term they have never heard of.
        ///
        /// Italic, not gray, for the absent. Graying is the scheme's disabled treatment, and these entries
        /// are anything but - a term absent from today's data is a reasonable thing to filter on for data
        /// yet to be imported. Italic carries "of a different kind" without borrowing that meaning, keeps
        /// full contrast, and follows any color scheme for free.
        /// </summary>
        private void propertyComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }
            var comboBox = (ComboBox) sender;
            var caption = comboBox.Items[e.Index].ToString();
            var standing = GetCaptionStanding(caption);
            e.DrawBackground();
            // The selected item keeps the scheme's own selection colors: the accent is unreadable on the
            // selection background, and the item the user is already on needs no guidance toward it.
            var foreColor = (e.State & DrawItemState.Selected) != 0
                ? SystemColors.HighlightText
                : SpectrumColumnStyle.GetForeColor(standing, e.ForeColor);
            var font = SpectrumColumnStyle.GetFontStyle(standing) == FontStyle.Italic
                ? GetUnanswerableFont(e.Font)
                : e.Font;
            TextRenderer.DrawText(e.Graphics, caption, font, e.Bounds, foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }

        /// <summary>
        /// The italic form of the list font, built once and reused: this is asked for on every item of
        /// every repaint, and a Font per draw would leak handles for as long as the dialog is open.
        /// </summary>
        private Font GetUnanswerableFont(Font baseFont)
        {
            if (_unanswerableFont == null || !Equals(_unanswerableFont.FontFamily, baseFont.FontFamily) ||
                _unanswerableFont.SizeInPoints != baseFont.SizeInPoints)
            {
                _unanswerableFont?.Dispose();
                _unanswerableFont = new Font(baseFont, baseFont.Style | FontStyle.Italic);
            }
            return _unanswerableFont;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _unanswerableFont?.Dispose();
            _unanswerableFont = null;
            base.OnFormClosed(e);
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

            if (columnIndex == propertyColumn.Index && e.Control is ComboBox comboBox)
            {
                comboBox.DrawMode = DrawMode.OwnerDrawFixed;
                // The grid reuses one editing control across cells, so the handler is removed before it is
                // added rather than accumulating a copy per edit.
                comboBox.DrawItem -= propertyComboBox_DrawItem;
                comboBox.DrawItem += propertyComboBox_DrawItem;
            }

            if (columnIndex == operationColumn.Index && e.Control is ComboBox operationComboBox &&
                rowIndex >= 0 && rowIndex < _rowBindingList.Count)
            {
                PopulateOperationItems(operationComboBox, _rowBindingList[rowIndex]);
            }
        }

        /// <summary>
        /// The operations worth offering for a row's column. A CV term the ontology declares no value type
        /// for is a pure flag: a spectrum either carries it or does not, so the blank tests are the only
        /// questions that can be asked, and offering a comparison invites a filter that can never match.
        ///
        /// Only the list offered while editing is narrowed, never <see cref="operationColumn"/>'s own
        /// items, so a committed value is still validated against the full set and cannot be rejected. An
        /// operation an existing filter already uses is kept in the list even where it would not be offered
        /// now, so opening and confirming this dialog cannot quietly rewrite a filter someone saved.
        /// </summary>
        private void PopulateOperationItems(ComboBox comboBox, Row row)
        {
            var operations = FilterOperations.ListOperations();
            if (row.Property != null && _propertyColumns.TryGetValue(row.Property, out var filterColumn) &&
                SpectrumClassColumn.IsValuelessCvColumn(filterColumn.SpectrumColumn))
            {
                operations = VALUELESS_COLUMN_OPERATIONS;
            }
            var displayNames = operations.Select(op => op.DisplayName).ToList();
            if (!string.IsNullOrEmpty(row.Operation) && !displayNames.Contains(row.Operation))
            {
                displayNames.Add(row.Operation);
            }
            comboBox.Items.Clear();
            comboBox.Items.AddRange(displayNames.Cast<object>().ToArray());
        }

        private void tabClauses_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updating)
            {
                return;
            }
            int newIndex = tabClauses.SelectedIndex;
            if (newIndex == CurrentPageIndex)
            {
                return;
            }
            if (newIndex < 0 || newIndex > FilterPages.Pages.Count)
            {
                tabClauses.SelectedIndex = CurrentPageIndex;
                return;
            }
            if (!SelectPage(newIndex))
            {
                tabClauses.SelectedIndex = CurrentPageIndex;
            }
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

        private void AddFilterColumn(FilterColumn filterColumn)
        {
            // The combobox and its reverse lookup are keyed by the displayed caption, so a caption can be
            // offered only once. Distinct CV terms cannot collide (the caption carries the unique accession);
            // the only possible clash is a vendor userParam named identically to a built-in property caption.
            // Interpreted properties are added first (see DisplayCurrentPage) and keep the plain caption.
            if (_propertyColumns.ContainsKey(filterColumn.Caption))
            {
                // The clashing userParam is offered under the marker the filter syntax uses to name one,
                // rather than dropped. Dropping it left ResolveFilterColumn with nothing to return, so a
                // saved criterion on such a term was not shown - and confirming the dialog, which rebuilds
                // the clause from the rows, then wrote it away.
                if (filterColumn.SpectrumColumn == null)
                {
                    return;
                }
                filterColumn = new FilterColumn(filterColumn.PropertyPath, filterColumn.PropertyType,
                    SpectrumClassFilter.USER_PARAM_PREFIX + filterColumn.Caption, filterColumn.SpectrumColumn);
                if (_propertyColumns.ContainsKey(filterColumn.Caption))
                {
                    return;
                }
            }
            _propertyColumns.Add(filterColumn.Caption, filterColumn);
            propertyColumn.Items.Add(filterColumn.Caption);
        }

        private void DisplayCurrentPage()
        {
            var currentPage = FilterPages.Pages.ElementAtOrDefault(CurrentPageIndex) ?? SpectrumClassFilter.GenericFilterPage;
            propertyColumn.Items.Clear();
            _propertyColumns.Clear();
            foreach (var column in currentPage.AvailableColumns)
            {
                var columnDescriptor = GetColumnDescriptor(column);
                if (columnDescriptor != null)
                {
                    AddFilterColumn(FilterColumn.FromColumnDescriptor(columnDescriptor));
                }
            }
            // The discovered mzML CV/user-parameter columns are not properties of the databound row
            // type, so they are offered here rather than through currentPage.AvailableColumns.
            foreach (var extraColumn in _extraColumns)
            {
                AddFilterColumn(FilterColumn.FromSpectrumClassColumn(extraColumn));
            }

            if (tabClauses.SelectedIndex != CurrentPageIndex)
            {
                tabClauses.SelectedIndex = CurrentPageIndex;
            }
            _rowList.Clear();
            _rowList.AddRange(GetRows(FilterPages.Clauses.ElementAtOrDefault(CurrentPageIndex) ?? FilterClause.EMPTY));
            _rowBindingList.ResetBindings();
        }

        private bool RememberFilterForCurrentPage()
        {
            var currentFilter = GetFilterForCurrentPage();
            if (currentFilter == null)
            {
                return false;
            }

            if (CurrentPageIndex == FilterPages.Pages.Count)
            {
                if (currentFilter.FilterSpecs.Any(spec => spec.Operation != FilterOperations.OP_HAS_ANY_VALUE))
                {
                    FilterPages = new FilterPages(FilterPages.Pages.Append(SpectrumClassFilter.GenericFilterPage),
                        FilterPages.Clauses.Append(currentFilter));
                }
            }
            else
            {
                FilterPages = FilterPages.ReplaceClause(CurrentPageIndex, currentFilter);
            }
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
                    // A CV operand is stored as the user typed it, so a number is converted to its
                    // invariant form here - the filter is evaluated invariantly wherever it ends up.
                    var operandText = propertyColumnDescriptor.SpectrumColumn == null
                        ? row.Value
                        : SpectrumClassFilter.ToInvariantCvOperand(row.Value, CultureInfo.CurrentCulture);
                    filterPredicate =
                        FilterPredicate.Parse(_rootColumn.DataSchema,
                            GetOperandType(propertyColumnDescriptor, filterOperation), filterOperation,
                            operandText);
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

        private void dataGridViewEx1_CellErrorTextNeeded(object sender, DataGridViewCellErrorTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rowList.Count)
            {
                return;
            }

            if (e.ColumnIndex == valueColumn.Index)
            {
                var row = _rowList[e.RowIndex];
                if (string.IsNullOrEmpty(row.Property))
                {
                    return;
                }
                if (!_propertyColumns.TryGetValue(row.Property, out var propertyColumnDescriptor))
                {
                    return;
                }

                var filterOperation = FilterOperations.ListOperations()
                    .FirstOrDefault(op => op.DisplayName == row.Operation);
                if (filterOperation == null || filterOperation == FilterOperations.OP_HAS_ANY_VALUE)
                {
                    return;
                }

                try
                {
                    FilterPredicate.Parse(_rootColumn.DataSchema, propertyColumnDescriptor.PropertyType,
                        filterOperation,
                        row.Value);
                }
                catch (Exception ex)
                {
                    e.ErrorText = ex.Message;
                }
            }
        }
    }
}
