/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditCEDlg : Form
    {
        private CollisionEnergyRegression _regression;
        private readonly IEnumerable<CollisionEnergyRegression> _existing;
        private bool _clickedOk;

        private readonly MessageBoxHelper _helper;

        public EditCEDlg(IEnumerable<CollisionEnergyRegression> existing)
        {
            _existing = existing;

            InitializeComponent();

            _helper = new MessageBoxHelper(this);
        }

        public CollisionEnergyRegression Regression
        {
            get { return _regression; }

            set
            {
                _regression = value;
                if (_regression == null)
                {
                    textName.Text = "";
                }
                else
                {
                    textName.Text = _regression.Name;
                    foreach (ChargeRegressionLine r in _regression.Conversions)
                    {
                        gridRegression.Rows.Add(r.Charge.ToString(),
                            r.Slope.ToString(), r.Intercept.ToString());
                    }
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_clickedOk)
            {
                _clickedOk = false; // Reset in case of failure.

                string name;
                if (!_helper.ValidateNameTextBox(e, textName, out name))
                    return;

                List<ChargeRegressionLine> conversions =
                    new List<ChargeRegressionLine>();

                foreach (DataGridViewRow row in gridRegression.Rows)
                {
                    if (row.IsNewRow)
                        continue;

                    int charge;
                    if (!ValidateCharge(e, row.Cells[0], out charge))
                        return;

                    double slope;
                    if (!ValidateSlope(e, row.Cells[1], out slope))
                        return;

                    double intercept;
                    if (!ValidateIntercept(e, row.Cells[2], out intercept))
                        return;

                    conversions.Add(new ChargeRegressionLine(charge, slope, intercept));
                }

                CollisionEnergyRegression regression =
                    new CollisionEnergyRegression(name, conversions);

                if (_regression == null && _existing.Contains(regression))
                {
                    _helper.ShowTextBoxError(textName, "The collision energy regression '{0}' already exists.", name);
                    e.Cancel = true;
                    return;
                }

                _regression = regression;
            }

            base.OnClosing(e);
        }

        private bool ValidateCharge(CancelEventArgs e, DataGridViewCell cell, out int charge)
        {
            if (!ValidateCell(e, cell, Convert.ToInt32, out charge))
                return false;

            if (0 >= charge || charge > 5)
            {
                InvalidCell(e, cell,
                    "The entry '{0}' is not a valid charge. Precursor charges must be between 1 and 5.",
                    charge);
                return false;
            }

            return true;
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

        private bool ValidateCell<T>(CancelEventArgs e, DataGridViewCell cell,
            Converter<string, T> conv, out T valueT)
        {
            valueT = default(T);
            string value = cell.Value.ToString();
            try
            {
                valueT = conv(value);
            }
            catch (Exception)
            {
                InvalidCell(e, cell, "The entry {0} is not valid.", value);
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

        private void btnOk_Click(object sender, EventArgs e)
        {
            _clickedOk = true;
        }

        private void gridRegression_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl + V for paste
            if (e.KeyCode == Keys.V && e.Control)
            {
                gridRegression.DoPaste(this, ValidateRegressionCellValues);
            }
            else if (e.KeyCode == Keys.Delete)
            {
                gridRegression.DoDelete();
            }
        }

        private bool ValidateRegressionCellValues(string[] values)
        {
            try
            {
                // Parse charge
                int.Parse(values[0].Trim());
            }
            catch (FormatException)
            {
                string message = string.Format("The value {0} is not a valid charge.  " +
                    "Charges must be integer values between 1 and 5.", values[0]);
                MessageBox.Show(this, message, Program.Name);
                return false;
            }

            try
            {
                // Parse slope
                double.Parse(values[1].Trim());
            }
            catch (FormatException)
            {
                string message = string.Format("The value {0} is not a valid slope.", values[1]);
                MessageBox.Show(this, message, Program.Name);
                return false;
            }

            try
            {
                // Parse intercept
                double.Parse(values[2].Trim());
            }
            catch (FormatException)
            {
                string message = string.Format("The value {0} is not a valid intercept.", values[2]);
                MessageBox.Show(this, message, Program.Name);
                return false;
            }
            return true;
        }
    }
}
