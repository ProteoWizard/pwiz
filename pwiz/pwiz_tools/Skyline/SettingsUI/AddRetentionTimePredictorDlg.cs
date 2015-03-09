/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class AddRetentionTimePredictorDlg : FormEx
    {
        public RetentionTimeRegression Regression { get; private set; }
        public RCalcIrt Calculator { get; private set; }

        public string PredictorName 
        {
            get { return txtName.Text; }
            set { txtName.Text = value; }
        }

        public double? PredictorWindow
        {
            get
            {
                double window;
                if (Double.TryParse(txtWindow.Text, out window))
                {
                    return window;
                }
                return null;
            }
            set { txtWindow.Text = value.ToString(); }
        }

        public AddRetentionTimePredictorDlg(string libName, string libPath)
        {
            InitializeComponent();

            txtName.Text = libName;
            Calculator = new RCalcIrt(libName, libPath);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            double window;
            if (!helper.ValidateDecimalTextBox(txtWindow, out window))
                return;

            if (Settings.Default.RetentionTimeList.Any(regression => regression.Name == txtName.Text))
            {
                MessageDlg.Show(this, Resources.AddRetentionTimePredictorDlg_OkDialog_A_retention_time_predictor_with_that_name_already_exists__Please_choose_a_new_name_);
                txtName.Focus();
                return;
            }

            Regression = new RetentionTimeRegression(
                txtName.Text, Calculator, null, null, window, new List<MeasuredRetentionTime>());

            DialogResult = DialogResult.OK;
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            NoDialog();
        }

        public void NoDialog()
        {
            DialogResult = DialogResult.No;
        }

        private void btnCalculator_Click(object sender, EventArgs e)
        {
            EditCalculator();
        }

        public void EditCalculator()
        {
            using (var calcDlg = new EditIrtCalcDlg(Calculator, Settings.Default.RTScoreCalculatorList))
            {
                if (calcDlg.ShowDialog(this) == DialogResult.OK)
                {
                    Calculator = ((RCalcIrt) Calculator.ChangeName(calcDlg.CalcName))
                        .ChangeDatabasePath(calcDlg.Calculator.PersistencePath);
                    txtName.Text = Calculator.Name;
                }
            }
        }
    }
}
