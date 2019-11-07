/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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
using pwiz.Common.Chemistry;
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

        private IonMobilityPredictor _predictor;
        private readonly IEnumerable<IonMobilityPredictor> _existing;
        private bool _showRegressions;
        private readonly bool _smallMoleculeUI; // Set true if document is non empty and not purely peptides

        public const int COLUMN_SEQUENCE = 0;
        public const int COLUMN_CHARGE = 1;
        public const int COLUMN_ION_MOBILITY = 2;
        public const int COLUMN_CCS = 3;
        public const int COLUMN_HIGH_ENERGY_OFFSET = 4;


        public EditDriftTimePredictorDlg(IEnumerable<IonMobilityPredictor> existing)
        {
            _existing = existing;
            _showRegressions = true;

            InitializeComponent();
            foreach (eIonMobilityUnits units in Enum.GetValues(typeof(eIonMobilityUnits)))
            {
                if (units != eIonMobilityUnits.none) // Don't present "none" as an option
                {
                    comboBoxIonMobilityUnits.Items.Add(IonMobilityFilter.IonMobilityUnitsL10NString(units));
                }
            }

            _smallMoleculeUI = Program.MainWindow.Document.HasSmallMolecules || Program.MainWindow.ModeUI != SrmDocument.DOCUMENT_TYPE.proteomic;
            if (_smallMoleculeUI)
            {
                gridMeasuredDriftTimes.Columns[COLUMN_SEQUENCE].HeaderText = Resources.EditDriftTimePredictorDlg_EditDriftTimePredictorDlg_Molecule;
                gridMeasuredDriftTimes.Columns[COLUMN_CHARGE].HeaderText = Resources.EditDriftTimePredictorDlg_EditDriftTimePredictorDlg_Adduct;
            }

            var targetResolver = TargetResolver.MakeTargetResolver(Program.ActiveDocumentUI);
            MeasuredDriftTimeSequence.TargetResolver = targetResolver;

            // TODO: ion mobility libraries are more complex than initially thought - leave these conversions to the mass spec vendors for now
            labelIonMobilityLibrary.Visible = comboLibrary.Visible = false;
 
            Icon = Resources.Skyline;

            _driverIonMobilityLibraryListComboDriver = new SettingsListComboDriver<IonMobilityLibrarySpec>(comboLibrary, Settings.Default.IonMobilityLibraryList);
            _driverIonMobilityLibraryListComboDriver.LoadList(null);
            UpdateControls();
        }

        public IonMobilityPredictor Predictor
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
                    textResolvingPower.Text = string.Format(@"{0:F04}", _predictor.WindowWidthCalculator.ResolvingPower);
                    textWidthAtDt0.Text = string.Format(@"{0:F04}", _predictor.WindowWidthCalculator.PeakWidthAtIonMobilityValueZero);
                    textWidthAtDtMax.Text = string.Format(@"{0:F04}", _predictor.WindowWidthCalculator.PeakWidthAtIonMobilityValueMax);
                    cbLinear.Checked = _predictor.WindowWidthCalculator.PeakWidthMode == IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.linear_range;
                }
                UpdateControls();
            }
        }

        private eIonMobilityUnits Units
        {
            set { comboBoxIonMobilityUnits.SelectedIndex = (int)value - 1; } // We don't present "none" as an option
            get { return (eIonMobilityUnits)comboBoxIonMobilityUnits.SelectedIndex + 1; }
        }

        private void UpdateMeasuredDriftTimesControl(IonMobilityPredictor predictor)
        {

            // List any measured ion mobility values, taking care to show only those matching display units
            gridMeasuredDriftTimes.Rows.Clear();
            if (predictor == null)
            {
                return;
            }
            _predictor = (_predictor??IonMobilityPredictor.EMPTY).ChangeMeasuredIonMobilityValues(predictor.MeasuredMobilityIons);
            Units = _predictor.GetIonMobilityUnits();
            var units = Units;

            if (predictor.MeasuredMobilityIons != null)
            {
                bool hasHighEnergyOffsets =
                    predictor.MeasuredMobilityIons.Any(p => p.Value.HighEnergyIonMobilityValueOffset != 0);
                cbOffsetHighEnergySpectra.Checked = hasHighEnergyOffsets;
                foreach (var p in predictor.MeasuredMobilityIons)
                {
                    var ccs = p.Value.CollisionalCrossSectionSqA.HasValue
                        ? string.Format(@"{0:F04}",p.Value.CollisionalCrossSectionSqA.Value)
                        : string.Empty;
                    var im = p.Value.IonMobility.Units == units ? p.Value.IonMobility.Mobility : null;
                    var imOffset = p.Value.IonMobility.Units == units ? p.Value.HighEnergyIonMobilityValueOffset : 0;
                    var chargeOrAdductString = _smallMoleculeUI
                                ? p.Key.Adduct.AdductFormula
                                : p.Key.Charge.ToString(LocalizationHelper.CurrentCulture);
                    if (hasHighEnergyOffsets)
                        gridMeasuredDriftTimes.Rows.Add(p.Key.Target,
                            chargeOrAdductString,
                            im.HasValue ? im.Value.ToString(LocalizationHelper.CurrentCulture) : string.Empty,
                            ccs,
                            imOffset.ToString(LocalizationHelper.CurrentCulture)
                            );
                    else
                        gridMeasuredDriftTimes.Rows.Add(p.Key.Target,
                            chargeOrAdductString,
                            im.HasValue ? im.Value.ToString(LocalizationHelper.CurrentCulture) : string.Empty,
                            ccs
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
            // Linear peak width vs Resolving Power
            label3.Visible = label3.Enabled = !cbLinear.Checked;
            textResolvingPower.Visible = textResolvingPower.Enabled = !cbLinear.Checked;
            labelWidthDtZero.Visible = labelWidthDtZero.Enabled = cbLinear.Checked;
            labelWidthDtMax.Visible = labelWidthDtMax.Enabled = cbLinear.Checked;
            labelWidthDtZeroUnits.Visible = labelWidthDtZeroUnits.Enabled = cbLinear.Checked;
            labelWidthDtMaxUnits.Visible = labelWidthDtMaxUnits.Enabled = cbLinear.Checked;
            textWidthAtDt0.Visible = textWidthAtDt0.Enabled = cbLinear.Checked;
            textWidthAtDtMax.Visible = textWidthAtDtMax.Enabled = cbLinear.Checked;
            if (labelWidthDtZero.Location.X > label3.Location.X)
            {
                var dX = labelWidthDtZero.Location.X - label3.Location.X;
                labelWidthDtZero.Location = new Point(labelWidthDtZero.Location.X - dX, labelWidthDtZero.Location.Y);
                labelWidthDtMax.Location = new Point(labelWidthDtMax.Location.X - dX, labelWidthDtMax.Location.Y);
                labelWidthDtZeroUnits.Location = new Point(labelWidthDtZeroUnits.Location.X - dX, labelWidthDtZeroUnits.Location.Y);
                labelWidthDtMaxUnits.Location = new Point(labelWidthDtMaxUnits.Location.X - dX, labelWidthDtMaxUnits.Location.Y);
                textWidthAtDt0.Location = new Point(textWidthAtDt0.Location.X - dX, textWidthAtDt0.Location.Y);
                textWidthAtDtMax.Location = new Point(textWidthAtDtMax.Location.X - dX, textWidthAtDtMax.Location.Y);
            }

            var oldVisible = _showRegressions;
            _showRegressions = (comboLibrary.SelectedIndex > 0); // 0th entry is "None"
            labelConversionParameters.Enabled = gridRegression.Enabled =
                labelConversionParameters.Visible = gridRegression.Visible =
                    _showRegressions;
            gridMeasuredDriftTimes.Columns[COLUMN_HIGH_ENERGY_OFFSET].Visible = cbOffsetHighEnergySpectra.Checked;

            if (oldVisible != _showRegressions)
            {
                int adjust = (gridRegression.Size.Height + 2*labelConversionParameters.Size.Height) * (_showRegressions ? 1 : -1);
                if (!labelIonMobilityLibrary.Visible) // TODO: ion mobility libraries are more complex than initially thought - leave these conversions to the mass spec vendors for now
                    adjust -= (2*labelIonMobilityLibrary.Size.Height + comboLibrary.Size.Height);
                Size = new Size(Size.Width, Size.Height + adjust);
                gridMeasuredDriftTimes.Size = new Size(gridMeasuredDriftTimes.Size.Width, gridMeasuredDriftTimes.Size.Height - adjust);
                cbOffsetHighEnergySpectra.Location = new Point(cbOffsetHighEnergySpectra.Location.X, gridMeasuredDriftTimes.Location.Y + gridMeasuredDriftTimes.Size.Height + cbOffsetHighEnergySpectra.Size.Height/4);
                btnUseResults.Location = new Point(btnUseResults.Location.X, cbOffsetHighEnergySpectra.Location.Y);
                labelIonMobilityLibrary.Location = new Point(labelIonMobilityLibrary.Location.X, labelIonMobilityLibrary.Location.Y - adjust);
                comboLibrary.Location = new Point(comboLibrary.Location.X, comboLibrary.Location.Y - adjust);
            }
        }

        public void OkDialog(bool forceOverwrite = false)
        {
            var helper = new MessageBoxHelper(this);

            var driftTable = new MeasuredDriftTimeTable(gridMeasuredDriftTimes, MeasuredDriftTimeSequence.TargetResolver);

            var table = new ChargeRegressionTable(gridRegression);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_existing.Contains(r => !Equals(_predictor, r) && Equals(name, r.Name)) && !forceOverwrite)
            {
                if (MessageBox.Show(this,
                    TextUtil.LineSeparate(string.Format(Resources.EditDriftTimePredictorDlg_OkDialog_An_ion_mobility_predictor_with_the_name__0__already_exists_,name),
                        Resources.EditDriftTimePredictorDlg_OkDialog_Do_you_want_to_change_it_),
                    Program.Name, MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    return;
                }
            }
            if (driftTable.GetTableMeasuredIonMobility(cbOffsetHighEnergySpectra.Checked, Units) == null) // Some error detected in the measured drift times table
            {
                return;
            }
            if (table.GetTableChargeRegressionLines() == null) // Some error detected in the charged regression lines table
            {
                return;
            }
            double resolvingPower = 0;
            double widthAtDt0 = 0;
            double widthAtDtMax = 0;
            IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType peakWidthType;
            if (cbLinear.Checked)
            {
                if (!helper.ValidateDecimalTextBox(textWidthAtDt0, out widthAtDt0))
                    return;
                if (!helper.ValidateDecimalTextBox(textWidthAtDtMax, out widthAtDtMax))
                    return;
                var errmsg = ValidateWidth(widthAtDt0);
                if (errmsg != null)
                {
                    helper.ShowTextBoxError(textWidthAtDt0, errmsg);
                    return;
                }
                errmsg = ValidateWidth(widthAtDtMax);
                if (errmsg != null)
                {
                    helper.ShowTextBoxError(textWidthAtDtMax, errmsg);
                    return;
                }
                peakWidthType = IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.linear_range;
            }
            else
            {
                if (!helper.ValidateDecimalTextBox(textResolvingPower, out resolvingPower))
                    return;

                var errmsg = ValidateResolvingPower(resolvingPower);
                if (errmsg != null)
                {
                    helper.ShowTextBoxError(textResolvingPower, errmsg);
                    return;
                }
                peakWidthType = IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power;
            }

            if ((comboLibrary.SelectedIndex > 0) && (comboLibrary.SelectedItem.ToString().Length == 0))
            {
                MessageBox.Show(this, Resources.EditDriftTimePredictorDlg_OkDialog_Drift_time_prediction_requires_an_ion_mobility_library_,
                    Program.Name);
                comboLibrary.Focus();
                return;
            }
            var ionMobilityLibrary = _driverIonMobilityLibraryListComboDriver.SelectedItem;

            IonMobilityPredictor predictor =
                new IonMobilityPredictor(name, driftTable.GetTableMeasuredIonMobility(cbOffsetHighEnergySpectra.Checked, Units), 
                    ionMobilityLibrary, table.GetTableChargeRegressionLines(), peakWidthType, resolvingPower, widthAtDt0, widthAtDtMax);

            _predictor = predictor;

            DialogResult = DialogResult.OK;
        }

        public static string ValidateWidth(double width)
        {
            if (width <= 0)
                return Resources.DriftTimeWindowWidthCalculator_Validate_Peak_width_must_be_non_negative_;
            return null;
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

        public void SetIonMobilityUnits(eIonMobilityUnits units)
        {
            Units = units;
        }
        public void SetResolvingPower(double power)
        {
            textResolvingPower.Text = power.ToString(LocalizationHelper.CurrentCulture);
        }

        public void SetWidthAtDtZero(double width)
        {
            textWidthAtDt0.Text = width.ToString(LocalizationHelper.CurrentCulture);
            UpdateControls();
        }

        public void SetWidthAtDtMax(double width)
        {
            textWidthAtDtMax.Text = width.ToString(LocalizationHelper.CurrentCulture);
            UpdateControls();
        }

        public void SetLinearRangeCheckboxState(bool checkedState)
        {
            cbLinear.Checked = checkedState;
            UpdateControls();
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
                var driftTable = new MeasuredDriftTimeTable(gridMeasuredDriftTimes, MeasuredDriftTimeSequence.TargetResolver);
                bool useHighEnergyOffset = cbOffsetHighEnergySpectra.Checked;
                var tempDriftTimePredictor = new IonMobilityPredictor(@"tmp", driftTable.GetTableMeasuredIonMobility(useHighEnergyOffset, Units), null, null, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power, 30, 0, 0);
                using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.EditDriftTimePredictorDlg_GetDriftTimesFromResults_Finding_ion_mobility_values_for_peaks,
                    Message = string.Empty,
                    ProgressValue = 0
                })
                {
                    longWaitDlg.PerformWork(this, 100, broker =>
                    {
                        tempDriftTimePredictor = tempDriftTimePredictor.ChangeMeasuredIonMobilityValuesFromResults(Program.MainWindow.Document, Program.MainWindow.DocumentFilePath, useHighEnergyOffset, broker);
                    });
                    if (!longWaitDlg.IsCanceled && tempDriftTimePredictor != null)
                    {
                        // Set display units based on what we found in the data
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

        private void cbLinear_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControls();
        }
    }

    public class MeasuredDriftTimeTable
    {
        private readonly DataGridView _gridMeasuredDriftTimePeptides;
        private readonly TargetResolver _targetResolver;

        public MeasuredDriftTimeTable(DataGridView gridMeasuredDriftTimePeptides, TargetResolver targetResolver)
        {
            _gridMeasuredDriftTimePeptides = gridMeasuredDriftTimePeptides;
            _targetResolver = targetResolver;
        }

        public Dictionary<LibKey, IonMobilityAndCCS> GetTableMeasuredIonMobility(bool useHighEnergyOffsets, eIonMobilityUnits units)
        {
            var e = new CancelEventArgs();
            var dict = new Dictionary<LibKey, IonMobilityAndCCS>();
            foreach (DataGridViewRow row in _gridMeasuredDriftTimePeptides.Rows)
            {
                if (row.IsNewRow)
                    continue;

                string seq;
                if (!ValidateSequence(e, row.Cells[EditDriftTimePredictorDlg.COLUMN_SEQUENCE], out seq))
                    return null;

                // OK, we have a non-empty "sequence" string, but is that actually a peptide or a molecule?
                // See if there's anything in the document whose text representation matches what's in the list
               
                var target = _targetResolver.ResolveTarget(seq);
                if (target == null || target.IsEmpty)
                    return null;

                Adduct charge;
                if (!ValidateCharge(e, row.Cells[EditDriftTimePredictorDlg.COLUMN_CHARGE], target.IsProteomic, out charge))
                    return null;

                double mobility;
                if (!ValidateDriftTime(e, row.Cells[EditDriftTimePredictorDlg.COLUMN_ION_MOBILITY], out mobility))
                    return null;

                double? ccs;
                if (!ValidateCCS(e, row.Cells[EditDriftTimePredictorDlg.COLUMN_CCS], out ccs))
                    return null;

                double highEnergyOffset = 0; // Set default value in case user does not provide one
                if (useHighEnergyOffsets && !ValidateHighEnergyDriftTimeOffset(e, row.Cells[EditDriftTimePredictorDlg.COLUMN_HIGH_ENERGY_OFFSET], out highEnergyOffset))
                    return null;
                var ionMobility = IonMobilityValue.GetIonMobilityValue(mobility, units);
                try
                {
                    dict.Add(new LibKey(target, charge),  IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobility, ccs, highEnergyOffset));
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                    // just take the first seen    
                }
            }
            return dict;
        }

        private bool ValidateCharge(CancelEventArgs e, DataGridViewCell cell, bool assumeProteomic, out Adduct charge)
        {
            if (assumeProteomic)
            {
                if (!ValidateCell(e, cell, Adduct.FromStringAssumeProtonated, out charge, true))
                {
                    return false;
                }
            }
            else
            {
                if (!ValidateCell(e, cell, Adduct.FromStringAssumeProtonatedNonProteomic, out charge, true))
                {
                    return false;
                }
            }

            var errmsg = ValidateCharge(charge);
            if (errmsg != null)
            {
                InvalidCell(e, cell, errmsg);
                return false;
            }

            return true;
        }

        public static string ValidateCharge(Adduct adduct)
        {
            if (adduct.AdductCharge == 0 || Math.Abs(adduct.AdductCharge) > TransitionGroup.MAX_PRECURSOR_CHARGE)
                return String.Format(Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__, adduct, TransitionGroup.MAX_PRECURSOR_CHARGE);
            return null;
        }

        private bool ValidateDriftTime(CancelEventArgs e, DataGridViewCell cell, out double driftTime)
        {
            if (!ValidateCell(e, cell, Convert.ToDouble, out driftTime, true))
                return false;

            return true;
        }

        private bool ValidateCCS(CancelEventArgs e, DataGridViewCell cell, out double? ccs)
        {
            double val;
            if (!ValidateCell(e, cell, Convert.ToDouble, out val, false))
            {
                ccs = null;
                return false;
            }
            ccs = val;

            return true;
        }

        private bool ValidateHighEnergyDriftTimeOffset(CancelEventArgs e, DataGridViewCell cell, out double driftTimeHighEnergyOffset)
        {
            if (!ValidateCell(e, cell, Convert.ToDouble, out driftTimeHighEnergyOffset, false))
                return false;

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
                if (required || !string.IsNullOrEmpty(value))
                {
                    InvalidCell(e, cell, Resources.EditDriftTimePredictorDlg_ValidateCell_The_entry__0__is_not_valid_, value);
                    return false;
                }
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
            Adduct tempAdduct;
            double tempDouble;

            if (values.Length < 3)
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
            if ((!Adduct.TryParse(values[EditDriftTimePredictorDlg.COLUMN_CHARGE].Trim(), out tempAdduct)) || ValidateCharge(tempAdduct) != null)
                return string.Format(Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                    values[EditDriftTimePredictorDlg.COLUMN_CHARGE].Trim(), TransitionGroup.MAX_PRECURSOR_CHARGE);

            // Parse drift time
            if (!double.TryParse(values[EditDriftTimePredictorDlg.COLUMN_ION_MOBILITY].Trim(), out tempDouble))
                return string.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_value__0__is_not_a_valid_drift_time_, values[EditDriftTimePredictorDlg.COLUMN_ION_MOBILITY].Trim());

            if (values.Length > EditDriftTimePredictorDlg.COLUMN_CCS)
            {
                // Parse CCS, if any
                if (!string.IsNullOrEmpty(values[EditDriftTimePredictorDlg.COLUMN_CCS]) && 
                        !double.TryParse(values[EditDriftTimePredictorDlg.COLUMN_CCS].Trim(), out tempDouble))
                    return string.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_value__0__is_not_a_valid_collisional_cross_section_, values[EditDriftTimePredictorDlg.COLUMN_CCS].Trim());
            }
            if (values.Length > EditDriftTimePredictorDlg.COLUMN_HIGH_ENERGY_OFFSET)
            {
                // Parse high energy offset, if any
                if (!string.IsNullOrEmpty(values[EditDriftTimePredictorDlg.COLUMN_HIGH_ENERGY_OFFSET]) &&
                        !double.TryParse(values[EditDriftTimePredictorDlg.COLUMN_HIGH_ENERGY_OFFSET].Trim(), out tempDouble))
                    return string.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_value__0__is_not_a_valid_high_energy_offset_, values[EditDriftTimePredictorDlg.COLUMN_HIGH_ENERGY_OFFSET].Trim());
            }
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
