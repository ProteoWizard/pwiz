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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditCEDlg : FormEx
    {
        private CollisionEnergyRegression _regression;
        private readonly IEnumerable<CollisionEnergyRegression> _existing;

        public EditCEDlg(IEnumerable<CollisionEnergyRegression> existing)
        {
            _existing = existing;

            InitializeComponent();

            var document = Program.ActiveDocumentUI;
            btnUseCurrent.Enabled = document.Settings.HasResults &&
                                    document.Settings.MeasuredResults.Chromatograms.Contains(
                                        chrom => chrom.OptimizationFunction is CollisionEnergyRegression);
            btnShowGraph.Enabled = btnUseCurrent.Enabled;
        }

        public string RegressionName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public CollisionEnergyRegression Regression
        {
            get { return _regression; }

            set
            {
                _regression = value;
                if (_regression == null)
                {
                    textName.Text = string.Empty;
                    gridRegression.Rows.Clear();
                    textStepSize.Text = CollisionEnergyRegression.DEFAULT_STEP_SIZE.ToString(LocalizationHelper.CurrentCulture);
                    textStepCount.Text = CollisionEnergyRegression.DEFAULT_STEP_COUNT.ToString(LocalizationHelper.CurrentCulture);
                }
                else
                {
                    textName.Text = _regression.Name;
                    gridRegression.Rows.Clear();
                    foreach (ChargeRegressionLine r in _regression.Conversions)
                    {
                        gridRegression.Rows.Add(r.Charge.ToString(LocalizationHelper.CurrentCulture),
                                                r.Slope.ToString(LocalizationHelper.CurrentCulture),
                                                r.Intercept.ToString(LocalizationHelper.CurrentCulture));
                    }
                    textStepSize.Text = _regression.StepSize.ToString(LocalizationHelper.CurrentCulture);
                    textStepCount.Text = _regression.StepCount.ToString(LocalizationHelper.CurrentCulture);
                }
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_existing.Contains(r => !ReferenceEquals(_regression, r) && Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, Resources.EditCEDlg_OkDialog_The_collision_energy_regression__0__already_exists, name);
                return;
            }

            List<ChargeRegressionLine> conversions =
                new List<ChargeRegressionLine>();

            foreach (DataGridViewRow row in gridRegression.Rows)
            {
                if (row.IsNewRow)
                    continue;

                int charge;
                if (!ValidateCharge(row.Cells[0], out charge))
                    return;

                double slope;
                if (!ValidateSlope(row.Cells[1], out slope))
                    return;

                double intercept;
                if (!ValidateIntercept(row.Cells[2], out intercept))
                    return;

                conversions.Add(new ChargeRegressionLine(charge, slope, intercept));
            }

            if (conversions.Count == 0)
            {
                MessageDlg.Show(this, Resources.EditCEDlg_OkDialog_Collision_energy_regressions_require_at_least_one_regression_function);
                return;
            }

            double stepSize;
            if (!helper.ValidateDecimalTextBox(textStepSize,
                    CollisionEnergyRegression.MIN_STEP_SIZE,
                    CollisionEnergyRegression.MAX_STEP_SIZE,
                    out stepSize))
                return;

            int stepCount;
            if (!helper.ValidateNumberTextBox(textStepCount,
                    OptimizableRegression.MIN_OPT_STEP_COUNT,
                    OptimizableRegression.MAX_OPT_STEP_COUNT,
                    out stepCount))
                return;

            _regression = new CollisionEnergyRegression(name, conversions, stepSize, stepCount);

            DialogResult = DialogResult.OK;
            Close();
        }

        private bool ValidateCharge(DataGridViewCell cell, out int charge)
        {
            if (!ValidateCell(cell, Convert.ToInt32, out charge))
                return false;

            if (0 >= charge || charge > 5)
            {
                InvalidCell(cell, Resources.EditCEDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge_Precursor_charges_must_be_between_1_and_5, charge);
                return false;
            }

            return true;
        }

        private bool ValidateSlope(DataGridViewCell cell, out double slope)
        {
            if (!ValidateCell(cell, Convert.ToDouble, out slope))
                return false;

            // TODO: Range check.

            return true;
        }

        private bool ValidateIntercept(DataGridViewCell cell, out double intercept)
        {
            if (!ValidateCell(cell, Convert.ToDouble, out intercept))
                return false;

            // TODO: Range check.

            return true;
        }

        private bool ValidateCell<TVal>(DataGridViewCell cell,
            Converter<string, TVal> conv, out TVal valueT)
        {
            valueT = default(TVal);
            if (cell.Value == null)
            {
                InvalidCell(cell, Resources.EditCEDlg_ValidateCell_A_value_is_required);
                return false;
            }
            string value = cell.Value.ToString();
            try
            {
                valueT = conv(value);
            }
            catch (Exception)
            {
                InvalidCell(cell, Resources.EditCEDlg_ValidateCell_The_entry__0__is_not_valid, value);
                return false;
            }

            return true;            
        }

        private void InvalidCell(DataGridViewCell cell,
            string message, params object[] args)
        {            
            MessageBox.Show(string.Format(message, args));
            gridRegression.Focus();
            gridRegression.ClearSelection();
            cell.Selected = true;
            gridRegression.CurrentCell = cell;
            gridRegression.BeginEdit(true);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
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

        private static bool ValidateRegressionCellValues(string[] values, IWin32Window parent, int lineNumber)
        {
            int tempInt;
            double tempDouble;
            string message;

            // Parse charge
            if (!int.TryParse(values[0].Trim(), out tempInt))
                message = string.Format(Resources.EditCEDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_charge_Charges_must_be_integer_values_between_1_and_5, values[0]);

            // Parse slope
            else if (!double.TryParse(values[1].Trim(), out tempDouble))
                message = string.Format(Resources.EditCEDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_slope, values[1]);

            // Parse intercept
            else if (!double.TryParse(values[2].Trim(), out tempDouble))
                message = string.Format(Resources.EditCEDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_intercept, values[2]);

            else
                return true;

            MessageDlg.Show(parent, string.Format(Resources.EditCEDlg_ValidateRegressionCellValues_On_line__0__1__, lineNumber, message));
            return false;
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            UseCurrentData();
        }

        public void UseCurrentData()
        {
            CERegressionData[] arrayData = GetRegressionDatas();
            if (arrayData == null)
                return;

            bool hasRegressionLines = false;
            var regressionLines = new RegressionLine[arrayData.Length];
            for (int i = 0; i < arrayData.Length; i++)
            {
                if (arrayData[i] == null)
                    continue;
                regressionLines[i] = arrayData[i].RegressionLine;
                if (regressionLines[i] != null)
                    hasRegressionLines = true;
            }

            if (!hasRegressionLines)
            {
                MessageDlg.Show(this, Resources.EditCEDlg_UseCurrentData_Insufficient_data_found_to_calculate_a_new_regression);
                return;
            }

            gridRegression.Rows.Clear();
            for (int i = 0; i < regressionLines.Length; i++)
            {
                var regressionLine = regressionLines[i];
                if (regressionLine == null)
                    continue;
                gridRegression.Rows.Add(new object[]
                                            {
                                                i.ToString(LocalizationHelper.CurrentCulture),
                                                string.Format("{0:F04}", regressionLine.Slope), // Not L10N
                                                string.Format("{0:F04}", regressionLine.Intercept) // Not L10N
                                            });
            }
        }

        private void btnShowGraph_Click(object sender, EventArgs e)
        {
            ShowGraph();
        }

        public void ShowGraph()
        {
            CheckDisposed();
            CERegressionData[] arrayData = GetRegressionDatas();
            if (arrayData == null)
                return;

            var listGraphData = new List<RegressionGraphData>();
            for (int charge = 0; charge < arrayData.Length; charge++)
            {
                var regressionData = arrayData[charge];
                if (regressionData == null)
                    continue;
                listGraphData.Add(new RegressionGraphData
                                      {
                                          Title = string.Format(Resources.EditCEDlg_ShowGraph_Collision_Energy_Regression_Charge__0__, charge),
                                          LabelX = Resources.EditCEDlg_ShowGraph_Precursor_m_z,
                                          LabelY = Resources.EditCEDlg_ShowGraph_Collision_Energy,
                                          XValues = regressionData.PrecursorMzValues,
                                          YValues = regressionData.BestValues,
                                          RegressionLine = regressionData.RegressionLine,
                                          RegressionLineCurrent = regressionData.RegressionLineSetting
                                      });
            }

            using (var dlg = new GraphRegression(listGraphData))
            {
                dlg.ShowDialog(this);
            }
        }

        private CERegressionData[] GetRegressionDatas()
        {
            var document = Program.ActiveDocumentUI;
            if (!document.Settings.HasResults)
                return null;
            if (!document.Settings.MeasuredResults.IsLoaded)
            {
                MessageBox.Show(this, Resources.EditCEDlg_GetRegressionDatas_Measured_results_must_be_completely_loaded_before_they_can_be_used_to_create_a_collision_energy_regression,
                                Program.Name);
                return null;
            }

            var regressionCurrent = _regression ??
                document.Settings.TransitionSettings.Prediction.CollisionEnergy;

            var arrayData = new CERegressionData[TransitionGroup.MAX_PRECURSOR_CHARGE + 1];
            var chromatograms = document.Settings.MeasuredResults.Chromatograms;
            for (int i = 0; i < chromatograms.Count; i++)
            {
                var chromSet = chromatograms[i];
                var regression = chromSet.OptimizationFunction as CollisionEnergyRegression;
                if (regression == null)
                    continue;

                foreach (var nodeGroup in document.MoleculeTransitionGroups)
                {
                    int charge = nodeGroup.TransitionGroup.PrecursorCharge;
                    if (arrayData[charge] == null)
                    {
                        var chargeRegression = (regressionCurrent != null ?
                            regressionCurrent.GetRegressionLine(charge) : null);
                        arrayData[charge] = new CERegressionData(chargeRegression != null ?
                            chargeRegression.RegressionLine : null);
                    }
                    arrayData[charge].Add(regression, nodeGroup, i);
                }
            }
            return arrayData;
        }

        private sealed class CERegressionData : RegressionData<CollisionEnergyRegression>
        {
            public CERegressionData(RegressionLine regressionLineSetting)
                : base(regressionLineSetting)
            {
            }

            protected override double GetValue(CollisionEnergyRegression regression,
                TransitionGroupDocNode nodeGroup, int step)
            {
                return regression.GetCollisionEnergy(nodeGroup.TransitionGroup.PrecursorCharge,
                                                      nodeGroup.PrecursorMz, step);
            }
        }

        #region Functional test support

        public double StepSize
        {
             get { return double.Parse(textStepSize.Text); }
             set { textStepSize.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public int StepCount
        {
            get { return int.Parse(textStepCount.Text); }
            set { textStepCount.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        #endregion
    }

    internal abstract class RegressionData<TReg>
        where TReg : OptimizableRegression
    {
        private readonly Dictionary<TransitionGroupDocNode, Dictionary<TReg, Dictionary<int, double>>> _dictGroupToOptTotals =
            new Dictionary<TransitionGroupDocNode, Dictionary<TReg, Dictionary<int, double>>>();

        protected RegressionData(RegressionLine regressionLineSetting)
        {
            RegressionLineSetting = regressionLineSetting;
        }

        public RegressionLine RegressionLineSetting { get; private set; }
        public RegressionLine RegressionLine
        {
            get
            {
                if (_dictGroupToOptTotals.Count < OptimizableRegression.MIN_RECALC_REGRESSION_VALUES)
                    return null;
                Statistics statCE = new Statistics(BestValues);
                Statistics statMz = new Statistics(PrecursorMzValues);
                return new RegressionLine(statCE.Slope(statMz), statCE.Intercept(statMz));
            }
        }

        public double[] PrecursorMzValues
        {
            get
            {
                return (from nodeGroup in _dictGroupToOptTotals.Keys
                        orderby nodeGroup.PrecursorMz
                        select nodeGroup.PrecursorMz).ToArray();
            }
        }

        public double[] BestValues
        {
            get
            {
                return (from dictOptTotalsPair in _dictGroupToOptTotals
                        orderby dictOptTotalsPair.Key.PrecursorMz
                        select GetBestValue(dictOptTotalsPair)).ToArray();
            }
        }

        protected abstract double GetValue(TReg regression, TransitionGroupDocNode nodeGroup, int step);

        /// <summary>
        /// Each <see cref="TransitionGroupDocNode"/> gets only one optimal value,
        /// which is taken by summing the areas for each different regression, for each step,
        /// and then choosing the step that produces the maximum area.
        /// </summary>
        private double GetBestValue(KeyValuePair<TransitionGroupDocNode, Dictionary<TReg, Dictionary<int, double>>> dictOptTotalsPair)
        {
            double maxArea = 0;
            double bestValue = 0;

            foreach (var optTotalsPair in dictOptTotalsPair.Value)
            {
                foreach (var optTotalPair in optTotalsPair.Value)
                {
                    if (maxArea < optTotalPair.Value)
                    {
                        maxArea = optTotalPair.Value;
                        bestValue = GetValue(optTotalsPair.Key, dictOptTotalsPair.Key, optTotalPair.Key);
                    }
                }
            }
            return bestValue;
        }

        public void Add(TReg regression, TransitionGroupDocNode nodeGroup, int iResult)
        {
            var result = nodeGroup.Results[iResult];
            if (result == null)
                return;

            Dictionary<TReg, Dictionary<int, double>> dictOptTotals;
            if (!_dictGroupToOptTotals.TryGetValue(nodeGroup, out dictOptTotals))
            {
                _dictGroupToOptTotals.Add(nodeGroup,
                    dictOptTotals = new Dictionary<TReg, Dictionary<int, double>>());
            }
            Dictionary<int, double> optTotals;
            if (!dictOptTotals.TryGetValue(regression, out optTotals))
            {
                dictOptTotals.Add(regression, optTotals = new Dictionary<int, double>());
            }

            foreach (var chromInfo in result)
            {
                if (chromInfo.PeakCountRatio != 1.0 || !chromInfo.Area.HasValue)
                    continue;

                int step = chromInfo.OptimizationStep;
                if (optTotals.ContainsKey(chromInfo.OptimizationStep))
                    optTotals[step] += chromInfo.Area.Value;
                else
                    optTotals.Add(step, chromInfo.Area.Value);
            }
        }
    }
}
