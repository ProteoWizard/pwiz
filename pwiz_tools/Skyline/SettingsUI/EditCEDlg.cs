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
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditCEDlg : Form
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
                    textStepSize.Text = CollisionEnergyRegression.DEFAULT_STEP_SIZE.ToString();
                    textStepCount.Text = CollisionEnergyRegression.DEFAULT_STEP_COUNT.ToString();
                }
                else
                {
                    textName.Text = _regression.Name;
                    gridRegression.Rows.Clear();
                    foreach (ChargeRegressionLine r in _regression.Conversions)
                    {
                        gridRegression.Rows.Add(r.Charge.ToString(),
                            r.Slope.ToString(), r.Intercept.ToString());
                    }
                    textStepSize.Text = _regression.StepSize.ToString();
                    textStepCount.Text = _regression.StepCount.ToString();
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

            if (_regression == null && _existing.Contains(r => Equals(name, r.Name)))
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
                gridRegression.Rows.Add(new[]
                                            {
                                                i.ToString(),
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

            var dlg = new GraphRegression(listGraphData);
            dlg.ShowDialog(this);
        }

        private CERegressionData[] GetRegressionDatas()
        {
            var document = Program.ActiveDocumentUI;
            if (!document.Settings.HasResults)
                return null;
            if (!document.Settings.MeasuredResults.IsLoaded)
            {
                MessageBox.Show(this, "Measured results must be completely loaded before they can be used to create a retention time regression.", Program.Name);
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

    internal abstract class RegressionData<T>
        where T : OptimizableRegression
    {
        private readonly List<double> _bestValues = new List<double>();
        private readonly List<double> _precursorMzValues = new List<double>();

        protected RegressionData(RegressionLine regressionLineSetting)
        {
            RegressionLineSetting = regressionLineSetting;
        }

        public RegressionLine RegressionLineSetting { get; private set; }
        public RegressionLine RegressionLine
        {
            get
            {
                if (_bestValues.Count < OptimizableRegression.MIN_RECALC_REGRESSION_VALUES)
                    return null;
                Statistics statCE = new Statistics(_bestValues);
                Statistics statMz = new Statistics(_precursorMzValues);
                return new RegressionLine(statCE.Slope(statMz), statCE.Intercept(statMz));
            }
        }

        public double[] BestValues { get { return _bestValues.ToArray(); } }
        public double[] PrecursorMzValues { get { return _precursorMzValues.ToArray(); } }

        public void Add(T regression, TransitionGroupDocNode nodeGroup, int iResult)
        {
            var chromInfo = GetMaxChromInfo(nodeGroup.Results[iResult]);
            if (chromInfo == null)
                return;

            _precursorMzValues.Add(nodeGroup.PrecursorMz);
            _bestValues.Add(GetValue(regression, nodeGroup, chromInfo.OptimizationStep));
        }

        protected abstract double GetValue(T regression, TransitionGroupDocNode nodeGroup, int step);

        private static TransitionGroupChromInfo GetMaxChromInfo(IEnumerable<TransitionGroupChromInfo> result)
        {
            if (result == null)
                return null;

            double maxArea = 0;
            TransitionGroupChromInfo maxChromInfo = null;
            foreach (var chromInfo in result)
            {
                if (chromInfo.PeakCountRatio == 1.0 && chromInfo.Area.HasValue && maxArea < chromInfo.Area)
                {
                    maxArea = chromInfo.Area.Value;
                    maxChromInfo = chromInfo;
                }
            }
            return maxChromInfo;
        }
    }
}
