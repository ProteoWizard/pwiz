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
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditDPDlg : FormEx
    {
        private DeclusteringPotentialRegression _regression;
        private readonly IEnumerable<DeclusteringPotentialRegression> _existing;

        public EditDPDlg(IEnumerable<DeclusteringPotentialRegression> existing)
        {
            _existing = existing;

            InitializeComponent();

            var document = Program.ActiveDocumentUI;
            btnUseCurrent.Enabled = document.Settings.HasResults &&
                                    document.Settings.MeasuredResults.Chromatograms.Contains(
                                        chrom => chrom.OptimizationFunction is DeclusteringPotentialRegression);
            btnShowGraph.Enabled = btnUseCurrent.Enabled;
        }

        public string RegressionName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public DeclusteringPotentialRegression Regression
        {
            get { return _regression; }
            
            set
            {
                _regression = value;
                if (_regression == null)
                {
                    textName.Text = "";
                    textSlope.Text = "";
                    textIntercept.Text = "";
                    textStepSize.Text = DeclusteringPotentialRegression.DEFAULT_STEP_SIZE.ToString(CultureInfo.CurrentCulture);
                    textStepCount.Text = DeclusteringPotentialRegression.DEFAULT_STEP_COUNT.ToString(CultureInfo.CurrentCulture);
                }
                else
                {
                    textName.Text = _regression.Name;
                    textSlope.Text = _regression.Slope.ToString(CultureInfo.CurrentCulture);
                    textIntercept.Text = _regression.Intercept.ToString(CultureInfo.CurrentCulture);
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
                helper.ShowTextBoxError(textName, "The declustering potential regression '{0}' already exists.", name);
                return;
            }

            double slope;
            if (!helper.ValidateDecimalTextBox(e, textSlope, out slope))
                return;

            double intercept;
            if (!helper.ValidateDecimalTextBox(e, textIntercept, out intercept))
                return;

            double stepSize;
            if (!helper.ValidateDecimalTextBox(e, textStepSize,
                    DeclusteringPotentialRegression.MIN_STEP_SIZE,
                    DeclusteringPotentialRegression.MAX_STEP_SIZE,
                    out stepSize))
                return;

            int stepCount;
            if (!helper.ValidateNumberTextBox(e, textStepCount,
                    OptimizableRegression.MIN_OPT_STEP_COUNT,
                    OptimizableRegression.MAX_OPT_STEP_COUNT,
                    out stepCount))
                return;

            _regression = new DeclusteringPotentialRegression(name, slope, intercept, stepSize, stepCount);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            UseCurrentData();
        }

        public void UseCurrentData()
        {
            DPRegressionData regressionData = GetRegressionData();
            if (regressionData == null)
                return;

            var regressionLine = regressionData.RegressionLine;
            if (regressionLine == null)
            {
                MessageDlg.Show(this, "Insufficient data found to calculate a new regression.");
                return;
            }

            textSlope.Text = string.Format("{0:F04}", regressionLine.Slope);
            textIntercept.Text = string.Format("{0:F04}", regressionLine.Intercept);
        }

        private void btnShowGraph_Click(object sender, EventArgs e)
        {
            ShowGraph();
        }

        public void ShowGraph()
        {
            DPRegressionData regressionData = GetRegressionData();
            if (regressionData == null)
                return;
            var graphData = new RegressionGraphData
                                {
                                    Title = "Declustering Potential Regression",
                                    LabelX = "Precursor m/z",
                                    LabelY = "Declustering Potential",
                                    XValues = regressionData.PrecursorMzValues,
                                    YValues = regressionData.BestValues,
                                    RegressionLine = regressionData.RegressionLine,
                                    RegressionLineCurrent = regressionData.RegressionLineSetting
                                };
            using (var dlg = new GraphRegression(new[] { graphData }))
            {
                dlg.ShowDialog(this);
            }
        }

        private DPRegressionData GetRegressionData()
        {
            var document = Program.ActiveDocumentUI;
            if (!document.Settings.HasResults)
                return null;
            if (!document.Settings.MeasuredResults.IsLoaded)
            {
                MessageBox.Show(this, "Measured results must be completely loaded before they can be used to create a declustring potential regression.", Program.Name);
                return null;
            }

            var regressionCurrent = _regression ??
                document.Settings.TransitionSettings.Prediction.DeclusteringPotential;
            var regressionData = new DPRegressionData(regressionCurrent != null ?
                regressionCurrent.RegressionLine : null);
            var chromatograms = document.Settings.MeasuredResults.Chromatograms;
            for (int i = 0; i < chromatograms.Count; i++)
            {
                var chromSet = chromatograms[i];
                var regression = chromSet.OptimizationFunction as DeclusteringPotentialRegression;
                if (regression == null)
                    continue;

                foreach (var nodeGroup in document.TransitionGroups)
                {
                    regressionData.Add(regression, nodeGroup, i);
                }
            }
            return regressionData;
        }

        private sealed class DPRegressionData : RegressionData<DeclusteringPotentialRegression>
        {
            public DPRegressionData(RegressionLine regressionLineSetting)
                : base(regressionLineSetting)
            {
            }

            protected override double GetValue(DeclusteringPotentialRegression regression,
                TransitionGroupDocNode nodeGroup, int step)
            {
                return regression.GetDeclustringPotential(nodeGroup.PrecursorMz, step);
            }
        }
    }
}
