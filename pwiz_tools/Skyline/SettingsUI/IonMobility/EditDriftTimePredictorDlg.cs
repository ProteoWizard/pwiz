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

        public EditDriftTimePredictorDlg(IEnumerable<DriftTimePredictor> existing)
        {
            _existing = existing;

            InitializeComponent();

            Icon = Resources.Skyline;

            _driverIonMobilityLibraryListComboDriver = new SettingsListComboDriver<IonMobilityLibrarySpec>(comboLibrary, Settings.Default.IonMobilityLibraryList);
            _driverIonMobilityLibraryListComboDriver.LoadList(null);

        }

        public DriftTimePredictor Predictor
        {
            get { return _predictor; }

            set
            {
                _predictor = value;
                gridRegression.Rows.Clear();
                if (_predictor == null)
                {
                    textName.Text = string.Empty;
                }
                else
                {
                    textName.Text = _predictor.Name;

                    comboLibrary.SelectedItem = _predictor.IonMobilityLibrary.Name;
                    // Reduce the sparse indexed-by-charge list to only non-empty members for display
                    foreach (ChargeRegressionLine r in _predictor.ChargeRegressionLines.Where(chargeRegressionLine => chargeRegressionLine != null))
                    {
                        gridRegression.Rows.Add(r.Charge.ToString(LocalizationHelper.CurrentCulture),
                                                r.Slope.ToString(LocalizationHelper.CurrentCulture),
                                                r.Intercept.ToString(LocalizationHelper.CurrentCulture));
                    }
                    textResolvingPower.Text = string.Format("{0:F04}", _predictor.ResolvingPower); // Not L10N
                }
            }
        }

        public void OkDialog()
        {
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            if (_existing.Contains(r => !ReferenceEquals(_predictor, r) && Equals(name, r.Name)))
            {
                if (MessageBox.Show(this,
                    TextUtil.LineSeparate(string.Format(Resources.EditDriftTimePredictorDlg_OkDialog_A_drift_time_predictor_with_the_name__0__already_exists_, name),
                    Resources.EditDriftTimePredictorDlg_OkDialog_Do_you_want_to_change_it_),
                    Program.Name, MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            if (GetTableChargeRegressionLines() == null) // Some error detected in the charged regression lines table
            {
                e.Cancel = true;
                return;
            }
            double resolvingPower;
            if (!helper.ValidateDecimalTextBox(e, textResolvingPower, out resolvingPower))
                return;

            var errmsg = ValidateResolvingPower(resolvingPower);
            if (errmsg != null)
            {
                helper.ShowTextBoxError(textResolvingPower, errmsg);
                return;
            }

            if ((comboLibrary.SelectedIndex != 0) && (comboLibrary.SelectedItem.ToString().Length == 0))
            {
                MessageBox.Show(this, Resources.EditDriftTimePredictorDlg_OkDialog_Drift_time_prediction_requires_an_ion_mobility_library_,
                                Program.Name);
                comboLibrary.Focus();
                return;
            }
            var ionMobilityLibrary = _driverIonMobilityLibraryListComboDriver.SelectedItem;

            DriftTimePredictor predictor =
                new DriftTimePredictor(name, ionMobilityLibrary, GetTableChargeRegressionLines(), resolvingPower);

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
                    MessageDlg.Show(this, e.Message);
                }
            }
        }

        public IList<ChargeRegressionLine> GetTableChargeRegressionLines()
        {
            var e = new CancelEventArgs();
            var dict = new Dictionary<int,ChargeRegressionLine>();
            foreach (DataGridViewRow row in gridRegression.Rows)
            {
                if (row.IsNewRow)
                    continue;

                int charge;
                if (!ValidateCharge(e, row.Cells[0], out charge))
                    return null;

                double slope;
                if (!ValidateSlope(e, row.Cells[1], out slope))
                    return null;

                double intercept;
                if (!ValidateIntercept(e, row.Cells[2], out intercept))
                    return null;

                try
                {
                    dict.Add(charge, new ChargeRegressionLine(charge, slope, intercept));
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                    // just take the first seen    
                }
            }
            return dict.Values.ToList(); 
        }

        public IEnumerable<LibKey> GetDocumentPeptides()
        {
            var document = Program.ActiveDocumentUI;
            if (!document.Settings.HasResults)
                yield break; // This shouldn't be possible, but just to be safe.
            if (!document.Settings.MeasuredResults.IsLoaded)
                yield break;

            var setPeps = new HashSet<LibKey>();
            foreach (var nodePep in document.Peptides)
            {
                string modSeq = document.Settings.GetModifiedSequence(nodePep);
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    // If a document contains the same peptide+chargestate twice, make sure it
                    // only gets added once.
                    var chargedPep = new LibKey(modSeq, nodeGroup.TransitionGroup.PrecursorCharge);
                    if (setPeps.Contains(chargedPep))
                        continue;
                    setPeps.Add(chargedPep);

                    if (nodePep.AveragePeakCountRatio < 0.5)
                        continue;

                    double? retentionTime = nodePep.SchedulingTime;
                    if (!retentionTime.HasValue)
                        continue;

                    yield return chargedPep;
                }
            }
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
        }

        public void PasteRegressionValues()
        {
            gridRegression.DoPaste(this, ValidateRegressionCellValues);
        }

        #endregion

        private bool ValidateCharge(CancelEventArgs e, DataGridViewCell cell, out int charge)
        {
            if (!ValidateCell(e, cell, Convert.ToInt32, out charge))
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

        private bool ValidateSlope(CancelEventArgs e, DataGridViewCell cell, out double slope)
        {
            if (!ValidateCell(e, cell, Convert.ToDouble, out slope))
                return false;

            // TODO: Range check.

            return true;
        }

        private bool ValidateIntercept(CancelEventArgs e, DataGridViewCell cell, out double intercept)
        {
            if (!ValidateCell(e, cell, Convert.ToDouble, out intercept))
                return false;

            // TODO: Range check.

            return true;
        }

        private bool ValidateCell<TVal>(CancelEventArgs e, DataGridViewCell cell,
            Converter<string, TVal> conv, out TVal valueT)
        {
            valueT = default(TVal);
            if (cell.Value == null)
            {
                InvalidCell(e, cell, Resources.EditDriftTimePredictorDlg_ValidateCell_A_value_is_required_);
                return false;
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
            gridRegression.Focus();
            gridRegression.ClearSelection();
            cell.Selected = true;
            gridRegression.CurrentCell = cell;
            gridRegression.BeginEdit(true);
            e.Cancel = true;
        }

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

        public static string ValidateRegressionCellValues(string[] values)
        {
            int tempInt;
            double tempDouble;

            // Parse charge
            if ((!int.TryParse(values[0].Trim(), out tempInt)) || ValidateCharge(tempInt) != null)
                return string.Format(Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_charge__Charges_must_be_integer_values_between_1_and__1__, values[0], TransitionGroup.MAX_PRECURSOR_CHARGE);

            // Parse slope
            if (!double.TryParse(values[1].Trim(), out tempDouble))
                return string.Format(Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_slope_, values[1]);

            // Parse intercept
            if (!double.TryParse(values[2].Trim(), out tempDouble))
                return string.Format(Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_intercept_, values[2]);

            return null;
        }

        private static bool ValidateRegressionCellValues(string[] values, IWin32Window parent, int lineNumber)
        {
            string message = ValidateRegressionCellValues(values);

            if (message == null)
                return true;

            MessageDlg.Show(parent, string.Format(Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_On_line__0___1_, lineNumber, message));
            return false;
        }

    }

}
