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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditIsolationSchemeDlg : FormEx
    {
        private IsolationScheme _isolationScheme;
        private readonly IEnumerable<IsolationScheme> _existing;
        private readonly GridViewDriver _gridViewDriver;
        public const int COLUMN_START = 0;
        public const int COLUMN_END = 1;
        public const int COLUMN_TARGET = 2;
        public const int COLUMN_START_MARGIN = 3;
        public const int COLUMN_END_MARGIN = 4;
        public const int COLUMN_CE_RANGE = 5;

        public static class WindowType
        {
            public static string MEASUREMENT
            {
                get { return SettingsUIResources.WindowType_MEASUREMENT_Measurement; }
            }

            public static string EXTRACTION
            {
                get { return SettingsUIResources.WindowType_EXTRACTION_Extraction; }
            }
        };

        public static class DeconvolutionMethod
        {
            public static string NONE
            {
                get { return SettingsUIResources.DoconvolutionMethod_NONE_None; }
            }

            public static string MSX
            {
                get { return SettingsUIResources.DoconvolutionMethod_MSX_Msx; }
            }

            public static string OVERLAP
            {
                get { return SettingsUIResources.DoconvolutionMethod_OVERLAP_Overlap; }
            }

            public static string MSX_OVERLAP
            {
                get { return SettingsUIResources.DeconvolutionMethod_MSX_OVERLAP_Overlap_and_MSX; }
            }

            public static string FAST_OVERLAP
            {
                get { return SettingsUIResources.DeconvolutionMethod_FAST_OVERLAP_Fast_Overlap; }
            }
        };

        public static class IsolationWidthType
        {
            public static string RESULTS
            {
                get { return SettingsUIResources.IsolationWidthType_RESULTS_Results; }
            }

            public static string RESULTS_WITH_MARGIN
            {
                get { return SettingsUIResources.IsolationWidthType_RESULTS_WITH_MARGIN_Results_with_margin; }
            }

            public static string FIXED
            {
                get { return SettingsUIResources.IsolationWidthType_FIXED_Fixed; }
            }
        }

        public EditIsolationSchemeDlg(IEnumerable<IsolationScheme> existing)
        {
            _existing = existing;
            InitializeComponent();

            Icon = Resources.Skyline;

            //Position GridView
            AdjustGridTop(1);

            foreach (DataGridViewColumn col in gridIsolationWindows.Columns)
            {
                col.ValueType = typeof(decimal);
            }
            gridIsolationWindows.AutoGenerateColumns = false;
            _gridViewDriver = new GridViewDriver(this, editIsolationWindowBindingSource,
                new SortableBindingList<EditIsolationWindow>());

            // Fix-up isolation width edit controls
            UpdateIsolationWidths();

            // Initialize deconvolution combo box.
            var deconvOptions = new object[]
            {
                DeconvolutionMethod.NONE,
                DeconvolutionMethod.MSX,
                DeconvolutionMethod.OVERLAP,
                DeconvolutionMethod.MSX_OVERLAP,
                DeconvolutionMethod.FAST_OVERLAP
            };
            comboDeconv.Items.AddRange(deconvOptions);
            comboDeconv.SelectedItem = DeconvolutionMethod.NONE;
            comboDeconvPre.Items.AddRange(deconvOptions);

            // Hide columns to match checkboxes, which start unchecked
            AdjustGridTop(-1);
            comboIsolation.Visible = false;
            colStartMargin.Visible = false;
            colCERange.Visible = false;

            // Initialize IsolationComboBox
            comboIsolation.Items.AddRange(
                new[]
                {
                    WindowType.MEASUREMENT,
                    WindowType.EXTRACTION
                });
            comboIsolation.SelectedItem = WindowType.MEASUREMENT;

            // Initialize results isolation width combo
            comboIsolationWidth.Items.AddRange(
                new[]
                {
                    IsolationWidthType.RESULTS,
                    IsolationWidthType.RESULTS_WITH_MARGIN,
                    IsolationWidthType.FIXED
                });
            comboIsolationWidth.SelectedItem = IsolationWidthType.RESULTS;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            gridIsolationWindows.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        public IsolationScheme IsolationScheme
        {
            get { return _isolationScheme; }

            set
            {
                _isolationScheme = value;

                textName.Text = _isolationScheme != null ? _isolationScheme.Name : string.Empty;
                // Default isolation scheme.
                if (_isolationScheme == null)
                {
                    rbUseResultsData.Checked = true;
                    comboIsolationWidth.SelectedItem = IsolationWidthType.RESULTS;
                    return;
                }

                // Handle an isolation scheme determined by the results.
                if (_isolationScheme.FromResults)
                {
                    rbUseResultsData.Checked = true;
                    // Asymetric isolation schemes are no longer meaningful, since SCIEX updated
                    // its reader library to use symmetric isolation
//                    if (_isolationScheme.PrecursorRightFilter.HasValue)
//                    {
//                        cbAsymIsolation.Checked = true;
//                        textRightPrecursorFilterMz.Text =
//                            _isolationScheme.PrecursorRightFilter.Value.ToString(LocalizationHelper.CurrentCulture);
//                    }

                    if (!_isolationScheme.PrecursorFilter.HasValue)
                    {
                        comboIsolationWidth.SelectedItem = IsolationWidthType.RESULTS;
                    }
                    else
                    {
                        var precursorFilter = _isolationScheme.UseMargin
                            ? TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER_MARGIN
                            : TransitionFullScan.DEFAULT_PRECURSOR_MULTI_FILTER;
                        comboIsolationWidth.SelectedItem = _isolationScheme.UseMargin
                            ? IsolationWidthType.RESULTS_WITH_MARGIN
                            : IsolationWidthType.FIXED;
                        precursorFilter = _isolationScheme.PrecursorFilter.Value;
                        textPrecursorFilterMz.Text = precursorFilter.ToString(LocalizationHelper.CurrentCulture);
                    }
                    UpdateDeconvCombo(comboDeconv);
                }

                // Handle predetermined isolation scheme.
                else
                {
                    rbPrespecified.Checked = true;

                    // Load grid and show appropriate columns.
                    bool showCERange = false;
                    bool showMargin = false;
                    foreach (var isolationWindow in _isolationScheme.PrespecifiedIsolationWindows)
                    {
                        double start = isolationWindow.Start;
                        double end = isolationWindow.End;
                        if (Equals(comboIsolation.SelectedItem, WindowType.MEASUREMENT))
                        {
                            start -= (isolationWindow.StartMargin ?? 0);
                            end += (isolationWindow.EndMargin ?? (isolationWindow.StartMargin ?? 0));
                        }
                        _gridViewDriver.Items.Add(new EditIsolationWindow
                        {
                            Start = start,
                            End = end,
                            Target = isolationWindow.Target,
                            StartMargin = isolationWindow.StartMargin,
                            EndMargin = isolationWindow.EndMargin,
                            CERange = isolationWindow.CERange
                        });
                        showCERange |= isolationWindow.CERange.HasValue;
                        showMargin |= isolationWindow.StartMargin.HasValue;
                    }

                    cbSpecifyMargin.Checked = showMargin;
                    cbSpecifyCERange.Checked = showCERange;

                    textWindowsPerScan.Text = _isolationScheme.WindowsPerScan.HasValue
                        ? _isolationScheme.WindowsPerScan.Value.ToString(LocalizationHelper.CurrentCulture)
                        : string.Empty;
                    UpdateDeconvCombo(comboDeconvPre);
                }
            }
        }

        private static string IsolationSchemeToDeconvType(string specialHandling)
        {
            switch (specialHandling)
            {
                case (IsolationScheme.SpecialHandlingType.OVERLAP):
                    return DeconvolutionMethod.OVERLAP;
                case (IsolationScheme.SpecialHandlingType.MULTIPLEXED):
                    return DeconvolutionMethod.MSX;
                case (IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED):
                    return DeconvolutionMethod.MSX_OVERLAP;
                case (IsolationScheme.SpecialHandlingType.FAST_OVERLAP):
                    return DeconvolutionMethod.FAST_OVERLAP;
                default:
                    return DeconvolutionMethod.NONE;
            }
        }

        private static string DeconvTypeToIsolationScheme(string deconvType)
        {
            if (deconvType == DeconvolutionMethod.OVERLAP)
                return IsolationScheme.SpecialHandlingType.OVERLAP;
            else if (deconvType == DeconvolutionMethod.MSX)
                return IsolationScheme.SpecialHandlingType.MULTIPLEXED;
            else if (deconvType == DeconvolutionMethod.MSX_OVERLAP)
                return IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED;
            else if (deconvType == DeconvolutionMethod.FAST_OVERLAP)
                return IsolationScheme.SpecialHandlingType.FAST_OVERLAP;
            else return IsolationScheme.SpecialHandlingType.NONE;
        }

        private void UpdateDeconvCombo(ComboBox combo)
        {
            combo.SelectedItem = IsolationSchemeToDeconvType(_isolationScheme.SpecialHandling);
        }

        private void rbFromResultsData_CheckedChanged(object sender, EventArgs e)
        {
            EnableControls();
        }

        private void EnableControls()
        {
            bool fromResults = rbUseResultsData.Checked;

            object deconvItem = null;
            textPrecursorFilterMz.Enabled = fromResults;
            if (comboIsolationWidth.Enabled != fromResults)
            {
                comboIsolationWidth.Enabled = fromResults;
                comboIsolationWidth.SelectedItem = fromResults
                    ? IsolationWidthType.RESULTS
                    : null;
            }
            labelIsolationWidth.Enabled = fromResults;
            labelTh.Enabled = fromResults;
            labelDeconvolution.Enabled = fromResults;
            if (comboDeconv.Enabled != fromResults)
            {
                comboDeconv.Enabled = fromResults;
                deconvItem = comboDeconv.SelectedItem;
                comboDeconv.SelectedItem = comboDeconvPre.SelectedItem;
            }

            btnCalculate.Enabled = !fromResults;
            btnImport.Enabled = !fromResults;
            btnGraph.Enabled = !fromResults;
            gridIsolationWindows.Enabled = !fromResults;
            cbSpecifyMargin.Enabled = !fromResults;
            cbSpecifyCERange.Enabled = !fromResults;
            labelDeconvPre.Enabled = !fromResults;
            if (comboDeconvPre.Enabled == fromResults)
            {
                comboDeconvPre.Enabled = !fromResults;
                comboDeconvPre.SelectedItem = deconvItem;
            }
            labelWindowsPerScan.Enabled =
                textWindowsPerScan.Enabled =
                    (!fromResults && Equals(comboDeconvPre.SelectedItem, DeconvolutionMethod.MSX));
        }

        private void comboIsolationWidth_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateIsolationWidths();
        }

        private void UpdateIsolationWidths()
        {
            if (comboIsolationWidth.SelectedItem == null ||
                Equals(comboIsolationWidth.SelectedItem, IsolationWidthType.RESULTS))
            {
                textPrecursorFilterMz.Text = string.Empty;
                textPrecursorFilterMz.Visible = false;
                labelTh.Visible = false;
            }
            else
            {
                double filterWidth = Equals(comboIsolationWidth.SelectedItem, IsolationWidthType.RESULTS_WITH_MARGIN)
                    ? TransitionFullScan.DEFAULT_PRECURSOR_MULTI_FILTER_MARGIN
                    : TransitionFullScan.DEFAULT_PRECURSOR_MULTI_FILTER;
                textPrecursorFilterMz.Text = filterWidth.ToString(LocalizationHelper.CurrentCulture);
                textPrecursorFilterMz.Visible = true;
                labelTh.Visible = true;
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_existing.Contains(en => !ReferenceEquals(_isolationScheme, en) && Equals(name, en.Name)))
            {
                helper.ShowTextBoxError(textName,
                    Resources.EditIsolationSchemeDlg_OkDialog_The_isolation_scheme_named__0__already_exists, name);
                return;
            }

            if (rbUseResultsData.Checked)
            {
                double? precursorFilter = null;
                bool filterMargin = Equals(comboIsolationWidth.SelectedItem, IsolationWidthType.RESULTS_WITH_MARGIN);
                if (!Equals(comboIsolationWidth.SelectedItem, IsolationWidthType.RESULTS))
                {
                    double minFilt = filterMargin ? 0 : TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER;
                    double maxFilt = filterMargin
                        ? TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER_MARGIN
                        : TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER;
                    double precFilt;
                    if (!helper.ValidateDecimalTextBox(textPrecursorFilterMz,
                        minFilt, maxFilt, out precFilt))
                        return;
                    precursorFilter = precFilt;
                }
                try
                {
                    _isolationScheme = new IsolationScheme(name, SpecialHandling, precursorFilter, null, filterMargin);
                }
                catch (InvalidDataException exception)
                {
                    MessageDlg.ShowException(this, exception);
                    return;
                }
            }
            else
            {
                // Validate prespecified windows.
                List<IsolationWindow> windowList;
                if ((windowList = GetIsolationWindows()) == null)
                    return;


                // Must be at least one window.
                if (windowList.Count == 0)
                {
                    _gridViewDriver.SelectCell(COLUMN_START, 0);
                    MessageDlg.Show(this,
                        Resources
                            .EditIsolationSchemeDlg_OkDialog_Specify_Start_and_End_values_for_at_least_one_isolation_window);
                    gridIsolationWindows.Focus();
                    _gridViewDriver.EditCell();
                    return;
                }

                int? windowsPerScan = null;
                if (Equals(SpecialHandling, IsolationScheme.SpecialHandlingType.MULTIPLEXED))
                {
                    int x;
                    if (!helper.ValidateNumberTextBox(textWindowsPerScan,
                        IsolationScheme.MIN_MULTIPLEXED_ISOLATION_WINDOWS,
                        IsolationScheme.MAX_MULTIPLEXED_ISOLATION_WINDOWS,
                        out x))
                        return;
                    windowsPerScan = x;
                }
                // Check for overlap and gaps
                var sortedWindowList = windowList.OrderBy(o => o.Start).ToList();
                bool gapsOk = false;
                bool overlapsOk = false;
                bool overlap = Overlap;
                int increment = overlap ? 2 : 1;
                int subtraction = overlap ? 3 : 1;
                const double tolerance = 0.001;
                for (int i = 0; i < sortedWindowList.Count - subtraction; i += increment)
                {
                    for (int j = 0; j < increment; j++)
                    {
                        IsolationWindow current = sortedWindowList.ElementAt(i + j);
                        IsolationWindow next = sortedWindowList.ElementAt(i + j + increment);
                        if (!gapsOk && next.Start - current.End > tolerance)
                        {
                            if (MultiButtonMsgDlg.Show(this,
                                    Resources
                                        .EditIsolationSchemeDlg_OkDialog_There_are_gaps_in_a_single_cycle_of_your_extraction_windows__Do_you_want_to_continue_,
                                    MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false) !=
                                DialogResult.Yes)
                            {
                                return;
                            }
                            gapsOk = true;
                        }
                        else if (!overlapsOk && current.End - next.Start > tolerance)
                        {
                            if (MultiButtonMsgDlg.Show(this,
                                    SettingsUIResources.EditIsolationSchemeDlgOkDialogThereAreOverlapsContinue,
                                    MultiButtonMsgDlg.BUTTON_YES,
                                    MultiButtonMsgDlg.BUTTON_NO, false) != DialogResult.Yes)
                            {
                                return;
                            }
                            overlapsOk = true;
                        }
                    }
                }
                try
                {
                    _isolationScheme = new IsolationScheme(name, windowList, SpecialHandling, windowsPerScan);
                }
                catch (InvalidDataException exception)
                {
                    MessageDlg.ShowException(this, exception);
                    return;
                }
            }

            DialogResult = DialogResult.OK;
        }

        public List<IsolationWindow> GetIsolationWindows()
        {
            //Overlap requires an even number of windows
            if (Overlap && _gridViewDriver.Items.Count%2 == 1)
            {
                MessageDlg.Show(this,
                    SettingsUIResources.EditIsolationSchemeDlg_GetIsolationWindows_Overlap_requires_an_even_number_of_windows_);
                return null;
            }

            // Validate prespecified windows.
            var windowList = new List<IsolationWindow>();
            for (int row = 0; row < _gridViewDriver.Items.Count; row++)
            {
                var editWindow = _gridViewDriver.Items[row];

                // Report any problems in this row.
                int errorCell = FindErrorCell(editWindow);
                if (errorCell >= COLUMN_START)
                {
                    _gridViewDriver.SelectCell(errorCell, row);
                    MessageDlg.Show(this,
                        string.Format(Resources.EditIsolationSchemeDlg_OkDialog_Specify__0__for_isolation_window,
                            _gridViewDriver.GetHeaderText(errorCell)));
                    _gridViewDriver.EditCell();
                    return null;
                }

                try
                {
                    var isolationWindow = IsolationWindow.CreateWithMargin(
                        editWindow.Start.Value,
                        editWindow.End.Value,
                        Equals(comboIsolation.SelectedItem, WindowType.MEASUREMENT),
                        null,
                        cbSpecifyMargin.Checked ? editWindow.StartMargin : null,
                        cbSpecifyCERange.Checked ? editWindow.CERange : null);
                    windowList.Add(isolationWindow);
                }
                catch (InvalidDataException exception)
                {
                    _gridViewDriver.SelectRow(row);
                    MessageDlg.ShowException(this, exception);
                    return null;
                }
            }
            return windowList;
        }

        private int FindErrorCell(EditIsolationWindow editWindow)
        {
            if (!editWindow.Start.HasValue)
                return COLUMN_START;
            if (!editWindow.End.HasValue)
                return COLUMN_END;
            if (cbSpecifyMargin.Checked && !editWindow.StartMargin.HasValue)
                return COLUMN_START_MARGIN;
            if (cbSpecifyCERange.Checked && !editWindow.CERange.HasValue)
                return COLUMN_CE_RANGE;
            return -1;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void comboDeconv_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool fromResults = rbUseResultsData.Checked;
            labelWindowsPerScan.Enabled =
                textWindowsPerScan.Enabled =
                (!fromResults && (Equals(comboDeconvPre.SelectedItem, DeconvolutionMethod.MSX) ||
                                  Equals(comboDeconvPre.SelectedItem, DeconvolutionMethod.MSX_OVERLAP)));
        }

        private void cbSpecifyMargin_CheckedChanged(object sender, EventArgs e)
        {
            if (cbSpecifyMargin.Checked)
            {
                AdjustGridTop(1);
                comboIsolation.Visible = true;
                colStartMargin.Visible = true;
            }
            else
            {
                AdjustGridTop(-1);
                comboIsolation.Visible = false;
                colStartMargin.Visible = false;
            }
        }

        private void AdjustGridTop(int direction)
        {
            if (direction == -1)
            {
                int shift = gridIsolationWindows.Top - comboIsolation.Top;
                gridIsolationWindows.Height += shift;
                gridIsolationWindows.Top -= shift;
            }
            else
            {
                int shift = comboIsolation.Height + 6;
                gridIsolationWindows.Height -= shift;
                gridIsolationWindows.Top += shift;
            }
        }

        private void cbSpecifyCERanges_CheckedChanged(object sender, EventArgs e)
        {
            colCERange.Visible = cbSpecifyCERange.Checked;
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            Calculate();
        }

        public void Calculate()
        {
            using (var calculateDlg = new CalculateIsolationSchemeDlg())
            {
                if (calculateDlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Get calculated isolation windows from Calculate dialog.
                    _gridViewDriver.Items.Clear();
                    var isolationWindows = calculateDlg.IsolationWindows;
                    if (isolationWindows.Count == 0)
                        return;

                    // Determine whether isolation windows have a target and margins.
                    cbSpecifyMargin.Checked = (isolationWindows[0].StartMargin.HasValue &&
                                               !Equals(isolationWindows[0].StartMargin.Value, 0.0));

                    // Determine whether CE range is specified.
                    cbSpecifyCERange.Checked = (isolationWindows[0].CERange.HasValue);

                    // Determine if calculation was on Isolation Or Extraction
                    comboIsolation.SelectedItem = calculateDlg.IsIsolation
                        ? WindowType.MEASUREMENT
                        : WindowType.EXTRACTION;

                    // Load isolation windows into grid.
                    foreach (var window in isolationWindows)
                    {
                        _gridViewDriver.Items.Add(window);
                    }

                    // Copy multiplexed windows settings.
                    comboDeconvPre.SelectedItem = calculateDlg.Deconvolution;
                    textWindowsPerScan.Text = calculateDlg.Multiplexed
                        ? calculateDlg.WindowsPerScan.ToString(LocalizationHelper.CurrentCulture)
                        : string.Empty;
                }
            }
        }

        private class GridViewDriver : SimpleGridViewDriver<EditIsolationWindow>
        {
            private readonly EditIsolationSchemeDlg _editIsolationSchemeDlg;

            public GridViewDriver(EditIsolationSchemeDlg editIsolationSchemeDlg, BindingSource bindingSource,
                SortableBindingList<EditIsolationWindow> items)
                : base(editIsolationSchemeDlg.gridIsolationWindows, bindingSource, items)
            {
                _editIsolationSchemeDlg = editIsolationSchemeDlg;
                GridView.DataError += GridView_DataError;
                GridView.CellEndEdit += GridView_CellEndEdit;
                GridView.DataBindingComplete += GridView_DataBindingComplete;
            }

            private void GridView_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
            {
                FormatGrid();
            }

            private void GridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
            {
                FormatGridColumn(e.ColumnIndex);
            }

            private void GridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
            {
                SelectCell(e.ColumnIndex, e.RowIndex);
                MessageDlg.Show(MessageParent,
                    string.Format(Resources.GridViewDriver_GridView_DataError__0__must_be_a_valid_number,
                        GetHeaderText(e.ColumnIndex)));
                EditCell(); // Edit bad data
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
                windowList.Sort((a, b) => a.Start < b.Start ? -1 : 1);
                foreach (var window in windowList)
                    Items.Add(window);

                // Set each empty End to the Start of the next window.
                for (int i = 0; i < Items.Count - 1; i++)
                {
                    if (!Items[i].End.HasValue)
                        Items[i].End = Items[i + 1].Start;
                }

                // If End of last window is empty, remove it.
                if (Items.Count > 1 && !Items[Items.Count - 1].End.HasValue)
                {
                    Items.RemoveAt(Items.Count - 1);
                }

                // Fill empty Target values.
                if (_editIsolationSchemeDlg.colTarget.Visible)
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
                    double value = (double) row.Cells[columnIndex].Value;
                    while (value != Math.Round(value, decimalPlaces))
                    {
                        if (++decimalPlaces == 4)
                            break;
                    }
                    if (decimalPlaces == 4)
                        break;
                }

                // Set the format.
                GridView.Columns[columnIndex].DefaultCellStyle.Format = @"N" + decimalPlaces;
            }

            private EditIsolationWindow CreateEditIsolationWindow(IList<object> values, int lineNumber = -1)
            {
                var columnValues = new double?[GridView.Columns.Count];
                int index = 0;
                foreach (var value in values)
                {
                    while (!GridView.Columns[index].Visible)
                        index++;
                    string s = (string) value;
                    if (!string.IsNullOrEmpty(s))
                    {
                        double d;
                        if (!double.TryParse(s, out d))
                        {
                            throw new InvalidDataException(
                                string.Format(
                                    Resources.GridViewDriver_GetValue_An_invalid_number__0__was_specified_for__1__2__,
                                    Helpers.TruncateString(s, 20),
                                    GridView.Columns[index].HeaderText,
                                    lineNumber > 0
                                        ? TextUtil.SpaceSeparate(string.Empty,
                                            string.Format(SettingsUIResources.GridViewDriver_GetValue_on_line__0__, lineNumber))
                                        : string.Empty));
                        }
                        columnValues[index] = d;
                    }
                    index++;
                }

                var isolationWindow = new EditIsolationWindow
                {
                    Start = columnValues[COLUMN_START],
                    End = columnValues[COLUMN_END],
                    Target = columnValues[COLUMN_TARGET],
                    StartMargin = columnValues[COLUMN_START_MARGIN],
                    EndMargin = columnValues[COLUMN_END_MARGIN],
                    CERange = columnValues[COLUMN_CE_RANGE]
                };

                isolationWindow.Validate();
                return isolationWindow;
            }

            private bool ValidateRow(object[] columns, IWin32Window parent, int lineNumber)
            {
                try
                {
                    // Create and validate isolation window.
                    CreateEditIsolationWindow(columns, lineNumber);
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(parent,
                        string.Format(Resources.GridViewDriver_ValidateRow_On_line__0__1__, lineNumber, x.Message), x);
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
            set
            {
                rbUseResultsData.Checked = value;
                rbPrespecified.Checked = !value;
            }
        }

        public double? PrecursorFilter
        {
            get { return Helpers.ParseNullableDouble(textPrecursorFilterMz.Text); }
            set { textPrecursorFilterMz.Text = Helpers.NullableDoubleToString(value); }
        }

        public string IsolationWidthTypeName
        {
            get { return (string) comboIsolationWidth.SelectedItem; }
            set { comboIsolationWidth.SelectedItem = value; }
        }

        public SimpleGridViewDriver<EditIsolationWindow> IsolationWindowGrid
        {
            get { return _gridViewDriver; }
        }

        public string SpecialHandling
        {
            get
            {
                var combo = rbPrespecified.Checked ? comboDeconvPre : comboDeconv;
                return DeconvTypeToIsolationScheme((string) combo.SelectedItem);
            }
            set
            {
                var combo = rbPrespecified.Checked ? comboDeconvPre : comboDeconv;
                combo.SelectedItem = IsolationSchemeToDeconvType(value);
            }
        }

        public double? WindowsPerScan
        {
            get { return Helpers.ParseNullableDouble(textWindowsPerScan.Text); }
            set { textWindowsPerScan.Text = Helpers.NullableDoubleToString(value); }
        }

        public bool SpecifyMargin
        {
            get { return cbSpecifyMargin.Checked; }
            set { cbSpecifyMargin.Checked = value; }
        }

        public bool SpecifyCERange
        {
            get { return cbSpecifyCERange.Checked; }
            set { cbSpecifyCERange.Checked = value; }
        }

        public object CurrentWindowType
        {
            get { return comboIsolation.SelectedIndex; }
            set
            {
                if (!Equals(value, WindowType.MEASUREMENT) && !Equals(value, WindowType.EXTRACTION))
                    throw new ArgumentOutOfRangeException();
                comboIsolation.SelectedItem = value;
            }
        }

        public bool Overlap
        {
            get
            {
                return Equals(comboDeconvPre.SelectedItem, DeconvolutionMethod.OVERLAP) ||
                       Equals(comboDeconvPre.SelectedItem, DeconvolutionMethod.MSX_OVERLAP) ||
                       Equals(comboDeconvPre.SelectedItem, DeconvolutionMethod.FAST_OVERLAP);
            }
        }

        public void Clear()
        {
            _gridViewDriver.Items.Clear();
        }

        public void Paste()
        {
            _gridViewDriver.OnPaste();
        }

        #endregion


        private object _lastWindowType = WindowType.MEASUREMENT;

        private void comboIsolation_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Nothing needs to be done if comboIsolation's value is the same
            if (Equals(comboIsolation.SelectedItem, _lastWindowType)) return;
            _lastWindowType = comboIsolation.SelectedItem;

            bool isIsolation = Equals(comboIsolation.SelectedItem, WindowType.MEASUREMENT);
            int row = 0;
            foreach (EditIsolationWindow window in _gridViewDriver.Items)
            {
                double startMargin = window.StartMargin ?? 0;
                double endMargin = startMargin;

                if (window.Start != null)
                {
                    double newStart;
                    if (isIsolation)
                    {
                        newStart = (double) window.Start - startMargin;
                    }
                    else
                    {
                        newStart = (double) window.Start + startMargin;
                    }
                    _gridViewDriver.SetCellValue(COLUMN_START, row, newStart);
                }
                if (window.End != null)
                {
                    double newEnd;
                    if (isIsolation)
                        newEnd = (double) window.End + endMargin;
                    else
                        newEnd = (double) window.End - endMargin;
                    _gridViewDriver.SetCellValue(COLUMN_END, row, newEnd);
                }
                row++;
            }
        }

        private void btnGraph_Click(object sender, EventArgs e)
        {
            OpenGraph();
        }

        public void OpenGraph()
        {
            if (Equals(comboDeconvPre.SelectedItem, DeconvolutionMethod.MSX) ||
                Equals(comboDeconvPre.SelectedItem, DeconvolutionMethod.MSX_OVERLAP))
            {
                MessageDlg.Show(this, SettingsUIResources.EditIsolationSchemeDlg_OpenGraph_Graphing_multiplexing_is_not_supported_);
                return;
            }

            List<IsolationWindow> windows = GetIsolationWindows();
            if (windows == null)
                return;
            int windowsPerScan;
            if (!int.TryParse(textWindowsPerScan.Text, out windowsPerScan))
                windowsPerScan = 1;
            bool useMargins = cbSpecifyMargin.Checked;
            using (var graphDlg = new DiaIsolationWindowsGraphForm(windows, useMargins,
                comboDeconvPre.SelectedItem, windowsPerScan))
            {
                graphDlg.ShowDialog(this);
            }
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            ImportRanges();
        }

        public void ImportRanges()
        {
            string activeDir = Settings.Default.ActiveDirectory;
            if (string.IsNullOrEmpty(activeDir))
                activeDir = null;
            var dataSources = ImportResultsDlg.GetDataSourcePaths(this, activeDir);
            if (dataSources == null)
                return;

            ImportRangesFromFiles(dataSources);
        }

        private void ImportRangesFromFiles(MsDataFileUri[] dataSources)
        {
            try
            {
                IsolationScheme isolationScheme = null;
                using (var dlg = new LongWaitDlg())
                {
                    dlg.Message = SettingsUIResources.EditIsolationSchemeDlg_ImportRangesFromFiles_Reading_isolation_scheme___;
                    var reader = new IsolationSchemeReader(dataSources);
                    dlg.PerformWork(this, 500, progressMonitor => isolationScheme = reader.Import(@"temp", progressMonitor));
                }

                if (isolationScheme != null)
                {
                    cbSpecifyMargin.Checked = isolationScheme.PrespecifiedIsolationWindows.Count(w => w.StartMargin.HasValue) > 1;
                    comboDeconvPre.SelectedItem = isolationScheme.SpecialHandling == IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED
                        ? DeconvolutionMethod.OVERLAP
                        : DeconvolutionMethod.NONE;
                    _gridViewDriver.Items.Clear();
                    foreach (var isolationWindow in isolationScheme.PrespecifiedIsolationWindows)
                    {
                        _gridViewDriver.Items.Add(new EditIsolationWindow
                        {
                            Start = isolationWindow.Start,
                            End = isolationWindow.End,
                            StartMargin = isolationWindow.StartMargin
                        });
                    }
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, TextUtil.LineSeparate(Resources.EditIsolationSchemeDlg_ImportRangesFromFiles_Failed_reading_isolation_scheme_, x.Message), x);
            }
        }
    }
}
