/*
 * Original author: Bian Pratt <bspratt .at. u.washington.edu>,
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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    public partial class EditDriftTimePredictorDlg : FormEx
    {
        private readonly SettingsListComboDriver<IonMobilityLibrarySpec> _driverIonMobilityLibraryListComboDriver;

        private DriftTimePredictor _predictor;
        private readonly IEnumerable<DriftTimePredictor> _existing;
        private bool _showRegressions;

        public const int COLUMN_SEQUENCE = 0;
        public const int COLUMN_CHARGE = 1;
        public const int COLUMN_DRIFT_TIME_MSEC = 2;


        public EditDriftTimePredictorDlg(IEnumerable<DriftTimePredictor> existing)
        {
            _existing = existing;
            _showRegressions = true;

            InitializeComponent();


            // TODO: ion mobility libraries are more complex than initially thought - put this off until after summer 2014 release
            labelIonMobilityLibrary.Visible = comboLibrary.Visible = false;
 
            Icon = Resources.Skyline;

            _driverIonMobilityLibraryListComboDriver = new SettingsListComboDriver<IonMobilityLibrarySpec>(comboLibrary, Settings.Default.IonMobilityLibraryList);
            _driverIonMobilityLibraryListComboDriver.LoadList(null);
            UpdateControls();
        }

        public DriftTimePredictor Predictor
        {
            get { return _predictor; }

            set
            {
                _predictor = value;
                gridRegression.Rows.Clear();
                gridMeasuredDriftTimes.Rows.Clear();
                if (_predictor == null)
                {
                    textName.Text = string.Empty;
                }
                else
                {
                    textName.Text = _predictor.Name;

                    // List any measured drift times
                    UpdateMeasuredDriftTimesControl(_predictor);

                    comboLibrary.SelectedItem = (_predictor.IonMobilityLibrary != null) ? _predictor.IonMobilityLibrary.Name : null;
                    if (_predictor.ChargeRegressionLines != null)
                    {
                        // Reduce the sparse indexed-by-charge list to only non-empty members for display
                        foreach (ChargeRegressionLine r in _predictor.ChargeRegressionLines.Where(chargeRegressionLine => chargeRegressionLine != null))
                        {
                            gridRegression.Rows.Add(r.Charge.ToString(LocalizationHelper.CurrentCulture),
                                r.Slope.ToString(LocalizationHelper.CurrentCulture),
                                r.Intercept.ToString(LocalizationHelper.CurrentCulture));
                        }
                    }
                    textResolvingPower.Text = string.Format("{0:F04}", _predictor.ResolvingPower); // Not L10N
                }
                UpdateControls();
            }
        }

        private void UpdateMeasuredDriftTimesControl(DriftTimePredictor predictor)
        {
            // List any measured drift times
            gridMeasuredDriftTimes.Rows.Clear();
            if (predictor.MeasuredDriftTimePeptides != null)
            {
                bool hasHighEnergyOffsets =
                    predictor.MeasuredDriftTimePeptides.Any(p => p.Value.HighEnergyDriftTimeOffsetMsec != 0);
                cbOffsetHighEnergySpectra.Checked = hasHighEnergyOffsets;
                foreach (var p in predictor.MeasuredDriftTimePeptides)
                {
                    if (hasHighEnergyOffsets)
                        gridMeasuredDriftTimes.Rows.Add(p.Key.Sequence,
                            p.Key.Charge.ToString(LocalizationHelper.CurrentCulture),
                            (p.Value.DriftTimeMsec(false) ?? 0).ToString(LocalizationHelper.CurrentCulture),
                            p.Value.HighEnergyDriftTimeOffsetMsec.ToString(LocalizationHelper.CurrentCulture)
                            );
                    else
                        gridMeasuredDriftTimes.Rows.Add(p.Key.Sequence,
                            p.Key.Charge.ToString(LocalizationHelper.CurrentCulture),
                            (p.Value.DriftTimeMsec(false) ?? 0).ToString(LocalizationHelper.CurrentCulture)
                            );
                }
            }
            else
            {
                cbOffsetHighEnergySpectra.Checked = false;
            }
        }

        private void UpdateControls()
        {
            var oldVisible = _showRegressions;
            _showRegressions = (comboLibrary.SelectedIndex > 0); // 0th entry is "None"
            labelConversionParameters.Enabled = gridRegression.Enabled =
                labelConversionParameters.Visible = gridRegression.Visible =
                    _showRegressions;
            gridMeasuredDriftTimes.Columns[3].Visible = cbOffsetHighEnergySpectra.Checked;

            if (oldVisible != _showRegressions)
            {
                int adjust = (gridRegression.Size.Height + 2*labelConversionParameters.Size.Height) * (_showRegressions ? 1 : -1);
                if (!labelIonMobilityLibrary.Visible) // TODO: ion mobility libraries are more complex than initially thought - put this off until after summer 2014 release
                    adjust -= (2*labelIonMobilityLibrary.Size.Height + comboLibrary.Size.Height);
                Size = new Size(Size.Width, Size.Height + adjust);
                gridMeasuredDriftTimes.Size = new Size(gridMeasuredDriftTimes.Size.Width, gridMeasuredDriftTimes.Size.Height - adjust);
                cbOffsetHighEnergySpectra.Location = new Point(cbOffsetHighEnergySpectra.Location.X, gridMeasuredDriftTimes.Location.Y + gridMeasuredDriftTimes.Size.Height + cbOffsetHighEnergySpectra.Size.Height/4);
                btnUseResults.Location = new Point(btnUseResults.Location.X, cbOffsetHighEnergySpectra.Location.Y);
                labelIonMobilityLibrary.Location = new Point(labelIonMobilityLibrary.Location.X, labelIonMobilityLibrary.Location.Y - adjust);
                comboLibrary.Location = new Point(comboLibrary.Location.X, comboLibrary.Location.Y - adjust);
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            var driftTable = new MeasuredDriftTimeTable(gridMeasuredDriftTimes);

            var table = new ChargeRegressionTable(gridRegression);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_existing.Contains(r => !ReferenceEquals(_predictor, r) && Equals(name, r.Name)))
            {
                if (MessageBox.Show(this,
                    TextUtil.LineSeparate(string.Format(Resources.EditDriftTimePredictorDlg_OkDialog_A_drift_time_predictor_with_the_name__0__already_exists_,name),
                        Resources.EditDriftTimePredictorDlg_OkDialog_Do_you_want_to_change_it_),
                    Program.Name, MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    return;
                }
            }
            if (driftTable.GetTableMeasuredDriftTimes(cbOffsetHighEnergySpectra.Checked) == null) // Some error detected in the measured drift times table
            {
                return;
            }
            if (table.GetTableChargeRegressionLines() == null) // Some error detected in the charged regression lines table
            {
                return;
            }
            double resolvingPower;
            if (!helper.ValidateDecimalTextBox(textResolvingPower, out resolvingPower))
                return;

            var errmsg = ValidateResolvingPower(resolvingPower);
            if (errmsg != null)
            {
                helper.ShowTextBoxError(textResolvingPower, errmsg);
                return;
            }

            if ((comboLibrary.SelectedIndex > 0) && (comboLibrary.SelectedItem.ToString().Length == 0))
            {
                MessageBox.Show(this, Resources.EditDriftTimePredictorDlg_OkDialog_Drift_time_prediction_requires_an_ion_mobility_library_,
                    Program.Name);
                comboLibrary.Focus();
                return;
            }
            var ionMobilityLibrary = _driverIonMobilityLibraryListComboDriver.SelectedItem;

            DriftTimePredictor predictor =
                new DriftTimePredictor(name, driftTable.GetTableMeasuredDriftTimes(cbOffsetHighEnergySpectra.Checked), 
                    ionMobilityLibrary, table.GetTableChargeRegressionLines(), resolvingPower);

            _predictor = predictor;

            DialogResult = DialogResult.OK;
        }

        public static string ValidateResolvingPower(double resolvingPower)
        {
            if (resolvingPower <= 0)
                return Resources.EditDriftTimePredictorDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_;
            return null;
        }

        private void comboIonMobilityLibrary_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_driverIonMobilityLibraryListComboDriver.SelectedIndexChangedEvent(sender, e))
                IonMobilityLibraryChanged();
        }

        private void IonMobilityLibraryChanged()
        {
            var calc = _driverIonMobilityLibraryListComboDriver.SelectedItem;
            if (calc != null)
            {
                try
                {
                    if (_predictor != null)
                        _predictor = _predictor.ChangeLibrary(calc);
                }
                catch (Exception e)
                {
                    MessageDlg.ShowException(this, e);
                }
            }
            UpdateControls();
        }


        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }


        #region Functional test support

        public void SetResolvingPower(double power)
        {
            textResolvingPower.Text = power.ToString(LocalizationHelper.CurrentCulture);
        }

        public void SetPredictorName(string name)
        {
            textName.Text = name;
        }

        public void AddIonMobilityLibrary()
        {
            CheckDisposed();
            _driverIonMobilityLibraryListComboDriver.AddItem();
        }

        public void EditIonMobilityLibraryList()
        {
            CheckDisposed();
            _driverIonMobilityLibraryListComboDriver.EditList();
        }

        public void EditCurrentIonMobilityLibrary()
        {
            CheckDisposed();
            _driverIonMobilityLibraryListComboDriver.EditCurrent();
        }

        public void ChooseIonMobilityLibrary(string name)
        {
            comboLibrary.SelectedItem = name;
            UpdateControls();
        }

        public void PasteRegressionValues()
        {
            gridRegression.DoPaste(this, ChargeRegressionTable.ValidateRegressionCellValues);
        }

        public void PasteMeasuredDriftTimes()
        {
            gridMeasuredDriftTimes.DoPaste(this, MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues);
        }

        public void SetOffsetHighEnergySpectraCheckbox(bool enable)
        {
            cbOffsetHighEnergySpectra.Checked = enable;
            UpdateControls();
        }

        public bool GetOffsetHighEnergySpectraCheckbox()
        {
            return cbOffsetHighEnergySpectra.Checked;
        }

        public void GetDriftTimesFromResults()
        {
            try
            {
                var driftTable = new MeasuredDriftTimeTable(gridMeasuredDriftTimes);
                var tempDriftTimePredictor = new DriftTimePredictor("tmp", driftTable.GetTableMeasuredDriftTimes(cbOffsetHighEnergySpectra.Checked), null, null, 30); // Not L10N
                using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.EditDriftTimePredictorDlg_btnGenerateFromDocument_Click_Finding_drift_time_values_for_peaks,
                    Message = string.Empty,
                    ProgressValue = 0
                })
                {
                    longWaitDlg.PerformWork(this, 100, broker =>
                    {
                        tempDriftTimePredictor = tempDriftTimePredictor.ChangeMeasuredDriftTimesFromResults(Program.MainWindow.Document, Program.MainWindow.DocumentFilePath, broker);
                    });
                    if (!longWaitDlg.IsCanceled && tempDriftTimePredictor != null)
                    {
                        UpdateMeasuredDriftTimesControl(tempDriftTimePredictor);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDlg.ShowException(this, ex);
            }
        }

        #endregion


        private void gridRegression_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl + V for paste
            if (e.KeyCode == Keys.V && e.Control)
            {
                PasteRegressionValues();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                gridRegression.DoDelete();
            }
        }

        private void gridMeasuredDriftTimes_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl + V for paste
            if (e.KeyCode == Keys.V && e.Control)
            {
                PasteMeasuredDriftTimes();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                gridMeasuredDriftTimes.DoDelete();
            }
        }

        private void cbOffsetHighEnergySpectra_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControls();
        }

        /// <summary>
        /// Leverage the existing code for displaying raw drift times in chromatogram graphs,
        /// to obtain plausible drift times for library use.
        /// </summary>
        private void btnGenerateFromDocument_Click(object sender, EventArgs e)
        {
            GetDriftTimesFromResults();
        }
    }

    public class MeasuredDriftTimeTable
    {
        private readonly DataGridView _gridMeasuredDriftTimePeptides;

        public MeasuredDriftTimeTable(DataGridView gridMeasuredDriftTimePeptides)
        {
            _gridMeasuredDriftTimePeptides = gridMeasuredDriftTimePeptides;
        }

        public Dictionary<LibKey, DriftTimeInfo> GetTableMeasuredDriftTimes(bool useHighEnergyOffsets)
        {
            var e = new CancelEventArgs();
            var dict = new Dictionary<LibKey, DriftTimeInfo>();
            foreach (DataGridViewRow row in _gridMeasuredDriftTimePeptides.Rows)
            {
                if (row.IsNewRow)
                    continue;

                string seq;
                if (!ValidateSequence(e, row.Cells[0], out seq))
                    return null;

                int charge;
                if (!ValidateCharge(e, row.Cells[1], out charge))
                    return null;

                double driftTime;
                if (!ValidateDriftTime(e, row.Cells[2], out driftTime))
                    return null;

                double highEnergyDriftTimeOffset = 0; // Set default value in case user does not provide one
                if (useHighEnergyOffsets && !ValidateHighEnergyDriftTimeOffset(e, row.Cells[3], out highEnergyDriftTimeOffset))
                    return null;

                try
                {
                    dict.Add(new LibKey(seq,charge), new DriftTimeInfo(driftTime, highEnergyDriftTimeOffset));
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                    // just take the first seen    
                }
            }
            return dict;
        }

        private bool ValidateCharge(CancelEventArgs e, DataGridViewCell cell, out int charge)
        {
            if (!ValidateCell(e, cell, Convert.ToInt32, out charge, true))
                return false;

            var errmsg = ValidateCharge(charge);
            if (errmsg != null)
            {
                InvalidCell(e, cell, errmsg);
                return false;
            }

            return true;
        }

        public static string ValidateCharge(int charge)
        {
            if (charge < 1 || charge > TransitionGroup.MAX_PRECURSOR_CHARGE)
                return String.Format(Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__, charge, TransitionGroup.MAX_PRECURSOR_CHARGE);
            return null;
        }

        private bool ValidateDriftTime(CancelEventArgs e, DataGridViewCell cell, out double driftTime)
        {
            if (!ValidateCell(e, cell, Convert.ToDouble, out driftTime, true))
                return false;

            // TODO: Range check.

            return true;
        }

        private bool ValidateHighEnergyDriftTimeOffset(CancelEventArgs e, DataGridViewCell cell, out double driftTimeHighEnergyOffset)
        {
            if (!ValidateCell(e, cell, Convert.ToDouble, out driftTimeHighEnergyOffset, false))
                return false;

            // TODO: Range check.

            return true;
        }

        private bool ValidateSequence(CancelEventArgs e, DataGridViewCell cell, out string sequence)
        {
            if (!ValidateCell(e, cell, Convert.ToString, out sequence, true))
                return false;

            return true;
        }

        private bool ValidateCell<TVal>(CancelEventArgs e, DataGridViewCell cell,
            Converter<string, TVal> conv, out TVal valueT, bool required)
        {
            valueT = default(TVal);
            if (cell.Value == null)
            {
                if (required)
                {
                    InvalidCell(e, cell, Resources.EditDriftTimePredictorDlg_ValidateCell_A_value_is_required_);
                    return false;
                }
                else
                {
                    return true; // Missing value is acceptable, don't change the output value
                }
            }
            string value = cell.Value.ToString();
            try
            {
                valueT = conv(value);
            }
            catch (Exception)
            {
                InvalidCell(e, cell, Resources.EditDriftTimePredictorDlg_ValidateCell_The_entry__0__is_not_valid_, value);
                return false;
            }

            return true;
        }

        private void InvalidCell(CancelEventArgs e, DataGridViewCell cell,
            string message, params object[] args)
        {
            MessageBox.Show(string.Format(message, args));
            _gridMeasuredDriftTimePeptides.Focus();
            _gridMeasuredDriftTimePeptides.ClearSelection();
            cell.Selected = true;
            _gridMeasuredDriftTimePeptides.CurrentCell = cell;
            _gridMeasuredDriftTimePeptides.BeginEdit(true);
            e.Cancel = true;
        }

        public static string ValidateMeasuredDriftTimeCellValues(string[] values)
        {
            int tempInt;
            double tempDouble;

            if (values.Count() < 3)
                return Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_pasted_text_must_have_three_columns_;

            // Parse sequence
            var sequence = values[EditDriftTimePredictorDlg.COLUMN_SEQUENCE];
            if (string.IsNullOrEmpty(sequence))
                return Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_A_modified_peptide_sequence_is_required_for_each_entry_;

            if (!FastaSequence.IsExSequence(sequence))
                return string.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, sequence);

            try
            {
                values[EditDriftTimePredictorDlg.COLUMN_SEQUENCE] = SequenceMassCalc.NormalizeModifiedSequence(sequence);
            }
            catch (Exception x)
            {
                return x.Message;
            }

            // Parse charge
            if ((!int.TryParse(values[EditDriftTimePredictorDlg.COLUMN_CHARGE].Trim(), out tempInt)) || ValidateCharge(tempInt) != null)
                return string.Format(Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                    values[EditDriftTimePredictorDlg.COLUMN_CHARGE].Trim(), TransitionGroup.MAX_PRECURSOR_CHARGE);

            // Parse drift time
            if (!double.TryParse(values[EditDriftTimePredictorDlg.COLUMN_DRIFT_TIME_MSEC].Trim(), out tempDouble))
                return string.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_value__0__is_not_a_valid_drift_time_, values[EditDriftTimePredictorDlg.COLUMN_DRIFT_TIME_MSEC].Trim());

            return null;
        }

        public static bool ValidateMeasuredDriftTimeCellValues(string[] values, IWin32Window parent, int lineNumber)
        {
            string message = ValidateMeasuredDriftTimeCellValues(values);

            if (message == null)
                return true;

            MessageDlg.Show(parent, string.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_On_line__0___1_, lineNumber, message));
            return false;
        }

    }

}
