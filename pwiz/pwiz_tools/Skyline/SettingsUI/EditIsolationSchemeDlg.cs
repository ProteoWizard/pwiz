/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.IO;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditIsolationSchemeDlg : FormEx
    {
        private IsolationScheme _isolationScheme;
        private readonly IEnumerable<IsolationScheme> _existing;
        private readonly GridViewDriver _gridViewDriver;

        // Isolation window grid column indexes
        public enum GridColumns
        {
            none = -1,
            start,
            end,
            target,
            start_margin,
            end_margin
        };

        public static class WindowMargin
        {
            public const string NONE = "None";
            public const string SYMMETRIC = "Symmetric";
            public const string ASYMMETRIC = "Asymmetric";
        };

        public EditIsolationSchemeDlg(IEnumerable<IsolationScheme> existing)
        {
            _existing = existing;

            InitializeComponent();
            _gridViewDriver = new GridViewDriver(gridIsolationWindows, editIsolationWindowBindingSource,
                                                 new SortableBindingList<EditIsolationWindow>());

            // Fix-up isolation width edit controls
            UpdateIsolationWidths();

            // Initialize margins combo box.
            comboMargins.Items.AddRange(
                new object[]
                    {
                        WindowMargin.NONE,
                        WindowMargin.SYMMETRIC,
                        WindowMargin.ASYMMETRIC
                    });
            comboMargins.SelectedItem = WindowMargin.NONE;  // Hides margin columns

            // Hide target column to match checkbox, which starts unchecked
            gridIsolationWindows.Columns[(int)GridColumns.target].Visible = false;

            // TODO: Implement Graph
            btnGraph.Hide();
        }

        public IsolationScheme IsolationScheme
        {
            get { return _isolationScheme; }

            set
            {
                _isolationScheme = value;

                textName.Text = _isolationScheme != null ? _isolationScheme.Name : string.Empty;
                var precursorFilter = TransitionFullScan.DEFAULT_PRECURSOR_MULTI_FILTER;

                // Default isolation scheme.
                if (_isolationScheme == null)
                {
                    textPrecursorFilterMz.Text = precursorFilter.ToString(CultureInfo.CurrentCulture);
                    rbUseResultsData.Checked = true;
                    return;
                }

                // Handle an isolation scheme determined by the results.
                if (_isolationScheme.FromResults)
                {
                    rbUseResultsData.Checked = true;
                    if (_isolationScheme.PrecursorRightFilter.HasValue)
                    {
                        cbAsymIsolation.Checked = true;
                        textRightPrecursorFilterMz.Text =
                            _isolationScheme.PrecursorRightFilter.Value.ToString(CultureInfo.CurrentCulture);
                    }

                    if (_isolationScheme.PrecursorFilter.HasValue)
                    {
                        precursorFilter = _isolationScheme.PrecursorFilter.Value;
                    }
                    textPrecursorFilterMz.Text = precursorFilter.ToString(CultureInfo.CurrentCulture);
                }

                // Handle predetermined isolation scheme.
                else
                {
                    rbPrespecified.Checked = true;

                    // Load grid and show appropriate columns.
                    bool showTarget = false;
                    bool showStartMargin = false;
                    bool showEndMargin = false;
                    foreach (var isolationWindow in _isolationScheme.PrespecifiedIsolationWindows)
                    {
                        _gridViewDriver.Items.Add(new EditIsolationWindow
                                                      {
                                                          Start = isolationWindow.Start,
                                                          End = isolationWindow.End,
                                                          Target = isolationWindow.Target,
                                                          StartMargin = isolationWindow.StartMargin,
                                                          EndMargin = isolationWindow.EndMargin
                                                      });
                        showTarget |= isolationWindow.Target.HasValue;
                        showStartMargin |= isolationWindow.StartMargin.HasValue;
                        showEndMargin |= isolationWindow.EndMargin.HasValue;
                    }

                    cbSpecifyTarget.Checked = showTarget;
                    comboMargins.SelectedItem = showStartMargin 
                        ? (showEndMargin ? WindowMargin.ASYMMETRIC : WindowMargin.SYMMETRIC) 
                        : WindowMargin.NONE;
                    cbMultiplexed.Checked = Equals(_isolationScheme.SpecialHandling,
                                                   IsolationScheme.SpecialHandlingType.MULTIPLEXED);
                    textWindowsPerScan.Text = _isolationScheme.WindowsPerScan.HasValue
                                                  ? _isolationScheme.WindowsPerScan.Value.ToString(CultureInfo.CurrentCulture)
                                                  : "";
                }
            }
        }

        private void rbFromResultsData_CheckedChanged(object sender, EventArgs e)
        {
            EnableControls();
        }

        private void EnableControls()
        {
            bool fromResults = rbUseResultsData.Checked;

            textPrecursorFilterMz.Enabled = fromResults;
            textRightPrecursorFilterMz.Enabled = fromResults;
            cbAsymIsolation.Enabled = fromResults;
            labelIsolationWidth.Enabled = fromResults;
            labelTh.Enabled = fromResults;

            btnCalculate.Enabled = !fromResults;
            btnGraph.Enabled = !fromResults;
            gridIsolationWindows.Enabled = !fromResults;
            cbSpecifyTarget.Enabled = !fromResults;
            cbMultiplexed.Enabled = !fromResults;
            comboMargins.Enabled = !fromResults;
            labelMargins.Enabled = !fromResults;
            labelWindowsPerScan.Enabled =
                textWindowsPerScan.Enabled = !fromResults && Equals(SpecialHandling, IsolationScheme.SpecialHandlingType.MULTIPLEXED);
        }

        private void cbAsymIsolation_CheckedChanged(object sender, EventArgs e)
        {
            UpdateIsolationWidths();
        }

        private void UpdateIsolationWidths()
        {
            textRightPrecursorFilterMz.Visible = cbAsymIsolation.Checked;
            if (cbAsymIsolation.Checked)
            {
                labelIsolationWidth.Text = "Isolation &widths:";
                textPrecursorFilterMz.Width = textRightPrecursorFilterMz.Width;
                double totalWidth;
                double? halfWidth = null;
                if (double.TryParse(textPrecursorFilterMz.Text, out totalWidth))
                    halfWidth = totalWidth/2;
                textPrecursorFilterMz.Text = textRightPrecursorFilterMz.Text = 
                    halfWidth.HasValue ? halfWidth.Value.ToString(CultureInfo.CurrentCulture) : "";
            }
            else
            {
                labelIsolationWidth.Text = "Isolation &width:";
                textPrecursorFilterMz.Width = textRightPrecursorFilterMz.Right - textPrecursorFilterMz.Left;
                double leftWidth;
                double? totalWidth = null;
                if (double.TryParse(textPrecursorFilterMz.Text, out leftWidth))
                {
                    double rightWidth;
                    if (double.TryParse(textRightPrecursorFilterMz.Text, out rightWidth))
                        totalWidth = leftWidth + rightWidth;
                    else
                        totalWidth = leftWidth*2;
                }
                textPrecursorFilterMz.Text = 
                    totalWidth.HasValue ? totalWidth.Value.ToString(CultureInfo.CurrentCulture) : "";
            }
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            if (_existing.Contains(en => !ReferenceEquals(_isolationScheme, en) && Equals(name, en.Name)))
            {
                helper.ShowTextBoxError(textName, "The isolation scheme named '{0}' already exists.", name);
                return;
            }

            if (rbUseResultsData.Checked)
            {
                double filterFactor = cbAsymIsolation.Checked ? 0.5 : 1;
                double minFilt = TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER*filterFactor;
                double maxFilt = TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER*filterFactor;
                double precFilt;
                if (!helper.ValidateDecimalTextBox(e, textPrecursorFilterMz,
                                                   minFilt, maxFilt, out precFilt))
                    return;
                double? precursorFilter = precFilt;
                double? precursorRightFilter = null;
                if (cbAsymIsolation.Checked)
                {
                    if (!helper.ValidateDecimalTextBox(e, textRightPrecursorFilterMz,
                                                       minFilt, maxFilt, out precFilt))
                        return;
                    precursorRightFilter = precFilt;
                }
                _isolationScheme = new IsolationScheme(name, precursorFilter, precursorRightFilter);
            }
            else
            {
                // Validate prespecified windows.
                var windowList = new List<IsolationWindow>();
                string marginType = MarginType;
                for (int row = 0; row < _gridViewDriver.Items.Count; row++)
                {
                    var editWindow = _gridViewDriver.Items[row];

                    // Report any problems in this row.
                    GridColumns errorCell = FindErrorCell(editWindow);
                    if (errorCell != GridColumns.none)
                    {
                        _gridViewDriver.SelectCell((int) errorCell, row);
                        MessageDlg.Show(this, string.Format("Specify {0} for isolation window.", _gridViewDriver.GetHeaderText((int) errorCell)));
                        _gridViewDriver.EditCell();
                        return;
                    }

                    IsolationWindow isolationWindow;
                    try
                    {
                        isolationWindow = new IsolationWindow(
// ReSharper disable PossibleInvalidOperationException
                            editWindow.Start.Value,
                            editWindow.End.Value,
// ReSharper restore PossibleInvalidOperationException
                            cbSpecifyTarget.Checked ? editWindow.Target : null,
                            !Equals(marginType, WindowMargin.NONE) ? editWindow.StartMargin : null,
                            Equals(marginType, WindowMargin.ASYMMETRIC) ? editWindow.EndMargin : null);
                    }
                    catch (InvalidDataException exception)
                    {
                        _gridViewDriver.SelectRow(row);
                        MessageDlg.Show(this, exception.Message);
                        return;
                    }
                    windowList.Add(isolationWindow);
                }

                // Must be at least one window.
                if (windowList.Count == 0)
                {
                    _gridViewDriver.SelectCell((int)GridColumns.start, 0);
                    MessageDlg.Show(this, "Specify Start and End values for at least one isolation window.");
                    gridIsolationWindows.Focus();
                    _gridViewDriver.EditCell();
                    return;
                }

                // Check unique targets.
                if (cbSpecifyTarget.Checked)
                {
// ReSharper disable PossibleInvalidOperationException
                    // IsolationWindow constructor above checks for null Target.
                    windowList.Sort(new IsolationWindowTargetComparer());
                    for (int row = 1; row < windowList.Count; row++)
                    {
                        if (windowList[row].Target.Value == windowList[row-1].Target.Value)
                        {
                            // Sort grid by Target so the user will see the duplicate Target values
                            // next to each other.  There isn't an easy way to do a secondary sort
                            // on the Start values, so just remove all the data from the grid and
                            // insert it in sorted order.
                            _gridViewDriver.Items.Clear();
                            foreach (var isolationWindow in windowList)
                            {
                                _gridViewDriver.Items.Add(new EditIsolationWindow(isolationWindow));
                            }
                            _gridViewDriver.SelectCell((int)GridColumns.target, row);
                            MessageDlg.Show(this, "The selected target is not unique.");
                            gridIsolationWindows.Focus();
                            _gridViewDriver.EditCell();
                            return;
                        }
                    }
// ReSharper restore PossibleInvalidOperationException
                }

                // Check unambiguous isolation window ranges.
                else
                {
                    windowList.Sort((w1, w2) => w1.Start.CompareTo(w2.Start));
                    for (int row = 1; row < windowList.Count; row++)
                    {
                        // If the previous window's end is >= to this window's end, it entirely contains this window.
                        string errorText = null;
                        if (windowList[row - 1].End >= windowList[row].End)
                            errorText = "The selected isolation window is contained by the previous window.";
                        // If the following window's start is <= the previous window's end, the current window is redundant.
                        else if (row < windowList.Count - 1 && windowList[row - 1].End >= windowList[row + 1].Start)
                            errorText = "The selected isolation window is covered by windows before and after it.";
                        if (errorText != null)
                        {
                            _gridViewDriver.Sort((int)GridColumns.start);
                            _gridViewDriver.SelectRow(row);
                            MessageDlg.Show(this, errorText);
                            return;
                        }
                    }
                }

                int? windowsPerScan = null;
                if (Equals(SpecialHandling, IsolationScheme.SpecialHandlingType.MULTIPLEXED))
                {
                    int x;
                    if (!helper.ValidateNumberTextBox(e, textWindowsPerScan, 
                        IsolationScheme.MIN_MULTIPLEXED_ISOLATION_WINDOWS, IsolationScheme.MAX_MULTIPLEXED_ISOLATION_WINDOWS,
                        out x))
                        return;
                    windowsPerScan = x;
                }

                try
                {
                    _isolationScheme = new IsolationScheme(name, windowList, SpecialHandling, windowsPerScan);
                }
                catch (InvalidDataException exception)
                {
                    MessageDlg.Show(this, exception.Message);
                    return;
                }
            }

            DialogResult = DialogResult.OK;
        }

        private GridColumns FindErrorCell(EditIsolationWindow editWindow)
        {
            if (!editWindow.Start.HasValue) 
                return GridColumns.start;
            if (!editWindow.End.HasValue) 
                return GridColumns.end;
            if (cbSpecifyTarget.Checked && !editWindow.Target.HasValue) 
                return GridColumns.target;
            string marginType = MarginType;
            if (!Equals(marginType, WindowMargin.NONE) && !editWindow.StartMargin.HasValue) 
                return GridColumns.start_margin;
            if (Equals(marginType, WindowMargin.ASYMMETRIC) && !editWindow.EndMargin.HasValue) 
                return GridColumns.end_margin;
            return GridColumns.none;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void cbMultiplexed_CheckedChanged(object sender, EventArgs e)
        {
            EnableControls();
        }

        private void comboMargins_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedItem = MarginType;
            if (Equals(selectedItem, WindowMargin.NONE))
            {
                gridIsolationWindows.Columns[(int) GridColumns.start_margin].Visible = false;
                gridIsolationWindows.Columns[(int) GridColumns.end_margin].Visible = false;
            }
            else if (Equals(selectedItem, WindowMargin.SYMMETRIC))
            {
                gridIsolationWindows.Columns[(int) GridColumns.start_margin].Visible = true;
                gridIsolationWindows.Columns[(int) GridColumns.end_margin].Visible = false;
                gridIsolationWindows.Columns[(int) GridColumns.start_margin].HeaderText = "Margin";
            }
            else
            {
                gridIsolationWindows.Columns[(int) GridColumns.start_margin].Visible = true;
                gridIsolationWindows.Columns[(int) GridColumns.end_margin].Visible = true;
                gridIsolationWindows.Columns[(int) GridColumns.start_margin].HeaderText = "Start margin";
            }
        }

        private void cbSpecifyTarget_CheckedChanged(object sender, EventArgs e)
        {
            gridIsolationWindows.Columns[(int)GridColumns.target].Visible = cbSpecifyTarget.Checked;
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            Calculate();
        }

        public void Calculate()
        {
            using (var calculateDlg = new CalculateIsolationSchemeDlg())
            {
                if (calculateDlg.ShowDialog() == DialogResult.OK)
                {
                    // Get calculated isolation windows from Calculate dialog.
                    _gridViewDriver.Items.Clear();
                    var isolationWindows = calculateDlg.IsolationWindows;
                    if (isolationWindows.Count == 0)
                        return;

                    // Determine whether isolation windows have a target and margins.
                    cbSpecifyTarget.Checked = isolationWindows[0].Target.HasValue;
                    if (isolationWindows[0].StartMargin.HasValue)
                    {
                        if (isolationWindows[0].EndMargin.HasValue)
                            comboMargins.SelectedItem = WindowMargin.ASYMMETRIC;
                        else
                            comboMargins.SelectedItem = WindowMargin.SYMMETRIC;
                    }
                    else
                        comboMargins.SelectedItem = WindowMargin.NONE;

                    // Load isolation windows into grid.
                    foreach (var window in isolationWindows)
                    {
                        _gridViewDriver.Items.Add(window);
                    }

                    // Copy multiplexed windows settings.
                    if (calculateDlg.Multiplexed)
                    {
                        cbMultiplexed.Checked = true;
                        textWindowsPerScan.Text = calculateDlg.WindowsPerScan.ToString(CultureInfo.CurrentCulture);
                    }
                    else
                    {
                        cbMultiplexed.Checked = false;
                        textWindowsPerScan.Text = string.Empty;
                    }
                }
            }
        }

        private class IsolationWindowTargetComparer : IComparer<IsolationWindow>
        {
            #region Implementation of IComparer<in IsolationWindow>

            public int Compare(IsolationWindow x, IsolationWindow y)
            {
                // Sort first by Target, then by Start value.
// ReSharper disable PossibleInvalidOperationException
                int result = x.Target.Value.CompareTo(y.Target.Value);
// ReSharper restore PossibleInvalidOperationException
                return result == 0 ? x.Start.CompareTo(y.Start) : result;
            }

            #endregion
        }

        private class GridViewDriver : SimpleGridViewDriver<EditIsolationWindow>
        {
            public GridViewDriver(DataGridViewEx gridView, BindingSource bindingSource,
                                  SortableBindingList<EditIsolationWindow> items)
                : base(gridView, bindingSource, items)
            {
                GridView.DataError += GridView_DataError;
                GridView.CellEndEdit += GridView_CellEndEdit;
                GridView.DataBindingComplete += GridView_DataBindingComplete;
            }

            void GridView_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
            {
                FormatGrid();
            }

            void GridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
            {
                FormatGridColumn(e.ColumnIndex);
            }

            private void GridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
            {
                SelectCell(e.ColumnIndex, e.RowIndex);
                MessageDlg.Show(MessageParent,
                                string.Format("{0} must be a valid number.", GetHeaderText(e.ColumnIndex)));
                EditCell();     // Edit bad data
            }

            public string GetHeaderText(int index)
            {
                return GridView.Columns[index].HeaderText;
            }

            protected override void DoPaste()
            {
                var windowList = new List<EditIsolationWindow>();

                if (!GridView.DoPaste(MessageParent, ValidateRow,
                                      values => windowList.Add(CreateEditIsolationWindow(values))))
                    return;

                // Special case for pasting only a single number.
                if (windowList.Count == 1 && !windowList[0].End.HasValue)
                {
                    if (windowList[0].Start.HasValue)
                    {
                        SetCellValue(windowList[0].Start.Value);
                    }
                    FormatGridColumn(GridView.CurrentCell.ColumnIndex);
                    return;
                }

                // Paste multiple items.
                Items.Clear();
                foreach (var window in windowList)
                    Items.Add(window);
                Items.Sort(GridView.Columns[(int)GridColumns.start].HeaderText);

                // Set each empty End to the Start of the next window.
                for (int i = 0; i < Items.Count - 1; i++)
                {
                    if (!Items[i].End.HasValue)
                        Items[i].End = Items[i + 1].Start;
                }

                // If End of last window is empty, remove it.
                if (Items.Count > 1 && !Items[Items.Count-1].End.HasValue)
                {
                    Items.RemoveAt(Items.Count - 1);
                }

                // Fill empty Target values.
                if (GridView.Columns[(int)GridColumns.target].Visible)
                {
                    foreach (var item in Items)
                    {
                        if (!item.Target.HasValue && item.Start.HasValue && item.End.HasValue)
                        {
                            item.Target = Math.Round((item.Start.Value + item.End.Value)/2, 4);
                        }
                    }
                }

                // Reformat grid after paste.
                FormatGrid();
            }

            private void FormatGrid()
            {
                for (int col = 0; col < GridView.Columns.Count; col++)
                {
                    FormatGridColumn(col);
                }
            }

            private void FormatGridColumn(int columnIndex)
            {
                if (!GridView.Columns[columnIndex].Visible)
                    return;

                // Find the maximum number of decimal places (up to 4) needed to display this column.
                int decimalPlaces = 0;
                foreach (DataGridViewRow row in GridView.Rows)
                {
                    if (row.Cells[columnIndex].Value == null) continue;
                    double value = (double)row.Cells[columnIndex].Value;
                    while (value != Math.Round(value, decimalPlaces))
                    {
                        if (++decimalPlaces == 4)
                            break;
                    }
                    if (decimalPlaces == 4)
                        break;
                }

                // Set the format.
                GridView.Columns[columnIndex].DefaultCellStyle.Format = "N" + decimalPlaces;
            }

            private EditIsolationWindow CreateEditIsolationWindow(IList<object> values, int lineNumber = -1)
            {
                // Index values can change depending on visibility of optional Target.
                int startMarginIndex = GridView.Columns[(int)GridColumns.target].Visible ? (int)GridColumns.start_margin : (int)GridColumns.start_margin-1;
                int endMarginIndex = startMarginIndex + 1;
                
                var isolationWindow = new EditIsolationWindow
                {
                    Start =         GetValue((int)GridColumns.start, 0, 
                                        lineNumber, values, values.Count > 0),
                    End =           GetValue((int)GridColumns.end, 1, 
                                        lineNumber, values, values.Count > 1),
                    Target =        GetValue((int)GridColumns.target, 2, lineNumber, values, 
                                        GridView.Columns[(int)GridColumns.target].Visible && values.Count > 2),
                    StartMargin =   GetValue((int)GridColumns.start_margin, startMarginIndex, lineNumber, values,
                                        GridView.Columns[(int)GridColumns.start_margin].Visible && values.Count > startMarginIndex),
                    EndMargin =     GetValue((int)GridColumns.end_margin, endMarginIndex, lineNumber, values,
                                        GridView.Columns[(int)GridColumns.end_margin].Visible && values.Count > endMarginIndex)
                };

                isolationWindow.Validate();
                return isolationWindow;
            }

            // Get a value from a list of values with detailed error reporting.
            private double? GetValue(int columnIndex, int listIndex, int lineNumber, IList<object> values,
                                     bool expectValue)
            {
                if (!expectValue || listIndex >= values.Count || ((string)values[listIndex]).Trim().Length == 0)
                    return null;
                double value;
                if (!double.TryParse((string) values[listIndex], out value))
                    throw new InvalidDataException(
                        string.Format(@"An invalid number (""{0}"") was specified for {1}{2}.",
                                      Helpers.TruncateString((string) values[listIndex], 20),
                                      GridView.Columns[columnIndex].HeaderText,
                                      lineNumber > 0 ? " on line " + lineNumber : ""));
                return value;
            }

            private bool ValidateRow(object[] columns, int lineNumber)
            {
                try
                {
                    // Create and validate isolation window.
                    CreateEditIsolationWindow(columns, lineNumber);
                }
                catch (Exception x)
                {
                    MessageDlg.Show(MessageParent, string.Format("On line {0}, {1}", lineNumber, x.Message));
                    return false;
                }
                return true;
            }
        }

        #region Functional Test Support

        public string IsolationSchemeName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public bool UseResults
        {
            get { return rbUseResultsData.Checked; }
            set { rbUseResultsData.Checked = value; rbPrespecified.Checked = !value; }
        }

        public double? PrecursorFilter
        {
            get { return Helpers.ParseNullableDouble(textPrecursorFilterMz.Text); }
            set { textPrecursorFilterMz.Text = Helpers.NullableDoubleToString(value); }
        }

        public double? PrecursorRightFilter
        {
            get { return textRightPrecursorFilterMz.Visible ? Helpers.ParseNullableDouble(textRightPrecursorFilterMz.Text) : null; }
            set { textRightPrecursorFilterMz.Text = Helpers.NullableDoubleToString(value); }
        }

        public bool AsymmetricFilter
        {
            get { return cbAsymIsolation.Checked; }
            set { cbAsymIsolation.Checked = value; }
        }

        public SimpleGridViewDriver<EditIsolationWindow> IsolationWindowGrid
        {
            get { return _gridViewDriver; }
        }

        public string SpecialHandling
        {
            get
            {
                return cbMultiplexed.Checked
                    ? IsolationScheme.SpecialHandlingType.MULTIPLEXED
                    : IsolationScheme.SpecialHandlingType.NONE;
            }
            set
            {
                cbMultiplexed.Checked = Equals(value, IsolationScheme.SpecialHandlingType.MULTIPLEXED);
            }
        }

        public double? WindowsPerScan
        {
            get { return Helpers.ParseNullableDouble(textWindowsPerScan.Text); }
            set { textWindowsPerScan.Text = Helpers.NullableDoubleToString(value); }
        }

        public bool SpecifyTarget
        {
            get { return cbSpecifyTarget.Checked; }
            set { cbSpecifyTarget.Checked = value; }
        }

        public string MarginType
        {
            get { return (string)comboMargins.SelectedItem; }
            set { comboMargins.SelectedItem = value; }
        }
    }

    #endregion
}
