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
using pwiz.Skyline.Model.DocSettings;
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
            var table = new ChargeRegressionTable(gridRegression);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            if (_existing.Contains(r => !ReferenceEquals(_predictor, r) && Equals(name, r.Name)))
            {
                if (MessageBox.Show(this,
                    TextUtil.LineSeparate(string.Format(Resources.EditDriftTimePredictorDlg_OkDialog_A_drift_time_predictor_with_the_name__0__already_exists_,name),
                        Resources.EditDriftTimePredictorDlg_OkDialog_Do_you_want_to_change_it_),
                    Program.Name, MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            if (table.GetTableChargeRegressionLines() == null) // Some error detected in the charged regression lines table
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
                new DriftTimePredictor(name, ionMobilityLibrary, table.GetTableChargeRegressionLines(), resolvingPower);

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
            gridRegression.DoPaste(this, ChargeRegressionTable.ValidateRegressionCellValues);
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
    }
}
