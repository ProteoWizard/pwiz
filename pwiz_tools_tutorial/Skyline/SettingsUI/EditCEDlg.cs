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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
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
                    textName.Text = "";
                    gridRegression.Rows.Clear();
                    textStepSize.Text = CollisionEnergyRegression.DEFAULT_STEP_SIZE.ToString(CultureInfo.CurrentCulture);
                    textStepCount.Text = CollisionEnergyRegression.DEFAULT_STEP_COUNT.ToString(CultureInfo.CurrentCulture);
                }
                else
                {
                    textName.Text = _regression.Name;
                    gridRegression.Rows.Clear();
                    foreach (ChargeRegressionLine r in _regression.Conversions)
                    {
                        gridRegression.Rows.Add(r.Charge.ToString(CultureInfo.CurrentCulture),
                                                r.Slope.ToString(CultureInfo.CurrentCulture),
                                                r.Intercept.ToString(CultureInfo.CurrentCulture));
                    }
                    textStepSize.Text = _regression.StepSize.ToString(CultureInfo.CurrentCulture);
                    textStepCount.Text = _regression.StepCount.ToString(CultureInfo.CurrentCulture);
                }
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

            if (_existing.Contains(r => !ReferenceEquals(_regression, r) && Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, "The collision energy regression '{0}' already exists.", name);
                return;
            }

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

            if (conversions.Count == 0)
            {
                MessageDlg.Show(this, "Collision energy regressions require at least one regression function.");
                return;
            }

            double stepSize;
            if (!helper.ValidateDecimalTextBox(e, textStepSize,
                    CollisionEnergyRegression.MIN_STEP_SIZE,
                    CollisionEnergyRegression.MAX_STEP_SIZE,
                    out stepSize))
                return;

            int stepCount;
            if (!helper.ValidateNumberTextBox(e, textStepCount,
                    OptimizableRegression.MIN_OPT_STEP_COUNT,
                    OptimizableRegression.MAX_OPT_STEP_COUNT,
                    out stepCount))
                return;

            _regression = new CollisionEnergyRegression(name, conversions, stepSize, stepCount);

            DialogResult = DialogResult.OK;
            Close();
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

        private bool ValidateCell<TVal>(CancelEventArgs e, DataGridViewCell cell,
            Converter<string, TVal> conv, out TVal valueT)
        {
            valueT = default(TVal);
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

        private bool ValidateRegressionCellValues(string[] values, int lineNumber)
        {
            int tempInt;
            double tempDouble;
            string message;

            // Parse charge
            if (!int.TryParse(values[0].Trim(), out tempInt))
                message = string.Format("the value {0} is not a valid charge.  " +
                    "Charges must be integer values between 1 and 5.", values[0]);

            // Parse slope
            else if (!double.TryParse(values[1].Trim(), out tempDouble))
                message = string.Format("the value {0} is not a valid slope.", values[1]);

            // Parse intercept
            else if (!double.TryParse(values[2].Trim(), out tempDouble))
                message = string.Format("the value {0} is not a valid intercept.", values[2]);

            else
                return true;

            MessageDlg.Show(this, string.Format("On line {0}, {1}", lineNumber, message));
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
                MessageDlg.Show(this, "Insufficient data found to calculate a new regression.");
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
                                                i.ToString(CultureInfo.CurrentCulture),
                                                string.Format("{0:F04}", regressionLine.Slope),
                                                string.Format("{0:F04}", regressionLine.Intercept)
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
                                          Title = string.Format("Collision Energy Regression Charge {0}", charge),
                                          LabelX = "Precursor m/z",
                                          LabelY = "Collision Energy",
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
                MessageBox.Show(this, "Measured results must be completely loaded before they can be used to create a collision energy regression.", Program.Name);
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

                foreach (var nodeGroup in document.TransitionGroups)
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
