/*
 * Original author: Bian Pratt <bspratt .at. proteinms.net>,
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    public class ChargeRegressionTable
    {
        private readonly DataGridView gridRegression;

        public ChargeRegressionTable(DataGridView gridRegression)
        {
            this.gridRegression = gridRegression;
        }

        public List<ChargeRegressionLine> GetTableChargeRegressionLines()
        {
            var e = new CancelEventArgs();
            var dict = new Dictionary<int, ChargeRegressionLine>();
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

        public static bool ValidateRegressionCellValues(string[] values, IWin32Window parent, int lineNumber)
        {
            string message = ValidateRegressionCellValues(values);

            if (message == null)
                return true;

            MessageDlg.Show(parent, string.Format(Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_On_line__0___1_, lineNumber, message));
            return false;
        }

    }
}